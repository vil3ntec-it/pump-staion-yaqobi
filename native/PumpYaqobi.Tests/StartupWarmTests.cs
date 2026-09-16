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

    /// <summary>
    /// پردهٔ لودینگ فقط در حالتِ ‎Starting‎ است، و ساده: لوگو، نوارِ خطی،
    /// و درصد. ⚠️ نه نامِ بخش — خواستهٔ صاحب ریپو «یک لودینگِ ساده و معمولی»
    /// بود و پیش از احراز هویت هیچ چیزی از درونِ برنامه نباید دیده شود.
    /// </summary>
    [Fact]
    public void TheCurtainHasAnAnimationAndACounter()
    {
        var x = Shell();
        Assert.Contains("IsVisible=\"{Binding IsStarting}\"", x);
        Assert.Contains("{Binding WarmProgress}", x);
        Assert.Contains("{Binding WarmPercentText}", x);
        Assert.Contains("IterationCount=\"INFINITE\"", x);
        Assert.DoesNotContain("{Binding WarmText}", x);

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
        // گذرِ دومِ داده دیگر پس از رمز اجرا نمی‌شود («بعد از رمز هنوز کند است»)
        Assert.DoesNotContain("Dispatcher.UIThread.Post(() => _ = WarmDataAsync()", vm);
    }

    /// <summary>
    /// ⚠️ گرم کردن هرگز نباید ناوبری کند. ریشهٔ گزارشِ «کاربر به بخش‌های
    /// مختلف منتقل می‌شود و بعد به قفل برمی‌گردد» همین بود: گذرِ گرم کردن با
    /// ‎GoAsync‎ در بخش‌ها می‌گشت و فقط به پرده تکیه می‌کرد.
    /// </summary>
    [Fact]
    public void WarmingNeverNavigates()
    {
        // ⚠️ کامنت‌ها کنار گذاشته می‌شوند: خودِ توضیحِ همین باگ نامِ ‎GoAsync‎
        // را دارد و بی این، آزمون خودش را قرمز می‌کرد.
        var w = Read("PumpYaqobi.App", "Services", "WarmUp.cs");
        var code = string.Join("\n", w.Split('\n')
                                      .Where(l => !l.TrimStart().StartsWith("//")));
        Assert.DoesNotContain("GoAsync", code);

        // ⚠️ گرم کردن با «دیده‌شو» ی خودِ بخش انجام می‌شود و در جای دائمیِ
        // نما — نه در قابِ موقت. با قابِ موقت، نما سرِ برداشته شدن از درختِ
        // بصری جدا می‌شود و ‎DataGrid‎ همهٔ ردیف‌هایش را دور می‌اندازد
        // (سنجیده شد: ۱٬۴۷۵ ms در برابرِ ۵۳ی دفعه‌های بعد).
        Assert.Contains("sec.IsShown = true;", code);
        Assert.Contains("sec.IsShown = false;", code);
    }

    /// <summary>
    /// سه حالت، و هر کدام دقیقاً یک صفحه. ⚠️ «کدام صفحه دیده شود» نباید از
    /// ترکیبِ دو پرچم حساب شود — همان ترکیب بود که کاربر را وسطِ لودینگ به
    /// صفحه‌های داخلی می‌بُرد.
    /// </summary>
    [Fact]
    public void EachPhaseShowsExactlyOneScreen()
    {
        var vm = Read("PumpYaqobi.App", "ViewModels", "MainViewModel.cs");
        Assert.Contains("enum AppPhase { Starting, Locked, Ready }", vm);
        Assert.Contains("IsStarting => Phase == AppPhase.Starting", vm);
        Assert.Contains("IsLockVisible => Phase == AppPhase.Locked", vm);
        // ⚠️ پوسته حینِ ‎Starting‎ هم چیده می‌شود — ولی زیرِ پردهٔ **مات**.
        // نمایی که در درختِ بصری نباشد اصلاً چیده نمی‌شود و گرم کردن فقط
        // ادایش را درمی‌آورد. روی صفحهٔ قفل اما نیست.
        // پوسته زیرِ قفل هم هست تا بقیهٔ صفحه‌ها پشتِ صفحهٔ رمز گرم شوند؛
        // خودِ صفحهٔ رمز مات و روی همه است (WarmAudit).
        Assert.Contains("IsShellVisible => true;", vm);
        Assert.Contains("WarmRestAsync", vm);
        Assert.Contains("() => Phase == AppPhase.Locked", vm);
    }

    /// <summary>گرم کردن یک بار در عمرِ برنامه — نه با هر ورود و خروج.</summary>
    [Fact]
    public void WarmingRunsOnlyOnce()
    {
        var w = Read("PumpYaqobi.App", "Services", "WarmUp.cs");
        // دو نوبت صدا زده می‌شود (پرده و پشتِ قفل)، پس Done دیگر در را نمی‌بندد؛
        // صفحهٔ گرم‌شده با _warmed دوباره گرم نمی‌شود.
        Assert.Contains("Done = true;", w);
        Assert.Contains("if (!_warmed.Add(sec)) continue;", w);
    }

    /// <summary>
    /// ⚠️ صفحه‌ای که کارتِ به‌روزرسانی دارد نباید منتظرِ بررسیِ نسخه بماند:
    /// جایی که اینترنت نباشد، باز شدنش به اندازهٔ مهلتِ اتصال طول می‌کشد
    /// (سنجشِ ‎warm‎: ۱٬۲۶۱ میلی‌ثانیه، و پس از آن تغییر ۱۷۵).
    ///
    /// از ۱۴۰۵/۰۶/۲۸ آن صفحه «بک‌اپ و به‌روزرسانی‌ها» است، نه «تنظیمات».
    /// </summary>
    [Fact]
    public void TheUpdateCardDoesNotWaitForTheVersionCheck()
    {
        var s = Read("PumpYaqobi.App", "ViewModels", "Sections", "BackupSectionViewModel.cs");

        // تنها جایی که بررسی از مسیرِ باز شدنِ صفحه صدا می‌خورد، همان
        // ‎Task.Run‎ی بی‌انتظار است.
        Assert.Contains("_ = Task.Run(async () => { try { await CheckUpdateAsync(); } catch { } });", s);

        var body = s[s.IndexOf("private Task RefreshAsync()", StringComparison.Ordinal)..];
        body = body[..body.IndexOf("\n    }", StringComparison.Ordinal)];
        Assert.DoesNotContain("await CheckUpdateAsync()",
            body.Replace("try { await CheckUpdateAsync(); } catch { }", ""));
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
