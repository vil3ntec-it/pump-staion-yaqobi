using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace PumpYaqobi.App.Services;

/// <summary>
/// ══ «روی صفحه می‌زنم، از آن کادر بیرون نمی‌شوم» ═══════════════════════════════
///
/// گزارشِ صاحب ریپو (۱۴۰۵/۰۷/۰۶): «روی کار یا جدولی هستم، روی صفحه می‌زنم، از
/// آن جدول یا اینپوت بیرون نمی‌شود — این را ریشه‌ای درست کن.»
///
/// ریشه یک قاعدهٔ خودِ آوالونیاست: کلیک روی چیزی که <b>فوکوس‌پذیر نیست</b>
/// (یک ‎Border‎، یک ‎Panel‎، جای خالیِ صفحه) فوکوس را جابه‌جا نمی‌کند. پس
/// کادرِ تایپِ قبلی همچنان فوکوس دارد: مکان‌نما تویش می‌زند، کلیدها به آن
/// می‌روند، و هر اتصالِ ‎LostFocus‎ی (مثلِ کادرِ رسیدِ سربرگِ قرض‌دار) هیچ‌وقت
/// خبردار نمی‌شود.
///
/// این سرویس همان کاری را می‌کند که هر فرمِ وب می‌کند: <b>کلیک روی جای خالی =
/// بیرون آمدن</b>.
///
/// ══ چرا فازِ حبابی و نه تونلی ════════════════════════════════════════════════
/// ⛔ تونلی <b>پیش از</b> خودِ کنترل‌ها می‌دوید و فوکوس را می‌برد — یعنی
/// ‎ExcelGrid‎ هنوز فرصت نکرده بود خانهٔ بازش را بنشاند و مقدارِ نیمه‌تایپ‌شده
/// از دست می‌رفت. در فازِ حبابی، هر کنترلی که فشار را مصرف کند
/// (‎TextBox‎ · ‎Button‎ · خانهٔ جدول · کشویی) رویداد را ‎Handled‎ می‌کند و این
/// شنونده اصلاً صدا زده نمی‌شود. یعنی فقط فشارِ روی <b>جای خالی</b> به این‌جا
/// می‌رسد.
///
/// ⚠️ و یک کمربندِ دوم هم هست: زنجیرهٔ بصریِ فشار بالا پیموده می‌شود و اگر
/// هر جدّی فوکوس‌پذیر باشد (یا کشویی/منوی شناور)، هیچ کاری نمی‌شود. پس
/// کنترلی که فشار را ‎Handled‎ نکند هم قربانی نمی‌شود.
///
/// ⛔ هیچ ‎Dispatcher‎ی و هیچ تأخیری در کار نیست.
/// </summary>
public sealed class FocusOutService
{
    private readonly TopLevel _top;

    public FocusOutService(TopLevel top)
    {
        _top = top;
        _top.AddHandler(InputElement.PointerPressedEvent, OnPressed, RoutingStrategies.Bubble);
    }

    private void OnPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.Handled) return;
        if (e.Source is not Visual v) return;

        for (Visual? x = v; x is not null; x = x.GetVisualParent())
        {
            if (ReferenceEquals(x, _top)) break;              // به پوستهٔ پنجره رسیدیم
            if (x is Popup or FlyoutPresenter) return;         // کشویی/منوی باز
            if (x is InputElement { Focusable: true }) return; // چیزی که خودش فوکوس می‌گیرد
        }

        // جای خالی بود: هر کادری که فوکوس دارد رهایش کند — و ‎LostFocus‎ش
        // شلیک شود تا مقدارش بنشیند.
        if (_top.FocusManager?.GetFocusedElement() is null) return;
        _top.FocusManager.ClearFocus();
    }
}
