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
        //  ⛔ نصبِ تازه از ۱۴۰۵/۰۷/۰۷ **بی‌رمز** باز می‌شود و صفحهٔ قفل ندارد.
        //  این سنجه همان مسیرِ رمزدار را می‌سنجد، پس رمز را خودش می‌گذارد.
        if (PumpYaqobi.App.Services.AppHost.Current.Auth.NeedsFirstRun()) PumpYaqobi.App.Services.AppHost.Current.Auth.CreateFirstAdmin("1234");

        //  سنجه با نصبِ **پلن‌دار** می‌دود — وگرنه داشبورد و مفاد/ضرر و
        //  تاریخچه‌ها قفل‌اند و باز نمی‌شوند. شرحش در `FakeLicense`؛ خودِ
        //  قفل در بندِ ۱۷ی `verify` و در `EntitlementsTests` سنجیده می‌شود.
        FakeLicense.Grant();


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
        Seed.Fill(PumpYaqobi.App.Services.AppHost.Current);

        var sec = vm.Sections.First(s => s.Id == "expenses");
        Wait(win, vm.GoAsync(sec));
        for (var i = 0; i < 6; i++) { Dispatcher.UIThread.RunJobs(); Pump(win); }

        var grid = win.GetVisualDescendants().OfType<DataGrid>()
                      .FirstOrDefault(g => g.IsEffectivelyVisible);
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

        // ⚠️ و رقمِ **فارسی** — که تا امروز اصلاً سنجیده نشده بود، در حالی که
        // بیشترِ خانه‌های عددیِ همین برنامه با همین رقم‌ها پر می‌شوند.
        bad += Probe(win, grid, "۱۲٬۳۴۵", Key.Left);
        bad += Probe(win, grid, "۱۲٬۳۴۵", Key.Right);
        bad += Probe(win, grid, "۱۲۳۴۵", Key.Left);
        bad += Probe(win, grid, "۱۲۳۴۵", Key.Right);

        // ══ و حالتِ دوم: «تایپ کردم، فلشِ چپ زدم، رفت راست» ═══════════════
        //
        // ⚠️ چهار سنجشِ بالا فقط **کُرسرِ داخلِ کادر** را می‌سنجند، و همین
        // گمراه کرد: گزارشِ صاحب ریپو دربارهٔ همان نیست، دربارهٔ **پریدنِ
        // خانه** است. در «حالتِ نوشتن» (وقتی با تایپ وارد خانه شده‌ای) فلش
        // ذخیره می‌کند و به خانهٔ بغلی می‌رود — و آن‌جاست که جهت وارونه
        // می‌شود.
        //
        // ⚠️ و این بار با **پیکسل** سنجیده می‌شود، نه با ایندکسِ ستون: کلِ
        // برنامه راست‌به‌چپ است و هر استدلالی روی شمارهٔ ستون یک بار غلط از
        // آب درآمده. چیزی که کاربر می‌بیند جای خانه روی صفحه است.
        Console.WriteLine();
        Console.WriteLine("بخش، در حالتِ نوشتن  کلید    جای خانه بعدِ هر فشار              انتظار   نتیجه");
        Console.WriteLine(new string('-', 66));

        // ⚠️ چند بخش، نه یکی: جدول‌ها ستون‌های متفاوتی دارند (کپسول، کشویی،
        // ستونِ ستاره‌ای) و گزارشِ صاحب ریپو نگفت کدام بخش. اگر جایی وارونه
        // باشد، این‌جا پیدا می‌شود.
        foreach (var id in new[] { "expenses", "safe", "sarrafi", "waraq", "debt",
                                   "shifts", "rasid", "amanat", "companies", "debtrasid" })
        {
            var s2 = vm.Sections.FirstOrDefault(x => x.Id == id);
            if (s2 is null) continue;
            Wait(win, vm.GoAsync(s2));
            for (var i = 0; i < 6; i++) { Dispatcher.UIThread.RunJobs(); Pump(win); }

            // بخشی که صفحهٔ درونی دارد، جدولش پشتِ یک کلیک است
            if (s2 is PumpYaqobi.App.ViewModels.ICardGridHost cards)
            { Wait(win, cards.OpenByNumberAsync(1)); for (var i = 0; i < 6; i++) Pump(win); }

            // ورق کارتِ خودش را دارد، نه ‎ICardGridHost‎
            if (s2.GetType().GetProperty("OpenCardCommand")?.GetValue(s2)
                    is CommunityToolkit.Mvvm.Input.IAsyncRelayCommand open
                && s2.GetType().GetProperty("Cards")?.GetValue(s2)
                    is System.Collections.IEnumerable list
                && list.Cast<object>().FirstOrDefault() is { } card)
            { Wait(win, open.ExecuteAsync(card)); for (var i = 0; i < 6; i++) Pump(win); }

            var g = win.GetVisualDescendants().OfType<DataGrid>()
                       .FirstOrDefault(x => x.IsEffectivelyVisible
                                         && x.Columns.Count(c => c.IsVisible && !c.IsReadOnly) >= 3
                                         && x.GetVisualDescendants().OfType<DataGridRow>().Any());
            if (g is null) { Console.WriteLine($"{Pad(id, 18)} جدولی با ردیف نبود"); continue; }

            bad += Jump(win, g, Key.Left, id);
            bad += Jump(win, g, Key.Right, id);
        }

        Console.WriteLine();
        Console.WriteLine(bad == 0
            ? "✅ کُرسر و خانه، هر دو همان‌جایی می‌روند که کلید می‌گوید"
            : $"❌ {bad} حالت وارونه است");
        return bad == 0 ? 0 : 1;
    }

    /// <summary>
    /// خانه‌ای را با **تایپ** باز می‌کند (نه با ‎F2‎)، یک فلش می‌زند، و
    /// می‌سنجد خانهٔ جاری روی صفحه به کدام سمت رفت.
    /// خروجی ۱ یعنی به سمتِ وارونه پرید.
    /// </summary>
    private static int Jump(Window win, DataGrid grid, Key key, string where)
    {
        grid.Focus();
        if (grid.SelectedIndex < 0) grid.SelectedIndex = 0;

        // از ستونِ وسط شروع می‌کنیم تا هر دو سمت جا داشته باشند
        var cols = grid.Columns.Where(c => c.IsVisible && !c.IsReadOnly).ToList();
        if (cols.Count < 3) { Console.WriteLine($"{Pad(where, 18)} ستونِ کافی نبود"); return 1; }
        grid.CurrentColumn = cols[cols.Count / 2];
        Pump(win);

        // ══ چند بار پشتِ سرِ هم، نه یک بار ═══════════════════════════════════
        //
        // گزارشِ صاحب ریپو: «موقعِ تایپ کردن می‌خواهم یک جهت بروم — یا چپ یا
        // راست — آن‌وقت باگ می‌خورد و برعکس می‌رود.»
        //
        // ⚠️ یک‌بار زدن این را نمی‌گیرد و یک بار همین گمراه کرد: مشکل سرِ
        // **دومین** فشار است، نه اولی. پس این‌جا هر بار تایپ می‌شود و فلش
        // زده می‌شود، و جای خانه بعدِ هر فشار ثبت می‌گردد.
        var xs = new List<double> { CellX(win, grid) };

        for (var step = 0; step < 4; step++)
        {
            // ورود به «حالتِ نوشتن»: چند رقم تایپ می‌شود، عینِ کاربر — نه یک
            // حرف. ⚠️ یک بار با یک حرف سنجیدیم و چیزی پیدا نشد.
            foreach (var ch in "۱۲۳۴۵") { win.KeyTextInput(ch.ToString()); Pump(win); }

            win.KeyPressQwerty(Phys(key), RawInputModifiers.None);
            win.KeyReleaseQwerty(Phys(key), RawInputModifiers.None);
            Pump(win);

            xs.Add(CellX(win, grid));
        }

        // ‎←‎ یعنی خانه باید هر بار به چپِ صفحه برود (ایکسِ کمتر)، ‎→‎ برعکس
        var want = key == Key.Left ? -1 : +1;
        var wrong = 0;
        for (var i = 1; i < xs.Count; i++)
        {
            if (xs[i] < 0 || xs[i - 1] < 0) { wrong++; continue; }
            var d = Math.Sign(xs[i] - xs[i - 1]);
            if (d != 0 && d != want) wrong++;        // صفر یعنی به لبه رسیده
        }
        var ok = wrong == 0;

        Console.WriteLine($"{Pad(where, 18)} {(key == Key.Left ? "←" : "→"),-6} "
                        + $"{string.Join(" ← ", xs.Select(v => v.ToString("0"))),-38} "
                        + $"{(want > 0 ? "راست" : "چپ"),6}   {(ok ? "✔" : $"✖ {wrong} پرشِ وارونه")}");

        Escape(win);
        return ok ? 0 : 1;
    }

    /// <summary>ایکسِ خانهٔ جاری در مختصاتِ پنجره — همان چیزی که چشم می‌بیند.</summary>
    private static double CellX(Window win, DataGrid grid)
    {
        var col = grid.CurrentColumn;
        if (col is null) return -1;

        var cell = grid.GetVisualDescendants().OfType<DataGridCell>()
                       .FirstOrDefault(c => c.IsEffectivelyVisible
                                         && ReferenceEquals(ColumnOf(c), col));
        if (cell is null) return -1;
        return cell.TranslatePoint(new Point(0, 0), win)?.X ?? -1;
    }

    private static DataGridColumn? ColumnOf(DataGridCell cell) =>
        cell.GetType().GetProperty("OwningColumn",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic
                | System.Reflection.BindingFlags.Public)
            ?.GetValue(cell) as DataGridColumn;

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
        // ⚠️ رقمِ فارسی (‎۰۶F۰..۰۶F۹‎) و جداکنندهٔ ‎٬‎ (‎066C‎) در این بازه‌اند
        // ولی **عدد**اند، نه حرف: مثلِ رقمِ لاتین چپ‌به‌راست چیده می‌شوند.
        // بی این تفکیک، انتظارِ آزمون خودش وارونه می‌شد.
        var digits = text.All(c => c is >= '۰' and <= '۹' or '٬' or ',' or '.'
                                   || c is >= '0' and <= '9');
        var rtl = !digits && text.Any(c => c >= 0x0600 && c <= 0x06FF);
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
