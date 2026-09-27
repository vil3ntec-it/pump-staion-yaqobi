namespace PumpYaqobi.Domain;

/// <summary>
/// ══ ساعتِ خودِ برنامه — نه ساعتِ ویندوز ══════════════════════════════════════
///
/// گزارشِ صاحب ریپو (۱۴۰۵/۰۷/۱۵): «وقتی تاریخ رو عوض می‌کنم تمامِ سیستم به هم
/// می‌خوره… با تغییرِ تاریخ جلو و عقب اشتراکِ وی‌آی‌پی یا دور زده می‌شه یا درجا
/// ختم می‌شه و سرور رو هم بهم می‌زنه… تاریخ یک چیزِ نمایشی است، و برنامه خودش
/// بتونه ماه و سال و روز رو از اینترنت ببینه و بفهمه کدوم واقعی است.»
///
/// ── ریشه ─────────────────────────────────────────────────────────────────
/// هر جای برنامه ‎DateTime.Now/UtcNow‎ را مستقیم می‌خواند. پس جلو بردنِ ساعتِ
/// ویندوز مجوزِ سالم را «منقضی» می‌دید (و پیگیرِ اشتراک هر ده دقیقه دستگاه را
/// دوباره به سرور می‌بست)، عقب بردنش هر «هر ده دقیقه یک بار»ی را برای همان
/// مدت خاموش می‌کرد (فاصلهٔ منفی هرگز «رسید» نمی‌شود)، ماهِ بخش‌ها و نقطهٔ سرخ
/// را جابه‌جا می‌کرد، و سطلِ زباله و چت را زودتر از موعد <b>برای همیشه</b> پاک
/// می‌کرد.
///
/// ── قاعده ─────────────────────────────────────────────────────────────────
/// <code>
///   حالا = لنگر + زمانِ گذشته از روی شمارندهٔ یکنواختِ سیستم (TickCount64)
///   لنگر = ساعتِ سرورِ حساب/گیت‌هاب (سرآیندِ Date) — اگر داریم؛
///          وگرنه ساعتِ ویندوز در لحظهٔ بالا آمدن
/// </code>
/// شمارندهٔ یکنواخت با عوض کردنِ ساعتِ ویندوز تکان نمی‌خورد (و زمانِ خواب هم
/// در آن هست)، پس وسطِ کار هیچ جابه‌جاییِ دستیِ ساعت اثری ندارد. فقط
/// جابه‌جاییِ کوچکِ خودِ ویندوز (همگام‌سازیِ خودکار، زیرِ <see cref="FollowMs"/>)
/// دنبال می‌شود — آن هم فقط تا ساعتِ اینترنت نیامده.
///
/// ⛔ <b>هیچ فایلی در برنامه ساعتِ خام نمی‌خواند</b> — آزمونِ
/// <c>AppClockTests.HichSaateKham_DarBarname_Nist</c> کلِ سورس را می‌گردد.
/// فاصله‌ها («هر ده دقیقه»، ترمزها) از <see cref="Mono"/> است، نه از تاریخ.
/// </summary>
public static class AppClock
{
    /// <summary>ساعتِ ویندوز — تزریق‌پذیر فقط برای آزمون‌ها.</summary>
    public static Func<DateTime> WallSource { get; set; } = () => DateTime.UtcNow;

    /// <summary>شمارندهٔ یکنواختِ سیستم (میلی‌ثانیه) — تزریق‌پذیر فقط برای آزمون‌ها.</summary>
    public static Func<long> MonoSource { get; set; } = () => Environment.TickCount64;

    /// <summary>
    /// جابه‌جاییِ کوچک‌تر از این (پیش از آمدنِ ساعتِ اینترنت) دنبال می‌شود:
    /// همگام‌سازیِ خودکارِ ویندوز چند ثانیه این‌ور و آن‌ور می‌برد، نه روزها.
    /// </summary>
    public const long FollowMs = 2 * 60_000;

    /// <summary>بیش از این فاصله میانِ ساعتِ ویندوز و ساعتِ واقعی ⇒ «ساعتِ ویندوز درست نیست».</summary>
    public const long SkewWarnMs = 2 * 60_000;

