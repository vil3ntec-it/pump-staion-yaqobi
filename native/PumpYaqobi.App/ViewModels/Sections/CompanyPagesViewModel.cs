using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PumpYaqobi.App.Controls;
using PumpYaqobi.App.Printing;
using PumpYaqobi.App.Services;
using PumpYaqobi.Application.Localization;
using PumpYaqobi.Application.Services;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;
using PumpYaqobi.Reporting.Pdf;
using PumpYaqobi.Services.Data;

namespace PumpYaqobi.App.ViewModels.Sections;

// ══ صفحه‌های روییِ حسابِ شرکت ═══════════════════════════════════════════════
// در سایت سه مودالِ جدا بودند: «📦 خریدها» (‎companyPurchasesModal‎)،
// «🗂️ جدول‌های آرشیو» (‎companyArchiveModal‎) و «🔍 جستجوی خرید»
// (‎cmpSearchModal‎). هیچ‌کدام چیزی نمی‌نویسند جز حذفِ یک آرشیو.

/// <summary>یک خریدِ مخزن در صفحهٔ خریدها — کارتِ ‎cmp-card‎ی سایت، به شکلِ ردیف.</summary>
public sealed class PurchaseItemViewModel
{
    public PurchaseItemViewModel(FuelPurchase e, int index)
    {
        Entity = e; Index = index;
        var ton = e.Ton != 0m ? e.Ton : e.Kg / 1000m;
        IndexText = Shamsi.Money(index);
        DateText = string.IsNullOrWhiteSpace(e.DateShamsi) ? "—" : e.DateShamsi!;
        SellerText = string.IsNullOrWhiteSpace(e.Seller) ? "—" : e.Seller!;
        TonText = Shamsi.Money(Math.Round(ton, 3), 3) + " تن";
        DensityText = e.Density != 0m ? Shamsi.Money(e.Density) : "—";
        LitersText = Shamsi.Money(Math.Round(e.Liters, 0, MidpointRounding.AwayFromZero));
        PriceTonText = Shamsi.Money(e.PriceTon);
        UsdRateText = Shamsi.Money(e.UsdRate);
        TotalUsdText = Shamsi.Money(Math.Round(e.TotalUsd, 2), 2) + " $";
        TotalAfnText = Shamsi.Money(Math.Round(e.TotalAfn, 0, MidpointRounding.AwayFromZero));
        PerLiterText = Shamsi.Money(Math.Round(e.PerLiter, 1), 1);
        NoteText = e.Note ?? "";
    }

    public FuelPurchase Entity { get; }
    public int Index { get; }
    public string IndexText { get; }
    public string DateText { get; }
    public string SellerText { get; }
    public string TonText { get; }
    public string DensityText { get; }
    public string LitersText { get; }
    public string PriceTonText { get; }
    public string UsdRateText { get; }
    public string TotalUsdText { get; }
    public string TotalAfnText { get; }
    public string PerLiterText { get; }
    public string NoteText { get; }
}

/// <summary>ردیفِ دستیِ جدولِ شرکت (بی خریدِ مخزن) در صفحهٔ خریدها — ‎_companyManualRowSection‎.</summary>
public sealed class ManualRowItemViewModel
{
    public ManualRowItemViewModel(CompanyRow r, int index, CompanyService calc)
    {
        IndexText = Shamsi.Money(index);
        DateText = string.IsNullOrWhiteSpace(r.DateShamsi) ? "—" : r.DateShamsi!;
        NameText = string.IsNullOrWhiteSpace(r.Name) ? "—" : r.Name!;
        TonText = Shamsi.Money(Math.Round(calc.Ton(r), 3), 3) + " تن";
        UsdText = Shamsi.Money(r.Usd);
        RateText = Shamsi.Money(r.Rate);
        TotalUsdText = Shamsi.Money(Math.Round(calc.TotalUsd(r), 2), 2) + " $";
        TotalAfnText = Shamsi.Money(Math.Round(calc.TotalAfn(r), 0, MidpointRounding.AwayFromZero));
    }
    public string IndexText { get; }
    public string DateText { get; }
    public string NameText { get; }
    public string TonText { get; }
    public string UsdText { get; }
    public string RateText { get; }
    public string TotalUsdText { get; }
    public string TotalAfnText { get; }
}

