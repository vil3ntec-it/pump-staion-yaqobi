using CommunityToolkit.Mvvm.ComponentModel;
using PumpYaqobi.App.Controls;
using CommunityToolkit.Mvvm.Input;
using PumpYaqobi.App.Printing;
using PumpYaqobi.App.Services;
using PumpYaqobi.Application.Localization;
using PumpYaqobi.Application.Services;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Reporting.Pdf;

namespace PumpYaqobi.App.ViewModels.Sections;

public sealed partial class ExpenseRowViewModel : RowViewModel
{
    private readonly Expense _e;
    private readonly ExpenseSectionViewModel _owner;

    public ExpenseRowViewModel(Expense e, ExpenseSectionViewModel owner)
    {
        _e = e; _owner = owner;
        Loading = true;
        _dateShamsi = e.DateShamsi ?? "";
        _title = e.Title ?? "";
        _amount = e.Amount;
        _note = e.Note ?? "";
        Loading = false;
    }

    public Expense Entity => _e;

    [ObservableProperty] private string _dateShamsi = "";
    [ObservableProperty] private string _title = "";
    [ObservableProperty] private decimal _amount;
    [ObservableProperty] private string _note = "";

    partial void OnDateShamsiChanged(string v) => Touch();
    partial void OnTitleChanged(string v) => Touch();
    partial void OnAmountChanged(decimal v) { Touch(); OnPropertyChanged(nameof(AmountText)); }
    partial void OnNoteChanged(string v) => Touch();

    public string AmountText { get => Shamsi.MoneyOrBlank(Amount); set => Amount = Shamsi.Num(value); }

    protected override void Apply()
    {
        _e.DateShamsi = DateShamsi;
        _e.Title = Title;
        _e.Amount = Amount;
        _e.Note = Note;
    }

    protected override Task SaveAsync() => _owner.SaveEntityAsync(_e);
}

/// <summary>
/// ══ بخشِ مصارف ═════════════════════════════════════════════════════════════
/// فهرستِ مصارفِ یک ماه و جمعشان. همین جمع، در «مفاد/ضرر» هم به کار می‌رود —
/// پس یک نسخه بیشتر ندارد.
/// </summary>
public sealed partial class ExpenseSectionViewModel
    : LedgerSectionViewModel<ExpenseRowViewModel, Expense>
{
    private readonly ExpenseService _calc;

    public ExpenseSectionViewModel(AppHost host)
        : base("expenses", "expenses", "مصارف", host.ExpenseLedger)
    {
        _calc = host.Expenses; _host = host;
        // «📝 یادداشت این بخش» — همتای ‎.sec-note-box‎ی سایت. کلیدش همان
        // کلیدِ نسخهٔ وب است تا نوت‌های واردشده سرِ جای خودشان بنشینند.
        Notes = new SectionNotesViewModel(Id, host.SectionNotes,
            (m, ok) => host.Toast(m, ok ? ToastKind.Ok : ToastKind.Warn));
    }

    private readonly AppHost _host;

    /// <summary>جمعِ ماهِ انتخاب‌شده — همان چیزی که در «مفاد/ضرر» هم به کار می‌رود.</summary>
    [ObservableProperty] private decimal _total;
    /// <summary>«مصارف امروز» — فقط ردیف‌های همین تاریخ.</summary>
    [ObservableProperty] private decimal _todayTotal;
    /// <summary>«مصارف کل» — همهٔ ماه‌ها، نه فقط ماهِ روی صفحه.</summary>
    [ObservableProperty] private decimal _grandTotal;

    public string TotalText => Shamsi.Money(Total);
    public string TodayText => Shamsi.Money(TodayTotal);
    public string GrandText => Shamsi.Money(GrandTotal);

    partial void OnTotalChanged(decimal v) => OnPropertyChanged(nameof(TotalText));
    partial void OnTodayTotalChanged(decimal v) => OnPropertyChanged(nameof(TodayText));
    partial void OnGrandTotalChanged(decimal v) => OnPropertyChanged(nameof(GrandText));

    /// <summary>
    /// واردِ مصارف که می‌شویم، جدول از دیتابیس تازه می‌شود — ردیف‌های «مصرف»ِ
    /// ورق ممکن است همین حالا اضافه شده باشند (سایت هم با هر ‎showSection‎
    /// دوباره ‎renderExpenses‎ را صدا می‌زد).
    /// </summary>
    public override async Task OnActivatedAsync()
    {
        if (IsLoaded) await ReloadRowsAsync();
    }

    protected override ExpenseRowViewModel Wrap(Expense e) => new(e, this);
    protected override long EntityIdOf(ExpenseRowViewModel r) => r.Entity.Id;
    protected override Expense EntityOf(ExpenseRowViewModel r) => r.Entity;

    protected override void Recalc()
    {
        Total = _calc.Total(Rows.Select(r => r.Entity));
        var today = Shamsi.Today();
        TodayTotal = _calc.Total(Rows.Select(r => r.Entity).Where(e => e.DateShamsi == today));
        _ = RefreshGrandAsync();
    }

    /// <summary>«مصارف کل» از همهٔ ماه‌ها خوانده می‌شود، نه از ردیف‌های روی صفحه.</summary>
    private async Task RefreshGrandAsync() =>
        GrandTotal = _calc.Total(await _host.ExpenseLedger.ListAsync(null));

    /// <summary>ردیفِ «جمله» — جمعِ همین ماه؛ همان عددی که «مفاد/ضرر» می‌خواند.</summary>
    protected override IReadOnlyList<TotalCell> BuildTotals() => new[]
    {
        new TotalCell("مبلغِ ماه", TotalText, "Pump.Warn", column: "مبلغ"),
        new TotalCell("امروز", TodayText),
    };

    /// <summary>‎pdfExpenses(monthKey)‎ — ورقِ مصارفِ همین ماه.</summary>
    [RelayCommand]
    private Task PdfAsync()
    {
        var rows = Rows.Select(r => r.Entity).ToList();
        var input = new ExpenseReportInput(Shamsi.MonthLabel(Month), rows,
                                           Shamsi.Today(), DocDates.Line());
        return Documents.ShowAsync(() => new ExpenseReport(input),
                                   "مصارف " + Shamsi.MonthLabel(Month));
    }
}
