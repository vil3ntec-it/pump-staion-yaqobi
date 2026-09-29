using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.EntityFrameworkCore;
using PumpYaqobi.App.Controls;
using PumpYaqobi.App.Services;
using PumpYaqobi.App.ViewModels;
using PumpYaqobi.App.ViewModels.Sections;
using PumpYaqobi.App.Views;
using PumpYaqobi.Application.Localization;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;
using PumpYaqobi.Services.Data;

namespace PumpYaqobi.UiTests;

/// <summary>
/// ══ فهرستِ ۱۴۰۵/۰۷/۱۷ (دوم) — با کلید و کلیکِ واقعی ══════════════════════════
///
///   ۵) شمارهٔ تماسِ حسابِ فرعی جدا از حسابِ اصلی
///   ۶) «جدول جدید» پس از «نه» دوباره می‌پرسد · جستجوی خرید با سربرگِ راهنما
///      و تاریخ/تن/شرکتِ هر نتیجه
///   ۷) فرمِ خرید: «0730» ⇐ «0.730» و «پول کل» ته قاب و پیدا
///
///     dotnet run --project PumpYaqobi.UiTests -c Release -- owner17b
/// </summary>
internal static class Owner17bProbe
{
    private static readonly List<string> Bad = new();

    public static int Run()
    {
        var tmpDb = Path.Combine(Path.GetTempPath(), "pump-o17b-" + Guid.NewGuid().ToString("N"), "pump.db");
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
        Settle(win);
        var h = AppHost.Current;
        Seed(h.Db);

        Phones(win, vm, h);
        Companies(win, vm);
        BuyForm(win, vm);

        Console.WriteLine();
        if (Bad.Count == 0) { Console.WriteLine("✅ حسابِ فرعی، «جدول جدید»، جستجوی خرید و فرمِ خرید — همه با کلیدِ واقعی"); return 0; }
        Console.WriteLine($"❌ {Bad.Count} ایراد:");
        foreach (var b in Bad) Console.WriteLine("   • " + b);
        return 1;
    }

    private static void Seed(PumpDbFactory dbf)
    {
        using var db = dbf.Create();
        var p = new Debtor { Name = "هارون", LegacyId = "o17b", Phone = "0700111222" };
        p.MainAccount.Name = "هارون";
        p.SubAccounts.Add(new DebtAccount { Name = "دکان", LegacySubId = "s17b" });
        db.Debtors.Add(p);
        var co = new TilCompany { Name = "شرکتِ پارس" };
        co.Rows.Add(new CompanyRow { Fuel = FuelType.Petrol, DateShamsi = "1405/5/12", Name = "راننده", Ton = 3m, Usd = 1000m, Rate = 70m });
        db.TilCompanies.Add(co);
        db.FuelPurchases.Add(new FuelPurchase { Fuel = FuelType.Petrol, Seller = "شرکتِ پارس", DateShamsi = "1405/6/2",
                                                Kg = 12300m, Ton = 12.3m, Density = 0.73m, LegacyId = "fe17b" });
        db.SaveChanges();
    }

    // ══ ۵) شمارهٔ تماسِ هر حساب ═════════════════════════════════════════════

    private static void Phones(Window win, MainViewModel vm, AppHost h)
    {
        Console.WriteLine();
        Console.WriteLine("── ۵) حسابِ اصلی و فرعی: شماره و «فیِ خرید» جدا ──");
        var debt = (DebtSectionViewModel)vm.Sections.First(s => s.Id == "debt");
        Wait(win, vm.GoAsync(debt));
        Settle(win);
        var card = debt.Cards.FirstOrDefault(c => c.Name == "هارون");
        if (card is null) { Fail("کارتِ هارون پیدا نشد"); return; }
        debt.OpenCommand.Execute(card);
        Settle(win);
        var person = debt.Person;
        if (person is null) { Fail("حسابِ هارون باز نشد"); return; }

        var phoneBox = win.GetVisualDescendants().OfType<TextBox>()
                          .FirstOrDefault(t => t.IsEffectivelyVisible && t.Watermark as string == "0700000000");
        if (phoneBox is null) { Fail("کادرِ شماره تماس پیدا نشد"); return; }
        Check($"حسابِ اصلی شمارهٔ شخص را نشان می‌دهد («{phoneBox.Text}»)", phoneBox.Text == "0700111222");

        var sub = person.Accounts.FirstOrDefault(a => !a.Entity.IsMain);
        if (sub is null) { Fail("حسابِ فرعی نیست"); return; }
        person.Current = sub;
        Settle(win);
        Check($"حسابِ فرعی شمارهٔ اصلی را ندارد («{phoneBox.Text}»)", string.IsNullOrEmpty(phoneBox.Text));

        phoneBox.Focus();
        win.KeyTextInput("0799888777");
        Settle(win);
        Wait(win, SaveGuard.FlushAllAsync());
        Settle(win);

        person.Current = person.Accounts.First(a => a.Entity.IsMain);
        Settle(win);
        Check($"برگشت به حسابِ اصلی ⇐ همان شمارهٔ خودش («{phoneBox.Text}»)", phoneBox.Text == "0700111222");
        person.Current = sub;
        Settle(win);
        Check($"و حسابِ فرعی شمارهٔ خودش را نگه داشت («{phoneBox.Text}»)", phoneBox.Text == "0799888777");

        using var db = h.Db.Create();
        var d = db.Debtors.AsNoTracking().Single(x => x.Name == "هارون");
        var sa = db.DebtAccounts.AsNoTracking().Single(x => x.DebtorId == d.Id);
        Check($"روی دیسک: شخص «{d.Phone}» و فرعی «{sa.Phone}»", d.Phone == "0700111222" && sa.Phone == "0799888777");
        debt.BackCommand.Execute(null);
        Settle(win);
    }

