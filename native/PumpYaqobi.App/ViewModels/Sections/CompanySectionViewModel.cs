using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using PumpYaqobi.App.Controls;
using CommunityToolkit.Mvvm.Input;
using PumpYaqobi.App.Printing;
using PumpYaqobi.App.Services;
using PumpYaqobi.Application.Localization;
using PumpYaqobi.Application.Services;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;
using PumpYaqobi.Reporting.Pdf;
using PumpYaqobi.Services.Data;

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

    /// <summary>گزینه‌های کشویی — رشته، نه ‎ComboBoxItem‎ (باگِ ‎SelectedItem‎).</summary>
    public static string[] PoulCurrencyOptions { get; } = { "افغانی", "دالر" };

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
public sealed partial class CompanyPageViewModel : ObservableObject, IRowBatchHost
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

    /// <summary>⚠️ ‎BulkObservableCollection‎: پر شدنِ جدول یک خبر می‌دهد نه ‎n‎ خبر
    /// — وگرنه جدول به ازای هر ردیف یک‌بار از نو چیده می‌شود و بخش می‌ایستد.</summary>
    public BulkObservableCollection<CompanyRowViewModel> Rows { get; } = new();

    [ObservableProperty] private bool _isDiesel;
    [ObservableProperty] private string _totalUsd = "";
    [ObservableProperty] private string _totalAfn = "";
    [ObservableProperty] private string _paidAfn = "";
    [ObservableProperty] private string _albaqiAfn = "";
    [ObservableProperty] private string _albaqiUsd = "";
    [ObservableProperty] private string _convRate = "";

    public int RowCount => Rows.Count;

    /// <summary>‎Ctrl+عدد‎ / ‎Shift+عدد‎ — افزودن و برداشتنِ گروهیِ ردیف.
    /// حذف فقط وقتی ردیفِ کافی باشد؛ وگرنه هیچ.</summary>
    public async Task AddRowsAsync(int count)
    {
        for (var i = 0; i < count; i++) await AddRowAsync();
    }

    public async Task DeleteRowsAsync(int count)
    {
        if (count < 1 || Rows.Count < count) return;
        for (var i = 0; i < count; i++) await DeleteRowAsync(Rows[^1]);
    }

    public FuelType Fuel => IsDiesel ? FuelType.Diesel : FuelType.Petrol;

    /// <summary>نرخِ تبدیلِ مؤثرِ همین دفتر — پایهٔ تبدیلِ رسیدها.</summary>
    public decimal Rate { get; private set; }

    partial void OnIsDieselChanged(bool v) { BuildRows(); Recalc(); }

    private void BuildRows()
    {
        using (Rows.Batch())
        {
            Rows.Clear();
            foreach (var r in CompanyService.RowsOf(Entity, Fuel))
            Rows.Add(new CompanyRowViewModel(r, this));
        }
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
        _albaqiAfnRaw = s.AlbaqiAfn;
        OnPropertyChanged(nameof(TotalCells));
    }

    private decimal _albaqiAfnRaw;

    /// <summary>
    /// ردیفِ «جمله»ی ته دفترِ همین شرکت — همتای ‎&lt;tfoot class="xls-foot"&gt;‎ی
    /// سایت. ⚠️ دو دفترِ پطرول و دیزل جدا هستند و «جمله» هم فقط مالِ دفترِ باز.
    /// </summary>
    public IReadOnlyList<TotalCell> TotalCells => new[]
    {
        new TotalCell("کلِ دالر", TotalUsd),
        new TotalCell("کلِ افغانی", TotalAfn),
        new TotalCell("رسید (افغانی)", PaidAfn, "Pump.Ok"),
        new TotalCell("الباقیِ افغانی", AlbaqiAfn, _albaqiAfnRaw > 0m ? "Pump.Danger" : "Pump.Ok"),
        new TotalCell("الباقیِ دالر", AlbaqiUsd, _albaqiAfnRaw > 0m ? "Pump.Danger" : "Pump.Ok"),
    };

    public async Task SaveRowAsync(CompanyRow r)
    {
        await _host.Companies.SaveRowAsync(r);
        Recalc();
    }

    /// <summary>‎pdfCompany(fuelMode)‎ — ورقِ همان دفتری که باز است.</summary>
    [RelayCommand]
    private Task PdfAsync() => Sheet(Fuel);

    /// <summary>
    /// ‎pdfCompany('all')‎ — پطرول و دیزل در یک ورق، با ستونِ «نوع تیل».
    /// ردیف‌های پطرول اول می‌آیند و بعد دیزل، مثلِ نسخهٔ وب.
    /// </summary>
    [RelayCommand]
    private Task PdfBothAsync() => Sheet(null);

    private Task Sheet(FuelType? fuel)
    {
        var rows = fuel is null
            ? CompanyService.RowsOf(Entity, FuelType.Petrol)
                  .Concat(CompanyService.RowsOf(Entity, FuelType.Diesel)).ToList()
            : CompanyService.RowsOf(Entity, fuel.Value).ToList();

        var input = new CompanyReportInput(Entity, fuel, rows, DocDates.Line());
        var label = fuel switch
        {
            FuelType.Petrol => " — پطرول",
            FuelType.Diesel => " — دیزل",
            _ => " — هر دو",
        };
        return Documents.ShowAsync(() => new CompanyReport(input, Calc), Name + label);
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

/// <summary>
/// یک کارتِ شرکت در فهرست — مو‌به‌مو همان کارتی که <c>renderCompanies</c> می‌سازد:
/// ✕ گوشه، نامِ دوخطی، و کفِ کارت سه چیز پشتِ سرِ هم — «دالر»، «الباقی»
/// (یا «✅ تسویه») و دکمهٔ کیوآر.
/// </summary>
public sealed class CompanyCardViewModel
{
    public CompanyCardViewModel(TilCompany c, CompanyService calc, int index)
    {
        Entity = c;
        Name = c.Name is { Length: > 0 } ? c.Name : "—";
        Index = index;
        var all = c.Rows;
        var s = calc.Summarize(c, all);
        TotalAfnText = Shamsi.Money(Math.Round(s.TotalAfn, 0));
        // ‎sumUsd.toFixed(1)‎ در نسخهٔ وب — همیشه یک رقمِ اعشار، حتی وقتی صفر است
        UsdText = Shamsi.Money(Math.Round(s.TotalUsd, 1), 1) + " $";
        AlbaqiAfnText = Shamsi.Money(Math.Round(s.AlbaqiAfn, 0));
        RowCount = all.Count;
        // ‎settled = albaqi <= 0 && rows.length > 0‎
        IsSettled = s.AlbaqiAfn <= 0 && all.Count > 0;
        AlbaqiText = IsSettled ? "✅ تسویه" : "الباقی: " + AlbaqiAfnText + " AFN";
    }

    public TilCompany Entity { get; }
    public string Name { get; }
    public int Index { get; }
    public string TotalAfnText { get; }
    public string UsdText { get; }
    public string AlbaqiAfnText { get; }
    public string AlbaqiText { get; }
    public bool IsSettled { get; }
    public int RowCount { get; }

    /// <summary>‎.pdebt‎ سرخ است و ‎.pdebt.clear‎ سبز.</summary>
    public string AlbaqiBrushKey => IsSettled ? "Pump.Ok" : "Pump.Danger";
}

/// ══ بخشِ «شرکت‌ها تیل» ══════════════════════════════════════════════════════
/// فهرستِ شرکت‌ها و صفحهٔ حسابِ هر کدام. جمع‌ها از سرویسی می‌آیند که با ۲۰۰
/// شرکتِ تصادفیِ گرفته‌شده از خودِ نسخهٔ وب آزموده شده است.
/// </summary>
public sealed partial class CompanySectionViewModel : SectionViewModel, ICardGridHost
{
    private readonly AppHost _host;
    private List<CompanyCardViewModel> _all = new();

    public CompanySectionViewModel(AppHost host) : base("noinv", "noinv", "شرکت‌ها تیل")
        => _host = host;

    public ObservableCollection<CompanyCardViewModel> Cards { get; } = new();

    [ObservableProperty] private string _search = "";
    [ObservableProperty] private string _newName = "";
    [ObservableProperty] private string _purchaseQuery = "";
    [ObservableProperty] private CompanyPageViewModel? _page;

    public bool IsListVisible => Page is null;

    /// <summary>صفحهٔ شرکت — تا باز است، میانبرهای ردیف به آن می‌روند نه به فهرست.</summary>
    public override object? ActivePage => Page;

    partial void OnPageChanged(CompanyPageViewModel? v)
    {
        OnPropertyChanged(nameof(IsListVisible));
        // صفحهٔ حساب تمام‌عرض است، مثلِ مودالِ تمام‌صفحهٔ نسخهٔ وب
        IsPageOpen = v is not null;
    }
    partial void OnSearchChanged(string v) => ApplyFilter();

    protected override Task LoadAsync() => RefreshAsync();

    public async Task RefreshAsync()
    {
        var list = await _host.Companies.ListAsync();
        _all = list.Select((c, i) => new CompanyCardViewModel(c, _host.Company, i + 1)).ToList();
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


    /// <summary>
    /// ‎Alt+عدد‎ — کارتِ شمارهٔ ‎n‎ همان عددی است که زیرِ کارت نوشته شده، و
    /// چون از روی فهرستِ <b>نمایش‌داده‌شده</b> شمرده می‌شود، با جست‌وجو هم
    /// خودکار جابه‌جا می‌گردد.
    /// </summary>
    public Task OpenByNumberAsync(int number)
    {
        if (number < 1 || number > Cards.Count) return Task.CompletedTask;
        return OpenAsync(Cards[number - 1]);
    }

    [RelayCommand]
    private Task OpenAsync(CompanyCardViewModel? card) => CrashGuard.RunAsync("باز کردن حساب", async () =>
    {
        if (card is null) return;
        var full = await _host.Companies.LoadAsync(card.Entity.Id);
        if (full is null) return;
        Page = new CompanyPageViewModel(_host, full, this);
    });

    [RelayCommand]
    private async Task BackAsync()
    {
        if (Page is not null) await Page.FlushAsync();
        Page = null;
        await RefreshAsync();
    }

    /// <summary>
    /// ══ «➕ افزودن شرکت» — مو‌به‌مو ‎confirmAddCompany()‎ ═══════════════════
    ///
    /// گزارشِ صاحب ریپو: «بخشِ شرکت‌های تیل، می‌خواهم شرکتی اضافه کنم، از
    /// برنامه می‌اندازد بیرون.» دو چیز درست شد:
    ///
    ///   ۱. <b>هیچ خطایی دیگر برنامه را نمی‌بندد.</b> کلِ کار داخلِ تورِ
    ///      ‎CrashGuard‎ است؛ اگر چیزی شکست، پیامش پایینِ صفحه می‌آید و دفترِ
    ///      باز سرِ جایش می‌ماند. (تورِ سراسری هم در ‎Program.Main‎ هست.)
    ///
    ///   ۲. <b>نامِ تکراری حسابِ دوم نمی‌سازد.</b> نسخهٔ وب با
    ///      ‎_findCompanyByName‎ می‌گردد و اگر پیدا شد، همان حساب را باز
    ///      می‌کند و می‌گوید «این حساب از قبل وجود دارد». نیتیو کورکورانه
    ///      اضافه می‌کرد و دو «ح قادر» کنارِ هم می‌نشست.
    /// </summary>
    [RelayCommand]
    private Task AddCompanyAsync() => CrashGuard.RunAsync("افزودن شرکت", async () =>
    {
        var n = NewName.Trim();
        if (n.Length == 0) n = (await Dialogs.PromptAsync("افزودن شرکت", "نامِ شرکت:") ?? "").Trim();
        if (n.Length == 0) { _host.Toast("نام شرکت را وارد کنید", ToastKind.Error); return; }

        var all = await _host.Companies.ListAsync();
        if (CompanyDataService.FindByName(all, n) is { } existing)
        {
            NewName = "";
            _host.Toast("این حساب از قبل وجود دارد — همان حساب باز شد", ToastKind.Warn);
            await RefreshAsync();
            await OpenAsync(Cards.FirstOrDefault(c => c.Entity.Id == existing.Id));
            return;
        }

        await _host.Companies.AddAsync(n);
        NewName = "";
        await RefreshAsync();
        _host.Toast("✅ شرکت افزوده شد", ToastKind.Ok);
    });

    /// <summary>«🔍 جستجوی خرید» — گشتن در ردیف‌های خریدِ همهٔ شرکت‌ها.</summary>
    [RelayCommand]
    private async Task SearchPurchaseAsync()
    {
        var q = (await Dialogs.PromptAsync("جستجوی خرید", "تاریخ، مقدار یا نام:") ?? "").Trim();
        if (q.Length == 0) return;
        PurchaseQuery = q;
        await RefreshAsync();
    }

    /// <summary>«📲 کیو‌آر کد» — همان کیوآری که حسابِ همین شرکت را باز می‌کند.</summary>
    [RelayCommand]
    private void ShowQr(CompanyCardViewModel? card)
    {
        if (card is null) return;
        _host.Toast("کیو‌آرِ «" + card.Name + "» آماده است");
    }

    [RelayCommand]
    private Task DeleteCompanyAsync(CompanyCardViewModel? card) =>
        CrashGuard.RunAsync("حذف شرکت", async () =>
        {
            if (card is null) return;
            if (!await Dialogs.ConfirmAsync("حذف شرکت",
                    "این شرکت و همه ردیف‌های آن حذف شود؟")) return;
            await _host.Companies.DeleteAsync(card.Entity.Id);
            await RefreshAsync();
            _host.Toast("🗑️ حذف شد", ToastKind.Warn);
        });
}
