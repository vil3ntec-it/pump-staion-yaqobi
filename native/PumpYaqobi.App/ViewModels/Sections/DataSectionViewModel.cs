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
}

/// <summary>یک عکسِ بکاپ روی دیسک.</summary>
public sealed class BackupRowViewModel
{
    public BackupRowViewModel(BackupFile f, int index) { Entity = f; Index = index; }

    public BackupFile Entity { get; }
    public int Index { get; }

    public string Day => Entity.Day;
    public string SizeText => Entity.SizeText;
    public string TakenText => Entity.TakenAt.ToString("HH:mm");
}

/// <summary>
/// ══ مدیریت داده‌ها — بندِ ۲۳ ═══════════════════════════════════════════════
/// همان ‎#sec-datamgmt‎ی نسخهٔ وب، با همان سه کار:
///
///   🗑️ **سطلِ زباله** — هر حذفی پانزده روز این‌جا می‌ماند و برمی‌گردد.
///   💾 **بکاپ** — عکسِ خودکارِ روزانه (چهارده تای آخر) + فایلی که کاربر
///      خودش جای امن می‌گذارد.
///   ♻️ **بازگردانی** — از همان عکس‌ها یا از فایلِ کاربر؛ همیشه با یک عکسِ
///      ایمنی از حالِ فعلی، تا اشتباه برگشت‌پذیر بماند.
///
/// ⚠️ بازگردانی کلِ دیتابیس را عوض می‌کند، پس بعدش همهٔ بخش‌ها باید دوباره
/// خوانده شوند — وگرنه صفحه‌ها عددِ دیتابیسِ قبلی را نشان می‌دهند.
/// </summary>
public sealed partial class DataSectionViewModel : SectionViewModel
{
    private readonly AppHost _host;
    private readonly MainViewModel _main;

    public DataSectionViewModel(AppHost host, MainViewModel main)
        : base("datamgmt", "settings", "مدیریت داده‌ها")
    { _host = host; _main = main; }

    public ObservableCollection<TrashRowViewModel> Trash { get; } = new();
    public ObservableCollection<BackupRowViewModel> Backups { get; } = new();

    [ObservableProperty] private TrashRowViewModel? _selectedTrash;
    [ObservableProperty] private BackupRowViewModel? _selectedBackup;
    [ObservableProperty] private string _status = "";
    [ObservableProperty] private bool _busy;

    public bool TrashEmpty => Trash.Count == 0;
    public bool NoBackups => Backups.Count == 0;

    /// <summary>«۱۵ روز» — همان ماندگاریِ نسخهٔ وب، نوشته تا کاربر بداند.</summary>
    public string RetentionText =>
        "هر حذفی " + Shamsi.Money(TrashService.RetentionDays) + " روز این‌جا می‌ماند و بعد خودش پاک می‌شود";

    public bool CanPurge => _host.Permissions.Can(Permission.PurgeData);
    public bool CanRestore => _host.Permissions.Can(Permission.Restore);

    protected override Task LoadAsync() => RefreshAsync();

    public override Task OnActivatedAsync() => RefreshAsync();

    private async Task RefreshAsync()
    {
        // کهنه‌ها اول می‌روند، تا فهرست همانی باشد که واقعاً مانده است.
        try { await _host.Trash.PruneAsync(); } catch { }

        Trash.Clear();
        var i = 0;
        try
        {
            foreach (var t in await _host.Trash.ListAsync())
                Trash.Add(new TrashRowViewModel(t, ++i));
        }
        catch (PermissionDeniedException) { }

        Backups.Clear();
        var j = 0;
        foreach (var b in _host.Backup.List())
            Backups.Add(new BackupRowViewModel(b, ++j));

        OnPropertyChanged(nameof(TrashEmpty));
        OnPropertyChanged(nameof(NoBackups));
    }

    // ── سطلِ زباله ───────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task RestoreTrashAsync()
    {
        var row = SelectedTrash;
        if (row is null) { _host.Toast("اول یک قلم را انتخاب کنید", ToastKind.Warn); return; }

        var why = await _host.Trash.RestoreAsync(row.Entity.Id);
        if (why is not null) { _host.Toast("❌ " + why, ToastKind.Error); return; }

        _host.Toast("↩️ «" + row.Label + "» برگشت", ToastKind.Ok);
        await RefreshAsync();
        await _main.ReloadAllAsync();
    }

