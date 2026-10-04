namespace PumpYaqobi.App.Services;

/// <summary>
/// ══ سربرگِ بالای پنجره: روشن یا خاموش (۱۴۰۵/۰۷/۱۹) ═════════════════════════
///
/// خواستهٔ صاحب ریپو: «برای نوارِ بالایی که شاملِ لاگین، پشتیبانی و غیره است
/// هم یک چک‌باکسِ مشابه اضافه کن تا کاربر بتواند پنهان یا دوباره نمایش دهد.»
/// همان الگوی ‎BannerPref‎، کنارِ همان کلید در داشبورد.
///
/// ⛔ فقط <b>دیدن</b> را عوض می‌کند — هیچ فرمانی، چراغی یا ساعتی خاموش نمی‌شود؛
/// میان‌برها (‎Ctrl+K‎ و …) همچنان کار می‌کنند و راهِ برگشت همیشه همان کلیدِ
/// داشبورد است. مقدارِ راحتی، پس ‎SaveSoon()‎ و فهرستِ ‎SaveComfortOnly‎.
/// </summary>
public static class HeaderPref
{
    private static bool? _show;

    public static event Action? Changed;

    public static bool Show
    {
        get
        {
            if (_show is { } v) return v;
            try { _show = AppSettings.Load().ShowHeader; } catch { _show = true; }
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
            s.ShowHeader = show;
            s.SaveSoon();
        }
        catch { }
        Changed?.Invoke();
    }

    /// <summary>فقط برای آزمون‌ها — بی دیسک.</summary>
    public static void TestSet(bool? show) => _show = show;
}
