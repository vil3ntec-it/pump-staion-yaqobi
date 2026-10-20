using PumpYaqobi.Domain;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace PumpYaqobi.Reporting.Pdf;

/// <summary>
/// ══ 🌐 PDF هم‌زبانِ برنامه (۱۴۰۵/۰۷/۲۸) ═══════════════════════════════════════
/// هر <c>.Text("…")</c>ِ این پوشه از همین‌جا می‌گذرد، نه از خودِ QuestPDF: افزونه‌ای که
/// در همین فضای نام است بر افزونهٔ <c>using QuestPDF.Fluent</c> مقدم است (قاعدهٔ C#).
/// پس هیچ گزارشی دست نخورد و گزارشِ تازه هم خودش پشتو می‌شود. در فارسی
/// <see cref="Lang.T"/> همان نوشته را پس می‌دهد.
/// </summary>
public static class LangText
{
    public static TextBlockDescriptor Text(this IContainer element, string? text) =>
        TextExtensions.Text(element, text is null ? null : Lang.T(text));
}
