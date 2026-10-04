using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ «هر بار می‌روم پروفایل، دوباره اسمِ پمپ را می‌خواهد» ═════════════════
///
/// گزارشِ صاحب ریپو با دو عکس (۱۴۰۵/۰۷/۱۱): «حسابِ تازه ساختم… همه‌شو تموم
/// کردم و هر بار که روی پروفایل برم می‌گه اسمِ پمپ رو انتخاب کن، انتخاب
/// می‌کنم، می‌رم دوباره همون بند و بساط. و بعد از ساختِ حساب هم ۳۰ روز
/// رایگان فعال نمی‌شه.»
///
/// در عکسِ دوم، پروفایل هم‌زمان می‌گفت «✅ پمپِ شما روی حسابتان هست» **و**
/// «سرورِ حساب: فعال نشده» و «ماندهٔ اشتراک: —».
///
/// ⛔ <b>هر سه شکایت یک ریشه داشتند</b>: دستگاه بند نمی‌شد.
///
/// <code>
/// کلیدِ عمومیِ جامانده ⇒ BindAsync ⇒ key_mismatch (بی هیچ پیامی)
///                     ⇒ توکنِ دستگاه خالی می‌ماند
///                     ⇒ «تمام» که به همان توکن بند بود، هیچ‌وقت نمی‌رسید ⇒ حلقه
///                     ⇒ مجوز صادر نمی‌شد ⇒ ۳۰ روزِ رایگان هم نمی‌آمد
/// </code>
/// </summary>
public class PumpStepLoopTests
{
    private static readonly string Root =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    private static string Src(string rel) =>
        File.ReadAllText(Path.Combine(Root, rel.Replace('/', Path.DirectorySeparatorChar)));

    private const string Cloud = "PumpYaqobi.App/Services/CloudLink.cs";
    private const string Vm = "PumpYaqobi.App/ViewModels/Sections/AccountSectionViewModel.cs";
    private const string Settings = "PumpYaqobi.App/Services/AppSettings.cs";

    /// <summary>
    /// ⛔ <b>«تمام» به بند شدنِ دستگاه بند نیست.</b> بند شدن کاری نیست که
    /// کاربر در آن صفحه بتواند انجام دهد (کادرِ کد از ۱۴۰۵/۰۷/۰۴ برداشته
    /// شده)، پس شرط کردنش یعنی گامی که هیچ راهِ خروجی ندارد.
    /// </summary>
    [Fact]
    public void Game_Pomp_Digar_Be_Band_Shodane_Dastgah_Band_Nist()
    {
        var vm = Src(Vm);
        //  «واردشده همیشه تمام» ⇒ رفتاری: AccountStepBehaviourTests (شورا، ت۳)
        //  ⛔ شرطِ قدیمی برنگردد
        Assert.DoesNotContain("LoginStep = SignedIn && activated && hasPump ? 4", vm);
    }

    /// <summary>
    /// ⛔ <b>مهرِ «گامِ پمپ تمام شد» ماندگار است</b>، وگرنه بازدیدِ بعدی
    /// دوباره از حالِ واقعی حساب می‌کرد و به همان گام برمی‌گشت.
    /// </summary>
    [Fact]
    public void Mohre_Game_Pomp_Roye_Disk_Mineshinad()
    {
        //  رفتارش (مهرِ ماندگار روی دیسک): AccountStepBehaviourTests (شورا، ت۳).
        //  ⛔ ممنوعه: مهر هرگز با ‎SaveSoon‎ نرود — گم شدنش یعنی برگشتِ حلقه.
        var vm = Src(Vm);
        var at = 0;
        while ((at = vm.IndexOf("PumpStepDone = true;", at, StringComparison.Ordinal)) >= 0)
        {
            Assert.DoesNotContain("SaveSoon", vm.Substring(at, Math.Min(120, vm.Length - at)));
            at += 20;
        }
    }

    private static int Count(string src, string needle)
    {
        int n = 0, i = 0;
        while ((i = src.IndexOf(needle, i, StringComparison.Ordinal)) >= 0) { n++; i += needle.Length; }
        return n;
    }
}
