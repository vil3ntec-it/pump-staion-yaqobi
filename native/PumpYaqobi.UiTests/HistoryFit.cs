using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PumpYaqobi.App.Controls;
using PumpYaqobi.App.Services;
using PumpYaqobi.App.ViewModels;
using PumpYaqobi.App.ViewModels.Sections;
using PumpYaqobi.App.Views;
using PumpYaqobi.Application.Localization;
using PumpYaqobi.Domain.Entities;

namespace PumpYaqobi.UiTests;

/// <summary>
/// ══ تاریخچه‌ها، با خواستهٔ صاحب ریپو (۱۴۰۵/۰۷/۱۲) ════════════════════════
///
///   پارچه‌ها: کارمند · روز/شب · شمارهٔ پایه · شروع · ختم
///   ورق‌ها:   جملهٔ هر شیفت — شمار و مبلغِ قرض، شمار و مبلغِ مصرف، فروش
///   صرافی:    تحویل به صرافی یا بردگیِ پمپ · مبلغ · فی · دالر
///   شرکت‌ها:  بردگیِ پمپ از شرکت (دالر) و رسیدِ پمپ = بردگیِ شرکت (با ارزش)
///   مصارف:    سرستونِ جدول با اسکرول نمی‌آید و نوارِ سفید نمی‌گذارد
///
///     dotnet run --project PumpYaqobi.UiTests -c Release -- historyfit [پوشهٔ عکس]
/// </summary>
internal static class HistoryFit
{
    private static int _bad;

    private static void Check(string what, bool ok, string? detail = null)
    {
        Console.WriteLine((ok ? "  ✔ " : "  ✖ ") + what + (detail is null ? "" : " — " + detail));
        if (!ok) _bad++;
    }

