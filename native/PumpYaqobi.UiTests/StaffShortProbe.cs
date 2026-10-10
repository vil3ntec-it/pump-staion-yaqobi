using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PumpYaqobi.App.Printing;
using PumpYaqobi.App.Services;
using PumpYaqobi.App.ViewModels;
using PumpYaqobi.App.ViewModels.Sections;
using PumpYaqobi.App.Views;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Reporting.Pdf;
using QuestPDF.Fluent;

namespace PumpYaqobi.UiTests;

/// <summary>
/// ══ کمبودیِ کارمندان، ۱۴۰۵/۰۷/۱۸ — با پنجرهٔ واقعی ══════════════════════════════
///   • کشوی ماه فقط ورق‌های همان ماه؛ «همه» جمعِ هر دو
///   • «رسید کمبودی» ⇒ مبلغ در کادر و فوکوس روی کادر؛ «از کجا آمد» ورق‌ها را با تاریخ می‌گوید
///   • «رسید کمبودی گرفتم» ⇒ ثبت برای همان ماه + رسیدِ چاپی (PDF)؛ «🧾 چاپ» روی رسیدِ ثبت‌شده
///
///     dotnet run --project PumpYaqobi.UiTests -c Release -- staffshort
/// </summary>
internal static class StaffShortProbe
{
    private static int _bad;
    private static void Check(string what, bool ok, string? d = null)
    {
        Console.WriteLine((ok ? "  ✔ " : "  ✖ ") + what + (d is null ? "" : " — " + d));
        if (!ok) _bad++;
    }

