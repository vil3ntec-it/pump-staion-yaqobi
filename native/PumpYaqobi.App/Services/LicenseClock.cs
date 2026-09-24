using System.Diagnostics;

namespace PumpYaqobi.App.Services;

/// <summary>
/// ══ ساعتی که با عقب بردنِ ساعتِ ویندوز عقب نمی‌رود ═══════════════════════════
///
/// ⛔ <b>درزی که این می‌بندد</b>: مجوزِ امضاشده <c>exp</c> دارد و
/// <see cref="LicenseGuard"/> آن را با ساعتِ همین کامپیوتر می‌سنجید. پس کسی
/// که ساعتِ ویندوز را یک ماه عقب می‌برد، مجوزِ منقضی را دوباره «سالم» می‌دید
/// — و ارفاقِ چهارده‌روزه هم با آن از نو شروع می‌شد.
///
/// ── قاعده ─────────────────────────────────────────────────────────────────
/// <code>
///   حالا = max(ساعتِ دیوار ، کف)
///   کف   = فقط جلو می‌رود: max(کف، ساعتِ دیوار، iatِ هر مجوزِ امضاشده)
///          و تا برنامه باز است، دستِ‌کم به اندازهٔ زمانِ گذشته (Stopwatch)
/// </code>
///
/// پس ساعتِ یخ‌زده یا عقب‌برده هم نمی‌تواند زمان را کندتر از خودِ برنامه
/// جلو ببرد، و <c>iat</c>ِ امضاشدهٔ سرور — که هیچ‌کس جز سرور نمی‌سازدش —
/// کف را دستِ‌کم تا لحظهٔ صدورِ آخرین مجوز بالا نگه می‌دارد.
///
/// ⚠️ <b>منطقهٔ زمانی هیچ اثری ندارد</b>: همه‌چیز میلی‌ثانیهٔ یونیکس است
/// (UTC). عوض کردنِ منطقهٔ زمانیِ ویندوز یا ساعتِ تابستانی عددِ UTC را عوض
/// نمی‌کند، پس نه کف را بالا می‌برد نه مجوزی را می‌بندد.
///
/// ⚠️ <b>تنها راهِ پایین آمدنِ کف</b> مجوزی است که <b>همین حالا از سرور</b>
/// رسیده و امضایش سالم است، و <c>iat</c>ش خیلی پایین‌تر از کف است
/// (<see cref="Anchor"/>) — یعنی ساعتِ کامپیوتر یک بار اشتباهی جلو رفته
/// بود و کف دنبالش رفته بود. زمانِ امضاشدهٔ سرور تنها لنگرِ قابلِ اعتماد
/// است؛ بی این راه، یک اشتباهِ ساعت مجوزِ مشتریِ پول‌داده را زودتر می‌بست.
/// </summary>
public static class LicenseClock
{
    /// <summary>کف باید دستِ‌کم این‌قدر جلو برود تا ارزشِ یک نوشتن را داشته باشد.</summary>
    private const long PersistStepMs = 60_000;

    /// <summary>عقب‌تر از کف به این اندازه ⇒ «ساعتِ این کامپیوتر عقب است».</summary>
    public const long BehindMs = 5 * 60_000;

    /// <summary>کفی که بیشتر از این از <c>iat</c>ِ مجوزِ تازه جلوتر است، اشتباه بوده.</summary>
    public const long ResetGapMs = 2L * 24 * 3600 * 1000;

    private static readonly object Gate = new();

    /// <summary>
    /// کفِ درونِ حافظه، به ازای پوشهٔ تنظیمات — همراهِ لحظهٔ Stopwatch که ثبت
    /// شد. ⚠️ به پوشه بسته است نه سراسری، تا آزمون‌های موازی (هر کدام پوشهٔ
    /// خودش) کفِ هم را نبینند.
    /// </summary>
    private static readonly Dictionary<string, (long Floor, long Ticks)> Running =
        new(StringComparer.Ordinal);

    /// <summary>ساعتِ دیوار — همان تزریق‌پذیرِ <see cref="Entitlements.Now"/>.</summary>
    public static long Wall() => Entitlements.Now();

