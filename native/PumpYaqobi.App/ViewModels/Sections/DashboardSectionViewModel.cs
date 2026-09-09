using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PumpYaqobi.App.Services;
using PumpYaqobi.Application.Localization;
using PumpYaqobi.Application.Services;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;

namespace PumpYaqobi.App.ViewModels.Sections;

/// <summary>یکی از پنج کارتِ بالای داشبورد (‎.dash-card‎).</summary>
public sealed partial class DashCardViewModel : ObservableObject
{
    public DashCardViewModel(string title, string colorKey, string? goSection)
    { _title = title; ColorKey = colorKey; GoSection = goSection; }

    public string ColorKey { get; }
    public string? GoSection { get; }

    [ObservableProperty] private string _title;
    [ObservableProperty] private string _value = "0";
    /// <summary>خطِ رشد: «↑ ۲۴٪ نسبت به دیروز» یا متنِ ثابتِ کارت‌های بی‌رشد.</summary>
    [ObservableProperty] private string _delta = "";
    [ObservableProperty] private string _deltaColorKey = "Pump.Muted";
    [ObservableProperty] private string _sub = "";

    /// <summary>‎_dashDelta‎ — پیکان، درصد و «نسبت به …».</summary>
    public void SetDelta(int? pct, string word)
    {
        if (pct is null)
        {
            Delta = "داده کافی برای مقایسه نیست";
            DeltaColorKey = "Pump.Muted";
            return;
        }
        var v = Math.Max(-999, Math.Min(999, pct.Value));
        var arrow = v > 0 ? "↑" : v < 0 ? "↓" : "•";
        Delta = $"{arrow} {Math.Abs(v)}٪ نسبت به {word}";
        DeltaColorKey = v > 0 ? "Pump.Ok" : v < 0 ? "Pump.Danger" : "Pump.Muted";
    }

    /// <summary>‎_dashDeltaTxt‎ — همان خط، ولی بدونِ درصد (مثلِ کارتِ قرض‌داران).</summary>
    public void SetDeltaText(string t) { Delta = t; DeltaColorKey = "Pump.Muted"; }
}

/// <summary>یک ردیفِ «وضعیت سوخت‌ها» (‎.fs-row‎).</summary>
public sealed class FuelStatusViewModel
{
    public required string Icon { get; init; }
    public required string Name { get; init; }
    public required string State { get; init; }
    public required string ColorKey { get; init; }
    public required double Percent { get; init; }
    public required string PercentText { get; init; }
    public required string CurrentText { get; init; }
    public required string CapacityText { get; init; }
    public required string GoSection { get; init; }
}

/// <summary>یک قلمِ فهرستِ «آخرین هشدارها» (‎_dashAlertItems‎).</summary>
public sealed class DashAlertViewModel
{
    public required string Icon { get; init; }
    public required string Text { get; init; }
    public required string Sub { get; init; }
    public required string ColorKey { get; init; }
    public string? GoSection { get; init; }
    /// <summary>هشدارِ واقعی است (‎cls !== 'ok'‎) و در شمارشِ زنگ می‌آید.</summary>
    public required bool IsReal { get; init; }
}

/// <summary>یک ستونِ نمودارِ فروش (‎.db-col‎).</summary>
public sealed partial class DashBarViewModel : ObservableObject
{
    public required int Index { get; init; }
    public required string Label { get; init; }
    /// <summary>بلندیِ ستون به درصد — همان ‎data-h‎، دستِ‌کم ۲.</summary>
    public required double Height { get; init; }
    public required string Tip { get; init; }
    [ObservableProperty] private bool _isSelected;
}

/// <summary>یک ردیفِ «آخرین فروش‌ها».</summary>
public sealed class DashRecentViewModel
{
    public required string Date { get; init; }
    public required string When { get; init; }
    public required string Fuel { get; init; }
    public required string Money { get; init; }
    public required string Liters { get; init; }
    public required string GoSection { get; init; }
}

