using System.Runtime.InteropServices;
using LibVLCSharp.Shared;

namespace PumpYaqobi.App.Services;

/// <summary>
/// ══ پخشِ زندهٔ RTSP و HLS — بندِ ۱۵ ═══════════════════════════════════════════
/// دوربینِ مداربستهٔ واقعی (و هر NVR/DVR) تصویرش را با ‎rtsp://‎ می‌دهد، نه با
/// عکسِ لحظه‌ای. تا پیش از این برنامه چنین لینکی را به پخش‌کنندهٔ ویندوز
/// می‌سپرد — یعنی کاربر برای دیدنِ دوربین باید از برنامه بیرون می‌رفت.
///
/// این کلاس تصویر را **داخلِ خودِ برنامه** می‌آورد: VLC فریم‌ها را رمزگشایی
/// می‌کند و به‌جای اینکه خودش روی پنجره‌ای بکشدشان، پیکسل‌های خام را به ما
/// می‌دهد و ما همان‌ها را روی کارتِ دوربین می‌گذاریم.
///
/// چرا «فراخوانِ ویدیو» (video callbacks) و نه کنترلِ آمادهٔ VLC:
///
///   • کنترلِ آمادهٔ VLC یک پنجرهٔ بومیِ جدا روی برنامه می‌نشاند که نه اسکرول
///     می‌شود، نه پشتِ کارت می‌رود، و نه با چیدمانِ اِوالونیا جور درمی‌آید.
///   • پیکسل‌های خام یعنی <b>پویشِ کیو‌آر روی همین تصویر</b> هم رایگان به دست
///     می‌آید — همان کاری که برای دوربین‌های MJPEG می‌کنیم.
///
/// ⚠️ **هیچ‌وقت نباید برنامه را بشکند.** اگر کتابخانهٔ VLC نبود یا بالا نیامد،
/// <see cref="Available"/> خاموش می‌ماند و کارتِ دوربین به همان رفتارِ قبلی
/// برمی‌گردد («باز کردن در پخش‌کنندهٔ ویندوز»). تصویرِ نیامدهٔ بی‌توضیح از یک
/// دکمهٔ صادق بدتر است، ولی برنامهٔ بسته‌شده از هر دو بدتر است.
/// </summary>
public sealed class VlcVideoFeed : IDisposable
{
    /// <summary>
    /// اندازهٔ ثابتِ فریم. خودِ VLC تصویرِ دوربین را به همین می‌رساند، پس
    /// نه لازم است اندازه را از دوربین بپرسیم و نه با هر بار عوض شدنِ آن
    /// بافر را از نو بسازیم.
    ///
    /// ⚠️ بزرگ‌ترش نه روی کارتِ ۲۱۰ پیکسلی دیده می‌شود و نه به خواندنِ کیو‌آر
    /// کمک می‌کند؛ فقط پردازنده و حافظه می‌خورد — ده دوربین یعنی ده برابر.
    /// </summary>
    public const uint Width = 960;
    public const uint Height = 540;
    private const uint Pitch = Width * 4;
    private const uint FrameBytes = Pitch * Height;

    // ── بالا آوردنِ کتابخانه، یک‌بار برای کلِ برنامه ─────────────────────────
    private static readonly object Gate = new();
    private static LibVLC? _vlc;
    private static bool _tried;

    /// <summary>پیامِ اینکه چرا VLC بالا نیامد — برای نشان دادن به کاربر.</summary>
    public static string InitError { get; private set; } = "";

    /// <summary>VLC در دسترس است؟ اولین بار کتابخانه را بالا می‌آورد.</summary>
    public static bool Available
    {
        get
        {
            lock (Gate)
            {
                if (_tried) return _vlc is not null;
                _tried = true;
                try
                {
                    Core.Initialize();
                    _vlc = new LibVLC(
                        "--no-audio",              // دوربینِ مداربسته صدا لازم ندارد
                        "--no-video-title-show",
                        "--quiet",
                        // تأخیرِ شبکه: کمترش تصویر را می‌بُرد، بیشترش تصویر را
                        // عقب می‌اندازد. ۳۰۰ میلی‌ثانیه همان چیزی است که خودِ
                        // VLC برای پخشِ زنده پیشنهاد می‌کند.
                        "--network-caching=300",
                        // RTSP روی TCP، نه UDP: روی وای‌فای و شبکهٔ شلوغ،
                        // UDP فریم گم می‌کند و تصویر تکه‌تکه می‌شود.
                        "--rtsp-tcp");
                }
                catch (Exception e)
                {
                    _vlc = null;
                    InitError = e.Message;
                }
                return _vlc is not null;
            }
        }
    }

