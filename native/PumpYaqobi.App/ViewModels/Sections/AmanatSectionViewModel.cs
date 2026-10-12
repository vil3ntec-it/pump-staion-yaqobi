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
using PumpYaqobi.Domain;

namespace PumpYaqobi.App.ViewModels.Sections;

/// <summary>یک خطِ «برچسب ⇐ عدد» (کادرهای خلاصه، تحلیل، خانه‌های برآورد).</summary>
public sealed record AmanatLine(string Label, string Value, string BrushKey = "Pump.Text", string Sub = "")
{
    public bool HasSub => Sub.Length > 0;
}

/// <summary>یک ردیفِ جدول‌های راهنما (پنج خانه) — ‎table.am-guide‎ی سایت.</summary>
public sealed record AmanatGuideRow(string C1, string C2, string C3, string C4, string C5,
                                    string C5BrushKey = "Pump.Text", bool IsNow = false)
{
    public string RowBrushKey => IsNow ? "Pump.Ok" : "Pump.Text";
}

/// <summary>یک محمولهٔ امانت — یک ردیفِ «📊 جدولِ رسیدها»ی صفحهٔ حساب.</summary>
public sealed partial class AmanatRowViewModel : RowViewModel
{
    private readonly AmanatRow _r;
    private readonly AmanatAccountViewModel _owner;

    public AmanatRowViewModel(AmanatRow r, AmanatAccountViewModel owner)
    {
        _r = r; _owner = owner;
        Loading = true;
        _dateShamsi = r.DateShamsi ?? ""; _name = r.Name ?? ""; _toAccount = r.ToAccount ?? "";
        _liters = r.Liters ?? 0m; _taken = r.Taken ?? 0m;
        _days = r.Days; _temp = r.Temp; _myPct = r.MyPct; _actual = r.Actual;
        _isClosed = r.State == AmanatRowState.Closed;
        _note = r.Note ?? "";
        Loading = false;
    }

    public AmanatRow Entity => _r;

    [ObservableProperty] private string _dateShamsi = "";
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private string _toAccount = "";
    [ObservableProperty] private decimal _liters;
    [ObservableProperty] private decimal _taken;
    //  ⚠️ این چهار تا «خالی» دارند و خالی با صفر یکی نیست (‎_amHas‎ی سایت):
    //  روزِ خالی ⇐ خودکار، گرمای خالی ⇐ پیش‌فرض، فیصدیِ خالی ⇐ ارث از حساب،
    //  موجودیِ خالی ⇐ اندازه نگرفته‌اید (صفر یعنی مخزن خالی است).
    [ObservableProperty] private decimal? _days;
    [ObservableProperty] private decimal? _temp;
    [ObservableProperty] private decimal? _myPct;
    [ObservableProperty] private decimal? _actual;
    [ObservableProperty] private bool _isClosed;
    [ObservableProperty] private string _note = "";

    partial void OnDateShamsiChanged(string v) { Touch(); Refresh(); }
    partial void OnNameChanged(string v) => Touch();
    partial void OnToAccountChanged(string v) => Touch();
    partial void OnLitersChanged(decimal v) { Touch(); Refresh(); }
    partial void OnTakenChanged(decimal v) { Touch(); Refresh(); }
    partial void OnDaysChanged(decimal? v) { Touch(); Refresh(); }
    partial void OnTempChanged(decimal? v) { Touch(); Refresh(); }
    partial void OnMyPctChanged(decimal? v) { Touch(); Refresh(); }
    partial void OnActualChanged(decimal? v) { Touch(); Refresh(); }
    partial void OnIsClosedChanged(bool v) { Touch(); Refresh(); }
    partial void OnNoteChanged(string v) => Touch();

    private static readonly string[] Computed =
    {
        nameof(LitersText), nameof(TakenText), nameof(DaysText), nameof(TempText), nameof(MyPctText),
        nameof(ActualText), nameof(DaysHint), nameof(TempHint), nameof(MyPctHint), nameof(ActualHint),
        nameof(LossText), nameof(LossPctText), nameof(NeedText), nameof(ShareText), nameof(NetText),
        nameof(NetBrushKey), nameof(RestText), nameof(RestBrushKey), nameof(DiffText), nameof(DiffBrushKey),
        nameof(ChipText), nameof(ChipTip), nameof(ChipBrushKey),
    };

    private void Refresh()
    {
        foreach (var n in Computed) OnPropertyChanged(n);
    }

    public void RefreshAll() => Refresh();

    private static string Opt(decimal? v) => v is null ? "" : Shamsi.Money(v.Value);
    private static decimal? OptNum(string? raw) =>
        string.IsNullOrWhiteSpace(Shamsi.ToEnDigits(raw ?? "")) ? null : Shamsi.Num(raw);

    public string LitersText { get => Shown(nameof(LitersText), Shamsi.MoneyOrBlank(Liters)); set { if (!Typed(nameof(LitersText), value)) return; Liters = Shamsi.Num(value); } }
    public string TakenText { get => Shown(nameof(TakenText), Shamsi.MoneyOrBlank(Taken)); set { if (!Typed(nameof(TakenText), value)) return; Taken = Shamsi.Num(value); } }
    public string DaysText { get => Shown(nameof(DaysText), Opt(Days)); set { if (!Typed(nameof(DaysText), value)) return; Days = OptNum(value); } }
    public string TempText { get => Shown(nameof(TempText), Opt(Temp)); set { if (!Typed(nameof(TempText), value)) return; Temp = OptNum(value); } }
    public string MyPctText { get => Shown(nameof(MyPctText), Opt(MyPct)); set { if (!Typed(nameof(MyPctText), value)) return; MyPct = OptNum(value); } }
    public string ActualText { get => Shown(nameof(ActualText), Opt(Actual)); set { if (!Typed(nameof(ActualText), value)) return; Actual = OptNum(value); } }

    private AmanatRowCalc C => _owner.CalcOf(_r);
    private AmanatSettings S => _owner.Settings;

    // ── نشان‌های کم‌رنگِ خانهٔ خالی (‎placeholder‎ی سایت) ──────────────────
    /// <summary>‎n2fa(c.autoDays) + ' روز'‎ — فقط وقتی روز دستی نوشته نشده.</summary>
    public string DaysHint => Days is null ? AmanatService.Fmt(C.AutoDays, 0) + " روز" : "";
    public string TempHint => Temp is null ? AmanatService.Fmt(S.DefTemp) : "";
    /// <summary>خالی ⇐ «↑۳» یعنی از فیصدیِ خودِ حساب آمده، نه نوشتهٔ شما.</summary>
    public string MyPctHint => MyPct is not null ? ""
        : _owner.Entity.MyPct is { } m ? "↑" + AmanatService.Fmt(m, 2) : "ننوشته";
    public string ActualHint => Actual is null ? "ثبت نشده" : "";

    // ── خانه‌های حساب‌شده — همان ‎_amAccPaint‎ ───────────────────────────────
    public string LossText => AmanatService.Fmt2(C.Loss);
    public string LossPctText => AmanatService.Fmt(C.LossPct, 3) + "٪";
    public string NeedText => C.NeedPct is { } n ? AmanatService.Fmt(n, 3) + "٪" : "—";
    public string ShareText => C.TargetL is { } t ? AmanatService.Fmt2(t) : "—";
    public string NetText => C.NetIfMy is { } n ? AmanatService.Fmt2(n) : "—";
    public string NetBrushKey => C.NetIfMy is not { } n ? "Pump.Muted" : n < 0m ? "Pump.Danger" : "Pump.Ok";
    public string RestText => AmanatService.Fmt2(C.Rest);
    public string RestBrushKey => C.Rest < -0.0001m ? "Pump.Danger" : "Pump.Ok";
    public string DiffText => C.HasActual ? AmanatService.Fmt2(C.Diff ?? 0m) : "—";
    public string DiffBrushKey => !C.HasActual ? "Pump.Muted"
        : Math.Abs(C.Diff ?? 0m) < 0.005m ? "Pump.Ok" : (C.Diff ?? 0m) > 0m ? "Pump.Danger" : "Pump.Info";

    // ── چیپِ پیشنهاد — زیرِ خانهٔ «فیصدیِ من» ─────────────────────────────
    private AmanatSuggest Adv
    {
        get
        {
            var c = C;
            return _owner.Calc.Suggest(c.Liters, c.Days, c.Temp, _owner.Entity.Fuel, _r.BasePct, S, c.MyPct);
        }
    }

    /// <summary>‎↪ ۳٫۶٪‎ — همین که تیل نوشته شد؛ بی تیل هیچ.</summary>
    public string ChipText => C.Liters == 0m ? "" : "↪ " + AmanatService.Fmt(Adv.Pct, 1) + "٪";

    /// <summary>سبز = با فیصدیِ فعلی سود دارید، سرخ = زیرِ سربه‌سرید.</summary>
    public string ChipBrushKey
    {
        get
        {
            var cur = C.MyPct;
            if (cur is null) return "Pump.Info";
            var net = Adv.NetAt(cur.Value);
            return net > 0.0001m ? "Pump.Ok" : net < -0.0001m ? "Pump.Danger" : "Pump.Warn";
        }
    }

    public string ChipTip
    {
        get
        {
            if (C.Liters == 0m) return "";
            var adv = Adv;
            var cur = C.MyPct;
            decimal? net = cur is null ? null : adv.NetAt(cur.Value);
            return "زیرِ " + AmanatService.Fmt(adv.EvenPct, 3) + "٪ ضرر است (بخارِ همین ردیف). "
                + (cur is null
                    ? "فیصدیِ این ردیف خالی است، پس با سودِ هدفِ " + AmanatService.Fmt(adv.Want, 2) + "٪ حساب شد: پیشنهاد " + AmanatService.Fmt(adv.Pct, 1) + "٪"
                    : "برای اینکه واقعاً " + AmanatService.Fmt(cur.Value, 2) + "٪ برایتان بماند، " + AmanatService.Fmt(adv.Pct, 1) + "٪ لازم است")
                + (net is null ? ". خانه خالی است."
                    : ". با " + AmanatService.Fmt(cur!.Value, 2) + "٪ فعلی، "
                      + (net >= 0m ? "برایتان می‌ماند: " : "از جیبتان می‌رود: ") + AmanatService.Fmt2(Math.Abs(net.Value)) + " لیتر.")
                + " زدنِ این دکمه فقط عدد را در همین خانه می‌نویسد.";
        }
    }

