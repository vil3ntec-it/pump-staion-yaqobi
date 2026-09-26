using PumpYaqobi.App.Services;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ نمونهٔ کهنه، تصمیمِ تازهٔ نمونهٔ دیگر را پس نمی‌گیرد ════════════════════
///
/// سنجهٔ `linkstates` روی پشتهٔ واقعی (۱۴۰۵/۰۷/۱۳): حساب از ریشه در پنل حذف
/// شد، سرور به توکنِ دستگاه «device_not_registered» گفت و توکن پاک شد — و چند
/// لحظه بعد `CloudLink`ِ ماندگارِ پروفایل (که از زمانِ ثبت‌نام زنده بود) کلِ
/// شیءِ کهنه‌اش را نوشت و توکنِ مرده را **برگرداند**: چراغ سبز و پروفایل «فعال»
/// برای پمپی که دیگر نبود. حالا بندهای حساب و پمپ سه‌طرفه نوشته می‌شوند.
/// </summary>
[Collection(AppHostCollection.Name)]
public class SettingsBondMergeTests : IDisposable
{
    private readonly string _dir;
    private readonly string? _was;

    public SettingsBondMergeTests()
    {
        _was = AppSettings.DirOverride;
        _dir = Path.Combine(Path.GetTempPath(), "pump-bond-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_dir);
        AppSettings.DirOverride = _dir;
    }

    public void Dispose()
    {
        AppSettings.DirOverride = _was;
        try { Directory.Delete(_dir, true); } catch { }
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void NemooneyeKohne_TokeneMorde_Ra_BarNemigardanad()
    {
        new AppSettings { CloudDeviceToken = "dev-live", CloudLicense = "lic", CloudName = "الف" }.Save();

        var stale = AppSettings.Load();      // مثلِ پروفایل: از زمانِ ثبت‌نام زنده
        var fresh = AppSettings.Load();      // مثلِ حلقهٔ پس‌زمینه
        fresh.CloudDeviceToken = "";         // سرور گفت «این دستگاه ثبت نیست»
        fresh.CloudLicense = "";
        fresh.Save();

        stale.CloudName = "ب";               // نمونهٔ کهنه چیزِ دیگری را عوض کرد
        stale.Save();

        var disk = AppSettings.Load();
        Assert.Equal("", disk.CloudDeviceToken);
        Assert.Equal("", disk.CloudLicense);
        Assert.Equal("ب", disk.CloudName);   // و تغییرِ خودش هم گم نشد
    }

    [Fact]
    public void AnchehKhodashAvazKarde_Hamaan_NeveshteMishavad()
    {
        new AppSettings { CloudDeviceToken = "old" }.Save();
        var a = AppSettings.Load();
        var b = AppSettings.Load();
        b.CloudStationId = "stn_b";
        b.Save();

        a.CloudDeviceToken = "new-from-bind"; // خودش بند کرد ⇒ مقدارِ خودش برنده است
        a.Save();

        var disk = AppSettings.Load();
        Assert.Equal("new-from-bind", disk.CloudDeviceToken);
        Assert.Equal("stn_b", disk.CloudStationId);
    }

    [Fact]
    public void NemooneyeNew_BiPaye_KoleKhodash_Ra_Minevisad()
    {
        new AppSettings { CloudDeviceToken = "x" }.Save();
        new AppSettings { CloudDeviceToken = "" }.Save();
        Assert.Equal("", AppSettings.Load().CloudDeviceToken);
    }

    // ══ بقیهٔ خانه‌ها هم — نه فقط بندهای حساب و پمپ ═══════════════════════════
    //  `syncui` روی CI (۱۴۰۵/۰۷/۱۴): «گزارشِ خطا» خاموش شد و نمونهٔ کهنه‌ای که
    //  همان لحظه ذخیره کرد، روشنش کرد.

    [Fact]
    public void NemooneyeKohne_KelideTanzimat_Ra_PasNemigirad()
    {
        new AppSettings { ThemeId = "blue" }.Save();

        var stale = AppSettings.Load();      // مثلِ CloudLinkِ پس از ورود
        var page = AppSettings.Load();       // مثلِ صفحهٔ «همگام‌سازی»
        page.ReportErrorsOff = true;
        page.Save();

        stale.ThemeId = "gold";              // نمونهٔ کهنه چیزِ دیگری را عوض کرد
        stale.Save();

        var disk = AppSettings.Load();
        Assert.True(disk.ReportErrorsOff);   // ⛔ پس گرفته نشد
        Assert.Equal("gold", disk.ThemeId);  // و تغییرِ خودش هم گم نشد
    }

    [Fact]
    public void FarhangeDarJaAvazShode_Ham_Neveshte_Mishavad()
    {
        new AppSettings().Save();
        var a = AppSettings.Load();
        var b = AppSettings.Load();

        b.ColumnWidths["debt"] = new[] { 10.0, 20.0 };   // در جا، همان مرجع
        b.Save();
        a.ColumnWidths["safe"] = new[] { 30.0 };         // نمونهٔ دیگر هم در جا
        a.Save();

        //  ⚠️ هر دو فرهنگ را عوض کرده‌اند؛ آن‌که دیرتر نوشت برنده است — ولی
        //  تغییرِ در جای خودش گم نمی‌شود (مقایسه با متن است، نه با مرجع).
        var disk = AppSettings.Load();
        Assert.True(disk.ColumnWidths.ContainsKey("safe"));
    }

    [Fact]
    public void AnchehKhodashAvazKarde_BarAnchehDiskDarad_Mineshinad()
    {
        new AppSettings { ReportErrorsOff = false }.Save();
        var a = AppSettings.Load();
        var b = AppSettings.Load();
        b.ReportErrorsOff = true; b.Save();
        a.ReportErrorsOff = true; a.LastSection = "safe"; a.Save();
        var disk = AppSettings.Load();
        Assert.True(disk.ReportErrorsOff);
        Assert.Equal("safe", disk.LastSection);

        //  و خاموش کردنِ دوباره هم می‌نشیند
        var c = AppSettings.Load(); c.ReportErrorsOff = false; c.Save();
        Assert.False(AppSettings.Load().ReportErrorsOff);
    }
}
