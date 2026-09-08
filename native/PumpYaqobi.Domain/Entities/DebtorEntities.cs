using PumpYaqobi.Domain.Enums;

namespace PumpYaqobi.Domain.Entities;

/// <summary>پایهٔ همهٔ رکوردها — بندِ ۵: کلید، زمانِ ساخت/ویرایش و حذفِ نرم.</summary>
public abstract class EntityBase
{
    public long Id { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    /// <summary>حذفِ نرم — رکوردِ مالی هیچ‌وقت واقعاً پاک نمی‌شود.</summary>
    public DateTime? DeletedAt { get; set; }
    public bool IsDeleted => DeletedAt.HasValue;
}

/// <summary>
/// قرض‌دار (یا شخصِ بی‌فاکتور). در HTML این‌ها DB.debtPersons و DB.noinvPersons
/// بودند — دو آرایهٔ جدا با ساختارِ یکسان؛ این‌جا یک موجودیت با یک تفکیک‌کننده.
/// </summary>
public class Debtor : EntityBase
{
    /// <summary>شناسهٔ نسخهٔ HTML (مثل «p1758…») — برای مهاجرت و کیو‌آرهای قدیمی.</summary>
    public string LegacyId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Phone { get; set; }
    /// <summary>«فیِ خرید» — فقط یادداشتِ دستی، در هیچ محاسبه‌ای نیست.</summary>
    public string? BuyFeeNote { get; set; }
    public bool IsNoInvoice { get; set; }

    /// <summary>حسابِ اصلی — در HTML خودِ شیءِ شخص، حسابِ اصلی هم بود.</summary>
    public DebtAccount MainAccount { get; set; } = new();
    /// <summary>حساب‌های فرعی (person.subs). هر کدام دفترِ کاملاً مستقل.</summary>
    public List<DebtAccount> SubAccounts { get; set; } = new();

    /// <summary>حسابِ اصلی + همهٔ فرعی‌ها — ترتیب مثل HTML: اول اصلی.</summary>
    public IEnumerable<DebtAccount> AllAccounts()
    {
        yield return MainAccount;
        foreach (var s in SubAccounts) if (s is not null) yield return s;
    }
}

/// <summary>
/// یک «حساب» — اصلی یا فرعی. هر حساب واحدِ خودش (Mode)، فیصدیِ خودش و دو دفترِ
/// جدا دارد: ردیف‌های تیل و ردیف‌های پول.
/// </summary>
public class DebtAccount : EntityBase
{
    /// <summary>
    /// ⚠️ دو کلیدِ جدا، هر دو اختیاری — عمدی است:
    /// حسابِ اصلی با ‎MainOfDebtorId‎ به شخص وصل می‌شود و حساب‌های فرعی با
    /// ‎DebtorId‎. اگر هر دو یک کلید می‌بودند، ذخیرهٔ حسابِ اصلی کلیدِ خالی
    /// می‌داد و SQLite با «FOREIGN KEY constraint failed» رد می‌کرد.
    /// </summary>
    public long? DebtorId { get; set; }
    public long? MainOfDebtorId { get; set; }
    /// <summary>شناسهٔ فرعیِ نسخهٔ HTML («s1758…»). برای حسابِ اصلی خالی است.</summary>
    public string? LegacySubId { get; set; }
    public bool IsMain => string.IsNullOrEmpty(LegacySubId);
    public string? Name { get; set; }
    public string? Note { get; set; }

    /// <summary>واحدِ همین حساب — مالِ حساب است نه مالِ شخص.</summary>
    public LedgerMode Mode { get; set; } = LedgerMode.Fuel;

    /// <summary>
    /// فیصدیِ پطرول و دیزل — کاملاً جدا از هم (قاعدهٔ صریحِ پروژه).
    /// null یعنی «برای این تیل فیصدی نیست».
    /// </summary>
    public decimal? PercentPetrol { get; set; }
    public decimal? PercentDiesel { get; set; }
    /// <summary>فیصدیِ مشترکِ نسخه‌های قدیمی — فقط برای سازگاریِ داده.</summary>
    public decimal? PercentLegacy { get; set; }

