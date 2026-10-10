using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PumpYaqobi.App.Services;
using PumpYaqobi.Application.Localization;
using PumpYaqobi.Application.Services;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;
using PumpYaqobi.Services.Security;

namespace PumpYaqobi.App.ViewModels.Sections;

/// <summary>
/// ══ مفاد / ضرر / اتحادیه ════════════════════════════════════════════════════
/// همان صفحه‌ای که ‎#sec-profit‎ نشان می‌دهد: کادرِ بزرگِ «مفاد/ضرر خالص» با
/// خطِ روند، دو کادرِ «درآمدها» و «مصارف»، نرخِ اتحادیه، خریدِ عمده از مشتری
/// و دو کادرِ دستی.
///
/// ⚠️ هیچ عددی این‌جا ساخته نمی‌شود — همه از ‎ProfitLossService‎ می‌آیند که با
/// خودِ نسخهٔ وب سنجیده شده. نرخِ اتحادیه تنها چیزی است که نوشته می‌شود.
/// </summary>
public sealed partial class ProfitSectionViewModel : SectionViewModel
{
    /// <summary>کارت‌های زیربخش در کشوییِ «☰ کارها»، نه کنارِ نوار (۱۴۰۵/۰۷/۱۶ — «شلوغ نکند»).</summary>
    protected override bool SubLinksInMenu => true;

    private readonly AppHost _host;
    private ProfitInput _db = new();

    public ProfitSectionViewModel(AppHost host)
        : base("profit", "profit", "مفاد / ضرر / اتحادیه")
    {
        _host = host;
        InitConversion();
        //  «همهٔ ماه‌ها» همان «همهٔ زمان‌ها»ی پیش‌فرض است — عددی که کاربر تا
        //  امروز می‌دید، بی هیچ تغییری، تا خودش دوره‌ای برگزیند.
        Picker = new YearMonthPicker(k =>
        {
            if (_loadingPicker) return;
            var p = ProfitPeriod.FromKey(k);
            if (p == _period) return;
            _period = p;
            _ = CrashGuard.RunAsync("مفاد / ضرر", ComputeAsync);
        }, "همهٔ ماه‌ها");
        host.Locks.Changed += id =>
        {
            if (id == SectionLockService.Profit)
                Avalonia.Threading.Dispatcher.UIThread.Post(RaiseVeil);
        };
        //  🏷️ نرخی که از تلگرام آمد (‎StationPublisher.RateTickAsync‎) همان لحظه
        //  در کادرِ «نرخ اتحادیه» دیده می‌شود — بی رفتن و برگشتن به بخش.
        PumpYaqobi.Services.Data.SettingsService.Written += (k, _) =>
        {
            if (k != PumpYaqobi.Services.Data.SettingsService.UnionRatePetrol
                && k != PumpYaqobi.Services.Data.SettingsService.UnionRateDiesel) return;
            Avalonia.Threading.Dispatcher.UIThread.Post(RefillUnion);
        };
    }

    /// <summary>
    /// کادرهای نرخ از روی تنظیمات — فقط اگر واقعاً فرق دارند، تا تایپِ همین
    /// لحظهٔ کاربر («۸» ⇒ «۸۰») زیرِ دستش بازنویسی نشود.
    /// </summary>
    internal void RefillUnion()
    {
        var p = _host.Settings.GetDecimal(PumpYaqobi.Services.Data.SettingsService.UnionRatePetrol);
        var d = _host.Settings.GetDecimal(PumpYaqobi.Services.Data.SettingsService.UnionRateDiesel);
        if (Shamsi.Num(UnionPetrol) == p && Shamsi.Num(UnionDiesel) == d) return;
        _filling = true;
        try
        {
            UnionPetrol = p == 0 ? "" : Shamsi.Money(p);
            UnionDiesel = d == 0 ? "" : Shamsi.Money(d);
        }
        finally { _filling = false; }
    }

