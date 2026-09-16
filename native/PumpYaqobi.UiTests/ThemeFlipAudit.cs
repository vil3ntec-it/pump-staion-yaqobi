using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PumpYaqobi.App.Controls;
using PumpYaqobi.App.Themes;
using PumpYaqobi.App.ViewModels;
using PumpYaqobi.App.Views;

namespace PumpYaqobi.UiTests;

/// <summary>
/// ══ عوض کردنِ تم چیزی را کج نکند ═══════════════════════════════════════════
///
/// گزارشِ صاحب ریپو: «توی عوض کردنِ تم خیلی باگ به وجود میاد… خط‌ها رو کج
/// می‌کرد، یعنی از وسطِ کادر به چپ یا راست می‌اومدن.»
///
/// این سنجش هر بخش را باز می‌کند، جای هر نوشتهٔ سرستون، هر خانهٔ «جمله»،
/// هر نوشتهٔ داخلِ خانه و هر خطِ جدول را می‌نویسد؛ بعد تم را عوض می‌کند
/// (آبی ⇒ طلایی ⇒ آبی) و همه را دوباره می‌سنجد. هر چیزی که بیش از نیم پیکسل
/// جابه‌جا شده باشد، همان‌جا نام برده می‌شود.
///
///     dotnet run --project PumpYaqobi.UiTests -- themeflip
/// </summary>
internal static class ThemeFlipAudit
{
    private const double Slack = 0.75;

    private sealed record Mark(string Where, double Offset);

