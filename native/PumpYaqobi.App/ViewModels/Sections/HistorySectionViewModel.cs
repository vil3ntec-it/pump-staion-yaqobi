using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PumpYaqobi.App.Services;
using PumpYaqobi.Application.Localization;
using PumpYaqobi.Services.Data;

namespace PumpYaqobi.App.ViewModels.Sections;

/// <summary>یک کارت در صفحهٔ «تاریخچه‌ها» — یک بخش و شمارِ ردیف‌هایش.</summary>
public sealed partial class HistoryCardViewModel : ObservableObject
{
    private readonly HistorySectionViewModel _owner;

    public HistoryCardViewModel(HistoryKind k, HistorySectionViewModel owner)
    { Entity = k; _owner = owner; }

    public HistoryKind Entity { get; }

    public string Label => Entity.Label;

    /// <summary>«۱۲۴ ردیف · تازه‌ترین: ۱۴۰۵/۰۶/۱۲» یا «هنوز چیزی ثبت نشده».</summary>
    public string SubText => Entity.Count == 0
        ? "هنوز چیزی ثبت نشده"
        : Shamsi.Money(Entity.Count) + " ردیف"
          + (string.IsNullOrWhiteSpace(Entity.LatestDate) ? "" : " · تازه‌ترین: " + Entity.LatestDate);

    [RelayCommand]
    private Task Open() => _owner.OpenAsync(Entity.Key);
}

/// <summary>دکمهٔ کوچکِ «شمارهٔ پایه» در تاریخچهٔ پارچه‌ها — ۰ یعنی همه.</summary>
public sealed partial class PumpChip : ObservableObject
{
    private readonly HistorySectionViewModel _owner;
    public PumpChip(int n, string text, HistorySectionViewModel owner) { Number = n; Text = text; _owner = owner; }
    public int Number { get; }
    public string Text { get; }
    public string Tip => Number == 0 ? "همهٔ پایه‌ها با هم" : "فقط تاریخچهٔ پایهٔ " + Text;
    public bool IsOn => _owner.IsPumpPicked(Number);
    internal void Raise() => OnPropertyChanged(nameof(IsOn));
    [RelayCommand] private void Pick() => _owner.PickPump(Number);
}

/// <summary>یک ردیفِ صفحهٔ تاریخچهٔ یک بخش.</summary>
public sealed partial class HistoryRowViewModel : ObservableObject
{
    private readonly Func<HistoryRowViewModel, Task>? _saveShift;

    public HistoryRowViewModel(HistoryRow r, int index,
                               Func<HistoryRowViewModel, Task>? saveShift = null)
    {
        Entity = r; Index = index; _saveShift = saveShift;
        if (r.ShiftRef is { } x)
        {
            _name = x.Name; _pump = x.PumpNum; _start = x.Start; _end = x.End;
            _price = x.Price; _debt = x.Debt;
        }
    }

    public HistoryRow Entity { get; }
    public int Index { get; }

    public string DateText => string.IsNullOrWhiteSpace(Entity.DateShamsi) ? "بی‌تاریخ" : Entity.DateShamsi;

    /// <summary>خانه‌های جدولِ همان بخش — ‎HistoryService.ColumnsOf‎.</summary>
    public IReadOnlyList<string> Cells => Entity.Cells ?? Array.Empty<string>();
    public string Title => Entity.Title;
    public string Detail => Entity.Detail;
    public string AmountText => Entity.AmountText;

    /// <summary>رنگِ مبلغ — سبزِ «آمد»، نارنجیِ «رفت»، یا رنگِ معمولی.</summary>
    public string AmountBrushKey => Entity.Tone switch
    {
        "in" => "Pump.Ok",
        "out" => "Pump.Accent",
        _ => "Pump.Text",
    };

    // ══ ویرایشِ پارچه از خودِ تاریخچه (۱۴۰۵/۰۷/۱۷) ═══════════════════════════
    //
    // «ویرایشِ تاریخچه هم بزار که اگه خاستم از خودِ تاریخچه‌ها بتونم تغییر بدم.»
    // شش خانه به همین ردیف بسته‌اند؛ هر خانه که بسته شود همان لحظه به
    // ‎ParchaDataService.EditShiftAsync‎ می‌رود (پارچه + پایهٔ ورق + گاوصندوق).
    // ⛔ نشد (ختم کمتر از شروع، پارچهٔ پاک‌شده) ⇒ خانه به عددِ ثبت‌شده برمی‌گردد.

