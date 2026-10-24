using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using PumpYaqobi.App.Services;
using PumpYaqobi.App.ViewModels;
using PumpYaqobi.App.Views;
using PumpYaqobi.Application.Localization;
using PumpYaqobi.Domain.Entities;

namespace PumpYaqobi.UiTests;

/// <summary>
/// ══ «✕ زدم، بسته نشد؛ صدای ویندوز آمد؛ انگار پنجره‌ای پشتِ برنامه بود» (۱۴۰۵/۰۸/۰۲) ══
///
/// گزارشِ صاحب ریپو: «صفحهٔ برنامه رو با ایکسِ ویندوز می‌خواستم ببندم، بسته نمی‌شد و
/// برنامه هم کار نمی‌کرد؛ انگار یک صفحهٔ جدا پشتِ صفحهٔ برنامه بود که اول اون باید
/// بسته می‌شد، و بعد اون هم نبود… همه تست‌ها رو روش انجام بده.»
///
/// ⛔ صدای «دینگ»ِ ویندوز یعنی پنجرهٔ برنامه **غیرفعال** است — کارِ ‎ShowDialog‎ با صاحبش.
/// همین سنجه روی کدِ پیشین نشان داد: ✕ (‎WM_SYSCOMMAND/SC_CLOSE‎) روی پنجرهٔ غیرفعال
/// اصلاً به ‎Closing‎ نمی‌رسد؛ «پیش از بستن» و «نسخهٔ تازه» پنجرهٔ جدا بودند. حالا هر دو
/// داخلِ خودِ پنجره‌اند (‎MainViewModel.Ask.cs‎) و ✕ِ دوم همیشه می‌بندد (‎HurryExit‎).
///
/// ⚠️ سنجه‌های دیگر بی‌پنجره‌اند (‎Avalonia.Headless‎) و غیرفعال شدن را **نمی‌بینند**. این
/// یکی پنجرهٔ واقعیِ ویندوز است (‎UsePlatformDetect‎) با همان ‎App‎ و ‎MainWindow‎ی برنامه،
/// ✕ را با همان پیامِ دکمهٔ ✕ می‌زند و حالِ پنجره‌ها را از خودِ ویندوز می‌خواند.
///
/// حالت‌ها (آرگومانِ دوم): ‎plain‎ (✕ ⇐ «بستن بی بکاپ») · ‎twice‎ (✕ ⇐ ✕) · ‎max‎ · ‎many‎
/// (پنج ✕ پشتِ هم) · ‎min‎ (بستن از نوارِ وظیفه وقتی کوچک است) · ‎offer‎ (کارتِ «نسخهٔ
/// تازه» باز است) · ‎offermin‎ · ‎yes‎ («بله، بکاپ بگیر»).
/// </summary>
internal static class CloseXProbe
{
    private static int _bad;
    private static IntPtr _main;
    private static Stopwatch? _xAt;
    private static int _closeWithinMs;
    private static bool _closed;

    public static int Run(string[] args)
    {
        if (!OperatingSystem.IsWindows())
        {
            Console.WriteLine("closex: فقط روی ویندوز معنا دارد — رد شد.");
            return 0;
        }
        var mode = args.Length > 1 ? args[1].ToLowerInvariant() : "plain";
        Console.WriteLine($"══ closex · حالت: {mode}");

        var tmpDb = Path.Combine(Path.GetTempPath(), "pump-closex-" + Guid.NewGuid().ToString("N"), "pump.db");
        AppHost.Start(tmpDb);
        FakeLicense.Grant();
        ExitBackup.Disabled = false;    // ⛔ خودِ همین پرسش سنجیده می‌شود (‎Program‎ برای بقیه خاموشش کرده)

        var life = new ClassicDesktopStyleApplicationLifetime { ShutdownMode = ShutdownMode.OnMainWindowClose };
        AppBuilder.Configure<PumpYaqobi.App.App>().UsePlatformDetect().SetupWithLifetime(life);
        var win = (MainWindow)life.MainWindow!;
        win.Closed += (_, _) =>
        {
            _closed = true;
            var ms = _xAt?.ElapsedMilliseconds ?? -1;
            Check(_xAt is not null && ms <= _closeWithinMs, $"برنامه بسته شد — {ms}ms پس از ✕ (سقف {_closeWithinMs}ms)");
        };

        //  ⛔ سقفِ کل: اگر پنجره بسته نشد، همان «کپ»ِ صاحب ریپو است — حال را بنویس و ✖
        var deadline = new Thread(() =>
        {
            Thread.Sleep(TimeSpan.FromSeconds(150));
            Console.WriteLine("  ✖ برنامه در ۱۵۰ ثانیه بسته نشد — حالِ پنجره‌ها:");
            Console.WriteLine(Dump());
            Environment.Exit(3);
        }) { IsBackground = true };
        deadline.Start();

        Dispatcher.UIThread.Post(async () =>
        {
            try { await Scenario(win, mode); }
            catch (Exception ex) { Fail("استثنا در سنجه: " + ex); try { win.Close(); } catch { } }
        });
        life.Start(args);
        Check(_closed, "برنامه بسته شد");
        Console.WriteLine(_bad == 0 ? "✔ closex سبز" : $"✖ closex: {_bad} مورد");
        return _bad == 0 ? 0 : 1;
    }

