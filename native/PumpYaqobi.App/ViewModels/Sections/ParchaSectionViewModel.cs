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
    /// <summary>
    /// ══ ⛔ هیچ فلشی روی این دو دکمه نیست — و نباید برگردد ══════════════════
    ///
    /// گزارشِ صاحب ریپو (۱۴۰۵/۰۷/۰۶): «توی بخشِ پارچه‌ها دکمه‌های چپ و راست
    /// برعکس کار می‌کنن.»
    ///
    /// ⚠️ <b>رفتارشان درست بود</b> (‎PullBase‎ ختمِ کارتِ دیگر را در شروعِ
    /// این می‌نشاند و ‎PushBase‎ برعکس؛ هر دو سنجیده شدند). آن‌چه برعکس بود
    /// نوشتهٔ رویشان:
    ///
    ///   ۱) کارتِ روز ستونِ صفر است و در چیدمانِ راست‌به‌راست‌به‌چپ <b>سمتِ
    ///      راست</b> رندر می‌شود، پس «از شب» یعنی از چپ به راست — ولی فلشِ
    ///      ‎←‎ رویش نوشته بود.
    ///   ۲) و بدتر: ‎←‎ و ‎→‎ هر دو ‎Bidi_Mirrored=Yes‎ هستند، پس در متنِ
    ///      راست‌به‌چپ خودِ شکل‌دهنده آینه‌شان می‌کند. یعنی هر عددی هم که
    ///      می‌گذاشتیم، «درست» بودنش به رفتارِ رندرِ متن بند بود.
    ///
    /// پس فلش برداشته شد و جایش <b>واژه</b> نشست: ابهامِ جهت با واژه پیش
    /// نمی‌آید. ⛔ فلش را برنگردانید.
    /// </summary>
    public string PullText => IsDay ? "گرفتن از شب" : "گرفتن از روز";
    public string PushText => IsDay ? "فرستادن به شب" : "فرستادن به روز";

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

    // ══ «این پایه از پایهٔ قبلی کمتر است» ════════════════════════════════
    //
    // خواستهٔ صریحِ صاحب ریپو (۱۴۰۵/۰۷/۰۶): «اگر پارچهٔ اول ۲۲۳۰۰۰ بود و
    // چندین پارچه با عددهای بالاتر دادم و بعد یکی پیدا شد که ۲۲۲۰۰۰ باشد،
    // یک پیام بیاید بغلِ همان کادرِ شروع پایه و بگوید این کمتر است — و
    // مانعی نباشد.»
    //
    // ⛔ **مانع نیست.** ذخیره می‌شود، فقط نشان می‌گذارد. شمارندهٔ پایه واقعاً
    // می‌تواند عوض شود (تعویضِ پایه، صفر شدنِ شمارنده) و قفل کردنِ کاربر روی
    // دادهٔ درست بدتر از نشان دادنِ یک هشدار است.
    //
    // ⚠️ دکمهٔ «دیدم» فقط **همین یک ذخیره** را عادی می‌کند؛ با عوض شدنِ عدد
    // دوباره سنجیده می‌شود، وگرنه یک بار زدنش هشدار را برای همیشه می‌بُرد.

    /// <summary>هشدارِ کنارِ کادرِ «شروع پایه» — یک خط، همیشه همان‌جا.</summary>
    [ObservableProperty] private bool _lowBase;
    [ObservableProperty] private string _lowBaseText = "";

    /// <summary>
    /// ⛔ <b>خودِ عدد داخلِ کادرش سرخ می‌شود.</b> خواستهٔ صریحِ صاحب ریپو
    /// (۱۴۰۵/۰۷/۰۶): «و توی خود همون کادر هم عدد سرخ بشه.» پیش از این تنها
    /// نشانه یک کادرِ هشدارِ کنارِ فرم بود که با رفتن به کادرِ دیگر از چشم
    /// می‌افتاد.
    /// </summary>
    public string StartBrushKey => LowBase ? "Pump.Danger" : "Pump.Text";
    /// <summary>«دیدم / اوکی» زده شد — پس ردیفِ ورق سرخ نمی‌شود.</summary>
    [ObservableProperty] private bool _lowBaseAcked;

    /// <summary>سرخ شدنِ ردیفِ ورق = هشدار هست و «دیدم» زده نشده.</summary>
    public bool LowBaseUnacked => LowBase && !LowBaseAcked;

    partial void OnLowBaseChanged(bool v)
    {
        OnPropertyChanged(nameof(LowBaseUnacked));
        OnPropertyChanged(nameof(StartBrushKey));
    }
    partial void OnLowBaseAckedChanged(bool v) => OnPropertyChanged(nameof(LowBaseUnacked));

    /// <summary>«✔ دیدم» — سرخی برداشته می‌شود، عدد دست نمی‌خورد.</summary>
    [RelayCommand]
    private void AckLowBase() { LowBaseAcked = true; LowBase = false; }

    /// <summary>
    /// ⚠️ **حین بار شدنِ کارت سنجیده نمی‌شود** و این لازم است، نه سلیقه:
    /// «بزرگ‌ترین ختمِ ثبت‌شده» شاملِ ختمِ **خودِ همین شیفت** هم هست، پس هر
    /// شیفتِ ذخیره‌شده‌ای که باز شود ‎Start &lt; MaxEnd‎ می‌دهد و هشدار برای
    /// هر پارچهٔ سالمی هم بالا می‌آمد. هشدار مالِ لحظهٔ **تایپ** است.
    /// </summary>
    private bool _loading;

    partial void OnPumpNumChanged(string v) { if (!_loading) _owner.CheckLowBase(this); }

    partial void OnStartChanged(string v)
    {
        Recalc();
        if (_loading) return;
        LowBaseAcked = false;
        _owner.CheckLowBase(this);
    }
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
        _loading = true;
        Name = s?.Name ?? "";
        LowBase = false; LowBaseAcked = false; LowBaseText = "";
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

        _loading = false;
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

