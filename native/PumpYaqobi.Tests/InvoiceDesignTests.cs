using PumpYaqobi.App.Services;
using PumpYaqobi.App.ViewModels.Sections;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ بازطراحیِ بخشِ فاکتور ════════════════════════════════════════════════════
///
/// خواستهٔ صاحب ریپو: «یک بار گفته بودم که دیزاینِ فاکتور را عوض کن، یو‌ای
/// یو‌اکس باشد… فاکتور را طراحی کن برایم با دقت و توجهِ زیاد.»
///
/// ظاهرش را با چشم باید دید (‎dotnet run --project PumpYaqobi.UiTests --
/// invshot &lt;پوشه&gt;‎). این‌جا همان چیزهایی قفل می‌شوند که **رفتار**اند و
/// برگشتنی: حسابِ دو پاره، دروازهٔ دکمهٔ ثبت، و صافیِ فهرست.
/// </summary>
[Collection(AppHostCollection.Name)]
public class InvoiceDesignTests
{
    private static InvoiceSectionViewModel New() => new(AppHost.Current);

    /// <summary>
    /// ⚠️ یک نادرستیِ کهنه: نوشتهٔ کنارِ عدد «جمله کل (فی × لیتر)» بود، در
    /// حالی که خودِ حساب «مبلغ بدون تیل» را هم جمع می‌زد. حالا هر پاره عددِ
    /// خودش را دارد و جمع همان دو است.
    /// </summary>
    [Fact]
    public void TheTotalIsTheTwoPartsAddedUp()
    {
        var vm = New();
        vm.FLiters = "200";
        vm.FPrice = "67";
        vm.FAmount = "5000";

        Assert.Equal(13_400m, vm.FuelPart);
        Assert.Equal(5_000m, vm.MoneyPart);
        Assert.True(vm.HasFuelPart);
        Assert.True(vm.HasMoneyPart);
        Assert.Contains("18,400", vm.FTotalText.Replace("٬", ",").Replace("۰", "0")
                                    .Replace("۱", "1").Replace("۲", "2").Replace("۳", "3")
                                    .Replace("۴", "4").Replace("۵", "5").Replace("۶", "6")
                                    .Replace("۷", "7").Replace("۸", "8").Replace("۹", "9"));
    }

    /// <summary>پارهٔ نداشته نباید خطی بگیرد.</summary>
    [Fact]
    public void AMoneyOnlyInvoiceHasNoFuelPart()
    {
        var vm = New();
        vm.FAmount = "5000";

        Assert.False(vm.HasFuelPart);
        Assert.True(vm.HasMoneyPart);
        Assert.Equal(0m, vm.FuelPart);
    }

    /// <summary>
    /// «به نام دیگر» مقدم است — همان نامی که فاکتور با آن در حساب می‌نشیند.
    /// </summary>
    [Fact]
    public void TheAliasIsTheNameTheInvoiceLandsUnder()
    {
        var vm = New();
        vm.FAmount = "500";
        vm.FCustomer = "عبدالرحمن نوری";
        Assert.Equal("عبدالرحمن نوری", vm.TargetName);

        vm.FAlias = "شرکت نوری";
        Assert.Equal("شرکت نوری", vm.TargetName);
        Assert.Contains("شرکت نوری", vm.GoesToMoneyText);
    }

    /// <summary>
    /// ⚠️ پیش از این هر دو شرطِ ثبت فقط **پس از** زدنِ دکمه و به شکلِ توست
    /// گفته می‌شدند. حالا دکمه تا درست نشود نمی‌خورد و دلیلش هم زیرش نوشته
    /// است.
    /// </summary>
    [Fact]
    public void SubmitIsShutUntilTheFormMakesSense()
    {
        var vm = New();
        Assert.False(vm.CanSubmit);
        Assert.Contains("نامِ مشتری", vm.SubmitHint);

        vm.FCustomer = "احمد";
        Assert.False(vm.CanSubmit);
        Assert.Contains("مبلغ بدون تیل", vm.SubmitHint);

        vm.FAmount = "500";
        Assert.True(vm.CanSubmit);
        Assert.Equal("", vm.SubmitHint);
    }

    /// <summary>
    /// صافیِ فهرست بی برگشتن به برگه عوض می‌شود — سه کلیک شد یکی.
    /// </summary>
    [Fact]
    public void TheListFilterSwitchesInPlace()
    {
        var vm = New();

        vm.FilterCommand.Execute("approved");
        Assert.True(vm.IsApprovedPane);
        Assert.False(vm.IsPendingPane);
        Assert.True(vm.IsList);

        vm.FilterCommand.Execute("all");
        Assert.True(vm.IsAllPane);
        Assert.Contains("همهٔ فاکتورها", vm.ListTitle);

        vm.FilterCommand.Execute("pending");
        Assert.True(vm.IsPendingPane);
    }

    /// <summary>
    /// ⚠️ صافی نباید جست‌وجو را پاک کند — کارتِ برگه می‌کند، این یکی نه.
    /// </summary>
    [Fact]
    public void SwitchingTheFilterKeepsWhatWasTyped()
    {
        var vm = New();
        vm.Search = "۱۲۳";

        vm.FilterCommand.Execute("approved");
        Assert.Equal("۱۲۳", vm.Search);

        vm.OpenListCommand.Execute("pending");
        Assert.Equal("", vm.Search);
    }
}