    private static long RunningFloor()
    {
        lock (Gate)
        {
            if (!Running.TryGetValue(AppSettings.Dir, out var r)) return 0;
            return r.Floor + (long)Stopwatch.GetElapsedTime(r.Ticks).TotalMilliseconds;
        }
    }

    /// <summary>«حالا» برای سنجشِ مجوز — هرگز عقب‌تر از کف.</summary>
    public static long Now(AppSettings f) =>
        Math.Max(Wall(), Math.Max(f.ClockFloorMs, RunningFloor()));

    /// <summary>ساعتِ دیوار از کف عقب‌تر است — «ساعتِ این کامپیوتر عقب است».</summary>
    public static bool Behind(AppSettings f) =>
        Wall() < Math.Max(f.ClockFloorMs, RunningFloor()) - BehindMs;

    /// <summary>
    /// کف را جلو می‌برد — از حلقهٔ پس‌زمینه (هر دقیقه) صدا می‌خورد.
    /// راست ⇒ کف روی دیسک هم باید تازه شود؛ <b>خودش</b> با
    /// <c>SaveSoon()</c> می‌نشاندش.
    /// </summary>
    public static bool Tick(AppSettings f)
    {
        long floor;
        lock (Gate)
        {
            var key = AppSettings.Dir;
            var running = 0L;
            if (Running.TryGetValue(key, out var r))
                running = r.Floor + (long)Stopwatch.GetElapsedTime(r.Ticks).TotalMilliseconds;
            floor = Math.Max(Math.Max(f.ClockFloorMs, Wall()), running);
            Running[key] = (floor, Stopwatch.GetTimestamp());
        }
        if (floor < f.ClockFloorMs + PersistStepMs) return false;
        f.ClockFloorMs = floor;
        f.SaveSoon();
        return true;
    }

    /// <summary>
    /// مجوزِ امضاشده‌ای رسید: <c>iat</c>ش لنگر است.
    ///
    /// <paramref name="fresh"/> یعنی «همین حالا از سرور آمد» — تنها حالتی که
    /// کف می‌تواند <b>پایین</b> بیاید، و فقط وقتی ساعتِ دیوار هم نزدیکِ همان
    /// <c>iat</c> است (یعنی کاربر ساعتِ اشتباهاً جلورفته را درست کرده). وگرنه
    /// فقط بالا می‌رود.
    /// </summary>
    public static void Anchor(AppSettings f, long issuedAt, bool fresh)
    {
        if (issuedAt <= 0) return;
        lock (Gate)
        {
            var key = AppSettings.Dir;
            var floor = Math.Max(f.ClockFloorMs, RunningFloorUnlocked(key));
            var wall = Wall();
            if (fresh && floor - issuedAt > ResetGapMs && Math.Abs(wall - issuedAt) < ResetGapMs / 2)
            {
                //  ⚠️ کفِ درونِ حافظه هم پایین می‌آید، وگرنه همان کفِ اشتباه از
                //  راهِ Stopwatch دوباره برمی‌گشت.
                f.ClockFloorMs = issuedAt;
                Running[key] = (issuedAt, Stopwatch.GetTimestamp());
                return;
            }
            //  ⚠️ کفِ درونِ حافظه را فقط `Tick` (حلقهٔ خودِ برنامه) بالا می‌برد؛
            //  این‌جا فقط کفِ همین تنظیمات جلو می‌رود.
            f.ClockFloorMs = Math.Max(f.ClockFloorMs, issuedAt);
        }
    }

    private static long RunningFloorUnlocked(string key) =>
        Running.TryGetValue(key, out var r)
            ? r.Floor + (long)Stopwatch.GetElapsedTime(r.Ticks).TotalMilliseconds
            : 0;

    /// <summary>⚠️ فقط برای آزمون — کفِ درونِ حافظهٔ همین پوشه را فراموش کن.</summary>
    public static void ForgetRunning()
    {
        lock (Gate) Running.Remove(AppSettings.Dir);
    }
}
