using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PumpYaqobi.App.Controls;
using PumpYaqobi.App.Services;
using PumpYaqobi.Application.Localization;
using PumpYaqobi.Application.Services;
using PumpYaqobi.Domain.Enums;

namespace PumpYaqobi.App.ViewModels.Sections;

/// <summary>یک قرض‌دار در فهرستِ «زیان ناشی از افزایش قیمت» — یک ردیف، مثلِ سایت.</summary>
public sealed class PriceLossRowViewModel
{
    public PriceLossRowViewModel(PlPerson p, int index) { Person = p; Index = index; }

    public PlPerson Person { get; }
    public int Index { get; }

    public string Name => Person.Name;
    public string FuelText => Lit(Person.T.Fuel)
        + (Person.T.OpenFuel > 0m ? " · " + Lit(Person.T.OpenFuel) + " تسویه‌نشده" : "");
    public string PrincipalText => Afn(Person.T.Principal);
    public string RefValueText => Afn(Person.T.RefValue);
    public string LossText => Afn(Person.T.Loss);
    public string InvLossText => Afn(Person.Iv.Loss)
        + (Person.Iv.Loss > 0m ? " · " + Shamsi.Money(Person.Iv.N) + " فاکتور"
           : Person.Iv.Gain > 0m ? " · " + Afn(Person.Iv.Gain) + " مفاد" : "");
    public string InvCountText => Shamsi.Money(Person.Inv.Count) + " فاکتور"
        + (Person.Iv.PendLoss > 0m ? " · " + Shamsi.Money(Person.Iv.PendN) + " در صف" : "");
    public string SingleText => Shamsi.Money(Person.T.SingleN) + " برداشت تکی"
        + (Person.T.InvRowN > 0 ? " + " + Shamsi.Money(Person.T.InvRowN) + " ردیفِ فاکتوری" : "");

    public string LossBrushKey => Person.T.Loss > 0m ? "Pump.Danger" : "Pump.Muted";
    public string InvLossBrushKey => Person.Iv.Loss > 0m ? "Pump.Danger" : "Pump.Muted";

    internal static string Afn(decimal v) =>
        Shamsi.Money(Math.Round(v, 0, MidpointRounding.AwayFromZero)) + " افغانی";
    internal static string Lit(decimal v) => Shamsi.Money(Math.Round(v, 2), 2) + " لیتر";
}

/// <summary>یک تکه از یک برداشت در صفحهٔ جزئیات — ‎_plDetailTablesHtml‎.</summary>
public sealed class PriceLossPartViewModel
{
    public PriceLossPartViewModel(int index, PlItem it, PlPart? p, int partNo)
    {
        Index = index;
        var multi = it.Parts.Count > 1;
        DateText = (it.Date.Length > 0 ? it.Date : "—") + (it.Acct.Length > 0 ? " · 📄 " + it.Acct : "");
        KindText = it.IsInvoice ? "🧾 فاکتور" + (it.InvNo > 0 ? " " + Shamsi.Money(it.InvNo) : "") : "🛢️ برداشت تکی";
        KindBrushKey = it.IsInvoice ? "Pump.Purple" : "Pump.Info";
        FuelText = it.Fuel == FuelType.Diesel ? "🟤 دیزل" : "⛽ پطرول";
        SaleText = it.Sale > 0m ? Shamsi.Money(Math.Round(it.Sale, 2), 2) : "—";

        if (p is null)
        {
            // مقدارِ هنوز تسویه‌نشده — تا روزِ رسید هیچ زیانی برایش حساب نمی‌شود
            QtyText = PriceLossRowViewModel.Lit(it.Remain);
            ReceiptText = "— هنوز نیامده";
            RefText = "—"; LossText = "—";
            StatusText = "🔴 تسویه‌نشده"; StatusBrushKey = "Pump.Danger";
            PrincipalText = it.Sale > 0m ? PriceLossRowViewModel.Afn(it.Sale * it.Remain) : "—";
            RefValueText = "—"; LossBrushKey = "Pump.Muted";
            return;
        }

        QtyText = PriceLossRowViewModel.Lit(p.Qty)
            + (multi ? " (" + Shamsi.Money(partNo) + " از " + Shamsi.Money(it.Parts.Count) + ")" : "");
        ReceiptText = (p.Date.Length > 0 ? p.Date : "—")
            + (p.InvNo > 0 ? " · 🧾 فاکتور " + Shamsi.Money(p.InvNo) : "");
        RefText = p.Ref > 0m ? Shamsi.Money(Math.Round(p.Ref, 2), 2)
                : p.NoRate ? "— ثبت نشده" : "—";
        LossText = p.NoRate || p.NoSale ? "—" : PriceLossRowViewModel.Afn(p.Loss);
        LossBrushKey = p.Loss > 0m ? "Pump.Danger" : "Pump.Muted";
        (StatusText, StatusBrushKey) =
            p.NoRate ? ("⚠️ نرخِ آن روز نیست", "Pump.Accent")
            : p.NoSale ? ("⚠️ بی‌نرخ", "Pump.Accent")
            : p.SameDay ? ("✅ رسیدِ همان روز", "Pump.Ok")
            : p.Loss > 0m ? ("📉 زیان", "Pump.Danger")
            : ("✅ بدون زیان", "Pump.Muted");
        PrincipalText = p.NoSale ? "—" : PriceLossRowViewModel.Afn(p.Principal);
        RefValueText = p.NoRate ? "—" : PriceLossRowViewModel.Afn(p.RefValue);
    }

