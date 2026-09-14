using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Presenters;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PumpYaqobi.App.ViewModels;
using PumpYaqobi.App.Views;

namespace PumpYaqobi.UiTests;

/// <summary>
/// ══ «کادرها دیده نمی‌شوند» و «فلشِ کشویی فقط جا می‌گیرد» ════════════════════
///
/// دو گزارشِ صاحب ریپو که هر دو با چشم بحث‌برانگیزند و با عدد نه:
///
///   ۱) «کادرها با بک‌گراند تفاوت رنگ ندارن، فقط یک خط نازک دارن که اونو هم
///      ندارن — الان هیچ‌کدوم دیده نمی‌شد، حتی خودِ جدول‌ها.»
///
///      پس این‌جا سنجیده می‌شود که مرزِ کارت **واقعاً** دیده می‌شود یا نه. دو
///      راه برای دیده شدن هست و یکی‌شان کافی است:
///        • خودِ کارت از بوم روشن‌تر/تیره‌تر باشد (اختلافِ روشنایی)، یا
///        • لبه‌اش از هر دو طرفش جدا باشد.
///      هر دو زیرِ آستانه ⇒ مرز نامرئی است، هر چند در کد «وجود» دارد.
///
///   ۲) «اون علامتِ ⌄ جوری است که فقط جا اضافه گرفته … تو جدول‌ها دیده نشه و
///      جا نگیره ولی کار کردش باشه.»
///
///      پس پهنای هر کادرِ کشویی داخلِ خانه با پهنای نوشته‌اش سنجیده می‌شود:
///      فاصله‌شان همان جایی است که فلش و بالشتکش می‌خورند.
///
///     dotnet run --project PumpYaqobi.UiTests -- look
/// </summary>
internal static class LookAudit
{
    /// <summary>
    /// کم‌ترین پلهٔ روشناییِ خودِ سطح از بوم.
    ///
    /// ⚠️ «یا پله یا لبه» بس نبود و همان اشتباهی بود که یک دور کشید: پالتِ
    /// قبلی با پلهٔ ۰٫۰۵۳ و لبهٔ ۰٫۱۳۲ از سنجشِ «یکی‌شان کافی است» سبز رد شد،
    /// در حالی که صاحب ریپو همان را «هیچ‌کدوم دیده نمی‌شد» گزارش کرده بود.
    /// پس حالا **هر دو** شرط لازم است — همان چیزی که خودش گفت: «کادر لازمه
    /// تا رنگ بگیره کمی، یا بک‌گراند».
    /// </summary>
    private const double MinStep = 1.08;

    /// <summary>کم‌ترین نسبتِ کنتراستِ لبه با سطحی که دورش کشیده شده.</summary>
    private const double MinRim = 1.18;

    /// <summary>بیشترین جایی که فلشِ کشویی حق دارد در خانهٔ جدول بخورد.</summary>
    private const double MaxGlyph = 6.0;

    public static int Run()
    {
        var tmpDb = Path.Combine(Path.GetTempPath(),
                                 "pump-look-" + Guid.NewGuid().ToString("N"), "pump.db");
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
        Wait(win, Task.CompletedTask);
        Seed.Fill(PumpYaqobi.App.Services.AppHost.Current);

        var bad = new List<string>();
        bad.AddRange(Palette(vm));
        bad.AddRange(Dropdowns(win, vm));
        bad.AddRange(Overflow(win, vm));

        Console.WriteLine();
        if (bad.Count == 0)
        {
            Console.WriteLine("✅ کادرها از بوم جدا دیده می‌شوند و فلشِ کشویی در جدول جا نمی‌گیرد");
            return 0;
        }

        Console.WriteLine($"❌ {bad.Count} ایراد:");
        foreach (var b in bad.Distinct()) Console.WriteLine("   • " + b);
        return 1;
    }

    // ══ ۱) کارت در بوم حل نشود ══════════════════════════════════════════════

