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

namespace PumpYaqobi.App.ViewModels.Sections;

/// <summary>یک محمولهٔ امانت.</summary>
public sealed partial class AmanatRowViewModel : RowViewModel
{
    private readonly AmanatRow _r;
    private readonly AmanatAccountViewModel _owner;

    public AmanatRowViewModel(AmanatRow r, AmanatAccountViewModel owner)
    {
        _r = r; _owner = owner;
        Loading = true;
        _dateShamsi = r.DateShamsi ?? ""; _name = r.Name ?? "";
        _liters = r.Liters ?? 0m; _taken = r.Taken ?? 0m; _days = r.Days ?? 0m;
        _temp = r.Temp ?? 0m; _actual = r.Actual ?? 0m;
        _isClosed = r.State == AmanatRowState.Closed;
        _note = r.Note ?? "";
        Loading = false;
    }

    public AmanatRow Entity => _r;

    [ObservableProperty] private string _dateShamsi = "";
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private decimal _liters;
    [ObservableProperty] private decimal _taken;
    [ObservableProperty] private decimal _days;
    [ObservableProperty] private decimal _temp;
    [ObservableProperty] private decimal _actual;
    [ObservableProperty] private bool _isClosed;
    [ObservableProperty] private string _note = "";

    partial void OnDateShamsiChanged(string v) => Touch();
    partial void OnNameChanged(string v) => Touch();
    partial void OnLitersChanged(decimal v) { Touch(); Refresh(); }
    partial void OnTakenChanged(decimal v) { Touch(); Refresh(); }
    partial void OnDaysChanged(decimal v) { Touch(); Refresh(); }
    partial void OnTempChanged(decimal v) { Touch(); Refresh(); }
    partial void OnActualChanged(decimal v) { Touch(); Refresh(); }
    partial void OnIsClosedChanged(bool v) { Touch(); Refresh(); }
    partial void OnNoteChanged(string v) => Touch();

    private void Refresh()
    {
        foreach (var n in new[] { nameof(LitersText), nameof(TakenText), nameof(DaysText),
                                  nameof(TempText), nameof(ActualText), nameof(LossText),
                                  nameof(LossPctText), nameof(RestText), nameof(ShareText),
                                  nameof(NetText), nameof(AskPctText) })
            OnPropertyChanged(n);
    }

    public void RefreshAll() => Refresh();

    public string LitersText { get => Shamsi.MoneyOrBlank(Liters); set => Liters = Shamsi.Num(value); }
    public string TakenText { get => Shamsi.MoneyOrBlank(Taken); set => Taken = Shamsi.Num(value); }
    public string DaysText { get => Shamsi.MoneyOrBlank(Days); set => Days = Shamsi.Num(value); }
    public string TempText { get => Shamsi.MoneyOrBlank(Temp); set => Temp = Shamsi.Num(value); }
    public string ActualText { get => Shamsi.MoneyOrBlank(Actual); set => Actual = Shamsi.Num(value); }

    private AmanatRowCalc C => _owner.CalcOf(_r);

    public string LossText => Shamsi.Money(Math.Round(C.Loss, 2));
    public string LossPctText => Shamsi.Money(Math.Round(C.LossPct, 3));
    public string RestText => Shamsi.Money(Math.Round(C.Rest, 2));
    public string ShareText => C.TargetL is null ? "—" : Shamsi.Money(Math.Round(C.TargetL.Value, 2));
    public string NetText => C.NetIfMy is null ? "—" : Shamsi.Money(Math.Round(C.NetIfMy.Value, 2));
    public string AskPctText => C.AskPct is null ? "—" : Shamsi.Money(C.AskPct.Value);

    protected override void Apply()
    {
        _r.DateShamsi = DateShamsi;
        _r.Name = Name;
        _r.Liters = Liters == 0m ? null : Liters;
        _r.Taken = Taken == 0m ? null : Taken;
        _r.Days = Days == 0m ? null : Days;
        _r.Temp = Temp == 0m ? null : Temp;
        _r.Actual = Actual == 0m ? null : Actual;
        _r.State = IsClosed ? AmanatRowState.Closed : AmanatRowState.Open;
        _r.Note = Note;
    }

    protected override Task SaveAsync() => _owner.SaveRowAsync(_r);
}

