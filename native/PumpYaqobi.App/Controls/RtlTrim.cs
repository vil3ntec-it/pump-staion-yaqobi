using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;

namespace PumpYaqobi.App.Controls;

/// <summary>
/// ══ نوشتهٔ فارسیِ بلند در خانهٔ جدول فقط «…» می‌شد ══════════════════════════
///
/// سنجیده شد، نه حدس (۱۴۰۵/۰۷/۱۲): در آوالونیا ۱۱.۲.۳ هر نوشتهٔ راست‌به‌چپی
/// که در کادرش جا نشود با ‎TextTrimming‎ (هر حالتش: ‎CharacterEllipsis‎ ·
/// ‎WordEllipsis‎ · ‎Prefix…‎ · ‎Leading…‎) **کلاً** «…» می‌شود — یادداشتِ ورق
/// در ستونِ ۳۰۰ پیکسلی هیچ واژه‌ای نشان نمی‌داد. و بی ‎Trimming‎ با
/// ‎NoWrap‎، **آغازِ** جمله از سمتِ راست بیرون می‌افتاد.
///
/// تنها ترکیبی که آغازِ جمله را نشان می‌دهد و از کادر بیرون نمی‌زند:
/// ‎TextWrapping=Wrap‎ + ‎MaxLines=1‎ + ‎TextTrimming=None‎ — یعنی خطِ اول،
/// تا آخرین واژه‌ای که جا می‌شود.
///
/// ⚠️ فقط برای نوشتهٔ **راست‌به‌چپ**. عدد و نوشتهٔ لاتین با همان «…»ِ همیشه
/// می‌مانند: عددی که بی‌صدا نصفه بریده شود از «…,930» بدتر است.
/// ⚠️ ‎Text‎ دست نمی‌خورد — فقط سه خاصیتِ ظاهری، پس اتصالِ ستون سالم می‌ماند.
/// </summary>
public static class RtlTrim
{
    public static readonly AttachedProperty<bool> EnabledProperty =
        AvaloniaProperty.RegisterAttached<TextBlock, bool>("Enabled", typeof(RtlTrim));

    /// <summary>
    /// فقط وسط‌چینیِ درست (بی تغییرِ شکستن و بریدن) — برای سرستون‌ها.
    /// </summary>
    public static readonly AttachedProperty<bool> CenterProperty =
        AvaloniaProperty.RegisterAttached<TextBlock, bool>("Center", typeof(RtlTrim));

    public static bool GetCenter(TextBlock t) => t.GetValue(CenterProperty);
    public static void SetCenter(TextBlock t, bool v) => t.SetValue(CenterProperty, v);

    public static bool GetEnabled(TextBlock t) => t.GetValue(EnabledProperty);
    public static void SetEnabled(TextBlock t, bool v) => t.SetValue(EnabledProperty, v);

    /// <summary>
    /// تصحیحی که همین کلاس گذاشته — تا ‎RenderTransform‎ی که کسِ دیگری گذاشته
    /// هرگز برداشته نشود.
    /// </summary>
    private static readonly AttachedProperty<bool> OwnedProperty =
        AvaloniaProperty.RegisterAttached<TextBlock, bool>("Owned", typeof(RtlTrim));

    /// <summary>
    /// ⛔ هر نوشتهٔ وسط‌چینِ برنامه، نه فقط جدول (۱۴۰۵/۰۷/۱۸، دوم). گزارشِ صاحب
    /// ریپو: «همه نوشته‌ها و همه حساب‌ها و سربرگ‌ها تو همه بخش‌ها… چپ یا راست
    /// رفتن.» سنجهٔ ‎oldpost‎ با پیکسلِ واقعی نشان داد: نامِ کارتِ قرض‌دار
    /// («مشتریِ 2») و شرکت، عنوانِ هر کارتِ جمع که با ایموجی شروع می‌شود
    /// («⛽ جمله پطرول»، «🏛️ جمله ماندگی»)، سربرگِ خلاصهٔ ورق — ۹ تا ۲۰ پیکسل
    /// کج، همان باگِ آوالونیا (تکهٔ عدد/ایموجی در سرِ چپِ خطِ راست‌به‌چپ «فاصلهٔ
    /// پایانی» شمرده می‌شود). تصحیح فقط روی خانه‌های جدول بود.
    /// </summary>
    private static bool Wants(TextBlock t) =>
        GetEnabled(t) || GetCenter(t) || t.TextAlignment == TextAlignment.Center || t.GetValue(OwnedProperty);