    public static int Run()
    {
        var tmpDb = Path.Combine(Path.GetTempPath(),
                                 "pump-themeflip-" + Guid.NewGuid().ToString("N"), "pump.db");
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
        Pump(win);
        Seed.Fill(PumpYaqobi.App.Services.AppHost.Current);

        var bad = new List<string>();
        var soft = new List<string>();
        var themes = PumpTheme.All.ToList();
        var first = vm.SelectedTheme;

        foreach (var sec in vm.Sections.ToList())
        {
            Wait(win, vm.GoAsync(sec));
            Settle(win);

            var host = win.GetVisualDescendants().OfType<ContentControl>()
                          .FirstOrDefault(c => ReferenceEquals(c.Content, sec));
            if (host is null) continue;
            var before = Measure(win, host);
            if (before.Count == 0) continue;

            // آبی ⇒ طلایی ⇒ … ⇒ آبی: هر تم یک‌بار، و آخر سر همان اولی
            var seq = themes.Where(t => !ReferenceEquals(t, first)).Append(first).ToList();
            foreach (var t in seq)
            {
                vm.SelectedTheme = t;
                Settle(win);
                var after = Measure(win, host);
                var moved = Diff(before, after);
                Console.WriteLine($"{sec.Id,-14} {t.Id,-6} {before.Count,4} سنجه   {moved.Count,3} جابه‌جا");
                foreach (var m in moved)
                {
                    Console.WriteLine($"     ✖ {m}");
                    bad.Add($"{sec.Id} · تمِ {t.Id} · {m}");
                }
            }
        }

        // ══ بخشی که **بعد** از تعویضِ تم ساخته می‌شود ════════════════════════
        // همان بخش، همان داده، ولی نوبتِ اول با تمِ آبی ساخته شده بود و حالا
        // با تمِ طلایی از نو باز می‌شود (بخشِ دیگری را وسط باز می‌کنیم تا
        // ویوی قبلی رها شود).
        var other = themes.First(t => !ReferenceEquals(t, first));
        foreach (var sec in vm.Sections.ToList())
        {
            vm.SelectedTheme = first;
            Wait(win, vm.GoAsync(sec));
            Settle(win);
            var host = win.GetVisualDescendants().OfType<ContentControl>()
                          .FirstOrDefault(c => ReferenceEquals(c.Content, sec));
            if (host is null) continue;
            var before = Measure(win, host);
            if (before.Count == 0) continue;

            vm.SelectedTheme = other;
            Settle(win);
            var elsewhere = vm.Sections.First(x => !ReferenceEquals(x, sec));
            Wait(win, vm.GoAsync(elsewhere));
            Settle(win);
            Wait(win, vm.GoAsync(sec));
            Settle(win);
            host = win.GetVisualDescendants().OfType<ContentControl>()
                      .FirstOrDefault(c => ReferenceEquals(c.Content, sec));
            if (host is null) continue;
            var after = Measure(win, host);
            var moved = Diff(before, after);
            Console.WriteLine($"{sec.Id,-14} تازه‌ساخته با {other.Id,-6} {before.Count,4} سنجه   {moved.Count,3} جابه‌جا");
            foreach (var m in moved) { Console.WriteLine($"     ✖ {m}"); bad.Add($"{sec.Id} · تازه‌ساخته با {other.Id} · {m}"); }
        }
        vm.SelectedTheme = first;

        // ══ سنجشِ پیکسلی — چیزی که چیدمان نمی‌بیند، رندر می‌بیند ══════════
        //
        // خط‌های جدول، سایه‌ها و حاشیه‌ها را چیدمان «جابه‌جا» نمی‌داند ولی
        // ممکن است کج کشیده شوند. پس هر بخش یک‌بار با تمِ آبی کشیده می‌شود،
        // بعد آبی ⇒ طلایی ⇒ آبی، و دوباره کشیده می‌شود: دو عکس باید یکی
        // باشند (جز ساعتِ سربرگ که ماسک می‌شود). و «طلاییِ تازه‌ساخته» با
        // «طلاییِ پس از تعویض» هم باید یکی باشند.
        Console.WriteLine();
        Console.WriteLine("سنجشِ پیکسلی (درصدِ پیکسل‌های متفاوت):");
        var shotDir = Path.Combine(Path.GetTempPath(), "pump-themeflip-shots");
        Directory.CreateDirectory(shotDir);
        foreach (var sec in vm.Sections.ToList())
        {
            if (sec.Id is "cameras" or "dashboard") continue;   // ساعت و دوربین هر بار فرق دارند
            vm.SelectedTheme = first;
            Wait(win, vm.GoAsync(sec)); Settle(win);
            var a = Pixels(win);
            vm.SelectedTheme = other; Settle(win);
            var goldFlipped = Pixels(win);
            vm.SelectedTheme = first; Settle(win);
            var b = Pixels(win);
            var back = Diff(a, b, (int)win.Width);

            // طلاییِ تازه: بخشِ دیگر باز، تم طلایی، برگشت
            var elsewhere = vm.Sections.First(x => !ReferenceEquals(x, sec));
            vm.SelectedTheme = other; Settle(win);
            Wait(win, vm.GoAsync(elsewhere)); Settle(win);
            Wait(win, vm.GoAsync(sec)); Settle(win);
            var goldFresh = Pixels(win);
            var fresh = Diff(goldFlipped, goldFresh, (int)win.Width);

            Console.WriteLine($"{sec.Id,-14} آبی⇒طلایی⇒آبی {back,6:0.00}%   طلاییِ تازه vs پس از تعویض {fresh,6:0.00}%");
            if (back > 0.2 || fresh > 0.2)
            {
                SavePng(win, Path.Combine(shotDir, sec.Id + "-gold-fresh.png"));
                vm.SelectedTheme = first; Settle(win);
                SavePng(win, Path.Combine(shotDir, sec.Id + "-blue-back.png"));
                // ⚠️ تا ۵٪ فقط گزارش می‌شود، نه ایراد: با ‎themediff‎ دیده شد که این
                // چند درصد لبهٔ یک‌پیکسلیِ کارت‌ها و نرمیِ حروف است (سایه و نورِ
                // لبه پس از تعویض با کسری از پیکسل جابه‌جا کشیده می‌شوند)، نه
                // جابه‌جاییِ خط یا نوشته — آن را سنجه‌های چیدمانِ بالا می‌گیرند.
                // بالاتر از ۵٪ یعنی چیزی واقعاً عوض شده.
                var msg = $"{sec.Id} · رندر پس از تعویضِ تم فرق دارد ({Math.Max(back, fresh):0.00}% پیکسل) — عکس‌ها در {shotDir}";
                if (back > 5 || fresh > 5) bad.Add(msg); else soft.Add(msg);
            }
        }
        vm.SelectedTheme = first;

        Console.WriteLine();
        if (soft.Count > 0)
        {
            Console.WriteLine($"ℹ️ {soft.Count} تفاوتِ ریزِ پیکسلی (لبه و نرمیِ حروف؛ زیرِ ۵٪):");
            foreach (var b in soft.Distinct()) Console.WriteLine("   • " + b);
        }
        if (bad.Count == 0)
        {
            Console.WriteLine("✅ عوض کردنِ تم هیچ خط و نوشته‌ای را جابه‌جا نمی‌کند");
            return 0;
        }
        Console.WriteLine($"❌ {bad.Count} ایراد:");
        foreach (var b in bad.Distinct()) Console.WriteLine("   • " + b);
        return 1;
    }

