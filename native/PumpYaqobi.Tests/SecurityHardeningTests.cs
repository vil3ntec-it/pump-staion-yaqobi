using PumpYaqobi.App.Services;
using PumpYaqobi.App.ViewModels.Sections;
using PumpYaqobi.Services.Data;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ بازبینیِ امنیتیِ ۱۴۰۵/۰۷/۱۲ — رازِ دیتابیس، «باز کن»، و متنِ خطا ═══════
///
/// هر بند یکی از یافته‌های همان بازبینی را با رفتار (یا — جایی که رفتار
/// بی پنجره سنجیدنی نیست — با خودِ سورس) قفل می‌کند.
/// </summary>
[Collection(AppHostCollection.Name)]
public class SecurityHardeningTests : IDisposable
{
    private readonly string _dir;
    private readonly string? _was;

    public SecurityHardeningTests()
    {
        _was = AppSettings.DirOverride;
        _dir = Path.Combine(Path.GetTempPath(), "pump-sec-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        SafeOpen.TestStart = null;
        AppSettings.DirOverride = _was;
        try { Directory.Delete(_dir, true); } catch { }
        GC.SuppressFinalize(this);
    }

    private static string Root() =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    private static string Src(params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { Root() }.Concat(parts).ToArray()));

    // ══ D) رمزِ نوشتنِ سرورِ خانگی بیرون از pump.db ══════════════════════

    /// <summary>
    /// ⛔ ردیفِ جامانده در دیتابیس به تنظیماتِ رمزشده می‌رود و از دیتابیس
    /// <b>پاک</b> می‌شود — <c>pump.db</c> داخلِ هر پشتیبانی است که به سرور
    /// می‌رود.
    /// </summary>
    [Fact]
    public void RamzeSarvar_AzDaftar_BeTanzimateRamzshode_Miravad()
    {
        var host = AppHost.Start(Path.Combine(Path.GetTempPath(), "pump-sec-" + Guid.NewGuid().ToString("N"), "pump.db"));
        if (host.Auth.NeedsFirstRun()) host.Auth.CreateFirstAdmin("1234");
        host.Auth.SignIn("admin", "1234");
        AppSettings.DirOverride = _dir;

        //  الف) تنظیمات رمزی ندارد ⇒ رمزِ دیتابیس جابه‌جا می‌شود
        host.Settings.Set(SettingsService.SyncCode, "tok-legacy");
        Assert.Equal("tok-legacy", HomeLink.Token(host));
        Assert.Null(host.Settings.Get(SettingsService.SyncCode));
        Assert.Equal("tok-legacy", AppSettings.Load().ServerToken);
        //  ⛔ و روی دیسک خام نیست
        Assert.DoesNotContain("tok-legacy", File.ReadAllText(Path.Combine(_dir, "settings.json")));

        //  ب) تنظیمات رمزِ خودش را دارد ⇒ همان می‌ماند و ردیفِ دیتابیس فقط پاک می‌شود
        host.Settings.Set(SettingsService.SyncCode, "read-key-mistaken-for-token");
        Assert.Equal("tok-legacy", HomeLink.Token(host));
        Assert.Null(host.Settings.Get(SettingsService.SyncCode));
    }

    [Fact]
    public void HichJa_RamzeSarvar_RaDarDaftar_NemiNevisad()
    {
        foreach (var f in Directory.EnumerateFiles(Path.Combine(Root(), "PumpYaqobi.App"), "*.cs", SearchOption.AllDirectories))
        {
            if (f.Contains("/obj/") || f.Contains("\\obj\\")) continue;
            Assert.DoesNotContain("Set(SettingsService.SyncCode", File.ReadAllText(f));
        }
    }

    // ══ F) «باز کن» فقط برای نشانی ═════════════════════════════════════════

    [Theory]
    [InlineData("https://yaqobipump.top/view/#d=1", true)]
    [InlineData("http://192.168.1.20/snapshot.jpg", true)]
    [InlineData("rtsp://192.168.1.30:554/stream1", true)]
    [InlineData("mailto:support@example.com", true)]
    [InlineData(@"C:\Windows\System32\calc.exe", false)]
    [InlineData("C:/Users/x/evil.lnk", false)]
    [InlineData(@"\\server\share\x.exe", false)]
    [InlineData("//server/share/x.exe", false)]
    [InlineData("file:///C:/Windows/System32/calc.exe", false)]
    [InlineData("ms-settings:printers", false)]
    [InlineData("javascript:alert(1)", false)]
    [InlineData("calc.exe", false)]
    [InlineData("https://ok.example/\nC:\\x.exe", false)]
    [InlineData("", false)]
    public void SafeOpen_FaghatNeshani_RaMipazirad(string url, bool ok)
    {
        Assert.Equal(ok, SafeOpen.IsAllowed(url));

        var started = 0;
        SafeOpen.TestStart = _ => { started++; return true; };
        Assert.Equal(ok, SafeOpen.Url(url));
        Assert.Equal(ok ? 1 : 0, started);
    }

