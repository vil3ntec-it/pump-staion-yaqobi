using PumpYaqobi.Domain.Enums;

namespace PumpYaqobi.Domain.Entities;

// ══ پارچه‌ها ══════════════════════════════════════════════════════════════════

/// <summary>روز یا شب.</summary>
public enum ShiftKind { Day = 1, Night = 2 }

/// <summary>
/// یک شیفتِ پارچه — همان <c>shiftData</c>ِ نسخهٔ وب.
/// فروش/پول/فایده در همان‌جا هم ذخیره می‌شدند؛ اینجا هم ذخیره می‌شوند تا
/// گزارش‌های کهنه دقیقاً همان عددی را نشان دهند که آن روز ثبت شد.
/// </summary>
public class ShiftData : EntityBase
{
    public string? Name { get; set; }
    public int PumpNum { get; set; }
    public decimal Start { get; set; }
    public decimal End { get; set; }
    public decimal Price { get; set; }
    public decimal ProfitPer { get; set; }
    public decimal BuyPerLiter { get; set; }
    public decimal Sale { get; set; }
    public decimal Money { get; set; }
    public decimal Debt { get; set; }
    public decimal Available { get; set; }
    public decimal AvailMan { get; set; }
    public decimal Profit { get; set; }
    public string? Note { get; set; }
    public string? SavedAt { get; set; }
}

/// <summary>
/// یک «پارچه» (گزارش) — پطرول: <c>DB.reports</c>، دیزل: <c>DB.shifts</c>.
/// هر دو در یک جدول جمع شده‌اند چون ساختارشان یکی است؛ <see cref="Fuel"/> جدایشان می‌کند.
/// </summary>
public class ParchaReport : EntityBase
{
    public string? LegacyId { get; set; }
    public int ReportNum { get; set; }
    public string? DateShamsi { get; set; }
    public int DateKey { get; set; }
    public string? DateMiladi { get; set; }
    public string? DateQamari { get; set; }
    public FuelType Fuel { get; set; } = FuelType.Petrol;

    public long? DayShiftId { get; set; }
    public ShiftData? DayShift { get; set; }
    public long? NightShiftId { get; set; }
    public ShiftData? NightShift { get; set; }
}

// ══ مخزن و خرید تیل ═══════════════════════════════════════════════════════════

/// <summary>یک خریدِ تیل (DB.fuelEntries) — پایهٔ «مخزن» و «فی لیترِ خرید».</summary>
public class FuelPurchase : EntityBase
{
    public string? LegacyId { get; set; }
    public FuelType Fuel { get; set; } = FuelType.Petrol;
    public string? DateShamsi { get; set; }
    public int DateKey { get; set; }
    public string? Seller { get; set; }
    public decimal Kg { get; set; }
    public decimal Density { get; set; }
    public decimal PriceTon { get; set; }
    public decimal UsdRate { get; set; }
    public decimal Ton { get; set; }
    public decimal Liters { get; set; }
    public decimal TotalUsd { get; set; }
    public decimal TotalAfn { get; set; }
    public decimal PerLiter { get; set; }
    public string? Note { get; set; }
}

/// <summary>میله‌زنیِ مخزن (DB.tankDips) — اندازهٔ واقعی در برابر اندازهٔ دفتری.</summary>
public class TankDip : EntityBase, ILedgerRow
{
    public FuelType Fuel { get; set; } = FuelType.Petrol;
    public string? DateShamsi { get; set; }
    public int DateKey { get; set; }
    public string? MonthKey { get; set; }
    public decimal Measured { get; set; }
    public decimal Expected { get; set; }
    public string? Note { get; set; }
}

/// <summary>تخلیهٔ تانکر (DB.tankerUnloads).</summary>
public class TankerUnload : EntityBase, ILedgerRow
{
    public FuelType Fuel { get; set; } = FuelType.Petrol;
    public string? DateShamsi { get; set; }
    public int DateKey { get; set; }
    public string? MonthKey { get; set; }
    public string? Driver { get; set; }
    public string? Plate { get; set; }
    public decimal Liters { get; set; }
    public string? Note { get; set; }
}

