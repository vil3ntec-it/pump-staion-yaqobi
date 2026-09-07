using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PumpYaqobi.App.Printing;
using PumpYaqobi.App.Services;
using PumpYaqobi.Application.Localization;
using PumpYaqobi.Application.Services;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;
using PumpYaqobi.Reporting.Pdf;

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

    // ══ گِردکردنِ نمایشی — مو‌به‌مو مثلِ ‎_renderFuelSection‎ ═══════════════
    //
    //   تن ‎toFixed(3)‎ · لیتر ‎Math.round‎ · دالر ‎toFixed(2)‎ ·
    //   افغانی ‎Math.round‎ · فی‌لیتر ‎toFixed(1)‎
    //
    // ⚠️ شمارِ اعشار **ثابت** است، نه «تا دو رقم»: سایت «۰» را هم «0.0»
    // می‌نویسد. پیش از این فی‌لیتر با دو رقم و بی‌صفرِ انتهایی نوشته می‌شد و
    // عددِ همان خرید در سایت و برنامه یکی دیده نمی‌شد.
    public string TonText => Shamsi.Money(Math.Round(N.Ton, 3), 3);
    public string LitersText => Shamsi.Money(Math.Round(N.Liters, 0, MidpointRounding.AwayFromZero));
    public string TotalUsdText => Shamsi.Money(Math.Round(N.TotalUsd, 2), 2);
    public string TotalAfnText => Shamsi.Money(Math.Round(N.TotalAfn, 0, MidpointRounding.AwayFromZero));
    public string PerLiterText => Shamsi.Money(Math.Round(N.PerLiter, 1), 1);

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

    // ── میله‌زنی: محاسبهٔ زنده ─────────────────────────────────────────────
    /// <summary>لیترِ میله‌زنیِ در حالِ تایپ — هنوز ثبت نشده.</summary>
    [ObservableProperty] private string _dipMeasured = "";

    /// <summary>«دفتر برابرِ عددِ واقعی شود» — تیکِ ‎dip-apply‎ی نسخهٔ وب.</summary>
    [ObservableProperty] private bool _dipApplyToBook;

    [ObservableProperty] private string _tankBookText = "—";
    [ObservableProperty] private string _tankCapacityText = "—";
    [ObservableProperty] private string _dipPercentText = "—";
    [ObservableProperty] private string _dipEmptyText = "—";
    [ObservableProperty] private string _dipDiffText = "—";
    [ObservableProperty] private string _dipDiffBrushKey = "Pump.Text";
    [ObservableProperty] private string _dipWarnText = "";
    [ObservableProperty] private bool _dipWarnVisible;
    [ObservableProperty] private bool _dipHasValue;

    /// <summary>موجودیِ دفتریِ همین لحظه — پایهٔ «اختلاف با دفتر».</summary>
    private decimal _bookNow;

    /// <summary>آخرین حسابِ مخزن — ورقِ PDF از همین برداشته می‌شود تا عددهای
    /// ورق مو‌به‌مو همان چیزی باشند که همان لحظه روی صفحه است.</summary>
    private TankState _tank;

    public FuelType Fuel => IsDiesel ? FuelType.Diesel : FuelType.Petrol;
    public string FuelLabel => IsDiesel ? "دیزل" : "پطرول";
    public string TankTitle => (IsDiesel ? "🟤 مخزن " : "⛽ مخزن ") + FuelLabel;
    public string CurrentTitle => "موجودی فعلی مخزن (" + FuelLabel + ")";
    public string MoneyTitle => "💰 خلاصه پول‌ها — " + FuelLabel;
    public string AddBuyText => "➕ ثبت خرید " + FuelLabel;
    public string PdfText => "📄 PDF مخزن " + FuelLabel;
    public string FuelToggleText => IsDiesel ? "⛽ رفتن به مخزن پطرول" : "🟤 رفتن به مخزن دیزل";
    public string StateText => IsLow ? "کمبودِ موجودی" : "موجودی کافی";

    // ══ پنجرهٔ «ثبت خرید» — ‎#addPurchaseModal‎ ══════════════════════════════
    //
    // گزارشِ صاحب ریپو: «خریدهای دیزل و پطرول این مدلی صفحهٔ نسبتاً کوچک
    // می‌آید و ثبت می‌کند، نه آن مدل.»
    //
    // تا دیروز دکمهٔ «ثبت خرید» یک ردیفِ خالی به فهرست می‌افزود و کاربر باید
    // همان‌جا میانِ نُه کادر پرش می‌کرد — یعنی خریدِ نصفه‌کاره هم ذخیره شده
    // بود. حالا مثلِ سایت یک پنجرهٔ کوچک باز می‌شود، همان‌جا زنده حساب
    // می‌کند، و تا همهٔ چهار عددِ لازم پر نشوند چیزی ثبت نمی‌شود.
    [ObservableProperty] private bool _buyOpen;
    [ObservableProperty] private string _buyDate = "";
    [ObservableProperty] private string _buySeller = "";
    [ObservableProperty] private string _buyKg = "";
    [ObservableProperty] private string _buyDensity = "";
    [ObservableProperty] private string _buyPriceTon = "";
    [ObservableProperty] private string _buyUsdRate = "";
    [ObservableProperty] private string _buyNote = "";

    [ObservableProperty] private string _buyTonText = "—";
    [ObservableProperty] private string _buyLitersText = "—";
    [ObservableProperty] private string _buyUsdText = "—";
    [ObservableProperty] private string _buyAfnText = "—";
    [ObservableProperty] private string _buyPerLiterText = "—";

    public string BuyTitle => IsDiesel ? "🟤 ثبت خرید دیزل" : "🛢️ ثبت خرید پطرول";

    partial void OnBuyKgChanged(string v) => CalcBuy();
    partial void OnBuyDensityChanged(string v) => CalcBuy();
    partial void OnBuyPriceTonChanged(string v) => CalcBuy();
    partial void OnBuyUsdRateChanged(string v) => CalcBuy();

    /// <summary>
    /// ‎calcPurchase()‎ — مو‌به‌مو، بی یک ذره تفاوت:
    ///
    ///     تن     = کیلو ÷ ۱۰۰۰
    ///     لیتر   = کیلو ÷ ثقلت
    ///     دالر   = تن × فیِ تن
    ///     افغانی = دالر × نرخِ دالر
    ///     فی‌لیتر = افغانی ÷ لیتر
    ///
    /// و همان گِردکردن‌های نمایشیِ سایت: تن سه رقم، لیتر گِرد، دالر دو رقم،
    /// افغانی گِرد، فی‌لیتر یک رقم.
    /// </summary>
    private void CalcBuy()
    {
        var n = Calc.Compute(Shamsi.Num(BuyKg), Shamsi.Num(BuyDensity),
                             Shamsi.Num(BuyPriceTon), Shamsi.Num(BuyUsdRate));

        BuyTonText = n.Ton == 0m ? "—" : Shamsi.Money(Math.Round(n.Ton, 3), 3) + " تن";
        BuyLitersText = n.Liters == 0m ? "—"
            : Shamsi.Money(Math.Round(n.Liters, 0, MidpointRounding.AwayFromZero)) + " لیتر";
        BuyUsdText = n.TotalUsd == 0m ? "—" : Shamsi.Money(Math.Round(n.TotalUsd, 2), 2) + " $";
        BuyAfnText = n.TotalAfn == 0m ? "—"
            : Shamsi.Money(Math.Round(n.TotalAfn, 0, MidpointRounding.AwayFromZero)) + " افغانی";
        BuyPerLiterText = n.PerLiter == 0m ? "—"
            : Shamsi.Money(Math.Round(n.PerLiter, 1), 1) + " افغانی";
    }

    /// <summary>«➕ ثبت خرید …» — ‎openAddPurchase(fuelType)‎: فرم خالی، تاریخِ امروز.</summary>
    [RelayCommand]
    private void OpenBuy()
    {
        BuyDate = Shamsi.Today();
        BuySeller = ""; BuyKg = ""; BuyDensity = ""; BuyPriceTon = ""; BuyUsdRate = ""; BuyNote = "";
        CalcBuy();
        OnPropertyChanged(nameof(BuyTitle));
        BuyOpen = true;
    }

    [RelayCommand]
    private void CancelBuy() => BuyOpen = false;

    /// <summary>
    /// «✔ ذخیره» — ‎confirmAddPurchase()‎.
    ///
    /// ⚠️ همان شرطِ سایت: هر چهار عدد لازم‌اند. بی این، خریدی با ثقلتِ صفر
    /// ثبت می‌شد که «لیتر» و «فی لیتر»ش صفر می‌ماند و موجودیِ مخزن را خراب
    /// می‌کرد. ثبتِ خودکار در حسابِ شرکتِ فروشنده کارِ ‎AddPurchaseAsync‎ است.
    /// </summary>
    [RelayCommand]
    private Task SaveBuyAsync() => CrashGuard.RunAsync("ثبت خرید", async () =>
    {
        var kg = Shamsi.Num(BuyKg);
        var density = Shamsi.Num(BuyDensity);
        var priceTon = Shamsi.Num(BuyPriceTon);
        var usdRate = Shamsi.Num(BuyUsdRate);

        if (kg <= 0m || density <= 0m || priceTon <= 0m || usdRate <= 0m)
        {
            _host.Toast("لطفاً همه مقادیر را وارد کنید", ToastKind.Error);
            return;
        }

        var seller = BuySeller.Trim();
        var p = new FuelPurchase
        {
            Fuel = Fuel,
            DateShamsi = BuyDate.Trim().Length > 0 ? BuyDate.Trim() : Shamsi.Today(),
            Seller = seller,
            Kg = kg, Density = density, PriceTon = priceTon, UsdRate = usdRate,
            Note = BuyNote.Trim(),
        };
        await _host.StorageData.AddPurchaseAsync(p);

        BuyOpen = false;
        await ReloadAsync();
        _host.Toast(seller.Length > 0
            ? "✅ خرید ثبت شد — در حساب شرکت هم اضافه شد"
            : "✅ خرید ثبت شد — فایده فی لیتر آپدیت شد", ToastKind.Ok);
    });

    /// <summary>ظرفیتِ مخزن — تنظیمی است و روی نوارِ پرشدگی اثر می‌گذارد.</summary>
    private string CapacityKey => IsDiesel ? "tankCapacity_diesel" : "tankCapacity_petrol";

    partial void OnIsDieselChanged(bool v)
    {
        foreach (var n in new[] { nameof(FuelLabel), nameof(TankTitle), nameof(CurrentTitle),
                                  nameof(MoneyTitle), nameof(AddBuyText), nameof(PdfText),
                                  nameof(FuelToggleText), nameof(BuyTitle) })
            OnPropertyChanged(n);
        _ = LoadAsync();
    }

    partial void OnCapacityChanged(string v)
    {
        _host.Settings.Set(CapacityKey, Shamsi.Num(v));
        _ = RecalcAsync();
    }

    partial void OnIsLowChanged(bool v) => OnPropertyChanged(nameof(StateText));

    partial void OnDipMeasuredChanged(string v) => RefreshDip();

    /// <summary>
    /// ‎dipLiveCalc‎ — همان چهار عددی که نسخهٔ وب هنگام تایپ نشان می‌داد:
    /// درصدِ پر بودن، فضای خالی (= چقدر از تانکر می‌شود گرفت)، اختلاف با دفتر،
    /// و هشدارِ «از حدِ مجاز گذشت».
    ///
    /// ⚠️ این‌جا هیچ چیزی ذخیره نمی‌شود؛ فقط حساب می‌شود. ثبت با دکمهٔ
    /// «میله‌زنیِ تازه» است.
    /// </summary>
    private void RefreshDip()
    {
        var t = _host.TankDip.Info(Fuel, _bookNow, Shamsi.Num(Capacity),
            _host.Settings.GetDecimal(
                PumpYaqobi.Services.Data.SettingsService.LowStockThreshold, 1000m));

        TankBookText = Shamsi.Money(Math.Round(t.Book, 0, MidpointRounding.AwayFromZero)) + " لیتر";
        TankCapacityText = Shamsi.Money(Math.Round(t.Capacity, 0, MidpointRounding.AwayFromZero))
                         + " لیتر" + (t.CapacityDefined ? "" : " (پیش‌فرض)");

        var measured = Shamsi.Num(DipMeasured);
        DipHasValue = measured > 0m;
        if (!DipHasValue)
        {
            DipPercentText = DipEmptyText = DipDiffText = "—";
            DipDiffBrushKey = "Pump.Text";
            DipWarnVisible = false;
            return;
        }

        var c = _host.TankDip.Calc(t, measured);
        DipPercentText = Shamsi.Money(Math.Round(c.Percent, 0, MidpointRounding.AwayFromZero)) + "٪";
        DipEmptyText = Shamsi.Money(Math.Round(c.Empty, 0, MidpointRounding.AwayFromZero)) + " لیتر";
        DipDiffText = (c.Diff > 0m ? "+" : "") + Shamsi.Money(c.Diff) + " لیتر";
        DipDiffBrushKey = c.Diff < 0m ? "Pump.Danger" : "Pump.Ok";
        DipWarnVisible = c.Over;
        DipWarnText = c.Over
            ? "🚨 اختلافِ " + Shamsi.Money(Math.Abs(c.Diff)) + " لیتر از حدِ مجاز ("
              + Shamsi.Money(c.Allowed) + " لیتر) بیشتر است — احتمالِ نشتی، دزدی یا خطای ثبت."
            : "";
    }

    [RelayCommand]
    private void ToggleFuel() => IsDiesel = !IsDiesel;

    /// <summary>‎pdfStorage(fuelType)‎ — ورقِ همان مخزنی که باز است.</summary>
    [RelayCommand]
    private Task PdfAsync()
    {
        var buys = Purchases.Select(p => p.Entity).ToList();
        var input = new StorageReportInput(Fuel, buys, _tank, DocDates.Line());
        return Documents.ShowAsync(() => new StorageReport(input, Calc), "مخزن " + FuelLabel);
    }

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

        // موجودیِ دفتری برای میله‌زنی — همان ‎book‎ی ‎__tankInfo‎ که از
        // ‎_fuelStock‎ می‌آید (خرید − فروش + اصلاحِ میله‌زنی‌های پیشین).
        _bookNow = t.Current;
        _tank = t;
        RefreshDip();
    }

    public async Task SavePurchaseAsync(FuelPurchase p)
    {
        await _host.StorageData.UpdatePurchaseAsync(p);
        await RecalcAsync();
    }

    public Task SaveDipAsync(TankDip d) => _host.StorageData.SaveDipAsync(d);

    // دکمهٔ «ثبت خرید» حالا ‎OpenBuy‎ است (پنجرهٔ کوچک)، نه ساختنِ ردیفِ خالی.

    

    /// <summary>
    /// ‎deletePurchase(id)‎ — با همان پرسشِ «مطمئنی؟»ی سایت.
    ///
    /// ⚠️ حذفِ خرید، ردیفِ حسابِ شرکت را دست نمی‌زند (خواستهٔ صریحِ صاحب ریپو:
    /// این دو حذف از هم جدا شدند) — همان کاری که ‎DeleteRowAsync‎ توضیح داده.
    /// </summary>
    [RelayCommand]
    private Task DeletePurchaseAsync(PurchaseRowViewModel? row) =>
        CrashGuard.RunAsync("حذف خرید", async () =>
        {
            if (row is null) return;
            if (!await Dialogs.ConfirmAsync("حذف خرید",
                    "این خرید از مخزن حذف شود؟ (ردیفِ حسابِ شرکت دست نمی‌خورد)")) return;
            await _host.StorageData.DeletePurchaseAsync(row.Entity.Id);
            Purchases.Remove(row);
            await RecalcAsync();
            _host.Toast("🗑️ خرید حذف شد", ToastKind.Warn);
        });

    /// <summary>
    /// ‎confirmTankDip‎ — ثبتِ میله‌زنی با همان عددی که کاربر تایپ کرده.
    ///
    /// ⚠️ «دفتری» از خودِ برنامه پر می‌شود، نه دستِ کاربر: اختلاف فقط وقتی
    /// معنی دارد که با موجودیِ همان لحظه سنجیده شود. پیش از این ستونِ دفتری
    /// صفر می‌ماند و هر میله‌زنی «اختلافِ کلِ مخزن» نشان می‌داد.
    /// </summary>
    [RelayCommand]
    private async Task AddDipAsync()
    {
        var measured = Shamsi.Num(DipMeasured);
        if (measured <= 0m) { _host.Toast("لیتر میله‌زنی را بنویسید", ToastKind.Error); return; }

        var expected = Math.Round(_bookNow, 0, MidpointRounding.AwayFromZero);
        var diff = TankDipService.JsRound(measured - _bookNow);

        var d = new TankDip
        {
            Fuel = Fuel, DateShamsi = Shamsi.Today(),
            Measured = measured, Expected = expected,
            BookAdjust = DipApplyToBook ? diff : 0m,
        };
        await _host.StorageData.SaveDipAsync(d);
        Dips.Insert(0, new DipRowViewModel(d, this));

        _host.Toast(diff < 0m
            ? "⚠️ " + Shamsi.Money(-diff) + " لیتر کم‌آمد نسبت به دفتر"
              + (DipApplyToBook ? " — دفتر برابر شد" : "")
            : "✅ ثبت شد — فرق: " + Shamsi.Money(diff) + " لیتر"
              + (DipApplyToBook ? " — دفتر برابر شد" : ""),
            diff < 0m ? ToastKind.Warn : ToastKind.Ok);

        DipMeasured = "";
        await RecalcAsync();
    }

    /// <summary>حذفِ میله‌زنی — اصلاحِ دفترش هم با خودش برمی‌گردد.</summary>
    [RelayCommand]
    private async Task DeleteDipAsync(DipRowViewModel? row)
    {
        if (row is null) return;
        await _host.Tools.DeleteDipAsync(row.Entity.Id);
        Dips.Remove(row);
        await RecalcAsync();
    }
}
