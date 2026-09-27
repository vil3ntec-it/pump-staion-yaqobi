using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PumpYaqobi.App.Services;
using PumpYaqobi.App.Update;
using PumpYaqobi.Application.Localization;
using PumpYaqobi.Application.Security;
using PumpYaqobi.Services.Data;

namespace PumpYaqobi.App.ViewModels.Sections;

/// <summary>یک عکسِ بکاپ روی دیسک.</summary>
public sealed class BackupRowViewModel
{
    public BackupRowViewModel(BackupFile f, int index) { Entity = f; Index = index; }

    public BackupFile Entity { get; }
    public int Index { get; }

    public string Day => Entity.Day;
    public string SizeText => Entity.SizeText;
    public string TakenText => Entity.TakenAt.ToString("HH:mm");

    /// <summary>یک خط برای کارتِ بالایی: «۱۴۰۵/۰۶/۲۸ · ساعت ۰۹:۱۲ · ۴٫۱ مگابایت».</summary>
    public string OneLine => $"{Day} · ساعت {TakenText} · {SizeText}";
}

/// <summary>
/// ══ ۲) بک‌اپ و به‌روزرسانی‌ها ═══════════════════════════════════════════════
/// صفحهٔ دومِ تنظیمات. خواستهٔ صریحِ صاحب ریپو (۱۴۰۵/۰۶/۲۸):
///
///   «بشه بک‌اپ‌ها رو مثلِ یک لیست دید که کشویی، پایینی‌ها هم دیده بشه ولی در
///    اصل یکی دیده بشه که غیرِ منظم نشه اون بخش… و به‌روزرسانی‌ها هم توش دیده
///    بشه و بشه که آپدیت کرد برنامه رو.»
///
/// پس: **تازه‌ترین بکاپ یک کارتِ بالا** است و بقیه داخلِ یک کادرِ کشویی
/// (<c>Expander</c>) می‌مانند تا صفحه شلوغ نشود.
///
/// ⚠️ جدولِ داخلِ کادرِ کشویی عمداً <c>ExcelGrid</c> است، نه <c>ItemsControl</c>:
/// کادرِ بسته یعنی جدولِ نامرئی، و جدولِ نامرئی ردیف‌هایش را پارک می‌کند
/// (<c>ExcelGrid.OnShownChanged</c>). با ‎ItemsControl‎ هر چهارده ردیف تا ابد
/// زنده می‌ماندند — همان چیزی که سنجشِ ‎idle‎ قدغن کرده.
/// </summary>
public sealed partial class BackupSectionViewModel : SectionViewModel
{
    private readonly AppHost _host;
    private readonly MainViewModel _main;
    private readonly UpdateService _update = new();
    private UpdateInfo? _info;
    private string? _downloaded;

    public BackupSectionViewModel(AppHost host, MainViewModel main)
        : base("backups", "settings", "بک‌اپ و به‌روزرسانی‌ها")
    {
        _host = host; _main = main;
        _currentVersion = AppVersion.Current;
        _installDir = UpdateService.InstallDir;
        _installNote = UpdateService.InstallDirWritable
            ? ""
            : "این پوشه اجازهٔ مدیر می‌خواهد؛ هنگامِ به‌روزرسانی ویندوز اجازه می‌پرسد.";

        // نتیجهٔ به‌روزرسانیِ گذشته — چه گرفت چه نگرفت، همین حالا گفته شود.
        var last = UpdateService.ConsumeLastResult();
        _lastFailure = last is null || last.Ok ? "" : last.Message;
        _lastSuccess = last is not null && last.Ok ? last.Message : "";
    }

    // ══ بکاپ‌ها ═══════════════════════════════════════════════════════════════

    /// <summary>بکاپ‌های پس از تازه‌ترین — همان‌هایی که داخلِ کادرِ کشویی‌اند.</summary>
    public ObservableCollection<BackupRowViewModel> Older { get; } = new();

    [ObservableProperty] private BackupRowViewModel? _newest;
    [ObservableProperty] private BackupRowViewModel? _selectedBackup;
    [ObservableProperty] private string _status = "";
    [ObservableProperty] private bool _busy;

