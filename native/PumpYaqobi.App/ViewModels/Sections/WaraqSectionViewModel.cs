using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PumpYaqobi.App.Services;
using PumpYaqobi.Application.Localization;
using PumpYaqobi.Application.Services;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;

namespace PumpYaqobi.App.ViewModels.Sections;

/// <summary>یک «پایه» (نازل) در ورق.</summary>
public sealed partial class WaraqPumpViewModel : RowViewModel
{
    private readonly WaraqPump _p;
    private readonly WaraqPageViewModel _owner;

    public WaraqPumpViewModel(WaraqPump p, WaraqPageViewModel owner)
    {
        _p = p; _owner = owner;
        Loading = true;
        _num = p.Num; _fuel = p.Fuel; _start = p.Start; _end = p.End;
        _price = p.PricePerLiter; _debt = p.Debt; _note = p.Note ?? "";
        _worker = p.Worker ?? ""; _pumpDate = p.DateShamsi ?? "";
        Loading = false;
    }

    public WaraqPump Entity => _p;

    [ObservableProperty] private int _num;
    [ObservableProperty] private FuelType _fuel;
    [ObservableProperty] private decimal _start;
    [ObservableProperty] private decimal _end;
    [ObservableProperty] private decimal _price;
    [ObservableProperty] private decimal _debt;
    [ObservableProperty] private string _note = "";

    /// <summary>ستونِ «نام» — کارمندِ همین پایه، مثلِ جدولِ ورق در نسخهٔ وب.</summary>
    [ObservableProperty] private string _worker = "";

    /// <summary>ستونِ «تاریخ» — خالی یعنی همان تاریخِ ورق.</summary>
    [ObservableProperty] private string _pumpDate = "";

    partial void OnWorkerChanged(string v) => Touch();
    partial void OnPumpDateChanged(string v) => Touch();
    partial void OnNumChanged(int v) => Touch();
    partial void OnFuelChanged(FuelType v) { Touch(); OnPropertyChanged(nameof(FuelText)); }
    partial void OnStartChanged(decimal v) { Touch(); Refresh(); }
    partial void OnEndChanged(decimal v) { Touch(); Refresh(); }
    partial void OnPriceChanged(decimal v) { Touch(); Refresh(); }
    partial void OnDebtChanged(decimal v) { Touch(); Refresh(); }
    partial void OnNoteChanged(string v) => Touch();

    private void Refresh()
    {
        foreach (var n in new[] { nameof(StartText), nameof(EndText), nameof(PriceText),
                                  nameof(DebtText), nameof(LitersText), nameof(SalesText) })
            OnPropertyChanged(n);
    }

    public string StartText { get => Shamsi.Money(Start); set => Start = Shamsi.Num(value); }
    public string EndText { get => Shamsi.Money(End); set => End = Shamsi.Num(value); }
    public string PriceText { get => Shamsi.Money(Price); set => Price = Shamsi.Num(value); }
    public string DebtText { get => Shamsi.Money(Debt); set => Debt = Shamsi.Num(value); }

    /// <summary>لیترِ منفی وجود ندارد — ‎Math.max(0, end−start)‎.</summary>
    public decimal Liters => Math.Max(0m, End - Start);
    public string LitersText => Shamsi.Money(Liters);
    public string SalesText => Shamsi.Money(Liters * Price);

    public string FuelText
    {
        get => Fuel.ToPersian();
        set => Fuel = value == "دیزل" ? FuelType.Diesel : FuelType.Petrol;
    }

    protected override void Apply()
    {
        _p.Num = Num; _p.Fuel = Fuel; _p.Start = Start; _p.End = End;
        _p.PricePerLiter = Price; _p.Debt = Debt; _p.Note = Note;
        _p.Worker = Worker; _p.DateShamsi = PumpDate;
    }

    protected override Task SaveAsync() => _owner.SavePumpAsync(_p);
}

/// <summary>یک ردیفِ «قرض/مصرف» در ورق.</summary>
public sealed partial class WaraqTxnViewModel : RowViewModel
{
    private readonly WaraqTransaction _t;
    private readonly WaraqPageViewModel _owner;

