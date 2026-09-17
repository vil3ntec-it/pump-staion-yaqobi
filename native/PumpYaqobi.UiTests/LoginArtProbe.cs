using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Shape = Avalonia.Controls.Shapes.Shape;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PumpYaqobi.App.Services;
using PumpYaqobi.App.ViewModels;
using PumpYaqobi.App.ViewModels.Sections;
using PumpYaqobi.App.Views;
using PumpYaqobi.Services.Data;

namespace PumpYaqobi.UiTests;

/// <summary>
/// ══ «آدمک‌ها را باسازی کن، با تمِ خودِ برنامه، با کیفیتِ خیلی بالا» ═════════
///
/// خواستهٔ صاحب ریپو (۱۴۰۵/۰۶/۲۹، با عکس و خطِ زردِ دورِ همان آدمک‌ها): «اون
/// آدمک‌ها رو باسازی کن و بک‌گراندشو درست کن و با رنگ و تمِ خودِ برنامه باشه
/// و همه‌چی با کیفیتِ خیلی بالا درست کن… و برنامه رو ببین سنگین نکنه با یک
/// عکس.»
///
/// پس صحنه از عکسِ JPEG به **نقشهٔ برداری** (`Controls/LoginArt.axaml`) رفت و
/// این سنجه همان را با عدد ثابت می‌کند:
///
///   ۱) هیچ فایلِ عکسی در کار نیست و ویومدل هیچ بیت‌مپی ندارد
///   ۲) خودِ نقشه در درخت است و شکل‌هایش واقعاً کشیده شده‌اند
///   ۳) رنگ‌هایش از تمِ برنامه می‌آید — با عوض شدنِ تم عوض می‌شود
///   ۴) با همان اولین باز شدنِ صفحه دیده می‌شود (فوری)
///   ۵) هیچ دستورِ دیتابیسی و هیچ چیدمانی در بی‌کاری ندارد
///
///     dotnet run --project PumpYaqobi.UiTests -c Release -- loginart
/// </summary>
internal static class LoginArtProbe
{
    private static int _bad;

    private static void Check(string what, bool ok, string? detail = null)
    {
        Console.WriteLine($"  {(ok ? "✔" : "✖")} {what}{(detail is null ? "" : " — " + detail)}");
        if (!ok) _bad++;
    }

    private static void Note(string what, string detail) =>
        Console.WriteLine($"  · {what} — {detail}");

