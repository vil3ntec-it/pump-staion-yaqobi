using System.Globalization;

namespace PumpYaqobi.App.Services;

/// <summary>
/// ══ دورهٔ آزمایشیِ یک‌ماهه، یک بار برای هر کامپیوتر (۱۴۰۵/۰۸/۱۷) ═════════════
///
/// خواستهٔ صریحِ صاحب ریپو: «یارو روی کامپیوترِ جدید برنامه رو نصب کرد بدونِ
/// اینترنت یک ماه آزمایشی‌اش باشه… و اگه یارو روی یک حساب یا کامپیوتر آزمایشی
/// رو یک بار گرفته بود بار بعد این قبول نشه.»
///
/// ── قاعده ─────────────────────────────────────────────────────────────────
///  • آغاز = نخستین باری که این کامپیوتر بی حساب بالا آمد، <b>یا</b> نخستین
///    باری که مجوزِ آزمایشیِ سرور این‌جا دیده شد — هر کدام زودتر.
///  • پایان = <b>همان روز و همان ساعت در ماهِ بعد</b> (تقویمِ شمسی، ساعتِ
///    همین کامپیوتر) — نه «۳۰ روز».
///  • ⛔ <b>هر آزمایشی روی این کامپیوتر به همین پایان بسته است</b> — آزمایشیِ
///    حسابِ تازهٔ سرور هم. پس ساختنِ حسابِ دوم روی همان کامپیوتر آزمایشیِ تازه
///    نمی‌دهد (<see cref="Entitlements.State"/> و <see cref="AppLock.Decide"/>).
///  • «حالا» از <see cref="LicenseClock"/> است؛ عقب بردنِ ساعتِ ویندوز دوره را
///    دراز نمی‌کند.
///
/// ── جای نگه‌داری ──────────────────────────────────────────────────────────
///  دو جا، و <b>زودترینِ</b> هر دو پذیرفته است (پس دست بردن در یکی دوره را
///  جلو نمی‌برد): ‎settings.json‎ و رونوشتِ رمزشده با DPAPI در رجیستریِ
///  همین کاربر، با نامی از اثرِ انگشتِ کامپیوتر — حذف و نصبِ دوبارهٔ برنامه
///  (حتی در پوشهٔ دیگر) دوره را از نو شروع نمی‌کند.
///  ⚠️ کسی که هر دو را دستی پاک کند می‌تواند دوباره بگیرد؛ مرزِ قطعی فقط
///  روی سرورِ حساب است (آزمایشیِ هر حساب فقط یک بار).
///
/// ⛔ هیچ داده‌ای پاک نمی‌شود؛ پایانِ دوره فقط برنامه را فقط‌خواندنی می‌کند.
/// </summary>
public static class MachineTrial
{
    private const string RegKey = @"Software\VILL3N\PumpYaqobi";

    /// <summary>⚠️ فقط برای سنجه‌ها: رجیستری را نزن، فقط حافظه.</summary>
    public static Dictionary<string, long>? TestVault { get; set; }

    private static readonly object Gate = new();
    private static long _vaultCache = -1;
    private static string _vaultName = "";

    /// <summary>پایانِ یک دوره که در <paramref name="startMs"/> آغاز شد — همان روز و ساعت، ماهِ شمسیِ بعد.</summary>
    public static long EndOf(long startMs)
    {
        if (startMs <= 0) return 0;
        var local = DateTimeOffset.FromUnixTimeMilliseconds(startMs).ToLocalTime().DateTime;
        var next = new PersianCalendar().AddMonths(local, 1);
        var off = TimeZoneInfo.Local.GetUtcOffset(next);
        return new DateTimeOffset(next, off).ToUnixTimeMilliseconds();
    }

    /// <summary>آغازِ ثبت‌شده — زودترینِ تنظیمات و رونوشتِ پنهان؛ صفر یعنی هنوز آغاز نشده.</summary>
    public static long Start(AppSettings f)
    {
        var a = f.MachineTrialStartMs;
        var b = Vault();
        if (a <= 0) return b;
        if (b <= 0) return a;
        return Math.Min(a, b);
    }

