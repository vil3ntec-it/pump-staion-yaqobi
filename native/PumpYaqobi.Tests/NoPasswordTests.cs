using PumpYaqobi.Application.Security;
using PumpYaqobi.Domain.Enums;
using PumpYaqobi.Services.Data;
using PumpYaqobi.Services.Security;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ «برنامه بدون رمز باشه» — نصبِ تازه، کامپیوترِ تازه ════════════════════
///
/// خواستهٔ صریحِ صاحب ریپو (۱۴۰۵/۰۷/۰۷): «برای هر کامپیوتر یک حسابِ جدید
/// باشه و بدون رمز، مگر حسابِ کاربری‌شو بزنه… کسایی که تازه به برنامه می‌رسن
/// نباید رمز داشته باشن و خودِ طرف برای خودش رمزِ خودشو می‌زنه… هر کی روی
/// کامپیوترِ جدید نصب کنه اطلاعات نباشه داخلش، مگر این که فولدرها رو یکی
/// برده باشه به کامپیوترِ جدید.»
///
/// ⚠️ این کلاس <b>هیچ‌وقت</b> ‎AppSettings.DirOverride‎ یا ‎AppHost.Start‎ را
/// لمس نمی‌کند — دیتابیسِ موقتِ خودش را می‌سازد. پس نشانِ
/// ‎[Collection(AppHostCollection.Name)]‎ لازم ندارد و
/// ‎SettingsCollectionRuleTests‎ هم کاری با آن ندارد.
/// </summary>
public class NoPasswordTests : IDisposable
{
    private readonly string _file = Path.Combine(Path.GetTempPath(), $"pump-nopw-{Guid.NewGuid():N}.db");
    private readonly PumpDbFactory _dbf;
    private readonly UserSession _session = new();
    private readonly AuthService _auth;

    public NoPasswordTests()
    {
        _dbf = new PumpDbFactory(_file);
        _dbf.EnsureReady();
        _auth = new AuthService(_dbf, _session);
    }

    public void Dispose() { try { if (File.Exists(_file)) File.Delete(_file); } catch { } }

    /// <summary>نصبِ تازه: نه رمزی هست، نه صفحهٔ قفلی — همان لحظه وارد می‌شود.</summary>
    [Fact]
    public void NasbeTaze_BiRamz_BazMishavad()
    {
        Assert.False(_auth.HasPassword());

        Assert.True(_auth.OpenWithoutPassword());
        Assert.True(_session.IsSignedIn);
        Assert.Equal(UserRole.Admin, _session.Role);

        // و باز هم رمزی نیست — «هشِ خالی» رمز نیست
        Assert.False(_auth.HasPassword());
    }

    /// <summary>
    /// ⛔ هشِ خالی «رمزی نیست» است، نه «رمزی که خالی است».
    /// هیچ رشته‌ای — حتی خودِ رشتهٔ خالی — نباید از آن رد شود.
    /// </summary>
    [Fact]
    public void HasheKhali_HichRamziRa_NemipaziRad()
    {
        _auth.OpenWithoutPassword();

        Assert.False(PasswordHasher.Verify("", ""));
        Assert.False(PasswordHasher.Verify("1234", ""));

        Assert.Equal(SignInResult.WrongPassword, _auth.SignIn("admin", "").Result);
        Assert.Equal(SignInResult.WrongPassword, _auth.SignIn("admin", "هرچه").Result);
    }

    /// <summary>خودِ کاربر رمزش را می‌گذارد — و از آن پس صفحهٔ قفل هست.</summary>
    [Fact]
    public void KarbarKhodash_RamzMigozarad_VaBarmidarad()
    {
        _auth.OpenWithoutPassword();

        _auth.SetFirstPassword("رمزِ خودم");
        Assert.True(_auth.HasPassword());
        Assert.Equal(SignInResult.Ok, _auth.SignIn("admin", "رمزِ خودم").Result);

        // ⛔ با بودنِ رمز، درِ بی‌رمز بسته است
        Assert.False(_auth.OpenWithoutPassword());
        Assert.Throws<InvalidOperationException>(() => _auth.SetFirstPassword("دوباره"));

        // و برداشتنش رمزِ فعلی می‌خواهد
        Assert.Throws<UnauthorizedAccessException>(() => _auth.ClearPassword("غلط"));
        _auth.ClearPassword("رمزِ خودم");
        Assert.False(_auth.HasPassword());
        Assert.True(_auth.OpenWithoutPassword());
    }

