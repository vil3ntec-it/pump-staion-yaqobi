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
        Assert.Contains("e.DateShamsi = Shamsi.DateInMonth(Month);", src);
    }
}
