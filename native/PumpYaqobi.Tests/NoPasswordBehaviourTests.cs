using PumpYaqobi.App.Services;
using PumpYaqobi.App.ViewModels;
using PumpYaqobi.App.ViewModels.Sections;
using PumpYaqobi.Services.Security;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ شورا، ت۳ — «برنامه بی رمز» با <b>رفتارِ</b> خودِ ویومدل‌ها ═══════════════
///
/// قاعده‌های ۱۴۰۵/۰۷/۰۷ که تا امروز فقط با گشتنِ رشته در سورسِ
/// <c>LockViewModel</c>، <c>MainViewModel</c> و <c>KeysSectionViewModel</c> قفل
/// بودند. این‌جا خودِ همان ویومدل‌ها با دفترِ واقعی می‌دوند.
/// </summary>
[Collection(AppHostCollection.Name)]
public class NoPasswordBehaviourTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "pump-nopwb-" + Guid.NewGuid().ToString("N")[..8]);
    private readonly string? _was = AppSettings.DirOverride;

    public NoPasswordBehaviourTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        Dialogs.ConfirmHook = null;
        AppSettings.DirOverride = _was;
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try { Directory.Delete(_dir, true); } catch { }
        GC.SuppressFinalize(this);
    }

    private AppHost Fresh() => new(Path.Combine(_dir, "pump.db"));

    /// <summary>
    /// ⛔ صفحهٔ قفل رمز نمی‌سازد: نصبِ تازه همان لحظه باز می‌شود و
    /// <c>SignedIn</c> می‌خورد — و با رمز، همان صفحه هیچ‌کس را وارد نمی‌کند.
    /// </summary>
    [Fact]
    public async Task SafheyeGhofl_BiRamz_Baz_VaBaRamz_Baste()
    {
        var host = Fresh();
        var signed = 0;
        var lockVm = new LockViewModel(host);
        lockVm.SignedIn += () => signed++;

        Assert.True(await lockVm.OpenIfNoPasswordAsync());
        Assert.Equal(1, signed);
        Assert.True(host.Session.IsSignedIn);
        Assert.False(host.Auth.HasPassword());          // و رمزی هم ساخته نشد

        host.Auth.SetFirstPassword("1234");
        host.Auth.SignOut();
        var again = new LockViewModel(host);
        again.SignedIn += () => signed++;
        Assert.False(await again.OpenIfNoPasswordAsync());
        Assert.Equal(1, signed);
        Assert.False(host.Session.IsSignedIn);
    }

    /// <summary>
    /// ⛔ رمزِ برنامه از «تنظیمات ← رمزها و کد» گذاشته و برداشته می‌شود — بی
    /// رمز «رمزِ فعلی» خواسته نمی‌شود، و برداشتن رمزِ فعلی می‌خواهد.
    /// </summary>
    [Fact]
    public async Task RamzeBarname_DarTanzimat_GozashteVaBardashteMishavad()
    {
        var host = Fresh();
        host.Auth.OpenWithoutPassword();
        var keys = new KeysSectionViewModel(host);
        Assert.False(keys.HasAppPassword);

        keys.Next = "abcd"; keys.Confirm = "abcd";
        keys.ChangeAppPasswordCommand.Execute(null);
        Assert.Equal("", keys.AppError);
        Assert.True(host.Auth.HasPassword());
        Assert.True(keys.HasAppPassword);

        //  بی رمزِ فعلی برداشته نمی‌شود
        Dialogs.ConfirmHook = (_, _) => true;
        await keys.RemoveAppPasswordCommand.ExecuteAsync(null);
        Assert.NotEqual("", keys.AppError);
        Assert.True(host.Auth.HasPassword());

        keys.Current = "abcd";
        await keys.RemoveAppPasswordCommand.ExecuteAsync(null);
        Assert.False(host.Auth.HasPassword());
        Assert.False(keys.HasAppPassword);
    }

    /// <summary>
    /// ⛔ <b>بی رمز، «خروج» بن‌بست است و انجام نمی‌شود</b> — و بی‌صدا هم نیست.
    /// با رمز، همان دکمه واقعاً بیرون می‌برد و صفحهٔ قفل می‌آید.
    /// </summary>
    [Fact]
    public async Task Khoroje_BiRamz_BonBast_NemiSazad()
    {
        var host = AppHost.Start(Path.Combine(Path.GetTempPath(), "pump-shared-" + Guid.NewGuid().ToString("N"), "pump.db"));   // ⚠️ بیرونِ ‎_dir‎: میزبانِ مشترک پس از این آزمون هم زنده است
        if (host.Auth.NeedsFirstRun()) host.Auth.CreateFirstAdmin("1234");
        host.Auth.SignIn("admin", "1234");
        var vm = new MainViewModel();
        try
        {
            host.Auth.ClearPassword("1234");
            host.Auth.OpenWithoutPassword();
            var phase = vm.Phase;

            await vm.SignOutCommand.ExecuteAsync(null);

            Assert.True(host.Session.IsSignedIn);
            Assert.Equal(phase, vm.Phase);
            Assert.Contains("رمزی گذاشته نشده", host.Toasts.Text);

            host.Auth.SetFirstPassword("1234");
            host.Auth.SignIn("admin", "1234");
            await vm.SignOutCommand.ExecuteAsync(null);

            Assert.False(host.Session.IsSignedIn);
            Assert.Equal(MainViewModel.AppPhase.Locked, vm.Phase);
        }
        finally
        {
            if (!host.Auth.HasPassword()) host.Auth.SetFirstPassword("1234");
            host.Auth.SignIn("admin", "1234");
        }
    }
}
