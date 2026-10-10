using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using PumpYaqobi.App.Services;
using PumpYaqobi.App.Themes;
using PumpYaqobi.App.ViewModels;
using PumpYaqobi.App.ViewModels.Sections;
using PumpYaqobi.App.Views;
using PumpYaqobi.Application.Localization;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;
using PumpYaqobi.Domain;

namespace PumpYaqobi.UiTests;

/// <summary>
/// ══ عکس‌های تبلیغاتی — با کیفیتِ ۲ برابر ═════════════════════════════════
///
///     dotnet run --project PumpYaqobi.UiTests -c Release -- marketing [پوشه]
///
/// خواستهٔ صاحب ریپو (۱۴۰۵/۰۷/۱۵): «عکس با کیفیت از برنامه بگیر، برای
/// تلگرام.» دادهٔ نمونهٔ ‎Seed‎ نام‌هایی مثلِ «قرض‌دارِ شمارهٔ ۱» دارد؛ این
/// یکی نام‌ها و عددهای باورپذیر دارد. ⛔ فقط در دیتابیسِ موقت و فقط برای عکس —
/// هرگز داخلِ خودِ برنامه نمی‌رود. هیچ چیزی را نمی‌سنجد.
/// </summary>
internal static class MarketingShots
{
    public static int Run(string? outDir)
    {
        outDir ??= Path.Combine(Path.GetTempPath(), "pump-marketing");
        Directory.CreateDirectory(outDir);
        var dir = Path.Combine(Path.GetTempPath(), "pump-mk-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        AppHost.Start(Path.Combine(dir, "pump.db"));
        FakeLicense.Grant();

        AppBuilder.Configure<PumpYaqobi.App.App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .SetupWithoutStarting();

        var win = new MainWindow { Width = 1440, Height = 900 };
        win.Show();
        var scale = double.TryParse(Environment.GetEnvironmentVariable("MK_SCALE"),
            System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var s) ? s : 2.0;
        LayoutCycleProbe.SetScaling(win, scale);
        Settle(win);
        var vm = (MainViewModel)win.DataContext!;
        LockIn.Wait(vm.Lock);
        Settle(win);
        //  دادهٔ نمونه پس از ورود (کارها اجازهٔ کاربرِ واردشده را می‌خواهند)
        Fill(AppHost.Current);
        Settle(win);

        SectionViewModel? By(string id) => vm.Sections.FirstOrDefault(x => x.Id == id);

        foreach (var theme in new[] { PumpTheme.Blue, PumpTheme.Gold })
        {
            ThemeManager.Apply(theme);
            Settle(win);
            var t = theme == PumpTheme.Blue ? "light" : "dark";

            if (By("debt") is DebtSectionViewModel debt)
            {
                Wait(win, vm.GoAsync(debt));
                Shot(win, Path.Combine(outDir, $"{t}-02-debtors.png"));
                var first = AppHost.Current.Debtors.ListAsync().GetAwaiter().GetResult().First();
                Wait(win, debt.OpenPersonAsync(first.Id));
                Shot(win, Path.Combine(outDir, $"{t}-03-account.png"));
                debt.CloseOpenPage();
                Settle(win);
            }
            foreach (var (id, name) in new[]
            {
                ("shifts", "04-parcha"), ("storage", "06-storage"), ("profit", "07-profit"),
                ("noinv", "08-companies"), ("invoices", "09-invoices"), ("safe", "10-safe"),
                ("attendance", "11-staff"), ("history", "12-history"),
            })
            {
                if (By(id) is not { } sec) continue;
                Wait(win, vm.GoAsync(sec));
                Shot(win, Path.Combine(outDir, $"{t}-{name}.png"));
            }
            if (By("waraq") is WaraqSectionViewModel waraq)
            {
                Wait(win, vm.GoAsync(waraq));
                var card = waraq.Cards.FirstOrDefault();
                if (card is not null && waraq.GetType().GetMethod("OpenCardAsync",
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                        ?.Invoke(waraq, new object?[] { card }) is Task open)
                    Wait(win, open);
                Shot(win, Path.Combine(outDir, $"{t}-05-waraq.png"));
                waraq.CloseOpenPage();
                Settle(win);
            }
            //  داشبورد آخر: تا هشدارها (هر پنج ثانیه) حساب شده باشند
            if (By("dashboard") is DashboardSectionViewModel dash)
            {
                Wait(win, vm.GoAsync(dash));
                Wait(win, dash.RefreshAsync());
                Shot(win, Path.Combine(outDir, $"{t}-01-dashboard.png"));
            }
        }
        Console.WriteLine("عکس‌ها در " + outDir);
        return 0;
    }

    // ══ دادهٔ نمونهٔ باورپذیر ═════════════════════════════════════════════

    private static readonly string[] People =
    {
        "حاجی قدیر احمدی", "محمد کریم رحیمی", "نجیب‌الله صافی", "ترانسپورتیِ امید", "سید جلال هاشمی",
        "عبدالرحمن نوری", "شرکتِ ساختمانیِ آریا", "داوود فرهمند", "زلمی احمدزی", "فضل‌احمد کاکړ",
        "موترِ باربریِ ۴۴۲۱", "حمیدالله یوسفی",
    };
    private static readonly string[] Companies = { "شرکتِ تیلِ آریانا", "شرکتِ نفتِ هرات", "شرکتِ تیلِ بلخ", "ترمینالِ حیرتان" };
    private static readonly string[] Staff = { "احمد", "بصیر", "جاوید", "نوید", "شفیق" };

    internal static void Fill(AppHost host)
    {
        var month = Shamsi.ThisMonth();
        var rnd = new Random(1405);
        string Day(int i) => $"{month}/{i:00}";
        string Ago(int d) => Shamsi.Of(AppClock.Today.AddDays(-d));

        host.Tools.RecordRateAsync(FuelType.Petrol, 68m).GetAwaiter().GetResult();
        host.Tools.RecordRateAsync(FuelType.Diesel, 64m).GetAwaiter().GetResult();

        // قرض‌داران — بعضی تمام کرده، بعضی کم مانده، بیشتر سالم
        for (var p = 0; p < People.Length; p++)
        {
            var d = host.Debtors.AddDebtorAsync(People[p], "07" + (90 + p) + "123" + (400 + p * 7), false).GetAwaiter().GetResult();
            var full = host.Debtors.LoadFullAsync(d.Id).GetAwaiter().GetResult()!;
            var acc = full.MainAccount!;
            host.Debt.SetPercent(acc, FuelType.Petrol, p % 4 == 0 ? 2 : null);
            host.Debtors.UpdateAccountAsync(acc).GetAwaiter().GetResult();
            var rows = 5 + p % 4;
            decimal usedP = 0, usedD = 0;
            for (var i = 1; i <= rows; i++)
            {
                var diesel = (p + i) % 3 == 0;
                var lit = 40 + rnd.Next(10, 90) * (p % 3 == 1 ? 3 : 1);
                if (diesel) usedD += lit; else usedP += lit;
                host.Debtors.SaveRowAsync(new DebtRow
                {
                    FuelAccountId = acc.Id, SortIndex = i,
                    DateShamsi = Ago(28 - i * 3),
                    Name = i % 3 == 0 ? "حواله" : People[p].Split(' ')[0],
                    Hawala = i % 3 == 0 ? (700 + p * 10 + i).ToString() : "",
                    Fuel = diesel ? FuelType.Diesel : FuelType.Petrol,
                    Liters = lit,
                    PricePerLiter = diesel ? 64 : 68,
                }).GetAwaiter().GetResult();
            }
            //  رسیدِ پولی در خودِ جدول — همان‌جا که برنامه رسید را می‌شمارد
            //  رسید = اعتبارِ پیش‌پرداخت: یکی اضافه برده، یکی ۹۰٪، یکی ۷۵٪، بقیه سالم
            var fm = p switch { 1 => 0.95m, 3 => 1m / 0.92m, 6 => 1m / 0.76m, _ => 1.6m + p % 4 * 0.15m };
            var bardagi = usedP * 68 + usedD * 64;
            host.Debtors.SaveRowAsync(new DebtRow
            {
                FuelAccountId = acc.Id, SortIndex = rows + 1, DateShamsi = Ago(1),
                Name = "رسیدِ نقد", Fuel = FuelType.Petrol, Rasid = Math.Round(bardagi * fm / 100) * 100,
            }).GetAwaiter().GetResult();
            //  رسیدِ تیل هم ردیفِ جدول است (سربرگ از روی ردیف‌ها حساب می‌شود)
            if (usedP > 0)
                host.Debtors.SaveRowAsync(new DebtRow
                { FuelAccountId = acc.Id, SortIndex = rows + 2, DateShamsi = Ago(1), Name = "رسیدِ پطرول", Fuel = FuelType.Petrol, RasidFuel = Math.Round(usedP * fm) }).GetAwaiter().GetResult();
            if (usedD > 0)
                host.Debtors.SaveRowAsync(new DebtRow
                { FuelAccountId = acc.Id, SortIndex = rows + 3, DateShamsi = Ago(1), Name = "رسیدِ دیزل", Fuel = FuelType.Diesel, RasidFuel = Math.Round(usedD * fm) }).GetAwaiter().GetResult();
            //  اعتبارِ پیش‌پرداختِ تیل: بیشتر سالم، یکی تمام‌کرده، یکی اضافه‌برده، یکی نزدیکِ ۹۰٪
            var f = fm;
            full = host.Debtors.LoadFullAsync(d.Id).GetAwaiter().GetResult()!;
            acc = full.MainAccount!;
            acc.RasidFuelPetrol = Math.Round(usedP * f / 10) * 10;
            acc.RasidFuelDiesel = Math.Round(usedD * f / 10) * 10;
            host.Debtors.UpdateAccountAsync(acc).GetAwaiter().GetResult();
        }

        // شرکت‌های تیل
        for (var k = 0; k < Companies.Length; k++)
        {
            var comp = host.Companies.AddAsync(Companies[k]).GetAwaiter().GetResult();
            for (var i = 1; i <= 6; i++)
            {
                host.Companies.SaveRowAsync(new CompanyRow
                {
                    CompanyId = comp.Id, Fuel = i % 3 == 0 ? FuelType.Diesel : FuelType.Petrol,
                    SortIndex = i, DateShamsi = Day(i * 4),
                    Name = i % 2 == 0 ? "خریدِ تانکر" : "رسید",
                    Kg = i % 2 == 0 ? 28000 + k * 1500 : 0,
                    Usd = i % 2 == 0 ? 705 + k * 3 : 0, Rate = 71,
                    Poul = i % 2 == 0 ? 0 : 450000 + k * 25000,
                }).GetAwaiter().GetResult();
            }
        }

        // پارچه‌ها — سی روز، روز و شب، پطرول و دیزل
        long p1 = 1_284_300, p2 = 948_120, p3 = 612_450;
        for (var o = 29; o >= 0; o--)
        {
            var day = Ago(o);
            var rep = host.ParchaData.AddAsync(FuelType.Petrol, day).GetAwaiter().GetResult();
            var dl = 1100 + rnd.Next(0, 700); var nl = 700 + rnd.Next(0, 500);
            host.ParchaData.SaveShiftAsync(rep, ShiftKind.Day, new ShiftData
            { Name = Staff[o % 5], PumpNum = 1, Start = p1, End = p1 + dl, Price = 68, ProfitPer = 2.5m, Debt = 8000 + rnd.Next(0, 9000) }).GetAwaiter().GetResult();
            p1 += dl;
            host.ParchaData.SaveShiftAsync(rep, ShiftKind.Night, new ShiftData
            { Name = Staff[(o + 2) % 5], PumpNum = 2, Start = p2, End = p2 + nl, Price = 68, ProfitPer = 2.5m, Debt = 4000 + rnd.Next(0, 6000) }).GetAwaiter().GetResult();
            p2 += nl;
            var dRep = host.ParchaData.AddAsync(FuelType.Diesel, day).GetAwaiter().GetResult();
            var ddl = 600 + rnd.Next(0, 500);
            host.ParchaData.SaveShiftAsync(dRep, ShiftKind.Day, new ShiftData
            { Name = Staff[(o + 1) % 5], PumpNum = 3, Start = p3, End = p3 + ddl, Price = 64, ProfitPer = 2m, Debt = 5000 + rnd.Next(0, 5000) }).GetAwaiter().GetResult();
            p3 += ddl;

            host.ExpenseLedger.AddAsync(new Expense
            { DateShamsi = day, Title = o % 3 == 0 ? "برق" : o % 3 == 1 ? "نانِ کارمندان" : "ترمیمِ پایه", Amount = 1500 + rnd.Next(0, 4000) }).GetAwaiter().GetResult();
            host.SafeLedger.AddAsync(new SafeEntry
            { DateShamsi = day, Kind = SafeEntryKind.Mandagi, Title = "ماندگیِ شیفت", Amount = 95000 + rnd.Next(0, 40000) }).GetAwaiter().GetResult();
            if (o % 2 == 0)
                host.SafeLedger.AddAsync(new SafeEntry
                { DateShamsi = day, Kind = SafeEntryKind.Bardagi, Title = o % 4 == 0 ? "پرداخت به شرکتِ تیل" : "مصرفِ جایگاه", Amount = 70000 + rnd.Next(0, 50000) }).GetAwaiter().GetResult();
            if (o % 6 == 0)
                host.SafeLedger.AddAsync(new SafeEntry
                { DateShamsi = day, Kind = SafeEntryKind.Mandagi, Title = "تبادله به دالر", Amount = 400 + rnd.Next(0, 500), Currency = Currency.Usd }).GetAwaiter().GetResult();
        }

        // ورقِ امروز
        {
            var w = host.WaraqData.OpenOrCreateAsync(Ago(0), "پمپ بنزین").GetAwaiter().GetResult();
            var day = w.Shifts.First(x => x.Kind == ShiftKind.Day);
            day.WorkerName = "احمد";
            host.WaraqData.SaveShiftAsync(day).GetAwaiter().GetResult();
            long[] starts = { 1_284_300, 948_120, 612_450 };
            for (var k = 1; k <= 3; k++)
                host.WaraqData.SavePumpAsync(new WaraqPump
                {
                    ShiftId = day.Id, SortIndex = k, Num = k, Fuel = k == 3 ? FuelType.Diesel : FuelType.Petrol,
                    Start = starts[k - 1], End = starts[k - 1] + 900 + k * 170, PricePerLiter = k == 3 ? 64 : 68, Debt = 6000,
                }).GetAwaiter().GetResult();
            var txns = day.Transactions.OrderBy(x => x.SortIndex).Take(5).ToList();
            var names = new[] { "حاجی قدیر", "نانِ کارمندان", "ترانسپورتیِ امید د", "سید جلال", "برق" };
            for (var k = 0; k < txns.Count; k++)
            {
                var exp = k == 1 || k == 4;
                txns[k].Name = names[k];
                txns[k].Liters = exp ? 0 : 40 + k * 25;
                txns[k].Amount = exp ? 800 + k * 300 : 0;
                txns[k].Type = exp ? WaraqTxnType.Expense : WaraqTxnType.Debt;
                txns[k].AmountAuto = !exp;
                host.WaraqData.SaveTxnAsync(txns[k]).GetAwaiter().GetResult();
            }
        }

        // خریدهای مخزن
        host.Settings.Set("tankCapacity_petrol", 60000m);
        host.Settings.Set("tankCapacity_diesel", 30000m);
        for (var i = 1; i <= 3; i++)
            host.StorageData.AddPurchaseAsync(new FuelPurchase
            { Fuel = FuelType.Petrol, DateShamsi = Ago(34 - i * 10), Seller = Companies[i % 4], Kg = 26700 + i * 100, Density = 0.745m, PriceTon = 705 + i, UsdRate = 71 }).GetAwaiter().GetResult();
        for (var i = 1; i <= 2; i++)
            host.StorageData.AddPurchaseAsync(new FuelPurchase
            { Fuel = FuelType.Diesel, DateShamsi = Ago(33 - i * 14), Seller = Companies[(i + 1) % 4], Kg = 13600 + i * 100, Density = 0.835m, PriceTon = 668 + i, UsdRate = 71 }).GetAwaiter().GetResult();

        // فاکتورها
        for (var i = 1; i <= 7; i++)
        {
            var v = host.Invoices.AddAsync(new Invoice
            {
                DateShamsi = Ago(i * 2), CustomerName = People[i % People.Length],
                Fuel = i % 3 == 0 ? FuelType.Diesel : FuelType.Petrol,
                PricePerLiter = i % 3 == 0 ? 64 : 68, Liters = 150 + i * 40,
                VehicleType = i % 2 == 0 ? "لاری" : "موتر", Phone = "079912340" + i,
            }).GetAwaiter().GetResult();
            if (i % 2 == 0) host.Invoices.ApproveAsync(v.Id, i % 3 == 0 ? 64m : 68m).GetAwaiter().GetResult();
        }

        // کارمندان و حاضری
        for (var i = 0; i < Staff.Length; i++)
        {
            var st = host.Attendance.AddStaffAsync(Staff[i], 13000 + i * 1000).GetAwaiter().GetResult();
            for (var d = 1; d <= 12; d++)
                host.Attendance.SaveRowAsync(new AttendanceRow
                { StaffId = st.Id, DateShamsi = Day(d), In = i % 2 == 0 ? "07:00" : "19:00", Out = i % 2 == 0 ? "19:00" : "07:00" }).GetAwaiter().GetResult();
        }
    }

    internal static void Shot(Window w, string path)
    {
        Settle(w);
        AppHost.Current.Toasts.Visible = false;   // توستِ گذرا روی عکس ننشیند
        Settle(w);
        using var frame = w.CaptureRenderedFrame();
        frame?.Save(path);
        Console.WriteLine("  📷 " + Path.GetFileName(path));
    }

    private static void Pump(Window w)
    {
        for (var i = 0; i < 8; i++) { Dispatcher.UIThread.RunJobs(); w.UpdateLayout(); }
    }

    internal static void Settle(Window w)
    {
        for (var i = 0; i < 60; i++) { Pump(w); Thread.Sleep(5); }
    }

    internal static void Wait(Window w, Task t)
    {
        for (var i = 0; i < 1500 && !t.IsCompleted; i++) { Pump(w); Thread.Sleep(2); }
        Settle(w);
    }
}