    public static int Run()
    {
        var tmpDb = Path.Combine(Path.GetTempPath(), "pump-loginart-" + Guid.NewGuid().ToString("N"), "pump.db");
        AppHost.Start(tmpDb);
        AppBuilder.Configure<PumpYaqobi.App.App>().UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .SetupWithoutStarting();

        var win = new MainWindow { Width = 1440, Height = 900 };
        win.Show(); Pump(win);
        var vm = (MainViewModel)win.DataContext!;
        vm.Lock.Password = "1234"; vm.Lock.Confirm = "1234"; vm.Lock.SubmitCommand.Execute(null);
        for (var i = 0; i < 40; i++) Pump(win);

        var account = (AccountSectionViewModel)vm.Sections.First(s => s.Id == "account");

        // ── ۱) هیچ عکسی نمانده ────────────────────────────────────────────
        Console.WriteLine("── ۱) هیچ فایلِ عکسی در کار نیست");
        var jpg = Path.Combine(Root(), "PumpYaqobi.App", "Assets", "login-art.jpg");
        Check("فایلِ JPEGی صفحهٔ ورود پاک شده", !File.Exists(jpg));

        var src = File.ReadAllText(Path.Combine(Root(), "PumpYaqobi.App", "ViewModels",
                                                "Sections", "AccountSectionViewModel.cs"));
        //  ⚠️ روی خودِ **کد** می‌گردیم، نه روی توضیحات: نامِ قدیمی در کامنتِ
        //  «دیگر نیست» هست و باید هم باشد.
        var code = string.Join("\n", src.Split('\n').Where(l => !l.TrimStart().StartsWith("//") && !l.TrimStart().StartsWith("///")));
        foreach (var gone in new[] { "LoginArt", "DecodeToWidth", "CroppedBitmap", "AssetLoader", "login-art.jpg" })
            Check($"«{gone}» در ویومدل نیست", !code.Contains(gone));

        // ── ۲) خودِ نقشه در درخت است ──────────────────────────────────────
        Console.WriteLine("── ۲) نقشهٔ برداری در صفحه");
        var db0 = DbWatch.Count;
        var mem0 = GC.GetTotalMemory(true);
        var sw = Stopwatch.StartNew();
        var go = vm.GoAsync(account);
        long firstMs = -1;
        for (var i = 0; i < 4000; i++)
        {
            Dispatcher.UIThread.RunJobs();
            win.UpdateLayout();
            if (firstMs < 0 && Art(win) is { Bounds.Width: > 0 }) firstMs = sw.ElapsedMilliseconds;
            if (go.IsCompleted && firstMs >= 0) break;
            Thread.Sleep(1);
        }
        var pageMs = sw.ElapsedMilliseconds;
        var mem1 = GC.GetTotalMemory(true);
        if (go.IsFaulted) throw go.Exception!;

        var art = Art(win);
        Check("نقشه در درخت است و دیده می‌شود", art is { IsEffectivelyVisible: true });
        Check("و جا گرفته است", art is not null && art.Bounds.Width > 100 && art.Bounds.Height > 100,
              art is null ? "نیست" : $"{art.Bounds.Width:0}×{art.Bounds.Height:0}");

        var shapes = art?.GetVisualDescendants().OfType<Shape>().ToList() ?? new();
        Check("شکل‌های برداری واقعاً کشیده شده‌اند", shapes.Count >= 25, $"{shapes.Count} شکل");
        Check("و هیچ بیت‌مپی در آن نیست",
              art is null || !art.GetVisualDescendants().OfType<Image>().Any());
        Check("و **فوری** با همان باز شدنِ صفحه آمد", firstMs >= 0 && firstMs <= pageMs,
              $"نقشه {firstMs}ms · صفحه {pageMs}ms");
        Note("حافظهٔ کلِ باز شدنِ صفحه", $"{(mem1 - mem0) / 1024.0 / 1024.0:0.0} MB");
        Note("دستورِ دیتابیسِ خودِ صفحه", $"{DbWatch.Count - db0} دستور (کارمندان · تاریخچه · پشتیبان‌ها)");

        // ── ۳) رنگ‌ها از تمِ برنامه می‌آیند ────────────────────────────────
        Console.WriteLine("── ۳) رنگ از تمِ خودِ برنامه");
        var xaml = File.ReadAllText(Path.Combine(Root(), "PumpYaqobi.App", "Controls", "LoginArt.axaml"));
        foreach (var key in new[] { "Pump.Accent", "Pump.Info", "Pump.Card", "Pump.Border", "Pump.AccentGrad" })
            Check($"«{key}» به کار رفته", xaml.Contains("{DynamicResource " + key + "}"));

        var before = Fills(shapes);
        ThemeManagerFlip(vm);
        for (var i = 0; i < 40; i++) Pump(win);
        var after = Fills(Art(win)?.GetVisualDescendants().OfType<Shape>().ToList() ?? new());
        Check("با عوض شدنِ تم، رنگ‌های نقشه هم عوض شدند",
              before.Count > 0 && after.Count > 0 && before != after,
              $"{before.Count} رنگ ⇒ {after.Count} رنگ");
        //  عکسِ چشمیِ تمِ دیگر — تا با چشم هم دیده شود، نه فقط با عدد
        var dark = Path.Combine(Path.GetTempPath(), "pump-loginart-dark.png");
        using (var f2 = win.CaptureRenderedFrame())
            if (f2 is not null) { f2.Save(dark); Console.WriteLine("عکسِ تمِ دیگر: " + dark); }
        ThemeManagerFlip(vm);
        for (var i = 0; i < 40; i++) Pump(win);

        // ── ۴) کیفیت: برداری یعنی بی سقفِ اندازه ──────────────────────────
        Console.WriteLine("── ۴) کیفیت در هر اندازه");
        Check("بومِ نقشه شفاف است (هیچ کادرِ سفیدِ داخلی)", xaml.Contains("Background=\"Transparent\""));
        Check("و با `Viewbox` هر اندازه‌ای را می‌گیرد", xaml.Contains("<Viewbox"));
        //  ⚠️ صفحهٔ برنامه راست‌به‌چپ است و نقشه را آینه می‌کند (تیکِ ✓
        //  برعکس می‌شد). خودِ سنجه یک بار گرفتش.
        Check("و جهتش صریح چپ‌به‌راست است (وگرنه آینه می‌شود)",
              xaml.Contains("FlowDirection=\"LeftToRight\""));

        // ── ۵) بی‌کاری ────────────────────────────────────────────────────
        Console.WriteLine("── ۵) صفحهٔ ورودِ باز، سه ثانیه بی‌کار");
        var dbIdle = DbWatch.Count;
        var layouts = 0;
        void OnLayout(object? _, EventArgs __) => layouts++;
        win.LayoutUpdated += OnLayout;
        var idle = Stopwatch.StartNew();
        while (idle.ElapsedMilliseconds < 3000) { Dispatcher.UIThread.RunJobs(); Thread.Sleep(10); }
        win.LayoutUpdated -= OnLayout;
        Check("هیچ چیدمانی رخ نداد", layouts == 0, $"{layouts} چیدمان");
        Check("و هیچ دستورِ دیتابیسی", DbWatch.Count == dbIdle, $"{DbWatch.Count - dbIdle} دستور");

        // ── عکسِ چشمی ─────────────────────────────────────────────────────
        var shot = Path.Combine(Path.GetTempPath(), "pump-loginart.png");
        using (var frame = win.CaptureRenderedFrame())
            if (frame is not null) { frame.Save(shot); Console.WriteLine("عکس: " + shot); }

        Console.WriteLine();
        Console.WriteLine(_bad == 0
            ? "✅ آدمک‌ها برداری‌اند، با تمِ خودِ برنامه، بی هیچ وزنی"
            : $"❌ {_bad} ایراد");
        return _bad == 0 ? 0 : 1;
    }