// ══ شرکت‌های تیل ══════════════════════════════════════════════════════════════

/// <summary>حسابِ یک شرکتِ تیل (DB.tilCompanies) — دو دفترِ جدا: پطرول و دیزل.</summary>
public class TilCompany : EntityBase
{
    public string? LegacyId { get; set; }
    public string? Name { get; set; }
    public string? Note { get; set; }
    /// <summary>نرخِ تبدیلِ دستیِ همین شرکت. صفر یعنی «از میانگینِ خریدها بگیر».</summary>
    public decimal? UsdRate { get; set; }
    public List<CompanyRow> Rows { get; set; } = new();
}

/// <summary>یک ردیفِ حسابِ شرکت. «تن/دالر/فی» خرید است و «پول» پرداخت.</summary>
public class CompanyRow : EntityBase
{
    public long CompanyId { get; set; }
    public TilCompany? Company { get; set; }
    public FuelType Fuel { get; set; } = FuelType.Petrol;
    public int SortIndex { get; set; }
    public string? DateShamsi { get; set; }
    public int DateKey { get; set; }
    public string? Name { get; set; }
    public decimal Kg { get; set; }
    public decimal Ton { get; set; }
    /// <summary>قیمتِ هر تن به دالر.</summary>
    public decimal Usd { get; set; }
    /// <summary>نرخِ دالر.</summary>
    public decimal Rate { get; set; }
    /// <summary>پرداختی.</summary>
    public decimal Poul { get; set; }
    public Currency PoulCurrency { get; set; } = Currency.Afn;
    /// <summary>نرخِ پولِ ردیف — فقط برای دادهٔ کهنه؛ ستونش برداشته شده.</summary>
    public decimal? PayRate { get; set; }
    /// <summary>ردیفی که خودکار از «خریدِ مخزن» ساخته شده.</summary>
    public string? SourcePurchaseId { get; set; }
    /// <summary>ردیفی که خودکار از رسیدِ صرافی یا گاوصندوق ساخته شده.</summary>
    public string? SourceReceiptId { get; set; }
    public string? Note { get; set; }

    /// <summary>ردیفِ واقعاً خالی — ‎_isEmptyCompanyRow‎.</summary>
    public bool IsEmpty =>
        string.IsNullOrWhiteSpace(Name) && Ton == 0m && Kg == 0m && Usd == 0m
        && Rate == 0m && Poul == 0m
        && SourcePurchaseId is null && SourceReceiptId is null;
}

// ══ تیل امانت ═════════════════════════════════════════════════════════════════

public enum AmanatRowState { Open = 1, Closed = 2 }

/// <summary>کادرِ حسابِ امانت (DB.amanatAccounts).</summary>
public class AmanatAccount : EntityBase
{
    public string? LegacyId { get; set; }
    public string? Name { get; set; }
    public FuelType Fuel { get; set; } = FuelType.Petrol;
    /// <summary>فیصدیِ خودِ پمپ.</summary>
    public decimal? MyPct { get; set; }
    public decimal? Rate { get; set; }
    public string? Note { get; set; }
    public List<AmanatRow> Rows { get; set; } = new();
}

/// <summary>یک محمولهٔ امانت.</summary>
public class AmanatRow : EntityBase
{
    public long AccountId { get; set; }
    public AmanatAccount? Account { get; set; }
    public int SortIndex { get; set; }
    public string? DateShamsi { get; set; }
    public int DateKey { get; set; }
    public string? Name { get; set; }
    public string? ToAccount { get; set; }
    public decimal? Liters { get; set; }
    public decimal? Taken { get; set; }
    public decimal? Days { get; set; }
    public decimal? Temp { get; set; }
    public AmanatRowState State { get; set; } = AmanatRowState.Open;
    public string? CloseDate { get; set; }
    /// <summary>«سرپوش» — باز یا بسته بودنِ درِ تانکر.</summary>
    public string? Lid { get; set; }
    public decimal? BasePct { get; set; }
    public decimal? Actual { get; set; }
    public string? Note { get; set; }
}