    static RtlTrim()
    {
        EnabledProperty.Changed.AddClassHandler<TextBlock>((t, e) =>
        {
            if (e.NewValue is true) Apply(t);
        });
        TextBlock.TextProperty.Changed.AddClassHandler<TextBlock>((t, _) =>
        {
            if (!Wants(t)) return;
            if (GetEnabled(t)) Apply(t);
            //  جای خطِ تازه پس از چیدمانِ همین فریم معلوم است
            Avalonia.Threading.Dispatcher.UIThread.Post(() => Recenter(t),
                Avalonia.Threading.DispatcherPriority.Loaded);
        });
        TextBlock.TextAlignmentProperty.Changed.AddClassHandler<TextBlock>((t, _) =>
        {
            if (Wants(t))
                Avalonia.Threading.Dispatcher.UIThread.Post(() => Recenter(t),
                    Avalonia.Threading.DispatcherPriority.Loaded);
        });
        //  ⚡ پس از هر چیدمان (نه ‎LayoutUpdated‎ی سراسری): فقط همین نوشته
        Visual.BoundsProperty.Changed.AddClassHandler<TextBlock>((t, _) =>
        {
            if (Wants(t)) Recenter(t);
        });
    }

    // ══ «حوالهٔ 1» از وسط کنار می‌افتاد (۱۴۰۵/۰۷/۱۸) ═══════════════════════════
    //
    // گزارشِ صاحب ریپو: «نوشته‌ها میان راست یا چپ… همه باید وسط باشن، و به محضِ
    // این‌که این اتفاق افتاد درجا درست بشه.» سنجهٔ ‎oldmonths‎ با پیکسل‌های
    // واقعیِ پنجره گرفتش: در آوالونیا ۱۱.۲.۳، خطِ راست‌به‌چپی که با عدد یا واژهٔ
    // لاتین تمام می‌شود («حوالهٔ 1»، «چکنهٔ 12»)، آن تکهٔ آخر را «فاصلهٔ پایانی»
    // می‌شمارد (‎Width‎ کوچک‌تر از ‎WidthIncludingTrailingWhitespace‎ می‌شود) و
    // وسط‌چینی را با پهنای کوچک‌تر حساب می‌کند — نوشته نیمِ همان تکه (~۶px) به
    // یک سو می‌افتد، با ‎Wrap‎ یا بی آن.
    //
    // ⛔ چاره ‎RenderTransform‎ است، نه ‎Padding‎ یا ‎Margin‎: جابه‌جاییِ کشیدن هیچ
    // پاسِ چیدمانی نمی‌سازد، پس نه چرخهٔ چیدمان دارد نه هزینه.
    //
    // ⛔ <b>از ۱۴۰۵/۰۷/۱۹ حدس نیست، اندازه است</b>: «فاصلهٔ پایانیِ واقعی را
    // آوالونیا درست کنار می‌گذارد» غلط بود — دادهٔ قدیمی («کریم »، تب، نویسهٔ
    // جهت‌نما) ۲٫۵ تا ۱۰ پیکسل کج می‌ماند و صاحب ریپو باید آن ردیف‌ها را پاک
    // می‌کرد. حالا جای جوهرِ نخستین تا آخرین نویسهٔ دیدنی از خودِ ‎TextLayout‎
    // (‎HitTestTextRange‎) خوانده می‌شود و همان وسط می‌رود — هر علتی که داشته
    // باشد. ⛔ متنِ کاربر دست نمی‌خورد. سنجه: ‎centerlab‎.