    private string _name = "";
    private int _pump;
    private decimal _start, _end, _price, _debt;

    /// <summary>این ردیف ویرایش می‌شود؟ (فقط پارچه‌ها)</summary>
    public bool CanEdit => Entity.ShiftRef is not null && _saveShift is not null;

    public string ShiftName => _name;
    public int ShiftPump => _pump;
    public decimal ShiftStart => _start;
    public decimal ShiftEnd => _end;
    public decimal ShiftPrice => _price;
    public decimal ShiftDebt => _debt;

    public string EditName
    {
        get => _name.Length == 0 ? "—" : _name;
        set { var v = (value ?? "").Trim(); if (v == "—") v = ""; if (v.Length == 0 || v == _name) { Raise(); return; } Set(() => _name = v); }
    }
    public string EditPump
    {
        get => _pump > 0 ? Shamsi.Money(_pump) : "—";
        set { var v = (int)Shamsi.Num(value); if (v == _pump) { Raise(); return; } Set(() => _pump = v); }
    }
    public string EditStart
    {
        get => Shamsi.Money(_start);
        set { if (!Shamsi.IsReadable(value)) { Raise(); return; } var v = Shamsi.Num(value); if (v == _start) { Raise(); return; } Set(() => _start = v); }
    }
    public string EditEnd
    {
        get => Shamsi.Money(_end);
        set { if (!Shamsi.IsReadable(value)) { Raise(); return; } var v = Shamsi.Num(value); if (v == _end) { Raise(); return; } Set(() => _end = v); }
    }
    public string EditPrice
    {
        get => Shamsi.Money(_price);
        set { if (!Shamsi.IsReadable(value)) { Raise(); return; } var v = Shamsi.Num(value); if (v == _price) { Raise(); return; } Set(() => _price = v); }
    }
    public string EditDebt
    {
        get => Shamsi.Money(_debt);
        set { if (!Shamsi.IsReadable(value)) { Raise(); return; } var v = Shamsi.Num(value); if (v == _debt) { Raise(); return; } Set(() => _debt = v); }
    }

    /// <summary>لیتر و پول از همان عددها — همان «فروش × فی»ِ پارچه.</summary>
    public string LitersCell => Entity.ShiftRef is null
        ? (Cells.Count > 10 ? Cells[10] : "")
        : Shamsi.Money(Math.Round(_end - _start)) + " لیتر";
    public string MoneyCell => Entity.ShiftRef is null
        ? (Cells.Count > 11 ? Cells[11] : "")
        : Shamsi.Money(Math.Round((_end - _start) * _price)) + " افغانی";

    private (string, int, decimal, decimal, decimal, decimal)? _undo;

    private void Set(Action change)
    {
        if (!CanEdit) { Raise(); return; }
        _undo ??= (_name, _pump, _start, _end, _price, _debt);
        change();
        Raise();
        SaveGuard.Watch(_saveShift!(this), "ویرایشِ پارچه از تاریخچه");
    }

    /// <summary>ذخیره نشد ⇒ خانه‌ها به همان عددِ ثبت‌شده برمی‌گردند.</summary>
    internal void Revert()
    {
        if (_undo is { } u) (_name, _pump, _start, _end, _price, _debt) = u;
        _undo = null;
        Raise();
    }

    /// <summary>ذخیره شد ⇒ این عددها «ثبت‌شده»‌اند.</summary>
    internal void Committed() => _undo = null;

    private void Raise()
    {
        foreach (var n in new[] { nameof(EditName), nameof(EditPump), nameof(EditStart), nameof(EditEnd),
                                  nameof(EditPrice), nameof(EditDebt), nameof(LitersCell), nameof(MoneyCell) })
            OnPropertyChanged(n);
    }
}