    /// <summary>
    /// «سپردهٔ پول» همین حساب — ‎acct.moneyDeposit‎ در نسخهٔ وب.
    ///
    /// ⚠️ عمداً در هیچ محاسبه‌ای نیست؛ در نسخهٔ وب هم نبود (فقط نوشته و
    /// نشان داده می‌شد). ولی مالِ **همین حساب** است، نه مالِ شخص — پس
    /// عوض کردنش روی حساب‌های دیگرِ همان شخص اثر ندارد.
    /// </summary>
    public decimal? MoneyDeposit { get; set; }

    /// <summary>
    /// رسیدِ تیل و رسیدِ پول، هر کدام برای هر نوع سوخت جدا.
    ///
    /// ⚠️ این چهار عدد دیگر «منبع» نیستند — از امروز فقط **جمعِ دفترِ رسید**
    /// (<see cref="RasidLog"/>) در آن‌ها نگه داشته می‌شود، تا PDF، آرشیو،
    /// نمای انباشته و هشدارها که همه از این‌ها می‌خوانند، دست‌نخورده بمانند.
    /// هرگز مستقیم در آن‌ها ننویسید؛ <c>DebtCalculationService.PushRasid</c>
    /// و <c>RasidLogSync</c> تنها راهِ عوض کردنشان‌اند.
    /// </summary>
    public decimal RasidFuelPetrol { get; set; }
    public decimal RasidFuelDiesel { get; set; }
    public decimal RasidMoneyPetrol { get; set; }
    public decimal RasidMoneyDiesel { get; set; }

    /// <summary>
    /// ══ دفترِ رسیدهای سربرگ — تنها منبعِ داده ═══════════════════════════════
    ///
    /// خواستهٔ صریحِ صاحب ریپو: «سربرگ، جدول و جمع باید از یک Data Source
    /// بخوانند. وقتی در سربرگ رسیدی وارد می‌شود باید همان لحظه ردیفِ خودش را
    /// در جدول داشته باشد و برعکس.»
    ///
    /// سایت هم دقیقاً همین را دارد (<c>acct.rasidLog</c>، خطِ ۳۴۶۲۰ی
    /// <c>index.html</c>): هر چیزی که در سربرگ نوشته شود یک «رسیدِ تازه» است
    /// و در دفتر ثبت می‌شود؛ کادرِ سربرگ جمعِ همهٔ آن‌ها را نشان می‌دهد و هر
    /// رسید ردیفِ خودش را در جدول نگه می‌دارد. پیش از این یک عددِ تکی بود و
    /// رسیدِ تازه، قبلی را پاک می‌کرد.
    /// </summary>
    public List<RasidEntry> RasidLog { get; set; } = new();

    /// <summary>دفترِ «واحد تیل».</summary>
    public List<DebtRow> FuelRows { get; set; } = new();
    /// <summary>دفترِ «واحد پول» — کاملاً جدا از بالایی.</summary>
    public List<DebtRow> MoneyRows { get; set; } = new();

    /// <summary>دفترِ فعال بر اساس واحدِ همین حساب (‎_acctRows‎ در HTML).</summary>
    public List<DebtRow> ActiveRows() => Mode.IsMoney() ? MoneyRows : FuelRows;
}

/// <summary>
/// ══ یک رسیدِ سربرگ ══════════════════════════════════════════════════════════
/// یک عددِ رسید که کاربر در سربرگِ حساب نوشته است. هر کدام مستقل‌اند: رسیدِ
/// تازه جای قبلی را نمی‌گیرد، ردیفِ خودش را در جدول دارد و با 🗑️ِ همان ردیف
/// پاک می‌شود.
///
/// <para>⚠️ در هیچ جمعی از ردیف‌های جدول شمرده نمی‌شود — دفترِ جداست. اثرش از
/// راهِ همان چهار عددِ <c>Rasid…</c>ی حساب دیده می‌شود که جمعِ همین دفترند.</para>
/// </summary>
public class RasidEntry : EntityBase
{
    public long AccountId { get; set; }

