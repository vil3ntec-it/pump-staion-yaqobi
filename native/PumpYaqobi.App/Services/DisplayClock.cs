using PumpYaqobi.Domain;

namespace PumpYaqobi.App.Services;

/// <summary>
/// ══ تاریخ و ساعتِ نمایشی — فقط برای دیدن (۱۴۰۵/۰۷/۱۶) ══════════════════════
///
/// خواستهٔ صریحِ صاحب ریپو (با عکس): «نمی‌تونم تاریخ رو از برنامه عوض کنم و
/// اجازه نمی‌ده و یک علامتِ هشدار میاد… من گفتم نمایشی — هر جور بخوام می‌ذارم
/// و روی برنامه تأثیر نذاره… هر دقیقه می‌گه از ساعتِ فلان چند دقیقه عقب است،
/// به من چه.»
///
/// ── قاعده ─────────────────────────────────────────────────────────────────
/// <code>
///   نمایشی = AppClock.Now + ClockShiftMs
/// </code>
/// ⛔ <b>فقط سه جا از این می‌خوانند</b>: تاریخ و ساعتِ سربرگ، نوارِ بالای
/// داشبورد، و پنجرهٔ «🕘 تاریخ و ساعت». هر تصمیمی (تاریخِ ردیف، ماهِ بخش‌ها،
/// اشتراک، فاصله‌ها، پاکِ سطل) همچنان از <see cref="AppClock"/> است — پس هیچ
/// تاریخی که کاربر این‌جا بگذارد روی حساب‌ها، ماه‌ها یا اشتراک اثری ندارد.
/// ⛔ نه ساعتِ ویندوز عوض می‌شود و نه اجازهٔ مدیر خواسته می‌شود.
/// ⛔ هیچ هشداری دربارهٔ «ساعتِ ویندوز جلو/عقب است» نشان داده نمی‌شود.
/// </summary>
public static class DisplayClock
{
    private static long _shiftMs;
    private static bool _loaded;

    /// <summary>عوض شد — سربرگ و داشبورد همان لحظه تازه شوند.</summary>
    public static event Action? Changed;

    /// <summary>فاصله از ساعتِ واقعیِ برنامه (میلی‌ثانیه). صفر = همان ساعتِ واقعی.</summary>
    public static long ShiftMs
    {
        get { EnsureLoaded(); return Interlocked.Read(ref _shiftMs); }
    }

    /// <summary>تاریخ و ساعتی که کاربر می‌بیند.</summary>
    public static DateTime Now
    {
        get
        {
            var s = ShiftMs;
            return s == 0 ? AppClock.Now : AppClock.Now.AddMilliseconds(s);
        }
    }

    /// <summary>کاربر خودش تاریخ و ساعت گذاشته است.</summary>
    public static bool Shifted => ShiftMs != 0;

    /// <summary>
    /// «این را نشان بده» — فقط فاصله ذخیره می‌شود (مقدارِ راحتی، ‎SaveSoon‎).
    /// کمتر از یک دقیقه فاصله یعنی همان ساعتِ واقعی.
    /// </summary>
    public static void Show(DateTime picked)
    {
        var shift = (long)(picked - AppClock.Now).TotalMilliseconds;
        if (Math.Abs(shift) < 60_000) shift = 0;
        Store(shift);
    }

    /// <summary>برگشت به ساعتِ واقعی.</summary>
    public static void Reset() => Store(0);

    private static void Store(long shift)
    {
        EnsureLoaded();
        Interlocked.Exchange(ref _shiftMs, shift);
        try
        {
            var s = AppSettings.Load();
            s.ClockShiftMs = shift;
            s.SaveSoon();
        }
        catch { }
        Changed?.Invoke();
    }

    private static void EnsureLoaded()
    {
        if (_loaded) return;
        _loaded = true;
        try { Interlocked.Exchange(ref _shiftMs, AppSettings.Load().ClockShiftMs); } catch { }
    }

    /// <summary>فقط برای آزمون‌ها — بی دیسک.</summary>
    public static void TestSet(long shift) { _loaded = true; Interlocked.Exchange(ref _shiftMs, shift); }
}
