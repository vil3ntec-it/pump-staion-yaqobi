using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PumpYaqobi.App.Services;
using PumpYaqobi.Services.Data;

namespace PumpYaqobi.App.ViewModels.Sections;

/// <summary>
/// ══ ۵) همگام‌سازی — جزئیات و «الان همگام کن» ════════════════════════════
///
/// بندِ ۱۱ی پرامپتِ ۲۲: «وضعیتِ Sync با جزئیات (صف، آخرین موفق، خطای
/// آخر، دکمهٔ «الان همگام کن»)، گزارشِ خطا روشن/خاموش.»
///
/// ⛔ <b>کادرِ نشانیِ سرور این‌جا نیست و نباید بیاید.</b> بندِ ۱۱ آن را
/// می‌خواهد ولی قاعدهٔ خودِ صاحب ریپو جلوتر است: نشانی در
/// <see cref="CloudConfig.BaseUrl"/> قفل است و <c>CloudAddressLockTests</c>
/// هر کادری را قدغن کرده. اگر از تنظیمات خوانده می‌شد، هر کسی برنامه را
/// به سرورِ خودش می‌برد و مجوزِ خودش را امضا می‌کرد — یعنی قفلِ اشتراک با
/// یک کادرِ متنی دور می‌خورد. شرحش در <c>native/docs/SYNC-fa.md</c>.
///
/// ⚠️ این صفحه <b>هیچ حلقه‌ای ندارد</b>: عددها فقط سرِ باز شدنِ صفحه و با
/// کلیکِ خودِ کاربر خوانده می‌شوند. صفحهٔ بسته صفر مصرف دارد.
/// </summary>
public sealed partial class SyncSectionViewModel : SectionViewModel
{
    private readonly AppHost _host;

    public SyncSectionViewModel(AppHost host) : base("sync", "settings", "همگام‌سازی")
    {
        _host = host;
        Show();
    }

    [ObservableProperty] private string _light = "خاکستری";
    [ObservableProperty] private string _lightBrushKey = "Pump.Muted";
    [ObservableProperty] private string _reason = "";
    [ObservableProperty] private string _queuedText = "—";
    [ObservableProperty] private string _lastOkText = "—";
    [ObservableProperty] private string _lastErrorText = "—";
    [ObservableProperty] private string _cursorText = "—";
    [ObservableProperty] private string _deviceText = "—";
    [ObservableProperty] private string _schemaText = "—";
    [ObservableProperty] private bool _busy;

    /// <summary>
    /// گزارشِ خطا — بندِ ۲۰٫۸: «با اجازهٔ کاربر».
    ///
    /// ⚠️ پیش‌فرض <b>روشن</b> است و همین‌جا می‌شود خاموشش کرد. دلیلش در
    /// خودِ صفحه نوشته شده تا کاربر بداند چه می‌رود و چه نمی‌رود.
    /// </summary>
    public bool ReportErrors
    {
        get => !AppSettings.Load().ReportErrorsOff;
        set
        {
            var f = AppSettings.Load();
            f.ReportErrorsOff = !value;
            f.Save();
            OnPropertyChanged();
        }
    }

    public override async Task OnActivatedAsync()
    {
        await base.OnActivatedAsync();
        Refresh();
    }

    /// <summary>عددها را از موتور و دفتر می‌خواند — بی هیچ درخواستِ شبکه‌ای.</summary>
    private void Refresh()
    {
        var sync = _host.SyncIfStarted;
        if (sync is null)
        {
            Light = "خاکستری";
            LightBrushKey = "Pump.Muted";
            Reason = "همگام‌سازی هنوز شروع نشده — پس از ورود به حساب خودش راه می‌افتد.";
            return;
        }

        (Light, LightBrushKey) = sync.Light switch
        {
            SyncLight.Synced => ("سبز — همگام", "Pump.Ok"),
            SyncLight.Queued => ("زرد — در صف", "Pump.Warn"),
            SyncLight.Failed => ("سرخ — نشد", "Pump.Danger"),
            _ => ("خاکستری — بند نشده", "Pump.Muted"),
        };
        Reason = sync.Reason;
        QueuedText = sync.Queued.ToString();
        LastOkText = sync.LastOkAt is { } at ? at.ToString("yyyy/MM/dd HH:mm") : "—";
        LastErrorText = sync.LastError.Length > 0 ? sync.LastError : "—";

        var state = new SyncStore(_host.Db).State();
        CursorText = state.Cursor.ToString();
        DeviceText = state.DeviceId.Length > 0 ? state.DeviceId : "—";
        SchemaText = state.Holding
            ? $"برنامه {PumpYaqobi.Persistence.OpLog.SchemaVersion} · سرور {state.ServerSchema} — تغییرها نگه داشته شده‌اند"
            : $"برنامه {PumpYaqobi.Persistence.OpLog.SchemaVersion}"
              + (state.ServerSchema > 0 ? $" · سرور {state.ServerSchema}" : "");
    }

