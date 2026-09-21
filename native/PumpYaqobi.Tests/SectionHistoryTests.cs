using System.Text.RegularExpressions;
using PumpYaqobi.Services.Data;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ «🕘 تاریخچه» در هر بخشی که تاریخچه دارد ═════════════════════════════════
///
/// گزارشِ صاحب ریپو (۱۴۰۵/۰۷/۰۶): «تاریخچهٔ بیشترِ بخش‌ها کار نمی‌کند —
/// تاریخچهٔ رسید نیست، تاریخچهٔ چکنه، تاریخچهٔ فاکتورها، تاریخچهٔ ورق‌ها…
/// برای مصارف هم نیست.»
///
/// دادهٔ هیچ‌کدام کم نبود: ‎HistoryService.FeedAsync‎ از روزِ اول هر سیزده دفتر
/// را می‌ساخت. آن‌چه نبود **درِ ورودی** بود — فقط سه بخش دکمه داشتند.
///
/// این آزمون سه چیز را قفل می‌کند:
///   ۱) هر کلیدی که یک بخش می‌گوید، واقعاً یکی از ‎HistoryService.Kinds‎ است
///      (کلیدِ تایپی‌شده یعنی صفحه‌ای که باز می‌شود و خالی است)؛
///   ۲) هیچ‌کدام از آن سیزده دفتر بی در نمانده؛
///   ۳) دکمه‌اش واقعاً در صفحه‌ها هست — ویومدلی که کلید داشته باشد و صفحه‌اش
///      دکمه نداشته باشد، برای کاربر یعنی «کار نمی‌کند».
/// </summary>
public class SectionHistoryTests
{
    private static string Root() => Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    private static readonly Regex KeyRx = new("HistoryKind\\s*=\\s*[^;]*?;", RegexOptions.Singleline);
    private static readonly Regex StrRx = new("\"([a-z]+)\"");

    private static List<(string File, string Key)> Assigned()
    {
        var dir = Path.Combine(Root(), "PumpYaqobi.App", "ViewModels", "Sections");
        var found = new List<(string, string)>();
        foreach (var f in Directory.GetFiles(dir, "*.cs"))
            foreach (Match m in KeyRx.Matches(File.ReadAllText(f)))
                foreach (Match k in StrRx.Matches(m.Value))
                    found.Add((Path.GetFileName(f), k.Groups[1].Value));
        return found;
    }

    [Fact]
    public void EveryKeyASectionClaimsIsARealHistoryKind()
    {
        var kinds = HistoryService.Kinds.Select(k => k.Key).ToHashSet(StringComparer.Ordinal);
        var assigned = Assigned();
        Assert.NotEmpty(assigned);
        foreach (var (file, key) in assigned)
            Assert.True(kinds.Contains(key), file + " کلیدِ ناشناختهٔ «" + key + "» را می‌گوید");
    }

    [Fact]
    public void NoLedgerIsLeftWithoutADoor()
    {
        var have = Assigned().Select(x => x.Key).ToHashSet(StringComparer.Ordinal);
        foreach (var (key, label) in HistoryService.Kinds)
            Assert.True(have.Contains(key), label + " (" + key + ") هیچ بخشی درش را باز نمی‌کند");
    }

    /// <summary>
    /// ⛔ دکمه‌ای که نباشد یعنی قابلیتی که کاربر پیدایش نمی‌کند — و دقیقاً
    /// همان چیزی بود که گزارش شد. پس شمارِ صفحه‌هایی که دکمه دارند دستِ‌کم
    /// به اندازهٔ شمارِ دفترهاست.
    /// </summary>
    [Fact]
    public void TheButtonIsOnThePagesToo()
    {
        var dir = Path.Combine(Root(), "PumpYaqobi.App", "Views", "Sections");
        var n = Directory.GetFiles(dir, "*.axaml")
                         .Count(f => File.ReadAllText(f).Contains("OpenHistoryCommand"));
        Assert.True(n >= HistoryService.Kinds.Length,
                    "فقط " + n + " صفحه دکمهٔ تاریخچه دارد، و دفترها " + HistoryService.Kinds.Length + " تا هستند");
    }

    /// <summary>
    /// ⛔ فرمان **یک جا**ست، نه یازده رونوشت. سه بخشِ اولی که این دکمه را
    /// داشتند هر کدام ‎OpenHistory‎ی خودشان را نوشته بودند؛ برگشتن به آن
    /// حالت یعنی فردا یکی‌شان کلیدِ دیگری بگیرد و کسی نفهمد.
    /// </summary>
    [Fact]
    public void TheCommandLivesInExactlyOnePlace()
    {
        var baseSrc = File.ReadAllText(Path.Combine(Root(), "PumpYaqobi.App", "ViewModels", "SectionViewModel.cs"));
        Assert.Contains("private Task OpenHistory()", baseSrc);

        var dir = Path.Combine(Root(), "PumpYaqobi.App", "ViewModels", "Sections");
        foreach (var f in Directory.GetFiles(dir, "*.cs"))
            Assert.DoesNotContain("private Task OpenHistory()", File.ReadAllText(f));
    }
}
