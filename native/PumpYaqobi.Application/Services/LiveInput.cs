namespace PumpYaqobi.Application.Services;

/// <summary>
/// ══ قالبِ زندهٔ کادرهای عدد و تاریخ (۱۴۰۵/۰۷/۱۹) ══════════════════════════════
///
/// خواستهٔ صاحب ریپو: «در تمامِ فیلدهای عددی، اعداد هنگامِ تایپ با جداکننده
/// نمایش داده شوند — ‹5000› ⇐ ‹5,000› — همان لحظه، نه پس از تمام شدن.» و «در
/// فیلدهای تاریخ، اسلش خودکار در جای درست بنشیند.»
///
/// ⛔ این فقط <b>نوشتهٔ کادر</b> است، نه خواندنِ عدد: ‎Shamsi.Num‎ از قبل «,»
/// و «٬» و «،» را جداکنندهٔ هزار می‌خواند، پس هیچ حسابی عوض نمی‌شود.
/// ⛔ رقم‌ها همان‌اند که کاربر زده (فارسی یا لاتین) — فقط جداکننده می‌آید و
/// می‌رود. متنی که جز رقم و جداکننده و یک ممیز چیزی دارد دست نمی‌خورد.
/// ⚠️ هر دو تابع خالص‌اند: متن و جای مکان‌نما می‌گیرند و همان دو را پس
/// می‌دهند. جای مکان‌نما از روی «چندمین نویسهٔ غیرِجداکننده» نگه داشته
/// می‌شود، پس کاما که آمد یا رفت مکان‌نما سرِ همان رقم می‌ماند.
/// </summary>
public static class LiveInput
{
    private static bool IsDigit(char c) => c is >= '0' and <= '9' or >= '۰' and <= '۹' or >= '٠' and <= '٩';
    private static bool IsSep(char c) => c is ',' or '٬' or '،';
    private static bool IsPoint(char c) => c is '.' or '٫';

    /// <summary>
    /// «12345.5» ⇐ «12,345.5». فقط بخشِ صحیح جداکننده می‌گیرد؛ اعشار دست نمی‌خورد.
    /// <paramref name="previous"/>: متنِ پیش از این تغییر. اگر تنها فرقش برداشتنِ یک
    /// جداکننده باشد (پاک‌کن روی کاما)، رقمِ پیشِ آن برداشته می‌شود — وگرنه کاما
    /// همان لحظه برمی‌گشت و پاک‌کن «کار نمی‌کرد».
    /// </summary>
    public static (string Text, int Caret) Number(string? text, int caret, string? previous = null)
    {
        var s = text ?? "";
        caret = Math.Clamp(caret, 0, s.Length);
        if (s.Length == 0) return (s, caret);

        // پاک‌کن روی کاما ⇒ رقمِ پیشش
        if (previous is { } p && p.Length == s.Length + 1)
        {
            var at = FirstDiff(p, s);
            if (at < p.Length && IsSep(p[at]) && Shape(p) && at > 0 && IsDigit(p[at - 1]))
            {
                s = s.Remove(at - 1, 1);
                caret = at - 1;
            }
        }

        if (!Shape(s)) return (text ?? "", Math.Clamp(caret, 0, (text ?? "").Length));

        // چندمین نویسهٔ «معنادار» (نه جداکننده) پیش از مکان‌نماست؟
        var keep = 0;
        for (var i = 0; i < caret && i < s.Length; i++) if (!IsSep(s[i])) keep++;

        var start = s.Length > 0 && (s[0] is '-' or '+') ? 1 : 0;
        var point = -1;
        for (var i = start; i < s.Length; i++) if (IsPoint(s[i])) { point = i; break; }
        var intEnd = point < 0 ? s.Length : point;

        var digits = new System.Text.StringBuilder();
        for (var i = start; i < intEnd; i++) if (!IsSep(s[i])) digits.Append(s[i]);

        var sb = new System.Text.StringBuilder();
        sb.Append(s, 0, start);
        for (var i = 0; i < digits.Length; i++)
        {
            if (i > 0 && (digits.Length - i) % 3 == 0) sb.Append(',');
            sb.Append(digits[i]);
        }
        if (point >= 0)
            for (var i = point; i < s.Length; i++) if (!IsSep(s[i])) sb.Append(s[i]);

        var outText = sb.ToString();
        var outCaret = outText.Length;
        var seen = 0;
        if (keep == 0) outCaret = 0;
        else
            for (var i = 0; i < outText.Length; i++)
            {
                if (IsSep(outText[i])) continue;
                if (++seen == keep) { outCaret = i + 1; break; }
            }
        return (outText, outCaret);
    }

