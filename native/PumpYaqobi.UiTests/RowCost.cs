using System.Diagnostics;
using Avalonia;
using Avalonia.Layout;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Headless;
using Avalonia.Markup.Xaml.Templates;
using Avalonia.Threading;
using PumpYaqobi.App.Controls;

namespace PumpYaqobi.UiTests;

/// <summary>
/// ══ یک ردیف چقدر آب می‌خورد ═════════════════════════════════════════════════
///
/// سنجشِ ‎waraqperf‎ ثابت کرد وقتِ باز شدنِ ورق **یک پاسِ چیدمان** است و بس:
/// ۱٬۰۸۷ میلی‌ثانیه برای یک ‎UpdateLayout()‎ روی ۸۰ ردیف. یعنی هر ردیف حدودِ
/// ۱۲ میلی‌ثانیه — که برای شش خانهٔ متنی خیلی زیاد است.
///
/// ولی «زیاد است» حدس است. این‌جا همان ردیف در خلأ سنجیده می‌شود تا معلوم شود
/// هزینه مالِ کیست: خودِ ‎DataGrid‎ی آوالونیا، سبکِ برنامه، یا ‎ExcelGrid‎.
///
///     dotnet run --project PumpYaqobi.UiTests -- rowcost
/// </summary>
internal static class RowCost
{
    private sealed class Row
    {
        public string A { get; set; } = "مشتری شمارهٔ ۱۲۳";
        public string B { get; set; } = "۱۲٬۳۴۵";
        public string C { get; set; } = "۶۷";
        public string D { get; set; } = "۸۲۷٬۱۱۵";
        public string E { get; set; } = "قرض";
        public string F { get; set; } = "تیل";
    }

    public static int Run()
    {
        AppBuilder.Configure<PumpYaqobi.App.App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .SetupWithoutStarting();

        Console.WriteLine();
        Console.WriteLine("ردیف   جدول                      نخستین چیدمان   هر ردیف");
        Console.WriteLine(new string('-', 64));

        foreach (var n in new[] { 20, 60, 100 })
        {
            Measure($"ExcelGrid (هم‌قدِ ردیف‌ها)", n, () => new ExcelGrid { GrowsToContent = true });
            Measure($"DataGrid خالیِ آوالونیا", n, () => new DataGrid
            {
                AutoGenerateColumns = false,
                HeadersVisibility = DataGridHeadersVisibility.Column,
                IsReadOnly = true,
            });
            Console.WriteLine();
        }

        return 0;
    }

    private static void Measure(string what, int rows, Func<DataGrid> make)
    {
        var grid = make();
        foreach (var (head, path) in new[] { ("نام", "A"), ("مقدار", "B"), ("فی", "C"),
                                             ("مبلغ", "D"), ("نوع", "E"), ("واحد", "F") })
            grid.Columns.Add(new DataGridTextColumn
            {
                Header = head,
                Binding = new Binding(path),
                Width = new DataGridLength(1, DataGridLengthUnitType.Auto),
            });

        grid.ItemsSource = Enumerable.Range(0, rows).Select(_ => new Row()).ToList();

        var page = new ScrollViewer { Name = "PageScroll", Content = grid };
        var win = new Window { Width = 1440, Height = 900, Content = page };
        win.Show();

        // یک پاسِ گرم‌کننده تا قالب و سبک‌ها سرِ جایشان بنشینند
        Dispatcher.UIThread.RunJobs();
        win.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        win.UpdateLayout();

        // حالا همان کاری که «باز شدنِ ورق» می‌کند: کلِ زیردرخت دوباره چیده شود
        grid.InvalidateMeasure();
        foreach (var d in grid.GetVisualDescendantsSafe().OfType<Layoutable>()) d.InvalidateMeasure();

        var sw = Stopwatch.StartNew();
        win.UpdateLayout();
        sw.Stop();

        var live = grid.GetVisualDescendantsSafe().OfType<DataGridRow>().Count();
        Console.WriteLine($"{rows,4}   {Pad(what, 26)} {sw.ElapsedMilliseconds,10:N0} ms "
                        + $"{(live == 0 ? 0 : sw.ElapsedMilliseconds / (double)live),8:N2} ms   "
                        + $"({live} ردیفِ زنده)");

        win.Close();
    }

    private static string Pad(string s, int n) => s.Length >= n ? s[..n] : s + new string(' ', n - s.Length);

    private static IEnumerable<Visual> GetVisualDescendantsSafe(this Visual v) =>
        Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(v);
}
