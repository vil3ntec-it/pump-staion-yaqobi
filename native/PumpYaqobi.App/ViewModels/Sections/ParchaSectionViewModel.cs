using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PumpYaqobi.App.Services;
using PumpYaqobi.Application.Localization;
using PumpYaqobi.Application.Services;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;

namespace PumpYaqobi.App.ViewModels.Sections;

/// <summary>یک شیفتِ پارچه روی جدول (روز یا شبِ یک پارچه).</summary>
public sealed partial class ParchaShiftViewModel : RowViewModel
{
    private readonly ParchaSectionViewModel _owner;

    public ParchaShiftViewModel(ParchaReport report, ShiftKind kind, ParchaSectionViewModel owner)
    {
        _owner = owner;
        Report = report;
        Kind = kind;
        Shift = (kind == ShiftKind.Day ? report.DayShift : report.NightShift) ?? new ShiftData();

        Loading = true;
        _dateShamsi = report.DateShamsi ?? "";
        _name = Shift.Name ?? "";
        _pumpNum = Shift.PumpNum;
        _start = Shift.Start;
        _end = Shift.End;
        _price = Shift.Price;
        _profitPer = Shift.ProfitPer;
        _debt = Shift.Debt;
        _note = Shift.Note ?? "";
        Loading = false;
    }

    public ParchaReport Report { get; }
    public ShiftKind Kind { get; }
    public ShiftData Shift { get; }

    public string KindText => Kind == ShiftKind.Day ? "روز" : "شب";
    public int ReportNum => Report.ReportNum;

    [ObservableProperty] private string _dateShamsi = "";
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private int _pumpNum;
    [ObservableProperty] private decimal _start;
    [ObservableProperty] private decimal _end;
    [ObservableProperty] private decimal _price;
    [ObservableProperty] private decimal _profitPer;
    [ObservableProperty] private decimal _debt;
    [ObservableProperty] private string _note = "";

    partial void OnDateShamsiChanged(string v) => Touch();
    partial void OnNameChanged(string v) => Touch();
    partial void OnPumpNumChanged(int v) => Touch();
    partial void OnStartChanged(decimal v) { Touch(); Refresh(); }
    partial void OnEndChanged(decimal v) { Touch(); Refresh(); }
    partial void OnPriceChanged(decimal v) { Touch(); Refresh(); }
    partial void OnProfitPerChanged(decimal v) { Touch(); Refresh(); }
    partial void OnDebtChanged(decimal v) { Touch(); Refresh(); }
    partial void OnNoteChanged(string v) => Touch();

    private void Refresh()
    {
        foreach (var n in new[] { nameof(StartText), nameof(EndText), nameof(PriceText),
                                  nameof(ProfitPerText), nameof(DebtText), nameof(SaleText),
                                  nameof(MoneyText), nameof(ProfitText), nameof(AvailableText),
                                  nameof(IsInvalid) })
            OnPropertyChanged(n);
    }

    public string StartText { get => Shamsi.Money(Start); set => Start = Shamsi.Num(value); }
    public string EndText { get => Shamsi.Money(End); set => End = Shamsi.Num(value); }
    public string PriceText { get => Shamsi.Money(Price); set => Price = Shamsi.Num(value); }
    public string ProfitPerText { get => Shamsi.Money(ProfitPer); set => ProfitPer = Shamsi.Num(value); }
    public string DebtText { get => Shamsi.Money(Debt); set => Debt = Shamsi.Num(value); }

    private ShiftNumbers N => _owner.Calc.Compute(Start, End, Price, ProfitPer, Debt);

    public string SaleText => Shamsi.Money(N.Sale);
    public string MoneyText => Shamsi.Money(N.Money);
    public string ProfitText => Shamsi.Money(N.Profit);
    public string AvailableText => Shamsi.Money(N.Available);

    /// <summary>«ختمِ پایه کمتر از شروع» — همان چیزی که نسخهٔ وب اجازه نمی‌داد.</summary>
    public bool IsInvalid => End < Start;

    protected override void Apply()
    {
        Report.DateShamsi = DateShamsi;
        Shift.Name = Name;
        Shift.PumpNum = PumpNum;
        Shift.Start = Start;
        Shift.End = End;
        Shift.Price = Price;
        Shift.ProfitPer = ProfitPer;
        Shift.Debt = Debt;
        Shift.Note = Note;
        Shift.SavedAt = DateShamsi;
    }