/// <summary>یک بخشِ خرید (پطرول یا دیزل) با خلاصه — ‎_companyPurchaseSection‎.</summary>
public sealed class PurchaseSectionViewModel
{
    public PurchaseSectionViewModel(FuelType fuel, IReadOnlyList<FuelPurchase> entries,
                                    IReadOnlyList<CompanyRow> manual, CompanyService calc)
    {
        Fuel = fuel;
        IsDiesel = fuel == FuelType.Diesel;
        Label = IsDiesel ? "🟤 خریدهای دیزل" : "⛽ خریدهای پطرول";
        BrushKey = IsDiesel ? "Pump.Warn" : "Pump.Accent";
        Purchases.ResetTo(entries.Select((e, i) => new PurchaseItemViewModel(e, i + 1)));
        var manualShown = manual.Where(r => calc.Ton(r) != 0m || calc.TotalUsd(r) != 0m).ToList();
        Manual.ResetTo(manualShown.Select((r, i) => new ManualRowItemViewModel(r, i + 1, calc)));

        CountText = Shamsi.Money(entries.Count);
        LitersText = Shamsi.Money(Math.Round(entries.Sum(e => e.Liters), 0, MidpointRounding.AwayFromZero)) + " لیتر";
        UsdText = Shamsi.Money(Math.Round(entries.Sum(e => e.TotalUsd), 1), 1) + " $";
        AfnText = Shamsi.Money(Math.Round(entries.Sum(e => e.TotalAfn), 0, MidpointRounding.AwayFromZero)) + " افغانی";
        EmptyText = "هیچ خرید " + (IsDiesel ? "دیزل" : "پطرول") + "ی برای این شرکت ثبت نشده";

        ManualCountText = Shamsi.Money(manualShown.Count);
        ManualTonText = Shamsi.Money(Math.Round(manualShown.Sum(r => calc.Ton(r)), 3), 3) + " تن";
        ManualUsdText = Shamsi.Money(Math.Round(manualShown.Sum(r => calc.TotalUsd(r)), 1), 1) + " $";
        ManualAfnText = Shamsi.Money(Math.Round(manualShown.Sum(r => calc.TotalAfn(r)), 0, MidpointRounding.AwayFromZero)) + " افغانی";
    }

    public FuelType Fuel { get; }
    public bool IsDiesel { get; }
    public string Label { get; }
    public string BrushKey { get; }
    public BulkRows<PurchaseItemViewModel> Purchases { get; } = new();
    public BulkRows<ManualRowItemViewModel> Manual { get; } = new();
    public bool HasPurchases => Purchases.Count > 0;
    public bool HasManual => Manual.Count > 0;
    public string CountText { get; }
    public string LitersText { get; }
    public string UsdText { get; }
    public string AfnText { get; }
    public string EmptyText { get; }
    public string ManualCountText { get; }
    public string ManualTonText { get; }
    public string ManualUsdText { get; }
    public string ManualAfnText { get; }
}

/// <summary>
/// «📦 خریدها» — ‎renderCompanyPurchasesPage‎: خریدهای مخزنِ همین شرکت (زنده،
/// یا فقط بازهٔ یک جدولِ آرشیو) به‌علاوهٔ ردیف‌های دستیِ همان دفتر.
/// </summary>
public sealed partial class CompanyPurchasesPageViewModel : ObservableObject
{
    private readonly AppHost _host;
    private readonly CompanySectionViewModel _section;
    private readonly IReadOnlyList<FuelPurchase> _all;
    private readonly List<CompanyRow> _petrolManual = new(), _dieselManual = new();
    private readonly List<FuelPurchase> _petrol = new(), _diesel = new();

