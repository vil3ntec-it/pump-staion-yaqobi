using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PumpYaqobi.App.Services;
using PumpYaqobi.Application.Localization;
using PumpYaqobi.Domain;

namespace PumpYaqobi.App.ViewModels.Sections;

/// <summary>یک پشتیبان روی سرورِ حساب — همان که فهرستِ سرور می‌دهد.</summary>
public sealed class CloudBackupRowViewModel
{
    public CloudBackupRowViewModel(CloudBackup b, BackupSectionViewModel owner) { Entity = b; Owner = owner; }
    public CloudBackup Entity { get; }
    /// <summary>فرمان از خودِ ردیف (درسِ کارتِ قرض‌دار: نه پیمودنِ درخت).</summary>
    public BackupSectionViewModel Owner { get; }

    public string Title => (Entity.Kind == "manual" ? "📤 دستی · " : "⏱ خودکار · ")
        + (Entity.Label.Length > 0 ? Entity.Label : Entity.Name);

    public string Detail => When + " · " + Size;

    public string When => Entity.CreatedAt <= 0 ? ""
        : DateTimeOffset.FromUnixTimeMilliseconds(Entity.CreatedAt).ToLocalTime()
              .ToString("yyyy/MM/dd HH:mm", System.Globalization.CultureInfo.InvariantCulture);

    public string Size => Entity.Bytes >= 1024 * 1024
        ? $"{Entity.Bytes / (1024.0 * 1024):0.0} مگابایت"
        : $"{Math.Max(1, Entity.Bytes / 1024)} کیلوبایت";
}

/// <summary>
/// ══ «☁️ بکاپ‌های روی سرورِ حساب» — دیدن و برگرداندن (۱۴۰۵/۰۷/۲۰) ══════════════
///
/// گزارشِ صاحب ریپو: «حسابم را روی کامپیوترِ دیگر بردم یا برنامه را حذف و نصب
/// کردم… هیچ اطلاعاتی که نوشته بودم را سرور نداد.» برنامه هر شش ساعت بکاپ
/// <b>می‌فرستاد</b> ولی <b>هیچ راهی برای پس گرفتن</b> نداشت.
///
/// ⛔ فقط بکاپِ همین پمپ (توکنِ دستگاه)، با هشِ سرور، و از همان راهِ همیشگیِ
/// بازگردانی: سنجشِ فایل، شمارِ رکورد پیش از پرسش، و عکسِ ایمنی از حالِ فعلی.
/// </summary>
public sealed partial class BackupSectionViewModel
{
    public ObservableCollection<CloudBackupRowViewModel> CloudBackups { get; } = new();

    [ObservableProperty] private string _cloudStatus = "";
    [ObservableProperty] private string _cloudStatusBrushKey = "Pump.Muted";
    [ObservableProperty] private bool _cloudBusy;

    private CloudLink NewCloud()
    {
        var s = AppSettings.Load();
        return new CloudLink(s, () => { s.Save(); return Task.CompletedTask; });
    }

    [RelayCommand]
    private async Task LoadCloudBackupsAsync()
    {
        if (CloudBusy) return;
        CloudBusy = true;
        CloudStatus = "در حالِ گرفتنِ فهرست از سرور…";
        CloudStatusBrushKey = "Pump.Muted";
        try
        {
            var cloud = NewCloud();
            if (!cloud.Activated)
            {
                CloudStatus = "اول با همان حساب وارد شوید (پروفایل) تا بکاپ‌های روی سرور دیده شوند.";
                CloudStatusBrushKey = "Pump.Warn";
                return;
            }
            var (ok, items, _, why) = await Task.Run(() => cloud.BackupListAsync());
            CloudBackups.Clear();
            if (!ok)
            {
                CloudStatus = "❌ فهرست نیامد — " + why;
                CloudStatusBrushKey = "Pump.Danger";
                return;
            }
            foreach (var b in items) CloudBackups.Add(new CloudBackupRowViewModel(b, this));
            CloudStatus = items.Count == 0
                ? "روی سرور هنوز هیچ بکاپی از این حساب نیست."
                : $"{items.Count} بکاپ روی سرور — بزرگ‌ترها معمولاً داده‌های بیشتری دارند.";
            CloudStatusBrushKey = items.Count == 0 ? "Pump.Warn" : "Pump.Muted";
        }
        catch (Exception ex)
        {
            CrashGuard.Write("فهرستِ بکاپ‌های سرور", ex);
            CloudStatus = "❌ فهرست نیامد — " + ErrorText.Friendly(ex);
            CloudStatusBrushKey = "Pump.Danger";
        }
        finally { CloudBusy = false; }
    }

    [RelayCommand]
    private async Task RestoreCloudAsync(CloudBackupRowViewModel? row)
    {
        if (row is null || CloudBusy) return;
        CloudBusy = true;
        CloudStatus = "در حالِ گرفتنِ بکاپ از سرور… (" + row.Size + ")";
        CloudStatusBrushKey = "Pump.Muted";
        string? file = null;
        try
        {
            Directory.CreateDirectory(_host.Backup.SnapshotDir);
            file = Path.Combine(_host.Backup.SnapshotDir,
                "tmp-cloud-" + Guid.NewGuid().ToString("N")[..8] + ".db");
            var cloud = NewCloud();
            var res = await Task.Run(() => cloud.BackupDownloadAsync(row.Entity.Id, file));
            if (!res.Ok)
            {
                CloudStatus = "❌ بکاپ از سرور نیامد — " + res.Why;
                CloudStatusBrushKey = "Pump.Danger";
                return;
            }
            CloudStatus = "بکاپ آمد — برای جایگزینی تأیید کنید.";
            CloudBusy = false;
            await RestoreFromAsync(file, "بکاپِ سرور · " + row.When, ownCloud: true);
            CloudStatus = "";
        }
        catch (Exception ex)
        {
            CrashGuard.Write("برگرداندنِ بکاپِ سرور", ex);
            CloudStatus = "❌ " + ErrorText.Friendly(ex);
            CloudStatusBrushKey = "Pump.Danger";
        }
        finally
        {
            CloudBusy = false;
            if (file is not null) try { File.Delete(file); } catch { }
        }
    }
}
