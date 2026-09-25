using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PumpYaqobi.App.Services;
using PumpYaqobi.App.ViewModels;
using PumpYaqobi.App.ViewModels.Sections;
using PumpYaqobi.App.Views;
using PumpYaqobi.Application.Localization;
using PumpYaqobi.Application.Services;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;

namespace PumpYaqobi.UiTests;

/// <summary>
/// ══ مفاد/ضرر و مخزن — با عکسِ صاحب ریپو (۱۴۰۵/۰۷/۱۳) ═════════════════════
///
///     dotnet run --project PumpYaqobi.UiTests -c Release -- plstore [پوشهٔ عکس]
///
/// سه خواسته، هر سه با پنجرهٔ واقعی سنجیده می‌شوند، نه با خواندنِ XAML:
/// <list type="number">
/// <item>کادرهای «خرید عمده» (و بقیهٔ کادرهای صفحه) هم‌پهنا و هم‌قد و زیرِ هم.</item>
/// <item>کشوی ماه/سال: عددِ هر دوره همان جمعِ دادهٔ همان دوره است — از روی
///   دادهٔ ساختگیِ خودِ سنجه حساب می‌شود، نه از روی خودِ برنامه.</item>
/// <item>مخزن: عنوان و عدد وسط، و «جمله ورودی/فروش» کادرِ هم‌شکلِ دو کادرِ تایپ.</item>
/// </list>
/// </summary>
internal static class ProfitStorageProbe
{
    private static int _bad;

    private static void Check(string what, bool ok, string? detail = null)
    {
        Console.WriteLine((ok ? "  ✔ " : "  ✖ ") + what + (detail is null ? "" : " — " + detail));
        if (!ok) _bad++;
    }

