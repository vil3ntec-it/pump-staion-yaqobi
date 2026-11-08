using PumpYaqobi.App.Services;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ قفلِ «فقط‌خواندنی» — خواستهٔ صاحب ریپو (۱۴۰۵/۰۷/۲۰) ══════════════════════
///
/// «کسی که هیچ اشتراکی نگرفته همه چی براش خوندنی باشه… کسایی که قبلن اشتراک
/// خریده بودن… یک هفته فرصت… آزمایشی‌ها از این هفته محروم.» و «تا حساب نسازد قفل».
/// تصمیم فقط در <see cref="AppLock.Decide"/> است؛ این‌جا هر حالش جدا.
/// </summary>
public class AppLockTests
{
    private const long Day = 86_400_000L;
    private const long Now = 1_800_000_000_000L;

    private static LicenseCheck Signed(long subEnds, long exp, string plan = "") =>
        new(exp > Now, exp > Now ? "ok" : "expired", Array.Empty<string>(), Array.Empty<string>(),
            subEnds, exp, plan == "trial" ? "آزمایشی" : "VIP", true, SignatureOk: true,
            Expired: exp <= Now, Plan: plan);

    [Fact]
    public void Baz_YaniHameChizBaz()
    {
        var s = AppLock.Decide(false, open: true, null, Now);
        Assert.False(s.ReadOnly);
        Assert.Equal(LockKind.None, s.Kind);
    }

    [Fact]
    public void BiHesab_FaqatKhandani()
    {
        var s = AppLock.Decide(hasAccount: false, open: false, null, Now);
        Assert.True(s.ReadOnly);
        Assert.Equal(LockKind.NoAccount, s.Kind);
        Assert.Contains("حساب بسازید", AppLock.Sentence(s));
    }

    [Fact]
    public void HesabBiEshterak_FaqatKhandani()
    {
        var s = AppLock.Decide(hasAccount: true, open: false, null, Now);
        Assert.True(s.ReadOnly);
        Assert.Equal(LockKind.NoSubscription, s.Kind);
    }

    [Fact]
    public void Azmayeshi_TamamShod_HamanLahze_Qofl_BiHafte()
    {
        //  یک دقیقه پس از پایانِ آزمایشی — هیچ هفتهٔ هشداری
        var s = AppLock.Decide(true, false, Signed(Now - 60_000, Now - 60_000, "trial"), Now);
        Assert.True(s.ReadOnly);
        Assert.Equal(LockKind.TrialEnded, s.Kind);
    }

    [Fact]
    public void Azmayeshi_HanuzBaqi_Baz()
    {
        var s = AppLock.Decide(true, false, Signed(Now + Day, Now - 1, "trial"), Now);
        Assert.False(s.ReadOnly);
    }

    /// <summary>
    /// ⛔ ۱۴۰۵/۰۸/۱۷: «همون روز و همون ساعت تموم بشه» — پس از پایانِ اشتراکِ پولی
    /// هیچ هفته‌ای نیست؛ همان لحظه فقط‌خواندنی. (هشدار پیش از پایان در نوار است.)
    /// </summary>
    [Theory]
    [InlineData(1_000)]
    [InlineData(60_000)]
    [InlineData(6 * Day)]
    public void Pooli_TamamShod_HamanLahze_Qofl(long agoMs)
    {
        var end = Now - agoMs;
        var s = AppLock.Decide(true, false, Signed(end, end), Now);
        Assert.True(s.ReadOnly);
        Assert.Equal(LockKind.PaidEnded, s.Kind);
    }

    [Fact]
    public void Pooli_YekDaqiqePishAzPayan_HanuzBaz()
    {
        var s = AppLock.Decide(true, false, Signed(Now + 60_000, Now - 1), Now);
        Assert.False(s.ReadOnly);
    }

    // ══ آزمایشیِ یک‌ماههٔ همین کامپیوتر (۱۴۰۵/۰۸/۱۷) ══════════════════════

    [Fact]
    public void BiHesab_AzmayesheKampyuter_Baz()
    {
        var s = AppLock.Decide(false, false, null, Now, machineTrialEnd: Now + Day);
        Assert.False(s.ReadOnly);
    }

    [Fact]
    public void BiHesab_AzmayesheKampyuter_TamamShod_Qofl()
    {
        var s = AppLock.Decide(false, false, null, Now, machineTrialEnd: Now - 1);
        Assert.True(s.ReadOnly);
        Assert.Equal(LockKind.TrialEnded, s.Kind);
    }