    private static IEnumerable<string> Palette(MainViewModel vm)
    {
        var bad = new List<string>();

        Console.WriteLine();
        Console.WriteLine("تم        سطح            رنگ        روشنایی   پله از بوم   جداییِ لبه   نتیجه");
        Console.WriteLine(new string('-', 86));

        foreach (var t in PumpYaqobi.App.Themes.PumpTheme.All)
        {
            vm.SelectedTheme = t;
            Dispatcher.UIThread.RunJobs();

            var canvas = Res("Pump.Dark") ?? Colors.White;
            foreach (var (name, key) in new[]
                     {
                         ("کارت", "Pump.Card"),
                         ("پنل", "Pump.Panel"),
                         ("جدول", "Pump.Table.Bg"),
                     })
            {
                if (Res(key) is not { } surface) continue;
                var edge = Res(key == "Pump.Table.Bg" ? "Pump.Table.Border" : "Pump.Border");

                // ⚠️ **نسبتِ کنتراست**، نه تفاضلِ روشنایی. تفاضل در تیرگی
                // له می‌شود: بومِ ‎#0b0d17‎ و کارتِ ‎#141931‎ فقط ۰٫۰۴۹ فاصله
                // دارند ولی چشم آن‌ها را روشن‌تر از مرمر جدا می‌بیند
                // (نسبتِ ۱٫۴۸ در برابرِ ۱٫۱۰). با تفاضل، تمِ تیره بی‌دلیل
                // رد می‌شد و تمِ روشنِ واقعاً محو قبول.
                var step = Ratio(surface, canvas);
                var rim = edge is null ? 1 : Ratio(edge.Value, surface);

                var ok = step >= MinStep && rim >= MinRim;
                Console.WriteLine($"{t.Id,-9} {name,-12} {Hex(surface),10} {Luma(surface),8:0.000} "
                                + $"{step,12:0.00}× {rim,11:0.00}×   {(ok ? "✔" : "✖")}");

                if (!ok)
                    bad.Add($"{t.Title} · «{name}»: پله از بوم {step:0.00}× (کمینه {MinStep:0.00}×) "
                          + $"و جداییِ لبه {rim:0.00}× (کمینه {MinRim:0.00}×)");
            }
        }

        return bad;
    }

    private static Color? Res(string key) =>
        Avalonia.Application.Current!.TryFindResource(key, out var v) && v is ISolidColorBrush b
            ? b.Color : null;

    // ══ ۲) فلشِ کشویی در خانهٔ جدول جا نگیرد ════════════════════════════════

    private static IEnumerable<string> Dropdowns(Window win, MainViewModel vm)
    {
        var bad = new List<string>();
        var head = false;

        foreach (var sec in vm.Sections.ToList())
        {
            Wait(win, vm.GoAsync(sec));
            for (var k = 0; k < 4; k++) { Dispatcher.UIThread.RunJobs(); Pump(win); }

            // ⚠️ فقط خانه‌های **دیده‌شونده**: از وقتی همهٔ بخش‌ها با هم در درختِ
            // بصری می‌مانند و فقط یکی‌شان دیده می‌شود
            // (‎SectionViewModel.IsShown‎)، این حلقه به خانه‌های بخش‌های پنهان
            // هم می‌رسید — و باز کردنِ پاپ‌آپی که روی صفحه نیست، همان‌جا
            // استثنا می‌داد.
            foreach (var cell in win.GetVisualDescendants().OfType<DataGridCell>()
                                    .Where(c => c.IsEffectivelyVisible))
            {
                var box = cell.GetVisualDescendants().OfType<ComboBox>().FirstOrDefault();
                if (box is null || box.Bounds.Width <= 0) continue;

                var text = box.GetVisualDescendants().OfType<TextBlock>()
                              .Where(t => !string.IsNullOrWhiteSpace(t.Text))
                              .OrderByDescending(t => t.Bounds.Width).FirstOrDefault();
                if (text is null) continue;

                // ⚠️ «چقدر جا خورده» را از خودِ قالب می‌پرسیم، نه از جمعِ پهنای
                // فرزندان: ‎Path‎ی داخلِ ‎Viewbox‎ پهنای خامِ ۲۰۱۰ دارد و هر
                // جمعی را بی‌معنا می‌کند. قالبِ پیش‌فرضِ آوالونیا ستونِ دومی
                // به پهنای ثابتِ ۳۲ برای فلش می‌گذارد؛ همان را می‌خوانیم.
                var glyph = box.GetVisualDescendants().OfType<Grid>()
                               .Where(g => g.ColumnDefinitions.Count > 1)
                               .Select(g => g.ColumnDefinitions.Skip(1).Sum(cd => cd.ActualWidth))
                               .DefaultIfEmpty(0).Max();

                if (!head)
                {
                    head = true;
                    Console.WriteLine();
                    Console.WriteLine("بخش            ستون              پهنای خانه  پهنای نوشته  فلش  اندازهٔ خط  نتیجه");
                    Console.WriteLine(new string('-', 86));
                }

                var col = cell.GetVisualAncestors().OfType<DataGridRow>().Any()
                    ? ColumnName(cell) : "";
                var ok = glyph <= MaxGlyph;
                Console.WriteLine($"{sec.Id,-14} {Cut(col),-17} {box.Bounds.Width,10:0} "
                                + $"{text.Bounds.Width,12:0} {glyph,5:0} {text.FontSize,10:0} "
                                + $"  {(ok ? "✔" : "✖")}");

                if (!ok)
                    bad.Add($"{sec.Id} · کشوییِ «{col}»: فلش {glyph:0} پیکسل از خانه می‌خورد");

                // ⚠️ فلش که رفت، تنها راهِ باز کردن خودِ کادر است — پس همین‌جا
                // ثابت می‌شود که هنوز باز می‌شود و گزینه‌ها را نشان می‌دهد.
                // بی این، «جا نگرفتن» می‌توانست به قیمتِ از کار افتادن باشد.
                box.IsDropDownOpen = true;
                for (var k = 0; k < 4; k++) { Dispatcher.UIThread.RunJobs(); Pump(win); }
                // ⚠️ گزینه‌ها را از **فرزندِ پاپ‌آپ** بشمار، نه از فرزندانِ خودِ
                // کادر: پاپ‌آپ ریشهٔ بصریِ جداگانهٔ خودش را دارد و
                // ‎GetVisualDescendants‎ی کادر هرگز به آن نمی‌رسد — یک‌بار
                // همین «صفر گزینه»ی دروغ، درستی را خرابی نشان داد.
                var pop = box.GetVisualDescendants().OfType<Popup>()
                             .FirstOrDefault(pp => pp.Name == "PART_Popup");
                var items = pop?.Child?.GetVisualDescendants().OfType<ComboBoxItem>().Count() ?? 0;
                var opened = box.IsDropDownOpen && items > 0;
                box.IsDropDownOpen = false;
                Pump(win);

                Console.WriteLine($"{"",14} {"↳ باز می‌شود؟",-17} {(opened ? "بله" : "نه"),10} "
                                + $"{items,12} گزینه");
                if (!opened)
                    bad.Add($"{sec.Id} · کشوییِ «{col}» دیگر باز نمی‌شود");

                if (Environment.GetEnvironmentVariable("PUMP_BOXPROBE") == "1")
                {
                    Console.WriteLine("   کادرِ کشویی از چه ساخته شده:");
                    foreach (var d in box.GetVisualDescendants().OfType<Control>())
                        Console.WriteLine($"      {d.GetType().Name,-20} w={d.Bounds.Width,6:0.0} "
                            + $"vis={d.IsVisible} col={Grid.GetColumn(d)} name={d.Name}"
                            + (d is Grid g
                                ? "  cols=[" + string.Join(" | ", g.ColumnDefinitions
                                    .Select(cd => $"{cd.Width}→{cd.ActualWidth:0}")) + "]"
                                : ""));
                    Environment.SetEnvironmentVariable("PUMP_BOXPROBE", "0");
                }

                break; // یک نمونه از هر بخش بس است
            }
        }

        return bad;
    }

