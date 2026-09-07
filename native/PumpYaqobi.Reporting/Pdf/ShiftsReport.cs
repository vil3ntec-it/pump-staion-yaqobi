using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace PumpYaqobi.Reporting.Pdf;

/// <param name="Reports">
/// همهٔ گزارش‌ها — پطرول و دیزل با هم. ورق خودش بر اساسِ تاریخ گروه می‌کند،
/// همان کاری که ‎pdfShifts‎ با ‎getDieselForDate‎ می‌کرد.
/// </param>
/// <param name="DieselOnly">
/// ورقِ ‎pdfDieselShifts‎: فقط دیزل، بی بخشِ پطرول. وگرنه ورقِ ‎pdfShifts‎ است
/// که پطرولِ هر روز را با دیزلِ همان روز کنارِ هم می‌آورد.
/// </param>
public sealed record ShiftsReportInput(
    IReadOnlyList<ParchaReport> Reports,
    string Dates,
    bool DieselOnly = false);

/// <summary>
/// ══ سندِ پارچه‌ها ═══════════════════════════════════════════════════════════
/// بازسازیِ ‎pdfShifts()‎ / ‎pdfDieselShifts()‎: برای هر تاریخ یک کارت، با
/// شیفتِ روز و شب کنارِ هم، و نوارِ «جمله کل ۲۴ ساعت» زیرشان.
///
/// ⚠️ بومِ ورق سفید است، نه سیاه — گلایهٔ صریحِ صاحب ریپو: «گزارش‌های پی‌دی‌افِ
/// پارچه‌ها سیاه‌اند و ورق‌شان بی‌استفاده مانده.» کارت‌ها فشرده‌اند تا چند
/// گزارش در یک ورق بنشیند و تونر حرام نشود.
///
/// ⚠️ کارتِ هر روز عمداً نمی‌شکند (‎ShowEntire‎): نیمهٔ یک گزارش در ته ورق و
/// نیمهٔ دیگرش سرِ ورقِ بعد، همان چیزی بود که خواندنِ ورق را سخت می‌کرد.
/// </summary>
public sealed class ShiftsReport : ISetupDocument
{
    private const string Head = "#b7791f";
    private const string DayBg = "#fdf6e3";
    private const string NightBg = "#eef2f9";
    private const string Line = "#d6dae1";
    private const string Green = "#0f7a4d";
    private const string Red = "#b3261e";
    private const string Amber = "#b7791f";

    private readonly ShiftsReportInput _in;

    public ShiftsReport(ShiftsReportInput input) => _in = input;

    /// <summary>تنظیمِ ورق — از «کارگاه چاپ». پیش‌فرض همان ورقی است که همیشه بود.</summary>
    public PageSetup Setup { get; set; } = PageSetup.Default;

    public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

    private static string N(decimal v) =>
        PersianText.Num(Math.Round(v, 0, MidpointRounding.AwayFromZero));

    /// <summary>گزارش‌های یک تاریخ، به ترتیبی که در فهرست آمده‌اند.</summary>
    private List<IGrouping<string, ParchaReport>> ByDate() =>
        _in.Reports.GroupBy(r => r.DateShamsi ?? "—").ToList();

    public void Compose(IDocumentContainer container)
    {
        var days = ByDate();
        DocStyle.Compose(container,
                         (_in.DieselOnly ? "🟤" : "⛽") + " پمپ یعقوبی — گزارش‌های پارچه"
                         + (_in.DieselOnly ? " (دیزل)" : ""),
                         PersianText.Num(days.Count) + " روز · "
                         + PersianText.Num(_in.Reports.Count) + " گزارش",
                         _in.Dates, Body,
                         titleColor: _in.DieselOnly ? DocStyle.Diesel : Head, setup: Setup);
    }

    private void Body(IContainer c) => c.Column(col =>
    {
        var days = ByDate();
        if (days.Count == 0)
        {
            col.Item().Border(1).BorderColor(Line).Padding(16).AlignCenter()
               .Text("هیچ گزارشی ثبت نشده")
               .FontSize(DocStyle.CellSize).FontColor(DocStyle.FootFg);
            return;
        }

        var i = 0;
        foreach (var g in days)
        {
            var n = ++i;
            col.Item().PaddingBottom(8).ShowEntire()
               .Element(x => Card(x, g.Key, g.ToList(), n, days.Count));
        }
    });

