using PumpYaqobi.Application.Services;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace PumpYaqobi.Reporting.Pdf;

/// <summary>ورودیِ سندِ حسابِ قرض‌دار — همان چیزی که ‎pdfPerson()‎ لازم داشت.</summary>
public sealed record DebtorStatementInput(
    string PersonName,
    string AccountTitle,
    bool IsMoneyLedger,
    FuelType? Filter,
    IReadOnlyList<DebtRow> Rows,
    decimal PercentPetrol, decimal PercentDiesel,
    decimal RasidPetrol, decimal RasidDiesel,
    decimal BordPetrol, decimal BordDiesel,
    decimal RasidRowsPetrol, decimal RasidRowsDiesel,
    string Dates);

/// <summary>
/// ══ سندِ حسابِ قرض‌دار ═══════════════════════════════════════════════════════
/// بازسازیِ مو‌به‌موی ‎pdfPerson()‎ — همان سربرگ، همان دو کادرِ «حساب پطرول» و
/// «حساب دیزل» با چهار خانه‌شان، همان ده ستون و همان ردیفِ «جمله».
///
/// ⚠️ علامتِ «الباقی» قرینه نوشته می‌شود (‎-rem‎): روی صفحه هم همین‌طور است.
/// یک‌بار عددِ خام چاپ شد و حسابی که ۸٬۸۷۹ لیتر داشت در ورق «‎-8,944‎» دیده شد.
///
/// ⚠️ ستونِ «الباقی» در ردیف‌ها عمداً خالی است — در خودِ جدولِ برنامه هم خالی
/// است («عددِ ردیف‌به‌ردیف جلو نیاید، جمع شود»).
/// </summary>
public sealed class DebtorStatementReport : IDocument
{
    private readonly DebtorStatementInput _in;
    private readonly DebtCalculationService _calc;

    public DebtorStatementReport(DebtorStatementInput input, DebtCalculationService calc)
    { _in = input; _calc = calc; }

    private bool ShowFuelColumn => _in.Filter is null;
    private string Unit => _in.IsMoneyLedger ? "افغانی" : "لیتر";
    private string RasidLabel => _in.IsMoneyLedger ? "مقدار رسید پول" : "مقدار رسید تیل";
    private string RemLabel => _in.IsMoneyLedger ? "الباقی پول" : "الباقی تیل";

    private static decimal R2(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);

    public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

    public void Compose(IDocumentContainer container)
    {
        var title = "⛽ پمپ یعقوبی — " + _in.AccountTitle;
        var sub = "حساب قرض‌دار — واحد " + (_in.IsMoneyLedger ? "پول" : "تیل")
                  + _in.Filter switch
                  {
                      FuelType.Petrol => " — حساب جداگانه ⛽ پطرول",
                      FuelType.Diesel => " — حساب جداگانه 🟤 دیزل",
                      _ => "",
                  };

        DocStyle.Compose(container, title, sub, _in.Dates, Body);
    }

    private void Body(IContainer c) => c.Column(col =>
    {
        // ── دو کادرِ حسابِ پطرول و دیزل ──────────────────────────────────────
        if (_in.Filter is null or FuelType.Petrol)
            col.Item().PaddingBottom(6).Element(x => AccountBox(x, FuelType.Petrol));
        if (_in.Filter is null or FuelType.Diesel)
            col.Item().PaddingBottom(6).Element(x => AccountBox(x, FuelType.Diesel));

        col.Item().PaddingTop(4).Element(Table);
    });

