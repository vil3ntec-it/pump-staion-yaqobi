using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace PumpYaqobi.Domain;

/// <summary>
/// ══ 🌐 زبانِ پشتو (۱۴۰۵/۰۷/۲۸) ═══════════════════════════════════════════════
///
/// خواستهٔ صاحب ریپو: «برای برنامه زبان پشتو هم اضافه کن» — «فقط پشتو، انگلیسی نمی‌خواهد»
/// و PDFها هم «هم‌زبانِ برنامه».
///
/// ⛔ <b>منبع همان متنِ فارسی است</b>، نه کلیدِ جدا: فرهنگِ <c>Lang/ps.json</c> «فارسی ⇐
/// پشتو» است و هیچ کدِ نمایشی عوض نشد. نوشته در لحظهٔ نمایش برگردانده می‌شود
/// (<c>Controls.LangHook</c> در برنامه، <see cref="T"/> در PDF). چیزی که در فرهنگ نیست
/// فارسی می‌ماند — هرگز خالی یا خراب نمی‌شود.
///
/// ترتیبِ جست‌وجو برای هر نوشته:
/// <code>
///   ۱) خودِ نوشته               «ذخیره»            ⇐ «خوندي کړئ»
///   ۲) عددها ⇐ {0}             «۱۲ لیتر فروخته شد» ⇐ «{0} لیتر فروخته شد»
///   ۳) بی سر و تهِ غیرِحرفی     «✅ ذخیره:»          ⇐ «✅» + «ذخیره» + «:»
///   ۴) الگوی میان‌دار           «مشتری احمد حذف شد» ⇐ «مشتری {0} حذف شد» (و «احمد» خودش برگردانده می‌شود)
///   ۵) تکه‌ها                   «… — …»، «…: …»، «(…)»، ««…»» هر تکه جدا
/// </code>
///
/// ⚡ <b>فارسی هیچ هزینه‌ای ندارد</b>: تا <see cref="Use"/> با «ps» صدا نخورد، فرهنگ بار
/// نمی‌شود و <see cref="T"/> همان نوشته را پس می‌دهد. در پشتو هر نوشته یک بار برگردانده
/// و در <c>Cache</c> نگه داشته می‌شود.
/// </summary>
public static class Lang
{
    public const string Persian = "fa";
    public const string Pashto = "ps";

    private static LangMap? _cur;

    /// <summary>زبانِ فعلی: «fa» یا «ps».</summary>
    public static string Code { get; private set; } = Persian;

    /// <summary>برگرداندن روشن است (زبان فارسی نیست).</summary>
    public static bool On => _cur is not null;

    /// <summary>زبان را بگذار. هر چیزِ جز «ps» یعنی فارسی.</summary>
    public static void Use(string? code)
    {
        if (code == Pashto)
        {
            if (_cur is null) _cur = new LangMap(ReadResource(Pashto));
            Code = Pashto;
        }
        else
        {
            _cur = null;
            Code = Persian;
        }
    }

    /// <summary>فرهنگِ جاسازی‌شده — برای آزمون‌ها هم.</summary>
    public static Dictionary<string, string> ReadResource(string code)
    {
        using var s = typeof(Lang).Assembly.GetManifestResourceStream("PumpYaqobi.Lang." + code + ".json")
                      ?? throw new InvalidOperationException("Lang resource missing: " + code);
        return JsonSerializer.Deserialize<Dictionary<string, string>>(s)
               ?? new Dictionary<string, string>();
    }

    /// <summary>نوشته به زبانِ فعلی. چیزی که برگردانده نشود، <b>همان شیء</b> برمی‌گردد.</summary>
    public static string T(string s) => _cur is { } m ? m.T(s) : s;

    /// <summary>موتورِ فعلی — فقط برای سنجه‌ها.</summary>
    public static LangMap? Current => _cur;

    /// <summary>برای جاهایی که <c>null</c> هم می‌آید.</summary>
    public static string? TOrNull(string? s) => s is null ? null : T(s);
}

/// <summary>
/// موتورِ برگرداندن برای یک فرهنگ — جدا از <see cref="Lang"/> تا آزمون‌ها بی دست زدن به
/// زبانِ سراسری (و آزمون‌های موازیِ دیگر) بسنجندش. ترتیبِ جست‌وجو بالای <see cref="Lang"/>.
/// </summary>
public sealed class LangMap
{
    private const int CacheMax = 50_000;
    private const int MaxDepth = 3;

