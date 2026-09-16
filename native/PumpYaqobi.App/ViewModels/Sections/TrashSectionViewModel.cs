using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PumpYaqobi.App.Services;
using PumpYaqobi.Application.Localization;
using PumpYaqobi.Application.Security;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Services.Data;

namespace PumpYaqobi.App.ViewModels.Sections;

/// <summary>یک قلم در سطلِ زباله.</summary>
public sealed class TrashRowViewModel
{
    public TrashRowViewModel(TrashItem item, int index) { Entity = item; Index = index; }

    public TrashItem Entity { get; }
    public int Index { get; }

    public string KindText => TrashService.KindLabel(Entity.Kind);
    public string Label => string.IsNullOrWhiteSpace(Entity.Label) ? "—" : Entity.Label!;
    public string DeletedText => Shamsi.Of(Entity.DeletedAtUtc.ToLocalTime());
    public string ByText => Entity.DeletedBy ?? "";

    /// <summary>«۹ روز مانده» — بعدش خودِ سطل پاکش می‌کند.</summary>
    public string DaysLeftText => Shamsi.Money(TrashService.DaysLeft(Entity)) + " روز مانده";

    /// <summary>یک خط برای کارتِ بالایی.</summary>
    public string OneLine => $"{KindText} · {Label}";
}

/// <summary>
/// ══ ۳) سطلِ زباله ══════════════════════════════════════════════════════════
/// صفحهٔ سومِ تنظیمات. خواستهٔ صریحِ صاحب ریپو (۱۴۰۵/۰۶/۲۸):
///
///   «توش حساب‌های که پاک کردم دیده بشه، مثلِ همان بک‌اپ‌ها باشه کشویی… و
///    بعد از ۱۵ روز خودبه‌خود پاک بشه از برنامه و دیگه دیده نشه، و قدرتِ
///    بازگرداندن هم داشته باشم.»
///
/// هر سه از قبل در <see cref="TrashService"/> بودند و دست نخورده‌اند:
/// پانزده روز (<see cref="TrashService.RetentionDays"/>)، پاک شدنِ خودکار
/// (<c>PruneAsync</c>، همین‌جا با هر باز شدنِ صفحه) و بازگرداندن
/// (<c>RestoreAsync</c>). چیزی که تازه است، **شکلِ صفحه** است: تازه‌ترین
/// حذف یک کارتِ بالا و بقیه داخلِ یک کادرِ کشویی.
/// </summary>
public sealed partial class TrashSectionViewModel : SectionViewModel
{
    private readonly AppHost _host;
    private readonly MainViewModel _main;

    public TrashSectionViewModel(AppHost host, MainViewModel main)
        : base("trash", "settings", "سطل زباله")
    { _host = host; _main = main; }

    /// <summary>قلم‌های پس از تازه‌ترین — همان‌هایی که داخلِ کادرِ کشویی‌اند.</summary>
    public ObservableCollection<TrashRowViewModel> Older { get; } = new();

    [ObservableProperty] private TrashRowViewModel? _newest;
    [ObservableProperty] private TrashRowViewModel? _selected;

    /// <summary>
    /// کادرِ کشویی باز است؟ خواستهٔ صاحب ریپو: «در اصل یکی دیده بشه که غیرِ
    /// منظم نشه اون بخش، و یک کادرِ کشویی باشه که توش بقیه‌شونو هم بشه دید».
    /// </summary>
    [ObservableProperty] private bool _showOlder;

    public string ToggleText => ShowOlder ? "▴ بستنِ فهرست" : "▾ دیدنِ بقیه";

    partial void OnShowOlderChanged(bool value) => OnPropertyChanged(nameof(ToggleText));

    [RelayCommand]
    private void ToggleOlder() => ShowOlder = !ShowOlder;


    public bool IsEmpty => Newest is null;
    public bool HasOlder => Older.Count > 0;
    public string OlderText => "پاک‌شده‌های پیشین — " + Shamsi.Money(Older.Count) + " قلم";

    public string RetentionText =>
        "هر چیزی که پاک کنید " + Shamsi.Money(TrashService.RetentionDays)
        + " روز این‌جا می‌ماند و هر وقت خواستید برمی‌گردد؛ بعد از آن خودش پاک می‌شود.";

    public bool CanPurge => _host.Permissions.Can(Permission.PurgeData);

    protected override Task LoadAsync() => RefreshAsync();
    public override Task OnActivatedAsync() => RefreshAsync();
    public override bool ActivationRepeatsLoad => true;

    private async Task RefreshAsync()
    {
        // کهنه‌ها اول می‌روند، تا فهرست همانی باشد که واقعاً مانده است.
        try { await _host.Trash.PruneAsync(); } catch { }

        var all = new List<TrashItem>();
        try { all.AddRange(await _host.Trash.ListAsync()); }
        catch (PermissionDeniedException) { }

        Older.Clear();
        Newest = all.Count > 0 ? new TrashRowViewModel(all[0], 1) : null;
        var i = 1;
        foreach (var t in all.Skip(1)) Older.Add(new TrashRowViewModel(t, ++i));

        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(HasOlder));
        OnPropertyChanged(nameof(OlderText));
    }

    [RelayCommand]
    private Task RestoreNewestAsync() => RestoreAsync(Newest);

    [RelayCommand]
    private Task RestoreSelectedAsync() => RestoreAsync(Selected);

    private async Task RestoreAsync(TrashRowViewModel? row)
    {
        if (row is null) { _host.Toast("اول یک قلم را انتخاب کنید", ToastKind.Warn); return; }

        var why = await _host.Trash.RestoreAsync(row.Entity.Id);
        if (why is not null) { _host.Toast("❌ " + why, ToastKind.Error); return; }

        _host.Toast("↩️ «" + row.Label + "» برگشت", ToastKind.Ok);
        await RefreshAsync();
        await _main.ReloadAllAsync();
    }

    [RelayCommand]
    private async Task PurgeAsync()
    {
        var row = Selected ?? Newest;
        if (row is null) { _host.Toast("اول یک قلم را انتخاب کنید", ToastKind.Warn); return; }

        if (!await Dialogs.ConfirmAsync("حذفِ همیشگی",
                "«" + row.Label + "» برای همیشه پاک شود؟ این کار برگشت ندارد."))
            return;

        try { await _host.Trash.PurgeAsync(row.Entity.Id); }
        catch (PermissionDeniedException) { _host.Toast("❌ این کار فقط از مدیر برمی‌آید", ToastKind.Error); return; }

        _host.Toast("🗑️ برای همیشه پاک شد", ToastKind.Error);
        await RefreshAsync();
    }

    [RelayCommand]
    private async Task EmptyAsync()
    {
        var n = Older.Count + (Newest is null ? 0 : 1);
        if (n == 0) { _host.Toast("سطلِ زباله خالی است", ToastKind.Info); return; }

        if (!await Dialogs.ConfirmAsync("خالی کردنِ سطل",
                "همهٔ " + Shamsi.Money(n) + " قلم برای همیشه پاک شوند؟ این کار برگشت ندارد."))
            return;

        int done;
        try { done = await _host.Trash.EmptyAsync(); }
        catch (PermissionDeniedException) { _host.Toast("❌ این کار فقط از مدیر برمی‌آید", ToastKind.Error); return; }

        _host.Toast("🗑️ " + Shamsi.Money(done) + " قلم پاک شد", ToastKind.Error);
        await RefreshAsync();
    }
}