    /// <summary>‎amUseSuggest‎ — عددِ پیشنهادی در خانهٔ فیصدیِ همین ردیف؛ هیچ‌جا خودکار نوشته نمی‌شود.</summary>
    [RelayCommand]
    private void UseSuggest()
    {
        if (C.Liters == 0m) return;
        MyPct = Adv.Pct;
    }

    protected override void Apply()
    {
        _r.DateShamsi = DateShamsi;
        _r.DateKey = Shamsi.Key(DateShamsi);
        _r.Name = Name;
        _r.ToAccount = ToAccount;
        _r.Liters = Liters == 0m ? null : Liters;
        _r.Taken = Taken == 0m ? null : Taken;
        _r.Days = Days;
        _r.Temp = Temp;
        _r.MyPct = MyPct;
        _r.Actual = Actual;
        _r.State = IsClosed ? AmanatRowState.Closed : AmanatRowState.Open;
        _r.Note = Note;
    }

    protected override Task SaveAsync() => _owner.SaveRowAsync(_r);
}

/// <summary>
/// کارتِ یک حسابِ امانت در شبکهٔ کارت‌ها — مو‌به‌مو همان چیزی که
/// <c>_amCardHtml</c> می‌سازد: نام، «باقیِ مشتری»، خطِ وضعیت، دو عددِ
/// رسید/بخار، خطِ فیصدی و شمارهٔ کارت.
/// </summary>
public sealed class AmanatCardViewModel
{
    public AmanatCardViewModel(AmanatAccount a, AmanatAccountCalc t, int seq)
    {
        Entity = a;
        Name = string.IsNullOrWhiteSpace(a.Name) ? "بی‌نام" : a.Name!.Trim();
        Index = seq;
        IsDiesel = a.Fuel == FuelType.Diesel;
        // عددِ ساده — برچسبش زیرِ همان عدد در کارت نوشته می‌شود (خواستهٔ صاحب ریپو)
        RestText = AmanatService.Fmt2(t.Rest);
        // ‎.pdebt.clear‎ سبز است وقتی چیزی برای مشتری مانده
        RestBrushKey = t.Rest > 0 ? "Pump.Ok" : "Pump.Danger";
        LitersText = (IsDiesel ? "🟤 " : "⛽ ") + AmanatService.Fmt(t.Liters, 0);
        LossText = "💨 " + AmanatService.Fmt2(t.Loss);

        // وضعیتِ کارت روی «هدف» سنجیده می‌شود، نه روی «صفر نشدن»
        if (t.Liters == 0m) { State = "➕ هنوز تیلی ثبت نشده"; StateBrushKey = "Pump.Muted"; }
        else if (t.MyPct is null) { State = "⚠️ فیصدی ثبت نشده"; StateBrushKey = "Pump.Warn"; }
        else if (t.Loss <= 0.0001m) { State = "✅ " + AmanatService.Fmt(t.MyPct.Value, 2) + "٪ کافی است"; StateBrushKey = "Pump.Ok"; }
        else if (t.NetIfMy < 0m) { State = "❌ ضرر — " + AmanatService.Fmt(t.AskPct ?? 0m, 1) + "٪ بگیرید"; StateBrushKey = "Pump.Danger"; }
        else { State = "🔸 " + AmanatService.Fmt2(t.Loss) + " لیتر از سهمتان می‌رود"; StateBrushKey = "Pump.Warn"; }

        PctText = t.MyPct is null
            ? "فیصدی: —"
            : "فیصدیِ شما " + AmanatService.Fmt(t.MyPct.Value, 2) + "٪ → بگیرید " + AmanatService.Fmt(t.AskPct ?? 0m, 1) + "٪";
    }

    public AmanatAccount Entity { get; }
    public string Name { get; }
    public int Index { get; }
    public bool IsDiesel { get; }
    public string RestText { get; }
    public string RestBrushKey { get; }
    public string State { get; }
    public string StateBrushKey { get; }
    public string LitersText { get; }
    public string LossText { get; }
    public string PctText { get; }
}

/// <summary>صفحهٔ یک حسابِ امانت — ‎#amanatAccModal‎ی سایت.</summary>
public sealed partial class AmanatAccountViewModel : ObservableObject, IRowBatchHost
{
    private readonly AppHost _host;
    private readonly AmanatSectionViewModel _section;

    public AmanatAccountViewModel(AppHost host, AmanatAccount a, AmanatSectionViewModel section)
    {
        _host = host; _section = section; Entity = a;
        _name = a.Name ?? "";
        _myPct = a.MyPct ?? 0m;
        _fuelIndex = a.Fuel == FuelType.Diesel ? 1 : 0;

        //  ‎_amSortedRows‎ — به ترتیبِ تاریخ؛ بی‌تاریخ آخر
        var ordered = AmanatService.SortedRows(a.Rows.OrderBy(r => r.SortIndex).ThenBy(r => r.Id));
        foreach (var r in ordered)
        {
            var vm = new AmanatRowViewModel(r, this);
            vm.Recalculated += Recalc;
            Rows.Add(vm);
        }
        Recalc();
    }

    public AmanatAccount Entity { get; }
    public AmanatService Calc => _host.AmanatCalc;
    public AmanatSettings Settings => _section.Settings;
    public AmanatSectionViewModel Section => _section;

    /// <summary>⚠️ ‎BulkRows‎: پر شدنِ جدول یک خبر می‌دهد نه ‎n‎ خبر
    /// — وگرنه جدول به ازای هر ردیف یک‌بار از نو چیده می‌شود و بخش می‌ایستد.</summary>
    public BulkRows<AmanatRowViewModel> Rows { get; } = new();

    public int RowCount => Rows.Count;

    /// <summary>‎Ctrl+عدد‎ / ‎Shift+عدد‎ — افزودن و برداشتنِ گروهیِ ردیف.
    /// حذف فقط وقتی ردیفِ کافی باشد؛ وگرنه هیچ.</summary>
    public async Task AddRowsAsync(int count)
    {
        for (var i = 0; i < count; i++) await AddRowAsync();
    }

    public IReadOnlyList<object> LastRows(int count) =>
        Rows.Skip(Math.Max(0, Rows.Count - count)).Cast<object>().ToList();

    public async Task DeleteRowsAsync(int count)
    {
        if (count < 1 || Rows.Count < count) return;
        for (var i = 0; i < count; i++) await DeleteRowAsync(Rows[^1]);
    }

    [ObservableProperty] private string _name = "";
    [ObservableProperty] private decimal _myPct;
    /// <summary>‎select.am-acc-fuel‎ — ۰ پطرول، ۱ دیزل.</summary>
    [ObservableProperty] private int _fuelIndex;

    /// <summary>
    /// ⚠️ نوارِ «فیصدیِ من (٪) / نرخ هر لیتر» به خواستهٔ صاحب ریپو از صفحهٔ حساب
    /// برداشته شد (سایت) — فقط خودِ کادر. ‎a.myPct‎ سرِ جایش است و ردیفِ بی‌فیصدی
    /// از آن ارث می‌برد (با «↑»).
    /// </summary>
    public string MyPctText { get => Shamsi.MoneyOrBlank(MyPct); set { if (Shamsi.IsReadable(value)) MyPct = Shamsi.Num(value); } }

    public string Title => "🛢️ " + (string.IsNullOrWhiteSpace(Name) ? "حسابِ بی‌نام" : Name.Trim());

    partial void OnNameChanged(string v) { Entity.Name = v; OnPropertyChanged(nameof(Title)); Save(); }

    partial void OnMyPctChanged(decimal v)
    {
        //  ‎amAccounts()‎ی سایت: ردیفِ بسته‌ای که فیصدیِ خودش را ننوشته، با فیصدیِ
        //  پیشینِ حساب قفل می‌شود تا عقب‌گرد نکند. ⚠️ فقط همین‌جا، نه هنگامِ باز شدنِ
        //  بخش — آن‌جا هزاران UPDATE می‌فرستاد (سنجه‌های ‎perf‎ و ‎perf-compare‎).
        var locked = AmanatService.LockClosedRowPcts(Entity);
        if (locked.Count > 0) SaveGuard.Watch(_host.Amanat.SaveRowsAsync(locked), "ردیف‌های امانت");
        Entity.MyPct = v == 0m ? null : v;
        OnPropertyChanged(nameof(MyPctText));
        Save();
        foreach (var r in Rows) r.RefreshAll();
        Recalc();
    }

    /// <summary>‎updateAmanatAccount(ai,'fuel')‎ — بخار و ضریب‌ها همان لحظه با سوختِ تازه.</summary>
    partial void OnFuelIndexChanged(int v)
    {
        var f = v == 1 ? FuelType.Diesel : FuelType.Petrol;
        if (Entity.Fuel == f) return;
        Entity.Fuel = f;
        Save();
        RefreshCalcs();
    }

    private void Save() => SaveGuard.Watch(_host.Amanat.UpdateAccountAsync(Entity), "حسابِ امانت");

    /// <summary>«مدت زمان» اگر دستی نوشته نشده باشد، از تاریخِ ردیف تا امروز.</summary>
    public static decimal AutoDaysOf(AmanatRow r) =>
        AmanatService.AutoDays(ParseDate(r.DateShamsi),
                               r.State == AmanatRowState.Closed ? ParseDate(r.CloseDate) : null,
                               AppClock.Now);

    private static DateTime? ParseDate(string? shamsi)
    {
        var k = Shamsi.Key(shamsi);
        if (k == 0) return null;
        try
        {
            var cal = new System.Globalization.PersianCalendar();
            return cal.ToDateTime(k / 10000, k / 100 % 100, k % 100, 0, 0, 0, 0);
        }
        catch { return null; }
    }

    public AmanatRowCalc CalcOf(AmanatRow r) =>
        _host.AmanatCalc.RowCalc(r, Entity, _section.Settings, AutoDaysOf(r));

    /// <summary>جمعِ سربرگ — کارتِ همین حساب هم از همین می‌خواند.</summary>
    public AmanatAccountCalc Totals { get; private set; }

    /// <summary>تنظیم یا سوخت عوض شد ⇒ همهٔ خانه‌های حساب‌شده و جمع‌ها.</summary>
    public void RefreshCalcs()
    {
        foreach (var r in Rows) r.RefreshAll();
        Recalc();
        RefreshRef();
        if (ShowWhatIf) PaintWhatIf();
    }