    private readonly Dictionary<string, string> _map;
    private readonly Dictionary<string, List<Tpl>> _tpls;
    private readonly Dictionary<string, string> _cache = new(StringComparer.Ordinal);

    public LangMap(IReadOnlyDictionary<string, string> map)
    {
        _map = new Dictionary<string, string>(map.Count, StringComparer.Ordinal);
        _tpls = new Dictionary<string, List<Tpl>>(StringComparer.Ordinal);
        foreach (var (k, v) in map)
        {
            if (string.IsNullOrEmpty(k) || string.IsNullOrEmpty(v)) continue;
            _map[k] = v;
            AddTpl(k, v);
        }
        //  ── کلیدهای کمکی؛ هرگز جای کلیدِ اصلی را نمی‌گیرند ──
        //  کد بیشترِ نوشته‌ها را با «+» می‌سازد: ‎n + " مورد"‎، ‎"‹ برگشت به " + title‎.
        //  پس تکه‌ای که فاصلهٔ سر/ته دارد خودش الگوست («{0} مورد»، «‹ برگشت به {0}»)،
        //  و همان بی فاصله و بی سر و تهِ غیرِحرفی («📈 …: » ⇐ «…») هم شناخته می‌شود.
        foreach (var (k, v) in map)
        {
            if (string.IsNullOrEmpty(k) || string.IsNullOrEmpty(v) || Holes.IsMatch(k)) continue;
            var kt = k.Trim();
            if (kt.Length > 0 && kt.Length != k.Length) _map.TryAdd(kt, v.Trim());
            if (FirstLetter(k) is var a and >= 0 && FirstLetter(v) is var va and >= 0)
            {
                var kc = k[a..(LastLetter(k) + 1)];
                if (kc.Length != k.Length) _map.TryAdd(kc, v[va..(LastLetter(v) + 1)]);
            }
            bool lead = char.IsWhiteSpace(k[0]), trail = char.IsWhiteSpace(k[^1]);
            if (!lead && !trail) continue;
            string tk = k, tv = v;
            if (lead) { tk = "{0}" + tk; tv = "{0}" + tv; }
            if (trail) { var h = lead ? "{1}" : "{0}"; tk += h; tv += h; }
            if (_map.TryAdd(tk, tv)) AddTpl(tk, tv);
        }
        foreach (var list in _tpls.Values) list.Sort((a, b) => b.Letters.CompareTo(a.Letters));
    }

    public int Count => _map.Count;

    private void AddTpl(string k, string v)
    {
        if (Tpl.TryMake(k, v) is not { } tpl) return;
        if (!_tpls.TryGetValue(tpl.Anchor, out var list)) _tpls[tpl.Anchor] = list = new List<Tpl>();
        list.Add(tpl);
    }

    /// <summary>فقط برای سنجه‌ها (‎PUMP_LANG_MISS‎): هر نوشته‌ای که برگردانده نشد.</summary>
    public Action<string>? Missed { get; set; }

    /// <summary>فرهنگ این نوشته را می‌شناسد — حتی اگر پشتویش همان فارسی باشد («پروفایل»).</summary>
    public bool Knows(string s) => !string.IsNullOrEmpty(s) && Translate(s, 0) is not null;

    /// <summary>نوشته به پشتو. چیزی که برگردانده نشود، <b>همان شیء</b> برمی‌گردد.</summary>
    public string T(string s)
    {
        if (string.IsNullOrEmpty(s) || !HasLetter(s)) return s;
        lock (_cache)
            if (_cache.TryGetValue(s, out var c)) return c;
        var r = Translate(s, 0) ?? s;
        if (ReferenceEquals(r, s)) Missed?.Invoke(s);
        lock (_cache)
        {
            if (_cache.Count > CacheMax) _cache.Clear();
            _cache[s] = r;
            //  نوشتهٔ برگردانده دوباره برگردانده نشود (قلابِ نوشته آن را دوباره می‌بیند)
            if (!ReferenceEquals(r, s)) _cache[r] = r;
        }
        return r;
    }

    // ── موتور ───────────────────────────────────────────────────────────────

