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

    /// <summary>
    /// جست‌وجوی فهرست — همان کادرِ «شماره فاکتور، نام مشتری، شماره تماس یا
    /// تاریخ» نسخهٔ وب: هر چهار تا، نه فقط نام.
    /// </summary>
    public bool Matches(string q) =>
        Number.ToString().Contains(q, StringComparison.Ordinal)
        || Customer.Contains(q, StringComparison.OrdinalIgnoreCase)
        || Phone.Contains(q, StringComparison.OrdinalIgnoreCase)
        || DateShamsi.Contains(q, StringComparison.Ordinal)
        || Vehicle.Contains(q, StringComparison.OrdinalIgnoreCase);

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

/// <summary>حالتِ صفحهٔ بخش: فرم، فهرستِ در صف، فهرستِ تایید شده، یا یک فاکتور.</summary>
public enum InvoicePane { Form, Pending, Approved, Detail }

/// <summary>
/// ══ بخشِ فاکتورها ═══════════════════════════════════════════════════════════
///
/// بازنویسیِ کامل تا «عینِ سایت» شود — خواستهٔ صریحِ صاحب ریپو: «ببین بخش
/// فاکتورها آن مدلی است، باید عینِ همان باشد بدون حتی یک ذره تفاوت.»
///
/// پیش از این نیتیو فقط یک فهرستِ ساده بود: دکمهٔ «فاکتورِ تازه» یک ردیفِ خالی
/// می‌ساخت و کاربر باید همان‌جا پرش می‌کرد. در سایت اما بخش سه تکه است:
///
///   ۱. <b>برگهٔ رسمیِ فاکتور</b> (‎.invp‎) — نام مشتری، به نام دیگر، شمارهٔ
///      خودکار، تاریخ، نوع تیل، فی، لیتر، نوع ماشین، «مبلغ بدون تیل»، شمارهٔ
///      تماس، و «جمله کل» که زنده حساب می‌شود.
///   ۲. <b>سه کارتِ آماری</b> — در صف، تایید شده، و مقایسهٔ نرخ.
///   ۳. <b>صفحهٔ فهرست</b> با جست‌وجو، و صفحهٔ تمام‌صفحهٔ خودِ فاکتور.
///
/// جمله کل مو‌به‌مو ‎invCalcTotal()‎: اگر فی و لیتر باشند ‎فی × لیتر‎، به‌اضافهٔ
/// «مبلغ بدون تیل». فاکتوری که فقط مبلغ دارد ‎by_money‎ است و لیتر ندارد.
/// </summary>
public sealed partial class InvoiceSectionViewModel : SectionViewModel
{
    private readonly AppHost _host;

    public InvoiceSectionViewModel(AppHost host) : base("invoices", "invoices", "ثبت فاکتورها")
        => _host = host;

    /// <summary>
    /// کارتِ «مقایسهٔ نرخ» جای خودش را در ردیفِ آماری دارد (کارتِ سوم)، پس
    /// ردیفِ خودکارِ کارت‌های زیربخش این‌جا لازم نیست — وگرنه یک لینک دو بار
    /// پیدا می‌شود و با سایت فرق می‌کند.
    /// </summary>
    protected override bool ShowSubLinks => false;

    public ObservableCollection<InvoiceRowViewModel> Rows { get; } = new();

    [ObservableProperty] private string _search = "";
    [ObservableProperty] private int _pendingCount;
    [ObservableProperty] private int _approvedCount;

    /// <summary>همان عددها با رقمِ فارسی و جداکنندهٔ هزار — مثلِ ‎n2fa‎ی سایت.</summary>
    public string PendingText => Shamsi.Money(PendingCount);
    public string ApprovedText => Shamsi.Money(ApprovedCount);

    partial void OnPendingCountChanged(int v) => OnPropertyChanged(nameof(PendingText));
    partial void OnApprovedCountChanged(int v) => OnPropertyChanged(nameof(ApprovedText));

    /// <summary>خالصِ «مقایسهٔ نرخ» — عددِ کارتِ سوم، از خودِ همان زیربخش.</summary>
    [ObservableProperty] private string _rateNetText = "0";

    // ══ برگهٔ فاکتور ═══════════════════════════════════════════════════════
    [ObservableProperty] private string _fCustomer = "";
    [ObservableProperty] private string _fAlias = "";
    [ObservableProperty] private string _fNumber = "";
    [ObservableProperty] private string _fDate = "";
    [ObservableProperty] private bool _fIsDiesel;
    [ObservableProperty] private string _fPrice = "";
    [ObservableProperty] private string _fLiters = "";
    [ObservableProperty] private string _fVehicle = "";
    [ObservableProperty] private string _fAmount = "";
    [ObservableProperty] private string _fPhone = "";
    [ObservableProperty] private string _fTotalText = "0 افغانی";
    [ObservableProperty] private string _numberHint = "(اولین فاکتور — قابل تغییر)";

