using CommunityToolkit.Mvvm.ComponentModel;
using PumpYaqobi.App.Services;
using PumpYaqobi.Application.Localization;
using PumpYaqobi.Application.Services;
using PumpYaqobi.Domain.Entities;

namespace PumpYaqobi.App.ViewModels.Sections;

/// <summary>یک ردیفِ صرافی. «شکسته» و «الباقی» محاسبه‌اند، نه ذخیره‌شده.</summary>
public sealed partial class ExchangeRowViewModel : RowViewModel
{
    private readonly ExchangeRow _e;
    private readonly ExchangeSectionViewModel _owner;

    public ExchangeRowViewModel(ExchangeRow e, ExchangeSectionViewModel owner)
    {
        _e = e; _owner = owner;
        Loading = true;
        _dateShamsi = e.DateShamsi ?? "";
        _description = e.Description ?? "";
        _amount = e.Amount;
        _currency = e.Currency;
        _rate = e.Rate;
        _bardagi = e.Bardagi;
        Loading = false;
    }

    public ExchangeRow Entity => _e;

    [ObservableProperty] private string _dateShamsi = "";
    [ObservableProperty] private string _description = "";
    [ObservableProperty] private decimal _amount;
    [ObservableProperty] private ExchangeCurrency _currency;
    [ObservableProperty] private decimal _rate;
    [ObservableProperty] private decimal _bardagi;

    partial void OnDateShamsiChanged(string v) => Touch();
    partial void OnDescriptionChanged(string v) => Touch();
    partial void OnAmountChanged(decimal v) { Touch(); Refresh(); }
    partial void OnCurrencyChanged(ExchangeCurrency v) { Touch(); OnPropertyChanged(nameof(CurrencyText)); }
    partial void OnRateChanged(decimal v) { Touch(); Refresh(); }
    partial void OnBardagiChanged(decimal v) { Touch(); Refresh(); }

    private void Refresh()
    {
        OnPropertyChanged(nameof(AmountText));
        OnPropertyChanged(nameof(RateText));
        OnPropertyChanged(nameof(BardagiText));
        OnPropertyChanged(nameof(UsdText));
        OnPropertyChanged(nameof(BaqiText));
    }

    public string AmountText  { get => Shamsi.Money(Amount);  set => Amount = Shamsi.Num(value); }
    public string RateText    { get => Shamsi.Money(Rate);    set => Rate = Shamsi.Num(value); }
    public string BardagiText { get => Shamsi.Money(Bardagi); set => Bardagi = Shamsi.Num(value); }

    /// <summary>دالرِ شکسته — مبلغ ÷ فی.</summary>
    public string UsdText => Shamsi.Money(Math.Round(_owner.Calc.ToUsd(_e), 2));
    /// <summary>الباقی — شکسته − بردگی.</summary>
    public string BaqiText => Shamsi.Money(Math.Round(_owner.Calc.RowBaqi(_e), 2));

    public string CurrencyText
    {
        get => Currency switch
        {
            ExchangeCurrency.Toman => "تومان",
            ExchangeCurrency.Kaldar => "کلدار",
            _ => "افغانی",
        };
        set => Currency = value switch
        {
            "تومان" => ExchangeCurrency.Toman,
            "کلدار" => ExchangeCurrency.Kaldar,
            _ => ExchangeCurrency.Afghani,
        };
    }

    protected override void Apply()
    {
        _e.DateShamsi = DateShamsi;
        _e.Description = Description;
        _e.Amount = Amount;
        _e.Currency = Currency;
        _e.Rate = Rate;
        _e.Bardagi = Bardagi;
    }

    protected override Task SaveAsync() => _owner.SaveEntityAsync(_e);
}

/// <summary>
/// ══ بخشِ صرافی ═════════════════════════════════════════════════════════════
/// همهٔ عددهای این صفحه دالرند: مبلغ به ارزِ خودش وارد می‌شود و با «فی»
/// شکسته می‌شود. فیِ صفر ⇒ صفر (نه بی‌نهایت) — مثلِ خودِ نسخهٔ وب.
/// </summary>
public sealed partial class ExchangeSectionViewModel
    : LedgerSectionViewModel<ExchangeRowViewModel, ExchangeRow>
{
    public ExchangeSectionViewModel(AppHost host)
        : base("sarrafi", "sarrafi", "صرافی", host.ExchangeLedger)
        => Calc = host.Exchange;

    internal ExchangeService Calc { get; }

    [ObservableProperty] private ExchangeSummary _summary;

    public string TotalUsd => Shamsi.Money(Math.Round(Summary.TotalUsd, 2));
    public string TotalBardagi => Shamsi.Money(Math.Round(Summary.TotalBardagi, 2));
    public string TotalBardagiUsd => Shamsi.Money(Math.Round(Summary.TotalBardagiUsd, 2));
    public string Baqi => Shamsi.Money(Math.Round(Summary.Baqi, 2));

    partial void OnSummaryChanged(ExchangeSummary v)
    {
        OnPropertyChanged(nameof(TotalUsd)); OnPropertyChanged(nameof(TotalBardagi));
        OnPropertyChanged(nameof(TotalBardagiUsd)); OnPropertyChanged(nameof(Baqi));
    }

    protected override ExchangeRowViewModel Wrap(ExchangeRow e) => new(e, this);
    protected override long EntityIdOf(ExchangeRowViewModel r) => r.Entity.Id;

    protected override void Recalc() => Summary = Calc.Summarize(Rows.Select(r => r.Entity));
}
