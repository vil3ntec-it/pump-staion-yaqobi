using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using PumpYaqobi.App.Services;
using PumpYaqobi.App.ViewModels;
using PumpYaqobi.App.ViewModels.Sections;
using PumpYaqobi.App.Views;

namespace PumpYaqobi.UiTests;

/// <summary>
/// ══ پردهٔ لودینگ کارش را می‌کند؟ ═════════════════════════════════════════════
///
/// خواستهٔ صاحب ریپو: «موقعِ تازه باز کردنِ اپ باید یک لودینگ داشته باشه تا
/// همهٔ بخش‌ها و برنامه رو رندر کنه؛ بعدش که شد، این بازگشت به صفحهٔ اصلی نباید
/// تأخیری داشته باشه… جوری نباشه با هر بار رندر شدن اپ حافظه اضافه کنه.»
///
/// پس سه چیز سنجیده می‌شود، نه یکی:
///
///   ۱) پرده واقعاً می‌آید و بعد می‌رود.
///   ۲) **همهٔ** بخش‌ها گرم می‌شوند — نه نیمی.
///   ۳) بعد از گرم شدن، نخستین باز کردنِ هر بخش دیگر «بازِ سرد» نیست.
///
/// و برای بندِ حافظه: گرم کردن دوباره اجرا نمی‌شود و هیچ صفحه‌ای دوبار ساخته
/// نمی‌شود — همان نمونهٔ کش‌شده گرم می‌شود. عددِ حافظه هم چاپ می‌شود تا اگر
/// روزی کسی صفحه‌ها را دور بیندازد و از نو بسازد، همین‌جا دیده شود.
///
///     dotnet run --project PumpYaqobi.UiTests -- warm
/// </summary>
internal static class WarmAudit
{
    public static int Run()
    {
        var dir = Path.Combine(Path.GetTempPath(), "pump-warm-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        AppHost.Start(Path.Combine(dir, "pump.db"));

        AppBuilder.Configure<PumpYaqobi.App.App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .SetupWithoutStarting();

        var win = new MainWindow { Width = 1440, Height = 900 };
        var vm = (MainViewModel)win.DataContext!;
        var bad = new List<string>();

        // ⚠️ **پیش از** هر پمپی گوش می‌دهیم، وگرنه چیزی گرفته نمی‌شود: پنجره
        // گرم کردن را در سازنده‌اش صف می‌کند و نخستین ‎RunJobs()‎ کلش را تا ته
        // می‌بَرد. یک بار همین‌طور شد و سنجش گفت «پرده اصلاً نیامد» در حالی که
        // آمده و رفته بود.
        var sawCurtain = false;
        var lockHiddenUnderCurtain = true;
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != nameof(MainViewModel.IsWarming) || !vm.IsWarming) return;
            sawCurtain = true;
            if (vm.IsLockVisible) lockHiddenUnderCurtain = false;
        };

        win.Show();
        Pump(win);

