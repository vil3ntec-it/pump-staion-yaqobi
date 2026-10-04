using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
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

namespace PumpYaqobi.UiTests;

/// <summary>
/// ══ شورا، ث۱ — «شروعِ سریع» با پنجره و کلیک و کلیدِ واقعی ═══════════════════
///     dotnet run --project PumpYaqobi.UiTests -c Release -- quickstart [پوشهٔ عکس]
///
/// پمپ‌دارِ تازه فقط با همین چک‌لیست جلو می‌رود: هر گام «👉 نشانم بده» را
/// می‌زند، کادرِ برجسته‌شده همان کادرِ درست است، همان‌جا می‌نویسد، و تیکِ همان
/// گام می‌خورد — تا نخستین ورق. بعد چک‌لیست خودش می‌رود و «🚀 راهنمای شروع»
/// برش می‌گرداند. و برگهٔ PDFِ «یک شیفت در ۵ دقیقه» یک ورق است.
/// </summary>
internal static class QuickStartProbe
{
    private static readonly List<string> Bad = new();

    public static int Run(string[] args)
    {
        var shots = args.Length > 1 ? args[1] : Path.Combine(Path.GetTempPath(), "pump-quickstart");
        Directory.CreateDirectory(shots);
        var tmpDb = Path.Combine(Path.GetTempPath(), "pump-qs-" + Guid.NewGuid().ToString("N"), "pump.db");
        AppHost.Start(tmpDb);
        if (AppHost.Current.Auth.NeedsFirstRun()) AppHost.Current.Auth.CreateFirstAdmin("1234");
        FakeLicense.Grant();
        Hints.Quiet = true;

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

        var dash = (DashboardSectionViewModel)vm.Sections.First(s => s.Id == "dashboard");
        GoDash(win, vm, dash);
        Check("چک‌لیست روی داشبوردِ نصبِ تازه دیده می‌شود", Card(win) is { IsEffectivelyVisible: true });
        Check($"پنج گام، هیچ‌کدام «شد» نیست ({dash.QuickStartTitle})",
              dash.QuickSteps.Count == 5 && dash.QuickSteps.All(s => !s.Done));
        Shot(win, shots, "0-fresh");

        // ۱) حساب — کادرِ ایمیلِ صفحهٔ ورود
        Step(win, vm, dash, QuickStart.Account, "account", "login-email");
        LoginOnePath(win, vm, shots);
        var f = AppSettings.Load(); f.CloudAccountToken = "qs-test"; f.Save();   // «ساختنِ حساب» — ابرِ واقعی در سنجه نیست
        vm.Account.RefreshAll();
        GoDash(win, vm, dash);
        Check("گامِ «حساب» با توکنِ روی دیسک «شد» شد", Done(dash, QuickStart.Account));

        // ۲) نامِ پمپ — کادرِ نامِ گامِ پمپ؛ تایپِ واقعی
        Step(win, vm, dash, QuickStart.PumpName, "account", "pump-name");
        var nameBox = Spot.Last as TextBox;
        if (nameBox is not null) { nameBox.Text = ""; win.KeyTextInput("پمپ آزمون"); Settle(win); }
        win.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);   // Enter ⇒ «ساختنِ پمپ و پایان»
        Settle(win);
        GoDash(win, vm, dash);
        Check("گامِ «نامِ پمپ» «شد» شد", Done(dash, QuickStart.PumpName));

        // ۳) ظرفیتِ مخزن
        Step(win, vm, dash, QuickStart.Capacity, "storage", "tank-capacity");
        if (Spot.Last is TextBox cap) { cap.SelectAll(); win.KeyTextInput("30000"); Settle(win); }
        GoDash(win, vm, dash);
        Check("گامِ «ظرفیتِ مخزن» «شد» شد", Done(dash, QuickStart.Capacity));

        // ۴) نخستین پارچه — کادرِ نامِ کارتِ روز؛ Enter ذخیره می‌کند
        Step(win, vm, dash, QuickStart.Parcha, "shifts", "parcha-day");
        var parcha = (ParchaSectionViewModel)vm.Sections.First(s => s.Id == "shifts");
        Check("کادرِ برجسته‌شده داخلِ کارتِ روز است",
              Spot.Last?.GetVisualAncestors().OfType<Control>().Any(a => a.Name == "DayCard") == true);
        win.KeyTextInput("کریم");
        parcha.Day.PumpNum = "1"; parcha.Day.Start = "1000"; parcha.Day.End = "1500"; parcha.Day.Price = "80";
        Settle(win);
        Spot.Last?.Focus();
        win.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Settle(win); Settle(win);
        GoDash(win, vm, dash);
        Check("گامِ «نخستین پارچه» «شد» شد", Done(dash, QuickStart.Parcha));