/// <summary>
/// ══ تاریخچه‌ها ═════════════════════════════════════════════════════════════
/// رونوشتِ ‎#sec-history‎ و ‎#sec-historyview‎ — تنها بخشی از نسخهٔ وب که در
/// نیتیو جای خالی مانده بود.
///
/// دو صفحه: فهرستِ کارت‌ها، و تاریخچهٔ یک بخشِ مشخص با کشویِ ماه. **هیچ چیزی
/// این‌جا عوض یا پاک نمی‌شود** — فقط برای دیدن است، همان‌طور که در نسخهٔ وب
/// زیرِ عنوان نوشته شده بود.
/// </summary>
public sealed partial class HistorySectionViewModel : SectionViewModel
{
    /// <summary>گزینهٔ اولِ کشویِ ماه.</summary>
    public const string AllMonths = "همهٔ ماه‌ها";

    private readonly AppHost _host;
    private List<HistoryRow> _feed = new();

    public HistorySectionViewModel(AppHost host)
        : base("history", "history", "تاریخچه‌ها")
    {
        _host = host;
        // «📝 یادداشت این بخش» — همتای ‎.sec-note-box‎ی سایت. کلیدش همان
        // کلیدِ نسخهٔ وب است تا نوت‌های واردشده سرِ جای خودشان بنشینند.
        Notes = new SectionNotesViewModel(Id, host.SectionNotes,
            (m, ok) => host.Toast(m, ok ? ToastKind.Ok : ToastKind.Warn));
        //  کشوی سال + ماه (۱۴۰۵/۰۷/۱۴). «همهٔ ماه‌ها» کلیدِ خالی است و
        //  «‎YYYY/*‎» همهٔ ماه‌های همان سال.
        Picker = new YearMonthPicker(k =>
        {
            var want = k.Length == 0 ? AllMonths : k;
            if (want != Month) Month = want;
        }, AllMonths);
    }

    /// <summary>کشوی سال و ماه. ⛔ صافی همان <see cref="Month"/> است؛ این فقط نما است.</summary>
    public YearMonthPicker Picker { get; }

    /// <summary>ماهِ یک ردیف در صافیِ جاری می‌گنجد؟ «همه»، یک سال (‎YYYY/*‎) یا یک ماه.</summary>
    private bool InMonth(string monthKey)
    {
        if (string.IsNullOrEmpty(Month) || Month == AllMonths) return true;
        if (Month.EndsWith(YearMonthPicker.AllMark, StringComparison.Ordinal))
            return monthKey.StartsWith(Month[..^YearMonthPicker.AllMark.Length] + "/", StringComparison.Ordinal);
        return monthKey == Month;
    }

    /// <summary>برچسبِ صافیِ جاری برای جملهٔ خلاصه.</summary>
    private string MonthWords => Month.EndsWith(YearMonthPicker.AllMark, StringComparison.Ordinal)
        ? "سالِ " + Month[..^YearMonthPicker.AllMark.Length]
        : Shamsi.MonthLabel(Month);

    public ObservableCollection<HistoryCardViewModel> Cards { get; } = new();
    /// <summary>⚠️ ‎BulkRows‎: پر شدنِ جدول یک خبر می‌دهد نه ‎n‎ خبر
    /// — وگرنه جدول به ازای هر ردیف یک‌بار از نو چیده می‌شود و بخش می‌ایستد.</summary>
    public BulkRows<HistoryRowViewModel> Rows { get; } = new();
    public ObservableCollection<string> Months { get; } = new();

    /// <summary>فهرستِ کارت‌ها باز است یا صفحهٔ یک بخش.</summary>
    [ObservableProperty] private bool _isListVisible = true;

    [ObservableProperty] private string _openKind = "";
    [ObservableProperty] private string _pageTitle = "";
    [ObservableProperty] private string _summary = "";
    [ObservableProperty] private string _month = AllMonths;

    public bool IsEmpty => Rows.Count == 0;

    /// <summary>ستون‌های جدولِ همین بخش؛ ‎null‎ یعنی جدولِ کلی.</summary>
    [ObservableProperty] private IReadOnlyList<HistoryCol>? _columns;

    public bool HasColumns => Columns is not null;

