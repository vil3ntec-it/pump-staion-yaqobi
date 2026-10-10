using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PumpYaqobi.App.Services;
using PumpYaqobi.Application.Localization;
using PumpYaqobi.Application.Services;
using PumpYaqobi.Domain.Entities;

namespace PumpYaqobi.App.ViewModels.Sections;

/// <summary>یک مخزنِ شماره‌دار روی صفحه — شماره، ظرفیت، موجودی و «در حالِ کشیدن».</summary>
public sealed class TankCardViewModel
{
    public TankCardViewModel(FuelTank t, TankLevel? lv, decimal threshold)
    {
        Entity = t;
        var cur = lv?.Display ?? 0m;
        Title = "🛢️ مخزنِ " + Shamsi.Money(t.Num);
        CurrentText = Shamsi.Money(Math.Round(cur)) + " لیتر";
        CapacityText = t.Capacity > 0m ? "از " + Shamsi.Money(t.Capacity) : "ظرفیت نوشته نشده";
        FillPercent = (double)(lv?.Percent ?? 0m);
        FillText = FillPercent.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + "%";
        Active = lv?.Active == true;
        IsLow = Active && cur <= threshold;
        IsEmpty = cur <= 0m;
        StateText = IsEmpty ? "⛔ خالی" : Active ? (IsLow ? "⚠️ در حالِ کشیدن — کم مانده" : "▶ در حالِ کشیدن") : "⏸ منتظر";
    }

    public FuelTank Entity { get; }
    public string Title { get; }
    public string CurrentText { get; }
    public string CapacityText { get; }
    public double FillPercent { get; }
    public string FillText { get; }
    public bool Active { get; }
    public bool IsLow { get; }
    public bool IsEmpty { get; }
    public string StateText { get; }
}

/// <summary>یک خانهٔ «چند لیتر از این خرید به این مخزن».</summary>
public sealed partial class TankShareViewModel : ObservableObject
{
    private readonly Action _changed;
    public TankShareViewModel(FuelTank t, decimal liters, Action changed)
    {
        Tank = t; _changed = changed;
        _liters = liters == 0m ? "" : Shamsi.Money(Math.Round(liters, 2), 2);
    }
    public FuelTank Tank { get; }
    public string Label => "مخزنِ " + Shamsi.Money(Tank.Num);
    [ObservableProperty] private string _liters = "";
    partial void OnLitersChanged(string v) => _changed();
    public decimal Value => Shamsi.Num(Liters);
}

/// <summary>
/// ══ مخزن‌های شماره‌دار در بخشِ مخزن (۱۴۰۵/۰۷/۲۲) ══
/// خواستهٔ صاحب ریپو: «خرید را در مخزن‌های مختلف می‌برم؛ تیل از اولی کم می‌شود، کم شد هشدار می‌رود،
/// تمام شد روی دومی می‌رود.» ⛔ موجودیِ کلِ تیل همان کارتِ بالاست و دست نمی‌خورد؛ این‌جا فقط
/// همان موجودی میانِ مخزن‌ها تقسیم و نشان داده می‌شود (‎StorageDataService.TankLevelsAsync‎).
/// </summary>
public sealed partial class StorageSectionViewModel
{
    public ObservableCollection<TankCardViewModel> Tanks { get; } = new();
    [ObservableProperty] private bool _hasTanks;
    [ObservableProperty] private string _tankNewNum = "";
    [ObservableProperty] private string _tankNewCapacity = "";
    [ObservableProperty] private string _tankAlert = "";

    private async Task LoadTanksAsync()
    {
        var defs = await _host.StorageData.TanksAsync(Fuel);
        var levels = defs.Count == 0 ? new List<TankLevel>() : await _host.StorageData.TankLevelsAsync(Fuel);
        var threshold = _host.Settings.GetDecimal(PumpYaqobi.Services.Data.SettingsService.LowStockThreshold, 1000m);
        Tanks.Clear();
        foreach (var t in defs)
            Tanks.Add(new TankCardViewModel(t, levels.FirstOrDefault(l => l.Id == t.Id) is { Id: > 0 } l ? l : null, threshold));
        HasTanks = Tanks.Count > 0;
        TankNewNum = Shamsi.Money(defs.Count == 0 ? 1 : defs.Max(x => x.Num) + 1);
        TankAlert = TankAlertText(Tanks);
    }

    /// <summary>هشدارِ مخزن‌ها: کدام در حالِ کشیدن است، کدام کم مانده و کدام تمام شد.</summary>
    internal static string TankAlertText(IReadOnlyList<TankCardViewModel> tanks)
    {
        if (tanks.Count == 0) return "";
        var act = tanks.FirstOrDefault(t => t.Active);
        var emptied = tanks.TakeWhile(t => !t.Active).Where(t => t.IsEmpty).Select(t => Shamsi.Money(t.Entity.Num)).ToList();
        var bits = new List<string>();
        if (emptied.Count > 0 && act is { IsEmpty: false })
            bits.Add("مخزنِ " + string.Join("، ", emptied) + " تمام شد — حالا از مخزنِ " + Shamsi.Money(act.Entity.Num) + " کشیده می‌شود");
        if (act is { IsLow: true, IsEmpty: false }) bits.Add("⚠️ مخزنِ " + Shamsi.Money(act.Entity.Num) + " کم مانده: " + act.CurrentText);
        if (tanks.All(t => t.IsEmpty)) bits.Add("⛔ همهٔ مخزن‌ها خالی است");
        return string.Join(" · ", bits);
    }

