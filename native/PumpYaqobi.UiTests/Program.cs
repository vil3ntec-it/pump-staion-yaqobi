using Avalonia.VisualTree;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Threading;
using PumpYaqobi.App.Themes;
using PumpYaqobi.App.ViewModels;
using PumpYaqobi.App.Views;

namespace PumpYaqobi.UiTests;

/// <summary>
/// ══ عکس‌گیرِ بی‌نمایشگر ═════════════════════════════════════════════════════
/// همان پنجرهٔ واقعیِ برنامه را با موتورِ رسمِ Skia می‌سازد و PNG می‌گیرد.
/// این‌طور هر بخشی که تحویل می‌دهیم، پیش از تحویل با چشم دیده شده است.
///
///     dotnet run --project PumpYaqobi.UiTests -- shots
/// </summary>
internal static class Program
{
    public static int Main(string[] args)
    {
        var outDir = args.Length > 0 ? args[0] : "shots";

        // ══ حالتِ «سنجشِ اسکرول» ═════════════════════════════════════════════
        //     dotnet run --project PumpYaqobi.UiTests -- scroll
        //
        // گزارشِ صاحب ریپو: «جز جدول‌ها دیگر هیچ چیزی اسکرول نمی‌شود». این
        // حالت به‌جای حدس زدن، در همان پنجرهٔ واقعی و در کوچک‌ترین اندازهٔ
        // مجاز، بخش‌به‌بخش می‌سنجد که محتوا از پنجره بلندتر است یا نه و آیا
        // اصلاً راهی برای رسیدن به بخشِ بیرون‌افتاده هست.
        if (outDir.Equals("scroll", StringComparison.OrdinalIgnoreCase)) return ScrollAudit();
        // ══ حالتِ «سنجشِ صفحهٔ حسابِ قرض‌دار» ═══════════════════════════════
        //     dotnet run --project PumpYaqobi.UiTests -- person
        // چرایی و کارش در ‎PersonAudit‎ نوشته شده.
        if (outDir.Equals("person", StringComparison.OrdinalIgnoreCase)) return PersonAudit.Run();
        // ══ حالتِ «سنجشِ پارچه‌ها و ورق‌ها» ═══════════════════════════════
        //     dotnet run --project PumpYaqobi.UiTests -- parcha
        if (outDir.Equals("parcha", StringComparison.OrdinalIgnoreCase)) return ParchaWaraqAudit.Run();
        // ══ حالتِ «کند نشدن با دادهٔ بزرگ» ═════════════════════════════════
        //     dotnet run --project PumpYaqobi.UiTests -- perf
        // ده هزار قرض‌دار و سیصد هزار ردیف (صدهزارتا در یک حساب)، بعد
        // زمان‌گیریِ کارهای روزمره. چرایی‌اش در ‎PerfAudit‎ نوشته شده.
        if (outDir.Equals("perf", StringComparison.OrdinalIgnoreCase)) return PerfAudit.Run();
        // ══ حالتِ «پنجره‌های گفت‌وگو» ══════════════════════════════════════
        //     dotnet run --project PumpYaqobi.UiTests -- dialogs
        // چرایی‌اش در ‎DialogAudit‎ نوشته شده: تنها مسیری که هیچ سنجشی نداشت.
        if (outDir.Equals("dialogs", StringComparison.OrdinalIgnoreCase)) return DialogAudit.Run();
        // ══ حالتِ «چرا این سه بخش دیر باز می‌شوند» ═════════════════════════
        //     dotnet run --project PumpYaqobi.UiTests -- ledgerperf
        // گاوصندوق، صرافی و مصارف با دادهٔ واقعاً دیده‌شونده. چرایی‌اش در
        // ‎LedgerPerf‎ نوشته شده — ‎PerfAudit‎ این سه را **خالی** می‌سنجید.
        if (outDir.Equals("ledgerperf", StringComparison.OrdinalIgnoreCase)) return LedgerPerf.Run();
        // ══ حالتِ «با زیاد شدنِ جدول کند می‌شود» ══════════════════════════
        //     dotnet run --project PumpYaqobi.UiTests -c Release -- bigtable
        // یک دفتر با شش اندازه (۵۰ تا ۲۰٬۰۰۰ ردیف) و چهار عدد برای هرکدام.
        // چرایی‌اش در ‎BigTable‎ نوشته شده.
        // ══ حالتِ «بخشِ پرینت دیر باز می‌شود» ══════════════════════════════
        //     dotnet run --project PumpYaqobi.UiTests -c Release -- printperf
        if (outDir.Equals("printperf", StringComparison.OrdinalIgnoreCase)) return PrintPerf.Run();
        // ══ حالتِ «رفتن داخلِ حساب و برگشتن، هر دو کند است» ═════════════════
        //     dotnet run --project PumpYaqobi.UiTests -c Release -- enterperf
        if (outDir.Equals("enterperf", StringComparison.OrdinalIgnoreCase)) return EnterPerf.Run();

        // ⟦ ته اسکرول باید بایستد، نه پرپر بزند ⟧
        //     dotnet run --project PumpYaqobi.UiTests -c Release -- scrollend
        if (outDir.Equals("scrollend", StringComparison.OrdinalIgnoreCase)) return ScrollEnd.Run();
        if (outDir.Equals("bigtable", StringComparison.OrdinalIgnoreCase))
        {
            BigTable.Trace = args.Length > 1 && args[1].Equals("trace", StringComparison.OrdinalIgnoreCase);
            if (args.Length > 2 && args[1].Equals("shot", StringComparison.OrdinalIgnoreCase)) BigTable.Shots = args[2];
            return BigTable.Run();
        }
        // ══ حالتِ «سرِ جدول و جملهٔ زیرش» ═══════════════════════════════════
        //     dotnet run --project PumpYaqobi.UiTests -- chrome
        if (outDir.Equals("chrome", StringComparison.OrdinalIgnoreCase)) return TableChromeAudit.Run();
        // ══ حالتِ «کلیدِ چپ و راست هنگامِ تایپ» ═════════════════════════════
        //     dotnet run --project PumpYaqobi.UiTests -- keys
        if (outDir.Equals("keys", StringComparison.OrdinalIgnoreCase)) return KeyAudit.Run();
        if (outDir.Equals("look", StringComparison.OrdinalIgnoreCase)) return LookAudit.Run();
        if (outDir.Equals("cells", StringComparison.OrdinalIgnoreCase)) return CellEditAudit.Run();
        if (outDir.Equals("waraqperf", StringComparison.OrdinalIgnoreCase)) return WaraqPerf.Run();
        // ══ «یک ردیف چقدر آب می‌خورد» — ریشهٔ کندیِ ورق ════════════════════
        //     dotnet run --project PumpYaqobi.UiTests -- rowcost
        if (outDir.Equals("rowcost", StringComparison.OrdinalIgnoreCase)) return RowCost.Run();
        if (outDir.Equals("vanish", StringComparison.OrdinalIgnoreCase)) return VanishProbe.Run();
        if (outDir.Equals("verify", StringComparison.OrdinalIgnoreCase)) return VerifyProbe.Run();
        //  «تست بزن ببین لاگین می‌شود، کد بزنی چه می‌شود، حساب ساخته می‌شود یا نه»
        if (outDir.Equals("cloudlogin", StringComparison.OrdinalIgnoreCase)) return CloudLoginProbe.Run();
        //  «تو کادرها + @ # ﷼ ( ) ؟ ؛ : , . نوشته نمی‌شوند»
        if (outDir.Equals("inputchars", StringComparison.OrdinalIgnoreCase)) return InputCharsProbe.Run();

        //  «آن عکسِ آدم‌ها را فوری کن و برنامه را با یک عکس سنگین نکن»
        if (outDir.Equals("loginart", StringComparison.OrdinalIgnoreCase)) return LoginArtProbe.Run();
        //  «سرور روشن است اما برنامه می‌گوید خاموش»
        if (outDir.Equals("serverdot", StringComparison.OrdinalIgnoreCase)) return ServerDotProbe.Run();
        // ══ «عوض کردنِ تم خط‌ها را کج می‌کند؟» ══════════════════════════════
        //     dotnet run --project PumpYaqobi.UiTests -- themeflip
        if (outDir.Equals("themeflip", StringComparison.OrdinalIgnoreCase)) return ThemeFlipAudit.Run();
        // ══ «موقعِ اسکرول لگ می‌زند؟» ═════════════════════════════════════════
        //     dotnet run --project PumpYaqobi.UiTests -- scrollperf
        // ══ «بخشی که تویش نیستم هیچ مصرفی نداشته باشد» ═══════════════════════
        //     dotnet run --project PumpYaqobi.UiTests -- idle
        if (outDir.Equals("idle", StringComparison.OrdinalIgnoreCase)) return IdleAudit.Run();
        if (outDir.Equals("scrollperf", StringComparison.OrdinalIgnoreCase))
        {
            ScrollPerf.Why = args.Length > 1 && args[1].Equals("why", StringComparison.OrdinalIgnoreCase);
            ScrollPerf.Trace = args.Length > 1 && args[1].Equals("trace", StringComparison.OrdinalIgnoreCase);
            return ScrollPerf.Run();
        }
        if (args.Length > 1 && args[0].Equals("themediff", StringComparison.OrdinalIgnoreCase))
            return ThemeFlipAudit.RunDiff(args[1], args.Length > 2 ? args[2] : null);
        // ══ «پنج سال استفاده از اپ» — کندی و باگ با دفترِ پنج‌ساله ═══════════
        //     dotnet run --project PumpYaqobi.UiTests -- years
        if (outDir.Equals("years", StringComparison.OrdinalIgnoreCase)) return YearsAudit.Run();
        // ══ «برنامه چرا دیر باز می‌شود؟» — اجرای سرد، مرحله به مرحله ═════════
        //     dotnet run --project PumpYaqobi.UiTests -- startup
        if (outDir.Equals("startup", StringComparison.OrdinalIgnoreCase)) return StartupAudit.Run();
        if (outDir.Equals("plprof", StringComparison.OrdinalIgnoreCase)) return YearsAudit.RunPriceLossProfile();
        // ══ «پردهٔ لودینگ کارش را می‌کند؟» ═════════════════════════════════
        //     dotnet run --project PumpYaqobi.UiTests -- warm
        if (outDir.Equals("warm", StringComparison.OrdinalIgnoreCase)) return WarmAudit.Run();
        // ══ «بازگشت به صفحهٔ اصلی کند است» — با دفترِ چندساله ═══════════════
        //     dotnet run --project PumpYaqobi.UiTests -- dashperf
        if (outDir.Equals("dashperf", StringComparison.OrdinalIgnoreCase)) return DashPerf.Run();
        // ══ عکسِ صفحهٔ چاپ و «تنظیمِ ورق» با گزارشِ واقعی ══════════════════════
        //     dotnet run --project PumpYaqobi.UiTests -- printshot <پوشه>
        if (args.Length > 1 && args[0].Equals("printshot", StringComparison.OrdinalIgnoreCase)) return PrintShot.Run(args[1]);
        if (args.Length > 1 && args[0].Equals("cellshot", StringComparison.OrdinalIgnoreCase)) return CellShot.Run(args[1]);
        // ══ سه صفحهٔ بخشِ فاکتور، بلند و کامل ═══════════════════════════════
        //     dotnet run --project PumpYaqobi.UiTests -- invshot <پوشه>
        if (args.Length > 1 && args[0].Equals("invshot", StringComparison.OrdinalIgnoreCase)) return InvoiceShot.Run(args[1]);
        // ══ عکسِ سه حالتِ آغاز: لودینگ، قفل، صفحهٔ اصلی ═════════════════════
        //     dotnet run --project PumpYaqobi.UiTests -- startshot <پوشه>
        if (args.Length > 1 && args[0].Equals("startshot", StringComparison.OrdinalIgnoreCase)) return InvoiceShot.Startup(args[1]);
        if (outDir.Equals("gridperf", StringComparison.OrdinalIgnoreCase)) return GridPerf.Run();
        if (outDir.Equals("cardperf", StringComparison.OrdinalIgnoreCase)) return GridPerf.Cards();

        Directory.CreateDirectory(outDir);

        // دیتابیسِ موقت — عکس‌گیری هرگز به دادهٔ واقعیِ کاربر دست نمی‌زند
        var tmpDb = Path.Combine(Path.GetTempPath(), "pump-shots-" + Guid.NewGuid().ToString("N"), "pump.db");
        PumpYaqobi.App.Services.AppHost.Start(tmpDb);

        AppBuilder.Configure<PumpYaqobi.App.App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .SetupWithoutStarting();

        var win = new MainWindow { Width = 1440, Height = 900 };
        win.Show();
        Pump(win);

        // ۱) صفحهٔ قفل — همان چیزی که کاربر اول می‌بیند
        Shot(win, Path.Combine(outDir, "00-lock.png"));

        // ۲) ورود با رمزِ نخستین اجرا، سپس هر تم یک عکس
        var vm = (MainViewModel)win.DataContext!;
        vm.Lock.Password = "1234";
        vm.Lock.Confirm = "1234";
        vm.Lock.SubmitCommand.Execute(null);
        Wait(win, Task.CompletedTask);
        Pump(win);

        Seed.Fill(PumpYaqobi.App.Services.AppHost.Current);

        // داشبورد پیش از پر شدنِ دیتابیس ساخته شده بود — یک‌بار از نو بخواند
        if (vm.Sections.FirstOrDefault(s => s.Id == "dashboard")
            is PumpYaqobi.App.ViewModels.Sections.DashboardSectionViewModel dash)
            Wait(win, dash.RefreshAsync());

        // ۳) هر تم یک عکس از داشبورد — و یک عکسِ چهارتایی (داشبورد، مخزن، شرکت‌ها،
        //    تنظیمات) تا صاحب ریپو تم‌ها را کنارِ هم ببیند و یکی را برگزیند
        foreach (var theme in PumpTheme.All)
        {
            ThemeManager.Apply(theme);
            Pump(win);
            Shot(win, Path.Combine(outDir, "theme-" + theme.Id + ".png"));

            var parts = new List<string>();
            foreach (var id in new[] { "dashboard", "storage", "noinv", "settings" })
            {
                if (vm.Sections.FirstOrDefault(s => s.Id == id) is not { } sec) continue;
                Wait(win, vm.GoAsync(sec));
                Pump(win); Dispatcher.UIThread.RunJobs(); Pump(win);
                var f = Path.Combine(outDir, "theme-" + theme.Id + "-" + id + ".png");
                Shot(win, f);
                parts.Add(f);
            }
            ThemeShot.Compose(parts, Path.Combine(outDir, "theme-" + theme.Id + "-4up.png"));
            foreach (var f in parts) File.Delete(f);
        }
        // ‎PUMP_SHOT_THEME=gold‎ ⇒ همهٔ عکس‌های بخش‌ها با تمِ تیره — برای دیدنِ باگ‌های دارک
        ThemeManager.Apply(string.Equals(Environment.GetEnvironmentVariable("PUMP_SHOT_THEME"), "gold", StringComparison.OrdinalIgnoreCase)
            ? PumpTheme.Gold : PumpTheme.Blue);
        if (vm.Sections.FirstOrDefault(s => s.Id == "dashboard") is { } home) Wait(win, vm.GoAsync(home));

        // ۴) هر بخش یک عکس — چیزی تحویل نمی‌دهیم که ندیده باشیم
        var n = 0;
        foreach (var sec in vm.Sections)
        {
            Wait(win, vm.GoAsync(sec));
            Pump(win);
            Dispatcher.UIThread.RunJobs();
            Pump(win);
            Shot(win, Path.Combine(outDir, $"{++n:00}-{sec.Id}.png"));
        }

        // ۴٫۵) زیربخش‌ها — «قرض‌های کهنه»، «تخلیهٔ تانکر»، «مدیریت داده‌ها» و
        //      مانندِ آن‌ها دکمهٔ نوار ندارند و از دلِ بخشِ خودشان باز می‌شوند.
        //      بی این حلقه، هیچ عکسی از آن‌ها گرفته نمی‌شد.
        foreach (var parent in vm.Sections)
        {
            foreach (var sub in parent.SubSections)
            {
                Wait(win, vm.GoAsync(parent));
                parent.OpenSub = sub;
                Pump(win);
                Dispatcher.UIThread.RunJobs();
                Pump(win);
                Shot(win, Path.Combine(outDir, $"{++n:00}-sub-{sub.Id}.png"));
                parent.OpenSub = null;
            }
        }

        // ۴٫۵ب) خودِ پروفایل — نه صفحهٔ ورود
        //
        // ⚠️ عکسِ ۲۰ عمداً صفحهٔ **ورود** است، چون دیتابیسِ آزمون حسابی ندارد
        // و همان چیزی است که کاربرِ تازه می‌بیند. ولی صفحهٔ پروفایل هم باید
        // دیده شود، پس همان‌جا «بعداً» را می‌زنیم و دوباره عکس می‌گیریم.
        if (vm.Sections.FirstOrDefault(s => s.Id == "account")
            is PumpYaqobi.App.ViewModels.Sections.AccountSectionViewModel acc)
        {
            Wait(win, vm.GoAsync(acc));
            acc.SkipPumpCommand.Execute(null);
            Pump(win);
            Dispatcher.UIThread.RunJobs();
            Pump(win);
            Shot(win, Path.Combine(outDir, "20b-account-profile.png"));
            acc.OpenAccountPageCommand.Execute(null);
        }

        // ۴٫۶) صفحهٔ جزئیاتِ «زیان ناشی از افزایش قیمت» — دو جدولِ برداشت‌ها و فاکتورها
        if (vm.Sections.FirstOrDefault(s => s.Id == "debt") is { } debtSec
            && debtSec.SubSections.OfType<PumpYaqobi.App.ViewModels.Sections.PriceLossSectionViewModel>().FirstOrDefault() is { } pl)
        {
            Wait(win, vm.GoAsync(debtSec));
            debtSec.OpenSub = pl;
            Pump(win);
            Dispatcher.UIThread.RunJobs();
            Pump(win);
            pl.OpenRowCommand.Execute(pl.Rows.FirstOrDefault());
            Pump(win);
            Dispatcher.UIThread.RunJobs();
            Pump(win);
            Shot(win, Path.Combine(outDir, $"{++n:00}-priceloss-person.png"));
            pl.ClosePerson();
            debtSec.OpenSub = null;
        }

        // ۵) صفحهٔ حسابِ یک قرض‌دار — مهم‌ترین صفحهٔ برنامه
        if (vm.Sections.FirstOrDefault(s => s.Id == "debt") is PumpYaqobi.App.ViewModels.Sections.DebtSectionViewModel debt)
        {
            Wait(win, vm.GoAsync(debt));
            Pump(win);
            Wait(win, debt.RefreshAsync());
            debt.OpenCommand.Execute(debt.Cards.FirstOrDefault());
            Pump(win);
            Dispatcher.UIThread.RunJobs();
            Pump(win);
            Shot(win, Path.Combine(outDir, "20-debt-person.png"));

            // «جدول جدید» → صفحهٔ جدول‌های آرشیو، با سربرگِ حسابِ زنده و ردیف‌های ویرایش‌شدنی
            if (debt.Person?.Current is { } cur)
            {
                PumpYaqobi.App.Services.Dialogs.ConfirmHook = (_, _) => true;
                Wait(win, cur.NewTableCommand.ExecuteAsync(null));
                PumpYaqobi.App.Services.Dialogs.ConfirmHook = null;
                Wait(win, cur.ToggleArchivesCommand.ExecuteAsync(null));
                Pump(win); Dispatcher.UIThread.RunJobs(); Pump(win);
                Shot(win, Path.Combine(outDir, "20b-debt-archive.png"));
                Wait(win, debt.CloseOverlayAsync());
                Pump(win);
            }
        }

        if (vm.Sections.FirstOrDefault(s => s.Id == "noinv") is PumpYaqobi.App.ViewModels.Sections.CompanySectionViewModel comp)
        {
            Wait(win, vm.GoAsync(comp));
            Pump(win);
            Wait(win, comp.RefreshAsync());
            comp.OpenCommand.Execute(comp.Cards.FirstOrDefault());
            Pump(win);
            Dispatcher.UIThread.RunJobs();
            Pump(win);
            Shot(win, Path.Combine(outDir, "21-company-page.png"));

            // صفحه‌های روییِ حساب: خریدها، جدول‌های آرشیو (بعد از «جدول جدید»)، جستجوی خرید
            if (comp.Page is { } cp)
            {
                Wait(win, comp.OpenPurchasesAsync(cp.Entity, null, null));
                Pump(win); Dispatcher.UIThread.RunJobs(); Pump(win);
                Shot(win, Path.Combine(outDir, "21b-company-purchases.png"));
                comp.CloseOverlay(); Pump(win);

                PumpYaqobi.App.Services.Dialogs.ConfirmHook = (_, _) => true;
                Wait(win, cp.NewTableCommand.ExecuteAsync(null));
                PumpYaqobi.App.Services.Dialogs.ConfirmHook = null;
                Pump(win); Dispatcher.UIThread.RunJobs(); Pump(win);
                Shot(win, Path.Combine(outDir, "21c-company-after-newtable.png"));
                Wait(win, comp.OpenArchiveAsync(cp.Entity, cp.Fuel));
                Pump(win); Dispatcher.UIThread.RunJobs(); Pump(win);
                Shot(win, Path.Combine(outDir, "21d-company-archive.png"));
                comp.CloseOverlay(); Pump(win);

                Wait(win, comp.OpenSearchAsync(cp.Entity.Id));
                if (comp.Overlay is PumpYaqobi.App.ViewModels.Sections.CompanySearchPageViewModel sp)
                {
                    sp.QtyText = "20";
                    Wait(win, sp.RunCommand.ExecuteAsync(null));
                }
                Pump(win); Dispatcher.UIThread.RunJobs(); Pump(win);
                Shot(win, Path.Combine(outDir, "21e-company-search.png"));
                comp.CloseOverlay(); Pump(win);
            }
        }

        if (vm.Sections.FirstOrDefault(s => s.Id == "waraq") is PumpYaqobi.App.ViewModels.Sections.WaraqSectionViewModel wq)
        {
            Wait(win, vm.GoAsync(wq));
            Pump(win);
            Wait(win, wq.ReloadAsync());
            wq.OpenCommand.Execute(wq.Sheets.FirstOrDefault());
            Pump(win);
            Dispatcher.UIThread.RunJobs();
            Pump(win);
            Shot(win, Path.Combine(outDir, "22-waraq-page.png"));
        }

        if (vm.Sections.FirstOrDefault(s => s.Id == "shifts") is PumpYaqobi.App.ViewModels.Sections.ParchaSectionViewModel pr)
        {
            Wait(win, vm.GoAsync(pr));
            Pump(win);
            Dispatcher.UIThread.RunJobs();
            Pump(win);
            Shot(win, Path.Combine(outDir, "23-parcha.png"));
        }

        if (vm.Sections.FirstOrDefault(s => s.Id == "amanat")
            is PumpYaqobi.App.ViewModels.Sections.AmanatSectionViewModel am)
        {
            Wait(win, vm.GoAsync(am));
            Pump(win);
            // «⚙️ تنظیمات مدیر» — کادرِ ضریب‌ها باز، عکس، بسته
            am.ToggleSettingsCommand.Execute(null);
            Pump(win);
            Dispatcher.UIThread.RunJobs();
            Pump(win);
            Shot(win, Path.Combine(outDir, "24-amanat-settings.png"));
            am.ToggleSettingsCommand.Execute(null);
            Pump(win);
            am.OpenCommand.Execute(am.Cards.FirstOrDefault());
            Pump(win);
            Dispatcher.UIThread.RunJobs();
            Pump(win);
            Shot(win, Path.Combine(outDir, "24-amanat-account.png"));
        }

        var failures = CheckShortcuts(win, vm);

        if (Environment.GetEnvironmentVariable("PUMP_PROBE") == "1"
            && vm.Sections.FirstOrDefault(s => s.Id == "chakana") is { } ck)
        { Wait(win, vm.GoAsync(ck)); Pump(win); }

        // ابزارِ عیب‌یابی: پهنای واقعیِ ستون‌های جدولِ چکنه
        if (Environment.GetEnvironmentVariable("PUMP_PROBE") == "1")
        {
            foreach (var g in win.GetVisualDescendants().OfType<Avalonia.Controls.DataGrid>())
            {
                Console.WriteLine("— جدول —");
                foreach (var c in g.Columns)
                    Console.WriteLine($"   [{c.Header}] w={c.ActualWidth:0} type={c.GetType().Name}");
                break;
            }
        }

        Console.WriteLine("عکس‌ها در: " + Path.GetFullPath(outDir));
        return failures;
    }