    /// <summary>
    /// ⛔ رمز هیچ‌وقت خام نمی‌نشیند — نه وقتی گذاشته می‌شود، نه وقتی برداشته.
    /// </summary>
    [Fact]
    public void Ramz_HichVaght_KhamNemineshinad()
    {
        _auth.OpenWithoutPassword();
        _auth.SetFirstPassword("۱۲۳۴۵۶");

        using (var db = _dbf.Create())
        {
            var h = db.Users.Single().PasswordHash!;
            Assert.DoesNotContain("۱۲۳۴۵۶", h);
            Assert.StartsWith("pbkdf2$sha256$", h);
        }

        _auth.ClearPassword("۱۲۳۴۵۶");
        using (var db = _dbf.Create())
            Assert.Equal("", db.Users.Single().PasswordHash);
    }

    /// <summary>
    /// ⛔ «برای هر کامپیوتر یک حسابِ جدید» — دفتر در پوشهٔ کاربرِ همان
    /// کامپیوتر است، نه کنارِ فایل‌های نصب. پس نصبِ تازه روی کامپیوترِ تازه
    /// <b>خالی</b> بالا می‌آید، و بردنِ همان پوشه یعنی بردنِ همان دفتر.
    /// </summary>
    [Fact]
    public void Daftar_DarPusheyeKarbar_Ast_NaKenareNasab()
    {
        var p = PumpDbFactory.DefaultPath;
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        Assert.StartsWith(appData, p);
        Assert.EndsWith("pump.db", p);
        Assert.NotEqual(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar),
                        Path.GetDirectoryName(p));
    }
}

