using PumpYaqobi.Domain.Enums;

namespace PumpYaqobi.App.Services;

/// <summary>
/// ══ ثبتِ نرخِ اتحادیه در تاریخچه — با مکث، نه با هر حرف ═══════════════════
///
/// ⛔ تا ۳.۱.۲۱۳ «📈 تاریخچهٔ نرخ اتحادیه» هیچ‌وقت پر نمی‌شد: عوض کردنِ نرخ
/// فقط تنظیمات را می‌نوشت و <c>ToolsDataService.RecordRateAsync</c> هیچ‌جا صدا
/// زده نمی‌شد.
///
/// ⚠️ نرخ حرف‌به‌حرف تایپ می‌شود («۸» ⇒ «۸۰» ⇒ «۸۰٫۵»)، پس هر حرف یک ردیف
/// می‌شد. این‌جا آخرین مقدارِ هر تیل نگه داشته و پس از <see cref="Delay"/>
/// بی‌تایپ یک بار ثبت می‌شود. بسته شدنِ برنامه وسطِ مکث هم گمش نمی‌کند —
/// <see cref="SaveGuard"/> پیش از بستن <see cref="FlushAsync"/> را می‌زند.
/// نرخِ صفر و نرخِ برابر با آخرین ثبت را خودِ سرویس نمی‌نویسد.
/// </summary>
public sealed class RateRecorder : IPendingWrite
{
    public static readonly TimeSpan Delay = TimeSpan.FromMilliseconds(1500);

    private readonly Func<FuelType, decimal, Task> _record;
    private readonly Dictionary<FuelType, decimal> _pending = new();
    private CancellationTokenSource? _wait;

    public RateRecorder(Func<FuelType, decimal, Task> record) => _record = record;

    public bool IsDirty { get { lock (_pending) return _pending.Count > 0; } }

    /// <summary>نرخِ تازهٔ یک تیل — پس از مکث ثبت می‌شود.</summary>
    public void Note(FuelType fuel, decimal rate)
    {
        CancellationToken ct;
        lock (_pending)
        {
            _pending[fuel] = rate;
            _wait?.Cancel();
            _wait = new CancellationTokenSource();
            ct = _wait.Token;
        }
        SaveGuard.Track(this);
        _ = Task.Delay(Delay, ct).ContinueWith(t =>
        {
            if (!t.IsCanceled) SaveGuard.Watch(FlushAsync(), "تاریخچهٔ نرخ اتحادیه");
        }, TaskScheduler.Default);
    }

    public async Task FlushAsync()
    {
        List<KeyValuePair<FuelType, decimal>> take;
        lock (_pending)
        {
            take = _pending.ToList();
            _pending.Clear();
            _wait?.Cancel();
        }
        foreach (var (fuel, rate) in take)
        {
            try { await _record(fuel, rate); }
            catch { /* اجازه نیست یا دیتابیس قفل — نرخ خودش در تنظیمات نشسته است */ }
        }
    }
}