    [Fact]
    public void HesabeTazeRuyeHamanKampyuter_AzmayesheTazeNamigirad()
    {
        //  سرور آزمایشیِ تازه‌ای تا ۲۵ روزِ دیگر داد، ولی آزمایشیِ این کامپیوتر دیروز تمام شد
        var s = AppLock.Decide(true, false, Signed(Now + 25 * Day, Now + 9 * Day, "trial"), Now, machineTrialEnd: Now - Day);
        Assert.True(s.ReadOnly);
        Assert.Equal(LockKind.TrialEnded, s.Kind);
    }

    [Fact]
    public void Pooli_BeAzmayesheKampyuter_BastePlanNist()
    {
        //  پولی هرگز به دورهٔ آزمایشیِ کامپیوتر بسته نیست
        var s = AppLock.Decide(true, false, Signed(Now + 25 * Day, Now - 1), Now, machineTrialEnd: Now - Day);
        Assert.False(s.ReadOnly);
    }

    [Fact]
    public void Pooli_MojavezeKohneyeAfline_BishAzErfaq_EtebarNadarad()
    {
        //  ⛔ ۱۴۰۵/۰۸/۱۷: sub_ends دورِ آینده است و مجوز ۳۰ روز است تازه نشده ⇒ هنوز باز.
        //  اشتراکِ امضاشده هیچ شرطِ «هر هفته آنلاین شو» ندارد.
        var exp = Now - 30 * Day;
        var s = AppLock.Decide(true, false, Signed(Now + 100 * Day, exp), Now);
        Assert.False(s.ReadOnly);
        Assert.Equal(LockKind.None, s.Kind);
    }

    [Fact]
    public void EmzayeNakhordeh_MesleBiEshterak()
    {
        var bad = Signed(Now + Day, Now + Day) with { SignatureOk = false };
        var s = AppLock.Decide(true, false, bad, Now);
        Assert.Equal(LockKind.NoSubscription, s.Kind);
    }

    [Fact]
    public void Plan_AzMojavezeEmzaShode_KhandeMishavad()
    {
        Assert.True(Signed(0, 0, "trial").IsTrial);
        Assert.True(Signed(0, 0, "TRIAL").IsTrial);
        Assert.False(Signed(0, 0, "vip").IsTrial);
        Assert.False(Signed(0, 0).IsTrial);
    }

    [Fact]
    public void ErrorText_Denied_JomleyeQofl()
    {
        var old = PumpYaqobi.Application.Security.PermissionService.ReadOnlyHook;
        try
        {
            PumpYaqobi.Application.Security.PermissionService.ReadOnlyHook = () => true;
            var ex = new AggregateException(new PumpYaqobi.Application.Security.PermissionDeniedException(
                PumpYaqobi.Application.Security.Permission.EditData));
            Assert.Equal(AppLock.Denied, ErrorText.Friendly(ex));
            Assert.DoesNotContain("EditData", ErrorText.Friendly(ex));
            Assert.True(Modules.Transient(ex.InnerException!));
        }
        finally { PumpYaqobi.Application.Security.PermissionService.ReadOnlyHook = old; }
    }

    // ══ نوارِ قفلِ کهنه (۱۴۰۵/۰۷/۲۰، عکسِ صاحب ریپو) ═══════════════════════
    //  سربرگ «دائمی · فعال» و همان لحظه «برنامه فقط‌خواندنی است — حساب بسازید»:
    //  نوار فقط سرِ باز شدن ساخته می‌شد و پس از ورود دوباره پرسیده نمی‌شد.

    [Fact]
    public void NavareGhofl_KohneMimanad_JomleyeTazeJayashMinshinad()
    {
        var stale = AppLock.Sentence(new LockStatus(true, LockKind.NoAccount));
        Assert.Equal("", PumpYaqobi.App.ViewModels.MainViewModel.NextNotice(stale, ""));
    }

    [Fact]
    public void PayameModir_BaNavareGhofl_PakNemishavad()
    {
        Assert.Equal("📣 خبر", PumpYaqobi.App.ViewModels.MainViewModel.NextNotice("📣 خبر", "🔒 x"));
    }

    [Fact]
    public void SaateSarbarg_NavareGhoflRa_DobaraMiporsad()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        var win = SrcText.Read(Path.Combine(root, "PumpYaqobi.App", "Views", "MainWindow.axaml.cs"));
        var tick = win[win.IndexOf("_clock.Tick +=", StringComparison.Ordinal)..win.IndexOf("_clock.Start();", StringComparison.Ordinal)];
        Assert.Contains("vm.RefreshLockBanner();", tick);
        var life = SrcText.Read(Path.Combine(root, "PumpYaqobi.App", "ViewModels", "MainViewModel.Lifecycle.cs"));
        Assert.Contains("RefreshLockBanner(force: true)", life);
    }
}
