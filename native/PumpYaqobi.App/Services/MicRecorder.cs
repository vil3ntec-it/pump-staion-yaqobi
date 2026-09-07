using NAudio.Wave;
using PumpYaqobi.Application.Services;

namespace PumpYaqobi.App.Services;

/// <summary>
/// ══ ضبط از مایکروفون ═══════════════════════════════════════════════════════
/// رونوشتِ ‎_vxRecord‎ — با این تفاوت که ‎getUserMedia‎ی مرورگر نیست، خودِ
/// مایکروفونِ ویندوز است.
///
/// مثلِ نسخهٔ وب، خودش با سکوت تمام می‌شود: بعد از اینکه صدایی شنیده شد، اگر
/// نیم‌ثانیه سکوت بیاید ضبط بسته می‌شود. کاربر لازم نیست دکمه را نگه دارد.
///
/// ⚠️ درخواستِ ۱۶ کیلوهرتزِ تک‌کاناله مستقیم از درایور: خودِ ویندوز تبدیل را
/// انجام می‌دهد و کیفیتش از کم کردنِ نرخِ دستیِ ما بهتر است. اگر درایور نتواند،
/// <see cref="VoiceEngine.DownTo16k"/> همان کارِ نسخهٔ وب را می‌کند.
/// </summary>
public sealed class MicRecorder
{
    /// <summary>بیشترین طولِ ضبط — همان ۵ ثانیهٔ نسخهٔ وب.</summary>
    public const int MaxMs = 5000;

    /// <summary>این‌قدر سکوتِ پس از صدا یعنی «حرفش تمام شد».</summary>
    public const int TailSilenceMs = 500;

    /// <summary>پایین‌تر از این، سکوت شمرده می‌شود.</summary>
    public const float SilenceLevel = 0.02f;

    /// <summary>بلندیِ لحظه‌ای، برای نشان دادنِ «دارم می‌شنوم».</summary>
    public event Action<float>? Level;

    /// <summary>
    /// یک‌بار ضبط تا سکوت یا سقفِ زمان.
    /// ‎null‎ یعنی مایکروفونی نبود یا ویندوز اجازه نداد.
    /// </summary>
    public async Task<float[]?> RecordAsync(CancellationToken ct = default)
    {
        try
        {
            return await Task.Run(() => Record(ct), ct);
        }
        catch (OperationCanceledException) { throw; }
        catch
        {
            // مایکروفون نبود، درایور نداد، یا این سیستم‌عامل winmm ندارد.
            return null;
        }
    }

    private float[]? Record(CancellationToken ct)
    {
        using var wave = new WaveInEvent
        {
            WaveFormat = new WaveFormat(VoiceEngine.SampleRate, 16, 1),
            BufferMilliseconds = 50,
        };

        var samples = new List<float>(VoiceEngine.SampleRate * 6);
        var done = new ManualResetEventSlim(false);
        var heard = false;
        var silentMs = 0;

        wave.DataAvailable += (_, e) =>
        {
            var loud = 0f;
            for (var i = 0; i + 1 < e.BytesRecorded; i += 2)
            {
                var v = (short)(e.Buffer[i] | (e.Buffer[i + 1] << 8)) / 32768f;
                samples.Add(v);
                var a = Math.Abs(v);
                if (a > loud) loud = a;
            }

            Level?.Invoke(loud);

            if (loud >= SilenceLevel) { heard = true; silentMs = 0; }
            else if (heard) silentMs += 50;

            if (heard && silentMs >= TailSilenceMs) done.Set();
            if (samples.Count >= VoiceEngine.SampleRate * MaxMs / 1000) done.Set();
        };

        wave.StartRecording();
        try
        {
            // سقفِ زمان کمی بلندتر از ‎MaxMs‎ است تا خودِ شرطِ بالا تصمیم بگیرد،
            // نه این مهلت — وگرنه صدای کاملِ کاربر نصفه می‌ماند.
            done.Wait(MaxMs + 1500, ct);
        }
        finally
        {
            try { wave.StopRecording(); } catch { }
        }

        return samples.Count > 0 ? samples.ToArray() : null;
    }
}
