using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PumpYaqobi.App.ViewModels;
using PumpYaqobi.App.Views;

namespace PumpYaqobi.UiTests;

/// <summary>
/// ══ خانهٔ جدول باید مثلِ اکسل رفتار کند ═════════════════════════════════════
///
/// گزارشِ صاحب ریپو با عکسِ خودِ اکسل:
///
///   «محضِ این‌که می‌روم، خودش جدول انتخاب شده است و اگر بک‌اسپیس را بزنم پاک
///    می‌شود، یا اگر بزنم روی یک عدد یا حرف آن تغییر می‌کند … الان برنامهٔ من
///    این را پاک نمی‌کند یا تغییر نمی‌دهد و باید سه بار بزنم رویش.»
///
///   «توی کادر یک کادرِ دیگر آمده … یک کادرِ سرخ دیده می‌شود که تویش حروف
///    نوشته … در اکسل توی کادر یک کادرِ دیگر نیست.»
///
/// هر دو ادعا با عدد سنجیدنی است، پس این‌جا روی جدولِ واقعی سنجیده می‌شوند:
///
///   ۱) <b>رفتار</b>: یک خانهٔ انتخاب‌شده، بی هیچ کلیکِ اضافه —
///        • حرف/عدد زدن ⇒ محتوا <b>جایگزین</b> شود (نه افزوده، نه نادیده)
///        • ‎Backspace‎ ⇒ پاک شود
///        • ‎Delete‎ ⇒ پاک شود
///   ۲) <b>شکل</b>: کادرِ تایپ باید دقیقاً هم‌اندازهٔ خانه باشد و هیچ لبه یا
///      پس‌زمینهٔ دیدنی نداشته باشد — وگرنه همان «کادر در کادر» است.
///
///     dotnet run --project PumpYaqobi.UiTests -- cells
/// </summary>
internal static class CellEditAudit
{
    /// <summary>بیشترین فاصلهٔ پذیرفتنیِ لبهٔ کادرِ تایپ از لبهٔ خانه.</summary>
    private const double Slack = 1.5;

    public static int Run()
    {
        var tmpDb = Path.Combine(Path.GetTempPath(),
                                 "pump-cells-" + Guid.NewGuid().ToString("N"), "pump.db");
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

        var bad = new List<string>();
        bad.AddRange(Behaviour(win, grid));
        bad.AddRange(Shape(win, grid));
        bad.AddRange(Resize(win, grid));
        bad.AddRange(TwoModes(win, grid));
        bad.AddRange(ChipToggles(win, vm));

        Console.WriteLine();
        if (bad.Count == 0)
        {
            Console.WriteLine("✅ خانه مثلِ اکسل رفتار می‌کند و کادرِ تایپ دیده نمی‌شود");
            return 0;
        }

        Console.WriteLine($"❌ {bad.Count} ایراد:");
        foreach (var b in bad) Console.WriteLine("   • " + b);
        return 1;
    }

    // ══ ۱) رفتار: یک ضربه، نه سه کلیک ═══════════════════════════════════════

    private static IEnumerable<string> Behaviour(Window win, DataGrid grid)
    {
        var bad = new List<string>();

        Console.WriteLine();
        Console.WriteLine("کار روی خانهٔ انتخاب‌شده          پیش        پس         انتظار      نتیجه");
        Console.WriteLine(new string('-', 78));

        bad.AddRange(One(win, grid, "تایپِ «7»", "برق دکان", "7",
                         () => win.KeyTextInput("7")));

        bad.AddRange(One(win, grid, "تایپِ «۷» روی عدد", "12,345", "7",
                         () => win.KeyTextInput("7")));

        bad.AddRange(One(win, grid, "Backspace", "برق دکان", "",
                         () => Tap(win, PhysicalKey.Backspace)));

        bad.AddRange(One(win, grid, "Delete", "برق دکان", "",
                         () => Tap(win, PhysicalKey.Delete)));

        return bad;
    }

