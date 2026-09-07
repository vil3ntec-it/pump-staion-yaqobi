using System.Text.Json;
using PumpYaqobi.Application.Services;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ بندِ ۲۴: موتورِ صدای آفلاین ═════════════════════════════════════════════
/// FFT، بانکِ مِل، بریدنِ سکوت، MFCC+دلتا و فاصلهٔ DTW — همه با پاسخِ خودِ
/// ‎index.html‎ در یک کرومیومِ واقعی سنجیده می‌شوند.
///
/// چرا این‌قدر سخت‌گیرانه: صداهایی که کاربر ثبت می‌کند فقط ویژگی‌های فشرده‌اند.
/// اگر موتورِ نیتیو یک واحد جای دیگری گرد کند، همهٔ صداهای ثبت‌شده بی‌مصرف
/// می‌شوند و کاربر فقط می‌بیند «دیگر نمی‌شناسد».
/// </summary>
public class VoiceParityTests
{
    private sealed record Band(int[] idx, double[] w);
    private sealed record FftCase(double[] inRe, double[] inIm, double[] outRe, double[] outIm);
    private sealed record TrimCase(string id, bool ok, int off, int len);
    private sealed record FeatCase(string id, bool ok, int n, int dim, int[] d);
    private sealed record DtwCase(string a, string b, double d);
    private sealed record DownCase(int rate, int len, double[] @out);
    private sealed record LevCase(string a, string b, int d);
    private sealed record NormCase(string s, string @out);
    private sealed record ScoreCase(string name, string q, double sc);
    private sealed record Consts(int SR, int FRAME, int HOP, int NFFT, int MEL, int CEP,
                                 double ACCEPT, double MARGIN);
    private sealed record Clip(string id, int[] pcm);
    private sealed record Golden(List<Band> bank, List<FftCase> fft, List<TrimCase> trim,
                                 List<FeatCase> feats, List<DtwCase> dtw, List<DownCase> down,
                                 List<LevCase> lev, List<NormCase> norm, List<ScoreCase> score,
                                 Consts consts, List<Clip> clips);

    private static Golden? _g;

    private static Golden G()
    {
        if (_g is not null) return _g;
        var p = Path.Combine(AppContext.BaseDirectory, "golden-voice.json");
        if (!File.Exists(p)) p = "golden-voice.json";
        return _g = JsonSerializer.Deserialize<Golden>(File.ReadAllText(p))!;
    }

    /// <summary>نمونه‌های ۱۶بیتی → اعشار، دقیقاً همان‌طور که هارنسِ طلایی می‌کند.</summary>
    private static float[] Pcm(string id)
    {
        var clip = G().clips.First(c => c.id == id);
        var f = new float[clip.pcm.Length];
        for (var i = 0; i < f.Length; i++) f[i] = clip.pcm[i] / 32768f;
        return f;
    }

    private static void Close(double expected, double actual, double tol = 1e-9)
    {
        var t = Math.Max(tol, Math.Abs(expected) * tol);
        Assert.True(Math.Abs(expected - actual) <= t, $"انتظار {expected} بود، {actual} آمد");
    }

    [Fact]
    public void Constants_HaveNotDrifted()
    {
        var c = G().consts;
        Assert.Equal(c.SR, VoiceEngine.SampleRate);
        Assert.Equal(c.FRAME, VoiceEngine.FrameLen);
        Assert.Equal(c.HOP, VoiceEngine.HopLen);
        Assert.Equal(c.NFFT, VoiceEngine.FftSize);
        Assert.Equal(c.MEL, VoiceEngine.MelBands);
        Assert.Equal(c.CEP, VoiceEngine.Cepstra);
        Assert.Equal(c.ACCEPT, VoiceEngine.Accept);
        Assert.Equal(c.MARGIN, VoiceEngine.Margin);
    }

    [Fact]
    public void MelBank_MatchesTheHtmlExactly()
    {
        var want = G().bank;
        var got = VoiceEngine.MelBank();
        Assert.Equal(want.Count, got.Length);
        for (var m = 0; m < want.Count; m++)
        {
            Assert.Equal(want[m].idx, got[m].Index);
            Assert.Equal(want[m].w.Length, got[m].Weight.Length);
            for (var i = 0; i < want[m].w.Length; i++) Close(want[m].w[i], got[m].Weight[i]);
        }
    }