    partial void OnFPriceChanged(string v) => CalcTotal();
    partial void OnFLitersChanged(string v) => CalcTotal();
    partial void OnFAmountChanged(string v) => CalcTotal();
    partial void OnSearchChanged(string v) => ApplyFilter();

    /// <summary>
    /// ‎invCalcTotal()‎ — «جمله کل (فی × لیتر)» به‌اضافهٔ «مبلغ بدون تیل».
    /// همان دو خطِ نسخهٔ وب، بی کم و زیاد.
    /// </summary>
    private void CalcTotal()
    {
        var total = Shamsi.Num(FPrice) * Shamsi.Num(FLiters) + Shamsi.Num(FAmount);
        FTotalText = Shamsi.Money(Math.Round(total, 0, MidpointRounding.AwayFromZero)) + " افغانی";
    }

    // ══ کدام صفحه جلوی چشم است ══════════════════════════════════════════════
    [ObservableProperty] private InvoicePane _pane = InvoicePane.Form;
    [ObservableProperty] private InvoiceRowViewModel? _detail;

    partial void OnPaneChanged(InvoicePane v)
    {
        foreach (var n in new[] { nameof(IsForm), nameof(IsList), nameof(IsDetail),
                                  nameof(ListTitle), nameof(ListCountText) })
            OnPropertyChanged(n);
        IsPageOpen = v != InvoicePane.Form;
        ApplyFilter();
    }

    public bool IsForm => Pane == InvoicePane.Form;
    public bool IsList => Pane is InvoicePane.Pending or InvoicePane.Approved;
    public bool IsDetail => Pane == InvoicePane.Detail;

    public string ListTitle => Pane == InvoicePane.Approved
        ? "🟢 فاکتورهای تایید شده" : "🟡 فاکتورهای در صف";

    public string ListCountText => Shamsi.Money(Rows.Count) + " فاکتور";

    private List<InvoiceRowViewModel> _all = new();

    protected override async Task LoadAsync()
    {
        var list = await _host.Invoices.ListAsync();
        _all = list.Select(v => new InvoiceRowViewModel(v, this)).ToList();

        PendingCount = list.Count(v => v.Status == InvoiceStatus.Pending);
        ApprovedCount = list.Count(v => v.Status == InvoiceStatus.Approved);

        // شمارهٔ بعدی و راهنماییِ زیرش — همان ‎renderInvoices()‎
        var next = await _host.Invoices.NextNumberAsync();
        if (FNumber.Trim().Length == 0) FNumber = next.ToString();
        NumberHint = next > 1
            ? "خودکار — فاکتور قبلی: شماره " + Shamsi.Money(next - 1) + " (قابل تغییر)"
            : "(اولین فاکتور — قابل تغییر)";
        if (FDate.Trim().Length == 0) FDate = Shamsi.Today();
        if (FPrice.Trim().Length == 0)
            FPrice = Shamsi.Money(_host.Settings.UnionRate(FIsDiesel ? FuelType.Diesel : FuelType.Petrol));

        RefreshRateCard();
        ApplyFilter();
    }

    public override Task OnActivatedAsync() => ReloadAsync();

    /// <summary>عددِ کارتِ «مقایسهٔ نرخ» از خودِ زیربخشِ همان صفحه می‌آید.</summary>
    private void RefreshRateCard()
    {
        if (SubSections.OfType<InvRateSectionViewModel>().FirstOrDefault() is not { } rate) return;
        _ = CrashGuard.RunAsync("مقایسهٔ نرخ", async () =>
        {
            await rate.RefreshAsync();
            RateNetText = Shamsi.Money(Math.Round(rate.Net, 0, MidpointRounding.AwayFromZero));
        });
    }

    /// <summary>فهرستِ همان صفحه‌ای که باز است، با جست‌وجوی خودش.</summary>
    private void ApplyFilter()
    {
        var q = Search.Trim();
        Rows.Clear();
        foreach (var r in _all)
        {
            var okPane = Pane switch
            {
                InvoicePane.Pending => r.IsPending,
                InvoicePane.Approved => r.IsApproved,
                _ => true,
            };
            if (!okPane) continue;
            if (q.Length > 0 && !r.Matches(q)) continue;
            Rows.Add(r);
        }
        OnPropertyChanged(nameof(ListCountText));
    }

    public Task SaveAsync(Invoice v) => _host.Invoices.UpdateAsync(v);

    // ══ فرمان‌های برگهٔ فاکتور ══════════════════════════════════════════════

