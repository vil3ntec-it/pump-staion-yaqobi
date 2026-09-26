using PumpYaqobi.App.Services;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ «با ده سال اطلاعات تست کن» (۱۴۰۵/۰۷/۱۴) ════════════════════════════════
///
/// سنجهٔ <c>PUMP_YEARS=10 … -- years</c> دو ریشه داد و هر دو این‌جا قفل‌اند:
///
/// ۱) عکسِ گوشیِ کارمند با یک سال داده ۱٫۶۵ میلیون نویسه بود و با ده سال ۱۴
///    مگابایت، در حالی که سرورِ حساب فایل را تا ۱٫۵ میلیون نویسه و بدنه را تا
///    دو مگابایت می‌پذیرد — پس گوشیِ بیرون از شبکهٔ پمپ هیچ‌وقت عکسی نمی‌دید.
///    <see cref="StationSnapshot.ForCloud"/> نسخهٔ سرور را زیرِ سقف می‌برد.
///
/// ۲) «آخرین هشدارها»ی داشبورد همهٔ ۱٬۸۰۱ هشدار را یک‌جا می‌ساخت: ۵٫۵ ثانیه
///    نخِ رابط. حالا فقط <c>AlertLimit</c> تا.
/// </summary>
public class TenYearsTests
{
    /// <summary>عکسی با یک بخشِ دفتری که هر ماهش <paramref name="perMonth"/> ردیف دارد.</summary>
    private static Dictionary<string, object?> Snap(int months, int perMonth)
    {
        var rows = new List<string[]>();
        var m = new List<string>();
        for (var i = 0; i < months; i++)
        {
            var month = $"{1395 + i / 12}/{i % 12 + 1:00}";
            for (var k = 0; k < perMonth; k++)
            {
                rows.Add(new[] { month + "/01", "ردیفِ نسبتاً بلندِ فارسی شمارهٔ " + k, "۱۲٬۰۰۰" });
                m.Add(month);
            }
        }
        return new Dictionary<string, object?>
        {
            ["v"] = 2,
            ["detail"] = true,
            ["alerts"] = new List<object?> { new Dictionary<string, object?> { ["k"] = "t-p", ["t"] = "مخزن" } },
            ["tank"] = new Dictionary<string, object?> { ["petrol"] = 1234.5 },
            ["debtors"] = new List<object?>(),
            ["sections"] = new Dictionary<string, object?>
            {
                ["safe"] = new Dictionary<string, object?>
                {
                    ["t"] = "گاوصندوق",
                    ["head"] = new List<string> { "تاریخ", "شرح", "مبلغ" },
                    ["rows"] = rows,
                    ["m"] = m,
                    ["sum"] = new List<string[]> { new[] { "جمع", "۹۹۹" } },
                },
                //  بخشی بی شکلِ ‎{rows, m}‎ — دست نمی‌خورد
                ["company"] = new Dictionary<string, object?> { ["list"] = new List<string> { "الف" } },
            },
        };
    }

    [Fact]
    public void AksKuchak_HamanAks_Ast()
    {
        var s = Snap(3, 5);
        Assert.Same(s, StationSnapshot.ForCloud(s));
    }

    [Fact]
    public void AksBozorg_ZireSaghf_Va_MaahhayeTaze_Mimanand()
    {
        var s = Snap(120, 400);                        // ده سال
        Assert.True(StationSnapshot.WireBytes(s) > StationSnapshot.CloudBudgetBytes);

        var c = StationSnapshot.ForCloud(s);
        Assert.True(StationSnapshot.WireBytes(c) <= StationSnapshot.CloudBudgetBytes);

        var safe = (Dictionary<string, object?>)((Dictionary<string, object?>)c["sections"]!)["safe"]!;
        var ms = (List<string>)safe["m"]!;
        var rows = (List<string[]>)safe["rows"]!;
        Assert.Equal(ms.Count, rows.Count);            // ردیف و ماه هم‌پا
        Assert.Contains("1404/12", ms);                // تازه‌ترین ماه هست
        Assert.DoesNotContain("1395/01", ms);          // کهنه‌ترین نه
        Assert.Equal(ms.Min(StringComparer.Ordinal), c["from"]);

        //  ⛔ جمع‌ها، هشدارها، مخزن و بخشِ دیگر دست نمی‌خورند
        Assert.Same(((Dictionary<string, object?>)((Dictionary<string, object?>)s["sections"]!)["safe"]!)["sum"], safe["sum"]);
        Assert.Same(s["alerts"], c["alerts"]);
        Assert.Same(s["tank"], c["tank"]);
        Assert.Same(((Dictionary<string, object?>)s["sections"]!)["company"],
                    ((Dictionary<string, object?>)c["sections"]!)["company"]);

        //  ⛔ عکسِ سرورِ خانگی (همان شیءِ اصلی) کامل می‌ماند
        Assert.Equal(120 * 400, ((List<string[]>)((Dictionary<string, object?>)((Dictionary<string, object?>)s["sections"]!)["safe"]!)["rows"]!).Count);
    }

    [Fact]
    public void BadaneyeDarkhast_FarsiRa_Farar_Nemidahad()
    {
        //  ⚠️ ‎\uXXXX‎ سه برابرِ UTF-8 است و سرور بدنهٔ بیش از دو مگابایت را رد می‌کند
        var src = File.ReadAllText(Path.Combine(Root(), "PumpYaqobi.App", "Services", "CloudLink.cs"));
        Assert.Contains("JsonContent.Create(body, options: WireJson)", src);
        Assert.Contains("UnsafeRelaxedJsonEscaping", src);
        Assert.Contains("JsonSerializerDefaults.Web", src);   // نام‌های camelCase همان می‌مانند
    }

    [Fact]
    public void Nasher_NoskheyeSarvar_Ra_Miferestad()
    {
        var src = File.ReadAllText(Path.Combine(Root(), "PumpYaqobi.App", "Services", "StationPublisher.cs"));
        Assert.Contains("PutFileAsync(CloudLiveFile, StationSnapshot.ForCloud(snap), ct)", src);
        Assert.Contains("\"body_too_large\"", src);
        //  ⛔ ساختنِ عکس دست‌بالا پنج درصدِ یک هسته
        Assert.Contains("if (!force && DateTime.UtcNow < _nextBuildAt) return false;", src);
        Assert.Contains("_nextBuildAt = DateTime.UtcNow + built.Elapsed * 19;", src);
    }

    [Fact]
    public void Dashboard_HameyeHoshdarha_Ra_Nemisazad()
    {
        var src = File.ReadAllText(Path.Combine(Root(), "PumpYaqobi.App", "ViewModels", "Sections", "DashboardSectionViewModel.cs"));
        Assert.Contains(".Take(AlertLimit)", src);
        Assert.Contains("BellCount = all.Count;", src);         // زنگ همهٔ هشدارها را می‌شمارد
        Assert.Contains("real.Take(5)", src);                   // پیامِ زنگ هم نه همه
        Assert.True(PumpYaqobi.App.ViewModels.Sections.DashboardSectionViewModel.AlertLimit <= 20);
    }

    private static string Root()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null && !Directory.Exists(Path.Combine(d.FullName, "PumpYaqobi.App")))
            d = d.Parent;
        Assert.NotNull(d);
        return d!.FullName;
    }
}