    /// <summary>
    /// چند واحد نوشته باید جابه‌جا شود تا جوهرش وسطِ خودش باشد (مثبت ⇒ به راست).
    /// ۰ یعنی نوشته از قبل وسط است.
    /// </summary>
    public static double CenterFix(TextBlock t)
    {
        if (t.TextAlignment != TextAlignment.Center || string.IsNullOrEmpty(t.Text)) return 0;
        if (!t.IsMeasureValid || !t.IsArrangeValid) return 0;      // ⛔ چیدمانِ کهنه را نخوان
        var text = t.Text;
        //  ══ جوهر، نه نویسه (۱۴۰۵/۰۷/۱۹) ══
        //  فاصله، تب، نویسهٔ جهت‌نما و نیم‌فاصلهٔ دو سرِ نوشته هیچ جوهری ندارند ولی
        //  آوالونیا با آن‌ها وسط می‌برد — «کریم »ِ دادهٔ قدیمی ۲٫۵ تا ۱۰ پیکسل کج بود
        //  (‎centerlab‎). پس وسطِ <b>نخستین تا آخرین نویسهٔ دیدنی</b> سنجیده می‌شود.
        var tl = t.TextLayout;
        if (tl is null || tl.TextLines.Count == 0) return 0;
        //  ⚠️ نوشتهٔ بلندِ ‎Wrap+MaxLines=1‎ فقط خطِ اولش دیده می‌شود — همان تکهٔ دیدنی
        //  وسط می‌رود («500 » از «500 افغانی» در ستونِ باریک ۳px کج بود)
        var line = tl.TextLines[0];
        int a = line.FirstTextSourceIndex, b = Math.Min(text.Length, a + line.Length) - 1;
        var whole = tl.TextLines.Count == 1 && a == 0 && b == text.Length - 1;
        while (a <= b && Blank(text[a])) a++;
        while (b >= a && Blank(text[b])) b--;
        if (a > b) return 0;
        //  ⚡ نوشتهٔ لاتین/عددیِ کامل و بی فاصلهٔ دو سر را آوالونیا درست وسط می‌برد
        if (whole && a == 0 && b == text.Length - 1 && !IsRtl(text)) return 0;
        //  ⚠️ نویسه‌به‌نویسه، نه یک‌جا: ‎HitTestTextRange‎ِ آوالونیا ۱۱.۲.۳ روی خطِ
        //  راست‌به‌چپی که چند تکه دارد (نیم‌فاصله، ایموجی، «/») فقط <b>تکهٔ اول</b> را
        //  می‌دهد — «ورق‌های روزانه» ۳۵ پیکسل به کنار می‌رفت (‎oldpost‎ گرفتش).
        double lo = double.MaxValue, hi = double.MinValue;
        //  ══ جوهرِ واقعیِ گلیف‌ها (۱۴۰۵/۰۷/۱۹، ویندوز) ══
        //  پهنای نویسه با جوهرش یکی نیست: ایموجیِ «🟤» روی ویندوز از قلمِ دیگری
        //  کشیده می‌شود و جوهرش از جای خالیِ خودش کوچک‌تر است (یا اصلاً کشیده
        //  نمی‌شود) — «🟤 جمله دیزل» ۱۳ پیکسل کج بود (‎align-windows‎). پس اول
        //  ‎InkBounds‎ِ خودِ هر تکهٔ شکل‌گرفته خوانده می‌شود؛ نشد ⇒ همان نویسه‌به‌نویسه.
        if (InkOf(tl, line, text) is { } ink) { lo = ink.lo; hi = ink.hi; }
        else
        for (var i = a; i <= b; i++)
        {
            if (Blank(text[i])) continue;
            var n = char.IsHighSurrogate(text[i]) && i < b ? 2 : 1;
            foreach (var r in tl.HitTestTextRange(i, n))
            {
                if (r.Width <= 0) continue;
                lo = Math.Min(lo, r.Left);
                hi = Math.Max(hi, r.Right);
            }
            i += n - 1;
        }
        if (hi <= lo) return 0;
        var inner = t.Bounds.Width - t.Padding.Left - t.Padding.Right;
        if (inner <= 0 || hi - lo >= inner - 1) return 0;           // پر از کادر
        //  مثبت ⇒ جوهر باید به راست برود
        var dx = inner / 2 - (lo + hi) / 2;
        return Math.Abs(dx) < 0.5 ? 0 : dx;
    }

