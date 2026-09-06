using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PumpYaqobi.App.Services;
using PumpYaqobi.Application.Localization;
using PumpYaqobi.Application.Services;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;

namespace PumpYaqobi.App.ViewModels.Sections;

/// <summary>یک ردیفِ حسابِ شرکت. همهٔ عددهای محاسبه‌ای از CompanyService می‌آیند.</summary>
public sealed partial class CompanyRowViewModel : RowViewModel
{
    private readonly CompanyRow _r;
    private readonly CompanyPageViewModel _owner;

    public CompanyRowViewModel(CompanyRow r, CompanyPageViewModel owner)
    {
        _r = r; _owner = owner;
        Loading = true;
        _dateShamsi = r.DateShamsi ?? "";
        _name = r.Name ?? "";
        _kg = r.Kg;
        _ton = r.Ton;
        _usd = r.Usd;
        _rate = r.Rate;
        _poul = r.Poul;
        _isUsdPay = r.PoulCurrency == Currency.Usd;
        Loading = false;
    }

    public CompanyRow Entity => _r;

    [ObservableProperty] private string _dateShamsi = "";
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private decimal _kg;
    [ObservableProperty] private decimal _ton;
    [ObservableProperty] private decimal _usd;
    [ObservableProperty] private decimal _rate;
    [ObservableProperty] private decimal _poul;
    [ObservableProperty] private bool _isUsdPay;

    partial void OnDateShamsiChanged(string v) => Touch();
    partial void OnNameChanged(string v) => Touch();
    partial void OnKgChanged(decimal v) { Touch(); Refresh(); }
    partial void OnTonChanged(decimal v) { Touch(); Refresh(); }
    partial void OnUsdChanged(decimal v) { Touch(); Refresh(); }
    partial void OnRateChanged(decimal v) { Touch(); Refresh(); }
    partial void OnPoulChanged(decimal v) { Touch(); Refresh(); }
    partial void OnIsUsdPayChanged(bool v) { Touch(); Refresh(); OnPropertyChanged(nameof(PoulCurrencyText)); }

    private void Refresh()
    {
        foreach (var n in new[] { nameof(KgText), nameof(TonText), nameof(UsdText), nameof(RateText),
                                  nameof(PoulText), nameof(TotalUsdText), nameof(TotalAfnText),
                                  nameof(AlbaqiAfnText), nameof(AlbaqiUsdText) })
            OnPropertyChanged(n);
        _owner.Recalc();
    }

    public string KgText { get => Shamsi.Money(Kg); set => Kg = Shamsi.Num(value); }
    public string TonText { get => Shamsi.Money(Ton); set => Ton = Shamsi.Num(value); }
    public string UsdText { get => Shamsi.Money(Usd); set => Usd = Shamsi.Num(value); }
    public string RateText { get => Shamsi.Money(Rate); set => Rate = Shamsi.Num(value); }
    public string PoulText { get => Shamsi.Money(Poul); set => Poul = Shamsi.Num(value); }

    public string PoulCurrencyText
    {
        get => IsUsdPay ? "دالر" : "افغانی";
        set => IsUsdPay = value == "دالر";
    }

    public string TotalUsdText => Shamsi.Money(Math.Round(_owner.Calc.TotalUsd(_r), 2));
    public string TotalAfnText => Shamsi.Money(Math.Round(_owner.Calc.TotalAfn(_r), 2));
    public string AlbaqiAfnText => Shamsi.Money(Math.Round(_owner.Calc.AlbaqiAfn(_r, _owner.Rate), 2));
    public string AlbaqiUsdText => Shamsi.Money(Math.Round(_owner.Calc.AlbaqiUsd(_r, _owner.Rate), 2));

    protected override void Apply()
    {
        _r.DateShamsi = DateShamsi;
        _r.DateKey = Shamsi.Key(DateShamsi);
        _r.Name = Name;
        _r.Kg = Kg;
        _r.Ton = Ton;
        _r.Usd = Usd;
        _r.Rate = Rate;
        _r.Poul = Poul;
        _r.PoulCurrency = IsUsdPay ? Currency.Usd : Currency.Afn;
    }

    protected override Task SaveAsync() => _owner.SaveRowAsync(_r);
}

/// <summary>صفحهٔ حسابِ یک شرکت — دو دفترِ جدا: پطرول و دیزل.</summary>
public sealed partial class CompanyPageViewModel : ObservableObject
{
    private readonly AppHost _host;
    private readonly CompanySectionViewModel _section;

    public CompanyPageViewModel(AppHost host, TilCompany c, CompanySectionViewModel section)
    {
        _host = host; _section = section; Entity = c;
        BuildRows();
        Recalc();
    }

    public TilCompany Entity { get; }
    public CompanyService Calc => _host.Company;
    public string Name => Entity.Name ?? "";

    public ObservableCollection<CompanyRowViewModel> Rows { get; } = new();

    [ObservableProperty] private bool _isDiesel;
    [ObservableProperty] private string _totalUsd = "";
    [ObservableProperty] private string _totalAfn = "";
    [ObservableProperty] private string _paidAfn = "";
    [ObservableProperty] private string _albaqiAfn = "";
    [ObservableProperty] private string _albaqiUsd = "";
    [ObservableProperty] private string _convRate = "";

    public FuelType Fuel => IsDiesel ? FuelType.Diesel : FuelType.Petrol;

    /// <summary>نرخِ تبدیلِ مؤثرِ همین دفتر — پایهٔ تبدیلِ رسیدها.</summary>
    public decimal Rate { get; private set; }

    partial void OnIsDieselChanged(bool v) { BuildRows(); Recalc(); }