    public WaraqTxnViewModel(WaraqTransaction t, WaraqPageViewModel owner)
    {
        _t = t; _owner = owner;
        Loading = true;
        _name = t.Name ?? ""; _liters = t.Liters; _amount = t.Amount;
        _isExpense = t.Type == WaraqTxnType.Expense; _fuel = t.Fuel;
        Loading = false;
    }

    public WaraqTransaction Entity => _t;

    [ObservableProperty] private string _name = "";
    [ObservableProperty] private decimal _liters;
    [ObservableProperty] private decimal _amount;
    [ObservableProperty] private bool _isExpense;
    [ObservableProperty] private FuelType _fuel;

    partial void OnNameChanged(string v) => Touch();
    partial void OnLitersChanged(decimal v) { _t.AmountAuto ??= true; Touch(); Refresh(); }
    partial void OnIsExpenseChanged(bool v) { Touch(); OnPropertyChanged(nameof(TypeText)); }
    partial void OnFuelChanged(FuelType v) { Touch(); OnPropertyChanged(nameof(FuelText)); }

    /// <summary>مبلغی که کاربر خودش بنویسد دیگر خودکار نیست و بازحساب نمی‌شود.</summary>
    partial void OnAmountChanged(decimal v)
    {
        if (!Loading) _t.AmountAuto = false;
        Touch(); Refresh();
    }

    private void Refresh()
    {
        OnPropertyChanged(nameof(LitersText));
        OnPropertyChanged(nameof(AmountText));
        OnPropertyChanged(nameof(EffectiveAmountText));
    }

    public string LitersText { get => Shamsi.Money(Liters); set => Liters = Shamsi.Num(value); }
    public string AmountText { get => Shamsi.Money(Amount); set => Amount = Shamsi.Num(value); }

    /// <summary>مبلغی که واقعاً در جمع‌ها شمرده می‌شود.</summary>
    public string EffectiveAmountText => Shamsi.Money(_owner.Calc.TxnAmount(_owner.Shift!, _t));

    /// <summary>
    /// پس از تازه‌شدنِ فیِ ورق، مبلغِ مؤثر دوباره خوانده شود — و اگر مبلغ
    /// خودکار است، خانهٔ «مبلغ» هم همان عددِ خودکار را نشان دهد (نه صفر).
    /// </summary>
    public void RefreshEffective()
    {
        if (_t.AmountAuto == true && _amount != _t.Amount)
        {
            _amount = _t.Amount;                 // بی‌آنکه ذخیره‌ای راه بیفتد
            OnPropertyChanged(nameof(Amount));
            OnPropertyChanged(nameof(AmountText));
        }
        OnPropertyChanged(nameof(EffectiveAmountText));
    }

    public string TypeText
    {
        get => IsExpense ? "مصرف" : "قرض";
        set => IsExpense = value == "مصرف";
    }

    public string FuelText
    {
        get => Fuel.ToPersian();
        set => Fuel = value == "دیزل" ? FuelType.Diesel : FuelType.Petrol;
    }

    protected override void Apply()
    {
        _t.Name = Name; _t.Liters = Liters; _t.Amount = Amount;
        _t.Type = IsExpense ? WaraqTxnType.Expense : WaraqTxnType.Debt;
        _t.Fuel = Fuel;
    }

    protected override Task SaveAsync() => _owner.SaveTxnAsync(_t);
}

/// <summary>صفحهٔ یک ورق — شیفتِ روز و شب، پایه‌ها و ردیف‌های قرض/مصرف.</summary>
public sealed partial class WaraqPageViewModel : ObservableObject, IRowBatchHost
{
    private readonly AppHost _host;
    private readonly WaraqSectionViewModel _section;

    public WaraqPageViewModel(AppHost host, WaraqEntry w, WaraqSectionViewModel section)
    {
        _host = host; _section = section; Entity = w;
        _isNight = w.ActiveShift == ShiftKind.Night;
        Build();
    }

    public WaraqEntry Entity { get; }
    public WaraqService Calc => _host.Waraq;
    public string Title => "ورقِ " + (Entity.DateShamsi ?? "");

