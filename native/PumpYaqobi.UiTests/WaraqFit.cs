using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PumpYaqobi.App.Controls;
using PumpYaqobi.App.Printing;
using PumpYaqobi.App.Services;
using PumpYaqobi.App.ViewModels;
using PumpYaqobi.App.ViewModels.Sections;
using PumpYaqobi.App.Views;
using PumpYaqobi.Application.Localization;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;
using PumpYaqobi.Reporting.Pdf;
using PumpYaqobi.Services.Data;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace PumpYaqobi.UiTests;

/// <summary>
/// ══ ورق، با عکسِ صاحب ریپو (۱۴۰۵/۰۷/۱۲) — چهار خواسته، با رفتار ═══════════
///
///   ۱) «یادداشت و نوع و واحد از جدول بیرون نشوند — دیوارشان را بگذار.»
///   ۲) قرائتِ پمپ از پارچه: ستونِ «تاریخ» و «هشدار» نیست، تیلِ پارچه این‌جا
///      عوض نمی‌شود، و خودِ خانهٔ مشکل‌دار سرخ است و درجا می‌گوید چرا.
///   ۳) «داخلِ پی‌دی‌اف جملهٔ این شیفت نیست.»
///   ۴) «پرینتِ ورق‌ها خیلی بی‌کیفیت است و با زوم تارتر.»
///
///     dotnet run --project PumpYaqobi.UiTests -c Release -- waraqfit [پوشهٔ عکس]
/// </summary>
internal static class WaraqFit
{
    private static int _bad;

    private static void Check(string what, bool ok, string? detail = null)
    {
        Console.WriteLine((ok ? "  ✔ " : "  ✖ ") + what + (detail is null ? "" : " — " + detail));
        if (!ok) _bad++;
    }