    private void Card(IContainer c, string date, List<ParchaReport> reps, int index, int total) =>
        c.Border(1).BorderColor(Line).Padding(9).Column(card =>
    {
        var petrol = reps.Where(r => r.Fuel != FuelType.Diesel).ToList();
        var diesel = reps.Where(r => r.Fuel == FuelType.Diesel).ToList();

        var pDays = petrol.Select(r => r.DayShift).Where(s => s?.Name is { Length: > 0 }).ToList();
        var pNights = petrol.Select(r => r.NightShift).Where(s => s?.Name is { Length: > 0 }).ToList();
        var dDays = diesel.Select(r => r.DayShift).Where(s => s?.Name is { Length: > 0 }).ToList();
        var dNights = diesel.Select(r => r.NightShift).Where(s => s?.Name is { Length: > 0 }).ToList();

        var first = reps[0];
        // «کامل» یعنی هر دو شیفت ثبت شده‌اند — همان شرطِ نسخهٔ وب، روی همان
        // سوختی که ورق دربارهٔ آن است
        var done = _in.DieselOnly
            ? dDays.Count > 0 && dNights.Count > 0
            : pDays.Count > 0 && pNights.Count > 0;

        var head = _in.DieselOnly ? DocStyle.Diesel : Head;

        card.Item().BorderBottom(2).BorderColor(head).PaddingBottom(5).Row(row =>
        {
            row.RelativeItem().Column(h =>
            {
                h.Item().Text((_in.DieselOnly ? "🟤" : "⛽") + " پمپ یعقوبی — گزارش "
                              + PersianText.Num(index))
                 .FontSize(DocStyle.BoxValue).Bold().FontColor(head);
                h.Item().Text("📅 " + date + "  |  " + DocStyle.Dash(first.DateMiladi)
                              + "  |  " + DocStyle.Dash(first.DateQamari))
                 .FontSize(DocStyle.BoxLabel).FontColor(DocStyle.Sub);
            });
            row.ConstantItem(150).AlignMiddle().Row(s =>
            {
                s.RelativeItem().AlignLeft().Text(done ? "✅ کامل" : "⏳ ناقص")
                 .FontSize(DocStyle.BoxLabel).Bold().FontColor(done ? Green : Red);
                s.ConstantItem(52).AlignLeft()
                 .Text(PersianText.Num(index) + " / " + PersianText.Num(total))
                 .FontSize(DocStyle.BoxLabel).FontColor(DocStyle.Sub);
            });
        });

        // ── پطرول (در ورقِ دیزل اصلاً نمی‌آید) ────────────────────────────
        if (!_in.DieselOnly)
        {
            card.Item().PaddingTop(6).Text("⛽ پطرول")
                .FontSize(DocStyle.HeadSize).Bold().FontColor(DocStyle.Petrol);
            card.Item().PaddingTop(3).Row(row =>
            {
                row.RelativeItem().PaddingLeft(5).Element(x => Blocks(x, pDays,
                    pDays.Count > 1 ? "☀️ روز (پطرول)" : "☀️ شیفت روزانه (پطرول)", true));
                row.RelativeItem().Element(x => Blocks(x, pNights,
                    pNights.Count > 1 ? "🌙 شب (پطرول)" : "🌙 شیفت شبانه (پطرول)", false));
            });
        }

        // ── دیزل (فقط اگر همان روز ثبتی داشته باشد) ──────────────────────
        if (_in.DieselOnly || dDays.Count > 0 || dNights.Count > 0)
        {
            card.Item().PaddingTop(6).Text("🟤 دیزل")
                .FontSize(DocStyle.HeadSize).Bold().FontColor(DocStyle.Diesel);
            card.Item().PaddingTop(3).Row(row =>
            {
                row.RelativeItem().PaddingLeft(5).Element(x => Blocks(x, dDays,
                    dDays.Count > 1 ? "☀️ روز (دیزل)" : "☀️ شیفت روزانه (دیزل)", true));
                row.RelativeItem().Element(x => Blocks(x, dNights,
                    dNights.Count > 1 ? "🌙 شب (دیزل)" : "🌙 شیفت شبانه (دیزل)", false));
            });
        }

        // ── جمله‌ها ──────────────────────────────────────────────────────
        var pS = Sum(petrol, s => s.Sale);
        var pM = Sum(petrol, s => s.Money);
        var pP = Sum(petrol, s => s.Profit);
        var dS = Sum(diesel, s => s.Sale);
        var dM = Sum(diesel, s => s.Money);
        var dP = Sum(diesel, s => s.Profit);

        card.Item().PaddingTop(7).Column(sum =>
        {
            if (!_in.DieselOnly)
                sum.Item().Element(x => SumLine(x, "⛽ پطرول", pS, pM, pP, false, head));
            if (diesel.Count > 0)
                sum.Item().PaddingTop(_in.DieselOnly ? 0 : 3)
                   .Element(x => SumLine(x, "🟤 دیزل", dS, dM, dP, false, head));
            sum.Item().PaddingTop(3)
               .Element(x => SumLine(x, "جمله کل ۲۴ ساعت", pS + dS, pM + dM, pP + dP, true, head));
        });
    });