    /// <summary>هیچ جای دیگری نشانی را مستقیم به ویندوز نمی‌سپارد.</summary>
    [Fact]
    public void HameyeNeshaniha_AzSafeOpen_Mirond()
    {
        foreach (var (file, parts) in new[]
        {
            ("CameraSectionViewModel", new[] { "PumpYaqobi.App", "ViewModels", "Sections", "CameraSectionViewModel.cs" }),
            ("AppsSectionViewModel", new[] { "PumpYaqobi.App", "ViewModels", "Sections", "AppsSectionViewModel.cs" }),
            ("QrWindow", new[] { "PumpYaqobi.App", "Views", "QrWindow.axaml.cs" }),
            ("GoogleSignIn", new[] { "PumpYaqobi.App", "Services", "GoogleSignIn.cs" }),
        })
        {
            var src = Src(parts);
            Assert.DoesNotContain("UseShellExecute", src);
            Assert.Contains("SafeOpen.", src);
        }
    }

    [Theory]
    [InlineData("abc_DEF-123", true)]
    [InlineData("../../evil", false)]
    [InlineData("a/b", false)]
    [InlineData("C:x", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void ShenaseyeResane_FaghatHarfVaRaghm(string? id, bool ok) =>
        Assert.Equal(ok, ChatSectionViewModel.SafeMediaId(id));

    [Fact]
    public void DllHa_FaghatAzSystem32()
    {
        var wave = Src("PumpYaqobi.App", "Services", "WaveRecorder.cs");
        var secret = Src("PumpYaqobi.App", "Services", "SecretStore.cs");
        Assert.Equal(CountOf(wave, "[DllImport(\"winmm.dll\")"),
                     CountOf(wave, "DefaultDllImportSearchPaths(DllImportSearchPath.System32)"));
        Assert.Equal(CountOf(secret, "[DllImport("),
                     CountOf(secret, "DefaultDllImportSearchPaths(DllImportSearchPath.System32)"));
    }

    private static int CountOf(string s, string needle)
    {
        var n = 0;
        for (var i = s.IndexOf(needle, StringComparison.Ordinal); i >= 0; i = s.IndexOf(needle, i + 1, StringComparison.Ordinal)) n++;
        return n;
    }

    // ══ G) متنِ خطا ═════════════════════════════════════════════════════════

    [Fact]
    public void MatneKhameEstesna_BeKarbar_NemiResad()
    {
        var path = new IOException(@"The process cannot access the file 'C:\Users\haroon\AppData\Roaming\PumpYaqobi\pump.db'");
        Assert.Equal(ErrorText.FileProblem, ErrorText.Friendly(path));
        Assert.Equal(ErrorText.NetworkProblem,
                     ErrorText.Friendly(new HttpRequestException("No such host is known. (api.vill3n.top:443)")));
        Assert.Equal(ErrorText.Generic, ErrorText.Friendly(new InvalidOperationException("Sequence contains no elements")));
        Assert.Equal(ErrorText.NetworkProblem,
                     ErrorText.Friendly(new AggregateException(new TimeoutException("x"))));
        Assert.Equal(ErrorText.Generic, ErrorText.Friendly(null));

        //  ⚠️ پیامِ خودِ برنامه، به فارسی، همان‌طور می‌ماند
        Exception ours;
        try { new SectionLockAccess().Throw(); throw null!; }
        catch (Exception e) { ours = e; }
        Assert.Equal("این بخش قفل نمی‌پذیرد: debt", ErrorText.Friendly(ours));
    }

    /// <summary>برای سنجیدنِ «پیامِ خودِ برنامه» — استثنایی که از اسمبلیِ خودمان بیاید.</summary>
    private sealed class SectionLockAccess
    {
        public void Throw() =>
            AppHost.Start(Path.Combine(Path.GetTempPath(), "pump-sec-" + Guid.NewGuid().ToString("N"), "pump.db"))
                   .Locks.SetPassword("debt", "1234");
    }

    [Fact]
    public void GozaresheSarvar_NameKarbar_VaKampyuter_RaNadarad()
    {
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var raw = $"at X in {profile}{Path.DirectorySeparatorChar}src{Path.DirectorySeparatorChar}a.cs "
                + $"on {Environment.MachineName} by {Environment.UserName}";
        var clean = ErrorText.Scrub(raw);

        Assert.Contains("%USERPROFILE%", clean);
        if (profile.Length > 3) Assert.DoesNotContain(profile, clean, StringComparison.OrdinalIgnoreCase);
        if (Environment.MachineName.Length >= 3)
            Assert.DoesNotContain(" " + Environment.MachineName + " ", clean, StringComparison.OrdinalIgnoreCase);
        if (Environment.UserName.Length >= 3)
            Assert.DoesNotContain("by " + Environment.UserName, clean, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CrashGuard_BeKarbar_MatneKham_NemiDahad()
    {
        var src = Src("PumpYaqobi.App", "Services", "CrashGuard.cs");
        Assert.Contains("ErrorText.Friendly(ex)", src);
        Assert.Contains("ErrorText.Scrub(ex.StackTrace", src);
        Assert.DoesNotContain("e?.Message is", src);

        foreach (var parts in new[]
        {
            new[] { "PumpYaqobi.App", "ViewModels", "Sections", "KeysSectionViewModel.cs" },
            new[] { "PumpYaqobi.App", "ViewModels", "Sections", "BackupSectionViewModel.cs" },
            new[] { "PumpYaqobi.App", "Services", "BackupPusher.cs" },
            new[] { "PumpYaqobi.App", "Services", "StationLink.cs" },
            new[] { "PumpYaqobi.App", "Services", "VlcVideoFeed.cs" },
        })
        {
            var s = Src(parts);
            Assert.DoesNotContain("+ ex.Message", s);
            Assert.DoesNotContain("+ e.Message", s);
            Assert.DoesNotContain("= ex.Message", s);
            Assert.DoesNotContain("= e.Message", s);
        }
    }
}
