namespace PumpYaqobi.Application.Services;

/// <summary>ویژگی‌های یک صدا — چارچوب‌ها × بُعد، فشرده در یک بایت.</summary>
/// <param name="Frames">تعدادِ چارچوب‌های زمانی.</param>
/// <param name="Dim">بُعدِ هر چارچوب (‎CEP × 2‎ — خودِ ضرایب و دلتاهایشان).</param>
/// <param name="Data">‎Frames × Dim‎ مقدارِ کوانتیده.</param>
public sealed record VoiceFeatures(int Frames, int Dim, sbyte[] Data);

/// <summary>
/// ══ موتورِ صدای آفلاین ══════════════════════════════════════════════════════
/// رونوشتِ ‎_vxFft‎ · ‎_vxGetMelBank‎ · ‎_vxTrim‎ · ‎_vxFeatures‎ · ‎_vxDtw‎ ·
/// ‎_vxDownTo16k‎ (بندِ ۲۴).
///
/// کارِ این موتور یک چیزِ مشخص است: از میانِ «فهرستِ نام‌های خودِ برنامه» آن
/// نامی را پیدا کند که گفته شده. برای این کار سرور و اینترنت لازم نیست:
///
///     صدا → بریدنِ سکوت → MFCC (+دلتا) → مقایسهٔ DTW با صداهای ثبت‌شده
///
/// همه‌اش روی خودِ دستگاه، بی هیچ فایلی که دانلود شود. یادگیری هم بی‌زحمت
/// است: هر بار که نشناخت و خودتان روی نام زدید، همان صدا برای آن نام ثبت
/// می‌شود و دفعهٔ بعد می‌شناسدش.
///
/// ⚠️ هر عددِ ثابتِ این‌جا از نسخهٔ وب آمده و سرخود عوض نمی‌شود: با تغییرِ
/// یکی‌شان، همهٔ صداهایی که کاربر تا امروز ثبت کرده بی‌مصرف می‌شوند —
/// ویژگی‌های ذخیره‌شده دیگر با ویژگی‌های تازه هم‌جنس نیستند.
/// </summary>
public static class VoiceEngine
{
    public const int SampleRate = 16000;   // _VX_SR
    public const int FrameLen = 400;       // _VX_FRAME — ۲۵ میلی‌ثانیه
    public const int HopLen = 160;         // _VX_HOP   — ۱۰ میلی‌ثانیه
    public const int FftSize = 512;        // _VX_NFFT
    public const int MelBands = 26;        // _VX_MEL
    public const int Cepstra = 13;         // _VX_CEP

    /// <summary>بیشتر از این فاصله یعنی «نشناختم» (‎_VX_ACCEPT‎).</summary>
    public const double Accept = 2.1;

    /// <summary>نفرِ اول باید دستِ‌کم این‌قدر از نفرِ دوم نزدیک‌تر باشد (‎_VX_MARGIN‎).</summary>
    public const double Margin = 1.12;

    /// <summary>بیشتر از این صدا برای یک نام نگه داشته نمی‌شود (‎_VX_MAX_PER_NAME‎).</summary>
    public const int MaxPerName = 3;

    // ── FFT رادیکس-۲ (درجا) ─────────────────────────────────────────────────
    /// <summary>
    /// ‎_vxFft(re, im)‎ — همان الگوریتم، و همان **دقت**.
    ///
    /// ⚠️ آرایه‌ها ‎float‎ هستند و متغیرهای محلی ‎double‎ — عمدی است، نه سهو:
    /// در نسخهٔ وب ‎re‎ و ‎im‎ از نوعِ ‎Float32Array‎ اند ولی ضرب‌ها با عددهای
    /// معمولیِ جاوااسکریپت (double) انجام می‌شوند و فقط هنگامِ **نشستن در
    /// آرایه** به ۳۲ بیت گرد می‌شوند. اگر این‌جا همه‌چیز double بماند، خروجی
    /// در رقمِ هفتم فرق می‌کند و ویژگی‌های کوانتیده گاهی یک واحد جابه‌جا
    /// می‌شوند — یعنی صداهایی که کاربر ثبت کرده دیگر نمی‌خوانند.
    /// </summary>
    public static void Fft(float[] re, float[] im)
    {
        var n = re.Length;
        for (int i = 1, j = 0; i < n; i++)
        {
            var bit = n >> 1;
            for (; (j & bit) != 0; bit >>= 1) j ^= bit;
            j ^= bit;
            if (i < j)
            {
                (re[i], re[j]) = (re[j], re[i]);
                (im[i], im[j]) = (im[j], im[i]);
            }
        }

        for (var len = 2; len <= n; len <<= 1)
        {
            var ang = -2 * Math.PI / len;
            var wr = Math.Cos(ang);
            var wi = Math.Sin(ang);
            for (var i = 0; i < n; i += len)
            {
                double cr = 1, ci = 0;
                for (var k = 0; k < len / 2; k++)
                {
                    double ur = re[i + k], ui = im[i + k];
                    var vr = re[i + k + len / 2] * cr - im[i + k + len / 2] * ci;
                    var vi = re[i + k + len / 2] * ci + im[i + k + len / 2] * cr;
                    re[i + k] = (float)(ur + vr); im[i + k] = (float)(ui + vi);
                    re[i + k + len / 2] = (float)(ur - vr); im[i + k + len / 2] = (float)(ui - vi);
                    var ncr = cr * wr - ci * wi;
                    ci = cr * wi + ci * wr; cr = ncr;
                }
            }
        }
    }