    public ObservableCollection<WaraqPumpViewModel> Pumps { get; } = new();
    public ObservableCollection<WaraqTxnViewModel> Txns { get; } = new();

    [ObservableProperty] private bool _isNight;
    [ObservableProperty] private string _workerName = "";
    [ObservableProperty] private decimal _fabricDebt;
    [ObservableProperty] private string _petrolLiters = "";
    [ObservableProperty] private string _dieselLiters = "";
    [ObservableProperty] private string _sales = "";
    [ObservableProperty] private string _debt = "";
    [ObservableProperty] private string _expenses = "";
    [ObservableProperty] private string _shortage = "";
    [ObservableProperty] private string _shortageLabel = "";

    public WaraqShift? Shift =>
        Entity.Shifts.FirstOrDefault(s => s.Kind == (IsNight ? ShiftKind.Night : ShiftKind.Day));

    public string FabricDebtText
    {
        get => Shamsi.Money(FabricDebt);
        set => FabricDebt = Shamsi.Num(value);
    }

    partial void OnIsNightChanged(bool v) => Build();

    /// <summary>
    /// ⚠️ هنگامِ پر کردنِ اولیهٔ کادرها هیچ چیزی ذخیره نمی‌شود.
    /// بدونِ این، نشستنِ «کارمندِ شیفت» یک ذخیره راه می‌انداخت که «قرضِ پارچه»
    /// را با مقدارِ هنوز-پرنشده (صفر) روی دیتابیس می‌نوشت و عددِ واقعی پاک می‌شد.
    /// </summary>
    private bool _filling;

    partial void OnWorkerNameChanged(string v) => SaveShift();

    partial void OnFabricDebtChanged(decimal v)
    {
        OnPropertyChanged(nameof(FabricDebtText));
        SaveShift();
    }

    private void SaveShift()
    {
        if (_filling) return;
        var sd = Shift;
        if (sd is null) return;
        sd.WorkerName = WorkerName;
        sd.FabricDebt = FabricDebt;
        _ = _host.WaraqData.SaveShiftAsync(sd);
        Recalc();
    }

    private void Build()
    {
        var sd = Shift;
        Pumps.Clear(); Txns.Clear();
        if (sd is null) return;

        _filling = true;
        WorkerName = sd.WorkerName ?? "";
        FabricDebt = sd.FabricDebt;
        _filling = false;

        foreach (var p in sd.Pumps.OrderBy(p => p.SortIndex).ThenBy(p => p.Id))
        {
            var vm = new WaraqPumpViewModel(p, this);
            vm.Recalculated += Recalc;
            Pumps.Add(vm);
        }
        foreach (var t in sd.Transactions.OrderBy(t => t.SortIndex).ThenBy(t => t.Id))
        {
            var vm = new WaraqTxnViewModel(t, this);
            vm.Recalculated += Recalc;
            Txns.Add(vm);
        }
        Recalc();
    }

    public void Recalc()
    {
        var sd = Shift;
        if (sd is null) return;
        var t = Calc.ShiftTotals(sd);
        PetrolLiters = Shamsi.Money(t.PetrolLiters);
        DieselLiters = Shamsi.Money(t.DieselLiters);
        Sales = Shamsi.Money(Math.Round(t.Sales, 0, MidpointRounding.AwayFromZero));
        Debt = Shamsi.Money(Math.Round(t.Debt, 0, MidpointRounding.AwayFromZero));
        Expenses = Shamsi.Money(Math.Round(t.Expenses, 0, MidpointRounding.AwayFromZero));

        var sh = Calc.Shortage(t);
        if (sh.Shortage > 0) { ShortageLabel = "کمبودی"; Shortage = Shamsi.Money(Math.Round(sh.Shortage)); }
        else if (sh.Excess > 0) { ShortageLabel = "اضافی"; Shortage = Shamsi.Money(Math.Round(sh.Excess)); }
        else { ShortageLabel = "کمبودی"; Shortage = "0"; }

        foreach (var x in Txns) x.RefreshEffective();
    }

    public async Task SavePumpAsync(WaraqPump p) { await _host.WaraqData.SavePumpAsync(p); Recalc(); }
    public async Task SaveTxnAsync(WaraqTransaction t) { await _host.WaraqData.SaveTxnAsync(t); Recalc(); }

