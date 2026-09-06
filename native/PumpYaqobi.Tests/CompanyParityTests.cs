using System.Text.Json;
using PumpYaqobi.Application.Services;
using PumpYaqobi.Domain.Entities;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// برابریِ عددیِ «شرکت‌های تیل» با نسخهٔ HTML — ۲۰۰ شرکتِ تصادفی که خودِ
/// ‎cmpTon/cmpTotalUsd/cmpAfnTotal/cmpPoulAfn/cmpPoulUsd/cmpAlbaqi/
/// cmpAlbaqiUsd/_companyConvRateOf‎ پاسخشان را داده‌اند.
///
/// این بخش یک گزارشِ صریحِ صاحب ریپو دارد: «الباقیِ دالر با افغانی برابر
/// نیست». آزمونِ آخر همان را نگه می‌دارد.
/// </summary>
public class CompanyParityTests
{
    private sealed record Comp(string name, double usdRate);
    private sealed record Row(string name, double kg, JsonElement ton, double usd,
                              double rate, double payRate, double poul, string poulCurrency);
    private sealed record Per(double ton, double usd, double afn, double paidAfn,
                              double paidUsd, double albAfn, double albUsd);
    private sealed record Case(Comp company, List<Row> rows, double rate, List<Per> perRow,
                               double totUsd, double totAfn, double paidAfn, double paidUsd);

    private static List<Case> Load()
    {
        var p = Path.Combine(AppContext.BaseDirectory, "golden-company.json");
        if (!File.Exists(p)) p = "golden-company.json";
        return JsonSerializer.Deserialize<List<Case>>(File.ReadAllText(p))!;
    }

    private static CompanyRow Map(Row r) => new()
    {
        Name = r.name,
        Kg = (decimal)r.kg,
        // «تن» در نسخهٔ وب می‌تواند رشتهٔ خالی باشد؛ آن یعنی صفر.
        Ton = r.ton.ValueKind == JsonValueKind.Number ? (decimal)r.ton.GetDouble() : 0m,
        Usd = (decimal)r.usd,
        Rate = (decimal)r.rate,
        PayRate = r.payRate == 0 ? null : (decimal)r.payRate,
        Poul = (decimal)r.poul,
        PoulCurrency = r.poulCurrency == "usd" ? Currency.Usd : Currency.Afn,
    };

    /// <summary>
    /// مقایسهٔ نسبی، نه مطلق: نسخهٔ وب با ‎double‎ حساب می‌کند و اینجا با
    /// ‎decimal‎. روی عددهای صدهزاری، آخرین رقمِ اعشارِ ‎double‎ خودش چند ده‌میلیونیم
    /// خطا دارد. چیزی که باید یکی باشد «همان عدد» است، نه بیتِ آخرِ ‎double‎.
    /// </summary>
    private static void Close(double expected, decimal actual)
    {
        var a = (double)actual;
        var tol = Math.Max(1e-6, Math.Abs(expected) * 1e-9);
        Assert.True(Math.Abs(expected - a) <= tol,
            $"انتظار {expected} بود، {a} آمد (اختلاف {Math.Abs(expected - a)})");
    }

    private static TilCompany MapC(Comp c) =>
        new() { Name = c.name, UsdRate = c.usdRate == 0 ? null : (decimal)c.usdRate };

    [Fact]
    public void EveryNumber_MatchesTheHtmlExactly()
    {
        var svc = new CompanyService();
        var n = 0;
        foreach (var c in Load())
        {
            var comp = MapC(c.company);
            var rows = c.rows.Select(Map).ToList();
            var rate = svc.ConvRate(comp, rows);
            Close(c.rate, rate);

            for (var i = 0; i < rows.Count; i++, n++)
            {
                var p = c.perRow[i];
                Close(p.ton, svc.Ton(rows[i]));
                Close(p.usd, svc.TotalUsd(rows[i]));
                Close(p.afn, svc.TotalAfn(rows[i]));
                Close(p.paidAfn, svc.PaidAfn(rows[i], rate));
                Close(p.paidUsd, svc.PaidUsd(rows[i], rate));
                Close(p.albAfn, svc.AlbaqiAfn(rows[i], rate));
                Close(p.albUsd, svc.AlbaqiUsd(rows[i], rate));
            }

            var s = svc.Summarize(comp, rows);
            Close(c.totUsd, s.TotalUsd);
            Close(c.totAfn, s.TotalAfn);
            Close(c.paidAfn, s.PaidAfn);
            Close(c.paidUsd, s.PaidUsd);
        }
        Assert.True(n > 500, "دادهٔ طلایی کم است: " + n);
    }

    [Fact]
    public void DollarAndAfghaniBalances_AgreeAtTheSameRate()
    {
        // گزارشِ صاحب ریپو: «در الباقی چرا دالر با افغانی برابر نیست؟»
        var svc = new CompanyService();
        var comp = new TilCompany { UsdRate = 70m };
        var r = new CompanyRow { Kg = 30000m, Usd = 700m, Rate = 70m, Poul = 500000m };
        var rate = svc.ConvRate(comp, new[] { r });
        Close((double)(svc.AlbaqiUsd(r, rate) * rate), svc.AlbaqiAfn(r, rate));
    }
}