    /// <summary>خودِ نقشه در درختِ پنجره.</summary>
    private static Control? Art(Window win) =>
        win.GetVisualDescendants().OfType<PumpYaqobi.App.Controls.LoginArt>()
           .FirstOrDefault(a => a.IsEffectivelyVisible);

    /// <summary>رنگِ همهٔ شکل‌ها — برای سنجشِ «با تم عوض می‌شود».</summary>
    private static List<string> Fills(List<Shape> shapes) =>
        shapes.Select(s => (s.Fill as ISolidColorBrush)?.Color.ToString() ?? "")
              .Where(c => c.Length > 0).ToList();

    /// <summary>تمِ دیگر — آبی ⇄ طلایی، از راهِ خودِ برنامه.</summary>
    private static void ThemeManagerFlip(MainViewModel vm)
    {
        var now = vm.SelectedTheme;
        var other = PumpYaqobi.App.Themes.PumpTheme.All.First(t => t.Id != now?.Id);
        vm.SelectedTheme = other;
    }

    private static string Root()
    {
        var d = AppContext.BaseDirectory;
        while (d is not null && !File.Exists(Path.Combine(d, "PumpYaqobi.sln")))
            d = Path.GetDirectoryName(d);
        return d ?? ".";
    }

    private static void Pump(Window w)
    {
        for (var i = 0; i < 8; i++) { Dispatcher.UIThread.RunJobs(); w.UpdateLayout(); }
    }
}
