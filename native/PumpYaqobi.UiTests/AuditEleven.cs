using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PumpYaqobi.App.Services;
using PumpYaqobi.App.Themes;
using PumpYaqobi.App.ViewModels;
using PumpYaqobi.App.ViewModels.Sections;
using PumpYaqobi.App.Views;
using PumpYaqobi.Application.Localization;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;

namespace PumpYaqobi.UiTests;

/// <summary>
/// ══ بازبینیِ فهرستِ یازده‌تایی (۱۴۰۵/۰۷/۱۲) — چهار بندی که هنوز سنجه نداشتند ══
///
/// خواستهٔ صاحب ریپو: «دقیق چک کن که کدوم‌ها جا مونده یا کدوم‌ها نصفه یا حتی
/// اجرا نشده». بقیهٔ بندها سنجهٔ خودشان را دارند (‎persist‎ · ‎keys‎ ·
/// ‎printpages‎ · ‎look‎)؛ این چهار تا تا امروز فقط «درست شد» گفته شده بودند:
///
///   ۷) ماه را عوض کردم ⇒ سرستون‌ها رفتند کنج
///   ۸) دارک مود — نوشته‌ها رفتند کنجِ چپ یا راست
///   ۹) ثبتِ خرید در مخزن ⇒ فرم همان لحظه جلوی چشم، بی اسکرول
///   ۱) جدولِ پایه‌های ورق روی کامپیوترِ کوچک‌تر از کادر بیرون می‌زند
///
///     dotnet run --project PumpYaqobi.UiTests -c Release -- audit11 [پوشهٔ عکس]
/// </summary>
internal static class AuditEleven
{
    private static int _bad;

    private static void Check(string what, bool ok, string? detail = null)
    {
        Console.WriteLine((ok ? "  ✔ " : "  ✖ ") + what + (detail is null ? "" : " — " + detail));
        if (!ok) _bad++;
    }

    public static int Run(string[] args)
    {
        var shots = args.Length > 1 ? args[1] : Path.Combine(Path.GetTempPath(), "pump-audit11");
        Directory.CreateDirectory(shots);
        var dir = Path.Combine(Path.GetTempPath(), "pump-audit11-" + Guid.NewGuid().ToString("N"));
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
        Seed();
        Settle(win);

        foreach (var theme in new[] { PumpTheme.Blue, PumpTheme.Gold })
        {
            ThemeManager.Apply(theme);
            Settle(win);
            Console.WriteLine();
            Console.WriteLine($"════ تمِ «{theme.Id}» ════");
            MonthSwitch(win, vm, shots, theme.Id);
        }
        ThemeManager.Apply(PumpTheme.Blue);
        Settle(win);

        Console.WriteLine();
        Console.WriteLine("════ ۹) ثبتِ خرید در مخزن ════");
        StorageBuy(win, vm, shots);

        Console.WriteLine();
        Console.WriteLine("════ ۱) جدولِ پایه‌های ورق، روی پنجرهٔ کوچک ════");
        PumpsGrid(win, vm, shots);

        Console.WriteLine();
        Console.WriteLine(_bad == 0 ? "✅ هر چهار بند سرِ جایش بود" : $"❌ {_bad} ایراد");
        return _bad == 0 ? 0 : 1;
    }

    private const string PrevMonth = "1405/06";

    private static void Seed()
    {
        var h = AppHost.Current;
        var now = Shamsi.ThisMonth();
        for (var i = 1; i <= 12; i++)
        {
            h.ExpenseLedger.AddAsync(new Expense
            { DateShamsi = now + "/" + (i % 9 + 1).ToString("00"), Title = "برق و آبِ دکان شمارهٔ " + i,
              Amount = 1500m * i, Note = "یادداشتِ نسبتاً بلندِ ردیفِ " + i }).GetAwaiter().GetResult();
            h.ExpenseLedger.AddAsync(new Expense
            { DateShamsi = PrevMonth + "/" + (i % 9 + 10).ToString("00"), Title = "خرج " + i,
              Amount = 20m * i, Note = "" }).GetAwaiter().GetResult();
        }
        for (var i = 0; i < 40; i++)
            h.StorageData.AddPurchaseAsync(new FuelPurchase
            {
                Fuel = FuelType.Petrol, DateShamsi = PrevMonth + "/" + (i % 28 + 1).ToString("00"),
                Seller = "", Kg = 5000m + i, Density = 0.74m, PriceTon = 900m, UsdRate = 70m, Note = "",
            }).GetAwaiter().GetResult();
    }

