using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using PumpYaqobi.App.Services;
using PumpYaqobi.Application.Localization;
using PumpYaqobi.Application.Security;
using PumpYaqobi.Services.Data;

namespace PumpYaqobi.App.ViewModels.Sections;

/// <summary>یک گزینهٔ کادرِ کشویی (‎Key‎ همان چیزی است که به سرویس می‌رود).</summary>
public sealed record PortableChoice(string Key, string Label, long Id = 0)
{
    public override string ToString() => Label;
}

/// <summary>
/// ══ خروجیِ یک حساب یا بخش، برای بازهٔ ماه/سال — و آوردنِ دوباره‌اش (۱۴۰۵/۰۷/۱۹) ══
///
/// خواستهٔ صاحب ریپو: «خروجی گرفتن از یک حساب یا بخش… برای بازهٔ ماه/سالِ
/// انتخابی… فایلِ خروجی دوباره وارد شود بی خراب شدن… فایلِ بکاپ به‌روز شود،
/// مسیرِ قبلی پیشنهاد شود، با ‹به‌روز کردنِ فایلِ قبلی› یا ‹تازه›.» و بعدش: «مثلِ
/// اکسل، نه خودِ اکسل — اسمِ اکسل را بردار.» پس فایل، فایلِ خودِ برنامه است
/// (‎PortableFile‎، ‎.pumphesab‎).
///
/// ⛔ هیچ قاعدهٔ دومی این‌جا نیست: ساختن ‎SyncStore.ExportPortable‎، نوشتن
/// ‎PortableFile‎ و آوردن ‎SyncStore.ImportPortable‎ — همان راهِ آزمودهٔ
/// همگام‌سازی (پدر پیش از فرزند، شناسهٔ سراسری، هیچ پاک کردنی).
/// </summary>
public sealed partial class BackupSectionViewModel
{
    public static readonly IReadOnlyList<PortableChoice> PortableKinds = new[]
    {
        new PortableChoice("debtor", "👤 حسابِ یک قرض‌دار"),
        new PortableChoice("company", "🏢 حسابِ یک شرکت"),
        new PortableChoice("waraq", "🧾 ورق‌ها"),
        new PortableChoice("parcha", "⛽ گزارش‌های پارچه"),
        new PortableChoice("safe", "🏦 گاوصندوق"),
        new PortableChoice("expenses", "💸 مصارف"),
        new PortableChoice("sarrafi", "💱 صرافی"),
        new PortableChoice("retail", "🛒 چکنه"),
        new PortableChoice("income", "➕ درآمدِ اضافی"),
        new PortableChoice("invoices", "🧾 فاکتورها"),
    };

    public ObservableCollection<PortableChoice> PortableTargets { get; } = new();

    [ObservableProperty] private PortableChoice? _portableKind = PortableKinds[0];
    [ObservableProperty] private PortableChoice? _portableTarget;
    [ObservableProperty] private string _portableFrom = "";
    [ObservableProperty] private string _portableTo = "";
    [ObservableProperty] private string _portableStatus = "";
    [ObservableProperty] private string _portableStatusBrushKey = "Pump.Muted";
    [ObservableProperty] private bool _askBackupOnExit = true;

    public bool PortableNeedsTarget => PortableKind?.Key is "debtor" or "company";

    partial void OnPortableKindChanged(PortableChoice? value)
    {
        OnPropertyChanged(nameof(PortableNeedsTarget));
        _ = FillTargetsAsync();
    }

    partial void OnAskBackupOnExitChanged(bool value)
    {
        try { var s = AppSettings.Load(); if (s.AskBackupOnExit != value) { s.AskBackupOnExit = value; s.Save(); } } catch { }
    }

    /// <summary>نام‌های قرض‌دار یا شرکت — فقط دو ستون، نه ردیف‌ها.</summary>
    public async Task FillTargetsAsync()
    {
        var kind = PortableKind?.Key;
        var keepId = PortableTarget?.Key == kind ? PortableTarget?.Id : null;
        if (kind is not ("debtor" or "company")) { PortableTargets.Clear(); PortableTarget = null; return; }
        var list = await Task.Run(() =>
        {
            return (kind == "debtor" ? _host.Debtors.NameList() : _host.Companies.NameList())
                .Select(x => new PortableChoice(kind, x.Name, x.Id)).ToList();
        });
        if (PortableKind?.Key != kind) return;
        PortableTargets.Clear();
        foreach (var c in list) PortableTargets.Add(c);
        PortableTarget = PortableTargets.FirstOrDefault(c => c.Id == keepId) ?? PortableTargets.FirstOrDefault();
    }

