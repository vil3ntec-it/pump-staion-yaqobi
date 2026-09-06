using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PumpYaqobi.App.Services;
using PumpYaqobi.Application.Localization;
using PumpYaqobi.Application.Services;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;

namespace PumpYaqobi.App.ViewModels.Sections;

/// <summary>یک خریدِ تیل روی جدول.</summary>
public sealed partial class PurchaseRowViewModel : RowViewModel
{
    private readonly FuelPurchase _p;
    private readonly StorageSectionViewModel _owner;

    public PurchaseRowViewModel(FuelPurchase p, StorageSectionViewModel owner)
    {
        _p = p; _owner = owner;
        Loading = true;
        _dateShamsi = p.DateShamsi ?? ""; _seller = p.Seller ?? "";
        _kg = p.Kg; _density = p.Density; _priceTon = p.PriceTon; _usdRate = p.UsdRate;
        _note = p.Note ?? "";
        Loading = false;
    }

    public FuelPurchase Entity => _p;

    [ObservableProperty] private string _dateShamsi = "";
    [ObservableProperty] private string _seller = "";
    [ObservableProperty] private decimal _kg;
    [ObservableProperty] private decimal _density;
    [ObservableProperty] private decimal _priceTon;
    [ObservableProperty] private decimal _usdRate;
    [ObservableProperty] private string _note = "";

    partial void OnDateShamsiChanged(string v) => Touch();
    partial void OnSellerChanged(string v) => Touch();
    partial void OnKgChanged(decimal v) { Touch(); Refresh(); }
    partial void OnDensityChanged(decimal v) { Touch(); Refresh(); }
    partial void OnPriceTonChanged(decimal v) { Touch(); Refresh(); }
    partial void OnUsdRateChanged(decimal v) { Touch(); Refresh(); }
    partial void OnNoteChanged(string v) => Touch();

    private void Refresh()
    {
        foreach (var n in new[] { nameof(KgText), nameof(DensityText), nameof(PriceTonText),
                                  nameof(UsdRateText), nameof(TonText), nameof(LitersText),
                                  nameof(TotalUsdText), nameof(TotalAfnText), nameof(PerLiterText) })
            OnPropertyChanged(n);
    }

    public string KgText { get => Shamsi.Money(Kg); set => Kg = Shamsi.Num(value); }
    public string DensityText { get => Shamsi.Money(Density); set => Density = Shamsi.Num(value); }
    public string PriceTonText { get => Shamsi.Money(PriceTon); set => PriceTon = Shamsi.Num(value); }
    public string UsdRateText { get => Shamsi.Money(UsdRate); set => UsdRate = Shamsi.Num(value); }

    private PurchaseNumbers N => _owner.Calc.Compute(Kg, Density, PriceTon, UsdRate);

    /// <summary>«⛽ خرید #۱» — شمارهٔ کارت در فهرست.</summary>
    public int Index { get; set; }
    public string HeadText => $"خرید #{Index}";

    public string TonText => Shamsi.Money(Math.Round(N.Ton, 3));
    public string LitersText => Shamsi.Money(Math.Round(N.Liters, 0, MidpointRounding.AwayFromZero));
    public string TotalUsdText => Shamsi.Money(Math.Round(N.TotalUsd, 2));
    public string TotalAfnText => Shamsi.Money(Math.Round(N.TotalAfn, 0, MidpointRounding.AwayFromZero));
    public string PerLiterText => Shamsi.Money(Math.Round(N.PerLiter, 2));

    protected override void Apply()
    {
        _p.DateShamsi = DateShamsi; _p.Seller = Seller; _p.Kg = Kg; _p.Density = Density;
        _p.PriceTon = PriceTon; _p.UsdRate = UsdRate; _p.Note = Note;
    }

    protected override Task SaveAsync() => _owner.SavePurchaseAsync(_p);
}

/// <summary>یک میله‌زنیِ مخزن.</summary>
public sealed partial class DipRowViewModel : RowViewModel
{
    private readonly TankDip _d;
    private readonly StorageSectionViewModel _owner;

    public DipRowViewModel(TankDip d, StorageSectionViewModel owner)
    {
        _d = d; _owner = owner;
        Loading = true;
        _dateShamsi = d.DateShamsi ?? ""; _measured = d.Measured; _expected = d.Expected;
        _note = d.Note ?? ""; _applyToBook = d.BookAdjust != 0m;
        Loading = false;
    }

    public TankDip Entity => _d;

    [ObservableProperty] private string _dateShamsi = "";
    [ObservableProperty] private decimal _measured;
    [ObservableProperty] private decimal _expected;
    [ObservableProperty] private string _note = "";

