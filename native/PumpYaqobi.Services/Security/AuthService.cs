using Microsoft.EntityFrameworkCore;
using PumpYaqobi.Application.Security;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;
using PumpYaqobi.Services.Data;

namespace PumpYaqobi.Services.Security;

public enum SignInResult { Ok = 1, WrongPassword = 2, LockedOut = 3, NoSuchUser = 4 }

public sealed record SignInOutcome(SignInResult Result, UserRole Role, TimeSpan? LockedFor = null);

/// <summary>
/// ══ ورود ══════════════════════════════════════════════════════════════════
/// رمزها فقط به‌صورت PBKDF2 نگه داشته می‌شوند و هرگز بازخوانی نمی‌شوند.
///
/// جلوگیری از حدسِ پیاپی: پس از سه تلاشِ غلط، هر تلاشِ بعدی زمانِ انتظارِ
/// دوبرابر می‌گیرد (۵ث، ۱۰ث، ۲۰ث…) تا سقفِ پنج دقیقه. برخلافِ نسخهٔ HTML،
/// این شمارنده در دیتابیس است نه در حافظهٔ صفحه، پس بستنِ برنامه دورش نمی‌زند.
/// </summary>
public sealed class AuthService
{
    private const string FailKey = "auth.failCount";
    private const string UntilKey = "auth.lockedUntilUtc";
    private static readonly TimeSpan MaxLock = TimeSpan.FromMinutes(5);

    private readonly PumpDbFactory _dbf;
    private readonly UserSession _session;

    public AuthService(PumpDbFactory dbf, UserSession session)
    { _dbf = dbf; _session = session; }

    /// <summary>نخستین اجرا: یک مدیر با رمزِ داده‌شده ساخته می‌شود.</summary>
    public bool NeedsFirstRun()
    {
        using var db = _dbf.Create();
        return !db.Users.Any();
    }

    /// <summary>
    /// ══ برنامه بی رمز باز می‌شود ═══════════════════════════════════════════
    ///
    /// خواستهٔ صریحِ صاحب ریپو (۱۴۰۵/۰۷/۰۷): «برنامه بدون رمز باشه، چون
    /// کسایی که تازه به برنامه می‌رسن نباید رمز داشته باشه و خود طرف برای
    /// خودش رمز خودشو می‌زنه.»
    ///
    /// پس نصبِ تازه <b>هیچ صفحهٔ قفلی</b> ندارد: مدیرِ بی‌رمز همان لحظه
    /// ساخته می‌شود و برنامه باز است. هر وقت خودِ کاربر در
    /// «تنظیمات ← رمزها و کد» رمزی گذاشت، از آن به بعد صفحهٔ قفل می‌آید.
    ///
    /// ⛔ <b>هشِ خالی یعنی «رمزی نیست»، نه «رمزی که خالی است».</b>
    /// <see cref="PasswordHasher.Verify"/> با هشِ خالی هیچ‌وقت درست
    /// نمی‌گوید، پس هیچ راهی نیست که رشتهٔ خالی «رمزِ درست» شمرده شود —
    /// تصمیم فقط از <see cref="HasPassword"/> می‌آید.
    /// ⚠️ و اپِ کارمندان با رمزِ نگذاشته باز نمی‌شود: هشِ خالی منتشر
    /// می‌شود و گوشی خودش می‌گوید «صاحبِ پمپ هنوز رمزی نگذاشته». کیو‌آر و
    /// کدِ پمپ روی کاغذ می‌گردند؛ دفترِ پمپ نباید بی رمز از شبکه خوانده
    /// شود.
    /// </summary>
    public bool HasPassword()
    {
        using var db = _dbf.Create();
        return db.Users.Any(u => u.IsActive && u.PasswordHash != null && u.PasswordHash != "");
    }