/// <summary>یک کادرِ حسابِ امانت.</summary>
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
        RestText = Fmt2(t.Rest);
        // ‎.pdebt.clear‎ سبز است وقتی چیزی برای مشتری مانده
        RestBrushKey = t.Rest > 0 ? "Pump.Ok" : "Pump.Danger";
        LitersText = (IsDiesel ? "🟤 " : "⛽ ") + Shamsi.Money(Math.Round(t.Liters, 0, MidpointRounding.AwayFromZero));
        LossText = "💨 " + Fmt2(t.Loss);

        // وضعیتِ کارت روی «هدف» سنجیده می‌شود، نه روی «صفر نشدن»
        if (t.Liters == 0m) { State = "➕ هنوز تیلی ثبت نشده"; StateBrushKey = "Pump.Muted"; }
        else if (t.MyPct is null) { State = "⚠️ فیصدی ثبت نشده"; StateBrushKey = "Pump.Warn"; }
        else if (t.Loss <= 0.0001m) { State = "✅ " + Fmt(t.MyPct.Value, 2) + "٪ کافی است"; StateBrushKey = "Pump.Ok"; }
        else if (t.NetIfMy < 0m) { State = "❌ ضرر — " + Fmt(t.AskPct ?? 0m, 1) + "٪ بگیرید"; StateBrushKey = "Pump.Danger"; }
        else { State = "🔸 " + Fmt2(t.Loss) + " لیتر از سهمتان می‌رود"; StateBrushKey = "Pump.Warn"; }

        PctText = t.MyPct is null
            ? "فیصدی: —"
            : "فیصدیِ شما " + Fmt(t.MyPct.Value, 2) + "٪ → بگیرید " + Fmt(t.AskPct ?? 0m, 1) + "٪";
    }

    /// <summary>‎_amFmt2‎ — دو رقمِ اعشار، بدونِ صفرهای بی‌مصرف.</summary>
    private static string Fmt2(decimal v) => Fmt(v, 2);

    private static string Fmt(decimal v, int d) =>
        Shamsi.Money(Math.Round(v, d, MidpointRounding.AwayFromZero));

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

public sealed partial class AmanatAccountViewModel : ObservableObject, IRowBatchHost
{
    private readonly AppHost _host;
    private readonly AmanatSectionViewModel _section;

    public AmanatAccountViewModel(AppHost host, AmanatAccount a, AmanatSectionViewModel section)
    {
        _host = host; _section = section; Entity = a;
        _name = a.Name ?? "";
        _myPct = a.MyPct ?? 0m;
        foreach (var r in a.Rows.OrderBy(r => r.SortIndex).ThenBy(r => r.Id))
        {
            var vm = new AmanatRowViewModel(r, this);
            vm.Recalculated += Recalc;
            Rows.Add(vm);
        }
        Recalc();
    }

    public AmanatAccount Entity { get; }
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

    public async Task DeleteRowsAsync(int count)
    {
        if (count < 1 || Rows.Count < count) return;
        for (var i = 0; i < count; i++) await DeleteRowAsync(Rows[^1]);
    }


    [ObservableProperty] private string _name = "";
    [ObservableProperty] private decimal _myPct;
    [ObservableProperty] private string _totalLiters = "";
    [ObservableProperty] private string _totalLoss = "";
    [ObservableProperty] private string _totalRest = "";
    [ObservableProperty] private string _totalShare = "";

    public string MyPctText { get => Shamsi.MoneyOrBlank(MyPct); set => MyPct = Shamsi.Num(value); }

    partial void OnNameChanged(string v) { Entity.Name = v; Save(); }

    partial void OnMyPctChanged(decimal v)
    {
        Entity.MyPct = v == 0m ? null : v;
        OnPropertyChanged(nameof(MyPctText));
        Save();
        foreach (var r in Rows) r.RefreshAll();
        Recalc();
    }

    private void Save() => _ = _host.Amanat.UpdateAccountAsync(Entity);

    /// <summary>«مدت زمان» اگر دستی نوشته نشده باشد، از تاریخِ ردیف تا امروز.</summary>
    private decimal AutoDaysOf(AmanatRow r) =>
        AmanatService.AutoDays(ParseDate(r.DateShamsi),
                               r.State == AmanatRowState.Closed ? ParseDate(r.CloseDate) : null,
                               DateTime.Now);

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

    public void Recalc()
    {
        var t = _host.AmanatCalc.AccountCalc(Entity, _section.Settings, AutoDaysOf);
        Totals = t;
        TotalLiters = Shamsi.Money(Math.Round(t.Liters, 2));
        TotalLoss = Shamsi.Money(Math.Round(t.Loss, 2));
        TotalRest = Shamsi.Money(Math.Round(t.Rest, 2));
        TotalShare = Shamsi.Money(Math.Round(t.Share, 2));
        OnPropertyChanged(nameof(TotalCells));
    }

    /// <summary>
    /// ردیفِ «جمله»ی ته جدول — همتای ‎&lt;tfoot&gt;‎ی سایت.
    /// ⚠️ حساب‌ها با هم جمع نمی‌شوند: هر حساب «جمله»ی خودش را دارد.
    /// </summary>
    public IReadOnlyList<TotalCell> TotalCells => new[]
    {
        new TotalCell("رسید (لیتر)", TotalLiters),
        new TotalCell("بخار", TotalLoss, "Pump.Warn"),
        new TotalCell("باقیِ تیل", TotalRest, "Pump.Info"),
        new TotalCell("سهمِ من", TotalShare, "Pump.Ok"),
    };

