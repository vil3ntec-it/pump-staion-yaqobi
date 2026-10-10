using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using PumpYaqobi.App.Controls;
using CommunityToolkit.Mvvm.Input;
using PumpYaqobi.App.Printing;
using PumpYaqobi.App.Services;
using PumpYaqobi.Application.Localization;
using PumpYaqobi.Application.Services;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Reporting.Pdf;

namespace PumpYaqobi.App.ViewModels.Sections;

/// <summary>یک کارمند در جدولِ کمبودی/اضافی.</summary>
public sealed partial class StaffShortRowViewModel : ObservableObject
{
    public StaffShortRowViewModel(StaffShortRow r) => Row = r;

    public StaffShortRow Row { get; }

    public string Name => Row.Name;
    public string ShiftsText => Shamsi.Money(Row.Shifts);
    public string ShortText => Row.RemainShort > 0m ? Shamsi.Money(Row.RemainShort) : "—";
    public string ExcessText => Row.RemainExcess > 0m ? Shamsi.Money(Row.RemainExcess) : "—";
    public string ShortBrushKey => Row.RemainShort > 0m ? "Pump.Danger" : "Pump.Muted";
    public string ExcessBrushKey => Row.RemainExcess > 0m ? "Pump.Ok" : "Pump.Muted";

    public bool CanSettleShort => Row.RemainShort > 0m;
    public bool CanSettleExcess => Row.RemainExcess > 0m;
    public bool IsSettled => !CanSettleShort && !CanSettleExcess;
}

/// <summary>یک رسید/پرداختِ ثبت‌شده.</summary>
public sealed class StaffSettleRowViewModel
{
    public StaffSettleRowViewModel(StaffShortSettle s) => Entity = s;

    public StaffShortSettle Entity { get; }

    public string Name => Entity.Name ?? "";
    public string KindText => Entity.Kind == StaffSettleKind.Excess ? "💸 پرداخت اضافی"
                                                                   : "💵 رسید کمبودی";
    public string KindBrushKey => Entity.Kind == StaffSettleKind.Excess ? "Pump.Ok" : "Pump.Danger";
    public string AmountText => Shamsi.Money(Entity.Amount);
    public string DateShamsi => Entity.DateShamsi ?? "";
    /// <summary>دوره‌ای که این رسید برای آن است.</summary>
    public string ForText => string.IsNullOrWhiteSpace(Entity.ForMonth) ? "همهٔ ماه‌ها"
        : Entity.ForMonth!.Length == 4 ? "سالِ " + Entity.ForMonth : Shamsi.MonthLabel(Entity.ForMonth!);
}

/// <summary>سهمِ یک شیفتِ یک ورق در کمبودی/اضافیِ کارمندِ انتخاب‌شده — با دلیل.</summary>
public sealed class StaffShortLineViewModel
{
    public StaffShortLineViewModel(StaffShortLine l)
    {
        Line = l;
        Title = "📝 ورق " + l.DateShamsi + " — " + (l.Kind == ShiftKind.Night ? "شب" : "روز");
        Reason = "قرضِ اعلام‌شده " + Shamsi.Money(l.Declared) + " − (قرض " + Shamsi.Money(l.Debt)
               + " + مصرف " + Shamsi.Money(l.Expenses) + ")";
        Amount = l.Shortage > 0 ? "🔴 کمبودی " + Shamsi.Money(l.Shortage)
               : l.Excess > 0 ? "🟢 اضافی " + Shamsi.Money(l.Excess) : "⚪ بی‌کمبودی";
        BrushKey = l.Shortage > 0 ? "Pump.Danger" : l.Excess > 0 ? "Pump.Ok" : "Pump.Muted";
    }
    public StaffShortLine Line { get; }
    public string Title { get; }
    public string Reason { get; }
    public string Amount { get; }
    public string BrushKey { get; }
}

