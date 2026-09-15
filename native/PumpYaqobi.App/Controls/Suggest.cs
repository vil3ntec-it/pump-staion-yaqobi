using System.Collections;
using System.Collections.Concurrent;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace PumpYaqobi.App.Controls;

/// <summary>
/// ══ پیشنهادِ خودکار — همان ‎data-suggest‎ی سایت ═══════════════════════════════
///
/// خواستهٔ صاحب ریپو: «یک بخشِ مهم تو برنامه نیست: پیشنهادیِ خودکارِ تکمیلِ
/// کلمه‌ها… توی سایت بود؛ بعضی بخش‌ها مثلِ پارچه‌ها اسمِ کارمندها پیشنهاد
/// می‌شد یا فاکتور.»
///
/// سایت دو منبع داشت: فهرست‌های نام‌دارِ خودِ برنامه (‎staff-names-dl‎، …) و
/// دستیارِ تایپ که از آن‌چه کاربر قبلاً نوشته یاد می‌گرفت. این‌جا هر دو یک‌جا:
///
///   • کلیدِ نام‌دار (‎Suggest.Key="staff"‎ روی ستون یا کادر) ⇒ ‎Provide(key, …)‎
///   • و **همیشه** مقدارهای همان ستونِ همان جدول (یادگیری از خودِ دفتر)
///
/// قاعده‌های سایت، عیناً: ↑↓ گرفته نمی‌شوند (ناوبریِ جدول کار می‌کند)، ‎Tab‎ روی
/// پیشنهادها می‌چرخد و بعد از آخری لیست بسته می‌شود تا ‎Tab‎ به خانهٔ بعد برود،
/// ‎Enter‎ پیشنهادِ روشن را برمی‌دارد، ‎Esc‎ می‌بندد، کلیک هم برمی‌دارد. مقدارِ
/// کادر فقط با کارِ صریحِ کاربر عوض می‌شود.
///
/// ⚠️ یک پاپ‌آپ برای کلِ برنامه، ساخته‌شده در نخستین نیاز — نه یکی در هر خانه.
/// </summary>
public static class Suggest
{
    /// <summary>کلیدِ فهرستِ نام‌دار روی ستونِ جدول یا کادرِ تایپ.</summary>
    public static readonly AttachedProperty<string?> KeyProperty =
        AvaloniaProperty.RegisterAttached<AvaloniaObject, string?>("Key", typeof(Suggest));

    public static string? GetKey(AvaloniaObject o) => o.GetValue(KeyProperty);
    public static void SetKey(AvaloniaObject o, string? v) => o.SetValue(KeyProperty, v);

    // ══ منبع‌های نام‌دار ═══════════════════════════════════════════════════
    private static readonly ConcurrentDictionary<string, Func<Task<IEnumerable<string>>>> _providers = new();
    private static readonly ConcurrentDictionary<string, (DateTime At, List<string> Items)> _cache = new();
    private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(20);

    /// <summary>ثبتِ یک فهرستِ نام‌دار (مثلاً «staff» = نامِ کارمندان).</summary>
    public static void Provide(string key, Func<Task<IEnumerable<string>>> source) => _providers[key] = source;

    /// <summary>
    /// فهرستِ یک کلید — از کَش، همان لحظه؛ تازه‌سازی در پس‌زمینه. مسیرِ تایپ
    /// نباید منتظرِ دیتابیس بماند.
    /// </summary>
    public static IReadOnlyList<string> Of(string? key)
    {
        if (string.IsNullOrEmpty(key) || !_providers.TryGetValue(key, out var src)) return Array.Empty<string>();
        var hit = _cache.TryGetValue(key, out var c);
        if (!hit || DateTime.UtcNow - c.At > Ttl)
        {
            _cache[key] = (DateTime.UtcNow, hit ? c.Items : new List<string>());
            _ = Task.Run(async () =>
            {
                try
                {
                    var items = (await src()).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().ToList();
                    _cache[key] = (DateTime.UtcNow, items);
                }
                catch { /* پیشنهاد رفاه است، نه داده */ }
            });
        }
        return hit ? c.Items : Array.Empty<string>();
    }

    // ══ کادرِ تایپِ مستقل (بیرونِ جدول) ═══════════════════════════════════
    static Suggest()
    {
        KeyProperty.Changed.AddClassHandler<TextBox>((tb, _) =>
        {
            tb.GotFocus -= OnBoxFocus; tb.GotFocus += OnBoxFocus;
        });
    }

    private static void OnBoxFocus(object? sender, GotFocusEventArgs e)
    {
        if (sender is TextBox tb) Attach(tb, Of(GetKey(tb)), Array.Empty<string>());
    }

    // ══ پاپ‌آپ ═════════════════════════════════════════════════════════════
    private static Popup? _popup;
    private static StackPanel? _list;
    private static TextBox? _box;
    private static List<string> _all = new();
    private static List<string> _items = new();
    private static int _active = -1;
    private static bool _picking;

    /// <summary>برای سنجش: پاپ‌آپ باز است و چند پیشنهاد دارد؟</summary>
    public static int Showing => _popup is { IsOpen: true } ? _items.Count : 0;
    public static int Active => _active;

    /// <summary>به یک کادرِ تایپ وصل می‌شود: نام‌دار + آموخته‌ها.</summary>
    public static void Attach(TextBox box, IReadOnlyList<string> named, IReadOnlyList<string> learned)
    {
        Detach();
        _all = named.Concat(learned).Where(s => !string.IsNullOrWhiteSpace(s))
                    .Select(s => s.Trim()).Distinct().ToList();
        if (_all.Count == 0) return;

        _box = box;
        box.PropertyChanged += OnBoxText;
        box.LostFocus += OnBoxLost;
        box.AddHandler(InputElement.KeyDownEvent, OnBoxKey, RoutingStrategies.Tunnel);
        box.DetachedFromVisualTree += OnBoxGone;
        Show();
    }

