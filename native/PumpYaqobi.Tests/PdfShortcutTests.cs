using System.Text.RegularExpressions;
using PumpYaqobi.App.Services;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ ‎Ctrl+P‎ به جایی برسد — بندِ ۲۰ و ۲۹ ═════════════════════════════════════
///
/// ‎ShortcutService‎ فرمانِ پی‌دی‌اف را با نامش پیدا می‌کند
/// (‎PdfCommandName = "PdfCommand"‎)، چون چهارده بخش آن را دارند و افزودنِ یک
/// واسط به همه‌شان چهارده جای دست‌کاری بود.
///
/// بهایش این است که تغییرِ نام، میانبر را **بی‌صدا** می‌شکند: نه خطای ساخت،
/// نه پیامی به کاربر — فقط ‎Ctrl+P‎ی که دیگر کار نمی‌کند. این آزمون همان
/// را می‌گیرد.
/// </summary>
public class PdfShortcutTests
{
    private static readonly string Root =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    private static string SectionsDir =>
        Path.Combine(Root, "PumpYaqobi.App", "ViewModels", "Sections");

    /// <summary>فایل‌هایی که یک ‎[RelayCommand]‎ به نامِ ‎PdfAsync‎ دارند.</summary>
    private static List<string> FilesWithPdfCommand() =>
        Directory.EnumerateFiles(SectionsDir, "*.cs")
                 .Where(f => Regex.IsMatch(File.ReadAllText(f),
                             @"\[RelayCommand\][\s\S]{0,200}?Task PdfAsync\s*\("))
                 .Select(Path.GetFileName)
                 .Select(x => x!)
                 .OrderBy(x => x, StringComparer.Ordinal)
                 .ToList();

    /// <summary>
    /// اگر این عدد صفر شود یعنی هیچ بخشی پی‌دی‌اف ندارد و ‎Ctrl+P‎ به جایی
    /// نمی‌رسد — دقیقاً همان حالتی که پیش از بندِ ۲۰ بود.
    /// </summary>
    [Fact]
    public void SectionsStillOfferAPdfCommand()
    {
        var files = FilesWithPdfCommand();
        Assert.True(files.Count >= 10,
            "فقط " + files.Count + " بخش فرمانِ ‎PdfAsync‎ دارد: " + string.Join(", ", files));
    }

    /// <summary>
    /// نامی که ‎ShortcutService‎ دنبالش می‌گردد باید همانی باشد که
    /// ‎CommunityToolkit‎ از ‎PdfAsync‎ می‌سازد: ‎PdfAsync‎ → ‎PdfCommand‎.
    /// </summary>
    [Fact]
    public void TheShortcutLooksForTheGeneratedCommandName()
        => Assert.Equal("PdfCommand", ShortcutService.PdfCommandName);

    /// <summary>
    /// ‎Ctrl+P‎ باید در خودِ ‎ShortcutService‎ بند باشد — نه فقط در توضیحاتش.
    /// (پیش از این توضیح می‌گفت «هنوز نیست» و واقعاً هم نبود.)
    /// </summary>
    [Fact]
    public void TheShortcutIsActuallyWiredUp()
    {
        var src = File.ReadAllText(Path.Combine(Root, "PumpYaqobi.App", "Services", "Shortcuts.cs"));
        Assert.Contains("Key.P", src);
        Assert.Contains("TryPdf()", src);
        Assert.DoesNotContain("هنوز نیست: در نسخهٔ نیتیو هیچ بخشی", src);
    }

    /// <summary>
    /// اولویت همان ترتیبِ ‎_kbPdfAction‎ی نسخهٔ وب است: اول صفحهٔ بازِ درونِ
    /// بخش (حسابِ شخص/شرکت)، بعد خودِ بخش. برعکسش یعنی وقتی حسابِ کسی باز
    /// است، ‎Ctrl+P‎ پی‌دی‌افِ فهرستِ پشتِ سر را می‌گرفت.
    /// </summary>
    [Fact]
    public void TheOpenPageWinsOverTheSection()
    {
        var src = File.ReadAllText(Path.Combine(Root, "PumpYaqobi.App", "Services", "Shortcuts.cs"));
        var i = src.IndexOf("ActivePage", StringComparison.Ordinal);
        var j = src.IndexOf("_vm.Current }", StringComparison.Ordinal);
        Assert.True(i >= 0 && j > i, "ترتیبِ «صفحهٔ باز، بعد بخش» در ‎TryPdf‎ پیدا نشد");
    }
}
