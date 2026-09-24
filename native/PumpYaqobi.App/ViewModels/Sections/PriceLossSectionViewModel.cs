using PumpYaqobi.App.Services;

namespace PumpYaqobi.App.ViewModels.Sections;

/// <summary>
/// ══ 📉 زیان ناشی از افزایش قیمت — کادرِ خالی، منتظرِ فرمولِ تازه ═════════════
///
/// خواستهٔ صریحِ صاحب ریپو (۱۴۰۵/۰۷/۱۲): «ناشی از افزایش قیمت — این را حذف کن
/// و دیگه نباشه؛ <b>کادرش باشه</b>، محتوای داخلش همه حذف، و یک فرمولِ بهتر
/// می‌دهم که درست کنی.»
///
/// پس این زیربخش <b>هست</b> و باید بماند — همان شناسه (‎priceloss‎) زیرِ
/// «قرض‌داران» (‎SubSectionTests‎)، همان کارت، همان رمزِ بخش
/// (‎SectionLockService.PriceLoss‎) و همان دروازهٔ پلن — ولی هیچ محاسبه‌ای
/// ندارد و هیچ چیزی از دیتابیس نمی‌خواند.
///
/// ⛔ فرمولِ قبلی (‎PriceLossService‎، رونوشتِ ‎_plReportAll‎ی سایت) با خودش
/// رفت؛ در تاریخچهٔ گیت هست. فرمولِ تازه را صاحب ریپو می‌دهد — از خودت
/// نساز و آن قدیمی را برنگردان.
/// </summary>
public sealed partial class PriceLossSectionViewModel : SectionViewModel
{
    public PriceLossSectionViewModel(AppHost host, Func<long, Task>? openPerson = null)
        : base("priceloss", "debt", "زیان ناشی از افزایش قیمت") { }

    /// <summary>هیچ چیزی نمی‌خواند — پس فعال‌سازیِ دوباره هم هیچ کاری ندارد.</summary>
    public override bool ActivationOnlyReadsDb => true;

    protected override Task LoadAsync() => Task.CompletedTask;
}
