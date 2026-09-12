using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using PumpYaqobi.Application.Localization;

namespace PumpYaqobi.App.Views;

/// <summary>
/// ══ «📅 ماه جدید» ══════════════════════════════════════════════════════════
///
/// خواستهٔ صریحِ صاحب ریپو: «کلیک ماه جدید → دیالوگ/فرمِ انتخابِ سال و ماه باز
/// شود → سال انتخاب شود → ماه انتخاب شود → تأیید → ماه در Data Layer
/// ایجاد شود → همان ماه فعال شود → بدون Restart.»
///
/// پیش از این دکمه‌اش بی‌سروصدا ماهِ بعدی را می‌ساخت و هیچ انتخابی نبود.
///
/// ⚠️ ماهی که از قبل هست دوباره ساخته نمی‌شود: همان‌جا گفته می‌شود و دکمه
/// خاموش می‌ماند.
/// </summary>
public partial class MonthPickWindow : Window
{
    private IReadOnlyCollection<string> _existing = Array.Empty<string>();

    public MonthPickWindow() => AvaloniaXamlLoader.Load(this);

    /// <summary>نتیجه: کلیدِ «YYYY/MM»، یا ‎null‎ اگر انصراف داد.</summary>
    public static MonthPickWindow For(IReadOnlyCollection<string> existing)
    {
        var w = new MonthPickWindow { _existing = existing };

        var thisYear = Year(Shamsi.ThisMonth());
        var years = existing.Select(Year).Where(y => y > 0).ToList();
        var lo = years.Count > 0 ? Math.Min(years.Min(), thisYear) : thisYear;
        var hi = (years.Count > 0 ? Math.Max(years.Max(), thisYear) : thisYear) + 1;

        for (var y = hi; y >= lo; y--) w.YearBox.Items.Add(y.ToString("0000"));
        for (var m = 1; m <= 12; m++) w.MonthBox.Items.Add(Shamsi.MonthName(m));

        w.YearBox.SelectedIndex = w.YearBox.ItemCount > 0 ? Math.Max(0, hi - thisYear) : -1;
        w.MonthBox.SelectedIndex = Month(Shamsi.ThisMonth()) - 1;

        w.YearBox.SelectionChanged += (_, _) => w.Check();
        w.MonthBox.SelectionChanged += (_, _) => w.Check();
        w.Opened += (_, _) => w.Check();
        return w;
    }

    private static int Year(string key)
    {
        var s = Shamsi.ToEnDigits(key);
        var i = s.IndexOf('/');
        return i > 0 && int.TryParse(s[..i], out var y) ? y : 0;
    }

    private static int Month(string key)
    {
        var s = Shamsi.ToEnDigits(key);
        var i = s.IndexOf('/');
        return i >= 0 && int.TryParse(s[(i + 1)..], out var m) ? m : 1;
    }

    private string? Key() =>
        YearBox.SelectedItem is string y && MonthBox.SelectedIndex >= 0
            ? y + "/" + (MonthBox.SelectedIndex + 1).ToString("00")
            : null;

    /// <summary>ماهِ تکراری ساخته نشود — همان‌جا گفته می‌شود.</summary>
    private void Check()
    {
        var k = Key();
        var dupe = k is not null && _existing.Contains(k);
        DupeText.IsVisible = dupe;
        DupeText.Text = dupe ? "این ماه از قبل ساخته شده — از کشویی انتخابش کنید." : "";
        OkBtn.IsEnabled = k is not null && !dupe;
    }

    private void OnOk(object? s, RoutedEventArgs e) => Close(Key());
    private void OnCancel(object? s, RoutedEventArgs e) => Close(null);
}
