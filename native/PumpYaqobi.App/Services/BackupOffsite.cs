using System.Globalization;
using PumpYaqobi.Domain;

namespace PumpYaqobi.App.Services;

/// <summary>
/// ══ شورا، ب۵ — «بکاپ فقط روی همین کامپیوتر است»؟ ══════════════════════════
///
/// بکاپِ روزانه روی همان دیسک است؛ دیسکِ سوخته یا کامپیوترِ دزدیده هر دو را با
/// هم می‌برد. «بیرون» یعنی دستِ‌کم یکی از دو راه: پشتیبان در هفت روزِ گذشته به
/// سرور رسیده (‎LastBackupSentAt‎، سرورِ خانگی یا سرورِ حساب)، یا در سی روزِ
/// گذشته یک «فایلِ بکاپ» (‎.pumpyaqobi‎) ساخته شده که می‌شود روی فلش برد.
/// ⛔ فقط می‌گوید؛ هیچ چیزی را نمی‌بندد.
/// </summary>
public static class BackupOffsite
{
    public static readonly TimeSpan ServerFresh = TimeSpan.FromDays(7);
    public static readonly TimeSpan FlashFresh = TimeSpan.FromDays(30);

    /// <summary>جای بیرونی هست؟ خالص — آزمون دارد.</summary>
    public static bool Ok(string lastSent, string lastExport, DateTime nowUtc) =>
        Fresh(lastSent, nowUtc, ServerFresh) || Fresh(lastExport, nowUtc, FlashFresh);

    public static bool Ok(AppSettings s) => Ok(s.LastBackupSentAt, s.LastFullExportAt, AppClock.UtcNow);

    private static bool Fresh(string raw, DateTime nowUtc, TimeSpan within) =>
        DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var t)
        && nowUtc - t.ToUniversalTime() <= within && t.ToUniversalTime() <= nowUtc.AddMinutes(5);
}
