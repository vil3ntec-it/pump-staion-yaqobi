using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using PumpYaqobi.App.Services;
using PumpYaqobi.Application.Localization;
using PumpYaqobi.Application.Security;
using PumpYaqobi.Application.Services;

namespace PumpYaqobi.App.ViewModels.Sections;

/// <summary>
/// ══ گزارش پایان ماه ════════════════════════════════════════════════════════
/// رونوشتِ ‎renderMonthReport‎ · ‎_mrCompute‎ · ‎_mrAllKeys‎.
///
/// فقط نمایش است: از پارچه‌ها، مصارف، خریدها، رسیدها، گاوصندوق و تخلیهٔ تانکر
/// جمع می‌شود و روی هیچ حسابی اثر نمی‌گذارد.
///
/// ⚠️ «مفاد» و «نتیجهٔ خالص» پشتِ همان اجازه‌ای هستند که بخشِ مفاد/ضرر دارد
/// (‎ViewProfit‎ — در نسخهٔ وب رمزِ جداگانه بود). بدونِ آن اجازه، به‌جای عدد
/// قفل نشان داده می‌شود، نه صفر — صفرِ دروغ بدتر از قفل است.
/// </summary>
public sealed partial class MonthReportSectionViewModel : SectionViewModel
{
    private readonly AppHost _host;
    private MonthReportSource? _src;

    public MonthReportSectionViewModel(AppHost host)
        : base("monthreport", "profit", "گزارش ماهانه") => _host = host;

    public ObservableCollection<string> Months { get; } = new();

    [ObservableProperty] private string? _month;

    [ObservableProperty] private string _netText = "—";
    [ObservableProperty] private string _netBrushKey = "Pump.Text";
    [ObservableProperty] private string _netGrowthText = "—";
    [ObservableProperty] private string _salesText = "—";
    [ObservableProperty] private string _salesSubText = "";
    [ObservableProperty] private string _petrolText = "—";
    [ObservableProperty] private string _petrolSubText = "";
    [ObservableProperty] private string _dieselText = "—";
    [ObservableProperty] private string _dieselSubText = "";
    [ObservableProperty] private string _profitText = "🔒";
    [ObservableProperty] private string _profitSubText = "";
    [ObservableProperty] private string _extraText = "—";
    [ObservableProperty] private string _expenseText = "—";
    [ObservableProperty] private string _expenseSubText = "";
    [ObservableProperty] private string _rasidText = "—";
    [ObservableProperty] private string _rasidSubText = "";
    [ObservableProperty] private string _buyText = "—";
    [ObservableProperty] private string _buySubText = "";
    [ObservableProperty] private string _safeText = "—";
    [ObservableProperty] private string _safeSubText = "";
    [ObservableProperty] private string _tankerText = "—";
    [ObservableProperty] private string _tankerSubText = "";

    /// <summary>بی اجازهٔ «مفاد/ضرر»، مفاد و نتیجهٔ خالص قفل می‌مانند.</summary>
    public bool ProfitLocked => !_host.Permissions.Can(Permission.ViewProfit);

    public string MonthLabel => Label(Month);

    partial void OnMonthChanged(string? v)
    {
        OnPropertyChanged(nameof(MonthLabel));
        Recalc();
    }

    protected override Task LoadAsync() => ReloadSourceAsync();

    public override Task OnActivatedAsync() => ReloadSourceAsync();

    private async Task ReloadSourceAsync()
    {
        _src = await _host.Tools.MonthSourceAsync();
        var keys = _host.Tools.MonthKeys(_src);

        var keep = Month;
        Months.Clear();
        foreach (var k in keys) Months.Add(k);

        Month = keep is not null && keys.Contains(keep) ? keep : keys.FirstOrDefault();
        Recalc();
    }

    /// <summary>«اسد 1405» — همان ‎_monthLabel‎ی نسخهٔ وب.</summary>
    private static string Label(string? key)
    {
        var p = (key ?? "").Split('/');
        if (p.Length != 2 || !int.TryParse(p[1], out var m)) return key ?? "";
        var name = Shamsi.MonthName(m);
        return (name.Length > 0 ? name : p[1]) + " " + p[0];
    }

    private static string Round(decimal v) =>
        Shamsi.Money(Math.Round(v, 0, MidpointRounding.AwayFromZero));

    private static string GrowthText(int? pct) =>
        pct is null ? "—" : (pct > 0 ? "+" : "") + Shamsi.Money(pct.Value) + "٪";

    private void Recalc()
    {
        if (_src is null || Month is null) return;

        var cur = _host.Tools.Month(_src, Month);
        var prev = _host.Tools.Month(_src, MonthReportService.PrevKey(Month));
        var locked = ProfitLocked;

        NetText = locked ? "🔒" : Round(cur.Net) + " افغانی";
        NetBrushKey = locked ? "Pump.Muted" : cur.Net >= 0m ? "Pump.Ok" : "Pump.Danger";
        NetGrowthText = locked
            ? "با اجازهٔ «مفاد / ضرر» باز می‌شود"
            : (cur.Net >= 0m ? "✅ مفاد خالص" : "❌ ضرر خالص")
              + " · نسبت به ماه قبل: " + GrowthText(MonthReportService.Growth(cur.Net, prev.Net));

        SalesText = Round(cur.Sales) + " افغانی";
        SalesSubText = "🛢️ " + Round(cur.Liters) + " لیتر · نسبت به ماه قبل: "
                     + GrowthText(MonthReportService.Growth(cur.Sales, prev.Sales));

        PetrolText = Round(cur.Petrol.Amount) + " افغانی";
        PetrolSubText = Round(cur.Petrol.Liters) + " لیتر · "
                      + Shamsi.Money(cur.Petrol.Parcha) + " پارچه";

        DieselText = Round(cur.Diesel.Amount) + " افغانی";
        DieselSubText = Round(cur.Diesel.Liters) + " لیتر · "
                      + Shamsi.Money(cur.Diesel.Parcha) + " پارچه";

        ProfitText = locked ? "🔒" : Round(cur.Profit) + " افغانی";
        ProfitSubText = locked ? "با اجازهٔ مفاد/ضرر باز می‌شود"
            : "نسبت به ماه قبل: " + GrowthText(MonthReportService.Growth(cur.Profit, prev.Profit));

        ExtraText = Round(cur.Extra) + " افغانی";

        ExpenseText = Round(cur.Expenses) + " افغانی";
        ExpenseSubText = "نسبت به ماه قبل: "
                       + GrowthText(MonthReportService.Growth(cur.Expenses, prev.Expenses));

        RasidText = Round(cur.Rasid) + " افغانی";
        RasidSubText = Shamsi.Money(cur.RasidCount) + " رسید در این ماه";

        BuyText = Round(cur.BuyPetrol.Liters + cur.BuyDiesel.Liters) + " لیتر";
        BuySubText = "⛽ " + Round(cur.BuyPetrol.Liters) + " لیتر · 🟤 "
                   + Round(cur.BuyDiesel.Liters) + " لیتر — جمله "
                   + Round(cur.BuyPetrol.Amount + cur.BuyDiesel.Amount) + " افغانی";

        SafeText = "بردگی " + Round(cur.SafeBardagi);
        SafeSubText = "ماندگی " + Round(cur.SafeMandagi);

        TankerText = Shamsi.Money(cur.TankerCount) + " تخلیه";
        TankerSubText = cur.TankerShort > 0m
            ? "⚠️ جمله کم‌آمد: " + Round(cur.TankerShort) + " لیتر"
            : "کم‌آمدی ثبت نشده";
    }
}