    // ══ پرده — «بخش آزاد، عددها تار» (۱۴۰۵/۰۷/۱۶) ════════════════════════════
    //
    //  خواستهٔ صاحب ریپو: «رمز مفاد و ضرر نباید بخش شو بگیره… بخش باید آزاد
    //  باشه؛ اون چارت و فایده و مصارف باید تار بشن و دیده نشن، و وقتی یارو
    //  روی اون بزنه رمز رو بخاد… و کاری کن که دوباره هم قفل بشه.»
    //
    //  ⛔ تار شدن فقط ظاهر نیست: تا پرده هست، عددِ واقعی **اصلاً به صفحه
    //  نمی‌رسد** — جای هر عدد یک عددِ ساختگیِ هم‌شکل می‌نشیند و نمودار صاف
    //  است. پس نه با بزرگ‌نمایی، نه با عکس، نه با ابزارِ دسترسی‌پذیری چیزی
    //  لو نمی‌رود. رنگِ سبز/سرخ هم نمی‌ماند (خودش می‌گفت مفاد است یا ضرر).
    //  ⛔ رمز همان رمزِ همین بخش است (یا رمزِ برنامه) و با همان ترمزِ حدس،
    //  از همان یک تابع (‎MainViewModel.AskSectionPasswordAsync‎).
    //  ⚠️ با رفتن از بخش دوباره قفل می‌شود (‎OnDeactivated‎)، و دکمهٔ «🔒» هم.

    /// <summary>عددها پشتِ پرده‌اند؟ (رمز دارد و هنوز زده نشده.)</summary>
    public bool Veiled => PlanVeiled || _host.Locks.NeedsUnlock(SectionLockService.Profit);

    /// <summary>
    /// ⛔ پلن مفاد را ندارد (استاندارد) ⇒ همان پرده، و هیچ رمزی بازش نمی‌کند
    /// (۱۴۰۵/۰۷/۲۰): «چارتِ مفاد و ضرر تار دیده بشن، با مصارف و فایدهٔ زیرِ
    /// چارت.» ‎Entitlements.Allows(Profit)‎ تنها تصمیم است.
    /// </summary>
    public bool PlanVeiled => !Entitlements.Allows(Entitlements.Profit);

    /// <summary>نوشتهٔ روی پرده — رمز، یا «در پلنِ شما نیست».</summary>
    public string VeilText => PlanVeiled
        ? "🔒 مفاد، مصارف و نمودار در پلنِ شما تار است — با وی‌آی‌پی یا دائمی دیده می‌شود"
        : "🔒 برای دیدنِ مفاد، مصارف و نمودار رمز بزنید";

    /// <summary>دکمهٔ «🔒 دوباره قفل کن» — فقط وقتی رمز هست و باز است.</summary>
    public bool CanRelock => _host.Locks.HasPassword(SectionLockService.Profit) && !Veiled;

    private const string VeilMoney = "88,888,888 افغانی";
    private static readonly double[] VeilTrend = { 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30 };

    public string NetShown => Veiled ? VeilMoney : NetText;
    public string NetCaptionShown => !Veiled ? NetCaption : PlanVeiled ? "🔒 در پلنِ شما نیست" : "🔒 پنهان — برای دیدن رمز بزنید";
    public string IncomeTitleShown => Veiled ? "📈 درآمدها" : IncomeTitle;
    public string IncomeShown => Veiled ? VeilMoney : IncomeText;
    public string ExpenseShown => Veiled ? VeilMoney : ExpenseText;
    public IReadOnlyList<double> TrendShown => Veiled ? VeilTrend : TrendValues;
    public string TrendBrushShown => Veiled ? "Pump.Muted" : TrendBrushKey;
    public string IncomeBrushShown => Veiled ? "Pump.Muted" : IncomeBrushKey;
    public string ExpenseBrushShown => Veiled ? "Pump.Muted" : "Pump.Danger";

    private void RaiseVeil()
    {
        foreach (var n in new[]
                 {
                     nameof(Veiled), nameof(PlanVeiled), nameof(VeilText), nameof(CanRelock), nameof(NetShown), nameof(NetCaptionShown),
                     nameof(IncomeTitleShown), nameof(IncomeShown), nameof(ExpenseShown), nameof(TrendShown),
                     nameof(TrendBrushShown), nameof(IncomeBrushShown), nameof(ExpenseBrushShown),
                     nameof(RealRows), nameof(RealNote),
                 })
            OnPropertyChanged(n);
    }

