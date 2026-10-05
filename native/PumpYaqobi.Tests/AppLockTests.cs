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

    [Theory]
    [InlineData(0, 7)]
    [InlineData(1, 6)]
    [InlineData(6, 1)]
    public void Pooli_TamamShod_YekHafteHoshdar_HanuzMinevisad(int daysAgo, int left)
    {
        var end = Now - daysAgo * Day - 1000;
        var s = AppLock.Decide(true, false, Signed(end, end), Now);
        Assert.False(s.ReadOnly);
        Assert.Equal(LockKind.PaidWarning, s.Kind);
        Assert.Equal(left, s.DaysLeft);
        Assert.Equal(end + 7 * Day, s.LockAt);
        Assert.Contains($"{left} روز دیگر", AppLock.Sentence(s));
    }

    [Fact]
    public void Pooli_PasAzHafte_Qofl()
    {
        var end = Now - 7 * Day - 1;
        var s = AppLock.Decide(true, false, Signed(end, end), Now);
        Assert.True(s.ReadOnly);
        Assert.Equal(LockKind.PaidEnded, s.Kind);
    }

    [Fact]
    public void Pooli_MojavezeKohneyeAfline_BishAzErfaq_EtebarNadarad()
    {
        //  sub_ends دورِ آینده است، ولی مجوز ۳۰ روز است تازه نشده (ارفاقِ آفلاین ۱۴ روز)
        var exp = Now - 30 * Day;
        var s = AppLock.Decide(true, false, Signed(Now + 100 * Day, exp), Now);
        Assert.Equal(LockKind.PaidEnded, s.Kind);
        Assert.True(s.ReadOnly);
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
}
