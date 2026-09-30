using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

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

    static RtlTrim()
    {
        EnabledProperty.Changed.AddClassHandler<TextBlock>((t, e) =>
        {
            if (e.NewValue is true) Apply(t);
        });
        TextBlock.TextProperty.Changed.AddClassHandler<TextBlock>((t, _) =>
        {
            if (!GetEnabled(t) && !GetCenter(t)) return;
            if (GetEnabled(t)) Apply(t);
            //  جای خطِ تازه پس از چیدمانِ همین فریم معلوم است
            Avalonia.Threading.Dispatcher.UIThread.Post(() => Recenter(t),
                Avalonia.Threading.DispatcherPriority.Loaded);
        });
        //  ⚡ پس از هر چیدمان (نه ‎LayoutUpdated‎ی سراسری): فقط همین نوشته
        Visual.BoundsProperty.Changed.AddClassHandler<TextBlock>((t, _) =>
        {
            if (GetEnabled(t) || GetCenter(t)) Recenter(t);
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
    // پاسِ چیدمانی نمی‌سازد، پس نه چرخهٔ چیدمان دارد نه هزینه. و فقط وقتی که
    // نوشته <b>واقعاً</b> با فاصله تمام نمی‌شود — فاصلهٔ پایانیِ واقعی را آوالونیا
    // درست کنار می‌گذارد.

    /// <summary>
    /// نیمِ همان پهنای گم‌شده — با جهتی که نوشته را به وسط برمی‌گرداند.
    /// ۰ یعنی نوشته از قبل وسط است.
    /// </summary>
    public static double CenterFix(TextBlock t)
    {
        if (t.TextAlignment != TextAlignment.Center || string.IsNullOrEmpty(t.Text)) return 0;
        if (char.IsWhiteSpace(t.Text[^1])) return 0;               // فاصلهٔ پایانیِ واقعی
        if (!t.IsMeasureValid || !t.IsArrangeValid) return 0;      // ⛔ چیدمانِ کهنه را نخوان
        var tl = t.TextLayout;
        if (tl is null || tl.TextLines.Count != 1) return 0;
        var line = tl.TextLines[0];
        //  خطِ بریده (نوشتهٔ بلندِ ‎Wrap‎) فاصلهٔ پایانیِ واقعی دارد
        if (line.FirstTextSourceIndex + line.Length < t.Text.Length) return 0;
        var gap = line.WidthIncludingTrailingWhitespace - line.Width;
        if (gap < 0.5) return 0;
        //  خطِ راست‌به‌چپ آن تکه را سمتِ چپ می‌کشد ⇒ نوشته به چپ افتاده
        return gap / 2;
    }

    private static void Recenter(TextBlock t)
    {
        var dx = CenterFix(t);
        var cur = t.RenderTransform as TranslateTransform;
        if (dx == 0)
        {
            if (cur is { X: not 0 }) t.RenderTransform = null;
            return;
        }
        //  ⚠️ در پنجرهٔ راست‌به‌چپ مختصاتِ محلی آینه است؛ «راست» منفی می‌شود
        var x = t.FlowDirection == FlowDirection.RightToLeft ? -dx : dx;
        if (cur is not null && Math.Abs(cur.X - x) < 0.25) return;
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
