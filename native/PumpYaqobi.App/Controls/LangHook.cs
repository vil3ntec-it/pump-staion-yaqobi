using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using PumpYaqobi.Domain;

namespace PumpYaqobi.App.Controls;

/// <summary>
/// ══ 🌐 هر نوشتهٔ رابط به زبانِ برنامه (۱۴۰۵/۰۷/۲۸) ══════════════════════════════
///
/// یک قلاب برای کلِ برنامه، به‌جای دست زدن به ۵۷ صفحه و ۲۰۰ فایل: هر بار که متنِ یک
/// <see cref="TextBlock"/> (و هر چه از آن ساخته می‌شود: برچسبِ دکمه، سرستون، راهنمای
/// کادر، کشویی، ‎ToolTip‎)، یک <see cref="Run"/> یا عنوانِ پنجره عوض شود، از
/// <see cref="Lang.T"/> می‌گذرد. ⛔ متنِ <see cref="TextBox"/> (نوشتهٔ کاربر) دست نمی‌خورد.
///
/// ⚡ فقط وقتی زبان فارسی نیست نصب می‌شود — در فارسی هیچ شنونده‌ای نیست
/// (قانونِ همیشگیِ سرعت). <c>SetCurrentValue</c> اتصالِ داده را نمی‌بُرد: مقدارِ تازهٔ
/// اتصال دوباره از همین‌جا می‌گذرد.
/// </summary>
public static class LangHook
{
    private static bool _installed;

    public static void Install()
    {
        if (_installed || !Lang.On) return;
        _installed = true;
        TextBlock.TextProperty.Changed.AddClassHandler<TextBlock>((tb, e) =>
        {
            if (e.NewValue is string s && Swap(s) is { } t) tb.SetCurrentValue(TextBlock.TextProperty, t);
        });
        Run.TextProperty.Changed.AddClassHandler<Run>((r, e) =>
        {
            if (e.NewValue is string s && Swap(s) is { } t) r.SetCurrentValue(Run.TextProperty, t);
        });
        Window.TitleProperty.Changed.AddClassHandler<Window>((w, e) =>
        {
            if (e.NewValue is string s && Swap(s) is { } t) w.SetCurrentValue(Window.TitleProperty, t);
        });
    }

    private static string? Swap(string s)
    {
        if (s.Length == 0) return null;
        var t = Lang.T(s);
        return ReferenceEquals(t, s) || t == s ? null : t;
    }
}