    public CompanyPurchasesPageViewModel(AppHost host, TilCompany c, FuelType? fuel, CompanyTableArchive? archive,
                                         IReadOnlyList<FuelPurchase> all, CompanySectionViewModel section)
    {
        _host = host; _section = section; _all = all;
        Company = c; Fuel = fuel; Archive = archive;

        var fuelLbl = fuel == FuelType.Petrol ? "⛽ خریدهای پطرول" : fuel == FuelType.Diesel ? "🟤 خریدهای دیزل" : "📦 خریدهای";
        Title = fuelLbl + " — " + (c.Name ?? "")
              + (archive is null ? "" : " — 🗂️ جدول آرشیو" + (string.IsNullOrWhiteSpace(archive.CreatedShamsi) ? "" : " (" + archive.CreatedShamsi + ")"));

        // ‎_cmpCollect‎ — نمای آرشیو: فقط بازهٔ همان جدول؛ نمای زنده: بعد از آخرین «جدول جدید»
        if (archive is null)
        {
            _petrol = CompanyPurchaseService.Live(all, c, FuelType.Petrol);
            _diesel = CompanyPurchaseService.Live(all, c, FuelType.Diesel);
            _petrolManual = CompanyPurchaseService.Manual(CompanyService.RowsOf(c, FuelType.Petrol), all);
            _dieselManual = CompanyPurchaseService.Manual(CompanyService.RowsOf(c, FuelType.Diesel), all);
        }
        else
        {
            var rows = CompanyDataService.ArchiveRows(archive);
            var inRange = CompanyPurchaseService.Of(all, c.Name, archive.Fuel, archive.PurchasesAfter, archive.PurchasesBefore);
            if (archive.Fuel == FuelType.Diesel) { _diesel = inRange; _dieselManual = CompanyPurchaseService.Manual(rows, all); }
            else { _petrol = inRange; _petrolManual = CompanyPurchaseService.Manual(rows, all); }
        }

        if (fuel != FuelType.Diesel) Sections.Add(new PurchaseSectionViewModel(FuelType.Petrol, _petrol, _petrolManual, host.Company));
        if (fuel != FuelType.Petrol) Sections.Add(new PurchaseSectionViewModel(FuelType.Diesel, _diesel, _dieselManual, host.Company));
    }

    public TilCompany Company { get; }
    public FuelType? Fuel { get; }
    public CompanyTableArchive? Archive { get; }
    public string Title { get; }
    public ObservableCollection<PurchaseSectionViewModel> Sections { get; } = new();

    /// <summary>خریدی که «جستجو» ما را به آن فرستاده — ردیفش پررنگ می‌شود.</summary>
    [ObservableProperty] private long _highlightedId;
    public void Highlight(long purchaseId)
    {
        HighlightedId = purchaseId;
        if (!Sections.Any(s => s.Purchases.Any(p => p.Entity.Id == purchaseId)))
            _host.Toast("این خرید در فهرستِ این صفحه نیست", ToastKind.Error);
    }

    [RelayCommand] private void Close() => _section.CloseOverlay();

    /// <summary>‎pdfCompanyPurchases‎ — دقیقاً همان چیزی که در صفحه باز است.</summary>
    [RelayCommand]
    private Task PdfAsync()
    {
        var input = new CompanyPurchasesReportInput(Title, Fuel,
            _petrol, _diesel, _petrolManual, _dieselManual, DocDates.Line());
        return Documents.ShowAsync(() => new CompanyPurchasesReport(input, _host.Company), Title);
    }
}

/// <summary>ردیفِ خواندنیِ یک جدولِ آرشیوِ شرکت.</summary>
public sealed class CompanyArchiveRowViewModel
{
    public CompanyArchiveRowViewModel(CompanyRow r, int index, CompanyService calc, decimal rate)
    {
        Index = index;
        IndexText = Shamsi.Money(index);
        LinkMark = string.IsNullOrWhiteSpace(r.SourcePurchaseId) ? "" : "📦";
        DateText = r.DateShamsi ?? "";
        NameText = r.Name ?? "";
        TonText = Shamsi.MoneyOrBlank(calc.Ton(r));
        UsdText = Shamsi.MoneyOrBlank(r.Usd);
        RateText = Shamsi.MoneyOrBlank(r.Rate);
        TotalUsdText = Shamsi.Money(Math.Round(calc.TotalUsd(r), 1), 1) + " $";
        TotalAfnText = Shamsi.Money(Math.Round(calc.TotalAfn(r), 0, MidpointRounding.AwayFromZero));
        PoulText = Shamsi.MoneyOrBlank(r.Poul);
        // ⛔ «واحدِ رسید» ستونِ خودش را دارد، مثلِ جدولِ زنده — پیش از این به
        // دُمِ عدد چسبیده بود («۵۰۰ ؋») و ستونِ جدولِ آرشیو با ستونِ جدولِ
        // اصلی یکی نبود. همان «جدول‌های آرشیو عینِ جدولِ اصلی نیستند».
        PoulCurrencyText = r.PoulCurrency == Currency.Usd ? "دالر" : "افغانی";
        PoulCurrencyBrushKey = r.PoulCurrency == Currency.Usd ? "Pump.Info" : "Pump.Ok";
        var alb = calc.AlbaqiAfn(r, rate);
        AlbaqiText = Shamsi.Money(Math.Round(alb, 0, MidpointRounding.AwayFromZero));
        AlbaqiBrushKey = alb > 0m ? "Pump.Danger" : "Pump.Ok";
        AlbaqiUsdText = Shamsi.Money(Math.Round(calc.AlbaqiUsd(r, rate), 2));
    }
    public int Index { get; }
    public string IndexText { get; }
    public string LinkMark { get; }
    public string DateText { get; }
    public string NameText { get; }
    public string TonText { get; }
    public string UsdText { get; }
    public string RateText { get; }
    public string TotalUsdText { get; }
    public string TotalAfnText { get; }
    public string PoulText { get; }
    public string PoulCurrencyText { get; }
    public string PoulCurrencyBrushKey { get; }
    public string AlbaqiText { get; }
    public string AlbaqiBrushKey { get; }
    public string AlbaqiUsdText { get; }

