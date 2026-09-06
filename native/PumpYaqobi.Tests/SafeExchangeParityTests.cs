using System.Text.Json;
using PumpYaqobi.Application.Services;
using PumpYaqobi.Domain.Entities;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// برابریِ عددیِ گاوصندوق و صرافی با نسخهٔ HTML — همان روشِ آزمونِ قرض‌داران:
/// ۱۵۰ حالتِ تصادفیِ قطعی از داخلِ خودِ index.html گرفته شد
/// (‎_safeAddAmt‎ · ‎sarrafiBroken‎ · ‎sarrafiBaqi‎) و این‌جا مو‌به‌مو سنجیده می‌شود.
/// </summary>
public class SafeExchangeParityTests
{
    private sealed record SafeRow(string type, double amount, string currency);
    private sealed record Pair(double afn, double usd);
    private sealed record SafeCase(List<SafeRow> rows, Pair bard, Pair mand, Pair net);

    private sealed record ExRow(double amount, double rate, double bardagi, string currency);
    private sealed record PerRow(double u, double q);
    private sealed record ExCase(List<ExRow> rows, double usd, double bard, double bardUsd, double baqi, List<PerRow> perRow);
    private sealed record Golden(List<SafeCase> safe, List<ExCase> ex);

    private static Golden Load()
    {
        var p = Path.Combine(AppContext.BaseDirectory, "golden-safe-exchange.json");
        if (!File.Exists(p)) p = "golden-safe-exchange.json";
        return JsonSerializer.Deserialize<Golden>(File.ReadAllText(p))!;
    }

    [Fact]
    public void Safe_TotalsMatchTheHtmlExactly()
    {
        var svc = new SafeService();
        int n = 0;
        foreach (var c in Load().safe)
        {
            var entries = c.rows.Select(r => new SafeEntry
            {
                Kind = r.type == "bardagi" ? SafeEntryKind.Bardagi : SafeEntryKind.Mandagi,
                Amount = (decimal)r.amount,
                Currency = r.currency == "usd" ? Currency.Usd : Currency.Afn
            });
            var got = svc.Summarize(entries);
            Assert.Equal((decimal)c.bard.afn, got.Bardagi.Afn, 2);
            Assert.Equal((decimal)c.bard.usd, got.Bardagi.Usd, 2);
            Assert.Equal((decimal)c.mand.afn, got.Mandagi.Afn, 2);
            Assert.Equal((decimal)c.mand.usd, got.Mandagi.Usd, 2);
            // موجودی خالص = ماندگی − بردگی
            Assert.Equal((decimal)c.net.afn, got.Net.Afn, 2);
            Assert.Equal((decimal)c.net.usd, got.Net.Usd, 2);
            n++;
        }
        Assert.Equal(150, n);
    }

    [Fact]
    public void Exchange_TotalsMatchTheHtmlExactly()
    {
        var svc = new ExchangeService();
        int n = 0;
        foreach (var c in Load().ex)
        {
            var rows = c.rows.Select(r => new ExchangeRow
            {
                Amount = (decimal)r.amount,
                Rate = (decimal)r.rate,
                Bardagi = (decimal)r.bardagi,
                Currency = r.currency switch
                {
                    "kaldar" => ExchangeCurrency.Kaldar,
                    "afghani" => ExchangeCurrency.Afghani,
                    _ => ExchangeCurrency.Toman
                }
            }).ToList();

            for (int i = 0; i < rows.Count; i++)
            {
                Assert.Equal((decimal)c.perRow[i].u, svc.ToUsd(rows[i]), 6);
                Assert.Equal((decimal)c.perRow[i].q, svc.RowBaqi(rows[i]), 6);
            }

            var got = svc.Summarize(rows);
            Assert.Equal((decimal)c.usd,     got.TotalUsd,        6);
            Assert.Equal((decimal)c.bard,    got.TotalBardagi,    6);
            Assert.Equal((decimal)c.bardUsd, got.TotalBardagiUsd, 6);
            Assert.Equal((decimal)c.baqi,    got.Baqi,            6);
            n++;
        }
        Assert.Equal(150, n);
    }

    [Fact] // فیِ صفر ⇒ صفر، نه بی‌نهایت (همان رفتارِ HTML)
    public void Exchange_ZeroRate_IsZeroNotInfinity()
        => Assert.Equal(0m, new ExchangeService().ToUsd(new ExchangeRow { Amount = 5000m, Rate = 0m }));
}