    public async Task SaveRowAsync(AmanatRow r)
    {
        await _host.Amanat.SaveRowAsync(r);
        Recalc();
    }

    /// <summary>
    /// بستهٔ آمادهٔ ورق. حساب‌ها همان‌جا حساب می‌شوند که روی صفحه حساب شده‌اند —
    /// «مدت زمانِ خودکار» تا امروز فقط این‌جا معنی دارد و نباید در لایهٔ سند
    /// دوباره نوشته شود.
    /// </summary>
    public AmanatReportAccount ReportAccount() => new(
        Entity.Name ?? "",
        Entity.Fuel == FuelType.Diesel ? "🟤 دیزل" : "⛽ پطرول",
        Totals,
        Rows.Select(r => new AmanatReportRow(r.Entity, CalcOf(r.Entity))).ToList());

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
            SortIndex = Entity.Rows.Count,
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

    [RelayCommand]
    private async Task DeleteRowAsync(AmanatRowViewModel? row)
    {
        if (row is null) return;
        await _host.Amanat.DeleteRowAsync(row.Entity.Id);
        Entity.Rows.Remove(row.Entity);
        Rows.Remove(row);
        Recalc();
    }

    [RelayCommand]
    private Task DeleteAccountAsync() => _section.DeleteAccountAsync(this);

    /// <summary>«← بازگشت» به شبکهٔ کارت‌ها.</summary>
    [RelayCommand]
    private void Back() => _section.BackCommand.Execute(null);
}

/// <summary>
/// ══ بخشِ تیل امانت ══════════════════════════════════════════════════════════
/// هر حساب یک کادر با ردیف‌های محموله. بخار، سهم و «فیصدیِ لازم» از سرویسی
/// می‌آیند که با ۴۰۰ ردیفِ گرفته‌شده از خودِ نسخهٔ وب آزموده شده.
/// </summary>
public sealed partial class AmanatSectionViewModel : SectionViewModel
{
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
    }

    public AmanatSettings Settings { get; private set; }

    /// <summary>«⚙️ تنظیمات مدیر» — ‎toggleAmanatSettings‎؛ کادرش زیرِ نوارِ فیلتر باز می‌شود.</summary>
    public AmanatSettingsEditorViewModel SettingsEditor { get; }
    [ObservableProperty] private bool _showSettings;

    [RelayCommand]
    private void ToggleSettings()
    {
        ShowSettings = !ShowSettings;
        if (ShowSettings) SettingsEditor.Fill(_host.Amanat.Settings());
    }

    /// <summary>کارت‌های دیده‌شده — پس از جست‌وجو و فیلترِ سوخت.</summary>
    public ObservableCollection<AmanatCardViewModel> Cards { get; } = new();

    [ObservableProperty] private string _search = "";
    /// <summary>‎_amFuelFilter‎ — «all» / «petrol» / «diesel».</summary>
    [ObservableProperty] private string _fuelFilter = "all";
    [ObservableProperty] private AmanatAccountViewModel? _page;
    [ObservableProperty] private string _emptyText = "";

    public bool IsListVisible => Page is null;

    /// <summary>حسابِ امانت — تا باز است، میانبرهای ردیف به آن می‌روند نه به فهرست.</summary>
    public override object? ActivePage => Page;
    public bool IsAll => FuelFilter == "all";
    public bool IsPetrol => FuelFilter == "petrol";
    public bool IsDieselFilter => FuelFilter == "diesel";
    /// <summary>نشانِ «فقط پطرول/دیزل» — تا کاربر نگوید «حسابم کجاست؟».</summary>
    public bool HasFuelChip => FuelFilter != "all";
    public string FuelChipText => FuelFilter == "diesel" ? "🟤 فقط دیزل ✕" : "⛽ فقط پطرول ✕";

    partial void OnPageChanged(AmanatAccountViewModel? v)
    {
        OnPropertyChanged(nameof(IsListVisible));
        // صفحهٔ حساب تمام‌عرض است، مثلِ مودالِ تمام‌صفحهٔ نسخهٔ وب
        IsPageOpen = v is not null;
    }
    partial void OnSearchChanged(string v) => ApplyFilter();

    partial void OnFuelFilterChanged(string v)
    {
        foreach (var n in new[] { nameof(IsAll), nameof(IsPetrol), nameof(IsDieselFilter),
                                  nameof(HasFuelChip), nameof(FuelChipText) })
            OnPropertyChanged(n);
        ApplyFilter();
    }

    [RelayCommand] private void SetFuel(string f) => FuelFilter = f;
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
        _all = list;
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var q = Search.Trim();
        Cards.Clear();
        var seq = 0;
        foreach (var a in _all)
        {
            if (FuelFilter == "petrol" && a.Entity.Fuel != FuelType.Petrol) continue;
            if (FuelFilter == "diesel" && a.Entity.Fuel != FuelType.Diesel) continue;
            if (q.Length > 0 && !(a.Entity.Name ?? "").Contains(q, StringComparison.OrdinalIgnoreCase)) continue;
            Cards.Add(new AmanatCardViewModel(a.Entity, a.Totals, ++seq));
        }

        EmptyText = q.Length > 0 ? $"با «{q}» حسابی پیدا نشد."
            : FuelFilter != "all"
                ? "برای " + (FuelFilter == "diesel" ? "دیزل" : "پطرول") + " حسابی نیست — دکمهٔ «➕ حساب جدید» را بزنید."
                : "هنوز حسابی ثبت نشده — دکمهٔ «➕ حساب جدید» را بزنید.";
    }

    [RelayCommand]
    private void Open(AmanatCardViewModel? card)
    {
        if (card is null) return;
        Page = _all.FirstOrDefault(a => a.Entity.Id == card.Entity.Id);
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

    /// <summary>«➕ حساب جدید» — سوختِ حساب از فیلترِ همان لحظه گرفته می‌شود.</summary>
    [RelayCommand]
    private async Task AddAccountAsync()
    {
        var fuel = FuelFilter == "diesel" ? FuelType.Diesel : FuelType.Petrol;
        var a = await _host.Amanat.AddAsync(fuel);
        var name = await Dialogs.PromptAsync("حساب جدید", "نامِ حساب:");
        if (!string.IsNullOrWhiteSpace(name))
        {
            a.Name = name.Trim();
            await _host.Amanat.UpdateAccountAsync(a);
        }
        _all.Add(new AmanatAccountViewModel(_host, a, this));
        ApplyFilter();
    }

    [RelayCommand]
    private async Task DeleteCardAsync(AmanatCardViewModel? card)
    {
        if (card is null) return;
        if (!await Dialogs.ConfirmAsync("حذفِ حساب",
                $"حسابِ «{card.Name}» با همهٔ محموله‌هایش پاک شود؟")) return;
        var vm = _all.FirstOrDefault(a => a.Entity.Id == card.Entity.Id);
        if (vm is not null) await DeleteAccountAsync(vm);
    }

    public async Task DeleteAccountAsync(AmanatAccountViewModel vm)
    {
        await _host.Amanat.DeleteAccountAsync(vm.Entity.Id);
        _all.Remove(vm);
        if (ReferenceEquals(Page, vm)) Page = null;
        ApplyFilter();
    }
}

