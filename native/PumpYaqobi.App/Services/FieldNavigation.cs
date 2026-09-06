using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace PumpYaqobi.App.Services;

/// <summary>
/// ══ ناوبریِ مکانیِ کادرها — همان «اکسل‌مانند»ِ نسخهٔ وب ═══════════════════
///
/// در فرم‌های برنامه (پارچه، خریدِ مخزن، مفاد، حاضری…) کادرها یک شبکه‌اند، نه
/// یک زنجیره. کاربر انتظار دارد کلیدِ «پایین» او را به کادرِ <b>زیرِ</b> همین
/// کادر ببرد، نه به «کادرِ بعدی در ترتیبِ Tab» — که در یک چیدمانِ دوستونه
/// اصلاً همان‌جا نیست.
///
///   • ↑ ↓ → ←  حرکت به نزدیک‌ترین کادر در همان جهت
///   • Enter    مثلِ «پایین»
///   • Tab      روی لیستِ کشویی و تیک، مقدار را عوض می‌کند (وگرنه Tabِ عادی)
///   • → ←      وقتی کُرسر وسطِ متن است، متن را ویرایش می‌کند — نه ناوبری
///
/// ══ جدول‌ها این‌جا نیستند ═══════════════════════════════════════════════════
/// ‎DataGrid‎ خودش ناوبریِ خانه‌به‌خانه دارد و بهتر از هر تقلیدی کار می‌کند، پس
/// اگر فوکوس داخلِ جدول باشد این لایه کنار می‌کشد.
///
/// الگوریتمِ انتخابِ کاندید مو‌به‌مو همان ‎_pickInDirection‎ است، با همان دو
/// درسی که در نسخهٔ وب به قیمتِ باگ به دست آمد:
///
///  ۱) بالا/پایین: اول «ردیفِ بعدی» را از روی نزدیک‌ترین فاصلهٔ عمودی پیدا کن،
///     بعد داخلِ همان ردیف نزدیک‌ترین از نظرِ افقی را بگیر. وگرنه یک کادرِ
///     تمام‌عرض (مثلِ «نام مشتری») همهٔ ردیف‌های میانی را می‌بلعد.
///  ۲) چپ/راست: فقط کادرهایی که واقعاً هم‌ردیف‌اند. وگرنه در فرم‌های دوستونه
///     کادرِ ردیفِ دیگر که «افقی نزدیک‌تر» به نظر می‌رسد برنده می‌شد و حرکت
///     قاطیِ ردیف‌ها می‌کرد.
/// </summary>
public sealed class FieldNavigationService
{
    private readonly TopLevel _root;

