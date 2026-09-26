using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PumpYaqobi.App.Services;
using PumpYaqobi.App.ViewModels;
using PumpYaqobi.App.ViewModels.Sections;
using PumpYaqobi.App.Views;
using PumpYaqobi.Application.Localization;
using PumpYaqobi.Domain.Entities;

namespace PumpYaqobi.UiTests;

/// <summary>
/// ══ رسیدِ سربرگ روی حسابی که جدولش خالی است ═══════════════════════════════
///
/// گزارشِ صاحب ریپو (۱۴۰۵/۰۷/۱۴): «وقتی توی سربرگ رسید می‌زنم و جدولی نباشه،
/// رسید رو می‌گیره اما جدولی ساخته نمی‌شه، و همون مقدار که توی یک حساب زدم توی
/// همهٔ حساب‌ها سرایت می‌کنه.»
///
/// همان کارِ کاربر، با کلیدِ واقعی روی کادرِ واقعیِ سربرگ: کلیک، تایپ، Enter
/// یا بیرون رفتن — و بعد **خودِ دیتابیس** برای هر حساب خوانده می‌شود، نه عددِ
/// روی صفحه.
///
///     dotnet run --project PumpYaqobi.UiTests -c Release -- headrasid
/// </summary>
internal static class HeadRasidProbe
{
    private static int _bad;

    private static void Check(string what, bool ok, string? detail = null)
    {
        Console.WriteLine((ok ? "  ✔ " : "  ✖ ") + what + (detail is null ? "" : " — " + detail));
        if (!ok) _bad++;
    }

