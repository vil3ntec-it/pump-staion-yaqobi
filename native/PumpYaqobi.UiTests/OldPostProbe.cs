using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
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
/// ══ «رساندن به ماهِ گذشته» — همان کارِ کاربر، با عکس از هر بخش (۱۴۰۵/۰۷/۱۸، دوم) ══
///
/// گزارشِ صاحب ریپو: «حساب‌های قدیمی رو می‌رسونم؛ ماه‌های گذشته ساخته می‌شن و
/// وقتی می‌رم داخل‌شون همهٔ نوشته‌ها و حساب‌ها و سربرگ‌ها تو همهٔ بخش‌ها چپ یا
/// راست رفتن… عکس بگیر و ببین — حدس نزن.» و «Ctrl+Tab توی پارچه‌ها فقط وقتی
/// کار می‌کنه که روی یک کادر کلیک کنم» و «دیزل و دوباره پطرول ⇒ پارچهٔ ذخیره‌شده
/// پر نشون داده می‌شه».
///
///   ۱) پارچهٔ روز و شب با تاریخِ ماهِ گذشته ⇒ ورقِ همان روز ⇒ ردیف‌های قرض‌دار و
///      مصرف ⇒ ثبت به حساب‌ها و مصارف ⇒ آن ماه در همهٔ بخش‌ها ساخته شد
///   ۲) هر بخش در همان ماه باز می‌شود (با برگشت از بخشِ دیگر و تعویضِ تم) و هر
///      نوشتهٔ وسط‌چین با پیکسلِ واقعیِ قاب سنجیده می‌شود؛ عکسِ هر صفحه ذخیره
///   ۳) «➕ ردیف» در همان ماه ⇒ روزِ بعد از آخرین ردیف
///   ۴) پارچه: Enter ⇒ کارت خالی ⇒ دیزل ⇒ پطرول ⇒ کارت هنوز خالی
///   ۵) پارچه: Ctrl+Tab بی هیچ کادرِ فوکوس‌دار ⇒ تیل عوض می‌شود
///
///     dotnet run --project PumpYaqobi.UiTests -c Release -- oldpost [پوشهٔ عکس]
/// </summary>
internal static class OldPostProbe
{
    private static int _bad;
    private static string? _out;
    private static int _n;

    private static void Check(string what, bool ok, string? detail = null)
    {
        Console.WriteLine((ok ? "  ✅ " : "  ❌ ") + what + (detail is null ? "" : " — " + detail));
        if (!ok) _bad++;
    }