    /// <summary>
    /// جوهرِ واقعیِ خطِ اول از روی ‎GlyphRun.InkBounds‎ِ هر تکه؛ جای هر تکه در خط
    /// از ‎HitTest‎ِ همان بازه. ‎null‎ ⇒ نشد، راهِ نویسه‌به‌نویسه.
    /// </summary>
    private static (double lo, double hi)? InkOf(TextLayout tl, TextLine line, string text)
    {
        try
        {
            double lo = double.MaxValue, hi = double.MinValue;
            var any = false;
            foreach (var run in line.TextRuns)
            {
                if (run is not ShapedTextRun sr) continue;
                if (!System.Runtime.InteropServices.MemoryMarshal.TryGetString(sr.Text, out var src, out var start, out var len)
                    || !ReferenceEquals(src, text) && src != text) return null;
                if (len <= 0) continue;
                var ib = sr.GlyphRun.InkBounds;
                if (ib.Width <= 0) continue;                       // فقط فاصله
                //  جای تکه در خط: کناره‌های همهٔ نویسه‌هایش (یک‌جا گاهی فقط تکهٔ اول را می‌دهد)
                double rl = double.MaxValue, rr = double.MinValue;
                for (var i = start; i < start + len; i++)
                    foreach (var r in tl.HitTestTextRange(i, 1))
                    {
                        if (r.Width <= 0) continue;
                        rl = Math.Min(rl, r.Left); rr = Math.Max(rr, r.Right);
                    }
                if (rr <= rl) return null;
                //  پهنای تکه با جمعِ پیش‌روی گلیف‌ها نخواند ⇒ نمی‌دانیم کجاست
                if (Math.Abs(rr - rl - sr.GlyphRun.Bounds.Width) > 1) return null;
                lo = Math.Min(lo, rl + ib.Left);
                hi = Math.Max(hi, rl + ib.Right);
                any = true;
            }
            return any && hi > lo ? (lo, hi) : null;
        }
        catch { return null; }
    }

    /// <summary>نویسه‌ای که هیچ جوهری ندارد.</summary>
    private static bool Blank(char c) =>
        char.IsWhiteSpace(c) || c is '\u200b' or '\u200c' or '\u200d' or '\u200e' or '\u200f'
            or '\u061c' or '\ufeff' or (>= '\u202a' and <= '\u202e') or (>= '\u2066' and <= '\u2069');

    private static void Recenter(TextBlock t)
    {
        var dx = CenterFix(t);
        var owned = t.GetValue(OwnedProperty);
        //  ⛔ جابه‌جاییِ کسِ دیگر دست نمی‌خورد
        if (!owned && t.RenderTransform is not null) return;
        var cur = t.RenderTransform as TranslateTransform;
        if (dx == 0)
        {
            if (owned)
            {
                t.RenderTransform = null;
                t.SetValue(OwnedProperty, false);
            }
            return;
        }
        //  ⚠️ در پنجرهٔ راست‌به‌چپ مختصاتِ محلی آینه است؛ «راست» منفی می‌شود
        var x = t.FlowDirection == FlowDirection.RightToLeft ? -dx : dx;
        if (cur is not null && Math.Abs(cur.X - x) < 0.25) return;
        t.SetValue(OwnedProperty, true);
        t.RenderTransform = new TranslateTransform(x, 0);
    }

    /// <summary>نوشته حرفِ راست‌به‌چپ (فارسی/عربی) دارد؟</summary>
    public static bool IsRtl(string? s)
    {
        if (string.IsNullOrEmpty(s)) return false;
        foreach (var ch in s)
            if (ch is >= '֐' and <= 'ࣿ' or >= 'יִ' and <= '﷿' or >= 'ﹰ' and <= '﻿')
                return true;
        return false;
    }

    private static void Apply(TextBlock t)
    {
        if (IsRtl(t.Text))
        {
            t.TextWrapping = TextWrapping.Wrap;
            t.TextTrimming = TextTrimming.None;
            t.MaxLines = 1;
        }
        else
        {
            t.TextWrapping = TextWrapping.NoWrap;
            t.TextTrimming = TextTrimming.CharacterEllipsis;
            t.MaxLines = 1;
        }
    }
}
