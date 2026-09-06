using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PumpYaqobi.App.Services;
using PumpYaqobi.Application.Localization;
using PumpYaqobi.Application.Services;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;
using PumpYaqobi.Services.Data;

namespace PumpYaqobi.App.ViewModels.Sections;

/// <summary>
/// یک کارتِ شیفت — «☀️ شیفت روزانه» یا «🌙 شیفت شبانه».
/// چیدمانِ خانه‌ها مو‌به‌مو همان کارتِ نسخهٔ وب است: نامِ کارمند و شمارهٔ پایه،
/// شروع و ختمِ پایه با دکمهٔ انتقال، جملهٔ قرض، فی لیتر، فایدهٔ فی‌لیتر
/// (اتومات از مخزن) و چهار خانهٔ خودکار.
/// </summary>
public sealed partial class ShiftFormViewModel : ObservableObject
{
    private readonly ParchaSectionViewModel _owner;

    public ShiftFormViewModel(ShiftKind kind, ParchaSectionViewModel owner)
    {
        Kind = kind; _owner = owner;
    }

    public ShiftKind Kind { get; }
    public bool IsDay => Kind == ShiftKind.Day;
    public bool IsNight => !IsDay;

    public string Title => (IsDay ? "☀️ شیفت روزانه (" : "🌙 شیفت شبانه (") + _owner.FuelLabel + ")";
    public string SaveText => IsDay ? "💾 ذخیره شیفت روز" : "💾 ذخیره شیفت شب";
    public string SavedText => IsDay ? "✅ شیفت روز ذخیره شد" : "✅ شیفت شب ذخیره شد";
    /// <summary>«← از شب» روی کارتِ روز و «← از روز» روی کارتِ شب.</summary>
    public string PullText => IsDay ? "← از شب" : "← از روز";
    public string PushText => IsDay ? "→ به شب" : "→ به روز";

    [ObservableProperty] private string _name = "";
    [ObservableProperty] private string _pumpNum = "";
    [ObservableProperty] private string _start = "";
    [ObservableProperty] private string _end = "";
    [ObservableProperty] private string _debt = "";
    [ObservableProperty] private string _price = "";
    [ObservableProperty] private string _profitPer = "";
    [ObservableProperty] private string _note = "";
    [ObservableProperty] private bool _saved;

    /// <summary>فایدهٔ فی‌لیتر را کاربر خودش نوشته — دیگر خودکار پر نمی‌شود.</summary>
    private bool _profitPerManual;

    partial void OnStartChanged(string v) => Recalc();
    partial void OnEndChanged(string v) => Recalc();
    partial void OnDebtChanged(string v) => Recalc();
    partial void OnProfitPerChanged(string v) => Recalc();

    partial void OnPriceChanged(string v)
    {
        AutoFillProfitPer();
        Recalc();
    }

    /// <summary>کاربر روی خانهٔ فایده تایپ کرد.</summary>
    public void MarkProfitPerManual() => _profitPerManual = true;

    /// <summary>دکمهٔ «ذخیره شیفت …» روی همین کارت.</summary>
    [RelayCommand]
    private Task SaveAsync() => _owner.SaveShiftAsync(this);

