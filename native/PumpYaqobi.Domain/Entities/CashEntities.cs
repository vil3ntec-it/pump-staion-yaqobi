using PumpYaqobi.Domain.Enums;

namespace PumpYaqobi.Domain.Entities;

/// <summary>ارزِ یک مبلغ. گاوصندوق و صرافی چندارزی‌اند و جمع‌ها هرگز خودکار
/// تبدیل نمی‌شوند — تا خطای نرخ به حساب سرایت نکند (قاعدهٔ نسخهٔ HTML).</summary>
public enum Currency { Afn = 1, Usd = 2 }

/// <summary>بردگی = پول از گاوصندوق بیرون رفته · ماندگی = پول داخل آمده.</summary>
public enum SafeEntryKind { Bardagi = 1, Mandagi = 2 }

/// <summary>یک ردیفِ گاوصندوق (DB.safeEntries).</summary>
public class SafeEntry : EntityBase
{
    public string? DateShamsi { get; set; }
    public int DateKey { get; set; }
    /// <summary>«1405/06» — برای فیلترِ ماهانه و ایندکس.</summary>
    public string? MonthKey { get; set; }
    public SafeEntryKind Kind { get; set; } = SafeEntryKind.Bardagi;
    public string? Title { get; set; }
    public decimal Amount { get; set; }
    public Currency Currency { get; set; } = Currency.Afn;
    public string? Note { get; set; }
}

/// <summary>ارزِ مبلغِ ورودیِ صرافی.</summary>
public enum ExchangeCurrency { Toman = 1, Kaldar = 2, Afghani = 3 }

/// <summary>یک ردیفِ صرافی (DB.sarrafiRows).</summary>
public class ExchangeRow : EntityBase
{
    public string? DateShamsi { get; set; }
    public int DateKey { get; set; }
    public string? MonthKey { get; set; }
    public string? Description { get; set; }
    /// <summary>مبلغ به همان ارزِ ستونِ «واحد».</summary>
    public decimal Amount { get; set; }
    public ExchangeCurrency Currency { get; set; } = ExchangeCurrency.Toman;
    /// <summary>فی — مبلغ بر آن تقسیم می‌شود تا دالر به دست آید.</summary>
    public decimal Rate { get; set; }
    /// <summary>بردگیِ پمپ بنزین، به دالر.</summary>
    public decimal Bardagi { get; set; }
}

/// <summary>یک مصرف (DB.expenses).</summary>
public class Expense : EntityBase
{
    public string? DateShamsi { get; set; }
    public int DateKey { get; set; }
    public string? MonthKey { get; set; }
    public string? Title { get; set; }
    public decimal Amount { get; set; }
    public string? Note { get; set; }
}

/// <summary>یک ردیفِ چکنه (DB.chakanaRows) — فروشِ خرد.</summary>
public class RetailRow : EntityBase
{
    public string? DateShamsi { get; set; }
    public int DateKey { get; set; }
    public string? MonthKey { get; set; }
    public string? Name { get; set; }
    public FuelType Fuel { get; set; } = FuelType.Petrol;
    public decimal Liters { get; set; }
    public decimal PricePerLiter { get; set; }
    public decimal Rasid { get; set; }
}
