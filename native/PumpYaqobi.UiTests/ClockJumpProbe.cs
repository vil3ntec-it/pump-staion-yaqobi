using System.Net;
using System.Net.Http;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using PumpYaqobi.App.Services;
using PumpYaqobi.App.ViewModels;
using PumpYaqobi.App.ViewModels.Sections;
using PumpYaqobi.App.Views;
using PumpYaqobi.Application.Localization;
using PumpYaqobi.Domain;

namespace PumpYaqobi.UiTests;

/// <summary>
/// ══ «وقتی تاریخ رو عوض می‌کنم تمامِ سیستم به هم می‌خوره» — با پنجرهٔ واقعی ══════
///
/// گزارشِ صاحب ریپو (۱۴۰۵/۰۷/۱۵). برنامه با اشتراکِ وی‌آی‌پی باز است؛ ساعتِ ویندوز
/// <b>وسطِ کار</b> یک سال جلو و بعد دو سال عقب می‌رود (‎AppClock.WallSource‎ — همان
/// چیزی که ویندوز به برنامه می‌دهد؛ شمارندهٔ یکنواختِ سیستم دست نمی‌خورد، همان‌طور
/// که روی کامپیوترِ واقعی). پس از هر جابه‌جایی، تیکِ واقعیِ پنجره می‌دود و بعد:
/// تاریخِ سربرگ، ماهِ بخش، ردیفِ تازه، قفل‌های اشتراک و کفِ ساعتِ مجوز سنجیده
/// می‌شوند. در آخر ساعتِ اینترنت می‌رسد و برنامه باید بگوید ساعتِ ویندوز چقدر
/// عقب است.
///
///     dotnet run --project PumpYaqobi.UiTests -c Release -- clockjump
/// </summary>
internal static class ClockJumpProbe
{
    private static int _bad;

    private static void Check(string what, bool ok, string? detail = null)
    {
        Console.WriteLine((ok ? "  ✔ " : "  ✖ ") + what + (detail is null ? "" : " — " + detail));
        if (!ok) _bad++;
    }