// ══ ورقِ روزانه ═══════════════════════════════════════════════════════════════

/// <summary>ورقِ یک روز (DB.waraqEntries) — دو شیفتِ جدا.</summary>
public class WaraqEntry : EntityBase
{
    public string? LegacyId { get; set; }
    public string? DateShamsi { get; set; }
    public int DateKey { get; set; }
    public string? Station { get; set; }
    public ShiftKind ActiveShift { get; set; } = ShiftKind.Day;
    public List<WaraqShift> Shifts { get; set; } = new();
}

/// <summary>یک شیفت داخلِ ورق.</summary>
public class WaraqShift : EntityBase
{
    public long WaraqId { get; set; }
    public WaraqEntry? Waraq { get; set; }
    public ShiftKind Kind { get; set; } = ShiftKind.Day;
    public string? WorkerName { get; set; }
    public decimal FabricDebt { get; set; }
    public decimal FabricAvailable { get; set; }
    public decimal AvailableFromShift { get; set; }
    public decimal PricePerLiter { get; set; }
    public List<WaraqPump> Pumps { get; set; } = new();
    public List<WaraqTransaction> Transactions { get; set; } = new();
}

/// <summary>یک «پایه» (نازل) در ورق.</summary>
public class WaraqPump : EntityBase
{
    public long ShiftId { get; set; }
    public WaraqShift? Shift { get; set; }
    public int SortIndex { get; set; }
    public int Num { get; set; }
    public FuelType Fuel { get; set; } = FuelType.Petrol;
    public string? Note { get; set; }
    public decimal Start { get; set; }
    public decimal End { get; set; }
    public decimal PricePerLiter { get; set; }
    public decimal Debt { get; set; }
}

public enum WaraqTxnType { Debt = 1, Expense = 2 }

/// <summary>یک ردیفِ «قرض/مصرف» در ورق.</summary>
public class WaraqTransaction : EntityBase
{
    public long ShiftId { get; set; }
    public WaraqShift? Shift { get; set; }
    public int SortIndex { get; set; }
    public string? Name { get; set; }
    public decimal Liters { get; set; }
    public decimal Amount { get; set; }
    public WaraqTxnType Type { get; set; } = WaraqTxnType.Debt;
    public FuelType Fuel { get; set; } = FuelType.Petrol;
}

// ══ فاکتورها ══════════════════════════════════════════════════════════════════

public enum InvoiceStatus { Pending = 1, Approved = 2 }

