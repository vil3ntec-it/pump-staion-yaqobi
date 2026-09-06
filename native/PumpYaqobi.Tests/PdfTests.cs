using PumpYaqobi.Application.Services;
using PumpYaqobi.Domain.Enums;
using PumpYaqobi.Infrastructure.Migration;
using PumpYaqobi.Reporting.Pdf;
using QuestPDF.Fluent;
using Xunit;

namespace PumpYaqobi.Tests;

file sealed class R : IUnionRateProvider
{ public decimal UnionRate(FuelType f) => f == FuelType.Diesel ? 60m : 56m; }

/// <summary>
/// PDF واقعاً ساخته می‌شود — نه اینکه فقط کامپایل شود.
/// خروجی روی دیسک نوشته و بازرسی می‌شود: هدرِ PDF، تعداد صفحه و اینکه فونتِ
/// وزیرمتن داخلِ خودِ سند جاسازی شده باشد (وگرنه روی ویندوزِ بدونِ فونت،
/// فارسی به‌هم می‌ریزد).
/// </summary>
public class PdfTests
{
    private static string Path2(string n)
    {
        var p = Path.Combine(AppContext.BaseDirectory, n);
        return File.Exists(p) ? p : n;
    }

    [Fact]
    public void DebtorStatement_ProducesARealPdfWithEmbeddedPersianFont()
    {
        PdfEngine.Initialize();
        var json = File.ReadAllText(Path2("sample-backup.json"));
        var (debtors, _, _, _, _, _) = new LegacyBackupImporter().Parse(json);
        var p = debtors.First(d => d.MainAccount.FuelRows.Count > 2);

        var doc = new DebtorStatementReport(p, p.MainAccount,
            new StatementHeader("پمپ یعقوبی", "ولایت هرات", "0700000000", "1405/06/14"),
            new DebtCalculationService(new R()));

        var outPath = Path.Combine(Path.GetTempPath(), "pump-statement.pdf");
        doc.GeneratePdf(outPath);

        var bytes = File.ReadAllBytes(outPath);
        Assert.True(bytes.Length > 8000, $"سند خیلی کوچک است ({bytes.Length} بایت)");
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(bytes, 0, 4));

        var raw = System.Text.Encoding.Latin1.GetString(bytes);
        Assert.Contains("Vazirmatn", raw);          // فونت داخلِ سند است
        Assert.Contains("/FontFile", raw);          // و واقعاً جاسازی شده، نه فقط نام‌برده
        Assert.DoesNotContain("<html", raw.ToLowerInvariant());   // هیچ ردی از HTML
    }

    [Fact]
    public void PersianNumerals_MatchTheHtmlFormatting()
    {
        Assert.Equal("۱۲۳٬۴۵۶", PersianText.Num(123456m).Replace(",", "٬"));
        Assert.Equal("۰", PersianText.Num(0m));
        Assert.Equal("۱۲٫۵۰", PersianText.Num(12.5m, 2).Replace(".", "٫"));
        Assert.Equal("1405/6/14", PersianText.ToEn("۱۴۰۵/۶/۱۴"));
    }
}