/// <summary>یک سوخت روی «موجودی مخازن».</summary>
public sealed class DashTankLegendViewModel
{
    public required string Name { get; init; }
    public required string ColorKey { get; init; }
    public required string Line { get; init; }
    public required double Share { get; init; }
}

/// <summary>
/// ══ داشبورد ═════════════════════════════════════════════════════════════════
/// همان صفحه‌ای که ‎#sec-dashboard‎ نشان می‌دهد: نوارِ بالا، پنج کارتِ آماری،
/// «وضعیت سوخت‌ها / روند فروش / موجودی مخازن»، و «آخرین هشدارها / نمودار فروش /
/// آخرین فروش‌ها».
///
/// ⚠️ داشبورد فقط می‌خواند. هیچ‌کدام از این محاسبه‌ها چیزی در پایگاه داده
/// نمی‌نویسند — درست مثلِ نسخهٔ وب.
/// </summary>
public sealed partial class DashboardSectionViewModel : SectionViewModel
{
    private readonly AppHost _host;
    private readonly DashboardService _calc = new();
    private readonly MainViewModel _main;

    private DashInput _db = new();
    private List<DashBucket> _disp = new();
    private int _slot;
    private decimal _petrolStock, _dieselStock, _petrolCap, _dieselCap, _threshold;

    public DashboardSectionViewModel(AppHost host, MainViewModel main)
        : base("dashboard", "dashboard", "داشبورد")
    { _host = host; _main = main; }

    // ── کارت‌های بالا ────────────────────────────────────────────────────────
    public DashCardViewModel AlertsCard { get; } = new("هشدارها", "Pump.Danger", null);
    public DashCardViewModel SalesCard { get; } = new("فروش تیل امروز", "Pump.Ok", "shifts");
    public DashCardViewModel DebtCard { get; } = new("الباقی قرض‌داران", "Pump.Info", "debt");
    public DashCardViewModel ExpenseCard { get; } = new("مصارف امروز", "Pump.Accent", "expenses");
    public DashCardViewModel SafeCard { get; } = new("موجودی گاوصندوق", "Pump.Ok", "safe");

    public IReadOnlyList<DashCardViewModel> Cards => new[]
        { AlertsCard, SalesCard, DebtCard, ExpenseCard, SafeCard };

    public ObservableCollection<FuelStatusViewModel> FuelStatus { get; } = new();
    public ObservableCollection<DashAlertViewModel> Alerts { get; } = new();
    public ObservableCollection<DashBarViewModel> Bars { get; } = new();
    public ObservableCollection<DashRecentViewModel> Recent { get; } = new();
    public ObservableCollection<DashTankLegendViewModel> TankLegend { get; } = new();

    // ── وضعیتِ نوارِ بالا و پایین ─────────────────────────────────────────────
    [ObservableProperty] private string _greeting = "سلام 👋";
    [ObservableProperty] private string _userName = "مدیر سیستم";
    [ObservableProperty] private string _clock = "00:00:00";
    [ObservableProperty] private string _dateLine = "";
    [ObservableProperty] private int _bellCount;
    [ObservableProperty] private string _footNote = "";
    [ObservableProperty] private string _searchText = "";

    // ── انتخاب‌ها ────────────────────────────────────────────────────────────
    [ObservableProperty] private DashRange _range = DashRange.Day;
    [ObservableProperty] private DashFuel _fuel = DashFuel.All;
    [ObservableProperty] private int? _selectedIndex;

    public bool IsRangeDay => Range == DashRange.Day;
    public bool IsRangeWeek => Range == DashRange.Week;
    public bool IsRangeMonth => Range == DashRange.Month;
    public bool IsRangeYear => Range == DashRange.Year;
    public bool IsFuelAll => Fuel == DashFuel.All;
    public bool IsFuelPetrol => Fuel == DashFuel.Petrol;
    public bool IsFuelDiesel => Fuel == DashFuel.Diesel;

