using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace PumpYaqobi.Reporting.Pdf;

/// <summary>یک ردیفِ کارت: [تیل (٪فیصدی), بردگی, رسید, فیصدی, الباقی] — همان ‎AcctBook.Fuels‎.</summary>
public sealed record DebtorCardInput(
    string Pump,
    string Name,
    string Account,
    string Unit,
    string Date,
    IReadOnlyList<string[]> Fuels,
    string Albaqi,
    string Status,
    bool Bad,
    byte[]? Qr);

/// <summary>
/// ══ کارتِ حسابِ قرض‌دار برای تلگرام (۱۴۰۵/۰۷/۲۱) ═══════════════════════════
///
/// خواستهٔ صاحب ریپو: «یک عکس از حسابش… فقط نشان بده بردگی، رسید و الباقی و
/// واحد که تیل است یا پول… و کیو‌آرش… پیام خیلی طولانی نباشه، ساده.»
///
/// ⛔ هیچ عددی این‌جا ساخته نمی‌شود: هر خانه همان رشته‌ای است که
/// ‎AcctSnapshots.ForDebtAccount‎ برای کیو‌آر می‌سازد — همان عددِ سربرگِ صفحهٔ
/// حساب. همان فونت و رنگ‌های ‎DocStyle‎، پس کارت با PDFِ برنامه یکی است.
/// ⚠️ با ‎GenerateImages‎ عکس می‌شود (PNG، ۲۲۰ DPI) — بی رابطِ کاربر، روی هر نخ.
/// </summary>
public sealed class DebtorCardImage : IDocument
{
    private readonly DebtorCardInput _in;

    public DebtorCardImage(DebtorCardInput input) { _in = input; }

    public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

    /// <summary>PNGِ کارت. ⛔ پیش از آن ‎PdfEngine.Initialize()‎ (فونت).</summary>
    public byte[] Png()
    {
        PdfEngine.Initialize();
        return this.GenerateImages(new ImageGenerationSettings
        {
            ImageFormat = ImageFormat.Png,
            RasterDpi = 220,
        }).First();
    }

    public void Compose(IDocumentContainer container) => container.Page(page =>
    {
        page.ContinuousSize(380);
        page.Margin(14);
        page.PageColor(Colors.White);
        page.ContentFromRightToLeft();
        page.DefaultTextStyle(t => t.FontFamily(PdfEngine.Font).FontSize(10.5f).FontColor(DocStyle.CellFg)
                                    .Fallback(f => f.FontFamily(PdfEngine.EmojiFont)));
        page.Content().Column(col =>
        {
            col.Spacing(8);

            // ── سربرگ: نام، حساب، تاریخ ──
            col.Item().Row(row =>
            {
                row.RelativeItem().Column(h =>
                {
                    h.Item().Text(_in.Name).FontSize(16).Bold();
                    if (_in.Account.Length > 0) h.Item().Text("📄 " + _in.Account).FontSize(10).FontColor(DocStyle.Sub);
                    h.Item().Text("⛽ " + _in.Pump + " · 📅 " + _in.Date).FontSize(9).FontColor(DocStyle.Sub);
                });
                row.ConstantItem(92).AlignLeft().AlignMiddle()
                   .Background(_in.Bad ? "#fde8e8" : "#e6f4ea").Border(1)
                   .BorderColor(_in.Bad ? DocStyle.Danger : "#2f855a")
                   .PaddingVertical(4).PaddingHorizontal(6).AlignCenter()
                   .Text(_in.Status).FontSize(10).Bold().FontColor(_in.Bad ? DocStyle.Danger : "#276749");
            });

            col.Item().Text("واحد: " + _in.Unit).FontSize(9.5f).FontColor(DocStyle.Sub);

            // ── جدول: تیل · رسید · بردگی · الباقی ──
            col.Item().Table(t =>
            {
                t.ColumnsDefinition(c =>
                {
                    c.RelativeColumn(1.3f);
                    c.RelativeColumn();
                    c.RelativeColumn();
                    c.RelativeColumn();
                });
                void Head(string s) => t.Cell().Background(DocStyle.HeadBg).Padding(5).AlignCenter()
                                        .Text(s).FontColor(DocStyle.HeadFg).FontSize(DocStyle.HeadSize).Bold();
                Head("تیل");
                Head("رسید");
                Head("بردگی");
                Head("الباقی");
                var i = 0;
                foreach (var f in _in.Fuels)
                {
                    if (f.Length < 5) continue;
                    var bg = i++ % 2 == 1 ? DocStyle.RowAlt : Colors.White;
                    void Cell(string s, string color, bool bold = false)
                    {
                        var x = t.Cell().Background(bg).BorderBottom(1).BorderColor(DocStyle.CellLine)
                                 .Padding(5).AlignCenter().Text(s).FontColor(color);
                        if (bold) x.Bold();
                    }
                    Cell(f[0], DocStyle.CellFg, true);
                    Cell(f[2], DocStyle.Money);
                    Cell(f[1], DocStyle.Hawala);
                    Cell(f[4], DocStyle.Fuel, true);
                }
            });

            // ── الباقیِ کل و کیو‌آر ──
            col.Item().Row(row =>
            {
                row.RelativeItem().AlignMiddle().Column(c =>
                {
                    c.Item().Text("الباقیِ کل").FontSize(9.5f).FontColor(DocStyle.Sub);
                    c.Item().Text(_in.Albaqi + " " + _in.Unit).FontSize(18).Bold()
                     .FontColor(_in.Bad ? DocStyle.Danger : DocStyle.Fuel);
                });
                if (_in.Qr is { Length: > 0 } qr)
                {
                    row.ConstantItem(96).Column(c =>
                    {
                        c.Item().Height(96).Image(qr).FitArea();
                        c.Item().AlignCenter().Text("اسکن ⇒ حساب").FontSize(8).FontColor(DocStyle.Sub);
                    });
                }
            });
        });
    });
}