    public void Recalc()
    {
        var t = _host.AmanatCalc.AccountCalc(Entity, _section.Settings, AutoDaysOf);
        Totals = t;
        string F2(decimal v) => AmanatService.Fmt2(v);
        string P(decimal v, int d) => AmanatService.Fmt(v, d) + "٪";

        // ══ سربرگِ حساب — کارت‌ها، مثلِ «شرکت‌های تیل» (‎#am-acc-chips‎) ══
        SumLiters = F2(t.Liters) + " لیتر";
        SumTakenSub = t.Taken != 0m ? "برده شده: " + F2(t.Taken) + " لیتر" : "";
        SumLoss = F2(t.Loss) + " لیتر";
        SumLossSub = t.LossMoney is { } lm ? AmanatService.Fmt(lm, 0) + " پول" : "";
        SumMyPct = t.MyPct is { } mp ? P(mp, 2) : "—";
        SumLossPct = P(t.LossPct, 3);
        SumTarget = t.TargetL is { } tl ? F2(tl) : "—";
        SumNet = t.NetIfMy is { } nm ? F2(nm) : "—";
        SumNetBrush = t.NetIfMy is { } n2 && n2 < 0m ? "Pump.Danger" : "Pump.Ok";
        SumRest = F2(t.Rest) + " لیتر";
        SumRestBrush = t.Rest < -0.0001m ? "Pump.Danger" : "Pump.Ok";
        HasActual = t.HasActual;
        SumActual = F2(t.Actual);
        SumRealDiff = F2(t.RealDiff);
        SumRealDiffBrush = Math.Abs(t.RealDiff) < 0.005m ? "Pump.Ok" : "Pump.Danger";

        OnPropertyChanged(nameof(TotalCells));
    }

    [ObservableProperty] private string _sumLiters = "";
    [ObservableProperty] private string _sumTakenSub = "";
    [ObservableProperty] private string _sumLoss = "";
    [ObservableProperty] private string _sumLossSub = "";
    [ObservableProperty] private string _sumMyPct = "";
    [ObservableProperty] private string _sumLossPct = "";
    [ObservableProperty] private string _sumTarget = "";
    [ObservableProperty] private string _sumNet = "";
    [ObservableProperty] private string _sumNetBrush = "Pump.Ok";
    [ObservableProperty] private string _sumRest = "";
    [ObservableProperty] private string _sumRestBrush = "Pump.Ok";
    [ObservableProperty] private bool _hasActual;
    [ObservableProperty] private string _sumActual = "";
    [ObservableProperty] private string _sumRealDiff = "";
    [ObservableProperty] private string _sumRealDiffBrush = "Pump.Ok";

    /// <summary>سربرگ‌های یک‌خطیِ جدول (۱۴۰۵/۰۷/۲۰) — همان ‎AmanatColumns.Short‎ِ PDF.</summary>
    public const string HLiters = "رسید (لیتر)";
    public const string HTaken = "برده شده (لیتر)";
    public const string HLoss = "بخار (لیتر)";
    public const string HLossPct = "فیصدیِ بخار";
    public const string HNeed = "فیصدیِ لازم";
    public const string HShare = "سهمیهٔ من (لیتر)";
    public const string HNet = "به من می‌رسد (لیتر)";
    public const string HRest = "الباقیِ طرف (لیتر)";
    public const string HActual = "موجودیِ واقعی";
    public const string HDiff = "اختلاف";

    /// <summary>
    /// ردیفِ «جمله»ی ته جدول — همتای ‎&lt;tfoot&gt;‎ی سایت (‎am-ft-*‎).
    /// ⚠️ حساب‌ها با هم جمع نمی‌شوند: هر حساب «جمله»ی خودش را دارد.
    /// </summary>
    public IReadOnlyList<TotalCell> TotalCells
    {
        get
        {
            var t = Totals;
            var list = new List<TotalCell>
            {
                new("رسید تیل", AmanatService.Fmt2(t.Liters), null, HLiters),
                new("برده شده", AmanatService.Fmt2(t.Taken), null, HTaken),
                new("تیلِ بخار", AmanatService.Fmt2(t.Loss), "Pump.Warn", HLoss),
                new("فیصدیِ بخار", AmanatService.Fmt(t.LossPct, 3) + "٪", "Pump.Warn", HLossPct),
                new("فیصدیِ لازم", t.NeedPct is { } n ? AmanatService.Fmt(n, 3) + "٪" : "—", "Pump.Info", HNeed),
                new("سهمیهٔ من", t.Share != 0m ? AmanatService.Fmt2(t.Share) : "—", "Pump.Info", HShare),
                new("به من می‌رسد", t.NetIfMy is { } m ? AmanatService.Fmt2(m) : "—", null, HNet),
                new("الباقیِ طرف", AmanatService.Fmt2(t.Rest), "Pump.Ok", HRest),
            };
            if (_section.ShowMeasure)
            {
                list.Add(new("موجودی واقعی", t.HasActual ? AmanatService.Fmt2(t.Actual) : "—", null, HActual));
                list.Add(new("اختلاف", t.HasActual ? AmanatService.Fmt2(t.RealDiff) : "—", null, HDiff));
            }
            return list;
        }
    }

    public void MeasureChanged() => OnPropertyChanged(nameof(TotalCells));

    public async Task SaveRowAsync(AmanatRow r)
    {
        await _host.Amanat.SaveRowAsync(r);
        Recalc();
    }

    /// <summary>
    /// بستهٔ آمادهٔ ورق — ردیف‌ها به ترتیبِ تاریخ (‎_amSortedRows‎).
    /// «مدت زمانِ خودکار» تا امروز فقط این‌جا معنی دارد و نباید در لایهٔ سند
    /// دوباره نوشته شود.
    /// </summary>
    public AmanatReportAccount ReportAccount() => new(
        Entity.Name ?? "",
        Entity.Fuel == FuelType.Diesel ? "دیزل" : "پطرول",
        Totals,
        AmanatService.SortedRows(Rows.Select(r => r.Entity))
            .Select(r => new AmanatReportRow(r, CalcOf(r))).ToList());

    /// <summary>‎printAmanatAccount(i)‎ — ورقِ همین یک حساب.</summary>
    [RelayCommand]
    private Task PdfAsync()
    {
        var name = string.IsNullOrWhiteSpace(Entity.Name) ? "بی‌نام" : Entity.Name!.Trim();
        var input = new AmanatReportInput(
            "تیل امانت — " + name, _section.Settings,
            new[] { ReportAccount() }, DocDates.Line(), ShowAccountHeads: false);
        return Documents.ShowAsync(() => new AmanatReport(input), "تیل امانت — " + name);
    }

    [RelayCommand]
    private async Task AddRowAsync()
    {
        var r = new AmanatRow
        {
            AccountId = Entity.Id,
            SortIndex = Entity.Rows.Count == 0 ? 0 : Entity.Rows.Max(x => x.SortIndex) + 1,
            DateShamsi = Shamsi.Today(),
            DateKey = Shamsi.Key(Shamsi.Today()),
        };
        await _host.Amanat.SaveRowAsync(r);
        Entity.Rows.Add(r);
        var vm = new AmanatRowViewModel(r, this);
        vm.Recalculated += Recalc;
        Rows.Add(vm);
        Recalc();
    }

    /// <summary>‎deleteAmanatRow‎ — به سطل؛ اگر حساب بی‌ردیف ماند، یک ردیفِ خالیِ تازه.</summary>
    [RelayCommand]
    private async Task DeleteRowAsync(AmanatRowViewModel? row)
    {
        if (row is null) return;
        await row.RetireWhileAsync(() => _host.Amanat.DeleteRowAsync(row.Entity.Id));
        Entity.Rows.Remove(row.Entity);
        Rows.Remove(row);
        if (Rows.Count == 0) await AddRowAsync();
        Recalc();
    }

    [RelayCommand]
    private Task DeleteAccountAsync() => _section.ConfirmDeleteAsync(this);

    /// <summary>«← بازگشت» به شبکهٔ کارت‌ها.</summary>
    [RelayCommand]
    private void Back() => _section.BackCommand.Execute(null);

    // ══ کشوهای صفحهٔ حساب — هر سه پیش‌فرض بسته (◂) ═════════════════════════
    [ObservableProperty] private bool _showWhatIf;
    [ObservableProperty] private bool _showRef;
    [ObservableProperty] private bool _showDanger;

    public string WhatIfCaret => ShowWhatIf ? "▾" : "◂";
    public string RefCaret => ShowRef ? "▾" : "◂";
    public string DangerCaret => ShowDanger ? "▾" : "◂";

    partial void OnShowWhatIfChanged(bool v) { OnPropertyChanged(nameof(WhatIfCaret)); if (v) PaintWhatIf(); }
    partial void OnShowRefChanged(bool v) { OnPropertyChanged(nameof(RefCaret)); if (v) RefreshRef(); }
    partial void OnShowDangerChanged(bool v) => OnPropertyChanged(nameof(DangerCaret));

    [RelayCommand] private void ToggleWhatIf() => ShowWhatIf = !ShowWhatIf;
    [RelayCommand] private void ToggleRef() => ShowRef = !ShowRef;
    [RelayCommand] private void ToggleDanger() => ShowDanger = !ShowDanger;

    // ══ 🌡️ در هر گرما چقدر تبخیر می‌شود؟ — ‎_amRefTableHtml(a)‎ ═══════════════
    public BulkRows<AmanatTempRowViewModel> RefRows { get; } = new();
    [ObservableProperty] private string _refNote = "";

    private void RefreshRef()
    {
        if (!ShowRef) return;
        RefRows.ResetTo(AmanatSettingsEditorViewModel.TempTable(Calc, Settings, Entity.Fuel));
        RefNote = AmanatSettingsEditorViewModel.TempTableNote(Settings, Entity.Fuel);
    }

    // ══ ⚠️ اگر قیمت بالا برود — تیلِ امانتیِ فروخته‌شده (۱۴۰۵/۰۷/۲۰) ══════════
    //  صاحب ریپو: «یارو ۱۰۰٬۰۰۰ تیل داده بود، نزدیکِ ۵۰٬۰۰۰ را به ۶۵ فروختم، حالا
    //  ۸۵ شده و تیلش را می‌خواهد… غرق شدم. شفاف بگو که اگر قیمت به این‌جا برسد راهِ
    //  برگشتی نیست، ساده و بی شلوغی.» یک دفترِ پیش‌نویس: نه از جدول می‌خواند، نه
    //  ذخیره می‌شود. حساب در ‎AmanatPriceRisk.Of‎ است.
    [ObservableProperty] private string _rkSold = "";
    [ObservableProperty] private string _rkSalePrice = "";
    [ObservableProperty] private string _rkNowPrice = "";
    [ObservableProperty] private bool _rkHas;
    [ObservableProperty] private IReadOnlyList<AmanatLine> _rkLines = Array.Empty<AmanatLine>();
    [ObservableProperty] private string _rkVerdict = "";
    [ObservableProperty] private string _rkVerdictBrush = "Pump.Text";