    // ── متن‌های ساختهٔ رندر ──────────────────────────────────────────────────
    [ObservableProperty] private string _chartSub = "";
    [ObservableProperty] private string _areaSub = "";
    [ObservableProperty] private string _detailDate = "";
    [ObservableProperty] private string _detailMoney = "";
    [ObservableProperty] private string _detailLiters = "";
    [ObservableProperty] private string _detailCount = "";
    [ObservableProperty] private string _detailFuel = "";
    [ObservableProperty] private string _detailGrowth = "";
    [ObservableProperty] private string _detailGrowthColorKey = "Pump.Muted";
    [ObservableProperty] private double _donutPercent;
    [ObservableProperty] private string _donutPercentText = "0٪";
    [ObservableProperty] private string _donutNote = "";
    /// <summary>سهمِ پطرول از کلِ ظرفیت — کمانِ اولِ دایره.</summary>
    [ObservableProperty] private double _donutPetrolShare;
    [ObservableProperty] private double _donutDieselShare;

    /// <summary>نقاطِ «روند فروش» (لیتر) برای رسمِ سطحی، از چپ به راست.</summary>
    [ObservableProperty] private IReadOnlyList<double> _areaValues = Array.Empty<double>();
    [ObservableProperty] private IReadOnlyList<string> _areaLabels = Array.Empty<string>();
    /// <summary>روندِ «مفاد» و «مصارف» — همان دو خطِ زیرِ نمودارِ ستونی.</summary>
    [ObservableProperty] private IReadOnlyList<double> _trendProfit = Array.Empty<double>();
    [ObservableProperty] private IReadOnlyList<double> _trendExpense = Array.Empty<double>();

    partial void OnRangeChanged(DashRange v)
    {
        SelectedIndex = null;
        foreach (var n in new[] { nameof(IsRangeDay), nameof(IsRangeWeek), nameof(IsRangeMonth), nameof(IsRangeYear) })
            OnPropertyChanged(n);
        Render();
    }

    partial void OnFuelChanged(DashFuel v)
    {
        foreach (var n in new[] { nameof(IsFuelAll), nameof(IsFuelPetrol), nameof(IsFuelDiesel) })
            OnPropertyChanged(n);
        Render();
    }

    [RelayCommand] private void SetRange(string r) => Range = r switch
    {
        "week" => DashRange.Week, "month" => DashRange.Month, "year" => DashRange.Year, _ => DashRange.Day,
    };

    [RelayCommand] private void SetFuel(string f) => Fuel = f switch
    {
        "petrol" => DashFuel.Petrol, "diesel" => DashFuel.Diesel, _ => DashFuel.All,
    };

    [RelayCommand] private void SelectBar(DashBarViewModel? b)
    {
        if (b is null) return;
        SelectedIndex = b.Index;
        Render();
    }

    /// <summary>
    /// لینک‌های داشبورد. شناسه می‌تواند زیربخش هم باشد (مثلاً ‎oldloans‎)، پس
    /// از ‎GoByIdAsync‎ می‌رود که هر دو را می‌شناسد — نه از جست‌وجوی دستی در
    /// ‎Sections‎ که فقط هجده بخشِ نوار را دارد.
    /// </summary>
    [RelayCommand] private void Go(string? section)
    {
        if (string.IsNullOrEmpty(section)) return;
        _ = _main.GoByIdAsync(section);
    }

    /// <summary>زنگِ بالا — همان ‎dashBellClick‎: فهرستِ هشدارها در یک پیام.</summary>
    [RelayCommand]
    private void Bell()
    {
        var real = Alerts.Where(a => a.IsReal).ToList();
        if (real.Count == 0) { _host.Toast("🔔 اعلان تازه‌ای نیست — همه‌چیز مرتب است", ToastKind.Ok); return; }
        _host.Toast("🔔 " + string.Join(" · ", real.Select(a => a.Text)), ToastKind.Warn);
    }

    protected override Task LoadAsync() => RefreshAsync();

    /// <summary>داشبورد خلاصهٔ بقیهٔ بخش‌هاست، پس هر بارِ ورود از نو خوانده می‌شود.</summary>
    public override Task OnActivatedAsync() => RefreshAsync();