    public static int Run()
    {
        var tmpDb = Path.Combine(Path.GetTempPath(), "pump-headrasid-" + Guid.NewGuid().ToString("N"), "pump.db");
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

        var debt = vm.Sections.OfType<DebtSectionViewModel>().First();
        Wait(win, vm.GoAsync(debt));
        Wait(win, debt.RefreshAsync());
        if (debt.Cards.Count < 3) { Console.WriteLine("✖ دستِ‌کم سه قرض‌دار لازم است"); return 1; }

        // تازه: یک قرض‌دارِ بی هیچ ردیف، همان «جدولی نباشه»
        debt.NewName = "قرض‌دارِ تازهٔ آزمون";
        Wait(win, debt.AddDebtorCommand.ExecuteAsync(null));
        var freshId = debt.Cards.First(c => c.Name == "قرض‌دارِ تازهٔ آزمون").Entity.Id;
        var ids = debt.Cards.Where(c => c.Entity.Id != freshId).Take(3).Select(c => c.Entity.Id).ToList();
        ids.Insert(0, freshId);

        // هر حساب یک بار باز شود تا مهاجرتِ یک‌بارهٔ رسیدهای دادهٔ نمونه تمام شود
        foreach (var id in ids) { OpenCard(win, debt, id); Wait(win, debt.BackCommand.ExecuteAsync(null)); }
        var baseAll = AllAccounts(ids);

        // ── الف) قرض‌دارِ تازه، جدولِ خالی ──────────────────────────────────
        Console.WriteLine("── الف) قرض‌دارِ تازه (هیچ ردیفی) ⇒ رسید از سربرگ ──");
        OpenCard(win, debt, freshId);
        var a = debt.Person!.Current!;
        Check($"جدول خالی است ({a.Rows.Count} ردیف)", a.Rows.Count == 0);
        TypeIntoHead(win, "7000", enter: true);
        Pump(win, 30);
        Check($"ردیفِ رسید در جدول ساخته شد ({a.Rows.Count} ردیف)", a.Rows.Count == 1);
        Check($"سربرگ ۷٬۰۰۰ ({HeadBox(win).Text})", Shamsi.Num(HeadBox(win).Text) == 7000m);
        var now1 = AllAccounts(ids);
        Compare(baseAll, now1, a.Entity.Id, 1, 7000m);
        baseAll = now1;

        // ── ب) عدد با نویسهٔ نامرئیِ صفحه‌کلید (RLM) و رقمِ فارسی ─────────
        Console.WriteLine("── ب) «‏۲۰۰۰» با نویسهٔ نامرئی ⇒ باز هم ردیف ──");
        TypeIntoHead(win, "‏۲۰۰۰", enter: true);
        Pump(win, 30);
        Check($"ردیفِ دوم ساخته شد ({a.Rows.Count} ردیف)", a.Rows.Count == 2);
        Check($"سربرگ ۹٬۰۰۰ ({HeadBox(win).Text})", Shamsi.Num(HeadBox(win).Text) == 9000m);
        var now2 = AllAccounts(ids);
        Compare(baseAll, now2, a.Entity.Id, 1, 2000m);
        baseAll = now2;

        // ── ج) متنِ ناخوانا ⇒ هیچ ردیفی، و کادر به جمعِ دفتر برمی‌گردد ────
        Console.WriteLine("── ج) متنِ ناخوانا ⇒ نه ردیف، نه سرایت ──");
        TypeIntoHead(win, "abc", enter: true);
        Pump(win, 30);
        Check($"ردیفی ساخته نشد ({a.Rows.Count})", a.Rows.Count == 2);
        Check($"کادر همان جمعِ دفتر است ({HeadBox(win).Text})", Shamsi.Num(HeadBox(win).Text) == 9000m);
        Compare(baseAll, AllAccounts(ids), -1, 0, 0m);
        SpreadCheck(win, debt, ids, freshId);

        // ── د) فقط‌خواندنی ⇒ هیچ ردیفِ شبح و هیچ سرایتی ──────────────────
        Console.WriteLine("── د) فقط‌خواندنی (قفلِ نرم) ⇒ نه ردیف، نه سرایت ──");
        var otherId = ids[1];
        OpenCard(win, debt, otherId);
        var o = debt.Person!.Current!;
        var oRows = o.Rows.Count; var oEntity = o.Entity.FuelRows.Count + o.Entity.MoneyRows.Count;
        var oHead = HeadBox(win).Text ?? "";
        var hook = PumpYaqobi.Application.Security.PermissionService.ReadOnlyHook;
        PumpYaqobi.Application.Security.PermissionService.ReadOnlyHook = () => true;
        try { TypeIntoHead(win, "5555", enter: true); Pump(win, 40); }
        finally { PumpYaqobi.Application.Security.PermissionService.ReadOnlyHook = hook; }
        Check($"ردیفی ساخته نشد ({oRows}⇐{o.Rows.Count})", o.Rows.Count == oRows);
        Check($"هیچ ردیفِ شبح در حافظهٔ حساب نماند ({oEntity}⇐{o.Entity.FuelRows.Count + o.Entity.MoneyRows.Count})",
              o.Entity.FuelRows.Count + o.Entity.MoneyRows.Count == oEntity);
        Check($"کادر به جمعِ دفتر برگشت («{oHead}» ⇐ «{HeadBox(win).Text}»)", (HeadBox(win).Text ?? "") == oHead);
        Compare(baseAll, AllAccounts(ids), -1, 0, 0m);
        Wait(win, debt.BackCommand.ExecuteAsync(null));
        SpreadCheck(win, debt, ids, -1);

        // ── ه) حسابِ فرعیِ تازه ───────────────────────────────────────────
        Console.WriteLine("── ه) حسابِ فرعیِ تازه ⇒ رسید از سربرگ ──");
        OpenCard(win, debt, freshId);
        Dialogs.PromptHook = (_, _) => "فرعیِ آزمون";
        try { Wait(win, debt.Person!.AddSubAccountCommand.ExecuteAsync(null)); } finally { Dialogs.PromptHook = null; }
        Pump(win, 20);
        var sub = debt.Person!.Current!;
        baseAll = AllAccounts(ids);
        TypeIntoHead(win, "3000", enter: true);
        Pump(win, 30);
        Check($"ردیف در حسابِ فرعی ساخته شد ({sub.Rows.Count} ردیف)", sub.Rows.Count == 1);
        Compare(baseAll, AllAccounts(ids), sub.Entity.Id, 1, 3000m);
        Wait(win, debt.BackCommand.ExecuteAsync(null));
        SpreadCheck(win, debt, ids, -1);

        Console.WriteLine();
        if (_bad == 0) { Console.WriteLine("✅ رسیدِ سربرگ فقط در همان حساب می‌نشیند و ردیفش ساخته می‌شود"); return 0; }
        Console.WriteLine($"❌ {_bad} ایراد");
        return 1;
    }

