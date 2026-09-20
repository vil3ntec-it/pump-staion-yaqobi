using PumpYaqobi.App.Services;

namespace PumpYaqobi.App.ViewModels.Sections;

/// <summary>
/// ══ تنظیمات — سه در، و بس ═══════════════════════════════════════════════════
///
/// خواستهٔ صریحِ صاحب ریپو (۱۴۰۵/۰۶/۲۸):
///
///   «بخشِ تنظیمات این مدل باشه، سه بخش داشته باشه؛ هر کدوم که بزنی صفحهٔ
///    جداگانه باز بشه: ۱) رمزها و کد ۲) بک‌اپ و به‌روزرسانی‌ها ۳) سطلِ زباله…
///    و تنظیماتِ الان همه‌شونو حذف کن و این تنظیمات توش باشه و اون تنظیماتِ
///    قدیم هیچ از اون‌ها نباشه داخلش.»
///
/// پس این صفحه فقط سه کارت است و هیچ کادر و تنظیمِ دیگری ندارد.
///
/// ⛔ **این‌ها از تنظیمات برداشته شدند و نباید برگردند** (هر کدام جای تازه‌اش
/// نوشته شده):
///   • نرخِ اتحادیه ⇒ «📈 تاریخچهٔ نرخ اتحادیه» (زیربخشِ مفاد/ضرر)
///   • آستانهٔ هشدارِ مخزن ⇒ خودِ بخشِ «مخزن»
///   • تم ⇒ کادرِ تمِ سربرگ (از قبل آن‌جا بود)
///   • کیو‌آرِ اپِ کارمندان و کدِ پمپ ⇒ «پروفایل»
///   • آوردنِ دادهٔ نسخهٔ وب ⇒ صفحهٔ «بک‌اپ و به‌روزرسانی‌ها»
///   • ظاهرِ جدول‌ها (رنگ/ضخامتِ خط) ⇒ برداشته شد؛ مقدارهای ذخیره‌شده
///     همچنان اعمال می‌شوند (<see cref="PumpYaqobi.App.Themes.TableStyle"/>).
/// </summary>
public sealed class SettingsSectionViewModel : SectionViewModel
{
    public SettingsSectionViewModel(AppHost host) : base("settings", "settings", "تنظیمات")
    {
    }

    /// <summary>
    /// ردیفِ خودکارِ کارت‌های زیربخش خاموش است: خودِ این صفحه سه کارتِ بزرگ
    /// دارد و همان‌ها درِ ورودند. دو جا بودنِ یک لینک فقط شلوغی است.
    /// </summary>
    protected override bool ShowSubLinks => false;

    public SectionViewModel? KeysPage => SubSections.FirstOrDefault(s => s.Id == "keys");
    public SectionViewModel? BackupsPage => SubSections.FirstOrDefault(s => s.Id == "backups");
    public SectionViewModel? TrashPage => SubSections.FirstOrDefault(s => s.Id == "trash");

    /// <summary>
    /// درِ چهارم — «اپِ گوشی: لینک و کد». خواستهٔ صریحِ صاحب ریپو
    /// (۱۴۰۵/۰۶/۲۸): لینکِ اندروید و آیفون و کدِ پمپ، یک‌جا و کپی‌شدنی.
    /// </summary>
    public SectionViewModel? AppsPage => SubSections.FirstOrDefault(s => s.Id == "apps");

    /// <summary>
    /// درِ پنجم — «همگام‌سازی». بندِ ۱۱ی پرامپتِ ۲۲: صف، آخرین موفق،
    /// خطای آخر و «الان همگام کن».
    /// ⛔ کادرِ نشانیِ سرور آن‌جا نیست و نباید بیاید.
    /// </summary>
    public SectionViewModel? SyncPage => SubSections.FirstOrDefault(s => s.Id == "sync");
}