    /// <summary>
    /// ══ آزمونِ میانبرها روی پنجرهٔ واقعی ═══════════════════════════════════
    /// آزمون‌های ‎PumpYaqobi.Tests‎ قرارداد را می‌سنجند (‎IRowBatchHost‎ و…)، ولی
    /// نه سیم‌کشی‌اش را: این‌که رویدادِ کلید اصلاً به ‎ShortcutService‎ می‌رسد،
    /// که بافرِ چندرقمی درست جمع می‌شود، و که کار دقیقاً هنگامِ <b>رها شدنِ</b>
    /// کلید انجام می‌گیرد. آن‌ها فقط با کلیدِ واقعی روی پنجرهٔ واقعی ثابت
    /// می‌شوند — و همین‌جا می‌شود.
    /// </summary>
    private static int CheckShortcuts(Window win, MainViewModel vm)
    {
        var bad = 0;
        void Check(string what, bool ok)
        {
            Console.WriteLine((ok ? "  ✔ " : "  ✖ ") + what);
            if (!ok) bad++;
        }

        Console.WriteLine("── میانبرهای صفحه‌کلید ──");

        // ── Ctrl+Shift+3 → بخشِ سوم (ورق‌های روزانه) ──
        win.KeyPressQwerty(PhysicalKey.Digit3, RawInputModifiers.Control | RawInputModifiers.Shift);
        win.KeyReleaseQwerty(PhysicalKey.Digit3, RawInputModifiers.Control | RawInputModifiers.Shift);
        win.KeyReleaseQwerty(PhysicalKey.ControlLeft, RawInputModifiers.Shift);
        win.KeyReleaseQwerty(PhysicalKey.ShiftLeft, RawInputModifiers.None);
        Pump(win);
        Check("Ctrl+Shift+3 → " + vm.Current?.Id, vm.Current?.Id == vm.Sections[2].Id);

        // ── عددِ دو رقمی: Ctrl+Shift+1 سپس 2 → بخشِ دوازدهم ──
        win.KeyPressQwerty(PhysicalKey.Digit1, RawInputModifiers.Control | RawInputModifiers.Shift);
        win.KeyReleaseQwerty(PhysicalKey.Digit1, RawInputModifiers.Control | RawInputModifiers.Shift);
        win.KeyPressQwerty(PhysicalKey.Digit2, RawInputModifiers.Control | RawInputModifiers.Shift);
        win.KeyReleaseQwerty(PhysicalKey.Digit2, RawInputModifiers.Control | RawInputModifiers.Shift);
        win.KeyReleaseQwerty(PhysicalKey.ControlLeft, RawInputModifiers.Shift);
        win.KeyReleaseQwerty(PhysicalKey.ShiftLeft, RawInputModifiers.None);
        Pump(win);
        Check("Ctrl+Shift+1,2 → بخشِ ۱۲ (" + vm.Current?.Id + ")", vm.Current?.Id == vm.Sections[11].Id);

        // ── «۰» یعنی دهمین بخش، نه صفرم ──
        win.KeyPressQwerty(PhysicalKey.Digit0, RawInputModifiers.Control | RawInputModifiers.Shift);
        win.KeyReleaseQwerty(PhysicalKey.Digit0, RawInputModifiers.Control | RawInputModifiers.Shift);
        win.KeyReleaseQwerty(PhysicalKey.ControlLeft, RawInputModifiers.Shift);
        win.KeyReleaseQwerty(PhysicalKey.ShiftLeft, RawInputModifiers.None);
        Pump(win);
        Check("Ctrl+Shift+0 → بخشِ ۱۰ (" + vm.Current?.Id + ")", vm.Current?.Id == vm.Sections[9].Id);

        // ── Ctrl+عدد روی یک بخشِ دفتری: ردیف افزوده شود ──
        var exp = vm.Sections.First(x => x.Id == "expenses");
        Wait(win, vm.GoAsync(exp));
        var table = vm.RowHost;
        if (table is null) { Check("جدولِ «مصارف» شناخته نشد", false); return bad; }

        var before = table.RowCount;
        win.KeyPressQwerty(PhysicalKey.Digit4, RawInputModifiers.Control);
        win.KeyReleaseQwerty(PhysicalKey.Digit4, RawInputModifiers.Control);
        win.KeyReleaseQwerty(PhysicalKey.ControlLeft, RawInputModifiers.None);
        Pump(win); Dispatcher.UIThread.RunJobs(); Pump(win);
        Check($"Ctrl+4 → ۴ ردیف افزوده شد ({before} → {table.RowCount})", table.RowCount == before + 4);

        // ── Shift+عدد: همان‌قدر برداشته شود ──
        var mid = table.RowCount;
        win.KeyPressQwerty(PhysicalKey.Digit3, RawInputModifiers.Shift);
        win.KeyReleaseQwerty(PhysicalKey.Digit3, RawInputModifiers.Shift);
        win.KeyReleaseQwerty(PhysicalKey.ShiftLeft, RawInputModifiers.None);
        Pump(win); Dispatcher.UIThread.RunJobs(); Pump(win);
        Check($"Shift+3 → ۳ ردیف حذف شد ({mid} → {table.RowCount})", table.RowCount == mid - 3);

        // ── ردیفِ کافی نیست → هیچ کاری نکند ──
        var keep = table.RowCount;
        win.KeyPressQwerty(PhysicalKey.Digit9, RawInputModifiers.Shift);
        win.KeyReleaseQwerty(PhysicalKey.Digit9, RawInputModifiers.Shift);
        win.KeyPressQwerty(PhysicalKey.Digit9, RawInputModifiers.Shift);
        win.KeyReleaseQwerty(PhysicalKey.Digit9, RawInputModifiers.Shift);
        win.KeyReleaseQwerty(PhysicalKey.ShiftLeft, RawInputModifiers.None);
        Pump(win); Dispatcher.UIThread.RunJobs(); Pump(win);
        Check($"Shift+99 با ردیفِ ناکافی → دست‌نخورده ({keep})", table.RowCount == keep);

        // ── Alt+عدد: حسابِ شمارهٔ ۱ در قرض‌داران باز شود ──
        var debt = vm.Sections.First(x => x.Id == "debt");
        Wait(win, vm.GoAsync(debt));
        win.KeyPressQwerty(PhysicalKey.Digit1, RawInputModifiers.Alt);
        win.KeyReleaseQwerty(PhysicalKey.Digit1, RawInputModifiers.Alt);
        win.KeyReleaseQwerty(PhysicalKey.AltLeft, RawInputModifiers.None);
        Pump(win); Dispatcher.UIThread.RunJobs(); Pump(win);
        Check("Alt+1 → حسابِ کارتِ ۱ باز شد", debt.ActivePage is not null);

        return bad;
    }

