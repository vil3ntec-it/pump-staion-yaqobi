using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.Threading;
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
/// (<see cref="Palette"/>) — آبی در تمِ روشن، طلایی در تمِ تیره. خودِ فایل
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

    /// <summary>
    /// ⚠️ **چند بار تجزیه شد، و اولین بار کِی** — برای سنجه، نه برای برنامه.
    ///
    /// گزارشِ صاحب ریپو (۱۴۰۵/۰۶/۳۰): «برنامه باز خیلی کند شده.» اسکنِ کامل
    /// **نگفت** که باز شدنِ پنجره از این نقشه کند شده (همان عدد روی
    /// `v3.1.113` هم بود)، ولی نشان داد این نقشه سرِ **چسبیدن به درخت** و
    /// روی **نخِ رابط** تجزیه می‌شد — ~۱۱۰ میلی‌ثانیه، برای صفحه‌ای که آن
    /// لحظه پنهان است. عدد را همین‌جا نگه می‌داریم تا سنجه بتواند ثابت کند
    /// نخِ رابط چیزی نپرداخته.
    /// </summary>
    public static int Parses { get; private set; }

    /// <summary>جمعِ وقتی که تجزیهٔ نقشه **روی نخِ رابط** گرفته — باید ~صفر بماند.</summary>
    public static long UiMs { get; private set; }

    /// <summary>جمعِ وقتِ تجزیه روی نخِ دیگر — این‌جا هزینه‌اش دیده نمی‌شود.</summary>
    public static long OffMs { get; private set; }

    /// <summary>تصویر باید دوباره ساخته شود (تم عوض شده یا هنوز نساخته‌ایم).</summary>
    private bool _stale = true;

    public LoginArt() => Content = _img;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        ActualThemeVariantChanged += OnThemeChanged;
        //  ⛔ **این‌جا نقشه ساخته نمی‌شود.** همهٔ بخش‌ها در درخت می‌مانند
        //  (`AllPages`)، پس «چسبیدن به درخت» یعنی «سرِ باز شدنِ برنامه» —
        //  و صفحهٔ ورود آن لحظه پنهان است. قاعدهٔ «بخشی که تویش نیستی هیچ
        //  مصرفی ندارد» این‌جا هم برقرار است.
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        ActualThemeVariantChanged -= OnThemeChanged;
        base.OnDetachedFromVisualTree(e);
    }

    /// <summary>
    /// ⚠️ **تنها جای ساختنِ نقشه، اولین اندازه‌گیریِ واقعی است.**
    ///
    /// کنترلِ پنهان (`IsVisible=false`ی بخشِ بسته) اصلاً اندازه گرفته نمی‌شود،
    /// پس این تابع تا وقتی صفحهٔ ورود جلوی چشم نیاید یک بار هم صدا نمی‌خورد —
    /// بی هیچ شنوندهٔ چیدمانی و بی `IsEffectivelyVisible` که در آوالونیا ۱۱
    /// خبر نمی‌دهد.
    /// </summary>
    protected override Size MeasureOverride(Size availableSize)
    {
        if (_stale) Paint();
        return base.MeasureOverride(availableSize);
    }

    /// <summary>
    /// تمِ تازه: فقط نشان می‌گذاریم. اگر صفحه پنهان باشد هیچ کاری نمی‌شود و
    /// اولین اندازه‌گیریِ بعدی خودش می‌سازد — پس تعویضِ تم هزینهٔ صفحهٔ
    /// نادیده را ندارد.
    /// </summary>
    private void OnThemeChanged(object? sender, EventArgs e)
    {
        _stale = true;
        InvalidateMeasure();
    }

    /// <summary>هم‌زمان دو بار ساخته نشود.</summary>
    private static readonly HashSet<bool> _building = new();

    /// <summary>
    /// ⚠️ **تجزیهٔ نقشه روی نخِ رابط انجام نمی‌شود.**
    ///
    /// خواندنِ ۱۶ کیلوبایت متن و ساختنِ تصویرِ برداری‌اش ~۱۱۶ میلی‌ثانیه است
    /// (سنجیده شد، حدس نیست). روی نخِ رابط، همان ۱۱۶ میلی‌ثانیه یک فریمِ
    /// کاملاً گم‌شده است — چه وقتِ گرم کردنِ صفحه‌ها (`WarmUp` هر بخش را یک
    /// بار می‌چیند) و چه سرِ باز کردنِ صفحه. پس تجزیه روی نخِ دیگر می‌رود و
    /// فقط **نشاندنِ** تصویرِ آماده روی نخِ رابط است.
    ///
    /// ⚠️ و نتیجه برای هر تم یک بار ساخته و کَش می‌شود، پس این هزینه در کلِ
    /// عمرِ برنامه دو بار است، نه با هر باز شدنِ صفحه.
    /// </summary>
    private void Paint()
    {
        _stale = false;
        try
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var dark = ActualThemeVariant == ThemeVariant.Dark;
            if (_cache.TryGetValue(dark, out var ready)) { _img.Source = ready; UiMs += sw.ElapsedMilliseconds; return; }
            if (!_building.Add(dark)) return;   // یکی دیگر همین حالا دارد می‌سازدش
            UiMs += sw.ElapsedMilliseconds;

            _ = Task.Run(() =>
            {
                var off = System.Diagnostics.Stopwatch.StartNew();
                SvgSource? parsed = null;
                try
                {
                    _svg ??= ReadAsset();
                    var text = _svg;
                    foreach (var (from, to) in Palette(dark))
                        text = text.Replace(from, to, StringComparison.OrdinalIgnoreCase);
                    parsed = SvgSource.LoadFromSvg(text);
                }
                catch { /* صفحه بی نقشه هم کامل است */ }
                var ms = off.ElapsedMilliseconds;

                Dispatcher.UIThread.Post(() =>
                {
                    _building.Remove(dark);
                    OffMs += ms;
                    if (parsed is null) return;
                    if (!_cache.TryGetValue(dark, out var img))
                    {
                        img = new SvgImage { Source = parsed };
                        _cache[dark] = img;
                        Parses++;
                    }
                    //  ⚠️ تمِ برنامه ممکن است در همین فاصله عوض شده باشد؛
                    //  فقط تصویرِ تمِ **همین حالا** نشانده می‌شود.
                    if ((ActualThemeVariant == ThemeVariant.Dark) == dark) _img.Source = img;
                }, DispatcherPriority.Background);
            });
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