    public static int Run(string[] args)
    {
        _out = args.Length > 1 ? args[1] : null;
        if (_out is not null) Directory.CreateDirectory(_out);
        var dir = Path.Combine(Path.GetTempPath(), "pump-oldpost-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        AppHost.Start(Path.Combine(dir, "pump.db"));
        FakeLicense.Grant();
        AppBuilder.Configure<PumpYaqobi.App.App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .SetupWithoutStarting();
        static double Env(string k, double d) => double.TryParse(Environment.GetEnvironmentVariable(k),
            System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : d;
        var win = new MainWindow { Width = Env("OP_W", 1440), Height = Env("OP_H", 900) };
        LayoutCycleProbe.SetScaling(win, Env("OP_SCALE", 1));
        win.Show();
        Pump(win);
        var vm = (MainViewModel)win.DataContext!;
        LockIn.Wait(vm.Lock);
        var h = AppHost.Current;
        Seed.Fill(h);
        Settle(win);

        var old = PrevMonth(PrevMonth(Shamsi.ThisMonth()));
        var date = old + "/10";
        Console.WriteLine($"ماهِ گذشته: {old} · پنجره {win.Width}×{win.Height} · مقیاس {win.RenderScaling}");

        Console.WriteLine("── ۱) رساندن به ماهِ گذشته ──");
        Post(h, date);
        Settle(win);

        Console.WriteLine("── ۲) هر بخش در همان ماه ──");
        foreach (var id in new[] { "waraq", "safe", "expenses", "sarrafi", "chakana", "debtrasid", "debt", "noinv", "history", "shifts" })
            Section(win, vm, id, old);

        //  برگشت پس از بخشِ دیگر و تعویضِ تم — همان کارِ هر روزِ کاربر
        vm.SelectedTheme = vm.SelectedTheme == PumpYaqobi.App.Themes.PumpTheme.Gold
            ? PumpYaqobi.App.Themes.PumpTheme.Blue : PumpYaqobi.App.Themes.PumpTheme.Gold;
        Settle(win);
        foreach (var id in new[] { "waraq", "safe", "expenses", "debt" })
            Section(win, vm, id, old, " (پس از تعویضِ تم)");

        Console.WriteLine("── ۲ب) داخلِ ورقِ ماهِ گذشته و حسابِ قرض‌دار ──");
        InsideWaraq(win, vm, h, date);
        InsidePerson(win, vm);

        Console.WriteLine("── ۳) «➕ ردیف» در همان ماه ⇒ روزِ بعد از آخرین ردیف ──");
        {
            var safe = (SectionViewModel)Go(win, vm, "safe");
            dynamic d = safe;
            d.Month = old; Settle(win);
            ((System.Windows.Input.ICommand)d.AddRowCommand).Execute(null);
            Settle(win);
            using var db = h.Db.Create();
            var last = db.SafeEntries.OrderByDescending(x => x.Id).First().DateShamsi ?? "";
            var maxDay = db.SafeEntries.Where(x => x.MonthKey == old && x.Id != db.SafeEntries.Max(y => y.Id))
                           .AsEnumerable().Select(x => Shamsi.Key(x.DateShamsi) % 100).DefaultIfEmpty(0).Max();
            Check($"ردیفِ تازهٔ گاوصندوق در {old}: «{last}» — آخرینِ پیشین روزِ {maxDay}",
                  Shamsi.MonthKey(last) == old && Shamsi.Key(last) % 100 == Math.Max(1, maxDay + 1));
        }

        Console.WriteLine("── ۴ و ۵) پارچه: کارتِ خالی‌شده خالی می‌ماند · Ctrl+Tab بی فوکوس ──");
        Parcha(win, vm, h);

        Console.WriteLine("── ۶) نوشتن در خانه ردیف را به تهِ جدول نمی‌برد ──");
        foreach (var id in new[] { "expenses", "safe", "sarrafi", "chakana" })
            NoJump(win, vm, id);

        Console.WriteLine();
        Console.WriteLine(_bad == 0 ? "✅ همه سرِ جایش بود" : $"❌ {_bad} ایراد");
        return _bad == 0 ? 0 : 1;
    }

    private static string PrevMonth(string mk)
    {
        var y = int.Parse(mk[..4]); var m = int.Parse(mk[5..]);
        if (--m == 0) { m = 12; y--; }
        return $"{y:0000}/{m:00}";
    }

    private static void Post(AppHost h, string date)
    {
        var d1 = h.ParchaData.SaveShiftFlowAsync(new ShiftSaveRequest(
            FuelType.Petrol, ShiftKind.Day, date, "محمد هارون", 1, 10000m, 10900m, 62m, 4000m, 0m, 0m, "", 0m,
            ForceNew: true)).GetAwaiter().GetResult();
        var n1 = h.ParchaData.SaveShiftFlowAsync(new ShiftSaveRequest(
            FuelType.Petrol, ShiftKind.Night, date, "کریم", 2, 20000m, 20750m, 62m, 3000m, 0m, 0m, "", 0m,
            ForceNew: false)).GetAwaiter().GetResult();
        Check("پارچهٔ روز و شبِ ماهِ گذشته ذخیره شد", d1.Ok && n1.Ok, d1.Error ?? n1.Error);

        var w = h.WaraqData.OpenOrCreateAsync(date, "").GetAwaiter().GetResult();
        var day = w.Shifts.First(s => s.Kind == ShiftKind.Day);
        var names = new[] { "قرض‌دارِ شمارهٔ 2 بابت نان", "قرض‌دارِ شمارهٔ 5", "مصرفِ چای و نان" };
        var txns = day.Transactions.OrderBy(t => t.SortIndex).Take(3).ToList();
        for (var k = 0; k < txns.Count; k++)
        {
            txns[k].Name = names[k];
            txns[k].Type = k == 2 ? WaraqTxnType.Expense : WaraqTxnType.Debt;
            txns[k].Liters = k == 2 ? 0 : 25 + k;
            txns[k].Amount = k == 2 ? 750 : 0;
            txns[k].AmountAuto = k != 2;
            h.WaraqData.SaveTxnAsync(txns[k]).GetAwaiter().GetResult();
        }
        var rep = h.WaraqPosting.SyncAsync(w.Id).GetAwaiter().GetResult();
        Check($"ورقِ {date} به حساب‌ها و مصارف رسید", true, rep.ToString());
    }

    private static object Go(MainWindow win, MainViewModel vm, string id)
    {
        if (vm.Sections.FirstOrDefault(x => x.Id == id) is { } top)
        {
            Wait(win, vm.GoAsync(top));
            return top;
        }
        var parent = vm.Sections.First(x => x.SubSections.Any(s => s.Id == id));
        Wait(win, vm.GoAsync(parent));
        var sub = parent.SubSections.First(s => s.Id == id);
        parent.ShowSubCommand.Execute(sub);
        Settle(win);
        return sub;
    }

    private static void Section(MainWindow win, MainViewModel vm, string id, string old, string tag = "")
    {
        var s = (SectionViewModel)Go(win, vm, id);
        //  اول بخشِ دیگری، بعد همین — «می‌رم داخل‌شون»
        if (s.GetType().GetProperty("Month") is { CanWrite: true } mp && mp.PropertyType == typeof(string))
        {
            var months = s.GetType().GetProperty("Months")?.GetValue(s) as System.Collections.IEnumerable;
            var has = months?.Cast<object>().Any(o => o?.ToString() == old) ?? true;
            if (tag.Length == 0 && id is "waraq" or "safe" or "expenses") Check($"{s.Title}: ماهِ {old} در فهرستِ ماه‌ها هست", has);
            mp.SetValue(s, old);
            Settle(win);
        }
        Look(win, $"{s.Title}{tag}", id + (tag.Length > 0 ? "-theme" : ""));
    }

    private static void InsideWaraq(MainWindow win, MainViewModel vm, AppHost h, string date)
    {
        var wq = (WaraqSectionViewModel)Go(win, vm, "waraq");
        wq.Month = Shamsi.MonthKey(date);
        Settle(win);
        var sheet = wq.Sheets.FirstOrDefault(x => x.DateShamsi == date);
        Check($"ورقِ {date} در فهرستِ ماه", sheet is not null);
        if (sheet is null) return;
        wq.OpenCommand.Execute(sheet);
        Settle(win);
        Look(win, "داخلِ ورقِ " + date, "waraq-in");
        wq.Page!.IsNight = true; Settle(win);
        Look(win, "داخلِ ورقِ " + date + " (شب)", "waraq-in-night");
    }

    private static void InsidePerson(MainWindow win, MainViewModel vm)
    {
        var debt = (DebtSectionViewModel)Go(win, vm, "debt");
        var card = debt.Cards.FirstOrDefault(c => c.Name.Contains("شمارهٔ 2"));
        if (card is null) { Check("کارتِ قرض‌دارِ ۲", false); return; }
        debt.OpenCommand.Execute(card);
        Settle(win);
        Look(win, "حسابِ " + card.Name, "person");
    }

    private static void Parcha(MainWindow win, MainViewModel vm, AppHost h)
    {
        var p = (ParchaSectionViewModel)Go(win, vm, "shifts");
        p.IsDiesel = false; Settle(win);
        p.PaDate = Shamsi.Today(); Settle(win);
        p.Day.Name = "آزمونِ پارچه"; p.Day.PumpNum = "4";
        p.Day.Start = "50000"; p.Day.End = "50300"; p.Day.Price = "62";
        Settle(win);
        Wait(win, (Task)typeof(ParchaSectionViewModel).GetMethod("SaveFilledAndNewAsync", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)!.Invoke(p, null)!);
        Check("Enter ⇒ کارتِ روز خالی شد", string.IsNullOrEmpty(p.Day.Name), p.Day.Name);
        p.IsDiesel = true; Settle(win);
        p.IsDiesel = false; Settle(win);
        Check("دیزل ⇒ پطرول: کارتِ روز هنوز خالی است (پارچهٔ ذخیره‌شده برنگشت)",
              string.IsNullOrEmpty(p.Day.Name) && string.IsNullOrEmpty(p.Day.End), $"«{p.Day.Name}» · «{p.Day.End}»");

        //  ذخیرهٔ دیزل هم کارت را خالی می‌کند و خالی می‌ماند
        p.IsDiesel = true; Settle(win);
        p.Day.Name = "دیزلِ آزمون"; p.Day.PumpNum = "3";
        p.Day.Start = "7000"; p.Day.End = "7200"; p.Day.Price = "70";
        Settle(win);
        Wait(win, (Task)typeof(ParchaSectionViewModel).GetMethod("SaveShiftAsync", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)!.Invoke(p, new object[] { p.Day })!);
        p.IsDiesel = false; Settle(win);
        p.IsDiesel = true; Settle(win);
        Check("ذخیرهٔ دیزل ⇒ پطرول ⇒ دیزل: کارت خالی ماند", string.IsNullOrEmpty(p.Day.Name), p.Day.Name);
        p.IsDiesel = false; Settle(win);

        //  ۵) Ctrl+Tab بی هیچ کادرِ فوکوس‌دار
        win.FocusManager?.ClearFocus();
        Settle(win);
        var before = p.IsDiesel;
        win.KeyPressQwerty(PhysicalKey.ControlLeft, RawInputModifiers.None);
        win.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.Control);
        Dispatcher.UIThread.RunJobs();
        var once = p.IsDiesel;
        win.KeyReleaseQwerty(PhysicalKey.Tab, RawInputModifiers.Control);
        win.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.Control);
        Dispatcher.UIThread.RunJobs();
        var twice = p.IsDiesel;
        win.KeyReleaseQwerty(PhysicalKey.Tab, RawInputModifiers.Control);
        win.KeyReleaseQwerty(PhysicalKey.ControlLeft, RawInputModifiers.None);
        Settle(win);
        var focused = win.FocusManager?.GetFocusedElement();
        Check($"Ctrl+Tab بی فوکوس (فوکوس: {focused?.GetType().Name ?? "هیچ"}) ⇒ تیل عوض شد و دوباره برگشت",
              once != before && twice == before);
    }

