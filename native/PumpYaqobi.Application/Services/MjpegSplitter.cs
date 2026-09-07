namespace PumpYaqobi.Application.Services;

/// <summary>
/// ══ جدا کردنِ فریم‌های MJPEG ════════════════════════════════════════════════
/// دوربین‌های ارزانِ شبکه‌ای تصویرِ زنده را به‌صورت یک جریانِ بی‌پایانِ عکس‌های
/// JPEG پشتِ سرِ هم می‌فرستند (‎multipart/x-mixed-replace‎). این کلاس آن جریان
/// را به عکس‌های جدا می‌شکند.
///
/// ⚠️ عمداً مرزِ ‎--boundary‎ را نمی‌خواند و خودِ نشانه‌های JPEG را دنبال می‌کند
/// (‎FF D8‎ آغاز، ‎FF D9‎ پایان): مرزِ هر دوربین فرق می‌کند، بعضی‌ها اصلاً
/// ‎Content-Length‎ نمی‌دهند، و بعضی هدرها را با ‎\n‎ می‌بندند نه ‎\r\n‎. ولی
/// همه‌شان JPEGِ درست می‌فرستند.
///
/// ⚠️ سقفِ اندازه لازم است: اگر لینک اصلاً MJPEG نباشد (مثلاً صفحهٔ HTML)،
/// هیچ ‎FF D9‎ای نمی‌آید و بافر تا بی‌نهایت بزرگ می‌شد.
/// </summary>
public sealed class MjpegSplitter
{
    /// <summary>بزرگ‌ترین فریمی که پذیرفته می‌شود — ۸ مگابایت، سخاوتمندانه برای 4K.</summary>
    public const int MaxFrameBytes = 8 * 1024 * 1024;

    private readonly List<byte> _buf = new();
    private bool _inFrame;

    /// <summary>
    /// تکه‌ای از جریان را می‌خورد و هر فریمِ کاملی که پیدا شد برمی‌گرداند.
    /// معمولاً صفر یا یک فریم، ولی اگر تکه بزرگ باشد می‌تواند چند تا باشد.
    /// </summary>
    public IEnumerable<byte[]> Feed(byte[] chunk, int count)
    {
        for (var i = 0; i < count; i++)
        {
            var b = chunk[i];

            if (!_inFrame)
            {
                // دنبالِ ‎FF D8‎ می‌گردیم؛ هرچه پیش از آن است (هدرِ multipart) دور ریخته می‌شود.
                if (_buf.Count == 0)
                {
                    if (b == 0xFF) _buf.Add(b);
                    continue;
                }
                if (b == 0xD8) { _buf.Add(b); _inFrame = true; continue; }
                // ‎FF‎ی پشتِ سرِ هم می‌تواند آغازِ واقعی باشد؛ بقیه دور ریخته می‌شوند.
                _buf.Clear();
                if (b == 0xFF) _buf.Add(b);
                continue;
            }

            _buf.Add(b);

            // پایانِ فریم: ‎FF D9‎
            if (b == 0xD9 && _buf.Count >= 2 && _buf[^2] == 0xFF)
            {
                yield return _buf.ToArray();
                _buf.Clear();
                _inFrame = false;
                continue;
            }

            if (_buf.Count > MaxFrameBytes) Reset();
        }
    }

    /// <summary>دور ریختنِ نیمه‌فریمِ فعلی — پس از قطع شدنِ اتصال.</summary>
    public void Reset()
    {
        _buf.Clear();
        _inFrame = false;
    }

    /// <summary>چند بایت هنوز در بافر مانده — برای آزمون و عیب‌یابی.</summary>
    public int Buffered => _buf.Count;
}