    /// <summary>
    /// کادرِ کشویی باز است؟ خواستهٔ صاحب ریپو: «در اصل یکی دیده بشه که غیرِ
    /// منظم نشه اون بخش، و یک کادرِ کشویی باشه که توش بقیه‌شونو هم بشه دید».
    /// </summary>
    [ObservableProperty] private bool _showOlder;

    public string ToggleText => ShowOlder ? "▴ بستنِ فهرست" : "▾ دیدنِ بقیه";

    partial void OnShowOlderChanged(bool value) => OnPropertyChanged(nameof(ToggleText));

    [RelayCommand]
    private void ToggleOlder() => ShowOlder = !ShowOlder;


    public bool NoBackups => Newest is null;
    public bool HasOlder => Older.Count > 0;
    public string OlderText => "بکاپ‌های پیشین — " + Shamsi.Money(Older.Count) + " فایل";

    public bool CanRestore => _host.Permissions.Can(Permission.Restore);

    public string KeepText =>
        "برنامه هر روز که باز شود خودش یک عکس می‌گیرد و "
        + Shamsi.Money(BackupService.KeepSnapshots) + " تای آخر را نگه می‌دارد.";

    protected override Task LoadAsync() => RefreshAsync();
    public override Task OnActivatedAsync() => RefreshAsync();
    public override bool ActivationRepeatsLoad => true;

    /// <summary>بررسیِ خودکارِ نسخه — یک‌بار، و هرگز منتظرش نمی‌مانیم.</summary>
    private bool _autoChecked;

    private Task RefreshAsync()
    {
        // ⚠️ منتظرِ اینترنت نمی‌مانیم: جایی که اینترنت نباشد، صفحه به اندازهٔ
        // مهلتِ اتصال معطل می‌ماند. نتیجه در ‎UpdateStatus‎ می‌نشیند و هر وقت
        // رسید، خودش دیده می‌شود.
        if (!_autoChecked)
        {
            _autoChecked = true;
            _ = Task.Run(async () => { try { await CheckUpdateAsync(); } catch { } });
        }

        Older.Clear();
        var all = _host.Backup.List();
        Newest = all.Count > 0 ? new BackupRowViewModel(all[0], 1) : null;
        var i = 1;
        foreach (var b in all.Skip(1)) Older.Add(new BackupRowViewModel(b, ++i));

        OnPropertyChanged(nameof(NoBackups));
        OnPropertyChanged(nameof(HasOlder));
        OnPropertyChanged(nameof(OlderText));
        //  ⛔ اجازه با هر دیدار دوباره پرسیده می‌شود: پردهٔ لودینگ این صفحه را
        //  **پیش از** ورود (پشتِ صفحهٔ قفل) می‌سازد و آن لحظه هنوز کسی وارد
        //  نشده — پس ‎CanRestore‎ نادرست خوانده می‌شد و دکمه‌های «بازگردانی» و
        //  «آوردنِ فایلِ کامل» برای مدیر هم تا بستنِ برنامه پنهان می‌ماندند
        //  (سنجهٔ ‎fullbackup‎ گرفتش).
        OnPropertyChanged(nameof(CanRestore));
        return Task.CompletedTask;
    }