    /// <summary>
    /// «دفتر برابر شود» — تیکِ همان کادرِ ‎dip-apply‎ی نسخهٔ وب. با زدنش،
    /// اختلافِ همین میله‌زنی در موجودیِ مخزن شمرده می‌شود.
    /// </summary>
    [ObservableProperty] private bool _applyToBook;

    partial void OnDateShamsiChanged(string v) => Touch();
    partial void OnMeasuredChanged(decimal v) { Touch(); Refresh(); }
    partial void OnExpectedChanged(decimal v) { Touch(); Refresh(); }
    partial void OnNoteChanged(string v) => Touch();
    partial void OnApplyToBookChanged(bool v) { Touch(); Refresh(); }

    private void Refresh()
    {
        OnPropertyChanged(nameof(MeasuredText));
        OnPropertyChanged(nameof(ExpectedText));
        OnPropertyChanged(nameof(DiffText));
        OnPropertyChanged(nameof(BookAdjustText));
    }

    /// <summary>چقدر از این میله‌زنی واقعاً به دفتر رفت — صفر یعنی هیچ.</summary>
    public string BookAdjustText => ApplyToBook ? Shamsi.Money(Measured - Expected) : "—";

    public string MeasuredText { get => Shamsi.Money(Measured); set => Measured = Shamsi.Num(value); }
    public string ExpectedText { get => Shamsi.Money(Expected); set => Expected = Shamsi.Num(value); }

    /// <summary>مثبت یعنی مخزن بیشتر از دفتر دارد.</summary>
    public string DiffText => Shamsi.Money(Measured - Expected);

    protected override void Apply()
    {
        _d.DateShamsi = DateShamsi; _d.Measured = Measured; _d.Expected = Expected; _d.Note = Note;
        _d.BookAdjust = ApplyToBook ? Measured - Expected : 0m;
    }

    protected override Task SaveAsync() => _owner.SaveDipAsync(_d);
}

/// <summary>
/// ══ بخشِ مخزن ══════════════════════════════════════════════════════════════
/// موجودیِ مخزن، خریدهای تیل و میله‌زنی — برای پطرول و دیزل، هر کدام جدا.
/// موجودی = مجموعِ لیترِ خریدها − مجموعِ فروشِ پارچه‌های همان سوخت.
/// </summary>
public sealed partial class StorageSectionViewModel : SectionViewModel
{
    private readonly AppHost _host;

    public StorageSectionViewModel(AppHost host) : base("storage", "storage", "مخزن")
        => _host = host;

    internal StorageService Calc => _host.Storage;

    public ObservableCollection<PurchaseRowViewModel> Purchases { get; } = new();
    public ObservableCollection<DipRowViewModel> Dips { get; } = new();

    [ObservableProperty] private bool _isDiesel;
    [ObservableProperty] private string _current = "";
    [ObservableProperty] private string _totalIn = "";
    [ObservableProperty] private string _totalOut = "";
    [ObservableProperty] private string _totalAfn = "";
    [ObservableProperty] private string _totalUsd = "";
    [ObservableProperty] private string _perLiter = "";
    [ObservableProperty] private string _lastBuyDate = "—";
    [ObservableProperty] private bool _isLow;
    [ObservableProperty] private string _capacity = "";
    [ObservableProperty] private double _fillPercent;
    [ObservableProperty] private string _fillText = "0%";
    [ObservableProperty] private string _thresholdText = "";

    public FuelType Fuel => IsDiesel ? FuelType.Diesel : FuelType.Petrol;
    public string FuelLabel => IsDiesel ? "دیزل" : "پطرول";
    public string TankTitle => (IsDiesel ? "🟤 مخزن " : "⛽ مخزن ") + FuelLabel;
    public string CurrentTitle => "موجودی فعلی مخزن (" + FuelLabel + ")";
    public string MoneyTitle => "💰 خلاصه پول‌ها — " + FuelLabel;
    public string AddBuyText => "➕ ثبت خرید " + FuelLabel;
    public string PdfText => "📄 PDF مخزن " + FuelLabel;
    public string FuelToggleText => IsDiesel ? "⛽ رفتن به مخزن پطرول" : "🟤 رفتن به مخزن دیزل";
    public string StateText => IsLow ? "کمبودِ موجودی" : "موجودی کافی";

    /// <summary>ظرفیتِ مخزن — تنظیمی است و روی نوارِ پرشدگی اثر می‌گذارد.</summary>
    private string CapacityKey => IsDiesel ? "tankCapacity_diesel" : "tankCapacity_petrol";

    partial void OnIsDieselChanged(bool v)
    {
        foreach (var n in new[] { nameof(FuelLabel), nameof(TankTitle), nameof(CurrentTitle),
                                  nameof(MoneyTitle), nameof(AddBuyText), nameof(PdfText),
                                  nameof(FuelToggleText) })
            OnPropertyChanged(n);
        _ = LoadAsync();
    }

