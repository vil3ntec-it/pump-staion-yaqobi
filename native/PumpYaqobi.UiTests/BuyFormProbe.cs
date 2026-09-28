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
using PumpYaqobi.Application.Localization;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;

namespace PumpYaqobi.UiTests;

/// <summary>
/// ══ فرمِ «ثبت خرید»ِ مخزن — ۱۴۰۵/۰۷/۱۶ ═════════════════════════════════════
///
/// گزارشِ صاحب ریپو با عکس: «کلیدهای چپ و راست و بالا که می‌زنم از داخلِ اون
/// کادر میره بیرون… یادداشتش نیمه دیده میشه… اگه نرخِ دالر یا قیمتِ هر تن رو
/// نزد اجباری نباشه… بعد از ثبت هم بشه ویرایشش کرد… یک قلم بزار».
///
/// همه با کلید و کلیکِ <b>واقعی</b>، روی دو اندازهٔ پنجره:
///   ۱) فرم کامل جلوی چشم، زیرِ نوارِ بخش‌ها، و «یادداشت» کامل دیده می‌شود
///   ۲) ↑ ↓ ← → و Tab هرگز از فرم بیرون نمی‌روند و صفحه را نمی‌لغزانند
///   ۳) بی قیمت و نرخ ثبت می‌شود — در حسابِ شرکت هم — و فی‌لیترِ خرید صفر نمی‌شود
///   ۴) ✏️ روی کارت همان فرم را پر باز می‌کند؛ قیمت و نرخ ⇒ خرید و حسابِ شرکت هم
///
///     dotnet run --project PumpYaqobi.UiTests -c Release -- buyform [پوشهٔ عکس]
/// </summary>
internal static class BuyFormProbe
{
    private static int _bad;

    private static void Check(string what, bool ok, string? detail = null)
    {
        Console.WriteLine((ok ? "  ✔ " : "  ✖ ") + what + (detail is null ? "" : " — " + detail));
        if (!ok) _bad++;
    }

