using Avalonia.Threading;
using Avalonia.VisualTree;

namespace PumpYaqobi.App.Controls;

/// <summary>
/// ══ مهلتِ پارکِ جدول‌ها (۱۴۰۵/۰۷/۲۲، بندِ ۱ِ کندی‌ها) ══
/// جدولی که پنهان شد ردیف‌هایش را ‎ParkGrace‎ نگه می‌دارد تا برگشتِ تند ارزان باشد؛ فقط آخرین
/// صفحه، و تعویضِ تم همه را همان لحظه پارک می‌کند. شرحش بالای ‎OnShownChanged‎.
/// </summary>
public partial class ExcelGrid
{
    /// <summary>چند ثانیه پس از پنهان شدن، ردیف‌ها دور ریخته شوند (صفر ⇒ همان لحظه).</summary>
    public static TimeSpan ParkGrace { get; set; } = TimeSpan.FromSeconds(8);

    private static readonly Dictionary<ExcelGrid, DateTime> _parkAt = new();
    private static DispatcherTimer? _parkTimer;

    private void ParkNow()
    {
        if (_parkedAway || _editing || ItemsSource is null) return;

        _parked = ItemsSource;
        _parkedAway = true;
        SetCurrentValue(ItemsSourceProperty, null);
    }

    /// <summary>شمارهٔ «موجِ» پنهان شدن — هر عوض شدنِ صفحه یک موج (‎NotifyPagesChanged‎).</summary>
    private static int _wave;
    private static readonly Dictionary<ExcelGrid, int> _parkWave = new();

    private static void ParkSoon(ExcelGrid g)
    {
        //  ⛔ فقط **آخرین** صفحه‌ای که بسته شد مهلت دارد (سنجهٔ ‎perf-compare‎، ۱۴۰۵/۰۷/۲۲): ردیف‌های
        //  زندهٔ پنهانِ چند صفحه، تعویضِ تم را ۴ برابر کند می‌کردند و پارکِ دیرشان وسطِ کارِ بعدی
        //  می‌افتاد. جدول‌های موجِ پیشین همین حالا پارک می‌شوند — همان هزینه‌ای که همیشه سرِ رفتن بود.
        foreach (var (old, w) in _parkWave.ToList())
            if (w != _wave) { _parkAt.Remove(old); _parkWave.Remove(old); if (!old.IsEffectivelyVisible && !old.SectionShown()) old.ParkNow(); }
        _parkWave[g] = _wave;
        _parkAt[g] = PumpYaqobi.Domain.AppClock.Mono + ParkGrace;
        if (_parkTimer is null)
        {
            //  ⚠️ فقط تا وقتی جدولی در صفِ پارک است می‌تپد؛ صفِ خالی ⇒ ایستاده (بی‌کاری صفر)
            _parkTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _parkTimer.Tick += (_, _) => ParkDue(PumpYaqobi.Domain.AppClock.Mono);
        }
        if (!_parkTimer.IsEnabled) _parkTimer.Start();
    }

    /// <summary>هر جدولی که مهلتش گذشته و هنوز پنهان است پارک می‌شود.</summary>
    private static void ParkDue(DateTime now)
    {
        foreach (var (g, at) in _parkAt.ToList())
        {
            if (at > now) continue;
            _parkAt.Remove(g);
            _parkWave.Remove(g);
            if (g.GetVisualRoot() is null || g.IsEffectivelyVisible || g.SectionShown()) continue;
            g.ParkNow();
        }
        if (_parkAt.Count == 0) _parkTimer?.Stop();
    }

    /// <summary>سنجه‌ها: همهٔ جدول‌های در صف همین حالا پارک شوند (مثلِ گذشتنِ مهلت).</summary>
    public static void ParkPendingNow() => ParkDue(DateTime.MaxValue);

    /// <summary>شمارِ جدول‌هایی که منتظرِ پارک‌اند — برای سنجه.</summary>
    public static int ParkPendingCount => _parkAt.Count;

    /// <summary>
    /// جدول‌هایی که مهلتشان (به‌اضافهٔ ‎slack‎) گذشته و هنوز در صف‌اند — یعنی تپشِ پارک کار نکرد.
    /// ⚠️ جدولی که وسطِ صبر دوباره پنهان شد مهلتِ تازه گرفته و این‌جا شمرده نمی‌شود.
    /// </summary>
    public static int ParkOverdueCount(TimeSpan slack)
    {
        var limit = PumpYaqobi.Domain.AppClock.Mono - slack;
        return _parkAt.Values.Count(at => at <= limit);
    }
}
