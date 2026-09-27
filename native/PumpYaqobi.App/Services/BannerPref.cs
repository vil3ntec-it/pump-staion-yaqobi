namespace PumpYaqobi.App.Services;

/// <summary>
/// ══ نوارِ چهار عددِ بالای پنجره: روشن یا خاموش (۱۴۰۵/۰۷/۱۶) ══════════════════
///
/// خواستهٔ صاحب ریپو (با عکس): «این بخش خیلی جا می‌گیرد… این باشد اما بشود
/// برش داشت — توی داشبورد یک دکمهٔ کوچک بگذار اگر کسی خواست… هیچ منطقی دست
/// نخورد، فقط خاموش و روشن.»
///
/// ⛔ فقط <b>دیدن</b> را عوض می‌کند. عددها همان‌اند و همان‌جا ساخته می‌شوند
/// (‎MainViewModel.RefreshBannerAsync‎)؛ خاموش که بود فقط خوانده نمی‌شوند
/// (قاعدهٔ «چیزِ پنهان هیچ دستورِ دیتابیسی نمی‌زند») و با روشن شدن همان لحظه
/// دوباره. مقدارِ راحتی، پس ‎SaveSoon()‎ و فهرستِ ‎SaveComfortOnly‎.
/// </summary>
public static class BannerPref
{
    private static bool? _show;

    public static event Action? Changed;

    public static bool Show
    {
        get
        {
            if (_show is { } v) return v;
            try { _show = AppSettings.Load().ShowBanner; } catch { _show = true; }
            return _show.Value;
        }
    }

    public static void Set(bool show)
    {
        if (Show == show) return;
        _show = show;
        try
        {
            var s = AppSettings.Load();
            s.ShowBanner = show;
            s.SaveSoon();
        }
        catch { }
        Changed?.Invoke();
    }

    /// <summary>فقط برای آزمون‌ها — بی دیسک.</summary>
    public static void TestSet(bool? show) => _show = show;
}
