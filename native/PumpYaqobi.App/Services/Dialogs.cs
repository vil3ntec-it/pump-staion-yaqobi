using Avalonia.Controls;
using Avalonia.Platform.Storage;
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

    /// <summary>
    /// انتخابِ یک فایلِ عکس از خودِ ویندوز — برای «افزودنِ دوربین با عکسِ کیو‌آر».
    /// ‎null‎ یعنی کاربر انصراف داد یا پنجره‌ای در کار نیست (آزمونِ بی‌پنجره).
    /// </summary>
    public static Task<string?> PickImageAsync(string title = "عکسِ کیو‌آر را انتخاب کنید") =>
        PickFileAsync(title, "عکس",
                      new[] { "*.png", "*.jpg", "*.jpeg", "*.webp", "*.bmp", "*.gif" });

    /// <summary>فایلِ بکاپِ نسخهٔ وب (‎pump-backup-….json‎).</summary>
    public static Task<string?> PickJsonAsync(string title = "فایلِ بکاپ را انتخاب کنید") =>
        PickFileAsync(title, "فایلِ بکاپ", new[] { "*.json" });

    private static async Task<string?> PickFileAsync(string title, string kind, string[] patterns)
    {
        var owner = Owner;
        if (owner is null) return null;
        return await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var top = TopLevel.GetTopLevel(owner);
            if (top is null) return null;
            var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = title,
                AllowMultiple = false,
                FileTypeFilter = new[] { new FilePickerFileType(kind) { Patterns = patterns } },
            });
            // ⚠️ ‎TryGetLocalPath‎ برای فایلی که واقعاً روی دیسک نیست (مثلاً
            // از یک ارائه‌دهندهٔ ابری) ‎null‎ می‌دهد — همان‌جا انصراف می‌شود،
            // نه یک مسیرِ ساختگی که بعداً باز نمی‌شود.
            return files.Count > 0 ? files[0].TryGetLocalPath() : null;
        });
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
