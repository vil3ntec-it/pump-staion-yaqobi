using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PumpYaqobi.App.Services;
using PumpYaqobi.Application.Localization;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Services.Data;

namespace PumpYaqobi.App.ViewModels.Sections;

/// <summary>یک رسیدِ ثبت‌شده در فهرستِ ماه.</summary>
public sealed class DebtReceiptRowViewModel
{
    public DebtReceiptRowViewModel(DebtQuickReceipt e, int index)
    {
        Entity = e;
        Index = index;
        DateShamsi = e.DateShamsi ?? "";
        Account = e.Account ?? "";
        Note = e.Note ?? "";
        AmountText = Shamsi.Money(e.Amount);
    }

    public DebtQuickReceipt Entity { get; }
    public int Index { get; }
    public string DateShamsi { get; }
    public string Account { get; }
    public string Note { get; }
    public string AmountText { get; }
}

/// <summary>
/// ══ بخشِ «رسید قرض‌داران» ═══════════════════════════════════════════════════
/// پرداختِ نقدیِ مستقیم به حسابِ یک قرض‌دار.
///
/// ⚠️ **هیچ دکمهٔ «ثبت» ندارد و نباید داشته باشد.** در نسخهٔ وب به‌محضِ کامل
/// شدنِ نام و مبلغ، ردیف خودش در حسابِ طرف می‌نشیند. این‌جا هم همان: با
/// بیرون رفتنِ فوکوس از کادرِ مبلغ (یا زدنِ Enter) ثبت می‌شود، کادرها خالی
/// می‌شوند و فوکوس به «نام» برمی‌گردد تا رسیدِ بعدی نوشته شود.
/// </summary>
public sealed partial class DebtReceiptSectionViewModel : SectionViewModel
{
    private readonly AppHost _host;

    public DebtReceiptSectionViewModel(AppHost host) : base("debtrasid", "debt", "رسید قرض‌داران")
    {
        _host = host;
        _dateShamsi = Shamsi.Today();
    }

    public ObservableCollection<DebtReceiptRowViewModel> Rows { get; } = new();
    public ObservableCollection<string> Months { get; } = new();

    // ── کادرهای ورودِ سریع ─────────────────────────────────────────────────
    [ObservableProperty] private string _typedName = "";
    [ObservableProperty] private string _amountText = "";
    [ObservableProperty] private string _note = "";
    [ObservableProperty] private string _dateShamsi;

    [ObservableProperty] private string _month = "";
    [ObservableProperty] private string _totalText = "0";

    /// <summary>فوکوس باید به کادرِ «نام» برگردد — صفحه به آن گوش می‌دهد.</summary>
    public event Action? FocusNameRequested;

    partial void OnMonthChanged(string v) => _ = ReloadAsync();

    protected override async Task LoadAsync()
    {
        var months = await _host.DebtReceipts.MonthsAsync();
        Months.Clear();
        foreach (var m in months) Months.Add(m);
        if (Month.Length == 0 || !Months.Contains(Month))
            _month = Months.FirstOrDefault() ?? Shamsi.MonthKey(Shamsi.Today());
        OnPropertyChanged(nameof(Month));
        await ReloadAsync();
    }

    private async Task ReloadAsync()
    {
        var list = await _host.DebtReceipts.ListAsync(Month.Length == 0 ? null : Month);
        Rows.Clear();
        var i = 1;
        foreach (var r in list) Rows.Add(new DebtReceiptRowViewModel(r, i++));
        TotalText = Shamsi.Money(list.Sum(r => r.Amount));
    }

    /// <summary>
    /// ‎quickAddDebtRasid‎ — همان لحظه‌ای که نام و مبلغ هر دو پر باشند.
    /// از کادرِ مبلغ (خروجِ فوکوس یا Enter) و از کادرِ نام صدا زده می‌شود.
    /// </summary>
    [RelayCommand]
    public async Task SubmitAsync()
    {
        var amount = Shamsi.Num(AmountText);
        var (res, person) = await _host.DebtReceipts.AddAsync(TypedName, amount, DateShamsi, Note);

        switch (res)
        {
            case QuickReceiptResult.Incomplete:
                return;                                     // هنوز تمام نشده — بی‌صدا
            case QuickReceiptResult.NotFound:
                _host.Toast("⚠️ حساب «" + TypedName.Trim() +
                            "» پیدا نشد — اول از بخش قرض‌داران اضافه کنید", ToastKind.Error);
                return;
        }

        _host.Toast("✅ در حساب " + person + " ثبت شد", ToastKind.Ok);
        TypedName = ""; AmountText = ""; Note = "";
        await LoadAsync();
        FocusNameRequested?.Invoke();
    }

    /// <summary>‎undoDebtRasid‎ — رسید و اثرش روی حساب، هر دو با هم.</summary>
    [RelayCommand]
    private async Task UndoAsync(DebtReceiptRowViewModel? row)
    {
        if (row is null) return;
        if (!await Dialogs.ConfirmAsync("برگرداندنِ رسید", "این رسید از حساب برداشته شود؟")) return;
        await _host.DebtReceipts.UndoAsync(row.Entity.Id);
        _host.Toast("↩️ رسید برداشته شد", ToastKind.Warn);
        await LoadAsync();
    }
}