    /// <summary>زدن روی پرده ⇒ رمز. درست ⇒ همان لحظه عددها پیدا.</summary>
    [RelayCommand]
    private async Task RevealAsync()
    {
        if (!Veiled) return;
        //  ⛔ پلن ندارد ⇒ هیچ رمزی بازش نمی‌کند؛ خودش می‌گوید چرا
        if (PlanVeiled) { Entitlements.Gate(_host, Entitlements.Profit); return; }
        await MainViewModel.AskSectionPasswordAsync(SectionLockService.Profit, Title);
        RaiseVeil();
    }

    /// <summary>«🔒 دوباره قفل کن» — بی رمز، همین حالا.</summary>
    [RelayCommand]
    private void Relock()
    {
        _host.Locks.Relock(SectionLockService.Profit);
        RaiseVeil();
    }

    /// <summary>رفتن از بخش ⇒ دوباره پشتِ پرده، تا کسی که بعد می‌آید نبیند.</summary>
    public override void OnDeactivated()
    {
        base.OnDeactivated();
        _host.Locks.Relock(SectionLockService.Profit);
        RaiseVeil();
    }

    // ══ دوره: همهٔ زمان‌ها · یک سال · یک ماه ═══════════════════════════════════
    //
    //  خواستهٔ صاحب ریپو (۱۴۰۵/۰۷/۱۳): «بخشِ مفاد و ضرر کادرِ کشوییِ ماه و سال
    //  ندارد… که آدم بفهمد برای ماه یا سال چقدر فایده بوده یا نبوده.»
    //  ⛔ فرمول دست نمی‌خورد (‎ProfitLossService.Compute‎)؛ فقط ورودی‌ها به همان
    //  دوره کوتاه می‌شوند — قاعدهٔ تاریخِ هر منبع بالای ‎ProfitPeriod‎.

    /// <summary>کشوی سال + ماه — همان کشویی که دفترهای ماهانه دارند.</summary>
    public YearMonthPicker Picker { get; }

    private ProfitPeriod _period = ProfitPeriod.All;
    private bool _loadingPicker;

    /// <summary>«همهٔ زمان‌ها» · «سالِ 1405» · «میزان 1405».</summary>
    [ObservableProperty] private string _periodText = "همهٔ زمان‌ها";

    private static string PeriodKey(ProfitPeriod p) =>
        p.IsAll ? "" : p.Month.Length == 0 ? p.Year + YearMonthPicker.AllMark : p.Year + "/" + p.Month;

    private static string PeriodLabel(ProfitPeriod p) =>
        p.IsAll ? "همهٔ زمان‌ها"
        : p.Month.Length == 0 ? "سالِ " + p.Year
        : Shamsi.MonthLabel(p.Year + "/" + p.Month);

    // ── نتیجه ────────────────────────────────────────────────────────────────
    [ObservableProperty] private string _netText = "0 افغانی";
    [ObservableProperty] private string _netCaption = "محاسبه در حال انجام...";
    [ObservableProperty] private bool _isProfit = true;
    [ObservableProperty] private string _incomeTitle = "📈 درآمدها";
    [ObservableProperty] private string _incomeText = "0";
    [ObservableProperty] private string _incomeBrushKey = "Pump.Ok";
    [ObservableProperty] private string _expenseText = "0";

    /// <summary>خطِ روندِ کادرِ بالا — صعودی برای مفاد، نزولی برای ضرر.</summary>
    public IReadOnlyList<double> TrendValues => IsProfit
        ? new double[] { 4, 16, 10, 28, 21, 37, 30, 47, 40, 57, 50, 68, 76 }
        : new double[] { 76, 64, 70, 52, 59, 43, 50, 33, 40, 23, 30, 12, 4 };

    public string TrendBrushKey => IsProfit ? "Pump.Ok" : "Pump.Danger";

    partial void OnIsProfitChanged(bool v)
    {
        OnPropertyChanged(nameof(TrendValues));
        OnPropertyChanged(nameof(TrendBrushKey));
        OnPropertyChanged(nameof(TrendShown));
        OnPropertyChanged(nameof(TrendBrushShown));
    }

