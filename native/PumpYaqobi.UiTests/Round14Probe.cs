using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PumpYaqobi.App.Services;
using PumpYaqobi.App.ViewModels;
using PumpYaqobi.App.Views;

namespace PumpYaqobi.UiTests;

/// <summary>
/// ══ فهرستِ شش‌تاییِ صاحب ریپو (۱۴۰۵/۰۷/۱۴) ═══════════════════════════════
///
///     dotnet run --project PumpYaqobi.UiTests -c Release -- round14 [پوشهٔ عکس]
/// </summary>
internal static class Round14Probe
{
    private static int _bad;

    private static void Check(string what, bool ok, string? detail = null)
    {
        Console.WriteLine((ok ? "  ✔ " : "  ✖ ") + what + (detail is null ? "" : " — " + detail));
        if (!ok) _bad++;
    }

    public static int Run(string[] args)
    {
        var shots = args.Length > 1 ? args[1] : Path.Combine(Path.GetTempPath(), "pump-round14");
        Directory.CreateDirectory(shots);
        var dir = Path.Combine(Path.GetTempPath(), "pump-r14-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        AppHost.Start(Path.Combine(dir, "pump.db"));
        FakeLicense.Grant();

        AppBuilder.Configure<PumpYaqobi.App.App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .SetupWithoutStarting();
        // ‎R14_START=gold‎: برنامه از اول با تمِ تیره بالا بیاید — همان حالِ عکسِ صاحب ریپو
        var startGold = Environment.GetEnvironmentVariable("R14_START") == "gold";
        if (startGold) PumpYaqobi.App.Themes.ThemeManager.Apply(PumpYaqobi.App.Themes.PumpTheme.Gold);
        var win = new MainWindow { Width = 1440, Height = double.TryParse(Environment.GetEnvironmentVariable("R14_H"), out var h14) ? h14 : 900 };
        win.Show();
        Pump(win);
        var vm = (MainViewModel)win.DataContext!;
        LockIn.Wait(vm.Lock);
        Settle(win);

        // ‎R14_AWAY=1‎: همهٔ بخش‌ها اول با یک تم دیده می‌شوند، تم در بخشِ «دیگری»
        // عوض می‌شود و بعد به هر کدام برمی‌گردیم — راهِ واقعیِ کاربر.
        if (Environment.GetEnvironmentVariable("R14_AWAY") == "1")
        {
            var ids = (Environment.GetEnvironmentVariable("R14_SECTIONS") ?? "expenses,sarrafi,safe").Split(',');
            foreach (var id in ids)
            {
                var s0 = vm.Sections.First(x => x.Id == id);
                Wait(win, vm.GoAsync(s0)); Settle(win);
                Shot(win, shots, id + "-before");
            }
            Wait(win, vm.GoAsync(vm.Sections.First(x => x.Id == "dashboard"))); Settle(win);
            vm.SelectedTheme = startGold ? PumpYaqobi.App.Themes.PumpTheme.Blue : PumpYaqobi.App.Themes.PumpTheme.Gold;
            Settle(win);
            foreach (var id in ids)
            {
                var s0 = vm.Sections.First(x => x.Id == id);
                Wait(win, vm.GoAsync(s0)); Settle(win);
                Shot(win, shots, id + "-after");
            }
            Console.WriteLine("✅ عکس‌ها گرفته شد");
            return 0;
        }

        foreach (var id in (Environment.GetEnvironmentVariable("R14_SECTIONS") ?? "attendance,debtrasid,sarrafi,expenses,dashboard").Split(','))
        {
            var s = vm.Sections.FirstOrDefault(x => x.Id == id);
            if (s is null) { Console.WriteLine("  ? " + id); continue; }
            Wait(win, vm.GoAsync(s));
            Settle(win);
            var (first, second) = startGold
                ? (PumpYaqobi.App.Themes.PumpTheme.Gold, PumpYaqobi.App.Themes.PumpTheme.Blue)
                : (PumpYaqobi.App.Themes.PumpTheme.Blue, PumpYaqobi.App.Themes.PumpTheme.Gold);
            Shot(win, shots, id + "-" + first.Id);
            PumpYaqobi.App.Themes.ThemeManager.Apply(second);
            Settle(win);
            Shot(win, shots, id + "-" + second.Id);
            PumpYaqobi.App.Themes.ThemeManager.Apply(first);
            Settle(win);
            Shot(win, shots, id + "-" + first.Id + "-back");
        }

        if (Environment.GetEnvironmentVariable("R14_SHOTS_ONLY") != "1")
        {
            Toolbar(win, vm);
            Receipt(win, vm, shots);
            AlertsList(win, vm, shots);
        }

        Console.WriteLine(_bad == 0 ? "✅ همهٔ بندها سبزند" : $"❌ {_bad} ایراد");
        return _bad == 0 ? 0 : 1;
    }

    // ══ ۱) نوارِ ابزار: همه در یک خط و هم‌قد ═══════════════════════════════
    private static void Toolbar(Window win, MainViewModel vm)
    {
        Console.WriteLine();
        Console.WriteLine("════ ۱) نوارِ ابزار: یک خط، یک قد ════");
        foreach (var id in new[] { "attendance", "expenses", "safe", "sarrafi", "debtrasid" })
        {
            var s = vm.Sections.First(x => x.Id == id);
            Wait(win, vm.GoAsync(s));
            Settle(win);
            var bar = win.GetVisualDescendants().OfType<WrapPanel>()
                         .FirstOrDefault(w => w.Classes.Contains("sec-tools") && w.IsEffectivelyVisible);
            if (bar is null) { Check(id + ": نوارِ ابزار پیدا شد", false); continue; }
            var items = bar.GetVisualDescendants()
                           .Where(c => c is Button { } b && !b.Classes.Contains("seg") || c is ComboBox || c is TextBox)
                           .OfType<Control>()
                           .Where(c => c.IsEffectivelyVisible && c.Bounds.Height > 0
                                       && !c.GetVisualAncestors().Any(a => a is ComboBox or TextBox))
                           .ToList();
            var rects = items.Select(c => Screen(c, win)).ToList();
            var heights = rects.Select(r => Math.Round(r.Height)).Distinct().ToList();
            var centers = rects.Select(r => Math.Round(r.Center.Y)).Distinct().ToList();
            Check($"{id}: همهٔ {items.Count} کنترل هم‌قد", heights.Count == 1, string.Join("، ", heights));
            Check($"{id}: همه در یک خط", centers.Max() - centers.Min() <= 1, string.Join("، ", centers));
        }
    }

    // ══ ۲) رسیدِ قرض‌دار / چکنه ═══════════════════════════════════════════
    private static void Receipt(Window win, MainViewModel vm, string shots)
    {
        Console.WriteLine();
        Console.WriteLine("════ ۲) رسید: ترتیب، هم‌قدی، چکنه و الباقی ════");
        var host = AppHost.Current;
        //  داده: یک قرض‌دار و یک حسابِ چکنه با بردگیِ ۶۰۰
        var karim = host.Debtors.AddDebtorAsync("کریم رسیدی", null, false).GetAwaiter().GetResult();
        using (var db = host.Db.Create())
        {
            var today = PumpYaqobi.Application.Localization.Shamsi.Today();
            db.RetailRows.Add(new PumpYaqobi.Domain.Entities.RetailRow
            {
                DateShamsi = today, DateKey = PumpYaqobi.Application.Localization.Shamsi.Key(today),
                MonthKey = PumpYaqobi.Application.Localization.Shamsi.MonthKey(today),
                Name = "نادر چکنه", Liters = 10, PricePerLiter = 60,
            });
            db.SaveChanges();
        }

        var r = (PumpYaqobi.App.ViewModels.Sections.DebtReceiptSectionViewModel)vm.Sections.First(x => x.Id == "debtrasid");
        Wait(win, vm.GoAsync(r));
        Settle(win);
        var form = win.GetVisualDescendants().OfType<WrapPanel>().First(w => w.Name == "QuickForm");
        var boxes = form.GetVisualDescendants().Where(c => c is TextBox || c is ComboBox).OfType<Control>()
                        .Where(c => !c.GetVisualAncestors().Any(a => a is ComboBox or TextBox)).ToList();
        var br = boxes.Select(b => Screen(b, win)).ToList();
        Check($"هر {boxes.Count} کادر هم‌قد", br.Select(x => Math.Round(x.Height)).Distinct().Count() == 1,
              string.Join("، ", br.Select(x => Math.Round(x.Height)).Distinct()));
        Check("و همه در یک خط", br.Max(x => x.Top) - br.Min(x => x.Top) <= 1);
        //  ترتیب از راست به چپ: تاریخ، نام، رسید، واحد، تیل، حساب، توضیحات
        var xs = br.Select(x => x.Right).ToList();
        Check("ترتیب از راست: تاریخ ← نام ← رسید ← واحد ← تیل ← حساب ← توضیحات",
              boxes.Count == 7 && xs.Zip(xs.Skip(1)).All(p => p.First > p.Second));
        Shot(win, shots, "r-form");

        //  ── چکنه ──
        r.TargetIndex = 1;
        Settle(win);
        Check("با «چکنه» واحد روی «پول» و بسته", r.UnitIndex == 0 && !r.UnitOpen && !r.IsFuel);
        r.TypedName = "نادر";
        r.AmountText = "200";
        Wait(win, r.SubmitAsync());
        using (var db = host.Db.Create())
        {
            var rows = db.RetailRows.Where(x => x.Name == "نادر چکنه").ToList();
            Check("رسید در همان حسابِ چکنه نشست (نامِ کامل پیدا شد)", rows.Count == 2 && rows.Any(x => x.Rasid == 200m),
                  string.Join("، ", rows.Select(x => x.Rasid)));
        }
        Check("و الباقی گفته شد: ۶۰۰ − ۲۰۰ = ۴۰۰", r.LastResult.Contains("الباقی: 400"), r.LastResult);
        Shot(win, shots, "r-chakana");
        var row = r.Rows.FirstOrDefault(x => x.Account == "نادر چکنه");
        Check("در فهرستِ ماه با نشانِ چکنه", row is not null && row.UnitText.Contains("چکنه"), row?.UnitText);
        if (row is not null)
        {
            PumpYaqobi.App.Services.Dialogs.ConfirmHook = (_, _) => true;
            Wait(win, r.UndoCommand.ExecuteAsync(row));
            using var db = host.Db.Create();
            Check("برگرداندن ⇒ ردیفِ چکنه هم رفت", db.RetailRows.Count(x => x.Name == "نادر چکنه") == 1);
        }
        r.TypedName = "ناشناس"; r.AmountText = "50";
        Wait(win, r.SubmitAsync());
        using (var db = host.Db.Create())
            Check("نامِ نبوده ⇒ هیچ ردیفی ساخته نشد", !db.RetailRows.Any(x => x.Name == "ناشناس"));
        r.TypedName = ""; r.AmountText = "";

        //  ── قرض‌دار، و ثبت فقط با بیرون رفتن از **کلِ** فرم ──
        r.TargetIndex = 0;
        Settle(win);
        var name = form.GetVisualDescendants().OfType<TextBox>().First(t => t.Name == "NameBox");
        var amount = form.GetVisualDescendants().OfType<TextBox>().First(t => t.Name == "AmountBox");
        var target = form.GetVisualDescendants().OfType<ComboBox>().First(t => t.Name == "TargetBox");
        int Count() { using var db = host.Db.Create(); return db.DebtQuickReceipts.Count(); }
        var before = Count();
        name.Focus(); r.TypedName = "کریم رسیدی"; Settle(win);
        amount.Focus(); r.AmountText = "100"; Settle(win);
        target.Focus(); Settle(win);
        Check("رفتن از «رسید» به کشوییِ «حساب» ثبت نکرد", Count() == before);
        var outside = win.GetVisualDescendants().OfType<Button>().First(b => b.IsEffectivelyVisible && !form.IsVisualAncestorOf(b));
        outside.Focus(); Settle(win);
        for (var i = 0; i < 100 && Count() == before; i++) { Pump(win); Thread.Sleep(10); }
        Check("بیرون رفتن از فرم ⇒ ثبت شد", Count() == before + 1);
        Check("و الباقیِ قرض‌دار گفته شد", r.LastResult.Contains("کریم رسیدی") && r.LastResult.Contains("الباقیِ پول: -100"),
              r.LastResult);
        Shot(win, shots, "r-debtor");
        _ = karim;
    }

    // ══ ۴) هشدارها: هر کدام کارتِ خودش، نه یک خطِ طولانی ═════════════════
    private static void AlertsList(Window win, MainViewModel vm, string shots)
    {
        Console.WriteLine("۴) فهرستِ هشدارهای داشبورد");
        var items = new List<AlertItem>
        {
            new("tank-petrol", "مخزنِ پطرول", "petrol", "out", "مخزنِ پطرول تمام شد — ۰ لیتر مانده (حدِ هشدار ۲٬۰۰۰)", "امروز پطرول سفارش بدهید"),
            new("d7-stP-over", "حاجی کریم", "petrol", "out", "حاجی کریم — ۳۵۰ لیتر پطرول بیش از حسابش برده", "بازپرسی کنید: چه کسی و چرا اضافه داد؟"),
            new("d9-stD-w90", "نادر", "diesel", "low", "نادر — ۹۰٪ حسابِ دیزل مصرف شده (۴۰ لیتر مانده)", "متوجه باشید؛ پیش از دادن بپرسید"),
        };
        AppHost.Current.LiveAlerts.Accept(items);
        var dash = vm.Sections.OfType<PumpYaqobi.App.ViewModels.Sections.DashboardSectionViewModel>().First();
        Wait(win, vm.GoAsync(dash));
        dash.ApplyAlerts();
        Settle(win);
        Check("سه هشدار در فهرست", dash.Alerts.Count == 3, dash.Alerts.Count.ToString());
        dash.BellCommand.Execute(null);
        Settle(win);
        Check("زنگ فهرست را باز کرد (نه توست)", dash.AlertsOpen);
        var popup = win.GetVisualDescendants().OfType<Avalonia.Controls.Primitives.Popup>()
            .FirstOrDefault(p => p.Name == "AlertsPopup");
        Check("پنجرهٔ فهرست در درخت هست و باز است", popup is not null && popup.IsOpen);
        var list = (popup?.Child as Visual)?.GetVisualDescendants().OfType<ItemsControl>().FirstOrDefault(i => i.Name == "AlertsList");
        var cards = list?.GetVisualDescendants().OfType<Button>().Count(b => b.Classes.Contains("card-shell")) ?? 0;
        Check("هر هشدار کارتِ جدا دارد", cards == 3, cards.ToString());
        var texts = list?.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text ?? "").ToList() ?? new();
        Check("هیچ متنی چند هشدار را با «·» پشتِ سرِ هم ندارد",
              !texts.Any(t => t.Contains("تمام شد") && t.Contains("حاجی")));
        Check("دستورِ کارِ هر هشدار دیده می‌شود", texts.Any(t => t.Contains("بازپرسی کنید")));
        Shot(win, shots, "alerts-list");
        if (popup?.Child is Control ch) ShotControl(ch, shots, "alerts-list-only");
        dash.CloseAlertsCommand.Execute(null);
        Settle(win);
        Check("بستن فهرست را می‌بندد", !dash.AlertsOpen);
        AppHost.Current.LiveAlerts.Accept(Array.Empty<AlertItem>());
        dash.ApplyAlerts();
    }