    /// <summary>خوراکِ جست‌وجوی همین صفحه — تاریخ، نام و عددها.</summary>
    public string Haystack => (DateText + " " + NameText + " " + TonText + " " + UsdText + " "
                               + RateText + " " + PoulText).Trim();
}

/// <summary>یک جدولِ آرشیو با جمع‌ها و دو دکمهٔ خریدهای همان بازه — ‎_renderCompanyHistoryPanel‎.</summary>
public sealed partial class CompanyArchiveViewModel : ObservableObject
{
    private readonly CompanyArchivePageViewModel _page;

    public CompanyArchiveViewModel(CompanyTableArchive h, TilCompany c, CompanyService calc,
                                   IReadOnlyList<FuelPurchase> all, CompanyArchivePageViewModel page)
    {
        _page = page; Entity = h;
        var rows = CompanyDataService.ArchiveRows(h);
        var s = calc.Summarize(c, rows);
        var rate = s.ConvRate;
        Rows.ResetTo(rows.Select((r, i) => new CompanyArchiveRowViewModel(r, i + 1, calc, rate)));
        Title = "🗂️ " + (h.Fuel == FuelType.Diesel ? "🟤 دیزل" : "⛽ پطرول") + " — " + (h.CreatedShamsi ?? "—")
              + " — " + Shamsi.Money(h.RowCount) + " ردیف";
        var ton = rows.Sum(r => calc.Ton(r));

        // ══ سربرگِ خودِ این آرشیو ═══════════════════════════════════════════
        //
        // گزارشِ صاحب ریپو (۱۴۰۵/۰۷/۰۶): «جدول‌های آرشیو عینِ جدولِ اصلی
        // نیستند و سربرگ‌های خودشان را هم ندارند و دقیق نیستند.»
        //
        // حق داشت: جدولِ زنده شش کادرِ خلاصه دارد (تن، کل دالر، کل افغانی،
        // رسیدِ دالر، رسیدِ افغانی، الباقی) و آرشیو هیچ‌کدام را نداشت — فقط
        // یک نوارِ «جمله» ته جدول. حالا همان شش عدد، از **همان**
        // ‎CompanyService.Summarize‎ی جدولِ زنده.
        //
        // ⛔ هیچ فرمولِ تازه‌ای این‌جا نوشته نشد؛ عددِ دومی و متفاوت بدتر از
        // نبودنِ عدد است.
        Fuel = h.Fuel;
        FuelWord = h.Fuel == FuelType.Diesel ? "🟤 دیزل" : "⛽ پطرول";
        DateText = h.CreatedShamsi ?? "—";
        RowCountText = Shamsi.Money(h.RowCount) + " ردیف";
        BarText = FuelWord + " · " + DateText + " · " + RowCountText;
        BarBrushKey = h.Fuel == FuelType.Diesel ? "Pump.Warn" : "Pump.Ok";

        TotalTonText = Shamsi.Money(Math.Round(ton, 2), 2) + " تن";
        TotalUsdText = Shamsi.Money(Math.Round(s.TotalUsd, 1), 1) + " $";
        TotalAfnText = Shamsi.Money(Math.Round(s.TotalAfn, 0, MidpointRounding.AwayFromZero)) + " افغانی";
        PaidUsdText = Shamsi.Money(Math.Round(s.PaidUsd, 1), 1) + " $";
        PaidAfnText = Shamsi.Money(Math.Round(s.PaidAfn, 0, MidpointRounding.AwayFromZero)) + " افغانی";
        AlbaqiAfnText = Shamsi.Money(Math.Round(s.AlbaqiAfn, 0, MidpointRounding.AwayFromZero)) + " AFN";
        AlbaqiUsdText = Shamsi.Money(Math.Round(s.AlbaqiUsd, 2)) + " $";
        AlbaqiBrushKey = s.AlbaqiAfn > 0m ? "Pump.Danger" : "Pump.Ok";
        AlbaqiStatus = s.AlbaqiAfn > 0m ? "بدهکاریم" : s.AlbaqiAfn < 0m ? "پیش‌پرداخت" : "تسویه";

        Totals = new[]
        {
            new TotalCell("خرید (تن)", Shamsi.Money(Math.Round(ton, 3), 3)),
            new TotalCell("کل ($)", Shamsi.Money(Math.Round(s.TotalUsd, 1), 1) + " $"),
            new TotalCell("کل (افغانی)", Shamsi.Money(Math.Round(s.TotalAfn, 0, MidpointRounding.AwayFromZero))),
            new TotalCell("رسید", Shamsi.Money(Math.Round(s.PaidAfn, 0, MidpointRounding.AwayFromZero)), "Pump.Ok"),
            new TotalCell("الباقی", Shamsi.Money(Math.Round(s.AlbaqiAfn, 0, MidpointRounding.AwayFromZero)),
                          s.AlbaqiAfn > 0m ? "Pump.Danger" : "Pump.Ok"),
        };
        var nP = CompanyPurchaseService.Of(all, c.Name, FuelType.Petrol, h.PurchasesAfter, h.PurchasesBefore).Count;
        var nD = CompanyPurchaseService.Of(all, c.Name, FuelType.Diesel, h.PurchasesAfter, h.PurchasesBefore).Count;
        PetrolBuyText = "⛽ خریدهای پطرول (" + Shamsi.Money(nP) + ")";
        DieselBuyText = "🟤 خریدهای دیزل (" + Shamsi.Money(nD) + ")";
    }