    partial void OnNetTextChanged(string value) => OnPropertyChanged(nameof(NetShown));
    partial void OnNetCaptionChanged(string value) => OnPropertyChanged(nameof(NetCaptionShown));
    partial void OnIncomeTitleChanged(string value) => OnPropertyChanged(nameof(IncomeTitleShown));
    partial void OnIncomeTextChanged(string value) => OnPropertyChanged(nameof(IncomeShown));
    partial void OnIncomeBrushKeyChanged(string value) => OnPropertyChanged(nameof(IncomeBrushShown));
    partial void OnExpenseTextChanged(string value) => OnPropertyChanged(nameof(ExpenseShown));

    // ── نرخِ اتحادیه ─────────────────────────────────────────────────────────
    [ObservableProperty] private string _unionPetrol = "";
    [ObservableProperty] private string _unionDiesel = "";

    partial void OnUnionPetrolChanged(string v) => SaveUnion(FuelType.Petrol, v);
    partial void OnUnionDieselChanged(string v) => SaveUnion(FuelType.Diesel, v);

    private bool _filling;

    /// <summary>
    /// ⚠️ هنگامِ پر کردنِ اولیه ذخیره نمی‌کنیم — وگرنه بازکردنِ صفحه، نرخِ
    /// ذخیره‌شده را با همان مقدار دوباره می‌نویسد و بی‌جهت در تاریخچهٔ نرخ
    /// یک ردیف می‌سازد.
    /// </summary>
    private void SaveUnion(FuelType fuel, string value)
    {
        if (_filling) return;
        if (!Shamsi.IsReadable(value)) return;   // ⛔ «۸a» نرخِ اتحادیه را صفر نکند (شورا، بندِ ۱)
        var rate = Shamsi.Num(value);
        _host.Settings.Set(fuel == FuelType.Diesel
            ? PumpYaqobi.Services.Data.SettingsService.UnionRateDiesel
            : PumpYaqobi.Services.Data.SettingsService.UnionRatePetrol, rate);
        // ⛔ و در «📈 تاریخچهٔ نرخ اتحادیه» — تا ۳.۱.۲۱۳ هیچ‌وقت ثبت نمی‌شد.
        _rateLog ??= new RateRecorder((f, r) => _host.Tools.RecordRateAsync(f, r));
        _rateLog.Note(fuel, rate);
    }

    private RateRecorder? _rateLog;

    // ── خریدِ عمده از مشتری ───────────────────────────────────────────────────
    [ObservableProperty] private string _bulkQty = "";
    [ObservableProperty] private string _bulkBuy = "";
    [ObservableProperty] private string _bulkMarket = "";
    [ObservableProperty] private string _bulkSellerText = "0 افغانی";
    [ObservableProperty] private string _bulkIncomeText = "0 افغانی";
    [ObservableProperty] private string _bulkSumText = "0 افغانی";

    partial void OnBulkQtyChanged(string v) => RecalcBulk();
    partial void OnBulkBuyChanged(string v) => RecalcBulk();
    partial void OnBulkMarketChanged(string v) => RecalcBulk();

    private void RecalcBulk()
    {
        var (seller, income) = ProfitLossService.BulkBuy(
            Shamsi.Num(BulkQty), Shamsi.Num(BulkBuy), Shamsi.Num(BulkMarket));
        BulkSellerText = Money(seller);
        BulkIncomeText = Money(income);
    }

    // ── دو کادرِ دستی ────────────────────────────────────────────────────────
    [ObservableProperty] private string _manualIncome = "";
    [ObservableProperty] private string _manualExpense = "";

    partial void OnManualIncomeChanged(string v) => Recalc();
    partial void OnManualExpenseChanged(string v) => Recalc();

    private static string Money(decimal v) =>
        Shamsi.Money(Math.Round(v, 0, MidpointRounding.AwayFromZero)) + " افغانی";

    protected override Task LoadAsync() => RefreshAsync();

    /// <summary>
    /// ⛔ فعال‌سازیِ این بخش فقط دفترِ دیتابیس را دوباره می‌خواند، پس با
    /// <c>PumpDbContext.Version</c>ِ دست‌نخورده اصلاً صدا زده نمی‌شود —
    /// ریشهٔ «هر بخش رو باز می‌کنم جدول‌ها یک ثانیه بعد میان» (۱۴۰۵/۰۷/۰۵).
    /// شرحش بالای <see cref="SectionViewModel.ActivationOnlyReadsDb"/>.
    /// </summary>
    public override bool ActivationOnlyReadsDb => true;

