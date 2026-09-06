using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PumpYaqobi.App.Services;
using PumpYaqobi.Application.Localization;
using PumpYaqobi.Application.Services;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;

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
public sealed partial class AmanatAccountViewModel : ObservableObject
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

    public void Recalc()
    {
        var t = _host.AmanatCalc.AccountCalc(Entity, _section.Settings, AutoDaysOf);
        TotalLiters = Shamsi.Money(Math.Round(t.Liters, 2));
        TotalLoss = Shamsi.Money(Math.Round(t.Loss, 2));
        TotalRest = Shamsi.Money(Math.Round(t.Rest, 2));
        TotalShare = Shamsi.Money(Math.Round(t.Share, 2));
    }

    public async Task SaveRowAsync(AmanatRow r)
    {
        await _host.Amanat.SaveRowAsync(r);
        Recalc();
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
}

/// <summary>
/// ══ بخشِ تیل امانت ══════════════════════════════════════════════════════════
/// هر حساب یک کادر با ردیف‌های محموله. بخار، سهم و «فیصدیِ لازم» از سرویسی
/// می‌آیند که با ۴۰۰ ردیفِ گرفته‌شده از خودِ نسخهٔ وب آزموده شده.
/// </summary>
public sealed partial class AmanatSectionViewModel : SectionViewModel
{
    private readonly AppHost _host;

    public AmanatSectionViewModel(AppHost host) : base("amanat", "amanat", "تیل امانت")
    {
        _host = host;
        Settings = host.Amanat.Settings();
    }

    public AmanatSettings Settings { get; private set; }

    public ObservableCollection<AmanatAccountViewModel> Accounts { get; } = new();

    [ObservableProperty] private bool _isDiesel;

    public FuelType Fuel => IsDiesel ? FuelType.Diesel : FuelType.Petrol;

    partial void OnIsDieselChanged(bool v) => _ = LoadAsync();

    protected override async Task LoadAsync()
    {
        Settings = _host.Amanat.Settings();
        Accounts.Clear();
        foreach (var a in await _host.Amanat.ListAsync(Fuel))
            Accounts.Add(new AmanatAccountViewModel(_host, a, this));
    }

    [RelayCommand]
    private async Task AddAccountAsync()
    {
        var a = await _host.Amanat.AddAsync(Fuel);
        Accounts.Add(new AmanatAccountViewModel(_host, a, this));
    }

    public async Task DeleteAccountAsync(AmanatAccountViewModel vm)
    {
        await _host.Amanat.DeleteAccountAsync(vm.Entity.Id);
        Accounts.Remove(vm);
    }
}
