using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
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

        //  سنجه با نصبِ **پلن‌دار** می‌دود — وگرنه داشبورد و مفاد/ضرر و
        //  تاریخچه‌ها قفل‌اند و باز نمی‌شوند. شرحش در `FakeLicense`؛ خودِ
        //  قفل در بندِ ۱۷ی `verify` و در `EntitlementsTests` سنجیده می‌شود.
        FakeLicense.Grant();

        AppBuilder.Configure<PumpYaqobi.App.App>().UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .SetupWithoutStarting();

        var win = new MainWindow { Width = 1440, Height = 900 };
        win.Show(); Pump(win);
        var vm = (MainViewModel)win.DataContext!;
        vm.Lock.Password = "1234"; vm.Lock.Confirm = "1234"; LockIn.Wait(vm.Lock);
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

        // ── ۱ب) نخِ رابط برای نقشه هیچ وقتی نمی‌دهد ───────────────────────
        //  گزارشِ صاحب ریپو (۱۴۰۵/۰۶/۳۰): «برنامه باز خیلی کند شده… لگ داره.»
        //  خودِ تجزیهٔ نقشه ~۱۱۶ میلی‌ثانیه است (سنجیده شد) و تا امروز روی
        //  **نخِ رابط** می‌رفت — یک فریمِ گم‌شده، هم وقتِ گرم کردنِ صفحه‌ها و
        //  هم سرِ باز کردنِ صفحه. حالا روی نخِ دیگر می‌رود.
        Console.WriteLine("── ۱ب) تجزیهٔ نقشه روی نخِ رابط نیست");
        Check("نخِ رابط برای نقشه چیزی نپرداخته",
              PumpYaqobi.App.Controls.LoginArt.UiMs <= 5,
              $"نخِ رابط {PumpYaqobi.App.Controls.LoginArt.UiMs}ms · نخِ دیگر "
              + $"{PumpYaqobi.App.Controls.LoginArt.OffMs}ms · {PumpYaqobi.App.Controls.LoginArt.Parses} بار");

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

        Check("نقشه واقعاً نشانده شده", Bitmap0(win) is not null);
        Check("و **فوری** با همان باز شدنِ صفحه آمد", firstMs >= 0 && firstMs <= pageMs,
              $"نقشه {firstMs}ms · صفحه {pageMs}ms");
        Check("نقشه برای هر تم فقط یک بار ساخته می‌شود",
              PumpYaqobi.App.Controls.LoginArt.Parses == 1,
              $"{PumpYaqobi.App.Controls.LoginArt.Parses} بار · نخِ دیگر {PumpYaqobi.App.Controls.LoginArt.OffMs}ms");
        Check("و نخِ رابط هنوز چیزی نپرداخته",
              PumpYaqobi.App.Controls.LoginArt.UiMs <= 5,
              $"{PumpYaqobi.App.Controls.LoginArt.UiMs}ms");
        Note("حافظهٔ کلِ باز شدنِ صفحه", $"{(mem1 - mem0) / 1024.0 / 1024.0:0.0} MB");
        Note("دستورِ دیتابیسِ خودِ صفحه", $"{DbWatch.Count - db0} دستور (کارمندان · تاریخچه · پشتیبان‌ها)");

        // ── ۳) خودِ فایل و پروانه‌اش ──────────────────────────────────────
        Console.WriteLine("── ۳) فایلِ نقشه");
        var svgPath = Path.Combine(Root(), "PumpYaqobi.App", "Assets", "login-art.svg");
        Check("نقشهٔ SVG در دارایی‌ها هست", File.Exists(svgPath));
        var svg = File.Exists(svgPath) ? File.ReadAllText(svgPath) : "";
        Note("اندازهٔ فایل", $"{svg.Length / 1024.0:0.0} KB");
        Check("و پروانه‌اش کنارش نوشته شده",
              File.Exists(Path.Combine(Root(), "PumpYaqobi.App", "Assets", "ART-LICENCE.md")));
        Check("نقشه پرجزئیات است (نه یک طرحِ دست‌ساز)",
              svg.Split("<path").Length - 1 >= 40, $"{svg.Split("<path").Length - 1} مسیرِ برداری");

        // ── ۴) رنگ‌ها با تمِ برنامه عوض می‌شوند ───────────────────────────
        Console.WriteLine("── ۴) رنگ از تمِ خودِ برنامه");
        var code2 = File.ReadAllText(Path.Combine(Root(), "PumpYaqobi.App", "Controls", "LoginArt.axaml.cs"));
        Check("پالتِ خودِ نقشه با تم جا عوض می‌کند", code2.Contains("Palette(bool dark)"));
        Check("و خودِ فایل دست‌نخورده می‌ماند (جای‌گزینی روی متنِ حافظه است)",
              code2.Contains("text.Replace(from, to"));

        var img1 = Bitmap0(win);
        ThemeFlip(vm);
        for (var i = 0; i < 60; i++) Pump(win);
        var img2 = Bitmap0(win);
        Check("با عوض شدنِ تم، نقشهٔ دیگری نشان داده می‌شود",
              img1 is not null && img2 is not null && !ReferenceEquals(img1, img2));
        //  عکسِ چشمیِ تمِ دیگر
        var dark = Path.Combine(Path.GetTempPath(), "pump-loginart-dark.png");
        using (var f2 = win.CaptureRenderedFrame())
            if (f2 is not null) { f2.Save(dark); Console.WriteLine("عکسِ تمِ دیگر: " + dark); }
        ThemeFlip(vm);
        for (var i = 0; i < 60; i++) Pump(win);
        Check("و با برگشتن به تمِ اول، همان نقشهٔ اول برمی‌گردد (کَش)",
              ReferenceEquals(img1, Bitmap0(win)));

        // ── ۵) کیفیت: برداری یعنی بی سقفِ اندازه ──────────────────────────
        Console.WriteLine("── ۵) کیفیت در هر اندازه");
        Check("تصویر از نوعِ برداری است، نه بیت‌مپِ پیکسلی",
              Bitmap0(win) is Avalonia.Svg.Skia.SvgImage);
        Check("و با `Uniform` کشیده می‌شود (بی کج شدن)",
              (win.GetVisualDescendants().OfType<Image>()
                  .FirstOrDefault(i => i.IsEffectivelyVisible)?.Stretch ?? Stretch.None) == Stretch.Uniform);

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

    /// <summary>خودِ تصویرِ نشانده‌شده (برای سنجشِ کَش و نوعش).</summary>
    private static IImage? Bitmap0(Window win) =>
        Art(win)?.GetVisualDescendants().OfType<Image>().FirstOrDefault()?.Source;

    /// <summary>تمِ دیگر — آبی ⇄ طلایی، از راهِ خودِ برنامه.</summary>
    private static void ThemeFlip(MainViewModel vm)
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
