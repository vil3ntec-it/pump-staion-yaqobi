using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PumpYaqobi.Services.Data;
using PumpYaqobi.App.Services;
using PumpYaqobi.App.ViewModels;
using PumpYaqobi.App.ViewModels.Sections;
using PumpYaqobi.App.Views;

namespace PumpYaqobi.UiTests;

/// <summary>
/// ══ «آن عکسِ آدم‌ها را فوری کن و برنامه را با یک عکس سنگین نکن» ═════════════
///
/// خواستهٔ صاحب ریپو (۱۴۰۵/۰۶/۲۹). پس این‌جا **عدد** می‌گیریم، نه اطمینانِ
/// حرفی:
///
///   ۱) خودِ فایل چند کیلوبایت است و باز کردن + بریدنش چند میلی‌ثانیه
///   ۲) از لحظهٔ رفتن به پروفایل تا دیده شدنِ عکس چند میلی‌ثانیه («فوری»)
///   ۳) بارِ دوم باید **صفر** کار باشد (یک بار برای همیشه)
///   ۴) چند مگابایت حافظه می‌گیرد و پس از بریدن چند پیکسل می‌ماند
///   ۵) هیچ دستورِ دیتابیسی نمی‌زند
///   ۶) و پردهٔ لودینگ (بارگذاریِ بخش) هیچ‌وقت لمسش نمی‌کند
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

        // ── ۱) خودِ فایل و هزینهٔ خامِ باز کردنش ───────────────────────────
        Console.WriteLine("── ۱) خودِ فایل");
        long bytes;
        using (var s = AssetLoader.Open(new Uri("avares://PumpYaqobi/Assets/login-art.jpg")))
        {
            using var ms = new MemoryStream();
            s.CopyTo(ms);
            bytes = ms.Length;
        }
        Check("فایل زیرِ ۱۵۰ کیلوبایت است", bytes <= 150 * 1024, $"{bytes / 1024.0:0.0} KB");

        // ── ۲) «فوری»: از رفتن به پروفایل تا دیده شدنِ عکس ────────────────
        Console.WriteLine("── ۲) از رفتن به پروفایل تا دیده شدنِ عکس");
        var db0 = DbWatch.Count;
        var mem0 = GC.GetTotalMemory(true);
        //  ⚠️ **همان لحظه‌ای که عکس می‌نشیند** سنجیده می‌شود، نه تهِ
        //  `GoAsync`: اگر منتظرِ تمام شدنِ کلِ باز شدنِ صفحه بمانیم، وقتِ
        //  خواندنِ ردیف‌های تب‌ها هم داخلش می‌آید و عددِ عکس دروغ می‌شود.
        var sw = Stopwatch.StartNew();
        var go = vm.GoAsync(account);
        long firstMs = -1;
        for (var i = 0; i < 4000 && (!go.IsCompleted || firstMs < 0); i++)
        {
            Dispatcher.UIThread.RunJobs();
            win.UpdateLayout();
            if (firstMs < 0 && account.LoginArt is not null) firstMs = sw.ElapsedMilliseconds;
            if (go.IsCompleted && firstMs >= 0) break;
            Thread.Sleep(1);
        }
        var pageMs = sw.ElapsedMilliseconds;
        if (go.IsFaulted) throw go.Exception!;
        var mem1 = GC.GetTotalMemory(true);
        var dbSpent = DbWatch.Count - db0;

        Check("عکس آمد", account.LoginArt is not null);
        Check("و با همان اولین باز شدنِ صفحه آمد، نه چند لحظه بعد",
              firstMs >= 0 && firstMs <= pageMs, $"عکس {firstMs}ms · صفحه {pageMs}ms");
        Note("کلِ باز شدنِ صفحه (ساختِ صفحه + عکس + ردیف‌های تب‌ها)", $"{pageMs}ms");
        //  ⚠️ «عکس زودتر از ردیف‌ها می‌آید» را **از روی ترتیبِ خودِ کد** قفل
        //  می‌کنیم، نه از روی میلی‌ثانیه: در اجرای بی‌پنجره هر دو داخلِ همان
        //  یک `GoAsync` می‌نشینند و ساعت نمی‌تواند جدایشان کند (قاعدهٔ
        //  «عددِ ساختاری دروغ نمی‌گوید»).

        if (account.LoginArt is Avalonia.Media.IImage img)
            Note("اندازهٔ عکسِ بریده", $"{img.Size.Width:0}×{img.Size.Height:0}");
        Note("حافظه", $"{(mem1 - mem0) / 1024.0 / 1024.0:0.0} MB برای کلِ باز شدنِ صفحه");
        //  ⚠️ این عدد مالِ **خودِ صفحهٔ پروفایل** است (کارمندان، تاریخچه،
        //  پشتیبان‌ها)، نه مالِ عکس. سهمِ عکس از دیتابیس در بندِ ۴ سنجیده
        //  می‌شود — از روی خودِ مسیرِ خواندنش.
        Note("دستورِ دیتابیسِ خودِ صفحهٔ پروفایل", $"{dbSpent} دستور (کارمندان · تاریخچه · پشتیبان‌ها)");

        // ── ۳) بارِ دوم: هیچ کاری نباید بشود ──────────────────────────────
        Console.WriteLine("── ۳) بارِ دوم — یک بار برای همیشه");
        var first = account.LoginArt;
        var other = vm.Sections.First(s => s.Id == "debt");
        Wait(win, vm.GoAsync(other));
        for (var i = 0; i < 20; i++) Pump(win);

        var mem2 = GC.GetTotalMemory(true);
        sw.Restart();
        Wait(win, vm.GoAsync(account));
        Pump(win);
        var againMs = sw.ElapsedMilliseconds;
        var mem3 = GC.GetTotalMemory(true);

        Check("همان شیءِ عکس است، نه یک عکسِ تازه", ReferenceEquals(first, account.LoginArt));
        Note("بارِ دومِ کلِ صفحه", $"{againMs}ms (عکس دیگر خوانده نمی‌شود)");
        Check("و حافظهٔ تازه‌ای نگرفت", mem3 - mem2 < 1024 * 1024,
              $"{(mem3 - mem2) / 1024.0:0} KB");

        // ── ۴) پردهٔ لودینگ عکس را لمس نمی‌کند ────────────────────────────
        Console.WriteLine("── ۴) بارگذاریِ بخش (پردهٔ لودینگ) عکس را نمی‌خواند");
        var src = File.ReadAllText(Path.Combine(Root(), "PumpYaqobi.App", "ViewModels",
                                                "Sections", "AccountSectionViewModel.cs"));
        var load = Between(src, "protected override async Task LoadAsync", "protected override");
        Check("در ‎LoadAsync‎ هیچ نامی از عکس نیست", !load.Contains("Art"),
              load.Contains("Art") ? "پردهٔ لودینگ عکس را می‌خواند" : "");

        //  «فوری» به شکلِ ساختاری: عکس پیش از ردیف‌های تب‌ها (سه پرس‌وجو)
        var act = Between(src, "public override async Task OnActivatedAsync", "/// <summary>پوشهٔ پشتیبان‌ها");
        var iArt = act.IndexOf("await LoadArtAsync", StringComparison.Ordinal);
        var iRows = act.IndexOf("await LoadRowsAsync", StringComparison.Ordinal);
        Check("عکس **پیش از** ردیف‌های تب‌ها خوانده می‌شود", iArt > 0 && iRows > iArt,
              iArt < 0 || iRows < 0 ? "یکی‌شان پیدا نشد" : $"عکس در {iArt} · ردیف‌ها در {iRows}");
        Check("و خواندنش فقط روی نخِ دیگر است", src.Contains("Task.Run"));
        Check("و عکس ‎static‎ است (یک بار برای همیشه)", src.Contains("private static IImage? _art"));

        //  سهمِ خودِ عکس از دیتابیس: مسیرِ خواندنش هیچ سرویسی را صدا نمی‌زند
        var art = Between(src, "private async Task LoadArtAsync", "LoadRowsAsync");
        Check("مسیرِ خواندنِ عکس هیچ سرویس یا دیتابیسی را لمس نمی‌کند",
              art.Length > 0 && !art.Contains("_host.") && !art.Contains("Service"),
              art.Length == 0 ? "مسیرِ خواندن پیدا نشد" : "");

        // ── ۵) خودِ باز کردن و بریدن، جدا از هر چیزِ دیگر ──────────────────
        Console.WriteLine("── ۵) خودِ باز کردن و بریدن، جدا");
        sw.Restart();
        using (var s = AssetLoader.Open(new Uri("avares://PumpYaqobi/Assets/login-art.jpg")))
        {
            var full = Bitmap.DecodeToWidth(s, 736);
            var open = sw.ElapsedMilliseconds;
            sw.Restart();
            var crop = new CroppedBitmap(full, new PixelRect(52, 86, 368, 396));
            var cut = sw.ElapsedMilliseconds;
            Note("باز کردنِ JPEG", $"{open}ms");
            Note("بریدن", $"{cut}ms");
            Check("باز کردن زیرِ ۱۰۰ میلی‌ثانیه است", open < 100, $"{open}ms");
            Check("بریدن تقریباً بی‌هزینه است (پوشش، نه کپی)", cut <= 5, $"{cut}ms");
            crop.Dispose();
            full.Dispose();
        }

        // ── ۶) و بی‌کاریِ صفحهٔ ورود: عکس چیزی را نمی‌چرخاند ───────────────
        Console.WriteLine("── ۶) صفحهٔ ورودِ باز، سه ثانیه بی‌کار");
        Wait(win, vm.GoAsync(account));
        for (var i = 0; i < 30; i++) Pump(win);
        var layouts = 0;
        void OnLayout(object? _, EventArgs __) => layouts++;
        win.LayoutUpdated += OnLayout;
        var idle = Stopwatch.StartNew();
        while (idle.ElapsedMilliseconds < 3000) { Dispatcher.UIThread.RunJobs(); Thread.Sleep(10); }
        win.LayoutUpdated -= OnLayout;
        Check("هیچ چیدمانی در بی‌کاری رخ نداد", layouts == 0, $"{layouts} چیدمان");

        Console.WriteLine();
        Console.WriteLine(_bad == 0
            ? "✅ عکس فوری می‌آید و برنامه را سنگین نمی‌کند"
            : $"❌ {_bad} ایراد");
        return _bad == 0 ? 0 : 1;
    }

    private static string Root()
    {
        var d = AppContext.BaseDirectory;
        while (d is not null && !File.Exists(Path.Combine(d, "PumpYaqobi.sln")))
            d = Path.GetDirectoryName(d);
        return d ?? ".";
    }

    private static string Between(string src, string from, string to)
    {
        var a = src.IndexOf(from, StringComparison.Ordinal);
        if (a < 0) return "";
        var b = src.IndexOf(to, a + from.Length, StringComparison.Ordinal);
        return b < 0 ? src[a..] : src[a..b];
    }

    private static void Pump(Window w)
    {
        for (var i = 0; i < 8; i++) { Dispatcher.UIThread.RunJobs(); w.UpdateLayout(); }
    }

    private static void Wait(Window win, Task t)
    {
        for (var i = 0; i < 4000 && !t.IsCompleted; i++) { Dispatcher.UIThread.RunJobs(); Thread.Sleep(2); }
        Dispatcher.UIThread.RunJobs();
        if (t.IsFaulted) throw t.Exception!;
    }
}