    private void BuildRows()
    {
        Rows.Clear();
        foreach (var r in CompanyService.RowsOf(Entity, Fuel))
            Rows.Add(new CompanyRowViewModel(r, this));
    }

    public void Recalc()
    {
        var rows = CompanyService.RowsOf(Entity, Fuel).ToList();
        var s = Calc.Summarize(Entity, rows);
        Rate = s.ConvRate;
        TotalUsd = Shamsi.Money(Math.Round(s.TotalUsd, 2));
        TotalAfn = Shamsi.Money(Math.Round(s.TotalAfn, 2));
        PaidAfn = Shamsi.Money(Math.Round(s.PaidAfn, 2));
        AlbaqiAfn = Shamsi.Money(Math.Round(s.AlbaqiAfn, 2));
        AlbaqiUsd = Shamsi.Money(Math.Round(s.AlbaqiUsd, 2));
        ConvRate = Shamsi.Money(Math.Round(s.ConvRate, 4));
    }

    public async Task SaveRowAsync(CompanyRow r)
    {
        await _host.Companies.SaveRowAsync(r);
        Recalc();
    }

    [RelayCommand]
    private async Task AddRowAsync()
    {
        var r = new CompanyRow
        {
            CompanyId = Entity.Id,
            Fuel = Fuel,
            SortIndex = CompanyService.RowsOf(Entity, Fuel).Count(),
            DateShamsi = Shamsi.Today(),
            DateKey = Shamsi.Key(Shamsi.Today()),
        };
        await _host.Companies.SaveRowAsync(r);
        Entity.Rows.Add(r);
        Rows.Add(new CompanyRowViewModel(r, this));
        Recalc();
    }

    [RelayCommand]
    private async Task DeleteRowAsync(CompanyRowViewModel? row)
    {
        if (row is null) return;
        await _host.Companies.DeleteRowAsync(row.Entity.Id);
        Entity.Rows.Remove(row.Entity);
        Rows.Remove(row);
        Recalc();
    }

    [RelayCommand]
    private Task BackAsync() => _section.BackCommand.ExecuteAsync(null);

    public async Task FlushAsync()
    {
        foreach (var r in Rows.ToList()) await r.FlushAsync();
    }
}

/// <summary>یک کارتِ شرکت در فهرست.</summary>
public sealed class CompanyCardViewModel
{
    public CompanyCardViewModel(TilCompany c, CompanyService calc)
    {
        Entity = c;
        Name = c.Name ?? "";
        var all = c.Rows;
        var s = calc.Summarize(c, all);
        TotalAfnText = Shamsi.Money(Math.Round(s.TotalAfn, 0));
        TotalUsdText = Shamsi.Money(Math.Round(s.TotalUsd, 2));
        AlbaqiAfnText = Shamsi.Money(Math.Round(s.AlbaqiAfn, 0));
        RowCount = all.Count;
    }

    public TilCompany Entity { get; }
    public string Name { get; }
    public string TotalAfnText { get; }
    public string TotalUsdText { get; }
    public string AlbaqiAfnText { get; }
    public int RowCount { get; }
}

/// <summary>
/// ══ بخشِ «شرکت‌ها تیل» ══════════════════════════════════════════════════════
/// فهرستِ شرکت‌ها و صفحهٔ حسابِ هر کدام. جمع‌ها از سرویسی می‌آیند که با ۲۰۰
/// شرکتِ تصادفیِ گرفته‌شده از خودِ نسخهٔ وب آزموده شده است.
/// </summary>
public sealed partial class CompanySectionViewModel : SectionViewModel
{
    private readonly AppHost _host;
    private List<CompanyCardViewModel> _all = new();

    public CompanySectionViewModel(AppHost host) : base("noinv", "noinv", "شرکت‌ها تیل")
        => _host = host;

    public ObservableCollection<CompanyCardViewModel> Cards { get; } = new();

    [ObservableProperty] private string _search = "";
    [ObservableProperty] private string _newName = "";
    [ObservableProperty] private CompanyPageViewModel? _page;

    public bool IsListVisible => Page is null;

    partial void OnPageChanged(CompanyPageViewModel? v) => OnPropertyChanged(nameof(IsListVisible));
    partial void OnSearchChanged(string v) => ApplyFilter();

    protected override Task LoadAsync() => RefreshAsync();

    public async Task RefreshAsync()
    {
        var list = await _host.Companies.ListAsync();
        _all = list.Select(c => new CompanyCardViewModel(c, _host.Company)).ToList();
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var s = Search.Trim();
        Cards.Clear();
        foreach (var c in _all)
            if (s.Length == 0 || c.Name.Contains(s, StringComparison.OrdinalIgnoreCase))
                Cards.Add(c);
    }

    [RelayCommand]
    private async Task OpenAsync(CompanyCardViewModel? card)
    {
        if (card is null) return;
        var full = await _host.Companies.LoadAsync(card.Entity.Id);
        if (full is null) return;
        Page = new CompanyPageViewModel(_host, full, this);
    }

    [RelayCommand]
    private async Task BackAsync()
    {
        if (Page is not null) await Page.FlushAsync();
        Page = null;
        await RefreshAsync();
    }

    [RelayCommand]
    private async Task AddCompanyAsync()
    {
        var n = NewName.Trim();
        if (n.Length == 0) return;
        await _host.Companies.AddAsync(n);
        NewName = "";
        await RefreshAsync();
    }

    [RelayCommand]
    private async Task DeleteCompanyAsync(CompanyCardViewModel? card)
    {
        if (card is null) return;
        await _host.Companies.DeleteAsync(card.Entity.Id);
        await RefreshAsync();
    }
}