    // ── بانکِ فیلترِ مِل ─────────────────────────────────────────────────────
    public sealed record MelBand(int[] Index, double[] Weight);

    private static MelBand[]? _bank;

    /// <summary>‎_vxGetMelBank()‎ — ۲۶ فیلترِ مثلثی بینِ ۱۰۰ و ۷۰۰۰ هرتز.</summary>
    public static MelBand[] MelBank()
    {
        if (_bank is not null) return _bank;

        static double HzToMel(double f) => 1127 * Math.Log(1 + f / 700);
        static double MelToHz(double m) => 700 * (Math.Exp(m / 1127) - 1);

        var lo = HzToMel(100);
        var hi = HzToMel(7000);
        var pts = new int[MelBands + 2];
        for (var i = 0; i < MelBands + 2; i++)
            pts[i] = (int)Math.Floor((FftSize + 1) * MelToHz(lo + (hi - lo) * i / (MelBands + 1)) / SampleRate);

        var bank = new MelBand[MelBands];
        for (var m = 1; m <= MelBands; m++)
        {
            int f0 = pts[m - 1], f1 = pts[m], f2 = pts[m + 1];
            var idx = new List<int>();
            var w = new List<double>();
            for (var k = f0; k < f1; k++) { idx.Add(k); w.Add((double)(k - f0) / Math.Max(1, f1 - f0)); }
            for (var k = f1; k <= f2; k++) { idx.Add(k); w.Add((double)(f2 - k) / Math.Max(1, f2 - f1)); }
            bank[m - 1] = new MelBand(idx.ToArray(), w.ToArray());
        }
        return _bank = bank;
    }

    // ── بریدنِ سکوت ─────────────────────────────────────────────────────────
    /// <summary>بازهٔ صدای واقعی داخلِ ضبط. ‎null‎ یعنی چیزی گفته نشد یا خیلی کوتاه بود.</summary>
    public readonly record struct Span(int Offset, int Length);

    /// <summary>
    /// ‎_vxTrim(pcm)‎ — سکوتِ اول و آخر بریده می‌شود.
    ///
    /// «کفِ نویز» صدکِ دهمِ انرژیِ پنجره‌هاست و «اوج» بیشترینشان؛ آستانه از هر
    /// دو ساخته می‌شود تا هم در اتاقِ ساکت کار کند هم کنارِ پمپِ روشن.
    /// </summary>
    public static Span? Trim(float[] pcm)
    {
        const int win = 320;
        var n = pcm.Length / win;
        if (n < 3) return null;

        // ⚠️ ‎en‎ در نسخهٔ وب ‎Float32Array‎ است — گردکردنش روی آستانه اثر دارد.
        var en = new float[n];
        for (var i = 0; i < n; i++)
        {
            double s = 0;
            for (var k = 0; k < win; k++) { double v = pcm[i * win + k]; s += v * v; }
            en[i] = (float)Math.Sqrt(s / win);
        }

        var sorted = (float[])en.Clone();
        Array.Sort(sorted);
        var floor = sorted[(int)Math.Floor(n * 0.1)];
        var peak = sorted[n - 1];
        if (peak < 0.008) return null;                       // چیزی گفته نشد

        var th = Math.Max(floor * 2.5, peak * 0.12);
        int a = -1, b = -1;
        for (var i = 0; i < n; i++) if (en[i] >= th) { if (a < 0) a = i; b = i; }
        if (a < 0 || b - a < 2) return null;                 // خیلی کوتاه

        const int pad = 5;
        var from = Math.Max(0, (a - pad) * win);
        var to = Math.Min(pcm.Length, (b + 1 + pad) * win);
        return new Span(from, to - from);
    }

