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

    public string LitersText { get => Shamsi.Money(Liters); set => Liters = Shamsi.Num(value); }
    public string TakenText { get => Shamsi.Money(Taken); set => Taken = Shamsi.Num(value); }
    public string DaysText { get => Shamsi.Money(Days); set => Days = Shamsi.Num(value); }
    public string TempText { get => Shamsi.Money(Temp); set => Temp = Shamsi.Num(value); }
    public string ActualText { get => Shamsi.Money(Actual); set => Actual = Shamsi.Num(value); }

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
        RestText = "باقیِ مشتری: " + Fmt2(t.Rest) + " لیتر";
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
    public ObservableCollection<AmanatRowViewModel> Rows { get; } = new();

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

    public string MyPctText { get => Shamsi.Money(MyPct); set => MyPct = Shamsi.Num(value); }

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
        // «📝 یادداشت این بخش» — همتای ‎.sec-note-box‎ی سایت. کلیدش همان
        // کلیدِ نسخهٔ وب است تا نوت‌های واردشده سرِ جای خودشان بنشینند.
        Notes = new SectionNotesViewModel(Id, host.SectionNotes,
            (m, ok) => host.Toast(m, ok ? ToastKind.Ok : ToastKind.Warn));
        _host = host;
        Settings = host.Amanat.Settings();
    }

    public AmanatSettings Settings { get; private set; }

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
