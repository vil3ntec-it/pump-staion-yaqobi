using System.Text.RegularExpressions;
using PumpYaqobi.Domain;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ 🌐 زبانِ پشتو (۱۴۰۵/۰۷/۲۸) ══════════════════════════════════════════════
/// «برای برنامه زبان پشتو هم اضافه کن» — موتورِ برگرداندن (‎LangMap‎) و فرهنگِ
/// جاسازی‌شده. ⚠️ هیچ آزمونی این‌جا زبانِ سراسری (‎Lang.Use‎) را عوض نمی‌کند: آزمون‌های
/// موازیِ دیگر نوشته‌ها و PDFهای فارسی را می‌سنجند.
/// </summary>
public class LangTests
{
    private static LangMap Map() => new(new Dictionary<string, string>
    {
        ["ذخیره"] = "خوندي کول",
        ["{0} لیتر فروخته شد"] = "{0} لیتره وپلورل شول",
        ["مشتری {0} حذف شد"] = "پېرودونکی {0} ړنګ شو",
        ["اشتراکِ «{0}» فعال است — {1} روز مانده."] = "د «{0}» ګډون فعال دی — {1} ورځې پاتې.",
        ["نقد"] = "نغد",
        ["پروفایل"] = "پروفایل",
        ["از «{0}» وارد شوید"] = "له «{0}» څخه ننوځئ",
        ["حساب"] = "حساب",
        ["موجودی"] = "موجودي",
        ["{0} و {1}"] = "{0} او {1}",
    });

    [Fact]
    public void Persian_IsTheDefault_AndReturnsTheVerySameString()
    {
        //  فارسی هیچ هزینه‌ای ندارد: همان شیء برمی‌گردد
        if (Lang.On) return;   // اجرای دستیِ برنامه با پشتو — آزمونِ سراسری بی‌معنی است
        const string s = "ذخیره";
        Assert.Same(s, Lang.T(s));
        Assert.Equal(Lang.Persian, Lang.Code);
    }

    [Fact]
    public void Exact_Number_Core_Template_Segments()
    {
        var m = Map();
        Assert.Equal("خوندي کول", m.T("ذخیره"));
        //  عدد ⇐ {0}
        Assert.Equal("۱۲ لیتره وپلورل شول", m.T("۱۲ لیتر فروخته شد"));
        Assert.Equal("1,250.5 لیتره وپلورل شول", m.T("1,250.5 لیتر فروخته شد"));
        //  سر و تهِ غیرِحرفی (نشانه، فاصله، دونقطه) دست نمی‌خورد
        Assert.Equal("✅ خوندي کول:", m.T("✅ ذخیره:"));
        //  الگوی میان‌دار — و سوراخ خودش برگردانده می‌شود
        Assert.Equal("پېرودونکی احمد ړنګ شو", m.T("مشتری احمد حذف شد"));
        Assert.Equal("پېرودونکی نغد ړنګ شو", m.T("مشتری نقد حذف شد"));
        Assert.Equal("✅ د «VIP» ګډون فعال دی — 42 ورځې پاتې.", m.T("✅ اشتراکِ «VIP» فعال است — 42 روز مانده."));
        Assert.Equal("له «پروفایل» څخه ننوځئ", m.T("از «پروفایل» وارد شوید"));
        //  تکه‌ها
        Assert.Equal("حساب: 500 · موجودي", m.T("حساب: 500 · موجودی"));
    }

    [Fact]
    public void Unknown_StaysPersian_AndNeverBreaks()
    {
        var m = Map();
        const string s = "این جمله در فرهنگ نیست";
        Assert.Same(s, m.T(s));
        Assert.Equal("", m.T(""));
        Assert.Equal("12,500", m.T("12,500"));
        Assert.Equal("VIP", m.T("VIP"));
        //  الگویی که جز سوراخ تقریباً هیچ ندارد هر جمله‌ای را نمی‌گیرد
        Assert.Equal("احمد و پسران", m.T("احمد و پسران"));
    }

    [Fact]
    public void Translated_IsNotTranslatedAgain()
    {
        //  قلابِ نوشته متنِ برگردانده را دوباره می‌بیند — نباید دوباره عوض شود
        var m = Map();
        var once = m.T("ذخیره");
        Assert.Same(once, m.T(once));
    }

    [Fact]
    public void EmbeddedPashto_LoadsAndIsSound()
    {
        var map = Lang.ReadResource(Lang.Pashto);
        Assert.True(map.Count > 3000, $"فرهنگِ پشتو فقط {map.Count} نوشته دارد");
        var holes = new Regex(@"\{\d+\}");
        var bad = new List<string>();
        foreach (var (k, v) in map)
        {
            if (string.IsNullOrWhiteSpace(v)) bad.Add("خالی: " + k);
            else if (!holes.Matches(k).Select(x => x.Value).Order().SequenceEqual(holes.Matches(v).Select(x => x.Value).Order()))
                bad.Add("سوراخ: " + k);
            else if (v != k && v.Contains('گ')) bad.Add("گِ فارسی: " + v);
        }
        Assert.True(bad.Count == 0, string.Join("\n", bad.Take(20)));
    }

    [Theory]
    [InlineData("ذخیره")]
    [InlineData("انصراف")]
    [InlineData("تنظیمات")]
    [InlineData("حذف")]
    [InlineData("داشبورد")]
    public void EmbeddedPashto_CoversCommonWords(string fa)
    {
        var m = new LangMap(Lang.ReadResource(Lang.Pashto));
        Assert.NotEqual(fa, m.T(fa));
    }

    /// <summary>
    /// ⛔ هر نوشتهٔ ثابتِ فارسیِ صفحه‌ها (‎Text/Content/Header/Watermark/ToolTip.Tip/Title‎)
    /// پشتو دارد. نوشتهٔ تازه ⇐ ترجمه‌اش در ‎PumpYaqobi.Domain/Lang/ps.json‎
    /// (‎python3 tools/lang-missing.py‎ فهرستِ بی‌ترجمه‌ها را می‌دهد).
    /// </summary>
    [Fact]
    public void EveryAxamlText_HasPashto()
    {
        var m = new LangMap(Lang.ReadResource(Lang.Pashto));
        var attr = new Regex(@"\b(?:Text|Content|Header|Watermark|ToolTip\.Tip|Title|PlaceholderText)=""([^""{][^""]*)""");
        var fa = new Regex(@"[\u0621-\u064A\u0671-\u06D3]");
        var miss = new List<string>();
        foreach (var f in Directory.EnumerateFiles(Path.Combine(Root(), "PumpYaqobi.App"), "*.axaml", SearchOption.AllDirectories))
        {
            if (f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar)) continue;
            var src = Regex.Replace(File.ReadAllText(f), "<!--.*?-->", "", RegexOptions.Singleline);
            foreach (Match a in attr.Matches(src))
            {
                var v = System.Net.WebUtility.HtmlDecode(a.Groups[1].Value);
                if (fa.IsMatch(v) && !m.Knows(v))
                    miss.Add(Path.GetFileName(f) + ": " + v);
            }
        }
        Assert.True(miss.Count == 0, "بی‌پشتو:\n" + string.Join("\n", miss.Take(30)));
    }

    private static string Root()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null && !File.Exists(Path.Combine(d.FullName, "PumpYaqobi.sln"))) d = d.Parent;
        Assert.NotNull(d);
        return d!.FullName;
    }
}