    public static int Run(string[] args)
    {
        var shots = args.Length > 1 ? args[1] : Path.Combine(Path.GetTempPath(), "pump-buyform");
        Directory.CreateDirectory(shots);
        var dir = Path.Combine(Path.GetTempPath(), "pump-buyform-" + Guid.NewGuid().ToString("N"));
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

        var h = AppHost.Current;
        for (var i = 0; i < 20; i++)
            h.StorageData.AddPurchaseAsync(new FuelPurchase
            {
                Fuel = FuelType.Petrol, DateShamsi = "1404/06/" + (i % 28 + 1).ToString("00"),
                Seller = "", Kg = 5000m + i, Density = 0.74m, PriceTon = 900m, UsdRate = 70m, Note = "",
            }).GetAwaiter().GetResult();
        var perLiterBefore = h.Settings.GetDecimal(PumpYaqobi.Services.Data.SettingsService.BuyPerLiterPetrol);

        var st = (StorageSectionViewModel)vm.Sections.First(s => s.Id == "storage");
        Wait(win, vm.GoAsync(st));

        foreach (var (w, ht) in new[] { (1440.0, 900.0), (1366.0, 700.0) })
        {
            win.Width = w; win.Height = ht;
            Settle(win);
            Console.WriteLine();
            Console.WriteLine($"════ پنجرهٔ {w}×{ht} ════");
            var sv = win.GetVisualDescendants().OfType<ScrollViewer>().First(s => s.Name == "PageScroll");
            foreach (var at in new[] { "بالای صفحه", "تهِ صفحه" })
            {
                sv.Offset = new Vector(0, at == "بالای صفحه" ? 0 : sv.Extent.Height);
                Settle(win);
                st.OpenBuyCommand.Execute(null);
                Settle(win);
                Fits(win, $"{w}×{ht} · {at}");
                if (at == "تهِ صفحه") Shot(win, shots, $"buy-{w}x{ht}");
                Arrows(win, sv, $"{w}×{ht} · {at}");
                st.CancelBuyCommand.Execute(null);
                Settle(win);
            }
        }
        win.Width = 1440; win.Height = 900;
        Settle(win);

        // ── ۳) بی قیمت و نرخ ────────────────────────────────────────────────
        Console.WriteLine();
        Console.WriteLine("════ ۳) ثبت بی قیمت و نرخ ════");
        const string seller = "شرکت آزمون قلم";
        st.OpenBuyCommand.Execute(null);
        Settle(win);
        st.BuySeller = seller; st.BuyKg = "6000"; st.BuyDensity = "0.75";
        st.BuyPriceTon = ""; st.BuyUsdRate = ""; st.BuyNote = "یادداشتِ نسبتاً بلند برای دیدنِ کاملِ کادر";
        Wait(win, st.SaveBuyCommand.ExecuteAsync(null));
        Check("فرم بسته شد — یعنی ثبت شد", !st.BuyOpen);
        var saved = Db(db => db.FuelPurchases.AsNoTracking().OrderByDescending(p => p.Id).First());
        Check("خریدِ تازه بی قیمت و نرخ روی دیسک است",
              saved.Seller == seller && saved.Kg == 6000m && saved.PriceTon == 0m && saved.UsdRate == 0m,
              $"فروشنده {saved.Seller} · کیلو {saved.Kg} · قیمت {saved.PriceTon} · نرخ {saved.UsdRate}");
        var coRow = Db(db => db.CompanyRows.AsNoTracking().FirstOrDefault(r => r.SourcePurchaseId == saved.LegacyId));
        Check("ردیفِ حسابِ شرکت هم ساخته شد", coRow is not null && coRow.Ton == 6m,
              coRow is null ? "نیست" : $"تن {coRow.Ton} · دالر {coRow.Usd} · نرخ {coRow.Rate}");
        var perLiterAfter = h.Settings.GetDecimal(PumpYaqobi.Services.Data.SettingsService.BuyPerLiterPetrol);
        Check("فی‌لیترِ خرید صفر نشد (خریدِ بی‌قیمت)", perLiterAfter == perLiterBefore && perLiterAfter > 0m,
              $"پیش {perLiterBefore} · پس {perLiterAfter}");
        var card = st.PurchaseCards.FirstOrDefault(c => c.Entity.Id == saved.Id);
        Check("کارتِ این خرید «قیمت/نرخ هنوز نوشته نشده» می‌گوید", card is { MissingPrice: true });
        var count = st.Purchases.Count;

        // ── ۴) ✏️ ───────────────────────────────────────────────────────────
        Console.WriteLine();
        Console.WriteLine("════ ۴) ✏️ ویرایش پس از ثبت ════");
        var sv2 = win.GetVisualDescendants().OfType<ScrollViewer>().First(s => s.Name == "PageScroll");
        sv2.Offset = new Vector(0, 0);
        Settle(win);
        var pen = win.GetVisualDescendants().OfType<Button>()
                     .Where(b => b.IsEffectivelyVisible && (b.Content as string) == "✏️")
                     .FirstOrDefault(b => ReferenceEquals(b.DataContext, card));
        if (pen is not null)
        {
            pen.BringIntoView();
            Settle(win);
            var pt = pen.TranslatePoint(new Point(pen.Bounds.Width / 2, pen.Bounds.Height / 2), win)!.Value;
            win.MouseDown(pt, MouseButton.Left);
            win.MouseUp(pt, MouseButton.Left);
            Settle(win);
        }
        Check("قلمِ ✏️ روی کارت هست و با کلیک فرم باز می‌شود", pen is not null && st.BuyOpen && st.BuyEditing);
        Check("فرم پر از عددهای همان خرید است",
              st.BuySeller == seller && st.BuyKg == "6000" && st.BuyDensity == "0.75"
              && st.BuyPriceTon == "" && st.BuyUsdRate == "" && st.BuyTitle.Contains("ویرایش"),
              $"«{st.BuyTitle}» · {st.BuySeller} · {st.BuyKg} · {st.BuyDensity} · «{st.BuyPriceTon}» · «{st.BuyUsdRate}»");
        Fits(win, "ویرایش");
        Shot(win, shots, "buy-edit");
        st.BuyPriceTon = "1000"; st.BuyUsdRate = "70";
        Wait(win, st.SaveBuyCommand.ExecuteAsync(null));
        var after = Db(db => db.FuelPurchases.AsNoTracking().First(p => p.Id == saved.Id));
        Check("خرید با قیمت و نرخ به‌روز شد — خریدِ تازه‌ای ساخته نشد",
              after.PriceTon == 1000m && after.UsdRate == 70m && st.Purchases.Count == count
              && Db(db => db.FuelPurchases.Count()) == count,
              $"قیمت {after.PriceTon} · نرخ {after.UsdRate} · شمار {st.Purchases.Count}/{count}");
        var coAfter = Db(db => db.CompanyRows.AsNoTracking().Where(r => r.SourcePurchaseId == saved.LegacyId).ToList());
        Check("ردیفِ حسابِ شرکت همان لحظه قیمت و نرخ گرفت — یک ردیف، نه دو",
              coAfter.Count == 1 && coAfter[0].Usd == 1000m && coAfter[0].Rate == 70m,
              string.Join("، ", coAfter.Select(r => $"دالر {r.Usd} نرخ {r.Rate}")));
        Check("کارت دیگر «هنوز نوشته نشده» نمی‌گوید و عددهایش تازه است",
              card is { MissingPrice: false } && card.TotalUsdText.Replace(",", "").Replace("٬", "") .StartsWith("6000"),
              card?.TotalUsdText);
        var perLiterEdit = h.Settings.GetDecimal(PumpYaqobi.Services.Data.SettingsService.BuyPerLiterPetrol);
        Check("قیمتِ تازه‌ترین خرید ⇒ فی‌لیترِ خرید تازه", perLiterEdit == after.PerLiter && after.PerLiter > 0m,
              $"{perLiterEdit} · {after.PerLiter}");

        // خریدِ بی‌فروشنده ⇒ فروشنده بعداً ⇒ همان لحظه در حسابِ شرکت
        st.OpenBuyCommand.Execute(null);
        Settle(win);
        st.BuyKg = "4000"; st.BuyDensity = "0.8";
        Wait(win, st.SaveBuyCommand.ExecuteAsync(null));
        var noSeller = Db(db => db.FuelPurchases.AsNoTracking().OrderByDescending(p => p.Id).First());
        var row2 = st.Purchases.First(c => c.Entity.Id == noSeller.Id);
        st.EditPurchaseCommand.Execute(row2);
        Settle(win);
        st.BuySeller = "شرکت دیرآمده";
        Wait(win, st.SaveBuyCommand.ExecuteAsync(null));
        var late = Db(db => db.CompanyRows.AsNoTracking().Count(r => r.SourcePurchaseId == noSeller.LegacyId));
        Check("فروشنده‌ای که بعداً نوشته شد ⇒ همان لحظه در حسابِ شرکت", late == 1, $"{late} ردیف");

        // بی وزن یا ثقلت ⇒ هنوز ثبت نمی‌شود (موجودیِ مخزن از همین دو است)
        st.OpenBuyCommand.Execute(null);
        Settle(win);
        st.BuyKg = "5000"; st.BuyDensity = "";
        var n0 = Db(db => db.FuelPurchases.Count());
        Wait(win, st.SaveBuyCommand.ExecuteAsync(null));
        Check("بی ثقلت ثبت نمی‌شود و فرم باز می‌ماند", st.BuyOpen && Db(db => db.FuelPurchases.Count()) == n0);
        st.CancelBuyCommand.Execute(null);
        Settle(win);

        Console.WriteLine();
        Console.WriteLine(_bad == 0 ? "✅ فرمِ خرید سالم است" : $"❌ {_bad} ایراد");
        return _bad == 0 ? 0 : 1;
    }

