using PumpYaqobi.Application.Services;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;
using PumpYaqobi.Reporting.Pdf;
using QuestPDF.Fluent;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ شورا، چ۵ — «گزارشِ ماهانهٔ پمپ» و «پروندهٔ قرض‌دار» ═════════════════════════
/// عددها از سرویس‌های خالص سنجیده می‌شوند (اولِ ماه + خرید − فروش + میله‌زنی =
/// آخرِ ماه، و پلِ آخرِ ماهِ جاری با عددِ خودِ بخشِ مخزن)، و هر دو سند واقعاً
/// ساخته، با وزیرمتنِ جاسازی‌شده، و به تصویر درمی‌آیند.
/// </summary>
public class PumpReportsTests
{
    private static FuelPurchase Buy(string? d, decimal l, FuelType f = FuelType.Petrol) =>
        new() { DateShamsi = d, Liters = l, Fuel = f };

    private static ParchaReport Sold(string? d, decimal day, decimal night = 0m, FuelType f = FuelType.Petrol) =>
        new() { DateShamsi = d, Fuel = f, DayShift = new ShiftData { Sale = day }, NightShift = new ShiftData { Sale = night } };

    private static List<FuelPurchase> Purchases() => new()
    {
        Buy("1405/05/10", 1000m),
        Buy("1405/06/03", 500m),
        Buy("1405/07/01", 300m),
        Buy(null, 200m),                          // بی‌تاریخ ⇒ «پیش از همه»
        Buy("1405/06/04", 9999m, FuelType.Diesel), // تیلِ دیگر
    };

    private static List<ParchaReport> Reports() => new()
    {
        Sold("1405/05/20", 100m),
        Sold("1405/06/10", 250m, 50m),
        Sold("1405/07/02", 400m),
        Sold("1405/06/11", 777m, 0m, FuelType.Diesel),
    };

    private static List<TankDip> Dips() => new()
    {
        new() { DateShamsi = "1405/06/20", Fuel = FuelType.Petrol, BookAdjust = -20m },
        new() { DateShamsi = "1405/05/02", Fuel = FuelType.Petrol, BookAdjust = 5m },
    };

    [Fact]
    public void Makhzan_AvvalVaAkharMah_Dorost()
    {
        var t = new PumpMonthlyService().Tank(FuelType.Petrol, "1405/06", Purchases(), Reports(), Dips());
        Assert.Equal(1000m + 200m - 100m + 5m, t.Opening);
        Assert.Equal(500m, t.Bought);
        Assert.Equal(300m, t.Sold);
        Assert.Equal(-20m, t.DipAdjust);
        Assert.Equal(t.Opening + t.Bought - t.Sold + t.DipAdjust, t.Closing);
        Assert.Equal(1285m, t.Closing);
    }

    /// <summary>آخرِ ماهی که پس از آن چیزی نیست = همان موجودیِ بخشِ مخزن (یک فرمول).</summary>
    [Fact]
    public void AkharMahJari_HamanAdadBakhsheMakhzan()
    {
        var all = new StorageService().Tank(
            Purchases().Where(p => p.Fuel == FuelType.Petrol),
            Reports().Where(r => r.Fuel == FuelType.Petrol), 0m,
            Dips().Where(d => d.Fuel == FuelType.Petrol));
        var t = new PumpMonthlyService().Tank(FuelType.Petrol, "1405/07", Purchases(), Reports(), Dips());
        Assert.Equal(all.Current, t.Closing);
        // و اولِ ماهِ بعد همان آخرِ ماهِ قبل است
        var jun = new PumpMonthlyService().Tank(FuelType.Petrol, "1405/06", Purchases(), Reports(), Dips());
        Assert.Equal(jun.Closing, t.Opening);
    }

    private static List<DebtRow> MoneyRows() => new()
    {
        new() { DateShamsi = "1405/05/01", Bardagi = 1000m },
        new() { DateShamsi = "1405/05/11", Rasid = 300m },
        new() { DateShamsi = "1405/06/01", Rasid = 200m, Bardagi = 500m },
        new() { DateShamsi = "1405/06/01", Rasid = 100m },
        new() { DateShamsi = null, Bardagi = 50m },
        new() { DateShamsi = "1405/06/02", Liters = 999m, RasidFuel = 999m }, // مالِ دفترِ تیل
    };