    /// <summary>هر چیزی که جایش مهم است، با فاصله‌اش از مرکزِ صاحبش.</summary>
    private static List<Mark> Measure(Visual root, Control host)
    {
        var list = new List<Mark>();
        var grids = host.GetVisualDescendants().OfType<DataGrid>()
                        .Where(g => g.IsEffectivelyVisible).ToList();
        var gi = 0;
        foreach (var grid in grids)
        {
            gi++;
            foreach (var h in grid.GetVisualDescendants().OfType<DataGridColumnHeader>()
                                  .Where(h => h.Bounds.Width > 0))
            {
                var name = h.Content?.ToString()?.Trim() ?? "";
                if (name.Length == 0) continue;
                var text = h.GetVisualDescendants().OfType<TextBlock>()
                            .FirstOrDefault(t => (t.Text ?? "").Trim() == name);
                if (text is not null)
                    list.Add(new Mark($"جدول{gi} · سرستونِ «{name}»", MidX(root, text) - MidX(root, h)));
                var sep = h.GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Rectangle>()
                           .FirstOrDefault(r => r.Name == "VerticalSeparator");
                if (sep is not null)
                    list.Add(new Mark($"جدول{gi} · خطِ سرستونِ «{name}»", RightX(root, sep) - RightX(root, h)));
            }

            var ri = 0;
            foreach (var row in grid.GetVisualDescendants().OfType<DataGridRow>()
                                    .Where(r => r.Bounds.Height > 0).Take(3))
            {
                ri++;
                var bottom = row.GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Rectangle>()
                                .FirstOrDefault(r => r.Name == "PART_BottomGridLine");
                if (bottom is not null)
                    list.Add(new Mark($"جدول{gi} · خطِ زیرِ ردیف {ri}", BottomY(root, bottom) - BottomY(root, row)));

                if (ri == 1 && Environment.GetEnvironmentVariable("PUMP_THEMEFLIP_DEBUG") is not null)
                    Console.WriteLine($"      [ردیف۱] جدول{gi} (جدول w={grid.Bounds.Width:0} سرستونِ ردیف={grid.RowHeaderWidth:0} ردیف w={row.Bounds.Width:0} x={row.Bounds.X:0}): " + string.Join(" | ",
                        row.GetVisualDescendants().OfType<DataGridCell>()
                           .Select(c => $"x={c.Bounds.X:0} w={c.Bounds.Width:0}")));
                // ⚠️ خانهٔ پُرکنندهٔ خودِ ‎DataGrid‎ (بیرونِ پهنای ستون‌ها) شمرده
                // نمی‌شود: پهنایش تا اولین بی‌اعتبارسازی کهنه می‌ماند (۴۲ ⇒ ۰)
                // ولی بیرونِ قابِ جدول است و هیچ‌وقت دیده نمی‌شود — با
                // ‎PUMP_THEMEFLIP_DEBUG=1‎ همین را می‌شود دید.
                var cellsArea = grid.Bounds.Width - (double.IsNaN(grid.RowHeaderWidth) ? 0 : grid.RowHeaderWidth) - 1;
                var ci = 0;
                foreach (var cell in row.GetVisualDescendants().OfType<DataGridCell>()
                                        .Where(c => c.Bounds.Width > 0 && c.Bounds.Right <= cellsArea + 3))
                {
                    ci++;
                    var line = cell.GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Rectangle>()
                                   .FirstOrDefault(r => r.Name == "PART_RightGridLine");
                    if (line is not null)
                        list.Add(new Mark($"جدول{gi} · خطِ خانه {ri}/{ci}", RightX(root, line) - RightX(root, cell)));
                    else if (Environment.GetEnvironmentVariable("PUMP_THEMEFLIP_DEBUG") is not null)
                        Console.WriteLine($"      [بی‌خط] جدول{gi} ردیف {ri} خانه {ci}: بچه‌ها: "
                            + string.Join(",", cell.GetVisualChildren().Select(c => c.GetType().Name + (c is Control cc ? "#" + cc.Name : ""))));
                    var tb = cell.GetVisualDescendants().OfType<TextBlock>()
                                 .FirstOrDefault(t => !string.IsNullOrWhiteSpace(t.Text));
                    if (tb is not null)
                        list.Add(new Mark($"جدول{gi} · نوشتهٔ خانه {ri}/{ci}", MidX(root, tb) - MidX(root, cell)));
                }
            }
        }

        // ══ هر نوشته‌ای در بخش، نسبت به کادرِ صاحبش ═══════════════════════
        // «از وسطِ کادر به چپ یا راست می‌آمدند» — پس نه فقط جدول: هر نوشته‌ای
        // که داخلِ یک Border/Panel است، فاصله‌اش تا مرکزِ همان کادر ثبت می‌شود.
        var ti = 0;
        foreach (var tb in host.GetVisualDescendants().OfType<TextBlock>()
                              .Where(t => t.IsEffectivelyVisible && t.Bounds.Width > 0
                                          && !string.IsNullOrWhiteSpace(t.Text)))
        {
            if (++ti > 1500) break;
            var owner = tb.GetVisualAncestors().OfType<Control>()
                          .FirstOrDefault(a => a is Border or Panel && a.Bounds.Width > tb.Bounds.Width + 4);
            if (owner is null) continue;
            var label = (tb.Text ?? "").Trim();
            if (label.Length > 24) label = label[..24];
            list.Add(new Mark($"نوشتهٔ «{label}» #{ti} در {owner.GetType().Name}", MidX(root, tb) - MidX(root, owner)));
        }
        var xi = 0;
        foreach (var box in host.GetVisualDescendants().OfType<TextBox>()
                               .Where(t => t.IsEffectivelyVisible && t.Bounds.Width > 0))
        {
            if (++xi > 300) break;
            var pres = box.GetVisualDescendants().OfType<Avalonia.Controls.Presenters.TextPresenter>().FirstOrDefault();
            if (pres is null) continue;
            list.Add(new Mark($"کادرِ تایپ #{xi} «{(box.Text ?? box.Watermark ?? "").Trim()}»", MidX(root, pres) - MidX(root, box)));
        }

        var si = 0;
        foreach (var strip in host.GetVisualDescendants().OfType<TotalsStrip>())
        {
            si++;
            var ci = 0;
            foreach (var c in strip.GetVisualChildren().OfType<Control>().Where(c => c.Bounds.Width > 0))
            {
                ci++;
                list.Add(new Mark($"نوارِ جمله {si} · خانه {ci}", MidX(root, c)));
            }
        }
        return list;
    }