    private static readonly object Gate = new();
    private static bool _based;
    private static long _baseMs;      // لنگر (میلی‌ثانیهٔ یونیکس)
    private static long _baseMono;    // شمارنده در لحظهٔ لنگر
    private static bool _trusted;
    private static long _trustedMono = long.MinValue;

    /// <summary>ساعتِ اینترنت (یا لنگری که از آن آمده) این‌جا هست.</summary>
    public static bool Trusted { get { lock (Gate) return _trusted; } }

    /// <summary>آخرین ساعتِ اینترنت چند میلی‌ثانیه پیش آمد (شمارندهٔ یکنواخت). منفی ⇒ هنوز نیامده.</summary>
    public static long TrustedAgeMs
    {
        get { lock (Gate) return _trusted ? MonoSource() - _trustedMono : -1; }
    }

    /// <summary>با یک اتفاق عوض شد (ساعتِ اینترنت آمد، یا کاربر ساعت را درست کرد).</summary>
    public static event Action? Changed;

    /// <summary>حالا — میلی‌ثانیهٔ یونیکس (UTC). ⛔ جای ‎DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()‎.</summary>
    public static long UnixMs
    {
        get
        {
            lock (Gate)
            {
                var mono = MonoSource();
                if (!_based) { Rebase(WallMs(), mono, trusted: false); return _baseMs; }
                var est = _baseMs + (mono - _baseMono);
                if (!_trusted)
                {
                    //  فقط لرزشِ کوچک — جابه‌جاییِ دستی (روزها، ماه‌ها) نادیده
                    var wall = WallMs();
                    if (Math.Abs(wall - est) <= FollowMs) { Rebase(wall, mono, false); return wall; }
                }
                return est;
            }
        }
    }

    /// <summary>حالا به UTC. ⛔ جای ‎DateTime.UtcNow‎.</summary>
    public static DateTime UtcNow => DateTime.SpecifyKind(DateTime.UnixEpoch.AddMilliseconds(UnixMs), DateTimeKind.Utc);

    /// <summary>حالا به وقتِ محلی. ⛔ جای ‎DateTime.Now‎.</summary>
    public static DateTime Now => TimeZoneInfo.ConvertTimeFromUtc(UtcNow, TimeZoneInfo.Local);

    /// <summary>امروز به وقتِ محلی. ⛔ جای ‎DateTime.Today‎.</summary>
    public static DateTime Today => Now.Date;

    /// <summary>
    /// زمانی که فقط جلو می‌رود و با ساعتِ ویندوز هیچ ربطی ندارد — برای
    /// فاصله‌ها و ترمزها («هر ده دقیقه»). ⛔ هرگز نمایش داده یا ذخیره نشود.
    /// </summary>
    public static DateTime Mono => MonoEpoch.AddMilliseconds(MonoSource());