    // ══ ۳) محتوای خانه از خانه بیرون نزند ══════════════════════════════════
    //
    // گزارشِ صاحب ریپو دربارهٔ ستونِ «نوع تیل»: «هر دو هم جا نمی‌شن … یکی بالا
    // و یکی پایین هم باشن و اگه طولِ جدول رو بزرگ هم کنه مشکل نیست، هر دو
    // دیده بشن.» حق داشت: دو کادرِ رادیویی روی هم ۵۰ پیکسل می‌خواستند و ردیفِ
    // ۴۴ پیکسلی دومی را می‌بُرید — و چون خانه وسط‌چین است، از پایین می‌بُرید،
    // پس نصفه دیده می‌شد و آدم فکر می‌کرد خراب است.
    //
    // این‌جا هر خانهٔ ساخته‌شده سنجیده می‌شود: آن‌چه خانه **می‌خواهد** از آن‌چه
    // **دارد** بلندتر نباشد.

    private static IEnumerable<string> Overflow(Window win, MainViewModel vm)
    {
        var bad = new List<string>();
        var head = false;

        // ⚠️ صفحهٔ «حسابِ قرض‌دار» بخشِ سرِ خودش نیست — صفحه‌ای است داخلِ
        // «قرض‌داران» — پس در ‎vm.Sections‎ نمی‌آید و اگر باز نشود، همان ستونِ
        // «نوع تیل» که گزارش شد اصلاً سنجیده نمی‌شود.
        foreach (var (id, content) in Targets(win, vm))
        {
            // ⚠️ کلِ پنجره را می‌گردیم، نه قابِ همان بخش را: صفحهٔ «حسابِ
            // قرض‌دار» خودش بخش نیست و ‎ContentControl‎ی که محتوایش با آن
            // برابر باشد پیدا نمی‌شد، پس همان ستونِ «نوع تیل» که گزارش شده بود
            // اصلاً سنجیده نمی‌شد. بیرونِ بخش‌ها هم جدولی نیست.
            var worst = new Dictionary<string, (double want, double have)>();
            foreach (var cell in win.GetVisualDescendants().OfType<DataGridCell>())
            {
                if (cell.Bounds.Height <= 0) continue;

                // ⚠️ ‎DesiredSize‎ این‌جا دروغ می‌گوید: آوالونیا آن را به همان
                // فضایی که داده شده می‌چسباند، پس محتوایی که بریده شده هم
                // «دقیقاً به اندازه» گزارش می‌شود. یک دور همین باعث شد سنجش
                // سبز بدهد در حالی که عکس، نصفهٔ «دیزل» را نشان می‌داد.
                //
                // پس به‌جای پرسیدن، **نگاه** می‌کنیم: هر فرزندِ کشیده‌شده کجا
                // نشسته، نسبت به خودِ خانه.
                var have = cell.Bounds.Height;
                var want = have;
                foreach (var d in cell.GetVisualDescendants().OfType<Control>())
                {
                    if (d.Bounds.Height <= 0 || !d.IsVisible) continue;
                    if (d.TranslatePoint(new Point(0, 0), cell) is not { } at) continue;
                    want = Math.Max(want, Math.Max(at.Y + d.Bounds.Height, -at.Y + have));
                }
                if (want - have <= 1) continue;

                var col = ColumnName(cell);
                if (!worst.TryGetValue(col, out var w) || want - have > w.want - w.have)
                    worst[col] = (want, have);
            }

            foreach (var (col, w) in worst)
            {
                if (!head)
                {
                    head = true;
                    Console.WriteLine();
                    Console.WriteLine("بخش            ستون              می‌خواهد   دارد   سرریز");
                    Console.WriteLine(new string('-', 62));
                }
                Console.WriteLine($"{id,-14} {Cut(col),-17} {w.want,8:0} {w.have,6:0} "
                                + $"{w.want - w.have,7:0}   ✖");
                bad.Add($"{id} · خانهٔ «{col}» {w.want - w.have:0} پیکسل از ردیف بیرون می‌زند");
            }
        }

        return bad;
    }

