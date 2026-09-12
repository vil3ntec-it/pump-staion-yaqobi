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
    // ══ درزِ آزمون — چرا این‌جاست ═════════════════════════════════════════════
    //
    // «➕ حساب جدید» و «📋 جدول جدید» هر دو پشتِ یک پنجرهٔ گفت‌وگو هستند. تا
    // امروز هیچ آزمونی نمی‌توانست از آن پنجره رد شود، پس اگر این دو از کار
    // می‌افتادند، هیچ چکی قرمز نمی‌شد و فقط صاحب ریپو رویِ ویندوز می‌فهمید —
    // که دقیقاً همان چیزی است که شد («چرا حساب فرعی کار نمی‌کند»).
    //
    // این دو قلاب فقط برای همان است: سنجشِ پنجرهٔ واقعی (‎UiTests -- person‎)
    // پاسخِ کاربر را از پیش می‌گذارد و کلِ مسیر — از دکمه تا دیتابیس — واقعاً
    // اجرا می‌شود. در برنامهٔ کاربر هر دو ‎null‎ هستند و هیچ اثری ندارند.
    public static Func<string, string, string?>? PromptHook;
    public static Func<string, string, bool>? ConfirmHook;

    private static Window? Owner =>
        Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime d
            ? d.MainWindow : null;

    /// <summary>یک رشته بپرس. ‎null‎ یعنی کاربر انصراف داد.</summary>
    public static async Task<string?> PromptAsync(string title, string message = "",
                                                  string initial = "", string ok = "تایید")
    {
        if (PromptHook is { } hook) return hook(title, message);
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

    /// <summary>
    /// انتخابِ یک فایل با پسوندهای داده‌شده. ‎null‎ یعنی انصراف (یا آزمونِ بی‌پنجره).
    /// </summary>
    public static async Task<string?> PickFileAsync(string title, string kind, string[] patterns)
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

    /// <summary>
    /// «کجا ذخیره شود؟» — پنجرهٔ ذخیرهٔ خودِ ویندوز. ‎null‎ یعنی انصراف.
    ///
    /// خودِ پنجره «فایل هست، جایگزین شود؟» را می‌پرسد، پس این‌جا دوباره
    /// پرسیده نمی‌شود.
    /// </summary>
    public static async Task<string?> SaveFileAsync(string title, string suggestedName,
                                                    string kind, string[] patterns)
    {
        var owner = Owner;
        if (owner is null) return null;
        return await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var top = TopLevel.GetTopLevel(owner);
            if (top is null) return null;
            var file = await top.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = title,
                SuggestedFileName = suggestedName,
                ShowOverwritePrompt = true,
                FileTypeChoices = new[] { new FilePickerFileType(kind) { Patterns = patterns } },
            });
            return file?.TryGetLocalPath();
        });
    }

    /// <summary>
    /// ══ «📝 ورق با تاریخ» ═══════════════════════════════════════════════════
    /// تاریخِ ورق را بپرس — همتای ‎openWaraqDatePicker‎ی سایت. ‎null‎ یعنی
    /// انصراف. ‎existingKeys‎ کلیدِ تاریخِ ورق‌های موجود است تا پنجره پیش از
    /// تایید بگوید «از قبل هست» یا «تازه ساخته می‌شود».
    /// </summary>
    public static async Task<string?> PickWaraqDateAsync(IReadOnlyCollection<int> existingKeys,
                                                         string? startDate = null)
    {
        if (PromptHook is { } hook) return hook("ورق با تاریخ", startDate ?? "");
        var owner = Owner;
        if (owner is null) return null;
        return await Dispatcher.UIThread.InvokeAsync(async () =>
            await WaraqDateWindow.For(existingKeys, startDate).ShowDialog<string?>(owner));
    }

    /// <summary>
    /// پنجرهٔ «📲 کیو‌آر» را نشان بده — همتای ‎#qrModal‎ی سایت.
    /// </summary>
    public static async Task ShowQrAsync(string name, string link, byte[]? png, string hint)
    {
        var owner = Owner;
        if (owner is null) return;
        await Dispatcher.UIThread.InvokeAsync(async () =>
            await QrWindow.For(name, link, png, hint).ShowDialog(owner));
    }

    /// <summary>«مطمئنی؟» — ‎true‎ فقط وقتی خودِ کاربر تایید کند.</summary>
    public static async Task<bool> ConfirmAsync(string title, string message,
                                                string ok = "بله", string cancel = "انصراف")
    {
        if (ConfirmHook is { } hook) return hook(title, message);
        var owner = Owner;
        if (owner is null) return false;
        var r = await Dispatcher.UIThread.InvokeAsync(async () =>
            await DialogWindow.ForConfirm(title, message, ok, cancel).ShowDialog<string?>(owner));
        return r is not null;
    }
}