    public static int Run(string[] args)
    {
        var shots = args.Length > 1 ? args[1] : Path.Combine(Path.GetTempPath(), "pump-waraqfit");
        Directory.CreateDirectory(shots);
        var dir = Path.Combine(Path.GetTempPath(), "pump-waraqfit-" + Guid.NewGuid().ToString("N"));
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
        var today = Shamsi.Today();

        //  ── دادهٔ واقعی از راهِ خودِ پارچه ─────────────────────────────────
        //  پارچهٔ اول: پایهٔ ۱ از ۱۰۰٬۰۰۰ تا ۱۲۰٬۰۰۰. پارچهٔ دوم همان پایه ولی از
        //  ۱۱۹٬۰۰۰ شروع می‌کند — هزار لیتر کمتر از ختمِ قبلی — و با نشان ثبت می‌شود.
        ShiftSaveResult Save(FuelType f, ShiftKind k, int num, decimal a, decimal b, bool low, bool fresh) =>
            h.ParchaData.SaveShiftFlowAsync(new ShiftSaveRequest(
                f, k, today, f == FuelType.Diesel ? "کریم" : "هارون", num, a, b, 100m,
                f == FuelType.Diesel ? 0m : 10_000m, 0m, 0m, "", 0m, fresh, low)).GetAwaiter().GetResult();
        var r1 = Save(FuelType.Petrol, ShiftKind.Day, 1, 100_000m, 120_000m, false, false);
        var r2 = Save(FuelType.Petrol, ShiftKind.Day, 1, 119_000m, 125_000m, true, true);
        var r3 = Save(FuelType.Diesel, ShiftKind.Day, 1, 25_000m, 26_000m, false, false);
        Check("سه پارچه ثبت شد و به ورق رفت", r1.Ok && r2.Ok && r3.Ok,
              string.Join(" · ", new[] { r1, r2, r3 }.Select(r => r.Ok ? "✔" : r.Error)));

        var w = h.WaraqData.OpenOrCreateAsync(today, "").GetAwaiter().GetResult();
        var day = w.Shifts.First(x => x.Kind == ShiftKind.Day);
        //  یادداشتِ بلند روی پایه‌ها و ردیف‌های تراکنش با نامِ بلند — همان چیزی که از کادر بیرون می‌زد
        foreach (var p in day.Pumps)
        {
            p.Note = "یادداشتِ بلندِ همین پایه برای سنجشِ دیوار — کارمند گفت پایه یک ساعت خاموش بود و بعد دوباره روشن شد";
            h.WaraqData.SavePumpAsync(p).GetAwaiter().GetResult();
        }
        foreach (var (t, i) in day.Transactions.OrderBy(x => x.SortIndex).Take(6).Select((t, i) => (t, i)))
        {
            t.Name = "محمد نبی احمدزی و پسران " + (i + 1);
            t.Liters = 20 + i;
            t.Type = i % 2 == 0 ? WaraqTxnType.Debt : WaraqTxnType.Expense;
            h.WaraqData.SaveTxnAsync(t).GetAwaiter().GetResult();
        }

        AppSettings.SaveColumnWidths("waraq.txns", new double[] { 700, 260, 260, 180, 180 });
        AppSettings.SaveColumnWidths("waraq.pumps", Enumerable.Repeat(260.0, 10).ToArray());
        var wq = (WaraqSectionViewModel)vm.Sections.First(s => s.Id == "waraq");
        Wait(win, vm.GoAsync(wq));

        wq.OpenCommand.Execute(wq.Sheets.First(x => x.Id == w.Id));
        Settle(win);
        var page = wq.Page!;
        for (var i = 0; i < 60 && page.Pumps.All(p => !p.LowBase || p.StartIssue.Contains("پارچه‌ها بررسی کنید") == false); i++)
        { Pump(win); Thread.Sleep(10); }
        Settle(win);
        ScrollToBottom(win);

        // ═══ ۱) دیوار ══════════════════════════════════════════════════════
        Console.WriteLine();
        Console.WriteLine("════ ۱) ستون‌ها از کادرِ جدول بیرون نمی‌زنند ════");
        //  الف) پهنای ذخیره‌شده‌ای که از قاب بزرگ‌تر است (روی نمایشگرِ بزرگ‌تر
        //  کشیده شده) — **پیش از** نخستین باز شدنِ جدول‌ها روی دیسک نشسته
        foreach (var width in new[] { 1440.0, 1366, 1280, 1100 })
        {
            win.Width = width;
            Settle(win);
            ScrollToBottom(win);
            Walls(win, $"ذخیره‌شدهٔ پهن · پنجرهٔ {width}");
            if (width is 1280) Shot(win, shots, "waraq-saved-wide");
        }
        Check("پهنای ذخیره‌شدهٔ کاربر روی دیسک دست نخورد",
              AppSettings.LoadColumnWidths("waraq.txns")?.FirstOrDefault() == 700);

        //  ب) بی پهنای ذخیره‌شده، با یادداشتِ بلند — مسیرِ «جای اضافه نیست» که
        //  پیش از این ستونِ «#» را نمی‌شمرد و به همان اندازه بیرون می‌زد
        foreach (var g in win.GetVisualDescendants().OfType<ExcelGrid>().Where(g => g.KeepInside).ToList())
            g.WidthScope = "fresh";
        //  فهرست دوباره پر شود (شب و برگشت به روز) — همان کاری که باز کردنِ ورقِ دیگر می‌کند
        page.SetNightCommand.Execute("night"); Settle(win);
        page.SetNightCommand.Execute("day"); Settle(win);
        foreach (var width in new[] { 1440.0, 1366, 1280, 1100 })
        {
            win.Width = width;
            Settle(win);
            ScrollToBottom(win);
            Walls(win, $"پهنای خودکار · پنجرهٔ {width}");
            if (width is 1366) Shot(win, shots, "waraq-" + width);
        }
        win.Width = 1440;
        Settle(win);
        ScrollToBottom(win);
        Walls(win, "دوباره ۱۴۴۰");
        if (PumpGrid(win) is ExcelGrid pg)
        {
            var sum = pg.Columns.Where(c => c.IsVisible).Sum(c => c.ActualWidth) + (double.IsNaN(pg.RowHeaderWidth) ? 0 : pg.RowHeaderWidth);
            Check("پنجره که بزرگ شد، ستون‌ها دوباره باز می‌شوند (کنارِ جدول خالی نمی‌ماند)",
                  sum >= pg.Bounds.Width - 12, $"ستون‌ها {sum:0} · کادر {pg.Bounds.Width:0}");
            //  یادداشتِ فارسیِ بلند: آغازِ جمله دیده می‌شود، نه فقط «…»
            var note = pg.GetVisualDescendants().OfType<TextBlock>().FirstOrDefault(t => t.Text?.Contains("خاموش بود", StringComparison.Ordinal) == true);
            Check("یادداشتِ فارسیِ بلند فقط «…» نمی‌شود — خطِ اولش دیده می‌شود",
                  note is { TextTrimming: var ntt, TextWrapping: Avalonia.Media.TextWrapping.Wrap, MaxLines: 1 } && ntt == Avalonia.Media.TextTrimming.None,
                  note is null ? "پیدا نشد" : $"{note.TextWrapping} · {note.TextTrimming} · {note.MaxLines}");
            Shot(win, shots, "waraq-note");
        }

        // ═══ ۲) قرائتِ پمپ ════════════════════════════════════════════════
        Console.WriteLine();
        Console.WriteLine("════ ۲) قرائتِ پمپ از پارچه ════");
        var grid = PumpGrid(win);
        var heads = grid?.Columns.Where(c => c.IsVisible).Select(c => c.Header as string ?? "").ToList() ?? new();
        Check("ستونِ «تاریخ» نیست", !heads.Contains("تاریخ"), string.Join("، ", heads));
        Check("ستونِ «هشدار» نیست", !heads.Contains("هشدار"));

        var fromParcha = page.Pumps.Where(p => p.FromParcha).ToList();
        Check("هر سه پایهٔ پارچه در ورق‌اند", fromParcha.Count == 3, fromParcha.Count.ToString());
        var diesel = fromParcha.FirstOrDefault(p => p.Fuel == FuelType.Diesel);
        diesel?.ToggleFuelCommand.Execute(null);
        Check("تیلِ پایهٔ پارچه این‌جا عوض نمی‌شود", diesel?.Fuel == FuelType.Diesel);
        var chips = grid?.GetVisualDescendants().OfType<Button>()
                        .Count(b => b.Classes.Contains("fuelchip") && b.IsEffectivelyVisible) ?? -1;
        var locks = grid?.GetVisualDescendants().OfType<Border>()
                        .Count(b => b.Classes.Contains("locked") && b.IsEffectivelyVisible) ?? -1;
        Check("کپسولِ تیلِ پایه‌های پارچه دکمه نیست (قفل)", chips == 0 && locks == 3, $"دکمه {chips} · قفل {locks}");

        var low = fromParcha.FirstOrDefault(p => p.LowBase);
        //  ⛔ دو خطِ کوتاه (۱۴۰۵/۰۷/۱۳، «روشن‌تر و خلاصه‌تر»): خطِ اول چه شده با فرق،
        //  خطِ دوم خودِ دو عدد — هر خط کوتاه، و هیچ خطِ سومی نیست.
        var lines = low?.StartIssue.Split('\n') ?? Array.Empty<string>();
        Check("پایهٔ کمتر نشان دارد و پیامش دو خطِ کوتاه است: «شروع ۱٬۰۰۰ لیتر کمتر است» و دو عدد",
              low is not null && lines.Length == 2 && lines[0].Contains("1,000") && lines[0].Contains("کمتر")
              && lines[1].Contains("120,000") && lines[1].Contains("119,000") && lines.All(l => l.Length <= 60),
              low?.StartIssue);
        var ok = fromParcha.Where(p => !p.LowBase).ToList();
        Check("پایه‌های سالم هیچ پیامی ندارند", ok.All(p => p.StartIssue == "" && p.EndIssue == ""));

        //  خودِ خانه: لبهٔ سرخ و مثلث دیده می‌شوند، و پیامش همان ‎ToolTip‎ است
        //  ⛔ پیام روی **خودِ خانه** است (۱۴۰۵/۰۷/۱۳): «فقط روی عدد پیام می‌آید، نه روی همهٔ کادرِ سرخ»
        var red = grid?.GetVisualDescendants().OfType<DataGridCell>()
                      .Where(c => IssueTextColumn.MessageOf(c).Length > 0).ToList() ?? new();
        Check("فقط یک خانه سرخ است (شروعِ همان پایه)", red.Count == 1, red.Count.ToString());
        var redCell = red.FirstOrDefault();
        var hostIn = redCell?.GetVisualDescendants().OfType<Grid>().FirstOrDefault(g => g.Classes.Contains("issuecell"));
        Check("پیام روی کلِ خانه است، نه فقط روی نوشتهٔ داخلش",
              redCell is not null && hostIn is not null && ToolTip.GetTip(hostIn) is null
              && ToolTip.GetTip(redCell) is not null,
              $"خانه {redCell?.Bounds.Width:0}×{redCell?.Bounds.Height:0} · نوشته {hostIn?.Bounds.Width:0}×{hostIn?.Bounds.Height:0}");
        Check("خطِ خودِ همان خانه سرخ است (کلاسِ issue روی خودِ خانه، نه کادرِ دوم)",
              redCell?.Classes.Contains("issue") == true && redCell.BorderThickness.Left >= 2);
        Check("هیچ کادرِ دومی داخلِ خانه نیست",
              hostIn?.Children.OfType<Border>().Any() == false);
        var redCells = grid?.GetVisualDescendants().OfType<DataGridCell>().Count(c => c.Classes.Contains("issue")) ?? -1;
        Check("فقط همان یک خانه خطِ سرخ دارد", redCells == 1, redCells.ToString());
        Check("پیامِ خانه همان پیامِ ردیف است و کامل (نه «…»)",
              red.FirstOrDefault() is { } rm && IssueTextColumn.MessageOf(rm) == low?.StartIssue
              && ToolTip.GetTip(rm) is Panel pn && pn.Children.OfType<TextBlock>().All(t => t.TextTrimming == Avalonia.Media.TextTrimming.None));
        Check("پیام درجا (بی تأخیر) و بالای کادر باز می‌شود",
              red.FirstOrDefault() is { } rh && ToolTip.GetShowDelay(rh) == 0 && ToolTip.GetPlacement(rh) == PlacementMode.Top);

        //  ⛔ انتخابِ خانه (کلیک/صفحه‌کلید) هیچ کادری خودکار باز نمی‌کند — فقط ماوس
        if (grid is not null && low is not null && red.Count == 1)
        {
            grid.SelectedItem = low;
            grid.CurrentColumn = grid.Columns.First(c => (c.Header as string) == "شروع");
            Settle(win);
            Check("انتخابِ همان خانه هیچ کادری خودکار باز نمی‌کند", !ToolTip.GetIsOpen(red[0]));
            ToolTip.SetIsOpen(red[0], true);      // همان چیزی که ماوس می‌کند
            Settle(win);
            Shot(win, shots, "waraq-issue");
            ToolTip.SetIsOpen(red[0], false);
        }

        //  ختمِ کمتر از شروع ⇒ خانهٔ «ختم» سرخ
        var manual = page.Pumps.FirstOrDefault(p => !p.FromParcha);
        if (manual is null)
        {
            page.AddPumpCommand.Execute(null);
            Settle(win);
            manual = page.Pumps.FirstOrDefault(p => !p.FromParcha);
        }
        if (manual is not null)
        {
            manual.StartText = "500"; manual.EndText = "400";
            Settle(win);
            Check("ختمِ کمتر از شروع ⇒ خانهٔ «ختم» سرخ و دلیلش گفته می‌شود",
                  manual.EndIssue.StartsWith("⚠️ ختم از شروع کمتر است") && manual.EndIssue.Contains("500")
                  && manual.EndIssue.Contains("400") && manual.StartIssue == "", manual.EndIssue);
            manual.ToggleFuelCommand.Execute(null);
            Check("پایهٔ دستی (نه از پارچه) همچنان تیلش عوض می‌شود", manual.Fuel == FuelType.Diesel);
        }
        else Check("پایهٔ دستی ساخته شد", false);

        //  برداشتنِ نشان — همان دکمهٔ «عادی شد»ِ قبلی، حالا در منوی راست‌کلیک
        if (low is IFlaggedRow f)
        {
            f.ClearFlagCommand.Execute(null);
            Settle(win);
            Check("«نشانِ سرخ را بردار» پیام را برمی‌دارد و هیچ عددی عوض نمی‌شود",
                  low.StartIssue == "" && low.Start == 119_000m && !low.LowBase);
        }

        // ═══ ۳) جملهٔ این شیفت در PDF ═══════════════════════════════════════
        Console.WriteLine();
        Console.WriteLine("════ ۳) PDFِ ورق ════");
        PdfEngine.Initialize();
        var input = new WaraqReportInput("پمپ یعقوبی", today, ShiftKind.Day, page.Shift!, "");
        var doc = new WaraqReport(input, h.Waraq) { Setup = PageSetup.Default };
        var rows = WaraqReport.ShiftTotalRows(page.Shift!);
        var tot = rows.LastOrDefault();
        Check("ردیفِ «جمله این شیفت» ته جدولِ پایه‌های PDF هست", tot.Label?.Contains("جمله این شیفت") == true,
              string.Join(" · ", rows.Select(r => $"{r.Label}: {r.Liters:0} لیتر، فروش {r.Sales:0}، قرض {r.Debt:0}")));
        Check("جملهٔ پطرول و دیزل جدا نوشته شده", rows.Any(r => r.Label.Contains("جمله پطرول")) && rows.Any(r => r.Label.Contains("جمله دیزل")));
        var t2 = h.Waraq.ShiftTotals(page.Shift!);
        Check("عددهای PDF همان عددهای صفحهٔ ورق‌اند (لیتر و فروش)",
              tot.Liters == t2.PetrolLiters + t2.DieselLiters && Math.Round(tot.Sales) == Math.Round(t2.Sales),
              $"PDF {tot.Liters:0}/{tot.Sales:0} · صفحه {t2.PetrolLiters + t2.DieselLiters:0}/{t2.Sales:0}");
        var img = doc.GenerateImages(new ImageGenerationSettings { ImageFormat = ImageFormat.Png, RasterDpi = 110 }).First();
        File.WriteAllBytes(Path.Combine(shots, "waraq-pdf.png"), img);
        Console.WriteLine("  📷 " + Path.Combine(shots, "waraq-pdf.png"));

        // ═══ ۴) کیفیتِ پیش‌نمایشِ چاپ ═══════════════════════════════════════
        Console.WriteLine();
        Console.WriteLine("════ ۴) پیش‌نمایشِ چاپ ════");
        var pv = new DocumentPreviewViewModel(s => new WaraqReport(input, h.Waraq) { Setup = s }, "ورق", PageSetup.Default);
        var pw = new DocumentPreviewWindow(pv) { Width = 1440, Height = 900 };
        pw.Show(win);
        Pump(pw);
        var until = DateTime.UtcNow + TimeSpan.FromMinutes(2);
        while (!pv.AllRendered && DateTime.UtcNow < until) { Pump(pw); Thread.Sleep(5); }
        var first = pv.PagesDpi;
        until = DateTime.UtcNow + TimeSpan.FromSeconds(60);
        while (pv.PagesDpi < pv.WantDpi() && DateTime.UtcNow < until) { Pump(pw); Thread.Sleep(10); }
        Pump(pw);
        Check("پس از آمدنِ ورق‌ها، ورقِ تیز جای ورقِ ۹۶ نقطه‌ای می‌نشیند",
              pv.PagesDpi >= DocumentPreviewViewModel.SharpMinDpi && pv.PagesDpi > first,
              $"{first} ⇒ {pv.PagesDpi} نقطه · پهنای ورق روی صفحه {pv.PageWidth:0}");
        var px = pv.CurrentPage?.PixelSize.Width ?? 0;
        Check("تصویرِ ورق از پهنایی که نشان داده می‌شود کم‌پیکسل‌تر نیست (یعنی کش داده نمی‌شود)",
              px >= pv.PageWidth, $"{px} پیکسل برای {pv.PageWidth:0}");
        Shot(pw, shots, "print-sharp");

        for (var i = 0; i < 4; i++) pv.ZoomInCommand.Execute(null);
        until = DateTime.UtcNow + TimeSpan.FromSeconds(60);
        while (pv.PagesDpi < pv.WantDpi() && DateTime.UtcNow < until) { Pump(pw); Thread.Sleep(10); }
        Pump(pw);
        px = pv.CurrentPage?.PixelSize.Width ?? 0;
        Check("با زوم، ورق دوباره و تیزتر ساخته می‌شود (نه همان تصویرِ کش‌داده)",
              pv.PagesDpi >= pv.WantDpi() && (px >= pv.PageWidth || pv.PagesDpi == DocumentPreviewViewModel.SharpMaxDpi),
              $"{pv.PagesDpi} نقطه · {px} پیکسل برای {pv.PageWidth:0}");
        Shot(pw, shots, "print-zoom");
        pw.Close();

        Console.WriteLine();
        Console.WriteLine(_bad == 0 ? "✅ هر چهار خواسته سرِ جایش بود" : $"❌ {_bad} ایراد");
        return _bad == 0 ? 0 : 1;
    }