    public int Index { get; }
    public string IndexText => Shamsi.Money(Index);
    public string DateText { get; }
    public string QtyText { get; }
    public string SaleText { get; }
    public string ReceiptText { get; }
    public string RefText { get; }
    public string LossText { get; }
    public string LossBrushKey { get; }
    public string StatusText { get; }
    public string StatusBrushKey { get; }
    public string PrincipalText { get; }
    public string RefValueText { get; }
    public string KindText { get; }
    public string KindBrushKey { get; }
    public string FuelText { get; }
}

/// <summary>یک فاکتورِ همین شخص در صفحهٔ جزئیات.</summary>
public sealed class PriceLossInvoiceViewModel
{
    public PriceLossInvoiceViewModel(PlInvoice v)
    {
        var up = v.HasDiff && v.Diff > 0m;
        var dn = v.HasDiff && v.Diff < 0m;
        NumberText = "🧾 فاکتور " + Shamsi.Money(v.No);
        DateText = v.Date.Length > 0 ? v.Date : "—";
        LitersText = PriceLossRowViewModel.Lit(v.Liters);
        RcText = v.Rc > 0m ? Shamsi.Money(Math.Round(v.Rc, 2), 2) : "—";
        RaText = v.Approved
            ? (v.Ra > 0m ? Shamsi.Money(Math.Round(v.Ra, 2), 2) : "—")
            : v.NowRate > 0m ? Shamsi.Money(Math.Round(v.NowRate, 2), 2) + " (نرخ امروز — هنوز قفل نشده)"
            : "هنوز تایید نشده";
        DplText = v.HasDiff
            ? Shamsi.Money(Math.Round(Math.Abs(v.Dpl), 2), 2) + " — " + (up ? "بالا رفته" : dn ? "پایین آمده" : "بی‌تغییر")
            : v.Pdpl > 0m ? Shamsi.Money(Math.Round(v.Pdpl, 2), 2) + " (اگر امروز تایید شود)" : "—";
        LossText = v.HasDiff
            ? PriceLossRowViewModel.Afn(Math.Abs(v.Diff)) + " — " + (up ? "ضرر این فاکتور" : dn ? "مفاد این فاکتور" : "بی‌تغییر")
            : v.PendDiff > 0m ? PriceLossRowViewModel.Afn(v.PendDiff) + " (پیش‌نمایش — در جمع نیست)" : "—";
        DiffBrushKey = up ? "Pump.Danger" : dn ? "Pump.Ok" : v.HasDiff ? "Pump.Muted" : "Pump.Purple";
        StatusText = v.Approved ? "✅ تاییدشده" : "⏳ در صف تایید";
        StatusBrushKey = v.Approved ? "Pump.Ok" : "Pump.Accent";
        AmountText = v.Rc > 0m ? PriceLossRowViewModel.Afn(v.Amount) : "—";
        FuelText = v.Fuel == FuelType.Diesel ? "🟤 دیزل" : "⛽ پطرول";
    }