    public override Task OnActivatedAsync() => RefreshAsync();
    public override bool ActivationRepeatsLoad => true;

    public async Task RefreshAsync()
    {
        //  ماه‌هایی که هر کدام از منبع‌ها داده دارند — فقط کلیدها، نه ردیف‌ها
        var months = new HashSet<string>(StringComparer.Ordinal) { Shamsi.ThisMonth() };
        foreach (var m in await _host.StorageData.ReportMonthsAsync()) months.Add(m);
        foreach (var m in await _host.Debtors.NoInvoiceMonthsAsync()) months.Add(m);
        foreach (var m in await _host.ExpenseLedger.MonthsAsync()) months.Add(m);
        foreach (var m in await _host.ExtraIncomeLedger.MonthsAsync()) months.Add(m);
        foreach (var m in await _host.WaraqData.MonthsAsync()) months.Add(m);
        _pending = await _host.Invoices.PendingRatesAsync();
        foreach (var r in _pending)
            if (r.DateKey > 0) months.Add($"{r.DateKey / 10000:0000}/{r.DateKey / 100 % 100:00}");

        _loadingPicker = true;
        try { Picker.Load(months, PeriodKey(_period)); }
        finally { _loadingPicker = false; }
        //  دوره‌ای که دیگر در فهرست نیست ⇒ همان چیزی که کشویی نشان می‌دهد
        _period = ProfitPeriod.FromKey(Picker.SelectedKey);

        await ComputeAsync();
        //  🔁 تبدیلِ تیل: قیمت‌های خودکار از همان خریدهای مخزن، و تاریخچه
        FillConvPrices(force: false);
        await LoadConversionsAsync();
    }

    /// <summary>
    /// فاکتورهای در صف (بی «ضرر دریافت شد») — یک بار در هر تازه‌سازی. ⛔ از
    /// ۱۴۰۵/۰۷/۲۰ فاکتورِ تاییدشده در مفاد و ضرر نیست (خواستهٔ صاحب ریپو).
    /// </summary>
    private List<PendingRate> _pending = new();

    /// <summary>«از کجا آمد» — هر شیفتِ هر ورق یک سطر (‎WaraqDataService.SalesLinesAsync‎).</summary>
    public BulkRows<ProfitSourceLine> Sources { get; } = new();
    [ObservableProperty] private string _sourcesNote = "";
    [ObservableProperty] private string _salesPetrolText = "";
    [ObservableProperty] private string _salesDieselText = "";
    [ObservableProperty] private string _rateDiffText = "";
    [ObservableProperty] private string _expenseLineText = "";
    [ObservableProperty] private string _otherIncomeText = "";

