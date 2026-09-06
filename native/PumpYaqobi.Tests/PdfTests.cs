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

    /// <summary>
    /// سند واقعاً ساخته می‌شود — نه اینکه فقط کامپایل شود. خروجی روی دیسک
    /// نوشته و بازرسی می‌شود: هدرِ PDF، اندازهٔ معقول، و مهم‌تر از همه اینکه
    /// فونتِ وزیرمتن داخلِ خودِ سند جاسازی شده باشد — وگرنه روی ویندوزی که آن
    /// فونت را ندارد، فارسیِ ورق به‌هم می‌ریزد.
    ///
    /// آخرین ادعا هم عمدی است: هیچ ردی از HTML نباید در سند باشد. کلِ برنامه
    /// برای همین بازنویسی شد.
    /// </summary>
    [Fact]
    public void DebtorStatement_ProducesARealPdfWithEmbeddedPersianFont()
    {
        PdfEngine.Initialize();
        var json = File.ReadAllText(Path2("sample-backup.json"));
        var (debtors, _, _, _, _, _) = new LegacyBackupImporter().Parse(json);
        var p = debtors.First(d => d.MainAccount.FuelRows.Count > 2);

        var input = new DebtorStatementInput(
            PersonName: p.Name,
            AccountTitle: "حساب " + p.Name,
            IsMoneyLedger: false,
            Filter: null,
            Rows: p.MainAccount.FuelRows,
            PercentPetrol: 0m, PercentDiesel: 0m,
            RasidPetrol: 0m, RasidDiesel: 0m,
            BordPetrol: 0m, BordDiesel: 0m,
            RasidRowsPetrol: 0m, RasidRowsDiesel: 0m,
            Dates: "1405/06/14");

        var doc = new DebtorStatementReport(input, new DebtCalculationService(new R()));

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

    /// <summary>
    /// عددهای ورقِ چاپ.
    ///
    /// ⚠️ این آزمون یک‌بار برعکس نوشته شده بود و انتظارِ رقمِ فارسی داشت. ولی
    /// ‎n2fa‎ در نسخهٔ وب دقیقاً ‎Number(n).toLocaleString('en-US')‎ است، یعنی
    /// رقمِ لاتین با جداکنندهٔ هزار. با رقمِ فارسی، ورقِ برنامهٔ کامپیوتری با
    /// ورقِ خودِ نسخهٔ وب فرق می‌کرد — و همان هم شد و صاحب ریپو گزارشش کرد.
    /// پس این‌جا عمداً «3,356» انتظار می‌رود، نه «۳٬۳۵۶».
    /// </summary>
    [Fact]
    public void SheetNumbersUseLatinDigits_JustLikeTheWeb()
    {
        Assert.Equal("123,456", PersianText.Num(123456m));
        Assert.Equal("0", PersianText.Num(0m));
        Assert.Equal("12.50", PersianText.Num(12.5m, 2));
        // وزنِ تن همیشه سه رقمِ اعشار دارد
        Assert.Equal("1.500", PersianText.Ton(1.5m));
        // ورودیِ کاربر می‌تواند رقمِ فارسی/عربی باشد — آن یکی باید لاتین شود
        Assert.Equal("1405/6/14", PersianText.ToEn("۱۴۰۵/۶/۱۴"));
        Assert.Equal("1405/6/14", PersianText.ToEn("١٤٠٥/٦/١٤"));
    }
}
