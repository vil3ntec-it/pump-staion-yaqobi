using CommunityToolkit.Mvvm.ComponentModel;
using PumpYaqobi.App.Services;
using PumpYaqobi.Application.Localization;
using PumpYaqobi.Application.Services;
using PumpYaqobi.Domain.Entities;

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

    public string AmountText { get => Shamsi.Money(Amount); set => Amount = Shamsi.Num(value); }

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
        => _calc = host.Expenses;

    [ObservableProperty] private decimal _total;

    public string TotalText => Shamsi.Money(Total);

    partial void OnTotalChanged(decimal v) => OnPropertyChanged(nameof(TotalText));

    protected override ExpenseRowViewModel Wrap(Expense e) => new(e, this);
    protected override long EntityIdOf(ExpenseRowViewModel r) => r.Entity.Id;
    protected override void Recalc() => Total = _calc.Total(Rows.Select(r => r.Entity));
}