    /// <summary>کادرِ یک حساب: نامِ کادر سمتِ راست، چهار خانهٔ آمار کنارش.</summary>
    private void AccountBox(IContainer c, FuelType fuel)
    {
        var petrol = fuel == FuelType.Petrol;
        var pct = petrol ? _in.PercentPetrol : _in.PercentDiesel;
        var rasid = petrol ? _in.RasidPetrol : _in.RasidDiesel;
        var bord = petrol ? _in.BordPetrol : _in.BordDiesel;
        var rasidRows = petrol ? _in.RasidRowsPetrol : _in.RasidRowsDiesel;

        var comm = rasid * pct / 100m;
        // ‎rem = برد + فیصدی − رسید − رسیدِ ردیف‌ها‎ ، و روی ورق قرینه‌اش نوشته می‌شود
        var rem = bord + comm - rasid - (_in.IsMoneyLedger ? rasidRows : 0m);
        var show = -rem;

        var line = petrol ? DocStyle.Petrol : DocStyle.Diesel;

        c.Border(1.2f).BorderColor(line).Padding(6).Row(row =>
        {
            row.ConstantItem(112).AlignMiddle().Border(1).BorderColor(line).Padding(5).AlignCenter()
               .Text(petrol ? "⛽ حساب پطرول" : "🟤 حساب دیزل")
               .FontSize(DocStyle.BoxValue).Bold().FontColor(line);

            row.RelativeItem().PaddingRight(6).Row(g =>
            {
                g.RelativeItem().PaddingLeft(4).Element(x => DocStyle.SumBox(x,
                    $"فیصدی ما ({PersianText.Num(pct)}٪)", PersianText.Num(R2(comm)) + " " + Unit, DocStyle.Danger));
                g.RelativeItem().PaddingLeft(4).Element(x => DocStyle.SumBox(x,
                    RasidLabel, PersianText.Num(R2(rasid)) + " " + Unit, DocStyle.Money));
                g.RelativeItem().PaddingLeft(4).Element(x => DocStyle.SumBox(x,
                    "برد", PersianText.Num(R2(bord)) + " " + Unit, DocStyle.Hawala));
                g.RelativeItem().Element(x => DocStyle.SumBox(x,
                    RemLabel, PersianText.Num(R2(show)) + " " + Unit, DocStyle.Blue));
            });
        });
    }

    private void Table(IContainer c) => c.Table(t =>
    {
        t.ColumnsDefinition(cd =>
        {
            cd.ConstantColumn(34);    // # — جا برای سه رقم، وگرنه «10» دو خطی می‌شود
            cd.ConstantColumn(66);    // تاریخ
            cd.RelativeColumn(2);     // نام
            cd.ConstantColumn(48);    // حواله
            if (ShowFuelColumn) cd.ConstantColumn(60);   // نوع تیل
            cd.ConstantColumn(52);    // مقدار تیل
            cd.ConstantColumn(44);    // فی لیتر
            cd.ConstantColumn(66);    // مقدار بردگی
            cd.ConstantColumn(56);    // رسید / رسید تیل
            cd.ConstantColumn(56);    // الباقی
        });

        t.Header(h =>
        {
            void Th(string s) => DocStyle.ThText(h.Cell(), s);
            Th("#"); Th("تاریخ"); Th("نام"); Th("حواله");
            if (ShowFuelColumn) Th("نوع تیل");
            Th("مقدار تیل"); Th("فی لیتر"); Th("مقدار بردگی");
            Th(_in.IsMoneyLedger ? "رسید" : "رسید تیل");
            Th("الباقی");
        });

        var i = 0;
        foreach (var r in _in.Rows)
        {
            var even = i % 2 == 1;
            var n = ++i;
            void Td(string s, string? color = null) => DocStyle.TdText(t.Cell(), even, s, color);

            var bardagi = _calc.RowBardagi(r);

            Td(PersianText.Num(n), DocStyle.Index);
            Td(DocStyle.Dash(r.DateShamsi));
            Td(DocStyle.Dash(r.Name));
            Td(DocStyle.Dash(r.Hawala), DocStyle.Hawala);
            if (ShowFuelColumn) Td(r.Fuel == FuelType.Diesel ? "🟤 دیزل" : "⛽ پطرول");
            Td(r.Liters > 0m ? PersianText.Num(r.Liters) : "—", DocStyle.Fuel);
            Td((r.PricePerLiter ?? 0m) > 0m ? PersianText.Num(r.PricePerLiter!.Value) : "—");
            Td(PersianText.Num(Math.Round(bardagi)), DocStyle.Hawala);
            if (_in.IsMoneyLedger)
                Td(r.Rasid > 0m ? PersianText.Num(r.Rasid) : "—", DocStyle.Money);
            else
                Td(r.RasidFuel > 0m ? PersianText.Num(r.RasidFuel) + " لیتر" : "—", DocStyle.Fuel);
            Td("");     // ⚠️ عمداً خالی — مثلِ خودِ جدولِ برنامه
        }

        // ── ردیفِ «جمله» ────────────────────────────────────────────────────
        var ft = _calc.SplitTotals(_in.Rows).All;
        DocStyle.Tf(t.Cell().ColumnSpan((uint)(ShowFuelColumn ? 5 : 4)), "جمله");
        DocStyle.Tf(t.Cell(), PersianText.Num(ft.Liters));
        DocStyle.Tf(t.Cell(), "—");
        DocStyle.Tf(t.Cell(), PersianText.Num(ft.Bardagi));
        DocStyle.Tf(t.Cell(), PersianText.Num(_in.IsMoneyLedger ? ft.Rasid : ft.RasidFuel));
        DocStyle.Tf(t.Cell(), PersianText.Num(ft.Albaqi));
    });
}