    /// <summary>خواندنِ دوبارهٔ همهٔ منبع‌ها و رسمِ صفحه.</summary>
    public async Task RefreshAsync()
    {
        var petrolReports = await _host.StorageData.ReportsAsync(FuelType.Petrol);
        var dieselReports = await _host.StorageData.ReportsAsync(FuelType.Diesel);
        var expenses = await _host.ExpenseLedger.ListAsync(null);
        var safe = await _host.SafeLedger.ListAsync(null);

        _db = new DashInput
        {
            Reports = petrolReports.Concat(dieselReports).ToList(),
            Expenses = expenses,
            SafeEntries = safe,
        };

        _threshold = _host.Settings.GetDecimal(
            PumpYaqobi.Services.Data.SettingsService.LowStockThreshold, 1000m);

        var pPur = await _host.StorageData.PurchasesAsync(FuelType.Petrol);
        var dPur = await _host.StorageData.PurchasesAsync(FuelType.Diesel);
        // اصلاحِ میله‌زنی این‌جا هم شمرده می‌شود، وگرنه داشبورد و صفحهٔ مخزن
        // دو عددِ مختلف نشان می‌دادند
        var pDip = await _host.StorageData.DipsAsync(FuelType.Petrol);
        var dDip = await _host.StorageData.DipsAsync(FuelType.Diesel);
        _petrolStock = _host.Storage.Tank(pPur, petrolReports, _threshold, pDip).Current;
        _dieselStock = _host.Storage.Tank(dPur, dieselReports, _threshold, dDip).Current;
        _petrolCap = _host.Settings.GetDecimal("tankCapacity_petrol", 0m);
        _dieselCap = _host.Settings.GetDecimal("tankCapacity_diesel", 0m);

        _debt = await BuildDebtInfoAsync();
        FootNote = (_host.Settings.GetString(PumpYaqobi.Services.Data.SettingsService.StationName)
                    is { Length: > 0 } sn ? sn : "سامانه مدیریت پمپ یعقوبی")
                   + " — نسخهٔ " + Update.AppVersion.Current;
        Render();
    }

    private DashDebtInfo _debt;

    private async Task<DashDebtInfo> BuildDebtInfoAsync()
    {
        var persons = await _host.Debtors.ListAsync();
        // ⚠️ جمع‌ها از دیتابیس، بی خواندنِ ردیف‌ها — داشبورد هم مثلِ نوارِ بالا
        // با هر باز شدن این را می‌خواهد.
        var accounts = await _host.Debtors.CardAccountsAsync();
        decimal total = 0;
        foreach (var list in accounts.Values)
        {
            // ⚠️ این‌جا دیگر ‎NormalizeAccount‎ صدا زده نمی‌شود و **نباید** بشود:
            // ردیف‌هایی که ‎CardAccountsAsync‎ می‌دهد «ردیفِ خلاصه»اند و
            // خوددرمانی داخلِ خودِ کوئری انجام شده. اگر دوباره درمان شوند،
            // بردگی‌شان از «لیتر × فی»ی نداشته دوباره حساب می‌شود و صفر
            // می‌گردد — یعنی عددِ داشبورد خراب می‌شود.
            var t = _host.Debt.SumTotals(list);
            total += Fuel switch
            {
                DashFuel.Petrol => t.Petrol.Albaqi,
                DashFuel.Diesel => t.Diesel.Albaqi,
                _ => t.All.Albaqi,
            };
        }

        var companies = await _host.Companies.ListAsync();
        var comps = companies.Count(c => _host.Company.Summarize(c, c.Rows).AlbaqiAfn > 0);
        var invoices = (await _host.Invoices.ListAsync()).Count;
        return new DashDebtInfo(total, persons.Count, invoices, comps);
    }