    // ══ ۶) شرکت‌ها ══════════════════════════════════════════════════════════

    private static void Companies(Window win, MainViewModel vm)
    {
        Console.WriteLine();
        Console.WriteLine("── ۶) شرکت‌ها: «جدول جدید» پس از «نه» · جستجوی خرید ──");
        var cs = (CompanySectionViewModel)vm.Sections.First(s => s.Id == "noinv");
        Wait(win, vm.GoAsync(cs));
        Settle(win);
        var card = cs.Cards.FirstOrDefault(c => c.Name == "شرکتِ پارس");
        if (card is null) { Fail("کارتِ «شرکتِ پارس» پیدا نشد"); return; }
        cs.OpenCommand.Execute(card);
        Settle(win);
        var page = cs.Page;
        if (page is null) { Fail("حسابِ شرکت باز نشد"); return; }
        if (page.IsDiesel) { page.IsDiesel = false; Settle(win); }

        var asks = 0;
        Dialogs.ConfirmHook = (_, _) => { asks++; return false; };
        try
        {
            var combo = win.GetVisualDescendants().OfType<ComboBox>()
                           .FirstOrDefault(c => c.IsEffectivelyVisible && ReferenceEquals(c.ItemsSource, page.Actions));
            if (combo is null) { Fail("کشوییِ «کارها» پیدا نشد"); return; }
            var newItem = page.Actions.First(a => a.Value == "new");
            for (var round = 1; round <= 3; round++)
            {
                combo.SelectedItem = newItem;          // همان کاری که کلیک روی گزینه می‌کند
                Settle(win);
                Check($"بارِ {round}: «جدول جدید» پرسید ({asks} پرسش)", asks == round);
                Check($"بارِ {round}: کشویی دوباره «☰ کارها» شد", combo.SelectedIndex == 0);
            }
        }
        finally { Dialogs.ConfirmHook = null; }

        //  ── جستجوی خرید: کلیک روی 🔍 با کادرِ خالی ⇐ صفحه با سربرگِ راهنما ──
        page.FindText = "";
        var find = win.GetVisualDescendants().OfType<Button>()
                      .FirstOrDefault(b => b.IsEffectivelyVisible && ReferenceEquals(b.Command, page.FindCommand));
        if (find is null) { Fail("دکمهٔ 🔍 پیدا نشد"); return; }
        Click(win, find);
        Settle(win);
        var sp = cs.Overlay as CompanySearchPageViewModel;
        Check("کلیکِ 🔍 ⇐ صفحهٔ جستجو باز شد", sp is not null);
        if (sp is null) return;
        var guide = win.GetVisualDescendants().OfType<Border>().FirstOrDefault(b => b.Name == "SearchGuide");
        Check("سربرگِ راهنما دیده می‌شود و می‌گوید چه بنویسد",
              guide is { IsEffectivelyVisible: true } && sp.GuideText.Contains("کیلو") && sp.GuideText.Contains("تاریخ"));
        var qty = win.GetVisualDescendants().OfType<TextBox>().FirstOrDefault(t => t.Name == "QtyBox");
        Check("کادرِ مقدار آمادهٔ تایپ است", qty is not null && ReferenceEquals(win.FocusManager?.GetFocusedElement(), qty));
        qty?.Focus();
        win.KeyTextInput("12300");
        Tap(win, PhysicalKey.Enter);
        Settle(win);
        var hit = sp.Results.FirstOrDefault();
        Check($"«12300» ⇐ {sp.Results.Count} نتیجه", sp.Results.Count == 1);
        if (hit is not null)
            Check($"نتیجه می‌گوید کی، چقدر و کدام شرکت: {hit.DateText} · {hit.TonText} · {hit.CompanyText}",
                  hit.DateText.Contains("1405/6/2") && hit.TonText.Contains("12.3") && hit.TonText.Contains("12,300 کیلو")
                  && hit.CompanyText.Contains("شرکتِ پارس"));

        //  تاریخ دقیق است: «1405/6/2» فقط همان روز، نه «1405/6/2x»
        sp.QtyText = ""; sp.DateText = "1405/5";
        Wait(win, sp.RunCommand.ExecuteAsync(null));
        Settle(win);
        var r = sp.Results.FirstOrDefault();
        Check($"«1405/5» ⇐ فقط ردیفِ همان ماه، با نامِ شرکت ({sp.Results.Count} · {r?.CompanyText})",
              sp.Results.Count == 1 && r!.CompanyText.Contains("شرکتِ پارس") && r.Name == "راننده");
        cs.CloseOverlay();
        Settle(win);
        cs.BackCommand.Execute(null);
        Settle(win);
    }

