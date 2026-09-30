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

    [Fact]
    public void Sorce_AddRow_MaheJoloyeCheshm()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        var src = File.ReadAllText(Path.Combine(root, "PumpYaqobi.App", "ViewModels", "LedgerSectionViewModel.cs"));
        Assert.Contains("e.DateShamsi = Shamsi.NextInMonth(Month, dates);", src);
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