    /// <summary>ساعت و تاریخِ نوارِ بالا — هر ثانیه، فقط وقتی داشبورد باز است.</summary>
    public void TickClock()
    {
        var d = DateTime.Now;
        Clock = $"{d.Hour:00}:{d.Minute:00}:{d.Second:00}";
        Greeting = d.Hour < 12 ? "صبح بخیر 👋" : d.Hour < 17 ? "چاشت بخیر 👋" : "شب بخیر 👋";
        DateLine = "· " + Shamsi.DayName(d) + " " + Shamsi.Of(d);
    }

    // ── رسم ─────────────────────────────────────────────────────────────────

    private static string Money(decimal v) => Shamsi.Money(Math.Round(v, 0, MidpointRounding.AwayFromZero));

    private void Render()
    {
        TickClock();

        var series = _calc.Series(Range, _db);
        _slot = series.Slot;
        _disp = series.Buckets.Skip(1).ToList();   // خانهٔ ۰ فقط پایهٔ رشد است
        var sel = SelectedIndex is null ? _slot : Math.Max(0, Math.Min(SelectedIndex.Value, _disp.Count - 1));
        var selB = _disp[sel];
        var prevB = sel == 0 ? series.Buckets[0] : _disp[sel - 1];
        var cur = DashboardService.Pick(selB, Fuel);
        var prev = DashboardService.Pick(prevB, Fuel);
        var isCur = sel == _slot;

        var rangeWord = Range switch
        {
            DashRange.Week => "این هفته", DashRange.Month => "این ماه",
            DashRange.Year => "امسال", _ => "امروز",
        };
        var perWord = Range switch
        {
            DashRange.Week => "هفته قبل", DashRange.Month => "ماه قبل",
            DashRange.Year => "سال قبل", _ => "روز قبل",
        };
        var bucketWord = isCur ? rangeWord : selB.Label;

        BuildAlerts();

        // ۱) فروش — بر حسبِ تیل، نه پول
        SalesCard.Title = "فروش تیل " + bucketWord;
        SalesCard.Value = Money(cur.Liters) + " لیتر";
        SalesCard.Sub = "⛽ " + Money(selB.Petrol.Liters) + " لیتر · 🟤 " + Money(selB.Diesel.Liters) + " لیتر";
        SalesCard.SetDelta(DashboardService.Growth(cur.Liters, prev.Liters), perWord);

        // ۲) قرض‌داران
        DebtCard.Value = Money(_debt.Total) + " افغانی";
        DebtCard.SetDeltaText("👥 " + _debt.Persons + " قرض‌دار");
        DebtCard.Sub = "🧾 " + _debt.Invoices + " فاکتور قرضی · 🏢 " + _debt.Companies + " شرکت";

        // ۳) مصارف
        var q = _calc.ExpQuick(_db.Expenses);
        ExpenseCard.Title = "مصارف " + bucketWord;
        ExpenseCard.Value = Money(selB.Exp) + " افغانی";
        ExpenseCard.Sub = "این ماه: " + Money(q.Month) + " افغانی";
        ExpenseCard.SetDelta(DashboardService.Growth(selB.Exp, prevB.Exp), perWord);

        // ۴) گاوصندوق
        var sb = DashboardService.SafeBalance(_db.SafeEntries);
        SafeCard.Value = Money(sb.Afn) + " افغانی" + (sb.Usd != 0 ? " · $" + Money(sb.Usd) : "");
        SafeCard.Sub = "ورود " + Money(selB.SafeIn) + " · خروج " + Money(selB.SafeOut);
        SafeCard.SetDelta(DashboardService.Growth(selB.SafeIn - selB.SafeOut, prevB.SafeIn - prevB.SafeOut), perWord);

        BuildTanks();
        BuildFuelStatus();
        BuildRecent();
        BuildBars(sel);

        var gPct = DashboardService.Growth(cur.Afn, prev.Afn);
        DetailDate = "📅 " + selB.Full;
        DetailMoney = "💰 فروش: " + Money(cur.Afn) + " افغانی";
        DetailLiters = "🛢️ لیتر: " + Money(cur.Liters) + " لیتر (⛽ " + Money(selB.Petrol.Liters)
                     + " · 🟤 " + Money(selB.Diesel.Liters) + ")";
        DetailCount = "🧾 تعداد ثبت: " + cur.Count;
        DetailFuel = "سوخت: " + DashboardService.FuelWord(Fuel);
        DetailGrowth = "📈 رشد نسبت به " + perWord + ": "
                     + (gPct is null ? "داده کافی نیست" : (gPct > 0 ? "+" : "") + gPct + "٪");
        DetailGrowthColorKey = gPct is null or 0 ? "Pump.Muted" : gPct > 0 ? "Pump.Ok" : "Pump.Danger";

        ChartSub = DashboardService.FuelWord(Fuel) + " — روی هر ستون کلیک کنید تا جزئیات و رشد همان بازه بیاید";
        AreaSub = "فروش تیل (لیتر) " + bucketWord + " — " + DashboardService.FuelWord(Fuel) + " · روی هر نقطه بزنید";

        AreaValues = _disp.Select(b => (double)Math.Max(0, DashboardService.Pick(b, Fuel).Liters)).ToList();
        AreaLabels = _disp.Select(b => b.Label).ToList();
        TrendProfit = _disp.Select(b => (double)Math.Max(0, DashboardService.Pick(b, Fuel).Profit)).ToList();
        TrendExpense = _disp.Select(b => (double)Math.Max(0, b.Exp)).ToList();
    }