    /// <summary>در کدام دفتر نوشته شده: واحدِ تیل یا واحدِ پول.</summary>
    public LedgerMode Unit { get; set; } = LedgerMode.Fuel;

    /// <summary>مالِ کدام تیل است.</summary>
    public FuelType Fuel { get; set; } = FuelType.Petrol;

    public decimal Value { get; set; }

    /// <summary>تاریخِ شمسیِ روزی که نوشته شد.</summary>
    public string? DateShamsi { get; set; }

    /// <summary>
    /// اگر این رسید از تاییدِ یک فاکتور آمده باشد، شمارهٔ همان فاکتور.
    /// برگرداندنِ تایید دقیقاً همین رکورد را برمی‌دارد — نه کم کردنِ یک عدد
    /// از جمع، که با رسیدهای دستیِ کاربر قاطی می‌شد.
    /// </summary>
    public long? InvoiceId { get; set; }

    public int SortIndex { get; set; }
}

/// <summary>یک ردیفِ جدولِ حساب.</summary>
public class DebtRow : EntityBase
{
    /// <summary>
    /// همان دلیلِ بالا: دفترِ تیل و دفترِ پول دو مجموعهٔ جدا هستند، پس هر کدام
    /// کلیدِ خودش را دارد. یکی از این دو پر است، نه هر دو.
    /// </summary>
    public long? FuelAccountId { get; set; }
    public long? MoneyAccountId { get; set; }
    public int SortIndex { get; set; }
    /// <summary>تاریخِ شمسی همان‌طور که کاربر نوشته (مثل «1405/6/14»).</summary>
    public string? DateShamsi { get; set; }
    /// <summary>کلیدِ عددیِ تاریخ برای مرتب‌سازی و ایندکس — سال×۱۰۰۰۰+ماه×۱۰۰+روز.</summary>
    public int DateKey { get; set; }
    public string? Name { get; set; }
    /// <summary>«حواله» — متن یا عدد، آزاد.</summary>
    public string? Hawala { get; set; }
    public FuelType Fuel { get; set; } = FuelType.Petrol;
    /// <summary>لیترِ برده‌شده.</summary>
    public decimal Liters { get; set; }
    /// <summary>فیِ دستیِ همین ردیف. خالی یعنی بردگیِ پولی صفر است.</summary>
    public decimal? PricePerLiter { get; set; }
    /// <summary>بردگیِ پولی. در دفترِ پول دستی است؛ در دفترِ تیل از لیتر×فی می‌آید.</summary>
    public decimal Bardagi { get; set; }
    /// <summary>رسیدِ نقدی همین ردیف.</summary>
    public decimal Rasid { get; set; }
    /// <summary>رسیدِ تیلیِ همین ردیف.</summary>
    public decimal RasidFuel { get; set; }
    public decimal Albaqi { get; set; }
    /// <summary>این ردیف در دفترِ پول ثبت شده (r.byMoney در HTML).</summary>
    public bool ByMoney { get; set; }

    /// <summary>
    /// از کجا آمده: خالی یعنی دستیِ خودِ کاربر، «waraq» یعنی از ورقِ روزانه،
    /// «parcha» یعنی از «رسید پارچه‌ها»، «debtQuick» یعنی ورودِ سریعِ رسید.
    /// </summary>
    public string? Src { get; set; }

    /// <summary>
    /// کلیدِ یکتای همان منبع («parcha|pr123»). هر بار که همان منبع دوباره ثبت
    /// شود، ردیفِ قبلی به‌روز می‌شود — ردیفِ دوم ساخته نمی‌شود.
    /// </summary>
    public string? SrcKey { get; set; }