    /// <summary>
    /// یک خانه را با متنِ داده‌شده آماده می‌کند، خانه را فقط <b>انتخاب</b>
    /// می‌کند (نه ویرایش)، کار را انجام می‌دهد و می‌گوید محتوا چه شد.
    ///
    /// ⚠️ «انتخاب، نه ویرایش» نکتهٔ همین سنجش است: شکایت دربارهٔ همین بود که
    /// روی خانهٔ فقط انتخاب‌شده، تایپ و ‎Backspace‎ کار نمی‌کرد.
    /// </summary>
    private static IEnumerable<string> One(Window win, DataGrid grid,
                                           string what, string start, string want,
                                           Action act)
    {
        var col = grid.Columns.FirstOrDefault(c => !c.IsReadOnly && c.IsVisible);
        if (col is null) return new[] { "ستونِ نوشتنی نبود" };

        // متنِ آغازین را از راهِ ویرایش می‌گذاریم، بعد ویرایش را می‌بندیم
        Select(win, grid, col);
        grid.BeginEdit();
        Pump(win);
        var box = Editor(win, grid);
        if (box is null) return new[] { $"{what}: کادرِ تایپ باز نشد" };
        box.Text = start;
        grid.CommitEdit(DataGridEditingUnit.Cell, true);
        Pump(win);

        var before = CellText(win, grid, col);

        // حالا فقط انتخاب است، نه ویرایش
        Select(win, grid, col);
        act();
        Pump(win);
        grid.CommitEdit(DataGridEditingUnit.Cell, true);
        Pump(win);

        var after = CellText(win, grid, col);
        var ok = after == want;

        Console.WriteLine($"{Pad(what, 30)} {Pad(Show(before), 10)} {Pad(Show(after), 10)} "
                        + $"{Pad(Show(want), 10)}  {(ok ? "✔" : "✖")}");

        return ok ? Array.Empty<string>()
                  : new[] { $"{what}: «{before}» ⇐ «{after}» شد، باید «{want}» می‌شد" };
    }

    private static string Show(string s) => s.Length == 0 ? "(خالی)" : s;

    /// <summary>خانه را انتخاب می‌کند و ویرایش را می‌بندد — نقطهٔ شروعِ هر آزمون.</summary>
    private static void Select(Window win, DataGrid grid, DataGridColumn col)
    {
        grid.CommitEdit(DataGridEditingUnit.Cell, true);
        grid.Focus();
        if (grid.SelectedIndex < 0) grid.SelectedIndex = 0;
        grid.CurrentColumn = col;
        Pump(win);
    }

    /// <summary>نوشتهٔ همان خانه، از خودِ چیزی که روی صفحه کشیده شده.</summary>
    private static string CellText(Window win, DataGrid grid, DataGridColumn col)
    {
        Pump(win);
        var cell = grid.GetVisualDescendants().OfType<DataGridCell>()
                       .FirstOrDefault(c => ReferenceEquals(ColumnOf(c), col)
                                         && c.GetVisualAncestors().OfType<DataGridRow>()
                                             .FirstOrDefault()?.GetIndex() == grid.SelectedIndex);
        if (cell is null) return "";
        var box = cell.GetVisualDescendants().OfType<TextBox>().FirstOrDefault(t => t.IsVisible);
        if (box is not null) return box.Text ?? "";
        return cell.GetVisualDescendants().OfType<TextBlock>()
                   .FirstOrDefault()?.Text ?? "";
    }

    // ══ ۲) شکل: کادرِ تایپ نه دیده شود نه تودرتو باشد ═══════════════════════