    private static List<string> Diff(List<Mark> a, List<Mark> b)
    {
        var moved = new List<string>();
        var byName = b.GroupBy(m => m.Where).ToDictionary(g => g.Key, g => g.First());
        foreach (var m in a)
        {
            if (!byName.TryGetValue(m.Where, out var n)) { moved.Add($"{m.Where}: بعد از تم گم شد"); continue; }
            if (double.IsNaN(m.Offset) || double.IsNaN(n.Offset)) continue;
            var d = n.Offset - m.Offset;
            if (Math.Abs(d) > Slack) moved.Add($"{m.Where}: {d:+0.0;-0.0}px");
        }
        return moved;
    }

    /// <summary>
    /// ══ عکسِ تفاوت — برای چشم ═══════════════════════════════════════════════
    ///     dotnet run --project PumpYaqobi.UiTests -- themediff <پوشه> [بخش,بخش,…]
    /// برای هر بخش: ‎a.png‎ (آبی)، ‎b.png‎ (آبی پس از طلایی)، ‎diff.png‎ (سرخ = فرق)،
    /// و ‎gold-fresh.png‎ / ‎gold-flip.png‎ / ‎gold-diff.png‎.
    /// </summary>
    public static int RunDiff(string outDir, string? ids)
    {
        var tmpDb = Path.Combine(Path.GetTempPath(), "pump-themediff-" + Guid.NewGuid().ToString("N"), "pump.db");
        PumpYaqobi.App.Services.AppHost.Start(tmpDb);
        AppBuilder.Configure<PumpYaqobi.App.App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .SetupWithoutStarting();
        var win = new MainWindow { Width = 1440, Height = 900 };
        win.Show();
        Pump(win);
        var vm = (MainViewModel)win.DataContext!;
        vm.Lock.Password = "1234"; vm.Lock.Confirm = "1234";
        vm.Lock.SubmitCommand.Execute(null);
        Wait(win, Task.CompletedTask); Pump(win);
        Seed.Fill(PumpYaqobi.App.Services.AppHost.Current);
        Directory.CreateDirectory(outDir);

        var want = (ids ?? "shifts,debt,amanat").Split(',', StringSplitOptions.RemoveEmptyEntries);
        var themes = PumpTheme.All.ToList();
        var first = vm.SelectedTheme;
        var other = themes.First(t => !ReferenceEquals(t, first));
        foreach (var id in want)
        {
            var sec = vm.Sections.FirstOrDefault(s => s.Id == id.Trim());
            if (sec is null) continue;
            vm.SelectedTheme = first;
            Wait(win, vm.GoAsync(sec)); Settle(win);
            var a = Frame(win); SavePng(win, Path.Combine(outDir, id + "-a.png"));
            vm.SelectedTheme = other; Settle(win);
            var gf = Frame(win); SavePng(win, Path.Combine(outDir, id + "-gold-flip.png"));
            vm.SelectedTheme = first; Settle(win);
            var b = Frame(win); SavePng(win, Path.Combine(outDir, id + "-b.png"));
            WriteDiff(a, b, Path.Combine(outDir, id + "-diff.png"));

            var elsewhere = vm.Sections.First(x => !ReferenceEquals(x, sec));
            vm.SelectedTheme = other; Settle(win);
            Wait(win, vm.GoAsync(elsewhere)); Settle(win);
            Wait(win, vm.GoAsync(sec)); Settle(win);
            var gfresh = Frame(win); SavePng(win, Path.Combine(outDir, id + "-gold-fresh.png"));
            WriteDiff(gf, gfresh, Path.Combine(outDir, id + "-gold-diff.png"));
            Console.WriteLine($"{id}: آبی⇒طلایی⇒آبی {Diff(a.Bytes, b.Bytes, 0):0.00}%   طلایی {Diff(gf.Bytes, gfresh.Bytes, 0):0.00}%  ({Box(a.Bytes, b.Bytes, a.Stride)})");
        }
        return 0;
    }

