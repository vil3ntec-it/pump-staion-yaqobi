using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
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
/// ⛔ صدای «دینگ»ِ ویندوز یعنی پنجرهٔ برنامه **غیرفعال** است (‎EnableWindow(false)‎) —
/// همان کاری که ‎ShowDialog‎ با صاحبش می‌کند. پس پرسش این است: پنجرهٔ پرسشِ
/// «پیش از بستن» (یا هر پرسشِ دیگری) **واقعاً دیده می‌شود**؟
///
/// ⚠️ سنجه‌های دیگر بی‌پنجره‌اند (‎Avalonia.Headless‎) و غیرفعال شدن، رو/زیرِ هم بودن و
/// بیرون از صفحه بودن را **نمی‌بینند**. این یکی پنجرهٔ واقعیِ ویندوز است (‎UsePlatformDetect‎)
/// با همان ‎App‎ و ‎MainWindow‎ی برنامه، و ✕ را با همان پیامی می‌زند که دکمهٔ ✕ می‌فرستد
/// (‎WM_SYSCOMMAND/SC_CLOSE‎). حالِ پنجره‌ها را از خودِ ویندوز می‌خواند (‎EnumWindows‎،
/// ‎IsWindowEnabled‎، ‎DWMWA_CLOAKED‎، ‎MonitorFromRect‎).
///
/// حالت‌ها (آرگومانِ دوم): ‎plain‎ · ‎max‎ · ‎many‎ (پنج ✕ پشتِ هم) · ‎min‎ (بستن از
/// نوارِ وظیفه وقتی کوچک است) · ‎offer‎ (پرسشِ «نسخهٔ تازه» باز است) · ‎offermin‎.
/// ⛔ «گیر» یعنی: پنجرهٔ برنامه غیرفعال است و هیچ پنجرهٔ دیدنیِ فعالی از همین برنامه
/// روی صفحه نیست. سنجه با سقفِ وقت می‌دود: برنامه‌ای که بسته نشود همین‌جا ✖ می‌گیرد.
/// </summary>
internal static class CloseXProbe
{
    private static int _bad;
    private static IntPtr _main;

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

