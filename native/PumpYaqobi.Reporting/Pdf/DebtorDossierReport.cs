using PumpYaqobi.Application.Localization;
using PumpYaqobi.Application.Services;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace PumpYaqobi.Reporting.Pdf;

/// <param name="RemainderPetrol">همان «الباقی»ِ سربرگِ حساب — صدا زننده می‌دهد، این‌جا حساب نمی‌شود.</param>
public sealed record DebtorDossierInput(
    string AccountTitle,
    bool IsMoneyLedger,
    DebtorDossier Dossier,
    decimal RemainderPetrol,
    decimal RemainderDiesel,
    string Dates);

/// <summary>
/// ══ شورا، چ۵ — «پروندهٔ قرض‌دار» ═══════════════════════════════════════════════
/// فقط‌خواندنی: حالِ فعلی (الباقیِ هر تیل، همان عددِ سربرگ)، میانگینِ رسید و
/// فاصلهٔ دو رسید، و تاریخچهٔ ماه‌به‌ماه از <see cref="DebtorDossierService"/>.
/// </summary>
public sealed class DebtorDossierReport : ISetupDocument
{
    private readonly DebtorDossierInput _in;

    public DebtorDossierReport(DebtorDossierInput input) => _in = input;

    public PageSetup Setup { get; set; } = PageSetup.Default;

    public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

    public void Compose(IDocumentContainer container) =>
        DocStyle.Compose(container, "📁 " + PumpBrand.Name + " — پروندهٔ قرض‌دار",
                         _in.AccountTitle, _in.Dates, Body, setup: Setup);

    private static string R(decimal v) =>
        PersianText.Num(Math.Round(v, 0, MidpointRounding.AwayFromZero));

    private string Unit => _in.IsMoneyLedger ? " افغانی" : " لیتر";

    private void Body(IContainer c) => c.Column(col =>
    {
        var d = _in.Dossier;

        col.Item().Text("📌 حالِ فعلی").FontSize(DocStyle.HeadSize + 1).Bold().FontColor(DocStyle.Ink(DocStyle.Title));
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
            Row("⛽ الباقی پطرول", R(_in.RemainderPetrol) + Unit, _in.RemainderPetrol > 0m ? DocStyle.Danger : DocStyle.Money);
            Row("🟤 الباقی دیزل", R(_in.RemainderDiesel) + Unit, _in.RemainderDiesel > 0m ? DocStyle.Danger : DocStyle.Money);
            Row("جمله برد", R(d.Taken) + Unit);
            Row("جمله رسید", R(d.Paid) + Unit, DocStyle.Money);
            Row("شمار رسیدها", PersianText.Num(d.ReceiptCount));
            Row("میانگین هر رسید", d.ReceiptCount == 0 ? "—" : R(d.AvgReceipt) + Unit);
            Row("میانگین فاصلهٔ دو رسید",
                d.AvgGapDays is { } g ? PersianText.Num(Math.Round(g, 1, MidpointRounding.AwayFromZero)) + " روز" : "—");
            Row("آخرین رسید", DocStyle.Dash(d.LastReceiptDate));
            Row("نخستین ردیف", DocStyle.Dash(d.FirstDate));
        });

        col.Item().PaddingTop(12).Text("🗓️ تاریخچهٔ ماه‌به‌ماه").FontSize(DocStyle.HeadSize + 1).Bold().FontColor(DocStyle.Ink(DocStyle.Title));
        col.Item().PaddingTop(4).Table(t =>
        {
            t.ColumnsDefinition(cd =>
            {
                cd.RelativeColumn(2f); cd.RelativeColumn(2f); cd.RelativeColumn(2f); cd.RelativeColumn(1.4f);
            });
            DocStyle.ThText(t.Cell(), "ماه");
            DocStyle.ThText(t.Cell(), "برد");
            DocStyle.ThText(t.Cell(), "رسید");
            DocStyle.ThText(t.Cell(), "شمار رسید");
            var even = false;
            foreach (var m in d.Months)
            {
                var label = m.Key == MonthReportService.NoDate ? m.Key : Shamsi.MonthLabel(m.Key);
                DocStyle.TdText(t.Cell(), even, label);
                DocStyle.TdText(t.Cell(), even, R(m.Taken));
                DocStyle.TdText(t.Cell(), even, R(m.Paid), DocStyle.Money);
                DocStyle.TdText(t.Cell(), even, PersianText.Num(m.Receipts));
                even = !even;
            }
            if (d.Months.Count == 0)
            {
                DocStyle.TdText(t.Cell(), false, "هنوز ردیفی نیست");
                DocStyle.TdText(t.Cell(), false, "—");
                DocStyle.TdText(t.Cell(), false, "—");
                DocStyle.TdText(t.Cell(), false, "—");
            }
        });
    });
}