    public static int Run(string[] args)
    {
        var shots = args.Length > 1 ? args[1] : Path.Combine(Path.GetTempPath(), "pump-plstore");
        Directory.CreateDirectory(shots);
        var dir = Path.Combine(Path.GetTempPath(), "pump-plstore-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);

        AppHost.Start(Path.Combine(dir, "pump.db"));
        FakeLicense.Grant();
        var data = SeedProfit(AppHost.Current);

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

        Profit(win, vm, data, shots);
        Storage(win, vm, shots);

        Console.WriteLine();
        Console.WriteLine(_bad == 0 ? "✅ همهٔ بندها سبزند" : $"❌ {_bad} ایراد");
        return _bad == 0 ? 0 : 1;
    }

    // ══ داده: سه ماه، هر منبع با تاریخِ خودش ═══════════════════════════════════

    /// <summary>یک قلمِ ساختگی: کدام ماه، کدام منبع، چه عددی.</summary>
    private sealed record Item(string Month, string Kind, decimal Value);

    private sealed record Seeded(List<Item> Items, List<(InvoiceRate Rate, string Month)> Rates,
                                 string M1, string M0, string Old);

    private static string PrevMonth(string m)
    {
        var y = int.Parse(m[..4]); var mm = int.Parse(m[5..]);
        if (--mm == 0) { mm = 12; y--; }
        return $"{y:0000}/{mm:00}";
    }

    private static int Key(string month, int day) => Shamsi.Key($"{month}/{day:00}");

    private static Seeded SeedProfit(AppHost h)
    {
        var m1 = Shamsi.ThisMonth();
        var m0 = PrevMonth(m1);
        var old = $"{int.Parse(m1[..4]) - 1:0000}/11";
        var items = new List<Item>();
        var rates = new List<(InvoiceRate, string)>();

        using var db = h.Db.Create();

        //  پارچه‌ها — سودِ دو شیفت
        void Report(string month, int day, FuelType fuel, decimal dayP, decimal nightP)
        {
            db.Reports.Add(new ParchaReport
            {
                Fuel = fuel, DateShamsi = $"{month}/{day:00}", DateKey = Key(month, day),
                DayShift = new ShiftData { Profit = dayP, Sale = dayP * 10 },
                NightShift = nightP == 0 ? null : new ShiftData { Profit = nightP, Sale = nightP * 10 },
            });
            items.Add(new Item(month, fuel == FuelType.Diesel ? "diesel" : "petrol", dayP + nightP));
        }
        Report(m1, 2, FuelType.Petrol, 1000, 500);
        Report(m1, 3, FuelType.Diesel, 200, 0);
        Report(m0, 12, FuelType.Petrol, 700, 0);
        Report(old, 5, FuelType.Petrol, 300, 40);

        //  درآمدِ اضافی و مصرف — با ‎MonthKey‎ی خودشان
        void Extra(string month, decimal v)
        {
            db.ExtraIncomes.Add(new ExtraIncome
            { DateShamsi = month + "/04", DateKey = Key(month, 4), MonthKey = month, Amount = v });
            items.Add(new Item(month, "extra", v));
        }
        void Spend(string month, decimal v)
        {
            db.Expenses.Add(new Expense
            { DateShamsi = month + "/06", DateKey = Key(month, 6), MonthKey = month, Title = "آزمون", Amount = v });
            items.Add(new Item(month, "expense", v));
        }
        Extra(m1, 50); Extra(m0, 20);
        Spend(m1, 400); Spend(m0, 100); Spend(old, 60);

        //  فاکتورِ تاییدشده — اختلافِ نرخ با **روزِ تایید**
        void Inv(int n, int dateKey, DateTime? approvedUtc, decimal liters, decimal price, decimal? onCreate,
                 decimal onApprove, string countsIn)
        {
            db.Invoices.Add(new Invoice
            {
                InvoiceNumber = n, Status = InvoiceStatus.Approved, DateKey = dateKey, ApprovedAtUtc = approvedUtc,
                Liters = liters, PricePerLiter = price, RateOnCreate = onCreate, RateOnApprove = onApprove,
            });
            rates.Add((new InvoiceRate(false, InvoiceStatus.Approved, liters, price, onCreate, onApprove), countsIn));
        }
        Inv(1, Key(m1, 1), DateTime.UtcNow, 100, 60, null, 62, m1);           // +۲۰۰ همین ماه
        Inv(2, Key(m0, 9), null, 50, 0, 70, 65, m0);                          // بی روزِ تایید ⇒ تاریخِ خودش
        Inv(3, Key(m0, 5), DateTime.UtcNow, 10, 60, null, 64, m1);            // ساخته در ماهِ پیش، تایید امروز ⇒ همین ماه

        //  قرض‌دارِ «بی‌فاکتور» — بردگیِ دفترِ تیلش مستقیم درآمد است
        var debtor = new Debtor { Name = "بی‌فاکتورِ آزمون", IsNoInvoice = true };
        db.Debtors.Add(debtor);
        db.SaveChanges();
        var acct = new DebtAccount { MainOfDebtorId = debtor.Id, Name = debtor.Name };
        db.DebtAccounts.Add(acct);
        db.SaveChanges();
        void Row(string month, int day, decimal liters)
        {
            db.DebtRows.Add(new DebtRow
            {
                FuelAccountId = acct.Id, DateShamsi = $"{month}/{day:00}", DateKey = Key(month, day),
                Fuel = FuelType.Petrol, Liters = liters, PricePerLiter = 60, SortIndex = day,
            });
            items.Add(new Item(month, "noinv", liters * 60));
        }
        Row(m1, 7, 10); Row(m0, 8, 5);
        db.SaveChanges();

        return new Seeded(items, rates, m1, m0, old);
    }

    /// <summary>عددِ خالصِ یک دوره، فقط از روی دادهٔ خودِ سنجه — با همان فرمولِ برنامه.</summary>
    private static string Expected(Seeded s, Func<string, bool> inPeriod)
    {
        decimal Sum(string kind) => s.Items.Where(i => i.Kind == kind && inPeriod(i.Month)).Sum(i => i.Value);
        var input = new ProfitInput
        {
            ShiftProfitPetrol = Sum("petrol"),
            ShiftProfitDiesel = Sum("diesel"),
            NoInvoiceSum = Sum("noinv"),
            ExtraIncomeSum = Sum("extra"),
            ExpenseSum = Sum("expense"),
            InvoiceRateDiffSum = ProfitLossService.InvoiceRateDiff(
                s.Rates.Where(r => inPeriod(r.Month)).Select(r => r.Rate)),
        };
        var r = ProfitLossService.Compute(input, 0, 0);
        return Shamsi.Money(Math.Round(Math.Abs(r.Net), 0, MidpointRounding.AwayFromZero)) + " افغانی"
               + (r.IsProfit ? " ✅" : " ❌");
    }

    private static string Shown(ProfitSectionViewModel p) => p.NetText + (p.IsProfit ? " ✅" : " ❌");

    // ══ ۱) مفاد / ضرر ═════════════════════════════════════════════════════════
    private static void Profit(Window win, MainViewModel vm, Seeded s, string shots)
    {
        Console.WriteLine();
        Console.WriteLine("════ ۱) مفاد / ضرر — دوره و ترازِ کادرها ════");
        var p = (ProfitSectionViewModel)vm.Sections.First(x => x.Id == "profit");
        Wait(win, vm.GoAsync(p));
        Settle(win);

        //  ── دوره ──
        Check("پیش‌فرض «همهٔ زمان‌ها» است", p.PeriodText == "همهٔ زمان‌ها", p.PeriodText);
        Check("«همه» = جمعِ همهٔ ماه‌ها", Shown(p) == Expected(s, _ => true), Shown(p) + " / " + Expected(s, _ => true));
        var years = p.Picker.Years.Select(y => y.Key).ToList();
        Check("کشوی سال هر دو سال را دارد", years.Contains(s.M1[..4]) && years.Contains(s.Old[..4]),
              string.Join("، ", years));

        Pick(win, p, s.M1[..4], s.M1);
        Check($"ماهِ «{Shamsi.MonthLabel(s.M1)}»: فقط دادهٔ همان ماه", Shown(p) == Expected(s, m => m == s.M1),
              Shown(p) + " / " + Expected(s, m => m == s.M1));
        Check("و برچسبِ دوره همان ماه", p.PeriodText == Shamsi.MonthLabel(s.M1), p.PeriodText);
        Check("فاکتورِ ساخته‌شده در ماهِ پیش و تاییدشده امروز، در همین ماه است",
              s.Rates.Count(r => r.Month == s.M1) == 2);
        Shot(win, shots, "01-profit-month");

        Pick(win, p, s.M0[..4], s.M0);
        Check($"ماهِ «{Shamsi.MonthLabel(s.M0)}»: فقط دادهٔ همان ماه", Shown(p) == Expected(s, m => m == s.M0),
              Shown(p) + " / " + Expected(s, m => m == s.M0));

        Pick(win, p, s.Old[..4], s.Old[..4] + YearMonthPicker.AllMark);
        Check($"سالِ {s.Old[..4]}: فقط همان سال", Shown(p) == Expected(s, m => m[..4] == s.Old[..4]),
              Shown(p) + " / " + Expected(s, m => m[..4] == s.Old[..4]));
        Check("برچسبِ دوره «سالِ …»", p.PeriodText == "سالِ " + s.Old[..4], p.PeriodText);

        Pick(win, p, s.M1[..4], s.M1[..4] + YearMonthPicker.AllMark);
        Check($"سالِ {s.M1[..4]}", Shown(p) == Expected(s, m => m[..4] == s.M1[..4]),
              Shown(p) + " / " + Expected(s, m => m[..4] == s.M1[..4]));

        Pick(win, p, "", "");
        Check("برگشت به «همه» همان عددِ آغاز", Shown(p) == Expected(s, _ => true), Shown(p));
        Shot(win, shots, "02-profit-all");

        //  ── کادرها ──
        var view = win.GetVisualDescendants().OfType<PumpYaqobi.App.Views.Sections.ProfitSectionView>()
                      .FirstOrDefault(v => v.IsEffectivelyVisible);
        if (view is null) { Check("صفحهٔ مفاد/ضرر پیدا شد", false); return; }
        var boxes = view.GetVisualDescendants().OfType<TextBox>().Where(t => t.Classes.Contains("plbox")).ToList();
        Check("هفت کادرِ تایپ، همه یک سبک", boxes.Count == 7, boxes.Count.ToString());
        var hs = boxes.Select(b => Math.Round(b.Bounds.Height, 1)).Distinct().ToList();
        Check("همه یک قد", hs.Count == 1, string.Join("، ", hs));
        var fs = boxes.Select(b => b.FontSize).Distinct().ToList();
        Check("همه یک قلم", fs.Count == 1, string.Join("، ", fs));

        Rect R(Visual v) => Screen(v, win);

        //  کادرها را با نوشتهٔ خودشان پیدا می‌کنیم: پنج عددِ جدا در همان پنج
        //  خاصیتی که کاربر تایپ می‌کند (بی هیچ نوشتنی در دیتابیس)، و دو کادرِ
        //  باقی‌مانده همان دو نرخِ اتحادیه‌اند.
        p.BulkQty = "11"; p.BulkBuy = "22"; p.BulkMarket = "33"; p.ManualIncome = "44"; p.ManualExpense = "55";
        Settle(win);
        TextBox Box(string text) => boxes.First(b => b.Text == text);
        var qty = Box("11"); var buy = Box("22"); var mkt = Box("33");
        var three = new[] { qty, buy, mkt }.Select(R).ToList();
        var ws = three.Select(r => Math.Round(r.Width, 1)).ToList();
        //  ⚠️ گردکردنِ چیدمان: سه ستونِ ستاره‌ای از یک پهنای درست، یک پیکسل با هم فرق می‌کنند
        Check("سه کادرِ خرید عمده هم‌پهنا", ws.Max() - ws.Min() <= 1, string.Join("، ", ws));
        Check("و روی یک خط", three.Select(r => Math.Round(r.Y)).Distinct().Count() == 1);

        var results = view.GetVisualDescendants().OfType<Border>().Where(b => b.Classes.Contains("plresult"))
                          .Select(R).OrderByDescending(r => r.X).ToList();
        var add = view.GetVisualDescendants().OfType<Button>()
                      .First(b => ReferenceEquals(b.Command, p.AddBulkCommand));
        var cols = three.OrderByDescending(r => r.X).ToList();
        var below = results.Concat(new[] { R(add) }).OrderByDescending(r => r.X).ToList();
        var edge = cols.Zip(below, (a, b) => Math.Max(Math.Abs(a.Left - b.Left), Math.Abs(a.Right - b.Right))).Max();
        Check("دو نتیجه و دکمهٔ ثبت دقیقاً زیرِ همان سه کادر", edge < 1, $"بیشترین اختلافِ لبه {edge:0.0}px");
        var bh = below.Select(r => Math.Round(r.Height, 1)).Distinct().ToList();
        Check("و هم‌قد", bh.Count == 1, string.Join("، ", bh));

        var union = boxes.Where(b => b.Text is not ("11" or "22" or "33" or "44" or "55")).Select(R).ToList();
        Check("دو کادرِ نرخِ اتحادیه پیدا شد", union.Count == 2, union.Count.ToString());
        if (union.Count != 2) return;
        Check("دو کادرِ نرخِ اتحادیه هم‌پهنا", Math.Abs(union[0].Width - union[1].Width) < 1,
              $"{union[0].Width:0.0} / {union[1].Width:0.0}");
        var manual = new[] { Box("44"), Box("55") }.Select(R).ToList();
        Check("دو کادرِ دستی هم‌پهنا", Math.Abs(manual[0].Width - manual[1].Width) < 1,
              $"{manual[0].Width:0.0} / {manual[1].Width:0.0}");
        var mid = (R(view).Left + R(view).Right) / 2;
        foreach (var (name, rs) in new[] { ("خرید عمده", three), ("اتحادیه", union), ("دستی", manual) })
        {
            var c = (rs.Min(r => r.Left) + rs.Max(r => r.Right)) / 2;
            Check($"شبکهٔ «{name}» وسطِ صفحه", Math.Abs(c - mid) < 20, $"{c - mid:+0;-0} px");
        }

        //  کشوها در نوارِ صافی هستند و دیده می‌شوند
        var combos = win.GetVisualDescendants().OfType<ComboBox>()
                        .Where(cb => cb.IsEffectivelyVisible && ReferenceEquals(cb.ItemsSource, p.Picker.Months)).ToList();
        Check("کشوی ماه روی صفحه است", combos.Count == 1);

        add.BringIntoView();
        Settle(win);
        Shot(win, shots, "03-profit-boxes");
        p.BulkQty = p.BulkBuy = p.BulkMarket = p.ManualIncome = p.ManualExpense = "";
        Settle(win);
    }

    private static void Pick(Window win, ProfitSectionViewModel p, string year, string key)
    {
        var y = p.Picker.Years.FirstOrDefault(i => i.Key == year);
        if (y is not null && !ReferenceEquals(p.Picker.Year, y)) { p.Picker.Year = y; Settle(win); }
        var m = p.Picker.Months.FirstOrDefault(i => i.Key == key);
        if (m is not null && !ReferenceEquals(p.Picker.Selected, m)) p.Picker.Selected = m;
        var want = ProfitPeriod.FromKey(key);
        var label = want.IsAll ? "همهٔ زمان‌ها"
                  : want.Month.Length == 0 ? "سالِ " + want.Year
                  : Shamsi.MonthLabel(want.Year + "/" + want.Month);
        for (var i = 0; i < 400 && p.PeriodText != label; i++) { Pump(win); Thread.Sleep(3); }
        Settle(win);
    }

    // ══ ۲) مخزن ═══════════════════════════════════════════════════════════════
    private static void Storage(Window win, MainViewModel vm, string shots)
    {
        Console.WriteLine();
        Console.WriteLine("════ ۲) مخزن — عنوانِ وسط و چهار کادرِ هم‌شکل ════");
        var st = (StorageSectionViewModel)vm.Sections.First(x => x.Id == "storage");
        Wait(win, vm.GoAsync(st));
        st.OpenBuyCommand.Execute(null);
        st.BuySeller = "شرکتِ آزمون";
        st.BuyKg = "30000"; st.BuyDensity = "0.7435"; st.BuyPriceTon = "1200"; st.BuyUsdRate = "66";
        Pump(win);
        Wait(win, st.SaveBuyCommand.ExecuteAsync(null));
        Settle(win);

        var view = win.GetVisualDescendants().OfType<PumpYaqobi.App.Views.Sections.StorageSectionView>()
                      .FirstOrDefault(v => v.IsEffectivelyVisible);
        if (view is null) { Check("صفحهٔ مخزن پیدا شد", false); return; }
        Rect R(Visual v) => Screen(v, win);

        var texts = view.GetVisualDescendants().OfType<TextBlock>().Where(t => t.IsEffectivelyVisible).ToList();
        var title = texts.FirstOrDefault(t => t.Text == st.TankTitle);
        var num = texts.FirstOrDefault(t => t.Text == st.Current && t.FontSize >= 30);
        var sub = texts.FirstOrDefault(t => t.Text == "لیتر باقی‌مانده");
        foreach (var (name, t) in new[] { ("عنوانِ مخزن", title), ("عددِ درشت", num), ("«لیتر باقی‌مانده»", sub) })
        {
            if (t is null) { Check(name + " پیدا شد", false); continue; }
            var col = R(t.GetVisualParent<StackPanel>()!);
            //  وسط = وسط‌چین و هم‌پهنای ستونِ خودش
            Check(name + " وسط است", t.TextAlignment == TextAlignment.Center && Math.Abs(R(t).Width - col.Width) < 1,
                  $"{t.TextAlignment} · {R(t).Width:0}/{col.Width:0}");
        }
        var chip = texts.FirstOrDefault(t => t.Text == st.StateText)?.GetVisualParent<Border>();
        if (chip is not null)
        {
            var col = R(chip.GetVisualParent<StackPanel>()!);
            var d = (R(chip).Left + R(chip).Right) / 2 - (col.Left + col.Right) / 2;
            Check("نشانِ حالِ مخزن وسط است", Math.Abs(d) < 1, $"{d:0.0}px");
        }

        var calc = view.GetVisualDescendants().OfType<Border>()
                       .Where(b => b.Classes.Contains("calc") && b.Classes.Contains("tankbox")).Select(R).ToList();
        var typed = view.GetVisualDescendants().OfType<TextBox>()
                        .Where(b => b.Classes.Contains("tankbox")).Select(R).ToList();
        Check("«جمله ورودی» و «جمله فروش» کادر دارند", calc.Count == 2, calc.Count.ToString());
        Check("و دو کادرِ تایپ سرِ جایشان‌اند", typed.Count == 2, typed.Count.ToString());
        var all = calc.Concat(typed).ToList();
        if (all.Count == 4)
        {
            Check("چهار کادر هم‌قد", all.Select(r => Math.Round(r.Height, 1)).Distinct().Count() == 1,
                  string.Join("، ", all.Select(r => r.Height.ToString("0.0"))));
            Check("چهار کادر هم‌پهنا", all.Max(r => r.Width) - all.Min(r => r.Width) < 1,
                  string.Join("، ", all.Select(r => r.Width.ToString("0.0"))));
            Check("و روی یک خط", all.Select(r => Math.Round(r.Y)).Distinct().Count() == 1);
        }
        var inText = texts.FirstOrDefault(t => t.Text == $"{st.TotalIn} لیتر");
        Check("جمله ورودی داخلِ کادرش نوشته شده", inText?.GetVisualParent<Border>()?.Classes.Contains("calc") == true,
              inText?.Text ?? "—");
        //  ⛔ فقط‌خواندنی — هیچ کادرِ تایپی به این دو بند نیست
        Check("و فقط‌خواندنی است (کادرِ تایپ نیست)",
              view.GetVisualDescendants().OfType<TextBox>().All(b => b.Text != $"{st.TotalIn} لیتر"));
        Shot(win, shots, "04-storage");
    }

    // ── ابزارها ──────────────────────────────────────────────────────────────

    /// <summary>
    /// جای واقعیِ کنترل روی پنجره. ⚠️ در راست‌به‌چپ نقطهٔ (۰،۰)ِ محلی لبهٔ
    /// **راستِ** چشم است، پس «مبدأ + اندازه» جای غلطی می‌داد؛ دو گوشه جدا
    /// برده می‌شوند و کمینه/بیشینه‌شان گرفته می‌شود.
    /// </summary>
    private static Rect Screen(Visual v, Visual root)
    {
        var a = v.TranslatePoint(new Point(0, 0), root);
        var b = v.TranslatePoint(new Point(v.Bounds.Width, v.Bounds.Height), root);
        if (a is not { } p || b is not { } q) return default;
        return new Rect(new Point(Math.Min(p.X, q.X), Math.Min(p.Y, q.Y)),
                        new Point(Math.Max(p.X, q.X), Math.Max(p.Y, q.Y)));
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
        for (var i = 0; i < 600 && !t.IsCompleted; i++) { Pump(w); Thread.Sleep(2); }
        Settle(w);
    }
}