    public static int Run()
    {
        var tmpDb = Path.Combine(Path.GetTempPath(), "pump-clockjump-" + Guid.NewGuid().ToString("N"), "pump.db");
        var offset = TimeSpan.Zero;
        AppClock.WallSource = () => DateTime.UtcNow + offset;   // ساعتِ ویندوز، جابه‌جاشدنی

        AppHost.Start(tmpDb);
        if (AppHost.Current.Auth.NeedsFirstRun()) AppHost.Current.Auth.CreateFirstAdmin("1234");
        FakeLicense.Grant();
        Entitlements.Unlocked = false;

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
        Pump(win);

        var safe = vm.Sections.OfType<SafeSectionViewModel>().First();
        var dash = vm.Sections.OfType<DashboardSectionViewModel>().First();
        Wait(win, vm.GoAsync(safe));

        var today = vm.TodayText;
        var month = safe.Month;
        var todayShamsi = Shamsi.Today();
        Console.WriteLine($"پیش از جابه‌جایی: «{today.Trim('⁧', '⁩')}» · ماهِ گاوصندوق {month}");
        Check("تاریخِ سربرگ با اسلش است", today.Contains('/') && !today.Contains('.'), today.Trim('⁧', '⁩'));
        Check("اشتراکِ وی‌آی‌پی باز است", Entitlements.Allows(Entitlements.Dashboard));

        foreach (var (label, jump) in new[] { ("یک سال جلو", TimeSpan.FromDays(365)), ("دو سال عقب", TimeSpan.FromDays(-730)) })
        {
            offset = jump;
            Console.WriteLine($"── ساعتِ ویندوز {label}: {Shamsi.Of((DateTime.UtcNow + offset).ToLocalTime())} ──");
            //  تیکِ واقعیِ پنجره (ساعتِ سربرگ، نیمه‌شب) و حلقهٔ پس‌زمینه (کفِ مجوز)
            Tick(win, TimeSpan.FromSeconds(2.5));
            var f = AppSettings.Load();
            LicenseClock.Tick(f);

            Check("تاریخِ سربرگ همان ماند", vm.TodayText == today, vm.TodayText.Trim('⁧', '⁩'));
            Check("«امروز»ِ برنامه همان ماند", Shamsi.Today() == todayShamsi, Shamsi.Today());
            Check("ماهِ گاوصندوق همان ماند", safe.Month == month, safe.Month);

            var before = safe.Rows.Count;
            Wait(win, safe.AddRowCommand.ExecuteAsync(null));
            var row = safe.Rows.LastOrDefault();
            Check($"ردیفِ تازه در همین ماه نشست ({before}⇐{safe.Rows.Count})",
                  safe.Rows.Count == before + 1 && safe.Month == month);
            Check("تاریخِ ردیفِ تازه امروزِ واقعی است", row is not null && row.DateShamsi == todayShamsi, row?.DateShamsi);

            Check("اشتراکِ وی‌آی‌پی هنوز باز است (نه درجا تمام)", Entitlements.Allows(Entitlements.Dashboard));
            Check("مجوز هنوز سالم است", LicenseGuard.CheckStored(f).Valid, LicenseGuard.CheckStored(f).Reason);
            var floorDays = (f.ClockFloorMs - AppClock.UnixMs) / 86_400_000d;
            Check("کفِ ساعتِ مجوز با ساعتِ ویندوز جلو نرفت", floorDays < 1, $"{floorDays:0.0} روز جلوتر");

            Wait(win, vm.GoAsync(dash));
            Check("داشبورد (بخشِ اشتراکی) باز شد", ReferenceEquals(vm.Current, dash));
            Wait(win, vm.GoAsync(safe));
        }

        //  ── ساعتِ اینترنت می‌رسد؛ ویندوز هنوز دو سال عقب است ──────────────
        Console.WriteLine("── ساعتِ اینترنت رسید (سرورِ حساب، سرآیندِ Date) ──");
        var req = new HttpRequestMessage(HttpMethod.Get, CloudConfig.Url("/api/health"));
        var res = new HttpResponseMessage(HttpStatusCode.OK);
        res.Headers.Date = DateTimeOffset.UtcNow;
        TimeSync.From(req, res, AppClock.MonoSource());
        Tick(win, TimeSpan.FromSeconds(1.5));
        Check("برنامه ساعتِ اینترنت را گرفت", AppClock.Trusted);
        Check("سربرگ ⚠️ دارد", vm.ClockNote == "⚠️", vm.ClockNote);
        Check("راهنما می‌گوید ساعتِ ویندوز عقب است", vm.ClockTip.Contains("عقب است"), vm.ClockTip);
        Check("تاریخ همان تاریخِ واقعی ماند", vm.TodayText == today);

        offset = TimeSpan.Zero;
        Tick(win, TimeSpan.FromSeconds(1.5));
        Check("ساعتِ ویندوز درست شد ⇒ ⚠️ رفت", vm.ClockNote == "", vm.ClockNote);

        Console.WriteLine(_bad == 0 ? "\n✅ همه سبز" : $"\n✖ {_bad} ایراد");
        return _bad == 0 ? 0 : 1;
    }

    /// <summary>زمانِ واقعی می‌گذرد تا تایمرهای پنجره (هر ثانیه) واقعاً بزنند.</summary>
    private static void Tick(Window w, TimeSpan t)
    {
        var until = DateTime.UtcNow + t;
        while (DateTime.UtcNow < until)
        {
            Dispatcher.UIThread.Post(() => { }, DispatcherPriority.Background);
            Pump(w, 2);
            Thread.Sleep(40);
        }
    }

    private static void Pump(Window w, int n = 8)
    {
        for (var i = 0; i < n; i++) { Dispatcher.UIThread.RunJobs(); w.UpdateLayout(); }
    }

    private static void Wait(Window w, Task t)
    {
        for (var i = 0; i < 400 && !t.IsCompleted; i++) Pump(w);
        Pump(w);
    }
}
