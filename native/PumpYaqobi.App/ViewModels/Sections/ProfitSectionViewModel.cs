using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PumpYaqobi.App.Services;
using PumpYaqobi.Application.Localization;
using PumpYaqobi.Application.Services;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;

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
    private readonly AppHost _host;
    private ProfitInput _db = new();

    public ProfitSectionViewModel(AppHost host)
        : base("profit", "profit", "مفاد / ضرر / اتحادیه") => _host = host;

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
    }

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
        _host.Settings.Set(fuel == FuelType.Diesel
            ? PumpYaqobi.Services.Data.SettingsService.UnionRateDiesel
            : PumpYaqobi.Services.Data.SettingsService.UnionRatePetrol, Shamsi.Num(value));
    }

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
        // ⛔ «برای یک جمع، همهٔ ردیف‌ها را نخوان» (۱۴۰۵/۰۷/۱۳).
        //  این صفحه تا امروز با هر فعال‌سازی همهٔ پارچه‌های پنج سال (با هر دو
        //  شیفت)، همهٔ مصارف، همهٔ درآمدهای اضافی و همهٔ فاکتورها را به شیءِ
        //  کامل می‌خواند تا پنج عدد جمع بزند — و چون SQLite کارِ «async»ش را
        //  روی همان نخِ صداکننده می‌کند، آن خواندن روی نخِ رابط بود: همان
        //  مکثی که کاربر درست سرِ اسکرولِ این صفحه حس می‌کرد.
        //  حالا هر پنج عدد از همان یکی-دو ستونِ خودشان می‌آیند.
        var petrol = await _host.StorageData.ShiftSumsAsync(FuelType.Petrol);
        var diesel = await _host.StorageData.ShiftSumsAsync(FuelType.Diesel);

        // «بی‌فاکتور»ها — بردگی‌شان مستقیم درآمد است (از قبل جمعِ خودِ SQLite بود)
        var noinvAccounts = (await _host.Debtors.CardAccountsAsync(noInvoice: true))
            .Values.SelectMany(x => x).ToList();

        var extraSum = await _host.ExtraIncomeLedger.SumAsync(e => e.Amount);
        _db = new ProfitInput
        {
            ShiftProfitPetrol = petrol.Profit,
            ShiftProfitDiesel = diesel.Profit,
            NoInvoiceAccounts = noinvAccounts,
            ExtraIncomeSum = extraSum,
            ExpenseSum = await _host.ExpenseLedger.SumAsync(e => e.Amount),
            InvoiceRateDiffSum = ProfitLossService.InvoiceRateDiff(await _host.Invoices.ApprovedRatesAsync()),
        };

        _filling = true;
        var p = _host.Settings.GetDecimal(PumpYaqobi.Services.Data.SettingsService.UnionRatePetrol);
        var d = _host.Settings.GetDecimal(PumpYaqobi.Services.Data.SettingsService.UnionRateDiesel);
        UnionPetrol = p == 0 ? "" : Shamsi.Money(p);
        UnionDiesel = d == 0 ? "" : Shamsi.Money(d);
        _filling = false;

        BulkSumText = Money(extraSum);
        Recalc();
    }

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
