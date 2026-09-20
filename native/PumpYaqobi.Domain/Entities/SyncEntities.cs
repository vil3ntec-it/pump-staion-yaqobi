namespace PumpYaqobi.Domain.Entities;

/// <summary>
/// ══ یک ردیفِ دفترِ تغییرات (Oplog) ═══════════════════════════════════════
///
/// بندِ ۲۰٫۱ پرامپت. هر تغییرِ کاربر <b>اول</b> در دیتابیسِ محلی می‌نشیند و
/// بعد یک ردیفِ این‌جا — در <b>همان</b> تراکنش، پس هیچ‌وقت داده بی op و هیچ
/// op بی داده نمی‌ماند.
///
/// ⚠️ این خودش <see cref="EntityBase"/> نیست و عمدی است: اگر بود، هر
/// نوشتنِ op خودش یک op می‌ساخت (حلقهٔ بی‌پایان)، حذفِ نرم می‌گرفت و
/// <c>SyncUid</c> می‌خواست. دفترِ تغییرات دادهٔ کاربر نیست، دفترِ کار است.
/// </summary>
public class SyncOp
{
    public long Id { get; set; }

    /// <summary>ULID — یکتا و مرتب. سرور با همین «تکراری» را می‌شناسد.</summary>
    public string OpId { get; set; } = string.Empty;

    /// <summary>
    /// نامِ <b>موجودیت</b> (مثلِ <c>DebtRow</c>)، نه نامِ جدولِ SQLite.
    /// فهرستِ سفیدِ سرور (<c>PUMP_TABLES</c>) با همین نام‌ها نوشته شده.
    /// </summary>
    public string TableName { get; set; } = string.Empty;

    /// <summary>‎SyncUid‎ی همان ردیف — شناسه‌ای که در هر دو دستگاه یکی است.</summary>
    public string RowUid { get; set; } = string.Empty;

    /// <summary><c>insert</c> · <c>update</c> · <c>delete</c></summary>
    public string OpType { get; set; } = "update";

    /// <summary>
    /// فقط فیلدهای <b>عوض‌شده</b>، به شکلِ JSON. حذف هیچ فیلدی ندارد
    /// (<c>{}</c>) و همین است که «پاک کردنِ یک اسم» را زیرِ یک کیلوبایت
    /// نگه می‌دارد.
    /// </summary>
    public string FieldsJson { get; set; } = "{}";

    /// <summary>
    /// اثرِ انگشتِ محلی — برای سنجشِ سلامتِ خودِ دفتر.
    /// ⚠️ به سرور <b>فرستاده نمی‌شود</b>؛ دلیلش در <c>docs/SYNC-fa.md</c>.
    /// </summary>
    public string Hash { get; set; } = string.Empty;

    /// <summary>زمانِ دستگاه (میلی‌ثانیهٔ یونیکس).</summary>
    public long ClientTs { get; set; }

    /// <summary>نسخهٔ schemaی برنامه وقتی این op ساخته شد.</summary>
    public int SchemaVersion { get; set; } = 1;

    /// <summary>سرور پذیرفتش (یا تکراری بود) ⇒ دیگر فرستاده نمی‌شود.</summary>
    public bool Synced { get; set; }

    /// <summary>چند بار تلاشِ ناموفق — برای دیدن در «جزئیاتِ همگام‌سازی».</summary>
    public int Attempts { get; set; }

    /// <summary>سرور چرا ردش کرد (خالی یعنی مشکلی نبوده).</summary>
    public string Rejected { get; set; } = string.Empty;
}

/// <summary>
/// ══ حالِ همگام‌سازی روی همین کامپیوتر ═══════════════════════════════════
///
/// همیشه <b>یک</b> ردیف با <c>Id = 1</c>. جدول است و نه فایل، تا با خودِ
/// دفتر در یک تراکنش بنشیند: cursor و opها هیچ‌وقت از هم جلو نمی‌افتند.
/// </summary>
public class SyncStateRow
{
    /// <summary>همیشه ۱.</summary>
    public long Id { get; set; } = 1;

    /// <summary>شناسهٔ این دستگاه، همان که سرور می‌بیند.</summary>
    public string DeviceId { get; set; } = string.Empty;

    /// <summary>
    /// ریشهٔ <c>SyncUid</c>ِ ردیف‌های <b>قدیمی</b> — شرحش در
    /// <c>docs/SYNC-fa.md</c> (بخشِ «شناسه‌ها»).
    /// </summary>
    public string UidSeed { get; set; } = string.Empty;

    /// <summary>تا کجای دفترِ سرور را گرفته‌ایم.</summary>
    public long Cursor { get; set; }

    public long LastPushAt { get; set; }
    public long LastPullAt { get; set; }

    /// <summary>آخرین باری که سرور واقعاً چیزی را پذیرفت.</summary>
    public long LastOkAt { get; set; }

    /// <summary>آخرین خطا — به فارسی، برای صفحهٔ تنظیمات.</summary>
    public string LastError { get; set; } = string.Empty;

    /// <summary>
    /// ۴۲۶ گرفته‌ایم: نسخهٔ برنامه از سرور جلوتر است، opها <b>نگه داشته</b>
    /// می‌شوند تا سرور به‌روز شود. هیچ چیزی دور ریخته نمی‌شود.
    /// </summary>
    public bool Holding { get; set; }

    /// <summary>نسخهٔ schemaی که سرور گفته می‌شناسد.</summary>
    public int ServerSchema { get; set; }

    /// <summary>
    /// کِی ردیف‌های از پیش موجود به دفتر رفتند (بارِ اول). صفر یعنی هنوز نه.
    /// </summary>
    public long SeededAt { get; set; }

    /// <summary>تا کدام شناسه از هر جدول در «بارِ اول» رفته‌ایم.</summary>
    public string SeedCursor { get; set; } = string.Empty;
}