    public CompanyTableArchive Entity { get; }
    public string Title { get; }
    public BulkRows<CompanyArchiveRowViewModel> Rows { get; } = new();
    public IReadOnlyList<TotalCell> Totals { get; }
    public string PetrolBuyText { get; }
    public string DieselBuyText { get; }
    [ObservableProperty] private int _highlightedRow = -1;

    // ══ نوارِ کشویی — همان الگوی آرشیوِ قرض‌داران ═══════════════════════════
    //
    // خواستهٔ صریحِ صاحب ریپو: «کاری کن آن جدول‌ها شبیهِ جدول‌های آرشیوِ
    // قرض‌داران بشود — همه یک جا ولی کشویی، هر کدام را خواستم باز کنم.»
    //
    // ⚠️ و این فقط ظاهر نیست: جدولِ بسته **نامرئی** است و ‎ExcelGrid‎
    // ردیف‌هایش را پارک می‌کند. پیش از این هر آرشیوِ این شرکت با همهٔ
    // ردیف‌هایش هم‌زمان زنده بود — ده آرشیوِ صدردیفی یعنی هزار ردیفِ زنده در
    // یک صفحه، همان چیزی که قاعدهٔ ‎idle‎ قدغنش کرده.
    public FuelType Fuel { get; }
    public string FuelWord { get; }
    public string DateText { get; }
    public string RowCountText { get; }
    public string BarText { get; }
    public string BarBrushKey { get; }

    [ObservableProperty] private bool _isOpen;

    [RelayCommand] private void Toggle() => IsOpen = !IsOpen;

    // ── شش عددِ سربرگ، همان‌هایی که جدولِ زنده دارد ──────────────────────
    public string TotalTonText { get; }
    public string TotalUsdText { get; }
    public string TotalAfnText { get; }
    public string PaidUsdText { get; }
    public string PaidAfnText { get; }
    public string AlbaqiAfnText { get; }
    public string AlbaqiUsdText { get; }
    public string AlbaqiBrushKey { get; }
    public string AlbaqiStatus { get; }

