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
    private string _link = "";
    private string _name = "";

    // ⚠️ ‎InitializeComponent()‎ لازم است، نه ‎AvaloniaXamlLoader.Load‎:
    // فیلدهای ‎x:Name‎ را آن پر می‌کند. توضیحِ کامل در ‎DialogWindow‎.
    public QrWindow() => InitializeComponent();

    /// <summary>کیو‌آرِ آماده را نشان بده. ‎png‎ی خالی یعنی چیزی برای نشان دادن نیست.</summary>
    public static QrWindow For(string name, string link, byte[]? png, string hint)
    {
        var w = new QrWindow();
        w.NameText.Text = name;
        w.HintText.Text = hint;
        w.LinkText.Text = link;
        w._png = png;
        w._link = link ?? "";
        w._name = name ?? "";
        w._suggested = "qr-" + Sanitize(name) + ".png";

        // بی نشانی، هیچ‌کدام از راه‌های فرستادن معنا ندارند
        var can = w._link.Length > 0;
        w.CopyBtn.IsEnabled = can;
        w.TextBtn.IsEnabled = can;
        w.WaBtn.IsEnabled = can;
        w.TgBtn.IsEnabled = can;

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

    // ══ فرستادن ═════════════════════════════════════════════════════════════

    /// <summary>
    /// پیامِ آماده — نامِ حساب و نشانی‌اش، و بس.
    ///
    /// ⛔ هیچ رمزی در آن نیست: نشانیِ کیو‌آر فقط رمزِ همان <b>یک</b> حساب را
    /// دارد (‎k‎)، نه رمزِ برنامه و نه رمزِ خواندنِ سرور — همان قاعدهٔ
    /// ‎KarLink.ShareText‎.
    /// </summary>
    private string ShareText() =>
        (_name.Trim().Length > 0 ? "حسابِ «" + _name.Trim() + "»\n\n" : "")
        + _link
        + "\n\nبا باز کردنِ این نشانی، حسابتان را می‌بینید.";

    private async void OnCopyLink(object? s, RoutedEventArgs e)
    {
        if (_link.Length == 0) return;
        await Dialogs.CopyAsync(_link);
    }

    private async void OnCopyText(object? s, RoutedEventArgs e)
    {
        if (_link.Length == 0) return;
        await Dialogs.CopyAsync(ShareText());
    }

    private void OnWhatsApp(object? s, RoutedEventArgs e) =>
        Open("https://wa.me/?text=" + Uri.EscapeDataString(ShareText()));

    private void OnTelegram(object? s, RoutedEventArgs e) =>
        Open("https://t.me/share/url?url=" + Uri.EscapeDataString(_link)
             + "&text=" + Uri.EscapeDataString(_name.Trim()));

    /// <summary>
    /// نشانی را با مرورگرِ خودِ سیستم باز می‌کند.
    ///
    /// ⚠️ هیچ‌وقت استثنا بیرون نمی‌دهد: روی ماشینی که مرورگرِ پیش‌فرض ندارد
    /// این کار شکست می‌خورد و نباید پنجرهٔ کیو‌آر را ببندد.
    /// </summary>
    private static void Open(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return;
        try
        {
            System.Diagnostics.Process.Start(
                new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch { /* مرورگری نبود — کپیِ لینک همیشه هست */ }
    }

    private void OnClose(object? s, RoutedEventArgs e) => Close();
}
