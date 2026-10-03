using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.EntityFrameworkCore;
using PumpYaqobi.App.Services;
using PumpYaqobi.App.Themes;
using PumpYaqobi.App.Update;
using PumpYaqobi.App.ViewModels;
using PumpYaqobi.App.ViewModels.Sections;
using PumpYaqobi.App.Views;
using PumpYaqobi.Services.Data;

namespace PumpYaqobi.UiTests;

/// <summary>
/// ══ «فایلِ کاملِ برنامه» و دو دکمهٔ تازهٔ بکاپ — با دکمه‌های واقعیِ پنجره ═══
///
/// خواستهٔ صاحب ریپو (۱۴۰۵/۰۷/۱۵): «اگر آن را توی برنامه آوردم، اطلاعاتِ همان
/// فایل همه‌شان بیاید… با دقت، اطلاعات است و خیلی مهم.» پس:
///   ۱) دفتر و تم و ترتیبِ نوار و اندازهٔ نوشته‌ها عوض می‌شوند ⇒ «💾 ذخیرهٔ فایلِ بکاپ»
///   ۲) همه‌چیز خراب می‌شود (حساب پاک، ردیف اضافه، تم و نوار برگشته)
///   ۳) «↩ بازگردانی از فایل» ⇒ شمارِ <b>هر</b> جدول با پیش از خرابی، نامِ حسابِ پاک‌شده
///      برگشته، و تم و نوار و نوشته‌ها همان لحظه روی پنجره
///   ۴) فایلِ دست‌خورده ⇒ رد، و یک ردیف هم عوض نشد
///   ۵) «📤 فرستادن به سرور» بی سرور ⇒ جملهٔ سرخِ راست، نه «رفت»
///   ۶) «💿 نصب از فایل» با فایلی که نصابِ ما نیست ⇒ رد، و هیچ چیزی اجرا نشد
///
///     dotnet run --project PumpYaqobi.UiTests -c Release -- fullbackup [پوشهٔ عکس]
/// </summary>
internal static class FullBackupProbe
{
    private static int _bad;

    private static void Check(string what, bool ok, string? detail = null)
    {
        Console.WriteLine((ok ? "  ✔ " : "  ✖ ") + what + (detail is null ? "" : " — " + detail));
        if (!ok) _bad++;
    }