    [Fact]
    public void Fft_MatchesTheHtmlExactly()
    {
        foreach (var c in G().fft)
        {
            var re = c.inRe.Select(v => (float)v).ToArray();
            var im = c.inIm.Select(v => (float)v).ToArray();
            VoiceEngine.Fft(re, im);
            for (var i = 0; i < re.Length; i++)
            {
                // ورودی و خروجی هر دو ۳۲بیتی‌اند، پس مقایسه هم باید ۳۲بیتی باشد.
                Assert.Equal((float)c.outRe[i], re[i]);
                Assert.Equal((float)c.outIm[i], im[i]);
            }
        }
    }

    [Fact]
    public void SilenceTrimming_MatchesTheHtmlExactly()
    {
        foreach (var c in G().trim)
        {
            var span = VoiceEngine.Trim(Pcm(c.id));
            if (!c.ok) { Assert.Null(span); continue; }
            Assert.NotNull(span);
            Assert.Equal(c.off, span!.Value.Offset);
            Assert.Equal(c.len, span.Value.Length);
        }
    }

    /// <summary>
    /// مهم‌ترین آزمونِ این بند: بایت‌به‌بایت. ویژگی‌ها همان چیزی‌اند که در
    /// دیتابیس می‌نشینند، پس یک بایتِ جابه‌جا یعنی صدای ثبت‌شده دیگر نمی‌خواند.
    /// </summary>
    [Fact]
    public void Features_MatchTheHtmlByteForByte()
    {
        foreach (var c in G().feats)
        {
            var f = VoiceEngine.Features(Pcm(c.id));
            if (!c.ok) { Assert.Null(f); continue; }
            Assert.NotNull(f);
            Assert.Equal(c.n, f!.Frames);
            Assert.Equal(c.dim, f.Dim);
            Assert.Equal(c.d.Length, f.Data.Length);
            for (var i = 0; i < c.d.Length; i++)
                Assert.True(c.d[i] == f.Data[i],
                    $"صدای «{c.id}» در خانهٔ {i}: انتظار {c.d[i]} بود، {f.Data[i]} آمد");
        }
    }

    [Fact]
    public void DtwDistance_MatchesTheHtml()
    {
        var feats = G().clips.ToDictionary(c => c.id, c => VoiceEngine.Features(Pcm(c.id)));
        foreach (var c in G().dtw)
        {
            var a = feats[c.a];
            var b = feats[c.b];
            Assert.NotNull(a); Assert.NotNull(b);
            Close(c.d, VoiceEngine.Distance(a!, b!));
        }
    }

    /// <summary>صدای هر کس با خودش فاصلهٔ صفر دارد و با دیگری نه.</summary>
    [Fact]
    public void EveryClip_IsNearestToItself()
    {
        foreach (var c in G().dtw)
        {
            if (c.a == c.b) Close(0, c.d, 1e-12);
            else Assert.True(c.d > 0.5, $"{c.a}/{c.b} خیلی نزدیک است: {c.d}");
        }
    }

    [Fact]
    public void Resampling_MatchesTheHtml()
    {
        foreach (var c in G().down)
        {
            var src = Pcm("ahmad").Take(c.len).ToArray();
            var got = VoiceEngine.DownTo16k(src, c.rate);
            Assert.Equal(c.@out.Length, got.Length);
            for (var i = 0; i < got.Length; i++) Assert.Equal((float)c.@out[i], got[i]);
        }
    }

    // ── تطبیقِ نام ─────────────────────────────────────────────────────────
    [Fact]
    public void Levenshtein_MatchesTheHtmlExactly()
    {
        foreach (var c in G().lev) Assert.Equal(c.d, StaffNameMatch.Levenshtein(c.a, c.b));
    }

    [Fact]
    public void NameNormalisation_MatchesTheHtmlExactly()
    {
        foreach (var c in G().norm) Assert.Equal(c.@out, StaffNameMatch.Norm(c.s));
    }

    [Fact]
    public void NameScore_MatchesTheHtmlExactly()
    {
        foreach (var c in G().score) Close(c.sc, StaffNameMatch.ScoreName(c.name, c.q));
    }
}
