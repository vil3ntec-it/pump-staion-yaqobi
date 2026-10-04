namespace PumpYaqobi.App.Services;

/// <summary>
/// ══ شورا، ج۵ — نخِ رابط بی آوالونیا ═══════════════════════════════════════════
///
/// پوسته (<c>PumpYaqobi.Shell</c>) آوالونیا نمی‌شناسد؛ هر جا که باید روی نخِ رابط
/// برود (توست، دامِ خطای نخِ رابط) از همین در می‌رود. ⛔ برنامه همان لحظه‌ای که
/// اسمبلی‌اش بار می‌شود این‌ها را به <c>Dispatcher.UIThread</c>ِ آوالونیا وصل
/// می‌کند (<c>UiThreadAvalonia</c>، یک <c>ModuleInitializer</c>) — پس رفتار همان
/// است که پیش از جدایی بود.
/// <para>⚠️ پیش‌فرض (وقتی آوالونیایی نیست): همین نخ، همین حالا.</para>
/// </summary>
public static class UiThread
{
    static UiThread() => ShellBoot.Ensure();

    public static Func<bool> CheckAccess { get; set; } = () => true;
    public static Action<Action> Post { get; set; } = a => a();

    /// <summary>دامِ خطای بی‌صاحبِ نخِ رابط. ورودی: «این خطا را بگیر؛ true یعنی رسیدگی شد».</summary>
    public static Action<Func<Exception, bool>> HookUnhandled { get; set; } = _ => { };
}
