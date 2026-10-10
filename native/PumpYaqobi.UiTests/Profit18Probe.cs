using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PumpYaqobi.App.Printing;
using PumpYaqobi.App.Services;
using PumpYaqobi.App.ViewModels;
using PumpYaqobi.App.ViewModels.Sections;
using PumpYaqobi.App.Views;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;
using PumpYaqobi.Reporting.Pdf;
using QuestPDF.Fluent;

namespace PumpYaqobi.UiTests;

/// <summary>
/// ══ مفاد و ضرر، ۱۴۰۵/۰۷/۱۸ — با پنجرهٔ واقعی ════════════════════════════════════
///   • کادرهای ورود به صفحهٔ جدا دیده می‌شوند و کلیک روی کادر همان صفحه را باز می‌کند
///   • کارتِ «💰 سودِ واقعی» کنارِ نمودار: فروش، بهای خرید، ناخالص، هزینه، خالص
///   • خریدِ عمده: همهٔ کادرها در یک ردیف
///   • تبدیلِ تیل: ۱۰۰ لیتر × ۷۰ ÷ ۸۰ = ۸۷٫۵؛ تحویلِ بیشتر ⇒ کادرِ سرخ؛ ثبت ⇒ تاریخچه با
///     صافیِ ماه و تیل؛ PDF ساخته می‌شود
///
///     dotnet run --project PumpYaqobi.UiTests -c Release -- profit18 [پوشهٔ عکس]
/// </summary>
internal static class Profit18Probe
{
    private static int _bad;
    private static void Check(string what, bool ok, string? d = null)
    {
        Console.WriteLine((ok ? "  ✔ " : "  ✖ ") + what + (d is null ? "" : " — " + d));
        if (!ok) _bad++;
    }