    /// <summary>
    /// نصبِ تازه: مدیرِ بی‌رمز ساخته و همان لحظه وارد می‌شود.
    /// اگر رمزی گذاشته شده باشد هیچ کاری نمی‌کند و <c>false</c> می‌دهد.
    /// </summary>
    public bool OpenWithoutPassword()
    {
        using var db = _dbf.Create();
        var user = db.Users.Where(u => u.IsActive).OrderBy(u => u.Id).FirstOrDefault();
        if (user is null)
        {
            user = new AppUser
            {
                UserName = "admin",
                DisplayName = "مدیر",
                Role = UserRole.Admin,
                PasswordHash = "",      // ⛔ «رمزی نیست»
            };
            db.Users.Add(user);
        }
        else if (!string.IsNullOrEmpty(user.PasswordHash)) return false;

        user.LastLoginUtc = DateTime.UtcNow;
        db.Audit.Add(new AuditEntry { Actor = user.UserName, Action = "open-no-password" });
        db.SaveChanges();
        _session.SignIn(user.Role, user.UserName);
        return true;
    }

    /// <summary>
    /// رمزِ نخست را می‌گذارد — همان‌جا که هنوز رمزی نیست.
    /// ⚠️ اگر رمزی هست، این راه بسته است و باید <see cref="ChangePassword"/>
    /// با رمزِ فعلی زده شود؛ وگرنه نشستِ بازِ یک کامپیوتر می‌توانست رمزِ
    /// صاحبش را عوض کند.
    /// </summary>
    public void SetFirstPassword(string password)
    {
        using var db = _dbf.Create();
        var user = db.Users.Where(u => u.IsActive).OrderBy(u => u.Id).FirstOrDefault()
                   ?? throw new InvalidOperationException("کاربری نیست.");
        if (!string.IsNullOrEmpty(user.PasswordHash))
            throw new InvalidOperationException("رمز از قبل گذاشته شده است.");
        user.PasswordHash = PasswordHasher.Hash(password);
        db.Audit.Add(new AuditEntry { Actor = user.UserName, Action = "password-set" });
        db.SaveChanges();
    }

    /// <summary>
    /// رمز را برمی‌دارد و برنامه دوباره بی‌رمز باز می‌شود — با رمزِ فعلی.
    /// همان قاعدهٔ «خودِ طرف رمزِ خودش را می‌زند»، از هر دو طرف.
    /// </summary>
    public void ClearPassword(string current)
    {
        using var db = _dbf.Create();
        var user = db.Users.Where(u => u.IsActive).OrderBy(u => u.Id).FirstOrDefault()
                   ?? throw new InvalidOperationException("کاربری نیست.");
        if (!PasswordHasher.Verify(current, user.PasswordHash))
            throw new UnauthorizedAccessException("رمزِ فعلی درست نیست.");
        user.PasswordHash = "";
        db.Audit.Add(new AuditEntry { Actor = user.UserName, Action = "password-clear" });
        db.SaveChanges();
    }

    public void CreateFirstAdmin(string password, string userName = "admin")
    {
        using var db = _dbf.Create();
        if (db.Users.Any()) throw new InvalidOperationException("کاربرِ مدیر از قبل هست.");
        db.Users.Add(new AppUser
        {
            UserName = userName,
            DisplayName = "مدیر",
            Role = UserRole.Admin,
            PasswordHash = PasswordHasher.Hash(password),
        });
        db.SaveChanges();
    }

    public SignInOutcome SignIn(string userName, string password)
    {
        using var db = _dbf.Create();

        var until = GetLockUntil(db);
        if (until is not null && until > DateTime.UtcNow)
            return new SignInOutcome(SignInResult.LockedOut, UserRole.Viewer, until - DateTime.UtcNow);

        var user = db.Users.FirstOrDefault(u => u.UserName == userName && u.IsActive);
        if (user is null)
        {
            RegisterFailure(db);                       // همان تأخیر، تا نبودنِ کاربر لو نرود
            return new SignInOutcome(SignInResult.NoSuchUser, UserRole.Viewer);
        }

        if (!PasswordHasher.Verify(password, user.PasswordHash))
        {
            RegisterFailure(db);
            return new SignInOutcome(SignInResult.WrongPassword, UserRole.Viewer);
        }

        if (PasswordHasher.NeedsRehash(user.PasswordHash))
            user.PasswordHash = PasswordHasher.Hash(password);   // تازه‌سازیِ بی‌صدا
        user.LastLoginUtc = DateTime.UtcNow;
        ClearFailures(db);
        db.Audit.Add(new AuditEntry { Actor = userName, Action = "login" });
        db.SaveChanges();

        _session.SignIn(user.Role, user.UserName);
        return new SignInOutcome(SignInResult.Ok, user.Role);
    }

