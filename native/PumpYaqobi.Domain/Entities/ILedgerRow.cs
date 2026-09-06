namespace PumpYaqobi.Domain.Entities;

/// <summary>
/// یک ردیفِ «دفتری»: تاریخِ شمسی دارد و با ماه فیلتر می‌شود.
/// گاوصندوق، صرافی، مصارف، چکنه، درآمدِ اضافی، تاریخچهٔ نرخ و مانندشان.
/// همین قرارداد به یک سرویسِ مشترک اجازه می‌دهد همهٔ این جدول‌ها را
/// با یک رفتارِ واحد بخواند و بنویسد.
/// </summary>
public interface ILedgerRow
{
    long Id { get; }
    string? DateShamsi { get; set; }
    int DateKey { get; set; }
    string? MonthKey { get; set; }
}