    /// <summary>
    /// عددهای همین دوره. «همهٔ زمان‌ها» <b>همان راهِ پیشین</b> است، مو‌به‌مو —
    /// پس عددی که کاربر تا امروز می‌دید عوض نمی‌شود.
    /// </summary>
    private async Task ComputeAsync()
    {
        var period = _period;
        var keys = period.Keys;

        // ⛔ «برای یک جمع، همهٔ ردیف‌ها را نخوان» (۱۴۰۵/۰۷/۱۳).
        //  این صفحه تا امروز با هر فعال‌سازی همهٔ پارچه‌های پنج سال (با هر دو
        //  شیفت)، همهٔ مصارف، همهٔ درآمدهای اضافی و همهٔ فاکتورها را به شیءِ
        //  کامل می‌خواند تا پنج عدد جمع بزند — و چون SQLite کارِ «async»ش را
        //  روی همان نخِ صداکننده می‌کند، آن خواندن روی نخِ رابط بود: همان
        //  مکثی که کاربر درست سرِ اسکرولِ این صفحه حس می‌کرد.
        //  حالا هر پنج عدد از همان یکی-دو ستونِ خودشان می‌آیند.
        //  ⛔ ۱۴۰۵/۰۷/۲۰: فایده = پولِ لیترِ فروخته‌شدهٔ ورق‌ها، پطرول و دیزل هر دو —
        //  نه فایدهٔ پارچه (صاحب ریپو: «نه از پارچه حساب بشه»).
        var lines = await _host.WaraqData.SalesLinesAsync(keys);
        var petrol = lines.Sum(l => l.PetrolMoney);
        var diesel = lines.Sum(l => l.DieselMoney);
        var todayP = _host.Settings.GetDecimal(PumpYaqobi.Services.Data.SettingsService.UnionRatePetrol);
        var todayD = _host.Settings.GetDecimal(PumpYaqobi.Services.Data.SettingsService.UnionRateDiesel);
        var rateDiff = ProfitLossService.PendingRateDiff(_pending.Where(r => period.Contains(r.DateKey)), todayP, todayD);

        decimal extraSum, expenseSum;
        ProfitInput input;
        if (period.IsAll)
        {
            extraSum = await _host.ExtraIncomeLedger.SumAsync(e => e.Amount);
            expenseSum = await _host.ExpenseLedger.SumAsync(e => e.Amount);
            // «بی‌فاکتور»ها — بردگی‌شان مستقیم درآمد است (از قبل جمعِ خودِ SQLite بود)
            var noinvAccounts = (await _host.Debtors.CardAccountsAsync(noInvoice: true))
                .Values.SelectMany(x => x).ToList();
            input = new ProfitInput
            {
                WaraqSalesPetrol = petrol,
                WaraqSalesDiesel = diesel,
                NoInvoiceAccounts = noinvAccounts,
                ExtraIncomeSum = extraSum,
                ExpenseSum = expenseSum,
                InvoiceRateDiffSum = rateDiff,
            };
        }
        else
        {
            //  همان یک ستون، فقط ماه یا سالِ همین دوره (‎MonthKey‎ی خودِ ردیف)
            extraSum = await _host.ExtraIncomeLedger.SumAsync(e => e.Amount, period.MonthFilter);
            expenseSum = await _host.ExpenseLedger.SumAsync(e => e.Amount, period.MonthFilter);
            input = new ProfitInput
            {
                WaraqSalesPetrol = petrol,
                WaraqSalesDiesel = diesel,
                NoInvoiceSum = await _host.Debtors.NoInvoiceBardagiAsync(keys),
                ExtraIncomeSum = extraSum,
                ExpenseSum = expenseSum,
                InvoiceRateDiffSum = rateDiff,
            };
        }
        //  دوره وسطِ خواندن عوض شد ⇒ این جواب کهنه است
        if (period != _period) return;
        _db = input;

        _filling = true;
        var p = _host.Settings.GetDecimal(PumpYaqobi.Services.Data.SettingsService.UnionRatePetrol);
        var d = _host.Settings.GetDecimal(PumpYaqobi.Services.Data.SettingsService.UnionRateDiesel);
        UnionPetrol = p == 0 ? "" : Shamsi.Money(p);
        UnionDiesel = d == 0 ? "" : Shamsi.Money(d);
        _filling = false;

        //  💰 سودِ واقعی (۱۴۰۵/۰۷/۱۸): همان لیترها × قیمتِ خریدِ واقعیِ مخزن، منهای مصارف
        var prices = await _host.StorageData.BuyPricesAsync();
        _prices = prices;
        if (period != _period) return;
        _real = RealProfitService.Compute(lines, prices,
            _host.Settings.GetDecimal(PumpYaqobi.Services.Data.SettingsService.BuyPerLiterPetrol),
            _host.Settings.GetDecimal(PumpYaqobi.Services.Data.SettingsService.BuyPerLiterDiesel),
            expenseSum);
        OnPropertyChanged(nameof(RealRows));
        OnPropertyChanged(nameof(RealNote));

        PeriodText = PeriodLabel(period);
        BulkSumText = Money(extraSum);
        ShowSources(lines, ProfitLossService.Breakdown(input));
        Recalc();
    }

    // ══ 💰 سودِ واقعی — کنارِ کادرِ نمودار (۱۴۰۵/۰۷/۱۸) ═══════════════════════
    //  شرحِ فرمول بالای ‎RealProfitService‎. ⛔ پشتِ همان پرده: تا پرده هست، عددِ واقعی
    //  به صفحه نمی‌رسد (همان قاعدهٔ ‎NetShown‎).
    private RealProfit _real;