    internal static void ShotControl(Control c, string dir, string name)
    {
        try
        {
            var size = new PixelSize(Math.Max(1, (int)c.Bounds.Width), Math.Max(1, (int)c.Bounds.Height));
            using var rtb = new Avalonia.Media.Imaging.RenderTargetBitmap(size);
            rtb.Render(c);
            rtb.Save(Path.Combine(dir, name + ".png"));
        }
        catch (Exception e) { Console.WriteLine("  (عکس نشد: " + e.Message + ")"); }
    }

    internal static Rect Screen(Visual v, Visual root)
    {
        var a = v.TranslatePoint(new Point(0, 0), root);
        var b = v.TranslatePoint(new Point(v.Bounds.Width, v.Bounds.Height), root);
        if (a is not { } p || b is not { } q) return default;
        return new Rect(new Point(Math.Min(p.X, q.X), Math.Min(p.Y, q.Y)),
                        new Point(Math.Max(p.X, q.X), Math.Max(p.Y, q.Y)));
    }

    internal static void Shot(Window win, string dir, string name)
    {
        Settle(win);
        using var f = win.CaptureRenderedFrame();
        var path = Path.Combine(dir, name + ".png");
        f?.Save(path);
        Console.WriteLine("  📷 " + path);
    }

    internal static void Pump(Window w) { for (var i = 0; i < 8; i++) { Dispatcher.UIThread.RunJobs(); w.UpdateLayout(); } }
    internal static void Settle(Window w) { for (var i = 0; i < 40; i++) { Pump(w); Thread.Sleep(5); } }
    internal static void Wait(Window w, Task t)
    {
        for (var i = 0; i < 600 && !t.IsCompleted; i++) { Pump(w); Thread.Sleep(2); }
        Settle(w);
    }
}
