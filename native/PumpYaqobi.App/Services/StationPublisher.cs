using System.Security.Cryptography;
using System.Text.Json;

namespace PumpYaqobi.App.Services;

/// <summary>
/// ══ انتشارِ زندهٔ برنامه روی سرورِ خانگی ═════════════════════════════════════
///
/// تا امروز سیم کشیده بود ولی برق نداشت: <see cref="HomeSync"/> بود و وصل هم
/// می‌شد، ولی هیچ داده‌ای منتشر نمی‌شد — پس اپِ کارمندان و ربات هیچ چیزی برای
/// نشان دادن نداشتند. این کلاس همان برق است.
///
///     نیتیو ──set──▶ stations/&lt;کد&gt;-live ──▶ اپِ کارمندان (گوشی/شورت‌کات)
///
/// ══ چرا ‎-live‎ و نه خودِ ‎stations/&lt;کد&gt;‎ ════════════════════════════════════
///
/// شاخهٔ ‎stations/&lt;کد&gt;‎ مالِ نسخهٔ وبِ قدیمی است و آن، کلِ شاخه را یک‌جا
/// ‎set‎ می‌کند. اگر این‌جا هم همان‌جا می‌نوشتیم، هر کدام دیگری را پاک می‌کرد.
/// شاخهٔ جدا یعنی هیچ‌کدام به آن یکی دست نمی‌زند.
///
/// ══ «هر تغییری که در اپ می‌شود در ربات هم باشد» ═══════════════════════════
///
/// بی این‌که حتی یک خط به مسیرهای ذخیرهٔ برنامه اضافه شود: هر
/// <see cref="Interval"/> یک عکسِ تازه ساخته می‌شود و <b>فقط اگر با عکسِ قبلی
/// فرق داشته باشد</b> فرستاده می‌شود. پس نه منطقِ ذخیره دست می‌خورد و نه
/// شبکه بیخود شلوغ می‌شود.
///
/// ⚠️ هیچ خطایی بیرون نمی‌دهد. سرورِ خانگی ممکن است خاموش باشد، اینترنت
/// نباشد، یا کاربر هنوز وارد نشده باشد — هیچ‌کدام نباید برنامه را بلرزاند.
/// </summary>
public sealed class StationPublisher : IAsyncDisposable
{
    /// <summary>هر چند وقت یک‌بار دنبالِ تغییر بگردد.</summary>
    public static readonly TimeSpan Interval = TimeSpan.FromSeconds(20);

    private readonly AppHost _host;
    private readonly HomeSync _sync;
    private readonly Func<string> _stationCode;
    private CancellationTokenSource? _loop;
    private string _lastHash = "";

    public StationPublisher(AppHost host, HomeSync sync, Func<string> stationCode)
    { _host = host; _sync = sync; _stationCode = stationCode; }

    /// <summary>‎stations/&lt;کد&gt;-live‎ — همان جایی که گوشی گوش می‌دهد.</summary>
    public static string PathOf(string? stationCode)
    {
        var code = (stationCode ?? "").Trim();
        if (code.Length == 0) code = "pump1";
        return "stations/" + code + "-live";
    }

    /// <summary>عکسِ همین لحظه، بی فرستادن — برای آزمون و برای دکمهٔ دستی.</summary>
    public Task<Dictionary<string, object?>> SnapshotAsync(CancellationToken ct = default)
        => StationSnapshot.BuildAsync(_host, ct);

    /// <summary>
    /// یک‌بار منتشر کن. ‎force‎ی خالی یعنی «فقط اگر چیزی عوض شده».
    /// خروجی: آیا واقعاً چیزی رفت.
    /// </summary>
    public async Task<bool> PublishOnceAsync(bool force = false, CancellationToken ct = default)
    {
        if (!_sync.Configured) return false;
        try
        {
            var snap = await StationSnapshot.BuildAsync(_host, ct);

            // ⚠️ ‎seq‎ هر بار عوض می‌شود، پس در محکِ «چیزی عوض شده؟» نمی‌آید —
            // وگرنه هر بیست ثانیه یک‌بار کلِ داده بیخود فرستاده می‌شد.
            var hash = HashOf(snap);
            if (!force && hash == _lastHash) return false;

            if (!await _sync.SetAsync(PathOf(_stationCode()), snap, ct)) return false;
            _lastHash = hash;
            return true;
        }
        catch (OperationCanceledException) { throw; }
        catch { return false; }
    }

    /// <summary>حلقهٔ پس‌زمینه. صدا زدنش دو بار، یکی بیشتر نمی‌سازد.</summary>
    public void Start()
    {
        if (_loop is not null) return;
        var cts = new CancellationTokenSource();
        _loop = cts;
        _ = Task.Run(() => LoopAsync(cts.Token), cts.Token);
    }

    private async Task LoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try { await PublishOnceAsync(false, ct); }
            catch (OperationCanceledException) { return; }
            catch { /* سرورِ خاموش خطا نیست */ }

            try { await Task.Delay(Interval, ct); }
            catch { return; }
        }
    }

    /// <summary>
    /// اثرِ انگشتِ عکس، بی ‎seq‎ و بی زمان — تا «عوض شد؟» معنی داشته باشد.
    /// </summary>
    public static string HashOf(Dictionary<string, object?> snap)
    {
        var copy = new Dictionary<string, object?>(snap);
        copy.Remove("seq");
        copy.Remove("at");
        copy.Remove("atUtc");
        var bytes = JsonSerializer.SerializeToUtf8Bytes(copy);
        return Convert.ToHexString(SHA256.HashData(bytes));
    }

    public async ValueTask DisposeAsync()
    {
        var cts = _loop;
        _loop = null;
        if (cts is null) return;
        await cts.CancelAsync();
        cts.Dispose();
    }
}