/// <summary>
/// یک سطر از «تاریخچهٔ پایه‌ها» — شروع و ختمِ یک شیفت، با نامِ کارمند، تاریخ،
/// شمارهٔ پایه و روزِ هفته. خواستهٔ صریحِ صاحب ریپو (۱۴۰۵/۰۷/۰۶).
/// </summary>
public sealed class BaseHistoryRowViewModel
{
    public BaseHistoryRowViewModel(ParchaDataService.BaseHistoryRow r, int index)
    {
        Index = index;
        DateShamsi = r.DateShamsi;
        DayName = r.DayName;
        ReportText = "#" + r.ReportNum;
        KindText = r.Kind == ShiftKind.Night ? "🌙 شب" : "☀️ روز";
        Name = r.Name.Length == 0 ? "—" : r.Name;
        PumpText = r.PumpNum == 0 ? "—" : Shamsi.Money(r.PumpNum);
        StartText = Shamsi.Money(r.Start);
        EndText = Shamsi.Money(r.End);
        LitersText = Shamsi.Money(r.End - r.Start);
        Low = r.Low;
        LowText = r.Low ? "🔴 کمتر" : "";
    }

    public int Index { get; }
    public string DateShamsi { get; }
    public string DayName { get; }
    public string ReportText { get; }
    public string KindText { get; }
    public string Name { get; }
    public string PumpText { get; }
    public string StartText { get; }
    public string EndText { get; }
    public string LitersText { get; }

    /// <summary>همان سرخیِ ورق — شروعی که از ختمِ پایهٔ قبلیِ همان شماره کمتر است.</summary>
    public bool Low { get; }
    public string LowText { get; }
    public string StartBrushKey => Low ? "Pump.Danger" : "Pump.Text";
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
        // «📝 یادداشت این بخش» — همتای ‎.sec-note-box‎ی سایت. کلیدش همان
        // کلیدِ نسخهٔ وب است تا نوت‌های واردشده سرِ جای خودشان بنشینند.
        Notes = new SectionNotesViewModel(Id, host.SectionNotes,
            (m, ok) => host.Toast(m, ok ? ToastKind.Ok : ToastKind.Warn));
        _host = host;
        _paDate = Shamsi.Today();
        // درِ «🕘 تاریخچه»ی همین بخش — شرحش بالای ‎SectionViewModel.HistoryKind‎
        HistoryKind = "shift";

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

    /// <summary>«🗂️ تاریخچهٔ پایه‌ها» — کشویی، پشتِ همان دکمه‌ای که تا امروز
    /// هیچ فرمانی نداشت.</summary>
    public BulkRows<BaseHistoryRowViewModel> BaseHistory { get; } = new();

    [ObservableProperty] private bool _showBaseHistory;