    // ── ویژگی‌ها ────────────────────────────────────────────────────────────
    /// <summary>
    /// ‎_vxFeatures(pcm)‎ — MFCC + دلتا، نرمال‌شده و کوانتیده.
    ///
    /// ⚠️ نوعِ هر آرایه عمداً همان نوعِ نسخهٔ وب است (‎float‎ برای هرچه
    /// ‎Float32Array‎ بوده، ‎double‎ برای انباشتگرهای محلی). این‌جا جای
    /// «تمیزکاری» نیست: ویژگی‌هایی که کاربر تا امروز ثبت کرده با همین دقت
    /// ساخته شده‌اند و باید با ویژگی‌های تازه هم‌جنس بمانند.
    /// </summary>
    public static VoiceFeatures? Features(float[] pcmRaw)
    {
        var span = Trim(pcmRaw);
        if (span is null || span.Value.Length < FrameLen * 3) return null;

        var len = span.Value.Length;
        var off = span.Value.Offset;

        // پیش‌تاکید — انرژیِ بمِ صدا کم و جزئیاتِ زیر پررنگ می‌شود
        var x = new float[len];
        x[0] = pcmRaw[off];
        for (var i = 1; i < len; i++) x[i] = (float)(pcmRaw[off + i] - 0.97 * pcmRaw[off + i - 1]);

        var nFrames = 1 + (len - FrameLen) / HopLen;
        if (nFrames < 4) return null;

        var bank = MelBank();
        var win = new float[FrameLen];
        for (var i = 0; i < FrameLen; i++)
            win[i] = (float)(0.54 - 0.46 * Math.Cos(2 * Math.PI * i / (FrameLen - 1)));   // همینگ

        var feats = new float[nFrames][];
        var re = new float[FftSize];
        var im = new float[FftSize];
        const int half = FftSize / 2;

        for (var f = 0; f < nFrames; f++)
        {
            Array.Clear(re); Array.Clear(im);
            var o = f * HopLen;
            for (var i = 0; i < FrameLen; i++) re[i] = x[o + i] * win[i];
            Fft(re, im);

            var pow = new float[half + 1];
            for (var k = 0; k <= half; k++)
                pow[k] = (float)(((double)re[k] * re[k] + (double)im[k] * im[k]) / FftSize);

            var mel = new float[MelBands];
            for (var m = 0; m < MelBands; m++)
            {
                double s = 0;
                var band = bank[m];
                for (var i = 0; i < band.Index.Length; i++)
                {
                    var k = band.Index[i];
                    if (k < pow.Length) s += pow[k] * band.Weight[i];
                }
                mel[m] = (float)Math.Log(s + 1e-10);
            }

            var c = new float[Cepstra];
            for (var j = 0; j < Cepstra; j++)
            {
                double s = 0;
                for (var m = 0; m < MelBands; m++) s += mel[m] * Math.Cos(Math.PI * j * (m + 0.5) / MelBands);
                c[j] = (float)s;
            }
            feats[f] = c;
        }

        // نرمال‌سازیِ میانگین/واریانس — تفاوتِ مایکروفون و بلندیِ صدا خنثی می‌شود
        var n = feats.Length;
        var mean = new float[Cepstra];
        var std = new float[Cepstra];
        foreach (var c in feats) for (var j = 0; j < Cepstra; j++) mean[j] += c[j];
        for (var j = 0; j < Cepstra; j++) mean[j] /= n;
        // ⚠️ ‎d‎ باید double باشد: در جاوااسکریپت تفریقِ دو عددِ ۳۲بیتی در ۶۴ بیت
        // انجام می‌شود و تا نشستن در آرایه گرد نمی‌شود. با ‎float d‎ اینجا یک بارِ
        // گردکردنِ اضافه می‌آمد و واریانس — و از آن‌جا همهٔ ویژگی‌ها — کمی جابه‌جا می‌شد.
        foreach (var c in feats)
            for (var j = 0; j < Cepstra; j++)
            {
                double d = (double)c[j] - mean[j];
                std[j] = (float)(std[j] + d * d);
            }
        for (var j = 0; j < Cepstra; j++)
        {
            std[j] = (float)Math.Sqrt((double)std[j] / n);
            // ‎|| 1‎ی نسخهٔ وب: واریانسِ صفر (صدای کاملاً یکنواخت) تقسیم بر صفر می‌داد.
            if (std[j] == 0) std[j] = 1;
        }

        var norm = new float[n][];
        for (var i = 0; i < n; i++)
        {
            var c = new float[Cepstra];
            for (var j = 0; j < Cepstra; j++) c[j] = (float)(((double)feats[i][j] - mean[j]) / std[j]);
            norm[i] = c;
        }

        // «دلتا» = حرکتِ صدا در زمان — دو نام با مصوت‌های همانند از همین جدا می‌شوند
        var dim = Cepstra * 2;
        var data = new sbyte[n * dim];
        for (var i = 0; i < n; i++)
        {
            var p2 = norm[Math.Max(0, i - 2)];
            var p1 = norm[Math.Max(0, i - 1)];
            var n1 = norm[Math.Min(n - 1, i + 1)];
            var n2 = norm[Math.Min(n - 1, i + 2)];
            for (var j = 0; j < Cepstra; j++)
            {
                data[i * dim + j] = Quantize(norm[i][j]);
                data[i * dim + Cepstra + j] =
                    Quantize((2.0 * (n2[j] - p2[j]) + (n1[j] - p1[j])) / 10 * 2.2);
            }
        }
        return new VoiceFeatures(n, dim, data);
    }

