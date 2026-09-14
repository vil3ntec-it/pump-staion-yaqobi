using System.Text.RegularExpressions;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ پردهٔ لودینگ و زنده ماندنِ صفحه‌ها ═══════════════════════════════════════
///
/// خواستهٔ صاحب ریپو:
///
///   «موقعِ تازه باز کردنِ اپ باید یک لودینگ داشته باشه تا همهٔ بخش‌ها و برنامه
///    رو رندر کنه؛ بعدش که شد، این بازگشت به صفحهٔ اصلی نباید تأخیری داشته
///    باشه… جوری نباشه با هر بار رندر شدن اپ حافظه اضافه کنه… یک انیمیشن با
///    صفحهٔ لودینگ طراحی و درست کن.»
///
/// رفتارِ واقعی‌اش را ‎dotnet run --project PumpYaqobi.UiTests -- warm‎ روی پنجرهٔ
/// واقعی می‌سنجد. این‌جا فقط چیزهایی قفل می‌شوند که «برگشتنی»اند — یعنی کسی
/// روزی دوباره‌شان می‌کند و همه چیز بی‌صدا کند می‌شود.
/// </summary>
public class StartupWarmTests
{
    private static readonly string Root =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    private static string Read(params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { Root }.Concat(parts).ToArray()));

    private static string Shell() => Read("PumpYaqobi.App", "Views", "MainWindow.axaml");

    /// <summary>
    /// ⚠️ ناحیهٔ محتوا نباید دوباره ‎ContentControl‎ی بشود که صفحه‌ها را
    /// جابه‌جا می‌کند. سنجشِ ‎waraqperf‎ گرفتش: بیرون رفتن از یک بخش و
    /// برگشتن، ردیف‌های جدولش را نابود و از نو می‌ساخت — باز کردنِ یک ورقِ
    /// ۸۰ ردیفی ۹۰۷ میلی‌ثانیه، در برابرِ ۱۳۰ با پنهان کردن به‌جای برداشتن.
    /// </summary>
    [Fact]
    public void EveryPageStaysInTheTreeAndOnlyOneIsShown()
    {
        var x = Shell();
        Assert.Contains("ItemsSource=\"{Binding AllPages}\"", x);
        Assert.Contains("IsVisible=\"{Binding IsShown}\"", x);
        Assert.DoesNotContain("<ContentControl Content=\"{Binding Content}\"", x);
    }

    /// <summary>پرده و انیمیشنش سرِ جایشان‌اند.</summary>
    [Fact]
    public void TheCurtainHasAnAnimationAndACounter()
    {
        var x = Shell();
        Assert.Contains("IsVisible=\"{Binding IsWarming}\"", x);
        Assert.Contains("{Binding WarmProgress}", x);
        Assert.Contains("{Binding WarmCount}", x);
        Assert.Contains("IterationCount=\"INFINITE\"", x);

        // ⚠️ ‎RenderTransform‎ انیماتورِ آماده ندارد و همان‌جا استثنا می‌دهد
        // («No animator registered for the property RenderTransform»).
        Assert.DoesNotContain("Value=\"scale(", x);
    }

    /// <summary>
    /// ⚠️ گشتِ پشتِ پرده نباید بارِ یک‌بارمصرفِ داده را خرج کند، وگرنه هر بخش
    /// تا آخرِ عمرِ برنامه روی عکسِ لحظهٔ آغاز می‌ماند.
    /// </summary>
    [Fact]
    public void WarmingGivesBackTheOneShotLoad()
    {
        var vm = Read("PumpYaqobi.App", "ViewModels", "MainViewModel.cs");
        Assert.Contains("sec.IsLoaded = false;", vm);
        Assert.Contains("WarmInnerAsync", vm);
    }

    /// <summary>گرم کردن یک بار در عمرِ برنامه — نه با هر ورود و خروج.</summary>
    [Fact]
    public void WarmingRunsOnlyOnce()
    {
        var w = Read("PumpYaqobi.App", "Services", "WarmUp.cs");
        Assert.Matches(new Regex(@"if \(Done\) return;\s*\r?\n\s*Done = true;"), w);
    }

    /// <summary>
    /// ⚠️ صفحهٔ «تنظیمات» نباید دوباره منتظرِ بررسیِ نسخه بماند: جایی که
    /// اینترنت نباشد، باز شدنش به اندازهٔ مهلتِ اتصال طول می‌کشد
    /// (سنجشِ ‎warm‎: ۱٬۲۶۱ میلی‌ثانیه، و پس از این تغییر ۱۷۵).
    /// </summary>
    [Fact]
    public void SettingsDoesNotWaitForTheUpdateCheck()
    {
        var s = Read("PumpYaqobi.App", "ViewModels", "Sections", "SettingsSectionViewModel.cs");
        var body = s[s.IndexOf("public override Task OnActivatedAsync()", StringComparison.Ordinal)..];
        body = body[..body.IndexOf("\n    }", StringComparison.Ordinal)];
        Assert.DoesNotContain("await CheckUpdateAsync()", body.Replace("try { await CheckUpdateAsync(); } catch { }", ""));
        Assert.Contains("Task.Run", body);
    }
}