/// <summary>
/// ══ سنجه‌های سورس: صفحهٔ قفل دیگر رمز نمی‌سازد ═════════════════════════════
///
/// خواندنِ خودِ فایل‌ها، همان روشِ <c>AppLinksTests</c> — ظاهرِ آوالونیا بی
/// پلتفرم ساخته نمی‌شود، ولی سیم‌کشی‌اش خواندنی است.
/// </summary>
public class NoPasswordSourceTests
{
    private static readonly string Root =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    private static string Read(params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { Root }.Concat(parts).ToArray()));

    /// <summary>
    /// بدنهٔ یک متد، با شمردنِ آکولاد — نه «تا متدِ بعدی».
    /// ⚠️ مرزِ «تا نامِ متدِ بعدی» شکننده است: هر متدی که بینشان بنشیند
    /// شمارش را به‌هم می‌زند و سنجه را سرخِ دروغ می‌کند (یک بار شد).
    /// </summary>
    private static string Body(string src, string signature)
    {
        var i = src.IndexOf(signature, StringComparison.Ordinal);
        Assert.True(i > 0, signature + " در سورس نیست");
        var open = src.IndexOf('{', i);
        var depth = 0;
        for (var j = open; j < src.Length; j++)
        {
            if (src[j] == '{') depth++;
            else if (src[j] == '}' && --depth == 0) return src[i..(j + 1)];
        }
        throw new Xunit.Sdk.XunitException("بدنهٔ " + signature + " بسته نشد");
    }

    /// <summary>
    /// ⛔ «نخستین اجرا ⇒ رمز بساز» برداشته شد و برنمی‌گردد: نه کادرِ
    /// «تکرارِ رمز»، نه <c>IsFirstRun</c>، نه <c>Confirm</c>.
    /// </summary>
    [Fact]
    public void SafheyeGhofl_DigarRamz_Nemisazad()
    {
        var vm = Read("PumpYaqobi.App", "ViewModels", "LockViewModel.cs");
        Assert.DoesNotContain("IsFirstRun", vm);
        Assert.DoesNotContain("CreateFirstAdmin", vm);
        Assert.Contains("OpenIfNoPasswordAsync", vm);
        Assert.Contains("HasPassword()", vm);

        var xaml = Read("PumpYaqobi.App", "Views", "LockView.axaml");
        //  ⚠️ کامنت‌ها اول برداشته می‌شوند: نامِ چیزِ برداشته‌شده در توضیحِ
        //  «این برداشته شد» هست و باید هم باشد.
        var code = System.Text.RegularExpressions.Regex.Replace(
            xaml, "<!--.*?-->", "", System.Text.RegularExpressions.RegexOptions.Singleline);
        Assert.DoesNotContain("IsFirstRun", code);
        Assert.DoesNotContain("Binding Confirm", code);
    }

    /// <summary>
    /// ⛔ هر سه راهِ خروجِ <c>WarmUpAsync</c> از یک جا رد می‌شوند
    /// (<c>LockOrOpenAsync</c>) — وگرنه یکی جا می‌ماند و برنامه گاهی صفحهٔ
    /// قفلِ بی‌رمز نشان می‌دهد.
    /// </summary>
    [Fact]
    public void HarSeRahe_WarmUp_AzYekJa_RadMishavand()
    {
        var mv = Read("PumpYaqobi.App", "ViewModels", "MainViewModel.cs");
        //  ⚠️ **خودِ بدنه** برداشته می‌شود، نه «تا متدِ بعدی»: یک بار مرزِ
        //  سنجه ‎WarmRestAsync‎ بود و تعریفِ خودِ ‎LockOrOpenAsync‎ بینشان
        //  نشست، پس سه فراخوان چهار شمرده شد و سنجه سرخِ دروغ داد.
        var warm = Body(mv, "public async Task WarmUpAsync");

        Assert.Equal(3, System.Text.RegularExpressions.Regex.Matches(warm, @"LockOrOpenAsync\(\)").Count);
        Assert.DoesNotContain("Phase = AppPhase.Locked", warm);

        Assert.Contains("private async Task LockOrOpenAsync()", mv);
        Assert.Contains("Lock.OpenIfNoPasswordAsync()", mv);
    }

    /// <summary>
    /// ⛔ بی رمز، «خروج» یک بن‌بست است: صفحهٔ قفلی که رمزی برای زدن ندارد.
    /// پس انجام نمی‌شود — و بی‌صدا هم رد نمی‌شود.
    /// </summary>
    [Fact]
    public void Khoroje_BiRamz_BonBast_NemiSazad()
    {
        var mv = Read("PumpYaqobi.App", "ViewModels", "MainViewModel.cs");
        var i = mv.IndexOf("private void SignOut()");
        Assert.True(i > 0);
        var body = mv[i..(i + 700)];
        Assert.Contains("Auth.HasPassword()", body);
        Assert.Contains("Toasts.Show", body);
    }

    /// <summary>
    /// ⛔ رمزِ برنامه تنها از «تنظیمات ← رمزها و کد» ساخته و برداشته می‌شود.
    /// </summary>
    [Fact]
    public void RamzeBarname_FaghatDarTanzimat_SakhteMishavad()
    {
        var vm = Read("PumpYaqobi.App", "ViewModels", "Sections", "KeysSectionViewModel.cs");
        Assert.Contains("SetFirstPassword", vm);
        Assert.Contains("ClearPassword", vm);
        Assert.Contains("HasAppPassword", vm);

        var xaml = Read("PumpYaqobi.App", "Views", "Sections", "KeysSectionView.axaml");
        Assert.Contains("RemoveAppPasswordCommand", xaml);
        Assert.Contains("AppLockStateText", xaml);
        //  کادرِ «رمزِ فعلی» بی رمز دیده نمی‌شود
        Assert.Contains("IsVisible=\"{Binding HasAppPassword}\"", xaml);
    }
}
