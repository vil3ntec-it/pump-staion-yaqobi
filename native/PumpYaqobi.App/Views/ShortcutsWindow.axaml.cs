using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;

namespace PumpYaqobi.App.Views;

/// <summary>
/// پنجرهٔ «⌨️ میانبرهای صفحه‌کلید» — همان که ‎F1‎ باز می‌کند.
///
/// ⛔ <b>این فهرست تنها جای نوشته شدنِ میانبرهاست.</b> اگر روزی میانبری اضافه
/// یا جابه‌جا شد، همین‌جا هم عوض می‌شود — وگرنه برنامه چیزی را نشان می‌دهد که
/// دیگر درست نیست، و آن از نداشتنِ فهرست بدتر است.
/// ‎ShortcutHelpTests‎ همین را قفل کرده: هر کلیدی که
/// <see cref="Services.ShortcutService"/> می‌گیرد باید این‌جا هم باشد.
/// </summary>
public partial class ShortcutsWindow : Window
{
    /// <summary>یک سطرِ فهرست؛ ‎Key‎ی خالی یعنی «سرگروه».</summary>
    public readonly record struct Row(string Key, string What);

    /// <summary>
    /// خودِ فهرست. ⚠️ ترتیبش همان ترتیبی است که کاربر لازم دارد، نه ترتیبِ کد:
    /// اول آن‌چه هر روز می‌زند.
    /// </summary>
    public static readonly Row[] All = new Row[]
    {
        new("", "گشتن در برنامه"),
        new("Alt + عدد",          "رفتن به بخشِ شمارهٔ N در نوارِ بالا (۰ یعنی دهم)"),
        new("Ctrl + Shift + عدد", "باز کردنِ کارتِ شمارهٔ N در قرض‌داران و شرکت‌ها"),
        new("Ctrl + K",           "ماشین‌حسابِ شناور — هر جای برنامه که باشی"),
        new("F1",                 "همین فهرست"),

        new("", "جدول‌ها"),
        new("Ctrl + عدد",   "افزودنِ N ردیف به جدولِ جلوی رو"),
        new("Shift + عدد",  "برداشتنِ N ردیفِ آخر — خالی‌ها بی‌پرسش، پرها با پرسش"),
        new("Ctrl + Delete","حذفِ همین ردیف"),
        new("Delete",       "خالی کردنِ خانه‌های انتخاب‌شده"),
        new("F2",           "باز کردنِ ویرایشِ خانه (یا دوبار-کلیک) — کلیکِ تک فقط انتخاب است، مثلِ اکسل"),
        new("Enter",        "نشاندنِ مقدار و رفتن به ردیفِ پایین (با Shift: بالا)"),
        new("Tab",          "خانهٔ بعدی (با Shift: خانهٔ پیشین)"),
        new("کلیدهای جهت‌دار", "حرکت بینِ خانه‌ها؛ با Shift انتخابِ چندتایی"),
        new("Home / End",   "سرِ ردیف و تهِ ردیف"),
        new("Esc",          "لغوِ ویرایش و برداشتنِ کادرِ انتخاب"),
        new("دوبار-کلیک روی خطِ ستون", "هم‌قد کردنِ ستون با محتوا — مثلِ اکسل"),

        new("", "کپی، برش و برگشت"),
        new("Ctrl + C", "کپیِ خانه‌های انتخاب‌شده (در اکسل هم چسبانده می‌شود)"),
        new("Ctrl + X", "برش: کپی و بعد خالی کردنِ همان خانه‌ها"),
        new("Ctrl + V", "چسباندن از همان‌جا به بعد — ردیفِ تازه ساخته نمی‌شود"),
        new("Ctrl + A", "انتخابِ همهٔ ردیف‌ها و همهٔ ستون‌ها"),
        new("Ctrl + Z", "برگرداندنِ آخرین کار: ویرایشِ خانه، حذفِ ردیف، حذفِ حساب"),
        new("Ctrl + Y", "دوباره انجام دادنِ همان (یا Ctrl + Shift + Z)"),

        new("", "ذخیره و چاپ"),
        new("Ctrl + S", "ذخیرهٔ فوری — برنامه خودش هم خودکار ذخیره می‌کند"),
        new("Ctrl + P", "پی‌دی‌افِ همان جایی که داخلش هستی"),
    };

    private static ShortcutsWindow? _open;

    public ShortcutsWindow()
    {
        InitializeComponent();
        foreach (var r in All) Rows.Children.Add(Build(r));
        KeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };
    }

    /// <summary>
    /// نشان دادنِ پنجره. ⚠️ دو بار زدنِ ‎F1‎ دو پنجره نمی‌سازد، و در اجرای
    /// بی‌پنجره (آزمون‌ها) هیچ کاری نمی‌کند — همان قاعدهٔ ‎Dialogs‎.
    /// </summary>
    public static async Task ShowAsync()
    {
        if (Avalonia.Application.Current?.ApplicationLifetime
            is not IClassicDesktopStyleApplicationLifetime { MainWindow: { } owner }) return;

        if (_open is { } already) { already.Activate(); return; }

        var w = new ShortcutsWindow();
        _open = w;
        try { await w.ShowDialog(owner); }
        finally { _open = null; }
    }

    /// <summary>
    /// یک سطر. ⚠️ رنگ‌ها با ‎DynamicResource‎ بسته می‌شوند، نه مقدارِ ثابت:
    /// وگرنه با عوض شدنِ تم (آبی ⇄ طلایی) این پنجره رنگِ تمِ قبلی را نگه
    /// می‌داشت — همان قاعدهٔ همیشگیِ برنامه.
    /// </summary>
    private static Control Build(Row r)
    {
        // سرگروه: فقط یک نوشتهٔ پررنگ با کمی فاصلهٔ بالا
        if (r.Key.Length == 0)
            return new TextBlock
            {
                Text = r.What,
                FontWeight = FontWeight.ExtraBold,
                FontSize = 13,
                Margin = new Thickness(0, 16, 0, 5),
            };

        var label = new TextBlock
        {
            Text = r.Key,
            FontWeight = FontWeight.Bold,
            FontSize = 12.5,
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
        };

        var chip = new Border
        {
            CornerRadius = new CornerRadius(7),
            Padding = new Thickness(10, 4),
            MinWidth = 152,
            BorderThickness = new Thickness(1),
            VerticalAlignment = VerticalAlignment.Center,
            Child = label,
        };
        chip[!Border.BackgroundProperty] = new DynamicResourceExtension("Pump.Input");
        chip[!Border.BorderBrushProperty] = new DynamicResourceExtension("Pump.Border");

        var what = new TextBlock
        {
            Text = r.What,
            FontSize = 12.5,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(12, 0, 0, 0),
        };

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*"),
            Margin = new Thickness(0, 2),
        };
        Grid.SetColumn(chip, 0);
        Grid.SetColumn(what, 1);
        grid.Children.Add(chip);
        grid.Children.Add(what);
        return grid;
    }

    private void OnClose(object? s, RoutedEventArgs e) => Close();
}