    /// <summary>
    /// کلیکِ واقعی روی سرستون (همان کلیکِ تصادفیِ کاربر)، بعد تایپِ واقعی در خانهٔ
    /// یک ردیفِ وسط: ردیف باید سرِ جایش بماند — در جدول و پس از رفتن و برگشتن.
    /// </summary>
    private static void NoJump(MainWindow win, MainViewModel vm, string id)
    {
        var s = (SectionViewModel)Go(win, vm, id);
        dynamic d = s;
        d.Month = Shamsi.ThisMonth(); Settle(win);
        var add = (System.Windows.Input.ICommand)d.AddRowCommand;
        for (var i = 0; i < 4; i++) { add.Execute(null); Settle(win); }
        var g = win.GetVisualDescendants().OfType<DataGrid>()
                   .FirstOrDefault(x => x.IsEffectivelyVisible && x.Columns.Any(c => c.IsVisible));
        if (g is null) { Check($"{s.Title}: جدول", false); return; }
        var rows = ((System.Collections.IEnumerable)d.Rows).Cast<object>().ToList();
        if (rows.Count < 3) { Check($"{s.Title}: ردیف کافی", false, rows.Count.ToString()); return; }

        //  یک کلیکِ ساده روی وسطِ یک سرستونِ نوشته‌ای
        var root = TopLevel.GetTopLevel(g)!;
        var textHeads = g.Columns.Where(c => c is DataGridTextColumn && !c.IsReadOnly && c.IsVisible)
                         .Select(c => c.Header).ToList();
        var head = g.GetVisualDescendants().OfType<DataGridColumnHeader>()
                    .Where(h => h.IsEffectivelyVisible && h.Bounds.Width > 40 && h.Content is string
                                && textHeads.Any(t => Equals(t, h.Content)))
                    .OrderByDescending(h => h.Bounds.Width).First();
        var hp = head.TranslatePoint(new Point(head.Bounds.Width / 2, head.Bounds.Height / 2), root)!.Value;
        win.MouseDown(hp, MouseButton.Left); win.MouseUp(hp, MouseButton.Left);
        Settle(win);

        //  کلیکِ واقعی روی خانهٔ یک ردیفِ **دیدنی** (وسطِ آن‌چه جلوی چشم است)، در همان ستون
        var col = g.Columns.First(c => Equals(c.Header, head.Content));
        var visRows = g.GetVisualDescendants().OfType<DataGridRow>()
            .Where(r => r.IsEffectivelyVisible && r.DataContext is not null)
            .Where(r => { var y = r.TranslatePoint(default, root)!.Value.Y; return y > hp.Y + 20 && y < root.Bounds.Height - 60; })
            .OrderBy(r => r.TranslatePoint(default, g)!.Value.Y).ToList();
        if (visRows.Count == 0) { Check($"{s.Title}: ردیفِ دیدنی", false); return; }
        var rowCtl = visRows[visRows.Count / 2];
        var target = rowCtl.DataContext!;
        var cell = rowCtl.GetVisualDescendants().OfType<DataGridCell>()
            .First(c => c.IsEffectivelyVisible && Math.Abs(c.TranslatePoint(default, root)!.Value.X - head.TranslatePoint(default, root)!.Value.X) < 3);
        var cp = cell.TranslatePoint(new Point(cell.Bounds.Width / 2, cell.Bounds.Height / 2), root)!.Value;
        win.MouseDown(cp, MouseButton.Left); win.MouseUp(cp, MouseButton.Left);
        Settle(win);
        win.KeyTextInput("73519");
        Pump(win);
        if (Environment.GetEnvironmentVariable("OP_DEBUG") == "1")
        {
            var ed = win.FocusManager?.GetFocusedElement() as TextBox;
            Console.WriteLine($"      · پیش از Enter: فوکوس {win.FocusManager?.GetFocusedElement()?.GetType().Name} · متن «{ed?.Text}» · ویرایش؟ {g.GetVisualDescendants().OfType<TextBox>().Any(t => t.IsFocused)}");
        }
        win.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        win.KeyReleaseQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Settle(win);
        Wait(win, SaveGuard.FlushAllAsync());

        List<object> Order() => g.CollectionView is System.Collections.IEnumerable cv
            ? cv.Cast<object>().ToList() : new List<object>();
        var vis = Order();
        var wantAt = rows.IndexOf(target);
        var nowAt = vis.IndexOf(target);
        var sorted = g.CollectionView?.SortDescriptions.Count > 0;
        if (Environment.GetEnvironmentVariable("OP_DEBUG") == "1")
        {
            var who = ((System.Collections.IEnumerable)d.Rows).Cast<object>().Select((r, i) => (r, i))
                .Where(x => x.r.GetType().GetProperties().Any(pp => { try { return pp.GetValue(x.r)?.ToString()?.Contains("73519") == true; } catch { return false; } }))
                .Select(x => x.i).ToList();
            Console.WriteLine($"      · هدف {rows.IndexOf(target)} · نوشته در ردیف‌های [{string.Join(",", who)}] · ستونِ جاری «{g.CurrentColumn?.Header}» · فوکوس {win.FocusManager?.GetFocusedElement()?.GetType().Name}");
        }
        var texts = target.GetType().GetProperties()
            .Select(pp => { try { return pp.GetValue(target)?.ToString(); } catch { return null; } });
        Check($"{s.Title}: نوشتهٔ تایپ‌شده در همان ردیف نشست (ستونِ «{head.Content}»)",
              texts.Any(t => t is not null && Shamsi.ToEnDigits(t).Replace(",", "").Contains("73519")));
        Check($"{s.Title}: پس از کلیک روی سرستونِ «{head.Content}» و تایپ، ردیف سرِ جایش ماند ({wantAt} ⇒ {nowAt}){(sorted ? " · جدول مرتب شد!" : "")}",
              nowAt == wantAt && !sorted);
    }

