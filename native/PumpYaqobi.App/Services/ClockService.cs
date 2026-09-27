using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;

namespace PumpYaqobi.App.Services;

/// <summary>
/// ══ تاریخ و ساعت از داخلِ خودِ برنامه (۱۴۰۵/۰۷/۱۵) ═══════════════════════════
///
/// خواستهٔ صاحب ریپو: «چرا می‌خواهم ساعت یا تاریخ را عوض کنم می‌رود تنظیماتِ
/// ویندوز؟ برنامهٔ من خودش داشته باشد، نرود از ویندوز باز شود.»
///
/// ⛔ ولی <b>ساعتِ دوم</b> ساخته نمی‌شود — و این قاعدهٔ ۱۴۰۵/۰۷/۱۳ سرِ جایش
/// است: تاریخِ هر ردیف، ماهِ هر دفتر، مهرِ مجوز و ترمزِ ارفاق همه از ساعتِ
/// کامپیوتر می‌آیند، و «تاریخِ دستیِ برنامه» یعنی روزی ردیف در ماهِ اشتباه
/// بنشیند. پس پنجرهٔ تاریخ و ساعت مالِ خودِ برنامه است (شمسی، با نامِ ماه)،
/// و «ثبت» <b>همان ساعتِ ویندوز</b> را عوض می‌کند — یک ساعت، یک حقیقت.
///
/// ⚠️ عوض کردنِ ساعتِ ویندوز دسترسیِ مدیر می‌خواهد؛ ویندوز خودش یک بار می‌پرسد
/// (‎runas‎). انصراف از آن پرسش خطا نیست و همان را می‌گوییم.
/// ⚠️ فرمان فقط از عددهای خودمان ساخته می‌شود (‎InvariantCulture‎) — هیچ
/// نوشتهٔ کاربری داخلِ خطِ فرمان نمی‌رود.
/// </summary>
public static class ClockService
{
    public enum Result { Done, Cancelled, Failed, NotWindows }

    /// <summary>⚠️ فقط برای آزمون: به‌جای اجرای واقعی.</summary>
    public static Func<ProcessStartInfo, Result>? TestRun { get; set; }

    /// <summary>ساعتِ ویندوز را روی این لحظه (به وقتِ محلی) می‌گذارد.</summary>
    public static Task<Result> SetAsync(DateTime local)
    {
        var stamp = local.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture);
        return RunAsync(Elevated("powershell.exe",
            "-NoProfile -NonInteractive -WindowStyle Hidden -Command \"Set-Date -Date '" + stamp + "' | Out-Null\""));
    }

    /// <summary>ساعتِ ویندوز را با ساعتِ اینترنت یکی می‌کند (خدمتِ زمانِ خودِ ویندوز).</summary>
    public static Task<Result> SyncInternetAsync()
        => RunAsync(Elevated("cmd.exe", "/c \"net start w32time >nul 2>&1 & w32tm /resync /force\""));

    public static ProcessStartInfo Elevated(string file, string args) => new(file, args)
    {
        UseShellExecute = true,
        Verb = "runas",
        WindowStyle = ProcessWindowStyle.Hidden,
        CreateNoWindow = true,
    };

    private static Task<Result> RunAsync(ProcessStartInfo psi) => Task.Run(() =>
    {
        if (TestRun is { } t) return t(psi);
        if (!OperatingSystem.IsWindows()) return Result.NotWindows;
        try
        {
            using var p = Process.Start(psi);
            if (p is null) return Result.Failed;
            if (!p.WaitForExit(30_000)) return Result.Failed;
            return p.ExitCode == 0 ? Result.Done : Result.Failed;
        }
        catch (Win32Exception e) when (e.NativeErrorCode == 1223) { return Result.Cancelled; }   // کاربر «نه» زد
        catch { return Result.Failed; }
    });

    /// <summary>جملهٔ آدمیزادِ هر نتیجه — ⛔ متنِ خامِ استثنا به کاربر نمی‌رسد.</summary>
    public static string Why(Result r) => r switch
    {
        Result.Done => "✅ ساعتِ کامپیوتر عوض شد",
        Result.Cancelled => "ویندوز اجازه خواست و «نه» زده شد — چیزی عوض نشد",
        Result.NotWindows => "این کار فقط روی ویندوز شدنی است",
        _ => "❌ ساعتِ کامپیوتر عوض نشد — دسترسیِ مدیرِ ویندوز لازم است",
    };
}
