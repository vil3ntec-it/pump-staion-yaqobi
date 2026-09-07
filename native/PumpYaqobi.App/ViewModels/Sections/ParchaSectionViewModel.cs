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

    /// <summary>جلوگیری از حلقه وقتی خودِ ‎calcShift‎ کادرِ فایده را می‌نویسد.</summary>
    private bool _writingProfitPer;

    partial void OnStartChanged(string v) => Recalc();
    partial void OnEndChanged(string v) => Recalc();
    partial void OnDebtChanged(string v) => Recalc();
    partial void OnPriceChanged(string v) => Recalc();

    /// <summary>
    /// ‎oninput="onProfitPerManual(p)"‎ در نسخهٔ وب هم بلافاصله ‎calcShift‎ را
    /// صدا می‌زد؛ پس تایپِ دستی هم از همین مسیر می‌گذرد و اگر فیِ خرید و فیِ
    /// فروش هر دو مثبت باشند، همان لحظه پس زده می‌شود. عمداً همان.
    /// </summary>
    partial void OnProfitPerChanged(string v) { if (!_writingProfitPer) Recalc(); }

    /// <summary>دکمهٔ «ذخیره شیفت …» روی همین کارت.</summary>
    [RelayCommand]
    private Task SaveAsync() => _owner.SaveShiftAsync(this);

    private void SetProfitPerQuiet(string v)
    {
        if (_profitPer == v) return;
        _writingProfitPer = true;
        _profitPer = v;
        OnPropertyChanged(nameof(ProfitPer));
        _writingProfitPer = false;
    }

    public decimal StartValue => Shamsi.Num(Start);
    public decimal EndValue => Shamsi.Num(End);
    public decimal DebtValue => Shamsi.Num(Debt);
    public decimal PriceValue => Shamsi.Num(Price);
    public decimal ProfitPerValue => Shamsi.Num(ProfitPer);

    /// <summary>‎calcShift(p)‎ با همان ورودی‌هایی که آن تابع از DOM می‌خواند.</summary>
    public ShiftCalc N => _owner.Calc.CalcShift(
        StartValue, EndValue, PriceValue, DebtValue, _owner.BuyPerLiter, ProfitPerValue);

    // خانه‌های خودکار و نوشته‌هایشان از خودِ ShiftCalc می‌آیند تا آزمونِ
    // برابری همان چیزی را بسنجد که این‌جا نشان داده می‌شود.
    public string SaleText => N.SaleText;
    public string MoneyText => N.MoneyText;
    public string ProfitText => N.ProfitText;
    public string AvailableText => N.AvailableText;
    public string BuyPerLabel => N.BuyPerLabel;

    /// <summary>‎availEl.style.color = available >= 0 ? var(--green) : var(--red)‎</summary>
    public string AvailableBrushKey => N.AvailableIsNegative ? "Pump.Danger" : "Pump.Ok";

    /// <summary>
    /// همان کاری که ‎calcShift‎ می‌کرد: اول کادرِ فایده را (در صورتِ خودکار
    /// بودن) می‌نویسد، بعد چهار خانهٔ محاسبه‌شده را تازه می‌کند.
    /// </summary>
    public void Recalc()
    {
        var n = N;
        if (n.ProfitPerIsAuto)
            SetProfitPerQuiet(n.ProfitPerBox.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture));

        foreach (var name in new[] { nameof(SaleText), nameof(MoneyText), nameof(ProfitText),
                                     nameof(AvailableText), nameof(AvailableBrushKey),
                                     nameof(BuyPerLabel) })
            OnPropertyChanged(name);
    }

    /// <summary>‎clearShiftInputs(p)‎ — و بعد نرخِ اتحادیه دوباره می‌نشیند.</summary>
    public void Clear() => Load(null);

    public void Load(ShiftData? s)
    {
        Name = s?.Name ?? "";
        PumpNum = s is null || s.PumpNum == 0 ? "" : s.PumpNum.ToString();
        Start = s is null || s.Start == 0m ? "" : Shamsi.Money(s.Start);
        End = s is null || s.End == 0m ? "" : Shamsi.Money(s.End);
        Debt = s is null || s.Debt == 0m ? "" : Shamsi.Money(s.Debt);
        Note = s?.Note ?? "";
        Saved = s is not null && s.Id != 0;

        SetProfitPerQuiet(s is null || s.ProfitPer == 0m ? "" : Shamsi.Money(s.ProfitPer));

        // ── باگ‌فیکسِ نسخهٔ وب که این‌جا هم لازم است ──────────────────────
        // ‎clearShiftInputs‎ فی را پاک می‌کرد و بعد ‎reapplyUnionRateToPrefix‎
        // نرخِ روزِ اتحادیه را دوباره می‌نشاند. اگر این نباشد، «پارچهٔ جدید»
        // نرخِ روز را می‌بَرد و کارمند باید هر بار دستی بنویسد.
        if (s is not null && s.Price != 0m) Price = Shamsi.Money(s.Price);
        else ReapplyUnionRate();

        Recalc();
    }

    /// <summary>
    /// ‎reapplyUnionRateToPrefix(p)‎ — نرخِ اتحادیهٔ **همین سوخت** در کادرِ فی.
    /// نرخِ پطرول و دیزل دو کلیدِ جدا هستند و هرگز جای هم نمی‌نشینند.
    /// </summary>
    public void ReapplyUnionRate()
    {
        var rate = _owner.UnionRate;
        Price = rate > 0m ? Shamsi.Money(rate) : "";
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

    /// <summary>
    /// ‎_forceNewParcha‎ و ‎_forceNewDiesel‎ — دو جفتِ کاملاً جدا. دکمهٔ «پارچهٔ
    /// جدیدِ روز» فقط پرچمِ روزِ **همان سوخت** را بالا می‌برد؛ پس زدنش هرگز
    /// پارچهٔ شب یا پارچهٔ سوختِ دیگر را دست نمی‌زند. خواستهٔ صریحِ صاحب ریپو.
    /// </summary>
    private readonly Dictionary<(FuelType, ShiftKind), bool> _forceNew = new();

    private bool ForceNew(FuelType f, ShiftKind k) =>
        _forceNew.TryGetValue((f, k), out var v) && v;

    private void SetForceNew(FuelType f, ShiftKind k, bool v) => _forceNew[(f, k)] = v;

    /// <summary>نرخِ اتحادیهٔ همین سوخت — ‎DB.unionRatePetrol‎ / ‎unionRateDiesel‎.</summary>
    internal decimal UnionRate => _host.Settings.UnionRate(Fuel);

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
        OnPropertyChanged(nameof(UnionRate));
        Day.Recalc(); Night.Recalc();
        _ = LoadAsync();
    }

    partial void OnDateFilterChanged(string v) => _ = ReloadLogAsync();

    protected override async Task LoadAsync()
    {
        await LoadCurrentAsync();
        await ReloadLogAsync();
    }

    /// <summary>
    /// ‎getCurrentReport(fuel)‎ — **آخرین** پارچهٔ همان سوخت، نه پارچهٔ تاریخِ
    /// نوشته‌شده. نسخهٔ وب هم همین را می‌کند؛ تاریخِ کادر تازه هنگامِ ذخیره
    /// سنجیده می‌شود و اگر فرق داشت، پارچهٔ تازه باز می‌شود.
    /// </summary>
    private async Task LoadCurrentAsync()
    {
        _current = await _host.ParchaData.CurrentAsync(Fuel);
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

    /// <summary>
    /// دکمهٔ «ذخیره شیفت …». کلِ منطقِ ‎saveShift‎ در
    /// <see cref="ParchaDataService.SaveShiftFlowAsync"/> است — همان‌جا که
    /// آزمونِ برابری با نسخهٔ وب آن را می‌سنجد. این‌جا فقط کادرها خوانده و
    /// نتیجه نشان داده می‌شود.
    /// </summary>
    internal async Task SaveShiftAsync(ShiftFormViewModel form)
    {
        var fuel = Fuel;
        var kind = form.Kind;

        var res = await _host.ParchaData.SaveShiftFlowAsync(new ShiftSaveRequest(
            Fuel: fuel,
            Kind: kind,
            DateShamsi: PaDate,
            Name: form.Name,
            PumpNum: (int)Shamsi.Num(form.PumpNum),
            Start: form.StartValue,
            End: form.EndValue,
            Price: form.PriceValue,
            Debt: form.DebtValue,
            BoxProfitPer: form.ProfitPerValue,
            AvailMan: 0m,
            Note: form.Note,
            BuyPerLiter: BuyPerLiter,
            ForceNew: ForceNew(fuel, kind)));

        if (!res.Ok) { _host.Toast(res.Error ?? "", ToastKind.Error); return; }

        SetForceNew(fuel, kind, false);
        _current = res.Report;
        if (res.Report is not null) ReportNumText = res.Report.ReportNum.ToString();

        form.Saved = true;
        _host.Toast(fuel == FuelType.Diesel
                        ? "✅ پارچه دیزل ذخیره شد"
                        : "✅ شیفت " + (form.IsDay ? "روز" : "شب") + " ذخیره شد",
                    ToastKind.Ok);

        // نسخهٔ وب پس از ذخیرهٔ دیزل فرم را خالی می‌کند، پطرول را نه
        if (fuel == FuelType.Diesel) form.Clear();

        await ReloadLogAsync();
    }

    partial void OnPaDateChanged(string v) => _ = PaDateChangedAsync((v ?? "").Trim());

    private async Task PaDateChangedAsync(string newDate)
    {
        if (newDate.Length == 0) return;

        if (_current is null)
        {
            _current = await _host.ParchaData.AddAsync(Fuel, newDate);
            ReportNumText = _current.ReportNum.ToString();
            SetForceNew(Fuel, ShiftKind.Day, false);
            SetForceNew(Fuel, ShiftKind.Night, false);
            await ReloadLogAsync();
            return;
        }

        var hasData = _current.DayShift is not null || _current.NightShift is not null;
        if (hasData)
        {
            if ((_current.DateShamsi ?? "") == newDate) return;
            _current = await _host.ParchaData.AddAsync(Fuel, newDate);
            ReportNumText = _current.ReportNum.ToString();
            SetForceNew(Fuel, ShiftKind.Day, false);
            SetForceNew(Fuel, ShiftKind.Night, false);
            Day.Clear(); Night.Clear();
            await ReloadLogAsync();
            _host.Toast("🆕 گزارش جدید برای " + newDate + " باز شد", ToastKind.Ok);
        }
        else
        {
            _current.DateShamsi = newDate;
            await _host.ParchaData.SaveReportAsync(_current);
            await ReloadLogAsync();
        }
    }

    /// <summary>«➕ گزارش جدید» — ‎startNewReport()‎.</summary>
    [RelayCommand]
    private async Task StartNewReportAsync()
    {
        if (_current is not null && _current.DayShift is null && _current.NightShift is null)
        { _host.Toast("گزارش فعلی هنوز خالی است", ToastKind.Error); return; }

        _current = await _host.ParchaData.AddAsync(Fuel, PaDate);
        ReportNumText = _current.ReportNum.ToString();
        Day.Clear(); Night.Clear();
        await ReloadLogAsync();
        _host.Toast("✅ گزارش جدید شروع شد", ToastKind.Ok);
    }

    /// <summary>
    /// «🆕 پارچهٔ جدید روز» — ‎newParcha('day', fuel)‎.
    ///
    /// ⚠️ این‌جا **هیچ رکوردی ساخته نمی‌شود**: فقط پرچم بالا می‌رود و همان یک
    /// کارت خالی می‌شود. پیش از این، هر بار زدنِ این دکمه یک پارچهٔ خالی در
    /// دیتابیس می‌ساخت و چون کارتِ دیگر هم از همان پارچهٔ تازه بار می‌شد،
    /// دادهٔ شیفتِ دیگر از جلوی چشم می‌رفت. نسخهٔ وب چنین نمی‌کند.
    /// </summary>
    [RelayCommand]
    private void NewParchaDay() => NewParcha(ShiftKind.Day);

    [RelayCommand]
    private void NewParchaNight() => NewParcha(ShiftKind.Night);

    private void NewParcha(ShiftKind kind)
    {
        SetForceNew(Fuel, kind, true);
        (kind == ShiftKind.Day ? Day : Night).Clear();
        _host.Toast("🆕 پارچهٔ " + (IsDiesel ? "دیزل " : "") + "جدید "
                    + (kind == ShiftKind.Day ? "روز" : "شب")
                    + " — فرم خالی شد، حالا پر و ذخیره کن", ToastKind.Info);
    }

    /// <summary>
    /// ‎transferBase(from, to)‎ + ‎confirmTransfer()‎ — ختمِ پایهٔ مبدا به شروعِ
    /// پایهٔ مقصد، و بعد بازمحاسبهٔ مقصد. کادرِ خالیِ مبدا انتقال نمی‌دهد.
    /// </summary>
    [RelayCommand]
    private void PullBase(ShiftFormViewModel? to)
    {
        if (to is null) return;
        TransferBase(to.IsDay ? Night : Day, to);
    }

    [RelayCommand]
    private void PushBase(ShiftFormViewModel? from)
    {
        if (from is null) return;
        TransferBase(from, from.IsDay ? Night : Day);
    }

    private void TransferBase(ShiftFormViewModel from, ShiftFormViewModel to)
    {
        if (from.End.Trim().Length == 0) { _host.Toast("ختم پایه مبدا خالی است", ToastKind.Error); return; }
        to.Start = from.End;     // نشستنِ مقدار خودش ‎calcShift(to)‎ را می‌آورد
        to.Recalc();
        _host.Toast("✅ پایه انتقال یافت", ToastKind.Ok);
    }

    /// <summary>
    /// ‎pdfShifts()‎ / ‎pdfDieselShifts()‎ — ورقِ «همهٔ گزارش‌ها».
    ///
    /// در حالتِ پطرول، دیزلِ **همان روزها** هم کنارِ پطرول می‌آید (کارِ
    /// ‎getDieselForDate‎ در نسخهٔ وب)؛ روزی که فقط دیزل دارد اصلاً کارت
    /// نمی‌گیرد — وگرنه ورق پر می‌شد از کارت‌هایی که نیمهٔ پطرولشان «ثبت
    /// نشده» است. در حالتِ دیزل، ورق فقط دیزل است و بخشِ پطرول ندارد.
    ///
    /// فیلترِ تاریخِ بالای فهرست روی ورق هم اثر می‌گذارد — چیزی که چاپ می‌شود
    /// همانی است که روی صفحه دیده می‌شود.
    /// </summary>
    [RelayCommand]
    private async Task PdfAsync()
    {
        var f = DateFilter.Trim();
        bool Keep(ParchaReport r) => f.Length == 0 || (r.DateShamsi ?? "").Contains(f);

        var diesel = (await _host.ParchaData.ListAsync(FuelType.Diesel, null)).Where(Keep).ToList();
        List<ParchaReport> all;

        if (IsDiesel)
        {
            all = diesel;
        }
        else
        {
            var petrol = (await _host.ParchaData.ListAsync(FuelType.Petrol, null))
                         .Where(Keep).ToList();
            var dates = petrol.Select(r => r.DateShamsi ?? "—").ToHashSet();
            all = petrol.Concat(diesel.Where(r => dates.Contains(r.DateShamsi ?? "—"))).ToList();
        }

        all = all.OrderBy(r => r.DateKey).ThenBy(r => r.Id).ToList();

        var input = new ShiftsReportInput(all, DocDates.Line(), IsDiesel);
        await Documents.ShowAsync(() => new ShiftsReport(input),
                                  "گزارش‌های پارچه — " + FuelLabel);
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