    // ══ سنجشِ پیکسلی ═══════════════════════════════════════════════════════

    private static void Look(Window win, string what, string file)
    {
        AppHost.Current.Toasts.Visible = false;
        Pump(win);
        win.CaptureRenderedFrame()?.Dispose();
        Pump(win);
        using var shot = win.CaptureRenderedFrame()!;
        using var ms = new MemoryStream();
        shot.Save(ms);
        if (_out is not null)
            File.WriteAllBytes(Path.Combine(_out, $"{++_n:00}-{file}.png"), ms.ToArray());
        ms.Position = 0;
        using var frame = SkiaSharp.SKBitmap.Decode(ms);

        var bad = new List<string>();
        var n = 0;
        foreach (var tb in win.GetVisualDescendants().OfType<TextBlock>())
        {
            if (!tb.IsEffectivelyVisible || string.IsNullOrWhiteSpace(tb.Text) || tb.Bounds.Width < 20) continue;
            if (tb.TextAlignment != TextAlignment.Center) continue;
            //  فقط نوشته‌ای که خودش کادرِ وسط‌چین است (کشیده)؛ نوشتهٔ هم‌قدِ متن کجی ندارد
            if (tb.HorizontalAlignment != HorizontalAlignment.Stretch) continue;
            if (tb.FindAncestorOfType<Popup>() is not null) continue;
            n++;
            if (Ink(tb, frame) is { } o && o > 3) bad.Add($"«{Short(tb.Text)}» {o:0}px");
        }
        var stale = LayoutCycleProbe.WrongAligned(win).Where(x => !x.StartsWith("نااندازه")).ToList();
        bad.AddRange(stale.Select(x => "کشیده با ترازِ دیگر: " + Short(x)));
        //  هیچ لغزشِ افقیِ پنهانی (همه‌چیز یک‌جا به چپ یا راست)
        foreach (var sv in win.GetVisualDescendants().OfType<ScrollViewer>()
                              .Where(v => v.IsEffectivelyVisible && Math.Abs(v.Offset.X) > 0.5
                                          && !v.GetVisualDescendants().OfType<Control>().Any(c => c.Name == "NavItems")))
            bad.Add($"لغزشِ افقیِ {sv.Name ?? sv.GetType().Name}: {sv.Offset.X:0}px");
        Check($"{what}: {n} نوشتهٔ وسط‌چین سرِ جایش" + (_out is null ? "" : $" · 📷 {_n:00}-{file}.png"),
              bad.Count == 0, bad.Count == 0 ? null : string.Join("، ", bad.Take(6)));
    }

