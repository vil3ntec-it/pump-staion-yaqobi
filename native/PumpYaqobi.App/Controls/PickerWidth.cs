using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using PumpYaqobi.App.ViewModels;
using PumpYaqobi.Application.Localization;

namespace PumpYaqobi.App.Controls;

/// <summary>
/// ══ کشوی سال و ماه با عوض شدنِ ماه پهنایش عوض نمی‌شود (۱۴۰۵/۰۷/۲۱) ══════════
///
/// پرسشِ صاحب ریپو: «هیچ بخش یا ماه و سالِ قدیم و جدید… در جدول و سربرگ و هر جا
/// که نوشته باشد به چپ یا راست نمی‌رود؟ حتی مفاد و ضرر.» سنجهٔ ‎monthshift all‎
/// نشان داد: کشوی ماه ‎MinWidth‎ داشت، نه پهنای ثابت؛ پس با هر ماه هم‌اندازهٔ
/// <b>نامِ همان ماه</b> می‌شد — «حوت — 1404/12» پهن‌تر از «حمل — 1405/01»، و
/// «همهٔ ماه‌های 1405» پهن‌تر از هر دو. چون کشو در نوارِ راست‌به‌چپ است، هر چیزی که
/// پس از آن نشسته (توضیحِ بخش، «📑 گزارش»، «PDF»، کادرِ «نامِ کارمندِ تازه») با
/// هر انتخاب ۳ تا ۸ پیکسل — و در مفاد/ضرر با «📆 همهٔ سال‌ها» ۳۲ پیکسل — می‌پرید.
///
/// حالا ‎MinWidth‎ از <b>پهن‌ترین برچسبِ ممکن</b> درمی‌آید: هر برچسبِ فهرست، با
/// نامِ هر دوازده ماه به‌جای نامِ ماهِ خودش و رقم‌ها صفر، با همان قلم و اندازهٔ
/// خودِ کشو (پس بزرگ‌نماییِ قلم هم را می‌گیرد) + قاب و جای نقطهٔ سرخ. فهرستی که
/// با سالِ دیگر عوض می‌شود پهنای تازه‌ای نمی‌سازد.
///
/// ⛔ فقط ‎MinWidth‎ بزرگ می‌شود، هرگز کوچک‌تر از آن‌چه در ‎axaml‎ نوشته شده.
/// ⛔ هیچ شنوندهٔ ‎LayoutUpdated‎ی ندارد — فقط تغییرِ فهرست و قلم.
/// </summary>
public static class PickerWidth
{
    public static readonly AttachedProperty<bool> StableProperty =
        AvaloniaProperty.RegisterAttached<ComboBox, bool>("Stable", typeof(PickerWidth));

    public static bool GetStable(ComboBox c) => c.GetValue(StableProperty);
    public static void SetStable(ComboBox c, bool v) => c.SetValue(StableProperty, v);

    /// <summary>قابِ کشوی فلوئنت: ‎Padding‎ ۱۲ + پیکانِ ۳۲ + لبه‌ها + اندکی جا.</summary>
    private const double Chrome = 56;
    /// <summary>‎Spacing="7"‎ + نقطهٔ سرخِ ده‌پیکسلی (‎Pump.MonthDotItem‎).</summary>
    private const double Dot = 17;

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<ComboBox, StrongBox> Floor = new();
    private sealed class StrongBox { public double V; }

    static PickerWidth()
    {
        StableProperty.Changed.AddClassHandler<ComboBox>((cb, e) =>
        {
            if (e.NewValue is not true) return;
            Floor.AddOrUpdate(cb, new StrongBox { V = double.IsNaN(cb.MinWidth) ? 0 : cb.MinWidth });
            cb.Items.CollectionChanged += (_, _) => Fit(cb);
            cb.PropertyChanged += (_, pe) =>
            {
                if (pe.Property == TemplatedControl.FontSizeProperty || pe.Property == TemplatedControl.FontFamilyProperty
                    || pe.Property == TemplatedControl.FontWeightProperty)
                    Fit(cb);
            };
            Fit(cb);
        });
    }

    /// <summary>
    /// برچسب‌های ممکن: هر برچسب با نامِ هر ماه، و هر رقم پهن‌ترین رقمِ همان قلم.
    /// ⚠️ رقم‌های ‎Vazirmatn‎ هم‌پهنا نیستند — «1404» دو پیکسل از «1400» پهن‌تر است و
    /// همان دو پیکسل با رفتن به سالِ پیش کشو را می‌پراند (سنجه گرفتش).
    /// </summary>
    public static IEnumerable<string> Candidates(IEnumerable<string> labels, char wideDigit = '0')
    {
        var names = Enumerable.Range(1, 12).Select(Shamsi.MonthName).Where(n => n.Length > 0).ToList();
        foreach (var l in labels)
        {
            var z = new string(l.Select(ch => char.IsDigit(ch) ? wideDigit : ch).ToArray());
            yield return z;
            foreach (var n in names.Where(n => z.Contains(n, StringComparison.Ordinal)))
                foreach (var other in names)
                    yield return z.Replace(n, other, StringComparison.Ordinal);
        }
    }

    internal static void Fit(ComboBox cb)
    {
        var labels = cb.Items.OfType<YearMonthItem>().Select(i => i.Label).ToList();
        if (labels.Count == 0) return;
        var face = new Typeface(cb.FontFamily, cb.FontStyle, cb.FontWeight);
        var wide = '0';
        double wd = 0;
        foreach (var d in "0123456789")
        {
            using var dl = new TextLayout(d.ToString(), face, cb.FontSize, null);
            if (dl.WidthIncludingTrailingWhitespace > wd) { wd = dl.WidthIncludingTrailingWhitespace; wide = d; }
        }
        double max = 0;
        foreach (var t in Candidates(labels, wide).Distinct())
        {
            using var tl = new TextLayout(t, face, cb.FontSize, null);
            max = Math.Max(max, tl.WidthIncludingTrailingWhitespace);
        }
        //  ⚠️ قاب ثابت است، نه از ‎Padding‎/‎BorderThickness‎ی همین لحظه: آن دو با سبکِ
        //  ‎:pointerover‎/‎:focus‎ و پیش از رسیدنِ سبک عوض می‌شوند و همان «۲ پیکسلِ
        //  پرنده» را برمی‌گرداندند (۱۷۴ ⇒ ۱۸۶ ⇒ ۱۸۸ — سنجه گرفتش).
        var want = Math.Ceiling(max + Dot + Chrome);
        var floor = Floor.TryGetValue(cb, out var f) ? f.V : 0;
        var w = Math.Max(floor, want);
        if (Math.Abs(cb.MinWidth - w) > 0.5) cb.MinWidth = w;
    }
}
