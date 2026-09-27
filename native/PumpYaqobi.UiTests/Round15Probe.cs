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

    // ══ ۱ب) نامِ برنامه: «پمپ بنزین»، و پس از نوشتنِ نامِ پمپ همان نام (۱۴۰۵/۰۷/۱۵) ══
    private static void Brand(Window win, MainViewModel vm)
    {
        var host = AppHost.Current;
        TextBlock? Title() => win.GetVisualDescendants().OfType<TextBlock>()
            .FirstOrDefault(t => t.FontSize == 21 && t.Text == vm.BrandName);
        Check("نصبِ تازه: «پمپ بنزین» در سربرگ و عنوانِ پنجره", vm.BrandName == "پمپ بنزین"
              && Title() is not null && win.Title == "پمپ بنزین", vm.BrandName + " · " + win.Title);
        Check("«پمپ یعقوبی» هیچ‌جای پنجره نیست",
              !win.GetVisualDescendants().OfType<TextBlock>().Any(t => (t.Text ?? "").Contains("پمپ یعقوبی")));
        //  همان کاری که «ساختنِ حساب» با نامِ پمپ می‌کند
        host.Settings.Set(PumpYaqobi.Services.Data.SettingsService.StationName, "پمپ بنزینِ کریمی");
        Round14Probe.Settle(win);
        Check("نامی که کاربر نوشت همان لحظه در سربرگ و عنوانِ پنجره", vm.BrandName == "پمپ بنزینِ کریمی"
              && Title() is not null && win.Title == "پمپ بنزینِ کریمی", vm.BrandName + " · " + win.Title);
        Check("و در قفل و گزارش‌ها هم همان نام", vm.Lock.Title == "پمپ بنزینِ کریمی"
              && PumpYaqobi.Application.Localization.PumpBrand.Name == "پمپ بنزینِ کریمی");
        host.Settings.Set(PumpYaqobi.Services.Data.SettingsService.StationName, "");
        Round14Probe.Settle(win);
        Check("نامِ پاک‌شده ⇒ دوباره «پمپ بنزین»", vm.BrandName == "پمپ بنزین" && win.Title == "پمپ بنزین");
    }

    // ══ ۱الف) یک کلیدِ کپسولی برای هر دو تم (عکسِ مرجعِ صاحب ریپو، ۱۴۰۵/۰۷/۱۵) ══
    //  «هر دو توی یک کادر»: روی روشن ⇒ نارنجی، خورشید، گوی راست؛ روی تیره ⇒
    //  بنفش، ماه، گوی چپ. زدنِ واقعیِ کلید هر دو سو را می‌رود، پهنا ثابت
    //  می‌ماند (سربرگ جابه‌جا نمی‌شود) و نوشته کامل جا می‌شود.
    private static void Switch(Window win, MainViewModel vm, string shots)
    {
        var sw = win.GetVisualDescendants().OfType<Avalonia.Controls.Primitives.ToggleButton>()
                    .Where(t => t.Classes.Contains("themeswitch")).ToList();
        Check("یک کلیدِ تم در سربرگ (نه دو دکمه، نه کشویی)", sw.Count == 1
              && !win.GetVisualDescendants().OfType<RadioButton>().Any(r => r.GroupName == "pump-theme"), sw.Count + " کلید");
        if (sw.Count != 1) return;
        var t = sw[0];
        var start = vm.SelectedTheme;
        Rect Screen(Visual v)
        {
            var a = v.TranslatePoint(new Point(0, 0), win)!.Value;
            var b = v.TranslatePoint(new Point(v.Bounds.Width, v.Bounds.Height), win)!.Value;
            return new Rect(new Point(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y)), new Point(Math.Max(a.X, b.X), Math.Max(a.Y, b.Y)));
        }
        Rect? firstBox = null;
        foreach (var dark in new[] { false, true })
        {
            //  زدنِ خودِ کلید (همان کاری که کلیکِ کاربر با ‎IsChecked‎ می‌کند)
            if (vm.SelectedTheme.IsDark != dark) { t.IsChecked = dark; Round14Probe.Settle(win); }
            var tag = dark ? "تیره" : "روشن";
            Check($"[{tag}] کلید و تم یکی‌اند", t.IsChecked == dark && vm.SelectedTheme.IsDark == dark, vm.SelectedTheme.Id);
            var pill = t.GetVisualDescendants().OfType<Border>().First(b => b.Name == "PART_Pill");
            Check($"[{tag}] رنگِ کپسول: {(dark ? "بنفش" : "نارنجی")}", pill.Background is Avalonia.Media.LinearGradientBrush g
                  && g.GradientStops.Any(x => x.Color == Avalonia.Media.Color.Parse(dark ? "#7C3AED" : "#FFC107")));
            var side = t.GetVisualDescendants().OfType<Grid>().First(x => x.Classes.Contains(dark ? "nightside" : "dayside"));
            var other = t.GetVisualDescendants().OfType<Grid>().First(x => x.Classes.Contains(dark ? "dayside" : "nightside"));
            Check($"[{tag}] فقط روی «{tag}» دیده می‌شود", side.IsEffectivelyVisible && !other.IsEffectivelyVisible);
            var inner = t.GetVisualDescendants().OfType<Avalonia.Controls.Presenters.ContentPresenter>().First(c => c.Name == "SwitchContent");
            Check($"[{tag}] وسطِ کپسول رنگِ فلوئنت نگرفته (شفاف)", inner.Background is null
                  || inner.Background is Avalonia.Media.ISolidColorBrush { Color.A: 0 }, inner.Background?.ToString());
            var text = side.GetVisualDescendants().OfType<TextBlock>().First(x => x.Classes.Contains("switchtext"));
            var knob = Screen(side.GetVisualDescendants().OfType<Border>().First(b => b.Classes.Contains("knob")));
            var tb = Screen(text);
            Check($"[{tag}] نوشتهٔ «{text.Text}» سفید و کامل", text.Foreground is Avalonia.Media.ISolidColorBrush w
                  && w.Color == Avalonia.Media.Colors.White && text.Bounds.Width >= text.DesiredSize.Width - 0.5, $"{text.Bounds.Width:0}/{text.DesiredSize.Width:0}");
            Check(dark ? "[تیره] گویِ ماه سمتِ چپ" : "[روشن] گویِ خورشید سمتِ راست",
                  dark ? knob.Right <= tb.Left + 0.5 : knob.Left >= tb.Right - 0.5, $"گوی {knob.Left:0}–{knob.Right:0} · نوشته {tb.Left:0}–{tb.Right:0}");
            var box = Screen(t);
            Check($"[{tag}] نوشته و گوی داخلِ کپسول", box.Contains(knob) && box.Contains(tb));
            firstBox ??= box;
            Check($"[{tag}] پهنا و جای کلید ثابت (سربرگ نمی‌پرد)", Math.Abs(box.X - firstBox.Value.X) < 0.5
                  && Math.Abs(box.Width - firstBox.Value.Width) < 0.5, $"{box.X:0}×{box.Width:0}");
            Round14Probe.Shot(win, shots, "theme-switch-" + (dark ? "dark" : "light"));
            Console.WriteLine($"  📐 کلید: {box.X:0},{box.Y:0} {box.Width:0}×{box.Height:0}");
        }
        //  و برگشت با همان کلید
        t.IsChecked = false;
        Round14Probe.Settle(win);
        Check("زدنِ دوباره ⇒ برگشت به روشن", !vm.SelectedTheme.IsDark && t.IsChecked == false, vm.SelectedTheme.Id);
        vm.SelectedTheme = start;
        Round14Probe.Settle(win);
        Check("تمی که از جای دیگر عوض شود، کلید هم با آن می‌رود", t.IsChecked == start.IsDark);
    }

    // ══ ۱) سربرگ: تم با دو دکمهٔ رادیویی، تاریخ با نامِ ماه، و ساعتِ خودِ برنامه ══
    private static void Header(Window win, MainViewModel vm, string shots)
    {
        Console.WriteLine();
        Console.WriteLine("════ ۱) سربرگ ════");
        Brand(win, vm);
        Switch(win, vm, shots);
        Check("کشوییِ تم دیگر در سربرگ نیست",
              !win.GetVisualDescendants().OfType<ComboBox>().Any(c => c.ItemsSource == vm.Themes));
        var clock = win.GetVisualDescendants().OfType<TextBlock>().FirstOrDefault(t => t.Text == vm.Clock);
        Check("ساعتِ سربرگ دوازده‌ساعته با AM/PM", clock is not null && (vm.Clock.Contains(" AM") || vm.Clock.Contains(" PM"))
              && System.Text.RegularExpressions.Regex.IsMatch(vm.Clock, @"(0[1-9]|1[0-2]):[0-5]\d:[0-5]\d [AP]M"), vm.Clock.Trim('\u200E'));
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
        Check("پنجرهٔ ساعت: پیش‌نمایش نامِ ماه دارد", cvm.PickedText.Contains("سنبله") && cvm.PickedText.Contains("09:05 AM"), cvm.PickedText);
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