    /// <summary>
    /// اگر این ردیف را یک فاکتور ساخته باشد، شناسهٔ همان فاکتور.
    /// با برگشت یا حذفِ فاکتور، دقیقاً همین ردیف برداشته می‌شود — نه ردیفِ دیگری.
    /// </summary>
    public long? InvoiceId { get; set; }
}

/// <summary>
/// ══ رسیدِ نقدیِ قرض‌دار ═══════════════════════════════════════════════════════
/// ‎DB.debtQuickReceipts‎ — پرداختِ نقدیِ مستقیم به حسابِ یک قرض‌دار، بی هیچ تیل.
///
/// این فقط «دفترچهٔ رسیدها»ست: خودِ اثرِ مالی همان لحظه به‌صورت یک ردیف در
/// حسابِ قرض‌دار می‌نشیند (با ‎SrcKey = "debtQuick|&lt;id&gt;"‎). پس این جدول
/// هیچ‌وقت در محاسبهٔ الباقی شمرده نمی‌شود — وگرنه هر رسید دو بار حساب می‌شد.
/// </summary>
public class DebtQuickReceipt : EntityBase
{
    /// <summary>شناسه‌ای که در ‎SrcKey‎ِ ردیفِ حساب می‌نشیند.</summary>
    public string LegacyId { get; set; } = string.Empty;
    public string? DateShamsi { get; set; }
    public int DateKey { get; set; }
    /// <summary>«1405/06» — برای منویِ ماه.</summary>
    public string? MonthKey { get; set; }
    /// <summary>نامِ قرض‌داری که رسید در حسابش نشست (نه متنی که کاربر تایپ کرد).</summary>
    public string? Account { get; set; }
    public string? Note { get; set; }
    public decimal Amount { get; set; }

    /// <summary>کلیدِ ردیفی که این رسید در حسابِ قرض‌دار ساخته.</summary>
    public string SrcKey => "debtQuick|" + LegacyId;
}

/// <summary>
/// ══ یک جدولِ آرشیوشدهٔ حساب — ‎acct.tableHistory[]‎ی سایت ═══════════════════
///
/// دکمهٔ «جدول جدید» (‎newPersonTable()‎) جدولِ فعلی را عکس می‌گیرد، همین‌جا
/// می‌گذارد و جدولِ زنده را خالی می‌کند. پس این‌ها **عکسِ گذشته** هستند:
///
///   ⚠️ هرگز در هیچ جمعِ زنده‌ای شمرده نمی‌شوند. الباقی و سربرگِ حساب فقط از
///     ردیف‌های زندهٔ همان حساب می‌آید — درست مثل سایت، که در آن هم
///     ‎tableHistory‎ فقط برای دیدن است.
///
/// ردیف‌ها به‌صورت JSON نگه داشته می‌شوند، نه رکوردِ جدا: عکسِ گذشته نباید
/// با ویرایشِ حسابِ زنده تکان بخورد، و کلیدِ خارجی به ردیف‌های زنده دقیقاً
/// همان تکان را می‌داد. سایت هم ‎JSON.parse(JSON.stringify(rows))‎ می‌کند.
/// </summary>
public class DebtTableArchive : EntityBase
{
    /// <summary>حسابی که این جدول از آن آرشیو شده.</summary>
    public long AccountId { get; set; }

    /// <summary>تاریخِ شمسیِ آرشیو شدن (‎h.createdAt‎).</summary>
    public string? CreatedShamsi { get; set; }

    /// <summary>واحدِ همان لحظه — ‎h.unitMode‎. عکس با واحدِ خودش می‌ماند.</summary>
    public bool IsMoney { get; set; }

    public decimal? PercentPetrol { get; set; }
    public decimal? PercentDiesel { get; set; }
    public decimal RasidFuelPetrol { get; set; }
    public decimal RasidFuelDiesel { get; set; }
    public decimal RasidMoneyPetrol { get; set; }
    public decimal RasidMoneyDiesel { get; set; }

    /// <summary>توضیحاتِ حساب در همان لحظه (‎h.note‎).</summary>
    public string? Note { get; set; }

    /// <summary>عکسِ ردیف‌ها — آرایهٔ JSONِ ‎DebtRow‎.</summary>
    public string RowsJson { get; set; } = "[]";

    /// <summary>شمارِ ردیف‌ها، تا فهرست بی باز کردنِ JSON هم عدد داشته باشد.</summary>
    public int RowCount { get; set; }
}