    private static void Detach()
    {
        if (_box is { } b)
        {
            b.PropertyChanged -= OnBoxText;
            b.LostFocus -= OnBoxLost;
            b.RemoveHandler(InputElement.KeyDownEvent, OnBoxKey);
            b.DetachedFromVisualTree -= OnBoxGone;
        }
        _box = null; _items = new(); _active = -1;
        if (_popup is not null) _popup.IsOpen = false;
    }

    private static void OnBoxGone(object? s, VisualTreeAttachmentEventArgs e) => Detach();
    private static void OnBoxLost(object? s, RoutedEventArgs e)
    {
        // مثلِ سایت: کمی صبر، تا کلیک روی خودِ فهرست گم نشود
        var box = _box;
        DispatcherTimer.RunOnce(() => { if (ReferenceEquals(box, _box) && !_picking) Detach(); }, TimeSpan.FromMilliseconds(150));
    }
    private static void OnBoxText(object? s, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == TextBox.TextProperty && !_picking) Show();
    }

    private static void Show()
    {
        if (_box is not { } box) return;
        var q = (box.Text ?? "").Trim();
        _items = (q.Length == 0 ? _all : _all.Where(v => v.Contains(q, StringComparison.OrdinalIgnoreCase)))
                 .Where(v => !string.Equals(v, q, StringComparison.Ordinal))
                 .Take(8).ToList();
        _active = -1;
        if (_items.Count == 0) { if (_popup is not null) _popup.IsOpen = false; return; }

        var p = EnsurePopup();
        _list!.Children.Clear();
        foreach (var v in _items)
        {
            var item = new Border
            {
                Padding = new Thickness(10, 6), CornerRadius = new CornerRadius(6), Cursor = new Cursor(StandardCursorType.Hand),
                Child = new TextBlock { Text = v, FontSize = 14, TextTrimming = TextTrimming.CharacterEllipsis },
                Tag = v,
            };
            item.PointerPressed += (_, e) => { Pick((string)item.Tag!); e.Handled = true; };
            item.PointerEntered += (_, _) => { _active = _list.Children.IndexOf(item); Highlight(); };
            _list.Children.Add(item);
        }
        p.PlacementTarget = box;
        p.Width = Math.Max(160, box.Bounds.Width);
        if (!p.IsOpen) p.IsOpen = true;
        Highlight();
    }

    private static Popup EnsurePopup()
    {
        if (_popup is not null) return _popup;
        _list = new StackPanel { Spacing = 1 };
        var border = new Border
        {
            Child = _list, Padding = new Thickness(4), CornerRadius = new CornerRadius(10), BorderThickness = new Thickness(1),
        };
        border.Bind(Border.BackgroundProperty, border.GetResourceObservable("Pump.Panel"));
        border.Bind(Border.BorderBrushProperty, border.GetResourceObservable("Pump.Border"));
        border.Bind(Border.BoxShadowProperty, border.GetResourceObservable("Pump.CardShadowHover"));
        _popup = new Popup
        {
            Child = border, Placement = PlacementMode.Bottom, VerticalOffset = 4,
            IsLightDismissEnabled = false, Topmost = true,
        };
        return _popup;
    }

    private static void Highlight()
    {
        if (_list is null) return;
        for (var i = 0; i < _list.Children.Count; i++)
        {
            if (_list.Children[i] is not Border b) continue;
            if (i == _active) b.Bind(Border.BackgroundProperty, b.GetResourceObservable("Pump.Selected"));
            else b.Background = Brushes.Transparent;
        }
    }

    private static void Pick(string v)
    {
        if (_box is not { } box) return;
        _picking = true;
        try
        {
            box.Text = v;
            box.CaretIndex = v.Length;
            box.SelectionStart = box.SelectionEnd = v.Length;
        }
        finally { _picking = false; }
        Detach();
    }

    private static void OnBoxKey(object? sender, KeyEventArgs e)
    {
        if (_popup is not { IsOpen: true } || _items.Count == 0) return;
        switch (e.Key)
        {
            case Key.Tab:
                var next = e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? _active - 1 : _active + 1;
                if (next < 0 || next >= _items.Count) { Detach(); return; }   // بگذار Tab به خانهٔ بعد برود
                _active = next; Highlight(); e.Handled = true; return;
            case Key.Enter when _active >= 0:
                Pick(_items[_active]); e.Handled = true; return;
            case Key.Escape:
                Detach(); e.Handled = true; return;
        }
    }

    // ══ کمکی برای جدول: مقدارهای همان ستون ═══════════════════════════════
    /// <summary>مقدارهای متنیِ همان ستون در همهٔ ردیف‌های همین جدول — یادگیری از خودِ دفتر.</summary>
    public static IReadOnlyList<string> ColumnValues(IEnumerable? items, DataGridColumn column)
    {
        if (items is null || column is not DataGridBoundColumn bc || bc.Binding is not Binding b || string.IsNullOrEmpty(b.Path))
            return Array.Empty<string>();
        var path = b.Path;
        System.Reflection.PropertyInfo? prop = null;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var list = new List<string>();
        foreach (var it in items)
        {
            if (it is null) continue;
            prop ??= it.GetType().GetProperty(path);
            if (prop is null || prop.PropertyType != typeof(string)) return Array.Empty<string>();
            if (prop.GetValue(it) is string s && s.Trim() is { Length: > 1 } t && seen.Add(t)) list.Add(t);
            if (list.Count >= 400) break;
        }
        return list;
    }
}