    // ── ابزار ───────────────────────────────────────────────────────────────

    private static void Walls(Window win, string at)
    {
        var grids = win.GetVisualDescendants().OfType<ExcelGrid>()
                       .Where(g => g.IsEffectivelyVisible && g.KeepInside).ToList();
        Check($"{at}: سه جدولِ ورق دیده می‌شوند", grids.Count == 3, grids.Count.ToString());
        foreach (var g in grids)
        {
            var name = string.Join("/", g.Columns.Where(c => c.IsVisible).Select(c => c.Header as string).Take(3));
            var cols = g.Columns.Where(c => c.IsVisible).Sum(c => c.ActualWidth)
                     + (g.HeadersVisibility.HasFlag(DataGridHeadersVisibility.Row) && !double.IsNaN(g.RowHeaderWidth) ? g.RowHeaderWidth : 0);
            var hbar = g.GetVisualDescendants().OfType<ScrollBar>()
                        .Any(b => b.Orientation == Avalonia.Layout.Orientation.Horizontal && b.IsEffectivelyVisible && b.Maximum > 1);
            //  و هیچ خانه‌ای (کپسول، یادداشت) از لبهٔ جدول بیرون نزده
            var edge = LeftRight(g, win);
            var outside = g.GetVisualDescendants().OfType<DataGridCell>()
                           //  ⚠️ خانهٔ پُرکنندهٔ خودِ ‎DataGrid‎ (بی محتوا، بیرونِ ستون‌ها) شمرده نمی‌شود
                           .Where(c => c.IsEffectivelyVisible && c.Bounds.Width > 0 && c.Content is not null)
                           .Select(c => LeftRight(c, win))
                           .Where(r => r.L < edge.L - 1 || r.R > edge.R + 1).ToList();
            if (outside.Count > 0 && Environment.GetEnvironmentVariable("PUMP_WF_DEBUG") == "1")
                Console.WriteLine($"     لبه {edge.L:0}…{edge.R:0} · بیرون: " + string.Join(" ", outside.Take(4).Select(r => $"{r.L:0}…{r.R:0}")));
            Check($"{at}: «{name}…» داخلِ دیوار است", cols <= g.Bounds.Width + 2 && !hbar && outside.Count == 0,
                  $"ستون‌ها {cols:0} · کادر {g.Bounds.Width:0} · خانهٔ بیرون‌زده {outside.Count}" + (hbar ? " · نوارِ افقی" : ""));
        }
    }

