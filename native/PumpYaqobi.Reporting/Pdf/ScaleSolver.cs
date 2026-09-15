using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace PumpYaqobi.Reporting.Pdf;

/// <summary>
/// ══ «جا دادن» با شمردنِ ورقِ واقعی ═══════════════════════════════════════════
///
/// رونوشتِ ‎pickScale()‎ی سایت. سه حالتِ «جا دادن همهٔ سطرها»، «کلِ گزارش در
/// یک ورق» و «در N×M ورق» با یک ضریبِ حدسی حل نمی‌شوند: سطرها از وسط نصف
/// نمی‌شوند، پس محتوایی که «۱٫۹ ورق» حساب می‌شود روی کاغذ ۲ ورق است. این‌جا
/// سند در چند مقیاس واقعاً چیده می‌شود و ورق‌هایش شمرده می‌شوند — دقیقاً
/// همان ‎biggestScaleFor‎: جست‌وجوی دودویی، هشت گام، میانِ ۸٪ و ۱۰۰٪.
///
/// ⚠️ شمردن با کیفیتِ پایین (۳۶ نقطه بر اینچ) انجام می‌شود: تعدادِ ورق به
/// کیفیتِ تصویر ربطی ندارد و این‌طور هر گام چند صدم ثانیه است، نه چند ثانیه.
/// </summary>
public static class ScaleSolver
{
    private const int Steps = 8;
    private const float Lo = 0.08f;

    /// <summary>
    /// درصدِ مقیاس برای تنظیمِ داده‌شده. حالت‌هایی که «جا دادن» نیستند همان
    /// درصدِ خودشان را برمی‌گردانند تا صداکننده یک راه بیشتر نداشته باشد.
    /// </summary>
    /// <param name="build">سند را با تنظیمِ داده‌شده می‌سازد.</param>
    public static int Solve(PageSetup setup, Func<PageSetup, IDocument> build)
    {
        switch (setup.Scale)
        {
            case PrintScale.Custom: return Math.Clamp(setup.ScalePercent, 10, 400);
            case PrintScale.None:
            case PrintScale.FitColumns: return 100;
        }

        var maxPages = setup.Scale == PrintScale.FitPages
            ? Math.Clamp(setup.FitWidthPages, 1, 20) * Math.Clamp(setup.FitHeightPages, 1, 99)
            : 1;

        int PagesAt(float scale)
        {
            try
            {
                var doc = build(setup.WithResolvedScale((int)Math.Round(scale * 100)));
                return doc.GenerateImages(new ImageGenerationSettings
                {
                    ImageFormat = ImageFormat.Png,
                    RasterDpi = 36,
                }).Count();
            }
            catch { return int.MaxValue; }
        }

        // اگر همین حالا جا می‌شود، دست نزن
        if (PagesAt(1f) <= maxPages) return 100;

        float lo = Lo, hi = 1f, best = Lo;
        for (var i = 0; i < Steps; i++)
        {
            var mid = (lo + hi) / 2f;
            if (PagesAt(mid) <= maxPages) { best = mid; lo = mid; }
            else hi = mid;
        }
        return Math.Clamp((int)Math.Floor(best * 100), 10, 100);
    }
}