    // ── نمونهٔ هر دوربین ────────────────────────────────────────────────────
    private MediaPlayer? _player;
    private IntPtr _buffer;
    private readonly object _bufferGate = new();

    // ⚠️ این سه باید به‌عنوان فیلد نگه داشته شوند: VLC اشاره‌گرشان را نگه
    // می‌دارد و اگر فقط محلی باشند، زباله‌روب جمعشان می‌کند و اولین فریم
    // برنامه را با AccessViolation می‌بندد.
    private MediaPlayer.LibVLCVideoLockCb? _lockCb;
    private MediaPlayer.LibVLCVideoUnlockCb? _unlockCb;
    private MediaPlayer.LibVLCVideoDisplayCb? _displayCb;

    /// <summary>یک فریمِ تازه: پیکسل‌های BGRA، به اندازهٔ ‎Width×Height‎.</summary>
    public event Action<byte[]>? FrameArrived;

    public event Action<string>? Failed;

    public bool IsRunning => _player is not null;

    public void Start(string url)
    {
        Stop();
        if (!Available) { Failed?.Invoke("پخش‌کنندهٔ ویدیو بالا نیامد" + Why()); return; }

        try
        {
            _buffer = Marshal.AllocHGlobal((int)FrameBytes);

            var player = new MediaPlayer(_vlc!);
            _lockCb = OnLock;
            _unlockCb = OnUnlock;
            _displayCb = OnDisplay;

            player.SetVideoFormat("RV32", Width, Height, Pitch);
            player.SetVideoCallbacks(_lockCb, _unlockCb, _displayCb);
            player.EncounteredError += OnError;

            using var media = new Media(_vlc!, new Uri(url));
            _player = player;
            player.Play(media);
        }
        catch (Exception e)
        {
            Stop();
            Failed?.Invoke("تصویر نیامد — لینک یا اتصال را بررسی کنید (" + Short(e.Message) + ")");
        }
    }

    public void Stop()
    {
        var player = _player;
        _player = null;

        if (player is not null)
        {
            try { player.EncounteredError -= OnError; } catch { }
            try { player.Stop(); } catch { }
            try { player.Dispose(); } catch { }
        }

        // ⚠️ بافر فقط **بعد از** ایستادنِ پخش آزاد می‌شود: تا وقتی VLC زنده
        // است ممکن است همان لحظه در حالِ نوشتن در آن باشد، و آزاد کردنش
        // زیرِ پایش یعنی بسته شدنِ برنامه.
        lock (_bufferGate)
        {
            if (_buffer != IntPtr.Zero) { Marshal.FreeHGlobal(_buffer); _buffer = IntPtr.Zero; }
        }

        _lockCb = null; _unlockCb = null; _displayCb = null;
    }

    public void Dispose() => Stop();

    // ── فراخوان‌های VLC ─────────────────────────────────────────────────────

    /// <summary>«کجا بنویسم؟» — همیشه همان یک بافر.</summary>
    private IntPtr OnLock(IntPtr opaque, IntPtr planes)
    {
        Marshal.WriteIntPtr(planes, 0, _buffer);
        return _buffer;
    }

    private void OnUnlock(IntPtr opaque, IntPtr picture, IntPtr planes) { }

    /// <summary>«فریم آماده است» — کپی می‌شود و بیرون می‌رود.</summary>
    private void OnDisplay(IntPtr opaque, IntPtr picture)
    {
        var handler = FrameArrived;
        if (handler is null) return;

        byte[] frame;
        lock (_bufferGate)
        {
            if (_buffer == IntPtr.Zero) return;
            frame = new byte[FrameBytes];
            Marshal.Copy(_buffer, frame, 0, (int)FrameBytes);
        }
        handler(frame);
    }

    private void OnError(object? sender, EventArgs e) =>
        Failed?.Invoke("پخش نشد — لینک، نام کاربری یا رمزِ دوربین را بررسی کنید");

    private static string Why() => InitError.Length > 0 ? " (" + Short(InitError) + ")" : "";

    private static string Short(string m) => m.Length > 80 ? m[..80] : m;
}
