using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ ستون‌های زندهٔ جدولِ قرض‌دار ═════════════════════════════════════════════
///
/// گزارشِ صاحب ریپو: «دو تا رسید تو یکی» — جدول هم ستونِ «رسید» داشت و هم
/// «رسید تیل»، و معلوم نبود کدام مالِ کدام است.
///
/// سایت این‌طور نیست و صریح هم نوشته (خطِ ۳۴۳۲۹ی <c>index.html</c>):
///   «ستونِ واحدِ مقابل از جدول برداشته شود — واحد تیل ⇐ ستونِ «رسید» (پولی)
///    حذف؛ واحد پول ⇐ ستونِ «رسید تیل» حذف.»
/// و همان‌جا برای «نوع تیل» (خطِ ۳۴۲۹۸): در حسابِ جداگانهٔ پطرول یا دیزل، وقتی
/// همهٔ ردیف‌ها یک تیل‌اند، آن ستون بی‌معناست و برداشته می‌شود.
/// </summary>
public class PersonColumnsTests
{
    private static readonly string Root =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    private static string Read(params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { Root }.Concat(parts).ToArray()));

    private static string Vm() =>
        Read("PumpYaqobi.App", "ViewModels", "Sections", "PersonViewModel.cs");

    /// <summary>در دفترِ تیل «رسید تیل» و در دفترِ پول «رسید» — نه هر دو.</summary>
    [Fact]
    public void OnlyOneReceiptColumnShowsAtATime()
    {
        var vm = Vm();
        Assert.Contains("public bool ShowRasidColumn => IsMoney;", vm);
        Assert.Contains("public bool ShowRasidFuelColumn => !IsMoney;", vm);
    }

    /// <summary>ردیفِ «جمله» هم همان یکی را نشان می‌دهد، نه هر دو.</summary>
    [Fact]
    public void TheTotalsRowShowsTheSameOneColumn()
    {
        var vm = Vm().Replace("\r", "");
        Assert.Contains("IsMoney\n                    ? new TotalCell(\"رسید\"", vm);
        Assert.Contains(": new TotalCell(\"رسید تیل\"", vm);
    }

    /// <summary>ستونِ «نوع تیل» در حسابِ جداگانهٔ پطرول/دیزل بی‌معناست.</summary>
    [Fact]
    public void TheFuelTypeColumnGoesAwayInASingleFuelView()
        => Assert.Contains("public bool ShowFuelTypeColumn => RowFilter == \"all\";", Vm());

    /// <summary>
    /// ستون‌ها از کدِ پشتِ نما نشانده می‌شوند، نه با ‎Binding‎ و نه با ‎x:Name‎:
    /// ستونِ ‎DataGrid‎ یک ‎AvaloniaObject‎ی ساده است — نه ‎DataContext‎ می‌گیرد
    /// و نه خاصیتِ ‎Name‎ دارد.
    /// </summary>
    [Fact]
    public void TheColumnsAreDrivenFromCodeBehind()
    {
        var cs = Read("PumpYaqobi.App", "Views", "Sections", "PersonView.axaml.cs");
        Assert.Contains("private void ApplyColumns()", cs);
        Assert.Contains("FindControl<DataGrid>(\"PersonGrid\")", cs);
        Assert.Contains("_acct.ShowRasidColumn", cs);
        Assert.Contains("_acct.ShowRasidFuelColumn", cs);
        Assert.Contains("_acct.ShowFuelTypeColumn", cs);

        // و جدول اسم دارد تا جدولِ آرشیو با آن قاطی نشود
        Assert.Contains("Name=\"PersonGrid\"", Read("PumpYaqobi.App", "Views", "Sections", "PersonView.axaml"));
    }

    /// <summary>با عوض شدنِ واحد و فیلتر، ستون‌ها هم خبردار می‌شوند.</summary>
    [Fact]
    public void ChangingTheUnitOrFilterRefreshesTheColumns()
    {
        var vm = Vm();
        Assert.Contains("nameof(ShowRasidColumn), nameof(ShowRasidFuelColumn), nameof(ShowFuelTypeColumn)", vm);
        Assert.Contains("nameof(ShowPetrolCard), nameof(ShowDieselCard), nameof(ShowFuelTypeColumn)", vm);
    }
}
