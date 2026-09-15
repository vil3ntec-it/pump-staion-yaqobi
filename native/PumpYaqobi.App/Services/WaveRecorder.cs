using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace PumpYaqobi.App.Services;

/// <summary>
/// ══ ضبطِ صدا از میکروفون — فقط ویندوز، بی هیچ بسته‌ای ═══════════════════════
///
/// «ضبطِ صدا هم باشد تا یارو برایم بفرستد و برای من هم باشد.» برنامه روی
/// ویندوز نصب می‌شود و ‎winmm‎ همان‌جا هست؛ کتابخانهٔ صوتی اضافه کردن یعنی یک
/// وابستگیِ بومیِ دیگر که ساخت را می‌شکند (همان دردِ androidx در اپِ اندروید).
///
/// خروجی یک WAV ساده است (۱۶ کیلوهرتز، تک‌کانال، ۱۶ بیت) — هر مرورگر و هر
/// گوشی‌ای بازش می‌کند. روی غیرِ ویندوز <see cref="Available"/> خاموش است و
/// دکمهٔ ضبط اصلاً نشان داده نمی‌شود.
/// </summary>
public sealed class WaveRecorder : IDisposable
{
    public static bool Available => OperatingSystem.IsWindows();

    private const int Rate = 16000;
    private const int Bits = 16;
    private const int BufBytes = Rate * 2 / 10;   // یک‌دهم ثانیه

    private IntPtr _dev;
    private readonly List<byte> _data = new();
    private readonly List<(IntPtr Hdr, IntPtr Buf)> _bufs = new();
    private GCHandle _self;
    private bool _running;

    public bool IsRecording => _running;

    /// <summary>شروع. روی غیرِ ویندوز یا بی میکروفون ‎false‎.</summary>
    public bool Start()
    {
        if (!Available || _running) return false;
        return StartWindows();
    }

    /// <summary>پایان — بایت‌های WAV. خالی یعنی چیزی ضبط نشد.</summary>
    public byte[] Stop()
    {
        if (!_running) return Array.Empty<byte>();
        _running = false;
        if (Available) StopWindows();
        return Wav(_data.ToArray());
    }

    [SupportedOSPlatform("windows")]
    private bool StartWindows()
    {
        var fmt = new WaveFormat
        {
            wFormatTag = 1, nChannels = 1, nSamplesPerSec = Rate, wBitsPerSample = Bits,
            nBlockAlign = 2, nAvgBytesPerSec = Rate * 2, cbSize = 0,
        };
        _self = GCHandle.Alloc(this);
        _callback = Callback;
        var r = waveInOpen(out _dev, unchecked((uint)-1), ref fmt, _callback, IntPtr.Zero, 0x00030000 /* CALLBACK_FUNCTION */);
        if (r != 0) { _self.Free(); return false; }
        for (var i = 0; i < 4; i++) AddBuffer();
        if (waveInStart(_dev) != 0) { StopWindows(); return false; }
        _running = true;
        _data.Clear();
        return true;
    }

    [SupportedOSPlatform("windows")]
    private void AddBuffer()
    {
        var buf = Marshal.AllocHGlobal(BufBytes);
        var hdr = Marshal.AllocHGlobal(Marshal.SizeOf<WaveHdr>());
        var h = new WaveHdr { lpData = buf, dwBufferLength = BufBytes };
        Marshal.StructureToPtr(h, hdr, false);
        waveInPrepareHeader(_dev, hdr, Marshal.SizeOf<WaveHdr>());
        waveInAddBuffer(_dev, hdr, Marshal.SizeOf<WaveHdr>());
        _bufs.Add((hdr, buf));
    }

    [SupportedOSPlatform("windows")]
    private void StopWindows()
    {
        try
        {
            if (_dev != IntPtr.Zero)
            {
                waveInStop(_dev);
                waveInReset(_dev);
                foreach (var (hdr, buf) in _bufs)
                {
                    waveInUnprepareHeader(_dev, hdr, Marshal.SizeOf<WaveHdr>());
                    Marshal.FreeHGlobal(hdr);
                    Marshal.FreeHGlobal(buf);
                }
                _bufs.Clear();
                waveInClose(_dev);
                _dev = IntPtr.Zero;
            }
        }
        catch { /* دستگاه رفت — چیزی برای آزاد کردن نیست */ }
        if (_self.IsAllocated) _self.Free();
    }

    private WaveInProc? _callback;

    [SupportedOSPlatform("windows")]
    private void Callback(IntPtr dev, uint msg, IntPtr inst, IntPtr param1, IntPtr param2)
    {
        if (msg != 0x3C0 /* MM_WIM_DATA */ || !_running) return;
        try
        {
            var h = Marshal.PtrToStructure<WaveHdr>(param1);
            if (h.dwBytesRecorded > 0)
            {
                var chunk = new byte[h.dwBytesRecorded];
                Marshal.Copy(h.lpData, chunk, 0, chunk.Length);
                lock (_data) _data.AddRange(chunk);
            }
            waveInAddBuffer(dev, param1, Marshal.SizeOf<WaveHdr>());
        }
        catch { /* بافرِ خراب — این تکه می‌افتد، ضبط ادامه دارد */ }
    }

    /// <summary>سربرگِ WAV روی نمونه‌های خام.</summary>
    public static byte[] Wav(byte[] pcm)
    {
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);
        w.Write("RIFF"u8); w.Write(36 + pcm.Length); w.Write("WAVE"u8);
        w.Write("fmt "u8); w.Write(16); w.Write((short)1); w.Write((short)1);
        w.Write(Rate); w.Write(Rate * 2); w.Write((short)2); w.Write((short)Bits);
        w.Write("data"u8); w.Write(pcm.Length); w.Write(pcm);
        w.Flush();
        return ms.ToArray();
    }

    public void Dispose()
    {
        if (_running) Stop();
    }

    // ── winmm ────────────────────────────────────────────────────────────
    private delegate void WaveInProc(IntPtr dev, uint msg, IntPtr inst, IntPtr p1, IntPtr p2);

    [StructLayout(LayoutKind.Sequential)]
    private struct WaveFormat
    {
        public short wFormatTag; public short nChannels; public int nSamplesPerSec; public int nAvgBytesPerSec;
        public short nBlockAlign; public short wBitsPerSample; public short cbSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WaveHdr
    {
        public IntPtr lpData; public int dwBufferLength; public int dwBytesRecorded; public IntPtr dwUser;
        public int dwFlags; public int dwLoops; public IntPtr lpNext; public IntPtr reserved;
    }

    [DllImport("winmm.dll")] private static extern int waveInOpen(out IntPtr dev, uint id, ref WaveFormat fmt, WaveInProc cb, IntPtr inst, uint flags);
    [DllImport("winmm.dll")] private static extern int waveInPrepareHeader(IntPtr dev, IntPtr hdr, int size);
    [DllImport("winmm.dll")] private static extern int waveInUnprepareHeader(IntPtr dev, IntPtr hdr, int size);
    [DllImport("winmm.dll")] private static extern int waveInAddBuffer(IntPtr dev, IntPtr hdr, int size);
    [DllImport("winmm.dll")] private static extern int waveInStart(IntPtr dev);
    [DllImport("winmm.dll")] private static extern int waveInStop(IntPtr dev);
    [DllImport("winmm.dll")] private static extern int waveInReset(IntPtr dev);
    [DllImport("winmm.dll")] private static extern int waveInClose(IntPtr dev);
}
