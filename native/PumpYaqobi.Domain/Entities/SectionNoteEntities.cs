namespace PumpYaqobi.Domain.Entities;

/// <summary>
/// ══ یادداشتِ یک بخش ═══════════════════════════════════════════════════════
///
/// همتای ‎_noteList(key)‎ی نسخهٔ وب — «صندوقِ نوت‌ها»ی هر بخش.
///
/// در سایت هر بخش پایینش یک کادرِ ‎.sec-note-box‎ دارد: چیزی می‌نویسی و با
/// «📨 فرستادن به صندوق نوت‌ها» همان‌جا ثبت می‌شود، و «📋 لیست نوت‌ها»
/// همه‌شان را نشان می‌دهد. چهارده بخشِ سایت این را دارند و برنامهٔ نیتیو
/// هیچ‌کدامش را نداشت.
///
/// ⚠️ ‎SectionKey‎ همان کلیدی است که سایت به کار می‌برد (‎chakana‎، ‎safe‎،
/// ‎expenses‎، ‎sarrafi‎، …) تا اگر روزی دادهٔ سایت وارد شد، نوت‌ها سرِ جای
/// خودشان بنشینند.
/// </summary>
public class SectionNote : EntityBase
{
    /// <summary>کلیدِ بخش — همان کلیدِ نسخهٔ وب.</summary>
    public string SectionKey { get; set; } = string.Empty;

    /// <summary>متنِ یادداشت.</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>تاریخِ شمسیِ ثبت — همان ‎at‎ی نسخهٔ وب.</summary>
    public string DateShamsi { get; set; } = string.Empty;
}

/// <summary>
/// پیش‌نویسِ همان کادر — چیزی که هنوز «فرستاده» نشده.
///
/// در سایت ‎_noteDrafts()[key]‎ است و با ‎saveNoteDraft‎ سرِ هر حرف ذخیره
/// می‌شود، تا اگر برنامه بسته شد، نوشتهٔ نیمه‌کاره نپرد.
/// </summary>
public class SectionNoteDraft : EntityBase
{
    public string SectionKey { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
}