        var life = new ClassicDesktopStyleApplicationLifetime { ShutdownMode = ShutdownMode.OnMainWindowClose };
        AppBuilder.Configure<PumpYaqobi.App.App>().UsePlatformDetect().SetupWithLifetime(life);
        var win = (MainWindow)life.MainWindow!;

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
            try { await Scenario(life, win, mode); }
            catch (Exception ex) { Fail("استثنا در سنجه: " + ex); try { win.Close(); } catch { } }
        });
        life.Start(args);
        Console.WriteLine(_bad == 0 ? "✔ closex سبز" : $"✖ closex: {_bad} مورد");
        return _bad == 0 ? 0 : 1;
    }

    private static async Task Scenario(ClassicDesktopStyleApplicationLifetime life, MainWindow win, string mode)
    {
        var vm = (MainViewModel)win.DataContext!;
        await Until(() => win.IsVisible && vm.Phase == MainViewModel.AppPhase.Ready, 90, "برنامه باز و آماده شد");
        _main = win.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
        Check(_main != IntPtr.Zero, "دستگیرهٔ پنجرهٔ برنامه");

        //  همان «در این اجرا چیزی نوشته شده» — بی آن پرسشِ «پیش از بستن» نمی‌آید.
        //  ⚠️ ورودِ بی‌رمز ‎SignedIn‎ را ممکن است دوباره بزند (و ‎ExitBackup.Mark‎ را)؛
        //  پس تا «لازم» شدن، هر ثانیه یک ردیفِ دیگر.
        win.Closing += (_, e) => Console.WriteLine($"  · Closing شلیک شد (لغو={e.Cancel}) {State(win, vm)}");
        await Task.Delay(2000);
        for (var i = 0; i < 10 && !ExitBackup.Needed(AppSettings.Load().AskBackupOnExit); i++)
        {
            await AppHost.Current.SafeLedger.AddAsync(new SafeEntry
            {
                DateShamsi = Shamsi.Today(), Kind = SafeEntryKind.Mandagi, Title = "closex " + i, Amount = 1250 + i,
            });
            await Task.Delay(1000);
        }
        Check(ExitBackup.Needed(AppSettings.Load().AskBackupOnExit), "پس از نوشتن، «پیش از بستن» پرسیده می‌شود");

        if (mode == "max") { win.WindowState = WindowState.Maximized; await Task.Delay(800); }

        if (mode is "offer" or "offermin")
        {
            if (mode == "offermin") { ShowWindow(_main, SW_MINIMIZE); await Task.Delay(800); }
            _ = Dialogs.ConfirmAsync(PumpYaqobi.App.Update.AutoUpdate.OfferTitle, "closex", "⬆️ نصب کن", "بعداً");
            await Until(() => Dialogs_().Count == 1, 15, "پرسشِ «نسخهٔ تازه» باز شد");
            await Task.Delay(600);
            if (mode == "offermin") { ShowWindow(_main, SW_RESTORE); await Task.Delay(1200); }
            Verdict("پرسشِ «نسخهٔ تازه» باز است");
        }

        if (mode == "min") { ShowWindow(_main, SW_MINIMIZE); await Task.Delay(800); }

        var sw = Stopwatch.StartNew();
        var clicks = mode == "many" ? 5 : 1;
        for (var i = 0; i < clicks; i++)
        {
            PostMessage(_main, WM_SYSCOMMAND, (IntPtr)SC_CLOSE, IntPtr.Zero);
            await Task.Delay(60);
        }

        var want = mode is "offer" or "offermin" ? 2 : 1;
        await Until(() => Dialogs_().Count >= want || !win.IsVisible, 30, "پس از ✕، پرسشِ «پیش از بستن» آمد");
        await Task.Delay(800);
        if (mode == "min") { ShowWindow(_main, SW_RESTORE); await Task.Delay(1200); }
        Console.WriteLine($"  · پس از ✕: {State(win, vm)}");
        Verdict("پس از ✕");

        //  کاربر هر پرسش را با دکمهٔ «نه»ی خودش جواب می‌دهد — «بستن بی بکاپ» / «بعداً»
        foreach (var d in Dialogs_().ToList())
        {
            var no = d.GetVisualDescendants().OfType<Button>()
                      .FirstOrDefault(b => b.Content as string is "بستن بی بکاپ" or "بعداً");
            Check(no is not null, $"دکمهٔ «نه» در «{d.Title}» پیدا شد");
            no?.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await Task.Delay(400);
        }

        Console.WriteLine($"  · پس از جواب: {State(win, vm)}");
        await Until(() => !win.IsVisible, 20, "برنامه پس از جواب بسته شد");
        if (win.IsVisible)
        {
            //  کاربرِ واقعی همین را می‌کند: دوباره ✕
            Console.WriteLine($"  · ✕ِ دوباره: {State(win, vm)}");
            PostMessage(_main, WM_SYSCOMMAND, (IntPtr)SC_CLOSE, IntPtr.Zero);
            await Until(() => !win.IsVisible, 20, "✕ِ دوباره برنامه را بست");
            if (win.IsVisible) { Console.WriteLine($"  · هنوز باز: {State(win, vm)}"); Console.WriteLine(Dump()); }
        }
        Console.WriteLine($"  ⏱ از ✕ تا بسته شدن: {sw.ElapsedMilliseconds}ms");
    }

    private static string State(MainWindow win, MainViewModel vm)
    {
        static object? F(object o, string n) =>
            o.GetType().GetField(n, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(o);
        return $"[_closing={F(win, "_closing")} _flushed={F(win, "_flushed")} فعال={IsWindowEnabled(_main)} " +
               $"پرسش‌ها={Dialogs_().Count} ارسال=«{vm.ExitSendText}»]";
    }

    private static List<Window> Dialogs_() =>
        ((IClassicDesktopStyleApplicationLifetime)Avalonia.Application.Current!.ApplicationLifetime!)
            .Windows.Where(w => w is not MainWindow && w.IsVisible).ToList();

    // ══ داوری با چشمِ خودِ ویندوز ════════════════════════════════════════════
    private static void Verdict(string when)
    {
        var all = Windows();
        var main = all.FirstOrDefault(w => w.Hwnd == _main);
        var others = all.Where(w => w.Hwnd != _main && w.Visible).ToList();
        Console.WriteLine($"  ── {when}:");
        Console.WriteLine(Dump());
        if (main is null) { Console.WriteLine("  (پنجرهٔ برنامه دیگر نیست)"); return; }

        var seen = others.Where(w => w.Enabled && !w.Cloaked && w.OnScreen && !w.Iconic).ToList();
        Check(main.Enabled || seen.Count > 0,
              $"{when}: برنامه غیرفعال است ⇒ دستِ کم یک پنجرهٔ دیدنی و فعال روی صفحه هست");
        foreach (var w in seen.Where(w => w.Owner == _main))
            Check(w.Z < main.Z, $"{when}: «{w.Title}» روی پنجرهٔ برنامه است، نه پشتش (z {w.Z} < {main.Z})");
        var popup = GetLastActivePopup(_main);
        if (!main.Enabled)
            Check(popup != _main && seen.Any(w => w.Hwnd == popup),
                  $"{when}: کلیک روی برنامه ویندوز را به پنجرهٔ پرسشِ دیدنی می‌برد (نه فقط «دینگ»)");
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
    [DllImport("user32.dll")] private static extern IntPtr GetLastActivePopup(IntPtr h);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern IntPtr MonitorFromRect(ref RECT r, uint flags);
    [DllImport("user32.dll")] private static extern IntPtr GetWindowLongPtr(IntPtr h, int i);
    [DllImport("user32.dll")] private static extern bool PostMessage(IntPtr h, int msg, IntPtr w, IntPtr l);
    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr h, int cmd);
    [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(IntPtr h, int a, out int v, int size);
}