    // ── ۷ و ۸ ───────────────────────────────────────────────────────────────

    private static void MonthSwitch(MainWindow win, MainViewModel vm, string shots, string theme)
    {
        var ex = vm.Sections.First(s => s.Id == "expenses");
        Wait(win, vm.GoAsync(ex));
        var now = Shamsi.ThisMonth();
        foreach (var (m, step) in new[] { (PrevMonth, "رفت"), (now, "برگشت"), (PrevMonth, "دوباره رفت"), (now, "دوباره برگشت") })
        {
            ((dynamic)ex).Month = m;
            Settle(win);
            var g = Grid(win);
            if (g is null) { Check($"{step}: جدول پیدا شد", false); continue; }
            var (ok, why) = Aligned(g);
            Check($"{step} ({m}): هر سرستون روی ستونِ خودش است و به کلِ پهنا پخش شده", ok, why);
        }
        Shot(win, shots, "month-" + theme);
    }

    /// <summary>
    /// «سرستون‌ها رفتند کنج»: سرستون‌ها با خانه‌های ردیفِ اول جور نیستند یا
    /// روی هم در یک گوشه جمع شده‌اند. هر دو این‌جا با عدد سنجیده می‌شوند، و
    /// نوشتهٔ هر سرستون هم باید داخلِ خودِ همان سرستون وسط باشد.
    /// </summary>
    private static (bool, string) Aligned(DataGrid g)
    {
        var heads = g.GetVisualDescendants().OfType<DataGridColumnHeader>()
                     .Where(h => h.IsEffectivelyVisible && h.Content is string s && s.Length > 0).ToList();
        var row = g.GetVisualDescendants().OfType<DataGridRow>().FirstOrDefault(r => r.IsEffectivelyVisible);
        if (heads.Count == 0 || row is null) return (false, "سرستون یا ردیف نبود");
        var cells = row.GetVisualDescendants().OfType<DataGridCell>().Where(c => c.IsEffectivelyVisible).ToList();
        var worst = 0.0; var sum = 0.0; var textOff = 0.0;
        foreach (var h in heads)
        {
            var hx = h.TranslatePoint(default, g)!.Value.X;
            sum += h.Bounds.Width;
            var cell = cells.MinBy(c => Math.Abs(c.TranslatePoint(default, g)!.Value.X - hx));
            if (cell is null) continue;
            var cx = cell.TranslatePoint(default, g)!.Value.X;
            worst = Math.Max(worst, Math.Max(Math.Abs(cx - hx), Math.Abs(cell.Bounds.Width - h.Bounds.Width)));
            var tb = h.GetVisualDescendants().OfType<TextBlock>().FirstOrDefault(t => t.IsEffectivelyVisible && t.Text?.Length > 0);
            if (tb is not null && h.Bounds.Width > tb.Bounds.Width + 8)
            {
                var mid = tb.TranslatePoint(new Point(tb.Bounds.Width / 2, 0), h)!.Value.X;
                textOff = Math.Max(textOff, Math.Abs(mid - h.Bounds.Width / 2));
            }
        }
        var spread = sum / Math.Max(1, g.Bounds.Width);
        var ok = worst <= 2 && spread >= 0.8 && textOff <= 6;
        return (ok, $"بیشترین جابه‌جایی {worst:0.#}px · پخش {spread:P0} · نوشتهٔ سرستون از وسط {textOff:0.#}px");
    }

    // ── ۹ ───────────────────────────────────────────────────────────────────

