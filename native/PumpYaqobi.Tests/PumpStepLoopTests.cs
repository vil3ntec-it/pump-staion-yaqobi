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
        //  ⛔ و از ۱۴۰۵/۰۷/۱۳ واردشده همیشه «تمام» است (پمپ و ثبت خودکارند)
        Assert.Contains("LoginStep = SignedIn || f.LoginSkipped ? 4 : 1;", vm);
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
        Assert.Contains("public bool PumpStepDone { get; set; }", Src(Settings));

        var vm = Src(Vm);
        //  هر سه مسیر — موفق، «پمپ از قبل هست»، و ورود با حسابی که پمپ دارد
        //  (`NextStepAfterSignInAsync`، ۱۴۰۵/۰۷/۱۳) — مهر می‌زنند
        Assert.Equal(3, Count(vm, "PumpStepDone = true;"));
        //  ⛔ و با `Save()`ی بادوام، نه `SaveSoon()`: گم شدنش یعنی برگشتِ حلقه
        //  (هر مهر، درست پشتِ سرش — شمردنِ کلِ فایل هر ذخیرهٔ دیگری را هم می‌شمرد)
        var at = 0;
        while ((at = vm.IndexOf("PumpStepDone = true;", at, StringComparison.Ordinal)) >= 0)
        {
            var next = vm.Substring(at, Math.Min(120, vm.Length - at));
            Assert.Contains(".Save(); } catch", next);
            Assert.DoesNotContain("SaveSoon", next);
            at += 20;
        }

        //  ⛔ حسابِ تازه یعنی گامِ پمپ از نو
        Assert.Contains("_settings.PumpStepDone = false;", Src(Cloud));
    }

    /// <summary>
    /// ⛔ <b>کلیدِ عمومی هم یک «بند» است.</b> بی این، نصبی که کلید را قفل
    /// کرده ولی توکن نگرفته بود، با ورودِ حسابِ دیگر کلیدش را نگه می‌داشت
    /// و از آن به بعد هر بند شدنی <c>key_mismatch</c> می‌گرفت.
    /// </summary>
    [Fact]
    public void Kelide_Omumi_Ham_Yek_Band_Ast()
    {
        var src = Src(Cloud);
        var at = src.IndexOf("var bound =", StringComparison.Ordinal);
        Assert.True(at > 0, "سنجشِ «بندی هست؟» پیدا نشد");
        var block = src[at..Math.Min(src.Length, at + 400)];
        Assert.Contains("CloudPublicKey", block);
    }

    /// <summary>
    /// ⛔ <b>کلیدی که چیزی را نگه نمی‌دارد، قفل نیست</b> — ولی فقط در
    /// <c>BindAsync</c>. <c>ActivateAsync</c> باید قفلِ سختش را نگه دارد:
    /// آن‌جا کاربر کدِ شش‌رقمی زده و کلیدِ ناجور یک هشدارِ واقعی است.
    /// </summary>
    [Fact]
    public void Kelide_BiMasraf_Faghat_Dar_Bind_Raha_Mishavad()
    {
        var src = Src(Cloud);
        Assert.Contains("var neverActivated = string.IsNullOrWhiteSpace(_settings.CloudDeviceToken)", src);
        //  ⛔ شرطش هر دو را می‌خواهد: نه توکن، نه مجوز
        var at = src.IndexOf("var neverActivated", StringComparison.Ordinal);
        Assert.Contains("CloudLicense", src[at..Math.Min(src.Length, at + 200)]);

        //  ⛔ و هر دو جا هنوز می‌توانند `key_mismatch` بدهند — قفل برنداشته شد.
        //  ⚠️ (۱۴۰۵/۰۷/۱۲) جملهٔ `key_mismatch` در یک کمک‌کار (`KeyMismatch()`)
        //  نشست چون سه راه — فعال‌سازی، بند، و مجوزِ تازهٔ `RefreshAsync` —
        //  همان را می‌دهند؛ پس ادعا از «دو رشته» به «یک رشته، و هر دو راهِ
        //  قدیمی هنوز صدایش می‌زنند» رفت. ضعیف نشد: هر دو جا شمرده می‌شوند.
        Assert.Equal(1, Count(src, "\"key_mismatch\")"));
        var act = src.IndexOf("public async Task<CloudResult> ActivateAsync", StringComparison.Ordinal);
        var bind = src.IndexOf("public async Task<CloudResult> BindAsync", StringComparison.Ordinal);
        var redeem = src.IndexOf("public async Task<CloudResult> RedeemAsync", StringComparison.Ordinal);
        Assert.Contains("return KeyMismatch();", src[act..bind]);
        Assert.Contains("return KeyMismatch();", src[bind..redeem]);

        //  ⚠️ و رها کردن فقط یک بار نوشته شده (مسیرِ bind)
        Assert.Equal(1, Count(src, "var neverActivated"));
    }

    /// <summary>
    /// ⛔ <b>شکستِ بند شدن دیگر بلعیده نمی‌شود.</b> حلقهٔ شصت‌ثانیه‌ای هر دور
    /// <c>BindAsync</c> را می‌زد و نتیجه‌اش را دور می‌ریخت، پس یک شکستِ
    /// دائمی برای همیشه نامرئی بود و کاربر فقط «فعال نشده» می‌دید.
    /// </summary>
    [Fact]
    public void Shekaste_Band_Shodan_Balide_Nemishavad()
    {
        var src = Src(Cloud);
        Assert.Contains("public static string LastBindWhy", src);
        Assert.Contains("LastBindWhy = bind.Ok ? \"\" : (bind.Why ?? \"\");", src);
        //  ⛔ خطِ قدیمی که هم استثنا و هم نتیجه را می‌خورد، برنگردد
        Assert.DoesNotContain("try { await BindAsync(ct); } catch", src);

        //  و پروفایل همان دلیل را نشان می‌دهد
        Assert.Contains("CloudLink.LastBindWhy", Src(Vm));
    }

    private static int Count(string src, string needle)
    {
        int n = 0, i = 0;
        while ((i = src.IndexOf(needle, i, StringComparison.Ordinal)) >= 0) { n++; i += needle.Length; }
        return n;
    }
}