    /// <summary>
    /// ⚠️ فقط یکی از دو جدول ردیف دارد — جدولِ پنهان حق ندارد ردیفِ زنده داشته
    /// باشد (قاعدهٔ سرعتِ این ریپو).
    /// </summary>
    public BulkRows<HistoryRowViewModel>? GenericRows => HasColumns ? null : Rows;

    partial void OnColumnsChanged(IReadOnlyList<HistoryCol>? v)
    {
        OnPropertyChanged(nameof(HasColumns));
        OnPropertyChanged(nameof(GenericRows));
    }

    protected override Task LoadAsync() => RefreshCardsAsync();

    /// <summary>
    /// ⛔ فعال‌سازی فقط دفتر را می‌خواند، پس با ‎Version‎ِ دست‌نخورده رد می‌شود:
    /// تا امروز هر برگشت به تاریخچه کلِ فهرستِ یک بخش را از نو می‌خواند و
    /// ماه/تیل/پایه‌ای را که کاربر برگزیده بود به «همه» برمی‌گرداند؛ و دکمهٔ
    /// «🕘 تاریخچه»ی هر بخش اول فهرستِ بخشِ <b>قبلی</b> را می‌خواند، بعد تازه.
    /// </summary>
    public override bool ActivationOnlyReadsDb => true;

    public override async Task OnActivatedAsync()
    {
        // تاریخچه خلاصهٔ دفترهای دیگر است، پس هر بار که کاربر وارد می‌شود
        // باید تازه شود — وگرنه عددها روی لحظهٔ ورودِ اول می‌مانند.
        if (IsListVisible) await RefreshCardsAsync();
        else await OpenAsync(OpenKind, keepFilters: true);
    }

    private async Task RefreshCardsAsync()
    {
        Cards.Clear();
        foreach (var k in await _host.History.CardsAsync())
            Cards.Add(new HistoryCardViewModel(k, this));
    }

    // ══ «از کجا آمدم؟» ══════════════════════════════════════════════════════
    //
    // گزارشِ صاحب ریپو (۱۴۰۵/۰۷/۰۶): «تاریخچه‌ها دکمهٔ برگشت به همان بخشی که
    // از آن رفتم ندارد و می‌رود توی بخشِ تاریخچه‌ها.»
    //
    // ⛔ دکمهٔ «برگشت به تاریخچه‌ها» فقط از صفحهٔ یک بخش به فهرستِ کارت‌ها
    // برمی‌گشت — یعنی کاربری که از «گاوصندوق» آمده بود، در تاریخچه‌ها
    // می‌مانْد و باید از نوار دنبالِ گاوصندوق می‌گشت.

    /// <summary>شناسهٔ بخشی که از آن آمدیم — خالی یعنی از خودِ نوار آمده‌ایم.</summary>
    [ObservableProperty] private string _originId = "";

    /// <summary>«برگشت به گاوصندوق» — نامِ همان بخش، نه یک واژهٔ کلی.</summary>
    [ObservableProperty] private string _originTitle = "";

    public bool HasOrigin => OriginId.Length > 0;

    public string OriginBackText => "‹ برگشت به " + OriginTitle;

    partial void OnOriginIdChanged(string v) => OnPropertyChanged(nameof(HasOrigin));
    partial void OnOriginTitleChanged(string v) => OnPropertyChanged(nameof(OriginBackText));

    /// <summary>ویومدلِ اصلی پیش از رفتن این را می‌نشاند.</summary>
    public void SetOrigin(string id, string title)
    {
        OriginId = id;
        OriginTitle = title;
    }

    /// <summary>
    /// برگشت به همان بخش.
    /// ⚠️ مبدأ پاک می‌شود، وگرنه بارِ بعد که کاربر خودش از نوار به
    /// «تاریخچه‌ها» بیاید، دکمهٔ برگشتِ یک بخشِ بی‌ربط را می‌بیند.
    /// </summary>
    [RelayCommand]
    private async Task BackToOriginAsync()
    {
        var id = OriginId;
        if (id.Length == 0) return;
        OriginId = ""; OriginTitle = "";
        IsListVisible = true;
        IsPageOpen = false;
        OpenKind = "";
        if (_host.GoSection is { } go) await go(id);
    }