    /// <summary>
    /// ‎autoFillProfitPerFromBuy‎ — فایدهٔ فی‌لیتر = فیِ فروش − فیِ خریدِ مخزن.
    /// اگر کاربر خودش نوشته باشد، دست نمی‌خورد.
    /// </summary>
    public void AutoFillProfitPer()
    {
        if (_profitPerManual) return;
        var buy = _owner.BuyPerLiter;
        if (buy <= 0m) return;
        var price = Shamsi.Num(Price);
        if (price <= 0m) return;
        SetProfitPerQuiet((price - buy).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture));
    }

    private void SetProfitPerQuiet(string v)
    {
        if (_profitPer == v) return;
        _profitPer = v;
        OnPropertyChanged(nameof(ProfitPer));
    }

    public decimal StartValue => Shamsi.Num(Start);
    public decimal EndValue => Shamsi.Num(End);
    public decimal DebtValue => Shamsi.Num(Debt);
    public decimal PriceValue => Shamsi.Num(Price);
    public decimal ProfitPerValue => Shamsi.Num(ProfitPer);

    private ShiftNumbers N =>
        _owner.Calc.Compute(StartValue, EndValue, PriceValue, ProfitPerValue, DebtValue);

    /// <summary>خانه‌های خودکار — تا وقتی چیزی وارد نشده «—» می‌مانند، مثلِ نسخهٔ وب.</summary>
    public string SaleText => Empty ? "—" : Shamsi.Money(N.Sale);
    public string MoneyText => Empty ? "—" : Shamsi.Money(N.Money);
    public string ProfitText => Empty ? "—" : Shamsi.Money(N.Profit);
    public string AvailableText => Empty ? "—" : Shamsi.Money(N.Available);

    private bool Empty => Start.Length == 0 && End.Length == 0;

    /// <summary>«فی خرید فعلی: ۳۰٫۱ ؋» — همان نوشتهٔ زیرِ خانهٔ فایده.</summary>
    public string BuyPerLabel =>
        _owner.BuyPerLiter > 0m
            ? "فی خرید فعلی: " + Shamsi.Money(Math.Round(_owner.BuyPerLiter, 1)) + " ؋"
            : "";

    public void Recalc()
    {
        foreach (var n in new[] { nameof(SaleText), nameof(MoneyText), nameof(ProfitText),
                                  nameof(AvailableText), nameof(BuyPerLabel) })
            OnPropertyChanged(n);
    }

    public void Load(ShiftData? s)
    {
        Name = s?.Name ?? "";
        PumpNum = s is null || s.PumpNum == 0 ? "" : s.PumpNum.ToString();
        Start = s is null || s.Start == 0m ? "" : Shamsi.Money(s.Start);
        End = s is null || s.End == 0m ? "" : Shamsi.Money(s.End);
        Debt = s is null || s.Debt == 0m ? "" : Shamsi.Money(s.Debt);
        Price = s is null || s.Price == 0m ? "" : Shamsi.Money(s.Price);
        _profitPerManual = false;
        ProfitPer = s is null || s.ProfitPer == 0m ? "" : Shamsi.Money(s.ProfitPer);
        Note = s?.Note ?? "";
        Saved = s is not null && s.Id != 0;
        AutoFillProfitPer();
        Recalc();
    }

    public ShiftData ToEntity(ShiftData? existing) =>
        (existing ?? new ShiftData()) is var s && s is not null
            ? Fill(s) : new ShiftData();

    private ShiftData Fill(ShiftData s)
    {
        s.Name = Name.Trim();
        s.PumpNum = (int)Shamsi.Num(PumpNum);
        s.Start = StartValue;
        s.End = EndValue;
        s.Debt = DebtValue;
        s.Price = PriceValue;
        s.ProfitPer = ProfitPerValue;
        s.BuyPerLiter = _owner.BuyPerLiter;
        s.Note = Note.Trim();
        return s;
    }
}

/// <summary>یک گزارشِ ۲۴ ساعته در فهرستِ پایینِ صفحه.</summary>
public sealed class ReportCardViewModel
{
    public ReportCardViewModel(ParchaReport r, ParchaService calc)
    {
        Entity = r;
        Title = $"گزارش #{r.ReportNum} — {r.DateShamsi}";
        var t = calc.Summarize(new[] { r });
        SaleText = Shamsi.Money(t.Sale);
        MoneyText = Shamsi.Money(t.Money);
        DebtText = Shamsi.Money(t.Debt);
        AvailableText = Shamsi.Money(t.Available);
        ProfitText = Shamsi.Money(t.Profit);
        DayName = r.DayShift?.Name ?? "—";
        NightName = r.NightShift?.Name ?? "—";
    }

