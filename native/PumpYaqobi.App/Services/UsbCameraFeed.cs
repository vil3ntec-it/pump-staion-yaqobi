using FlashCap;

namespace PumpYaqobi.App.Services;

/// <summary>یک دوربینِ USBِ پیداشده — نامش و شماره‌اش در فهرست.</summary>
/// <param name="Index">شمارهٔ همین دوربین در فهرست؛ در لینک می‌نشیند (‎usb:0‎).</param>
public sealed record UsbCamera(int Index, string Name);

/// <summary>
/// ══ دوربینِ USB (وب‌کم) — بندِ ۱۵ ═════════════════════════════════════════════
/// همتای ‎refreshLocalCams‎ · ‎_localCamToggle‎ · ‎stopAllLocalCams‎ی نسخهٔ وب،
/// که آن‌جا با ‎getUserMedia‎ انجام می‌شد.
///
/// ⚠️ چرا FlashCap و نه OpenCV یا Emgu: هر دوی آن‌ها ده‌ها مگابایت فایلِ بومی
/// به نصاب اضافه می‌کنند. FlashCap **خالصِ دات‌نت** است و مستقیم با
/// DirectShow/Media Foundationِ خودِ ویندوز حرف می‌زند، پس اندازهٔ نصاب
/// تقریباً دست‌نخورده می‌ماند.
///
/// ⚠️ مثلِ پخش‌کنندهٔ ویدیو، این هم هرگز نباید برنامه را بشکند: اگر شمردنِ
/// دوربین‌ها یا باز کردنشان نشد، فهرست خالی برمی‌گردد و کارت پیامِ روشن
/// می‌دهد — نه یک کادرِ سیاه و نه یک استثنا.
/// </summary>
public sealed class UsbCameraFeed : IDisposable
{
    /// <summary>پیشوندِ لینکِ دوربینِ USB — ‎usb:0‎، ‎usb:1‎ …</summary>
    public const string Scheme = "usb:";

    /// <summary>لینکِ دوربینِ شمارهٔ داده‌شده.</summary>
    public static string UrlOf(int index) =>
        Scheme + index.ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>شمارهٔ دوربین از لینک. ‎null‎ یعنی لینکِ USB نیست.</summary>
    public static int? IndexOf(string? url)
    {
        var u = (url ?? "").Trim();
        if (!u.StartsWith(Scheme, StringComparison.OrdinalIgnoreCase)) return null;
        return int.TryParse(u[Scheme.Length..], System.Globalization.NumberStyles.Integer,
                            System.Globalization.CultureInfo.InvariantCulture, out var i) && i >= 0
            ? i : null;
    }

    /// <summary>
    /// دوربین‌های USBِ وصل‌شده. فهرستِ خالی یعنی «هیچ‌کدام» — چه واقعاً نباشند،
    /// چه سیستم اجازه ندهد. هیچ‌وقت استثنا نمی‌دهد.
    /// </summary>
    public static IReadOnlyList<UsbCamera> List()
    {
        try
        {
            var found = new List<UsbCamera>();
            var i = 0;
            foreach (var d in new CaptureDevices().EnumerateDescriptors())
            {
                // دستگاهی که هیچ حالتِ تصویری ندارد دوربین نیست (مثلاً ورودیِ صدا).
                if (d.Characteristics.Length == 0) continue;
                var name = string.IsNullOrWhiteSpace(d.Name) ? "دوربینِ USB" : d.Name;
                found.Add(new UsbCamera(i++, name));
            }
            return found;
        }
        catch (Exception e)
        {
            LastError = e.Message;
            return Array.Empty<UsbCamera>();
        }
    }

    /// <summary>چرا کار نکرد — برای نشان دادن به کاربر.</summary>
    public static string LastError { get; private set; } = "";

    private CaptureDevice? _device;
    private CancellationTokenSource? _cts;

    /// <summary>هر فریمِ تازه، به‌صورت بایت‌های یک عکس (‎ExtractImage‎).</summary>
    public event Action<byte[]>? FrameArrived;

    public event Action<string>? Failed;

    public bool IsRunning => _device is not null;

    public void Start(int index)
    {
        Stop();
        var cts = new CancellationTokenSource();
        _cts = cts;
        _ = Task.Run(() => OpenAsync(index, cts.Token), cts.Token);
    }

    private async Task OpenAsync(int index, CancellationToken ct)
    {
        try
        {
            var descriptors = new CaptureDevices().EnumerateDescriptors()
                .Where(d => d.Characteristics.Length > 0).ToList();

            if (index < 0 || index >= descriptors.Count)
            {
                Failed?.Invoke("این دوربینِ USB دیگر وصل نیست");
                return;
            }

            var descriptor = descriptors[index];

            // بزرگ‌ترین حالتی که تا ‎۱۲۸۰‎ پهنا دارد: بزرگ‌ترش روی کارتِ ۲۱۰
            // پیکسلی دیده نمی‌شود و فقط پردازنده می‌خورد، ولی خیلی کوچکش
            // خواندنِ کیو‌آر را سخت می‌کند.
            var chars = descriptor.Characteristics
                .Where(c => c.Width <= 1280)
                .OrderByDescending(c => c.Width)
                .FirstOrDefault() ?? descriptor.Characteristics[0];

            var device = await descriptor.OpenAsync(chars, async scope =>
            {
                try
                {
                    var image = scope.Buffer.ExtractImage();
                    FrameArrived?.Invoke(image);
                }
                catch { /* فریمِ خراب — همان یکی رد می‌شود */ }
                await Task.CompletedTask;
            });

            if (ct.IsCancellationRequested) { await device.StopAsync(); device.Dispose(); return; }

            _device = device;
            await device.StartAsync();
        }
        catch (Exception e)
        {
            LastError = e.Message;
            Failed?.Invoke("دوربینِ USB باز نشد — دسترسیِ دوربینِ ویندوز را بررسی کنید ("
                           + Short(e.Message) + ")");
        }
    }

    public void Stop()
    {
        var cts = _cts;
        _cts = null;
        if (cts is not null) { try { cts.Cancel(); } catch { } cts.Dispose(); }

        var device = _device;
        _device = null;
        if (device is null) return;

        // ⚠️ بستن روی نخِ دیگر: ‎StopAsync‎ منتظرِ تمام شدنِ فریمِ در جریان
        // می‌ماند و اگر روی نخِ رابط کاربری صدا زده شود، صفحه یخ می‌زند.
        _ = Task.Run(async () =>
        {
            try { await device.StopAsync(); } catch { }
            try { device.Dispose(); } catch { }
        });
    }

    public void Dispose() => Stop();

    private static string Short(string m) => m.Length > 80 ? m[..80] : m;
}
