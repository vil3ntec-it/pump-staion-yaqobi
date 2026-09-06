using System.Globalization;
using System.Text.Json;
using PumpYaqobi.Application.Services;
using PumpYaqobi.Domain.Entities;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// داشبورد — همان اعدادی که ‎renderDashboard‎ در یک کرومیومِ واقعی نشان می‌دهد.
///
/// تقریباً هر چیزِ داشبورد به «امروز» بند است، پس فایلِ طلایی لحظهٔ اجرای خودش
/// را هم نگه می‌دارد و این‌جا همان لحظه به سرویس داده می‌شود — وگرنه آزمونی که
/// سرِ نیمه‌شب اجرا شود بی‌دلیل می‌شکند.
/// </summary>
public class DashboardParityTests
{
    private sealed record GrowthCase(double cur, double prev, int? g);
    private sealed record Layout(string range, int cols, int slot, List<string> labels, List<string> fulls);
    private sealed record Exp(string date, double amount);
    private sealed record Quick(double day, double week, double month, double year);
    private sealed record Safe(string date, double amount, string currency, string type);
    private sealed record Balance(double afn, double usd);
    private sealed record Golden(string nowIso, string today, List<GrowthCase> growth,
                                 List<Layout> layout, List<Exp> exps, Quick expQuick,
                                 List<Safe> safe, Balance safeBalance);

    private static Golden Load()
    {
        var p = Path.Combine(AppContext.BaseDirectory, "golden-dash.json");
        if (!File.Exists(p)) p = "golden-dash.json";
        return JsonSerializer.Deserialize<Golden>(File.ReadAllText(p))!;
    }

    /// <summary>لحظهٔ محلیِ همان اجرای جاوااسکریپت.</summary>
    private static DateTime Now(Golden g) =>
        DateTime.Parse(g.nowIso, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind).ToLocalTime();

    private static DashRange Range(string s) => s switch
    {
        "week" => DashRange.Week,
        "month" => DashRange.Month,
        "year" => DashRange.Year,
        _ => DashRange.Day,
    };

    [Fact]
    public void Growth_matches_the_web()
    {
        var g = Load();
        foreach (var c in g.growth)
            Assert.Equal(c.g, DashboardService.Growth((decimal)c.cur, (decimal)c.prev));
    }

    [Fact]
    public void Bar_layout_matches_the_web()
    {
        var g = Load();
        var svc = new DashboardService(() => Now(g));
        foreach (var l in g.layout)
        {
            var range = Range(l.range);
            Assert.Equal(l.cols, DashboardService.Cols(range));
            Assert.Equal(l.slot, svc.TodaySlot(range, l.cols));

            var s = svc.Series(range, new DashInput());
            // ‎buckets‎ یک خانه بیشتر از ستون‌هاست: خانهٔ ۰ فقط پایهٔ رشد است
            Assert.Equal(l.cols + 1, s.Buckets.Count);
            Assert.Equal(l.slot, s.Slot);
            for (var i = 0; i < s.Buckets.Count; i++)
            {
                Assert.Equal(l.labels[i], s.Buckets[i].Label);
                Assert.Equal(l.fulls[i], s.Buckets[i].Full);
            }
        }
    }

    [Fact]
    public void Expense_quick_totals_match_the_web()
    {
        var g = Load();
        var svc = new DashboardService(() => Now(g));
        var rows = g.exps.Select(e => new Expense { DateShamsi = e.date, Amount = (decimal)e.amount }).ToList();
        var q = svc.ExpQuick(rows);
        Assert.Equal((decimal)g.expQuick.day, q.Day);
        Assert.Equal((decimal)g.expQuick.week, q.Week);
        Assert.Equal((decimal)g.expQuick.month, q.Month);
        Assert.Equal((decimal)g.expQuick.year, q.Year);
    }

    [Fact]
    public void Safe_balance_matches_the_web()
    {
        var g = Load();
        var rows = g.safe.Select(e => new SafeEntry
        {
            DateShamsi = e.date,
            Amount = (decimal)e.amount,
            Currency = e.currency == "usd" ? Currency.Usd : Currency.Afn,
            Kind = e.type == "bardagi" ? SafeEntryKind.Bardagi : SafeEntryKind.Mandagi,
        }).ToList();
        var b = DashboardService.SafeBalance(rows);
        Assert.Equal((decimal)g.safeBalance.afn, b.Afn);
        Assert.Equal((decimal)g.safeBalance.usd, b.Usd);
    }

    /// <summary>«همه» باید دقیقاً جمعِ دو سوخت باشد، نه چیزِ دیگری.</summary>
    [Fact]
    public void Pick_all_is_the_sum_of_both_fuels()
    {
        var b = new DashBucket
        {
            Petrol = new DashCell { Afn = 10, Liters = 3, Profit = 2, Count = 1 },
            Diesel = new DashCell { Afn = 5, Liters = 1, Profit = 4, Count = 2 },
        };
        var all = DashboardService.Pick(b, DashFuel.All);
        Assert.Equal(15m, all.Afn);
        Assert.Equal(4m, all.Liters);
        Assert.Equal(6m, all.Profit);
        Assert.Equal(3, all.Count);
        Assert.Equal(10m, DashboardService.Pick(b, DashFuel.Petrol).Afn);
        Assert.Equal(5m, DashboardService.Pick(b, DashFuel.Diesel).Afn);
    }
}