    partial void OnCapacityChanged(string v)
    {
        _host.Settings.Set(CapacityKey, Shamsi.Num(v));
        _ = RecalcAsync();
    }

    partial void OnIsLowChanged(bool v) => OnPropertyChanged(nameof(StateText));

    [RelayCommand]
    private void ToggleFuel() => IsDiesel = !IsDiesel;

    protected override async Task LoadAsync()
    {
        var buys = await _host.StorageData.PurchasesAsync(Fuel);
        Purchases.Clear();
        foreach (var p in buys) Purchases.Add(Track(new PurchaseRowViewModel(p, this)));

        for (var i = 0; i < Purchases.Count; i++) Purchases[i].Index = Purchases.Count - i;

        var dips = await _host.StorageData.DipsAsync(Fuel);
        Dips.Clear();
        foreach (var d in dips) Dips.Add(new DipRowViewModel(d, this));

        Capacity = Shamsi.Money(_host.Settings.GetDecimal(CapacityKey, 10000m));
        await RecalcAsync();
    }

    private PurchaseRowViewModel Track(PurchaseRowViewModel r)
    {
        r.Recalculated += () => _ = RecalcAsync();
        return r;
    }

    private async Task RecalcAsync()
    {
        var reports = await _host.StorageData.ReportsAsync(Fuel);
        var threshold = _host.Settings.GetDecimal(
            PumpYaqobi.Services.Data.SettingsService.LowStockThreshold, 1000m);
        // میله‌زنی‌ها هم به موجودی می‌رسند: «برابر کردنِ دفتر با عددِ واقعی»
        var t = Calc.Tank(Purchases.Select(p => p.Entity), reports, threshold,
                          Dips.Select(d => d.Entity));

        Current = Shamsi.Money(Math.Round(t.Display, 0, MidpointRounding.AwayFromZero));
        TotalIn = Shamsi.Money(Math.Round(t.In, 0, MidpointRounding.AwayFromZero));
        TotalOut = Shamsi.Money(Math.Round(t.Out, 0, MidpointRounding.AwayFromZero));
        TotalAfn = Shamsi.Money(Math.Round(t.TotalAfn, 0, MidpointRounding.AwayFromZero));
        TotalUsd = Shamsi.Money(Math.Round(t.TotalUsd, 2));
        IsLow = t.IsLow;
        ThresholdText = "حد هشدار: " + Shamsi.Money(threshold) + " لیتر";

        // فیِ لیترِ خرید و تاریخِ آخرین خرید — همان دو عددِ «خلاصه پول‌ها»
        var last = Purchases.FirstOrDefault();
        PerLiter = Shamsi.Money(Math.Round(
            _host.Settings.GetDecimal(Fuel == FuelType.Diesel
                ? PumpYaqobi.Services.Data.SettingsService.BuyPerLiterDiesel
                : PumpYaqobi.Services.Data.SettingsService.BuyPerLiterPetrol), 1));
        LastBuyDate = last?.DateShamsi ?? "—";

        // نوارِ پرشدگی: نسبتِ موجودی به ظرفیت، سقفِ صد درصد
        var cap = Shamsi.Num(Capacity);
        FillPercent = cap > 0m ? (double)Math.Min(100m, Math.Max(0m, t.Display / cap * 100m)) : 0;
        FillText = Math.Round(FillPercent) + "%";
    }

    public async Task SavePurchaseAsync(FuelPurchase p)
    {
        await _host.StorageData.UpdatePurchaseAsync(p);
        await RecalcAsync();
    }

    public Task SaveDipAsync(TankDip d) => _host.StorageData.SaveDipAsync(d);

    [RelayCommand]
    private async Task AddPurchaseAsync()
    {
        var p = new FuelPurchase { Fuel = Fuel, DateShamsi = Shamsi.Today(), Density = 0.75m };
        await _host.StorageData.AddPurchaseAsync(p);
        Purchases.Insert(0, Track(new PurchaseRowViewModel(p, this)));
        await RecalcAsync();
    }

    [RelayCommand]
    private async Task DeletePurchaseAsync(PurchaseRowViewModel? row)
    {
        if (row is null) return;
        await _host.StorageData.DeletePurchaseAsync(row.Entity.Id);
        Purchases.Remove(row);
        await RecalcAsync();
    }

    [RelayCommand]
    private async Task AddDipAsync()
    {
        var d = new TankDip { Fuel = Fuel, DateShamsi = Shamsi.Today() };
        await _host.StorageData.SaveDipAsync(d);
        Dips.Insert(0, new DipRowViewModel(d, this));
    }
}
