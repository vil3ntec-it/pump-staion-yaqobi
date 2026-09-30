using PumpYaqobi.Application.Services;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ ورق: ردیف‌ها تکان نمی‌خورند، نوشته اصلاح نمی‌شود، دیزل در جمله‌اش ════════
/// گزارشِ صاحب ریپو (۱۴۰۵/۰۷/۱۸): «جدول‌ها اتومات می‌رن بالا یا پایین… جملات
/// پاک یا بیخود اصلاح می‌شن… وقتی دیزل می‌نویسم توی جمله دیزل نوشته نمی‌شه.»
/// رفتارِ رابط را سنجهٔ ‎waraqtype‎ با کلیدِ واقعی می‌سنجد؛ این‌جا قاعده‌ها.
/// </summary>
public class WaraqStableRowsTests
{
    private static readonly string Root =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    private static string Src(string rel) =>
        File.ReadAllText(Path.Combine(Root, rel.Replace('/', Path.DirectorySeparatorChar)));

    private const string Vm = "PumpYaqobi.App/ViewModels/Sections/WaraqSectionViewModel.cs";

    private static string Body(string src, string sig)
    {
        var i = src.IndexOf(sig, StringComparison.Ordinal);
        Assert.True(i >= 0, sig);
        var open = src.IndexOf('{', i);
        var depth = 0;
        for (var k = open; k < src.Length; k++)
        {
            if (src[k] == '{') depth++;
            else if (src[k] == '}' && --depth == 0) return src[open..(k + 1)];
        }
        return src[open..];
    }

    [Fact]
    public void RadifeTaze_VaHazf_HichRadifeDigariRa_JabejaNemikonad()
    {
        var s = Src(Vm);
        //  ⛔ تقسیمِ نیمه‌به‌نیمه فقط سرِ باز شدن (‎Build‎)، نه با افزودن و حذف
        Assert.DoesNotContain("SplitTxns", Body(s, "private async Task AddTxnAsync()"));
        Assert.DoesNotContain("SplitTxns", Body(s, "private async Task DeleteTxnAsync("));
        Assert.Contains("PlaceNewTxn(vm)", Body(s, "private async Task AddTxnAsync()"));
        Assert.Contains("RemoveTxnRow(row)", Body(s, "private async Task DeleteTxnAsync("));
        //  ردیفِ تازه تهِ جدولِ دوم — همان‌جا که شماره‌اش می‌گوید
        Assert.Contains("TxnsSecond.Add(vm)", Body(s, "private void PlaceNewTxn("));
        Assert.DoesNotContain("Clear()", Body(s, "private void PlaceNewTxn("));
        Assert.DoesNotContain("Clear()", Body(s, "private void RemoveTxnRow("));
    }

    [Fact]
    public void Felesh_Takmele_RaNemipazirad_FaghatTabYaEnter()
    {
        var s = Src("PumpYaqobi.App/Controls/Suggest.cs");
        var key = Body(s, "private static void OnBoxKey(");
        var arrows = key[key.IndexOf("case Key.Right:", StringComparison.Ordinal)..];
        arrows = arrows[..arrows.IndexOf("return;", StringComparison.Ordinal)];
        Assert.Contains("Revert()", arrows);
        Assert.DoesNotContain("Accept()", arrows);
        Assert.Contains("case Key.Tab when _ghost.Length > 0:", key);
        Assert.Contains("case Key.Enter when _ghost.Length > 0:", key);
    }

    [Fact]
    public void TilAzNam_BiNeshane_HamanPishfarzePatrol()
    {
        var s = Body(Src(Vm), "private async Task AutoFromNameAsync(");
        Assert.Contains("PostingService.DetectFuelType(text)", s);
        Assert.DoesNotContain("if (PostingService.MentionsFuel(text))", s);
        Assert.Equal(FuelType.Diesel, PostingService.DetectFuelType("کریم دیزل"));
        Assert.Equal(FuelType.Diesel, PostingService.DetectFuelType("د کریم"));
        Assert.Equal(FuelType.Petrol, PostingService.DetectFuelType("کریم"));
    }

    [Fact]
    public void LitreRadifha_BeTafkikeTil_VaBeJomlePayeHaEzafeNemishavad()
    {
        var sd = new WaraqShift();
        sd.Pumps.Add(new WaraqPump { Fuel = FuelType.Diesel, Start = 100, End = 400, PricePerLiter = 80 });
        sd.Pumps.Add(new WaraqPump { Fuel = FuelType.Petrol, Start = 0, End = 500, PricePerLiter = 70 });
        sd.Transactions.Add(new WaraqTransaction { Name = "کریم دیزل", Liters = 40, Fuel = FuelType.Diesel });
        sd.Transactions.Add(new WaraqTransaction { Name = "علی", Liters = 25, Fuel = FuelType.Petrol });
        sd.Transactions.Add(new WaraqTransaction { Name = "نان", Liters = 5, Fuel = FuelType.Diesel, Type = WaraqTxnType.Expense });
        sd.Transactions.Add(new WaraqTransaction { Name = "", Liters = 99, Fuel = FuelType.Diesel });

        var calc = new WaraqService();
        var (petrol, diesel) = calc.TxnLiters(sd);
        Assert.Equal(25m, petrol);
        Assert.Equal(45m, diesel);

        //  ⛔ جمعِ پایه‌ها همان قرائت می‌ماند — لیترِ قرض دو بار شمرده نمی‌شود
        var t = calc.ShiftTotals(sd);
        Assert.Equal(300m, t.DieselLiters);
        Assert.Equal(500m, t.PetrolLiters);
    }
}