    // ══ ۷) فرمِ خرید ═══════════════════════════════════════════════════════

    private static void BuyForm(Window win, MainViewModel vm)
    {
        Console.WriteLine();
        Console.WriteLine("── ۷) فرمِ خرید: «0730» ⇐ «0.730» · پول کل ──");
        var st = (StorageSectionViewModel)vm.Sections.First(s => s.Id == "storage");
        Wait(win, vm.GoAsync(st));
        Settle(win);
        st.OpenBuyCommand.Execute(null);
        Settle(win);
        var box = win.GetVisualDescendants().OfType<TextBox>().FirstOrDefault(t => t.Name == "BuyDensityBox");
        if (box is null) { Fail("کادرِ ثقلت پیدا نشد"); return; }
        box.Focus();
        foreach (var ch in "0730") { win.KeyTextInput(ch.ToString()); Pump(win); }
        Settle(win);
        Check($"تایپِ «0730» ⇐ کادر «{box.Text}» و مکان‌نما ته متن", box.Text == "0.730" && box.CaretIndex == box.Text!.Length);
        Check($"ویومدل همان را دارد («{st.BuyDensity}»)", st.BuyDensity == "0.730");
        st.BuyKg = "1000"; st.BuyPriceTon = "1200"; st.BuyUsdRate = "66";
        Settle(win);
        Check($"لیتر با ۰٫۷۳۰ حساب شد ({st.BuyLitersText})", st.BuyLitersText == "1,370 لیتر");
        foreach (var name in new[] { "BuyUsdBox", "BuyAfnBox" })
        {
            var b = win.GetVisualDescendants().OfType<Border>().FirstOrDefault(x => x.Name == name);
            var p = b?.TranslatePoint(new Point(0, b.Bounds.Height), win);
            Check($"«{name}» ته قاب دیده می‌شود (پایینش {p?.Y:0} از {win.Bounds.Height:0})",
                  b is { IsEffectivelyVisible: true } && p is { } q && q.Y <= win.Bounds.Height);
        }
        var usd = win.GetVisualDescendants().OfType<TextBlock>().FirstOrDefault(t => t.Text == st.BuyUsdText && t.IsEffectivelyVisible);
        Check($"«پول کل» درشت است (قلم {usd?.FontSize})", usd is { FontSize: >= 20 });
        st.CancelBuyCommand.Execute(null);
        Settle(win);
    }

    // ── ابزار ─────────────────────────────────────────────────────────────

    private static void Click(Window win, Control c)
    {
        var p = c.TranslatePoint(new Point(c.Bounds.Width / 2, c.Bounds.Height / 2), win);
        if (p is null) { Fail("جای دکمه معلوم نیست"); return; }
        win.MouseDown(p.Value, MouseButton.Left);
        win.MouseUp(p.Value, MouseButton.Left);
        Settle(win);
    }

    private static void Tap(Window win, PhysicalKey key)
    {
        win.KeyPressQwerty(key, RawInputModifiers.None);
        win.KeyReleaseQwerty(key, RawInputModifiers.None);
    }

    private static void Check(string what, bool ok)
    {
        Console.WriteLine($"  {(ok ? "✔" : "✖")} {what}");
        if (!ok) Bad.Add(what);
    }

    private static void Fail(string what) { Console.WriteLine("  ✖ " + what); Bad.Add(what); }

    private static void Settle(Window w)
    {
        var end = DateTime.UtcNow + TimeSpan.FromMilliseconds(700);
        while (DateTime.UtcNow < end) { Dispatcher.UIThread.RunJobs(); w.UpdateLayout(); Thread.Sleep(10); }
        Pump(w);
    }

    private static void Wait(Window w, Task t)
    {
        var end = DateTime.UtcNow + TimeSpan.FromSeconds(30);
        while (!t.IsCompleted && DateTime.UtcNow < end)
        { Dispatcher.UIThread.RunJobs(); w.UpdateLayout(); Thread.Sleep(5); }
        Pump(w);
    }

    private static void Pump(Window w)
    { for (var i = 0; i < 8; i++) { Dispatcher.UIThread.RunJobs(); w.UpdateLayout(); } }
}
