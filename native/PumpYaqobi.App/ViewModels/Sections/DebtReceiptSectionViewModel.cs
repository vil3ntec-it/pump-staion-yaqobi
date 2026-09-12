using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using PumpYaqobi.App.Controls;
using CommunityToolkit.Mvvm.Input;
using PumpYaqobi.App.Printing;
using PumpYaqobi.App.Services;
using PumpYaqobi.Application.Localization;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Reporting.Pdf;
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

    public DebtReceiptSectionViewModel(AppHost host) : base("debtrasid", "debt", "رسید قرض‌داران / چکنه")
    {
        // «📝 یادداشت این بخش» — همتای ‎.sec-note-box‎ی سایت. کلیدش همان
        // کلیدِ نسخهٔ وب است تا نوت‌های واردشده سرِ جای خودشان بنشینند.
        Notes = new SectionNotesViewModel(Id, host.SectionNotes,
            (m, ok) => host.Toast(m, ok ? ToastKind.Ok : ToastKind.Warn));
        _host = host;
        _dateShamsi = Shamsi.Today();
    }

    /// <summary>⚠️ ‎BulkRows‎: پر شدنِ جدول یک خبر می‌دهد نه ‎n‎ خبر
    /// — وگرنه جدول به ازای هر ردیف یک‌بار از نو چیده می‌شود و بخش می‌ایستد.</summary>
    public BulkRows<DebtReceiptRowViewModel> Rows { get; } = new();
    public ObservableCollection<string> Months { get; } = new();

    // ── کادرهای ورودِ سریع ─────────────────────────────────────────────────
    [ObservableProperty] private string _typedName = "";
    [ObservableProperty] private string _amountText = "";
    [ObservableProperty] private string _note = "";
    [ObservableProperty] private string _dateShamsi;

    [ObservableProperty] private string _month = "";
    [ObservableProperty] private string _totalText = "0";

    /// <summary>ردیفِ «جمله»ی ته جدول — جمعِ رسیدهای همین ماه.</summary>
    public IReadOnlyList<TotalCell> TotalCells => new[]
    {
        new TotalCell("شمارِ رسیدها", Shamsi.Money(Rows.Count)),
        new TotalCell("مبلغِ رسید", TotalText, "Pump.Ok", "مبلغ رسید"),
    };

    partial void OnTotalTextChanged(string v) => OnPropertyChanged(nameof(TotalCells));

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
        using (Rows.Batch())
        {
            Rows.Clear();
            var i = 1;
            foreach (var r in list) Rows.Add(new DebtReceiptRowViewModel(r, i++));
        }
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

    /// <summary>‎pdfDebtRasid(monthKey)‎ — ورقِ رسیدهای همین ماه.</summary>
    [RelayCommand]
    private Task PdfAsync()
    {
        var rows = Rows.Select(r => r.Entity).ToList();
        var input = new DebtReceiptReportInput(Shamsi.MonthLabel(Month), rows, DocDates.Line());
        return Documents.ShowAsync(() => new DebtReceiptReport(input),
                                   "رسید قرض‌داران " + Shamsi.MonthLabel(Month));
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