    public void SignOut() => _session.SignOut();

    /// <summary>
    /// ══ قفلِ اپِ کارمندان ═══════════════════════════════════════════════════
    ///
    /// خواستهٔ صریحِ صاحب ریپو: «برنامه برای شورت‌کات و اندروید جوری باشد که یک
    /// رمز داشته باشد — اول <b>رمزِ برنامهٔ نیتیوِ کامپیوتر</b> را بخواهد.»
    ///
    /// پس اپِ کارمندان همان رمز را می‌پرسد. رمز هیچ‌وقت از این‌جا بیرون نمی‌رود
    /// و <b>خواندنی هم نیست</b>؛ فقط همان رشتهٔ ‎pbkdf2$sha256$…‎ (نمک + هش)
    /// منتشر می‌شود و خودِ گوشی، رمزِ تایپ‌شده را با همان نمک و همان شمارِ دور
    /// می‌پزد و نتیجه را مقایسه می‌کند. یعنی:
    ///   • رمزِ خام هیچ‌جا نمی‌رود،
    ///   • و رمزِ اپ همیشه همان رمزِ برنامه است — عوض که شد، خودبه‌خود عوض شد.
    ///
    /// ⚠️ همین‌طور که هست منتشر می‌شود، نه چیزی بیشتر: هشِ PBKDF2 با ۲۱۰٬۰۰۰
    /// دور، پشتِ رمزِ خودِ سرورِ خانگی. ‎null‎ یعنی هنوز مدیری ساخته نشده.
    /// </summary>
    public string? AdminPasswordHash()
    {
        using var db = _dbf.Create();
        return db.Users
            .Where(u => u.IsActive && u.Role == UserRole.Admin)
            .OrderBy(u => u.Id)
            .Select(u => u.PasswordHash)
            .FirstOrDefault();
    }

    public void ChangePassword(string userName, string current, string next)
    {
        using var db = _dbf.Create();
        var user = db.Users.FirstOrDefault(u => u.UserName == userName)
                   ?? throw new InvalidOperationException("چنین کاربری نیست.");
        if (!PasswordHasher.Verify(current, user.PasswordHash))
            throw new UnauthorizedAccessException("رمزِ فعلی درست نیست.");
        user.PasswordHash = PasswordHasher.Hash(next);
        db.Audit.Add(new AuditEntry { Actor = userName, Action = "password-change" });
        db.SaveChanges();
    }

    // ── قفلِ فزاینده ───────────────────────────────────────────────────────
    private static string? Get(Persistence.PumpDbContext db, string k) =>
        db.Settings.AsNoTracking().FirstOrDefault(s => s.Key == k)?.Value;

    private static void Set(Persistence.PumpDbContext db, string k, string? v)
    {
        var row = db.Settings.FirstOrDefault(s => s.Key == k);
        if (row is null) db.Settings.Add(new Setting { Key = k, Value = v });
        else row.Value = v;
    }

    private static DateTime? GetLockUntil(Persistence.PumpDbContext db) =>
        DateTime.TryParse(Get(db, UntilKey), null, System.Globalization.DateTimeStyles.RoundtripKind, out var t)
            ? t : null;

    private static void RegisterFailure(Persistence.PumpDbContext db)
    {
        var n = int.TryParse(Get(db, FailKey), out var v) ? v + 1 : 1;
        Set(db, FailKey, n.ToString());
        if (n >= 3)
        {
            var wait = TimeSpan.FromSeconds(Math.Min(MaxLock.TotalSeconds, 5 * Math.Pow(2, n - 3)));
            Set(db, UntilKey, DateTime.UtcNow.Add(wait).ToString("O"));
        }
        db.SaveChanges();
    }

    private static void ClearFailures(Persistence.PumpDbContext db)
    {
        Set(db, FailKey, "0");
        Set(db, UntilKey, null);
    }
}
