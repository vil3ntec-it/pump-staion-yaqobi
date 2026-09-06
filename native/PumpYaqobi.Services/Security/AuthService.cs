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