    public IReadOnlyList<RealProfitRow> RealRows => Veiled
        ? new[]
        {
            new RealProfitRow("💵 فروشِ تیل", VeilMoney, "Pump.Muted"),
            new RealProfitRow("🏷️ بهای خریدِ همان لیترها", VeilMoney, "Pump.Muted"),
            new RealProfitRow("📊 سودِ ناخالص", VeilMoney, "Pump.Muted"),
            new RealProfitRow("💸 هزینه‌ها (مصارف)", VeilMoney, "Pump.Muted"),
            new RealProfitRow("✅ سودِ خالص", VeilMoney, "Pump.Muted", true),
        }
        : new[]
        {
            new RealProfitRow("💵 فروشِ تیل (مبلغِ کل)", Money(_real.Sales), "Pump.Info"),
            new RealProfitRow("🏷️ بهای خریدِ همان لیترها", Money(_real.Cost), "Pump.Warn"),
            new RealProfitRow("⛽ پطرول — سودِ هر لیتر", PerLiterText(_real.Petrol), "Pump.Accent"),
            new RealProfitRow("🟤 دیزل — سودِ هر لیتر", PerLiterText(_real.Diesel), "Pump.Accent"),
            new RealProfitRow("📊 سودِ ناخالص (فروش − خرید)", Signed(_real.Gross), _real.Gross >= 0 ? "Pump.Ok" : "Pump.Danger"),
            new RealProfitRow("💸 هزینه‌ها (مصارف)", Money(_real.Expenses), "Pump.Danger"),
            new RealProfitRow("✅ سودِ خالص (ناخالص − هزینه‌ها)", Signed(_real.Net), _real.Net >= 0 ? "Pump.Ok" : "Pump.Danger", true),
        };

    /// <summary>هشدارِ لیترهایی که قیمتِ خرید ندارند — یا خالی.</summary>
    public string RealNote => Veiled || _real.UnpricedLiters == 0 ? ""
        : $"⚠️ {Liters(_real.UnpricedLiters)} لیتر قیمتِ خرید ندارد (در مخزن خریدی از آن تیل ثبت نشده) — در بهای خرید شمرده نشد.";

    private static string PerLiterText(RealFuel f) =>
        f.Liters == 0 ? "فروشی نیست"
        : f.PricedLiters == 0 ? $"فروش {Rate(f.SalePerLiter)} · قیمتِ خرید ثبت نشده"
        : $"فروش {Rate(f.SalePerLiter)} − خرید {Rate(f.BuyPerLiter)} = {Rate(f.ProfitPerLiter)}";

    private static string Rate(decimal v) => Shamsi.Money(Math.Round(v, 2), 2);

    private static string Signed(decimal v) => (v < 0 ? "−" : "") + Money(Math.Abs(v));

    /// <summary>سقفِ سطرهای «از کجا آمد» — تازه‌ترین‌ها؛ جمع‌ها همیشه از همه.</summary>
    public const int SourceLimit = 120;

    private void ShowSources(List<WaraqSaleLine> lines, ProfitBreakdown b)
    {
        decimal pl = lines.Sum(l => l.PetrolLiters), dl = lines.Sum(l => l.DieselLiters);
        SalesPetrolText = $"⛽ پطرول: {Liters(pl)} لیتر — {Money(b.Petrol)}";
        SalesDieselText = $"🟤 دیزل: {Liters(dl)} لیتر — {Money(b.Diesel)}";
        RateDiffText = b.InvoiceRateDiff > 0 ? $"📉 ضررِ نرخِ فاکتورهای در صف: {Money(b.InvoiceRateDiff)}"
                     : b.InvoiceRateDiff < 0 ? $"📈 مفادِ نرخِ فاکتورهای در صف: {Money(-b.InvoiceRateDiff)}"
                     : "⚪ نرخِ فاکتورهای در صف: بی‌تفاوت";
        ExpenseLineText = $"💸 مصارف: {Money(b.Expenses)}";
        OtherIncomeText = $"➕ بی‌فاکتورها: {Money(b.NoInvoice)} · درآمدِ اضافی: {Money(b.Extra)}";
        var shown = lines.Count > SourceLimit ? lines.Skip(lines.Count - SourceLimit).ToList() : lines;
        Sources.ResetTo(shown.AsEnumerable().Reverse().Select(l => new ProfitSourceLine(l)));
        SourcesNote = lines.Count == 0 ? "در این دوره هیچ ورقی با فروش نیست."
                    : lines.Count > SourceLimit ? $"{SourceLimit} شیفتِ تازه‌تر از {lines.Count} — جمع‌ها از همه است."
                    : $"{lines.Count} شیفت";
    }

