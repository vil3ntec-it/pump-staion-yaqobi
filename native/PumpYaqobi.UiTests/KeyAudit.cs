using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PumpYaqobi.App.ViewModels;
using PumpYaqobi.App.Views;

namespace PumpYaqobi.UiTests;

/// <summary>
/// ══ کلیدِ چپ و راست هنگامِ تایپ ═════════════════════════════════════════════
///
/// گزارشِ صاحب ریپو: «موقعِ تایپ، آدم اگر بخواهد چپ برود اتومات می‌رود راست،
/// بعدش می‌رود چپ — این باگِ خیلی مزخرفی است.»
///
/// این‌جا همان کار روی خانهٔ واقعیِ جدول انجام می‌شود و <b>جای کُرسر</b> پیش و
/// پس از هر کلید چاپ می‌گردد. با عدد معلوم می‌شود کُرسر کجا می‌رود، نه با حدس.
///
///     dotnet run --project PumpYaqobi.UiTests -- keys
/// </summary>
internal static class KeyAudit
{
    public static int Run()
    {
        var tmpDb = Path.Combine(Path.GetTempPath(),
                                 "pump-keys-" + Guid.NewGuid().ToString("N"), "pump.db");
        PumpYaqobi.App.Services.AppHost.Start(tmpDb);

        AppBuilder.Configure<PumpYaqobi.App.App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .SetupWithoutStarting();

        var win = new MainWindow { Width = 1440, Height = 900 };
        win.Show();
        Pump(win);

        var vm = (MainViewModel)win.DataContext!;
        vm.Lock.Password = "1234";
        vm.Lock.Confirm = "1234";
        vm.Lock.SubmitCommand.Execute(null);
        Pump(win);
        Seed.Fill(PumpYaqobi.App.Services.AppHost.Current);

        var sec = vm.Sections.First(s => s.Id == "expenses");
        Wait(win, vm.GoAsync(sec));
        for (var i = 0; i < 6; i++) { Dispatcher.UIThread.RunJobs(); Pump(win); }

        var grid = win.GetVisualDescendants().OfType<DataGrid>().FirstOrDefault();
        if (grid is null) { Console.WriteLine("جدولی نبود"); return 1; }

        var bad = 0;
        Console.WriteLine();
        Console.WriteLine("متنِ خانه            کلید    کُرسر پیش ← پس   انتظار   نتیجه");
        Console.WriteLine(new string('-', 66));

        // دو جنسِ متن: عددِ لاتین (بیشترِ خانه‌ها) و نامِ فارسی
        bad += Probe(win, grid, "12,345", Key.Left);
        bad += Probe(win, grid, "12,345", Key.Right);
        bad += Probe(win, grid, "برق دکان", Key.Left);
        bad += Probe(win, grid, "برق دکان", Key.Right);

        Console.WriteLine();
        Console.WriteLine(bad == 0
            ? "✅ کُرسر همان‌جایی می‌رود که کلید می‌گوید"
            : $"❌ {bad} حالت وارونه است");
        return bad == 0 ? 0 : 1;
    }

    /// <summary>
    /// یک خانه را باز می‌کند، متن می‌گذارد، کُرسر را وسط می‌برد و یک کلید
    /// می‌زند. خروجی ۱ یعنی کُرسر وارونه رفت.
    /// </summary>
    private static int Probe(Window win, DataGrid grid, string text, Key key)
    {
        var box = Editor(win, grid);
        if (box is null) { Console.WriteLine("کادرِ تایپ باز نشد"); return 1; }

        box.Text = text;
        box.CaretIndex = text.Length / 2;
        Pump(win);

        var before = box.CaretIndex;
        win.KeyPressQwerty(Phys(key), RawInputModifiers.None);
        win.KeyReleaseQwerty(Phys(key), RawInputModifiers.None);
        Pump(win);
        var after = box.CaretIndex;

        // در متنِ راست‌به‌چپ، «چپ» یعنی جلو رفتن در رشته و «راست» یعنی عقب.
        // در متنِ لاتین برعکس. این‌جا فقط می‌گوییم کُرسر تکان خورد و در کدام
        // سمتِ رشته — قضاوتِ درست/غلط را خودِ گزارش پایین‌تر می‌کند.
        var rtl = text.Any(c => c >= 0x0600 && c <= 0x06FF);
        var want = key == Key.Left ? (rtl ? +1 : -1) : (rtl ? -1 : +1);
        var got = Math.Sign(after - before);
        var ok = got == want;

        Console.WriteLine($"{Pad(text, 18)} {(key == Key.Left ? "←" : "→"),-6} "
                        + $"{before,9} ← {after,-6} {(want > 0 ? "+1" : "-1"),6}   {(ok ? "✔" : "✖")}");

        Escape(win);
        return ok ? 0 : 1;
    }

    private static string Pad(string s, int n) => s.Length >= n ? s[..n] : s + new string(' ', n - s.Length);

    private static PhysicalKey Phys(Key k) => k == Key.Left ? PhysicalKey.ArrowLeft : PhysicalKey.ArrowRight;

    /// <summary>خانهٔ اولِ جدول را به حالتِ ویرایش می‌برد و کادرش را می‌دهد.</summary>
    private static TextBox? Editor(Window win, DataGrid grid)
    {
        grid.Focus();
        if (grid.SelectedIndex < 0 && grid.GetVisualDescendants().OfType<DataGridRow>().Any())
            grid.SelectedIndex = 0;
        grid.CurrentColumn = grid.Columns.FirstOrDefault(c => !c.IsReadOnly && c.IsVisible);
        Pump(win);
        grid.BeginEdit();
        Pump(win);
        return grid.GetVisualDescendants().OfType<TextBox>().FirstOrDefault(t => t.IsVisible);
    }

    private static void Escape(Window win)
    {
        win.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        win.KeyReleaseQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        Pump(win);
    }

    private static void Wait(Window w, Task t)
    {
        var end = DateTime.UtcNow + TimeSpan.FromSeconds(30);
        while (!t.IsCompleted && DateTime.UtcNow < end)
        { Dispatcher.UIThread.RunJobs(); w.UpdateLayout(); Thread.Sleep(5); }
        Pump(w);
    }

    private static void Pump(Window w)
    { for (var i = 0; i < 8; i++) { Dispatcher.UIThread.RunJobs(); w.UpdateLayout(); } }
}