    /// <summary>
    /// ‎q(v)‎ — یک ویژگی در یک بایت. ‎Math.round‎ِ جاوااسکریپت است، پس نصفه‌ها
    /// به بالا می‌روند حتی برای عددِ منفی (‎Math.round(-2.5) === -2‎).
    /// </summary>
    private static sbyte Quantize(double v)
    {
        var r = Math.Floor(v * 24 + 0.5);
        if (r > 127) r = 127;
        if (r < -127) r = -127;
        return (sbyte)r;
    }

    // ── فاصلهٔ DTW ──────────────────────────────────────────────────────────
    /// <summary>
    /// ‎_vxDtw(A, B)‎ — «کِشسان در زمان»: تندتر یا کندتر گفتنِ همان نام
    /// فاصله را زیاد نمی‌کند. نوارِ محدود هم سرعت می‌دهد و هم جلوی
    /// هم‌ترازیِ بی‌معنیِ دو صدای خیلی نابرابر را می‌گیرد.
    /// </summary>
    public static double Distance(VoiceFeatures a, VoiceFeatures b)
    {
        if (a is null || b is null) return double.PositiveInfinity;
        int n = a.Frames, m = b.Frames, dim = a.Dim;
        if (n == 0 || m == 0 || a.Dim != b.Dim) return double.PositiveInfinity;

        var band = Math.Max(12, Math.Abs(n - m) + 8);
        const double inf = 1e18;
        var prev = new double[m + 1];
        var cur = new double[m + 1];
        Array.Fill(prev, inf);
        prev[0] = 0;

        for (var i = 1; i <= n; i++)
        {
            Array.Fill(cur, inf);
            var jlo = Math.Max(1, i - band);
            var jhi = Math.Min(m, i + band);
            for (var j = jlo; j <= jhi; j++)
            {
                double s = 0;
                var ai = (i - 1) * dim;
                var bj = (j - 1) * dim;
                for (var k = 0; k < dim; k++)
                {
                    var d = (a.Data[ai + k] - b.Data[bj + k]) / 24.0;
                    s += d * d;
                }
                var best = Math.Min(prev[j], Math.Min(cur[j - 1], prev[j - 1]));
                cur[j] = Math.Sqrt(s) + (best == inf ? 0 : best);
            }
            (prev, cur) = (cur, prev);
        }

        return prev[m] >= inf ? double.PositiveInfinity : prev[m] / (n + m);
    }

    // ── کم کردنِ نرخِ نمونه ──────────────────────────────────────────────────
    /// <summary>
    /// ‎_vxDownTo16k‎ — هر مایکروفونی نرخِ خودش را دارد (۴۴٫۱ و ۴۸ رایج‌اند)
    /// و موتور فقط ۱۶ کیلوهرتز می‌فهمد.
    ///
    /// ⚠️ میانگینِ پنجرهٔ کوچک، نه فقط نمونه‌برداریِ ساده: بی آن، صدای تیز
    /// «تا می‌خورد» و نویزِ ساختگی می‌سازد که ویژگی‌ها را خراب می‌کند.
    /// </summary>
    public static float[] DownTo16k(float[] all, double srcRate)
    {
        if (Math.Abs(srcRate - SampleRate) < 1) return all;

        var ratio = srcRate / SampleRate;
        var n = (int)Math.Floor(all.Length / ratio);
        var outp = new float[n];
        var w = Math.Max(1, (int)Math.Floor(ratio));

        for (var i = 0; i < n; i++)
        {
            var a = i * ratio;
            var i0 = (int)Math.Floor(a);
            var i1 = Math.Min(all.Length - 1, i0 + 1);
            double s = 0;
            var c = 0;
            for (var k = 0; k < w && i0 + k < all.Length; k++) { s += all[i0 + k]; c++; }
            outp[i] = c > 1
                ? (float)(s / c)
                : (float)(all[i0] + ((double)all[i1] - all[i0]) * (a - i0));
        }
        return outp;
    }
}