    private static async Task Scenario(MainWindow win, string mode)
    {
        var vm = (MainViewModel)win.DataContext!;
        await Until(() => win.IsVisible && vm.Phase == MainViewModel.AppPhase.Ready, 90, "برنامه باز و آماده شد");
        _main = win.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
        Check(_main != IntPtr.Zero, "دستگیرهٔ پنجرهٔ برنامه");

        //  همان «در این اجرا چیزی نوشته شده» — بی آن پرسشِ «پیش از بستن» نمی‌آید.
        //  ⚠️ ورودِ بی‌رمز ‎SignedIn‎ را ممکن است دوباره بزند (و ‎ExitBackup.Mark‎ را)؛
        //  پس تا «لازم» شدن، هر ثانیه یک ردیفِ دیگر.
        await Task.Delay(1500);
        for (var i = 0; i < 10 && !ExitBackup.Needed(AppSettings.Load().AskBackupOnExit); i++)
        {
            await AppHost.Current.SafeLedger.AddAsync(new SafeEntry
            {
                DateShamsi = Shamsi.Today(), Kind = SafeEntryKind.Mandagi, Title = "closex " + i, Amount = 1250 + i,
            });
            await Task.Delay(500);
        }
        Check(ExitBackup.Needed(AppSettings.Load().AskBackupOnExit), "پس از نوشتن، «پیش از بستن» پرسیده می‌شود");

        if (mode == "max") { win.WindowState = WindowState.Maximized; await Task.Delay(800); }
        if (mode is "min" or "offermin") { ShowWindow(_main, SW_MINIMIZE); await Task.Delay(800); }

        Task<bool>? offer = null;
        if (mode is "offer" or "offermin")
        {
            offer = PumpYaqobi.App.Update.AutoUpdate.Ask(
                new PumpYaqobi.App.Update.UpdateInfo(true, "3.1.0", "9.9.9", null, 0, null));
            await Until(() => vm.IsAskingCard, 10, "کارتِ «نسخهٔ تازه» در گوشهٔ خودِ پنجره آمد");
            if (mode == "offermin") { ShowWindow(_main, SW_RESTORE); await Task.Delay(1200); }
            Check(!vm.IsAskingBlocking, "کارت جلوی کار را نمی‌گیرد");
            Verdict("کارتِ «نسخهٔ تازه» باز است");
        }

        _xAt = Stopwatch.StartNew();
        _closeWithinMs = mode == "yes" ? 60_000 : 20_000;
        var clicks = mode == "many" ? 5 : 1;
        for (var i = 0; i < clicks; i++)
        {
            X();
            await Task.Delay(60);
        }
        if (mode == "many") return;     // ✕ِ دوم همان «همین حالا ببند» است — Closed داوری می‌کند
        if (mode == "min") { await Task.Delay(800); ShowWindow(_main, SW_RESTORE); }

        await Until(() => vm.IsAskingBlocking || !win.IsVisible, 20, "پس از ✕، «پیش از بستن» داخلِ خودِ پنجره آمد");
        Check(vm.IsAskingBlocking, "پرسشِ «پیش از بستن» تمام‌پنجره است");
        await Task.Delay(400);
        Verdict("پرسشِ «پیش از بستن» باز است");
        if (offer is not null)
            Check(offer.IsCompleted && !offer.Result, "✕ وسطِ کارتِ «نسخهٔ تازه» یعنی «بعداً» — نه نصب");

        switch (mode)
        {
            case "twice" or "min" or "offer" or "offermin":
                X();                                     // کاربرِ واقعی: دوباره ✕
                break;
            case "yes":
                vm.AskAcceptCommand.Execute(null);       // «بله، بکاپ بگیر»
                break;
            default:
                vm.AskDeclineCommand.Execute(null);      // «بستن بی بکاپ»
                break;
        }
    }

    private static void X() => PostMessage(_main, WM_SYSCOMMAND, (IntPtr)SC_CLOSE, IntPtr.Zero);

