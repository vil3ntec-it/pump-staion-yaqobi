using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Layout;
// ‎ItemsRepeater‎ و ‎UniformGridLayout‎ از بستهٔ جداگانهٔ خودشان می‌آیند
using Avalonia.Controls.Templates;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PumpYaqobi.App.Controls;

namespace PumpYaqobi.UiTests;

/// <summary>
/// ══ آیا جدول واقعاً مجازی‌سازی می‌کند؟ ═══════════════════════════════════════
///
/// ادعا در کد ارزشی ندارد؛ این‌جا اندازه گرفته می‌شود. یک ‎ExcelGrid‎ واقعی
/// در پنجره‌ای واقعی ساخته می‌شود، بار به بار داده‌اش بزرگ‌تر می‌شود
/// (۱۰۰۰ → یک میلیون) و هر بار سه چیز شمرده می‌شود:
///
///   • چند ردیفِ **زنده** در درختِ بصری هست (‎DataGridRow‎)
///   • ساختِ نخستین چقدر طول کشید
///   • یک اسکرول به وسطِ داده چقدر طول کشید
///
/// معیارِ قبولی همان است که صاحب ریپو نوشت: «تعداد رکوردها نباید مستقیماً
/// تعداد عناصر را بالا ببرد». پس اگر داده هزار برابر شود و ردیف‌های زنده هم
/// چند برابر شوند، این آزمون می‌شکند.
/// </summary>
internal static class GridPerf
{
    private sealed class Row
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Amount { get; set; } = "";
    }

    /// <summary>بیشترین ردیفِ زندهٔ پذیرفتنی — یک صفحه به‌اضافهٔ حاشیهٔ بازیافت.</summary>
    private const int MaxLiveRows = 120;

    public static int Run()
    {
        AppBuilder.Configure<PumpYaqobi.App.App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .SetupWithoutStarting();

        var grid = new ExcelGrid
        {
            IsReadOnly = true,
            Columns =
            {
                new DataGridTextColumn { Header = "#",    Binding = new Avalonia.Data.Binding("Id") },
                new DataGridTextColumn { Header = "نام",  Binding = new Avalonia.Data.Binding("Name") },
                new DataGridTextColumn { Header = "مبلغ", Binding = new Avalonia.Data.Binding("Amount") },
            },
        };

        // پنجرهٔ واقعی، با همان اسکرولِ صفحه‌ای که برنامه دارد
        var page = new ScrollViewer { Name = "PageScroll", Content = grid };
        var win = new Window { Width = 1200, Height = 700, Content = page };
        win.Show();
        Pump(win);

        Console.WriteLine();
        Console.WriteLine("ردیف‌ها        ساخت      اسکرول    ردیفِ زنده   نتیجه");
        Console.WriteLine(new string('-', 58));

        var sizes = new[] { 1_000, 10_000, 100_000, 500_000, 1_000_000 };
        var bad = new List<string>();

        foreach (var n in sizes)
        {
            var data = new LazyRows(n);

            var build = Stopwatch.StartNew();
            grid.ItemsSource = data;
            Pump(win);
            build.Stop();

            var scroll = Stopwatch.StartNew();
            grid.ScrollIntoView(data[n / 2], null);
            Pump(win);
            scroll.Stop();

            var live = grid.GetVisualDescendants().OfType<DataGridRow>().Count();
            var ok = live <= MaxLiveRows;
            if (!ok) bad.Add($"{n:N0} ردیف → {live} ردیفِ زنده");

            Console.WriteLine($"{n,10:N0}  {build.ElapsedMilliseconds,7} ms  "
                            + $"{scroll.ElapsedMilliseconds,6} ms  {live,8}      {(ok ? "✔" : "✖")}");
        }

        Console.WriteLine();
        if (bad.Count == 0)
        {
            Console.WriteLine($"✅ ردیف‌های زنده زیرِ {MaxLiveRows} ماندند — مجازی‌سازی کار می‌کند");
            return 0;
        }

        Console.WriteLine("❌ مجازی‌سازی کار نمی‌کند:");
        foreach (var b in bad) Console.WriteLine("   • " + b);
        return 1;
    }

    /// <summary>
    /// فهرستِ تنبل — ردیف‌ها فقط وقتی ساخته می‌شوند که جدول واقعاً سراغشان
    /// برود. اگر آزمون خودش یک میلیون شیء بسازد، چیزی که می‌سنجیم حافظهٔ
    /// آزمون است نه رفتارِ جدول.
    /// </summary>
    private sealed class LazyRows(int count) : System.Collections.IList
    {
        public int Count => count;
        public bool IsFixedSize => true;
        public bool IsReadOnly => true;
        public bool IsSynchronized => false;
        public object SyncRoot => this;

        public object? this[int index]
        {
            get => new Row { Id = index + 1, Name = "قرض‌دار " + (index + 1), Amount = (index * 137 % 99999).ToString("N0") };
            set => throw new NotSupportedException();
        }

        public int Add(object? value) => throw new NotSupportedException();
        public void Clear() => throw new NotSupportedException();
        public bool Contains(object? value) => false;
        public int IndexOf(object? value) => value is Row r ? r.Id - 1 : -1;
        public void Insert(int index, object? value) => throw new NotSupportedException();
        public void Remove(object? value) => throw new NotSupportedException();
        public void RemoveAt(int index) => throw new NotSupportedException();
        public void CopyTo(Array array, int index) => throw new NotSupportedException();

        public System.Collections.IEnumerator GetEnumerator()
        {
            for (var i = 0; i < count; i++) yield return this[i]!;
        }
    }

    /// <summary>
    /// ══ فهرست‌های کارتی ═════════════════════════════════════════════════════
    /// قرض‌داران، شرکت‌ها، ورق‌ها و… جدول نیستند، ولی می‌توانند هزاران کارت
    /// شوند. این‌جا دو چیدمان کنارِ هم سنجیده می‌شوند تا معلوم شود آیا عوض
    /// کردنشان واقعاً چیزی عوض می‌کند یا نه:
    ///
    ///   • ‎ItemsControl‎ + ‎AutoFillPanel‎ — چیزی که امروز هست
    ///   • ‎ItemsRepeater‎ + ‎UniformGridLayout‎ — پیشنهاد
    ///
    /// «کارتِ زنده» یعنی ‎Border‎ی که واقعاً در درختِ بصری ساخته شده.
    /// </summary>
    public static int Cards()
    {
        AppBuilder.Configure<PumpYaqobi.App.App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .SetupWithoutStarting();

        const int n = 50_000;
        var data = new LazyRows(n);

        var card = new Avalonia.Controls.Templates.FuncDataTemplate<object?>(
            (_, _) => new Border { Height = 90, Child = new TextBlock { Text = "کارت" } }, true);

        // ── امروز ──
        var ic = new ItemsControl
        {
            ItemsSource = data,
            ItemTemplate = card,
            ItemsPanel = new Avalonia.Controls.Templates.FuncTemplate<Panel?>(
                () => new PumpYaqobi.App.Controls.AutoFillPanel { MinItemWidth = 252, Gap = 12 }),
        };
        var w1 = new Window { Width = 1200, Height = 700,
                              Content = new ScrollViewer { Name = "PageScroll", Content = ic } };
        w1.Show();
        var t1 = Stopwatch.StartNew();
        Pump(w1);
        t1.Stop();
        var live1 = w1.GetVisualDescendants().OfType<Border>().Count(b => b.Height == 90);

        // ── پیشنهاد ──
        var ir = new ItemsRepeater
        {
            ItemsSource = data,
            ItemTemplate = card,
            Layout = new UniformGridLayout { MinItemWidth = 252, MinColumnSpacing = 12, MinRowSpacing = 12 },
        };
        var w2 = new Window { Width = 1200, Height = 700,
                              Content = new ScrollViewer { Name = "PageScroll", Content = ir } };
        w2.Show();
        var t2 = Stopwatch.StartNew();
        Pump(w2);
        t2.Stop();
        var live2 = w2.GetVisualDescendants().OfType<Border>().Count(b => b.Height == 90);

        Console.WriteLine();
        Console.WriteLine($"فهرستِ کارتی با {n:N0} آیتم");
        Console.WriteLine(new string('-', 58));
        Console.WriteLine($"ItemsControl + AutoFillPanel   {t1.ElapsedMilliseconds,6} ms   {live1,8} کارتِ زنده");
        Console.WriteLine($"ItemsRepeater + UniformGrid    {t2.ElapsedMilliseconds,6} ms   {live2,8} کارتِ زنده");
        Console.WriteLine();

        if (live2 <= MaxLiveRows)
        {
            Console.WriteLine("✅ چیدمانِ پیشنهادی مجازی‌سازی می‌کند");
            return 0;
        }
        Console.WriteLine("❌ چیدمانِ پیشنهادی هم مجازی‌سازی نمی‌کند — دنبالِ راهِ دیگری باید بود");
        return 1;
    }

    private static void Pump(Window w)
    {
        for (var i = 0; i < 4; i++)
        {
            Dispatcher.UIThread.RunJobs();
            w.Measure(new Size(w.Width, w.Height));
            w.Arrange(new Rect(0, 0, w.Width, w.Height));
            Dispatcher.UIThread.RunJobs();
        }
    }
}