    public string NumberText { get; }
    public string DateText { get; }
    public string LitersText { get; }
    public string RcText { get; }
    public string RaText { get; }
    public string DplText { get; }
    public string LossText { get; }
    public string DiffBrushKey { get; }
    public string StatusText { get; }
    public string StatusBrushKey { get; }
    public string AmountText { get; }
    public string FuelText { get; }
}

/// <summary>صفحهٔ جزئیاتِ یک قرض‌دار — ‎renderPlPersonPage‎ (‎sec-plperson‎).</summary>
public sealed partial class PriceLossPersonViewModel : ObservableObject
{
    private readonly PriceLossSectionViewModel _owner;

    public PriceLossPersonViewModel(PlPerson p, PriceLossSectionViewModel owner)
    {
        Person = p; _owner = owner;
        var t = p.T;
        Title = "📉 " + p.Name;
        FuelText = PriceLossRowViewModel.Lit(t.Fuel);
        PrincipalText = PriceLossRowViewModel.Afn(t.Principal);
        RefValueText = PriceLossRowViewModel.Afn(t.RefValue);
        LossText = PriceLossRowViewModel.Afn(t.Loss);
        InvLossText = PriceLossRowViewModel.Afn(p.Iv.Loss);
        var total = t.Loss + p.Iv.Loss;
        TotalText = PriceLossRowViewModel.Afn(total);
        OpenFuelText = PriceLossRowViewModel.Lit(t.OpenFuel);
        RecordsText = Shamsi.Money(t.SingleN) + " برداشت تکی · " + Shamsi.Money(p.Inv.Count) + " فاکتور";
        LossBrushKey = t.Loss > 0m ? "Pump.Danger" : "Pump.Ok";
        InvLossBrushKey = p.Iv.Loss > 0m ? "Pump.Danger" : "Pump.Ok";
        TotalBrushKey = total > 0m ? "Pump.Danger" : "Pump.Ok";

        NoRateWarn = t.NoRateN > 0
            ? "⚠️ برای " + Shamsi.Money(t.NoRateN) + " رسیدِ این حساب، نرخ اتحادیه برای همان تاریخ موجود نیست "
              + "(یا نرخِ خودِ برداشت ثبت نشده). این موارد در هیچ جمعی شمرده نشده‌اند و هیچ نرخِ تخمینی جایشان گذاشته نشده."
            : "";
        PendWarn = p.Iv.PendLoss > 0m
            ? "🧾 " + Shamsi.Money(p.Iv.PendN) + " فاکتورِ این شخص هنوز در صفِ تایید است و نرخِ اتحادیهٔ امروز از قیمتشان بالاتر رفته. "
              + "اگر همین حالا تایید شوند، " + PriceLossRowViewModel.Afn(p.Iv.PendLoss) + " ضرر می‌خورد. (در هیچ جمعی شمرده نشده.)"
            : "";

        var lines = new List<PriceLossPartViewModel>();
        var ln = 0;
        foreach (var it in p.Items)
        {
            var pi = 0;
            foreach (var part in it.Parts) lines.Add(new PriceLossPartViewModel(++ln, it, part, ++pi));
            if (it.Remain > 0m) lines.Add(new PriceLossPartViewModel(++ln, it, null, 0));
        }
        Lines.ResetTo(lines);
        Invoices.ResetTo(p.Inv.Select(v => new PriceLossInvoiceViewModel(v)));

        LineTotals = new[]
        {
            new TotalCell("جمع", p.Name, null, TotalCell.NoColumn),
            new TotalCell("مقدار باقی‌مانده", PriceLossRowViewModel.Lit(t.Fuel), "Pump.Info"),
            new TotalCell("زیان", PriceLossRowViewModel.Afn(t.Loss), LossBrushKey),
            new TotalCell("وضعیت", Shamsi.Money(t.LossN) + " رکوردِ دارای زیان", "Pump.Muted"),
            new TotalCell("مبلغ ثبت‌شده", PriceLossRowViewModel.Afn(t.Principal), "Pump.Accent"),
            new TotalCell("ارزش با نرخ رسید", PriceLossRowViewModel.Afn(t.RefValue), "Pump.Purple"),
        };
        InvoiceTotals = new[]
        {
            new TotalCell("ضرر این فاکتور", PriceLossRowViewModel.Afn(p.Iv.Loss), InvLossBrushKey),
            new TotalCell("وضعیت", Shamsi.Money(p.Iv.N) + " فاکتورِ ضررده", "Pump.Muted"),
        };
    }