    partial void OnRkSoldChanged(string v) => PaintRisk();
    partial void OnRkSalePriceChanged(string v) => PaintRisk();
    partial void OnRkNowPriceChanged(string v) => PaintRisk();

    private void PaintRisk()
    {
        var sold = Math.Max(0m, Has(RkSold) ?? 0m);
        var sale = Math.Max(0m, Has(RkSalePrice) ?? 0m);
        var now = Math.Max(0m, Has(RkNowPrice) ?? 0m);
        RkHas = sold > 0m && sale > 0m && now > 0m;
        if (!RkHas) { RkLines = Array.Empty<AmanatLine>(); RkVerdict = ""; return; }

        var r = AmanatPriceRisk.Of(sold, sale, now);
        static string M(decimal v) => Shamsi.Money(Math.Round(v, 0, MidpointRounding.AwayFromZero)) + " افغانی";
        RkLines = new[]
        {
            new AmanatLine("پولی که از فروش گرفتید", M(r.Got), "Pump.Ok"),
            new AmanatLine("برای پس دادنِ همان تیل امروز باید بدهید", M(r.NeedNow), "Pump.Info"),
            new AmanatLine(r.Loss >= 0m ? "ضرر" : "سود", M(Math.Abs(r.Loss)), r.Loss > 0m ? "Pump.Danger" : "Pump.Ok"),
            new AmanatLine("هر ۱ افغانی بالا رفتنِ قیمت", M(r.PerAfghani) + " ضررِ بیشتر", "Pump.Danger"),
        };
        (RkVerdict, RkVerdictBrush) = r.Loss > 0m
            ? ($"⛔ {Shamsi.Money(sold)} لیتر را به {Shamsi.Money(sale)} فروختید؛ حالا قیمت {Shamsi.Money(now)} است. "
               + $"برای پس دادن {M(r.Loss)} از جیبِ خودتان می‌رود — این پول برنمی‌گردد.", "Pump.Danger")
            : r.Loss < 0m
                ? ("✅ قیمت پایین آمده؛ پس دادنِ تیل ارزان‌تر از پولی است که گرفتید.", "Pump.Ok")
                : ("قیمت همان است؛ نه ضرر، نه سود.", "Pump.Text");
    }

    public const string RiskRule =
        "قاعدهٔ ساده: تیلِ امانتی مالِ شما نیست. اگر آن را بفروشید، پولش را کنار بگذارید یا همان روز همان‌قدر تیل بخرید — "
        + "وگرنه هر افغانی که قیمت بالا برود، ضربِ لیترهای فروخته‌شده از جیبِ شما می‌رود و راهِ برگشت نیست.";

    // ══ 🧾 برآوردِ دستی — «قبول کنم؟ چند فیصد بگیرم؟» ════════════════════════
    //  یک دفترِ پیش‌نویس: نه از جدول می‌خواند، نه در آن می‌نویسد، نه ذخیره می‌شود.
    //  فقط «سودِ هدف» در تنظیمات می‌نشیند (همان ‎amSetProfitPct‎).
    private bool _wiSeeded;
    [ObservableProperty] private string _wiLiters = "";
    [ObservableProperty] private string _wiDays = "30";
    [ObservableProperty] private string _wiTemp = "";
    [ObservableProperty] private string _wiMyPct = "";
    [ObservableProperty] private string _wiRate = "";
    [ObservableProperty] private string _wiProfit = "";
    [ObservableProperty] private bool _wiHasLiters;
    [ObservableProperty] private string _wiEmptyNote = "مقدارِ تیلِ طرف را بنویسید تا بگوید قبول کنید یا نه.";
    [ObservableProperty] private IReadOnlyList<AmanatLine> _wiCells = Array.Empty<AmanatLine>();
    [ObservableProperty] private string _wiVerdict = "";
    [ObservableProperty] private string _wiVerdictBrush = "Pump.Text";
    [ObservableProperty] private IReadOnlyList<AmanatGuideRow> _wiFuture = Array.Empty<AmanatGuideRow>();
    [ObservableProperty] private bool _wiHasAnalysis;
    [ObservableProperty] private IReadOnlyList<AmanatLine> _wiAnalysis = Array.Empty<AmanatLine>();
    [ObservableProperty] private string _wiAnalysisVerdict = "";
    [ObservableProperty] private string _wiAnalysisBrush = "Pump.Text";
    [ObservableProperty] private string _wiThermal = "";

    public const string WiFutureNote =
        "ردیفِ نشان‌دار همان چیزی است که بالا نوشته‌اید؛ ردیف‌های بعدی می‌گویند «اگر از دستتان در رفت و بیشتر ماند، چه». "
        + "این‌ها فقط راهنمایی‌اند — هر فیصدی که خودتان بخواهید همان می‌ماند و برنامه چیزی را عوض نمی‌کند."
        + " برای نوشتنِ پیشنهاد روی یک ردیفِ جدول، دکمهٔ ریزِ کنارِ خانهٔ «فیصدیِ من»ِ همان ردیف را بزنید.";

    private bool _wiFilling;

    /// <summary>‎_amWhatIfSeed‎ — روز ۳۰، گرمای پیش‌فرض، فیصدی و نرخِ خودِ حساب.</summary>
    private void SeedWhatIf()
    {
        _wiFilling = true;
        try
        {
            WiLiters = "";
            WiDays = "30";
            WiTemp = AmanatSettingsEditorViewModel.N(Settings.DefTemp);
            WiMyPct = Entity.MyPct is { } m ? AmanatSettingsEditorViewModel.N(m) : "";
            WiRate = Entity.Rate is { } r ? AmanatSettingsEditorViewModel.N(r) : "";
            WiProfit = AmanatSettingsEditorViewModel.N(Settings.ProfitPct);
        }
        finally { _wiFilling = false; }
        _wiSeeded = true;
    }

    partial void OnWiLitersChanged(string v) => PaintWhatIf();
    partial void OnWiDaysChanged(string v) => PaintWhatIf();
    partial void OnWiTempChanged(string v) => PaintWhatIf();
    partial void OnWiMyPctChanged(string v) => PaintWhatIf();
    partial void OnWiRateChanged(string v) => PaintWhatIf();

    /// <summary>‎amSetProfitPct‎ — خالی یا خراب ⇐ ۲؛ منفی ⇐ ۰. در تنظیمات ذخیره می‌شود.</summary>
    partial void OnWiProfitChanged(string v)
    {
        if (_wiFilling) return;
        var raw = Shamsi.ToEnDigits(v ?? "").Trim();
        var n = AmanatSettingsEditorViewModel.Num(raw, decimal.MinValue);
        var p = raw.Length == 0 || n == decimal.MinValue ? AmanatSettings.Default.ProfitPct : Math.Max(0m, n);
        if (p != Settings.ProfitPct) _section.ApplySettings(Settings with { ProfitPct = p });
        PaintWhatIf();
    }

    /// <summary>«↺ پاک کردن».</summary>
    [RelayCommand]
    private void ResetWhatIf()
    {
        SeedWhatIf();
        PaintWhatIf();
    }

    private static decimal? Has(string? v)
    {
        var t = Shamsi.ToEnDigits(v ?? "").Trim();
        if (t.Length == 0) return null;
        var n = AmanatSettingsEditorViewModel.Num(t, decimal.MinValue);
        return n == decimal.MinValue ? null : n;
    }

    /// <summary>‎_amWhatIfPaint‎ + ‎_amAnalysisHtml‎.</summary>
    public void PaintWhatIf()
    {
        if (_wiFilling) return;
        if (!_wiSeeded) SeedWhatIf();
        var s = Settings;
        var l = Math.Max(0m, Has(WiLiters) ?? 0m);
        WiHasLiters = l > 0m;
        if (l == 0m)
        {
            WiCells = Array.Empty<AmanatLine>(); WiFuture = Array.Empty<AmanatGuideRow>();
            WiVerdict = ""; WiHasAnalysis = false; WiAnalysis = Array.Empty<AmanatLine>();
            WiAnalysisVerdict = ""; WiThermal = "";
            return;
        }
        var w = Calc.WhatIf(l, Has(WiDays) ?? 0m, Has(WiTemp), Entity.Fuel, Has(WiMyPct), Has(WiRate), s);
        string F2(decimal v) => AmanatService.Fmt2(v);
        string L(decimal v) => F2(v) + " لیتر";
        string Money(decimal v) => w.Rate is { } r ? " (" + AmanatService.Fmt(Math.Abs(v) * r, 0) + " پول)" : "";

        var cells = new List<AmanatLine>
        {
            new("🛢️ تیلِ بخار", L(w.Loss), "Pump.Warn"),
            new("٪ فیصدیِ بخار", AmanatService.Fmt(w.LossPct, 3) + "٪", "Pump.Warn"),
            new("⛔ سربه‌سر — زیرش ضرر", AmanatService.Fmt(w.Suggest.EvenPct, 3) + "٪", "Pump.Danger"),
            new("✅ پیشنهاد", AmanatService.Fmt(w.Suggest.Pct, 1) + "٪", "Pump.Ok"),
        };
        if (w.Share is { } sh) cells.Add(new("سهمیهٔ من", L(sh), "Pump.Info"));
        if (w.Net is { } nt) cells.Add(new("به من می‌رسد", L(nt), nt < 0m ? "Pump.Danger" : "Pump.Ok"));
        cells.Add(new("الباقیِ طرف", L(w.Rest), "Pump.Ok"));
        if (w.NeedPct is { } np) cells.Add(new("٪ فیصدیِ لازم", AmanatService.Fmt(np, 3) + "٪", "Pump.Info"));
        WiCells = cells;

        if (w.MyPct is null)
        {
            var at = w.Suggest.NetAt(w.Suggest.Pct);
            WiVerdict = "«فیصدیِ من» را هم بنویسید تا بگوید با آن فیصدی سود می‌کنید یا ضرر."
                + " با این تیل و این شرایط، دستِ‌کم " + AmanatService.Fmt(w.Suggest.EvenPct, 3)
                + "٪ لازم است تا فقط سربه‌سر شوید؛ با سودِ هدفِ " + AmanatService.Fmt(w.Suggest.Want, 2)
                + "٪ (کادرِ بالا)، پیشنهاد " + AmanatService.Fmt(w.Suggest.Pct, 1)
                + "٪ می‌شود و آن‌وقت " + L(at) + Money(at) + " برایتان می‌ماند.";
            WiVerdictBrush = "Pump.Muted";
        }
        else if (w.Net > 0.0001m)
        {
            WiVerdict = "✅ قبول کنید — با " + AmanatService.Fmt(w.MyPct.Value, 2) + "٪، بعد از بخار "
                + L(w.Net!.Value) + " برایتان می‌ماند" + Money(w.Net.Value) + ".";
            WiVerdictBrush = "Pump.Ok";
        }
        else if (w.Net < -0.0001m)
        {
            WiVerdict = "⛔ ضرر می‌کنید — با " + AmanatService.Fmt(w.MyPct.Value, 2) + "٪، بخار ("
                + L(w.Loss) + ") از سهمِ شما بیشتر است و " + L(Math.Abs(w.Net!.Value))
                + " از جیبتان می‌رود" + Money(w.Net.Value) + ".\nفیصدی را دستِ‌کم به "
                + AmanatService.Fmt(w.AskPct ?? 0m, 1) + "٪ ببرید تا ضرر نکنید.";
            WiVerdictBrush = "Pump.Danger";
        }
        else
        {
            WiVerdict = "➖ سربه‌سر — با " + AmanatService.Fmt(w.MyPct.Value, 2)
                + "٪ نه سود می‌کنید نه ضرر. برای سود، بالاتر از " + AmanatService.Fmt(w.AskPct ?? 0m, 1) + "٪ بگیرید.";
            WiVerdictBrush = "Pump.Warn";
        }

        WiFuture = w.Future.Select(f => new AmanatGuideRow(
            AmanatService.Fmt(Math.Round(f.Days, MidpointRounding.AwayFromZero), 0) + " روز" + (f.IsNow ? " ← همین که نوشتید" : ""),
            L(f.Loss),
            AmanatService.Fmt(f.EvenPct, 3) + "٪",
            AmanatService.Fmt(f.SuggestPct, 1) + "٪",
            f.Net is { } n2 ? L(n2) : "—",
            f.Net is not { } n3 ? "Pump.Muted" : n3 < 0m ? "Pump.Danger" : "Pump.Ok",
            f.IsNow)).ToList();

        PaintAnalysis(w, s, L, Money);
    }