/// <summary>
/// ══ کمبودی و اضافیِ کارمندان ═══════════════════════════════════════════════
/// رونوشتِ ‎_renderStaffShortPanel‎ · ‎openStaffShortSettle‎ ·
/// ‎confirmStaffShortSettle‎ · ‎deleteStaffShortSettle‎.
///
///   🔴 کمبودی = کارمند بدهکار است → «رسید کمبودی» از او می‌گیریم.
///   🟢 اضافی  = پمپ بدهکار است    → «پرداخت اضافی» به او می‌دهیم.
///
/// عددها از خودِ ورق‌های روزانه حساب می‌شوند و این‌جا هیچ‌جا ذخیره نمی‌شوند؛
/// فقط تسویه‌ها ذخیره می‌شوند و از باقی‌مانده کم می‌کنند. پس با پاک کردنِ یک
/// تسویه، باقی‌ماندهٔ همان کارمند دوباره بالا می‌رود — همان‌طور که باید.
///
/// ══ ۱۴۰۵/۰۷/۱۸ — دوره، سهمِ هر ورق، رسیدِ چاپی ══════════════════════════════
/// خواستهٔ صاحب ریپو: «محاسباتِ کمبودی فقط از ورق‌ها و سهمِ هر ورق جداگانه… فیلترهای
/// کشوییِ ماه و سال محاسباتِ همان دوره… دکمه‌های رسیدِ کمبودی درست کار کنند و رسیدها
/// با اطلاعاتِ دقیق تولید و چاپ شوند.»
///   • کشوی سال و ماه (‎Picker‎) ⇒ فقط ورق‌ها و رسیدهای همان دوره (‎StaffShortPeriodAsync‎).
///   • انتخابِ کارمند ⇒ فهرستِ ورق‌هایی که کمبودی/اضافی از آن‌ها آمده، با تاریخ و دلیل.
///   • «رسید کمبودی» ⇒ کارمند و مبلغ در کادرِ ثبت (و فوکوس همان‌جا)، «رسید کمبودی گرفتم»
///     ⇒ ثبت برای همان دوره و باز شدنِ رسیدِ چاپی؛ هر رسیدِ ثبت‌شده «🧾 چاپ» دارد.
///   • خطای اجازه (کارمندِ غیرمدیر، فقط‌خواندنی) دیگر بی‌صدا گم نمی‌شود.
/// </summary>
public sealed partial class StaffShortSectionViewModel : SectionViewModel
{
    private readonly AppHost _host;
    private ProfitPeriod _period = ProfitPeriod.All;
    private bool _loadingPicker;
    private List<StaffShortLine> _lines = new();

    public StaffShortSectionViewModel(AppHost host)
        : base("staffshort", "attendance", "کمبودی کارمندان")
    {
        _host = host;
        Picker = new YearMonthPicker(k =>
        {
            if (_loadingPicker) return;
            var p = ProfitPeriod.FromKey(k);
            if (p == _period) return;
            _period = p;
            _ = CrashGuard.RunAsync("کمبودی کارمندان", RefreshAsync);
        }, "همهٔ ماه‌ها");
    }

    /// <summary>کشوی سال و ماه — ماه‌هایی که ورق یا رسیدِ کمبودی دارند.</summary>
    public YearMonthPicker Picker { get; }

    /// <summary>⚠️ ‎BulkRows‎: پر شدنِ جدول یک خبر می‌دهد نه ‎n‎ خبر
    /// — وگرنه جدول به ازای هر ردیف یک‌بار از نو چیده می‌شود و بخش می‌ایستد.</summary>
    public BulkRows<StaffShortRowViewModel> Rows { get; } = new();
    public ObservableCollection<StaffSettleRowViewModel> Settles { get; } = new();
    /// <summary>ورق‌های کارمندِ انتخاب‌شده در همین دوره.</summary>
    public BulkRows<StaffShortLineViewModel> SelectedLines { get; } = new();

    [ObservableProperty] private StaffShortRowViewModel? _selected;
    [ObservableProperty] private string _amount = "";
    [ObservableProperty] private string _totalShort = "—";
    [ObservableProperty] private string _totalExcess = "—";
    [ObservableProperty] private string _periodText = "همهٔ ماه‌ها";

    /// <summary>کادرِ ثبت باید فوکوس بگیرد (کلیکِ «رسید کمبودی» روی ردیف).</summary>
    public event Action? AmountFocusRequested;

    /// <summary>ردیفِ «جمله»ی ته جدول — کمبودی و اضافی، جدا از هم.</summary>
    public IReadOnlyList<TotalCell> TotalCells => new[]
    {
        new TotalCell("کارمندها", Shamsi.Money(Rows.Count)),
        new TotalCell("🔴 کمبودیِ مانده", TotalShort, "Pump.Danger"),
        new TotalCell("🟢 اضافیِ مانده", TotalExcess, "Pump.Ok"),
    };

