using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.EntityFrameworkCore;
using PumpYaqobi.App.Services;
using PumpYaqobi.App.ViewModels;
using PumpYaqobi.App.ViewModels.Sections;
using PumpYaqobi.App.Views;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;
using PumpYaqobi.Services.Data;

namespace PumpYaqobi.UiTests;

/// <summary>
/// ══ «گزارش‌های پارچه ویرایشی باشند» · «فی لیتر دقیقاً ضربِ لیتر» (۱۴۰۵/۰۷/۱۹) ══
///
/// با پنجره و کلیک و کلیدِ واقعی: یک پارچه ثبت می‌شود، صفحهٔ «گزارش‌ها» باز، «✏️
/// ویرایش» زده، ختم «12960» و فی «60.14» حرف‌به‌حرف نوشته و ذخیره می‌شود. بعد
/// دیسک خوانده می‌شود: پارچه، پایهٔ ورق و فروش همان عدد را دارند و فروش دقیقاً
/// لیتر × فی است (بی گرد کردن).
///
///     dotnet run --project PumpYaqobi.UiTests -c Release -- reportedit
/// </summary>
internal static class ReportEditProbe
{
    private static readonly List<string> Bad = new();

    public static int Run()
    {
        var dir = Path.Combine(Path.GetTempPath(), "pump-repedit-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        AppHost.Start(Path.Combine(dir, "pump.db"));
        FakeLicense.Grant();
        AppBuilder.Configure<PumpYaqobi.App.App>().UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .SetupWithoutStarting();
        var win = new MainWindow { Width = 1440, Height = 900 };
        win.Show();
        Round14Probe.Settle(win);
        var vm = (MainViewModel)win.DataContext!;
        LockIn.Wait(vm.Lock);
        Round14Probe.Settle(win);
        var h = AppHost.Current;

        var today = PumpYaqobi.Application.Localization.Shamsi.Today();
        var saved = h.ParchaData.SaveShiftFlowAsync(new ShiftSaveRequest(
            FuelType.Petrol, ShiftKind.Day, today, "احمد", 1, 10000m, 11000m, 55m, 0m, 0m, 0m, "", 40m, true))
            .GetAwaiter().GetResult();
        Check("پارچه ثبت شد", saved.Ok);

        var pa = (ParchaSectionViewModel)vm.Sections.First(s => s.Id == "shifts");
        Round14Probe.Wait(win, vm.GoAsync(pa));
        pa.OpenReportsCommand.Execute(null);
        for (var i = 0; i < 100 && pa.Reports.Count == 0; i++) Round14Probe.Settle(win);
        var card = pa.Reports.FirstOrDefault();
        Check("گزارش در فهرست است", card is not null);
        if (card is null) return Done();
        pa.ShowReportCommand.Execute(card);
        Round14Probe.Settle(win);

        var edit = Find<Button>(win, b => (b.Content as string)?.Contains("ویرایشِ این شیفت") == true);
        Check("دکمهٔ «✏️ ویرایش» دیده می‌شود", edit is not null);
        if (edit is null) return Done();
        Click(win, edit);

        var boxes = win.GetVisualDescendants().OfType<TextBox>()
            .Where(b => b.Classes.Contains("rsedit") && b.IsEffectivelyVisible).ToList();
        Check($"شش کادرِ ویرایش ({boxes.Count})", boxes.Count == 6);
        if (boxes.Count != 6) return Done();
        TypeInto(win, boxes[3], "12960");     // ختم
        TypeInto(win, boxes[4], "60.14");     // فی لیتر
        var save = Find<Button>(win, b => (b.Content as string)?.Contains("ذخیرهٔ ویرایش") == true);
        Check("دکمهٔ ذخیره", save is not null);
        if (save is null) return Done();
        Click(win, save);
        for (var i = 0; i < 200; i++) { Round14Probe.Settle(win); if (save.IsEffectivelyVisible == false) break; }

        using (var db = h.Db.Create())
        {
            var rep = db.Reports.AsNoTracking().Include(r => r.DayShift).Single(r => r.Id == saved.Report!.Id);
            var sh = rep.DayShift!;
            Check($"پارچه: ختم «{sh.End}» و فی «{sh.Price}»", sh.End == 12960m && sh.Price == 60.14m);
            var liters = sh.End - sh.Start;
            Check($"لیترِ فروش {sh.Sale} = ختم − شروع", sh.Sale == liters);
            Check($"پول = لیتر × فی دقیقاً ({liters} × 60.14 = {liters * 60.14m} ⇐ {sh.Money})", sh.Money == liters * 60.14m);
            var pump = db.Set<WaraqPump>().AsNoTracking().FirstOrDefault(p => p.SrcKey != null && p.SrcKey.Contains(saved.Report.Id.ToString()));
            Check($"پایهٔ ورق همان را گرفت (ختم {pump?.End} · فی {pump?.PricePerLiter})",
                  pump is not null && pump.End == 12960m && pump.PricePerLiter == 60.14m);
        }
        Check("صفحهٔ گزارش همان عدد را نشان می‌دهد",
              pa.OpenReport?.Shifts[0].Items.Any(l => l.Value == "12,960") == true);

        // ══ فاکتورِ قرض‌دار از خودِ حسابش: دیدن، تایید، برگشت — یک رکورد ══
        Console.WriteLine("── فاکتور از حسابِ قرض‌دار ──");
        var debtor = h.Debtors.AddDebtorAsync("کریم احمدی", null, false).GetAwaiter().GetResult();
        var invRec = h.Invoices.AddAsync(new Invoice
        {
            InvoiceNumber = 7, DateShamsi = today, CustomerName = "کریم احمدی",
            Fuel = FuelType.Petrol, PricePerLiter = 60.14m, Liters = 100m,
        }).GetAwaiter().GetResult();
        var debt = (DebtSectionViewModel)vm.Sections.First(s => s.Id == "debt");
        Round14Probe.Wait(win, vm.GoAsync(debt));
        Round14Probe.Wait(win, debt.OpenPersonAsync(debtor.Id));
        var acct = debt.Person?.Current;
        if (acct is not null) Round14Probe.Wait(win, acct.LoadInvoicesAsync());
        Check($"فاکتور در حسابِ همان قرض‌دار ({acct?.Invoices.Count})", acct?.Invoices.Count == 1);
        if (acct is null || acct.Invoices.Count != 1) return Done();
        acct.IsInvoicesOpen = true;
        Round14Probe.Settle(win);
        var row = Find<Button>(win, b => b.Classes.Contains("acct-inv"));
        Check("ردیفِ فاکتور در حساب کلیک‌شدنی است", row is not null);
        if (row is null) return Done();
        Click(win, row);
        for (var i = 0; i < 60; i++) Round14Probe.Settle(win);
        var inv = (InvoiceSectionViewModel)vm.Sections.First(s => s.Id == "invoices");
        Check($"همان فاکتور باز شد (بخش: {vm.Current?.Id} · فاکتور {inv.Detail?.Entity.Id})",
              ReferenceEquals(vm.Current, inv) && inv.IsDetail && inv.Detail?.Entity.Id == invRec.Id);
        if (inv.Detail is { } d) Round14Probe.Wait(win, inv.ApproveCommand.ExecuteAsync(d));
        Check("تایید شد", inv.Detail?.IsApproved == true);
        inv.BackCommand.Execute(null);
        for (var i = 0; i < 60; i++) Round14Probe.Settle(win);
        Check($"«‹ برگشت» به همان حساب (بخش: {vm.Current?.Id})", ReferenceEquals(vm.Current, debt) && debt.PersonOpen);
        Check("فهرستِ حساب «تایید شده» می‌گوید", debt.Person?.Current?.Invoices.FirstOrDefault()?.Approved == true);
        using (var db = h.Db.Create())
            Check("یک رکورد، نه دو", db.Set<Invoice>().AsNoTracking().Count() == 1);
        return Done();
    }

    private static int Done()
    {
        Console.WriteLine();
        if (Bad.Count == 0) { Console.WriteLine("✅ گزارشِ پارچه ویرایش شد و همه‌جا همان عدد نشست"); return 0; }
        Console.WriteLine($"❌ {Bad.Count} ایراد"); return 1;
    }

    private static T? Find<T>(Window w, Func<T, bool> ok) where T : Control =>
        w.GetVisualDescendants().OfType<T>().FirstOrDefault(c => c.IsEffectivelyVisible && ok(c));

    private static void Click(Window w, Control c)
    {
        c.BringIntoView();
        Round14Probe.Settle(w);
        var p = c.TranslatePoint(new Point(c.Bounds.Width / 2, c.Bounds.Height / 2), w);
        if (p is null) return;
        w.MouseDown(p.Value, MouseButton.Left);
        w.MouseUp(p.Value, MouseButton.Left);
        Round14Probe.Settle(w);
    }

    private static void TypeInto(Window w, TextBox b, string text)
    {
        Click(w, b);
        b.SelectAll();
        foreach (var ch in text)
        {
            var phys = ch is >= '0' and <= '9' ? PhysicalKey.Digit0 + (ch - '0') : ch == '.' ? PhysicalKey.Period : PhysicalKey.None;
            if (phys != PhysicalKey.None) w.KeyPressQwerty(phys, RawInputModifiers.None);
            w.KeyTextInput(ch.ToString());
            if (phys != PhysicalKey.None) w.KeyReleaseQwerty(phys, RawInputModifiers.None);
        }
        Round14Probe.Settle(w);
        Check($"کادر «{text}» را دارد («{b.Text}»)", b.Text == text);
    }

    private static void Check(string what, bool ok)
    {
        Console.WriteLine($"  {(ok ? "✔" : "✖")} {what}");
        if (!ok) Bad.Add(what);
    }
}