    /// <summary>
    /// «این آرشیو با جست‌وجو جور است؟» — تاریخ، تیل، و متنِ هر ردیفش.
    /// ⚠️ ردیف‌ها همین حالا در حافظه‌اند، پس هیچ پرس‌وجوی تازه‌ای نمی‌خواهد.
    /// </summary>
    public bool Matches(string q)
    {
        if (q.Length == 0) return true;
        if (BarText.Contains(q, StringComparison.OrdinalIgnoreCase)) return true;
        foreach (var r in Rows)
            if (r.Haystack.Contains(q, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    [RelayCommand] private Task OpenPurchases(string? fuel) =>
        _page.OpenPurchasesAsync(this, fuel == "diesel" ? FuelType.Diesel : FuelType.Petrol);
    [RelayCommand] private Task Delete() => _page.DeleteAsync(this);
}

/// <summary>
/// ══ «🗂️ جدول‌های آرشیو» — همهٔ آرشیوهای یک شرکت، یک‌جا و کشویی ═══════════
///
/// خواستهٔ صریحِ صاحب ریپو (۱۴۰۵/۰۷/۰۶): «کاری کن آن جدول‌ها شبیهِ جدول‌های
/// آرشیوِ قرض‌داران بشود — همه یک جا ولی کشویی، هر کدام را خواستم باز کنم و
/// حساب‌های مربوطِ همان را نشانم بدهد… و سرچ هم ندارد.»
///
/// ⛔ **دو صفحهٔ جدا برای پطرول و دیزل رفت.** پیش از این هر تیل صفحهٔ خودش را
/// داشت و از صفحهٔ حساب دو دکمهٔ جدا باز می‌شدند — همان «کادرِ آرشیو جا خیلی
/// می‌گیرد». حالا یک صفحه، هر دو تیل، و نوارِ رنگیِ هر آرشیو می‌گوید مالِ
/// کدام تیل است.
///
/// ⚠️ ‎Fuel‎ی صفحه برداشته نشد و همان تیلی است که کاربر از آن آمده — فقط
/// برای پیش‌باز کردنِ تازه‌ترین آرشیوِ همان تیل به کار می‌رود.
/// </summary>
public sealed partial class CompanyArchivePageViewModel : ObservableObject
{
    private readonly AppHost _host;
    private readonly CompanySectionViewModel _section;
    private readonly IReadOnlyList<FuelPurchase> _all;
    private readonly List<CompanyArchiveViewModel> _every = new();

    public CompanyArchivePageViewModel(AppHost host, TilCompany c, FuelType fuel, IReadOnlyList<CompanyTableArchive> arcs,
                                       IReadOnlyList<FuelPurchase> all, CompanySectionViewModel section)
    {
        _host = host; _section = section; _all = all;
        Company = c; Fuel = fuel;
        Title = "🗂️ جدول‌های آرشیو — " + (c.Name ?? "");

        // تازه‌ترین اول، هر دو تیل با هم
        foreach (var h in arcs.OrderByDescending(h => h.Id))
            _every.Add(new CompanyArchiveViewModel(h, c, host.Company, all, this));

        // ⚠️ تازه‌ترین آرشیوِ همان تیلی که کاربر از آن آمده، باز باشد — وگرنه
        // صفحه‌ای پر از نوارِ بسته باز می‌شود و کاربر نمی‌داند کدام را بزند.
        var first = _every.FirstOrDefault(a => a.Fuel == fuel) ?? _every.FirstOrDefault();
        if (first is not null) first.IsOpen = true;

        ApplyFilter();
        EmptyText = "این شرکت هنوز جدولِ آرشیوی ندارد";
    }

    public TilCompany Company { get; }
    public FuelType Fuel { get; }
    public string Title { get; }
    public string EmptyText { get; }
    public ObservableCollection<CompanyArchiveViewModel> Archives { get; } = new();
    public bool IsEmpty => Archives.Count == 0;

    /// <summary>«🔍 سرچ» — روی تاریخ، تیل و متنِ ردیف‌های هر آرشیو.</summary>
    [ObservableProperty] private string _search = "";

    partial void OnSearchChanged(string v) => ApplyFilter();

    private void ApplyFilter()
    {
        var q = (Search ?? "").Trim();
        Archives.Clear();
        foreach (var a in _every) if (a.Matches(q)) Archives.Add(a);
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(CountText));
    }

    public string CountText => Shamsi.Money(Archives.Count) + " جدول از "
                             + Shamsi.Money(_every.Count);

    public void Highlight(long archiveId, int rowIndex)
    {
        foreach (var a in Archives)
        {
            a.HighlightedRow = a.Entity.Id == archiveId ? rowIndex : -1;
            // ⚠️ آرشیوی که «برو به همان ردیف» نشانش می‌دهد باید باز باشد،
            // وگرنه کاربر به نوارِ بسته فرستاده می‌شود.
            if (a.Entity.Id == archiveId) a.IsOpen = true;
        }
    }

    [RelayCommand] private void Close() => _section.CloseOverlay();

    /// <summary>
    /// ‎openCompanyPurchasesHistoryFuel‎ — خریدهای همان بازه: اگر جدولِ هم‌زمانِ
    /// آن تیل هست همان، وگرنه خریدهای همان بازه از آن تیل.
    /// </summary>
    public async Task OpenPurchasesAsync(CompanyArchiveViewModel a, FuelType fuel)
    {
        var h = a.Entity;
        if (h.Fuel == fuel) { await _section.OpenPurchasesAsync(Company, fuel, h); return; }
        var all = await _host.Companies.ListArchivesAsync(Company.Id);
        var sib = all.FirstOrDefault(x => x.Id != h.Id && x.Fuel == fuel && x.PurchasesBefore == h.PurchasesBefore);
        await _section.OpenPurchasesAsync(Company, fuel,
            sib ?? new CompanyTableArchive { CompanyId = Company.Id, Fuel = fuel, CreatedShamsi = h.CreatedShamsi,
                                             PurchasesAfter = h.PurchasesAfter, PurchasesBefore = h.PurchasesBefore, RowsJson = "[]" });
    }

    public Task DeleteAsync(CompanyArchiveViewModel a) => CrashGuard.RunAsync("حذف آرشیو", async () =>
    {
        if (!await Dialogs.ConfirmAsync("حذف جدول آرشیو",
                "این جدولِ آرشیو با " + Shamsi.Money(a.Entity.RowCount) + " ردیف حذف شود؟")) return;
        await _host.Companies.DeleteArchiveAsync(a.Entity.Id);
        _every.Remove(a);
        Archives.Remove(a);
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(CountText));
        if (_section.Page is { } p) await p.RefreshMetaAsync();
        _host.Toast("🗑️ آرشیو حذف شد", ToastKind.Warn);
    });
}

/// <summary>یک موردِ پیداشده در جستجو.</summary>
public sealed partial class PurchaseHitViewModel : ObservableObject
{
    private readonly CompanySearchPageViewModel _page;
    public PurchaseHitViewModel(PurchaseHit h, CompanySearchPageViewModel page)
    {
        _page = page; Hit = h;
        FuelText = h.Fuel == FuelType.Diesel ? "🟤 دیزل" : "⛽ پطرول";
        FuelBrushKey = h.Fuel == FuelType.Diesel ? "Pump.Warn" : "Pump.Ok";
        DateText = "📅 " + (h.Date.Length > 0 ? h.Date : "—") + " · " + h.Where;
        var kg = Math.Round(h.Ton * 1000m, 0, MidpointRounding.AwayFromZero);
        TonText = Shamsi.Money(kg / 1000m, 3) + " تن";
        UsdText = Shamsi.Money(Math.Round(h.Usd, 0, MidpointRounding.AwayFromZero)) + " $";
        AfnText = Shamsi.Money(Math.Round(h.Afn, 0, MidpointRounding.AwayFromZero)) + " افغانی";
        GoText = h.IsPurchase ? "↗ رفتن به خریدهای " + (h.Fuel == FuelType.Diesel ? "دیزل" : "پطرول") : "↗ رفتن به این جدول";
        CanGo = h.CompanyId != 0;
    }
    public PurchaseHit Hit { get; }
    public string Name => Hit.Name;
    public string FuelText { get; }
    public string FuelBrushKey { get; }
    public string DateText { get; }
    public string TonText { get; }
    public string UsdText { get; }
    public string AfnText { get; }
    public string GoText { get; }
    public bool CanGo { get; }
    [RelayCommand] private Task Go() => _page.GoAsync(this);
}

/// <summary>
/// «🔍 جستجوی خرید» — ‎cmpSearchModal‎ / ‎runCmpSearch‎: مقدار (کیلو یا تن) و/یا
/// تاریخ، با انتخابِ تیل؛ همان لحظه پیدا می‌شود و «رفتن» ما را سرِ همان خرید
/// یا همان جدول می‌برد. فقط می‌خواند.
/// </summary>
public sealed partial class CompanySearchPageViewModel : ObservableObject
{
    private readonly AppHost _host;
    private readonly CompanySectionViewModel _section;
    private readonly long? _companyId;