    partial void OnTotalShortChanged(string v) => OnPropertyChanged(nameof(TotalCells));
    partial void OnTotalExcessChanged(string v) => OnPropertyChanged(nameof(TotalCells));

    partial void OnSelectedChanged(StaffShortRowViewModel? value)
    {
        SelectedLines.ResetTo(value is null ? Enumerable.Empty<StaffShortLineViewModel>()
            : _lines.Where(l => l.Key == value.Row.Key).OrderByDescending(l => l.DateKey).ThenByDescending(l => l.Kind)
                    .Select(l => new StaffShortLineViewModel(l)));
        OnPropertyChanged(nameof(SelectedTitle));
        OnPropertyChanged(nameof(HasSelected));
    }

    public bool HasSelected => Selected is not null;

    /// <summary>کادرِ ثبت می‌گوید رسید برای کیست و چقدر مانده — بی حدس.</summary>
    public string SelectedTitle => Selected is null
        ? "اول از جدول یک کارمند (یا دکمهٔ «رسید کمبودی» کنارِ نامش) را بزنید."
        : $"کارمند: {Selected.Name} — دوره: {PeriodText} — 🔴 کمبودیِ مانده {Selected.ShortText} · 🟢 اضافیِ مانده {Selected.ExcessText}";

    public bool IsEmpty => Rows.Count == 0;
    public bool HasSettles => Settles.Count > 0;

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