    /// <summary>«الان همگام کن» — بی منتظر ماندنِ نوبتِ حلقه.</summary>
    [RelayCommand]
    private Task SyncNowAsync() => CrashGuard.RunAsync("همگام‌سازی", async () =>
    {
        Busy = true;
        try
        {
            var ok = await _host.Sync.SyncNowAsync();
            Refresh();
            _host.Toast(ok ? "✅ همگام شد" : "⏳ " + Reason, ok ? ToastKind.Ok : ToastKind.Warn);
        }
        finally { Busy = false; }
    });

    /// <summary>تازه کردنِ عددهای روی صفحه.</summary>
    [RelayCommand]
    private void RefreshNow() => Refresh();

    /// <summary>
    /// ══ «بازیابی از سرور» — بندِ ۹ ══════════════════════════════════════
    ///
    /// عکسِ کاملِ دفترِ این پمپ را از سرور می‌گیرد و روی دفترِ محلی
    /// می‌نشاند.
    ///
    /// ⛔ <b>پیش از هر کاری یک پشتیبانِ رمزشدهٔ محلی گرفته می‌شود</b>
    /// (<see cref="SyncBackup"/>). بازگردانی کاری است که نمی‌شود پس گرفت،
    /// مگر نسخه‌ای از «قبلش» باشد.
    /// ⚠️ و پرسیده می‌شود، چون کارِ بزرگی است.
    /// </summary>
    [RelayCommand]
    private Task RestoreAsync() => CrashGuard.RunAsync("بازیابی از سرور", async () =>
    {
        var yes = await Dialogs.ConfirmAsync(
            "بازیابی از سرور",
            "همهٔ حساب‌های این پمپ از سرور گرفته و روی دفترِ همین کامپیوتر نشانده می‌شوند. "
            + "پیش از آن یک پشتیبانِ رمزشده از حالِ فعلی گرفته می‌شود. ادامه می‌دهید؟");
        if (!yes) return;

        Busy = true;
        try
        {
            var copy = SyncBackup.Write(_host.Db, label: "pre-restore");
            if (copy is null)
            {
                _host.Toast("❌ پشتیبانِ پیش از بازیابی گرفته نشد — بازیابی انجام نشد", ToastKind.Error);
                return;
            }

            var file = AppSettings.Load();
            var cloud = new CloudLink(file, () => { file.Save(); return Task.CompletedTask; });
            var (ok, json, why) = await cloud.SyncSnapshotAsync();
            if (!ok)
            {
                _host.Toast("❌ " + why, ToastKind.Error);
                return;
            }

            var report = new SyncStore(_host.Db).RestoreSnapshot(json);
            Refresh();
            _host.Toast($"✅ {report.Applied} ردیف از سرور نشست"
                        + (report.Failed > 0 ? $" · {report.Failed} ننشست" : ""),
                        report.Failed > 0 ? ToastKind.Warn : ToastKind.Ok);
        }
        finally { Busy = false; }
    });

    /// <summary>یک پشتیبانِ رمزشدهٔ محلی — بندِ ۹، «با یک کلیک».</summary>
    [RelayCommand]
    private void BackupNow()
    {
        var path = SyncBackup.Write(_host.Db, label: "manual");
        _host.Toast(path is null ? "❌ پشتیبان گرفته نشد" : "✅ پشتیبانِ رمزشده ساخته شد",
                    path is null ? ToastKind.Error : ToastKind.Ok);
    }
}