    [RelayCommand]
    private async Task AddPumpAsync()
    {
        var sd = Shift;
        if (sd is null) return;
        var p = new WaraqPump
        {
            ShiftId = sd.Id,
            SortIndex = sd.Pumps.Count,
            Num = sd.Pumps.Count + 1,
            PricePerLiter = sd.PricePerLiter,
        };
        await _host.WaraqData.SavePumpAsync(p);
        sd.Pumps.Add(p);
        var vm = new WaraqPumpViewModel(p, this);
        vm.Recalculated += Recalc;
        Pumps.Add(vm);
        Recalc();
    }

    [RelayCommand]
    private async Task DeletePumpAsync(WaraqPumpViewModel? row)
    {
        if (row is null) return;
        await _host.WaraqData.DeletePumpAsync(row.Entity.Id);
        Shift?.Pumps.Remove(row.Entity);
        Pumps.Remove(row);
        Recalc();
    }

    [RelayCommand]
    private async Task AddTxnAsync()
    {
        var sd = Shift;
        if (sd is null) return;
        var t = new WaraqTransaction { ShiftId = sd.Id, SortIndex = sd.Transactions.Count };
        await _host.WaraqData.SaveTxnAsync(t);
        sd.Transactions.Add(t);
        var vm = new WaraqTxnViewModel(t, this);
        vm.Recalculated += Recalc;
        Txns.Add(vm);
        Recalc();
    }

    [RelayCommand]
    private async Task DeleteTxnAsync(WaraqTxnViewModel? row)
    {
        if (row is null) return;
        var sd = Shift;
        if (sd is null) return;
        await _host.WaraqData.DeleteTxnAsync(row.Entity.Id);
        sd.Transactions.Remove(row.Entity);
        Txns.Remove(row);
        Recalc();
    }

    public int RowCount => Txns.Count;

    /// <summary>‎Ctrl+عدد‎ / ‎Shift+عدد‎ روی جدولِ تراکنش‌های ورقِ باز.
    /// مثلِ نسخهٔ وب دستِ‌کم یک ردیف می‌ماند و ردیفِ کافی نبود، هیچ.</summary>
    public async Task AddRowsAsync(int count)
    {
        for (var i = 0; i < count; i++) await AddTxnAsync();
    }

    public async Task DeleteRowsAsync(int count)
    {
        if (count < 1 || Txns.Count - count < 1) return;
        for (var i = 0; i < count; i++) await DeleteTxnAsync(Txns[^1]);
    }

    [RelayCommand]
    private Task BackAsync() => _section.BackCommand.ExecuteAsync(null);

    public async Task FlushAsync()
    {
        foreach (var p in Pumps.ToList()) await p.FlushAsync();
        foreach (var t in Txns.ToList()) await t.FlushAsync();
    }
}

/// <summary>
/// ══ بخشِ ورق‌های روزانه ═════════════════════════════════════════════════════
/// فهرستِ ورق‌ها و صفحهٔ هر ورق. یک ورق برای هر روز — ورقِ تکراری ساخته نمی‌شود.
/// </summary>
/// <summary>
/// کارتِ یک ورق در فهرست — مو‌به‌مو همان کارتی که <c>renderWaraqList</c>
/// می‌سازد: «☀️🌙 تاریخ»، نامِ جایگاه و کارمندان، مبلغِ فروشِ هر دو شیفت،
/// و خطِ «قرض / مصرف».
/// </summary>
public sealed class WaraqCardViewModel
{
    public WaraqCardViewModel(WaraqEntry w, WaraqService calc)
    {
        Entity = w;
        Title = "☀️🌙 " + (string.IsNullOrWhiteSpace(w.DateShamsi) ? "—" : w.DateShamsi);

        var day = w.Shifts.FirstOrDefault(s => s.Kind == ShiftKind.Day);
        var night = w.Shifts.FirstOrDefault(s => s.Kind == ShiftKind.Night);
        var d = day is null ? default : calc.ShiftTotals(day);
        var n = night is null ? default : calc.ShiftTotals(night);

        var workers = new[] { day?.WorkerName, night?.WorkerName }
            .Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
        SubText = (w.Station ?? "") + (workers.Length > 0 ? " — " + string.Join(" / ", workers) : "");

        SalesText = Money(d.Sales + n.Sales) + " افغانی";
        DebtText = "قرض: " + Money(d.Debt + n.Debt);
        ExpenseText = "مصرف: " + Money(d.Expenses + n.Expenses);
    }

