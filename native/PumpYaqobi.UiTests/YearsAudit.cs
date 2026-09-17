using System.Data.Common;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using PumpYaqobi.App.Services;
using PumpYaqobi.App.ViewModels;
using PumpYaqobi.App.ViewModels.Sections;
using PumpYaqobi.App.Views;
using PumpYaqobi.Application.Localization;

namespace PumpYaqobi.UiTests;

/// <summary>
/// ══ پنج سال کار با برنامه — «ببین چقدر کند می‌شود و چه باگ‌هایی رو می‌کند» ═══
///
/// خواستهٔ صاحب ریپو: «همه را با پنج سال استفاده از اپ تست کن.»
///
/// این سنجش یک دیتابیس می‌سازد که شکلش شکلِ دفترِ واقعیِ یک پمپ بعد از پنج
/// سال است: هر روز چند ردیف در هر دفتر، هر روز یک ورق با دو شیفت، صدها
/// قرض‌دار با ردیف‌هایی که در پنج سال پخش شده‌اند، شرکت‌ها، امانت، خریدها،
/// حاضری… بعد همان پنجرهٔ واقعی باز می‌شود و کارهای روزمره زمان می‌گیرند —
/// نه فقط «باز کردنِ بخش» که بارها سنجیده شده، بلکه رفتن به ماهِ سه سال پیش،
/// باز کردنِ حسابی که پنج سال ردیف دارد، ورقِ پنج سال پیش، تاریخچه‌ها…
///
///     dotnet run --project PumpYaqobi.UiTests -- years
/// </summary>
internal static class YearsAudit
{
    private const int Years = 5;
    private const int People = 600;
    private const int RowsPerPerson = 100;     // ۶۰ هزار ردیفِ قرض‌دار در پنج سال
    private const int Companies = 15;
    private const int CompanyRows = 400;
    private const int Amanats = 20;
    private const int AmanatRows = 300;
    private const int Staff = 8;

    private const long Slow = 1_000;
    private const long Broken = 3_000;

    private static readonly List<(string What, long Ms)> Marks = new();
    private static readonly Stopwatch Budget = Stopwatch.StartNew();
    private const long BudgetMs = 600_000;
    private static bool _outOfTime;

