namespace PumpYaqobi.Tests;

/// <summary>
/// ══ ترتیبِ راه‌اندازی ═══════════════════════════════════════════════════════
///
/// باگی که برنامهٔ صاحب ریپو را سفید و مرده کرد:
/// ‎Program.Main‎ پیش از ‎BuildAvaloniaApp()‎ به ‎Dispatcher.UIThread‎ دست زد.
/// آوالونیا با همان دست زدن دیسپچر را همان‌جا می‌سازد، و چون هنوز
/// ‎UsePlatformDetect()‎ اجرا نشده بود، دیسپچر به حلقهٔ پیام‌های ویندوز وصل
/// نمی‌شد: پنجره باز می‌شد، نوارِ عنوان می‌آمد، و هیچ چیز رسم نمی‌شد.
///
/// ⚠️ چرا آزمونِ متنی و نه رفتاری: این باگ فقط روی ویندوزِ واقعی دیده می‌شود.
/// همهٔ آزمون‌ها و سنجش‌های این ریپو روی لینوکسِ بی‌نمایشگر اجرا می‌شوند و
/// آن‌جا دیسپچر دستی پمپ می‌شود، پس هیچ‌کدام این را نگرفتند — و نمی‌گیرند.
/// تنها چیزی که جلویش را می‌گیرد، همین قاعدهٔ ساده است.
/// </summary>
public class StartupOrderTests
{
    private static readonly string Root =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    private static string Read(params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { Root, "PumpYaqobi.App" }.Concat(parts).ToArray()));

    /// <summary>
    /// ‎Program.Main‎ پیش از راه‌اندازیِ آوالونیا اجرا می‌شود، پس حق ندارد به
    /// دیسپچر — یا هر چیزِ دیگری از آوالونیا که آن را می‌سازد — دست بزند.
    /// </summary>
    [Fact]
    public void ProgramDoesNotTouchTheDispatcherBeforeAvaloniaStarts()
    {
        var src = Read("Program.cs");
        Assert.False(src.Contains("Dispatcher", StringComparison.Ordinal),
            "‎Program.cs‎ به ‎Dispatcher‎ دست زده — همان باگی که پنجره را سفید کرد. "
            + "گیرندهٔ خطای نخِ رابط باید در ‎App.OnFrameworkInitializationCompleted‎ بنشیند.");
    }

    /// <summary>تورِ بی‌آوالونیا (AppDomain و Task) باید همان اولِ کار باشد.</summary>
    [Fact]
    public void ProgramStillInstallsTheNonAvaloniaSafetyNet()
        => Assert.Contains("CrashGuard.Install()", Read("Program.cs"), StringComparison.Ordinal);

    /// <summary>
    /// و گیرندهٔ نخِ رابط باید واقعاً جایی نصب شود، وگرنه یک استثنای ساده
    /// دوباره کلِ برنامه را می‌بندد.
    /// </summary>
    [Fact]
    public void TheUiSafetyNetIsInstalledAfterAvaloniaIsUp()
        => Assert.Contains("CrashGuard.InstallUi()", Read("App.axaml.cs"), StringComparison.Ordinal);
}