    private static decimal Sum(List<ParchaReport> reps, Func<ShiftData, decimal> pick) =>
        reps.Sum(r => (r.DayShift is null ? 0m : pick(r.DayShift))
                    + (r.NightShift is null ? 0m : pick(r.NightShift)));

    /// <summary>چند شیفتِ هم‌نوعِ یک روز، پشتِ هم. هیچ‌کدام نبود، «ثبت نشده».</summary>
    private void Blocks(IContainer c, List<ShiftData?> list, string label, bool day)
    {
        if (list.Count == 0) { Block(c, null, label, day); return; }
        c.Column(col =>
        {
            var i = 0;
            foreach (var s in list)
            {
                var n = ++i;
                var title = list.Count > 1 ? label + " #" + PersianText.Num(n) : label;
                col.Item().PaddingBottom(list.Count > 1 ? 4 : 0)
                   .Element(x => Block(x, s, title, day));
            }
        });
    }

    private void Block(IContainer c, ShiftData? s, string label, bool day) =>
        c.Border(1).BorderColor(Line).Column(col =>
    {
        col.Item().Background(day ? DayBg : NightBg).Padding(4).AlignCenter()
           .Text(label).FontSize(DocStyle.BoxLabel).Bold()
           .FontColor(day ? Amber : DocStyle.Blue);

        if (s is null || string.IsNullOrWhiteSpace(s.Name))
        {
            col.Item().Padding(10).AlignCenter().Text("ثبت نشده")
               .FontSize(DocStyle.BoxLabel).FontColor(DocStyle.FootFg);
            return;
        }

        void Line2(string l, string v, string? color = null) =>
            col.Item().BorderTop(1).BorderColor(DocStyle.CellLine).Padding(3).Row(r =>
            {
                r.RelativeItem().Text(l).FontSize(DocStyle.BoxLabel).FontColor(DocStyle.Sub);
                r.RelativeItem().AlignLeft().Text(v)
                 .FontSize(DocStyle.BoxLabel).Bold().FontColor(color ?? DocStyle.CellFg);
            });

        Line2("کارمند", s.Name + (s.PumpNum > 0 ? " (پایه #" + PersianText.Num(s.PumpNum) + ")" : ""));
        Line2("پایه (شروع ← ختم)", N(s.Start) + " ← " + N(s.End) + " لیتر");
        Line2("فروش", N(s.Sale) + " لیتر", DocStyle.Blue);
        Line2("فی فروش", N(s.Price) + " ؋", Amber);
        Line2("فی خرید", s.BuyPerLiter > 0m ? PersianText.Num(s.BuyPerLiter, 1) + " ؋" : "—", Red);
        Line2("فایده / لیتر", N(s.ProfitPer) + " ؋", Green);
        Line2("پول کل", N(s.Money) + " افغانی", Amber);
        Line2("فایده کل", N(s.Profit) + " افغانی", Green);
        if (s.Debt > 0m)
        {
            Line2("جمله قرض", N(s.Debt) + " افغانی", Red);
            Line2("💵 پول موجود", N(s.Available) + " افغانی", s.Available < 0m ? Red : Green);
        }
        if (!string.IsNullOrWhiteSpace(s.Note))
            col.Item().BorderTop(1).BorderColor(DocStyle.CellLine).Padding(3)
               .Text("📝 " + s.Note).FontSize(DocStyle.BoxLabel).FontColor(DocStyle.Sub);
    });

    private void SumLine(IContainer c, string title, decimal sale, decimal money,
                         decimal profit, bool grand, string head) =>
        c.Border(grand ? 1.4f : 1).BorderColor(grand ? head : Line).Padding(5).Row(row =>
    {
        row.ConstantItem(120).Text(title)
           .FontSize(DocStyle.BoxLabel).Bold().FontColor(grand ? head : DocStyle.Sub);

        void Cell(string l, string v, string color)
        {
            row.RelativeItem().Row(r =>
            {
                r.AutoItem().Text(l).FontSize(DocStyle.BoxLabel).FontColor(DocStyle.Sub);
                r.RelativeItem().PaddingRight(4).Text(v)
                 .FontSize(DocStyle.BoxLabel).Bold().FontColor(color);
            });
        }

        Cell("فروش: ", N(sale) + " لیتر", DocStyle.Blue);
        Cell("پول: ", N(money) + " افغانی", Amber);
        Cell("فایده: ", N(profit) + " افغانی", Green);
    });
}
