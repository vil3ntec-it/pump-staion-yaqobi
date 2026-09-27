using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PumpYaqobi.App.Services;
using PumpYaqobi.App.ViewModels;
using PumpYaqobi.App.ViewModels.Sections;
using PumpYaqobi.App.Views;

namespace PumpYaqobi.UiTests;

/// <summary>
/// ══ فهرستِ ۱۴۰۵/۰۷/۱۵ی صاحب ریپو — سربرگ، ترتیبِ بخش‌ها، رسیدِ پارچه ══════
///
///     dotnet run --project PumpYaqobi.UiTests -c Release -- round15 [پوشهٔ عکس]
/// </summary>
internal static class Round15Probe
{
    private static int _bad;

    private static void Check(string what, bool ok, string? detail = null)
    {
        Console.WriteLine((ok ? "  ✔ " : "  ✖ ") + what + (detail is null ? "" : " — " + detail));
        if (!ok) _bad++;
    }

    public static int Run(string[] args)
    {
        var shots = args.Length > 1 ? args[1] : Path.Combine(Path.GetTempPath(), "pump-round15");
        Directory.CreateDirectory(shots);
        var dir = Path.Combine(Path.GetTempPath(), "pump-r15-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        AppHost.Start(Path.Combine(dir, "pump.db"));
        FakeLicense.Grant();

        AppBuilder.Configure<PumpYaqobi.App.App>().UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .SetupWithoutStarting();
        var win = new MainWindow { Width = 1366, Height = 800 };
        win.Show();
        Round14Probe.Settle(win);
        var vm = (MainViewModel)win.DataContext!;
        LockIn.Wait(vm.Lock);
        Round14Probe.Settle(win);

        Header(win, vm, shots);
        NavOrderCheck(win, vm, shots);
        RasidTable(win, vm, shots);

        Console.WriteLine(_bad == 0 ? "✅ همهٔ بندها سبزند" : $"❌ {_bad} ایراد");
        return _bad == 0 ? 0 : 1;
    }

    // ══ ۱) سربرگ: تم با دو دکمهٔ رادیویی، تاریخ با نامِ ماه، و ساعتِ خودِ برنامه ══
    private static void Header(Window win, MainViewModel vm, string shots)
    {
        Console.WriteLine();
        Console.WriteLine("════ ۱) سربرگ ════");
        var radios = win.GetVisualDescendants().OfType<RadioButton>().Where(r => r.Classes.Contains("theme")).ToList();
        Check("دو دکمهٔ رادیوییِ تم در سربرگ", radios.Count == 2, radios.Count + " دکمه");
        Check("کشوییِ تم دیگر در سربرگ نیست",
              !win.GetVisualDescendants().OfType<ComboBox>().Any(c => c.ItemsSource == vm.Themes));
        if (radios.Count == 2)
        {
            var before = vm.SelectedTheme;
            var target = radios.First(r => r.IsChecked != true);
            target.IsChecked = true;
            Round14Probe.Settle(win);
            Check("زدنِ دکمهٔ دیگر تم را عوض می‌کند", vm.SelectedTheme != before, before.Id + " ⇒ " + vm.SelectedTheme.Id);
            Round14Probe.Shot(win, shots, "header-" + vm.SelectedTheme.Id);
            vm.SelectedTheme = before;
            Round14Probe.Settle(win);
            Check("و برگشتن هم دکمه‌ها را درست تیک می‌زند",
                  radios.Count(r => r.IsChecked == true) == 1 && (radios[0].IsChecked == true) == !before.IsDark);
        }
        var today = MainViewModel.HeaderDate(DateTime.Now);
        var shown = win.GetVisualDescendants().OfType<TextBlock>().FirstOrDefault(t => t.Text == today);
        Check("تاریخِ سربرگ: روزِ هفته، روز، نامِ ماه، سال", shown is not null && today.Contains(PumpYaqobi.Application.Localization.Shamsi.MonthName(
                  int.Parse(PumpYaqobi.Application.Localization.Shamsi.Of(DateTime.Now).Split('/')[1]))), today.Trim('⁧', '⁩'));

        //  پنجرهٔ «🕘 تاریخ و ساعت»ِ خودِ برنامه — نه تنظیماتِ ویندوز
        var runs = new List<System.Diagnostics.ProcessStartInfo>();
        ClockService.TestRun = psi => { runs.Add(psi); return ClockService.Result.Done; };
        var cw = new ClockWindow { Width = 560, Height = 520 };
        cw.Show();
        Round14Probe.Settle(cw);
        var cvm = (ClockViewModel)cw.DataContext!;
        cvm.MonthIndex = 5; cvm.Day = 31; cvm.HourIndex = 9; cvm.MinuteIndex = 5;
        Round14Probe.Settle(cw);
        Check("پنجرهٔ ساعت: سنبله ۳۱ روز دارد، حوت ۲۹ یا ۳۰", cvm.Days.Count == 31
              && new ClockViewModel(new DateTime(2026, 3, 1)) is var h && (h.MonthIndex = 11) == 11 && h.Days.Count is 29 or 30);
        Check("پنجرهٔ ساعت: پیش‌نمایش نامِ ماه دارد", cvm.PickedText.Contains("سنبله") && cvm.PickedText.Contains("09:05"), cvm.PickedText);
        using (var f = cw.CaptureRenderedFrame()) f?.Save(Path.Combine(shots, "clock-window.png"));
        cvm.ApplyCommand.Execute(null);
        for (var i = 0; i < 50 && runs.Count == 0; i++) { Round14Probe.Pump(cw); Thread.Sleep(10); }
        Round14Probe.Settle(cw);
        Check("«ثبت» همان ساعتِ ویندوز را با اجازهٔ مدیر عوض می‌کند (بی ساعتِ دوم)",
              runs.Count == 1 && runs[0].Verb == "runas" && runs[0].Arguments.Contains("Set-Date")
              && runs[0].Arguments.Contains(cvm.Picked.ToString("yyyy-MM-dd'T'HH:mm", System.Globalization.CultureInfo.InvariantCulture)),
              runs.Count > 0 ? runs[0].Arguments : "هیچ");
        Check("و نتیجه گفته می‌شود", cvm.Status.Contains("✅"), cvm.Status);
        ClockService.TestRun = _ => ClockService.Result.Cancelled;
        cvm.ApplyCommand.Execute(null);
        for (var i = 0; i < 50 && !cvm.Status.Contains("نه"); i++) { Round14Probe.Pump(cw); Thread.Sleep(10); }
        Check("«نه»ی ویندوز خطا نیست و گفته می‌شود", cvm.Status.Contains("چیزی عوض نشد"), cvm.Status);
        ClockService.TestRun = null;
        cw.Close();
    }

    // ══ ۲) ترتیبِ بخش‌ها ═══════════════════════════════════════════════════
    private static void NavOrderCheck(Window win, MainViewModel vm, string shots)
    {
        Console.WriteLine();
        Console.WriteLine("════ ۲) ترتیبِ بخش‌های نوار ════");
        var sarrafi = vm.Sections.First(s => s.Id == "sarrafi");
        vm.MoveNav(sarrafi, NavOrder.Where.First);
        Round14Probe.Settle(win);
        Check("صرافی اولِ نوار", vm.NavSections[0].Id == "sarrafi");
        var firstBtn = NavButtons(win).FirstOrDefault();
        Check("و دکمهٔ اولِ نوار هم همان است", firstBtn?.DataContext == sarrafi);
        //  مقدارِ راحتی است و با ‎SaveSoon‎ (۶۰۰ms بعد) روی دیسک می‌نشیند
        for (var i = 0; i < 150 && !AppSettings.Load().NavOrder.StartsWith("sarrafi,"); i++) { Round14Probe.Pump(win); Thread.Sleep(10); }
        Check("ترتیب ذخیره شد (و با بستن و باز کردنِ برنامه می‌ماند)", AppSettings.Load().NavOrder.StartsWith("sarrafi,"), AppSettings.Load().NavOrder);
        Check("برنامهٔ تازه‌بازشده هم صرافی را اول می‌بیند", new MainViewModel(AppSettings.Load()).NavSections[0].Id == "sarrafi");
        Round14Probe.Shot(win, shots, "nav-sarrafi-first");
        vm.MoveNav(sarrafi, NavOrder.Where.Last);
        Round14Probe.Settle(win);
        Check("صرافی آخرِ نوار", vm.NavSections[^1].Id == "sarrafi");
        vm.MoveNav(sarrafi, NavOrder.Where.Earlier);
        Round14Probe.Settle(win);
        Check("یک خانه جلوتر", vm.NavSections[^2].Id == "sarrafi");
        Check("بخش‌های برنامه همان‌اند (فقط ترتیبِ دیدن عوض شد)",
              vm.Sections.Select(s => s.Id).Take(18).SequenceEqual(new MainViewModel(AppSettings.Load()).Sections.Select(s => s.Id).Take(18)));
        vm.NavResetCommand.Execute(null);
        Round14Probe.Settle(win);
        for (var i = 0; i < 150 && AppSettings.Load().NavOrder.Length > 0; i++) { Round14Probe.Pump(win); Thread.Sleep(10); }
        Check("ترتیبِ پیش‌فرض برگشت", AppSettings.Load().NavOrder.Length == 0 && vm.NavSections[0].Id == "dashboard");
        var menu = NavButtons(win).FirstOrDefault()?.ContextMenu;
        Check("راست‌کلیکِ هر بخش منوی جابه‌جایی دارد", menu?.Items.Count >= 5, (menu?.Items.Count ?? 0) + " قلم");
    }

    private static List<Button> NavButtons(Window win)
    {
        var strip = win.GetVisualDescendants().OfType<PumpYaqobi.App.Controls.NavStrip>().First();
        //  راست‌به‌چپ: دکمهٔ «اول» سمتِ راست است ⇒ ترتیبِ فرزندان همان ترتیبِ نوار
        return strip.GetVisualDescendants().OfType<Button>().Where(b => b.Classes.Contains("nav")).ToList();
    }

    // ══ ۳) رسیدِ پارچه: سربرگِ جمع‌وجور، و جدولی که ناپدید نمی‌شود ═════════
    private static void RasidTable(Window win, MainViewModel vm, string shots)
    {
        Console.WriteLine();
        Console.WriteLine("════ ۳) رسید پارچه‌ها ════");
        var sec = (ParchaReceiptSectionViewModel)vm.Sections.First(s => s.Id == "rasid");
        Round14Probe.Wait(win, vm.GoAsync(sec));
        var sub = win.GetVisualDescendants().OfType<TextBlock>()
                     .FirstOrDefault(t => t.Classes.Contains("sec-sub") && t.IsEffectivelyVisible);
        var head = sub?.GetVisualAncestors().OfType<Border>().FirstOrDefault(b => b.Classes.Contains("card-head"));
        Check("توضیحِ زیرِ عنوان یک ردیفِ تمام‌پهناست، نه ستونِ باریک",
              sub is not null && head is not null && sub.Bounds.Width > head.Bounds.Width * 0.8,
              $"{sub?.Bounds.Width:0} از {head?.Bounds.Width:0}");
        Check("و سربرگ کوتاه است (زیرِ ۱۱۰ پیکسل)", head is not null && head.Bounds.Height < 110, $"{head?.Bounds.Height:0} پیکسل");

        void Ops(string what, Func<Task> op)
        {
            Round14Probe.Wait(win, op());
            Round14Probe.Settle(win);
            var grid = win.GetVisualDescendants().OfType<DataGrid>().FirstOrDefault(g => g.IsEffectivelyVisible);
            var rows = win.GetVisualDescendants().OfType<DataGridRow>().Count(r => r.IsEffectivelyVisible && r.Bounds.Height > 0);
            var want = Math.Min(sec.Rows.Count, 8);
            Check($"{what}: جدول دیده می‌شود با ردیف‌هایش",
                  sec.Rows.Count == 0 || (grid is not null && grid.Bounds.Height > 40 && rows >= want),
                  $"{sec.Rows.Count} ردیف · {rows} ردیفِ دیدنی · قابِ جدول {grid?.Bounds.Height:0}");
        }
        Ops("افزودنِ یک ردیف", () => sec.AddRowCommand.ExecuteAsync(null));
        Ops("افزودنِ پنج ردیف", () => sec.AddRowsAsync(5));
        Ops("برداشتنِ دو ردیف", () => sec.DeleteRowsAsync(2));
        Ops("رفتن به بخشِ دیگر و برگشت", async () =>
        {
            await vm.GoAsync(vm.Sections.First(s => s.Id == "safe"));
            Round14Probe.Settle(win);
            await vm.GoAsync(sec);
        });
        Ops("تعویضِ تم در همین بخش", () => { vm.SelectedTheme = vm.SelectedTheme.IsDark ? PumpYaqobi.App.Themes.PumpTheme.Blue : PumpYaqobi.App.Themes.PumpTheme.Gold; return Task.CompletedTask; });
        Ops("ثبتِ همه در حساب‌ها", () => sec.PostAllCommand.ExecuteAsync(null));
        Ops("افزودنِ هفتاد ردیف (پنجرهٔ چسبان)", () => sec.AddRowsAsync(70));
        var page = win.GetVisualDescendants().OfType<ScrollViewer>().First(v => v.Name == "PageScroll");
        foreach (var y in new[] { 400.0, 1200, 2400, 99999, 0 })
        {
            page.Offset = new Vector(0, Math.Min(y, Math.Max(0, page.Extent.Height - page.Viewport.Height)));
            Round14Probe.Settle(win);
            var rows = win.GetVisualDescendants().OfType<DataGridRow>().Count(r => r.IsEffectivelyVisible && r.Bounds.Height > 0);
            Check($"اسکرول تا {page.Offset.Y:0}: جدول ناپدید نشد", rows > 0, rows + " ردیفِ دیدنی");
        }
        Round14Probe.Shot(win, shots, "rasid");
    }
}