    public ParchaReport Entity { get; }
    public string Title { get; }
    public string SaleText { get; }
    public string MoneyText { get; }
    public string DebtText { get; }
    public string AvailableText { get; }
    public string ProfitText { get; }
    public string DayName { get; }
    public string NightName { get; }
}

/// <summary>
/// ══ بخشِ پارچه‌ها ═══════════════════════════════════════════════════════════
/// همان صفحهٔ نسخهٔ وب: بالا کارتِ «ثبت پارچه فعلی» با دو کارتِ شیفتِ روز و شب
/// کنارِ هم، و پایین فهرستِ «گزارش‌های ۲۴ ساعته».
///
/// چهار عددِ خودکار (فروش، جملهٔ موجودی، فایده، پولِ موجود) از سرویسی می‌آیند
/// که با ۴۰۰ شیفتِ گرفته‌شده از خودِ نسخهٔ وب آزموده شده.
/// </summary>
public sealed partial class ParchaSectionViewModel : SectionViewModel
{
    private readonly AppHost _host;
    private ParchaReport? _current;

    public ParchaSectionViewModel(AppHost host) : base("shifts", "shifts", "پارچه‌ها")
    {
        _host = host;
        _paDate = Shamsi.Today();
        Day = new ShiftFormViewModel(ShiftKind.Day, this);
        Night = new ShiftFormViewModel(ShiftKind.Night, this);
    }

    internal ParchaService Calc => _host.Parcha;

    public ShiftFormViewModel Day { get; }
    public ShiftFormViewModel Night { get; }

    public ObservableCollection<ReportCardViewModel> Reports { get; } = new();

    [ObservableProperty] private bool _isDiesel;
    [ObservableProperty] private string _paDate;
    [ObservableProperty] private string _reportNumText = "—";
    [ObservableProperty] private string _dateFilter = "";

    public FuelType Fuel => IsDiesel ? FuelType.Diesel : FuelType.Petrol;
    public string FuelLabel => IsDiesel ? "دیزل" : "پطرول";
    public string CardTitle => "📝 ثبت پارچه فعلی (" + FuelLabel + ")";
    public string FuelToggleText => IsDiesel ? "⛽ رفتن به پطرول" : "🟤 دیزل";

    /// <summary>فیِ خریدِ همین سوخت — پایهٔ پر شدنِ خودکارِ «فایدهٔ فی‌لیتر».</summary>
    public decimal BuyPerLiter => _host.Settings.GetDecimal(
        IsDiesel ? SettingsService.BuyPerLiterDiesel : SettingsService.BuyPerLiterPetrol);

    partial void OnIsDieselChanged(bool v)
    {
        OnPropertyChanged(nameof(FuelLabel));
        OnPropertyChanged(nameof(CardTitle));
        OnPropertyChanged(nameof(FuelToggleText));
        OnPropertyChanged(nameof(Day));
        OnPropertyChanged(nameof(Night));
        Day.Recalc(); Night.Recalc();
        _ = LoadAsync();
    }

    partial void OnDateFilterChanged(string v) => _ = ReloadLogAsync();

    protected override async Task LoadAsync()
    {
        await LoadCurrentAsync();
        await ReloadLogAsync();
    }

    /// <summary>پارچهٔ همان تاریخ اگر باشد باز می‌شود، وگرنه کارت خالی می‌ماند.</summary>
    private async Task LoadCurrentAsync()
    {
        var list = await _host.ParchaData.ListAsync(Fuel, Shamsi.MonthKey(PaDate));
        _current = list.FirstOrDefault(r => r.DateShamsi == PaDate);
        ReportNumText = _current is null ? "—" : _current.ReportNum.ToString();
        Day.Load(_current?.DayShift);
        Night.Load(_current?.NightShift);
    }

    private async Task ReloadLogAsync()
    {
        var list = await _host.ParchaData.ListAsync(Fuel, null);
        Reports.Clear();
        var f = DateFilter.Trim();
        foreach (var r in list.OrderByDescending(r => r.DateKey).ThenByDescending(r => r.Id))
            if (f.Length == 0 || (r.DateShamsi ?? "").Contains(f))
                Reports.Add(new ReportCardViewModel(r, Calc));
    }