    /// <summary>پایانِ دورهٔ این کامپیوتر؛ صفر یعنی هنوز آغاز نشده.</summary>
    public static long Ends(AppSettings f) => EndOf(Start(f));

    /// <summary>
    /// ثبتِ آغاز (فقط یک بار): <paramref name="startMs"/> اگر زودتر از ثبت‌شده باشد
    /// می‌نشیند — هرگز دیرتر. راست ⇒ چیزی عوض شد.
    /// </summary>
    public static bool Begin(AppSettings f, long startMs)
    {
        if (startMs <= 0) return false;
        var cur = Start(f);
        if (cur > 0 && cur <= startMs)
        {
            //  یکی از دو جا خالی یا دیرتر است ⇒ همان زودترین را پس بنشان
            if (f.MachineTrialStartMs != cur) { f.MachineTrialStartMs = cur; Save(f); }
            if (Vault() != cur) WriteVault(cur);
            return false;
        }
        f.MachineTrialStartMs = startMs;
        Save(f);
        WriteVault(startMs);
        return true;
    }

    /// <summary>
    /// از <c>SoftLock</c> صدا می‌خورد: بی حساب ⇒ دوره از همین حالا (اگر نبود)؛ مجوزِ
    /// آزمایشیِ امضاشده ⇒ آغازش (یک ماه پیش از پایانش، یا زمانِ صدورش) ثبت می‌شود.
    /// </summary>
    public static void Observe(AppSettings f, bool hasAccount, LicenseCheck? check, long nowMs)
    {
        try
        {
            if (check is { SignatureOk: true, IsTrial: true })
            {
                var s = check.SubscriptionEndsAt > 0 ? MonthBefore(check.SubscriptionEndsAt) : check.IssuedAt;
                if (s <= 0 || s > nowMs) s = check.IssuedAt > 0 ? Math.Min(check.IssuedAt, nowMs) : nowMs;
                Begin(f, s);
            }
            else if (!hasAccount && Start(f) <= 0)
                Begin(f, nowMs);
        }
        catch { /* ثبت نشد ⇒ بارِ بعد؛ هرگز برنامه را نمی‌بندد */ }
    }

    private static long MonthBefore(long ms)
    {
        var local = DateTimeOffset.FromUnixTimeMilliseconds(ms).ToLocalTime().DateTime;
        var prev = new PersianCalendar().AddMonths(local, -1);
        return new DateTimeOffset(prev, TimeZoneInfo.Local.GetUtcOffset(prev)).ToUnixTimeMilliseconds();
    }

    private static void Save(AppSettings f)
    {
        try { f.Save(); } catch { }
    }

    // ── رونوشتِ پنهان ────────────────────────────────────────────────────

    private static string Name() =>
        "tr" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes("pump|trial|" + CloudConfig.MachineFingerprint() + "|" + Environment.UserName
                + (AppSettings.DirOverride is null ? "" : "|" + AppSettings.Dir.ToLowerInvariant()))))[..12];

    private static long Vault()
    {
        var name = Name();
        if (TestVault is { } t) return t.TryGetValue(name, out var v) ? v : 0;
        if (!OperatingSystem.IsWindows()) return 0;
        lock (Gate)
        {
            if (_vaultCache >= 0 && _vaultName == name) return _vaultCache;
            long x = 0;
            try
            {
                using var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RegKey);
                if (k?.GetValue(name) is string raw
                    && long.TryParse(SecretStore.Unprotect(raw), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n))
                    x = n;
            }
            catch { }
            _vaultName = name; _vaultCache = x;
            return x;
        }
    }

    private static void WriteVault(long startMs)
    {
        var name = Name();
        if (TestVault is { } t) { t[name] = startMs; return; }
        if (!OperatingSystem.IsWindows()) return;
        lock (Gate) { _vaultName = name; _vaultCache = startMs; }
        try
        {
            using var k = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(RegKey);
            k.SetValue(name, SecretStore.Protect(startMs.ToString(CultureInfo.InvariantCulture)));
        }
        catch { }
    }
}