    /// <summary>
    /// «⛓️ برسیِ زنجیرهٔ پایه‌ها» — کلیدِ همان حالتِ **اختیاریِ** تازه:
    /// ختمِ هر پایه باید شروعِ پارچهٔ بعدیِ همان پایه باشد. خاموشش که کنید،
    /// فقط هشدارِ «کمتر است» می‌ماند (که از اول بود و اختیاری نیست).
    /// </summary>
    /// ⚠️ پیش‌فرضش همان پیش‌فرضِ تنظیمات است و مقدارِ واقعی در
    /// <see cref="LoadAsync"/> می‌نشیند، نه در سازنده: سازندهٔ بخش‌ها روی
    /// مسیرِ **باز شدنِ برنامه** است و خواندنِ ‎settings.json‎ آن‌جا همان
    /// هزینه‌ای است که قاعدهٔ «باز شدنِ برنامه» قدغنش کرده.
    [ObservableProperty] private bool _chainCheck = true;

    /// <summary>حین نشاندنِ مقدارِ ذخیره‌شده، ذخیرهٔ دوباره لازم نیست.</summary>
    private bool _loadingChainCheck;

    partial void OnChainCheckChanged(bool v)
    {
        if (!_loadingChainCheck)
        {
            var st = AppSettings.Load();
            st.ParchaChainCheck = v;
            st.SaveSoon();
        }
        // همان لحظه دوباره سنجیده شود — وگرنه کلید تا تایپِ بعدی بی‌اثر است
        CheckLowBase(Day);
        CheckLowBase(Night);
    }

    public string BaseHistoryToggleText =>
        ShowBaseHistory ? "🗂️ بستنِ تاریخچهٔ پایه‌ها" : "🗂️ تاریخچهٔ پایه‌ها";

    partial void OnShowBaseHistoryChanged(bool v)
    {
        OnPropertyChanged(nameof(BaseHistoryToggleText));
        if (v) _ = ReloadBaseHistoryAsync();
    }

    /// <summary>
    /// ⚠️ جدولِ داخلِ کادرِ کشویی عمداً ‎ExcelGrid‎ است: کادرِ بسته یعنی جدولِ
    /// نامرئی و جدولِ نامرئی ردیف‌هایش را پارک می‌کند — همان قاعده‌ای که
    /// ‎idle‎ قفلش کرده. و تا کاربر بازش نکند، **یک پرس‌وجو هم** زده نمی‌شود.
    /// </summary>
    [RelayCommand]
    private void ToggleBaseHistory() => ShowBaseHistory = !ShowBaseHistory;