    private static IEnumerable<string> Shape(Window win, DataGrid grid)
    {
        var bad = new List<string>();
        var col = grid.Columns.FirstOrDefault(c => !c.IsReadOnly && c.IsVisible);
        if (col is null) return bad;

        Select(win, grid, col);
        grid.BeginEdit();
        Pump(win);

        var box = Editor(win, grid);
        var cell = box?.GetVisualAncestors().OfType<DataGridCell>().FirstOrDefault();
        if (box is null || cell is null) return new[] { "کادرِ تایپ باز نشد" };

        if (Environment.GetEnvironmentVariable("PUMP_EDITPROBE") == "1")
        {
            Console.WriteLine("   کادرهای تایپِ داخلِ جدول:");
            foreach (var t in grid.GetVisualDescendants().OfType<TextBox>())
                Console.WriteLine($"      name={t.Name} vis={t.IsVisible} w={t.Bounds.Width:0} "
                    + $"h={t.Bounds.Height:0} halign={t.HorizontalAlignment} text=«{t.Text}» "
                    + $"parent={t.GetVisualParent()?.GetType().Name}");
            Console.WriteLine("   لبه‌های داخلِ کادرِ برگزیده:");
            foreach (var b in box.GetVisualDescendants().OfType<Border>())
                Console.WriteLine($"      name={b.Name} w={b.Bounds.Width:0} bt={b.BorderThickness} "
                    + $"bg={(b.Background as ISolidColorBrush)?.Color.ToString() ?? "—"}");
        }

        Console.WriteLine();
        Console.WriteLine("کادرِ تایپ در برابرِ خانه       مقدار                        نتیجه");
        Console.WriteLine(new string('-', 78));

        // ── هم‌اندازه بودن ────────────────────────────────────────────────
        //
        // ⚠️ در مختصاتِ **پنجره**، نه مختصاتِ خانه: درختِ برنامه راست‌به‌چپ
        // است و ‎TranslatePoint(0,0)‎ مبدأ را از سمتِ راست می‌دهد، پس
        // «فاصله از چپ» عددِ وارونه درمی‌آمد (۴۳۱ و ‎−۴۱۰‎) و درستی را
        // خرابی نشان می‌داد. اندازه‌ها در مختصاتِ پنجره آینه ندارند.
        // ⚠️ «جای محتوا»ی خانه، نه خودِ خانه: بالشتکِ ‎10,4‎ی خانه مالِ خودش
        // است و اکسل هم دارد. آن‌چه نباید باشد، کادری است **کوچک‌تر از جای
        // محتوا** — چون آن وقت لبه‌اش وسطِ خانه می‌افتد و «کادر در کادر»
        // دیده می‌شود.
        var roomW = cell.Bounds.Width - cell.Padding.Left - cell.Padding.Right;
        var gapW = roomW - box.Bounds.Width;
        var worst = gapW;

        var fits = worst <= Slack;
        Console.WriteLine($"{Pad("کوچک‌تر از جای محتوای خانه", 30)} "
                        + $"{Pad($"{box.Bounds.Width:0} در {roomW:0}", 28)} "
                        + $"{(fits ? "✔" : "✖")}");
        if (!fits)
            bad.Add($"کادرِ تایپ {worst:0} پیکسل از خانه کوچک‌تر است — «کادر در کادر» می‌شود");

        // ── لبه و پس‌زمینهٔ خودِ کادر ──────────────────────────────────────
        var rim = box.GetVisualDescendants().OfType<Border>()
                     .Where(b => b.Bounds.Width > 0)
                     .Select(b => Math.Max(Math.Max(b.BorderThickness.Left, b.BorderThickness.Right),
                                           Math.Max(b.BorderThickness.Top, b.BorderThickness.Bottom)))
                     .DefaultIfEmpty(0).Max();
        var bare = rim <= 0.01;
        Console.WriteLine($"{Pad("ضخامتِ لبهٔ کادرِ تایپ", 30)} {Pad($"{rim:0.##} پیکسل", 28)} "
                        + $"{(bare ? "✔" : "✖")}");
        if (!bare) bad.Add($"کادرِ تایپ لبهٔ {rim:0.##} پیکسلی دارد و دیده می‌شود");

        // ── قابِ خانهٔ فعال: دورِ خودِ خانه، نه دورِ نوشته ──────────────────
        var frame = cell.GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Rectangle>()
                        .FirstOrDefault(r => r.Name == "CurrencyVisual" && r.IsVisible
                                          && r.Stroke is not null && r.StrokeThickness > 0);
        if (frame is not null)
        {
            var off = frame.TranslatePoint(new Point(0, 0), cell) ?? new Point(0, 0);
            var hugs = Math.Abs(off.X) <= Slack && Math.Abs(off.Y) <= Slack
                    && Math.Abs(frame.Bounds.Width - cell.Bounds.Width) <= Slack * 2
                    && Math.Abs(frame.Bounds.Height - cell.Bounds.Height) <= Slack * 2;
            Console.WriteLine($"{Pad("قابِ خانهٔ فعال", 30)} "
                            + $"{Pad($"{frame.Bounds.Width:0}×{frame.Bounds.Height:0} در خانهٔ "
                                   + $"{cell.Bounds.Width:0}×{cell.Bounds.Height:0}", 28)} "
                            + $"{(hugs ? "✔" : "✖")}");
            if (!hugs)
                bad.Add("قابِ خانهٔ فعال دورِ تمامِ خانه نیست — همان کادرِ سرخِ کوچک می‌شود");
        }