    private static Dictionary<long, (int Rows, decimal Rasid, decimal Cache)> AllAccounts(List<long> ids)
    {
        var map = new Dictionary<long, (int, decimal, decimal)>();
        foreach (var id in ids)
        {
            var d = AppHost.Current.Debtors.LoadFullAsync(id).GetAwaiter().GetResult()!;
            foreach (var a in d.AllAccounts())
            {
                var rows = a.FuelRows.Concat(a.MoneyRows).Where(r => r.DeletedAt is null).ToList();
                var c = AppHost.Current.Debt;
                var ok = a.RasidFuelPetrol == c.ReceiptTotal(a, Domain.Enums.LedgerMode.Fuel, Domain.Enums.FuelType.Petrol)
                      && a.RasidFuelDiesel == c.ReceiptTotal(a, Domain.Enums.LedgerMode.Fuel, Domain.Enums.FuelType.Diesel)
                      && a.RasidMoneyPetrol == c.ReceiptTotal(a, Domain.Enums.LedgerMode.Money, Domain.Enums.FuelType.Petrol)
                      && a.RasidMoneyDiesel == c.ReceiptTotal(a, Domain.Enums.LedgerMode.Money, Domain.Enums.FuelType.Diesel);
                map[a.Id] = (rows.Count, rows.Sum(r => r.Rasid + r.RasidFuel), ok ? 1m : 0m);
            }
        }
        return map;
    }

    private static void Compare(Dictionary<long, (int Rows, decimal Rasid, decimal Cache)> was,
                                Dictionary<long, (int Rows, decimal Rasid, decimal Cache)> now,
                                long target, int addRows, decimal addRasid)
    {
        foreach (var (id, w) in was)
        {
            if (!now.TryGetValue(id, out var n)) { Check($"حسابِ {id} هنوز هست", false); continue; }
            var want = id == target ? (w.Rows + addRows, w.Rasid + addRasid) : (w.Rows, w.Rasid);
            // کشِ روی دیسک فقط برای حسابی که رسید گرفت سنجیده می‌شود: حسابِ فرعیِ دادهٔ
            // نمونه که هرگز باز نشده کشِ خودِ دادهٔ نمونه را دارد و به این کار ربطی ندارد.
            var ok = n.Rows == want.Item1 && n.Rasid == want.Item2 && (id != target || n.Cache == 1m);
            Check($"حسابِ {id}{(id == target ? " (همین)" : "")}: {w.Rows}⇐{n.Rows} ردیف · رسید {w.Rasid:0}⇐{n.Rasid:0} · کشِ روی دیسک {(n.Cache == 1m ? "درست" : "کهنه")}", ok);
        }
        foreach (var id in now.Keys.Except(was.Keys))
            if (id != target) Check($"حسابِ تازهٔ {id} ناخواسته", false);
    }