        // ۵) نخستین ورق — دکمهٔ «اضافه کردن» برجسته؛ کلیکِ همان
        Dialogs.PromptHook = (_, _) => PumpYaqobi.Application.Localization.Shamsi.Today();   // کادرِ تاریخِ ورق: امروز
        Step(win, vm, dash, QuickStart.Waraq, "waraq", "waraq-new");
        Check("پیش از نوشتنِ ردیف «شد» نیست (ورقی که پارچه ساخت به‌تنهایی کافی نیست)", !Done(dash, QuickStart.Waraq));
        if (Spot.Last is Button add) Click(win, add);
        Settle(win); Settle(win);
        var waraq = (WaraqSectionViewModel)vm.Sections.First(s => s.Id == "waraq");
        Check("ورقِ امروز باز شد", waraq.Page is not null);
        if (waraq.Page is { } page)
        {
            Wait(win, ((CommunityToolkit.Mvvm.Input.IAsyncRelayCommand)page.AddTxnCommand).ExecuteAsync(null));
            var row = page.Txns.Last();
            row.Name = "کریم"; row.AmountText = "500";
            Settle(win);
            Wait(win, SaveGuard.FlushAllAsync());
        }
        GoDash(win, vm, dash);
        Check("گامِ «نخستین ورق» «شد» شد", Done(dash, QuickStart.Waraq));
        using (var db = AppHost.Current.Db.Create())
            Check("ردیفِ ورق واقعاً روی دیسک است", db.WaraqTransactions.Any(t => t.Name == "کریم"));

        Check("همه شد ⇒ چک‌لیست خودش رفت", Card(win) is not { IsEffectivelyVisible: true });
        Wait(win, ((CommunityToolkit.Mvvm.Input.IAsyncRelayCommand)dash.OpenQuickStartCommand).ExecuteAsync(null));
        Settle(win);
        Check($"«🚀 راهنمای شروع» برش گرداند ({dash.QuickStartTitle})",
              Card(win) is { IsEffectivelyVisible: true } && dash.QuickStartTitle.Contains("5 از 5"));
        Shot(win, shots, "5-done");

        // برگهٔ PDF — یک ورق
        PumpYaqobi.Reporting.Pdf.PdfEngine.Initialize();
        var pages = QuestPDF.Fluent.GenerateExtensions.GenerateImages(new PumpYaqobi.Reporting.Pdf.QuickStartReport()).Count();
        Check($"«یک شیفت در ۵ دقیقه» یک ورق است ({pages})", pages == 1);

