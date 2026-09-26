using PumpYaqobi.Application.Localization;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ رسیدِ سربرگِ قرض‌دار: ردیف ساخته شود و به حسابِ دیگر سرایت نکند ══════
///
/// گزارشِ صاحب ریپو (۱۴۰۵/۰۷/۱۴): «توی سربرگ رسید می‌زنم و جدولی اگه نباشه
/// رسید رو می‌گیره اما جدولی ساخته نمی‌شه… و همون مقدار که توی یک حساب زدم
/// توی همه حساب‌ها سرایت می‌کنه.»
///
/// ریشه‌ها و رفتارِ کامل در سنجهٔ رابطِ <c>headrasid</c>
/// (<c>PumpYaqobi.UiTests/HeadRasidProbe.cs</c>) سنجیده می‌شود؛ این‌جا فقط
/// قاعده‌هایی که بی پنجره سنجیدنی‌اند.
/// </summary>
public class HeadReceiptTests
{
    private static string Root()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null && !File.Exists(Path.Combine(d.FullName, "PumpYaqobi.sln"))) d = d.Parent;
        Assert.NotNull(d);
        return d!.FullName;
    }

    private static string Src(params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { Root(), "PumpYaqobi.App" }.Concat(parts).ToArray()));

    [Theory]
    [InlineData("‏7000", 7000)]     // RLM — صفحه‌کلیدِ فارسیِ ویندوز
    [InlineData("‎۲۰۰۰", 2000)]     // LRM + رقمِ فارسی
    [InlineData("7 000", 7000)]     // فاصلهٔ نشکن
    [InlineData("۱۲٬۵۰۰", 12500)]        // جداکنندهٔ هزارگانِ فارسی
    [InlineData("۱۲٫۵", 12.5)]           // ممیزِ فارسی
    [InlineData("1,250", 1250)]
    [InlineData("", 0)]
    [InlineData("abc", 0)]
    public void Num_NevisehayeNamarei_RaNemikhorad(string text, double want) =>
        Assert.Equal((decimal)want, Shamsi.Num(text));

    [Fact]
    public void RasidiKeNanashast_RadifeShabh_Nemimanad()
    {
        var src = Src("ViewModels", "Sections", "PersonViewModel.cs");
        var i = src.IndexOf("private async Task AddHeadReceiptAsync(", StringComparison.Ordinal);
        Assert.True(i >= 0);
        var body = src[i..src.IndexOf("private async Task PlaceHeadReceiptAsync(", i, StringComparison.Ordinal)];
        // شکستِ ذخیره (قفلِ نرم، دیسکِ قفل) ردیفِ در حافظه را پس می‌گیرد و کادر را برمی‌گرداند
        Assert.Contains("catch", body);
        Assert.Contains("Remove(row)", body);
        Assert.Contains("RepaintHeadEdits()", body);
        Assert.Contains("throw;", body);
    }

    [Fact]
    public void KasheRasid_RoyeDisk_Mineshinad()
    {
        var src = Src("ViewModels", "Sections", "PersonViewModel.cs");
        var i = src.IndexOf("internal async Task SyncReceiptsAsync()", StringComparison.Ordinal);
        Assert.True(i >= 0);
        var body = src[i..src.IndexOf("\n    }", i, StringComparison.Ordinal)];
        // «عوض نشد» تنها وقتی زود برمی‌گردد که ویومدل هم همان را دارد
        Assert.Contains("var changed = _host.Debt.SyncReceiptTotals(Entity);", body);
        Assert.Contains("UpdateAccountAsync", body);
    }

    [Fact]
    public void KadreSarbarg_HamishehJameDaftarRaNeshanMidahad()
    {
        var code = Src("Views", "Sections", "PersonView.axaml.cs");
        Assert.Contains("internal static void ShowLedgerSum(TextBox tb)", code);
        Assert.Contains("ShowLedgerSum(tb)", code[code.IndexOf("RasidBoxLost", StringComparison.Ordinal)..]);
        var xaml = Src("Views", "Sections", "PersonView.axaml");
        Assert.Contains("Tag=\"petrol\"", xaml);
        Assert.Contains("Tag=\"diesel\"", xaml);
    }
}