    /// <summary>‎_amAnalysisHtml‎ — «🧮 تحلیل — چند فیصد بگیرم که ضرر نکنم؟»</summary>
    private void PaintAnalysis(AmanatWhatIf w, AmanatSettings s, Func<decimal, string> L, Func<decimal, string> money)
    {
        WiHasAnalysis = true;
        var rows = new List<AmanatLine>
        {
            new("تیلی که می‌گیرید", L(w.Liters)),
            new("بخارِ تخمینی", L(w.Loss) + " · " + AmanatService.Fmt(w.LossPct, 3) + "٪" + (w.MyPct is null ? "" : money(w.Loss)), "Pump.Warn"),
        };
        if (w.MyPct is not { } my)
        {
            WiAnalysis = rows;
            WiAnalysisVerdict = "بالا در «٪ فیصدیِ من» بنویسید چقدر می‌خواهید برایتان بماند — مثلاً ۳٪. "
                + "بعد خودش می‌گوید برای رسیدن به همان، چند فیصد باید بگیرید.";
            WiAnalysisBrush = "Pump.Muted";
            WiThermal = "";
            return;
        }
        var enough = w.Net >= -0.0001m;
        rows.Add(new("الباقیِ طرف (تیل − سهمِ شما)", L(w.Rest), "Pump.Ok"));
        rows.Add(new("فیصدیِ شما (هدف) " + AmanatService.Fmt(my, 2) + "٪", L(w.Share ?? 0m) + money(w.Share ?? 0m)));
        rows.Add(new("اگر همین " + AmanatService.Fmt(my, 2) + "٪ را بگیرید، بعد از بخار می‌ماند",
                     L(w.Net ?? 0m), enough ? "Pump.Ok" : "Pump.Danger"));
        rows.Add(new("فیصدیِ لازم (" + AmanatService.Fmt(my, 2) + "٪ + " + AmanatService.Fmt(w.LossPct, 3) + "٪ تبخیر"
                     + (s.HandlingPct != 0m ? " + " + AmanatService.Fmt(s.HandlingPct, 2) + "٪ اندازه‌گیری" : "") + ")",
                     AmanatService.Fmt(w.NeedPct ?? 0m, 3) + "٪", "Pump.Info"));
        rows.Add(new("گِردشده — همین را بگیرید", AmanatService.Fmt(w.AskPct ?? 0m, 1) + "٪", "Pump.Ok"));
        rows.Add(new("با " + AmanatService.Fmt(w.AskPct ?? 0m, 1) + "٪ می‌گیرید", L(w.AskL ?? 0m) + money(w.AskL ?? 0m)));
        rows.Add(new("و بعد از بخار برایتان می‌ماند", L(w.NetIfAsk ?? 0m) + money(w.NetIfAsk ?? 0m), "Pump.Ok"));
        WiAnalysis = rows;

        WiAnalysisVerdict = enough
            ? "✅ با " + AmanatService.Fmt(my, 2) + "٪ هم ضرر نمی‌کنید، ولی " + L(w.Loss)
              + " از سهمتان را بخار می‌خورد و فقط " + L(w.Net ?? 0m) + " برایتان می‌ماند.\n"
              + "↩️ اگر " + AmanatService.Fmt(w.AskPct ?? 0m, 1) + "٪ بگیرید، کلِ " + L(w.Share ?? 0m)
              + " که می‌خواستید سالم برایتان می‌ماند (در واقع " + L(w.NetIfAsk ?? 0m) + ")."
            : "❌ " + L(w.Share ?? 0m) + " که می‌خواستید به شما نمی‌رسد — بخار " + L(w.Loss)
              + " است و فقط " + L(w.Net ?? 0m) + " می‌ماند" + money(w.Net ?? 0m) + ".\n"
              + "↩️ فیصدی را ببرید به " + AmanatService.Fmt(w.AskPct ?? 0m, 1) + "٪ تا کلِ " + L(w.Share ?? 0m) + " برایتان بماند.";
        WiAnalysisBrush = enough ? "Pump.Ok" : "Pump.Danger";

        var th = AmanatService.Fmt(Math.Abs(Calc.Thermal(100m, w.Temp, Entity.Fuel, s).Pct), 2);
        WiThermal = "🌡️ جدا از بخار: با گرمای " + AmanatService.Fmt(w.Temp) + "°C خودِ عددِ لیتر حدود "
            + th + "٪ با دمای مرجع (۱۵٫۶°C) فرق می‌کند. "
            + "این بخار نیست و با سرد شدنِ هوا برمی‌گردد — رویش فیصدی نگیرید.";
    }
}

/// <summary>
/// ══ بخشِ تیل امانت ══════════════════════════════════════════════════════════
/// همان سه صفحهٔ نسخهٔ وب: شبکهٔ کارت‌ها (‎#sec-amanat‎)، «📊 خلاصه و ابزارها»
/// (‎#amanatToolsModal‎) و صفحهٔ یک حساب (‎#amanatAccModal‎). بخار، سهم و «فیصدیِ
/// لازم» از سرویسی می‌آیند که با ۴۰۰ ردیفِ گرفته‌شده از خودِ نسخهٔ وب آزموده شده.
/// </summary>
public sealed partial class AmanatSectionViewModel : SectionViewModel
{
    private const string MeasureKey = "amanat.measure";
    private readonly AppHost _host;
    private List<AmanatAccountViewModel> _all = new();

    public AmanatSectionViewModel(AppHost host) : base("amanat", "amanat", "تیل امانت")
    {
        // ↓ درِ «🕘 تاریخچه»ی همین بخش — شرحش بالای ‎SectionViewModel.HistoryKind‎
        HistoryKind = "amanat";

        // «📝 یادداشت این بخش» — همتای ‎.sec-note-box‎ی سایت. کلیدش همان
        // کلیدِ نسخهٔ وب است تا نوت‌های واردشده سرِ جای خودشان بنشینند.
        Notes = new SectionNotesViewModel(Id, host.SectionNotes,
            (m, ok) => host.Toast(m, ok ? ToastKind.Ok : ToastKind.Warn));
        _host = host;
        Settings = host.Amanat.Settings();
        SettingsEditor = new AmanatSettingsEditorViewModel(host.AmanatCalc, host.Amanat.SaveSettings, RefreshAsync);
        SettingsEditor.Fill(Settings);
        try { _showMeasure = host.Settings.GetString(MeasureKey) == "1"; } catch { _showMeasure = false; }
        _feeTemp = "";
    }

    public AmanatSettings Settings { get; private set; }

    /// <summary>«⚙️ تنظیمات مدیر» — ‎toggleAmanatSettings‎؛ داخلِ «خلاصه و ابزارها».</summary>
    public AmanatSettingsEditorViewModel SettingsEditor { get; }
    [ObservableProperty] private bool _showSettings;

    [RelayCommand]
    private void ToggleSettings()
    {
        ShowSettings = !ShowSettings;
        if (ShowSettings) SettingsEditor.Fill(_host.Amanat.Settings());
    }

    /// <summary>‎amSetProfitPct‎ و هر تغییرِ تنظیمی که از درونِ صفحهٔ حساب می‌آید — بی ساختنِ دوبارهٔ حساب‌ها.</summary>
    public void ApplySettings(AmanatSettings s)
    {
        Settings = s;
        _host.Amanat.SaveSettings(s);
        SettingsEditor.Fill(s);
        foreach (var a in _all) { if (!ReferenceEquals(a, Page)) a.Recalc(); }
        Page?.RefreshCalcs();
        ApplyFilter();
    }

    /// <summary>کارت‌های دیده‌شده — پس از جست‌وجو و فیلترِ سوخت.</summary>
    public ObservableCollection<AmanatCardViewModel> Cards { get; } = new();

    [ObservableProperty] private string _search = "";
    /// <summary>‎_amFuelFilter‎ — «all» / «petrol» / «diesel».</summary>
    [ObservableProperty] private string _fuelFilter = "all";
    [ObservableProperty] private AmanatAccountViewModel? _page;
    [ObservableProperty] private string _emptyText = "";
    /// <summary>صفحهٔ «📊 خلاصه و ابزارها» باز است؟ (‎#amanatToolsModal.open‎)</summary>
    [ObservableProperty] private bool _showTools;
    /// <summary>‎_amShowMeasure‎ — ستون‌های «موجودی واقعی» و «اختلاف»؛ پیش‌فرض خاموش.</summary>
    [ObservableProperty] private bool _showMeasure;