    /// <summary>گزارشِ جاری؛ اگر نبود ساخته می‌شود.</summary>
    private async Task<ParchaReport> EnsureReportAsync()
    {
        if (_current is not null) return _current;
        _current = await _host.ParchaData.AddAsync(Fuel, PaDate);
        ReportNumText = _current.ReportNum.ToString();
        return _current;
    }

    internal async Task SaveShiftAsync(ShiftFormViewModel form)
    {
        if (form.Name.Trim().Length == 0) { _host.Toast("نام کارمند را وارد کنید", ToastKind.Error); return; }
        if (form.EndValue < form.StartValue)
        { _host.Toast("ختم پایه نمی‌تواند کمتر از شروع باشد", ToastKind.Error); return; }

        var rep = await EnsureReportAsync();
        var existing = form.Kind == ShiftKind.Day ? rep.DayShift : rep.NightShift;
        var shift = form.ToEntity(existing);
        await _host.ParchaData.SaveShiftAsync(rep, form.Kind, shift);
        if (form.Kind == ShiftKind.Day) rep.DayShift = shift; else rep.NightShift = shift;
        form.Saved = true;
        _host.Toast("✅ شیفت " + (form.IsDay ? "روز" : "شب") + " ذخیره شد", ToastKind.Ok);
        await ReloadLogAsync();
    }

    partial void OnPaDateChanged(string v) => _ = LoadCurrentAsync();

    /// <summary>«➕ گزارش جدید» — پارچهٔ تازه با همان تاریخ.</summary>
    [RelayCommand]
    private async Task StartNewReportAsync()
    {
        _current = await _host.ParchaData.AddAsync(Fuel, PaDate);
        ReportNumText = _current.ReportNum.ToString();
        Day.Load(null); Night.Load(null);
        await ReloadLogAsync();
    }

    /// <summary>«🆕 پارچهٔ جدید روز/شب» — کارت خالی می‌شود، پارچهٔ پیشین دست‌نخورده می‌ماند.</summary>
    [RelayCommand]
    private async Task NewParchaDayAsync()
    {
        _current = await _host.ParchaData.AddAsync(Fuel, PaDate);
        ReportNumText = _current.ReportNum.ToString();
        Day.Load(null);
        await ReloadLogAsync();
    }

    [RelayCommand]
    private async Task NewParchaNightAsync()
    {
        _current = await _host.ParchaData.AddAsync(Fuel, PaDate);
        ReportNumText = _current.ReportNum.ToString();
        Night.Load(null);
        await ReloadLogAsync();
    }

    /// <summary>«← از شب» / «→ به شب» — انتقالِ ختمِ یک شیفت به شروعِ دیگری.</summary>
    [RelayCommand]
    private void PullBase(ShiftFormViewModel? to)
    {
        if (to is null) return;
        var from = to.IsDay ? Night : Day;
        if (from.End.Trim().Length == 0) { _host.Toast("ختم پایه مبدا خالی است", ToastKind.Error); return; }
        to.Start = from.End;
    }

    [RelayCommand]
    private void PushBase(ShiftFormViewModel? from)
    {
        if (from is null) return;
        var to = from.IsDay ? Night : Day;
        if (from.End.Trim().Length == 0) { _host.Toast("ختم پایه مبدا خالی است", ToastKind.Error); return; }
        to.Start = from.End;
    }

    [RelayCommand]
    private void ToggleFuel() => IsDiesel = !IsDiesel;

    [RelayCommand]
    private async Task DeleteReportAsync(ReportCardViewModel? card)
    {
        if (card is null) return;
        await _host.ParchaData.DeleteAsync(card.Entity.Id);
        if (_current?.Id == card.Entity.Id) { _current = null; ReportNumText = "—"; Day.Load(null); Night.Load(null); }
        await ReloadLogAsync();
    }
}