    private readonly record struct FrameData(byte[] Bytes, int Stride, int W, int H);

    private static FrameData Frame(Window win)
    {
        using var frame = win.CaptureRenderedFrame();
        if (frame is null) return new(Array.Empty<byte>(), 0, 0, 0);
        using var fb = frame.Lock();
        var bytes = new byte[fb.RowBytes * fb.Size.Height];
        System.Runtime.InteropServices.Marshal.Copy(fb.Address, bytes, 0, bytes.Length);
        return new(bytes, fb.RowBytes, fb.Size.Width, fb.Size.Height);
    }

    /// <summary>جعبهٔ دربرگیرندهٔ پیکسل‌های متفاوت — «کجا فرق دارد».</summary>
    private static string Box(byte[] a, byte[] b, int stride)
    {
        int minX = int.MaxValue, minY = int.MaxValue, maxX = -1, maxY = -1;
        if (a.Length != b.Length || stride == 0) return "?";
        for (var i = 0; i + 3 < a.Length; i += 4)
        {
            if (Math.Abs(a[i] - b[i]) > 8 || Math.Abs(a[i + 1] - b[i + 1]) > 8 || Math.Abs(a[i + 2] - b[i + 2]) > 8)
            {
                var y = i / stride; var x = (i % stride) / 4;
                if (x < minX) minX = x; if (x > maxX) maxX = x; if (y < minY) minY = y; if (y > maxY) maxY = y;
            }
        }
        return maxX < 0 ? "هیچ" : $"x {minX}–{maxX}, y {minY}–{maxY}";
    }

