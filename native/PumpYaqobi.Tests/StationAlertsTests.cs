using PumpYaqobi.App.Services;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ «برنامه یک پیام بدهد» ═══════════════════════════════════════════════════
///
/// خواستهٔ صریحِ صاحب ریپو: «وقتی که یک قرض‌دار اضافه برد یا کم مانده بود از
/// حسابش، برنامه یک پیام بدهد — حتی اگر گوشی خاموش یا حتی اگر توی برنامه نبود
/// هم پیام برود تا بفهمد.»
///
/// فهرستِ هشدارها داخلِ خودِ عکسِ زنده می‌رود تا هم اپ و هم کارِ پس‌زمینهٔ گوشی
/// یک چیز ببینند. اینجا شکل و <b>کلیدِ</b> همان فهرست سنجیده می‌شود.
/// </summary>
public class StationAlertsTests
{
    private static Dictionary<string, object?> Person(long id, string name,
        string p = "ok", string d = "ok", string m = "ok")
        => new() { ["id"] = id, ["name"] = name, ["stP"] = p, ["stD"] = d, ["stM"] = m };

    private static List<Dictionary<string, object?>> Run(
        List<object?>? people, Dictionary<string, object?>? tank = null)
        => StationSnapshot.Alerts(people, tank).Cast<Dictionary<string, object?>>().ToList();

    private static string S(Dictionary<string, object?> a, string k) => a[k] as string ?? "";

    /// <summary>حسابِ سالم هیچ زنگی نمی‌زند — وگرنه کارمند زنگ را بی‌صدا می‌کند.</summary>
    [Fact]
    public void AHealthyAccountIsSilent()
        => Assert.Empty(Run(new List<object?> { Person(1, "هارون") }));

    /// <summary>«اضافه برد» و «کم مانده» — هر تیل جدا.</summary>
    [Fact]
    public void EachFuelRaisesItsOwnWarning()
    {
        var a = Run(new List<object?> { Person(7, "کریم", p: "out", d: "low") });

        Assert.Equal(2, a.Count);
        Assert.Equal("out", S(a[0], "s"));
        Assert.Equal("پطرول", S(a[0], "f"));
        Assert.Contains("اضافه نده", S(a[0], "t"), StringComparison.Ordinal);
        Assert.Equal("low", S(a[1], "s"));
        Assert.Equal("دیزل", S(a[1], "f"));
        Assert.Contains("کریم", S(a[1], "t"), StringComparison.Ordinal);
    }

    /// <summary>دفترِ پول هم همان‌قدر مهم است.</summary>
    [Fact]
    public void TheMoneyLedgerWarnsToo()
    {
        var a = Assert.Single(Run(new List<object?> { Person(3, "نصیر", m: "out") }));
        Assert.Equal("پول", S(a, "f"));
    }

    /// <summary>
    /// ⚠️ قلبِ ماجرا: کلید تا حال عوض نشده <b>ثابت</b> می‌ماند، پس گوشی هر
    /// بیست ثانیه دوباره زنگ نمی‌زند؛ و با بدتر شدنِ حال <b>عوض</b> می‌شود،
    /// پس «کم مانده ⇒ تمام شد» خبرِ تازهٔ خودش را می‌دهد.
    /// </summary>
    [Fact]
    public void TheKeyIsStableUntilTheSituationChanges()
    {
        var low = Assert.Single(Run(new List<object?> { Person(9, "سمیع", p: "low") }));
        var again = Assert.Single(Run(new List<object?> { Person(9, "سمیع", p: "low") }));
        Assert.Equal(S(low, "k"), S(again, "k"));

        var worse = Assert.Single(Run(new List<object?> { Person(9, "سمیع", p: "out") }));
        Assert.NotEqual(S(low, "k"), S(worse, "k"));

        // و دو نفرِ جدا هرگز یک کلید ندارند
        var other = Assert.Single(Run(new List<object?> { Person(10, "سمیع", p: "low") }));
        Assert.NotEqual(S(low, "k"), S(other, "k"));
    }

    /// <summary>مخزن هم همان‌جاست — «موجودیِ تیل در مخزن را ببینند».</summary>
    [Fact]
    public void TheTankWarnsOnTheSameList()
    {
        var tank = new Dictionary<string, object?>
        {
            ["petrol"] = new Dictionary<string, object?> { ["low"] = true, ["near"] = false, ["show"] = 120.0 },
            ["diesel"] = new Dictionary<string, object?> { ["low"] = false, ["near"] = true, ["show"] = 900.0 },
        };
        var a = Run(new List<object?>(), tank);

        Assert.Equal(2, a.Count);
        Assert.Equal("tank-petrol-out", S(a[0], "k"));
        Assert.Contains("ته کشید", S(a[0], "t"), StringComparison.Ordinal);
        Assert.Equal("tank-diesel-low", S(a[1], "k"));
    }

    /// <summary>مخزنِ پُر ساکت است.</summary>
    [Fact]
    public void AFullTankIsSilent()
        => Assert.Empty(Run(new List<object?>(), new Dictionary<string, object?>
        {
            ["petrol"] = new Dictionary<string, object?> { ["low"] = false, ["near"] = false },
        }));

    /// <summary>عکسِ ناقص نباید برنامه را بخواباند.</summary>
    [Fact]
    public void BrokenInputIsSurvived()
    {
        Assert.Empty(Run(null, null));
        Assert.Empty(Run(new List<object?> { null, "چیزِ بی‌ربط", new Dictionary<string, object?>() }));
    }
}
