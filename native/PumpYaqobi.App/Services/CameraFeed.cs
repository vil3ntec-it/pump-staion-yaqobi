using Avalonia.Media.Imaging;
using PumpYaqobi.Application.Services;

namespace PumpYaqobi.App.Services;

/// <summary>
/// ══ تصویرِ زندهٔ یک دوربین ═══════════════════════════════════════════════════
/// همان کاری که ‎_camMount‎ برای لینک‌های «عکس» می‌کرد، ولی بومی: خودِ برنامه
/// از دوربین عکس می‌گیرد و نشان می‌دهد — نه ‎&lt;img&gt;‎، نه مرورگر.
///
/// دو حالتِ واقعیِ دوربین‌های شبکه‌ای پوشش داده می‌شود:
///
///   • **MJPEG** (‎multipart/x-mixed-replace‎) — یک اتصالِ باز که پشتِ سرِ هم
///     عکس می‌فرستد. تصویر روان است و بارِ شبکه کم.
///   • **عکسِ لحظه‌ای** (‎snapshot.cgi‎ و مانندش) — هر بار یک عکس. این‌جا هر
///     یک ثانیه دوباره گرفته می‌شود، با همان ‎_t=‎ی نسخهٔ وب تا کشِ میانی
///     عکسِ کهنه ندهد.
///
/// خودش می‌فهمد کدام است: از ‎Content-Type‎ِ پاسخ. اگر دوربین دروغ بگوید،
/// باز هم کار می‌کند — جداکنندهٔ فریم روی یک عکسِ تنها هم همان یک عکس را می‌دهد.
/// </summary>
public sealed class CameraFeed : IDisposable
{
    /// <summary>فاصلهٔ گرفتنِ عکسِ لحظه‌ای. کمتر از این، دوربینِ ارزان را می‌خواباند.</summary>
    public const int SnapshotIntervalMs = 1000;

    /// <summary>پس از قطع شدن، این‌قدر صبر و دوباره تلاش — بی این، یک قطعیِ کوتاه تصویر را برای همیشه می‌بُرد.</summary>
    public const int RetryDelayMs = 3000;

    private static readonly HttpClient Http = new(new SocketsHttpHandler
    {
        // اتصالِ MJPEG هیچ‌وقت تمام نمی‌شود، پس مهلتِ کلی نباید باشد.
        PooledConnectionLifetime = TimeSpan.FromMinutes(10),
    })
    { Timeout = Timeout.InfiniteTimeSpan };

    private CancellationTokenSource? _cts;

    /// <summary>هر فریمِ تازه. روی نخِ رابط کاربری صدا زده نمی‌شود.</summary>
    public event Action<Bitmap>? FrameArrived;

    /// <summary>
    /// همان فریم، ولی هنوز فشرده (JPEG).
    ///
    /// پویشِ کیو‌آر از این می‌خواند نه از ‎FrameArrived‎: تصویرِ اِوالونیا برای
    /// نشان دادن ساخته شده و پیکسل‌های خامش را پس نمی‌دهد، ولی خواندنِ کیو‌آر
    /// دقیقاً همان پیکسل‌ها را می‌خواهد. جدا بودنشان یعنی پویش حتی وقتی کار
    /// می‌کند که رمزگشاییِ تصویر برای نمایش شکست بخورد.
    /// </summary>
    public event Action<byte[]>? JpegArrived;

    /// <summary>پیامِ خطا برای نشان دادن زیرِ کارت.</summary>
    public event Action<string>? Failed;

    public bool IsRunning => _cts is not null;

    /// <summary>شروعِ پخش. اگر از پیش در حال پخش باشد، اول متوقف می‌شود.</summary>
    public void Start(string url)
    {
        Stop();
        var cts = new CancellationTokenSource();
        _cts = cts;
        _ = Task.Run(() => LoopAsync(url, cts.Token), cts.Token);
    }

    public void Stop()
    {
        var cts = _cts;
        _cts = null;
        if (cts is null) return;
        try { cts.Cancel(); } catch { }
        cts.Dispose();
    }

    public void Dispose() => Stop();

    private async Task LoopAsync(string url, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await OnceAsync(url, ct);
            }
            catch (OperationCanceledException) { return; }
            catch (Exception e)
            {
                Failed?.Invoke("تصویر نیامد — لینک یا اتصال را بررسی کنید (" + Short(e) + ")");
            }

            try { await Task.Delay(RetryDelayMs, ct); }
            catch (OperationCanceledException) { return; }
        }
    }

    private static string Short(Exception e)
    {
        var m = e.Message;
        return m.Length > 80 ? m[..80] : m;
    }

    private async Task OnceAsync(string url, CancellationToken ct)
    {
        var target = CameraService.CacheBusted(url, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        using var req = new HttpRequestMessage(HttpMethod.Get, target);
        using var res = await Http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
        res.EnsureSuccessStatusCode();

        var type = res.Content.Headers.ContentType?.MediaType ?? "";
        if (type.Contains("multipart", StringComparison.OrdinalIgnoreCase))
        {
            await StreamMjpegAsync(res, ct);
            return;
        }

        // عکسِ تنها — بگیر، نشان بده، یک ثانیه صبر کن، دوباره.
        var bytes = await res.Content.ReadAsByteArrayAsync(ct);
        Publish(bytes);
        while (!ct.IsCancellationRequested)
        {
            await Task.Delay(SnapshotIntervalMs, ct);
            var again = CameraService.CacheBusted(url, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            Publish(await Http.GetByteArrayAsync(again, ct));
        }
    }

    private async Task StreamMjpegAsync(HttpResponseMessage res, CancellationToken ct)
    {
        var splitter = new MjpegSplitter();
        await using var stream = await res.Content.ReadAsStreamAsync(ct);
        var buffer = new byte[64 * 1024];

        while (!ct.IsCancellationRequested)
        {
            var n = await stream.ReadAsync(buffer, ct);
            if (n <= 0) return;                       // دوربین اتصال را بست — حلقهٔ بیرونی دوباره وصل می‌شود
            foreach (var frame in splitter.Feed(buffer, n)) Publish(frame);
        }
    }

    /// <summary>
    /// بایت‌های JPEG → تصویر. رمزگشایی روی همین نخِ پس‌زمینه انجام می‌شود تا
    /// رابط کاربری هنگام آمدنِ فریم نلرزد؛ نشستنش روی صفحه کارِ خودِ بخش است.
    /// </summary>
    private void Publish(byte[] bytes)
    {
        if (bytes.Length == 0) return;

        // ⚠️ پیش از رمزگشاییِ تصویر: اگر قالبِ فریم برای نمایش ناشناخته باشد،
        // پویشِ کیو‌آر همچنان همان بایت‌ها را می‌بیند.
        try { JpegArrived?.Invoke(bytes); } catch { }

        try
        {
            using var ms = new MemoryStream(bytes, writable: false);
            FrameArrived?.Invoke(new Bitmap(ms));
        }
        catch
        {
            // فریمِ نصفه یا قالبِ ناشناخته — همان یکی رد می‌شود، پخش نمی‌ایستد.
        }
    }
}