    public static int Run(string? shots)
    {
        var root = Path.Combine(Path.GetTempPath(), "pump-fullbackup-" + Guid.NewGuid().ToString("N"));
        var tmpDb = Path.Combine(root, "pump.db");
        AppHost.Start(tmpDb);
        if (AppHost.Current.Auth.NeedsFirstRun()) AppHost.Current.Auth.CreateFirstAdmin("1234");
        FakeLicense.Grant();

        AppBuilder.Configure<PumpYaqobi.App.App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .SetupWithoutStarting();

        var win = new MainWindow { Width = 1440, Height = 900 };
        win.Show();
        Pump(win);
        var vm = (MainViewModel)win.DataContext!;
        vm.Lock.Password = "1234";
        LockIn.Wait(vm.Lock);
        Pump(win);
        Seed.Fill(AppHost.Current);
        var host = AppHost.Current;

        // ── ۱) حالِ «کاربر»: تم، نوار، نوشته‌ها ─────────────────────────────
        Console.WriteLine("── ۱) تم، ترتیبِ نوار و اندازهٔ نوشته‌ها عوض می‌شوند، بعد «💾 ذخیرهٔ فایلِ بکاپ» ──");
        var safe = vm.Sections.First(s => s.Id == "safe");
        vm.SelectedTheme = PumpTheme.Gold;
        vm.MoveNav(safe, PumpYaqobi.App.ViewModels.NavOrder.Where.First);
        safe.FontBiggerCommand.Execute(null); safe.FontBiggerCommand.Execute(null);
        NoteFontViewModel.Instance.Scale = 1.4;
        Pump(win, 20);
        var wantSafeFont = safe.FontScale;

        var settings = vm.Sections.First(x => x.Id == "settings");
        var page = (BackupSectionViewModel)settings.SubSections.First(x => x.Id == "backups");
        Wait(win, vm.GoAsync(settings));
        settings.ShowSubCommand.Execute(page);
        for (var i = 0; i < 40; i++) Pump(win);

        var file = Path.Combine(root, "flash", "همه" + FullBackup.Extension);
        Dialogs.SaveFileHook = _ => file;
        try { Click(win, "💾 ذخیرهٔ فایلِ بکاپ"); Until(win, () => page.FullStatus.StartsWith("✅") || page.FullStatus.StartsWith("❌")); }
        finally { Dialogs.SaveFileHook = null; }
        Check("فایلِ کامل ساخته شد", File.Exists(file), page.FullStatus.Replace("\n", " ⏎ "));
        Check("جمله سبز و راست است", page.FullStatus.StartsWith("✅"));
        Shot(win, shots, "fb-1-export");

        SqliteClear();
        var want = FullBackup.CountRows(host.Db.DbPath)!;
        string gone;
        using (var db = host.Db.Create()) gone = db.Debtors.OrderBy(d => d.Id).Select(d => d.Name).First();
        Check($"دفترِ پیش از خرابی شمرده شد ({want.Values.Sum()} ردیف)", want.Values.Sum() > 50);

        // ── ۲) خرابی ────────────────────────────────────────────────────────
        Console.WriteLine("── ۲) خرابی: حساب پاک، ردیف اضافه، تم و نوار و نوشته برگشت ──");
        using (var db = host.Db.Create())
        {
            db.Database.ExecuteSqlRaw("DELETE FROM DebtRows; DELETE FROM Debtors WHERE Name = {0};", gone);
            db.Expenses.Add(new PumpYaqobi.Domain.Entities.Expense { DateShamsi = "1405/07/15", MonthKey = "1405/07", Title = "آشغال", Amount = 1 });
            db.SaveChanges();
        }
        vm.SelectedTheme = PumpTheme.Blue;
        vm.NavResetCommand.Execute(null);
        safe.FontResetCommand.Execute(null);
        NoteFontViewModel.Instance.Scale = 1;
        Pump(win, 20);
        SqliteClear();
        var broken = FullBackup.CountRows(host.Db.DbPath)!;
        Check("خرابی واقعاً دفتر را عوض کرد", broken["DebtRows"] == 0 && broken["Expenses"] != want["Expenses"]);

        // ── ۳) آوردن ────────────────────────────────────────────────────────
        Console.WriteLine("── ۳) «↩ بازگردانی از فایل» ⇒ همه برگشت ──");
        var pre = AppSettings.Load();
        var bonds = (pre.CloudDeviceToken, pre.CloudPublicKey, pre.StationCode);
        string? asked = null;
        Dialogs.PickFileHook = _ => file;
        Dialogs.ConfirmHook = (_, m) => { asked = m; return true; };
        try { Click(win, "↩ بازگردانی از فایل"); Until(win, () => page.FullStatus.StartsWith("✅") || page.FullStatus.StartsWith("❌") || page.FullStatus.StartsWith("⚠️")); }
        finally { Dialogs.PickFileHook = null; Dialogs.ConfirmHook = null; }
        Check("پیش از جایگزینی پرسیده شد، با خلاصهٔ فایل", asked is not null && asked.Contains("قرض‌داران") && asked.Contains("عکسِ ایمنی"),
              asked?.Replace("\n", " ⏎ "));
        Check("جملهٔ نتیجه سبز است", page.FullStatus.StartsWith("✅"), page.FullStatus.Replace("\n", " ⏎ "));

        SqliteClear();
        var back = FullBackup.CountRows(host.Db.DbPath)!;
        var off = 0;
        foreach (var (t, n) in want)
        {
            if (FullBackup.Housekeeping.Contains(t)) continue;
            if (!back.TryGetValue(t, out var got) || got != n) { off++; Check($"جدولِ «{t}»", false, $"{n} ⇐ {got}"); }
        }
        Check($"شمارِ هر جدولِ داده همان پیش از خرابی است ({want.Count} جدول)", off == 0);
        using (var db = host.Db.Create())
            Check($"حسابِ پاک‌شده («{gone}») برگشت", db.Debtors.Any(d => d.Name == gone));
        Check("تمِ تیره همان لحظه روی پنجره نشست", vm.SelectedTheme.Id == PumpTheme.Gold.Id, vm.SelectedTheme.Id);
        Check("گاوصندوق دوباره اولِ نوار است", vm.NavSections[0].Id == "safe", vm.NavSections[0].Id);
        Check($"اندازهٔ نوشتهٔ گاوصندوق برگشت ({wantSafeFont})", Math.Abs(safe.FontScale - wantSafeFont) < 0.001, safe.FontScale.ToString());
        Check("اندازهٔ کادرهای یادداشت برگشت", Math.Abs(NoteFontViewModel.Instance.Scale - 1.4) < 0.001, NoteFontViewModel.Instance.Scale.ToString());
        AppSettings.FlushNow();
        var disk = AppSettings.Load();
        Check("روی دیسک هم نشست (تم و نوار)", disk.ThemeId == "gold" && disk.NavOrder.StartsWith("safe"), disk.ThemeId + " · " + disk.NavOrder);
        Check("بندهای این کامپیوتر دست نخوردند (توکنِ دستگاه، کلید، کدِ پمپ)",
              (disk.CloudDeviceToken, disk.CloudPublicKey, disk.StationCode) == bonds);
        Shot(win, shots, "fb-2-import");

        // ── ۳ب) دوبار-کلیک روی فایل در ویندوز (‎OpenRequest‎) — همان درِ دکمه ──
        //  «چرا فایلِ برنامه رو که می‌خوام باز کنم، برنامهٔ من پیشنهاد نمی‌شه؟»
        //  نصاب پسوند را به برنامه می‌سپارد و ویندوز مسیر را می‌دهد؛ این‌جا همان
        //  مسیر به برنامهٔ باز داده می‌شود و باید با همان سنجش و همان پرسش بیاید.
        Console.WriteLine("── ۳ب) دوبار-کلیک روی «.pumpyaqobi» ⇒ همان سنجش، همان پرسش، همه برگشت ──");
        Wait(win, vm.GoAsync(vm.Sections.First(x => x.Id == "dashboard")));
        using (var db = host.Db.Create())
            db.Database.ExecuteSqlRaw("DELETE FROM DebtRows; DELETE FROM Debtors WHERE Name = {0};", gone);
        page.FullStatus = "";
        string? asked2 = null;
        Dialogs.ConfirmHook = (_, m) => { asked2 = m; return true; };
        try
        {
            OpenRequest.Add(file);
            Until(win, () => page.FullStatus.StartsWith("✅") || page.FullStatus.StartsWith("❌") || page.FullStatus.StartsWith("⚠️"));
        }
        finally { Dialogs.ConfirmHook = null; }
        Check("پیش از جایگزینی پرسیده شد", asked2 is not null && asked2.Contains("قرض‌داران"), asked2?.Replace("\n", " ⏎ "));
        Check("خودش به صفحهٔ «بک‌اپ» رفت", ReferenceEquals(vm.Current, settings) && ReferenceEquals(settings.OpenSub, page),
              vm.Current?.Id + " / " + settings.OpenSub?.Id);
        Check("آمد", page.FullStatus.StartsWith("✅"), page.FullStatus.Replace("\n", " ⏎ "));
        using (var db = host.Db.Create())
            Check($"حسابِ پاک‌شده دوباره برگشت («{gone}»)", db.Debtors.Any(d => d.Name == gone));

        // ── ۴) فایلِ دست‌خورده ────────────────────────────────────────────
        Console.WriteLine("── ۴) فایلِ دست‌خورده ⇒ رد، و یک ردیف هم عوض نشد ──");
        var bad = Path.Combine(root, "bad" + FullBackup.Extension);
        var bytes = File.ReadAllBytes(file);
        File.WriteAllBytes(bad, bytes[..(bytes.Length * 2 / 3)]);
        var before = FullBackup.CountRows(host.Db.DbPath)!;
        Dialogs.PickFileHook = _ => bad;
        Dialogs.ConfirmHook = (_, _) => true;
        try { Click(win, "↩ بازگردانی از فایل"); Until(win, () => page.FullStatus.StartsWith("❌")); }
        finally { Dialogs.PickFileHook = null; Dialogs.ConfirmHook = null; }
        Check("رد شد، با جملهٔ سرخ", page.FullStatus.StartsWith("❌") && page.FullStatus.Contains("هیچ چیزی عوض نشد"), page.FullStatus);
        SqliteClear();
        var after = FullBackup.CountRows(host.Db.DbPath)!;
        Check("دفتر دست نخورد", before.All(kv => after.TryGetValue(kv.Key, out var v) && v == kv.Value));

        // ── ۵) فرستادن به سرور، بی سرور ───────────────────────────────────
        Console.WriteLine("── ۵) «📤 فرستادن به سرور» بی هیچ سروری ⇒ راست می‌گوید ──");
        //  ⛔ کنارِ بقیهٔ دکمه‌های «تازه‌ترین بکاپ» (۱۴۰۵/۰۷/۱۵) — نه کارتِ جدا
        var sendBtn = win.GetVisualDescendants().OfType<Button>().FirstOrDefault(b => (b.Content as string) == "📤 فرستادن به سرور");
        var saveBtn = win.GetVisualDescendants().OfType<Button>().FirstOrDefault(b => (b.Content as string) == "💾 ذخیرهٔ فایلِ بکاپ");
        Check("«فرستادن به سرور» کنارِ «ذخیرهٔ فایلِ بکاپ» است",
              sendBtn is not null && saveBtn is not null && ReferenceEquals(sendBtn.Parent, saveBtn.Parent));
        Click(win, "📤 فرستادن به سرور");
        Until(win, () => !page.SendingToServer && page.ServerStatus.Length > 0 && !page.ServerStatus.StartsWith("در حالِ"));
        Check("جمله سرخ است و «رفت» نمی‌گوید", page.ServerStatus.StartsWith("❌") && page.ServerStatusBrushKey == "Pump.Danger",
              page.ServerStatus);
        Check("می‌گوید بکاپِ محلی سالم است", page.ServerStatus.Contains("همین کامپیوتر"));

        // ── ۶) نصب از فایل ─────────────────────────────────────────────────
        Console.WriteLine("── ۶) «💿 نصب از فایل» با فایلی که نصابِ ما نیست ⇒ رد ──");
        var notOurs = Path.Combine(root, "PumpYaqobi-Setup.exe");
        File.WriteAllText(notOurs, "MZ نه‌واقعی");
        var launched = false;
        UpdateService.TestStart = _ => { launched = true; return true; };
        Dialogs.PickFileHook = _ => notOurs;
        try { Click(win, "💿 نصبِ نسخهٔ تازه از فایل (بی اینترنت)"); Until(win, () => page.OfflineStatus.Length > 0); }
        finally { Dialogs.PickFileHook = null; UpdateService.TestStart = null; }
        Check("رد شد", page.OfflineStatus.StartsWith("❌"), page.OfflineStatus);
        Check("هیچ چیزی اجرا نشد", !launched);
        Shot(win, shots, "fb-3-page");

        // ── ۷) خروجیِ یک حساب (فایلِ حساب) و آوردنِ دوباره — ۱۴۰۵/۰۷/۱۹ ─────────────
        Console.WriteLine("── ۷) «📤 خروجیِ یک حساب» ⇒ فایلِ حساب ⇒ خرابی ⇒ «📥 آوردن» ⇒ همان عدد؛ دوباره ⇒ همان فایل به‌روز ──");
        SqliteClear();
        long rowId; string rowLit; long debtorId;
        using (var db = host.Db.Create())
        {
            var r = db.DebtRows.AsNoTracking().Where(x => x.FuelAccountId != null && x.Liters > 0).OrderBy(x => x.Id).First();
            var acct = db.DebtAccounts.AsNoTracking().First(a => a.Id == r.FuelAccountId);
            debtorId = acct.MainOfDebtorId ?? acct.DebtorId ?? 0;
            rowId = r.Id; rowLit = r.Liters.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
        page.PortableKind = BackupSectionViewModel.PortableKinds.First(k => k.Key == "debtor");
        Until(win, () => page.PortableTargets.Any(t => t.Id == debtorId));
        page.PortableTarget = page.PortableTargets.First(t => t.Id == debtorId);
        Pump(win, 10);
        var xls = Path.Combine(root, "flash", "حساب" + PortableFile.Extension);
        var saveAsked = 0;
        Dialogs.SaveFileHook = _ => { saveAsked++; return xls; };
        try { Click(win, "📤 ساختنِ فایلِ حساب"); Until(win, () => page.PortableStatus.StartsWith("✅") || page.PortableStatus.StartsWith("❌")); }
        finally { Dialogs.SaveFileHook = null; }
        Check("فایلِ حساب ساخته شد", File.Exists(xls) && PortableFile.ReadSnapshot(xls) is not null, page.PortableStatus);
        Shot(win, shots, "fb-7-portable");

        SqliteClear();
        using (var db = host.Db.Create())
            db.Database.ExecuteSqlRaw("UPDATE DebtRows SET Liters = '999' WHERE Id = {0};", rowId);
        Dialogs.PickFileHook = _ => xls;
        string? impAsk = null;
        Dialogs.ConfirmHook = (_, m) => { impAsk = m; return true; };
        page.PortableStatus = "";
        try { Click(win, "📥 آوردن از فایلِ حساب"); Until(win, () => page.PortableStatus.StartsWith("✅") || page.PortableStatus.StartsWith("❌") || page.PortableStatus.StartsWith("⚠️")); }
        finally { Dialogs.PickFileHook = null; Dialogs.ConfirmHook = null; }
        Check("پیش از آوردن پرسید و گفت چیزی پاک نمی‌شود", impAsk?.Contains("پاک نمی‌شود") == true);
        SqliteClear();
        using (var db = host.Db.Create())
        {
            var lit2 = db.DebtRows.AsNoTracking().First(x => x.Id == rowId).Liters.ToString(System.Globalization.CultureInfo.InvariantCulture);
            Check($"ردیفِ خراب‌شده همان عددِ فایل را گرفت ({lit2} = {rowLit})", lit2 == rowLit, page.PortableStatus);
        }

        var xlsBefore = File.GetLastWriteTimeUtc(xls);
        Thread.Sleep(1100);
        string? oldAsk = null;
        Dialogs.ConfirmHook = (_, m) => { oldAsk = m; return true; };
        Dialogs.SaveFileHook = _ => { saveAsked++; return null; };
        page.PortableStatus = "";
        try { Click(win, "📤 ساختنِ فایلِ حساب"); Until(win, () => page.PortableStatus.StartsWith("✅") || page.PortableStatus.StartsWith("❌")); }
        finally { Dialogs.SaveFileHook = null; Dialogs.ConfirmHook = null; }
        Check("بارِ دوم مسیرِ قبلی را پیشنهاد کرد", oldAsk?.Contains(Path.GetFileName(xls)) == true);
        Check("«به‌روز کردنِ فایلِ قبلی» همان فایل را نوشت، بی پرسیدنِ مسیرِ تازه",
              saveAsked == 1 && File.GetLastWriteTimeUtc(xls) > xlsBefore, $"save={saveAsked}");

        Console.WriteLine(_bad == 0 ? "\n✅ فایلِ کاملِ برنامه: همه سبز" : $"\n✖ {_bad} ایراد");
        return _bad == 0 ? 0 : 1;
    }

    // ── ابزار ─────────────────────────────────────────────────────────────

    private static void Click(Window win, string text)
    {
        Button? b = null;
        for (var i = 0; i < 40 && b is null; i++)
        {
            b = win.GetVisualDescendants().OfType<Button>()
                   .FirstOrDefault(x => x.Content as string == text && x.IsEffectivelyVisible && x.IsEffectivelyEnabled);
            if (b is null) Pump(win);
        }
        if (b is null) { Check($"دکمهٔ «{text}» دیده و زدنی است", false); return; }
        //  ⚠️ رویدادِ ‎Click‎ فرمان را اجرا نمی‌کند (آن کارِ ‎OnClick‎ی درونیِ دکمه
        //  است)؛ همان فرمانِ همان دکمهٔ دیده‌شده و زدنی اجرا می‌شود.
        if (b.Command?.CanExecute(b.CommandParameter) != true) { Check($"دکمهٔ «{text}» فرمانِ اجراشدنی دارد", false); return; }
        b.Command.Execute(b.CommandParameter);
        Pump(win);
    }

    private static void Until(Window win, Func<bool> done)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (!done() && sw.Elapsed < TimeSpan.FromSeconds(60)) { Pump(win); Thread.Sleep(10); }
        Pump(win, 20);
    }

    private static void SqliteClear() => Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

    private static void Shot(Window win, string? dir, string name)
    {
        if (string.IsNullOrWhiteSpace(dir)) return;
        Directory.CreateDirectory(dir);
        using var s = win.CaptureRenderedFrame();
        var p = Path.Combine(dir, name + ".png");
        s?.Save(p);
        Console.WriteLine("  📷 " + p);
    }

    private static void Pump(Window w, int n = 8)
    {
        for (var i = 0; i < n; i++) { Dispatcher.UIThread.RunJobs(); w.UpdateLayout(); }
    }

    private static void Wait(Window w, Task t)
    {
        for (var i = 0; i < 400 && !t.IsCompleted; i++) Pump(w);
        Pump(w);
    }
}