    private static string Money(decimal v) =>
        Shamsi.Money(Math.Round(v, 0, MidpointRounding.AwayFromZero));

    public WaraqEntry Entity { get; }
    public string Title { get; }
    public string SubText { get; }
    public string SalesText { get; }
    public string DebtText { get; }
    public string ExpenseText { get; }
}

public sealed partial class WaraqSectionViewModel : SectionViewModel
{
    private readonly AppHost _host;

    public WaraqSectionViewModel(AppHost host) : base("waraq", "waraq", "ورق‌های روزانه")
    {
        _host = host;
        _month = Shamsi.ThisMonth();
    }

    public ObservableCollection<WaraqEntry> Sheets { get; } = new();
    /// <summary>کارت‌های همان ورق‌ها، با جمعِ هر دو شیفت.</summary>
    public ObservableCollection<WaraqCardViewModel> Cards { get; } = new();
    public bool IsEmpty => Cards.Count == 0;
    public ObservableCollection<string> Months { get; } = new();

    [ObservableProperty] private string _month;
    [ObservableProperty] private WaraqPageViewModel? _page;

    public bool IsListVisible => Page is null;

    /// <summary>ورقِ باز — تا باز است، میانبرهای ردیف به آن می‌روند نه به فهرست.</summary>
    public override object? ActivePage => Page;

    partial void OnPageChanged(WaraqPageViewModel? v) => OnPropertyChanged(nameof(IsListVisible));
    partial void OnMonthChanged(string v) => _ = ReloadAsync();

    protected override async Task LoadAsync()
    {
        Months.Clear();
        foreach (var m in await _host.WaraqData.MonthsAsync()) Months.Add(m);
        if (!Months.Contains(Month)) Months.Insert(0, Month);
        await ReloadAsync();
    }

    public async Task ReloadAsync()
    {
        Sheets.Clear();
        Cards.Clear();
        foreach (var w in await _host.WaraqData.ListAsync(Month))
        {
            Sheets.Add(w);
            Cards.Add(new WaraqCardViewModel(w, _host.Waraq));
        }
        OnPropertyChanged(nameof(IsEmpty));
    }

    [RelayCommand]
    private Task OpenCardAsync(WaraqCardViewModel? c) => OpenAsync(c?.Entity);

    [RelayCommand]
    private Task DeleteCardAsync(WaraqCardViewModel? c) => DeleteSheetAsync(c?.Entity);

    [RelayCommand]
    private async Task OpenAsync(WaraqEntry? w)
    {
        if (w is null) return;
        var full = await _host.WaraqData.LoadAsync(w.Id);
        if (full is null) return;
        Page = new WaraqPageViewModel(_host, full, this);
    }

    [RelayCommand]
    private async Task NewSheetAsync()
    {
        var station = _host.Settings.GetString(Services.SettingsKeys.StationName);
        var w = await _host.WaraqData.OpenOrCreateAsync(Shamsi.Today(), station);
        var mk = Shamsi.MonthKey(w.DateShamsi);
        if (!Months.Contains(mk)) Months.Insert(0, mk);
        Month = mk;
        await ReloadAsync();
        var full = await _host.WaraqData.LoadAsync(w.Id);
        if (full is not null) Page = new WaraqPageViewModel(_host, full, this);
    }

    [RelayCommand]
    private async Task BackAsync()
    {
        if (Page is not null) await Page.FlushAsync();
        Page = null;
        await ReloadAsync();
    }

    [RelayCommand]
    private async Task DeleteSheetAsync(WaraqEntry? w)
    {
        if (w is null) return;
        await _host.WaraqData.DeleteAsync(w.Id);
        await ReloadAsync();
    }
}
