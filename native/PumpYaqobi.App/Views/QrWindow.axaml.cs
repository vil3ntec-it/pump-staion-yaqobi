using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Markup.Xaml;
using PumpYaqobi.App.Services;

namespace PumpYaqobi.App.Views;

/// <summary>
/// پنجرهٔ «📲 کیو‌آرِ حساب» — همتای ‎#qrModal‎ی نسخهٔ وب.
///
/// در برنامهٔ نیتیو اصلاً نبود: نه کیو‌آری ساخته می‌شد و نه جایی دیده می‌شد،
/// در حالی که در سایت هر حساب کیو‌آرِ خودش را دارد.
/// </summary>
public partial class QrWindow : Window
{
    private byte[]? _png;
    private string _suggested = "qr.png";

    public QrWindow() => AvaloniaXamlLoader.Load(this);

    /// <summary>کیو‌آرِ آماده را نشان بده. ‎png‎ی خالی یعنی چیزی برای نشان دادن نیست.</summary>
    public static QrWindow For(string name, string link, byte[]? png, string hint)
    {
        var w = new QrWindow();
        w.NameText.Text = name;
        w.HintText.Text = hint;
        w.LinkText.Text = link;
        w._png = png;
        w._suggested = "qr-" + Sanitize(name) + ".png";

        if (png is { Length: > 0 })
        {
            using var ms = new MemoryStream(png);
            w.Code.Source = new Bitmap(ms);
        }
        else
        {
            w.Code.IsVisible = false;
            w.SaveBtn.IsEnabled = false;
        }
        return w;
    }

    /// <summary>نامِ فایل نباید حرفی داشته باشد که ویندوز نمی‌پذیرد.</summary>
    private static string Sanitize(string s)
    {
        foreach (var c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '-');
        return s.Trim().Length == 0 ? "acct" : s.Trim();
    }

    private async void OnSave(object? s, RoutedEventArgs e)
    {
        if (_png is not { Length: > 0 }) return;
        var path = await Dialogs.SaveFileAsync("ذخیرهٔ کیو‌آر", _suggested, "عکس", new[] { "*.png" });
        if (string.IsNullOrWhiteSpace(path)) return;
        await File.WriteAllBytesAsync(path, _png);
    }

    private void OnClose(object? s, RoutedEventArgs e) => Close();
}