    private static void WriteDiff(FrameData a, FrameData b, string path)
    {
        if (a.Bytes.Length == 0 || a.Bytes.Length != b.Bytes.Length) return;
        var bmp = new Avalonia.Media.Imaging.WriteableBitmap(new PixelSize(a.W, a.H), new Vector(96, 96),
            Avalonia.Platform.PixelFormat.Bgra8888, Avalonia.Platform.AlphaFormat.Premul);
        using (var fb = bmp.Lock())
        {
            var outBytes = new byte[fb.RowBytes * a.H];
            for (var y = 0; y < a.H; y++)
                for (var x = 0; x < a.W; x++)
                {
                    var i = y * a.Stride + x * 4; var o = y * fb.RowBytes + x * 4;
                    var d = Math.Abs(a.Bytes[i] - b.Bytes[i]) > 8 || Math.Abs(a.Bytes[i + 1] - b.Bytes[i + 1]) > 8 || Math.Abs(a.Bytes[i + 2] - b.Bytes[i + 2]) > 8;
                    if (d) { outBytes[o] = 0; outBytes[o + 1] = 0; outBytes[o + 2] = 255; outBytes[o + 3] = 255; }
                    else { outBytes[o] = (byte)(a.Bytes[i] / 3 + 100); outBytes[o + 1] = (byte)(a.Bytes[i + 1] / 3 + 100); outBytes[o + 2] = (byte)(a.Bytes[i + 2] / 3 + 100); outBytes[o + 3] = 255; }
                }
            System.Runtime.InteropServices.Marshal.Copy(outBytes, 0, fb.Address, outBytes.Length);
        }
        bmp.Save(path);
    }

    /// <summary>پیکسل‌های پنجره (BGRA). سربرگِ ۱۳۰ پیکسلیِ بالا (ساعت) ماسک می‌شود.</summary>
    private static byte[] Pixels(Window win)
    {
        using var frame = win.CaptureRenderedFrame();
        if (frame is null) return Array.Empty<byte>();
        using var fb = frame.Lock();
        var bytes = new byte[fb.RowBytes * fb.Size.Height];
        System.Runtime.InteropServices.Marshal.Copy(fb.Address, bytes, 0, bytes.Length);
        var mask = Math.Min(bytes.Length, fb.RowBytes * 130);
        Array.Clear(bytes, 0, mask);
        return bytes;
    }

    private static double Diff(byte[] a, byte[] b, int width)
    {
        if (a.Length == 0 || a.Length != b.Length) return 100;
        long diff = 0, total = a.Length / 4;
        for (var i = 0; i + 3 < a.Length; i += 4)
            if (Math.Abs(a[i] - b[i]) > 8 || Math.Abs(a[i + 1] - b[i + 1]) > 8 || Math.Abs(a[i + 2] - b[i + 2]) > 8) diff++;
        return total == 0 ? 0 : 100.0 * diff / total;
    }

    private static void SavePng(Window win, string path)
    {
        try { using var f = win.CaptureRenderedFrame(); f?.Save(path); } catch { /* عکس رفاه است */ }
    }

    private static double MidX(Visual root, Visual v)
        => v.TranslatePoint(new Point(v.Bounds.Width / 2, 0), root)?.X ?? double.NaN;
    private static double RightX(Visual root, Visual v)
        => v.TranslatePoint(new Point(v.Bounds.Width, 0), root)?.X ?? double.NaN;
    private static double BottomY(Visual root, Visual v)
        => v.TranslatePoint(new Point(0, v.Bounds.Height), root)?.Y ?? double.NaN;

    private static void Settle(Window win)
    {
        for (var k = 0; k < 8; k++) { Dispatcher.UIThread.RunJobs(); Pump(win); }
    }

    private static void Pump(Window win)
    {
        Dispatcher.UIThread.RunJobs();
        win.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }

    private static void Wait(Window win, Task t)
    {
        while (!t.IsCompleted) { Dispatcher.UIThread.RunJobs(); Thread.Sleep(5); }
        t.GetAwaiter().GetResult();
        Pump(win);
    }
}