    [RelayCommand]
    private async Task PurgeTrashAsync()
    {
        var row = SelectedTrash;
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
    private async Task EmptyTrashAsync()
    {
        if (Trash.Count == 0) { _host.Toast("سطلِ زباله خالی است", ToastKind.Info); return; }

        if (!await Dialogs.ConfirmAsync("خالی کردنِ سطل",
                "همهٔ " + Shamsi.Money(Trash.Count) + " قلم برای همیشه پاک شوند؟ این کار برگشت ندارد."))
            return;

        int n;
        try { n = await _host.Trash.EmptyAsync(); }
        catch (PermissionDeniedException) { _host.Toast("❌ این کار فقط از مدیر برمی‌آید", ToastKind.Error); return; }

        _host.Toast("🗑️ " + Shamsi.Money(n) + " قلم پاک شد", ToastKind.Error);
        await RefreshAsync();
    }

    // ── بکاپ ─────────────────────────────────────────────────────────────────

    /// <summary>«💾 ذخیرهٔ فایلِ بکاپ» — همان ‎downloadBackupNow‎.</summary>
    [RelayCommand]
    private async Task SaveBackupAsync()
    {
        var target = await Dialogs.SaveFileAsync("فایلِ بکاپ کجا ذخیره شود؟",
                                                 BackupService.SuggestedFileName(),
                                                 "بکاپِ پمپ", new[] { "*.db" });
        if (target is null) return;

        Busy = true;
        try
        {
            await Task.Run(() => _host.Backup.WriteSnapshot(target));
            Status = "آخرین فایلِ بکاپ: " + target;
            _host.Toast("💾 فایلِ بکاپ ساخته شد — جای امن نگهش دارید", ToastKind.Ok);
        }
        catch (PermissionDeniedException) { _host.Toast("❌ بکاپ فقط از مدیر برمی‌آید", ToastKind.Error); }
        catch (Exception ex) { _host.Toast("❌ بکاپ گرفته نشد: " + ex.Message, ToastKind.Error); }
        finally { Busy = false; }
    }

    /// <summary>یک عکسِ همین حالا، کنارِ عکس‌های روزانه.</summary>
    [RelayCommand]
    private async Task SnapshotNowAsync()
    {
        Busy = true;
        try
        {
            var path = await Task.Run(() => _host.Backup.SnapshotToday());
            _host.Toast(path is null ? "❌ عکس گرفته نشد" : "📸 عکسِ امروز تازه شد",
                        path is null ? ToastKind.Error : ToastKind.Ok);
            await RefreshAsync();
        }
        finally { Busy = false; }
    }

    // ── بازگردانی ────────────────────────────────────────────────────────────

    [RelayCommand]
    private Task RestoreSelectedAsync()
    {
        var row = SelectedBackup;
        if (row is null) { _host.Toast("اول یک عکس را انتخاب کنید", ToastKind.Warn); return Task.CompletedTask; }
        return RestoreFromAsync(row.Entity.Path, "عکسِ " + row.Day);
    }

    [RelayCommand]
    private async Task RestoreFromFileAsync()
    {
        var path = await Dialogs.PickFileAsync("فایلِ بکاپ را انتخاب کنید",
                                               "بکاپِ پمپ", new[] { "*.db" });
        if (path is null) return;
        await RestoreFromAsync(path, Path.GetFileName(path));
    }

    private async Task RestoreFromAsync(string path, string what)
    {
        var records = BackupService.Inspect(path);
        if (records < 0)
        {
            _host.Toast("❌ این فایل بکاپِ این برنامه نیست", ToastKind.Error);
            return;
        }

        // ⚠️ عددِ رکوردها **پیش از** پرسیدن نشان داده می‌شود: کاربر باید بداند
        // دارد چه چیزی را جایگزینِ چه چیزی می‌کند، نه بعد از اینکه دیر شد.
        if (!await Dialogs.ConfirmAsync("بازگردانی",
                $"همهٔ حساب‌های فعلی با «{what}» ({Shamsi.Money(records)} رکورد) جایگزین شود؟\n"
                + "پیش از این کار، از حالِ فعلی یک عکسِ ایمنی گرفته می‌شود."))
            return;

        Busy = true;
        RestoreOutcome outcome;
        try { outcome = await Task.Run(() => _host.Backup.Restore(path)); }
        catch (PermissionDeniedException)
        {
            _host.Toast("❌ بازگردانی فقط از مدیر برمی‌آید", ToastKind.Error);
            return;
        }
        finally { Busy = false; }

        _host.Toast((outcome.Ok ? "" : "❌ ") + outcome.Message,
                    outcome.Ok ? ToastKind.Ok : ToastKind.Error);

        if (outcome.SafetyCopy is not null)
            Status = "عکسِ ایمنیِ حالِ قبلی: " + outcome.SafetyCopy;

        if (!outcome.Ok) return;

        await RefreshAsync();
        await _main.ReloadAllAsync();
    }
}