    private DashTank PetrolTank => DashboardService.Tank(FuelType.Petrol, _petrolStock,
        _petrolCap > 0 ? _petrolCap : null, _threshold);

    private DashTank DieselTank => DashboardService.Tank(FuelType.Diesel, _dieselStock,
        _dieselCap > 0 ? _dieselCap : null, _threshold);

    private void BuildTanks()
    {
        var list = new[] { PetrolTank, DieselTank };
        var totCur = list.Sum(t => t.Current);
        var totCap = list.Sum(t => t.Capacity);
        var pct = totCap > 0 ? (int)Math.Floor(Math.Min(100m, totCur / totCap * 100m) + 0.5m) : 0;
        DonutPercent = pct;
        DonutPercentText = pct + "٪";
        DonutPetrolShare = totCap > 0 ? (double)Math.Min(100m, list[0].Current / totCap * 100m) : 0;
        DonutDieselShare = totCap > 0 ? (double)Math.Min(100m, list[1].Current / totCap * 100m) : 0;

        TankLegend.Clear();
        var keys = new[] { "Pump.Ok", "Pump.Warn" };
        for (var i = 0; i < list.Length; i++)
            TankLegend.Add(new DashTankLegendViewModel
            {
                Name = list[i].Name,
                ColorKey = keys[i],
                Line = Money(list[i].Current) + " لیتر از " + Money(list[i].Capacity),
                Share = totCap > 0 ? (double)(list[i].Current / totCap * 100m) : 0,
            });

        // خطِ زیرِ دایره — «بیشترین موجودی»
        var top = list.OrderByDescending(t => t.Current).First();
        DonutNote = top.Current > 0
            ? "🏆 بیشترین موجودی: " + top.Name + " — " + Money(top.Current) + " لیتر ("
              + (top.Capacity > 0 ? (int)Math.Floor(Math.Min(100m, top.Current / top.Capacity * 100m) + 0.5m) : 0) + "٪)"
            : "هیچ تیلی در مخزن نیست";
    }