    private async Task ReloadBaseHistoryAsync()
    {
        var list = await _host.ParchaData.BaseHistoryAsync(Fuel);
        using (BaseHistory.Batch())
        {
            BaseHistory.Clear();
            var i = 1;
            foreach (var r in list) BaseHistory.Add(new BaseHistoryRowViewModel(r, i++));
        }
    }

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
        OnPropertyChanged(nameof(BaseHistoryToggleText));
        _ = Services.CrashGuard.RunAsync("خواندنِ پارچه‌ها", LoadAsync);
    }

    partial void OnDateFilterChanged(string v) => _ = ReloadLogAsync();

    protected override async Task LoadAsync()
    {
        _loadingChainCheck = true;
        try { ChainCheck = AppSettings.Load().ParchaChainCheck; }
        catch { }
        finally { _loadingChainCheck = false; }

        await LoadCurrentAsync();
        await ReloadLogAsync();
        if (ShowBaseHistory) await ReloadBaseHistoryAsync();
    }

    // ══════════════════════════════════════════════════════════════════════
    //  ══ «این پایه کمتر است» ══════════════════════════════════════════════
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// بزرگ‌ترین ختمِ پایهٔ ثبت‌شده، به ازای هر شمارهٔ پایه — و ‎0‎ برای
    /// «هر شماره‌ای».
    ///
    /// ⚠️ در حافظه کَش می‌شود و فقط با ذخیره یا عوض شدنِ سوخت دوباره خوانده
    /// می‌شود: این سنجه با **هر حرفِ تایپ** صدا زده می‌شود و یک پرس‌وجو به
    /// ازای هر کلید یعنی همان کندی‌ای که قاعدهٔ سرعتِ این ریپو قدغنش کرده.
    /// </summary>
    private readonly Dictionary<(FuelType, int), decimal> _lastBase = new();

    internal void CheckLowBase(ShiftFormViewModel form)
    {
        var num = (int)Shamsi.Num(form.PumpNum);
        var start = form.StartValue;
        if (start <= 0m) { form.LowBase = false; form.LowBaseText = ""; return; }

        if (!_lastBase.TryGetValue((Fuel, num), out var prev))
        {
            // هنوز نمی‌دانیم — می‌پرسیم و همان لحظه دوباره می‌سنجیم
            _ = FillLastBaseAsync(Fuel, num, form);
            return;
        }

        Apply(form, start, prev, num);
    }

    /// <summary>
    /// ══ زنجیرهٔ پایه: «ختمِ این، شروعِ بعدی» ══════════════════════════════════
    ///
    /// خواستهٔ صریحِ صاحب ریپو (۱۴۰۵/۰۷/۰۶): «یکی رو رسوندم ۱۰۰۰۰۰ و ختمِ این
    /// باشه؛ شروعِ اون پارچهٔ جدید باید ۱۰۰۰۰۰ باشه، و اگه مثلاً ۱۰۰۰۰۱ بود
    /// بگه این مقدار از اون یکی بیشتر زده شده و بررسی باید بشه… و من چندین
    /// پایه دارم و می‌خوام با پایه‌ها در ارتباط باشن.»
    ///
    /// پس سه حال، و هر سه <b>به ازای همان شمارهٔ پایه</b>:
    /// <list type="bullet">
    ///   <item>‎start &lt; prev‎ ⇒ «کمتر است» (همان هشدارِ قبلی، دست‌نخورده)</item>
    ///   <item>‎start &gt; prev‎ ⇒ «بیشتر زده شده» — <b>تازه، و اختیاری</b></item>
    ///   <item>‎start == prev‎ ⇒ زنجیره سالم است، هیچ نشانه‌ای</item>
    /// </list>
    ///
    /// ⚠️ <b>روز و شبِ یک پایه یک زنجیره‌اند</b>، نه دو تا: شمارندهٔ پایه یکی
    /// است و شیفتِ شب از همان‌جایی شروع می‌شود که روز تمام کرده. پایهٔ دوم
    /// زنجیرهٔ خودش را دارد و هیچ‌وقت با پایهٔ اول سنجیده نمی‌شود — همان
    /// قاعده‌ای که <c>LastBaseAsync</c> و <c>BaseHistoryAsync</c> از قبل
    /// داشتند (کلید <c>PumpNum</c>).
    ///
    /// ⛔ <b>هیچ‌کدام مانع نیست.</b> شمارندهٔ پایه واقعاً عوض می‌شود (تعویضِ
    /// پایه، صفر شدنِ شمارنده)، پس قفل کردنِ کاربر روی دادهٔ درست بدتر از یک
    /// هشدار است.
    ///
    /// ⚠️ و «بیشتر» فقط با <b>شمارهٔ پایهٔ نوشته‌شده</b> سنجیده می‌شود: بی
    /// شماره، <c>LastBaseAsync</c> بزرگ‌ترین ختمِ <b>همهٔ</b> پایه‌ها را
    /// می‌دهد و برابری با آن بی‌معناست — هر پارچهٔ سالمی هشدار می‌گرفت.
    /// </summary>
    private void Apply(ShiftFormViewModel form, decimal start, decimal prev, int num)
    {
        void None() { form.LowBase = false; form.LowBaseText = ""; }

        if (prev <= 0m) { None(); return; }

        if (start < prev)
        {
            form.LowBaseText = "⚠️ این شروع پایه از پایهٔ قبلی (" + Shamsi.Money(prev)
                             + ") کمتر است — ثبت می‌شود، ولی در ورق سرخ می‌ماند.";
            form.LowBase = !form.LowBaseAcked;
            return;
        }

        if (start > prev && ChainCheck && num > 0)
        {
            form.LowBaseText = "⚠️ ختمِ پایهٔ " + Shamsi.Money(num) + " روی "
                             + Shamsi.Money(prev) + " مانده بود؛ این شروع "
                             + Shamsi.Money(start - prev)
                             + " لیتر بیشتر زده شده — بررسی شود. ثبت می‌شود.";
            form.LowBase = !form.LowBaseAcked;
            return;
        }

        None();
    }

    private async Task FillLastBaseAsync(FuelType fuel, int num, ShiftFormViewModel form)
    {
        try
        {
            var v = await _host.ParchaData.LastBaseAsync(fuel, num);
            _lastBase[(fuel, num)] = v;
            if (fuel == Fuel) Apply(form, form.StartValue, v, num);
        }
        catch { /* هشدار رفاه است، نه اصل — نبودش صفحه را نمی‌شکند */ }
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

        // ══ «ده پارچه با همان تاریخ باید هر ده تا در همان ورق بیایند» ══════
        //
        // گزارشِ صاحب ریپو (۱۴۰۵/۰۷/۰۶). ورق از روزِ اول همین کار را می‌کرد —
        // هر پارچه با ‎srcKey‎ِ خودش یک ردیفِ جدا در **همان** ورقِ آن تاریخ —
        // ولی فقط وقتی پارچهٔ تازه‌ای ساخته می‌شد. بی دکمهٔ «پارچهٔ جدید»،
        // ذخیرهٔ دوم همان پارچه را **بازنویسی** می‌کرد و ردیفِ اول از ورق
        // می‌رفت: کاربر ده تا پر می‌کرد و یکی می‌دید.
        //
        // ⛔ و درست هم همین بود: «ذخیره»ی دوباره یعنی **ویرایشِ** همان پارچه،
        // نه پارچهٔ تازه. فرقشان یک چیز است: کارمند و شمارهٔ پایه. اگر عوض
        // شده باشد، این یک پارچهٔ **دیگر** است.
        //
        // ⚠️ این تصمیم عمداً این‌جاست، نه در ‎SaveShiftFlowAsync‎: آن تابع
        // قرارش با نسخهٔ وب است و ‎ShiftParityTests‎ شمارِ پارچه‌هایش را
        // مو‌به‌مو می‌سنجد. این‌جا لایهٔ رابط است — همان جایی که دکمهٔ
        // «پارچهٔ جدید» هم پرچمش را می‌گذارد.
        var force = ForceNew(fuel, kind);
        var autoNew = false;
        if (!force && _current is not null
            && (_current.DateShamsi ?? "").Trim() == (PaDate ?? "").Trim())
        {
            var prev = kind == ShiftKind.Day ? _current.DayShift : _current.NightShift;
            if (prev is not null
                && (((prev.Name ?? "").Trim() != (form.Name ?? "").Trim())
                    || prev.PumpNum != (int)Shamsi.Num(form.PumpNum)))
            { force = true; autoNew = true; }
        }

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
            ForceNew: force,
            LowBase: form.LowBaseUnacked));

        if (!res.Ok) { _host.Toast(res.Error ?? "", ToastKind.Error); return; }

        SetForceNew(fuel, kind, false);
        _current = res.Report;
        if (res.Report is not null) ReportNumText = res.Report.ReportNum.ToString();

        form.Saved = true;
        _host.Toast(fuel == FuelType.Diesel
                        ? "✅ پارچه دیزل ذخیره شد"
                        : "✅ شیفت " + (form.IsDay ? "روز" : "شب") + " ذخیره شد",
                    ToastKind.Ok);

        if (autoNew)
            _host.Toast("🆕 کارمند/شمارهٔ پایه عوض شده بود — پارچهٔ تازه‌ای در "
                        + "همان ورقِ " + PaDate + " باز شد", ToastKind.Info);

        // ⚠️ دو حال دارد (کمتر / بیشتر)، پس پیام نمی‌گوید کدام — همان یک خطِ
        // کنارِ کادر گفته است. پیامِ ثابتِ «کمتر» برای حالتِ «بیشتر» دروغ بود.
        if (form.LowBaseUnacked)
            _host.Toast("🔴 زنجیرهٔ پایه نخواند — ثبت شد و در ورق سرخ ماند",
                        ToastKind.Warn);

        // پایهٔ تازه ⇒ کَشِ «بزرگ‌ترین ختم» کهنه شد
        _lastBase.Remove((fuel, (int)Shamsi.Num(form.PumpNum)));
        _lastBase.Remove((fuel, 0));

        // نسخهٔ وب پس از ذخیرهٔ دیزل فرم را خالی می‌کند، پطرول را نه
        if (fuel == FuelType.Diesel) form.Clear();

        await ReloadLogAsync();
        if (ShowBaseHistory) await ReloadBaseHistoryAsync();
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