    /// <summary>باز کردنِ تاریخچهٔ یک بخش — همان ‎openSectionHistory‎.</summary>
    /// <param name="keepFilters">
    /// ⛔ از ۱۴۰۵/۰۷/۱۶: خواندنِ دوبارهٔ <b>همان</b> بخش (برگشت به تاریخچه‌ها پس از
    /// آن‌که دفتر عوض شد) ماه، تیل و پایه‌ای را که کاربر برگزیده بود نگه می‌دارد —
    /// تا امروز هر بار به «همه» برمی‌گشت. صافی‌ای که دیگر معنا ندارد (ماه یا
    /// پایه‌ای که ردیفی ندارد) به «همه» برمی‌گردد.
    /// </param>
    public async Task OpenAsync(string kind, bool keepFilters = false)
    {
        var keep = keepFilters && IsPageOpen && kind == OpenKind;
        var (month, fuel, pump) = (Month, _fuelPick, _pumpPick);
        OpenKind = kind;
        Columns = HistoryService.ColumnsOf(kind);
        PageTitle = "🕘 تاریخچهٔ " + HistoryService.LabelOf(kind);
        IsListVisible = false;
        IsPageOpen = true;

        _feed = await _host.History.FeedAsync(kind);

        // کشویِ ماه از خودِ ردیف‌ها ساخته می‌شود، نه از تقویم: ماهی که ثبتی
        // ندارد در فهرست نمی‌آید تا کاربر دنبالِ صفحهٔ خالی نگردد.
        Months.Clear();
        Months.Add(AllMonths);
        foreach (var m in _feed.Select(r => r.MonthKey).Where(m => m.Length > 0)
                               .Distinct().OrderByDescending(m => m))
            Months.Add(m);

        //  صافیِ پارچه‌ها از خودِ ردیف‌ها: فقط پایه‌هایی که واقعاً ثبت شده‌اند
        _fuelPick = keep ? fuel : "all";
        _pumpPick = keep && _feed.Any(r => r.Pump == pump) ? pump : 0;
        Pumps.Clear();
        if (kind == "shift")
        {
            Pumps.Add(new PumpChip(0, "همه", this));
            foreach (var n in _feed.Select(r => r.Pump).Where(n => n > 0).Distinct().OrderBy(n => n))
                Pumps.Add(new PumpChip(n, Shamsi.Money(n), this));
        }
        RaiseFilters();

        var want = AllMonths;
        if (keep && month != AllMonths)
        {
            var year = month.EndsWith(YearMonthPicker.AllMark, StringComparison.Ordinal)
                ? month[..^YearMonthPicker.AllMark.Length] : null;
            if (year is not null ? Months.Any(m => m.StartsWith(year + "/", StringComparison.Ordinal))
                                 : Months.Contains(month))
                want = month;
        }
        Month = want;           // خودش ‎Apply‎ را صدا می‌زند
        Picker.Load(Months.Where(m => m != AllMonths), want == AllMonths ? "" : want);
        Apply();
    }

