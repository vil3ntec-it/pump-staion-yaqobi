using PumpYaqobi.Application.Localization;
using PumpYaqobi.Application.Services;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace PumpYaqobi.Reporting.Pdf;

/// <param name="ProfitLocked">بی رمزِ «مفاد/ضرر» خانه‌های مفاد و نتیجهٔ خالص «🔒» چاپ می‌شوند، نه صفر.</param>
public sealed record PumpMonthlyInput(
    string MonthLabel,
    MonthReport Month,
    TankMonth Petrol,
    TankMonth Diesel,
    bool ProfitLocked,
    string Dates);

/// <summary>
/// ══ شورا، چ۵ — «گزارشِ ماهانهٔ پمپ» (برای بانک و اتحادیه) ═══════════════════════
///
/// فقط‌خواندنی. سه بخش: فروش (از همان <see cref="MonthReport"/>ِ گزارشِ پایانِ ماه)،
/// خرید، و مخزنِ هر تیل (موجودیِ اول ⇐ خرید ⇐ فروش ⇐ اصلاحِ میله‌زنی ⇐ موجودیِ
/// آخر، از <see cref="PumpMonthlyService"/>). ⛔ هیچ عددی این‌جا حساب نمی‌شود جز
/// جمعِ دو تیل؛ رنگ و اندازه فقط از <see cref="DocStyle"/>.
/// </summary>
public sealed class PumpMonthlyReport : ISetupDocument
{
    private readonly PumpMonthlyInput _in;

    public PumpMonthlyReport(PumpMonthlyInput input) => _in = input;

    public PageSetup Setup { get; set; } = PageSetup.Default;

    public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

    public void Compose(IDocumentContainer container) =>
        DocStyle.Compose(container, "📑 " + PumpBrand.Name + " — گزارشِ ماهانهٔ پمپ",
                         _in.MonthLabel, _in.Dates, Body, setup: Setup);

    private static string R(decimal v) =>
        PersianText.Num(Math.Round(v, 0, MidpointRounding.AwayFromZero));

    private void Body(IContainer c) => c.Column(col =>
    {
        var m = _in.Month;

        col.Item().Text("💰 فروش").FontSize(DocStyle.HeadSize + 1).Bold().FontColor(DocStyle.Ink(DocStyle.Title));
        col.Item().PaddingTop(4).Table(t =>
        {
            t.ColumnsDefinition(cd => { cd.RelativeColumn(2f); cd.RelativeColumn(2f); cd.RelativeColumn(2f); });
            DocStyle.ThText(t.Cell(), "تیل");
            DocStyle.ThText(t.Cell(), "لیتر");
            DocStyle.ThText(t.Cell(), "افغانی");
            var i = 0;
            void Row(string a, string b, string cc, string? color = null)
            {
                var even = i++ % 2 == 1;
                DocStyle.TdText(t.Cell(), even, a, color);
                DocStyle.TdText(t.Cell(), even, b);
                DocStyle.TdText(t.Cell(), even, cc);
            }
            Row("⛽ پطرول", R(m.Petrol.Liters), R(m.Petrol.Amount), DocStyle.Petrol);
            Row("🟤 دیزل", R(m.Diesel.Liters), R(m.Diesel.Amount), DocStyle.Diesel);
            Row("جمله", R(m.Liters), R(m.Sales));
        });

        col.Item().PaddingTop(12).Text("🛒 خرید").FontSize(DocStyle.HeadSize + 1).Bold().FontColor(DocStyle.Ink(DocStyle.Title));
        col.Item().PaddingTop(4).Table(t =>
        {
            t.ColumnsDefinition(cd => { cd.RelativeColumn(2f); cd.RelativeColumn(2f); cd.RelativeColumn(2f); });
            DocStyle.ThText(t.Cell(), "تیل");
            DocStyle.ThText(t.Cell(), "لیتر");
            DocStyle.ThText(t.Cell(), "افغانی");
            DocStyle.TdText(t.Cell(), false, "⛽ پطرول", DocStyle.Petrol);
            DocStyle.TdText(t.Cell(), false, R(m.BuyPetrol.Liters));
            DocStyle.TdText(t.Cell(), false, R(m.BuyPetrol.Amount));
            DocStyle.TdText(t.Cell(), true, "🟤 دیزل", DocStyle.Diesel);
            DocStyle.TdText(t.Cell(), true, R(m.BuyDiesel.Liters));
            DocStyle.TdText(t.Cell(), true, R(m.BuyDiesel.Amount));
        });

        col.Item().PaddingTop(12).Text("🛢️ مخزن (لیتر)").FontSize(DocStyle.HeadSize + 1).Bold().FontColor(DocStyle.Ink(DocStyle.Title));
        col.Item().PaddingTop(4).Table(t =>
        {
            t.ColumnsDefinition(cd =>
            {
                cd.RelativeColumn(1.6f);
                for (var k = 0; k < 5; k++) cd.RelativeColumn(2f);
            });
            foreach (var h in new[] { "تیل", "اولِ ماه", "خرید", "فروش", "میله‌زنی", "آخرِ ماه" })
                DocStyle.ThText(t.Cell(), h);
            var even = false;
            foreach (var (label, color, tm) in new[] { ("⛽ پطرول", DocStyle.Petrol, _in.Petrol),
                                                        ("🟤 دیزل", DocStyle.Diesel, _in.Diesel) })
            {
                DocStyle.TdText(t.Cell(), even, label, color);
                DocStyle.TdText(t.Cell(), even, R(tm.Opening));
                DocStyle.TdText(t.Cell(), even, R(tm.Bought));
                DocStyle.TdText(t.Cell(), even, R(tm.Sold));
                DocStyle.TdText(t.Cell(), even, DocStyle.DashNum(Math.Round(tm.DipAdjust, 0)));
                DocStyle.TdText(t.Cell(), even, R(tm.Closing), tm.Closing < 0m ? DocStyle.Danger : null);
                even = !even;
            }
        });

        col.Item().PaddingTop(12).Text("📊 خلاصه").FontSize(DocStyle.HeadSize + 1).Bold().FontColor(DocStyle.Ink(DocStyle.Title));
        col.Item().PaddingTop(4).Table(t =>
        {
            t.ColumnsDefinition(cd => { cd.RelativeColumn(2.4f); cd.RelativeColumn(2.6f); });
            var i = 0;
            void Row(string a, string b, string? color = null)
            {
                var even = i++ % 2 == 1;
                DocStyle.TdText(t.Cell(), even, a);
                DocStyle.TdText(t.Cell(), even, b, color);
            }
            Row("📈 مفاد ثبت‌شده", _in.ProfitLocked ? "🔒" : R(m.Profit) + " افغانی");
            Row("🛒 درآمد اضافی", R(m.Extra) + " افغانی");
            Row("💸 مصارف", R(m.Expenses) + " افغانی", DocStyle.Danger);
            Row("🧾 رسید نقدی قرض‌داران", R(m.Rasid) + " افغانی", DocStyle.Money);
            Row("⚖️ نتیجهٔ خالص",
                _in.ProfitLocked ? "🔒" : R(m.Net) + " افغانی — " + (m.Net >= 0m ? "مفاد" : "ضرر"),
                _in.ProfitLocked ? null : m.Net >= 0m ? "#059669" : "#e11d48");
        });
    });
}