    private static (double L, double R) LeftRight(Visual v, Visual root)
    {
        var a = v.TranslatePoint(default, root)?.X ?? 0;
        var b = v.TranslatePoint(new Point(v.Bounds.Width, 0), root)?.X ?? 0;
        return (Math.Min(a, b), Math.Max(a, b));
    }

    private static DataGrid? PumpGrid(Window win) =>
        win.GetVisualDescendants().OfType<DataGrid>()
           .FirstOrDefault(x => x.IsEffectivelyVisible && x.Columns.Any(c => (c.Header as string) == "ختم"));

    private static void ScrollToBottom(Window win)
    {
        foreach (var sv in win.GetVisualDescendants().OfType<ScrollViewer>().Where(s => s.IsEffectivelyVisible))
            sv.Offset = new Vector(sv.Offset.X, 0);
        Settle(win);
        //  جدول‌های تراکنش تنبل‌اند — تا صفحه به آن‌ها نرسد ساخته نمی‌شوند
        foreach (var sv in win.GetVisualDescendants().OfType<ScrollViewer>().Where(s => s.IsEffectivelyVisible && s.Extent.Height > s.Viewport.Height + 4))
            sv.Offset = new Vector(sv.Offset.X, Math.Max(0, sv.Extent.Height - sv.Viewport.Height) / 2);
        Settle(win);
    }

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
