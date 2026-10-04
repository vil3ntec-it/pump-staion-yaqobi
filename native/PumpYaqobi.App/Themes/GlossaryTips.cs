using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using PumpYaqobi.Application.Localization;

namespace PumpYaqobi.App.Themes;

/// <summary>
/// شورا، ث۵ — سرستون یا عنوانِ بخشی که واژهٔ برنامه است (پایه، ختم، الباقی…)
/// زیرِ ماوس معنایش را می‌گوید.
///
/// ⚡ <b>تنبل است، و این عمدی است</b>: ToolTip فقط وقتی ماوس **اول بار** روی همان
/// سرستون یا عنوان می‌رود گذاشته می‌شود. نسخهٔ نخست (گشتنِ سرستون‌ها با هر
/// جدول، و اتصالِ ToolTip روی عنوانِ هر بخش) بارِ نخستِ ToolTip را وسطِ اسکرول
/// می‌انداخت — سنجهٔ ‎bigtable‎ یک گامِ ~۳۶۰ms دید (روی ‎main‎ ~۹۰). حالا تا کسی
/// ماوس را روی سرستون نبرد، هیچ کاری نمی‌شود.
/// </summary>
public static class GlossaryTips
{
    private static bool _installed;

    public static void Install()
    {
        if (_installed) return;
        _installed = true;
        InputElement.PointerEnteredEvent.AddClassHandler<DataGridColumnHeader>((h, _) =>
            Tip(h, h.Content as string ?? (h.Content as TextBlock)?.Text));
        InputElement.PointerEnteredEvent.AddClassHandler<TextBlock>((t, _) =>
        {
            if (t.Classes.Contains("card-title")) Tip(t, t.Text);
        });
    }

    private static void Tip(Control c, string? text)
    {
        if (ToolTip.GetTip(c) is not null) return;
        try { if (Glossary.MeaningIn(text) is { } m) ToolTip.SetTip(c, m); }
        catch { /* راهنما رفاه است */ }
    }
}
