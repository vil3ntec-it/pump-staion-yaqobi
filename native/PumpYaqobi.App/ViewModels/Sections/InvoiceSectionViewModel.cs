using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PumpYaqobi.App.Services;
using PumpYaqobi.Application.Localization;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;
using PumpYaqobi.Services.Data;

namespace PumpYaqobi.App.ViewModels.Sections;

/// <summary>یک فاکتور روی جدول.</summary>
public sealed partial class InvoiceRowViewModel : RowViewModel
{
    private readonly Invoice _v;
    private readonly InvoiceSectionViewModel _owner;

    public InvoiceRowViewModel(Invoice v, InvoiceSectionViewModel owner)
    {
        _v = v; _owner = owner;
        Loading = true;
        _dateShamsi = v.DateShamsi ?? ""; _customer = v.CustomerName ?? "";
        _debtAlias = v.DebtAlias ?? ""; _vehicle = v.VehicleType ?? ""; _phone = v.Phone ?? "";
        _fuel = v.Fuel; _price = v.PricePerLiter; _liters = v.Liters; _amount = v.Amount;
        Loading = false;
    }

    public Invoice Entity => _v;

    [ObservableProperty] private string _dateShamsi = "";
    [ObservableProperty] private string _customer = "";
    [ObservableProperty] private string _debtAlias = "";
    [ObservableProperty] private string _vehicle = "";
    [ObservableProperty] private string _phone = "";
    [ObservableProperty] private FuelType _fuel;
    [ObservableProperty] private decimal _price;
    [ObservableProperty] private decimal _liters;
    [ObservableProperty] private decimal _amount;

    partial void OnDateShamsiChanged(string v) => Touch();
    partial void OnCustomerChanged(string v) => Touch();
    partial void OnDebtAliasChanged(string v) => Touch();
    partial void OnVehicleChanged(string v) => Touch();
    partial void OnPhoneChanged(string v) => Touch();
    partial void OnFuelChanged(FuelType v) { Touch(); OnPropertyChanged(nameof(FuelText)); }
    partial void OnPriceChanged(decimal v) { Touch(); Refresh(); }
    partial void OnLitersChanged(decimal v) { Touch(); Refresh(); }
    partial void OnAmountChanged(decimal v) { Touch(); Refresh(); }

    private void Refresh()
    {
        foreach (var n in new[] { nameof(PriceText), nameof(LitersText), nameof(AmountText),
                                  nameof(TotalText), nameof(KindText), nameof(PartsText) })
            OnPropertyChanged(n);
    }

    public int Number => _v.InvoiceNumber;
    public bool IsApproved => _v.Status == InvoiceStatus.Approved;
    public bool IsPending => !IsApproved;
    public string StatusText => IsApproved ? "تایید شده" : "در انتظارِ تایید";

    public string PriceText { get => Shamsi.Money(Price); set => Price = Shamsi.Num(value); }
    public string LitersText { get => Shamsi.Money(Liters); set => Liters = Shamsi.Num(value); }
    public string AmountText { get => Shamsi.Money(Amount); set => Amount = Shamsi.Num(value); }

    /// <summary>«فقط مبلغ» یا «تیل» — همان تفکیکی که همهٔ رفتارها به آن بند است.</summary>
    public string KindText => InvoiceService.IsMoneyOnly(_v) ? "فقط مبلغ" : "تیل";

    /// <summary>
    /// خطِ زیرِ نامِ مشتری در فهرست — همان ‎.mt‎ نسخهٔ وب: بخش‌های فاکتور،
    /// جمعِ کل و تاریخ، پشتِ سرِ هم.
    /// </summary>
    public string PartsText
    {
        get
        {
            var parts = new List<string>();
            if (Liters > 0) parts.Add((Fuel == FuelType.Diesel ? "🟤 " : "⛽ ")
                                      + Shamsi.Money(Liters) + " لیتر × " + Shamsi.Money(Price));
            if (Amount > 0) parts.Add("💵 " + Shamsi.Money(Amount) + " افغانی");
            parts.Add("💰 " + TotalText + " افغانی");
            parts.Add("📅 " + (DateShamsi.Length > 0 ? DateShamsi : "—"));
            return string.Join("   ·   ", parts);
        }
    }

    /// <summary>‎.inv-chip‎ — «🟢 تایید شده» یا «🟡 در صف».</summary>
    public string ChipText => IsApproved ? "🟢 تایید شده" : "🟡 در صف";
    public string ChipBrushKey => IsApproved ? "Pump.Ok" : "Pump.Warn";