    private string? Translate(string s, int depth)
    {
        if (Whole(s) is { } w) return w;

        var a = FirstLetter(s);
        if (a < 0) return null;
        var b = LastLetter(s);
        //  سر و تهِ غیرِحرفی («✅ »، «:»، «.») — بی هر دو، بی سر، بی ته
        Span<(int From, int To)> cuts = stackalloc (int, int)[3];
        var n = 0;
        if (a > 0 || b < s.Length - 1) cuts[n++] = (a, b + 1);
        if (a > 0 && b < s.Length - 1) { cuts[n++] = (a, s.Length); cuts[n++] = (0, b + 1); }

        for (var i = 0; i < n; i++)
            if (Whole(s[cuts[i].From..cuts[i].To]) is { } c) return s[..cuts[i].From] + c + s[cuts[i].To..];

        if (depth >= MaxDepth) return null;
        if (Template(s, depth) is { } t0) return t0;
        for (var i = 0; i < n; i++)
            if (Template(s[cuts[i].From..cuts[i].To], depth) is { } t) return s[..cuts[i].From] + t + s[cuts[i].To..];
        if (Segments(s[a..(b + 1)], depth) is { } g) return s[..a] + g + s[(b + 1)..];
        if (WordByWord(s[a..(b + 1)]) is { } ww) return s[..a] + ww + s[(b + 1)..];
        return null;
    }

    /// <summary>خودِ نوشته، یا همان با عددهایی که <c>{n}</c> شده‌اند.</summary>
    private string? Whole(string s)
    {
        if (_map.TryGetValue(s, out var t)) return t;
        if (!HasDigit(s)) return null;
        var nums = new List<string>();
        var key = Num.Replace(s, m => { nums.Add(m.Value); return "{" + (nums.Count - 1) + "}"; });
        if (!_map.TryGetValue(key, out t)) return null;
        return Fill(t, nums);
    }

    private static string Fill(string tpl, IReadOnlyList<string> vals) =>
        Holes.Replace(tpl, m =>
        {
            var i = int.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
            return i < vals.Count ? vals[i] : m.Value;
        });

    private string? Template(string core, int depth)
    {
        if (_tpls.Count == 0) return null;
        HashSet<Tpl>? tried = null;
        foreach (var w in Words(core))
        {
            if (!_tpls.TryGetValue(w, out var list)) continue;
            foreach (var tpl in list)
            {
                if (!(tried ??= new HashSet<Tpl>()).Add(tpl)) continue;
                if (tpl.Match(core) is not { } holes) continue;
                var vals = new string[holes.Length];
                for (var i = 0; i < holes.Length; i++)
                    vals[i] = Translate(holes[i], depth + 1) ?? holes[i];
                return Fill(tpl.Dst, vals);
            }
        }
        return null;
    }

    /// <summary>جداکننده‌هایی که دو سویشان دو تکهٔ مستقل است.</summary>
    private static readonly Regex Seps = new(
        @"(\s+[—–·•|⇐⇒←→]\s+|\s*\n\s*|:\s+|[،؛]\s*|\s*[«»()]\s*|\s*…\s*)",
        RegexOptions.CultureInvariant);

    private string? Segments(string core, int depth)
    {
        var parts = Seps.Split(core);
        if (parts.Length < 2) return null;
        var sb = new StringBuilder(core.Length + 16);
        var any = false;
        for (var i = 0; i < parts.Length; i++)
        {
            var p = parts[i];
            //  جداکننده‌ها (اندیس‌های فرد) همان‌طور می‌مانند
            if (i % 2 == 0 && p.Length > 0 && HasLetter(p) && Translate(p, depth + 1) is { } t)
            {
                sb.Append(t);
                any = true;
            }
            else sb.Append(p);
        }
        return any ? sb.ToString() : null;
    }

    /// <summary>
    /// آخرین راه، فقط برای دو یا سه واژه که <b>هر کدام</b> جدا شناخته‌اند — مثلِ تاریخِ
    /// «سه‌شنبه میزان» ⇐ «درېشنبه تله». یک واژهٔ ناشناخته ⇒ هیچ (نیمه‌ترجمه ساخته نمی‌شود).
    /// </summary>
    private string? WordByWord(string core)
    {
        var words = core.Split(' ');
        if (words.Length is < 2 or > 3) return null;
        for (var i = 0; i < words.Length; i++)
        {
            if (!HasLetter(words[i])) continue;
            if (!_map.TryGetValue(words[i], out var t) || t.Contains(' ')) return null;
            words[i] = t;
        }
        return string.Join(' ', words);
    }