    private void BuildFuelStatus()
    {
        FuelStatus.Clear();
        foreach (var (t, sec) in new[] { (PetrolTank, "storage"), (DieselTank, "storage-diesel") })
        {
            string state, color;
            if (t.Raw <= 0) { state = "تمام شده"; color = "Pump.Danger"; }
            else if (t.Raw <= _threshold) { state = "کمبود"; color = "Pump.Danger"; }
            else if (t.Raw <= _threshold * 2) { state = "رو به اتمام"; color = "Pump.Warn"; }
            else { state = "موجودی کافی"; color = "Pump.Ok"; }

            var cap = Math.Max(0, t.Capacity);
            var pct = cap > 0 ? Math.Max(0, Math.Min(100, (int)Math.Floor(t.Current / cap * 100m + 0.5m))) : 0;
            var parts = t.Name.Split(' ');
            FuelStatus.Add(new FuelStatusViewModel
            {
                Icon = parts[0],
                Name = parts.Length > 1 ? parts[1] : t.Name,
                State = state,
                ColorKey = color,
                Percent = pct,
                PercentText = pct + "٪",
                CurrentText = "موجودی: " + Money(t.Current) + " لیتر",
                CapacityText = "ظرفیت: " + Money(cap) + " لیتر",
                GoSection = sec,
            });
        }
    }

    private void BuildAlerts()
    {
        Alerts.Clear();
        foreach (var (t, label, sec) in new[]
                 { (PetrolTank, "⛽ پطرول", "storage"), (DieselTank, "🟤 دیزل", "storage-diesel") })
        {
            var st = Math.Round(t.Raw, 0, MidpointRounding.AwayFromZero);
            if (st > _threshold) continue;
            Alerts.Add(new DashAlertViewModel
            {
                Icon = "⚠️",
                Text = st <= 0 ? "مخزن " + label + " تمام شده!"
                               : "مخزن " + label + " کم است — " + Shamsi.Money(st) + " لیتر",
                Sub = "همین حالا رسیدگی کنید",
                ColorKey = "Pump.Danger",
                GoSection = sec,
                IsReal = true,
            });
        }
        BellCount = Alerts.Count(a => a.IsReal);
        AlertsCard.Value = BellCount + " مورد";
        AlertsCard.SetDeltaText(BellCount > 0 ? "نیاز به بررسی" : "همه‌چیز مرتب است");
        AlertsCard.Sub = "برای دیدن فهرست بزنید";
    }

    private void BuildRecent()
    {
        var rows = new List<DashRecentViewModel>();
        foreach (var r in _db.Reports)
        {
            if (r.DateShamsi is null or "") continue;
            var isPetrol = r.Fuel == FuelType.Petrol;
            if (Fuel == DashFuel.Petrol && !isPetrol) continue;
            if (Fuel == DashFuel.Diesel && isPetrol) continue;
            foreach (var (w, sh) in new[] { ("روز", r.DayShift), ("شب", r.NightShift) })
            {
                if (sh is null || (sh.Money == 0 && sh.Sale == 0)) continue;
                rows.Add(new DashRecentViewModel
                {
                    Date = r.DateShamsi,
                    When = w,
                    Fuel = isPetrol ? "⛽ پطرول" : "🟤 دیزل",
                    Money = Money(sh.Money),
                    Liters = Money(sh.Sale) + " لیتر",
                    GoSection = isPetrol ? "shifts" : "shifts-diesel",
                });
            }
        }
        Recent.Clear();
        foreach (var r in rows.OrderByDescending(x => Shamsi.Key(x.Date)).Take(6)) Recent.Add(r);
    }

    private void BuildBars(int sel)
    {
        var vals = _disp.Select(b => DashboardService.Pick(b, Fuel)).ToList();
        var maxA = Math.Max(1m, vals.Count == 0 ? 1m : vals.Max(v => v.Afn));
        Bars.Clear();
        for (var i = 0; i < _disp.Count; i++)
        {
            var b = _disp[i];
            var v = vals[i];
            var h = (double)Math.Floor(v.Afn / maxA * 100m + 0.5m);
            Bars.Add(new DashBarViewModel
            {
                Index = i,
                Label = b.Label,
                Height = Math.Max(2, h),
                Tip = $"📅 {b.Full} — 💰 {Money(v.Afn)} افغانی · 🛢️ {Money(v.Liters)} لیتر · "
                    + $"📈 {Money(v.Profit)} مفاد · 🧾 {v.Count} ثبت",
                IsSelected = i == sel,
            });
        }
    }
}
