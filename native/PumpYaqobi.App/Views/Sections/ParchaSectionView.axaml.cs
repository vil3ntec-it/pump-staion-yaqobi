using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;
using System.Linq;
using PumpYaqobi.App.Controls;
using PumpYaqobi.App.ViewModels.Sections;

namespace PumpYaqobi.App.Views.Sections;

public partial class ParchaSectionView : UserControl
{
    public ParchaSectionView()
    {
        AvaloniaXamlLoader.Load(this);
        //  ⚠️ تونلی: پیش از ‎FieldNavigationService‎ (حبابی روی پنجره) که Enter و
        //  Tab را به کادرِ بعدی می‌برد. ⚠️ ولی پس از تکملهٔ درون‌خطی نه — تکمله
        //  روی خودِ کادر است و تونل از بالا به پایین می‌رود، پس صریح پرسیده می‌شود.
        AddHandler(KeyDownEvent, OnKeys, RoutingStrategies.Tunnel);
    }

    /// <summary>
    /// ══ کلیدهای پارچه (۱۴۰۵/۰۷/۱۷) ══════════════════════════════════════════
    ///
    /// خواستهٔ صاحب ریپو: «این سه چهارتا رو که پر می‌کنم باید برم پایین تا ذخیره
    /// رو بزنم؛ جوری کن اینتر بزنم ذخیره بشه — لازم به ماوس و اسکرول نباشه. و
    /// زدنِ تب نوعِ تیل رو عوض کنه و به کادرها کاری نداشته باشه؛ کادرها با
    /// چهار کلید درست کار می‌کنن، به اون‌ها دست نمی‌زنی.»
    ///
    ///   Enter (هر کادرِ کارتِ روز یا شب) ⇒ هر کارتِ پر ذخیره + پارچهٔ جدید (۱۴۰۵/۰۷/۱۸)
    ///   Tab و Ctrl+Tab (هر جای صفحهٔ پارچه) ⇒ همان دکمهٔ «⛽/🟤»ِ تعویضِ تیل
    ///
    /// ⛔ هیچ منطقِ تازه‌ای نیست — دو فرمانِ موجود. کلیدهای جهت دست نخوردند.
    /// ⛔ تکملهٔ باز (نامِ کارمند) اول می‌آید: Enter/Tab همان را می‌پذیرند.
    /// </summary>
    private void OnKeys(object? sender, KeyEventArgs e)
    {
        if (DataContext is not ParchaSectionViewModel vm || !vm.ShowMain) return;
        var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as Control;

        //  ══ Ctrl+Tab ⇒ پطرول ⇄ دیزل (۱۴۰۵/۰۷/۱۸) ════════════════════════════
        //  خواستهٔ صاحب ریپو: «با کنترول تب هم بشه دیزل و پطرول رو زود عوض کرد.»
        //  همان دکمهٔ «⛽/🟤» (‎ToggleFuelCommand‎)، روی هر فشار — Ctrl نگه‌داشته و
        //  هر Tab یک بار. (رفتنِ روز ⇄ شبِ ۱۴۰۵/۰۷/۱۷ پس گرفته شد؛ ورق همان روز/شب است.)
        if (e.Key == Key.Tab && e.KeyModifiers.HasFlag(KeyModifiers.Control)
            && !e.KeyModifiers.HasFlag(KeyModifiers.Alt))
        {
            if (vm.ToggleFuelCommand.CanExecute(null)) vm.ToggleFuelCommand.Execute(null);
            e.Handled = true;
            return;
        }

        if (e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Alt)) return;
        if (Suggest.Showing > 0) return;

        if (e.Key == Key.Tab)
        {
            if (vm.ToggleFuelCommand.CanExecute(null)) vm.ToggleFuelCommand.Execute(null);
            e.Handled = true;
            return;
        }

        //  ══ Enter ⇒ هر کارتی که پر است ذخیره، و پارچهٔ جدید (۱۴۰۵/۰۷/۱۸) ══════
        //  از هر کادرِ دو کارت (روز یا شب) — نه فقط کارتی که پر است. کادرِ دیگرِ
        //  صفحه (تاریخ، جست‌وجو) و دکمه‌ها Enterِ خودشان را دارند و دست نمی‌خورند.
        if (e.Key == Key.Enter && focused is TextBox { AcceptsReturn: false } tb
            && tb.DataContext is ShiftFormViewModel)
        {
            if (!_saving)
            {
                _saving = true;
                _ = SaveAllAsync(vm);
            }
            e.Handled = true;
        }
    }

    private bool _saving;

    private async System.Threading.Tasks.Task SaveAllAsync(ParchaSectionViewModel vm)
    {
        try { await vm.SaveFilledAndNewAsync(); }
        catch (System.Exception ex) { Services.CrashGuard.Write("Enter ⇒ ذخیرهٔ پارچه", ex); }
        finally { _saving = false; }
    }
}