    private async Task RefreshAsync()
    {
        var months = await _host.Tools.StaffShortMonthsAsync();
        _loadingPicker = true;
        try { Picker.Load(months, Picker.SelectedKey); }
        finally { _loadingPicker = false; }
        _period = ProfitPeriod.FromKey(Picker.SelectedKey);
        var period = _period;

        var (rows, lines, settles) = await _host.Tools.StaffShortPeriodAsync(period);
        if (period != _period) return;
        _lines = lines;
        PeriodText = period.IsAll ? "همهٔ ماه‌ها"
                   : period.Month.Length == 0 ? "سالِ " + period.Year
                   : Shamsi.MonthLabel(period.Year + "/" + period.Month);

        var keep = Selected?.Row.Key;
        using (Rows.Batch())
        {
            Rows.Clear();
            foreach (var r in rows) Rows.Add(new StaffShortRowViewModel(r));
        }
        Selected = Rows.FirstOrDefault(r => r.Row.Key == keep);
        OnSelectedChanged(Selected);

        Settles.Clear();
        foreach (var s in settles) Settles.Add(new StaffSettleRowViewModel(s));

        var ts = rows.Sum(r => r.RemainShort);
        var te = rows.Sum(r => r.RemainExcess);
        TotalShort = ts > 0m ? Shamsi.Money(ts) : "—";
        TotalExcess = te > 0m ? Shamsi.Money(te) : "—";

        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(HasSettles));
    }

    /// <summary>ورقِ همین جدول — با فهرستِ تسویه‌ها زیرش.</summary>
    [RelayCommand]
    private Task PdfAsync()
    {
        var input = new StaffShortReportInput(
            Rows.Select(r => r.Row).ToList(),
            Settles.Select(s => s.Entity).ToList(),
            PeriodText + " · " + DocDates.Line());
        return Documents.ShowAsync(() => new StaffShortReport(input), "کمبودی کارمندان — " + PeriodText);
    }

    /// <summary>«💵 رسید کمبودی» — کارمند و مبلغ در کادرِ ثبت، و فوکوس همان‌جا.</summary>
    [RelayCommand]
    private void PickShort(StaffShortRowViewModel? row)
    {
        if (row is null || !row.CanSettleShort) return;
        Selected = row;
        Amount = Shamsi.Money(row.Row.RemainShort);
        AmountFocusRequested?.Invoke();
    }

    /// <summary>«💸 پرداخت اضافی».</summary>
    [RelayCommand]
    private void PickExcess(StaffShortRowViewModel? row)
    {
        if (row is null || !row.CanSettleExcess) return;
        Selected = row;
        Amount = Shamsi.Money(row.Row.RemainExcess);
        AmountFocusRequested?.Invoke();
    }

    [RelayCommand]
    private async Task SettleShortAsync() => await SettleAsync(StaffSettleKind.Short);

    [RelayCommand]
    private async Task SettleExcessAsync() => await SettleAsync(StaffSettleKind.Excess);

    /// <summary>کلیدِ دورهٔ رسید: «1405/07» برای یک ماه، «1405» برای یک سال، خالی برای همه.</summary>
    private string? ForMonth => _period.IsAll ? null
        : _period.Month.Length == 0 ? _period.Year : _period.Year + "/" + _period.Month;

    public StaffShortSettle? LastSettle { get; private set; }

    private async Task SettleAsync(StaffSettleKind kind)
    {
        if (Selected is null) { _host.Toast("اول کارمند را انتخاب کنید", ToastKind.Error); return; }
        if (Shamsi.FirstUnreadable(("مقدار", Amount)) is { } bad) { _host.Toast("«" + bad + "» عدد نیست — ذخیره نشد.", ToastKind.Warn); return; }

        StaffShortSettle? settle;
        try { settle = await _host.Tools.SettleForAsync(Selected.Row, kind, Shamsi.Num(Amount), ForMonth); }
        catch (PumpYaqobi.Application.Security.PermissionDeniedException ex)
        {
            //  ⛔ پیش از این همین استثنا بی‌صدا در فرمان گم می‌شد و دکمه «کار نمی‌کرد».
            _host.Toast("⛔ " + ex.Message, ToastKind.Error);
            return;
        }
        if (settle is null)
        {
            var cap = kind == StaffSettleKind.Excess
                ? Selected.Row.RemainExcess : Selected.Row.RemainShort;
            _host.Toast(cap <= 0m
                ? (kind == StaffSettleKind.Excess ? "این کارمند در این دوره اضافیِ مانده ندارد" : "این کارمند در این دوره کمبودیِ مانده ندارد")
                : "مبلغ باید بین ۱ و باقی‌مانده (" + Shamsi.Money(cap) + ") باشد", ToastKind.Error);
            return;
        }

        LastSettle = settle;
        _host.Toast(kind == StaffSettleKind.Excess
            ? "✅ پرداختِ اضافیِ " + Selected.Name + " ثبت شد — رسیدش باز شد"
            : "✅ رسیدِ کمبودیِ " + Selected.Name + " ثبت شد — رسیدش باز شد", ToastKind.Ok);

        Amount = "";
        var lines = _lines;
        var period = PeriodText;
        await RefreshAsync();
        await PrintReceiptAsync(settle, lines, period);
    }

    /// <summary>«🧾 چاپ» روی هر رسیدِ ثبت‌شده — با ورق‌های همان دوره.</summary>
    [RelayCommand]
    private async Task ReceiptAsync(StaffSettleRowViewModel? row)
    {
        if (row is null) return;
        var s = row.Entity;
        var period = ProfitPeriod.FromKey(string.IsNullOrWhiteSpace(s.ForMonth) ? ""
            : s.ForMonth!.Length == 4 ? s.ForMonth + "/*" : s.ForMonth);
        var (_, lines, _) = await _host.Tools.StaffShortPeriodAsync(period);
        await PrintReceiptAsync(s, lines, row.ForText);
    }

    private Task PrintReceiptAsync(StaffShortSettle s, List<StaffShortLine> lines, string period)
    {
        var input = new StaffSettleReceiptInput(s, period, lines, DocDates.Line());
        return Documents.ShowAsync(() => new StaffSettleReceipt(input),
            (s.Kind == StaffSettleKind.Excess ? "رسید پرداخت اضافی " : "رسید کمبودی ") + (s.Name ?? ""));
    }

    [RelayCommand]
    private async Task DeleteSettleAsync(StaffSettleRowViewModel? row)
    {
        if (row is null) return;
        if (!await Dialogs.ConfirmAsync("حذفِ این مورد",
                "باقی‌ماندهٔ کمبودی/اضافی دوباره بالا می‌رود. حذف شود؟")) return;
        try { await _host.Tools.DeleteSettleAsync(row.Entity.Id); }
        catch (PumpYaqobi.Application.Security.PermissionDeniedException ex)
        { _host.Toast("⛔ " + ex.Message, ToastKind.Error); return; }
        await RefreshAsync();
    }
}