    public PlPerson Person { get; }
    public string Title { get; }
    public string FuelText { get; }
    public string PrincipalText { get; }
    public string RefValueText { get; }
    public string LossText { get; }
    public string InvLossText { get; }
    public string TotalText { get; }
    public string OpenFuelText { get; }
    public string RecordsText { get; }
    public string LossBrushKey { get; }
    public string InvLossBrushKey { get; }
    public string TotalBrushKey { get; }
    public string NoRateWarn { get; }
    public string PendWarn { get; }
    public bool HasNoRateWarn => NoRateWarn.Length > 0;
    public bool HasPendWarn => PendWarn.Length > 0;

    public BulkRows<PriceLossPartViewModel> Lines { get; } = new();
    public BulkRows<PriceLossInvoiceViewModel> Invoices { get; } = new();
    public bool HasLines => Lines.Count > 0;
    public bool HasInvoices => Invoices.Count > 0;
    public IReadOnlyList<TotalCell> LineTotals { get; }
    public IReadOnlyList<TotalCell> InvoiceTotals { get; }

    /// <summary>گروهِ «فقط فاکتور» هنوز حسابِ قرض‌داری ندارد و چیزی برای باز کردن نیست.</summary>
    public bool CanOpenAccount => Person.Id is not null;

    [RelayCommand] private void Back() => _owner.ClosePerson();
    [RelayCommand] private Task OpenAccount() => _owner.OpenAccountAsync(Person);
}

/// <summary>
/// ══ 📉 زیان ناشی از افزایش قیمت ═══════════════════════════════════════════
///
/// همان ‎sec-priceloss‎ و ‎sec-plperson‎ی سایت: کارت‌های خلاصه، سه فیلتر
/// (همه / پرداخت‌نشده / پرداخت‌شده)، جدولی که هر قرض‌دار در آن یک ردیف است،
/// و صفحهٔ جزئیاتِ هر شخص با دو جدولِ «برداشت‌ها» و «فاکتورها».
///
/// در سایت از کارتِ بخشِ قرض‌داران باز می‌شود — پس این‌جا هم زیربخشِ همان است.
/// ⚠️ فقط گزارش است: هیچ عددی از این‌جا وارد بدهی، الباقی یا موجودی نمی‌شود
/// و هیچ داده‌ای را عوض نمی‌کند (<see cref="PriceLossService"/>).
/// </summary>
public sealed partial class PriceLossSectionViewModel : SectionViewModel
{
    private readonly AppHost _host;
    private readonly Func<long, Task>? _openPerson;
    private readonly PriceLossService _svc = new();
    private PriceLossReport _report = new(Array.Empty<PlPerson>(), new PlGrand());

    public PriceLossSectionViewModel(AppHost host, Func<long, Task>? openPerson = null)
        : base("priceloss", "debt", "زیان ناشی از افزایش قیمت")
    { _host = host; _openPerson = openPerson; }

    public BulkRows<PriceLossRowViewModel> Rows { get; } = new();

    /// <summary>‎_lossFilter‎ — ۰ همه · ۱ فقط پرداخت‌نشده · ۲ فقط پرداخت‌شده.</summary>
    [ObservableProperty] private int _filterIndex;
    public bool IsAll => FilterIndex == 0;
    public bool IsOpen => FilterIndex == 1;
    public bool IsPaid => FilterIndex == 2;

    [ObservableProperty] private PriceLossPersonViewModel? _page;
    public bool IsListVisible => Page is null;
    public override object? ActivePage => Page;