    // ── الگوی میان‌دار ─────────────────────────────────────────────────────────

    private static readonly Regex Holes = new(@"\{(\d+)\}", RegexOptions.CultureInvariant);

    private static readonly Regex Num = new(
        @"[0-9۰-۹٠-٩](?:[0-9۰-۹٠-٩,٫٬./:]*[0-9۰-۹٠-٩])?",
        RegexOptions.CultureInvariant);

    private sealed class Tpl
    {
        public required string Src { get; init; }
        public required string Dst { get; init; }
        public required string Anchor { get; init; }
        public required int Letters { get; init; }
        private Regex? _rx;

        public static Tpl? TryMake(string src, string dst)
        {
            if (!Holes.IsMatch(src)) return null;
            var literal = Holes.Replace(src, "\u0001");
            var letters = literal.Count(IsLetter);
            //  الگویی که جز سوراخ تقریباً هیچ ندارد («{0} و {1}») هر جمله‌ای را می‌گرفت
            if (letters < 3) return null;
            var anchor = "";
            foreach (var w in Words(literal))
                if (w.Length > anchor.Length) anchor = w;
            if (anchor.Length < 2) return null;
            return new Tpl { Src = src, Dst = dst, Anchor = anchor, Letters = letters };
        }

        public string[]? Match(string s)
        {
            Regex rx;
            try { rx = _rx ??= Build(Src); }
            catch (ArgumentException) { return null; }
            Match m;
            try { m = rx.Match(s); }
            catch (RegexMatchTimeoutException) { return null; }
            if (!m.Success) return null;
            var n = 0;
            foreach (Match h in Holes.Matches(Src))
                n = Math.Max(n, int.Parse(h.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) + 1);
            var r = new string[n];
            for (var i = 0; i < n; i++) r[i] = m.Groups["h" + i].Success ? m.Groups["h" + i].Value : "";
            return r;
        }

        private static Regex Build(string src)
        {
            var sb = new StringBuilder("^");
            var seen = new HashSet<int>();
            var last = 0;
            foreach (Match h in Holes.Matches(src))
            {
                sb.Append(Regex.Escape(src[last..h.Index]));
                var i = int.Parse(h.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
                sb.Append(seen.Add(i) ? "(?<h" + i + ">.*?)" : @"\k<h" + i + ">");
                last = h.Index + h.Length;
            }
            sb.Append(Regex.Escape(src[last..])).Append('$');
            return new Regex(sb.ToString(), RegexOptions.CultureInvariant | RegexOptions.Singleline,
                             TimeSpan.FromMilliseconds(50));
        }
    }

    // ── حرف و عدد ───────────────────────────────────────────────────────────

    /// <summary>حرفِ عربی‌نویس (بی رقم و نشانه) یا حرفِ لاتین.</summary>
    private static bool IsLetter(char c) =>
        c is >= 'ء' and <= 'ٟ' or >= 'ٰ' and <= 'ۓ' or >= 'ۺ' and <= 'ۿ'
        || (c < '؀' && char.IsLetter(c));

    private static bool IsArabic(char c) =>
        c is >= 'ء' and <= 'ٟ' or >= 'ٰ' and <= 'ۓ' or >= 'ۺ' and <= 'ۿ';

    private static bool HasLetter(string s)
    {
        foreach (var c in s) if (IsArabic(c)) return true;
        return false;
    }

    private static bool HasDigit(string s)
    {
        foreach (var c in s)
            if (c is >= '0' and <= '9' or >= '۰' and <= '۹' or >= '٠' and <= '٩') return true;
        return false;
    }

    private static int FirstLetter(string s)
    {
        for (var i = 0; i < s.Length; i++) if (IsLetter(s[i])) return i;
        return -1;
    }

    private static int LastLetter(string s)
    {
        for (var i = s.Length - 1; i >= 0; i--) if (IsLetter(s[i])) return i;
        return -1;
    }

    /// <summary>واژه‌های عربی‌نویسِ نوشته (برای پیدا کردنِ الگو).</summary>
    private static IEnumerable<string> Words(string s)
    {
        var start = -1;
        for (var i = 0; i <= s.Length; i++)
        {
            var arabic = i < s.Length && (IsArabic(s[i]) || s[i] == '‌');
            if (arabic && start < 0) start = i;
            else if (!arabic && start >= 0)
            {
                yield return s[start..i].Trim('‌');
                start = -1;
            }
        }
    }
}