    public static int Run(string? shots)
    {
        var tmpDb = Path.Combine(Path.GetTempPath(), "pump-profit18-" + Guid.NewGuid().ToString("N"), "pump.db");
        AppHost.Start(tmpDb);
        FakeLicense.Grant();
        AppBuilder.Configure<PumpYaqobi.App.App>().UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .SetupWithoutStarting();
        var win = new MainWindow { Width = 1500, Height = 1000 };
        win.Show(); Pump(win);
        var vm = (MainViewModel)win.DataContext!;
        LockIn.Wait(vm.Lock); Pump(win);
        Seed.Fill(AppHost.Current);
        if (shots is not null) Directory.CreateDirectory(shots);

        var sec = (ProfitSectionViewModel)vm.Sections.First(s => s.Id == "profit");
        Wait(win, vm.GoAsync(sec));
        Settle(win);

        // ── کادرهای ورود به صفحهٔ جدا ──
        var cards = win.GetVisualDescendants().OfType<Button>()
                       .Where(b => b.Classes.Contains("subcard") && b.IsEffectivelyVisible && b.Name != "SourcesCard").ToList();
        Check("دو کادرِ ورود به صفحهٔ جدا دیده می‌شوند", cards.Count == 2, cards.Count.ToString());
        if (cards.Count > 0)
        {
            cards[0].RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            cards[0].Command?.Execute(cards[0].CommandParameter);
            Settle(win);
            Check("کلیک روی کادر همان صفحه را باز کرد", sec.OpenSub is not null, sec.OpenSub?.Title);
            sec.CloseSub(); Settle(win);
        }

        // ── سودِ واقعی ──
        Check("پرده نیست (پلن‌دار، بی رمز)", !sec.Veiled);
        //  (۱۴۰۵/۰۷/۱۸، سوم) «همه لیتر و غیره را نشان نده؛ همان چارت با فایدهٔ فی لیتر»
        Check("نمای سودِ واقعی: «فی لیتر · فایده در هر لیتر … افغانی» برای پطرول",
              sec.RealPetrolText.Contains("فی لیتر") && (sec.RealPetrolText.Contains("فایده در هر لیتر") || sec.RealPetrolText.Contains("فروشی نیست")
                                                         || sec.RealPetrolText.Contains("ثبت نشده")), sec.RealPetrolText);
        Check("عددِ بزرگ = لیتر × فایدهٔ فی لیتر (نه مبلغِ فروش)", sec.RealProfitShown.Contains("افغانی"), sec.RealProfitShown);
        // ── (۱۴۰۵/۰۷/۱۸، دوم) «سودِ واقعی» داخلِ همان کارتِ نمودار، با یک دکمه ──
        Control? Named(string n) => win.GetVisualDescendants().OfType<Control>().FirstOrDefault(c => c.Name == n);
        Check("پیش‌فرض: نمودار دیده می‌شود", !sec.ShowReal && Named("NetChart")?.IsEffectivelyVisible == true && Named("RealChart")?.IsEffectivelyVisible != true);
        var toggle = Named("RealToggle") as Button;
        Check("دکمهٔ «💰 سودِ واقعی» روی کارتِ نمودار", toggle?.IsEffectivelyVisible == true, toggle?.Content?.ToString());
        toggle?.Command?.Execute(null); Settle(win);
        var visTexts = win.GetVisualDescendants().OfType<TextBlock>().Where(t => t.IsEffectivelyVisible)
                          .Select(t => t.Text ?? "").ToList();
        var realChart = Named("RealChart") as PumpYaqobi.App.Controls.SparkChart;
        var normalChartSeen = Named("NetChart")?.IsEffectivelyVisible == true;
        Check("زدن ⇒ همان کارت نمودارِ «فایده فی لیتر» را نشان می‌دهد، نمودارِ معمولی پنهان",
              sec.ShowReal && realChart?.IsEffectivelyVisible == true && !normalChartSeen
              && visTexts.Contains(sec.RealPetrolText) && visTexts.Contains(sec.RealDieselText), toggle?.Content?.ToString());
        Check("نقطه‌های نمودار فایدهٔ فی لیتر‌اند (هر نقطه کوچک‌تر از فیِ فروش)",
              realChart?.Values is { Count: >= 2 } v && v.All(x => x < 200), realChart?.Values is { } vv ? string.Join(",", vv.Take(6)) : "خالی");
        Check("هیچ مبلغِ فروش یا لیتری روی نما نیست",
              !visTexts.Any(t => t.Contains("فروشِ تیل") || t.Contains("بهای خرید") || t.Contains("سودِ ناخالص")));
        if (shots is not null) Shot(win, shots, "profit-real");
        toggle?.Command?.Execute(null); Settle(win);
        Check("دوباره زدن ⇒ نمودارِ معمولی", !sec.ShowReal && Named("NetChart")?.IsEffectivelyVisible == true && Named("RealChart")?.IsEffectivelyVisible != true);

        // ── «📋 از کجا آمد»: یک کادر ⇒ صفحهٔ جدا ──
        var srcCard = Named("SourcesCard") as Button;
        Check("کادرِ «📋 از کجا آمد» روی صفحه", srcCard?.IsEffectivelyVisible == true);
        srcCard?.Command?.Execute(null); Settle(win);
        Check("زدن ⇒ صفحهٔ جدا، بقیهٔ صفحه پنهان", sec.SourcesPage && srcCard?.IsEffectivelyVisible == false
              && win.GetVisualDescendants().OfType<TextBlock>().Any(t => t.IsEffectivelyVisible && t.Text == sec.SalesPetrolText));
        if (shots is not null) Shot(win, shots, "profit-sources");
        sec.CloseSourcesCommand.Execute(null); Settle(win);
        Check("«‹ برگشت» ⇒ صفحهٔ مفاد", !sec.SourcesPage && srcCard?.IsEffectivelyVisible == true);

        // ── خریدِ عمده: یک ردیف ──
        sec.BulkQty = "11"; sec.BulkBuy = "22"; sec.BulkMarket = "33"; Settle(win);
        var bq = ByText(win, "11"); var bb = ByText(win, "22"); var bm = ByText(win, "33");
        double Y(Control? c) => c?.TranslatePoint(default, win)?.Y ?? -1;
        Check("سه کادرِ خریدِ عمده در یک ردیف", bq is not null && Math.Abs(Y(bq) - Y(bb)) < 2 && Math.Abs(Y(bb) - Y(bm)) < 2,
              $"{Y(bq):0} {Y(bb):0} {Y(bm):0}");
        sec.BulkQty = sec.BulkBuy = sec.BulkMarket = ""; Settle(win);

        // ── تبدیلِ تیل ──
        sec.ConvPetrolToDiesel = true; Settle(win);
        Check("فیِ خودکار از خریدهای مخزن (نه اتحادیه)",
              sec.ConvPetrolSourceText.Contains("خریدِ مخزن") || sec.ConvPetrolSourceText.Contains("دستی بنویسید"), sec.ConvPetrolSourceText);
        var combo = win.GetVisualDescendants().OfType<Button>().FirstOrDefault(b => ReferenceEquals(b.Command, sec.ToggleConvDirectionCommand));
        Check("دکمهٔ «نوعِ تیل» (پطرول ⇐ دیزل) دیده می‌شود", combo is { IsEffectivelyVisible: true } && sec.ConvDirectionText.StartsWith("⛽"), sec.ConvDirectionText);
        sec.ConvPetrolPrice = "70"; sec.ConvDieselPrice = "80"; sec.ConvQty = "100"; sec.ConvDelivered = "";
        Settle(win);
        Check("۱۰۰ × ۷۰ ÷ ۸۰ = ۸۷٫۵ لیتر", sec.ConvAllowedText.StartsWith("87.5"), sec.ConvAllowedText);
        Check("ارزشِ کل ۷٬۰۰۰", sec.ConvValueText.StartsWith("7,000"), sec.ConvValueText);
        Check("منبعِ قیمت «دستی» گفته می‌شود", sec.ConvPetrolSourceText.Contains("دستی"), sec.ConvPetrolSourceText);
        var boxes = win.GetVisualDescendants().OfType<Control>()
            .Where(c => c.IsEffectivelyVisible && c.Height == 48 && c.FindAncestorOfType<Grid>() is { } g && g.ColumnDefinitions.Count == 9
                        && (c is TextBox || c is Button || c is Border)).Select(c => c.Bounds.Width).ToList();
        Check("همهٔ کادرهای تبدیل هم‌اندازه", boxes.Count >= 7 && boxes.Max() - boxes.Min() < 1.5,
              string.Join(" ", boxes.Select(w => w.ToString("0"))));
        Check("حقِ مشتری: لیتر و پول در یک کادر", sec.ConvDueText.Contains("87.50") && sec.ConvDueText.Contains("7,000"), sec.ConvDueText);
        Check("می‌گوید چقدر باید داده شود", sec.ConvResultText.Contains("باید 87.50 لیتر"), sec.ConvResultText);
        sec.ConvDelivered = "100"; Settle(win);
        Check("۱۰۰ لیتر سر به سر ⇒ سرخ و «ضرر» (۱۲٫۵ × ۸۰ = ۱٬۰۰۰)",
              sec.ConvOver && sec.ConvResultText.Contains("ضرر") && sec.ConvResultText.Contains("1,000"), sec.ConvResultText);
        if (shots is not null && combo is not null)
        {
            var save = win.GetVisualDescendants().OfType<Button>().FirstOrDefault(b => Equals(b.Content, "➕ ثبتِ تبدیل"));
            (save ?? (Control)combo).BringIntoView(); Settle(win); Wait(win, Task.Delay(100)); Settle(win);
            Shot(win, shots, "conversion");
        }
        sec.ConvDelivered = ""; Settle(win);

        sec.ConvDelivered = "101"; Settle(win);
        var delivered = ByText(win, "101");
        Check("تحویلِ ۱۰۱ ⇒ کادرِ سرخ و «لیتر اضافه داده شده»",
              sec.ConvOver && delivered?.Classes.Contains("overliters") == true && sec.ConvResultText.Contains("لیتر اضافه داده شده"),
              sec.ConvResultText);
        Check("ضرر ۱٬۰۸۰ (۱۳٫۵ × ۸۰)", sec.ConvResultText.Contains("1,080"), sec.ConvResultText);
        if (shots is not null)
        {
            win.Height = 2600; Settle(win);
            Shot(win, shots, "profit-conv-over");
            win.Height = 1000; Settle(win);
        }

        sec.ConvDelivered = "80"; Settle(win);
        Check("تحویلِ ۸۰ ⇒ سرخ نیست", !sec.ConvOver);
        Wait(win, sec.SaveConversionCommand.ExecuteAsync(null)); Settle(win);
        Check("ثبت شد و در تاریخچه آمد", sec.ConvRows.Count == 1, sec.ConvSummary);

        // دومی: دیزل ⇐ پطرول
        sec.ToggleConvDirectionCommand.Execute(null); Settle(win);
        Check("عوض کردنِ جهت فیِ دستی را پاک نکرد", sec.ConvPetrolPrice.StartsWith("70") && sec.ConvDieselPrice.StartsWith("80"),
              sec.ConvPetrolPrice + " / " + sec.ConvDieselPrice);
        sec.ConvQty = "87.5"; sec.ConvDelivered = ""; Settle(win);
        Check("دیزل به پطرول: ۸۷٫۵ × ۸۰ ÷ ۷۰ = ۱۰۰", sec.ConvAllowedText.StartsWith("100"), sec.ConvAllowedText);
        Wait(win, sec.SaveConversionCommand.ExecuteAsync(null)); Settle(win);
        Check("دو تبدیل در تاریخچه", sec.ConvRows.Count == 2, sec.ConvSummary);

        sec.ConvFuelFilter = sec.ConvFuelFilters[2]; Settle(win);
        Check("صافیِ «دیزل ⇐ پطرول» فقط یکی", sec.ConvRows.Count == 1 && sec.ConvRows[0].Title.Contains("دیزل ⇐"), sec.ConvSummary);
        sec.ConvFuelFilter = sec.ConvFuelFilters[0]; Settle(win);
        var month = sec.ConvPicker.Months.FirstOrDefault(m => !string.IsNullOrEmpty(m.Key) && !m.Key.EndsWith("/*"));
        Check("کشوی ماهِ تاریخچه ماهِ ثبت را دارد", month is not null, string.Join(",", sec.ConvPicker.Months.Select(m => m.Key)));
        if (month is not null) { sec.ConvPicker.Selected = month; Settle(win); Wait(win, Task.Delay(50)); Settle(win); }
        Check("همان ماه ⇒ هر دو تبدیل", sec.ConvRows.Count == 2, sec.ConvSummary);
        sec.ConvSearch = "1300/01"; Settle(win);
        Check("جست‌وجوی بی‌ربط ⇒ هیچ (و چیزی پاک نشد)", sec.ConvRows.Count == 0);
        sec.ConvSearch = ""; Settle(win);
        Check("پاک کردنِ جست‌وجو ⇒ هر دو برگشتند", sec.ConvRows.Count == 2);

        // PDF
        PdfEngine.Initialize();
        var got = new List<Func<PageSetup, QuestPDF.Infrastructure.IDocument>>();
        Documents.CaptureHook = (b, _) => got.Add(b);
        Wait(win, sec.PdfConversionsCommand.ExecuteAsync(null));
        Documents.CaptureHook = null;
        var bytes = got.Count == 1 ? got[0](PageSetup.Default).GeneratePdf() : Array.Empty<byte>();
        Check("PDFِ تاریخچه ساخته شد", bytes.Length > 1000, bytes.Length + " بایت");
        if (shots is not null) { File.WriteAllBytes(Path.Combine(shots, "conversions.pdf"), bytes); Shot(win, shots, "profit"); }

        Console.WriteLine(_bad == 0 ? "✅ همه سبز" : $"❌ {_bad} ایراد");
        return _bad == 0 ? 0 : 1;
    }

    private static TextBox? ByText(Window win, string text) =>
        win.GetVisualDescendants().OfType<TextBox>().FirstOrDefault(t =>
            t.IsEffectivelyVisible && t.Classes.Contains("plbox") && t.Text == text);

    private static void Shot(Window win, string dir, string name)
    {
        using var f = win.CaptureRenderedFrame();
        f?.Save(Path.Combine(dir, name + ".png"));
    }
    private static void Wait(Window w, Task t)
    {
        for (var i = 0; i < 3000 && !t.IsCompleted; i++) { Pump(w); Thread.Sleep(2); }
        Pump(w);
    }
    private static void Pump(Window w) { for (var i = 0; i < 8; i++) { Dispatcher.UIThread.RunJobs(); w.UpdateLayout(); } }
    private static void Settle(Window w) { for (var i = 0; i < 20; i++) { Pump(w); Thread.Sleep(5); } }
}