    /// <summary>هر بخش، و بعد صفحهٔ حسابِ نخستین قرض‌دار.</summary>
    private static IEnumerable<(string Id, object Content)> Targets(Window win, MainViewModel vm)
    {
        foreach (var sec in vm.Sections.ToList())
        {
            Wait(win, vm.GoAsync(sec));
            for (var k = 0; k < 4; k++) { Dispatcher.UIThread.RunJobs(); Pump(win); }
            yield return (sec.Id, sec);

            if (sec is not PumpYaqobi.App.ViewModels.ICardGridHost cards) continue;
            Wait(win, cards.OpenByNumberAsync(1));
            for (var k = 0; k < 4; k++) { Dispatcher.UIThread.RunJobs(); Pump(win); }
            if (sec.ActivePage is { } page) yield return (sec.Id + "/صفحه", page);
        }
    }

    private static string ColumnName(DataGridCell cell)
    {
        var grid = cell.GetVisualAncestors().OfType<DataGrid>().FirstOrDefault();
        if (grid is null) return "";
        foreach (var c in grid.Columns)
            if (ReferenceEquals(c, cell.GetType().GetProperty("OwningColumn",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic
                  | System.Reflection.BindingFlags.Public)?.GetValue(cell)))
                return c.Header?.ToString()?.Trim() ?? "";
        return "";
    }

    /// <summary>نسبتِ کنتراستِ دو رنگ — همان فرمولِ استانداردِ دسترسی‌پذیری.</summary>
    private static double Ratio(Color a, Color b)
    {
        var (x, y) = (Luma(a) + 0.05, Luma(b) + 0.05);
        return x > y ? x / y : y / x;
    }

    private static double Luma(Color c) =>
        (0.2126 * c.R + 0.7152 * c.G + 0.0722 * c.B) / 255.0;

    private static string Hex(Color c) => $"#{c.R:x2}{c.G:x2}{c.B:x2}";

    private static string Cut(string s) => s.Length <= 15 ? s : s[..15];

    private static void Wait(Window w, Task t)
    {
        var end = DateTime.UtcNow + TimeSpan.FromSeconds(30);
        while (!t.IsCompleted && DateTime.UtcNow < end)
        { Dispatcher.UIThread.RunJobs(); w.UpdateLayout(); Thread.Sleep(5); }
        Pump(w);
    }

    private static void Pump(Window w)
    {
        for (var i = 0; i < 8; i++) { Dispatcher.UIThread.RunJobs(); w.UpdateLayout(); }
    }
}
