using Microsoft.EntityFrameworkCore;
using PumpYaqobi.Domain.Entities;

namespace PumpYaqobi.Services.Data;

/// <summary>
/// ══ مُهرِ کامپیوترِ دفتر — «بک‌اپ را روی کامپیوترِ دیگر و حسابِ تازه نیاورد» ══
///
/// گزارشِ صاحب ریپو (۱۴۰۵/۰۷/۲۰): «بک‌اپِ برنامه رو روی کامپیوترِ دیگه
/// ثبت می‌کنی، یارو ده‌ها حسابِ کاربری درست می‌کنه و دیگه لازم نداره اشتراک
/// بخره.» درِ «آوردنِ بکاپ» با اشتراکِ پولی بسته شد؛ این راهِ دومِ همان کار
/// را می‌بندد: کپیِ خودِ فایلِ دفتر در پوشهٔ نصبِ تازه، که نخستین حساب
/// برش می‌داشت (‎AccountLedger.ShouldClaimRoot‎).
///
/// ⛔ <b>هیچ داده‌ای پاک یا جابه‌جا نمی‌شود.</b> دفترِ بیگانه سرِ جایش
/// می‌ماند و حسابِ بی‌اشتراک فقط دفترِ تازهٔ خودش را می‌گیرد.
/// ⚠️ دفترِ بی‌مُهر (نسخه‌های پیشین) سرِ نخستین باز شدن به نامِ همین
/// کامپیوتر مُهر می‌خورد — وگرنه مشتریِ امروزی که بی‌حساب نوشته بود، با
/// ساختنِ حساب دفترش را از دست می‌داد.
/// </summary>
public static class LedgerStamp
{
    /// <summary>مُهرِ فعلی؛ خالی یعنی نیست یا خوانده نشد.</summary>
    public static string Read(PumpDbFactory dbf)
    {
        try
        {
            using var db = dbf.Create();
            return db.SyncState.AsNoTracking().Where(x => x.Id == 1)
                     .Select(x => x.LedgerMachine).FirstOrDefault() ?? "";
        }
        catch { return ""; }
    }

    /// <summary>اگر مُهری نیست، مُهرِ همین کامپیوتر را می‌زند. ⇒ نوشت؟</summary>
    public static bool StampIfEmpty(PumpDbFactory dbf, string machine)
    {
        if (string.IsNullOrWhiteSpace(machine)) return false;
        try
        {
            using var db = dbf.Create();
            var row = db.SyncState.FirstOrDefault(x => x.Id == 1);
            if (row is null) { db.SyncState.Add(new SyncStateRow { Id = 1, LedgerMachine = machine }); }
            else if (string.IsNullOrEmpty(row.LedgerMachine)) row.LedgerMachine = machine;
            else return false;
            db.SaveChanges();
            return true;
        }
        catch { return false; }
    }

    /// <summary>
    /// دفتر مالِ همین کامپیوتر است؟ — خالص. مُهرِ خالی یا کامپیوترِ بی‌شناسه
    /// «بله» است (نمی‌دانیم ⇒ هیچ کسی بیرون نمی‌ماند).
    /// </summary>
    public static bool Trusted(string? stamp, string? machine)
    {
        var s = (stamp ?? "").Trim();
        var m = (machine ?? "").Trim();
        return s.Length == 0 || m.Length == 0 || string.Equals(s, m, StringComparison.Ordinal);
    }
}
