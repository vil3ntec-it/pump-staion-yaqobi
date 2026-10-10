using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.Data.Sqlite;
using PumpYaqobi.App.Services;
using PumpYaqobi.App.ViewModels;
using PumpYaqobi.App.ViewModels.Sections;
using PumpYaqobi.App.Views;
using PumpYaqobi.Services.Data;

namespace PumpYaqobi.UiTests;

/// <summary>
/// ══ بکاپ‌ها، ۱۴۰۵/۰۷/۱۸ — با پنجرهٔ واقعی ═══════════════════════════════════════
///   • همهٔ بکاپ‌های پوشه دیده می‌شوند: روزانه، 🛟 ایمنی، 🔒 رمزشده
///   • «👁 مشاهده» محتوای بکاپ را نشان می‌دهد و هیچ چیزی در دفتر یا خودِ فایل عوض نمی‌شود
///   • «♻ بازیابی» پیش از هر کاری می‌گوید چه چیزی جایگزین می‌شود (و «نه» ⇒ هیچ)
///   • فایلِ ناخوانا ⇒ پیامِ روشن، نه خالی؛ دوبار-کلیک روی ‎.pyq‎ ⇒ همان «مشاهده»
///
///     dotnet run --project PumpYaqobi.UiTests -c Release -- backupview
/// </summary>
internal static class BackupViewProbe
{
    private static int _bad;
    private static void Check(string what, bool ok, string? d = null)
    {
        Console.WriteLine((ok ? "  ✔ " : "  ✖ ") + what + (d is null ? "" : " — " + d));
        if (!ok) _bad++;
    }

    public static int Run()
    {
        var root = Path.Combine(Path.GetTempPath(), "pump-bview-" + Guid.NewGuid().ToString("N"));
        AppHost.Start(Path.Combine(root, "pump.db"));
        FakeLicense.Grant();
        AppBuilder.Configure<PumpYaqobi.App.App>().UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .SetupWithoutStarting();
        var win = new MainWindow { Width = 1440, Height = 1000 };
        win.Show(); Pump(win);
        var vm = (MainViewModel)win.DataContext!;
        LockIn.Wait(vm.Lock); Pump(win);
        Seed.Fill(AppHost.Current);
        var host = AppHost.Current;

        host.Backup.SnapshotToday();
        host.Backup.SafetyCopy();
        var pyq = host.WriteSyncBackup("پیش-از-آزمون");

        var settings = vm.Sections.First(x => x.Id == "settings");
        var page = (BackupSectionViewModel)settings.SubSections.First(x => x.Id == "backups");
        Wait(win, vm.GoAsync(settings));
        settings.ShowSubCommand.Execute(page);
        for (var i = 0; i < 40; i++) Pump(win);
        Wait(win, page.OnActivatedAsync()); Pump(win);

        Check("هر سه بکاپ در فهرست", page.AllBackups.Count == 3,
              string.Join(" | ", page.AllBackups.Select(b => b.Kind + " " + b.Name)));
        Check("عکسِ ایمنی هم دیده می‌شود", page.AllBackups.Any(b => b.Kind.Contains("ایمنی")));
        Check("پشتیبانِ رمزشده هم دیده می‌شود", pyq is null || page.AllBackups.Any(b => b.Name == Path.GetFileName(pyq)));

        page.ShowOlder = true;
        for (var i = 0; i < 30; i++) Pump(win);
        var eyes = win.GetVisualDescendants().OfType<Button>()
                      .Where(b => b.Content as string == "👁 مشاهده" && b.IsEffectivelyVisible).ToList();
        Check("دکمهٔ «👁 مشاهده» روی ردیف‌ها", eyes.Count >= 2, eyes.Count.ToString());

        var live = BackupPeeker.Read(host.DbPath);
        var target = page.AllBackups.First(b => b.Kind.Contains("روزانه"));
        var hash = FullBackup.Sha256(target.Entity.Path);
        var eye = eyes.FirstOrDefault(b => b.CommandParameter == target) ?? eyes.FirstOrDefault();
        if (eye?.Command?.CanExecute(eye.CommandParameter) == true) eye.Command.Execute(eye.CommandParameter);
        Until(win, () => page.IsViewing);
        var viewer = win.GetVisualDescendants().OfType<Border>().FirstOrDefault(b => b.Name == "BackupViewer");
        Check("پنجرهٔ مشاهده باز شد و دیده می‌شود", page.IsViewing && viewer?.IsEffectivelyVisible == true, page.ViewSummary);
        Check("شمارِ بخش‌ها نشان داده شد", page.ViewTables.Count >= 5 && page.ViewTables.Any(t => t.Label == "قرض‌داران"),
              string.Join(" · ", page.ViewTables.Select(t => t.Label + " " + t.Count)));
        Check("سلامتِ فایل گفته شد", page.ViewIntegrity.StartsWith("✅"), page.ViewIntegrity);
        SqliteConnection.ClearAllPools();
        Check("خودِ فایلِ بکاپ دست نخورد", FullBackup.Sha256(target.Entity.Path) == hash);
        Check("دفترِ زنده دست نخورد", BackupPeeker.Read(host.DbPath).Total == live.Total);

        string? asked = null;
        Dialogs.ConfirmHook = (_, msg) => { asked = msg; return false; };
        try { Wait(win, page.RestoreViewedCommand.ExecuteAsync(null)); }
        finally { Dialogs.ConfirmHook = null; }
        Check("بازیابی پیش از کار می‌پرسد و آن‌چه جایگزین می‌شود را می‌گوید",
              asked is not null && (asked.Contains("چه چیزی جایگزین می‌شود") || asked.Contains("یکی است")) && asked.Contains("عکسِ ایمنی"),
              asked?.Replace("\n", " ⏎ "));
        Check("«نه» ⇒ هیچ چیزی عوض نشد", BackupPeeker.Read(host.DbPath).Total == live.Total);

        var junk = Path.Combine(root, "خراب.db");
        File.WriteAllText(junk, "x");
        Wait(win, page.ViewFileAsync(junk));
        Check("فایلِ ناخوانا ⇒ پیامِ روشن", page.ViewSummary.StartsWith("❌") && page.ViewSummary.Contains("بکاپِ این برنامه نیست"), page.ViewSummary);

        if (pyq is not null)
        {
            page.CloseViewCommand.Execute(null); Pump(win);
            OpenRequest.Add(pyq);
            Until(win, () => page.IsViewing);
            Check("دوبار-کلیک روی ‎.pyq‎ ⇒ مشاهده (رمز باز شد)", page.IsViewing && page.ViewSummary.Contains("ردیف"), page.ViewSummary);
            Check("و فایلِ موقتی جا نماند", !Directory.EnumerateFiles(host.Backup.SnapshotDir, "tmp-*").Any());
        }

        Console.WriteLine(_bad == 0 ? "✅ همه سبز" : $"❌ {_bad} ایراد");
        return _bad == 0 ? 0 : 1;
    }

    private static void Until(Window win, Func<bool> done)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (!done() && sw.Elapsed < TimeSpan.FromSeconds(30)) { Pump(win); Thread.Sleep(10); }
        Pump(win);
    }
    private static void Wait(Window w, Task t)
    {
        for (var i = 0; i < 5000 && !t.IsCompleted; i++) { Pump(w); Thread.Sleep(2); }
        Pump(w);
        if (t.IsFaulted) throw t.Exception!;
    }
    private static void Pump(Window w) { for (var i = 0; i < 8; i++) { Dispatcher.UIThread.RunJobs(); w.UpdateLayout(); } }
}