    // ══ «پطرول / دیزل» و «پایهٔ ۱ ۲ ۳ …» — فقط تاریخچهٔ پارچه‌ها (۱۴۰۵/۰۷/۱۳) ══
    //
    // گزارشِ صاحب ریپو: «در تاریخچهٔ پارچه‌ها پطرول و دیزل قاطی‌اند و پایهٔ ۱ و
    // ۲ و ۳ قاطی‌اند؛ یک کادر برای پطرول، یکی برای دیزل، و دکمه‌های کوچکِ
    // شمارهٔ پایه که تاریخچهٔ همان پایه را نشان بدهد — یا همه با هم.»
    // ⛔ فقط صافی است: هیچ پرس‌وجوی تازه‌ای نمی‌زند و هیچ چیزی نمی‌نویسد.
    partial void OnOpenKindChanged(string value) => OnPropertyChanged(nameof(HasShiftFilters));
    private string _fuelPick = "all";
    private int _pumpPick;
    public ObservableCollection<PumpChip> Pumps { get; } = new();
    /// <summary>
    /// ══ ویرایشِ یک پارچه از خودِ تاریخچه (۱۴۰۵/۰۷/۱۷) ══════════════════════
    /// همان ‎EditShiftAsync‎: شیفتِ پارچه، پایهٔ همان شیفت در ورق، فروشِ
    /// گاوصندوق، و بعد حساب‌های همان ورق (فی که عوض شود مبلغِ خودکارِ ردیف‌ها
    /// هم عوض می‌شود). نشد ⇒ خانه برمی‌گردد و می‌گوید چرا.
    /// </summary>
    internal async Task SaveShiftEditAsync(HistoryRowViewModel row)
    {
        if (row.Entity.ShiftRef is not { } r) return;
        try
        {
            var res = await _host.ParchaData.EditShiftAsync(r.ReportId, r.Kind, row.ShiftName,
                row.ShiftPump, row.ShiftStart, row.ShiftEnd, row.ShiftPrice, row.ShiftDebt);
            if (!res.Ok)
            {
                row.Revert();
                _host.Toast("⚠️ " + (res.Error ?? "ذخیره نشد"), ToastKind.Warn);
                return;
            }
            row.Committed();
            foreach (var id in res.WaraqIds)
            {
                try { await _host.WaraqPosting.SyncAsync(id); }
                catch { /* ورق ذخیره شد؛ حساب‌ها با ذخیرهٔ بعدیِ همان ورق درست می‌شوند */ }
            }
        }
        catch
        {
            row.Revert();
            throw;   // ⛔ نگهبانِ ذخیره می‌گوید — بی‌صدا نیست
        }
    }

    public bool HasShiftFilters => OpenKind == "shift";
    public bool IsFuelAll => _fuelPick == "all";
    public bool IsFuelPetrol => _fuelPick == "petrol";
    public bool IsFuelDiesel => _fuelPick == "diesel";

    [RelayCommand]
    private void PickFuel(string? which)
    {
        _fuelPick = which is "petrol" or "diesel" ? which : "all";
        RaiseFilters();
        Apply();
    }

    internal void PickPump(int n)
    {
        _pumpPick = n;
        RaiseFilters();
        Apply();
    }

    internal bool IsPumpPicked(int n) => _pumpPick == n;

    private void RaiseFilters()
    {
        foreach (var n in new[] { nameof(HasShiftFilters), nameof(IsFuelAll), nameof(IsFuelPetrol), nameof(IsFuelDiesel) })
            OnPropertyChanged(n);
        foreach (var c in Pumps) c.Raise();
    }

    private bool Keep(HistoryRow r)
    {
        if (OpenKind != "shift") return true;
        if (_fuelPick == "petrol" && r.Fuel != PumpYaqobi.Domain.Enums.FuelType.Petrol) return false;
        if (_fuelPick == "diesel" && r.Fuel != PumpYaqobi.Domain.Enums.FuelType.Diesel) return false;
        return _pumpPick == 0 || r.Pump == _pumpPick;
    }

    /// <summary>برگشت به کارت‌ها.</summary>
    [RelayCommand]
    private async Task BackAsync()
    {
        IsListVisible = true;
        IsPageOpen = false;
        OpenKind = "";
        Rows.Clear();
        _feed = new List<HistoryRow>();
        await RefreshCardsAsync();
    }

    partial void OnMonthChanged(string value) => Apply();

    private void Apply()
    {
        var picked = _feed.Where(r => InMonth(r.MonthKey)).Where(Keep).ToList();

        using (Rows.Batch())
        {
            Rows.Clear();
            var i = 0;
            foreach (var r in picked) Rows.Add(new HistoryRowViewModel(r, ++i, r.ShiftRef is null ? null : SaveShiftEditAsync));
        }

        Summary = picked.Count == 0
            ? (Month == AllMonths ? "هنوز چیزی در این بخش ثبت نشده" : "در این ماه چیزی ثبت نشده")
            : "مجموعاً " + Shamsi.Money(picked.Count) + " ردیف"
              + (Month == AllMonths ? "" : " در " + MonthWords)
              + " — از تازه به کهنه";

        OnPropertyChanged(nameof(IsEmpty));
    }
}