    /// <summary>قاب کامل در پنجره، زیرِ نوارِ بخش‌ها؛ و «یادداشت» کامل داخلِ قاب.</summary>
    private static void Fits(MainWindow win, string at)
    {
        var card = win.GetVisualDescendants().OfType<Border>().First(b => b.Name == "BuyCard");
        var nav = win.GetVisualDescendants().OfType<Border>().FirstOrDefault(b => b.Name == "NavBar");
        var navBottom = nav is { IsVisible: true } ? nav.TranslatePoint(new Point(0, nav.Bounds.Height), win)!.Value.Y : 0;
        var top = card.TranslatePoint(default, win)!.Value.Y;
        var bottom = top + card.Bounds.Height;
        Check($"{at}: فرمِ خرید کامل جلوی چشم است و زیرِ نوارِ بخش‌ها نمی‌رود",
              top >= navBottom - 1 && bottom <= win.ClientSize.Height + 1,
              $"بالا {top:0} · نوار تا {navBottom:0} · پایین {bottom:0} · پنجره {win.ClientSize.Height:0}");

        //  «یادداشت» — همان لحظهٔ باز شدن، بی لغزاندنِ قاب، کامل دیده شود
        var inner = card.GetVisualDescendants().OfType<ScrollViewer>().First();
        var note = card.GetVisualDescendants().OfType<TextBox>()
                       .First(t => t.Watermark as string == "اختیاری");
        var nTop = note.TranslatePoint(default, inner)!.Value.Y;
        var nBottom = nTop + note.Bounds.Height;
        Check($"{at}: کادرِ «یادداشت» کامل دیده می‌شود",
              nTop >= -1 && nBottom <= inner.Viewport.Height + 1 && note.Bounds.Height >= 30,
              $"{nTop:0}…{nBottom:0} در دیدِ {inner.Viewport.Height:0}");
    }

