using PumpYaqobi.Application.Services;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;
using PumpYaqobi.Reporting.Pdf;
using QuestPDF.Fluent;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// سندهای چاپ/PDF — هم ساخته شوند و هم صفحه‌هایشان به تصویر درآیند.
/// تصویرها در پوشهٔ خروجی می‌مانند تا با چشم با ورقِ نسخهٔ وب سنجیده شوند.
/// </summary>
public class PdfLookTests
{
    private static string OutDir
    {
        get
        {
            var d = Environment.GetEnvironmentVariable("PUMP_PDF_OUT")
                    ?? Path.Combine(Path.GetTempPath(), "pump-pdf");
            Directory.CreateDirectory(d);
            return d;
        }
    }

    private static DebtCalculationService Debt() => new(new Rates());

    private sealed class Rates : IUnionRateProvider
    {
        public decimal UnionRate(FuelType fuel) => 62m;
    }

    private static List<DebtRow> SampleRows()
    {
        var rows = new List<DebtRow>();
        var data = new (string date, string name, string hawala, FuelType f, decimal l, decimal p, decimal rasid)[]
        {
            ("1401/6/30", "تانکر", "902", FuelType.Petrol, 108, 59, 3356),
            ("1401/8/17", "—",     "866", FuelType.Petrol, 84,  60, 2472),
            ("1402/4/5",  "مزدا",  "557", FuelType.Petrol, 226, 66, 0),
            ("1402/4/9",  "تراکتور","941", FuelType.Petrol, 374, 65, 0),
            ("1402/8/28", "مزدا",  "739", FuelType.Diesel, 138, 60, 0),
            ("1403/7/12", "سراچه", "574", FuelType.Petrol, 91,  67, 4543),
            ("1403/9/26", "بس",    "981", FuelType.Diesel, 162, 58, 0),
            ("1404/1/16", "هایلکس","673", FuelType.Diesel, 93,  58, 0),
        };
        foreach (var d in data)
            rows.Add(new DebtRow
            {
                DateShamsi = d.date, Name = d.name == "—" ? null : d.name, Hawala = d.hawala,
                Fuel = d.f, Liters = d.l, PricePerLiter = d.p, Rasid = d.rasid,
            });
        for (var i = 0; i < 12; i++)
            rows.Add(new DebtRow { DateShamsi = "1405/5/26", Fuel = FuelType.Petrol });
        return rows;
    }

    [Fact]
    public void DebtorStatement_RendersAndLooksLikeTheHtmlSheet()
    {
        PdfEngine.Initialize();
        var calc = Debt();
        var rows = SampleRows();
        foreach (var r in rows) calc.NormalizeRow(r);

        var input = new DebtorStatementInput(
            PersonName: "احمد ولی",
            AccountTitle: "حساب احمد ولی",
            IsMoneyLedger: true,
            Filter: null,
            Rows: rows,
            PercentPetrol: 0m, PercentDiesel: 4m,
            RasidPetrol: 225893m, RasidDiesel: 0m,
            BordPetrol: 56790m, BordDiesel: 23070m,
            RasidRowsPetrol: 0m, RasidRowsDiesel: 0m,
            Dates: "1405/6/15  ·  1448/03/24  ·  2026/09/06");

        var doc = new DebtorStatementReport(input, calc);
        var pdf = Path.Combine(OutDir, "debtor.pdf");
        doc.GeneratePdf(pdf);
        Assert.True(new FileInfo(pdf).Length > 4000);

        var i = 0;
        foreach (var img in doc.GenerateImages())
        {
            File.WriteAllBytes(Path.Combine(OutDir, $"debtor-{++i}.png"), img);
            if (i >= 2) break;
        }
        Assert.True(i >= 1);
    }

    [Fact]
    public void ExchangeSheet_RendersAndLooksLikeTheHtmlSheet()
    {
        PdfEngine.Initialize();
        var rows = new List<ExchangeRow>();
        for (var k = 1; k <= 21; k++)
            rows.Add(new ExchangeRow
            {
                DateShamsi = "1405/6/2",
                Description = null,
                Amount = 0, Rate = 0, Bardagi = 0,
                Currency = ExchangeCurrency.Toman,
            });

        var doc = new ExchangeReport(
            new ExchangeReportInput("سنبله 1405", rows, "1405/6/15  ·  1448/03/24  ·  2026/09/06"),
            new ExchangeService());

        var pdf = Path.Combine(OutDir, "exchange.pdf");
        doc.GeneratePdf(pdf);
        Assert.True(new FileInfo(pdf).Length > 3000);

        var n = 0;
        foreach (var img in doc.GenerateImages())
        {
            File.WriteAllBytes(Path.Combine(OutDir, $"exchange-{++n}.png"), img);
            break;
        }
        Assert.True(n >= 1);
    }
}