    /// <summary>
    /// انتظارِ «پمپ‌شونده». نخِ رابط کاربری همین نخ است، پس
    /// <c>GetAwaiter().GetResult()</c> رویِ کاری که ادامه‌اش را به همین نخ
    /// برمی‌گرداند قفل می‌کرد و عکس‌گیری وسطِ کار می‌خوابید. این‌جا به‌جای
    /// مسدود کردن، حلقهٔ رویداد چرخانده می‌شود تا کار واقعاً تمام شود.
    /// </summary>
    private static void Wait(Window w, Task t)
    {
        var end = DateTime.UtcNow + TimeSpan.FromSeconds(30);
        while (!t.IsCompleted && DateTime.UtcNow < end)
        {
            Dispatcher.UIThread.RunJobs();
            w.UpdateLayout();
            Thread.Sleep(5);
        }
        if (!t.IsCompleted) { Console.WriteLine("  ⚠ کار در ۳۰ ثانیه تمام نشد"); return; }
        t.GetAwaiter().GetResult();          // خطا اگر بود، همین‌جا بالا بیاید
        Pump(w);
    }

    /// <summary>چند دورِ چیدمان/رسم تا صفحه واقعاً ساخته شود.</summary>
    private static void Pump(Window w)
    {
        for (var i = 0; i < 8; i++)
        {
            Dispatcher.UIThread.RunJobs();
            w.UpdateLayout();
        }
    }