    [Fact]
    public void Parvande_DaftarePool()
    {
        var d = DebtorDossierService.Build(MoneyRows(), isMoney: true);
        Assert.Equal(1550m, d.Taken);
        Assert.Equal(600m, d.Paid);
        Assert.Equal(3, d.ReceiptCount);
        Assert.Equal(200m, d.AvgReceipt);
        Assert.Equal(21m, d.AvgGapDays);               // ۱۱ اسد ⇐ ۱ سنبله؛ دو رسیدِ یک روز یک روز است
        Assert.Equal("1405/06/01", d.LastReceiptDate);
        Assert.Equal("1405/05/01", d.FirstDate);
        Assert.Equal(new[] { "1405/06", "1405/05", MonthReportService.NoDate }, d.Months.Select(m => m.Key));
        Assert.Equal(new DossierMonth("1405/06", 500m, 300m, 2), d.Months[0]);
        Assert.Equal(new DossierMonth("1405/05", 1000m, 300m, 1), d.Months[1]);
    }

    [Fact]
    public void Parvande_DaftareTil_LitrVaRasideTil()
    {
        var d = DebtorDossierService.Build(MoneyRows(), isMoney: false);
        Assert.Equal(999m, d.Taken);
        Assert.Equal(999m, d.Paid);
        Assert.Equal(1, d.ReceiptCount);
        Assert.Null(d.AvgGapDays);                       // یک روزِ رسید ⇒ فاصله‌ای نیست
        Assert.Single(d.Months);
    }

    [Fact]
    public void Parvande_Khali()
    {
        var d = DebtorDossierService.Build(new List<DebtRow>(), true);
        Assert.Empty(d.Months);
        Assert.Equal(0, d.ReceiptCount);
        Assert.Equal(0m, d.AvgReceipt);
        Assert.Null(d.LastReceiptDate);
    }

    // ── سندها ────────────────────────────────────────────────────────────
    private static void Check(QuestPDF.Infrastructure.IDocument doc, string name)
    {
        PdfEngine.Initialize();
        var dir = Path.Combine(Path.GetTempPath(), "pump-pdf");
        Directory.CreateDirectory(dir);
        var pdf = Path.Combine(dir, name + ".pdf");
        doc.GeneratePdf(pdf);
        var bytes = File.ReadAllBytes(pdf);
        Assert.True(bytes.Length > 3000, $"{name}: سند خیلی کوچک است ({bytes.Length})");
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(bytes, 0, 4));
        var raw = System.Text.Encoding.Latin1.GetString(bytes);
        Assert.Contains("Vazirmatn", raw);
        Assert.Contains("/FontFile", raw);
        Assert.NotEmpty(doc.GenerateImages());
    }

    private const string Dates = "1405/06/15  ·  1448/03/24  ·  2026/09/06";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void GozaresheMahane_SakhteMishavad(bool locked)
    {
        var src = new MonthReportSource(Reports(), new List<Expense>(), new List<ExtraIncome>(), Purchases(),
            new List<DebtQuickReceipt>(), new List<SafeEntry>(), new List<TankerUnload>());
        var month = new MonthReportService().Compute(src, "1405/06");
        var pm = new PumpMonthlyService();
        var input = new PumpMonthlyInput("سنبله 1405", month,
            pm.Tank(FuelType.Petrol, "1405/06", Purchases(), Reports(), Dips()),
            pm.Tank(FuelType.Diesel, "1405/06", Purchases(), Reports(), Dips()), locked, Dates);
        Check(new PumpMonthlyReport(input), "pump-monthly" + (locked ? "-locked" : ""));
    }

    [Fact]
    public void Parvande_SakhteMishavad()
    {
        var input = new DebtorDossierInput("حساب کریم", true, DebtorDossierService.Build(MoneyRows(), true),
                                           950m, 0m, Dates);
        Check(new DebtorDossierReport(input), "debtor-dossier");
        var empty = new DebtorDossierInput("حساب تازه", false, DebtorDossierService.Build(new List<DebtRow>(), false),
                                           0m, 0m, Dates);
        Check(new DebtorDossierReport(empty), "debtor-dossier-empty");
    }
}
