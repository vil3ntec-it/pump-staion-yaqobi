using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;
using CommunityToolkit.Mvvm.Input;
using PumpYaqobi.App.Services;
using PumpYaqobi.Application.Localization;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Services.Data;

namespace PumpYaqobi.App.ViewModels.Sections;

/// <summary>یک تعارضِ باز — برای کارتِ «⚠️ تعارض‌ها» (شورا ب۱).</summary>
public sealed class ConflictRowViewModel
{
    public ConflictRowViewModel(SyncConflict c, BackupSectionViewModel owner) { Entity = c; Owner = owner; }
    public SyncConflict Entity { get; }
    /// <summary>فرمان‌ها از خودِ ردیف (درسِ کارتِ قرض‌دار: نه پیمودنِ درخت).</summary>
    public BackupSectionViewModel Owner { get; }

    private static readonly Dictionary<string, string> Names = new(StringComparer.Ordinal)
    {
        ["SafeEntry"] = "گاوصندوق", ["Expense"] = "مصارف", ["ExchangeRow"] = "صرافی", ["RetailRow"] = "چکنه",
        ["DebtRow"] = "ردیفِ حسابِ قرض‌دار", ["Debtor"] = "قرض‌دار", ["DebtAccount"] = "حسابِ قرض‌دار",
        ["WaraqPump"] = "پایهٔ ورق", ["WaraqTransaction"] = "ردیفِ ورق", ["WaraqEntry"] = "ورق",
        ["ShiftData"] = "شیفتِ پارچه", ["ParchaReport"] = "پارچه", ["FuelPurchase"] = "خریدِ مخزن",
        ["CompanyRow"] = "ردیفِ شرکت", ["TilCompany"] = "شرکت", ["Invoice"] = "فاکتور", ["StaffMember"] = "کارمند",
    };

    public string Title => (Names.GetValueOrDefault(Entity.TableName) ?? Entity.TableName) + " · " + Entity.Field;

    private static string Show(string? raw)
    {
        try
        {
            using var d = JsonDocument.Parse(raw ?? "null");
            var e = d.RootElement;
            return e.ValueKind switch
            {
                JsonValueKind.String => e.GetString() is { Length: > 0 } s ? s : "(خالی)",
                JsonValueKind.Null => "(خالی)",
                _ => e.ToString(),
            };
        }
        catch (JsonException) { return raw ?? ""; }
    }

    private string LocalValue
    {
        get
        {
            try
            {
                using var d = JsonDocument.Parse(Entity.LocalJson);
                return d.RootElement.TryGetProperty(Entity.Field, out var v) ? Show(v.GetRawText()) : "";
            }
            catch (JsonException) { return ""; }
        }
    }

    /// <summary>کدام در دفتر ماند و کدام کنار رفت — هر دو صریح.</summary>
    public string Detail => Entity.Winner == "remote"
        ? $"در دفتر: «{Show(Entity.RemoteJson)}» (از کامپیوترِ دیگر) · کنار رفت: «{LocalValue}» (همین‌جا)"
        : $"در دفتر: «{LocalValue}» (همین‌جا) · کنار رفت: «{Show(Entity.RemoteJson)}» (از کامپیوترِ دیگر)";

    public string When => DateTimeOffset.FromUnixTimeMilliseconds(Entity.At).ToLocalTime().ToString("HH:mm", CultureInfo.InvariantCulture);
}

public sealed partial class BackupSectionViewModel
{
    // ══ شورا ب۵ — «بکاپ فقط روی همین کامپیوتر است» و آزمونِ بازیابی ══════════
    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty] private bool _onlyLocal;
    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty] private string _drillText = "";
    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty] private string _drillBrushKey = "Pump.Ok";

    private void LoadOffsite()
    {
        try { OnlyLocal = !BackupOffsite.Ok(AppSettings.Load()); } catch { OnlyLocal = false; }
        var d = _host.Drill.Last();
        DrillText = d?.Text ?? "";
        DrillBrushKey = d is { Ok: false } ? "Pump.Danger" : "Pump.Ok";
    }

    /// <summary>«🧪 آزمونِ بازیابی — همین حالا»: عکسِ تازه، باز در پوشهٔ موقت، و سنجش با دفتر.</summary>
    [RelayCommand]
    private async Task DrillNowAsync()
    {
        Busy = true;
        try
        {
            try { await SaveGuard.FlushAllAsync(); } catch { }
            var d = await Task.Run(() => _host.Drill.RunNow());
            DrillText = d.Text;
            DrillBrushKey = d.Ok ? "Pump.Ok" : "Pump.Danger";
            _host.Toast(d.Ok ? "✅ آخرین بکاپ باز شد و با دفتر برابر است" : d.Text, d.Ok ? ToastKind.Ok : ToastKind.Error);
            await RefreshAsync();
        }
        finally { Busy = false; }
    }

    public ObservableCollection<ConflictRowViewModel> Conflicts { get; } = new();
    public bool HasConflicts => Conflicts.Count > 0;
    public string ConflictsTitle => $"⚠️ تعارض‌ها ({Shamsi.Money(Conflicts.Count)}) — یک خانه هم این‌جا و هم روی کامپیوترِ دیگر عوض شده بود";

    private void LoadConflicts()
    {
        Conflicts.Clear();
        try
        {
            foreach (var c in _host.Store.OpenConflicts().Take(200)) Conflicts.Add(new ConflictRowViewModel(c, this));
        }
        catch { /* جدول هنوز ساخته نشده (دفترِ خیلی کهنه) — چیزی برای نشان دادن نیست */ }
        OnPropertyChanged(nameof(HasConflicts));
        OnPropertyChanged(nameof(ConflictsTitle));
    }

    /// <summary>«همین بماند» — فقط ردپا بسته می‌شود؛ هیچ داده‌ای عوض نمی‌شود.</summary>
    [RelayCommand]
    private void KeepConflict(ConflictRowViewModel? row)
    {
        if (row is null) return;
        _host.Store.KeepConflict(row.Entity.Id);
        LoadConflicts();
    }

    /// <summary>«آن یکی را برگردان» — یک opِ تازه، روی همهٔ کامپیوترها؛ هیچ چیزی پاک نمی‌شود.</summary>
    [RelayCommand]
    private void RestoreConflict(ConflictRowViewModel? row)
    {
        if (row is null) return;
        var ok = _host.Store.RestoreConflict(row.Entity.Id);
        _host.Toast(ok ? "↩ مقدارِ دیگر برگشت و به کامپیوترهای دیگر هم می‌رود" : "❌ نشد — آن ردیف دیگر نیست",
                    ok ? ToastKind.Ok : ToastKind.Error);
        if (ok) _host.Sync.Nudge();
        LoadConflicts();
    }
}
