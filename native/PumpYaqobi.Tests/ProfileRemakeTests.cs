using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ ریمیکِ پروفایل و اشتراک‌ها — و قفلِ «منطق دست نخورد» ═══════════════════
///
/// خواستهٔ صاحب ریپو (۱۴۰۵/۰۷/۰۵): «بخشِ پروفایل را ۱۰۰ فیصد ریمیک کن و
/// بهترین نوعش را درست کن، و برای اشتراک‌ها هم بهترین‌ها را… دیزاینِ
/// یو‌آی‌یو‌ایکسِ الترا و خیلی خوشگل. <b>منطق را اصلاً دست نمی‌زنی.</b>»
///
/// پس این آزمون دو چیز را با هم قفل می‌کند: شکل واقعاً عوض شده باشد، و
/// هیچ خاصیت یا فرمانِ تازه‌ای زیرِ پوستش ساخته نشده باشد.
/// </summary>
public class ProfileRemakeTests
{
    //  ⚠️ همان راهی که ‎AppLinksTests‎ و ‎ProfilePillTests‎ می‌روند — یک
    //  نسخهٔ سومِ «ریشه کجاست» یعنی روزی یکی‌شان بی‌صدا هیچ فایلی نخواند.
    private static readonly string Root =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    private static string Read(params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { Root }.Concat(parts).ToArray()));

    private static string Account() =>
        Read("PumpYaqobi.App", "Views", "Sections", "AccountSectionView.axaml");

    /// <summary>فقط صفحهٔ پروفایل — همان مرزی که آزمونِ رنگ‌بندی هم می‌شناسد.</summary>
    private static string Profile()
    {
        var x = Account();
        return x[x.IndexOf("👤 خودِ پروفایل", StringComparison.Ordinal)..];
    }

    /// <summary>‎{Binding Foo.Bar}‎ ⇒ ‎Foo‎ — ریشهٔ هر مسیرِ اتصال.</summary>
    private static HashSet<string> BindingRoots(string xaml) =>
        Regex.Matches(xaml, @"\{Binding\s+!?([A-Za-z_][A-Za-z0-9_]*)")
             .Select(m => m.Groups[1].Value).ToHashSet(StringComparer.Ordinal);

    /// <summary>
    /// نامی که واقعاً باید در سورسِ ویومدل باشد.
    ///
    /// ⚠️ ‎[RelayCommand]‎ خودش ‎XxxCommand‎ را می‌سازد و آن نام هیچ‌وقت در
    /// سورس نوشته نمی‌شود — در سورس ‎XxxAsync‎ یا ‎Xxx‎ است. پس پسوندِ
    /// ‎Command‎ برداشته می‌شود، وگرنه این آزمون هر فرمانی را «تازه» می‌دید.
    /// </summary>
    private static string SourceName(string root) =>
        root.EndsWith("Command", StringComparison.Ordinal)
            ? root[..^"Command".Length] : root;

    // ══ ۱) پروفایل ══════════════════════════════════════════════════════════

    /// <summary>
    /// ⛔ <b>هیچ خاصیتِ تازه‌ای ساخته نشد.</b> هر ریشهٔ اتصالی که صفحهٔ
    /// پروفایل می‌زند باید از قبل در ‎AccountSectionViewModel.cs‎ باشد —
    /// خودِ ویومدل یا ‎ProfileRow‎ی که همان‌جا تعریف شده.
    ///
    /// این تنها راهِ سنجیدنِ «منطق دست نخورد» است که با هر بازچینیِ بعدی هم
    /// می‌مانَد: شکل آزاد است، سطحِ زیرش نه.
    /// </summary>
    [Fact]
    public void RimakeProfile_HichKhasiyate_Tazei_Nasakht()
    {
        var vm = Read("PumpYaqobi.App", "ViewModels", "Sections", "AccountSectionViewModel.cs");
        foreach (var root in BindingRoots(Profile()))
            Assert.True(vm.Contains(SourceName(root), StringComparison.Ordinal),
                        $"«{root}» در صفحهٔ پروفایل بسته شده ولی در ویومدل نیست — "
                        + "یعنی برای ظاهر، منطقِ تازه ساخته شده.");
    }

    /// <summary>
    /// و چیزی هم از دست نرفت: هر کارِ پروفایل سرِ جایش است.
    ///
    /// ⚠️ این فهرست عمداً <b>فرمان</b>هاست، نه نوشته‌ها: نوشته با هر ریمیکی
    /// عوض می‌شود، ولی دکمه‌ای که گم شود یعنی قابلیتی که رفت.
    /// </summary>
    [Fact]
    public void RimakeProfile_HichKari_GomNashod()
    {
        var p = Profile();
        foreach (var cmd in new[]
                 {
                     "OpenAccountPageCommand", "PullHomeCommand", "SignOutCommand",
                     "CopyAccessCodeCommand", "ShowAccessQrCommand", "LoadAccessCodeCommand",
                     "RotateAccessCodeCommand", "ForgetPumpCommand",
                     "RedeemSubCommand", "RefreshSubCommand",
                     "SetTabCommand", "OpenBackupsCommand",
                 })
            Assert.Contains(cmd, p);

        //  و خرج کردنِ کدِ اشتراک همین‌جا می‌ماند (قاعدهٔ ۱۴۰۵/۰۷/۰۴)
        Assert.Contains("Binding SubCode", p);
    }

    /// <summary>شکل واقعاً عوض شد — نه فقط جابه‌جاییِ چند خط.</summary>
    [Fact]
    public void RimakeProfile_Shekl_Avaz_Shod()
    {
        var p = Profile();

        //  ⛔ سه ستونِ هم‌وزنِ قدیمی رفت: هیچ‌کدامشان سرصفحه نبود
        Assert.DoesNotContain("ColumnDefinitions=\"300,*,330\"", p);

        //  سرصفحهٔ پهن با آواتارِ گرد
        Assert.Contains("CornerRadius=\"52\"", p);
        //  و چهار عددِ سرِ دست
        Assert.Contains("⏳ ماندهٔ اشتراک", p);
        Assert.Contains("📦 نسخهٔ برنامه", p);
        //  ردیفِ «کلید ⇠ مقدار» با خطِ مویی
        Assert.Contains("Classes=\"kvrow\"", p);
        Assert.Contains("Border.kvrow", Account());
    }

    // ══ ۲) اشتراک و پلن‌ها ══════════════════════════════════════════════════

    private static string Vip() =>
        Read("PumpYaqobi.App", "Views", "Sections", "VipSectionView.axaml");

    [Fact]
    public void RimakePlanha_HichKhasiyate_Tazei_Nasakht()
    {
        var vm = Read("PumpYaqobi.App", "ViewModels", "Sections", "VipSectionViewModel.cs");
        foreach (var root in BindingRoots(Vip()))
        {
            //  ‎StringFormat‎ی داخلِ ‎{Binding …}‎ اتصال نیست، قالبِ نمایش است
            if (root is "StringFormat") continue;
            Assert.True(vm.Contains(SourceName(root), StringComparison.Ordinal),
                        $"«{root}» در صفحهٔ اشتراک بسته شده ولی در ویومدل نیست.");
        }
    }

    /// <summary>
    /// پلن‌ها کنارِ هم‌اند، نه زیرِ هم: پلن را با پلن مقایسه می‌کنند و
    /// چهار کارتِ صفحه‌قد یعنی چهار بار اسکرول برای یک تصمیم.
    /// </summary>
    [Fact]
    public void Planha_KenareHam_Hastand()
    {
        var v = Vip();
        Assert.Contains("<ItemsPanelTemplate><WrapPanel /></ItemsPanelTemplate>", v);

        //  ⛔ و قیمت همچنان از ویومدل می‌آید، نه از خودِ صفحه
        Assert.Contains("{Binding Price}", v);
        var vm = Read("PumpYaqobi.App", "ViewModels", "Sections", "VipSectionViewModel.cs");
        Assert.Contains("\"پایه\", \"—\"", vm);
    }

    /// <summary>
    /// ⚠️ کلیدِ «آزمایشِ حالتِ بی‌اشتراک» سرِ جایش ماند — تنها راهی که صاحبِ
    /// پمپ خودش قفل‌ها را می‌بیند، و فقط می‌بندد.
    /// </summary>
    [Fact]
    public void KelideAzmayesh_SareJayash_Mand()
    {
        Assert.Contains("ToggleTestCommand", Vip());
        Assert.Contains("Binding TestDeny", Vip());
    }
}