    /// <summary>«1405/03» یا «1405» ⇒ کلیدِ نخستین/آخرین روز؛ خالی ⇒ صفر (همه).</summary>
    public static int MonthKeyOf(string? text, bool end)
    {
        var t = Shamsi.ToEnDigits(text ?? "").Trim().Replace('-', '/');
        if (t.Length == 0) return 0;
        var parts = t.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (!int.TryParse(parts[0], out var y) || y < 1300 || y > 1500) return -1;
        var m = end ? 12 : 1;
        if (parts.Length > 1 && (!int.TryParse(parts[1], out m) || m < 1 || m > 12)) return -1;
        return y * 10000 + m * 100 + (end ? 31 : 1);
    }

    private string PickKey() => PortableKind?.Key + (PortableNeedsTarget ? ":" + PortableTarget?.Id : "");

    [RelayCommand]
    private async Task ExportPortableAsync()
    {
        if (!CanRestore) { _host.Toast("❌ خروجی گرفتن فقط از مدیر برمی‌آید", ToastKind.Error); return; }
        var kind = PortableKind;
        if (kind is null) return;
        if (PortableNeedsTarget && PortableTarget is null)
        {
            PortableStatus = "❌ اول نامِ حساب را انتخاب کنید";
            PortableStatusBrushKey = "Pump.Danger";
            return;
        }
        var from = MonthKeyOf(PortableFrom, false);
        //  ⛔ فقط «از ماه» نوشته شد ⇒ همان یک ماه (یا همان یک سال) — «یک ماه از مصارف»
        var to = MonthKeyOf(string.IsNullOrWhiteSpace(PortableTo) ? PortableFrom : PortableTo, true);
        if (from < 0 || to < 0 || (from > 0 && to > 0 && from > to))
        {
            PortableStatus = "❌ بازهٔ ماه درست نیست — مثلاً «1405/01» تا «1405/06»، یا خالی برای همه";
            PortableStatusBrushKey = "Pump.Danger";
            return;
        }

        var title = kind.Label.Substring(kind.Label.IndexOf(' ') + 1)
                  + (PortableNeedsTarget ? " — " + PortableTarget!.Label : "")
                  + (from > 0 || to > 0 ? $" ({PortableFrom}{(string.IsNullOrWhiteSpace(PortableTo) ? "" : " تا " + PortableTo)})" : "");

        //  ⛔ مسیرِ قبلی پیشنهاد می‌شود: «به‌روز کردنِ همان فایل» یا «فایلِ تازه»
        var settings = AppSettings.Load();
        var key = PickKey();
        string? target = null;
        if (settings.PortablePaths.TryGetValue(key, out var last) && File.Exists(last)
            && await Dialogs.ConfirmAsync("فایلِ قبلی",
                   $"این بخش پیش‌تر در «{Path.GetFileName(last)}» ذخیره شده بود.\n"
                   + "همان فایل با اطلاعاتِ تازه به‌روز شود؟", "به‌روز کردنِ فایلِ قبلی", "فایلِ تازه"))
            target = last;
        target ??= await Dialogs.SaveFileAsync("فایلِ حساب کجا ذخیره شود؟",
                       SafeName(title) + PortableFile.Extension, "فایلِ حسابِ پمپ", new[] { "*" + PortableFile.Extension });
        if (target is null) return;
        if (!target.EndsWith(PortableFile.Extension, StringComparison.OrdinalIgnoreCase)) target += PortableFile.Extension;

        Busy = true;
        PortableStatus = "در حالِ ساختنِ فایل…";
        PortableStatusBrushKey = "Pump.Muted";
        try
        {
            try { await SaveGuard.FlushAllAsync(); } catch { }
            var pick = new PortablePick(kind.Key, PortableTarget?.Id ?? 0, from, to);
            var ex = await Task.Run(() =>
            {
                var e = _host.Store.ExportPortable(pick);
                PortableFile.Write(target, title, e);
                //  ⛔ «ساخته شد» یعنی «خوانده می‌شود»
                if (PortableFile.ReadSnapshot(target) != e.SnapshotJson)
                    throw new IOException("فایلِ ساخته‌شده دوباره خوانده نشد");
                return e;
            });
            settings = AppSettings.Load();
            settings.PortablePaths[key] = target;
            settings.Save();
            PortableStatus = $"✅ ساخته شد: {Path.GetFileName(target)} · {Shamsi.Money(ex.RowCount)} ردیف در "
                           + $"{Shamsi.Money(ex.Tables.Count)} جدول";
            PortableStatusBrushKey = "Pump.Ok";
            _host.Toast("📤 فایلِ حساب ساخته شد", ToastKind.Ok);
        }
        catch (Exception ex)
        {
            CrashGuard.Write("خروجیِ بخش", ex);
            PortableStatus = "❌ فایل ساخته نشد: " + ErrorText.Friendly(ex);
            PortableStatusBrushKey = "Pump.Danger";
        }
        finally { Busy = false; }
    }