    public FieldNavigationService(TopLevel root)
    {
        _root = root;
        // Bubble: اول خودِ کنترل (و جدول) فرصت دارد؛ ما فقط چیزی را می‌گیریم
        // که کسی برنداشته است.
        _root.AddHandler(InputElement.KeyDownEvent, OnKeyDown, RoutingStrategies.Bubble);
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Handled) return;
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Alt)) return;

        var key = e.Key;
        if (key is not (Key.Up or Key.Down or Key.Left or Key.Right or Key.Enter or Key.Tab)) return;

        if (_root.FocusManager?.GetFocusedElement() is not Control from) return;
        if (!IsField(from)) return;

        // جدول ناوبریِ خودش را دارد — دست نمی‌زنیم
        if (from.FindAncestorOfType<DataGrid>() is not null) return;

        // ── Tab روی کشویی/تیک: مقدار عوض شود (همان رفتارِ نسخهٔ وب) ──
        if (key == Key.Tab)
        {
            if (!e.KeyModifiers.HasFlag(KeyModifiers.Shift) && Toggle(from)) e.Handled = true;
            return;
        }

        // ── چپ/راست وسطِ متن = ویرایش، نه ناوبری ──
        if (key is Key.Left or Key.Right && from is TextBox tb && CaretInsideText(tb)) return;

        var scope = ScopeOf(from);
        if (scope is null) return;

        // Enter روی کشویی/تیک هم مقدار را عوض می‌کند؛ وگرنه مثلِ «پایین» است
        if (key == Key.Enter && Toggle(from)) { e.Handled = true; return; }

        var dir = key switch
        {
            Key.Up => Dir.Up,
            Key.Down or Key.Enter => Dir.Down,
            Key.Left => Dir.Left,
            _ => Dir.Right,
        };

        var best = Pick(from, dir, Fields(scope));
        if (best is null) return;

        best.Focus(NavigationMethod.Directional);
        if (best is TextBox t) t.SelectAll();
        e.Handled = true;
    }

    public enum Dir { Up, Down, Left, Right }

    /// <summary>کادرهایی که ایستگاهِ ناوبری‌اند.</summary>
    private static bool IsField(Control c) =>
        c is TextBox or ComboBox or CheckBox or ToggleButton && c.IsEffectivelyEnabled && c.IsEffectivelyVisible;

    /// <summary>
    /// محدودهٔ حرکت: نزدیک‌ترین صفحهٔ بخش. ناوبری هرگز از یک بخش به بخشِ
    /// دیگر یا به نوارِ بالای پنجره نمی‌پرد.
    /// </summary>
    private Control? ScopeOf(Control from) =>
        from.FindAncestorOfType<Controls.SectionPage>() as Control
        ?? from.FindAncestorOfType<UserControl>() as Control
        ?? _root as Control;

    private static List<Control> Fields(Control scope) =>
        scope.GetVisualDescendants()
             .OfType<Control>()
             .Where(c => IsField(c)
                         && c.FindAncestorOfType<DataGrid>() is null
                         && c.Bounds is { Width: > 0, Height: > 0 })
             .ToList();

    /// <summary>مرکزِ کادر در دستگاهِ مختصاتِ محدوده.</summary>
    private static Point? Center(Control c, Visual scope)
    {
        var p = c.TranslatePoint(new Point(c.Bounds.Width / 2, c.Bounds.Height / 2), scope);
        return p;
    }

    private static Control? Pick(Control from, Dir dir, List<Control> all)
    {
        var scope = from.GetVisualRoot() as Visual;
        if (scope is null) return null;
        if (Center(from, scope) is not { } me) return null;

        var boxes = new List<(Control c, Point center)>();
        foreach (var c in all)
        {
            if (ReferenceEquals(c, from)) continue;
            if (Center(c, scope) is { } o) boxes.Add((c, o));
        }

        var i = PickIndex(me, from.Bounds.Height, dir, boxes.Select(b => b.center).ToList());
        return i < 0 ? null : boxes[i].c;
    }

    /// <summary>
    /// قلبِ الگوریتم، جدا از Avalonia تا بشود مو‌به‌مو آزمودش: مرکزِ کادرِ
    /// فعلی، قدِ آن، جهت، و مرکزِ همهٔ کاندیدها → شمارهٔ برنده (یا ‎-1‎).
    /// </summary>
    public static int PickIndex(Point me, double myHeight, Dir dir, IReadOnlyList<Point> candidates)
    {
        var vertical = dir is Dir.Up or Dir.Down;
        var valid = new List<(int i, double primary, double secondary)>();
        var minPrimary = double.PositiveInfinity;

        for (var i = 0; i < candidates.Count; i++)
        {
            var dx = candidates[i].X - me.X;
            var dy = candidates[i].Y - me.Y;

            bool ok;
            double primary, secondary;
            switch (dir)
            {
                case Dir.Up:    ok = dy < -3; primary = -dy; secondary = Math.Abs(dx); break;
                case Dir.Down:  ok = dy > 3;  primary = dy;  secondary = Math.Abs(dx); break;
                case Dir.Left:  ok = dx < -3; primary = -dx; secondary = Math.Abs(dy); break;
                default:        ok = dx > 3;  primary = dx;  secondary = Math.Abs(dy); break;
            }
            if (!ok) continue;
            valid.Add((i, primary, secondary));
            if (vertical && primary < minPrimary) minPrimary = primary;
        }

        if (valid.Count == 0) return -1;

        if (vertical)
        {
            // «ردیفِ بعدی» = نزدیک‌ترین فاصلهٔ عمودی؛ بعد در همان ردیف، نزدیک‌ترینِ افقی
            var band = Math.Max(24, minPrimary * 0.6);
            var best = -1;
            var bestSecondary = double.PositiveInfinity;
            foreach (var v in valid)
                if (v.primary <= minPrimary + band && v.secondary < bestSecondary)
                { bestSecondary = v.secondary; best = v.i; }
            return best;
        }

        // چپ/راست: اول فقط هم‌ردیف‌ها
        var rowEps = Math.Max(24, myHeight * 0.75);
        {
            var best = -1;
            var bestPrimary = double.PositiveInfinity;
            foreach (var v in valid)
                if (v.secondary <= rowEps && v.primary < bestPrimary)
                { bestPrimary = v.primary; best = v.i; }
            if (best >= 0) return best;
        }

        // هیچ هم‌ردیفی نبود → نزدیک‌ترینِ کلی، با وزنِ بیشتر روی محورِ عمودی
        {
            var best = -1;
            var bestScore = double.PositiveInfinity;
            foreach (var v in valid)
            {
                var score = v.primary + v.secondary * 2.2;
                if (score < bestScore) { bestScore = score; best = v.i; }
            }
            return best;
        }
    }

    /// <summary>کشویی یک پله جلو، تیک برعکس. کادرِ متنی عوض نمی‌شود.</summary>
    private static bool Toggle(Control c)
    {
        switch (c)
        {
            case ComboBox cb when cb.ItemCount > 0:
                cb.SelectedIndex = (cb.SelectedIndex + 1) % cb.ItemCount;
                return true;
            case CheckBox chk:
                chk.IsChecked = chk.IsChecked != true;
                return true;
            case ToggleButton tgl:
                tgl.IsChecked = tgl.IsChecked != true;
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// کُرسر «وسطِ» متن است؟ یعنی چیزی انتخاب نشده و نه سرِ متن است نه تهِ آن.
    /// فقط در این حالت چپ/راست باید متن را ویرایش کند.
    /// </summary>
    private static bool CaretInsideText(TextBox tb)
    {
        var len = tb.Text?.Length ?? 0;
        if (tb.SelectionStart != tb.SelectionEnd) return false;
        var caret = tb.CaretIndex;
        return caret > 0 && caret < len;
    }
}
