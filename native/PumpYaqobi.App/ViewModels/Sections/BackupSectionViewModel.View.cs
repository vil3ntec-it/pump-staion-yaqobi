using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PumpYaqobi.App.Services;
using PumpYaqobi.App.Update;
using PumpYaqobi.Domain;
using PumpYaqobi.Application.Localization;
using PumpYaqobi.Services.Data;

namespace PumpYaqobi.App.ViewModels.Sections;

/// <summary>
/// ══ «👁 مشاهدهٔ بکاپ» و «♻ بازیابیِ بکاپ» — دو کارِ جدا (۱۴۰۵/۰۷/۱۸) ══════════════
///
/// خواستهٔ صاحب ریپو: «امکانِ مشاهدهٔ بکاپ برای دیدنِ محتوای بکاپ بدون تغییر در اطلاعاتِ
/// فعلی، و امکانِ بازیابیِ بکاپ… پیش از جایگزینی مشخص کند چه اطلاعاتی جایگزین می‌شود و
/// قبل از آن یک بکاپِ ایمنی بگیرد.»
///
///   • مشاهده: فقط ‎BackupPeeker.Read‎ روی فایل (یا نسخهٔ بازشدهٔ موقتِ ‎.pyq‎/مُهردار/فشرده)
///     — ⛔ هیچ چیزی در دفترِ زنده نوشته نمی‌شود و فایلِ موقت همان لحظه پاک می‌شود.
///   • بازیابی: همان راهِ همیشگیِ ‎RestoreFromAsync‎ (سنجش ⇐ «چه چیزی جایگزین می‌شود» ⇐
///     پرسش ⇐ بکاپِ ایمنی ⇐ جایگزینی). راهِ دومی ساخته نشد.
/// </summary>
public sealed partial class BackupSectionViewModel
{
    /// <summary>همهٔ بکاپ‌های پوشه — روزانه، ایمنی، رمزشده و پشتیبان (‎BackupService.ListAll‎).</summary>
    public ObservableCollection<BackupRowViewModel> AllBackups { get; } = new();

    [ObservableProperty] private bool _isViewing;
    [ObservableProperty] private string _viewTitle = "";
    [ObservableProperty] private string _viewSummary = "";
    [ObservableProperty] private string _viewIntegrity = "";
    [ObservableProperty] private string _viewBrushKey = "Pump.Muted";
    private string? _viewPath;

    public ObservableCollection<BackupPeekTable> ViewTables { get; } = new();

    /// <summary>
    /// فایلِ بکاپ را برای خواندن آماده می‌کند: مُهرِ پمپ، فشرده، رمزشده — همان سه گامِ
    /// بازیابی. هر فایلِ موقت در <paramref name="temps"/> می‌آید تا صدازننده پاکش کند.
    /// </summary>
    private async Task<(string? Plain, string Why)> OpenPlainAsync(string path, List<string> temps)
    {
        var seal = await BackupKeys.OpenForRestoreAsync(path, _host.Backup.SnapshotDir);
        if (!seal.Ok) return (null, seal.Why);
        if (seal.Temp) temps.Add(seal.Path);
        var p = seal.Path;
        if (BackupService.IsGzip(p))
        {
            var plain = await Task.Run(() => BackupService.ExpandIfGzip(p, _host.Backup.SnapshotDir));
            if (plain is null) return (null, "بکاپِ فشرده باز نشد — فایل ناقص یا دست‌خورده است");
            temps.Add(plain); p = plain;
        }
        if (SyncBackup.IsEncrypted(p))
        {
            var dec = await Task.Run(() => _host.Backup.DecryptToTemp(p));
            if (dec is null) return (null, "این پشتیبانِ رمزشده روی این کامپیوتر باز نمی‌شود — یا مالِ کامپیوتر یا کاربرِ دیگری است، یا دست خورده");
            temps.Add(dec); p = dec;
        }
        return (p, "");
    }

    private static void Clean(List<string> temps)
    {
        foreach (var t in temps)
            foreach (var f in new[] { t, t + "-wal", t + "-shm" })
                try { if (File.Exists(f)) File.Delete(f); } catch { }
    }

    /// <summary>«👁 مشاهده» روی یک ردیف.</summary>
    [RelayCommand]
    private Task ViewBackupAsync(BackupRowViewModel? row) =>
        row is null ? Task.CompletedTask : ViewFileAsync(row.Entity.Path);

    /// <summary>«👁 مشاهدهٔ بکاپ از فایل» — هر جای دیسک یا فلش.</summary>
    [RelayCommand]
    private async Task ViewFromFileAsync()
    {
        var path = await Dialogs.PickFileAsync("فایلِ بکاپ برای مشاهده", "بکاپِ پمپ",
            new[] { "*" + FullBackup.Extension, "*.db", "*" + SyncBackup.Extension });
        if (path is not null) await ViewFileAsync(path);
    }