    private static string Liters(decimal v) => Shamsi.Money(Math.Round(v, 2), 2);

    private void Recalc()
    {
        var r = ProfitLossService.Compute(_db, Shamsi.Num(ManualIncome), Shamsi.Num(ManualExpense));

        NetText = Money(Math.Abs(r.Net));
        IsProfit = r.IsProfit;
        NetCaption = r.IsProfit ? "✅ مفاد خالص" : "❌ ضرر خالص";

        // درآمدِ منفی هم ممکن است (وقتی شیفت‌ها ضرر داده‌اند) — همان‌طور که
        // نسخهٔ وب می‌کند، عدد مثبت نشان داده می‌شود و عنوان عوض می‌شود.
        IncomeTitle = r.Income >= 0 ? "📈 درآمدها" : "📉 درآمدها (منفی)";
        IncomeBrushKey = r.Income >= 0 ? "Pump.Ok" : "Pump.Danger";
        IncomeText = Money(Math.Abs(r.Income));
        ExpenseText = Money(r.Expenses);
    }

    /// <summary>«➕ ثبت» — خریدِ عمده در جدولِ درآمدهای اضافی می‌نشیند.</summary>
    [RelayCommand]
    private async Task AddBulkAsync()
    {
        if (Shamsi.FirstUnreadable(("مقدار تیل", BulkQty), ("قیمت خرید", BulkBuy), ("قیمت بازار", BulkMarket)) is { } bad) { _host.Toast("«" + bad + "» عدد نیست — ذخیره نشد.", ToastKind.Warn); return; }
        var qty = Shamsi.Num(BulkQty);
        var buy = Shamsi.Num(BulkBuy);
        var market = Shamsi.Num(BulkMarket);
        if (qty <= 0) { _host.Toast("مقدار تیل را بنویسید", ToastKind.Warn); return; }

        var (_, income) = ProfitLossService.BulkBuy(qty, buy, market);
        var seller = await Dialogs.PromptAsync("خرید عمده", "نامِ فروشنده (اختیاری):") ?? "";
        var today = Shamsi.Today();

        await _host.ExtraIncomeLedger.AddAsync(new ExtraIncome
        {
            DateShamsi = today, DateKey = Shamsi.Key(today), MonthKey = Shamsi.MonthKey(today),
            Qty = qty, Buy = buy, Market = market, Seller = seller.Trim(), Amount = income,
        });

        BulkQty = BulkBuy = BulkMarket = "";
        _host.Toast("✅ ثبت شد", ToastKind.Ok);
        await RefreshAsync();
    }
}

/// <summary>یک سطرِ «از کجا آمد»: تاریخ، شیفت و پولِ هر تیل.</summary>
public sealed class ProfitSourceLine
{
    public ProfitSourceLine(WaraqSaleLine l)
    {
        Title = "📝 ورق " + l.DateShamsi + " — " + (l.Kind == ShiftKind.Night ? "شب" : "روز");
        var parts = new List<string>();
        if (l.PetrolLiters != 0) parts.Add($"⛽ {Shamsi.Money(Math.Round(l.PetrolLiters, 2), 2)} لیتر = {Shamsi.Money(Math.Round(l.PetrolMoney, 0, MidpointRounding.AwayFromZero))}");
        if (l.DieselLiters != 0) parts.Add($"🟤 {Shamsi.Money(Math.Round(l.DieselLiters, 2), 2)} لیتر = {Shamsi.Money(Math.Round(l.DieselMoney, 0, MidpointRounding.AwayFromZero))}");
        Detail = string.Join(" · ", parts);
        Total = Shamsi.Money(Math.Round(l.PetrolMoney + l.DieselMoney, 0, MidpointRounding.AwayFromZero)) + " افغانی";
    }
    public string Title { get; }
    public string Detail { get; }
    public string Total { get; }
}

/// <summary>یک سطرِ کارتِ «💰 سودِ واقعی».</summary>
public sealed record RealProfitRow(string Label, string Value, string BrushKey, bool Strong = false);