    /// <summary>جمعِ کل: فاکتورِ «فقط مبلغ» همان مبلغ، وگرنه فی × لیتر.</summary>
    public string TotalText =>
        Shamsi.Money(InvoiceService.IsMoneyOnly(_v) ? Amount : Liters * Price);

    public string FuelText
    {
        get => Fuel.ToPersian();
        set => Fuel = value == "دیزل" ? FuelType.Diesel : FuelType.Petrol;
    }

    public void RefreshStatus()
    {
        OnPropertyChanged(nameof(IsApproved));
        OnPropertyChanged(nameof(IsPending));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(ChipText));
        OnPropertyChanged(nameof(ChipBrushKey));
    }

    protected override void Apply()
    {
        _v.DateShamsi = DateShamsi; _v.CustomerName = Customer; _v.DebtAlias = DebtAlias;
        _v.VehicleType = Vehicle; _v.Phone = Phone; _v.Fuel = Fuel;
        _v.PricePerLiter = Price; _v.Liters = Liters; _v.Amount = Amount;
    }

    protected override Task SaveAsync() => _owner.SaveAsync(_v);
}

/// <summary>
/// ══ بخشِ فاکتورها ═══════════════════════════════════════════════════════════
/// ثبت، تایید و برگشتِ فاکتور. تایید، بخشِ پولی را به دفترِ پولِ حسابِ قرض‌دار و
/// بخشِ تیل را به «مقدار رسیدِ تیل»ِ همان حساب می‌برد؛ برگشت دقیقاً همان را
/// پس می‌گیرد.
/// </summary>
public sealed partial class InvoiceSectionViewModel : SectionViewModel
{
    private readonly AppHost _host;

    public InvoiceSectionViewModel(AppHost host) : base("invoices", "invoices", "ثبت فاکتورها")
        => _host = host;

    public ObservableCollection<InvoiceRowViewModel> Rows { get; } = new();

    [ObservableProperty] private string _search = "";
    [ObservableProperty] private bool _onlyPending;
    [ObservableProperty] private int _pendingCount;
    [ObservableProperty] private int _approvedCount;

    partial void OnSearchChanged(string v) => _ = LoadAsync();
    partial void OnOnlyPendingChanged(bool v) => _ = LoadAsync();

    protected override async Task LoadAsync()
    {
        var list = await _host.Invoices.ListAsync(OnlyPending ? InvoiceStatus.Pending : null, Search);
        Rows.Clear();
        foreach (var v in list) Rows.Add(new InvoiceRowViewModel(v, this));

        var all = await _host.Invoices.ListAsync();
        PendingCount = all.Count(v => v.Status == InvoiceStatus.Pending);
        ApprovedCount = all.Count(v => v.Status == InvoiceStatus.Approved);
    }

    public Task SaveAsync(Invoice v) => _host.Invoices.UpdateAsync(v);

    [RelayCommand]
    private async Task AddInvoiceAsync()
    {
        var v = await _host.Invoices.AddAsync(new Invoice
        {
            DateShamsi = Shamsi.Today(),
            PricePerLiter = _host.Settings.UnionRate(FuelType.Petrol),
        });
        Rows.Insert(0, new InvoiceRowViewModel(v, this));
        PendingCount++;
    }

    [RelayCommand]
    private async Task ApproveAsync(InvoiceRowViewModel? row)
    {
        if (row is null || row.IsApproved) return;
        await row.FlushAsync();
        var rate = _host.Settings.UnionRate(row.Entity.Fuel);
        await _host.Invoices.ApproveAsync(row.Entity.Id, rate);
        row.Entity.Status = InvoiceStatus.Approved;
        row.RefreshStatus();
        PendingCount--; ApprovedCount++;
    }

    [RelayCommand]
    private async Task RevertAsync(InvoiceRowViewModel? row)
    {
        if (row is null || !row.IsApproved) return;
        await _host.Invoices.RevertAsync(row.Entity.Id);
        row.Entity.Status = InvoiceStatus.Pending;
        row.RefreshStatus();
        PendingCount++; ApprovedCount--;
    }

    [RelayCommand]
    private async Task DeleteInvoiceAsync(InvoiceRowViewModel? row)
    {
        if (row is null) return;
        await _host.Invoices.DeleteAsync(row.Entity.Id);
        Rows.Remove(row);
        await LoadAsync();
    }
}