    public CompanySearchPageViewModel(AppHost host, long? companyId, CompanySectionViewModel section)
    {
        _host = host; _companyId = companyId; _section = section;
        ScopeText = "در خریدها و جدول‌های همهٔ شرکت‌ها";
        Message = "مقدار (کیلو یا تن) یا تاریخ را بنویسید و «جستجو» را بزنید";
        _ = LoadScopeAsync();
    }

    private async Task LoadScopeAsync()
    {
        if (_companyId is null) return;
        var c = await _host.Companies.LoadAsync(_companyId.Value);
        if (c is not null) ScopeText = "در خریدها و جدول‌های «" + c.Name + "»";
    }

    [ObservableProperty] private string _scopeText = "";
    [ObservableProperty] private string _qtyText = "";
    [ObservableProperty] private string _dateText = "";
    /// <summary>۰ هر دو · ۱ پطرول · ۲ دیزل — ‎cs-fuel‎.</summary>
    [ObservableProperty] private int _fuelIndex;
    [ObservableProperty] private string _message = "";
    [ObservableProperty] private bool _notFound;
    public bool IsAll => FuelIndex == 0;
    public bool IsPetrol => FuelIndex == 1;
    public bool IsDiesel => FuelIndex == 2;
    partial void OnFuelIndexChanged(int v)
    {
        foreach (var n in new[] { nameof(IsAll), nameof(IsPetrol), nameof(IsDiesel) }) OnPropertyChanged(n);
    }
    [RelayCommand] private void SetFuel(string? f) => FuelIndex = f == "petrol" ? 1 : f == "diesel" ? 2 : 0;

