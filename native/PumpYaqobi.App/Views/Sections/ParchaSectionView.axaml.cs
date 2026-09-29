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
    ///   Enter (داخلِ کادرهای کارتِ شیفت) ⇒ همان دکمهٔ «💾 ذخیره شیفت …»ِ همان کارت
    ///   Tab (هر جای صفحهٔ پارچه)          ⇒ همان دکمهٔ «⛽/🟤»ِ تعویضِ تیل
    ///
    /// ⛔ هیچ منطقِ تازه‌ای نیست — دو فرمانِ موجود. کلیدهای جهت دست نخوردند.
    /// ⛔ تکملهٔ باز (نامِ کارمند) اول می‌آید: Enter/Tab همان را می‌پذیرند.
    /// </summary>
    private void OnKeys(object? sender, KeyEventArgs e)
    {
        if (DataContext is not ParchaSectionViewModel vm || !vm.ShowMain) return;
        var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as Control;

        //  ══ Ctrl+Tab ⇒ کارتِ روز ⇄ کارتِ شب (۱۴۰۵/۰۷/۱۷، دوم) ══════════════
        //  همان لحظهٔ فشار؛ Ctrl نگه‌داشته و هر Tab یک بار — به‌جای Ctrl+۱/۲ که با
        //  رها کردنِ کلید کار می‌کرد. ⛔ هیچ داده‌ای عوض نمی‌شود، فقط جای نوشتن.
        if (e.Key == Key.Tab && e.KeyModifiers.HasFlag(KeyModifiers.Control)
            && !e.KeyModifiers.HasFlag(KeyModifiers.Alt))
        {
            bool In(string card) => focused is not null && this.FindControl<ContentControl>(card) is { } c
                                    && (ReferenceEquals(focused, c) || focused.GetVisualAncestors().Contains(c));
            //  در روز ⇒ شب · در شب ⇒ روز · هیچ‌کدام ⇒ روز
            var target = this.FindControl<ContentControl>(In("DayCard") ? "NightCard" : "DayCard");
            var box = target?.GetVisualDescendants().OfType<TextBox>()
                             .FirstOrDefault(t => t.IsEffectivelyVisible && t.IsEnabled && !t.IsReadOnly);
            box?.Focus(NavigationMethod.Tab);
            box?.SelectAll();
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

        if (e.Key == Key.Enter && focused is TextBox { AcceptsReturn: false } tb
            && tb.DataContext is ShiftFormViewModel form)
        {
            //  کادرِ فعلی اول بنشیند (کادرهای کارت با هر حرف می‌نشینند؛ این فقط احتیاط است)
            if (form.SaveCommand.CanExecute(null)) form.SaveCommand.Execute(null);
            e.Handled = true;
        }
    }
}
