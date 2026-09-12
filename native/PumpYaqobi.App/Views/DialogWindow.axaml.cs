using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace PumpYaqobi.App.Views;

/// <summary>
/// پنجرهٔ گفت‌وگوی کوچک — «بپرس» (prompt) و «مطمئنی؟» (confirm) نسخهٔ وب،
/// این‌بار پنجرهٔ واقعیِ ویندوز. نتیجه‌اش رشته است: ‎null‎ یعنی انصراف.
/// </summary>
public partial class DialogWindow : Window
{
    private string? _result;

    // ⚠️ ‎InitializeComponent()‎ و نه ‎AvaloniaXamlLoader.Load(this)‎ — و این
    // فرقِ ظاهری نیست:
    //
    // فیلدهای ‎x:Name‎ (‎TitleText‎، ‎Input‎، ‎OkBtn‎ …) را **کدِ تولیدشدهٔ**
    // ‎InitializeComponent‎ پر می‌کند. با صدا زدنِ مستقیمِ ‎Load‎، خودِ XAML
    // بار می‌شود ولی آن فیلدها ‎null‎ می‌مانند — و اولین دست زدن به آن‌ها
    // می‌شود «‎Object reference not set to an instance of an object‎».
    //
    // همان پیامی که صاحب ریپو گرفت و هیچ آزمونی نمی‌دیدش: تنها سنجشی که این
    // مسیر را می‌دواند با ‎Dialogs.PromptHook‎ از خودِ پنجره رد می‌شد. حالا
    // سنجشِ ‎dialogs‎ پنجره‌های واقعی را می‌سازد و همین را می‌پاید.
    public DialogWindow() => InitializeComponent();

    public static DialogWindow ForPrompt(string title, string message, string initial, string ok)
    {
        var w = new DialogWindow();
        w.TitleText.Text = title;
        w.MessageText.Text = message;
        w.MessageText.IsVisible = message.Length > 0;
        w.Input.IsVisible = true;
        w.Input.Text = initial;
        w.OkBtn.Content = ok;
        w.Opened += (_, _) => { w.Input.Focus(); w.Input.SelectAll(); };
        return w;
    }

    public static DialogWindow ForConfirm(string title, string message, string ok, string cancel)
    {
        var w = new DialogWindow();
        w.TitleText.Text = title;
        w.MessageText.Text = message;
        w.OkBtn.Content = ok;
        w.CancelBtn.Content = cancel;
        return w;
    }

    private void OnOk(object? s, RoutedEventArgs e)
    {
        _result = Input.IsVisible ? (Input.Text ?? "") : "";
        Close(_result);
    }

    private void OnCancel(object? s, RoutedEventArgs e) => Close(null);

    private void OnInputKey(object? s, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) OnOk(s, e);
        else if (e.Key == Key.Escape) OnCancel(s, e);
    }
}