    public bool IsListVisible => Page is null && !ShowTools;
    public bool IsToolsVisible => Page is null && ShowTools;

    /// <summary>حسابِ امانت — تا باز است، میانبرهای ردیف به آن می‌روند نه به فهرست.</summary>
    public override object? ActivePage => Page;

    /// <summary>دفتر عوض شد ⇒ حسابِ بازِ امانتِ دفترِ قبلی بسته شود.</summary>
    public override void CloseOpenPage() { Page = null; ShowTools = false; }
    public bool IsAll => FuelFilter == "all";
    public bool IsPetrol => FuelFilter == "petrol";
    public bool IsDieselFilter => FuelFilter == "diesel";
    /// <summary>نشانِ «فقط پطرول/دیزل» — تا کاربر نگوید «حسابم کجاست؟».</summary>
    public bool HasFuelChip => FuelFilter != "all";
    public string FuelChipText => FuelFilter == "diesel" ? "🟤 فقط دیزل ✕" : "⛽ فقط پطرول ✕";

    partial void OnPageChanged(AmanatAccountViewModel? v)
    {
        OnPropertyChanged(nameof(IsListVisible));
        OnPropertyChanged(nameof(IsToolsVisible));
        // صفحهٔ حساب تمام‌عرض است، مثلِ مودالِ تمام‌صفحهٔ نسخهٔ وب
        IsPageOpen = v is not null || ShowTools;
    }

    partial void OnShowToolsChanged(bool v)
    {
        OnPropertyChanged(nameof(IsListVisible));
        OnPropertyChanged(nameof(IsToolsVisible));
        IsPageOpen = v || Page is not null;
    }

    partial void OnShowMeasureChanged(bool v)
    {
        try { _host.Settings.Set(MeasureKey, v ? "1" : "0"); } catch { }
        Page?.MeasureChanged();
    }

    /// <summary>‎toggleAmanatMeasure‎ — «📏 اندازه‌گیری».</summary>
    [RelayCommand]
    private void ToggleMeasure()
    {
        ShowMeasure = !ShowMeasure;
        _host.Toast(ShowMeasure ? "📏 ستون‌های اندازه‌گیری آمد" : "📏 ستون‌های اندازه‌گیری رفت", ToastKind.Info);
    }

    partial void OnSearchChanged(string v) => ApplyFilter();

    partial void OnFuelFilterChanged(string v)
    {
        foreach (var n in new[] { nameof(IsAll), nameof(IsPetrol), nameof(IsDieselFilter),
                                  nameof(HasFuelChip), nameof(FuelChipText) })
            OnPropertyChanged(n);
        ApplyFilter();
    }

    [RelayCommand] private void SetFuel(string f) => FuelFilter = f is "petrol" or "diesel" ? f : "all";
    [RelayCommand] private void ClearFuel() => FuelFilter = "all";

    protected override Task LoadAsync() => RefreshAsync();

    public async Task RefreshAsync()
    {
        Settings = _host.Amanat.Settings();
        if (Settings != SettingsEditor.Current) SettingsEditor.Fill(Settings);
        var list = new List<AmanatAccountViewModel>();
        foreach (var fuel in new[] { FuelType.Petrol, FuelType.Diesel })
            foreach (var a in await _host.Amanat.ListAsync(fuel))
                list.Add(new AmanatAccountViewModel(_host, a, this));
        //  ترتیبِ سایت: ترتیبِ ساخته شدن (آرایهٔ ‎DB.amanatAccounts‎)
        _all = list.OrderBy(a => a.Entity.Id).ToList();
        var open = Page?.Entity.Id;
        if (open is { } id) Page = _all.FirstOrDefault(a => a.Entity.Id == id);
        ApplyFilter();
    }

    private bool Match(AmanatAccount a) => AmanatService.Matches(a, FuelFilter, Search);

    private void ApplyFilter()
    {
        var q = Search.Trim();
        Cards.Clear();
        var seq = 0;
        foreach (var a in _all)
        {
            if (!Match(a.Entity)) continue;
            Cards.Add(new AmanatCardViewModel(a.Entity, a.Totals, ++seq));
        }

        EmptyText = q.Length > 0 ? $"با «{q}» حسابی پیدا نشد."
            : FuelFilter != "all"
                ? "برای " + (FuelFilter == "diesel" ? "دیزل" : "پطرول") + " حسابی نیست — دکمهٔ «➕ حساب جدید» را بزنید."
                : "هنوز حسابی ثبت نشده — دکمهٔ «➕ حساب جدید» را بزنید.";
        if (ShowTools) PaintTools();
    }

    [RelayCommand]
    private void Open(AmanatCardViewModel? card)
    {
        if (card is null) return;
        Page = _all.FirstOrDefault(a => a.Entity.Id == card.Entity.Id);
    }

    public override async Task AfterUndoAsync()
    {
        var id = Page?.Entity.Id;
        await RefreshAsync();
        if (id is { } i) Page = _all.FirstOrDefault(a => a.Entity.Id == i);
    }

    [RelayCommand]
    private void Back()
    {
        Page = null;
        ApplyFilter();   // عددهای کارت با آنچه در صفحهٔ حساب عوض شد جور شود
    }

    /// <summary>
    /// ‎printAmanat()‎ — ورقِ همان حساب‌هایی که روی صفحه دیده می‌شوند (فیلترِ
    /// سوخت و جست‌وجو هر دو اثر دارند).
    ///
    /// ⚠️ حساب‌ها با هم جمع نمی‌شوند: هر حساب جدول و ردیفِ «جمله»ی خودش را
    /// دارد. جمع زدنشان عددِ بی‌معنی می‌داد.
    /// </summary>
    [RelayCommand]
    private Task PdfAsync()
    {
        var ids = Cards.Select(c => c.Entity.Id).ToList();
        var list = ids.Select(id => _all.FirstOrDefault(a => a.Entity.Id == id))
                      .Where(a => a is not null)
                      .Select(a => a!.ReportAccount())
                      .ToList();

        var title = "تیل امانت" + FuelFilter switch
        {
            "petrol" => " — پطرول",
            "diesel" => " — دیزل",
            _ => "",
        };

        var input = new AmanatReportInput(title, Settings, list, DocDates.Line());
        return Documents.ShowAsync(() => new AmanatReport(input), title);
    }

    /// <summary>ورقِ یک کارت، بی باز کردنِ صفحه‌اش.</summary>
    [RelayCommand]
    private Task PdfCardAsync(AmanatCardViewModel? card)
    {
        var vm = card is null ? null : _all.FirstOrDefault(a => a.Entity.Id == card.Entity.Id);
        return vm is null ? Task.CompletedTask : vm.PdfCommand.ExecuteAsync(null);
    }

    /// <summary>
    /// ‎addAmanatAccount‎ — «➕ حساب جدید»: کادرِ تازه با یک ردیفِ خالی؛ سوختش از
    /// فیلترِ همان لحظه (دیزل اگر فیلتر دیزل است، وگرنه پطرول). نام را در صفحهٔ حساب
    /// می‌نویسید — سایت هم چیزی نمی‌پرسد.
    /// </summary>
    [RelayCommand]
    private async Task AddAccountAsync()
    {
        var fuel = FuelFilter == "diesel" ? FuelType.Diesel : FuelType.Petrol;
        var a = await _host.Amanat.AddAsync(fuel);
        _all.Add(new AmanatAccountViewModel(_host, a, this));
        ApplyFilter();
        _host.Toast("✅ کادرِ حسابِ جدید ساخته شد", ToastKind.Ok);
    }

    [RelayCommand]
    private async Task DeleteCardAsync(AmanatCardViewModel? card)
    {
        if (card is null) return;
        var vm = _all.FirstOrDefault(a => a.Entity.Id == card.Entity.Id);
        if (vm is not null) await ConfirmDeleteAsync(vm);
    }

    /// <summary>‎deleteAmanatAccount‎ — می‌پرسد، به سطل می‌رود (۳۰ روز).</summary>
    public async Task ConfirmDeleteAsync(AmanatAccountViewModel vm)
    {
        if (!await Dialogs.ConfirmAsync("حذفِ حساب",
                "کادرِ حسابِ «" + AmanatService.AccLabel(vm.Entity) + "» با همهٔ ردیف‌هایش پاک شود؟\nتا ۳۰ روز در سطل زباله می‌ماند.")) return;
        await DeleteAccountAsync(vm);
        _host.Toast("🗑️ حساب به سطل زباله رفت", ToastKind.Warn);
    }

    public async Task DeleteAccountAsync(AmanatAccountViewModel vm)
    {
        await _host.Amanat.DeleteAccountAsync(vm.Entity.Id);
        _all.Remove(vm);
        if (ReferenceEquals(Page, vm)) Page = null;
        ApplyFilter();
    }

    // ══ 📊 خلاصه و ابزارها ════════════════════════════════════════════════════
    [RelayCommand]
    private void OpenTools()
    {
        Page = null;
        ShowTools = true;
        SettingsEditor.Fill(_host.Amanat.Settings());
        PaintTools();
    }

    [RelayCommand]
    private void CloseTools()
    {
        ShowTools = false;
        ApplyFilter();
    }

    public const string Disclaimer =
        "درصد کمبودی — این مقدار یک برآورد محاسباتی است و جایگزین اندازه‌گیری واقعی موجودی نمی‌شود. "
        + "مقدار تبخیر واقعی بدون اندازه‌گیری فیزیکی قابل تعیین قطعی نیست.";

    [ObservableProperty] private IReadOnlyList<AmanatLine> _petrolSum = Array.Empty<AmanatLine>();
    [ObservableProperty] private IReadOnlyList<AmanatLine> _dieselSum = Array.Empty<AmanatLine>();
    [ObservableProperty] private string _petrolSumTitle = "⛽ پطرول";
    [ObservableProperty] private string _dieselSumTitle = "🟤 دیزل";
    [ObservableProperty] private IReadOnlyList<string> _errors = Array.Empty<string>();
    [ObservableProperty] private IReadOnlyList<string> _warnings = Array.Empty<string>();