    /// <summary>
    /// فقط عدد: علامتِ اختیاری، رقم و جداکننده، و دست‌بالا یک ممیز با رقم‌های پسش.
    /// </summary>
    private static bool Shape(string s)
    {
        var i = s.Length > 0 && (s[0] is '-' or '+') ? 1 : 0;
        var anyDigit = false;
        var pointSeen = false;
        for (; i < s.Length; i++)
        {
            var c = s[i];
            if (IsDigit(c)) { anyDigit = true; continue; }
            if (IsSep(c)) { if (pointSeen) return false; continue; }
            if (IsPoint(c)) { if (pointSeen) return false; pointSeen = true; continue; }
            return false;
        }
        return anyDigit;
    }

    private static int FirstDiff(string a, string b)
    {
        var n = Math.Min(a.Length, b.Length);
        for (var i = 0; i < n; i++) if (a[i] != b[i]) return i;
        return n;
    }

    /// <summary>
    /// «14050719» ⇐ «1405/07/19» · «140571» ⇐ «1405/7/1» · «1405/1»+«5» ⇐ «1405/1/5».
    /// سال چهار رقم؛ ماه تا وقتی از ۱۲ نگذشته؛ روز تا وقتی از ۳۱ نگذشته.
    /// ⛔ فقط وقتی متن <b>بلندتر</b> شده (تایپ یا چسباندن) — پاک کردنِ یک «/» با
    /// پاک‌کن نباید همان لحظه برگردد. ⛔ «/»ی خودِ کاربر هیچ‌وقت جابه‌جا نمی‌شود،
    /// و متنی که جز رقم و «/» دارد دست نمی‌خورد.
    /// </summary>
    public static (string Text, int Caret) Date(string? text, int caret, string? previous = null)
    {
        var s = text ?? "";
        caret = Math.Clamp(caret, 0, s.Length);
        if (s.Length == 0 || (previous is not null && s.Length <= previous.Length)) return (s, caret);
        foreach (var c in s) if (!IsDigit(c) && c != '/') return (s, caret);

        var keep = 0;
        for (var i = 0; i < caret; i++) if (s[i] != '/') keep++;

        var parts = s.Split('/');
        var last = parts.Length - 1;
        //  ⛔ بخش‌هایی که «/»ِ خودِ کاربر بسته‌شان دست نمی‌خورند؛ فقط بخشِ در حالِ تایپ
        var outParts = new List<string>(parts[..last]);
        var seg = parts[last];
        var idx = last;                         // ۰ سال · ۱ ماه · ۲ روز
        while (true)
        {
            if (idx >= 2 || seg.Length == 0) { outParts.Add(seg); break; }
            var take = idx == 0 ? Math.Min(4, seg.Length) : Fit(seg, 12);
            if (take >= seg.Length) { outParts.Add(seg); break; }
            outParts.Add(seg[..take]);
            seg = seg[take..];
            idx++;
        }
        var outText = string.Join("/", outParts);

        var outCaret = outText.Length;
        if (keep == 0) outCaret = 0;
        else
        {
            var seen = 0;
            for (var i = 0; i < outText.Length; i++)
            {
                if (outText[i] == '/') continue;
                if (++seen == keep) { outCaret = i + 1; break; }
            }
        }
        // مکان‌نمای تهِ متن پس از «/»ی تازه
        if (caret == s.Length) outCaret = outText.Length;
        return (outText, outCaret);
    }

    /// <summary>چند رقمِ اولِ ماه یا روز در سقفِ خودش جا می‌شود (دست‌کم یکی).</summary>
    private static int Fit(string seg, int max)
    {
        if (seg.Length <= 1) return seg.Length;
        var two = Value(seg[0]) * 10 + Value(seg[1]);
        return two >= 1 && two <= max ? 2 : 1;
    }

    private static int Value(char c) => c switch
    {
        >= '0' and <= '9' => c - '0',
        >= '۰' and <= '۹' => c - '۰',
        >= '٠' and <= '٩' => c - '٠',
        _ => 0,
    };
}