    // ── کارت‌های بالای صفحه — همیشه تصویرِ کاملِ همهٔ داده‌ها ────────────────
    [ObservableProperty] private string _lossPersonsText = "0 نفر";
    [ObservableProperty] private string _fuelText = "0 لیتر";
    [ObservableProperty] private string _principalText = "0 افغانی";
    [ObservableProperty] private string _refValueText = "0 افغانی";
    [ObservableProperty] private string _lossText = "0 افغانی";
    [ObservableProperty] private string _invLossText = "0 افغانی";
    [ObservableProperty] private string _totalText = "0 افغانی";
    [ObservableProperty] private string _openFuelText = "0 لیتر";
    [ObservableProperty] private string _recordsText = "";
    [ObservableProperty] private string _lossPersonsBrushKey = "Pump.Ok";
    [ObservableProperty] private string _lossBrushKey = "Pump.Ok";
    [ObservableProperty] private string _invLossBrushKey = "Pump.Ok";
    [ObservableProperty] private string _totalBrushKey = "Pump.Ok";
    [ObservableProperty] private string _noRateWarn = "";
    [ObservableProperty] private string _pendWarn = "";
    [ObservableProperty] private bool _isEmpty = true;
    [ObservableProperty] private string _shownText = "";
    public bool HasNoRateWarn => NoRateWarn.Length > 0;
    public bool HasPendWarn => PendWarn.Length > 0;
    partial void OnNoRateWarnChanged(string v) => OnPropertyChanged(nameof(HasNoRateWarn));
    partial void OnPendWarnChanged(string v) => OnPropertyChanged(nameof(HasPendWarn));

    /// <summary>ردیفِ «جمعِ نما» — روی ردیف‌های دیده‌شده، مثلِ ‎tfoot‎ی سایت.</summary>
    [ObservableProperty] private IReadOnlyList<TotalCell> _totalCells = Array.Empty<TotalCell>();

    partial void OnFilterIndexChanged(int v)
    {
        foreach (var n in new[] { nameof(IsAll), nameof(IsOpen), nameof(IsPaid) })
            OnPropertyChanged(n);
        ApplyFilter();
    }

    partial void OnPageChanged(PriceLossPersonViewModel? v)
    {
        OnPropertyChanged(nameof(IsListVisible));
        IsPageOpen = v is not null;
    }

    [RelayCommand]
    private void SetFilter(string? which) =>
        FilterIndex = which switch { "open" => 1, "paid" => 2, _ => 0 };

    [RelayCommand]
    private void OpenRow(PriceLossRowViewModel? row)
    {
        if (row is null) return;
        Page = new PriceLossPersonViewModel(row.Person, this);
    }

    public void ClosePerson() => Page = null;

    /// <summary>‎lossOpenPerson‎ — حسابِ کاملِ همین شخص در بخشِ قرض‌داران.</summary>
    public async Task OpenAccountAsync(PlPerson p)
    {
        if (p.Id is null)
        {
            _host.Toast("برای این نام هنوز حسابِ قرض‌داری ساخته نشده — فقط فاکتور دارد", ToastKind.Warn);
            return;
        }
        if (_openPerson is null) return;
        await _openPerson(p.Id.Value);
    }

    protected override Task LoadAsync() => RefreshAsync();
    public override Task OnActivatedAsync() => RefreshAsync();