    /// <summary>
    /// ⛔ فقط خواندن. دوبار-کلیک روی ‎.db‎/‎.pyq‎ هم به این‌جا می‌رسد (‎OpenRequest‎) — نه به
    /// بازیابی.
    /// </summary>
    public async Task ViewFileAsync(string path)
    {
        BackupPeek peek;
        var temps = new List<string>();
        Busy = true;
        try
        {
            if (!File.Exists(path)) peek = new BackupPeek(false, "فایل پیدا نشد: " + path, false,
                                                         Array.Empty<BackupPeekTable>(), 0, "", "", "");
            else if (path.EndsWith(FullBackup.Extension, StringComparison.OrdinalIgnoreCase))
            {
                using var info = await Task.Run(() => FullBackup.Read(path, AppVersion.Current));
                peek = info.Ok && info.DbPath is { } db
                    ? await Task.Run(() => BackupPeeker.Read(db))
                    : new BackupPeek(false, info.Why, false, Array.Empty<BackupPeekTable>(), 0, "", "", "");
            }
            else
            {
                var (plain, why) = await OpenPlainAsync(path, temps);
                peek = plain is null
                    ? new BackupPeek(false, why, false, Array.Empty<BackupPeekTable>(), 0, "", "", "")
                    : await Task.Run(() => BackupPeeker.Read(plain));
            }
        }
        finally { Clean(temps); Busy = false; }

        _viewPath = peek.Ok ? path : null;
        ViewTitle = "👁 " + Path.GetFileName(path) + " — فقط دیدن، هیچ چیزی عوض نمی‌شود";
        ViewTables.Clear();
        if (!peek.Ok)
        {
            ViewSummary = "❌ " + peek.Why;
            ViewIntegrity = "";
            ViewBrushKey = "Pump.Danger";
        }
        else
        {
            foreach (var t in peek.Tables) ViewTables.Add(t);
            var dates = new[]
            {
                peek.LatestWaraq.Length > 0 ? "آخرین ورق " + peek.LatestWaraq : "",
                peek.LatestDebtRow.Length > 0 ? "آخرین ردیفِ حساب " + peek.LatestDebtRow : "",
                peek.LatestSafe.Length > 0 ? "آخرین گاوصندوق " + peek.LatestSafe : "",
            }.Where(x => x.Length > 0);
            ViewSummary = Shamsi.Money(peek.Total) + " ردیف در همهٔ بخش‌ها" + (dates.Any() ? " · " + string.Join(" · ", dates) : "");
            ViewIntegrity = peek.Integrity ? "✅ فایل سالم است (integrity_check)" : "⚠️ سنجشِ سلامتِ فایل رد شد — بازیابی‌اش توصیه نمی‌شود";
            ViewBrushKey = peek.Integrity ? "Pump.Ok" : "Pump.Warn";
        }
        IsViewing = true;
    }

    [RelayCommand]
    private void CloseView() { IsViewing = false; _viewPath = null; ViewTables.Clear(); }

    /// <summary>«♻ بازیابیِ همین بکاپ» از پنجرهٔ مشاهده.</summary>
    [RelayCommand]
    private Task RestoreViewedAsync() =>
        _viewPath is null ? Task.CompletedTask : RestoreAnyAsync(_viewPath);

    /// <summary>«♻ بازیابی» روی یک ردیف.</summary>
    [RelayCommand]
    private Task RestoreBackupAsync(BackupRowViewModel? row) =>
        row is null ? Task.CompletedTask : RestoreAnyAsync(row.Entity.Path);

    private Task RestoreAnyAsync(string path) =>
        path.EndsWith(FullBackup.Extension, StringComparison.OrdinalIgnoreCase)
            ? ImportFullFromAsync(path)
            : RestoreFromAsync(path, Path.GetFileName(path));

    /// <summary>«📂» روی یک ردیف — پوشهٔ همان فایل، با خودِ فایل انتخاب‌شده.</summary>
    [RelayCommand]
    private void ShowInFolder(BackupRowViewModel? row)
    {
        if (row is null) return;
        try
        {
            var p = row.Entity.Path;
            if (!File.Exists(p)) { _host.Toast("این فایل دیگر در پوشه نیست", ToastKind.Warn); return; }
            var psi = OperatingSystem.IsWindows()
                ? new System.Diagnostics.ProcessStartInfo("explorer.exe", "/select,\"" + p + "\"")
                : new System.Diagnostics.ProcessStartInfo(Path.GetDirectoryName(p)!) { UseShellExecute = true };
            System.Diagnostics.Process.Start(psi);
        }
        catch { _host.Toast("پوشه باز نشد", ToastKind.Warn); }
    }

    /// <summary>
    /// متنِ «چه چیزی جایگزین می‌شود» — هر جدول: حالا ⇐ پس از بازیابی. نشد ⇒ خالی
    /// (پرسش با همان عددِ کلِ رکوردها می‌ماند).
    /// </summary>
    private string ReplaceText(string plainBackup)
    {
        try
        {
            var live = BackupPeeker.Read(_host.DbPath);
            var bak = BackupPeeker.Read(plainBackup);
            if (!live.Ok || !bak.Ok) return "";
            var lines = BackupPeeker.Diff(live, bak);
            return lines.Count == 0
                ? "شمارِ ردیف‌های هر بخش با حالِ فعلی یکی است."
                : "چه چیزی جایگزین می‌شود (حالا ⇐ پس از بازیابی):\n" + string.Join("\n", lines.Take(14))
                  + (lines.Count > 14 ? "\n…" : "");
        }
        catch { return ""; }
    }
}