        // ══ پرده باید **پیش از** رمز بیاید ════════════════════════════════
        //
        // گزارشِ صاحب ریپو: «یک صفحهٔ جدا موقعِ باز شدنِ اپ، نه این‌که رمز را
        // بزنم بعد بیاید… من اول فکر کردم برنامه خراب شده.»
        //
        // پس این‌جا **پیش از** ورود سنجیده می‌شود: اگر کسی روزی ترتیب را
        // برگرداند، همین‌جا قرمز می‌شود.
        // ⚠️ «پیش از رمز» یعنی: تا وقتی پرده هست، هنوز وارد نشده‌ایم.
        var curtainBeforePassword = sawCurtain && vm.IsLocked;

        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < 60_000)
        {
            Dispatcher.UIThread.RunJobs();
            win.UpdateLayout();
            if (vm.Warm.Done && !vm.IsWarming) break;
            Thread.Sleep(2);
        }
        sw.Stop();

        // ── و حالا رمز، پس از پرده ────────────────────────────────────────
        var lockShownAfterCurtain = vm.IsLockVisible;

        vm.Lock.Password = "1234";
        vm.Lock.Confirm = "1234";
        vm.Lock.SubmitCommand.Execute(null);
        for (var i = 0; i < 40; i++) { Dispatcher.UIThread.RunJobs(); win.UpdateLayout(); }

        var all = vm.Sections.Concat(vm.Sections.SelectMany(s => s.SubSections)).ToList();

        Console.WriteLine();
        Console.WriteLine($"پرده پیش از رمز آمد؟             {(curtainBeforePassword ? "بله" : "نه")}");
        Console.WriteLine($"صفحهٔ رمز زیرِ پرده پنهان بود؟   {(lockHiddenUnderCurtain ? "بله" : "نه")}");
        Console.WriteLine($"پرده که رفت، رمز آمد؟            {(lockShownAfterCurtain ? "بله" : "نه")}");
        Console.WriteLine($"پردهٔ لودینگ دیده شد؟            {(sawCurtain ? "بله" : "نه")}");
        Console.WriteLine($"پرده رفت؟                        {(vm.IsWarming ? "نه" : "بله")}");
        Console.WriteLine($"چند بخش گرم شد                   {vm.Warm.Warmed} از {all.Count}");
        Console.WriteLine($"چقدر طول کشید                    {sw.ElapsedMilliseconds:N0} ms");
        Console.WriteLine($"حافظهٔ برنامه پس از گرم شدن      {GC.GetTotalMemory(true) / 1024 / 1024:N0} MB");
        Console.WriteLine();

        if (!sawCurtain) bad.Add("پردهٔ لودینگ اصلاً نیامد");
        if (!curtainBeforePassword) bad.Add("پرده پیش از صفحهٔ رمز نیامد");
        if (!lockHiddenUnderCurtain) bad.Add("صفحهٔ رمز زیرِ پرده هم دیده می‌شد");
        if (!lockShownAfterCurtain) bad.Add("پرده که رفت، صفحهٔ رمز نیامد");
        if (vm.IsWarming) bad.Add("پرده نرفت");
        if (vm.Warm.Warmed < all.Count) bad.Add($"فقط {vm.Warm.Warmed} بخش از {all.Count} گرم شد");

        // ── حالا نخستین باز کردنِ هر بخش باید ارزان باشد ───────────────────
        Console.WriteLine("بخش                              نخستین باز کردن   بارِ دوم");
        Console.WriteLine(new string('-', 66));

        const long goal = 400;
        var first = new Dictionary<SectionViewModel, long>();
        foreach (var sec in vm.Sections)
            first[sec] = Time(() => { Wait(win, vm.GoAsync(sec)); Settle(win); });

        // ⚠️ بارِ دوم هم لازم است: عددِ بارِ اول هنوز خواندنِ دادهٔ همان بخش را
        // در خود دارد، و آن کارِ داده است نه کارِ رابط. تفاوتِ این دو می‌گوید
        // کدام است.
        foreach (var sec in vm.Sections)
        {
            var again = Time(() => { Wait(win, vm.GoAsync(sec)); Settle(win); });
            var t = first[sec];
            var mark = t <= goal ? "✔" : "✘";
            Console.WriteLine($"{Pad(sec.Title, 32)} {t,8:N0} ms {again,10:N0} ms   {mark}");
            if (t > goal) bad.Add($"{sec.Title}: نخستین باز کردن {t:N0} ms");
        }

        // ── و گرم کردن نباید دوباره اجرا شود ──────────────────────────────
        var before = vm.Warm.Warmed;
        Wait(win, vm.WarmUpAsync(win.UpdateLayout));
        if (vm.Warm.Warmed != before)
            bad.Add("گرم کردن دوباره اجرا شد — یعنی هر ورود دوباره حافظه می‌گیرد");

        Console.WriteLine();
        if (bad.Count == 0)
        {
            Console.WriteLine("✅ پرده می‌آید، همهٔ بخش‌ها گرم می‌شوند، و بازِ اول دیگر سرد نیست");
            return 0;
        }
        Console.WriteLine($"❌ {bad.Count} ایراد:");
        foreach (var b in bad.Distinct()) Console.WriteLine("   • " + b);
        return 1;
    }

    private static long Time(Action a)
    {
        var sw = Stopwatch.StartNew();
        a();
        sw.Stop();
        return sw.ElapsedMilliseconds;
    }

    private static string Pad(string s, int n) => s.Length >= n ? s[..n] : s + new string(' ', n - s.Length);

    private static void Wait(Window w, Task t)
    {
        var end = DateTime.UtcNow + TimeSpan.FromSeconds(60);
        while (!t.IsCompleted && DateTime.UtcNow < end)
        { Dispatcher.UIThread.RunJobs(); w.UpdateLayout(); Thread.Sleep(1); }
        Pump(w);
    }

    private static void Settle(Window w)
    {
        for (var i = 0; i < 60; i++)
        {
            Dispatcher.UIThread.RunJobs();
            w.UpdateLayout();
            if (w.IsMeasureValid && w.IsArrangeValid) return;
        }
    }

    private static void Pump(Window w)
    { for (var i = 0; i < 8; i++) { Dispatcher.UIThread.RunJobs(); w.UpdateLayout(); } }
}