    [RelayCommand]
    private async Task AddTankAsync()
    {
        var num = (int)Shamsi.Num(TankNewNum);
        var err = await _host.StorageData.SaveTankAsync(new FuelTank { Fuel = Fuel, Num = num, Capacity = Shamsi.Num(TankNewCapacity) });
        if (err is not null) { _host.Toast(err, ToastKind.Warn); return; }
        TankNewCapacity = "";
        _host.Toast("✅ مخزنِ " + Shamsi.Money(num) + " ساخته شد", ToastKind.Ok);
        await LoadTanksAsync();
    }

    [RelayCommand]
    private async Task EditTankCapacityAsync(TankCardViewModel? card)
    {
        if (card is null) return;
        var v = await Dialogs.PromptAsync("ظرفیتِ مخزنِ " + Shamsi.Money(card.Entity.Num), "ظرفیت به لیتر:",
                                           card.Entity.Capacity == 0m ? "" : Shamsi.Money(card.Entity.Capacity));
        if (v is null) return;
        var t = card.Entity;
        var err = await _host.StorageData.SaveTankAsync(new FuelTank
        {
            Id = t.Id, Fuel = t.Fuel, Num = t.Num, Note = t.Note, Capacity = Shamsi.Num(v),
            SyncUid = t.SyncUid, CreatedAt = t.CreatedAt,
        });
        if (err is not null) { _host.Toast(err, ToastKind.Warn); return; }
        await LoadTanksAsync();
    }

    [RelayCommand]
    private async Task DeleteTankAsync(TankCardViewModel? card)
    {
        if (card is null) return;
        if (!await Dialogs.ConfirmAsync("حذفِ مخزن", "مخزنِ " + Shamsi.Money(card.Entity.Num) + " حذف شود؟\n"
                + "سهمِ خریدهایش به مخزنِ اول برمی‌گردد. موجودیِ کلِ تیل عوض نمی‌شود.")) return;
        await _host.StorageData.DeleteTankAsync(card.Entity.Id);
        await LoadTanksAsync();
    }

    // ── تقسیمِ یک خرید میانِ مخزن‌ها ───────────────────────────────────────
    [ObservableProperty] private PurchaseRowViewModel? _splitFor;
    public ObservableCollection<TankShareViewModel> SplitShares { get; } = new();
    [ObservableProperty] private string _splitTitle = "";
    [ObservableProperty] private string _splitRemainText = "";
    [ObservableProperty] private bool _splitOver;

    [RelayCommand]
    private async Task OpenSplitAsync(PurchaseRowViewModel? row)
    {
        if (row is null) return;
        var defs = await _host.StorageData.TanksAsync(Fuel);
        if (defs.Count == 0) { _host.Toast("اول در «مخزن‌های شماره‌دار» مخزن بسازید", ToastKind.Warn); return; }
        var fills = await _host.StorageData.FillsAsync(Fuel);
        fills.TryGetValue(row.Entity.Id, out var mine);
        SplitShares.Clear();
        foreach (var t in defs) SplitShares.Add(new TankShareViewModel(t, mine?.GetValueOrDefault(t.Id) ?? 0m, RefreshSplit));
        SplitFor = row;
        SplitTitle = "تقسیمِ خریدِ " + (row.Entity.DateShamsi ?? "") + " — " + Shamsi.Money(Math.Round(row.Entity.Liters)) + " لیتر";
        RefreshSplit();
    }

    private void RefreshSplit()
    {
        if (SplitFor is null) return;
        var left = SplitFor.Entity.Liters - SplitShares.Sum(s => s.Value);
        SplitOver = left < -0.005m;
        SplitRemainText = SplitOver
            ? "⛔ " + Shamsi.Money(Math.Round(-left, 2), 2) + " لیتر بیشتر از خرید"
            : left > 0.005m ? "باقی‌مانده (به مخزنِ اول): " + Shamsi.Money(Math.Round(left, 2), 2) + " لیتر" : "✅ همهٔ خرید تقسیم شد";
    }

    [RelayCommand]
    private async Task SaveSplitAsync()
    {
        if (SplitFor is null) return;
        var err = await _host.StorageData.SetFillsAsync(SplitFor.Entity.Id,
            SplitShares.ToDictionary(s => s.Tank.Id, s => s.Value));
        if (err is not null) { _host.Toast(err, ToastKind.Warn); return; }
        SplitFor = null;
        _host.Toast("✅ خرید میانِ مخزن‌ها تقسیم شد", ToastKind.Ok);
        await LoadTanksAsync();
    }

    [RelayCommand]
    private void CloseSplit() => SplitFor = null;
}
