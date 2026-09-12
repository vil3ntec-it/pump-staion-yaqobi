using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using PumpYaqobi.App.Controls;
using CommunityToolkit.Mvvm.Input;
using PumpYaqobi.App.Services;
using PumpYaqobi.Application.Localization;
using PumpYaqobi.Application.Services;
using PumpYaqobi.Domain.Entities;

namespace PumpYaqobi.App.ViewModels.Sections;

/// <summary>یک کارمند با جمع‌های همان ماه.</summary>
public sealed partial class StaffViewModel : ObservableObject
{
    private readonly AttendanceSectionViewModel _owner;

    public StaffViewModel(StaffMember s, StaffMonth m, AttendanceSectionViewModel owner)
    {
        _owner = owner; Entity = s;
        Name = s.Name ?? "";
        SalaryText = Shamsi.Money(s.Salary);
        Days = m.Days;
        HoursText = Shamsi.Money(Math.Round(m.Hours, 2));
        Paid = m.Paid;
        ShortageText = Shamsi.Money(m.Shortage);
    }

    public StaffMember Entity { get; }
    public string Name { get; }
    public string SalaryText { get; }
    public int Days { get; }
    public string HoursText { get; }
    public bool Paid { get; }
    public bool NotPaid => !Paid;
    public string ShortageText { get; }
    public string PaidText => Paid ? "پرداخت شده" : "پرداخت نشده";
}

/// <summary>یک ردیفِ حاضری.</summary>
public sealed partial class AttendanceRowViewModel : RowViewModel
{
    private readonly AttendanceRow _r;
    private readonly AttendanceSectionViewModel _owner;

    public AttendanceRowViewModel(AttendanceRow r, string staffName, AttendanceSectionViewModel owner)
    {
        _r = r; _owner = owner; StaffName = staffName;
        Loading = true;
        _dateShamsi = r.DateShamsi ?? ""; _in = r.In ?? ""; _out = r.Out ?? ""; _note = r.Note ?? "";
        Loading = false;
    }

    public AttendanceRow Entity => _r;
    public string StaffName { get; }

    [ObservableProperty] private string _dateShamsi = "";
    [ObservableProperty] private string _in = "";
    [ObservableProperty] private string _out = "";
    [ObservableProperty] private string _note = "";

    partial void OnDateShamsiChanged(string v) => Touch();
    partial void OnInChanged(string v) { Touch(); OnPropertyChanged(nameof(HoursText)); }
    partial void OnOutChanged(string v) { Touch(); OnPropertyChanged(nameof(HoursText)); }
    partial void OnNoteChanged(string v) => Touch();

    public string HoursText => Shamsi.Money(Math.Round(_owner.Calc.Hours(_r), 2));

    protected override void Apply()
    {
        _r.DateShamsi = DateShamsi; _r.In = In; _r.Out = Out; _r.Note = Note;
    }

    protected override Task SaveAsync() => _owner.SaveRowAsync(_r);
}

/// <summary>
/// ══ بخشِ حاضری و معاش ═══════════════════════════════════════════════════════
/// کارمندان، حاضریِ روزانه و معاشِ ماه. شیفتِ شب که از نیمه‌شب رد می‌شود،
/// ساعتش درست شمرده می‌شود.
/// </summary>
public sealed partial class AttendanceSectionViewModel : SectionViewModel
{
    private readonly AppHost _host;

    public AttendanceSectionViewModel(AppHost host) : base("attendance", "attendance", "حاضری و معاش")
    {
        _host = host;
        _month = Shamsi.ThisMonth();
    }

    internal AttendanceService Calc => _host.AttendanceCalc;

    /// <summary>دوازده ماهِ اخیر — کافی است، و فهرست را از دیتابیس نمی‌خواهد.</summary>
    public ObservableCollection<string> Months { get; } = new(
        Enumerable.Range(0, 12).Select(i => Shamsi.MonthOf(DateTime.Now.AddMonths(-i))));

    public ObservableCollection<StaffViewModel> Staff { get; } = new();
    /// <summary>⚠️ ‎BulkRows‎: پر شدنِ جدول یک خبر می‌دهد نه ‎n‎ خبر
    /// — وگرنه جدول به ازای هر ردیف یک‌بار از نو چیده می‌شود و بخش می‌ایستد.</summary>
    public BulkRows<AttendanceRowViewModel> Rows { get; } = new();

    /// <summary>ردیفِ «جمله»ی ته جدول — روزها و جمعِ ساعت‌های همین کارمند و ماه.</summary>
    public IReadOnlyList<TotalCell> TotalCells
    {
        get
        {
            var hours = Rows.Sum(r => Calc.Hours(r.Entity));
            return new[]
            {
                new TotalCell("روزها", Shamsi.Money(Rows.Count)),
                new TotalCell("جمعِ ساعت", Shamsi.Money(Math.Round(hours, 2)), column: "ساعت"),
            };
        }
    }

    public void RefreshTotals() => OnPropertyChanged(nameof(TotalCells));

    [ObservableProperty] private string _month;
    [ObservableProperty] private string _newName = "";
    [ObservableProperty] private string _newSalary = "";

    partial void OnMonthChanged(string v) => _ = LoadAsync();

    protected override async Task LoadAsync()
    {
        var staff = await _host.Attendance.StaffAsync();
        var rows = await _host.Attendance.RowsAsync(Month);
        var pays = await _host.Attendance.PaymentsAsync();
        var shorts = await _host.Attendance.ShortagesAsync();

        Staff.Clear();
        foreach (var s in staff)
            Staff.Add(new StaffViewModel(s, Calc.Month(s, rows, pays, shorts, Month), this));

        var byId = staff.ToDictionary(s => s.Id, s => s.Name ?? "");
        using (Rows.Batch())
        {
            Rows.Clear();
            foreach (var r in rows)
            Rows.Add(new AttendanceRowViewModel(r, byId.TryGetValue(r.StaffId, out var n) ? n : "", this));
        }
        RefreshTotals();
    }

    public Task SaveRowAsync(AttendanceRow r)
    {
        RefreshTotals();                      // ساعتِ ردیف عوض شد ⇒ «جمله» هم
        return _host.Attendance.SaveRowAsync(r);
    }

    [RelayCommand]
    private async Task AddStaffAsync()
    {
        var n = NewName.Trim();
        if (n.Length == 0) return;
        await _host.Attendance.AddStaffAsync(n, Shamsi.Num(NewSalary));
        NewName = ""; NewSalary = "";
        await LoadAsync();
    }

    [RelayCommand]
    private async Task DeleteStaffAsync(StaffViewModel? s)
    {
        if (s is null) return;
        await _host.Attendance.DeleteStaffAsync(s.Entity.Id);
        await LoadAsync();
    }

    [RelayCommand]
    private async Task MarkInAsync(StaffViewModel? s)
    {
        if (s is null) return;
        await _host.Attendance.MarkAsync(s.Entity.Id, arriving: true, DateTime.Now.ToString("HH:mm"));
        await LoadAsync();
    }

    [RelayCommand]
    private async Task MarkOutAsync(StaffViewModel? s)
    {
        if (s is null) return;
        await _host.Attendance.MarkAsync(s.Entity.Id, arriving: false, DateTime.Now.ToString("HH:mm"));
        await LoadAsync();
    }

    [RelayCommand]
    private async Task PaySalaryAsync(StaffViewModel? s)
    {
        if (s is null) return;
        await _host.Attendance.PaySalaryAsync(s.Entity.Id, Month, s.Entity.Salary);
        await LoadAsync();
    }
}
