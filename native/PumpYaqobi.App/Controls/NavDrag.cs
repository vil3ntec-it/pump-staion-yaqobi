using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace PumpYaqobi.App.Controls;

/// <summary>
/// ══ جابه‌جا کردنِ بخش‌های نوار با کشیدن و رها کردن (۱۴۰۵/۰۷/۱۶) ═══════════════
///
/// خواستهٔ صاحب ریپو: «بخش‌ها را که جابه‌جا می‌خواهم بکنم، با کشیدنشان — نه
/// کلیکِ راست یا چپ که بزن این سمت یا آن سمت.»
///
/// ── رفتار ──────────────────────────────────────────────────────────────────
///   • فشار روی یک بخش و کشیدن بیش از <see cref="Threshold"/> پیکسل ⇒ کشیدن شروع
///     می‌شود؛ کمتر از آن همان کلیکِ همیشگی است (رفتن به بخش).
///   • زیرِ ماوس، خانه‌ای که بخش پیش از آن می‌نشیند خطِ رنگی می‌گیرد
///     (کلاسِ ‎drop‎) و خودِ بخشِ در حالِ کشیدن کم‌رنگ است (‎dragging‎).
///   • رها کردن ⇒ ‎MainViewModel.MoveNavTo‎ (فقط ترتیبِ دیدن — ‎NavOrder‎).
///   • ‎Esc‎ یا از دست رفتنِ ماوس ⇒ هیچ چیزی عوض نمی‌شود.
///
/// ⚠️ رویدادها <b>تونلی</b> و با ‎handledEventsToo‎ گرفته می‌شوند: خودِ ‎Button‎
/// فشار را مصرف می‌کند. وقتی کشیدن شروع شد، ماوس از دکمه گرفته می‌شود
/// (‎Capture‎)، پس رها کردن دیگر «کلیک» نیست و به بخش نمی‌رود.
/// ⚠️ جهت از جای واقعیِ خانه‌ها خوانده می‌شود، نه از ‎FlowDirection‎: در نوارِ
/// راست‌به‌چپ خانهٔ اول سمتِ راست است، و این حدس نیست — سنجیده می‌شود.
/// </summary>
public sealed class NavDrag
{
    public const double Threshold = 8;

    private readonly ItemsControl _items;
    private readonly Action<object, int> _drop;
    private Button? _source;
    private Point _start;
    private bool _dragging;
    private Button? _mark;

    /// <summary>کشیدن همین حالا در جریان است (برای سنجه‌ها).</summary>
    public bool Dragging => _dragging;

    private NavDrag(ItemsControl items, Action<object, int> drop)
    {
        _items = items;
        _drop = drop;
        items.AddHandler(InputElement.PointerPressedEvent, OnPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
        items.AddHandler(InputElement.PointerMovedEvent, OnMoved, RoutingStrategies.Tunnel, handledEventsToo: true);
        items.AddHandler(InputElement.PointerReleasedEvent, OnReleased, RoutingStrategies.Tunnel, handledEventsToo: true);
        items.AddHandler(InputElement.PointerCaptureLostEvent, (_, _) => { if (_dragging) Cancel(); }, RoutingStrategies.Bubble, handledEventsToo: true);
        items.AddHandler(InputElement.KeyDownEvent, (_, e) => { if (e.Key == Key.Escape && _dragging) { Cancel(); e.Handled = true; } },
            RoutingStrategies.Tunnel, handledEventsToo: true);
    }

    /// <summary>روی فهرستِ بخش‌های نوار سوار می‌شود. ‎drop(item, index)‎ = «پیش از خانهٔ index».</summary>
    public static NavDrag Attach(ItemsControl items, Action<object, int> drop) => new(items, drop);

    private void OnPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(_items).Properties.IsLeftButtonPressed) return;
        _source = (e.Source as Visual)?.FindAncestorOfType<Button>(includeSelf: true);
        if (_source is null || !_source.Classes.Contains("nav")) { _source = null; return; }
        _start = e.GetPosition(_items);
        _dragging = false;
    }

    private void OnMoved(object? sender, PointerEventArgs e)
    {
        if (_source is null) return;
        var p = e.GetPosition(_items);
        if (!_dragging)
        {
            if (Math.Abs(p.X - _start.X) < Threshold && Math.Abs(p.Y - _start.Y) < Threshold) return;
            _dragging = true;
            e.Pointer.Capture(_items);
            _source.Classes.Add("dragging");
        }
        Mark(Target(p.X));
        e.Handled = true;
    }

    private void OnReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_source is null) return;
        if (!_dragging) { _source = null; return; }
        var index = Target(e.GetPosition(_items).X);
        var item = _source.DataContext;
        End();
        e.Pointer.Capture(null);
        e.Handled = true;
        if (item is not null) _drop(item, index);
    }

    private void Cancel() => End();

    private void End()
    {
        _source?.Classes.Remove("dragging");
        _mark?.Classes.Remove("drop");
        _mark?.Classes.Remove("drop-end");
        _mark = null;
        _source = null;
        _dragging = false;
    }

    /// <summary>دکمه‌های نوار به ترتیبِ فهرست، با وسطِ هر کدام در فضای ‎_items‎.</summary>
    private List<(Button Button, double Center)> Slots()
    {
        var list = new List<(Button, double)>();
        foreach (var c in _items.GetRealizedContainers())
        {
            var b = c as Button ?? (c as Visual)?.GetVisualDescendants().OfType<Button>().FirstOrDefault();
            if (b is null) continue;
            var pt = b.TranslatePoint(new Point(b.Bounds.Width / 2, 0), _items);
            if (pt is { } q) list.Add((b, q.X));
        }
        return list;
    }

    /// <summary>
    /// خانه‌ای که بخش پیش از آن می‌نشیند: شمارِ خانه‌هایی که «پیش از» ماوس‌اند —
    /// در راست‌به‌چپ یعنی سمتِ راستِ ماوس.
    /// </summary>
    public int Target(double x)
    {
        var slots = Slots();
        if (slots.Count == 0) return 0;
        var rtl = slots.Count > 1 && slots[0].Center > slots[^1].Center;
        return slots.Count(s => rtl ? s.Center > x : s.Center < x);
    }

    private void Mark(int index)
    {
        var slots = Slots();
        Button? next = null;
        var end = false;
        if (index < slots.Count) next = slots[index].Button;
        else if (slots.Count > 0) { next = slots[^1].Button; end = true; }
        if (ReferenceEquals(next, _mark) && (_mark?.Classes.Contains("drop-end") ?? false) == end) return;
        _mark?.Classes.Remove("drop");
        _mark?.Classes.Remove("drop-end");
        _mark = next;
        _mark?.Classes.Add(end ? "drop-end" : "drop");
    }
}
