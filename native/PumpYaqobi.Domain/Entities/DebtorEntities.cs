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

    /// <summary>رسیدِ تیل و رسیدِ پول، هر کدام برای هر نوع سوخت جدا.</summary>
    public decimal RasidFuelPetrol { get; set; }
    public decimal RasidFuelDiesel { get; set; }
    public decimal RasidMoneyPetrol { get; set; }
    public decimal RasidMoneyDiesel { get; set; }

    /// <summary>دفترِ «واحد تیل».</summary>
    public List<DebtRow> FuelRows { get; set; } = new();
    /// <summary>دفترِ «واحد پول» — کاملاً جدا از بالایی.</summary>
    public List<DebtRow> MoneyRows { get; set; } = new();

    /// <summary>دفترِ فعال بر اساس واحدِ همین حساب (‎_acctRows‎ در HTML).</summary>
    public List<DebtRow> ActiveRows() => Mode.IsMoney() ? MoneyRows : FuelRows;
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

    /// <summary>
    /// ‎invFuelId‎ — ردیفِ **نمایشیِ** «رسید تیل»ِ یک فاکتور.
    ///
    /// این ردیف فقط ستونِ «رسید تیل» را پر می‌کند (بردگی و فی و رسیدِ پول صفر)،
    /// پس هیچ محاسبه‌ای را عوض نمی‌کند. کارش این است که کاربر داخلِ جدولِ حساب
    /// ببیند چند لیتر، به نامِ چه کسی و از کدام فاکتور رسیده — پیش از این فقط
    /// کادرِ «مقدار رسید تیل» بی‌صدا عدد می‌گرفت و در جدول هیچ ردیفی نمی‌آمد
    /// (گلایهٔ صریحِ صاحب ریپو).
    ///
    /// ⚠️ جدا از <see cref="InvoiceId"/> است: یک فاکتور می‌تواند هم ردیفِ پولی
    /// داشته باشد هم ردیفِ نمایشیِ تیل، و آن دو یکی نیستند.
    /// </summary>
    public long? InvoiceFuelId { get; set; }
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