    /// <summary>↑ ↓ ← → و Tab: نشانگر همیشه داخلِ قاب، و صفحهٔ زیر تکان نمی‌خورد.</summary>
    private static void Arrows(MainWindow win, ScrollViewer page, string at)
    {
        var card = win.GetVisualDescendants().OfType<Border>().First(b => b.Name == "BuyCard");
        var first = card.GetVisualDescendants().OfType<TextBox>().First(t => t.IsEffectivelyVisible);
        first.Focus();
        Settle(win);
        var offset = page.Offset;
        var cardTop = card.TranslatePoint(default, win)!.Value.Y;
        var keys = new[]
        {
            PhysicalKey.ArrowUp, PhysicalKey.ArrowUp, PhysicalKey.ArrowLeft, PhysicalKey.ArrowRight,
            PhysicalKey.ArrowRight, PhysicalKey.ArrowLeft, PhysicalKey.ArrowDown, PhysicalKey.ArrowDown,
            PhysicalKey.ArrowDown, PhysicalKey.ArrowDown, PhysicalKey.ArrowDown, PhysicalKey.ArrowDown,
            PhysicalKey.ArrowLeft, PhysicalKey.ArrowRight, PhysicalKey.ArrowUp, PhysicalKey.ArrowUp,
            PhysicalKey.ArrowUp, PhysicalKey.ArrowUp, PhysicalKey.ArrowUp, PhysicalKey.ArrowUp,
            PhysicalKey.PageDown, PhysicalKey.PageUp, PhysicalKey.End, PhysicalKey.Home,
        };
        var outside = new List<string>();
        var visited = new HashSet<Control>();
        foreach (var k in keys)
        {
            win.KeyPressQwerty(k, RawInputModifiers.None);
            win.KeyReleaseQwerty(k, RawInputModifiers.None);
            Pump(win);
            var f = TopLevel.GetTopLevel(win)?.FocusManager?.GetFocusedElement() as Control;
            if (f is null || !card.IsVisualAncestorOf(f)) outside.Add(k + "⇒" + (f?.GetType().Name ?? "هیچ"));
            else visited.Add(f);
        }
        for (var i = 0; i < 14; i++)
        {
            win.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.None);
            win.KeyReleaseQwerty(PhysicalKey.Tab, RawInputModifiers.None);
            Pump(win);
            var f = TopLevel.GetTopLevel(win)?.FocusManager?.GetFocusedElement() as Control;
            if (f is null || !card.IsVisualAncestorOf(f)) outside.Add("Tab⇒" + (f?.GetType().Name ?? "هیچ"));
        }
        Settle(win);
        Check($"{at}: کلیدهای جهت‌دار و Tab از فرم بیرون نمی‌روند",
              outside.Count == 0, string.Join("، ", outside.Take(5)));
        Check($"{at}: کلیدهای جهت‌دار میانِ کادرهای خودِ فرم می‌روند", visited.Count >= 4, $"{visited.Count} کادر");
        var cardTop2 = card.TranslatePoint(default, win)!.Value.Y;
        Check($"{at}: صفحهٔ زیرِ فرم تکان نخورد", Math.Abs(page.Offset.Y - offset.Y) < 1 && Math.Abs(cardTop2 - cardTop) < 1,
              $"اسکرول {offset.Y:0}⇒{page.Offset.Y:0} · بالای قاب {cardTop:0}⇒{cardTop2:0}");
    }

    private static T Db<T>(Func<PumpYaqobi.Persistence.PumpDbContext, T> q)
    {
        using var db = AppHost.Current.Db.Create();
        return q(db);
    }

    private static void Shot(Window win, string dir, string name)
    {
        Settle(win);
        //  ⚠️ نخستین عکس روپوشِ تازه را یک قدم عقب می‌گیرد — دومی همان پنجرهٔ واقعی است
        win.CaptureRenderedFrame()?.Dispose();
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
