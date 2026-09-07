using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
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
/// </summary>
public sealed partial class StaffShortSectionViewModel : SectionViewModel
{
    private readonly AppHost _host;

    public StaffShortSectionViewModel(AppHost host)
        : base("staffshort", "attendance", "کمبودی کارمندان") => _host = host;

    public ObservableCollection<StaffShortRowViewModel> Rows { get; } = new();
    public ObservableCollection<StaffSettleRowViewModel> Settles { get; } = new();

    [ObservableProperty] private StaffShortRowViewModel? _selected;
    [ObservableProperty] private string _amount = "";
    [ObservableProperty] private string _totalShort = "—";
    [ObservableProperty] private string _totalExcess = "—";

    public bool IsEmpty => Rows.Count == 0;
    public bool HasSettles => Settles.Count > 0;

    protected override Task LoadAsync() => RefreshAsync();

    public override Task OnActivatedAsync() => RefreshAsync();

    private async Task RefreshAsync()
    {
        var rows = await _host.Tools.StaffShortAsync();
        var settles = await _host.Tools.SettlesAsync();

        var keep = Selected?.Row.Key;
        Rows.Clear();
        foreach (var r in rows) Rows.Add(new StaffShortRowViewModel(r));
        Selected = Rows.FirstOrDefault(r => r.Row.Key == keep);

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
            DocDates.Line());
        return Documents.ShowAsync(() => new StaffShortReport(input), "کمبودی کارمندان");
    }

    /// <summary>«💵 رسید کمبودی» — مبلغ از پیش با باقی‌مانده پر می‌شود.</summary>
    [RelayCommand]
    private void PickShort(StaffShortRowViewModel? row)
    {
        if (row is null || !row.CanSettleShort) return;
        Selected = row;
        Amount = Shamsi.Money(row.Row.RemainShort);
    }

    /// <summary>«💸 پرداخت اضافی».</summary>
    [RelayCommand]
    private void PickExcess(StaffShortRowViewModel? row)
    {
        if (row is null || !row.CanSettleExcess) return;
        Selected = row;
        Amount = Shamsi.Money(row.Row.RemainExcess);
    }

    private StaffSettleKind _kind = StaffSettleKind.Short;

    [RelayCommand]
    private async Task SettleShortAsync() => await SettleAsync(StaffSettleKind.Short);

    [RelayCommand]
    private async Task SettleExcessAsync() => await SettleAsync(StaffSettleKind.Excess);

    private async Task SettleAsync(StaffSettleKind kind)
    {
        if (Selected is null) { _host.Toast("اول کارمند را انتخاب کنید", ToastKind.Error); return; }
        _kind = kind;

        var ok = await _host.Tools.SettleAsync(Selected.Row, kind, Shamsi.Num(Amount));
        if (!ok)
        {
            var cap = kind == StaffSettleKind.Excess
                ? Selected.Row.RemainExcess : Selected.Row.RemainShort;
            _host.Toast("مبلغ باید بین ۱ و باقی‌مانده (" + Shamsi.Money(cap) + ") باشد",
                        ToastKind.Error);
            return;
        }

        _host.Toast(_kind == StaffSettleKind.Excess
            ? "✅ پرداختِ اضافیِ " + Selected.Name + " ثبت شد"
            : "✅ رسیدِ کمبودیِ " + Selected.Name + " ثبت شد", ToastKind.Ok);

        Amount = "";
        await RefreshAsync();
    }

    [RelayCommand]
    private async Task DeleteSettleAsync(StaffSettleRowViewModel? row)
    {
        if (row is null) return;
        if (!await Dialogs.ConfirmAsync("حذفِ این مورد",
                "باقی‌ماندهٔ کمبودی/اضافی دوباره بالا می‌رود. حذف شود؟")) return;
        await _host.Tools.DeleteSettleAsync(row.Entity.Id);
        await RefreshAsync();
    }
}