    /// <summary>‎_amSumCardHtml‎ — هشت خط (یکی جداکننده).</summary>
    public static List<AmanatLine> SumLines(AmanatFuelTotals t)
    {
        var pct = AmanatService.Fmt(t.LossPct, 3);
        return new List<AmanatLine>
        {
            new("رسید کل", AmanatService.Fmt2(t.Liters) + " لیتر"),
            new("برده شده", AmanatService.Fmt2(t.Taken) + " لیتر"),
            new("بخارِ تخمینی", AmanatService.Fmt2(t.Loss) + " لیتر"),
            new("سهمِ شما (فیصدی)", AmanatService.Fmt2(t.Share) + " لیتر"),
            new("باقی تیلِ مشتری", AmanatService.Fmt2(t.Rest) + " لیتر"),
            new("درصد بخار", pct + "٪"),
            new("یعنی روی هر فیصدی", "+" + pct + "٪ بگیرید"),
        };
    }

    private void PaintTools()
    {
        var o = _host.AmanatCalc.Overview(_all.Select(a => a.Entity).ToList(), Settings,
                                          (_, r) => AmanatAccountViewModel.AutoDaysOf(r), Match);
        PetrolSum = SumLines(o.Petrol);
        DieselSum = SumLines(o.Diesel);
        PetrolSumTitle = "⛽ پطرول (" + o.Petrol.Count + " حساب)";
        DieselSumTitle = "🟤 دیزل (" + o.Diesel.Count + " حساب)";
        Errors = o.Errors.Select(e => "❌ " + e).ToList();
        Warnings = o.Warnings.Select(w => "⚠️ " + w).ToList();
        if (ShowFee) PaintFee();
    }

    // ── 🧮 ماشین‌حسابِ فیصدی — ‎renderAmanatFee‎ ──────────────────────────────
    [ObservableProperty] private bool _showFee;
    [ObservableProperty] private string _feeLiters = "";
    [ObservableProperty] private string _feeDays = "";
    [ObservableProperty] private string _feeTemp = "";
    [ObservableProperty] private int _feeFuelIndex;
    [ObservableProperty] private string _feeMy = "";
    [ObservableProperty] private IReadOnlyList<AmanatLine> _feeBoxes = Array.Empty<AmanatLine>();
    [ObservableProperty] private string _feeVerdict = "";
    [ObservableProperty] private string _feeVerdictBrush = "Pump.Text";
    [ObservableProperty] private IReadOnlyList<AmanatGuideRow> _feeGuide = Array.Empty<AmanatGuideRow>();

    public string FeeCaret => ShowFee ? "▾" : "◂";
    partial void OnShowFeeChanged(bool v) { OnPropertyChanged(nameof(FeeCaret)); if (v) PaintFee(); }
    partial void OnFeeLitersChanged(string v) => PaintFee();
    partial void OnFeeDaysChanged(string v) => PaintFee();
    partial void OnFeeTempChanged(string v) => PaintFee();
    partial void OnFeeFuelIndexChanged(int v) => PaintFee();
    partial void OnFeeMyChanged(string v) => PaintFee();

    [RelayCommand] private void ToggleFee() => ShowFee = !ShowFee;

    private static decimal? FeeNum(string? v)
    {
        var t = Shamsi.ToEnDigits(v ?? "").Trim();
        if (t.Length == 0) return null;
        var n = AmanatSettingsEditorViewModel.Num(t, decimal.MinValue);
        return n == decimal.MinValue ? null : n;
    }

    public void PaintFee()
    {
        if (!ShowFee) return;
        var s = Settings;
        var calc = _host.AmanatCalc;
        var liters = FeeNum(FeeLiters) ?? 10000m;
        var days = FeeNum(FeeDays) ?? 30m;
        var temp = FeeNum(FeeTemp) ?? s.DefTemp;
        var fuel = FeeFuelIndex == 1 ? FuelType.Diesel : FuelType.Petrol;
        var my = FeeNum(FeeMy);
        var a = calc.FeeAdvice(liters, days, temp, fuel, my, s);

        FeeBoxes = new List<AmanatLine>
        {
            new("کمبودیِ تخمینی", AmanatService.Fmt2(a.Loss) + " لیتر"),
            new("فیصدیِ سربه‌سر", AmanatService.Fmt(a.NeedPct, 3) + "٪"),
            new("فیصدیِ پیشنهادی", AmanatService.Fmt(a.SafePct, 3) + "٪", "Pump.Ok"),
            new("با فیصدیِ شما", a.MyPct is null ? "—" : AmanatService.Fmt2(a.MyLiters ?? 0m) + " لیتر"),
            new("کم / اضافه", a.Diff is not { } d ? "—" : (d >= 0m ? "+" : "−") + AmanatService.Fmt2(Math.Abs(d)) + " لیتر"),
        };

        if (a.MyPct is null)
        {
            FeeVerdict = "روی " + AmanatService.Fmt2(a.Liters) + " لیتر که "
                + AmanatService.Fmt(Math.Round(days, MidpointRounding.AwayFromZero), 0)
                + " روز در " + AmanatService.Fmt(temp) + "°C بماند، حدود " + AmanatService.Fmt2(a.Loss)
                + " لیتر کم می‌آید. برای جبرانش دستِ‌کم " + AmanatService.Fmt(a.NeedPct, 3)
                + "٪ بگیرید؛ با حاشیهٔ اطمینان " + AmanatService.Fmt(a.SafePct, 3) + "٪ پیشنهاد می‌شود.";
            FeeVerdictBrush = "Pump.Text";
        }
        else if (a.Diff >= 0m)
        {
            FeeVerdict = "✅ با " + AmanatService.Fmt(a.MyPct.Value, 3) + "٪ کمبودی جبران می‌شود و "
                + AmanatService.Fmt2(a.Diff!.Value) + " لیتر هم اضافه می‌آید.";
            FeeVerdictBrush = "Pump.Ok";
        }
        else
        {
            FeeVerdict = "❌ با " + AmanatService.Fmt(a.MyPct.Value, 3) + "٪ به اندازهٔ "
                + AmanatService.Fmt2(Math.Abs(a.Diff!.Value)) + " لیتر کم می‌آورید. دستِ‌کم "
                + AmanatService.Fmt(a.NeedPct, 3) + "٪ لازم است.";
            FeeVerdictBrush = "Pump.Danger";
        }

        var near = AmanatService.NearestGuideDay(days);
        FeeGuide = AmanatService.GuideDays.Select(d =>
        {
            var x = calc.FeeAdvice(liters, d, temp, fuel, my, s);
            var cover = x.MyPct is null ? "—"
                : x.Diff >= 0m ? "جبران می‌شود" : AmanatService.Fmt2(Math.Abs(x.Diff!.Value)) + " لیتر کم";
            var brush = x.MyPct is null ? "Pump.Text" : x.Diff >= 0m ? "Pump.Ok" : "Pump.Danger";
            return new AmanatGuideRow(d + " روز", AmanatService.Fmt2(x.Loss) + " لیتر",
                AmanatService.Fmt(x.NeedPct, 3) + "٪", AmanatService.Fmt(x.SafePct, 3) + "٪",
                cover, brush, d == near);
        }).ToList();
    }
}

/// <summary>یک ردیفِ جدولِ «در هر گرما چقدر تبخیر می‌شود» — ‎_amTempTableHtml‎.</summary>
public sealed class AmanatTempRowViewModel
{
    public AmanatTempRowViewModel(decimal temp, decimal factor, decimal perMonth, decimal thermalPct, bool near)
    {
        // ‎_amFmt(n, dec)‎ — تا ‎dec‎ رقمِ اعشار، بی صفرهای انتهایی («۱×»، نه «۱٫۰۰×»)
        TempText = F(temp, 0) + "°C";
        FactorText = F(factor, 2) + "×";
        MonthText = F(perMonth, 3) + "٪";
        YearText = F(perMonth * 12m, 3) + "٪";
        ThermalText = (thermalPct >= 0m ? "+" : "−") + F(Math.Abs(thermalPct), 2) + "٪";
        IsNear = near;
    }

    private static string F(decimal v, int d) => AmanatService.Fmt(v, d);

    public string TempText { get; }
    public string FactorText { get; }
    public string MonthText { get; }
    public string YearText { get; }
    /// <summary>انبساطِ گرمایی — تبخیر نیست و با سرد شدن برمی‌گردد.</summary>
    public string ThermalText { get; }
    /// <summary>نزدیک‌ترین ردیف به گرمای پیش‌فرض — در سایت پررنگ می‌شود.</summary>
    public bool IsNear { get; }
    public string NearBrushKey => IsNear ? "Pump.Ok" : "Pump.Text";
}

/// <summary>
/// ══ ⚙️ تنظیمات مدیر — ضریب‌های محاسبهٔ تیل امانت ═══════════════════════════
/// همتای ‎#am-settings‎ · ‎fillAmanatSettings‎ · ‎setAmanatSetting‎ ·
/// ‎resetAmanatSettings‎ی سایت. تا امروز برنامه این ضریب‌ها را فقط می‌خواند
/// و هیچ‌جا نمی‌شد عوضشان کرد.
///
/// هر خانه مثلِ سایت ‎oninput‎ است: تا عدد عوض شود، همان لحظه ذخیره می‌شود و
/// همهٔ حساب‌ها دوباره حساب می‌شوند. عددِ خراب یا خالی همان پیش‌فرضِ همان
/// ضریب حساب می‌شود (‎amSettings()‎ همین کار را می‌کند).
///
/// ⚠️ فرمول‌ها این‌جا نیستند — <see cref="AmanatService"/> است. این‌جا فقط
/// خانه‌های تایپ و همان جدولِ راهنمای «تبخیر در هر گرما».
/// </summary>
public sealed partial class AmanatSettingsEditorViewModel : ObservableObject
{
    /// <summary>دماهای جدولِ راهنما — ‎AM_TEMP_ROWS‎.</summary>
    private static readonly decimal[] TempRowsC = { 0, 10, 15, 20, 25, 30, 35, 40, 45, 50 };

