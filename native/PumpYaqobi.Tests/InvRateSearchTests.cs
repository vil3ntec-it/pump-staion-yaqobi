using PumpYaqobi.App.ViewModels.Sections;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ جست‌وجوی «مقایسهٔ نرخ فاکتورها» با شمارهٔ فاکتور (۱۴۰۵/۰۸/۰۲) ══
/// سنجهٔ ‎verify‎ (بند ۱۱) روز دوم ماهِ آبان سرخ شد: «۸» در تاریخِ ‎1405/08/02‎ بود و همهٔ
/// فاکتورها بیرون می‌آمدند. این‌جا بی وابستگی به امروز.
/// </summary>
public class InvRateSearchTests
{
    [Fact]
    public void Shomare_TarikhRaNemiGirad()
    {
        Assert.True(InvRateSectionViewModel.Matches("8", 8, "مشتریِ 8", "1405/08/02"));
        Assert.False(InvRateSectionViewModel.Matches("8", 3, "مشتریِ 3", "1405/08/02"));
        Assert.False(InvRateSectionViewModel.Matches("5", 3, "احمد", "1405/07/15"));
        Assert.True(InvRateSectionViewModel.Matches("۱۲", 112, "", "1405/01/01"));   // رقمِ فارسی
    }

    [Fact]
    public void TarikheTarikhVar_HanuzPeydaMishavad()
    {
        Assert.True(InvRateSectionViewModel.Matches("08/02", 3, "", "1405/08/02"));
        Assert.True(InvRateSectionViewModel.Matches("۱۴۰۵/۰۸", 3, "", "1405/08/02"));
        Assert.False(InvRateSectionViewModel.Matches("07/02", 3, "", "1405/08/02"));
    }

    [Fact]
    public void NameMoshtari_VaKhali()
    {
        Assert.True(InvRateSectionViewModel.Matches("احمد", 3, "حاجی احمد", "1405/08/02"));
        Assert.True(InvRateSectionViewModel.Matches("  ", 3, "", ""));
    }
}
