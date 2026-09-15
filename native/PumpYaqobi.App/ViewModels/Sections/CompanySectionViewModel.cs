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
using PumpYaqobi.Services.Vision;

namespace PumpYaqobi.App.ViewModels.Sections;

/// <summary>یک ردیفِ حسابِ شرکت. همهٔ عددهای محاسبه‌ای از CompanyService می‌آیند.</summary>
public sealed partial class CompanyRowViewModel : RowViewModel, ILockedRow
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

    /// <summary>
    /// ردیفی که از خریدِ مخزن آمده (‎srcPurchaseId‎): در سایت با نشانِ 📦 و
    /// خانه‌های فقط‌خواندنی. حذفش آزاد است — «فقط همین ردیفِ حساب پاک می‌شود،
    /// خریدِ مخزن سرِ جایش می‌ماند».
    /// </summary>
    public bool IsLinked => !string.IsNullOrWhiteSpace(_r.SourcePurchaseId);
    public bool IsLocked => IsLinked;
    public string LinkMark => IsLinked ? "📦" : "";

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
    partial void OnIsUsdPayChanged(bool v)
    {
        Touch(); Refresh();
        OnPropertyChanged(nameof(PoulCurrencyText));
        OnPropertyChanged(nameof(PoulCurrencyChipBrushKey));
    }

    private void Refresh()
    {
        foreach (var n in new[] { nameof(KgText), nameof(TonText), nameof(UsdText), nameof(RateText),
                                  nameof(PoulText), nameof(TotalUsdText), nameof(TotalAfnText),
                                  nameof(AlbaqiAfnText), nameof(AlbaqiUsdText) })
            OnPropertyChanged(n);
        _owner.Recalc();
    }

    public string KgText { get => Shamsi.MoneyOrBlank(Kg); set => Kg = Shamsi.Num(value); }
    /// <summary>ستونِ «خرید (تن)» — ردیفِ کهنه که فقط کیلو دارد هم به تن دیده می‌شود (‎cmpTon‎).</summary>
    public string TonText { get => Shamsi.MoneyOrBlank(_owner.Calc.Ton(_r)); set => Ton = Shamsi.Num(value); }
    public string UsdText { get => Shamsi.MoneyOrBlank(Usd); set => Usd = Shamsi.Num(value); }
    public string RateText { get => Shamsi.MoneyOrBlank(Rate); set => Rate = Shamsi.Num(value); }
    public string PoulText { get => Shamsi.MoneyOrBlank(Poul); set => Poul = Shamsi.Num(value); }

    /// <summary>گزینه‌های کشویی — رشته، نه ‎ComboBoxItem‎ (باگِ ‎SelectedItem‎).</summary>
    public static string[] PoulCurrencyOptions { get; } = { "افغانی", "دالر" };

    public string PoulCurrencyText
    {
        get => IsUsdPay ? "دالر" : "افغانی";
        set => IsUsdPay = value == "دالر";
    }

    // ══ کپسول به‌جای کشویی ════════════════════════════════════════════════
    // خواستهٔ صاحب ریپو: «اون علامتِ ▾ فقط جا گرفته» و «با تب یا اینتر عوض
    // بشه». ‎ExcelGrid‎ کلاسِ ‎celltoggle‎ را با ‎Enter‎/‎Tab‎ می‌زند.
    public string PoulCurrencyChipBrushKey => IsUsdPay ? "Pump.Accent" : "Pump.Text";

    [RelayCommand]
    private void TogglePoulCurrency() => IsUsdPay = !IsUsdPay;

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

    /// <summary>نوارِ «➕ ردیف / ➕➕ چندتایی»ِ پایینِ جدول — همتای ‎addCompanyRowsBulk‎ی سایت.</summary>
    public System.Windows.Input.ICommand? RowAddCommand => AddRowCommand;

    public CompanyPageViewModel(AppHost host, TilCompany c, CompanySectionViewModel section)
    {
        _host = host; _section = section; Entity = c;
        Actions.Add(new SetupOption("", "☰ کارها — انتخاب کنید…", "", ""));
        Actions.Add(new SetupOption("new", "📋 جدول جدید", "جدولِ فعلی آرشیو می‌شود و جدولِ خالی باز می‌شود", "📋"));
        Actions.Add(new SetupOption("buy-petrol", "⛽ خریدهای پطرول", "خریدهای مخزنِ همین شرکت", "⛽"));
        Actions.Add(new SetupOption("buy-diesel", "🟤 خریدهای دیزل", "خریدهای مخزنِ همین شرکت", "🟤"));
        Action = Actions[0];
        BuildRows();
        Recalc();
    }

    public TilCompany Entity { get; }
    public CompanyService Calc => _host.Company;
    public string Name => Entity.Name ?? "";

    // ══ سربرگِ صفحه — «قبلی/بعدی»، «تغییر نام» ════════════════════════════
    [ObservableProperty] private string _posText = "";
    [ObservableProperty] private bool _canPrev;
    [ObservableProperty] private bool _canNext;
    [ObservableProperty] private string _nameText = "";

    [RelayCommand] private Task PrevAsync() => _section.NavigateAsync(-1);
    [RelayCommand] private Task NextAsync() => _section.NavigateAsync(+1);

    /// <summary>‎renameCurrentCompany‎ — نامِ تازه؛ خریدهای مخزن با نامِ فروشنده پیدا می‌شوند، پس نام مهم است.</summary>
    [RelayCommand]
    private Task RenameAsync() => CrashGuard.RunAsync("تغییر نام", async () =>
    {
        var n = (await Dialogs.PromptAsync("✏️ تغییر نام", "نامِ تازهٔ شرکت:", Name, "✔ ذخیره") ?? "").Trim();
        if (n.Length == 0 || n == Name) return;
        await _host.Companies.RenameAsync(Entity.Id, n);
        Entity.Name = n;
        NameText = n;
        OnPropertyChanged(nameof(Name));
        await RefreshMetaAsync();
        _host.Toast("✅ نام عوض شد", ToastKind.Ok);
    });

    // ══ کادرِ کشوییِ «کارها» — ‎cm-actions‎ / ‎cmDoAction‎ ═══════════════════
    // خواستهٔ صاحب ریپو در سایت: «یک کادرِ کشویی به‌جای ردیفِ شلوغِ دکمه‌ها».
    // «حذف این جدول» عمداً این‌جا نیست و ته صفحه، دور از بقیه، نشسته.
    public ObservableCollection<SetupOption> Actions { get; } = new();
    [ObservableProperty] private SetupOption? _action;
    private bool _actionBusy;

    partial void OnActionChanged(SetupOption? v)
    {
        if (v is null || v.Value.Length == 0 || _actionBusy) return;
        _actionBusy = true;
        try
        {
            switch (v.Value)
            {
                case "new": _ = NewTableAsync(); break;
                case "buy-petrol": _ = OpenPurchasesAsync("petrol"); break;
                case "buy-diesel": _ = OpenPurchasesAsync("diesel"); break;
            }
            Action = Actions[0];      // ‎this.selectedIndex = 0‎ی سایت
        }
        finally { _actionBusy = false; }
    }

    // ══ خریدهای مخزن و جدول‌های آرشیو — شمارنده‌های سربرگ ══════════════════
    [ObservableProperty] private string _petrolBuyText = "⛽ خریدهای پطرول";
    [ObservableProperty] private string _dieselBuyText = "🟤 خریدهای دیزل";
    [ObservableProperty] private string _arcPetrolText = "";
    [ObservableProperty] private string _arcDieselText = "";
    [ObservableProperty] private bool _hasArcPetrol;
    [ObservableProperty] private bool _hasArcDiesel;
    public bool HasArchives => HasArcPetrol || HasArcDiesel;
    partial void OnHasArcPetrolChanged(bool v) => OnPropertyChanged(nameof(HasArchives));
    partial void OnHasArcDieselChanged(bool v) => OnPropertyChanged(nameof(HasArchives));

    /// <summary>‎renderCompanyPurchases‎ + ‎_renderCompanyArcBoxes‎ — شمارنده‌ها از نو.</summary>
    public async Task RefreshMetaAsync()
    {
        var all = await _host.StorageData.AllPurchasesAsync();
        var nP = CompanyPurchaseService.Live(all, Entity, FuelType.Petrol).Count;
        var nD = CompanyPurchaseService.Live(all, Entity, FuelType.Diesel).Count;
        PetrolBuyText = "⛽ خریدهای پطرول (" + Shamsi.Money(nP) + ")";
        DieselBuyText = "🟤 خریدهای دیزل (" + Shamsi.Money(nD) + ")";

        var arcs = await _host.Companies.ListArchivesAsync(Entity.Id);
        var aP = arcs.Count(h => h.Fuel != FuelType.Diesel);
        var aD = arcs.Count(h => h.Fuel == FuelType.Diesel);
        HasArcPetrol = aP > 0; HasArcDiesel = aD > 0;
        ArcPetrolText = "🗂️ جدول‌های آرشیو پطرول (" + Shamsi.Money(aP) + ")";
        ArcDieselText = "🗂️ جدول‌های آرشیو دیزل (" + Shamsi.Money(aD) + ")";
    }

    [RelayCommand]
    private Task OpenPurchasesAsync(string? fuel) =>
        _section.OpenPurchasesAsync(Entity, fuel == "diesel" ? FuelType.Diesel : fuel == "petrol" ? FuelType.Petrol : null, null);

    [RelayCommand]
    private Task OpenArchiveAsync(string? fuel) =>
        _section.OpenArchiveAsync(Entity, fuel == "diesel" ? FuelType.Diesel : FuelType.Petrol);

    /// <summary>«🔍 جستجوی خرید» — در خریدها و جدول‌های همین شرکت.</summary>
    [RelayCommand]
    private Task SearchAsync() => _section.OpenSearchAsync(Entity.Id);

    /// <summary>
    /// ‎newCompanyTable‎ — فقط جدولِ همان تیلی که باز است نو می‌شود؛ پطرول و دیزل
    /// دو دفترِ جدا هستند.
    /// </summary>
    [RelayCommand]
    private Task NewTableAsync() => CrashGuard.RunAsync("جدول جدید", async () =>
    {
        var fuelWord = IsDiesel ? "🟤 دیزل" : "⛽ پطرول";
        if (Rows.Count == 0) { _host.Toast("جدولِ " + fuelWord + " خالی است", ToastKind.Error); return; }
        if (!await Dialogs.ConfirmAsync("📋 جدول جدید",
                "جدولِ فعلیِ " + fuelWord + " آرشیو می‌شود و جدولِ خالیِ تازه‌ای باز می‌شود. ادامه؟")) return;
        await FlushAsync();
        await _host.Companies.ArchiveTableAsync(Entity.Id, Fuel, Shamsi.Today());
        Entity.Rows.RemoveAll(r => r.Fuel == Fuel);
        var full = await _host.Companies.LoadAsync(Entity.Id);
        if (full is not null)
        {
            Entity.PurchaseCheckpointPetrol = full.PurchaseCheckpointPetrol;
            Entity.PurchaseCheckpointDiesel = full.PurchaseCheckpointDiesel;
        }
        BuildRows();
        Recalc();
        await RefreshMetaAsync();
        _host.Toast("✅ جدولِ " + fuelWord + " نو شد — جدولِ قبلی در کادرِ «جدول‌های آرشیو» است", ToastKind.Ok);
    });

    /// <summary>‎deleteCurrentCompanyTable‎ — فقط ردیف‌های جدولِ فعلی؛ آرشیوها دست‌نخورده.</summary>
    [RelayCommand]
    private Task ClearTableAsync() => CrashGuard.RunAsync("حذف این جدول", async () =>
    {
        var fuelWord = IsDiesel ? "🟤 دیزل" : "⛽ پطرول";
        if (!await Dialogs.ConfirmAsync("🗑️ حذف این جدول",
                "جدولِ فعلیِ " + fuelWord + " حسابِ «" + Name + "» با " + Shamsi.Money(Rows.Count)
                + " ردیف پاک شود؟\n\nجدول‌های آرشیو دست نمی‌خورند.")) return;
        await FlushAsync();
        await _host.Companies.ClearTableAsync(Entity.Id, Fuel);
        Entity.Rows.RemoveAll(r => r.Fuel == Fuel);
        BuildRows();
        Recalc();
        _host.Toast("🗑️ جدولِ فعلی پاک شد — آرشیوها دست‌نخورده‌اند", ToastKind.Warn);
    });

    /// <summary>⚠️ ‎BulkRows‎: پر شدنِ جدول یک خبر می‌دهد نه ‎n‎ خبر
    /// — وگرنه جدول به ازای هر ردیف یک‌بار از نو چیده می‌شود و بخش می‌ایستد.</summary>
    public BulkRows<CompanyRowViewModel> Rows { get; } = new();

    [ObservableProperty] private bool _isDiesel;
    public bool IsPetrol => !IsDiesel;

    /// <summary>پطرول/دیزل با دو دکمهٔ رادیویی — نه کلیدِ لغزان.</summary>
    [RelayCommand]
    private void SetFuel(string? which) => IsDiesel = which == "diesel";
    [ObservableProperty] private string _totalTon = "";
    [ObservableProperty] private string _paidUsd = "";
    [ObservableProperty] private string _albaqiStatus = "";
    [ObservableProperty] private string _albaqiBrushKey = "Pump.Muted";
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

    partial void OnIsDieselChanged(bool v) { OnPropertyChanged(nameof(IsPetrol)); BuildRows(); Recalc(); }

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
        // «جمله مقدار (تن)» — سایت کیلو و تن را با هم می‌نوشت؛ خواستهٔ صاحب ریپو: فقط تن
        TotalTon = Shamsi.Money(Math.Round(rows.Sum(r => Calc.Ton(r)), 2), 2) + " تن";
        PaidUsd = Shamsi.Money(Math.Round(s.PaidUsd, 1), 1) + " $";
        AlbaqiStatus = s.AlbaqiAfn > 0m ? "🔴 بدهکاریم" : s.AlbaqiAfn < 0m ? "🟢 طلبکاریم" : "⚪ تسویه";
        AlbaqiBrushKey = s.AlbaqiAfn > 0m ? "Pump.Danger" : s.AlbaqiAfn < 0m ? "Pump.Ok" : "Pump.Muted";
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
        // ⚠️ برچسب‌ها همان سربرگِ ستون‌های جدول‌اند تا هر جمع زیرِ ستونِ خودش بنشیند
        new TotalCell("خرید (تن)", TotalTon),
        new TotalCell("کل ($)", TotalUsd),
        new TotalCell("کل (افغانی)", TotalAfn),
        new TotalCell("رسید (افغانی)", PaidAfn, "Pump.Ok", "رسید"),
        new TotalCell("الباقی", AlbaqiAfn, _albaqiAfnRaw > 0m ? "Pump.Danger" : "Pump.Ok"),
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
    [ObservableProperty] private CompanyPageViewModel? _page;

    /// <summary>
    /// صفحهٔ رویی — «خریدها»، «جدول‌های آرشیو» یا «جستجوی خرید». در سایت هر
    /// کدام یک مودالِ جداگانه بود که روی حساب باز می‌شد؛ این‌جا هم روی حساب
    /// می‌نشیند و با «✕» به همان حساب برمی‌گردد.
    /// </summary>
    [ObservableProperty] private object? _overlay;

    public bool IsListVisible => Page is null && Overlay is null;
    public bool IsPageVisible => Page is not null && Overlay is null;
    public bool IsOverlayVisible => Overlay is not null;

    /// <summary>صفحهٔ شرکت — تا باز است، میانبرهای ردیف به آن می‌روند نه به فهرست.</summary>
    public override object? ActivePage => Page;

    partial void OnPageChanged(CompanyPageViewModel? v)
    {
        OnPropertyChanged(nameof(IsListVisible));
        OnPropertyChanged(nameof(IsPageVisible));
        // صفحهٔ حساب تمام‌عرض است، مثلِ مودالِ تمام‌صفحهٔ نسخهٔ وب
        IsPageOpen = v is not null || Overlay is not null;
    }

    partial void OnOverlayChanged(object? v)
    {
        OnPropertyChanged(nameof(IsListVisible));
        OnPropertyChanged(nameof(IsPageVisible));
        OnPropertyChanged(nameof(IsOverlayVisible));
        IsPageOpen = Page is not null || v is not null;
    }

    public void CloseOverlay() => Overlay = null;

    /// <summary>‎navigateCompany(dir)‎ — قبلی/بعدی در همان ترتیبِ فهرست.</summary>
    public async Task NavigateAsync(int delta)
    {
        if (Page is null) return;
        var idx = _all.FindIndex(c => c.Entity.Id == Page.Entity.Id);
        var to = idx + delta;
        if (idx < 0 || to < 0 || to >= _all.Count) return;
        await Page.FlushAsync();
        await OpenAsync(_all[to]);
    }

    private async Task<TilCompany?> FreshAsync(TilCompany c) => await _host.Companies.LoadAsync(c.Id) ?? c;

    /// <summary>‎openCompanyPurchases‎ / ‎openCompanyPurchasesHistory‎.</summary>
    public async Task OpenPurchasesAsync(TilCompany c, FuelType? fuel, CompanyTableArchive? archive)
    {
        var full = await FreshAsync(c) ?? c;
        var all = await _host.StorageData.AllPurchasesAsync();
        Overlay = new CompanyPurchasesPageViewModel(_host, full, fuel, archive, all, this);
    }

    /// <summary>‎openCompanyArchive(fuel)‎ — صفحهٔ جداگانهٔ جدول‌های آرشیوِ همان تیل.</summary>
    public async Task OpenArchiveAsync(TilCompany c, FuelType fuel)
    {
        var full = await FreshAsync(c) ?? c;
        var arcs = await _host.Companies.ListArchivesAsync(c.Id);
        var all = await _host.StorageData.AllPurchasesAsync();
        Overlay = new CompanyArchivePageViewModel(_host, full, fuel, arcs, all, this);
    }

    /// <summary>‎openCmpSearch(companyId)‎ — ‎null‎ یعنی همهٔ شرکت‌ها.</summary>
    public Task OpenSearchAsync(long? companyId)
    {
        Overlay = new CompanySearchPageViewModel(_host, companyId, this);
        return Task.CompletedTask;
    }

    /// <summary>‎csGoBuy‎ / ‎csGoRow‎ — رفتن به همان خرید یا همان جدول.</summary>
    public Task GoToAsync(PurchaseHit hit) => CrashGuard.RunAsync("رفتن", async () =>
    {
        if (hit.CompanyId == 0) { _host.Toast("این خرید به حسابِ هیچ شرکتی وصل نیست", ToastKind.Warn); return; }
        var card = _all.FirstOrDefault(c => c.Entity.Id == hit.CompanyId);
        if (card is null) { await RefreshAsync(); card = _all.FirstOrDefault(c => c.Entity.Id == hit.CompanyId); }
        if (card is null) return;
        Overlay = null;
        await OpenAsync(card);
        if (Page is null) return;
        Page.IsDiesel = hit.Fuel == FuelType.Diesel;

        if (hit.IsPurchase)
        {
            // خریدِ قدیمی که با «جدول جدید» آرشیو شده در صفحهٔ خریدهای «زنده» نیست
            var arcs = await _host.Companies.ListArchivesAsync(hit.CompanyId);
            var cp = hit.Fuel == FuelType.Diesel ? Page.Entity.PurchaseCheckpointDiesel : Page.Entity.PurchaseCheckpointPetrol;
            var h = hit.PurchaseId > cp ? null
                  : arcs.FirstOrDefault(x => x.Fuel == hit.Fuel && hit.PurchaseId > x.PurchasesAfter && hit.PurchaseId <= x.PurchasesBefore);
            await OpenPurchasesAsync(Page.Entity, hit.Fuel, h);
            if (Overlay is CompanyPurchasesPageViewModel pp) pp.Highlight(hit.PurchaseId);
        }
        else if (hit.ArchiveId != 0)
        {
            await OpenArchiveAsync(Page.Entity, hit.Fuel);
            if (Overlay is CompanyArchivePageViewModel ap) ap.Highlight(hit.ArchiveId, hit.RowIndex);
        }
        else
            _host.Toast("ردیفِ " + Shamsi.Money(hit.RowIndex + 1) + " جدولِ فعلی", ToastKind.Info);
    });
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
        var page = new CompanyPageViewModel(_host, full, this);
        var idx = _all.FindIndex(c => c.Entity.Id == full.Id);
        page.PosText = Shamsi.Money(idx + 1) + " از " + Shamsi.Money(_all.Count);
        page.CanPrev = idx > 0;
        page.CanNext = idx >= 0 && idx < _all.Count - 1;
        page.NameText = page.Name;
        Page = page;
        await page.RefreshMetaAsync();
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
        if (n.Length == 0) n = (await Dialogs.PromptAsync("افزودن شرکت تیل", "نام شرکت:", "", "✔ افزودن") ?? "").Trim();
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

    /// <summary>«🔍 جستجوی خرید» — ‎openCmpSearch()‎ روی همهٔ شرکت‌ها؛ با تن و/یا تاریخ.</summary>
    [RelayCommand]
    private Task SearchPurchaseAsync() => OpenSearchAsync(null);

    /// <summary>
    /// «📲 کیو‌آر کد» — حسابِ همین شرکت، داخلِ خودِ کد.
    ///
    /// ⚠️ این دکمه تا امروز فقط یک **پیام** می‌داد و هیچ کدی نمی‌ساخت.
    /// حالا مثلِ کیو‌آرِ قرض‌داران: عکسِ حساب فشرده می‌شود و در نشانیِ صفحهٔ
    /// ‎view/‎ می‌نشیند؛ گوشی بی رمز و بی سرور بازش می‌کند.
    /// </summary>
    [RelayCommand]
    private Task ShowQrAsync(CompanyCardViewModel? card) =>
        CrashGuard.RunAsync("کیو‌آر", async () =>
        {
            if (card is null) return;

            var full = await _host.Companies.LoadAsync(card.Entity.Id);

            // ⚠️ عکس را خودِ ‎AcctSnapshots‎ از روی **موجودیتِ شرکت** می‌سازد،
            // نه از روی ردیف‌هایی که این‌جا آماده شوند: تفکیکِ پطرول/دیزل و
            // آرشیوِ ماه‌ها هر دو به خودِ ردیف‌ها نیاز دارند، نه به متنِ آن‌ها.
            var snap = full is null
                ? new AcctSnapshot { Name = card.Name, Kind = "شرکت تیل", Date = Shamsi.Today() }
                : AcctSnapshots.ForCompany(full, _host.Company);

            var live = full is null ? "" : await AcctLive.EnsureAsync(_host, full);

            var link = AcctView.Url(_host.Settings.GetString(SettingsKeys.ViewerUrl), snap,
                                    AcctLink.Build(card.Entity.Id, null, "company"), live);
            var png = await Task.Run(() => QrWriter.EncodePng(link));

            await Dialogs.ShowQrAsync("📲 " + card.Name, link, png,
                live.Length > 0
                    ? "این کد حسابِ همین شرکت را روی گوشی باز می‌کند — بی رمز؛ و هر تغییری که این‌جا بدهید تا یک دقیقه بعد روی گوشی هم می‌آید."
                    : "این کد حسابِ همین شرکت را روی گوشی باز می‌کند — بی رمز و بی سرور.");
        });

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