    public BulkRows<PurchaseHitViewModel> Results { get; } = new();

    [RelayCommand] private void Close() => _section.CloseOverlay();

    [RelayCommand]
    private Task RunAsync() => CrashGuard.RunAsync("جستجوی خرید", async () =>
    {
        var raw = Shamsi.ToEnDigits(QtyText).Trim();
        decimal? qty = decimal.TryParse(new string(raw.Where(ch => char.IsDigit(ch) || ch == '.' || ch == '-').ToArray()),
                                        System.Globalization.NumberStyles.Float,
                                        System.Globalization.CultureInfo.InvariantCulture, out var q) ? q : null;
        var dq = CompanyPurchaseService.DateNorm(DateText);
        if (qty is null && dq.Length == 0)
        {
            Results.ResetTo(Array.Empty<PurchaseHitViewModel>());
            NotFound = false;
            Message = "اول مقدار یا تاریخ را بنویسید";
            return;
        }
        var purchases = await _host.StorageData.AllPurchasesAsync();
        var companies = await _host.Companies.ListAsync();
        var arcs = (await _host.Companies.AllArchivesAsync())
                   .Select(h => (h, (IReadOnlyList<CompanyRow>)CompanyDataService.ArchiveRows(h))).ToList();
        FuelType? fuel = FuelIndex == 1 ? FuelType.Petrol : FuelIndex == 2 ? FuelType.Diesel : null;
        var hits = _host.CompanyPurchases.Search(purchases, companies, arcs, qty, DateText, fuel, _companyId);
        Results.ResetTo(hits.Select(h => new PurchaseHitViewModel(h, this)));
        NotFound = hits.Count == 0;
        Message = hits.Count == 0 ? "پیدا نشد — با این مقدار/تاریخ چیزی ثبت نشده است"
                                  : Shamsi.Money(hits.Count) + " مورد پیدا شد — روی «رفتن» بزنید";
    });

    public Task GoAsync(PurchaseHitViewModel hit) => _section.GoToAsync(hit.Hit);
}