/// <summary>فاکتورِ فروش (DB.invoices).</summary>
public class Invoice : EntityBase
{
    public string? LegacyId { get; set; }
    public int InvoiceNumber { get; set; }
    public InvoiceStatus Status { get; set; } = InvoiceStatus.Pending;
    public FuelType Fuel { get; set; } = FuelType.Petrol;
    public string? DateShamsi { get; set; }
    public int DateKey { get; set; }
    public string? CustomerName { get; set; }
    public string? DebtAlias { get; set; }
    public string? VehicleType { get; set; }
    public string? Phone { get; set; }
    public decimal PricePerLiter { get; set; }
    public decimal Liters { get; set; }
    public decimal Amount { get; set; }
    public bool ByMoney { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

// ══ کارمندان ══════════════════════════════════════════════════════════════════

public class StaffMember : EntityBase
{
    public string? LegacyId { get; set; }
    public string? Name { get; set; }
    public string? ShiftIn { get; set; }
    public string? ShiftOut { get; set; }
    public decimal Salary { get; set; }
    public int PayDay { get; set; } = 1;
    public string? Note { get; set; }
}

public class AttendanceRow : EntityBase
{
    public long StaffId { get; set; }
    public StaffMember? Staff { get; set; }
    public string? DateShamsi { get; set; }
    public int DateKey { get; set; }
    /// <summary>«HH:mm» — آمدن.</summary>
    public string? In { get; set; }
    /// <summary>«HH:mm» — رفتن.</summary>
    public string? Out { get; set; }
    public string? Note { get; set; }
}

public class SalaryPayment : EntityBase
{
    public long StaffId { get; set; }
    public StaffMember? Staff { get; set; }
    /// <summary>«1405/06».</summary>
    public string? MonthKey { get; set; }
    public string? DateShamsi { get; set; }
    public decimal Amount { get; set; }
    public string? Note { get; set; }
}

/// <summary>کمبودیِ کارمند — کسریِ پولِ شیفت.</summary>
public class StaffShortage : EntityBase
{
    public long? StaffId { get; set; }
    public string? Name { get; set; }
    public string? DateShamsi { get; set; }
    public int DateKey { get; set; }
    public decimal Amount { get; set; }
    public decimal Paid { get; set; }
    public string? Note { get; set; }
}

// ══ دیگر ══════════════════════════════════════════════════════════════════════

public class Camera : EntityBase
{
    public string? LegacyId { get; set; }
    public string? Name { get; set; }
    public string? Url { get; set; }
    public string? Note { get; set; }
    public int SortIndex { get; set; }
}

/// <summary>درآمدِ اضافی (DB.extraIncomes).</summary>
public class ExtraIncome : EntityBase, ILedgerRow
{
    public string? DateShamsi { get; set; }
    public int DateKey { get; set; }
    public string? MonthKey { get; set; }
    public decimal Qty { get; set; }
    public decimal Buy { get; set; }
    public decimal Market { get; set; }
    public string? Seller { get; set; }
    public decimal Amount { get; set; }
    public string? Note { get; set; }
}

/// <summary>تاریخچهٔ نرخِ اتحادیه (DB.rateHistory).</summary>
public class RateHistoryEntry : EntityBase, ILedgerRow
{
    public FuelType Fuel { get; set; } = FuelType.Petrol;
    public string? DateShamsi { get; set; }
    public int DateKey { get; set; }
    public string? MonthKey { get; set; }
    public decimal Rate { get; set; }
    public string? Note { get; set; }
}

/// <summary>
/// سطلِ زباله (DB.trash) — هر حذفی اول اینجا می‌آید و قابلِ برگرداندن است.
/// نوعِ رکورد و خودِ رکورد به‌صورت JSON نگه داشته می‌شوند تا هر جدولی را بپذیرد.
/// </summary>
public class TrashItem : EntityBase
{
    public string? Kind { get; set; }
    public string? Label { get; set; }
    public string? PayloadJson { get; set; }
    public DateTime DeletedAtUtc { get; set; } = DateTime.UtcNow;
    public string? DeletedBy { get; set; }
}

/// <summary>تنظیماتِ کلید-مقدارِ خودِ برنامه (نامِ پمپ، نرخ‌ها، آستانه‌ها…).</summary>
public class Setting : EntityBase
{
    public string Key { get; set; } = "";
    public string? Value { get; set; }
}

/// <summary>کاربرِ برنامه — رمزها هرگز به‌صورت متنِ خام ذخیره نمی‌شوند.</summary>
public class AppUser : EntityBase
{
    public string? UserName { get; set; }
    public string? DisplayName { get; set; }
    public UserRole Role { get; set; } = UserRole.Viewer;
    /// <summary>PBKDF2-SHA256 · نمکِ ۱۶ بایتی · ۲۱۰٬۰۰۰ دور.</summary>
    public string? PasswordHash { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? LastLoginUtc { get; set; }
}

/// <summary>ردِ کارهای مهم — «تاریخچه‌ها».</summary>
public class AuditEntry : EntityBase
{
    public DateTime AtUtc { get; set; } = DateTime.UtcNow;
    public string? DateShamsi { get; set; }
    public string? Actor { get; set; }
    public string? Action { get; set; }
    public string? Target { get; set; }
    public string? Detail { get; set; }
}
