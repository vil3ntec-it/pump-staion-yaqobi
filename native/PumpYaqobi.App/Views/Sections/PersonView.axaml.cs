using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;

namespace PumpYaqobi.App.Views.Sections;

public partial class PersonView : UserControl
{
    public PersonView() => AvaloniaXamlLoader.Load(this);

    /// <summary>
    /// ══ کادرِ «مقدار رسید»ِ سربرگ — همتای ‎personRasidFocus‎ی سایت ═══════════
    ///
    /// کادر جمعِ همهٔ رسیدهای دفتر را نشان می‌دهد. با دست خوردن خالی می‌شود،
    /// چون عددی که می‌نویسید «رسیدِ تازه» است نه ویرایشِ آن جمع. با بیرون رفتن
    /// (‎UpdateSourceTrigger=LostFocus‎) همان عدد در دفتر ثبت می‌شود و کادر
    /// دوباره جمعِ تازه را نشان می‌دهد.
    ///
    /// بی این، کاربر روی «۵٬۰۰۰» می‌نوشت «۲٬۰۰۰» و جمع ۷٬۰۰۰ می‌شد — همان
    /// چیزی که در سایت هم با پاک شدنِ کادر جلویش گرفته شده.
    /// </summary>
    private void RasidBoxFocus(object? sender, GotFocusEventArgs e)
    {
        if (sender is TextBox tb) tb.Text = "";
    }

    /// <summary>
    /// ‎Enter‎ همان‌جا ثبت کند — بی این، کاربر باید از کادر بیرون می‌رفت.
    /// (همتای ‎onkeydown=Enter ⇒ blur‎ی کادرِ رسیدِ سایت.)
    /// </summary>
    private void RasidBoxKey(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || sender is not TextBox tb) return;
        // فوکوس که برود، ‎LostFocus‎ همان لحظه عدد را در دفتر می‌نشاند
        TopLevel.GetTopLevel(tb)?.FocusManager?.ClearFocus();
        e.Handled = true;
    }
}