    /// <summary>«💾 ذخیرهٔ فایلِ بکاپ» — همان ‎downloadBackupNow‎ی نسخهٔ وب.</summary>
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
        catch (Exception ex)
        {
            CrashGuard.Write("بکاپ", ex);
            _host.Toast("❌ بکاپ گرفته نشد: " + ErrorText.Friendly(ex), ToastKind.Error);
        }
        finally { Busy = false; }
    }

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

    [RelayCommand]
    private Task RestoreNewestAsync() =>
        Newest is null ? Task.CompletedTask : RestoreFromAsync(Newest.Entity.Path, "عکسِ " + Newest.Day);

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
        //  ⛔ ردیفی که هنوز در مکثِ ذخیره است اول روی دفترِ **فعلی** بنشیند
        //  (و با عکسِ ایمنی برود) — وگرنه پس از جایگزینی روی دفترِ تازه
        //  می‌نشست، با شماره‌ای که آن‌جا مالِ ردیفِ دیگری است.
        try { await SaveGuard.FlushAllAsync(); } catch { }
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

    // ══ «فایلِ کاملِ برنامه» — همه‌چیز در یک فایل، مثلِ اکسل (۱۴۰۵/۰۷/۱۵) ══════
    //
    //  خواستهٔ صاحب ریپو: «مثلِ اکسل که تمامِ اطلاعات را دارد و یارو خیلی
    //  آسان می‌تواند اطلاعاتش را توی فلش ببرد… تمامِ حساب‌ها و تم‌ها و
    //  تنظیمات… و اگر آن را توی برنامه آوردم، اطلاعاتِ همان فایل همه‌شان
    //  بیاید — با دقت.» شکلِ فایل و سنجش‌هایش: ‎Services.Data.FullBackup‎.

    [ObservableProperty] private string _fullStatus = "";
    [ObservableProperty] private string _fullStatusBrushKey = "Pump.Muted";

    private string PumpName()
    {
        try { return _host.Settings.GetString(SettingsKeys.StationName); } catch { return ""; }
    }

    /// <summary>«📦 ساختنِ فایلِ کامل» — دفتر + تم و تنظیمات، در یک فایل.</summary>
    [RelayCommand]
    private async Task ExportFullAsync()
    {
        var pump = PumpName();
        var target = await Dialogs.SaveFileAsync("فایلِ کاملِ برنامه کجا ذخیره شود؟ (مثلاً فلش)",
                                                 FullBackup.SuggestedFileName(pump),
                                                 "فایلِ کاملِ برنامه", new[] { "*" + FullBackup.Extension });
        if (target is null) return;
        if (!target.EndsWith(FullBackup.Extension, StringComparison.OrdinalIgnoreCase)) target += FullBackup.Extension;

        Busy = true;
        FullStatus = "در حالِ ساختن و سنجیدنِ فایل…";
        FullStatusBrushKey = "Pump.Muted";
        try
        {
            //  هر نوشتهٔ در صف اول روی دفتر بنشیند — «همین حالا» یعنی همین حالا
            try { await SaveGuard.FlushAllAsync(); } catch { }
            var settings = _main.CapturePortableSettings();
            var info = await Task.Run(() =>
                FullBackup.Write(_host.Backup, target, AppVersion.Current, settings, pump));
            FullStatus = $"✅ ساخته و سنجیده شد: {Path.GetFileName(target)}\n"
                       + $"{Shamsi.Money(info.TotalRows)} ردیف در {Shamsi.Money(info.Tables.Count)} جدول · "
                       + SizeText(info.FileBytes) + " · با تم و تنظیمات";
            FullStatusBrushKey = "Pump.Ok";
            _host.Toast("📦 فایلِ کامل ساخته شد — می‌شود روی فلش برد", ToastKind.Ok);
        }
        catch (PermissionDeniedException)
        {
            FullStatus = "";
            _host.Toast("❌ ساختنِ فایل فقط از مدیر برمی‌آید", ToastKind.Error);
        }
        catch (Exception ex)
        {
            CrashGuard.Write("فایلِ کامل", ex);
            FullStatus = "❌ فایل ساخته نشد: " + ErrorText.Friendly(ex);
            FullStatusBrushKey = "Pump.Danger";
            _host.Toast(FullStatus, ToastKind.Error);
        }
        finally { Busy = false; }
    }

    /// <summary>
    /// «📂 آوردنِ فایلِ کامل» — پیش از هر نوشتنی همهٔ سنجش‌ها (قالب، نسخه،
    /// اثرِ انگشت، سلامتِ SQLite، شمارِ هر جدول)؛ بعد پرسش با خلاصهٔ فایل؛
    /// بعد جایگزینی با عکسِ ایمنی؛ و بعد <b>دوباره</b> شمارِ هر جدول در خودِ
    /// برنامه با فایل. ⛔ هر مرحله‌ای نشد ⇒ هیچ چیزی عوض نمی‌شود و گفته می‌شود.
    /// </summary>
    [RelayCommand]
    private async Task ImportFullAsync()
    {
        var path = await Dialogs.PickFileAsync("فایلِ کاملِ برنامه را انتخاب کنید",
                                               "فایلِ کاملِ برنامه", new[] { "*" + FullBackup.Extension });
        if (path is null) return;

        Busy = true;
        FullStatus = "در حالِ خواندن و سنجیدنِ فایل…";
        FullStatusBrushKey = "Pump.Muted";
        FullBackupInfo info;
        try { info = await Task.Run(() => FullBackup.Read(path, AppVersion.Current)); }
        finally { Busy = false; }

        using (info)
        {
            if (!info.Ok)
            {
                FullStatus = "❌ " + info.Why + " — هیچ چیزی عوض نشد";
                FullStatusBrushKey = "Pump.Danger";
                _host.Toast(FullStatus, ToastKind.Error);
                return;
            }

            //  ⚠️ پیش از پرسیدن، کاربر ببیند چه چیزی جای چه چیزی می‌نشیند
            var summary = FullSummary(info);
            if (!await Dialogs.ConfirmAsync("آوردنِ فایلِ کامل",
                    summary + "\n\nهمهٔ اطلاعاتِ این کامپیوتر با این فایل جایگزین شود؟\n"
                    + "پیش از این کار، از حالِ فعلی یک عکسِ ایمنی گرفته می‌شود.",
                    "بله، بیاور"))
            {
                FullStatus = "";
                return;
            }

            Busy = true;
            FullStatus = "در حالِ آوردن…";
            try
            {
                try { await SaveGuard.FlushAllAsync(); } catch { }
                RestoreOutcome outcome;
                try { outcome = await Task.Run(() => FullBackup.Restore(_host.Backup, info)); }
                catch (PermissionDeniedException)
                {
                    FullStatus = "";
                    _host.Toast("❌ آوردن فقط از مدیر برمی‌آید", ToastKind.Error);
                    return;
                }

                if (outcome.SafetyCopy is not null) Status = "عکسِ ایمنیِ حالِ قبلی: " + outcome.SafetyCopy;
                if (!outcome.Ok)
                {
                    FullStatus = "❌ " + outcome.Message;
                    FullStatusBrushKey = "Pump.Danger";
                    _host.Toast(FullStatus, ToastKind.Error);
                    return;
                }

                //  ⛔ «آمد» یعنی شمرده شد — هر جدولِ داده در خودِ برنامه با فایل
                var mismatch = await Task.Run(() => FullBackup.VerifyRestored(_host.Db.DbPath, info));

                var applied = info.SettingsJson is null ? 0 : _main.ApplyPortableSettings(info.SettingsJson).Count;

                await RefreshAsync();
                await _main.ReloadAllAsync();

                if (mismatch is not null)
                {
                    FullStatus = "⚠️ آمد، ولی سنجشِ پس از آوردن ناجور بود — " + mismatch
                               + "\nعکسِ ایمنیِ حالِ قبلی: " + (outcome.SafetyCopy ?? "—");
                    FullStatusBrushKey = "Pump.Danger";
                    _host.Toast(FullStatus, ToastKind.Error);
                    return;
                }

                FullStatus = $"✅ همه آمد و شمرده شد: {Shamsi.Money(info.TotalRows)} ردیف در "
                           + $"{Shamsi.Money(info.Tables.Count)} جدول"
                           + (applied > 0 ? " · تم و تنظیمات هم نشست" : "")
                           + (outcome.SafetyCopy is null ? "" : "\nعکسِ ایمنیِ حالِ قبلی: " + outcome.SafetyCopy);
                FullStatusBrushKey = "Pump.Ok";
                _host.Toast("✅ فایلِ کامل آمد — همهٔ حساب‌ها و تنظیمات", ToastKind.Ok);
            }
            catch (Exception ex)
            {
                CrashGuard.Write("آوردنِ فایلِ کامل", ex);
                FullStatus = "❌ آوردن انجام نشد: " + ErrorText.Friendly(ex);
                FullStatusBrushKey = "Pump.Danger";
                _host.Toast(FullStatus, ToastKind.Error);
            }
            finally { Busy = false; }
        }
    }

    /// <summary>خلاصهٔ فایل برای پرسشِ پیش از آوردن.</summary>
    public static string FullSummary(FullBackupInfo info)
    {
        var who = string.IsNullOrWhiteSpace(info.PumpName) ? "" : $"«{info.PumpName}» · ";
        var lines = new List<string>
        {
            $"{who}ساخته‌شده {info.Shamsi} {info.Time} با نسخهٔ {info.AppVersion}",
            $"قرض‌داران: {Shamsi.Money(info.Rows("Debtors"))} · ردیف‌های حساب: {Shamsi.Money(info.Rows("DebtRows"))}",
            $"ورق‌ها: {Shamsi.Money(info.Rows("WaraqEntries"))} · گاوصندوق: {Shamsi.Money(info.Rows("SafeEntries"))} · فاکتورها: {Shamsi.Money(info.Rows("Invoices"))}",
            $"همه: {Shamsi.Money(info.TotalRows)} ردیف در {Shamsi.Money(info.Tables.Count)} جدول"
                + (info.SettingsJson is null ? "" : " · به‌علاوهٔ تم و تنظیمات"),
        };
        return string.Join("\n", lines);
    }

    private static string SizeText(long bytes) => bytes >= 1024 * 1024
        ? $"{bytes / (1024.0 * 1024):0.0} مگابایت"
        : $"{Math.Max(1, bytes / 1024)} کیلوبایت";

    // ══ «📤 فرستادنِ بکاپ به سرور — همین حالا» (۱۴۰۵/۰۷/۱۵) ═════════════════
    //  «یارو خودش هم اگر خواست بک‌اپ را به سرور بفرستد بتواند — الان دکمه‌ای
    //  نداریم.» همان دورِ شش‌ساعته (‎BackupPusher‎)، همین حالا، با نشانِ «دستی».

    [ObservableProperty] private string _serverStatus = "";
    [ObservableProperty] private string _serverStatusBrushKey = "Pump.Muted";
    [ObservableProperty] private bool _sendingToServer;

    [RelayCommand]
    private async Task SendToServerAsync()
    {
        if (SendingToServer) return;
        SendingToServer = true;
        ServerStatus = "در حالِ فرستادن…";
        ServerStatusBrushKey = "Pump.Muted";
        try
        {
            try { await SaveGuard.FlushAllAsync(); } catch { }
            var pusher = _host.BackupToServer;
            var ok = await pusher.RunOnceAsync(manual: true);
            (ServerStatus, ServerStatusBrushKey) = ServerResult(ok, pusher.LastHomeOk, pusher.LastCloudOk, pusher.LastError);
            _host.Toast(ServerStatus, ok ? ToastKind.Ok : ToastKind.Error);
        }
        catch (Exception ex)
        {
            CrashGuard.Write("فرستادنِ بکاپ", ex);
            ServerStatus = "❌ فرستاده نشد: " + ErrorText.Friendly(ex);
            ServerStatusBrushKey = "Pump.Danger";
        }
        finally { SendingToServer = false; }
    }

    /// <summary>جملهٔ نتیجه — هر مقصد جدا گفته می‌شود، راست.</summary>
    public static (string Text, string Brush) ServerResult(bool ok, bool home, bool cloud, string why)
    {
        var at = DateTime.Now.ToString("HH:mm");
        if (home && cloud) return ($"✅ ساعتِ {at} روی سرورِ خانگی و سرورِ حساب نشست", "Pump.Ok");
        if (home) return ($"✅ ساعتِ {at} روی سرورِ خانگی نشست · سرورِ حساب نه", "Pump.Ok");
        if (cloud) return ($"✅ ساعتِ {at} روی سرورِ حساب نشست · سرورِ خانگی نه", "Pump.Ok");
        return ("❌ به هیچ سروری نرسید" + (string.IsNullOrWhiteSpace(why) ? "" : " — " + why)
                + " · بکاپِ روی همین کامپیوتر سالم است", "Pump.Danger");
    }

    // ══ «💿 نصبِ نسخهٔ تازه از فایل» — بی اینترنت (۱۴۰۵/۰۷/۱۵) ═══════════════
    //  شرح بالای ‎Update.OfflineInstaller‎.

    [ObservableProperty] private string _offlineStatus = "";
    [ObservableProperty] private string _offlineStatusBrushKey = "Pump.Muted";

    [RelayCommand]
    private async Task InstallFromFileAsync()
    {
        var path = await Dialogs.PickFileAsync("فایلِ نصبِ نسخهٔ تازه را انتخاب کنید (PumpYaqobi-Setup.exe)",
                                               "فایلِ نصبِ برنامه", new[] { "*.exe" });
        if (path is null) return;

        var d = OfflineInstaller.Inspect(path, AppVersion.Current);
        OfflineStatus = (d.CanRun ? "" : "❌ ") + d.Message;
        OfflineStatusBrushKey = d.CanRun ? "Pump.Muted" : "Pump.Danger";
        if (!d.CanRun) { _host.Toast(OfflineStatus, ToastKind.Error); return; }

        var ask = d.Kind == OfflineInstaller.Verdict.Same
            ? $"همین نسخه ({d.Version}) دوباره نصب شود (تعمیر)؟"
            : $"برنامه از {AppVersion.Current} به {d.Version} به‌روز شود؟";
        if (!await Dialogs.ConfirmAsync("نصب از فایل",
                ask + "\nبرنامه بسته می‌شود، نصب انجام می‌شود و دوباره باز می‌شود.\n"
                + "حساب‌ها، تم و تنظیمات دست نمی‌خورند.", "بله، نصب کن"))
            return;

        try { await SaveGuard.FlushAllAsync(); } catch { }
        AppSettings.FlushNow();
        if (!UpdateService.LaunchOffline(path))
        {
            OfflineStatus = "❌ نصاب باز نشد — شاید اجازهٔ ویندوز رد شد";
            OfflineStatusBrushKey = "Pump.Danger";
            return;
        }
        OfflineStatus = "نصاب باز شد — برنامه را برای نصب می‌بندد";
        //  ⚠️ برنامه خودش بسته نمی‌شود: نصاب (CloseApplications) می‌پرسد و
        //  می‌بندد. اگر کاربر ویزارد را لغو کند، برنامه سرِ جایش می‌ماند.
    }

    // ══ آوردنِ دادهٔ نسخهٔ وب ══════════════════════════════════════════════════
    //
    // این‌جا ماند چون کارش همان کارِ بکاپ است: یک فایلِ بیرونی که جای
    // حساب‌های فعلی را می‌گیرد، با همان عکسِ ایمنیِ پیش از نوشتن.

    [ObservableProperty] private bool _importBusy;
    [ObservableProperty] private string _importStatus = "";

    [RelayCommand]
    private async Task ImportLegacyAsync()
    {
        var path = await Dialogs.PickJsonAsync();
        if (path is null) return;

        ImportBusy = true;
        ImportStatus = "در حال خواندنِ فایل…";
        try
        {
            var json = await File.ReadAllTextAsync(path);
            var res = await _host.LegacyImport.ImportAsync(json);

            if (!res.Ok && res.Message.Contains("خالی نیست"))
            {
                var yes = await Dialogs.ConfirmAsync(
                    "جایگزینیِ همهٔ حساب‌ها",
                    $"این فایل {res.SourceRecords} رکورد دارد و جای همهٔ حساب‌های فعلی را می‌گیرد. "
                    + "پیش از جایگزینی، یک بکاپِ کامل از دادهٔ فعلی گرفته می‌شود. ادامه؟",
                    "بله، جایگزین کن");
                if (!yes) { ImportStatus = "انصراف داده شد — چیزی عوض نشد"; return; }
                res = await _host.LegacyImport.ImportAsync(json, replaceExisting: true);
            }

            ImportStatus = res.Message
                + (res.BackupPath is not null
                    ? Environment.NewLine + "📦 بکاپِ پیش از مهاجرت: " + res.BackupPath : "")
                + (res.Warnings.Count > 0
                    ? Environment.NewLine + "⚠️ " + string.Join(" · ", res.Warnings.Take(5)) : "");
            _host.Toast(res.Ok ? res.Message : "❌ " + res.Message,
                        res.Ok ? ToastKind.Ok : ToastKind.Error);

            if (res.Ok) _host.Toast("برای دیدنِ حساب‌ها، برنامه را ببندید و باز کنید", ToastKind.Info);
        }
        catch (Exception ex)
        {
            CrashGuard.Write("آوردنِ داده", ex);
            ImportStatus = "❌ " + ErrorText.Friendly(ex);
        }
        finally { ImportBusy = false; }
    }

    // ══ به‌روزرسانیِ برنامه ════════════════════════════════════════════════════
    //
    // ⚠️ کادرِ به‌روزرسانی عمداً هیچ نشانی یا نامِ منبعی نشان نمی‌دهد — فقط
    // «نسخهٔ فعلی»، «نسخهٔ تازه» و دکمه. (خواستهٔ صریحِ صاحب ریپو.)

    [ObservableProperty] private string _currentVersion;
    [ObservableProperty] private string _updateStatus = "";

    /// <summary>
    /// رنگِ جملهٔ حالِ به‌روزرسانی. ⛔ «نرسیدیم» باید سرخ باشد و «به‌روز است»
    /// نه — وگرنه همان دو حال دوباره یکی دیده می‌شوند.
    /// </summary>
    [ObservableProperty] private string _updateStatusBrushKey = "Pump.Muted";
    [ObservableProperty] private bool _updateAvailable;
    [ObservableProperty] private bool _checking;
    [ObservableProperty] private bool _downloading;
    [ObservableProperty] private double _downloadPercent;
    [ObservableProperty] private bool _readyToInstall;
    [ObservableProperty] private string _installDir = "";
    [ObservableProperty] private string _installNote = "";
    [ObservableProperty] private string _packageText = "";
    [ObservableProperty] private string _lastFailure = "";
    [ObservableProperty] private string _lastSuccess = "";

    /// <summary>
    /// ══ «بررسیِ به‌روزرسانی» ══════════════════════════════════════════════
    ///
    /// ⛔ <b>این دکمه هیچ‌وقت بی‌جواب نمی‌ماند.</b> سه چیز جلوی همان یک کلیک
    /// را می‌گرفت و هر سه بسته شد:
    ///
    ///   ۱) بررسیِ خودکار <c>Checking</c> را روشن می‌کرد و دکمه به
    ///      <c>!Checking</c> بسته بود. با شبکهٔ کند، تا ۷۵ ثانیه دکمه
    ///      **خاکستری** بود و کلیکِ کاربر هیچ کاری نمی‌کرد.
    ///   ۲) هیچ <c>catch</c>ی نبود: یک استثنا از فرمان بیرون می‌زد و صفحه
    ///      روی «در حال بررسی…» می‌ماند، برای همیشه.
    ///   ۳) و راهی برای «خودم می‌گیرمش» نبود.
    ///
    /// پس: کلیک، بررسیِ در جریان را **لغو** می‌کند و از نو می‌پرسد (نه
    /// این‌که بی‌صدا رد شود)، و هر شکستی یک جملهٔ سرخ می‌شود.
    /// </summary>
    private CancellationTokenSource? _checkCts;

    // ⛔ AllowConcurrentExecutions **مهم‌ترین خطِ این بخش است** و همان
    // ریشه‌ای است که دکمه را مرده می‌کرد: ‎AsyncRelayCommand‎ی پیش‌فرض تا
    // پایانِ یک اجرا ‎CanExecute‎ را ‎false‎ می‌کند، و این اجرا **یک درخواستِ
    // اینترنتی** است — تا ۷۵ ثانیه با دو در. پس کلیکِ اول دکمه را خاکستری
    // می‌کرد و هر کلیکِ بعدی بی‌صدا بلعیده می‌شد. همان قاعدهٔ
    // ‎DebtSectionViewModel.OpenAsync‎، این بار برای کارتِ به‌روزرسانی.
    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task CheckUpdateAsync()
    {
        // بررسیِ قبلی (خودکار یا دستی) لغو می‌شود — کلیکِ کاربر جلوتر است
        var old = _checkCts;
        var cts = new CancellationTokenSource();
        _checkCts = cts;
        if (old is not null) { try { old.Cancel(); } catch { } }

        Checking = true;
        UpdateStatus = "در حال بررسی…";
        UpdateStatusBrushKey = "Pump.Muted";
        try
        {
            var info = await _update.CheckAsync(cts.Token);
            if (!ReferenceEquals(_checkCts, cts)) return;   // کلیکِ تازه‌تر آمد
            _info = info;
            UpdateAvailable = info.Available;
            PackageText = info.PackageText;
            // ⛔ جمله در خودِ ‎UpdateInfo‎ ساخته می‌شود — سه حال («تازه هست» ·
            // «به‌روز است» · «نرسیدیم») یک جا از هم جدا می‌شوند و این‌جا
            // دوباره نوشته نمی‌شوند.
            UpdateStatus = info.StatusText;
            UpdateStatusBrushKey = info.StatusBrushKey;
        }
        catch (OperationCanceledException) { return; }
        catch
        {
            if (!ReferenceEquals(_checkCts, cts)) return;
            // ⛔ ساکت نمی‌ماند و «به‌روز است» هم نمی‌گوید
            UpdateStatus = "❌ بررسیِ به‌روزرسانی انجام نشد — دکمه را دوباره بزنید";
            UpdateStatusBrushKey = "Pump.Danger";
        }
        finally
        {
            if (ReferenceEquals(_checkCts, cts)) Checking = false;
            cts.Dispose();
        }
    }

    /// <summary>
    /// «باز کردنِ صفحهٔ دانلود» — راهِ بیرون وقتی شبکه نمی‌گذارد.
    /// ⚠️ نشانی در رابط نوشته نمی‌شود؛ فقط به مرورگرِ سیستم سپرده می‌شود
    /// (ساختنش در <c>UpdateService</c> است، همان یک جا).
    /// </summary>
    [RelayCommand]
    private void OpenDownloadPage()
    {
        if (!UpdateService.OpenDownloadPage())
            _host.Toast("❌ مرورگر باز نشد", ToastKind.Error);
    }

    [RelayCommand]
    private async Task DownloadUpdateAsync()
    {
        if (_info is null || !_info.Available) return;
        Downloading = true;
        DownloadPercent = 0;
        UpdateStatus = "در حال گرفتنِ نسخهٔ تازه…";
        UpdateStatusBrushKey = "Pump.Muted";
        try
        {
            var progress = new Progress<double>(p => DownloadPercent = p);
            _downloaded = await _update.DownloadAsync(_info, progress);
            if (_downloaded is null)
            {
                // ⛔ سرخ، و با راهِ بیرون — «هیچ اتفاقی نیفتاد» باگ است.
                // ⚠️ اگر چک‌سام یا نشانی رد شد، همان دلیل (بی نامِ میزبان).
                UpdateStatus = _update.LastProblem.Length > 0
                    ? "❌ " + _update.LastProblem
                    : "❌ گرفتنِ نسخهٔ تازه انجام نشد — «باز کردنِ صفحهٔ دانلود» را بزنید";
                UpdateStatusBrushKey = "Pump.Danger";
                return;
            }
            ReadyToInstall = true;
            UpdateStatus = "نسخهٔ تازه گرفته شد — آمادهٔ نصب";
            UpdateStatusBrushKey = "Pump.Muted";
        }
        catch
        {
            UpdateStatus = "❌ گرفتنِ نسخهٔ تازه انجام نشد — «باز کردنِ صفحهٔ دانلود» را بزنید";
            UpdateStatusBrushKey = "Pump.Danger";
        }
        finally { Downloading = false; }
    }

    /// <summary>
    /// نصب و راه‌اندازیِ دوباره.
    ///
    /// ⚠️ برنامه باید همین‌جا بسته شود: تا وقتی باز است، فایل‌های خودش قفل‌اند
    /// و جای‌گزینی شکست می‌خورد.
    /// </summary>
    [RelayCommand]
    private async Task InstallUpdateAsync()
    {
        if (_downloaded is null) return;
        LastFailure = "";
        //  ⛔ هر نوشتهٔ در صف همین حالا روی دیسک می‌نشیند — نصاب برنامه را
        //  می‌بندد و ردیفی که هنوز در مکثِ ذخیره است، با آن می‌رفت.
        try { await SaveGuard.FlushAllAsync(); } catch { }
        if (!UpdateService.Launch(_downloaded, _info?.LatestVersion))
        {
            UpdateStatus = "نصبِ نسخهٔ تازه انجام نشد — فایلِ گرفته‌شده دیگر با چک‌سامش جور نیست یا نصاب بالا نیامد. دوباره «گرفتن» را بزنید.";
            UpdateStatusBrushKey = "Pump.Danger";
            ReadyToInstall = false;
            _downloaded = null;
            return;
        }

        UpdateStatus = "برنامه بسته می‌شود و با نسخهٔ تازه باز می‌شود";
        _host.Toast("برنامه بسته می‌شود و با نسخهٔ تازه باز می‌شود", ToastKind.Info);

        _ = Task.Run(async () =>
        {
            await Task.Delay(1200);
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (Avalonia.Application.Current?.ApplicationLifetime
                    is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime d)
                    d.Shutdown();
                else Environment.Exit(0);
            });
        });
    }
}
