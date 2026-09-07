namespace PumpYaqobi.Application.Services;

/// <summary>
/// ══ تطبیقِ نامِ گفته‌شده با فهرستِ حساب‌ها ═══════════════════════════════════
/// رونوشتِ ‎_staffLev‎ · ‎_staffSim‎ · ‎_staffNorm‎ · ‎_staffScoreName‎ ·
/// و تصمیمِ ‎_staffApplyVoice‎ (بندِ ۲۴).
///
/// وقتی موتورِ گفتار متنی می‌دهد، آن متن هیچ‌وقت دقیقاً نامِ حساب نیست:
/// «احمدشاه» می‌شود «احمد شاه»، «کریم» می‌شود «كريم». پس نزدیک‌ترین نام
/// پیدا می‌شود، نه نامِ برابر.
/// </summary>
public static class StaffNameMatch
{
    /// <summary>نمرهٔ لازم برای «مطمئنم» — پایین‌تر از این، پرسیده می‌شود.</summary>
    public const double SureScore = 0.72;

    /// <summary>نفرِ اول باید دستِ‌کم این‌قدر از نفرِ دوم جلوتر باشد.</summary>
    public const double SureGap = 0.08;

    /// <summary>پایین‌تر از این، اصلاً پیشنهادی داده نمی‌شود.</summary>
    public const double AskScore = 0.35;

    /// <summary>در فهرستِ «کدام‌شان بود؟» نام‌های کم‌نمره‌تر از این نمی‌آیند.</summary>
    public const double ListScore = 0.3;

    /// <summary>‎_staffLev(a, b)‎ — فاصلهٔ ویرایشی.</summary>
    public static int Levenshtein(string a, string b)
    {
        int m = a.Length, n = b.Length;
        if (m == 0) return n;
        if (n == 0) return m;

        var prev = new int[n + 1];
        var cur = new int[n + 1];
        for (var j = 0; j <= n; j++) prev[j] = j;

        for (var i = 1; i <= m; i++)
        {
            cur[0] = i;
            for (var j = 1; j <= n; j++)
                cur[j] = Math.Min(Math.Min(prev[j] + 1, cur[j - 1] + 1),
                                  prev[j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1));
            (prev, cur) = (cur, prev);
        }
        return prev[n];
    }

    /// <summary>‎_staffSim(a, b)‎ — یک منهای فاصلهٔ نسبی؛ بینِ صفر و یک.</summary>
    public static double Similarity(string a, string b)
    {
        var len = Math.Max(a.Length, b.Length);
        if (len == 0) len = 1;
        return 1 - (double)Levenshtein(a, b) / len;
    }

    /// <summary>
    /// ‎_staffNorm(s)‎ — نرمال‌سازیِ ‎normFa‎ به‌علاوهٔ حرف‌های عربی.
    ///
    /// ⚠️ موتورهای گفتار گاهی «أ إ آ ة ؤ ئ ى» می‌نویسند؛ بی این، «كريم»
    /// هیچ‌وقت با «کریم» یکی نمی‌شد.
    /// </summary>
    public static string Norm(string? s)
    {
        var t = PostingService.NormFa(s);
        var sb = new System.Text.StringBuilder(t.Length);
        foreach (var ch in t)
            sb.Append(ch switch
            {
                'أ' or 'إ' or 'آ' or 'ٱ' => 'ا',
                'ة' => 'ه',
                'ؤ' => 'و',
                'ئ' or 'ى' => 'ی',
                _ => ch,
            });
        return sb.ToString();
    }

    /// <summary>‎_staffScoreName(name, q)‎ — نمرهٔ نزدیکیِ یک نام به گفتهٔ کاربر.</summary>
    public static double ScoreName(string? name, string? q)
    {
        var nName = Norm(name);
        var nQ = Norm(q);
        if (nQ.Length == 0 || nName.Length == 0) return 0;
        if (nName == nQ) return 1;
        if (nName.Contains(nQ, StringComparison.Ordinal)
            || nQ.Contains(nName, StringComparison.Ordinal)) return 0.95;

        var best = Similarity(nName, nQ);

        // مقایسهٔ کلمه‌به‌کلمه: «احمد» در «احمد شاه» باید نمرهٔ خوبی بگیرد،
        // حتی وقتی فاصلهٔ ویرایشیِ کلِ دو رشته بزرگ است.
        var qT = nQ.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var nT = nName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (qT.Length > 0 && nT.Length > 0)
        {
            double sum = 0;
            foreach (var qt in qT)
            {
                var mx = double.NegativeInfinity;
                foreach (var nt in nT) mx = Math.Max(mx, Similarity(nt, qt));
                sum += mx;
            }
            best = Math.Max(best, sum / qT.Length);
        }
        return best;
    }

    /// <summary>یک نامزد در فهرستِ تطبیق.</summary>
    /// <param name="Key">کلیدِ یکتای همان حساب («p12|s3»).</param>
    public readonly record struct Candidate(string Key, string PersonKey, string Label, double Score);

    /// <summary>تصمیمِ نهایی: باز کن، بپرس، یا نشناختم.</summary>
    public enum VoiceOutcome { Open, Ask, NotFound }

    public readonly record struct VoiceDecision(
        VoiceOutcome Outcome, Candidate? Best, IReadOnlyList<Candidate> Choices);

    /// <summary>
    /// ‎_staffApplyVoice(alts)‎ — از میانِ برداشت‌های صوتی، تصمیمِ نهایی.
    ///
    /// ⚠️ نمرهٔ بالا به‌تنهایی کافی نیست: اگر دو نفر تقریباً هم‌نمره باشند
    /// (گفتنِ «احمد» با دو حسابِ «احمدشاه» و «احمد ولی») نباید حدس بزند —
    /// باید بپرسد. و «هم‌نمره» فقط بینِ **دو شخصِ جدا** معنی دارد: حسابِ
    /// اصلی و فرعیِ یک شخص رقیبِ هم نیستند.
    /// </summary>
    public static VoiceDecision Decide(IEnumerable<Candidate> accounts, IReadOnlyList<string> alternatives)
    {
        var scored = accounts
            .Select(a => a with { Score = alternatives.Count == 0 ? 0 : alternatives.Max(t => ScoreName(a.Label, t)) })
            .OrderByDescending(a => a.Score)
            .ToList();

        if (scored.Count == 0)
            return new VoiceDecision(VoiceOutcome.NotFound, null, Array.Empty<Candidate>());

        var best = scored[0];
        var second = scored.FirstOrDefault(x => x.PersonKey != best.PersonKey,
                                           new Candidate("", "", "", double.NaN));
        var clear = double.IsNaN(second.Score) || (best.Score - second.Score) >= SureGap;

        if (best.Score >= SureScore && clear)
            return new VoiceDecision(VoiceOutcome.Open, best, Array.Empty<Candidate>());

        if (best.Score >= AskScore)
            return new VoiceDecision(VoiceOutcome.Ask, best,
                scored.Where(x => x.Score >= ListScore).Take(4).ToList());

        return new VoiceDecision(VoiceOutcome.NotFound, best, Array.Empty<Candidate>());
    }
}
