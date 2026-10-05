using PumpYaqobi.Services.Data;

namespace PumpYaqobi.App.Services;

/// <summary>
/// ══ کلیدِ بکاپِ پمپِ فعلی — و تنها تصمیمِ «این بکاپ برای این حساب باز می‌شود؟» ══
///
/// خواستهٔ صاحب ریپو (۱۴۰۵/۰۷/۲۰): «یارو بتواند حساب‌هایش را ببیند، حتی بی
/// اشتراک؛ ولی آزمایشیِ دوباره با حسابِ دوم نتواند — حتی آفلاین.»
///
///   مُهرِ همین پمپ   ⇒ باز، با هر حالِ اشتراک (حتی تمام‌شده؛ فقط‌خواندنی می‌ماند)
///   مُهرِ پمپِ دیگر  ⇒ هرگز — کلیدش را این حساب ندارد
///   بی مُهر (کهنه)   ⇒ همان درِ همیشگی: فقط اشتراکِ پولی (‎AppLock.RestoreBlocked‎)
///
/// ⛔ فقط کلیدِ پمپِ <b>فعلی</b> به کار می‌رود — کلیدِ پمپِ حسابِ قبلی که روی همین
/// کامپیوتر مانده، بکاپِ آن را برای حسابِ تازه باز نمی‌کند.
/// ⚠️ کامپیوترِ کاملاً تازه یک بار ورود با اینترنت می‌خواهد تا کلید برسد؛ پس از آن
/// آفلاین هم باز می‌شود.
/// </summary>
public static class BackupKeys
{
    /// <summary>کلیدِ ذخیره‌شدهٔ یک پمپ — یا <c>null</c>.</summary>
    public static byte[]? Cached(AppSettings s, string stationId)
    {
        if (string.IsNullOrWhiteSpace(stationId)) return null;
        foreach (var part in (s.BackupKeys ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var i = part.IndexOf('=');
            if (i <= 0 || !string.Equals(part[..i], stationId, StringComparison.Ordinal)) continue;
            try { var k = Convert.FromBase64String(part[(i + 1)..]); return k.Length == 32 ? k : null; }
            catch { return null; }
        }
        return null;
    }

    private static void Remember(AppSettings s, string stationId, byte[] key)
    {
        //  فقط کلیدِ همین پمپ — شرحش در ‎ForCurrentAsync‎
        s.BackupKeys = stationId + "=" + Convert.ToBase64String(key);
    }

    /// <summary>کلیدِ پمپِ فعلی — اول از همین کامپیوتر، وگرنه (اگر بشود) از سرور.</summary>
    public static async Task<byte[]?> ForCurrentAsync(bool network = true, CancellationToken ct = default)
    {
        var s = AppSettings.Load();
        var station = (s.CloudStationId ?? "").Trim();
        if (station.Length == 0) return null;
        //  ⛔ کلیدِ پمپِ دیگری (حسابِ قبلیِ همین کامپیوتر) نگه داشته نمی‌شود: حسابِ تازه
        //  هیچ ردی از کلیدِ آن روی دیسک نمی‌بیند — نه با برنامه، نه با دست بردن در فایل.
        var mine = Cached(s, station);
        if ((s.BackupKeys ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries)
                .Any(p => !p.StartsWith(station + "=", StringComparison.Ordinal)))
        {
            s.BackupKeys = mine is null ? "" : station + "=" + Convert.ToBase64String(mine);
            s.Save();
        }
        if (mine is { } k) return k;
        if (!network) return null;
        try
        {
            var cloud = new CloudLink(s, () => { s.Save(); return Task.CompletedTask; });
            var got = await cloud.BackupKeyAsync(ct);
            //  ⛔ کلیدی که مالِ پمپِ دیگری درآمد نگه داشته نمی‌شود
            if (!got.Ok || !string.Equals(got.StationId, station, StringComparison.Ordinal)) return null;
            var fresh = AppSettings.Load();
            Remember(fresh, got.StationId, got.Key);
            fresh.Save();
            return got.Key;
        }
        catch { return null; }
    }

    /// <summary>نتیجهٔ «این فایل این‌جا باز می‌شود؟».</summary>
    public sealed record Opened(bool Ok, string Path, bool OwnPump, string Why, bool Temp);

    /// <summary>
    /// فایلِ آمده را برای بازگردانی آماده می‌کند. مُهرخورده ⇒ با کلیدِ همین پمپ در یک فایلِ
    /// موقت باز می‌شود (‎Temp‎ ⇒ صدازننده پاکش کند). بی مُهر ⇒ همان فایل، و درِ اشتراک
    /// تصمیم می‌گیرد.
    /// </summary>
    public static async Task<Opened> OpenForRestoreAsync(string path, string tempDir, CancellationToken ct = default)
    {
        await ForCurrentAsync(network: false, ct);   // ⛔ اول کلیدِ پمپِ دیگری از دیسک برود
        if (!BackupSeal.IsSealed(path)) return new(true, path, false, "", false);
        var owner = BackupSeal.StationOf(path);
        var mine = (AppSettings.Load().CloudStationId ?? "").Trim();
        if (string.IsNullOrEmpty(owner)) return new(false, path, false, "فایلِ بکاپ خراب است", false);
        if (mine.Length == 0)
            return new(false, path, false, "این بکاپ به حسابِ صاحبش قفل است — اول با همان حساب وارد شوید (پروفایل).", false);
        if (!string.Equals(owner, mine, StringComparison.Ordinal))
            return new(false, path, false, "🔒 این بکاپ مالِ پمپِ حسابِ دیگری است و فقط با همان حساب باز می‌شود.", false);
        var key = await ForCurrentAsync(network: true, ct);
        if (key is null)
            return new(false, path, false, "کلیدِ بکاپِ این حساب هنوز روی این کامپیوتر نیست — یک بار با اینترنت وارد شوید و دوباره بزنید.", false);
        Directory.CreateDirectory(tempDir);
        var tmp = System.IO.Path.Combine(tempDir, "tmp-open-" + Guid.NewGuid().ToString("N")[..8] + ".db");
        if (!await Task.Run(() => BackupSeal.Open(path, tmp, key), ct))
            return new(false, path, false, "این بکاپ با کلیدِ این حساب باز نشد — یا دست خورده یا مالِ حسابِ دیگری است.", false);
        return new(true, tmp, true, "", true);
    }

    /// <summary>
    /// اگر کلیدِ پمپِ فعلی هست، فایل را <b>درجا</b> مُهر می‌کند. ‎false‎ ⇒ بی مُهر ماند
    /// (هنوز کلیدی نیست — بی‌حساب یا هرگز آنلاین نشده).
    /// </summary>
    public static async Task<bool> SealIfPossibleAsync(string path, bool network = true, CancellationToken ct = default)
    {
        var key = await ForCurrentAsync(network, ct);
        var station = (AppSettings.Load().CloudStationId ?? "").Trim();
        if (key is null || station.Length == 0) return false;
        await Task.Run(() => BackupSeal.Seal(path, path, station, key), ct);
        return true;
    }
}