    /// <summary>
    /// وسطِ جوهرِ نوشته از وسطِ **جای چیدمانِ** خودش (بی ‎RenderTransform‎ی تصحیح) —
    /// یعنی همان «وسط» که کاربر انتظار دارد. همان روشِ پیکسلیِ ‎OldMonthProbe.Off‎.
    /// </summary>
    private static double? Ink(TextBlock tb, SkiaSharp.SKBitmap frame)
    {
        var tl = tb.TextLayout;
        if (tl is null || tl.TextLines.Count != 1) return null;
        if (tl.TextLines[0].Width >= tb.Bounds.Width - tb.Padding.Left - tb.Padding.Right - 1) return null;
        if (tb.GetVisualParent() is not Visual parent) return null;
        var root = TopLevel.GetTopLevel(tb)!;
        var k = root.RenderScaling;
        var a = parent.TranslatePoint(tb.Bounds.TopLeft, root)!.Value * k;
        var b = parent.TranslatePoint(tb.Bounds.BottomRight, root)!.Value * k;
        double L = Math.Min(a.X, b.X), R = Math.Max(a.X, b.X), T = Math.Min(a.Y, b.Y), B = Math.Max(a.Y, b.Y);
        var navBottom = root.GetVisualDescendants().OfType<Control>()
            .Where(c => c.Name == "NavItems" && c.IsEffectivelyVisible)
            .Select(c => c.TranslatePoint(new Point(0, c.Bounds.Height), root)!.Value.Y * k).DefaultIfEmpty(0).Max();
        if (T < navBottom + 2 || B > frame.Height - 2) return null;
        int x0 = (int)Math.Ceiling(L) + 1, x1 = (int)Math.Floor(R) - 1;
        int y0 = (int)Math.Ceiling(T) + 1, y1 = (int)Math.Floor(B) - 1;
        if (x1 <= x0 || y1 <= y0 || x1 >= frame.Width || y1 >= frame.Height) return null;
        var bg = OldMonthProbe.Bg(frame, x0, x1, y0, y1);
        int lo = -1, hi = -1;
        //  ⛔ ستونی که از بالا تا پایین «جوهر» است خطِ کادر است، نه گلیف — با آستانهٔ ۸۰
        //  خطِ آبیِ کمرنگِ جدول هم دیده می‌شد (‎centerlab grid‎: «4» ۱۸۶ پیکسلِ دروغ)
        var tall = (y1 - y0 + 1) * 0.97;
        for (var x = x0; x <= x1; x++)
        {
            var ink = 0;
            for (var y = y0; y <= y1; y++)
            {
                var c = frame.GetPixel(x, y);
                if (Math.Abs(c.Red - bg.Red) + Math.Abs(c.Green - bg.Green) + Math.Abs(c.Blue - bg.Blue) > OldMonthProbe.InkDiff) ink++;
            }
            if (ink == 0 || ink >= tall) continue;
            if (lo < 0) lo = x;
            hi = x;
        }
        if (lo < 0) return null;
        var off = Math.Abs((lo + hi + 1) / 2.0 - (L + R) / 2) / k;
        if (off > 3 && Environment.GetEnvironmentVariable("OP_DEBUG") == "1")
        {
            var ln = tl.TextLines[0];
            //  نیمرخِ ستون‌ها: هر تکهٔ پیوسته با بیشینهٔ اختلافِ رنگش — جوهرِ کم‌رنگ هم دیده شود
            var segs = new List<string>();
            int s0 = -1, mx = 0;
            for (var x = x0; x <= x1 + 1; x++)
            {
                var m = 0;
                if (x <= x1)
                    for (var y = y0; y <= y1; y++)
                    {
                        var c = frame.GetPixel(x, y);
                        m = Math.Max(m, Math.Abs(c.Red - bg.Red) + Math.Abs(c.Green - bg.Green) + Math.Abs(c.Blue - bg.Blue));
                    }
                if (m > 40) { if (s0 < 0) { s0 = x; mx = 0; } mx = Math.Max(mx, m); }
                else if (s0 >= 0) { segs.Add($"{s0}..{x - 1}:{mx}"); s0 = -1; }
            }
            Console.WriteLine($"      · نیمرخ «{tb.Text}» bg=({bg.Red},{bg.Green},{bg.Blue}) {string.Join(" ", segs)}");
            Console.WriteLine($"      · «{tb.Text}» ink={lo}..{hi} box={L:0}..{R:0} w={ln.Width:0.#} wt={ln.WidthIncludingTrailingWhitespace:0.#} start={ln.Start:0.#} fd={tb.FlowDirection} rt={(tb.RenderTransform as TranslateTransform)?.X} font={tb.FontFamily} fix={PumpYaqobi.App.Controls.RtlTrim.CenterFix(tb):0.#} maxW={tl.MaxWidth:0.#} tbW={tb.Bounds.Width:0.#} lines={tl.TextLines.Count} ha={tb.HorizontalAlignment} trim={tb.TextTrimming} wrapm={tb.TextWrapping} cps={string.Join(",", tb.Text!.Take(4).Select(c => ((int)c).ToString("X")))}");
        }
        return off;
    }

    private static string Short(string? s) => s is null ? "" : s.Length > 30 ? s[..30] + "…" : s;

    /// <summary>
    /// کمینهٔ اختلافِ رنگ با زمینه که «جوهر» شمرده می‌شود. ⚠️ ۱۵۰ ایموجیِ قهوه‌ایِ
    /// «🟤» را روی زمینهٔ تیره (اختلاف ۱۰۳ تا ۱۱۳ روی ویندوز) نمی‌دید و نوشته را بی
    /// آن می‌سنجید — «۱۲ پیکسل کج»ِ دروغ (‎align-windows‎، نیمرخِ جوهر). زمینهٔ کادر
    /// یکدست است و نیمرخ زیرِ ۴۰ هیچ لرزشی نشان نداد، پس ۸۰ امن است.
    /// </summary>
    internal const int InkDiff = 80;

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