    private static readonly DateTime MonoEpoch = new(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static long _lastTicks;

    /// <summary>
    /// شماره‌ای یکتا و رو به بالا از روی ساعت (برای شناسه‌های کهنهٔ «c…»/«ss…») —
    /// ساعتِ میلی‌ثانیه‌ای دو بار در یک میلی‌ثانیه همان را می‌داد.
    /// </summary>
    public static long UniqueTicks()
    {
        lock (Gate)
        {
            var t = UtcNowUnlocked().Ticks;
            _lastTicks = t > _lastTicks ? t : _lastTicks + 1;
            return _lastTicks;
        }
    }

    private static DateTime UtcNowUnlocked()
    {
        var mono = MonoSource();
        if (!_based) Rebase(WallMs(), mono, trusted: false);
        return DateTime.UnixEpoch.AddMilliseconds(_baseMs + (mono - _baseMono));
    }

    /// <summary>ساعتِ ویندوز چقدر از ساعتِ واقعی جلوتر است (منفی = عقب‌تر). بی ساعتِ اینترنت صفر.</summary>
    public static long WallSkewMs
    {
        get
        {
            if (!Trusted) return 0;
            return WallMs() - UnixMs;
        }
    }

    /// <summary>
    /// پاکِ <b>همیشگی</b> بر پایهٔ زمان (سطلِ زباله، چت) مجاز است؟ فقط با ساعتِ
    /// اینترنت. ⛔ ساعتِ ویندوزِ جلورفته پیش از این سطلِ پانزده‌روزه را همان
    /// لحظه خالی می‌کرد؛ بی اینترنت چیزی برای همیشه پاک نمی‌شود، فقط منتظر
    /// می‌ماند. (تزریق‌پذیر فقط برای آزمون.)
    /// </summary>
    public static bool SafeToPurge => SafeToPurgeOverride ?? Trusted;

    /// <summary>⚠️ فقط برای آزمون.</summary>
    public static bool? SafeToPurgeOverride { get; set; }

    /// <summary>ساعتِ ویندوز بیش از دو دقیقه با ساعتِ واقعی فرق دارد.</summary>
    public static bool WallIsOff => Math.Abs(WallSkewMs) > SkewWarnMs;

    /// <summary>
    /// ساعتِ مطمئن رسید (سرآیندِ Date از سرورِ حساب یا گیت‌هاب، یا لنگری که از
    /// همان ساعت در همین روشن‌بودنِ کامپیوتر مانده).
    /// <paramref name="monoAt"/> لحظه‌ای است که آن ساعت درست بود.
    /// ⚠️ ساعتِ بی‌معنا (پیش از ۲۰۲۴ یا پس از ۲۱۰۰) پذیرفته نمی‌شود.
    /// </summary>
    public static bool Accept(long serverUnixMs, long monoAt)
    {
        if (serverUnixMs < MinSaneMs || serverUnixMs > MaxSaneMs) return false;
        bool moved;
        lock (Gate)
        {
            var mono = MonoSource();
            var now = serverUnixMs + (mono - monoAt);
            var before = _based ? _baseMs + (mono - _baseMono) : now;
            moved = !_trusted || Math.Abs(now - before) > 1000;
            Rebase(now, mono, trusted: true);
            _trustedMono = monoAt;
        }
        if (moved) try { Changed?.Invoke(); } catch { /* نمایش رفاه است */ }
        return true;
    }

    /// <summary>
    /// کاربر خودش ساعتِ ویندوز را درست کرد (پنجرهٔ «🕘 تاریخ و ساعت»). تا
    /// ساعتِ اینترنت نیامده، همان حرفِ آخر است؛ با ساعتِ اینترنت کاری نمی‌کند.
    /// </summary>
    public static void UserSetWall()
    {
        lock (Gate)
        {
            if (_trusted) return;
            Rebase(WallMs(), MonoSource(), trusted: false);
        }
        try { Changed?.Invoke(); } catch { }
    }

    /// <summary>
    /// کفی که زمان هرگز از آن عقب‌تر دیده نمی‌شود (آخرین زمانِ دانسته، روی
    /// دیسک) — فقط سرِ بالا آمدن و فقط تا ساعتِ اینترنت نیامده.
    /// </summary>
    public static void AtLeast(long unixMs)
    {
        lock (Gate)
        {
            if (_trusted || unixMs < MinSaneMs || unixMs > MaxSaneMs) return;
            var mono = MonoSource();
            var est = _based ? _baseMs + (mono - _baseMono) : WallMs();
            if (est < unixMs) Rebase(unixMs, mono, trusted: false);
        }
    }

    /// <summary>⚠️ فقط برای آزمون — همه‌چیز از نو.</summary>
    public static void ResetForTests()
    {
        lock (Gate)
        {
            _based = false; _trusted = false; _trustedMono = long.MinValue; SafeToPurgeOverride = null;
            WallSource = () => DateTime.UtcNow;
            MonoSource = () => Environment.TickCount64;
        }
    }

    private static readonly long MinSaneMs = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();
    private static readonly long MaxSaneMs = new DateTimeOffset(2100, 1, 1, 0, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();

    private static long WallMs() =>
        new DateTimeOffset(DateTime.SpecifyKind(WallSource().ToUniversalTime(), DateTimeKind.Utc)).ToUnixTimeMilliseconds();

    private static void Rebase(long ms, long mono, bool trusted)
    {
        _baseMs = ms; _baseMono = mono; _based = true;
        if (trusted) _trusted = true;
    }
}
