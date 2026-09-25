using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ داشبوردِ تازهٔ پمپ‌بنزین — شکل نو، منطق دست‌نخورده ═════════════════════
///
/// خواستهٔ صاحب ریپو (۱۴۰۵/۰۷/۱۳): «داشبوردِ فعلی خیلی قدیمی و به درد نخور
/// است؛ حذفش کن و یک داشبوردِ مناسبِ پمپ‌بنزین بگذار… <b>هیچ منطقی دست
/// نخوره</b>.»
///
/// پس سه چیز با هم قفل می‌شوند: شکل واقعاً عوض شده (نقشهٔ مخزن به‌جای نوارِ
/// عمودی)، هیچ خاصیتِ تازه‌ای زیرِ پوستش ساخته نشده، و هیچ داده‌ای که
/// داشبوردِ قبلی نشان می‌داد از قلم نیفتاده.
/// </summary>
public class DashboardRemakeTests
{
    private static readonly string Root =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    private static string Read(params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { Root }.Concat(parts).ToArray()));

    private static string View() =>
        Read("PumpYaqobi.App", "Views", "Sections", "DashboardSectionView.axaml");

    private static string Vm() =>
        Read("PumpYaqobi.App", "ViewModels", "Sections", "DashboardSectionViewModel.cs");

    private static HashSet<string> BindingRoots(string xaml) =>
        Regex.Matches(xaml, @"\{Binding\s+!?([A-Za-z_][A-Za-z0-9_]*)(?![A-Za-z0-9_]*=)")
             .Select(m => m.Groups[1].Value).ToHashSet(StringComparer.Ordinal);

    private static string SourceName(string root) =>
        root.EndsWith("Command", StringComparison.Ordinal) ? root[..^"Command".Length] : root;

    /// <summary>
    /// ⛔ هیچ خاصیت یا فرمانِ تازه‌ای برای ظاهر ساخته نشد.
    ///
    /// ⚠️ و این آزمون سه اتصالِ مردهٔ داشبوردِ قبلی را هم گرفت: ‎UserName‎ و
    /// ‎SearchText‎ هیچ‌وقت در ویومدل نبودند (کادرِ جست‌وجویی که هیچ کاری نمی‌کرد)،
    /// و ساعتِ ثانیه‌دارش یک بار سرِ باز شدن نوشته می‌شد و دیگر تکان نمی‌خورد —
    /// ساعتِ زنده همان سربرگِ پنجره است. هر سه از صفحه رفتند.
    /// </summary>
    [Fact]
    public void Dashboard_HichKhasiyateTaze_Nasakht()
    {
        var vm = Vm();
        foreach (var root in BindingRoots(View()))
            Assert.True(vm.Contains(SourceName(root), StringComparison.Ordinal),
                        $"«{root}» در داشبورد بسته شده ولی در ویومدل نیست — یعنی منطقِ تازه.");
    }

    /// <summary>
    /// و چیزی از دست نرفت: هر داده و هر کاری که داشبوردِ قبلی نشان می‌داد
    /// هنوز جایی روی صفحه دارد.
    /// </summary>
    [Fact]
    public void Dashboard_HichDadeyi_GomNashod()
    {
        var x = View();
        foreach (var must in new[]
        {
            "Cards", "Bars", "SelectBarCommand", "SetRangeCommand", "SetFuelCommand",
            "FuelStatus", "TankLegend", "DonutPercentText", "DonutPetrolShare", "DonutDieselShare",
            "DonutNote", "Alerts", "Recent", "TrendProfit", "TrendExpense", "AreaValues",
            "AreaLabels", "DetailMoney", "DetailLiters", "DetailCount", "DetailGrowth",
            "Greeting", "DateLine", "BellCommand", "BellCount",
            "GoCommand", "FootNote",
        })
            Assert.True(x.Contains(must, StringComparison.Ordinal), $"«{must}» دیگر روی داشبورد نیست");
    }

    /// <summary>
    /// شکل واقعاً عوض شد: مخزن‌ها همان نقشهٔ مخزنِ بخشِ مخزن‌اند (فشرده)، و
    /// دکمه‌های رفتنِ سریع به بخش‌های روزمرهٔ پمپ سرِ داشبوردند.
    /// </summary>
    [Fact]
    public void Dashboard_BaNaghsheyeMakhzan_Va_KarhayeRozane()
    {
        var x = View();
        Assert.Contains("<c:TankGauge", x);
        Assert.Contains("Compact=\"True\"", x);
        Assert.DoesNotContain("WaterPercent", x); // آب تا دستگاه اندازه نداده دیده نمی‌شود
        foreach (var go in new[] { "shifts", "waraq", "debt", "storage", "safe" })
            Assert.Contains($"CommandParameter=\"{go}\"", x);
    }
}