    /// <summary>هر حسابِ هر قرض‌دار: کادرِ سربرگ دقیقاً جمعِ دفترِ خودش — نه عددی از حسابِ دیگر.</summary>
    private static void SpreadCheck(Window win, DebtSectionViewModel debt, List<long> ids, long skip)
    {
        var bad = 0;
        foreach (var id in ids)
        {
            OpenCard(win, debt, id);
            foreach (var acct in debt.Person!.Accounts.ToList())
            {
                debt.Person.Current = acct;
                Pump(win, 10);
                var want = acct.HeadPetrolRasidEdit;
                var got = HeadBox(win).Text ?? "";
                if (got != want) { bad++; Console.WriteLine($"     ✖ حسابِ {acct.Entity.Id}: کادر «{got}» ولی دفتر «{want}»"); }
            }
            Wait(win, debt.BackCommand.ExecuteAsync(null));
        }
        Check($"هیچ عددی به حسابِ دیگری سرایت نکرد ({bad} ناجور)", bad == 0);
    }

    private sealed record Snap(int Rows, decimal Rasid, decimal HeadPetrol)
    {
        public override string ToString() => $"{Rows} ردیف · رسید {Rasid:0}";
    }

    /// <summary>ردیف‌ها و جمعِ رسیدِ حسابِ اصلی — از خودِ دیتابیس.</summary>
    private static Snap Snapshot(long debtorId)
    {
        var d = AppHost.Current.Debtors.LoadFullAsync(debtorId).GetAwaiter().GetResult()!;
        var acct = d.AllAccounts().First();
        var rows = acct.FuelRows.Concat(acct.MoneyRows).Where(r => r.DeletedAt is null).ToList();
        var money = acct.Mode == Domain.Enums.LedgerMode.Money;
        var live = money ? acct.MoneyRows : acct.FuelRows;
        var head = live.Where(r => r.DeletedAt is null && r.Fuel == Domain.Enums.FuelType.Petrol)
                       .Sum(r => money ? r.Rasid : r.RasidFuel);
        return new Snap(rows.Count, rows.Sum(r => r.Rasid + r.RasidFuel), head);
    }

    private static void Dump(long debtorId)
    {
        var d = AppHost.Current.Debtors.LoadFullAsync(debtorId).GetAwaiter().GetResult()!;
        foreach (var a in d.AllAccounts())
        {
            Console.WriteLine($"     acct {a.Id} mode={a.Mode} migrated={a.ReceiptsMigrated} rFP={a.RasidFuelPetrol} rFD={a.RasidFuelDiesel} rMP={a.RasidMoneyPetrol} rMD={a.RasidMoneyDiesel}");
            foreach (var r in a.FuelRows.Concat(a.MoneyRows).Where(r => r.DeletedAt is null))
                Console.WriteLine($"       row {r.Id} fuelAcc={r.FuelAccountId} monAcc={r.MoneyAccountId} fuel={r.Fuel} lit={r.Liters} rasid={r.Rasid} rasidF={r.RasidFuel} date={r.DateShamsi} src={r.SrcKey}");
        }
    }

    private static void OpenCard(Window win, DebtSectionViewModel debt, long id)
    {
        var card = debt.Cards.First(c => c.Entity.Id == id);
        debt.OpenCommand.Execute(card);
        for (var i = 0; i < 200 && !(debt.PersonOpen && debt.Person?.Accounts.FirstOrDefault()?.Entity.MainOfDebtorId == id); i++)
            Pump(win);
        Pump(win, 20);
    }

    private static TextBox HeadBox(Window win) =>
        win.GetVisualDescendants().OfType<PumpYaqobi.App.Views.Sections.PersonView>()
           .First(v => v.IsEffectivelyVisible)
           .GetVisualDescendants().OfType<TextBox>()
           .First(t => t.Classes.Contains("fsv") && t.IsEffectivelyVisible);

    private static void TypeIntoHead(Window win, string text, bool enter)
    {
        var box = HeadBox(win);
        box.Focus();
        Pump(win);
        win.KeyTextInput(text);
        Pump(win);
        if (enter) win.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Pump(win, 20);
    }

    private static void FocusAndLeave(Window win)
    {
        var box = HeadBox(win);
        box.Focus();
        Pump(win);
        win.FocusManager?.ClearFocus();
        Pump(win, 20);
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