    public static int Run(string[] args)
    {
        var shots = args.Length > 1 ? args[1] : Path.Combine(Path.GetTempPath(), "pump-historyfit");
        Directory.CreateDirectory(shots);
        var dir = Path.Combine(Path.GetTempPath(), "pump-historyfit-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);

        AppHost.Start(Path.Combine(dir, "pump.db"));
        FakeLicense.Grant();
        AppBuilder.Configure<PumpYaqobi.App.App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .SetupWithoutStarting();
        var win = new MainWindow { Width = 1440, Height = 900 };
        win.Show();
        Pump(win);
        var vm = (MainViewModel)win.DataContext!;
        LockIn.Wait(vm.Lock);
        Settle(win);

        var h = AppHost.Current;
        Seed.Fill(h);
        //  مصارفِ فراوان — همان حالتی که جدول از «پنجرهٔ چسبان» (۶۰ ردیف) می‌گذرد
        for (var i = 1; i <= 150; i++)
            h.ExpenseLedger.AddAsync(new Expense
            {
                DateShamsi = Shamsi.Today(), Title = "مصرفِ شمارهٔ " + i, Amount = 100m * i,
            }).GetAwaiter().GetResult();

        var hist = (HistorySectionViewModel)vm.Sections.First(s => s.Id == "history");
        Wait(win, vm.GoAsync(hist));

        // ═══ مصارف: اسکرول ═════════════════════════════════════════════════
        Console.WriteLine();
        Console.WriteLine("════ مصارف — اسکرول ════");
        Wait(win, hist.OpenAsync("expense"));
        Settle(win);
        var sv = PageScroll(win);
        Check("صفحهٔ تاریخچه اسکرول دارد", sv is not null && sv.Extent.Height > sv.Viewport.Height + 50,
              sv is null ? "—" : $"{sv.Extent.Height:0} / {sv.Viewport.Height:0}");
        if (sv is not null)
        {
            foreach (var (at, name) in new[] { (0.0, "top"), (0.35, "mid"), (1.0, "bottom") })
            {
                sv.Offset = new Vector(0, Math.Max(0, sv.Extent.Height - sv.Viewport.Height) * at);
                Settle(win);
                Shot(win, shots, "expense-" + name);
                if (name != "mid") continue;

                //  ۱) سرستون با اسکرول نیامده: بالای دید نیست
                var grid = win.GetVisualDescendants().OfType<ExcelGrid>().First(g => g.IsEffectivelyVisible);
                var head = grid.GetVisualDescendants().OfType<Avalonia.Controls.DataGridColumnHeader>()
                               .Where(x => x.IsEffectivelyVisible && x.Bounds.Height > 0)
                               .Select(x => x.TranslatePoint(new Point(0, x.Bounds.Height), win)?.Y ?? -1).DefaultIfEmpty(-1).Max();
                Check("وسطِ اسکرول، سرستون همراهِ دید نیامده (بیرونِ دید است)", head <= 1, $"پایینِ سرستون {head:0}");
                //  ۲) نوارِ سفید نیست: نخستین ردیفِ دیده‌شده از بالای دید شروع می‌شود
                var rows = grid.GetVisualDescendants().OfType<DataGridRow>()
                               .Where(r => r.IsEffectivelyVisible && r.Bounds.Height > 0)
                               .Select(r => (Top: r.TranslatePoint(default, win)?.Y ?? 9999, Bot: r.TranslatePoint(new Point(0, r.Bounds.Height), win)?.Y ?? -1, r))
                               .ToList();
                var cover = rows.Where(r => r.Bot > 0 && r.Top < win.ClientSize.Height).ToList();
                var top = cover.Count == 0 ? 9999 : cover.Min(r => r.Top);
                var bot = cover.Count == 0 ? -1 : cover.Max(r => r.Bot);
                Check("بالای دید ردیف است، نه نوارِ سفید", top <= 0.5, $"نخستین ردیف از {top:0}");
                Check("پایینِ دید هم ردیف است (قاب کوتاه نمانده)", bot >= win.ClientSize.Height - 1, $"آخرین ردیف تا {bot:0} از {win.ClientSize.Height:0}");
                var nums = cover.OrderBy(r => r.Top).Select(r => r.r.GetIndex()).ToList();
                Check("ردیف‌ها پشتِ سرِ هم‌اند (نه جاافتاده، نه وارونه)",
                      nums.Zip(nums.Skip(1), (a, b) => b - a).All(d => d == 1), string.Join(",", nums.Take(6)));
            }
        }

        // ═══ ستون‌های هر بخش ══════════════════════════════════════════════════
        void Kind(string key, string[] heads, Func<HistoryRowViewModel, bool> sample, string what)
        {
            Console.WriteLine();
            Console.WriteLine($"════ {key} ════");
            Wait(win, hist.OpenAsync(key));
            Settle(win);
            var g = win.GetVisualDescendants().OfType<ExcelGrid>().FirstOrDefault(x => x.IsEffectivelyVisible);
            var got = g?.Columns.Select(c => c.Header as string ?? "").ToList() ?? new();
            Check($"{key}: ستون‌ها", heads.All(got.Contains), string.Join("، ", got));
            Check($"{key}: {what}", hist.Rows.Any(sample),
                  string.Join(" | ", hist.Rows.Take(2).Select(r => string.Join(" · ", r.Cells))));
            Check($"{key}: هر ردیف همهٔ خانه‌هایش را دارد", hist.Rows.All(r => r.Cells.Count == got.Count));
            if (PageScroll(win) is { } ps) { ps.Offset = default; Settle(win); }
            Shot(win, shots, "hist-" + key);
        }

        Kind("shift", new[] { "کارمند", "شیفت", "شمارهٔ پایه", "شروعِ پایه", "ختمِ پایه" },
             r => r.Cells.Count > 7 && r.Cells[3].Contains("روز") && r.Cells[4] != "—" && r.Cells[6] != "" && r.Cells[7] != "",
             "کارمند، روز/شب، شروع و ختم پر است");
        Kind("waraq", new[] { "شمارِ قرض", "جملهٔ قرض", "شمارِ مصرف", "جملهٔ مصرف", "جملهٔ فروش" },
             r => r.Cells.Count > 8 && r.Cells[8].Contains("افغانی") && r.Cells[1].Length > 0,
             "یک ردیف برای هر شیفت با جمله‌ها");
        Kind("sarrafi", new[] { "چه شد", "مبلغ", "واحد", "فی", "دالر" },
             r => r.Cells.Count > 6 && (r.Cells[1].Contains("تحویل به صرافی") || r.Cells[1].Contains("بردگیِ پمپ")),
             "تحویل به صرافی یا بردگیِ پمپ نوشته شده");
        Check("sarrafi: هم «تحویل به صرافی» هست هم «بردگیِ پمپ»",
              hist.Rows.Any(r => r.Cells[1].Contains("تحویل")) && hist.Rows.Any(r => r.Cells[1].Contains("بردگی")));
        Kind("company", new[] { "بردگیِ پمپ از شرکت ($)", "رسیدِ پمپ = بردگیِ شرکت", "تن" },
             r => r.Cells.Count > 8 && (r.Cells[5] != "—" || r.Cells[7] != "—"),
             "بردگیِ پمپ یا رسیدِ پمپ با ارزش");
        Check("company: ارزِ رسید نوشته شده (افغانی یا $)",
              hist.Rows.Where(r => r.Cells[7] != "—").All(r => r.Cells[7].EndsWith("افغانی") || r.Cells[7].EndsWith("$")));

        Console.WriteLine();
        Console.WriteLine(_bad == 0 ? "✅ تاریخچه‌ها سرِ جایشان‌اند" : $"❌ {_bad} ایراد");
        return _bad == 0 ? 0 : 1;
    }

    private static ScrollViewer? PageScroll(Window win) =>
        win.GetVisualDescendants().OfType<ScrollViewer>()
           .Where(s => s.IsEffectivelyVisible && s.Extent.Height > s.Viewport.Height + 4
                       && s.FindAncestorOfType<DataGrid>() is null)
           .OrderByDescending(s => s.Extent.Height).FirstOrDefault();

    private static void Shot(Window win, string dir, string name)
    {
        Settle(win);
        using var f = win.CaptureRenderedFrame();
        var path = Path.Combine(dir, name + ".png");
        f?.Save(path);
        Console.WriteLine("  📷 " + path);
    }

    private static void Pump(Window w)
    {
        for (var i = 0; i < 8; i++) { Dispatcher.UIThread.RunJobs(); w.UpdateLayout(); }
    }

    private static void Settle(Window w)
    {
        for (var i = 0; i < 40; i++) { Pump(w); Thread.Sleep(5); }
    }

    private static void Wait(Window w, Task t)
    {
        for (var i = 0; i < 400 && !t.IsCompleted; i++) { Pump(w); Thread.Sleep(2); }
        Settle(w);
    }
}