        grid.CancelEdit(DataGridEditingUnit.Cell);
        Pump(win);
        return bad;
    }

    // ══ ۳) اندازهٔ ستون بی سقف و بی کف ══════════════════════════════════════
    //
    // گزارشِ صاحب ریپو: «اندازه‌های جدول رو نمی‌تونم هر چقد که می‌خوام بزرگ یا
    // کوچیک کنم و این خیلی اذیت می‌کنه … محدودیت هم نباشه و اگه زیاد بزرگ شد
    // به چپ و راست هم اسکرول بشه.»
    //
    // پس هر ستون تا تهِ کوچکی و تا تهِ بزرگی کشیده می‌شود و دیده می‌شود که
    // واقعاً همان‌قدر شد. و بعد: جدولی که از قابش پهن‌تر شده باید نوارِ لغزشِ
    // افقی داشته باشد.

    private static IEnumerable<string> Resize(Window win, DataGrid grid)
    {
        var bad = new List<string>();
        var col = grid.Columns.FirstOrDefault(c => c.IsVisible);
        if (col is null) return bad;

        Console.WriteLine();
        Console.WriteLine("کشیدنِ ستون                     خواسته     شد        نتیجه");
        Console.WriteLine(new string('-', 78));

        var back = col.Width;

        foreach (var want in new double[] { 30, 900 })
        {
            col.Width = new DataGridLength(want, DataGridLengthUnitType.Pixel);
            Pump(win);
            var got = col.ActualWidth;
            var ok = Math.Abs(got - want) <= 2;
            Console.WriteLine($"{Pad("ستونِ «" + (col.Header?.ToString() ?? "") + "»", 30)} "
                            + $"{Pad(want.ToString("0"), 10)} {Pad(got.ToString("0"), 9)} "
                            + $"{(ok ? "✔" : "✖")}");
            if (!ok)
                bad.Add($"ستونِ «{col.Header}» به {want:0} نرفت و {got:0} ماند — هنوز محدودیت دارد");
        }

        // ── جدولِ پهن‌تر از قاب، افقی می‌لغزد؟ ──────────────────────────────
        var bar = grid.GetVisualDescendants().OfType<ScrollBar>()
                      .FirstOrDefault(b => b.Orientation == Avalonia.Layout.Orientation.Horizontal);
        var slides = bar is { IsVisible: true } && bar.Maximum > 0;
        Console.WriteLine($"{Pad("نوارِ لغزشِ افقی وقتی پهن شد", 30)} "
                        + $"{Pad(slides ? "هست" : "نیست", 20)} {(slides ? "✔" : "✖")}");
        if (!slides) bad.Add("جدول از قابش پهن‌تر شد ولی افقی نمی‌لغزد");

        // ── و با ‎Shift+چرخ‎ واقعاً می‌لغزد؟ ────────────────────────────────
        // این را باید سنجید چون همین را به صاحب ریپو می‌گوییم که بزند.
        if (bar is not null)
        {
            var before = bar.Value;
            // ⚠️ به سمتِ **راستِ** جدول، نه چپ: سرِ جدول ایستاده‌ایم و
            // چرخاندن به عقب همان‌جا می‌ماند (لبه است) — یک بار همین سنجش را
            // بی‌دلیل قرمز کرد.
            for (var i = 0; i < 6; i++)
            {
                grid.RaiseEvent(new PointerWheelEventArgs(
                    grid, null!, grid.GetVisualRoot() as Visual ?? grid, default,
                    0, Avalonia.Input.PointerPointProperties.None,
                    KeyModifiers.Shift, new Vector(0, 1)));
                Pump(win);
            }
            var moved = Math.Abs(bar.Value - before) > 0.5;
            Console.WriteLine($"{Pad("Shift+چرخ افقی می‌برد", 30)} "
                            + $"{Pad($"{before:0} ← {bar.Value:0}", 20)} {(moved ? "✔" : "✖")}");
            if (!moved) bad.Add("‎Shift+چرخ‎ جدول را افقی نمی‌برد");
        }

        col.Width = back;
        Pump(win);
        return bad;
    }

    // ══ ۴) دو حالتِ ویرایشِ اکسل ════════════════════════════════════════════
    //
    // گزارشِ صاحب ریپو: «توی اکسل موقعِ نوشتنِ حروف یا اعداد، وسط یا اول یا آخر
    // فرقی نمی‌کند — بخواهی بروی کادرِ بعدی، می‌رود. ولی تو اپِ من این قابلیت
    // وجود ندارد.»
    //
    // پس دو حالت جدا سنجیده می‌شود، و نکته‌اش همین «فرقی نمی‌کند» است: کُرسر
    // را عمداً **وسطِ** متن می‌گذاریم، جایی که فلش می‌توانست فقط کُرسر را ببرد.
    //
    //   • با تایپ آمده‌ایم  ⇒ فلش ذخیره می‌کند و خانهٔ بعدی
    //   • با ‎F2‎ آمده‌ایم   ⇒ فلش فقط کُرسر را داخلِ متن می‌برد

    private static IEnumerable<string> TwoModes(Window win, DataGrid grid)
    {
        var bad = new List<string>();
        var cols = grid.Columns.Where(c => c.IsVisible).ToList();
        var col = cols.FirstOrDefault(c => !c.IsReadOnly);
        if (col is null || cols.Count < 2) return bad;

        Console.WriteLine();
        Console.WriteLine("حالتِ ویرایش                    کلید   ستون پیش ← پس   کُرسر   نتیجه");
        Console.WriteLine(new string('-', 78));

        // ── حالتِ نوشتن: با تایپ ─────────────────────────────────────────
        Select(win, grid, col);
        win.KeyTextInput("456");
        Pump(win);
        var box = Editor(win, grid);
        if (box is not null) { box.CaretIndex = 1; Pump(win); }   // عمداً وسطِ متن
        var from = cols.IndexOf(grid.CurrentColumn!);
        Tap(win, PhysicalKey.ArrowLeft);
        Pump(win);
        var to = cols.IndexOf(grid.CurrentColumn!);
        var moved = to != from;
        Console.WriteLine($"{Pad("با تایپ آمده (کُرسر وسط)", 30)} {"←",-6} {from,9} ← {to,-6} "
                        + $"{"وسط",6}   {(moved ? "✔" : "✖")}");
        if (!moved) bad.Add("در حالتِ نوشتن، فلش به خانهٔ بعدی نمی‌رود");

        // ── حالتِ ویرایش: با ‎F2‎ ──────────────────────────────────────────
        Select(win, grid, col);
        Tap(win, PhysicalKey.F2);
        Pump(win);
        box = Editor(win, grid);
        if (box is null) return bad.Append("‎F2‎ ویرایش را باز نکرد").ToList();
        box.Text = "12,345";
        box.CaretIndex = 3;
        Pump(win);
        var caretFrom = box.CaretIndex;
        var colFrom = cols.IndexOf(grid.CurrentColumn!);
        Tap(win, PhysicalKey.ArrowLeft);
        Pump(win);
        var stayed = cols.IndexOf(grid.CurrentColumn!) == colFrom;
        var caretMoved = box.CaretIndex != caretFrom;
        Console.WriteLine($"{Pad("با F2 آمده (کُرسر وسط)", 30)} {"←",-6} {colFrom,9} ← "
                        + $"{cols.IndexOf(grid.CurrentColumn!),-6} {caretFrom} ← {box.CaretIndex}"
                        + $"   {(stayed && caretMoved ? "✔" : "✖")}");
        if (!stayed) bad.Add("در حالتِ ‎F2‎ فلش نباید خانه را عوض کند");
        if (!caretMoved) bad.Add("در حالتِ ‎F2‎ فلش باید کُرسر را ببرد");

        grid.CancelEdit(DataGridEditingUnit.Cell);
        Pump(win);
        return bad;
    }

    // ══ ۵) کپسولِ «نوع تیل» با Tab و Enter عوض شود ══════════════════════════
    //
    // گزارشِ صاحب ریپو: «نوعِ تیل هم تو بخشِ قرض‌داران با تب یا اینتر عوض نمی‌شه.»
    // ⚠️ و کارِ خودم بود: آن ستون تا دیروز دو دکمهٔ رادیویی داشت و شناخته
    // می‌شد؛ وقتی به یک کپسول تبدیلش کردم از فهرستِ «خانه‌های عوض‌شدنی» افتاد.

    private static IEnumerable<string> ChipToggles(Window win, MainViewModel vm)
    {
        var bad = new List<string>();

        var debt = vm.Sections.FirstOrDefault(s => s.Id == "debt");
        if (debt is not PumpYaqobi.App.ViewModels.ICardGridHost cards) return bad;
        Wait(win, vm.GoAsync(debt));
        Wait(win, cards.OpenByNumberAsync(1));
        for (var i = 0; i < 6; i++) { Dispatcher.UIThread.RunJobs(); Pump(win); }

        var grid = win.GetVisualDescendants().OfType<DataGrid>()
                      .FirstOrDefault(g => g.Columns.Any(c => c.Header?.ToString() == "نوع تیل"));
        var col = grid?.Columns.FirstOrDefault(c => c.Header?.ToString() == "نوع تیل");
        if (grid is null || col is null) return bad;

        Console.WriteLine();
        Console.WriteLine("کپسولِ «نوع تیل»                کلید    پیش      پس       نتیجه");
        Console.WriteLine(new string('-', 78));

        foreach (var (name, key) in new[] { ("Tab", PhysicalKey.Tab), ("Enter", PhysicalKey.Enter) })
        {
            grid.Focus();
            if (grid.SelectedIndex < 0) grid.SelectedIndex = 0;
            grid.CurrentColumn = col;
            Pump(win);

            var before = ChipText(grid, col);
            Tap(win, key);
            Pump(win);
            var after = ChipText(grid, col);
            var ok = before.Length > 0 && after.Length > 0 && before != after;

            Console.WriteLine($"{Pad("با " + name, 30)} {Pad(name, 7)} {Pad(before, 8)} "
                            + $"{Pad(after, 8)} {(ok ? "✔" : "✖")}");
            if (!ok) bad.Add($"«نوع تیل» با {name} عوض نمی‌شود («{before}» ماند)");
        }

        return bad;
    }

    private static string ChipText(DataGrid grid, DataGridColumn col)
    {
        var cell = grid.GetVisualDescendants().OfType<DataGridCell>()
                       .FirstOrDefault(c => ReferenceEquals(ColumnOf(c), col)
                                         && c.GetVisualAncestors().OfType<DataGridRow>()
                                             .FirstOrDefault()?.GetIndex() == grid.SelectedIndex);
        return (cell?.GetVisualDescendants().OfType<Button>().FirstOrDefault()?.Content
                ?? "").ToString() ?? "";
    }

    // ── ابزار ─────────────────────────────────────────────────────────────

    private static DataGridColumn? ColumnOf(DataGridCell cell) =>
        cell.GetType().GetProperty("OwningColumn",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic
              | System.Reflection.BindingFlags.Public)?.GetValue(cell) as DataGridColumn;

    private static TextBox? Editor(Window win, DataGrid grid) =>
        grid.GetVisualDescendants().OfType<TextBox>().FirstOrDefault(t => t.IsVisible);

    private static void Tap(Window win, PhysicalKey key)
    {
        win.KeyPressQwerty(key, RawInputModifiers.None);
        win.KeyReleaseQwerty(key, RawInputModifiers.None);
    }

    private static string Pad(string s, int n) =>
        s.Length >= n ? s[..n] : s + new string(' ', n - s.Length);

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
