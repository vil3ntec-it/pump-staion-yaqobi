using System.Diagnostics;

namespace PumpYaqobi.App.Services;

/// <summary>
/// ══ بستن و دوباره باز کردنِ برنامه — برای تعویضِ زبان (۱۴۰۵/۰۷/۲۸) ══════════════
/// نمونهٔ تازه با <c>--restart</c> بالا می‌آید و مثلِ <c>--after-update</c> تا ۶۰ ثانیه
/// منتظرِ رفتنِ همین نمونه می‌ماند (قفلِ تک‌نمونه، ‎Program.Main‎)؛ این نمونه از همان درِ
/// «بستن برای نصب» می‌رود (‎UpdateExit.CloseForInstall‎): پرسشِ بکاپ سرِ راه نمی‌ایستد و
/// پروسه واقعاً می‌رود. ⚠️ نوشته‌ها پیش از صدا زدن روی دیسک نشسته‌اند (‎SaveGuard‎).
/// </summary>
public static class AppRestart
{
    /// <summary>
    /// 🌐 زبان را بنویس و برنامه را دوباره باز کن. ⚠️ نوشتنِ بادوام (‎Save‎ با ‎fsync‎)
    /// لازم است — پروسه همین حالا می‌رود — ولی بیرونِ نخِ رابط (قاعدهٔ ‎SectionOpenSpeedTests‎).
    /// </summary>
    public static async Task<bool> SwitchLanguageAsync(AppSettings settings, string code)
    {
        settings.UiLang = code;
        await Task.Run(settings.Save);
        try { await SaveGuard.FlushAllAsync(); } catch { /* ‎SaveGuard‎ خودش شکست را نشان می‌دهد */ }
        return Now();
    }

    /// <summary>راست ⇒ نمونهٔ تازه راه افتاد و این یکی بسته می‌شود.</summary>
    public static bool Now()
    {
        var exe = Environment.ProcessPath;
        if (string.IsNullOrEmpty(exe)) return false;
        //  در اجرای ‎dotnet App.dll‎ (فقط توسعه) خودِ ‎dll‎ هم باید داده شود
        var args = Path.GetFileNameWithoutExtension(exe).Equals("dotnet", StringComparison.OrdinalIgnoreCase)
            ? $"\"{typeof(AppRestart).Assembly.Location}\" --restart"
            : "--restart";
        try
        {
            Process.Start(new ProcessStartInfo(exe, args)
            {
                UseShellExecute = false,
                WorkingDirectory = AppContext.BaseDirectory,
            });
        }
        catch { return false; }
        Update.UpdateExit.CloseForInstall();
        return true;
    }
}
