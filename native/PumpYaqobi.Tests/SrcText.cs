namespace PumpYaqobi.Tests;

/// <summary>
/// ══ شورا، ج۴ — خواندنِ سورسِ کلاسی که چند فایل شد ══════════════════════════════
///
/// <c>ExcelGrid</c>، <c>MainViewModel</c> و <c>CloudLink</c> (شورا، د۵) به چند فایلِ <c>partial</c> شکستند (بی
/// تغییرِ یک رفتار). آزمون‌هایی که متنِ «ExcelGrid.cs» یا «MainViewModel.cs» را
/// می‌خوانند همان کلاس را می‌خواهند، پس این‌جا همهٔ تکه‌ها <b>به همان ترتیبِ فایلِ
/// پیشین</b> پشتِ هم برمی‌گردند — متنی که آزمون می‌بیند همان فایلِ دیروز است
/// (به‌علاوهٔ سرآیندِ هر تکه)، و برش‌های «از این متد تا آن متد» همان‌طور کار می‌کنند.
/// ⚠️ هر فایلِ دیگری همان <c>File.ReadAllText</c> است.
/// </summary>
internal static class SrcText
{
    private static readonly Dictionary<string, string[]> Split = new(StringComparer.Ordinal)
    {
        ["ExcelGrid.cs"] = new[] { "ExcelGrid.cs", "ExcelGrid.Sticky.cs", "ExcelGrid.Widths.cs",
                                   "ExcelGrid.Keys.cs", "ExcelGrid.Clipboard.cs", "ExcelGrid.Commit.cs" },
        ["MainViewModel.cs"] = new[] { "MainViewModel.cs", "MainViewModel.Lights.cs", "MainViewModel.Notices.cs",
                                       "MainViewModel.Lifecycle.cs", "MainViewModel.Navigation.cs", "MainViewModel.Ledger.cs" },
        //  شورا، د۵: پنج تکهٔ اول همان فایلِ پیشین‌اند، به همان ترتیب؛ چهار تای آخر از
        //  پیش تکهٔ جدا بودند و آزمون‌هایشان همچنان نامِ خودشان را هم می‌خوانند.
        ["CloudLink.cs"] = new[] { "CloudLink.cs", "CloudLink.License.cs", "CloudLink.Support.cs",
                                   "CloudLink.Account.cs", "CloudLink.Session.cs", "CloudLink.Wire.cs",
                                   "CloudLink.Sync.cs", "CloudLink.Rate.cs", "CloudLink.Watch.cs",
                                   "CloudLink.LiveConfig.cs" },
    };

    public static string Read(string path)
    {
        var name = Path.GetFileName(path);
        if (!Split.TryGetValue(name, out var parts)) return File.ReadAllText(path);
        var dir = Path.GetDirectoryName(path)!;
        return string.Join("\n", parts.Select(p => File.ReadAllText(Path.Combine(dir, p))));
    }

    /// <summary>«MainViewModel.Lights.cs» ⇒ «MainViewModel.cs» — نامِ فایلِ کلاس، مثلِ پیش از شکستن.</summary>
    public static string ClassFile(string name)
    {
        foreach (var (root, parts) in Split)
            if (parts.Contains(name, StringComparer.Ordinal)) return root;
        return name;
    }

    public static string Read(string path, System.Text.Encoding enc) =>
        Split.ContainsKey(Path.GetFileName(path)) ? Read(path) : File.ReadAllText(path, enc);
}