    protected override Task SaveAsync() => _owner.SaveShiftAsync(this);
}

/// <summary>
/// ══ بخشِ پارچه‌ها ═══════════════════════════════════════════════════════════
/// هر پارچه دو شیفت دارد (روز و شب) و هر شیفت یک ردیفِ جدول است.
/// چهار عددِ حساب‌شده — فروش، پول، فایده و رسیده — از سرویسی می‌آیند که با
/// ۴۰۰ شیفتِ گرفته‌شده از خودِ نسخهٔ وب آزموده شده.
/// </summary>
public sealed partial class ParchaSectionViewModel : SectionViewModel
{
    private readonly AppHost _host;

    public ParchaSectionViewModel(AppHost host) : base("shifts", "shifts", "پارچه‌ها")
    {
        _host = host;
        _month = Shamsi.ThisMonth();
    }

    internal ParchaService Calc => _host.Parcha;

    public ObservableCollection<ParchaShiftViewModel> Rows { get; } = new();
    public ObservableCollection<string> Months { get; } = new();

    [ObservableProperty] private string _month;
    [ObservableProperty] private bool _isDiesel;
    [ObservableProperty] private string _totalSale = "";
    [ObservableProperty] private string _totalMoney = "";
    [ObservableProperty] private string _totalDebt = "";
    [ObservableProperty] private string _totalAvailable = "";
    [ObservableProperty] private string _totalProfit = "";

    public FuelType Fuel => IsDiesel ? FuelType.Diesel : FuelType.Petrol;

    partial void OnMonthChanged(string v) => _ = ReloadAsync();
    partial void OnIsDieselChanged(bool v) => _ = LoadAsync();

    protected override async Task LoadAsync()
    {
        Months.Clear();
        foreach (var m in await _host.ParchaData.MonthsAsync(Fuel)) Months.Add(m);
        if (!Months.Contains(Month)) Months.Insert(0, Month);
        await ReloadAsync();
    }

    private List<ParchaReport> _reports = new();

    private async Task ReloadAsync()
    {
        _reports = await _host.ParchaData.ListAsync(Fuel, Month);
        Rows.Clear();
        foreach (var r in _reports)
        {
            Rows.Add(Track(new ParchaShiftViewModel(r, ShiftKind.Day, this)));
            Rows.Add(Track(new ParchaShiftViewModel(r, ShiftKind.Night, this)));
        }
        Recalc();
    }

    private ParchaShiftViewModel Track(ParchaShiftViewModel r)
    {
        r.Recalculated += Recalc;
        return r;
    }

    private void Recalc()
    {
        var t = Calc.Summarize(_reports);
        TotalSale = Shamsi.Money(t.Sale);
        TotalMoney = Shamsi.Money(t.Money);
        TotalDebt = Shamsi.Money(t.Debt);
        TotalAvailable = Shamsi.Money(t.Available);
        TotalProfit = Shamsi.Money(t.Profit);
    }

    internal async Task SaveShiftAsync(ParchaShiftViewModel row)
    {
        await _host.ParchaData.SaveShiftAsync(row.Report, row.Kind, row.Shift);
        if (row.Kind == ShiftKind.Day) row.Report.DayShift = row.Shift;
        else row.Report.NightShift = row.Shift;
        Recalc();
    }

    [RelayCommand]
    private async Task AddParchaAsync()
    {
        var r = await _host.ParchaData.AddAsync(Fuel, Shamsi.Today());
        var mk = Shamsi.MonthKey(r.DateShamsi);
        if (!Months.Contains(mk)) Months.Insert(0, mk);
        if (mk != Month) { Month = mk; return; }
        _reports.Add(r);
        Rows.Add(Track(new ParchaShiftViewModel(r, ShiftKind.Day, this)));
        Rows.Add(Track(new ParchaShiftViewModel(r, ShiftKind.Night, this)));
        Recalc();
    }

    [RelayCommand]
    private async Task DeleteParchaAsync(ParchaShiftViewModel? row)
    {
        if (row is null) return;
        await _host.ParchaData.DeleteAsync(row.Report.Id);
        await ReloadAsync();
    }
}
