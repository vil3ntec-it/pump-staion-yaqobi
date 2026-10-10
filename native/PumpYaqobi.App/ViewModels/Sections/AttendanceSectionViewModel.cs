using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using PumpYaqobi.App.Controls;
using CommunityToolkit.Mvvm.Input;
using PumpYaqobi.App.Services;
using PumpYaqobi.Application.Localization;
using PumpYaqobi.Application.Services;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain;

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
        // ↓ درِ «🕘 تاریخچه»ی همین بخش — شرحش بالای ‎SectionViewModel.HistoryKind‎
        HistoryKind = "attend";

        _host = host;
        _month = Shamsi.ThisMonth();
        //  کشوی سال + ماه — همان ‎YearMonthPicker‎ِ دفترهای ماهانه
        Picker = new YearMonthPicker(k => { if (k.Length > 0 && k != Month) Month = k; });
        Picker.Load(Months, _month);
    }

    /// <summary>کشوی سال و ماه (۱۴۰۵/۰۷/۱۴). ⛔ ماه همان <see cref="Month"/> است؛ این فقط نما است.</summary>
    public YearMonthPicker Picker { get; }

    internal AttendanceService Calc => _host.AttendanceCalc;

    /// <summary>
    /// ماه‌های کشویی — ماهِ جاری، و از ۱۴۰۵/۰۷/۱۸ هر ماهی که حاضری، پرداختِ معاش یا ورق دارد
    /// (‎RefreshMonthsAsync‎). تا امروز فقط دوازده ماهِ اخیرِ ساعت بود: ماهِ کهنه‌تر با داده
    /// برگزیدنی نبود و ماهِ بی‌داده فهرست می‌شد.
    /// </summary>
    public ObservableCollection<string> Months { get; } = new(new[] { Shamsi.ThisMonth() });

    private async Task RefreshMonthsAsync()
    {
        var set = new HashSet<string>(StringComparer.Ordinal) { Shamsi.ThisMonth(), Month };
        foreach (var m in await _host.Attendance.MonthsAsync()) set.Add(m);
        foreach (var m in await _host.Tools.StaffShortMonthsAsync()) if (m.Length == 7) set.Add(m);
        var list = set.Where(m => m.Length == 7).OrderByDescending(m => m, StringComparer.Ordinal).ToList();
        if (list.SequenceEqual(Months)) return;
        Months.Clear();
        foreach (var m in list) Months.Add(m);
        Picker.Load(Months, Month);
    }

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
                new TotalCell("روزها", Shamsi.Money(Rows.Count), column: TotalCell.NoColumn),
                new TotalCell("جمعِ ساعت", Shamsi.Money(Math.Round(hours, 2)), column: "ساعت"),
            };
        }
    }

    public void RefreshTotals() => OnPropertyChanged(nameof(TotalCells));

    [ObservableProperty] private string _month;
    [ObservableProperty] private string _newName = "";
    [ObservableProperty] private string _newSalary = "";

    partial void OnMonthChanged(string v) => _ = Services.CrashGuard.RunAsync("خواندنِ حاضری", LoadAsync);

    /// <summary>
    /// ⛔ پیش از هر کارِ دیتابیسیِ این صفحه: ذخیرهٔ تأخیریِ ردیفی که همین حالا
    /// تایپ شد، اگر <b>پس از</b> «ورود/خروج» می‌نشست، کلِ ردیفِ کهنه را روی
    /// ساعتِ تازه می‌نوشت و آن را پاک می‌کرد.
    /// </summary>
    private async Task FlushRowsAsync()
    {
        foreach (var r in Rows.ToList())
            try { await r.FlushAsync(); } catch { /* نگهبانِ ذخیره می‌گوید */ }
    }

    protected override async Task LoadAsync()
    {
        await FlushRowsAsync();
        var staff = await _host.Attendance.StaffAsync();
        var rows = await _host.Attendance.RowsAsync(Month);
        var pays = await _host.Attendance.PaymentsAsync();
        await RefreshMonthsAsync();

        //  ⛔ کمبودیِ کارتِ هر کارمند فقط از ورق‌های **همین ماه** (۱۴۰۵/۰۷/۱۸) — همان عددِ
        //  «کمبودی کارمندان» (‎StaffShortPeriodAsync‎، منهای رسیدهای همین ماه)، با نامِ نرمال‌شده.
        //  پیش از این از جدولِ ‎StaffShortages‎ می‌آمد که هیچ جای برنامه در آن نمی‌نوشت.
        var (shortRows, _, _) = await _host.Tools.StaffShortPeriodAsync(ProfitPeriod.FromKey(Month));
        var shortBy = shortRows.ToDictionary(r => r.Key, r => r.RemainShort);

        Staff.Clear();
        foreach (var s in staff)
        {
            var m = Calc.Month(s, rows, pays, Array.Empty<StaffShortage>(), Month);
            m = m with { Shortage = shortBy.TryGetValue(PostingService.NormFa(s.Name ?? ""), out var sh) ? sh : 0m };
            Staff.Add(new StaffViewModel(s, m, this));
        }

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
        if (Shamsi.FirstUnreadable(("معاش", NewSalary)) is { } bad) { _host.Toast("«" + bad + "» عدد نیست — ذخیره نشد.", ToastKind.Warn); return; }
        await FlushRowsAsync();
        await _host.Attendance.AddStaffAsync(n, Shamsi.Num(NewSalary));
        NewName = ""; NewSalary = "";
        await LoadAsync();
    }

    [RelayCommand]
    private async Task DeleteStaffAsync(StaffViewModel? s)
    {
        if (s is null) return;
        //  ⛔ پیش از حذف می‌پرسد (۱۴۰۵/۰۷/۱۶) — کارمند با همهٔ حاضری‌هایش می‌رود
        if (!await Dialogs.ConfirmAsync("حذفِ کارمند",
                "«" + s.Entity.Name + "» با همهٔ حاضری‌هایش به سطلِ زباله برود؟ (تا پانزده روز برمی‌گردد)"))
            return;
        await FlushRowsAsync();
        await _host.Attendance.DeleteStaffAsync(s.Entity.Id);
        await LoadAsync();
    }

    [RelayCommand]
    private async Task MarkInAsync(StaffViewModel? s)
    {
        if (s is null) return;
        await FlushRowsAsync();
        await _host.Attendance.MarkAsync(s.Entity.Id, arriving: true, AppClock.Now.ToString("HH:mm"));
        await LoadAsync();
    }

    [RelayCommand]
    private async Task MarkOutAsync(StaffViewModel? s)
    {
        if (s is null) return;
        await FlushRowsAsync();
        await _host.Attendance.MarkAsync(s.Entity.Id, arriving: false, AppClock.Now.ToString("HH:mm"));
        await LoadAsync();
    }

    [RelayCommand]
    private async Task PaySalaryAsync(StaffViewModel? s)
    {
        if (s is null) return;
        await FlushRowsAsync();
        //  ⛔ نتیجه دور ریخته نمی‌شود: پرداختِ دوباره در همان ماه رد می‌شود و
        //  دکمه‌ای که هیچ نگوید، در چشمِ کاربر باگ است.
        var paid = await _host.Attendance.PaySalaryAsync(s.Entity.Id, Month, s.Entity.Salary);
        _host.Toast(paid ? "✅ معاشِ " + s.Entity.Name + " پرداخت شد"
                         : "معاشِ " + s.Entity.Name + " برای همین ماه از قبل پرداخت شده است",
                    paid ? ToastKind.Ok : ToastKind.Warn);
        await LoadAsync();
    }
}
