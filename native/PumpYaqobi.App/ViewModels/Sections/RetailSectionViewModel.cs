using CommunityToolkit.Mvvm.ComponentModel;
using PumpYaqobi.App.Services;
using PumpYaqobi.Application.Localization;
using PumpYaqobi.Application.Services;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;

namespace PumpYaqobi.App.ViewModels.Sections;

/// <summary>یک ردیفِ چکنه. «بردگی» و «الباقی» محاسبه‌اند مگر ردیفِ پولی.</summary>
public sealed partial class RetailRowViewModel : RowViewModel
{
    private readonly RetailRow _e;
    private readonly RetailSectionViewModel _owner;

    public RetailRowViewModel(RetailRow e, RetailSectionViewModel owner)
    {
        _e = e; _owner = owner;
        Loading = true;
        _dateShamsi = e.DateShamsi ?? "";
        _name = e.Name ?? "";
        _fuel = e.Fuel;
        _liters = e.Liters;
        _pricePerLiter = e.PricePerLiter;
        _byMoney = e.ByMoney;
        _manualBardagi = e.Bardagi;
        _rasid = e.Rasid;
        _note = e.Note ?? "";
        Loading = false;
    }

    public RetailRow Entity => _e;

    [ObservableProperty] private string _dateShamsi = "";
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private FuelType _fuel;
    [ObservableProperty] private decimal _liters;
    [ObservableProperty] private decimal _pricePerLiter;
    [ObservableProperty] private bool _byMoney;
    [ObservableProperty] private decimal _manualBardagi;
    [ObservableProperty] private decimal _rasid;
    [ObservableProperty] private string _note = "";

    partial void OnDateShamsiChanged(string v) => Touch();
    partial void OnNameChanged(string v) => Touch();
    partial void OnFuelChanged(FuelType v) { Touch(); OnPropertyChanged(nameof(FuelText)); }
    // ⚠️ همان قاعدهٔ ‎updateChakana‎: نوشتن در «مقدار بردگی» یعنی حالتِ دستی،
    // و نوشتنِ دوبارهٔ تیل/فی حالتِ خودکار (تیل×فی) را برمی‌گرداند.
    partial void OnLitersChanged(decimal v) { Touch(); AutoIfMeasured(); Refresh(); }
    partial void OnPricePerLiterChanged(decimal v) { Touch(); AutoIfMeasured(); Refresh(); }
    partial void OnByMoneyChanged(bool v) { Touch(); Refresh(); }
    partial void OnManualBardagiChanged(decimal v) { Touch(); Refresh(); }

    private void AutoIfMeasured()
    {
        if (Liters > 0m && (PricePerLiter > 0m || ManualBardagi == 0m)) ByMoney = false;
    }
    partial void OnRasidChanged(decimal v) { Touch(); Refresh(); }
    partial void OnNoteChanged(string v) => Touch();

    private void Refresh()
    {
        OnPropertyChanged(nameof(LitersText)); OnPropertyChanged(nameof(PriceText));
        OnPropertyChanged(nameof(ManualBardagiText)); OnPropertyChanged(nameof(RasidText));
        OnPropertyChanged(nameof(BardagiText)); OnPropertyChanged(nameof(AlbaqiText));
    }

    public string LitersText { get => Shamsi.Money(Liters); set => Liters = Shamsi.Num(value); }
    public string PriceText { get => Shamsi.Money(PricePerLiter); set => PricePerLiter = Shamsi.Num(value); }
    public string ManualBardagiText { get => Shamsi.Money(ManualBardagi); set => ManualBardagi = Shamsi.Num(value); }
    public string RasidText { get => Shamsi.Money(Rasid); set => Rasid = Shamsi.Num(value); }

    /// <summary>
    /// «مقدار بردگی» — یک خانهٔ ویرایش‌پذیر، درست مثلِ نسخهٔ وب: خوانده‌شدنش
    /// عددِ محاسبه‌شده است و نوشتنِ دستی رویش، ردیف را «پولی» می‌کند.
    /// </summary>
    public string BardagiText
    {
        get => Shamsi.Money(_owner.Calc.Bardagi(_e));
        set { ManualBardagi = Shamsi.Num(value); ByMoney = true; }
    }
    public string AlbaqiText => Shamsi.Money(_owner.Calc.Albaqi(_e));

    public string FuelText
    {
        get => Fuel.ToPersian();
        set => Fuel = value == "دیزل" ? FuelType.Diesel : FuelType.Petrol;
    }

    protected override void Apply()
    {
        _e.DateShamsi = DateShamsi;
        _e.Name = Name;
        _e.Fuel = Fuel;
        _e.Liters = Liters;
        _e.PricePerLiter = PricePerLiter;
        _e.ByMoney = ByMoney;
        _e.Bardagi = ManualBardagi;
        _e.Rasid = Rasid;
        _e.Note = Note;
    }

    protected override Task SaveAsync() => _owner.SaveEntityAsync(_e);
}

/// <summary>
/// ══ بخشِ چکنه ══════════════════════════════════════════════════════════════
/// فروشِ خرد. ردیفِ «به پول» بردگی‌اش همان عددِ نوشته‌شده است، نه لیتر×فی —
/// همان قاعده‌ای که آزمونِ برابری با نسخهٔ وب رویش ایستاده.
/// </summary>
public sealed partial class RetailSectionViewModel
    : LedgerSectionViewModel<RetailRowViewModel, RetailRow>
{
    public RetailSectionViewModel(AppHost host)
        : base("chakana", "debtrasid", "چکنه", host.RetailLedger)
        => Calc = host.Retail;

    internal RetailService Calc { get; }

    [ObservableProperty] private RetailSummary _summary;

    public string TotalLiters => Shamsi.Money(Summary.Liters);
    public string TotalBardagi => Shamsi.Money(Summary.Bardagi);
    public string TotalRasid => Shamsi.Money(Summary.Rasid);
    public string TotalAlbaqi => Shamsi.Money(Summary.Albaqi);

    partial void OnSummaryChanged(RetailSummary v)
    {
        OnPropertyChanged(nameof(TotalLiters)); OnPropertyChanged(nameof(TotalBardagi));
        OnPropertyChanged(nameof(TotalRasid)); OnPropertyChanged(nameof(TotalAlbaqi));
    }

    protected override RetailRowViewModel Wrap(RetailRow e) => new(e, this);
    protected override long EntityIdOf(RetailRowViewModel r) => r.Entity.Id;
    protected override RetailRow EntityOf(RetailRowViewModel r) => r.Entity;
    protected override void Recalc() => Summary = Calc.Summarize(Rows.Select(r => r.Entity));
}