    private static void Shot(Window w, string path)
    {
        using var frame = w.CaptureRenderedFrame();
        if (frame is null) { Console.WriteLine("  ✖ عکس گرفته نشد: " + path); return; }
        frame.Save(path);
        Console.WriteLine("  ✔ " + Path.GetFileName(path));
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  سنجشِ اسکرول
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// برای هر بخش می‌گوید: محتوا چقدر بلند است، پنجره چقدر جا دارد، و اگر
    /// بلندتر است آیا ‎ScrollViewer‎ی هست که واقعاً بتواند به تهش برساند.
    ///
    /// پنجره عمداً در کوچک‌ترین اندازهٔ مجاز (‎MinWidth×MinHeight‎) باز می‌شود:
    /// در ۱۴۴۰×۹۰۰ بیشترِ بخش‌ها اصلاً سرریز نمی‌کنند و سنجش بی‌نتیجه می‌ماند.
    /// </summary>
    /// <summary>
    /// کوتاه‌ترین بلندیِ پذیرفتنی برای جدولِ یک بخش. کمتر از این یعنی فیلتر و
    /// جمع‌ها جای جدول را خورده‌اند و کاربر فقط سرِ ستون‌ها را می‌بیند.
    /// </summary>
    private const double MinGridHeight = 120;

    private static int ScrollAudit()
    {
        var tmpDb = Path.Combine(Path.GetTempPath(), "pump-scroll-" + Guid.NewGuid().ToString("N"), "pump.db");
        PumpYaqobi.App.Services.AppHost.Start(tmpDb);

        AppBuilder.Configure<PumpYaqobi.App.App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .SetupWithoutStarting();

        var win = new MainWindow { Width = 1000, Height = 640 };
        win.Show();
        Pump(win);

        var vm = (MainViewModel)win.DataContext!;
        vm.Lock.Password = "1234";
        vm.Lock.Confirm = "1234";
        vm.Lock.SubmitCommand.Execute(null);
        Wait(win, Task.CompletedTask);
        Pump(win);
        Seed.Fill(PumpYaqobi.App.Services.AppHost.Current);

        Console.WriteLine();
        Console.WriteLine("بخش                  سرریز؟   بلندیِ صفحه / جا    اسکرول  درونی     جدول      درست؟");
        Console.WriteLine(new string('-', 74));

        var broken = new List<string>();

        foreach (var sec in vm.Sections)
        {
            Wait(win, vm.GoAsync(sec));
            Pump(win);
            Dispatcher.UIThread.RunJobs();
            Pump(win);

            // ریشهٔ محتوای همین بخش — همان ‎ContentControl‎ی که بخش داخلش است
            var host = win.GetVisualDescendants().OfType<ContentControl>()
                          .FirstOrDefault(c => ReferenceEquals(c.Content, sec));
            if (host is null) { Console.WriteLine($"{sec.Id,-20} — میزبان پیدا نشد"); continue; }

            // ══ اسکرولِ صفحه ══════════════════════════════════════════════
            // از این نسخه، اسکرول یکی است و مالِ کلِ پنجره (‎PageScroll‎) —
            // مثلِ سایت. پس بلندیِ محتوا را از همان می‌پرسیم، نه از
            // اسکرول‌ویورِ درونِ بخش (که دیگر وجود ندارد).
            var page = win.GetVisualDescendants().OfType<ScrollViewer>()
                          .FirstOrDefault(v => v.Name == "PageScroll");

            var avail = page?.Viewport.Height ?? host.Bounds.Height;
            var wanted = page?.Extent.Height ?? host.Bounds.Height;
            var overflows = wanted > avail + 1;

            // ══ جدول ══════════════════════════════════════════════════════
            // ⚠️ با **نوعِ** واقعی می‌سنجیم، نه با نامِ کلاس: بدنهٔ بیشترِ
            // بخش‌ها ‎c:ExcelGrid‎ است که فرزندِ ‎DataGrid‎ است.
            var grid = host.GetVisualDescendants().OfType<DataGrid>()
                           .FirstOrDefault(g => g.IsEffectivelyVisible);
            var gridH = grid?.Bounds.Height ?? 0;

            // جدولِ خالی حقِ کوتاه بودن دارد — سرِ ستون‌ها تنها همین‌قدر است.
            // «له‌شده» یعنی از چیزی که خودش می‌خواهد کوتاه‌تر شده، آن هم تا
            // زیرِ حدِ خواندنی.
            var gridWants = grid?.DesiredSize.Height ?? 0;
            var squashed = grid is not null
                        && gridH + 1 < Math.Min(MinGridHeight, gridWants);

            // ══ هیچ اسکرولِ درونی نباید بلغزد ══════════════════════════════
            // قاعدهٔ صریحِ صاحب ریپو: «تا وقتی نوارِ بخش‌ها به سقف نچسبیده،
            // محتوای بخش نباید تکان بخورد.» هر اسکرول‌ویورِ درونِ بخش این را
            // می‌شکند، چون چرخِ ماوس اولْ آن را می‌لغزاند نه صفحه را. پس
            // اسکرول‌ویورهایی را می‌شماریم که واقعاً چیزی برای لغزاندن دارند.
            var inner = host.GetVisualDescendants().OfType<ScrollViewer>()
                            .Count(v => v.Extent.Height > v.Viewport.Height + 1);

            // ══ و اسکرولِ خودِ جدول ════════════════════════════════════════
            //
            // ⚠️ این شمارشِ بالا جدول را نمی‌گرفت: ‎DataGrid‎ی آوالونیا برای
            // ردیف‌هایش ‎ScrollViewer‎ ندارد؛ خودش می‌لغزاند و فقط یک
            // ‎ScrollBar‎ دارد. برای همین «کادرِ محدودی که ردیفِ ۴۰ و ۵۰ تویش
            // گیر می‌کرد» از چشمِ این سنجش افتاده بود و صاحب ریپو باید
            // خودش گزارشش می‌کرد.
            //
            // قاعده روشن است: جدول باید هم‌قدِ ردیف‌هایش بلند شود و صفحه
            // بلندتر گردد — پس نوارِ لغزشِ عمودیِ جدول نباید چیزی برای
            // لغزاندن داشته باشد.
            var vbar = grid?.GetVisualDescendants().OfType<ScrollBar>()
                            .FirstOrDefault(b => b.Orientation == Orientation.Vertical);

            // ⚠️ یک استثناء که **اندازه‌گیری** ساختش، نه سلیقه: جدولی که
            // ردیف‌هایش از یک صفحه بیشتر شده، سرِ همان یک صفحه می‌ایستد و
            // نوارِ لغزشِ خودش را به‌عمد دارد. بی این تنگنا، ‎DataGrid‎ هر
            // ردیف را واقعاً می‌سازد و گاوصندوقِ ۲۰۰ ردیفی ۴٫۶ ثانیه طول
            // می‌کشد (‎ledgerperf‎). آن‌چه ممنوع است کادرِ **کوچک** است —
            // همان که ردیفِ ۴۰ و ۵۰ را قایم می‌کرد — نه ایستادن سرِ یک صفحه.
            var atScreenCap = grid is not null && avail > 0 && gridH >= avail - 1;
            var caged = vbar is not null && vbar.Maximum > 1 && !atScreenCap;

            var ok = page is not null && !squashed && inner == 0 && !caged;

            var mark = ok ? (overflows ? "✔" : "—") : "✖";
            if (caged) Console.WriteLine($"        ⚠️ {sec.Id}: جدول داخلِ کادر گیر کرده "
                                       + $"(لغزشِ درونی تا {vbar!.Maximum:0} پیکسل)");
            Console.WriteLine($"{sec.Id,-20} {(overflows ? "بله" : "نه"),-8} "
                            + $"{wanted,6:0} / {avail,-6:0}      {(page is not null ? "صفحه" : "—"),-6} "
                            + $"{(inner > 0 ? $"درونی {inner}" : "        "),-9} "
                            + $"{(grid is not null ? $"جدول {gridH,4:0}" : "         ")}  {mark}");

            if (!ok) broken.Add(sec.Id);
        }

        Console.WriteLine();
        if (broken.Count == 0)
        {
            Console.WriteLine("✅ همهٔ بخش‌ها با اسکرولِ صفحه می‌لغزند و هیچ‌کدام اسکرولِ درونی ندارند");
            return 0;
        }

        Console.WriteLine("❌ این بخش‌ها با مدلِ سایت نمی‌خوانند — یا اسکرولِ صفحه پیدا"
                        + $" نشد، یا اسکرولِ درونی دارند، یا جدولشان از {MinGridHeight:0}"
                        + " پیکسل کوتاه‌تر شده:");
        foreach (var b in broken) Console.WriteLine("   • " + b);
        return 1;
    }
}
