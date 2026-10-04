using PumpYaqobi.Application.Localization;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ «توی ماه‌های قبل نمی‌شه جدول اضافه کرد» (۱۴۰۵/۰۷/۱۸) ════════════════════
/// ردیفِ تازه در ماهی که جلوی چشم است ساخته می‌شود، نه با تاریخِ امروز در ماهِ
/// جاری. رفتارِ کامل با پنجرهٔ واقعی: ‎-- oldmonths‎.
/// </summary>
public class OldMonthRowTests
{
    [Fact]
    public void MaheJari_HamanEmruz() => Assert.Equal(Shamsi.Today(), Shamsi.DateInMonth(Shamsi.ThisMonth()));

    [Theory]
    [InlineData("1404/03")]
    [InlineData("1403/12")]
    [InlineData("1404/7")]
    public void MaheGhabl_TarikhDarHamanMah(string month)
    {
        var d = Shamsi.DateInMonth(month);
        var parts = month.Split('/');
        Assert.Equal($"{int.Parse(parts[0]):0000}/{int.Parse(parts[1]):00}", Shamsi.MonthKey(d));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("1404/*")]
    [InlineData("xx")]
    [InlineData("1404/13")]
    public void KelideKharab_Emruz(string? month) => Assert.Equal(Shamsi.Today(), Shamsi.DateInMonth(month));

    /// <summary>
    /// ⛔ شورا ت۳ — رفتاری، با بخشِ «مصارف» و دفترِ واقعی: در ماهِ گذشته ردیفِ
    /// تازه مالِ <b>همان ماه</b> است و «روزِ بعد از آخرین ردیف» را می‌گیرد.
    /// </summary>
    [Fact]
    public async Task Masaref_DarMaheGhabl_RadifeHamanMah_Misazad()
    {
        var host = new PumpYaqobi.App.Services.AppHost(
            Path.Combine(Path.GetTempPath(), "pump-omr-" + Guid.NewGuid().ToString("N"), "pump.db"));
        if (host.Auth.NeedsFirstRun()) host.Auth.CreateFirstAdmin("1234");
        host.Auth.SignIn("admin", "1234");
        var sec = new PumpYaqobi.App.ViewModels.Sections.ExpenseSectionViewModel(host);
        await sec.EnsureLoadedAsync();
        sec.Month = "1404/03";

        await sec.AddRowCommand.ExecuteAsync(null);
        await sec.AddRowCommand.ExecuteAsync(null);

        //  از دیسک، نه از جدولِ صفحه (بارگذاریِ دوبارهٔ ماه ناهمگام است)
        var dates = (await host.ExpenseLedger.ListAsync("1404/03")).Select(e => e.DateShamsi).OrderBy(d => d).ToList();
        Assert.Equal(new[] { "1404/03/01", "1404/03/02" }, dates);
    }

    // ══ «ادامهٔ تاریخِ همون ماه» (۱۴۰۵/۰۷/۱۸، دوم) ══════════════════════════
    [Fact]
    public void RoozeBaadAzAkharinRadif() =>
        Assert.Equal("1405/06/11", Shamsi.NextInMonth("1405/06",
            new[] { "1405/06/02", "1405/06/10", "1405/06/05", null, "1405/05/30" }));

    [Fact]
    public void MaheKhali_RoozeAval() =>
        Assert.Equal("1405/06/01", Shamsi.NextInMonth("1405/06", new[] { "1405/05/20" }));

    [Fact]
    public void AkharinRooz_AzMahBiroonNemiravad() =>
        Assert.Equal("1404/06/31", Shamsi.NextInMonth("1404/06", new[] { "1404/06/31" }));

    [Fact]
    public void RaghameFarsi() =>
        Assert.Equal("1405/06/11", Shamsi.NextInMonth("۱۴۰۵/۰۶", new[] { "۱۴۰۵/۰۶/۱۰" }));

    [Fact]
    public void MaheJari_Emruz() =>
        Assert.Equal(Shamsi.Today(), Shamsi.NextInMonth(Shamsi.ThisMonth(), new[] { "1300/01/01" }));
}
