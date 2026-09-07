using System.Text.RegularExpressions;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ ترتیبِ نوارِ بخش‌ها = ترتیبِ نوارِ سایت ═══════════════════════════════════
///
/// چرا این آزمون بیش از یک وسواسِ ظاهری است: میانبرِ ‎Ctrl+Shift+عدد‎ بخش را
/// **با شمارهٔ جایش در همین فهرست** باز می‌کند (‎ShortcutService‎ →
/// ‎vm.Sections[n-1]‎). پس ترتیبِ فهرست، خودش بخشی از قراردادِ صفحه‌کلید است.
///
/// پیش از این ترتیب از ردیفِ دهم به بعد با سایت فرق داشت و ‎Ctrl+Shift+۱۰‎ که
/// در سایت «گاوصندوق» بود، این‌جا «رسید قرض‌داران» را باز می‌کرد — و همین‌طور
/// تا هجده. آزمونِ میانبرها این را نمی‌گرفت، چون خودش هم با
/// ‎vm.Sections[n]‎ می‌سنجید و به خودش ارجاع می‌داد.
///
/// فهرستِ زیر از خودِ ‎index.html‎ خوانده می‌شود (دکمه‌های ‎.nav-btn‎ و
/// ‎showSection('…')‎)، نه از حافظه — پس اگر روزی صاحب ریپو دکمه‌ای به سایت
/// اضافه یا جابه‌جا کند، همین‌جا قرمز می‌شود.
/// </summary>
public class NavOrderTests
{
    private static readonly string Root =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    /// <summary>‎id‎ی هجده دکمهٔ نوارِ سایت، به ترتیبِ خودِ صفحه.</summary>
    private static List<string> SiteNav()
    {
        var html = File.ReadAllText(Path.Combine(Root, "..", "index.html"));

        // فقط دکمه‌های نوار: ‎<button class="nav-btn" onclick="showSection('x',this)">‎
        //
        // ⚠️ ‎nav-btn[^"]*‎ نه ‎nav-btn‎ تنها: دکمهٔ بخشِ باز کلاسِ ‎active‎ هم
        // دارد (‎class="nav-btn active"‎) و با الگوی سفت‌وسخت از قلم می‌افتاد —
        // یعنی «داشبورد» شمرده نمی‌شد و همهٔ شماره‌ها یکی جابه‌جا می‌شدند.
        var ids = new List<string>();
        foreach (Match m in Regex.Matches(html,
                     @"class=""nav-btn[^""]*""[^>]*onclick=""showSection\('([^']+)'"))
            ids.Add(m.Groups[1].Value);
        return ids;
    }

    /// <summary>شناسهٔ بخش‌های نیتیو، به ترتیبِ ‎BuildSections‎.</summary>
    private static List<string> NativeSections()
    {
        var cs = File.ReadAllText(Path.Combine(Root, "PumpYaqobi.App", "ViewModels", "MainViewModel.cs"));
        var i = cs.IndexOf("private IEnumerable<SectionViewModel> BuildSections", StringComparison.Ordinal);
        Assert.True(i >= 0, "‎BuildSections‎ پیدا نشد");
        var end = cs.IndexOf("\n    };", i, StringComparison.Ordinal);
        Assert.True(end > i, "پایانِ ‎BuildSections‎ پیدا نشد");
        var body = cs[i..end];

        // نامِ کلاسِ هر بخش → شناسهٔ همان بخش (همانی که در سازندهٔ ‎base(...)‎ است)
        var names = Regex.Matches(body, @"new (\w+)SectionViewModel")
                         .Select(m => m.Groups[1].Value).ToList();

        var ids = new List<string>();
        foreach (var n in names)
        {
            var file = Path.Combine(Root, "PumpYaqobi.App", "ViewModels", "Sections",
                                    n + "SectionViewModel.cs");
            var src = File.ReadAllText(file);

            // دو شکلِ ممکن در سازنده:
            //   ‎: base("safe", …)‎
            //   ‎: base(noInvoice ? "noinv" : "debt", …)‎  ← شاخهٔ پیش‌فرض، یعنی دومی
            var cond = Regex.Match(src, @"base\(\w+ \? ""[^""]+"" : ""([^""]+)""");
            var plain = Regex.Match(src, @"base\(""([^""]+)""");
            Assert.True(cond.Success || plain.Success, $"شناسهٔ بخشِ {n} خوانده نشد");
            ids.Add(cond.Success ? cond.Groups[1].Value : plain.Groups[1].Value);
        }
        return ids;
    }

    /// <summary>سایت هجده دکمهٔ سرصفحه دارد — نه کمتر، نه بیشتر.</summary>
    [Fact]
    public void TheSiteStillHasEighteenNavButtons()
        => Assert.Equal(18, SiteNav().Count);

    /// <summary>
    /// هجده بخشِ **اولِ** نیتیو باید دقیقاً همان هجده دکمهٔ سایت باشند، به
    /// همان ترتیب — وگرنه ‎Ctrl+Shift+عدد‎ بخشِ دیگری را باز می‌کند.
    /// </summary>
    [Fact]
    public void TheFirstEighteenSectionsMatchTheSiteNavOrder()
    {
        var site = SiteNav();
        var native = NativeSections();

        Assert.True(native.Count >= site.Count,
            $"نیتیو {native.Count} بخش دارد ولی سایت {site.Count} دکمه");

        for (var i = 0; i < site.Count; i++)
        {
            Assert.True(site[i] == native[i],
                $"جایگاهِ {i + 1}: سایت «{site[i]}» ولی نیتیو «{native[i]}» — "
                + $"یعنی Ctrl+Shift+{i + 1} بخشِ اشتباه را باز می‌کند");
        }
    }

    /// <summary>
    /// بخش‌های اضافیِ نیتیو باید **بعد از** هجدهمی بنشینند. اگر یکی‌شان وسط
    /// بیفتد، همهٔ شماره‌های بعدش یک واحد جابه‌جا می‌شوند.
    /// </summary>
    [Fact]
    public void ExtraNativeSectionsComeAfterTheSitesEighteen()
    {
        var native = NativeSections();
        var siteIds = SiteNav().ToHashSet();

        for (var i = 18; i < native.Count; i++)
            Assert.False(siteIds.Contains(native[i]),
                $"بخشِ «{native[i]}» هم در نوارِ سایت هست هم بعد از هجدهمی نشسته");
    }
}
