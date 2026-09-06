using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using PumpYaqobi.App.Views;

namespace PumpYaqobi.App.Services;

/// <summary>
/// گفت‌وگوهای کوچکِ برنامه — پنجره‌های واقعیِ سیستم، نه ‎prompt/confirm‎ِ مرورگر.
///
/// در آزمون‌های بی‌پنجره (headless) پنجرهٔ اصلی وجود ندارد؛ آن‌جا این‌ها بدون
/// نشان دادنِ چیزی «انصراف» برمی‌گردانند تا آزمون معلق نماند.
/// </summary>
public static class Dialogs
{
    private static Window? Owner =>
        Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime d
            ? d.MainWindow : null;

    /// <summary>یک رشته بپرس. ‎null‎ یعنی کاربر انصراف داد.</summary>
    public static async Task<string?> PromptAsync(string title, string message = "",
                                                  string initial = "", string ok = "تایید")
    {
        var owner = Owner;
        if (owner is null) return null;
        return await Dispatcher.UIThread.InvokeAsync(async () =>
            await DialogWindow.ForPrompt(title, message, initial, ok).ShowDialog<string?>(owner));
    }

    /// <summary>«مطمئنی؟» — ‎true‎ فقط وقتی خودِ کاربر تایید کند.</summary>
    public static async Task<bool> ConfirmAsync(string title, string message,
                                                string ok = "بله", string cancel = "انصراف")
    {
        var owner = Owner;
        if (owner is null) return false;
        var r = await Dispatcher.UIThread.InvokeAsync(async () =>
            await DialogWindow.ForConfirm(title, message, ok, cancel).ShowDialog<string?>(owner));
        return r is not null;
    }
}