    /// <summary>منابعِ ضریب‌ها — ‎AM_SOURCES‎.</summary>
    public static readonly IReadOnlyList<(string Title, string Url)> Sources = new[]
    {
        ("پژوهشِ میدانیِ تبخیرِ مخزنِ پطرول — با میانگین دمای ۳۲٫۶°C، بیش از ۰٫۵۲٪ در سال",
         "https://www.researchgate.net/publication/274376940_MANAGEMENT_OF_EVAPORATION_LOSSES_OF_GASOLINE'S_STORAGE_TANKS"),
        ("تبخیرِ پطرول و دیزل در انبارش — دیزل در برابر پطرول ناچیز",
         "https://link.springer.com/article/10.1007/s10553-021-01304-0"),
        ("آمارِ رسمیِ کانادا با مدلِ EPA — کلِ تبخیرِ یک پمپ ۰٫۱۵٪ِ گردشِ تیل",
         "https://www150.statcan.gc.ca/n1/pub/16-001-m/2012015/part-partie1-eng.htm"),
        ("همان — روشِ محاسبهٔ تبخیر و اثرِ دمای مخزنِ زیرزمینی",
         "https://www150.statcan.gc.ca/n1/pub/16-001-m/2012015/appendix-appendice1-eng.htm"),
        ("EPA — مخزن‌های نگهداریِ فرآورده‌های نفتی و تبخیرِ آن‌ها (AP-42)",
         "https://www.epa.gov/air-emissions-factors-and-quantification/ap-42-fifth-edition-volume-i-chapter-4-evaporation-loss-0"),
        ("فشارِ بخارِ پطرول (Reid) — پایهٔ این‌که تبخیر با گرما بالا می‌رود",
         "https://en.wikipedia.org/wiki/Reid_vapor_pressure"),
        ("انبساطِ گرماییِ تیل — پطرول ۱٫۲ و دیزل ۰٫۸۵ میلی‌لیتر در لیتر بر درجه",
         "https://www.ricochetfuel.com/blog/the-facts-about-fuel-expansion-and-contraction/"),
        ("NIST — اثرِ گرما بر حجمِ تیل در داد‌وستد (دمای مرجع ۶۰°F)",
         "https://www.nist.gov/speech-testimony/hot-fuels-impact-commercial-transactions-thermal-expansion-gasoline"),
        ("EPA AP-42 بخش ۵٫۲ — تنفسِ مخزنِ زیرزمینیِ جایگاه: ۱۲۰ میلی‌گرم در هر لیترِ گردشِ تیل (≈ ۰٫۰۱۶٪)",
         "https://www.epa.gov/sites/default/files/2020-09/documents/5.2_transportation_and_marketing_of_petroleum_liquids.pdf"),
        ("EPA AP-42 بخش ۷٫۱ — مخزنِ «آزادانه تنفس‌کننده» در برابرِ مخزنِ دارایِ شیرِ فشار/خلأ (بدونِ ضریبِ ثابت)",
         "https://www.epa.gov/sites/default/files/2020-10/documents/ch07s01.pdf"),
    };

    private readonly AmanatService _calc;
    private readonly Action<AmanatSettings> _save;
    private readonly Func<Task> _refresh;
    private bool _filling;

    public AmanatSettingsEditorViewModel(AmanatService calc, Action<AmanatSettings> save, Func<Task> refresh)
    { _calc = calc; _save = save; _refresh = refresh; Fill(AmanatSettings.Default); }

    [ObservableProperty] private string _basePct = "";
    [ObservableProperty] private string _fPetrol = "";
    [ObservableProperty] private string _fDiesel = "";
    [ObservableProperty] private string _tankFactor = "";
    [ObservableProperty] private string _defTemp = "";
    [ObservableProperty] private string _safetyPct = "";
    [ObservableProperty] private string _refTemp = "";
    [ObservableProperty] private string _tDouble = "";
    [ObservableProperty] private string _handlingPct = "";

    [ObservableProperty] private string _formulaText = "";
    public BulkRows<AmanatTempRowViewModel> TempRows { get; } = new();
    public string SourcesText => string.Join("\n", Sources.Select(s => "• " + s.Title + "\n  " + s.Url));

    /// <summary>تنظیمِ در حالِ اثر — همان که آخرین بار ذخیره شد.</summary>
    public AmanatSettings Current { get; private set; } = AmanatSettings.Default;

    /// <summary>‎fillAmanatSettings‎ — خانه‌ها را از تنظیمِ ذخیره‌شده پر می‌کند، بی ذخیرهٔ دوباره.</summary>
    public void Fill(AmanatSettings s)
    {
        _filling = true;
        try
        {
            Current = s;
            BasePct = N(s.BasePct); FPetrol = N(s.FPetrol); FDiesel = N(s.FDiesel);
            TankFactor = N(s.TankFactor); DefTemp = N(s.DefTemp); SafetyPct = N(s.SafetyPct);
            RefTemp = N(s.RefTemp); TDouble = N(s.TDouble); HandlingPct = N(s.HandlingPct);
        }
        finally { _filling = false; }
        Describe(s);
    }

    public static string N(decimal v) => v.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>‎_amNum‎ — رقمِ فارسی هم پذیرفته می‌شود؛ خراب یا خالی ⇒ پیش‌فرضِ همان ضریب.</summary>
    public static decimal Num(string? v, decimal fallback) =>
        decimal.TryParse(Shamsi.ToEnDigits(v ?? "").Trim().Replace('٫', '.').Replace(',', '.'),
                         System.Globalization.NumberStyles.Float,
                         System.Globalization.CultureInfo.InvariantCulture, out var n)
            ? n : fallback;

    /// <summary>خانه‌های تایپ → تنظیم. هر خانهٔ خراب همان پیش‌فرضِ خودش می‌شود.</summary>
    public AmanatSettings Parse()
    {
        var d = AmanatSettings.Default;
        return Current with
        {
            BasePct = Num(BasePct, d.BasePct), FPetrol = Num(FPetrol, d.FPetrol), FDiesel = Num(FDiesel, d.FDiesel),
            TankFactor = Num(TankFactor, d.TankFactor), DefTemp = Num(DefTemp, d.DefTemp),
            SafetyPct = Num(SafetyPct, d.SafetyPct), RefTemp = Num(RefTemp, d.RefTemp),
            TDouble = Num(TDouble, d.TDouble), HandlingPct = Num(HandlingPct, d.HandlingPct),
        };
    }

    partial void OnBasePctChanged(string v) => Apply();
    partial void OnFPetrolChanged(string v) => Apply();
    partial void OnFDieselChanged(string v) => Apply();
    partial void OnTankFactorChanged(string v) => Apply();
    partial void OnDefTempChanged(string v) => Apply();
    partial void OnSafetyPctChanged(string v) => Apply();
    partial void OnRefTempChanged(string v) => Apply();
    partial void OnTDoubleChanged(string v) => Apply();
    partial void OnHandlingPctChanged(string v) => Apply();

    /// <summary>‎setAmanatSetting‎ — ذخیره و حسابِ دوبارهٔ همهٔ حساب‌ها، همان لحظه.</summary>
    private void Apply()
    {
        if (_filling) return;
        var s = Parse();
        if (s == Current) return;
        Current = s;
        _save(s);
        Describe(s);
        _ = _refresh();
    }

    /// <summary>‎resetAmanatSettings‎ — «📚 برگشت به ضریب‌های منبع».</summary>
    [RelayCommand]
    private Task Reset()
    {
        _save(AmanatSettings.Default);
        Fill(AmanatSettings.Default);
        return _refresh();
    }

    /// <summary>متنِ فرمول و جدولِ «تبخیر در هر گرما» — ‎fillAmanatSettings‎ + ‎_amTempTableHtml‎.</summary>
    private void Describe(AmanatSettings s)
    {
        FormulaText =
            "ضریب گرما = ۲ ^ ((گرما − " + Shamsi.Money(s.RefTemp) + ") ÷ " + Shamsi.Money(s.TDouble) + ")"
            + " — یعنی هر " + Shamsi.Money(s.TDouble) + " درجه، تبخیر دو برابر می‌شود.\n"
            + "تبخیر = مقدار تیل × (پایه ÷ ۱۰۰) × (روز ÷ ۳۰) × ضریب گرما × ضریب تیل × ضریب مخزن\n"
            + "فیصدیِ لازم = فیصدیِ هدفِ شما + درصد تبخیر"
            + (s.HandlingPct != 0m ? " + " + AmanatService.Fmt(s.HandlingPct, 2) + "٪ (اندازه‌گیری و ریخت‌وپاش)" : "");

        var rows = TempTable(_calc, s, FuelType.Petrol);
        TempRows.ResetTo(rows);
        TableNote = TempTableNote(s, FuelType.Petrol);
    }

    [ObservableProperty] private string _tableNote = "";

    /// <summary>‎_amTempTableHtml(s, fuel)‎ — ده ردیفِ ‎AM_TEMP_ROWS‎ برای یک سوخت.</summary>
    public static List<AmanatTempRowViewModel> TempTable(AmanatService calc, AmanatSettings s, FuelType fuel)
    {
        var ff = calc.FuelFactor(fuel, s);
        var rows = new List<AmanatTempRowViewModel>();
        foreach (var t in TempRowsC)
        {
            var f = calc.TempFactor(t, s);
            var perMonth = s.BasePct * f * ff * s.TankFactor;      // ٪ در ماه
            var th = calc.Thermal(1m, t, fuel, s).Pct;
            rows.Add(new AmanatTempRowViewModel(t, f, perMonth, th, Math.Abs(t - s.DefTemp) < 2.5m));
        }
        return rows;
    }

    /// <summary>نوشتهٔ زیرِ همان جدول (‎.am-an-note‎).</summary>
    public static string TempTableNote(AmanatSettings s, FuelType fuel) =>
        "ستونِ تبخیر واقعاً از بین می‌رود. ستونِ انبساط گرمایی فقط عددِ لیتر را "
        + "نسبت به دمای مرجعِ ۱۵٫۶°C کم و زیاد نشان می‌دهد و با سرد شدنِ هوا برمی‌گردد — رویش فیصدی نگیرید. "
        + "عددها برای " + (fuel == FuelType.Diesel ? "دیزل" : "پطرول") + " و با ضریبِ مخزنِ "
        + AmanatService.Fmt(s.TankFactor, 2) + " حساب شده‌اند.";
}

/// <summary>
/// ══ ضررِ تیلِ امانتیِ فروخته‌شده وقتی قیمت بالا می‌رود (۱۴۰۵/۰۷/۲۰) ══════════
/// پولِ گرفته = لیترِ فروخته × نرخِ فروش؛ پولِ لازم برای پس دادن = همان لیتر × نرخِ امروز.
/// ضرر = تفاوتِ این دو (منفی یعنی سود). هر یک افغانی بالا رفتنِ نرخ = همان لیترها ضرر.
/// </summary>
public static class AmanatPriceRisk
{
    public readonly record struct Result(decimal Got, decimal NeedNow, decimal Loss, decimal PerAfghani);

    public static Result Of(decimal soldLiters, decimal salePrice, decimal nowPrice)
    {
        var got = soldLiters * salePrice;
        var need = soldLiters * nowPrice;
        return new Result(got, need, need - got, soldLiters);
    }
}