    /// <summary>«✅ ثبت فاکتور» — ‎invSubmit()‎.</summary>
    [RelayCommand]
    private Task SubmitAsync() => CrashGuard.RunAsync("ثبت فاکتور", async () =>
    {
        var name = FCustomer.Trim();
        if (name.Length == 0) { _host.Toast("نام مشتری را بنویسید", ToastKind.Error); return; }

        var price = Shamsi.Num(FPrice);
        var liters = Shamsi.Num(FLiters);
        var amount = Shamsi.Num(FAmount);
        if (liters <= 0m && amount <= 0m)
        {
            _host.Toast("یا لیتر و فی را بنویسید، یا «مبلغ بدون تیل» را", ToastKind.Error);
            return;
        }

        var number = (int)Shamsi.Num(FNumber);
        var v = await _host.Invoices.AddAsync(new Invoice
        {
            InvoiceNumber = number,
            DateShamsi = FDate.Trim().Length > 0 ? FDate.Trim() : Shamsi.Today(),
            CustomerName = name,
            DebtAlias = FAlias.Trim(),
            VehicleType = FVehicle.Trim(),
            Phone = FPhone.Trim(),
            Fuel = FIsDiesel ? FuelType.Diesel : FuelType.Petrol,
            PricePerLiter = price,
            Liters = liters,
            Amount = amount,
        });

        ResetForm();
        await ReloadAsync();
        _host.Toast("✅ فاکتور شماره " + Shamsi.Money(v.InvoiceNumber) + " ثبت شد", ToastKind.Ok);
    });

    /// <summary>«🔄 پاک کردن فرم» — ‎invResetForm()‎.</summary>
    [RelayCommand]
    private void ResetForm()
    {
        FCustomer = ""; FAlias = ""; FVehicle = ""; FPhone = "";
        FLiters = ""; FAmount = "";
        FNumber = ""; FDate = Shamsi.Today();
        FPrice = Shamsi.Money(_host.Settings.UnionRate(FIsDiesel ? FuelType.Diesel : FuelType.Petrol));
        CalcTotal();
    }

    /// <summary>نوعِ تیل عوض شد ⇒ فیِ پیشنهادی هم همان نرخِ اتحادیهٔ خودش.</summary>
    [RelayCommand]
    private void SetFuel(string? which)
    {
        FIsDiesel = which == "diesel";
        FPrice = Shamsi.Money(_host.Settings.UnionRate(FIsDiesel ? FuelType.Diesel : FuelType.Petrol));
    }

    // ══ صفحه‌ها ═════════════════════════════════════════════════════════════

    [RelayCommand]
    private void OpenList(string? which)
    {
        Search = "";
        Pane = which == "approved" ? InvoicePane.Approved : InvoicePane.Pending;
    }

    [RelayCommand]
    private void CloseList() { Pane = InvoicePane.Form; Detail = null; }

    [RelayCommand]
    private void OpenDetail(InvoiceRowViewModel? row)
    {
        if (row is null) return;
        Detail = row;
        Pane = InvoicePane.Detail;
    }

    // ══ کارهای هر فاکتور ════════════════════════════════════════════════════

    [RelayCommand]
    private Task ApproveAsync(InvoiceRowViewModel? row) =>
        CrashGuard.RunAsync("تایید فاکتور", async () =>
        {
            if (row is null || row.IsApproved) return;
            await row.FlushAsync();
            var rate = _host.Settings.UnionRate(row.Entity.Fuel);
            await _host.Invoices.ApproveAsync(row.Entity.Id, rate);
            await ReloadAsync();
            _host.Toast("✅ فاکتور تایید شد", ToastKind.Ok);
        });

    [RelayCommand]
    private Task RevertAsync(InvoiceRowViewModel? row) =>
        CrashGuard.RunAsync("برگشت فاکتور", async () =>
        {
            if (row is null || !row.IsApproved) return;
            await _host.Invoices.RevertAsync(row.Entity.Id);
            await ReloadAsync();
            _host.Toast("↩️ به صف برگشت", ToastKind.Warn);
        });

    [RelayCommand]
    private Task DeleteInvoiceAsync(InvoiceRowViewModel? row) =>
        CrashGuard.RunAsync("حذف فاکتور", async () =>
        {
            if (row is null) return;
            if (!await Dialogs.ConfirmAsync("حذف فاکتور",
                    "فاکتور شماره " + Shamsi.Money(row.Number) + " حذف شود؟")) return;
            await _host.Invoices.DeleteAsync(row.Entity.Id);
            if (ReferenceEquals(Detail, row)) { Detail = null; Pane = InvoicePane.Form; }
            await ReloadAsync();
            _host.Toast("🗑️ حذف شد", ToastKind.Warn);
        });
}
