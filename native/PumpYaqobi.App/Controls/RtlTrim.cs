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
            if (GetEnabled(t)) Apply(t);
        });
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