/// <summary>
/// ══ داشبورد نباید کلِ دفتر را بخواند ═════════════════════════════════════════
///
/// گزارشِ صاحب ریپو، دو بار: «بازگشت به صفحهٔ اصلی هم همان‌جور کند است.»
///
/// ریشه‌اش این بود که ‎RefreshAsync‎ با هر بار باز شدنِ داشبورد
/// ‎ListAsync(null)‎ می‌زد — یعنی همهٔ مصارف و همهٔ ردیف‌های گاوصندوقِ همهٔ
/// سال‌ها به شیءِ کامل درمی‌آمدند تا چهار عدد حساب شود.
///
/// عددش را ‎dotnet run --project PumpYaqobi.UiTests -- dashperf‎ می‌سنجد (با
/// دفترِ چندساله، نه با دانهٔ ده‌ردیفی که سال‌ها این را پنهان کرده بود).
/// این‌جا فقط همان الگو قفل می‌شود، چون برگشتنی است.
/// </summary>
public class DashboardReadsTests
{
    private static readonly string Root =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    private static string Dash() => File.ReadAllText(Path.Combine(
        Root, "PumpYaqobi.App", "ViewModels", "Sections", "DashboardSectionViewModel.cs"));

    [Fact]
    public void TheDashboardNeverPullsEveryLedgerRow()
    {
        var s = Dash();
        Assert.DoesNotContain("ExpenseLedger.ListAsync(null)", s);
        Assert.DoesNotContain("SafeLedger.ListAsync(null)", s);
        Assert.DoesNotContain("Invoices.ListAsync()", s);
        Assert.DoesNotContain("Debtors.ListAsync()", s);
    }

    /// <summary>
    /// ⚠️ «هفته» می‌تواند از سرِ سال به سالِ پیش برگردد، پس مصارف باید **دو
    /// سال** خوانده شوند نه یکی — وگرنه اولِ سال، عددِ هفته کم می‌آید.
    /// </summary>
    [Fact]
    public void ExpensesCoverLastYearToo()
    {
        var s = Dash();
        Assert.Contains("ExpenseLedger.ListAsync(year + \"/\")", s);
        Assert.Contains("ExpenseLedger.ListAsync(prev + \"/\")", s);
    }

    /// <summary>ماندهٔ گاوصندوق همهٔ تاریخ را می‌خواهد — ولی سه ستون، نه کلِ شیء.</summary>
    [Fact]
    public void TheSafeBalanceReadsThreeColumnsNotWholeRows()
    {
        var s = Dash();
        Assert.Contains("SafeLedger.SelectAsync", s);

        // ⚠️ ‎SUM‎ هرگز به SQLite داده نمی‌شود: مبلغ‌ها متن‌اند و
        // ‎CAST(... AS REAL)‎ برای حساب‌داری خطرناک است. کامنت‌ها کنار
        // گذاشته می‌شوند، وگرنه خودِ همین هشدار آزمون را قرمز می‌کند.
        var led = File.ReadAllText(Path.Combine(
            Root, "PumpYaqobi.Services", "Data", "LedgerService.cs"));
        var code = string.Join("\n", led.Split('\n')
                                        .Where(l => !l.TrimStart().StartsWith("//")));
        Assert.DoesNotContain("CAST(", code);
        Assert.DoesNotContain("FromSqlRaw", code);
    }
}