    public static int Run()
    {
        var tmpDb = Path.Combine(Path.GetTempPath(), "pump-staffshort-" + Guid.NewGuid().ToString("N"), "pump.db");
        AppHost.Start(tmpDb);
        FakeLicense.Grant();
        AppBuilder.Configure<PumpYaqobi.App.App>().UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .SetupWithoutStarting();
        var win = new MainWindow { Width = 1500, Height = 1000 };
        win.Show(); Pump(win);
        var vm = (MainViewModel)win.DataContext!;
        LockIn.Wait(vm.Lock); Pump(win);

        var host = AppHost.Current;
        async Task Waraq(string date, string worker, decimal declared)
        {
            var w = await host.WaraqData.OpenOrCreateAsync(date, null);
            var s = w.Shifts.First(x => x.Kind == ShiftKind.Day);
            s.WorkerName = worker;
            await host.WaraqData.SaveShiftAsync(s);
            await host.WaraqData.SavePumpAsync(new WaraqPump { ShiftId = s.Id, SortIndex = 0, Num = 1, Debt = declared });
        }
        Wait(win, Waraq("1405/06/10", "کریم", 500m));
        Wait(win, Waraq("1405/07/02", "کریم", 300m));
        Wait(win, Waraq("1405/07/05", "رحیم", 200m));

        var att = vm.Sections.First(s => s.Id == "attendance");
        Wait(win, vm.GoAsync(att)); Settle(win);
        var sec = (StaffShortSectionViewModel)att.SubSections.First(s => s.Id == "staffshort");
        att.ShowSub(sec); Settle(win);
        Wait(win, sec.EnsureLoadedAsync()); Wait(win, sec.OnActivatedAsync()); Settle(win);

        decimal Short(string n) => sec.Rows.FirstOrDefault(r => r.Name == n)?.Row.RemainShort ?? -1;
        Check("«همه»: کریم ۸۰۰، رحیم ۲۰۰", Short("کریم") == 800m && Short("رحیم") == 200m, $"{Short("کریم")} / {Short("رحیم")}");

        var mizan = sec.Picker.Months.FirstOrDefault(m => m.Key == "1405/07");
        Check("کشوی ماه «۱۴۰۵/۰۷» را دارد", mizan is not null, string.Join(",", sec.Picker.Months.Select(m => m.Key)));
        if (mizan is not null) { sec.Picker.Selected = mizan; Settle(win); Wait(win, Task.Delay(60)); Settle(win); }
        Check("ماهِ میزان: کریم فقط ۳۰۰", Short("کریم") == 300m && Short("رحیم") == 200m, $"{Short("کریم")} / {Short("رحیم")}");

        var karim = sec.Rows.First(r => r.Name == "کریم");
        sec.PickShortCommand.Execute(karim); Settle(win);
        var box = win.GetVisualDescendants().OfType<TextBox>().FirstOrDefault(t => t.Name == "AmountBox");
        Check("«رسید کمبودی» ⇒ مبلغِ ۳۰۰ در کادر", sec.Amount == "300", sec.Amount);
        Check("و فوکوس روی کادرِ مبلغ", box is not null && box.IsFocused);
        Check("«از کجا آمد» فقط ورقِ ۱۴۰۵/۰۷/۰۲", sec.SelectedLines.Count == 1 && sec.SelectedLines[0].Title.Contains("1405/07/02"),
              string.Join(" | ", sec.SelectedLines.Select(l => l.Title)));

        PdfEngine.Initialize();
        var got = new List<Func<PageSetup, QuestPDF.Infrastructure.IDocument>>();
        Documents.CaptureHook = (b, _) => got.Add(b);
        sec.Amount = "120";
        Wait(win, sec.SettleShortCommand.ExecuteAsync(null)); Settle(win);
        Check("ثبت شد برای «1405/07»", sec.LastSettle?.ForMonth == "1405/07" && sec.LastSettle.RemainBefore == 300m,
              sec.LastSettle?.ForMonth + " / " + sec.LastSettle?.RemainBefore);
        Check("ماندهٔ کریم در میزان ۱۸۰", Short("کریم") == 180m, Short("کریم").ToString());
        var pdf1 = got.Count == 1 ? got[0](PageSetup.Default).GeneratePdf() : Array.Empty<byte>();
        Check("رسیدِ چاپی خودکار باز و PDF ساخته شد", pdf1.Length > 1000, got.Count + " سند، " + pdf1.Length + " بایت");

        got.Clear();
        var row = sec.Settles.FirstOrDefault();
        Check("رسید در دفترِ رسیدها با دورهٔ میزان", row is not null && row.ForText.Length > 0, row?.ForText);
        if (row is not null) Wait(win, sec.ReceiptCommand.ExecuteAsync(row));
        var pdf2 = got.Count == 1 ? got[0](PageSetup.Default).GeneratePdf() : Array.Empty<byte>();
        Check("«🧾 چاپ» رسیدِ ثبت‌شده را دوباره می‌سازد", pdf2.Length > 1000, pdf2.Length + " بایت");
        Documents.CaptureHook = null;

        var sonbola = sec.Picker.Months.FirstOrDefault(m => m.Key == "1405/06");
        if (sonbola is not null) { sec.Picker.Selected = sonbola; Settle(win); Wait(win, Task.Delay(60)); Settle(win); }
        Check("سنبله: کریم ۵۰۰ و رسیدِ میزان نیامد", Short("کریم") == 500m && sec.Settles.Count == 0,
              Short("کریم") + " / " + sec.Settles.Count);

        Console.WriteLine(_bad == 0 ? "✅ همه سبز" : $"❌ {_bad} ایراد");
        return _bad == 0 ? 0 : 1;
    }

    private static void Wait(Window w, Task t)
    {
        for (var i = 0; i < 3000 && !t.IsCompleted; i++) { Pump(w); Thread.Sleep(2); }
        Pump(w);
        if (t.IsFaulted) throw t.Exception!;
    }
    private static void Pump(Window w) { for (var i = 0; i < 8; i++) { Dispatcher.UIThread.RunJobs(); w.UpdateLayout(); } }
    private static void Settle(Window w) { for (var i = 0; i < 20; i++) { Pump(w); Thread.Sleep(5); } }
}