    [RelayCommand]
    private async Task ImportPortableAsync()
    {
        var path = await Dialogs.PickFileAsync("فایلِ حسابِ همین برنامه را انتخاب کنید", "فایلِ حسابِ پمپ",
                                               new[] { "*" + PortableFile.Extension });
        if (path is not null) await ImportPortableFromAsync(path);
    }

    /// <summary>آوردنِ فایلِ یک بخش — ⛔ چیزی پاک نمی‌شود، پاک‌شده‌ای زنده نمی‌شود.</summary>
    public async Task ImportPortableFromAsync(string path)
    {
        if (!CanRestore) { _host.Toast("❌ آوردن فقط از مدیر برمی‌آید", ToastKind.Error); return; }
        if (!RestoreAllowed()) return;
        var json = await Task.Run(() => PortableFile.ReadSnapshot(path));
        if (json is null)
        {
            PortableStatus = "❌ این فایل، خروجیِ بخشِ همین برنامه نیست — هیچ چیزی عوض نشد";
            PortableStatusBrushKey = "Pump.Danger";
            return;
        }
        using var doc = JsonDocument.Parse(json);
        var rows = 0;
        if (doc.RootElement.TryGetProperty("tables", out var tables))
            foreach (var t in tables.EnumerateObject()) rows += t.Value.GetArrayLength();
        if (!await Dialogs.ConfirmAsync("آوردن از فایل",
                $"«{Path.GetFileName(path)}» · {Shamsi.Money(rows)} ردیف\n\n"
                + "ردیف‌های همین فایل در دفتر می‌نشینند یا به‌روز می‌شوند.\n"
                + "⛔ هیچ ردیفی پاک نمی‌شود و ردیفی که این‌جا پاک کرده‌اید برنمی‌گردد.\n"
                + "پیش از این کار از دفترِ فعلی یک عکسِ ایمنی گرفته می‌شود.", "بله، بیاور"))
            return;

        Busy = true;
        PortableStatus = "در حالِ آوردن…";
        try
        {
            try { await SaveGuard.FlushAllAsync(); } catch { }
            using var paused = _host.SyncIfStarted is { } se ? await se.PauseAsync() : null;
            var safety = await Task.Run(() => _host.Backup.SafetyCopy());
            var rep = await Task.Run(() => _host.Store.ImportPortable(doc.RootElement));
            await RefreshAsync();
            await _main.ReloadAllAsync();
            PortableStatus = $"✅ {Shamsi.Money(rep.Added)} ردیفِ تازه · {Shamsi.Money(rep.Updated)} به‌روز"
                           + (rep.SkippedDeleted > 0 ? $" · {Shamsi.Money(rep.SkippedDeleted)} پاک‌شده دست نخورد" : "")
                           + (rep.Failed > 0 ? $" · ⚠️ {Shamsi.Money(rep.Failed)} ننشست ({rep.Why})" : "")
                           + (safety is null ? "" : "\n" + SafetyLine(safety));
            PortableStatusBrushKey = rep.Failed > 0 ? "Pump.Warn" : "Pump.Ok";
            _host.Toast(rep.Failed > 0 ? "⚠️ بخشی از فایل ننشست" : "✅ فایل آمد", rep.Failed > 0 ? ToastKind.Warn : ToastKind.Ok);
        }
        catch (Exception ex)
        {
            CrashGuard.Write("آوردنِ بخش", ex);
            PortableStatus = "❌ آوردن انجام نشد: " + ErrorText.Friendly(ex);
            PortableStatusBrushKey = "Pump.Danger";
        }
        finally { Busy = false; }
    }

    private static string SafeName(string s)
    {
        var bad = Path.GetInvalidFileNameChars().Concat(new[] { '/', '\\', ':' }).ToHashSet();
        return new string(s.Select(c => bad.Contains(c) ? '-' : c).ToArray()).Trim();
    }
}