    public static int Run()
    {
        var dir = Path.Combine(Path.GetTempPath(), "pump-years-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var file = Path.Combine(dir, "pump.db");

        var sw = Stopwatch.StartNew();
        var stats = Seed(file);
        sw.Stop();
        Console.WriteLine($"پنج سال داده ساخته شد در {sw.ElapsedMilliseconds:N0} ms — {new FileInfo(file).Length / 1_048_576.0:0.0} MB");
        foreach (var (k, v) in stats) Console.WriteLine($"   {k,-28}{v,10:N0}");

        AppHost.Start(file);

        //  سنجه با نصبِ **پلن‌دار** می‌دود — وگرنه داشبورد و مفاد/ضرر و
        //  تاریخچه‌ها قفل‌اند و باز نمی‌شوند. شرحش در `FakeLicense`؛ خودِ
        //  قفل در بندِ ۱۷ی `verify` و در `EntitlementsTests` سنجیده می‌شود.
        FakeLicense.Grant();

        var host = AppHost.Current;
        if (host.Auth.NeedsFirstRun()) host.Auth.CreateFirstAdmin("1234");
        host.Auth.SignIn("admin", "1234");

        Console.WriteLine();
        Console.WriteLine("کار                                              زمان");
        Console.WriteLine(new string('-', 58));

        Mark("خواندنِ فهرستِ قرض‌داران (بی صفحه)", () => host.Debtors.ListAsync(false).GetAwaiter().GetResult());
        Mark("جمع‌های همهٔ حساب‌ها (بی صفحه)", () => host.Debtors.CardAccountsAsync(false).GetAwaiter().GetResult());
        Mark("عکسِ زندهٔ ایستگاه (StationSnapshot)", () => StationSnapshot.BuildAsync(host).GetAwaiter().GetResult());
        Mark("قرض‌های کهنه (همهٔ ردیف‌ها)", () =>
            host.Tools.AgingAsync(PumpYaqobi.Application.Services.AgingFilter.All).GetAwaiter().GetResult());

        AppBuilder.Configure<PumpYaqobi.App.App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .SetupWithoutStarting();

        var win = new MainWindow { Width = 1440, Height = 900 };
        var vm = (MainViewModel)win.DataContext!;
        Mark("باز شدنِ پنجره", () => { win.Show(); Pump(win); });

        Mark("ورود و لودینگ", () =>
        {
            vm.Lock.Password = "1234";
            LockIn.Wait(vm.Lock);
            var end = DateTime.UtcNow + TimeSpan.FromSeconds(180);
            while (vm.Phase == MainViewModel.AppPhase.Starting && DateTime.UtcNow < end)
            { Dispatcher.UIThread.RunJobs(); win.UpdateLayout(); Thread.Sleep(2); }
            Settle(win);
        });

        var thisMonth = Shamsi.ThisMonth();
        var oldMonth = (int.Parse(thisMonth[..4]) - 3) + "/03";

        // ══ هر بخش و هر زیربخش ═════════════════════════════════════════════
        foreach (var sec in vm.Sections.ToList())
        {
            Mark("بخشِ " + sec.Id + " — رفتن (داده + اولین چیدمان)", () => Wait(win, vm.GoAsync(sec)));
            Mark("  " + sec.Id + ": ته‌نشین شدنِ چیدمان", () => Settle(win));
            Console.WriteLine($"     ({LastPasses} پاس · {GridDiag(win)})");
            foreach (var sub in sec.SubSections.ToList())
            {
                // ⚠️ مثلِ خودِ کاربر: فقط باز کردن، و انتظار برای همان باری که
                // ‎MainViewModel‎ می‌کند — نه یک ‎OnActivatedAsync‎ی اضافه از این‌جا.
                Mark("  زیربخشِ " + sub.Id, () => { sec.ShowSub(sub); Wait(win, vm.LastSubOpen ?? Task.CompletedTask); Settle(win); });
                sec.CloseSub();
                Settle(win);
            }

            // ماهِ سه سال پیش و برگشت — دفترهای ماهانه
            if (sec.GetType().GetProperty("Month") is { } mp && mp.PropertyType == typeof(string) && mp.CanWrite)
            {
                Mark($"  {sec.Id}: خواندنِ ماهِ {oldMonth} (فقط داده)", () => { mp.SetValue(sec, oldMonth); WaitIdle(); });
                Mark($"  {sec.Id}: چیدمانِ همان ماه", () => Settle(win));
                Console.WriteLine($"     ({LastPasses} پاس · {GridDiag(win)})");
                Mark($"  {sec.Id}: برگشت به ماهِ جاری", () => { mp.SetValue(sec, thisMonth); WaitIdle(); Settle(win); });
            }
        }

        // ══ صفحهٔ اصلی — رفت‌وبرگشت ═══════════════════════════════════════
        var home = vm.Sections.First(s => s.Id == "dashboard");
        var other = vm.Sections.First(s => s.Id == "expenses");
        for (var i = 1; i <= 3; i++)
        {
            Wait(win, vm.GoAsync(other)); Settle(win);
            Mark($"بازگشت به صفحهٔ اصلی (بارِ {i})", () => { Wait(win, vm.GoAsync(home)); Settle(win); });
        }

        // ══ قرض‌داران: حسابی با پنج سال ردیف ═══════════════════════════════
        if (vm.Sections.FirstOrDefault(s => s.Id == "debt") is DebtSectionViewModel debt)
        {
            Wait(win, vm.GoAsync(debt)); Settle(win);
            Mark("جست‌وجوی نام در فهرستِ کارت‌ها", () => { debt.Search = "بزرگ"; Pump(win); debt.Search = ""; Pump(win); });
            Mark($"باز کردنِ حسابی با {RowsPerPerson * 5:N0} ردیفِ پنج‌ساله", () => { Wait(win, debt.OpenByNumberAsync(1)); SettleLong(win); });
            if (debt.Person?.Current is { } acct && acct.Rows.Count > 0)
            {
                Mark($"ویرایشِ یک خانه در جدولِ {acct.Rows.Count:N0} ردیفی", () => { acct.Rows[0].LitersText = "7"; Pump(win); });
                Mark("رسیدِ سربرگ در حسابِ بزرگ", () => { acct.RasidFuelPetrolText = "500"; SettleLong(win); });
            }
            Mark("بستنِ حساب", () => { debt.PersonOpen = false; Pump(win); });
            Mark("باز کردنِ حسابِ معمولی (شمارهٔ ۳۰۰)", () => { Wait(win, debt.OpenByNumberAsync(300)); Settle(win); });
            debt.PersonOpen = false; Pump(win);
        }

        // ══ ورق: امروز و پنج سال پیش ═══════════════════════════════════════
        if (vm.Sections.FirstOrDefault(s => s.Id == "waraq") is WaraqSectionViewModel wq)
        {
            Wait(win, vm.GoAsync(wq));
            Mark("ورق: فهرستِ ورق‌ها", () => { Wait(win, wq.ReloadAsync()); Settle(win); });
            Console.WriteLine($"   ({wq.Sheets.Count:N0} ورق در فهرست)");
            if (wq.Sheets.Count > 0)
            {
                Mark("ورق: باز کردنِ ورقِ امروز", () => { wq.OpenCommand.Execute(wq.Sheets.First()); SettleLong(win); });
                if (wq.Page is { Txns.Count: > 0 } page)
                {
                    var row = page.Txns[0];
                    Mark("ورق: ویرایشِ یک خانه (ثبت در حساب‌ها)", () =>
                    { row.Name = "قرض‌دارِ 5"; row.AmountText = "500"; Wait(win, row.FlushAsync()); });
                }
                Mark("ورق: باز کردنِ ورقِ پنج سال پیش", () => { wq.OpenCommand.Execute(wq.Sheets.Last()); SettleLong(win); });
            }
        }

        // ══ شرکت‌ها و امانت: باز کردنِ یک حساب ════════════════════════════
        if (vm.Sections.FirstOrDefault(s => s.Id == "companies") is CompanySectionViewModel co)
        {
            Wait(win, vm.GoAsync(co)); Settle(win);
            if (co.Cards.Count > 0 && co.GetType().GetProperty("OpenCommand")?.GetValue(co) is System.Windows.Input.ICommand oc)
                Mark($"شرکت: باز کردنِ حسابی با {CompanyRows:N0} ردیف", () => { oc.Execute(co.Cards[0]); SettleLong(win); });
        }
        if (vm.Sections.FirstOrDefault(s => s.Id == "amanat") is AmanatSectionViewModel am)
        {
            Wait(win, vm.GoAsync(am)); Settle(win);
            if (am.Cards.Count > 0 && am.GetType().GetProperty("OpenCommand")?.GetValue(am) is System.Windows.Input.ICommand oc)
                Mark($"امانت: باز کردنِ حسابی با {AmanatRows:N0} ردیف", () => { oc.Execute(am.Cards[0]); SettleLong(win); });
        }

        // ══ تاریخچه‌ها ═════════════════════════════════════════════════════
        if (vm.Sections.FirstOrDefault(s => s.Id == "history") is HistorySectionViewModel hs)
        {
            Wait(win, vm.GoAsync(hs)); Settle(win);
            foreach (var (k, l) in PumpYaqobi.Services.Data.HistoryService.Kinds)
                Mark("تاریخچه: " + l, () => { Wait(win, hs.OpenAsync(k)); Settle(win); });
        }

        // ══ تم ═════════════════════════════════════════════════════════════
        // ⚠️ روی همان بخشی که کاربر ایستاده (این‌جا: تاریخچه با فهرستِ بلند)،
        // چون هزینهٔ تعویضِ تم همان درختِ جلوی چشم است، نه صفحه‌های کش‌شده.
        Mark("تعویضِ تم (آبی ⇄ طلایی)", () =>
        {
            Mark("  تم ⇒ طلایی (فقط Apply)", () => PumpYaqobi.App.Themes.ThemeManager.Apply(PumpYaqobi.App.Themes.PumpTheme.Gold));
            Mark("  تم ⇒ طلایی (چیدمان)", () => Pump(win));
            Mark("  تم ⇒ آبی (فقط Apply)", () => PumpYaqobi.App.Themes.ThemeManager.Apply(PumpYaqobi.App.Themes.PumpTheme.Blue));
            Mark("  تم ⇒ آبی (چیدمان)", () => Pump(win));
        });
        Mark("تعویضِ تم روی داشبورد (آبی ⇄ طلایی)", () =>
        {
            Wait(win, vm.GoAsync(home)); Settle(win);
            PumpYaqobi.App.Themes.ThemeManager.Apply(PumpYaqobi.App.Themes.PumpTheme.Gold); Pump(win);
            PumpYaqobi.App.Themes.ThemeManager.Apply(PumpYaqobi.App.Themes.PumpTheme.Blue); Pump(win);
        });

        // ══ گزارش ══════════════════════════════════════════════════════════
        Console.WriteLine();
        var slow = Marks.Where(m => m.Ms > Slow).OrderByDescending(m => m.Ms).ToList();
        if (slow.Count == 0) Console.WriteLine($"🎯 هیچ کاری از {Slow:N0} ms رد نشد");
        else
        {
            Console.WriteLine($"🎯 {slow.Count} کار از هدفِ {Slow:N0} ms ردند:");
            foreach (var (what, ms) in slow) Console.WriteLine($"   {what,-46}{ms,6:N0} ms");
        }
        var bad = Marks.Where(m => m.Ms > Broken).ToList();
        Console.WriteLine();
        if (bad.Count == 0) { Console.WriteLine($"✅ با پنج سال داده هیچ کاری از {Broken:N0} ms نگذشت"); return 0; }
        foreach (var (what, ms) in bad) Console.WriteLine($"❌ {what}: {ms:N0} ms");
        return 1;
    }

    private static void Mark(string what, Action run)
    {
        if (_outOfTime) { Console.WriteLine($"{what,-48}     — رد شد (بودجهٔ زمان تمام شد)"); return; }
        var sw = Stopwatch.StartNew();
        try { run(); }
        catch (Exception e) { Console.WriteLine($"{what,-48}  ✖ {e.GetType().Name}: {e.Message}"); Marks.Add((what + " (خطا)", Broken + 1)); return; }
        sw.Stop();
        Marks.Add((what, sw.ElapsedMilliseconds));
        Console.WriteLine($"{what,-48}{sw.ElapsedMilliseconds,6:N0} ms");
        if (Budget.ElapsedMilliseconds > BudgetMs) { _outOfTime = true; Console.WriteLine("⛔ بودجهٔ زمان تمام شد"); }
    }

    /// <summary>
    /// ══ «زیان ناشی از افزایش قیمت» با پنج سال داده — کجا وقت می‌رود؟ ══════════
    ///     dotnet run --project PumpYaqobi.UiTests -- plprof
    /// بی پنجره: خواندنِ قرض‌داران با رسیدها، فاکتورها، تاریخچهٔ نرخ، و خودِ
    /// ‎Build‎ — هر کدام جدا، سه بار.
    /// </summary>
    public static int RunPriceLossProfile()
    {
        var dir = Path.Combine(Path.GetTempPath(), "pump-plprof-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var file = Path.Combine(dir, "pump.db");
        Seed(file);
        AppHost.Start(file);

        //  سنجه با نصبِ **پلن‌دار** می‌دود — وگرنه داشبورد و مفاد/ضرر و
        //  تاریخچه‌ها قفل‌اند و باز نمی‌شوند. شرحش در `FakeLicense`؛ خودِ
        //  قفل در بندِ ۱۷ی `verify` و در `EntitlementsTests` سنجیده می‌شود.
        FakeLicense.Grant();

        var host = AppHost.Current;
        if (host.Auth.NeedsFirstRun()) host.Auth.CreateFirstAdmin("1234");
        host.Auth.SignIn("admin", "1234");

        var svc = new PumpYaqobi.Application.Services.PriceLossService();
        for (var round = 1; round <= 3; round++)
        {
            Console.WriteLine($"── دورِ {round}");
            IReadOnlyList<PumpYaqobi.Domain.Entities.Debtor> debtors = Array.Empty<PumpYaqobi.Domain.Entities.Debtor>();
            IReadOnlyList<PumpYaqobi.Domain.Entities.Invoice> invoices = Array.Empty<PumpYaqobi.Domain.Entities.Invoice>();
            IReadOnlyList<PumpYaqobi.Domain.Entities.RateHistoryEntry> rates = Array.Empty<PumpYaqobi.Domain.Entities.RateHistoryEntry>();
            Mark("قرض‌داران با رسیدها (LoadAllAsync withReceipts)", () => debtors = host.Debtors.LoadAllAsync(withReceipts: true).GetAwaiter().GetResult());
            Mark("فاکتورها (Invoices.ListAsync)", () => invoices = host.Invoices.ListAsync().GetAwaiter().GetResult());
            Mark("تاریخچهٔ نرخ (RateHistoryAsync)", () => rates = host.Tools.RateHistoryAsync().GetAwaiter().GetResult());
            PumpYaqobi.Application.Services.PriceLossReport? rep = null;
            Mark("خودِ گزارش (PriceLossService.Build)", () => rep = svc.Build(debtors, invoices, rates,
                host.Settings.UnionRate(PumpYaqobi.Domain.Enums.FuelType.Petrol),
                host.Settings.UnionRate(PumpYaqobi.Domain.Enums.FuelType.Diesel), PumpYaqobi.Application.Localization.Shamsi.Today()));
            Console.WriteLine($"   قرض‌دار {debtors.Count:N0} · ردیف {debtors.Sum(d => d.AllAccounts().Sum(a => (a.FuelRows?.Count ?? 0) + (a.MoneyRows?.Count ?? 0))):N0} · فاکتور {invoices.Count:N0} · نرخ {rates.Count:N0} · اشخاصِ گزارش {rep?.List.Count ?? 0:N0}");
            var vm = new PumpYaqobi.App.ViewModels.Sections.PriceLossSectionViewModel(host);
            Mark("ویومدل کامل (RefreshAsync، بی پنجره)", () => vm.RefreshAsync().GetAwaiter().GetResult());
            Console.WriteLine($"   ردیف‌های جدول {vm.Rows.Count:N0}");
        }
        return 0;
    }

    // ══ ساختنِ پنج سال داده ═══════════════════════════════════════════════════

    internal static List<(string, long)> Seed(string file)
    {
        var dbf = new PumpYaqobi.Services.Data.PumpDbFactory(file);
        dbf.EnsureReady();
        using var db = dbf.Create();
        var conn = db.Database.GetDbConnection();
        conn.Open();
        PerfAudit.Exec(conn, "PRAGMA synchronous=OFF;");
        PerfAudit.Exec(conn, "PRAGMA foreign_keys=OFF;");
        using var tx = conn.BeginTransaction();

        var now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
        var stats = new List<(string, long)>();
        var days = Days();
        stats.Add(("روز", days.Count));
        var rnd = new Random(7);

        // ── اشخاص و کارمندان ──────────────────────────────────────────────
        var accounts = new List<long>();
        var names = new List<string>();
        using (var person = new PerfAudit.Insert(conn, "Debtors"))
        using (var account = new PerfAudit.Insert(conn, "DebtAccounts"))
        {
            for (var i = 1; i <= People; i++)
            {
                var name = i == 1 ? "قرض‌دارِ بزرگ" : "قرض‌دارِ " + i;
                names.Add(name);
                person.Reset();
                person.Set("Name", name); person.Set("LegacyId", "p" + i); person.Set("IsNoInvoice", i % 20 == 0 ? 1 : 0);
                person.Set("Phone", "07000" + i.ToString("00000"));
                person.Set("CreatedAt", now); person.Set("UpdatedAt", now);
                var pid = person.Run();
                account.Reset();
                account.Set("MainOfDebtorId", pid); account.Set("Name", name); account.Set("Mode", i % 5 == 0 ? 2 : 1);
                account.Set("PercentPetrol", i % 3 == 0 ? "2" : null);
                account.Set("CreatedAt", now); account.Set("UpdatedAt", now);
                accounts.Add(account.Run());
                if (i % 15 == 0)
                {
                    account.Reset();
                    account.Set("DebtorId", pid); account.Set("Name", "فرعی " + i); account.Set("LegacySubId", "s" + i);
                    account.Set("Mode", 1); account.Set("CreatedAt", now); account.Set("UpdatedAt", now);
                    accounts.Add(account.Run());
                }
            }
        }
        stats.Add(("قرض‌دار", People));

        var staffIds = new List<long>();
        using (var st = new PerfAudit.Insert(conn, "StaffMembers"))
            for (var i = 1; i <= Staff; i++)
            {
                st.Reset(); st.Set("Name", "کارمندِ " + i); st.Set("Salary", 15000 + i * 500); st.Set("PayDay", 1);
                st.Set("ShiftIn", "07:00"); st.Set("ShiftOut", "19:00"); st.Set("CreatedAt", now); st.Set("UpdatedAt", now);
                staffIds.Add(st.Run());
            }

        // ── ردیف‌های قرض‌داران، پخش در پنج سال ────────────────────────────
        long debtRows = 0;
        using (var row = new PerfAudit.Insert(conn, "DebtRows"))
        {
            for (var a = 0; a < accounts.Count; a++)
            {
                var n = a == 0 ? RowsPerPerson * 5 : RowsPerPerson;
                for (var k = 0; k < n; k++)
                {
                    var d = days[(a * 131 + k * 17) % days.Count];
                    row.Reset();
                    row.Set("FuelAccountId", accounts[a]); row.Set("SortIndex", k);
                    row.Set("DateShamsi", d.Date); row.Set("DateKey", d.Key);
                    row.Set("Name", "بردگیِ " + k); row.Set("Hawala", "ح" + k);
                    row.Set("Fuel", k % 3 == 0 ? 2 : 1);
                    var liters = k % 50 + 5;
                    row.Set("Liters", liters.ToString()); row.Set("PricePerLiter", "62");
                    row.Set("Bardagi", (liters * 62).ToString()); row.Set("Rasid", k % 4 == 0 ? "1000" : "0");
                    row.Set("RasidFuel", "0"); row.Set("Albaqi", (liters * 62).ToString()); row.Set("ByMoney", 0);
                    row.Set("CreatedAt", now); row.Set("UpdatedAt", now);
                    row.Run(); debtRows++;
                }
            }
        }
        stats.Add(("ردیفِ قرض‌دار", debtRows));

        // ── دفترهای روزانه ───────────────────────────────────────────────
        long ledger = 0;
        using var safe = new PerfAudit.Insert(conn, "SafeEntries");
        using var exp = new PerfAudit.Insert(conn, "Expenses");
        using var exch = new PerfAudit.Insert(conn, "ExchangeRows");
        using var retail = new PerfAudit.Insert(conn, "RetailRows");
        using var rasid = new PerfAudit.Insert(conn, "RasidEntries");
        using var quick = new PerfAudit.Insert(conn, "DebtQuickReceipts");
        using var inv = new PerfAudit.Insert(conn, "Invoices");
        using var dip = new PerfAudit.Insert(conn, "TankDips");
        using var buy = new PerfAudit.Insert(conn, "FuelPurchases");
        using var unload = new PerfAudit.Insert(conn, "TankerUnloads");
        using var rate = new PerfAudit.Insert(conn, "RateHistory");
        using var att = new PerfAudit.Insert(conn, "Attendance");
        using var sal = new PerfAudit.Insert(conn, "SalaryPayments");
        using var shortage = new PerfAudit.Insert(conn, "StaffShortages");
        using var precpt = new PerfAudit.Insert(conn, "ParchaReceipts");
        using var wentry = new PerfAudit.Insert(conn, "WaraqEntries");
        using var wshift = new PerfAudit.Insert(conn, "WaraqShifts");
        using var wpump = new PerfAudit.Insert(conn, "WaraqPumps");
        using var wtxn = new PerfAudit.Insert(conn, "WaraqTransactions");
        using var shiftData = new PerfAudit.Insert(conn, "ShiftDataSet");
        using var report = new PerfAudit.Insert(conn, "Reports");

        void Base(PerfAudit.Insert ins, Day d, int i, string title)
        {
            ins.Reset();
            ins.Set("DateShamsi", d.Date); ins.Set("DateKey", d.Key); ins.Set("MonthKey", d.Month);
            ins.Set("Name", title); ins.Set("Title", title); ins.Set("Description", title); ins.Set("Note", "");
            ins.Set("SortIndex", i); ins.Set("CreatedAt", now); ins.Set("UpdatedAt", now);
        }

        var dayNo = 0;
        foreach (var d in days)
        {
            dayNo++;
            for (var i = 0; i < 5; i++)
            {
                Base(safe, d, i, i % 2 == 0 ? "ماندگیِ شیفت" : "بردگیِ روزانه");
                safe.Set("Kind", i % 3 == 0 ? 1 : 2); safe.Set("Amount", 1000 + (dayNo * 7 + i) % 9000); safe.Set("Currency", i % 7 == 0 ? 2 : 1);
                safe.Run(); ledger++;
            }
            for (var i = 0; i < 4; i++)
            {
                Base(exp, d, i, i % 2 == 0 ? "نانِ کارمندان" : "ترمیم"); exp.Set("Amount", 200 + (dayNo * 3 + i) % 1500);
                exp.Run(); ledger++;
            }
            for (var i = 0; i < 2; i++)
            {
                Base(exch, d, i, "حوالهٔ " + dayNo); exch.Set("Amount", 100000 + (dayNo % 50) * 1000); exch.Set("Currency", i % 2 + 1);
                exch.Set("Rate", 70 + dayNo % 5); exch.Set("Bardagi", i == 0 ? 300 : 0);
                exch.Run(); ledger++;
            }
            for (var i = 0; i < 6; i++)
            {
                Base(retail, d, i, "چکنهٔ " + i); retail.Set("Fuel", i % 3 == 0 ? 2 : 1); retail.Set("Liters", 10 + i);
                retail.Set("PricePerLiter", 62); retail.Set("Rasid", i % 2 == 0 ? 400 : 0); retail.Set("Bardagi", 0); retail.Set("ByMoney", 0);
                retail.Run(); ledger++;
            }
            for (var i = 0; i < 2; i++)
            {
                Base(rasid, d, i, ""); rasid.Set("AccountId", accounts[(dayNo * 13 + i) % accounts.Count]);
                rasid.Set("Unit", 1); rasid.Set("Fuel", 1); rasid.Set("Value", 500 + i * 100);
                rasid.Run(); ledger++;
            }
            Base(quick, d, 0, names[dayNo % names.Count]); quick.Set("LegacyId", "q" + dayNo); quick.Set("Amount", 700); quick.Run(); ledger++;
            Base(inv, d, 0, "مشتری " + dayNo); inv.Set("InvoiceNumber", dayNo); inv.Set("CustomerName", "مشتری " + dayNo);
            inv.Set("Fuel", dayNo % 2 + 1); inv.Set("PricePerLiter", "62"); inv.Set("Liters", "100"); inv.Set("Status", 2); inv.Set("CreatedAtUtc", now); inv.Run(); ledger++;
            Base(dip, d, 0, ""); dip.Set("Fuel", 1); dip.Set("Measured", 8000 - dayNo % 3000); dip.Set("Expected", 8100 - dayNo % 3000); dip.Run(); ledger++;
            if (dayNo % 7 == 0)
            {
                Base(buy, d, 0, ""); buy.Set("Fuel", dayNo % 14 == 0 ? 2 : 1); buy.Set("Seller", "شرکتِ تیلِ " + (dayNo % Companies + 1));
                buy.Set("Kg", 20000); buy.Set("Density", "0.73"); buy.Set("PriceTon", 1200); buy.Set("UsdRate", 65);
                buy.Set("Ton", 20); buy.Set("Liters", 27397); buy.Set("TotalUsd", 24000); buy.Set("TotalAfn", 1560000); buy.Set("PerLiter", "56.9");
                buy.Run(); ledger++;
                Base(unload, d, 0, ""); unload.Set("Fuel", 1); unload.Set("Manifest", "27000"); unload.Set("Actual", "26900"); unload.Run(); ledger++;
                Base(shortage, d, 0, "کارمندِ " + (dayNo % Staff + 1)); shortage.Set("StaffId", staffIds[dayNo % Staff]); shortage.Set("Amount", 500); shortage.Set("Paid", 0); shortage.Run(); ledger++;
            }
            if (d.Date.EndsWith("/01"))
            {
                Base(rate, d, 0, ""); rate.Set("Fuel", 1); rate.Set("Rate", "70"); rate.Set("Value", "70"); rate.Set("AtUtc", now); rate.Run();
                foreach (var sid in staffIds)
                {
                    Base(sal, d, 0, ""); sal.Set("StaffId", sid); sal.Set("Amount", 15000); sal.Run(); ledger++;
                }
            }
            foreach (var sid in staffIds)
            {
                Base(att, d, 0, ""); att.Set("StaffId", sid); att.Set("In", "07:00"); att.Set("Out", "19:00"); att.Run(); ledger++;
            }
            for (var i = 0; i < 2; i++)
            {
                Base(precpt, d, i, names[(dayNo + i) % names.Count]); precpt.Set("Account", names[(dayNo + i) % names.Count]);
                precpt.Set("Hawala", "ح" + dayNo); precpt.Set("Liters", 50); precpt.Set("PricePerLiter", 62); precpt.Set("Rasid", 3100); precpt.Set("Posted", 1);
                precpt.Run(); ledger++;
            }

            // ورق: یک ورق در روز، دو شیفت، هر شیفت چهار پایه و شش ردیفِ قرض/مصرف
            Base(wentry, d, 0, ""); wentry.Set("Station", "پمپ یعقوبی"); wentry.Set("ActiveShift", 1);
            var wid = wentry.Run();
            for (var s = 1; s <= 2; s++)
            {
                wshift.Reset(); wshift.Set("WaraqId", wid); wshift.Set("Kind", s); wshift.Set("WorkerName", "کارمندِ " + s);
                wshift.Set("PricePerLiter", 62); wshift.Set("PricePerLiterDiesel", 58); wshift.Set("CreatedAt", now); wshift.Set("UpdatedAt", now);
                var shid = wshift.Run();
                for (var p = 1; p <= 4; p++)
                {
                    wpump.Reset(); wpump.Set("ShiftId", shid); wpump.Set("SortIndex", p); wpump.Set("Num", p); wpump.Set("Fuel", p % 2 == 0 ? 2 : 1);
                    wpump.Set("Worker", "کارمندِ " + p); wpump.Set("Start", dayNo * 1000 + p * 100); wpump.Set("End", dayNo * 1000 + p * 100 + 350);
                    wpump.Set("PricePerLiter", 62); wpump.Set("Debt", 0); wpump.Set("DateShamsi", d.Date); wpump.Set("CreatedAt", now); wpump.Set("UpdatedAt", now);
                    wpump.Run();
                }
                for (var t = 0; t < 6; t++)
                {
                    wtxn.Reset(); wtxn.Set("ShiftId", shid); wtxn.Set("SortIndex", t); wtxn.Set("Name", names[(dayNo * 5 + t) % names.Count]);
                    wtxn.Set("Liters", 20 + t); wtxn.Set("Amount", (20 + t) * 62); wtxn.Set("Type", t % 5 == 4 ? 2 : 1); wtxn.Set("Fuel", t % 2 + 1);
                    wtxn.Set("Unit", 1); wtxn.Set("CreatedAt", now); wtxn.Set("UpdatedAt", now);
                    wtxn.Run();
                }
                ledger += 10;
            }

            // پارچه: هر روز یک گزارش با شیفتِ روز
            shiftData.Reset(); shiftData.Set("Name", "کارمندِ 1"); shiftData.Set("PumpNum", 1); shiftData.Set("Start", dayNo * 1000); shiftData.Set("End", dayNo * 1000 + 900);
            shiftData.Set("Price", 62); shiftData.Set("ProfitPer", 3); shiftData.Set("BuyPerLiter", 59); shiftData.Set("Sale", 55800); shiftData.Set("Money", 40000);
            shiftData.Set("Debt", 15800); shiftData.Set("Available", 40000); shiftData.Set("CreatedAt", now); shiftData.Set("UpdatedAt", now);
            var sd = shiftData.Run();
            Base(report, d, 0, ""); report.Set("ReportNum", dayNo); report.Set("Fuel", 1); report.Set("DayShiftId", sd); report.Set("LegacyId", "r" + dayNo); report.Run();
            ledger += 2;
        }
        stats.Add(("ردیفِ دفترها و ورق‌ها", ledger));

        // ── شرکت‌ها و امانت ───────────────────────────────────────────────
        using (var co = new PerfAudit.Insert(conn, "TilCompanies"))
        using (var cr = new PerfAudit.Insert(conn, "CompanyRows"))
            for (var i = 1; i <= Companies; i++)
            {
                co.Reset(); co.Set("Name", "شرکتِ تیلِ " + i); co.Set("LegacyId", "c" + i); co.Set("CreatedAt", now); co.Set("UpdatedAt", now);
                var cid = co.Run();
                for (var k = 0; k < CompanyRows; k++)
                {
                    var d = days[(i * 97 + k * 23) % days.Count];
                    cr.Reset(); cr.Set("CompanyId", cid); cr.Set("Fuel", k % 3 == 0 ? 2 : 1); cr.Set("SortIndex", k);
                    cr.Set("DateShamsi", d.Date); cr.Set("DateKey", d.Key); cr.Set("Name", k % 4 == 0 ? "پرداخت" : "خرید");
                    cr.Set("Kg", k % 4 == 0 ? 0 : 20000); cr.Set("Ton", k % 4 == 0 ? 0 : 20); cr.Set("Usd", k % 4 == 0 ? 0 : 24000); cr.Set("Rate", 65);
                    cr.Set("Poul", k % 4 == 0 ? 500000 : 0); cr.Set("PoulCurrency", 1); cr.Set("CreatedAt", now); cr.Set("UpdatedAt", now);
                    cr.Run();
                }
            }
        stats.Add(("ردیفِ شرکت‌ها", Companies * CompanyRows));

        using (var aa = new PerfAudit.Insert(conn, "AmanatAccounts"))
        using (var ar = new PerfAudit.Insert(conn, "AmanatRows"))
            for (var i = 1; i <= Amanats; i++)
            {
                aa.Reset(); aa.Set("Name", "امانتِ " + i); aa.Set("Fuel", i % 2 + 1); aa.Set("MyPct", "2"); aa.Set("CreatedAt", now); aa.Set("UpdatedAt", now);
                var aid = aa.Run();
                for (var k = 0; k < AmanatRows; k++)
                {
                    var d = days[(i * 89 + k * 19) % days.Count];
                    ar.Reset(); ar.Set("AccountId", aid); ar.Set("SortIndex", k); ar.Set("DateShamsi", d.Date); ar.Set("DateKey", d.Key);
                    ar.Set("Name", "تانکرِ " + k); ar.Set("Liters", "20000"); ar.Set("Taken", k % 3 == 0 ? "0" : "19500"); ar.Set("Days", "12");
                    ar.Set("Temp", "28"); ar.Set("State", k % 3 == 0 ? 1 : 2); ar.Set("CreatedAt", now); ar.Set("UpdatedAt", now);
                    ar.Run();
                }
            }
        stats.Add(("ردیفِ امانت", Amanats * AmanatRows));

        // ── آرشیوهای جدولِ چهل حساب ───────────────────────────────────────
        var rowsJson = "[" + string.Join(",", Enumerable.Range(0, 200).Select(k =>
            "{\"DateShamsi\":\"" + days[k % days.Count].Date + "\",\"Name\":\"ردیفِ " + k +
            "\",\"Fuel\":1,\"Liters\":10,\"PricePerLiter\":62,\"Bardagi\":620,\"Rasid\":0,\"RasidFuel\":0,\"Albaqi\":620,\"ByMoney\":false}")) + "]";
        using (var arc = new PerfAudit.Insert(conn, "DebtTableArchives"))
            for (var a = 0; a < 40; a++)
                for (var i = 0; i < 4; i++)
                {
                    arc.Reset(); arc.Set("AccountId", accounts[a]); arc.Set("RowsJson", rowsJson); arc.Set("RowCount", 200);
                    arc.Set("IsMoney", 0); arc.Set("CreatedShamsi", days[(a * 200 + i * 300) % days.Count].Date);
                    arc.Set("CreatedAt", now); arc.Set("UpdatedAt", now);
                    arc.Run();
                }
        stats.Add(("آرشیوِ جدول", 160));

        tx.Commit();
        PerfAudit.Exec(conn, "ANALYZE;");
        conn.Close();
        return stats;
    }

    private readonly record struct Day(string Date, int Key, string Month);

    /// <summary>هر روزِ پنج سالِ گذشته تا امروز — به شمسی.</summary>
    private static List<Day> Days()
    {
        var today = Shamsi.Today();
        var todayKey = Shamsi.Key(today);
        var y0 = int.Parse(today[..4]) - (Years - 1);
        var list = new List<Day>();
        for (var y = y0; y <= y0 + Years - 1; y++)
            for (var m = 1; m <= 12; m++)
            {
                var len = m <= 6 ? 31 : m <= 11 ? 30 : 29;
                for (var d = 1; d <= len; d++)
                {
                    var s = $"{y}/{m:00}/{d:00}";
                    var k = Shamsi.Key(s);
                    if (k > todayKey) break;
                    list.Add(new Day(s, k, s[..7]));
                }
            }
        return list;
    }

    private static void Pump(Window w)
    {
        for (var i = 0; i < 3; i++) { Dispatcher.UIThread.RunJobs(); w.UpdateLayout(); }
    }

    /// <summary>کارِ پس‌زمینهٔ ویومدل (خواندنِ دیتابیس) تمام شود — بی چیدمان.</summary>
    private static void WaitIdle()
    {
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < 20_000)
        {
            Dispatcher.UIThread.RunJobs();
            if (!Dispatcher.UIThread.HasJobsWithPriority(DispatcherPriority.Background)) { Thread.Sleep(30); Dispatcher.UIThread.RunJobs(); break; }
            Thread.Sleep(2);
        }
    }

    /// <summary>وضعِ اولین جدولِ دیده‌شده: چند ردیف، چند ساخته، چند پاسِ اندازه‌گیری.</summary>
    private static string GridDiag(Window w)
    {
        var g = Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(w)
                 .OfType<PumpYaqobi.App.Controls.ExcelGrid>().FirstOrDefault(x => x.IsEffectivelyVisible);
        if (g is null) return "بی جدول";
        var rows = g.ItemsSource is System.Collections.ICollection c ? c.Count : -1;
        var live = Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(g).OfType<DataGridRow>().Count();
        return $"جدول: {rows} ردیف · {live} زنده · نمایش تا {g.DiagShown} · اندازه‌گیری‌ها {PumpYaqobi.App.Controls.ExcelGrid.DiagMeasure}";
    }

    /// <summary>چند پاس تا چیدمان معتبر شد — برای فهمیدنِ «حلقهٔ چیدمان».</summary>
    private static int LastPasses;

    private static void Settle(Window w)
    {
        var sw = Stopwatch.StartNew();
        var n = 0;
        for (var i = 0; i < 60 && sw.ElapsedMilliseconds < 5_000; i++)
        {
            n++;
            Dispatcher.UIThread.RunJobs();
            w.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
            if (w.IsMeasureValid && w.IsArrangeValid && !Dispatcher.UIThread.HasJobsWithPriority(DispatcherPriority.Background)) break;
        }
        LastPasses = n;
    }

    /// <summary>کارهای پس‌زمینه (خواندنِ ماه، باز کردنِ حساب) را هم صبر می‌کند.</summary>
    private static void SettleLong(Window w)
    {
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < 400) { Pump(w); Thread.Sleep(5); }
        Settle(w);
    }

    private static void Wait(Window w, Task t)
    {
        var sw = Stopwatch.StartNew();
        while (!t.IsCompleted && sw.ElapsedMilliseconds < 30_000) Pump(w);
        Pump(w);
        if (!t.IsCompleted) Console.WriteLine("        ⚠️ سی ثانیه گذشت و هنوز تمام نشده");
    }
}