/// <summary>یک ردیفِ جدولِ «در هر گرما چقدر تبخیر می‌شود» — ‎_amTempTableHtml‎.</summary>
public sealed class AmanatTempRowViewModel
{
    public AmanatTempRowViewModel(decimal temp, double factor, decimal perMonth, decimal thermalPct, bool near)
    {
        // ‎_amFmt(n, dec)‎ — تا ‎dec‎ رقمِ اعشار، بی صفرهای انتهایی («۱×»، نه «۱٫۰۰×»)
        TempText = F(temp, 0) + "°C";
        FactorText = F((decimal)factor, 2) + "×";
        MonthText = F(perMonth, 3) + "٪";
        YearText = F(perMonth * 12m, 3) + "٪";
        ThermalText = (thermalPct >= 0m ? "+" : "−") + F(Math.Abs(thermalPct), 2) + "٪";
        IsNear = near;
    }

    private static string F(decimal v, int d) =>
        Math.Round(v, d).ToString(d <= 0 ? "0" : "0." + new string('#', d),
                                  System.Globalization.CultureInfo.InvariantCulture);

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

    private static string N(decimal v) => v.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture);

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
            + (s.HandlingPct != 0m ? " + " + Shamsi.Money(Math.Round(s.HandlingPct, 2), 2) + "٪ (اندازه‌گیری و ریخت‌وپاش)" : "");

        var ff = _calc.FuelFactor(FuelType.Petrol, s);
        var rows = new List<AmanatTempRowViewModel>();
        foreach (var t in TempRowsC)
        {
            var f = _calc.TempFactor(t, s);
            var perMonth = s.BasePct * (decimal)f * ff * s.TankFactor;      // ٪ در ماه
            var th = _calc.Thermal(1m, t, FuelType.Petrol, s).Pct;
            rows.Add(new AmanatTempRowViewModel(t, f, perMonth, th, Math.Abs(t - s.DefTemp) < 2.5m));
        }
        TempRows.ResetTo(rows);
    }
}