    private static List<Window> Dialogs_() =>
        ((IClassicDesktopStyleApplicationLifetime)Avalonia.Application.Current!.ApplicationLifetime!)
            .Windows.Where(w => w is not MainWindow && w.IsVisible).ToList();

    // ══ داوری با چشمِ خودِ ویندوز ════════════════════════════════════════════
    private static void Verdict(string when)
    {
        Console.WriteLine($"  ── {when}:");
        Console.WriteLine(Dump());
        Check(IsWindowEnabled(_main), $"{when}: پنجرهٔ برنامه فعال است — کلیک و ✕ به آن می‌رسد (بی «دینگ»)");
        Check(Dialogs_().Count == 0, $"{when}: هیچ پنجرهٔ جدای پرسشی نیست که پشت یا بیرون بیفتد");
    }

    private sealed record Win(IntPtr Hwnd, int Z, string Class, string Title, bool Visible, bool Enabled,
                              bool Iconic, bool Cloaked, bool OnScreen, IntPtr Owner, RECT Rect, bool Topmost);

    private static List<Win> Windows()
    {
        var pid = (uint)Environment.ProcessId;
        var list = new List<Win>();
        var z = 0;
        EnumWindows((h, _) =>
        {
            z++;
            GetWindowThreadProcessId(h, out var p);
            if (p != pid) return true;
            var cls = new StringBuilder(256); GetClassName(h, cls, cls.Capacity);
            var tt = new StringBuilder(512); GetWindowText(h, tt, tt.Capacity);
            GetWindowRect(h, out var r);
            DwmGetWindowAttribute(h, DWMWA_CLOAKED, out int cloak, sizeof(int));
            var ex = GetWindowLongPtr(h, GWL_EXSTYLE).ToInt64();
            list.Add(new Win(h, z, cls.ToString(), tt.ToString(), IsWindowVisible(h), IsWindowEnabled(h),
                             IsIconic(h), cloak != 0, MonitorFromRect(ref r, 0) != IntPtr.Zero,
                             GetWindow(h, GW_OWNER), r, (ex & WS_EX_TOPMOST) != 0));
            return true;
        }, IntPtr.Zero);
        return list;
    }

    private static string Dump()
    {
        var sb = new StringBuilder();
        foreach (var w in Windows().Where(w => w.Visible || w.Hwnd == _main))
            sb.AppendLine($"     z{w.Z,-4} {(w.Hwnd == _main ? "★" : " ")} «{w.Title}» [{w.Class}] " +
                          $"دیدنی={w.Visible} فعال={w.Enabled} کوچک={w.Iconic} پنهانِ‌DWM={w.Cloaked} " +
                          $"روی‌صفحه={w.OnScreen} رو={w.Topmost} صاحب={(w.Owner == _main ? "برنامه" : w.Owner.ToString())} " +
                          $"({w.Rect.Left},{w.Rect.Top})-({w.Rect.Right},{w.Rect.Bottom})");
        sb.Append($"     پیش‌زمینه={(GetForegroundWindow() == _main ? "برنامه" : GetForegroundWindow().ToString())}");
        return sb.ToString();
    }

    private static async Task Until(Func<bool> ok, int seconds, string what)
    {
        var sw = Stopwatch.StartNew();
        while (!ok() && sw.Elapsed.TotalSeconds < seconds) await Task.Delay(100);
        Check(ok(), $"{what} ({sw.ElapsedMilliseconds}ms)");
    }

    private static void Check(bool ok, string what)
    {
        Console.WriteLine((ok ? "  ✔ " : "  ✖ ") + what);
        if (!ok) _bad++;
    }

    private static void Fail(string what) => Check(false, what);

    // ══ Win32 ══════════════════════════════════════════════════════════════
    private const int WM_SYSCOMMAND = 0x0112, SC_CLOSE = 0xF060, SW_MINIMIZE = 6, SW_RESTORE = 9;
    private const int GW_OWNER = 4, GWL_EXSTYLE = -20, DWMWA_CLOAKED = 14;
    private const long WS_EX_TOPMOST = 0x8;

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    private delegate bool EnumProc(IntPtr h, IntPtr l);

    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumProc f, IntPtr l);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] private static extern bool IsWindowEnabled(IntPtr h);
    [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr h);
    [DllImport("user32.dll")] private static extern IntPtr GetWindow(IntPtr h, int cmd);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern IntPtr MonitorFromRect(ref RECT r, uint flags);
    [DllImport("user32.dll")] private static extern IntPtr GetWindowLongPtr(IntPtr h, int i);
    [DllImport("user32.dll")] private static extern bool PostMessage(IntPtr h, int msg, IntPtr w, IntPtr l);
    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr h, int cmd);
    [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(IntPtr h, int a, out int v, int size);
}