        Console.WriteLine(Bad.Count == 0 ? "✅ همه سبز" : $"❌ {Bad.Count} ایراد");
        return Bad.Count == 0 ? 0 : 1;
    }

    private static void Step(Window win, MainViewModel vm, DashboardSectionViewModel dash, string id, string section, string spot)
    {
        Console.WriteLine($"── {id} ──");
        var step = dash.QuickSteps.First(s => s.Id == id);
        var btn = win.GetVisualDescendants().OfType<Button>()
            .FirstOrDefault(b => b.IsEffectivelyVisible && ReferenceEquals(b.CommandParameter, step));
        Check("دکمهٔ «👉 نشانم بده» دیده می‌شود", btn is not null);
        if (btn is null) return;
        Click(win, btn);
        Settle(win); Settle(win);
        Check($"بخشِ «{section}» باز شد ({vm.Current?.Id})", vm.Current?.Id == section);
        var host = Spot.Last?.GetSelfAndVisualAncestors().OfType<Control>().FirstOrDefault(c => Spot.GetId(c) == spot);
        Check($"کادرِ «{spot}» برجسته شد", host is not null && Spot.Last!.Classes.Contains("spot"),
              Spot.Last is null ? "هیچ کادری برجسته نشد" : "کادرِ دیگری برجسته شد");
        Check("و فوکوس روی همان است", Spot.Last?.IsFocused == true || Spot.Last is Button);
    }

    /// <summary>
    /// شورا، ث۳ — «صفحهٔ ورود فقط یک مسیرِ اصلی (ایمیل ⇐ کد ⇐ تمام) و بقیه زیرِ
    /// «راه‌های دیگر»». با پنجره و کلیکِ واقعی: آن‌چه دیده می‌شود، نه آن‌چه
    /// ویومدل می‌گوید.
    /// </summary>
    private static void LoginOnePath(Window win, MainViewModel vm, string shots)
    {
        Console.WriteLine("── ث۳) صفحهٔ ورود: یک مسیرِ اصلی ──");
        bool Seen(string text) => win.GetVisualDescendants().OfType<Control>().Any(c => c.IsEffectivelyVisible && c switch
        {
            TextBlock t => t.Text == text,
            ContentControl cc => cc.Content as string == text,
            TextBox tb => tb.Watermark == text,
            _ => false,
        });
        Button? Btn(string text) => win.GetVisualDescendants().OfType<Button>()
            .FirstOrDefault(b => b.IsEffectivelyVisible && b.Content as string == text);

        Check("مسیرِ اصلی: «ورود یا ساختنِ حساب»", Seen("ورود یا ساختنِ حساب"));
        Check("و دکمه‌اش «فرستادنِ کد به ایمیل» است", Btn("فرستادنِ کد به ایمیل") is not null);
        Check("کادرِ رمز در مسیرِ اصلی نیست", !Seen("دستِ‌کم هشت نویسه"));
        Check("نامِ کاربر و تکرارِ رمز و شرایط هم نیستند",
              !Seen("نامِ شما") && !Seen("تکرارِ رمز") && !Seen("شرایط و ضوابط"));
        Check("کلیدِ دوتکهٔ «حساب می‌سازم/حساب دارم» جلوی چشم نیست",
              Btn("حساب می‌سازم") is null && Btn("حساب دارم") is null);
        Check("عنوانِ دوم («ساختنِ حساب»/«خوش آمدید») روی عنوانِ اول نمی‌نشیند",
              !Seen("ساختنِ حساب") && !Seen("خوش آمدید"));
        Check("«ورود با ایمیل و رمز» پیش از باز کردنِ «راه‌های دیگر» پنهان است", Btn("🔑 ورود با ایمیل و رمز") is null);

        var ways = win.GetVisualDescendants().OfType<ToggleButton>().FirstOrDefault(t => t.Name == "OtherWays");
        Check("«راه‌های دیگر ▾» دیده می‌شود", ways is { IsEffectivelyVisible: true });
        if (ways is null) return;
        Shot(win, shots, "1-login");
        Click(win, ways);
        Shot(win, shots, "1b-other-ways");
        var pass = Btn("🔑 ورود با ایمیل و رمز");
        Check("پس از کلیک، دو راهِ رمزدار هست", pass is not null && Btn("🆕 ساختنِ حساب با رمز") is not null);
        if (pass is null) return;
        Click(win, pass);
        Check("«ورود با رمز» ⇒ کادرِ رمز و «خوش آمدید»", Seen("دستِ‌کم هشت نویسه") && Seen("خوش آمدید"));
        Check("و همان کلیدِ دوتکهٔ همیشگی برگشت", Btn("حساب می‌سازم") is not null && Btn("حساب دارم") is not null);
        var back = Btn("‹ برگشت به ورود با کدِ تأییدِ ایمیل (بی رمز)");
        Check("راهِ برگشت به مسیرِ اصلی هست", back is not null);
        if (back is not null) Click(win, back);
        Check("و برگشت: باز مسیرِ اصلی، بی رمز", Seen("ورود یا ساختنِ حساب") && !Seen("دستِ‌کم هشت نویسه"));
        Check("نامِ کهنهٔ کد هیچ‌جای صفحه نیست",
              !win.GetVisualDescendants().OfType<TextBlock>().Any(t => t.IsEffectivelyVisible
                  && PumpYaqobi.Application.Localization.CodeNames.OldNameIn(t.Text) is not null));
    }

    private static bool Done(DashboardSectionViewModel d, string id) => d.QuickSteps.FirstOrDefault(s => s.Id == id)?.Done == true;

    private static Control? Card(Window w) =>
        w.GetVisualDescendants().OfType<Border>().FirstOrDefault(b => b.Name == "QuickStartCard");

    private static void GoDash(Window win, MainViewModel vm, DashboardSectionViewModel dash)
    {
        Wait(win, vm.GoAsync(dash));
        Wait(win, dash.RefreshQuickStartAsync());
        Settle(win);
    }

    private static void Shot(Window w, string dir, string name)
    {
        try { w.CaptureRenderedFrame()?.Save(Path.Combine(dir, name + ".png")); } catch { /* عکس رفاه است */ }
    }

    private static void Click(Window win, Control c)
    {
        c.BringIntoView(); Settle(win);
        var p = c.TranslatePoint(new Point(c.Bounds.Width / 2, c.Bounds.Height / 2), win);
        if (p is null) { Check("جای دکمه پیدا شد", false); return; }
        win.MouseDown(p.Value, MouseButton.Left);
        win.MouseUp(p.Value, MouseButton.Left);
        Settle(win);
    }

    private static void Check(string what, bool ok, string? detail = null)
    {
        Console.WriteLine($"  {(ok ? "✔" : "✖")} {what}" + (detail is null || ok ? "" : " — " + detail));
        if (!ok) Bad.Add(what);
    }

    private static void Settle(Window w)
    {
        var end = DateTime.UtcNow + TimeSpan.FromMilliseconds(600);
        while (DateTime.UtcNow < end) { Dispatcher.UIThread.RunJobs(); w.UpdateLayout(); Thread.Sleep(10); }
        Pump(w);
    }

    private static void Wait(Window w, Task t)
    {
        var end = DateTime.UtcNow + TimeSpan.FromSeconds(30);
        while (!t.IsCompleted && DateTime.UtcNow < end) { Dispatcher.UIThread.RunJobs(); w.UpdateLayout(); Thread.Sleep(5); }
        Pump(w);
    }

    private static void Pump(Window w)
    { for (var i = 0; i < 8; i++) { Dispatcher.UIThread.RunJobs(); w.UpdateLayout(); } }
}