    public async Task RefreshAsync()
    {
        var debtors = await _host.Debtors.LoadAllAsync(withReceipts: true);
        var invoices = await _host.Invoices.ListAsync();
        var rates = await _host.Tools.RateHistoryAsync();
        _report = _svc.Build(debtors, invoices, rates,
                             _host.Settings.UnionRate(FuelType.Petrol),
                             _host.Settings.UnionRate(FuelType.Diesel),
                             Shamsi.Today());

        var g = _report.G;
        LossPersonsText = Shamsi.Money(g.LossPersons) + " نفر";
        LossPersonsBrushKey = g.LossPersons > 0 ? "Pump.Danger" : "Pump.Ok";
        FuelText = PriceLossRowViewModel.Lit(g.Fuel);
        PrincipalText = PriceLossRowViewModel.Afn(g.Principal);
        RefValueText = PriceLossRowViewModel.Afn(g.RefValue);
        LossText = PriceLossRowViewModel.Afn(g.Loss);
        LossBrushKey = g.Loss > 0m ? "Pump.Danger" : "Pump.Ok";
        InvLossText = PriceLossRowViewModel.Afn(g.InvLoss);
        InvLossBrushKey = g.InvLoss > 0m ? "Pump.Danger" : "Pump.Ok";
        TotalText = PriceLossRowViewModel.Afn(g.Loss + g.InvLoss);
        TotalBrushKey = g.Loss + g.InvLoss > 0m ? "Pump.Danger" : "Pump.Ok";
        OpenFuelText = PriceLossRowViewModel.Lit(g.OpenFuel);
        RecordsText = Shamsi.Money(g.SingleN) + " برداشت تکی · " + Shamsi.Money(g.InvN) + " فاکتور";
        NoRateWarn = g.NoRateN > 0
            ? "⚠️ برای " + Shamsi.Money(g.NoRateN) + " رسید، نرخ اتحادیه برای همان تاریخ موجود نیست "
              + "(یا نرخِ خودِ برداشت ثبت نشده). این موارد در هیچ جمعی شمرده نشده‌اند و هیچ نرخِ تخمینی جایشان گذاشته نشده — "
              + "نرخِ همان روز را در «مفاد / ضرر ← نرخ اتحادیه» بنویسید تا خودکار وارد محاسبه شوند."
            : "";
        PendWarn = g.PendLoss > 0m
            ? "🧾 " + Shamsi.Money(g.PendN) + " فاکتور هنوز در صفِ تایید است و نرخِ اتحادیهٔ امروز از قیمتِ خودشان بالاتر رفته. "
              + "اگر همین حالا تایید شوند، " + PriceLossRowViewModel.Afn(g.PendLoss) + " ضرر می‌خورد. "
              + "(این عدد در هیچ جمعی شمرده نشده — تا وقتی تایید نشود، نرخِ تایید نه ثبت می‌شود نه حدس زده می‌شود.)"
            : "";
        IsEmpty = _report.List.Count == 0;

        // صفحهٔ بازِ جزئیات با دادهٔ تازه دوباره ساخته می‌شود
        if (Page is { } open)
            Page = _report.List.FirstOrDefault(x => x.Key == open.Person.Key) is { } again
                ? new PriceLossPersonViewModel(again, this) : null;

        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var list = _report.List.Where(x => FilterIndex switch
        {
            1 => x.HasOpenFuel,
            2 => x.HasPaid,
            _ => true,
        }).ToList();
        Rows.ResetTo(list.Select((x, i) => new PriceLossRowViewModel(x, i + 1)));

        decimal fuel = 0, principal = 0, refValue = 0, loss = 0, invLoss = 0;
        int single = 0, inv = 0;
        foreach (var x in list)
        {
            fuel += x.T.Fuel; principal += x.T.Principal; refValue += x.T.RefValue;
            loss += x.T.Loss; single += x.T.SingleN; inv += x.Inv.Count; invLoss += x.Iv.Loss;
        }
        var label = FilterIndex switch { 1 => "فقط پرداخت‌نشده‌ها", 2 => "فقط پرداخت‌شده‌ها", _ => "همه" };
        ShownText = "جمعِ نمای «" + label + "» — " + Shamsi.Money(list.Count) + " قرض‌دار";
        TotalCells = new[]
        {
            new TotalCell("تیل باقی‌مانده", PriceLossRowViewModel.Lit(fuel), "Pump.Info"),
            new TotalCell("مبلغ ثبت‌شده", PriceLossRowViewModel.Afn(principal), "Pump.Accent"),
            new TotalCell("ارزش با نرخ رسید", PriceLossRowViewModel.Afn(refValue), "Pump.Purple"),
            new TotalCell("زیان افزایش قیمت", PriceLossRowViewModel.Afn(loss), loss > 0m ? "Pump.Danger" : "Pump.Muted"),
            new TotalCell("زیان نرخ فاکتور", PriceLossRowViewModel.Afn(invLoss), invLoss > 0m ? "Pump.Danger" : "Pump.Muted"),
            new TotalCell("تعداد فاکتور", Shamsi.Money(inv) + " فاکتور"),
            new TotalCell("تعداد برداشت تکی", Shamsi.Money(single) + " برداشت تکی"),
        };
    }
}
