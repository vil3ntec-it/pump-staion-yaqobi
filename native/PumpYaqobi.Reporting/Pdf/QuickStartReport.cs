using PumpYaqobi.Application.Localization;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace PumpYaqobi.Reporting.Pdf;

/// <summary>یک گام از برگهٔ «یک شیفت در ۵ دقیقه».</summary>
public sealed record QuickStartStep(string Where, string Do, string Keys);

/// <summary>
/// ══ «یک شیفت در ۵ دقیقه» — برگهٔ یک‌صفحه‌ای برای میرزا (شورا، ث۱) ══════════
/// همان کارهای هر روزِ برنامه، به ترتیب، با کلیدهایش — تا میرزای تازه بی
/// پرسیدن یک شیفت را ببندد. فقط نوشته است؛ هیچ عددی از دفتر خوانده نمی‌شود.
///
/// ⚠️ هر جمله رفتارِ **امروزِ** برنامه است (Enter در پارچه ذخیره می‌کند،
/// Ctrl + Tab در پارچه تیل و در ورق روز/شب را عوض می‌کند، «/» حساب را می‌گوید).
/// اگر یکی از آن‌ها عوض شد، همین فهرست هم — `QuickStartTests` کلیدها را با
/// فهرستِ میانبرهای خودِ برنامه می‌سنجد.
/// </summary>
public sealed class QuickStartReport : ISetupDocument
{
    public static readonly IReadOnlyList<QuickStartStep> Steps = new QuickStartStep[]
    {
        new("⛽ پارچه‌ها",
            "کارتِ «روز»: نامِ کارمند، شمارهٔ پایه، شروع و ختمِ پایه و فی. شروع باید دقیقاً همان ختمِ قبلیِ همان پایه باشد — فرق داشت، عددش سرخ می‌شود.",
            "Enter ⇐ ذخیره · Ctrl + Tab ⇐ پطرول/دیزل"),
        new("🌙 پارچهٔ شب",
            "همان کارتِ «شب». شروعِ شب همان ختمِ روز است، حتی اگر روز هنوز ذخیره نشده.",
            "Enter ⇐ ذخیرهٔ هر دو و پارچهٔ تازه"),
        new("📄 ورق‌ها",
            "«اضافه کردن» برای همان روز. پایه‌ها و فروش خودشان از پارچه می‌آیند.",
            "Ctrl + Tab ⇐ روز/شب"),
        new("🧾 ردیفِ قرض",
            "نام (پیشنهادِ کم‌رنگ را Tab بپذیرد) و مقدار. «د» یعنی دیزل؛ «/هارون» یعنی در حسابِ هارون بنشیند؛ «/چکنه» یعنی دفترِ چکنه.",
            "Tab ⇐ پذیرفتنِ پیشنهاد"),
        new("💸 مصرف",
            "همان ردیف با نوعِ «مصرف» — خودش به «مصارف» می‌رود.",
            "Enter/Tab روی کپسول ⇐ عوض کردن"),
        new("🏦 گاوصندوق",
            "دست نزنید: «جمله فروش (منهای قرض و مصرف)»ِ همین شیفت خودش آن‌جا می‌نشیند.",
            "—"),
        new("✅ پایانِ شیفت",
            "همه‌چیز همان لحظه ذخیره می‌شود؛ برای خیالِ راحت Ctrl + S، و از ورق PDF برای امضا.",
            "Ctrl + S ⇐ همین حالا ذخیره · Ctrl + P ⇐ PDFِ همین‌جا"),
    };

    public PageSetup Setup { get; set; } = PageSetup.Default;

    public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

    public void Compose(IDocumentContainer container) =>
        DocStyle.Compose(container, "⏱️ " + PumpBrand.Name + " — یک شیفت در ۵ دقیقه",
            "برگهٔ راهنمای میرزا — کنارِ کامپیوتر بچسبانید",
            DocDates.Line(), Body, setup: Setup);

    private static void Body(IContainer c) => c.Column(col =>
    {
        col.Spacing(6);
        var i = 0;
        foreach (var s in Steps)
        {
            i++;
            col.Item().Border(1).BorderColor(DocStyle.Edge(DocStyle.BoxLine)).Padding(7).Row(row =>
            {
                row.ConstantItem(26).AlignMiddle().AlignCenter().Text(PersianText.Num(i))
                   .FontSize(15).Bold().FontColor(DocStyle.Ink(DocStyle.Title));
                row.RelativeItem().Column(cc =>
                {
                    cc.Item().Text(s.Where).FontSize(11.5f).Bold().FontColor(DocStyle.Ink(DocStyle.CellFg));
                    cc.Item().PaddingTop(2).Text(s.Do).FontSize(10).FontColor(DocStyle.Ink(DocStyle.CellFg));
                    if (s.Keys != "—")
                        cc.Item().PaddingTop(3).Text("⌨️ " + s.Keys).FontSize(9).FontColor(DocStyle.Ink(DocStyle.Blue));
                });
            });
        }

        col.Item().PaddingTop(6).Border(1).BorderColor(DocStyle.Edge(DocStyle.Danger)).Padding(7).Column(cc =>
        {
            cc.Item().Text("اگر چیزی سرخ شد").Bold().FontColor(DocStyle.Ink(DocStyle.Danger));
            cc.Item().PaddingTop(2).Text("هیچ چیزی پاک نمی‌شود و هیچ چیزی مانعِ ذخیره نیست — سرخ فقط می‌گوید «بررسی کنید». "
                + "Ctrl + Z هر کاری را برمی‌گرداند و F1 همهٔ کلیدها را نشان می‌دهد.").FontSize(10);
        });
    });
}
