using System;
using System.Collections.Generic;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.Svg.Skia;

namespace PumpYaqobi.App.Controls;

/// <summary>
/// ══ عکسِ صفحهٔ ورود — نقشهٔ **برداریِ** آماده، با رنگِ تمِ برنامه ═══════════
///
/// خواستهٔ صاحب ریپو (۱۴۰۵/۰۶/۲۹، پس از رد کردنِ نقشهٔ دست‌سازِ خودمان):
/// «نه، این چیه؛ یک عکس پیدا کن مثلِ همون که بود ولی با کیفیت و جزئیات.» و
/// پیش‌ترش: «با رنگ و تمِ خودِ برنامه باشه.»
///
/// پس یک نقشهٔ **حرفه‌ای و پرجزئیات** از مجموعهٔ unDraw (پروانهٔ MIT،
/// `Assets/ART-LICENCE.md`) گذاشته شد — همان چیدمانِ عکسِ مرجع: دو آدم کنارِ
/// یک فهرستِ تیک‌دارِ بزرگ.
///
/// ⚠️ **و SVG است، نه عکسِ پیکسلی**: در هر اندازه و روی هر صفحه‌ای (۴K هم)
/// تیز است، و جزئیاتش (چین‌های لباس، سایه‌ها، برگ‌ها) همان جزئیاتِ خودِ
/// نقشه است، نه چیزی که ما کشیده باشیم.
///
/// ⚠️ **رنگ‌ها با تم عوض می‌شوند**: پالتِ خودِ فایل (خاکستری‌ها و سرمه‌ایِ
/// unDraw) پیش از ساختنِ تصویر با رنگ‌های تمِ برنامه جا عوض می‌کند
/// (<see cref="Map"/>) — آبی در تمِ روشن، طلایی در تمِ تیره. خودِ فایل
/// دست‌نخورده می‌ماند.
///
/// ⚠️ **هزینه**: متنِ ۱۶ کیلوبایتی یک بار خوانده می‌شود و نتیجهٔ هر تم یک بار
/// ساخته و کَش می‌شود (<see cref="_cache"/>)، پس رفتن و برگشتن به صفحه هیچ
/// کاری نمی‌کند. سنجه: `loginart`.
/// </summary>
public class LoginArt : UserControl
{
    /// <summary>متنِ خامِ فایل — یک بار برای همیشه.</summary>
    private static string? _svg;

    /// <summary>تصویرِ آمادهٔ هر تم (دو تا، نه بیشتر).</summary>
    private static readonly Dictionary<bool, SvgImage> _cache = new();

    private readonly Image _img = new()
    {
        Stretch = Stretch.Uniform,
        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
    };

    public LoginArt() => Content = _img;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        ActualThemeVariantChanged += OnThemeChanged;
        Paint();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        ActualThemeVariantChanged -= OnThemeChanged;
        base.OnDetachedFromVisualTree(e);
    }

    private void OnThemeChanged(object? sender, EventArgs e) => Paint();

    private void Paint()
    {
        try
        {
            _svg ??= ReadAsset();
            var dark = ActualThemeVariant == ThemeVariant.Dark;
            if (!_cache.TryGetValue(dark, out var img))
            {
                var text = _svg;
                foreach (var (from, to) in Palette(dark))
                    text = text.Replace(from, to, StringComparison.OrdinalIgnoreCase);
                img = new SvgImage { Source = SvgSource.LoadFromSvg(text) };
                _cache[dark] = img;
            }
            _img.Source = img;
        }
        catch { /* صفحه بی نقشه هم کامل است */ }
    }

    /// <summary>
    /// ⚠️ جدولِ رنگ — از پالتِ خودِ نقشه به تمِ برنامه.
    ///
    /// رنگِ پوست و موی آدم‌ها دست نمی‌خورد (رنگِ تم نیستند)؛ فقط سطح‌های
    /// خاکستری و سرمه‌ایِ نقشه — همان‌هایی که «کادر» و «تاکید»اند — جا عوض
    /// می‌کنند. در تمِ تیره لباس‌ها روشن می‌شوند، وگرنه آدم‌ها روی بومِ مشکی
    /// گم می‌شدند.
    /// </summary>
    private static (string From, string To)[] Map(bool dark) => dark
        ? new[]
        {
            ("#e6e6e6", "#2a2620"),   // کادرِ بزرگِ پشت
            ("#ccc",    "#3d3venue"), // (جای‌نگه‌دار — پایین درست می‌شود)
        }
        : new[]
        {
            ("#e6e6e6", "#e8eff9"),
            ("#ccc",    "#cfe0f7"),
        };

    private static (string From, string To)[] Palette(bool dark) => dark
        ? new[]
        {
            ("#e6e6e6", "#2b2721"),   // کادرِ بزرگِ پشت
            ("#ccc",    "#5a4f2c"),   // نوارهای فهرست ⇒ طلاییِ مات، تا خوانده شوند
            ("#3f3d56", "#ffd700"),   // تاکید ⇒ طلایی
            ("#2f2e41", "#f3e9c8"),   // لباس و مو ⇒ روشن، تا روی مشکی دیده شوند
            ("#fff",    "#1c1a16"),   // کاغذِ سفید ⇒ کارتِ تیره
        }
        : new[]
        {
            ("#e6e6e6", "#e8eff9"),
            ("#ccc",    "#cfe0f7"),
            ("#3f3d56", "#1e3a8a"),
            ("#2f2e41", "#243b6b"),
        };

    private static string ReadAsset()
    {
        using var s = AssetLoader.Open(new Uri("avares://PumpYaqobi/Assets/login-art.svg"));
        using var r = new StreamReader(s);
        return r.ReadToEnd();
    }
}
