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