    private static void StorageBuy(MainWindow win, MainViewModel vm, string shots)
    {
        var st = (StorageSectionViewModel)vm.Sections.First(s => s.Id == "storage");
        Wait(win, vm.GoAsync(st));
        var sv = win.GetVisualDescendants().OfType<ScrollViewer>()
                    .Where(s => s.IsEffectivelyVisible).MaxBy(s => s.Extent.Height);
        Check("بخشِ مخزن با چهل خرید بلندتر از صفحه است", sv is not null && sv.Extent.Height > sv.Viewport.Height * 1.5,
              sv is null ? "" : $"{sv.Extent.Height:0} در برابرِ {sv.Viewport.Height:0}");
        //  کاربر تهِ فهرست است — همان‌جا که «ثبت خرید» را از فهرست می‌زند
        foreach (var at in new[] { "بالای صفحه", "تهِ صفحه" })
        {
            if (sv is not null) sv.Offset = new Vector(0, at == "بالای صفحه" ? 0 : sv.Extent.Height);
            Settle(win);
            st.OpenBuyCommand.Execute(null);
            Settle(win);
            var card = win.GetVisualDescendants().OfType<Border>().FirstOrDefault(b => b.Name == "BuyCard");
            var top = card?.TranslatePoint(default, win)?.Y ?? -1;
            var bottom = top + (card?.Bounds.Height ?? 0);
            Check($"{at}: فرمِ خرید همان لحظه کامل جلوی چشم است",
                  card is not null && top >= 0 && bottom <= win.ClientSize.Height,
                  $"بالا {top:0} · پایین {bottom:0} · پنجره {win.ClientSize.Height:0}");
            var focused = TopLevel.GetTopLevel(win)?.FocusManager?.GetFocusedElement() as TextBox;
            Check($"{at}: نشانگر در نخستین کادرِ فرم است", focused is not null && card!.IsVisualAncestorOf(focused));
            //  «قفل بشه»: چرخِ ماوس فرم را از جلوی چشم نمی‌برد
            if (card is not null)
            {
                var mid = card.TranslatePoint(new Point(card.Bounds.Width / 2, card.Bounds.Height / 2), win)!.Value;
                for (var k = 0; k < 6; k++) win.MouseWheel(mid, new Vector(0, -3));
                Settle(win);
                var top2 = card.TranslatePoint(default, win)!.Value.Y;
                Check($"{at}: چرخِ ماوس فرم را از جلوی چشم نمی‌برد",
                      top2 >= 0 && top2 + card.Bounds.Height <= win.ClientSize.Height, $"بالا {top2:0}");
            }
            if (at == "تهِ صفحه") Shot(win, shots, "storage-buy");
            st.CancelBuyCommand.Execute(null);
            Settle(win);
        }
    }

    // ── ۱ ───────────────────────────────────────────────────────────────────

