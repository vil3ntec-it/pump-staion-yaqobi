using System.Globalization;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using PumpYaqobi.Application.Localization;

namespace PumpYaqobi.App.Views;

/// <summary>
/// ══ «📝 ورق با تاریخ» ══════════════════════════════════════════════════════
///
/// همتای ‎#waraqDateModal‎ · ‎openWaraqDatePicker‎ · ‎confirmWaraqDate‎ی نسخهٔ وب.
/// نتیجه‌اش تاریخِ شمسیِ انتخاب‌شده است؛ ‎null‎ یعنی انصراف.
///
/// ⚠️ ورقِ تکراری ساخته نمی‌شود و این پنجره خودش هم پیش از تایید می‌گوید
/// کدام است: «📂 ورقِ این تاریخ از قبل هست» یا «🆕 ورقِ تازه ساخته می‌شود» —
/// دقیقاً همان دو جملهٔ ‎_wqDateHint‎ی سایت. تصمیمِ نهایی را خودِ
/// ‎WaraqDataService.OpenOrCreateAsync‎ می‌گیرد که با کلیدِ تاریخ می‌گردد،
/// پس ورقی که برای فردا ساخته شود همان ورقی است که پارچهٔ فردا در آن می‌نشیند.
/// </summary>
public partial class WaraqDateWindow : Window
{
    private static readonly PersianCalendar Cal = new();

    /// <summary>کلیدِ تاریخِ ورق‌هایی که از قبل هستند — برای همان جملهٔ راهنما.</summary>
    private IReadOnlyCollection<int> _existing = Array.Empty<int>();

    public WaraqDateWindow() => AvaloniaXamlLoader.Load(this);

    public static WaraqDateWindow For(IReadOnlyCollection<int> existingKeys, string? startDate = null)
    {
        var w = new WaraqDateWindow { _existing = existingKeys };
        w.DateBox.Text = string.IsNullOrWhiteSpace(startDate) ? Shamsi.Today() : startDate;
        w.Refresh();
        return w;
    }

    // ── جابه‌جاییِ تاریخ ──────────────────────────────────────────────────────

    private void OnNextDay(object? s, RoutedEventArgs e) => Step(1, 0);
    private void OnPrevDay(object? s, RoutedEventArgs e) => Step(-1, 0);
    private void OnNextMonth(object? s, RoutedEventArgs e) => Step(0, 1);
    private void OnPrevMonth(object? s, RoutedEventArgs e) => Step(0, -1);

    private void OnToday(object? s, RoutedEventArgs e)
    {
        DateBox.Text = Shamsi.Today();
        Refresh();
    }

    /// <summary>«⌨️ نوشتن» — تا این را نزنند کادر دست‌نخوردنی است، مثلِ سایت.</summary>
    private void OnAllowTyping(object? s, RoutedEventArgs e)
    {
        DateBox.IsReadOnly = false;
        DateBox.Focus();
        DateBox.SelectAll();
    }

    private void Step(int days, int months)
    {
        var d = Shamsi.ToDate(DateBox.Text) ?? DateTime.Today;
        if (months != 0)
        {
            // ماه در تقویمِ **شمسی** جابه‌جا می‌شود، نه با ۳۰ روزِ سرِ دست
            int y = Cal.GetYear(d), m = Cal.GetMonth(d), day = Cal.GetDayOfMonth(d);
            m += months;
            while (m > 12) { m -= 12; y++; }
            while (m < 1) { m += 12; y--; }
            day = Math.Min(day, Cal.GetDaysInMonth(y, m));
            d = Cal.ToDateTime(y, m, day, 0, 0, 0, 0);
        }
        if (days != 0) d = d.AddDays(days);

        // یکدست‌سازی مثلِ ‎confirmWaraqDate‎: «1405/06/01» ⇒ «1405/6/1»
        DateBox.Text = $"{Cal.GetYear(d)}/{Cal.GetMonth(d)}/{Cal.GetDayOfMonth(d)}";
        Refresh();
    }

    // ── راهنمای زیرِ کادر ────────────────────────────────────────────────────

    private void Refresh()
    {
        var raw = (DateBox.Text ?? "").Trim();
        var key = Shamsi.Key(raw);
        if (key == 0 || Shamsi.ToDate(raw) is not { } d)
        {
            Hint.Text = "⚠️ تاریخ را مثل 1405/5/4 بنویسید";
            OkBtn.IsEnabled = false;
            return;
        }

        OkBtn.IsEnabled = true;
        var day = Shamsi.DayName(d) + " — ";
        Hint.Text = _existing.Contains(key)
            ? day + "📂 ورقِ این تاریخ از قبل هست — همان باز می‌شود"
            : day + "🆕 ورقِ تازه برای این تاریخ ساخته می‌شود";
    }

    private void OnDateKey(object? s, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) OnOk(s, e);
        else if (e.Key == Key.Escape) OnCancel(s, e);
        else Avalonia.Threading.Dispatcher.UIThread.Post(Refresh);
    }

    private void OnOk(object? s, RoutedEventArgs e)
    {
        var raw = (DateBox.Text ?? "").Trim();
        if (Shamsi.ToDate(raw) is not { } d) return;
        Close($"{Cal.GetYear(d)}/{Cal.GetMonth(d)}/{Cal.GetDayOfMonth(d)}");
    }

    private void OnCancel(object? s, RoutedEventArgs e) => Close(null);
}