    private static void PumpsGrid(MainWindow win, MainViewModel vm, string shots)
    {
        var h = AppHost.Current;
        //  دادهٔ واقعی **پیش از** باز شدنِ ورق — همان ورقی که کاربر فردا باز می‌کند
        var w = h.WaraqData.OpenOrCreateAsync(Shamsi.Today(), "").GetAwaiter().GetResult();
        var day = w.Shifts.First(x => x.Kind == PumpYaqobi.Domain.Entities.ShiftKind.Day);
        for (var i = 0; i < 6; i++)
            h.WaraqData.SavePumpAsync(new PumpYaqobi.Domain.Entities.WaraqPump
            {
                ShiftId = day.Id, SortIndex = i, Num = i + 1, Worker = "محمد نبی احمدزی",
                Start = 1234567m + i * 10000, End = 1239876m + i * 10000, PricePerLiter = 64.5m,
                Note = "یادداشتِ بلندِ همین پایه",
            }).GetAwaiter().GetResult();
        var wq = (WaraqSectionViewModel)vm.Sections.First(s => s.Id == "waraq");
        Wait(win, vm.GoAsync(wq));
        wq.OpenCommand.Execute(wq.Sheets.First(x => x.Id == w.Id));
        Settle(win);
        foreach (var width in new[] { 1440.0, 1280, 1100, 1024 })
        {
            win.Width = width;
            Settle(win);
            var g = win.GetVisualDescendants().OfType<DataGrid>()
                       .FirstOrDefault(x => x.IsEffectivelyVisible && x.Columns.Any(c => (c.Header as string) == "ختم"));
            if (g is null) { Check($"{width}: جدولِ پایه‌ها پیدا شد", false); continue; }
            var cols = g.Columns.Where(c => c.IsVisible).Sum(c => c.ActualWidth)
                     + (g.HeadersVisibility.HasFlag(DataGridHeadersVisibility.Row) && !double.IsNaN(g.RowHeaderWidth) ? g.RowHeaderWidth : 0);
            var room = g.Bounds.Width;
            //  ⚠️ در راست‌به‌چپ، (0,0)ِ کنترل لبهٔ **راستِ** آن در پنجره است؛
            //  پس هر دو گوشه برده و کوچک‌تر/بزرگ‌تر گرفته می‌شوند.
            var a = g.TranslatePoint(default, win)!.Value.X;
            var b = g.TranslatePoint(new Point(room, 0), win)!.Value.X;
            var (left, right) = (Math.Min(a, b), Math.Max(a, b));
            Check($"پنجرهٔ {width}: ستون‌ها در کادرِ جدول جا می‌شوند و جدول از پنجره بیرون نمی‌زند",
                  cols <= room + 2 && left >= 0 && right <= win.ClientSize.Width + 1,
                  $"ستون‌ها {cols:0} · کادر {room:0} · لبه {left:0}…{right:0}");
            var hbar = g.GetVisualDescendants().OfType<ScrollBar>()
                        .Any(b => b.Orientation == Avalonia.Layout.Orientation.Horizontal && b.IsEffectivelyVisible && b.Maximum > 1);
            Check($"پنجرهٔ {width}: جدولِ پایه‌ها نوارِ لغزشِ افقی ندارد", !hbar);
            //  و جا شدن با «…» کردنِ عدد نیست: هیچ عددی نباید بریده شود
            var cut = g.GetVisualDescendants().OfType<DataGridCell>()
                       .Where(c => c.IsEffectivelyVisible)
                       .Select(c => c.GetVisualDescendants().OfType<TextBlock>().FirstOrDefault(t => t.Text?.Any(char.IsDigit) == true))
                       .Where(t => t is not null && Cut(t!))
                       .Select(t => t!.Text).Distinct().ToList();
            Check($"پنجرهٔ {width}: هیچ عددی در جدولِ پایه‌ها بریده («…») نشده", cut.Count == 0,
                  string.Join("، ", cut.Take(4)));
            if (width is 1024 or 1440) Shot(win, shots, "pumps-" + width);
        }
        win.Width = 1440;
        Settle(win);
    }

    // ── ابزار ───────────────────────────────────────────────────────────────

    /// <summary>
    /// نوشته از جای خودش بلندتر است؟ — با ‎FormattedText‎ِ بی‌قید، نه
    /// ‎DesiredSize‎ که خودش با همان قیدِ تنگ سنجیده می‌شود (درسِ ۱۴۰۵/۰۶/۲۷).
    /// </summary>
    private static bool Cut(TextBlock t)
    {
        var ft = new Avalonia.Media.FormattedText(t.Text ?? "", System.Globalization.CultureInfo.CurrentCulture,
            t.FlowDirection, new Avalonia.Media.Typeface(t.FontFamily, t.FontStyle, t.FontWeight),
            t.FontSize, null);
        return ft.Width > t.Bounds.Width + 1.5;
    }

    private static DataGrid? Grid(Window win) =>
        win.GetVisualDescendants().OfType<DataGrid>()
           .Where(g => g.IsEffectivelyVisible && g.Columns.Any(c => (c.Header as string) == "عنوان"))
           .FirstOrDefault();

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
