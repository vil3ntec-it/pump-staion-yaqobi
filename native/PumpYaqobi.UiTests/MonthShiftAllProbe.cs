using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PumpYaqobi.App.Controls;
using PumpYaqobi.App.Services;
using PumpYaqobi.App.ViewModels;
using PumpYaqobi.App.ViewModels.Sections;
using PumpYaqobi.App.Views;
using PumpYaqobi.Application.Localization;
using PumpYaqobi.Domain.Entities;

namespace PumpYaqobi.UiTests;

/// <summary>
/// ══ «هیچ بخش یا ماه و سالِ قدیم و جدید… به چپ یا راست نمی‌ره؟ حتی مفاد و ضرر» (۱۴۰۵/۰۷/۲۱) ══
///
/// ‎monthshift‎ فقط چهار دفتر را می‌سنجید. این یکی <b>هر بخشی</b> را که کشوی سال/ماه یا
/// گزینهٔ بازه دارد — یا فقط نوشتهٔ ماه و سال نشان می‌دهد — روی یک دفترِ دوساله
/// (‎YearsAudit.Seed‎: هر روز ورق، پارچه، قرض‌دار، شرکت، خرید، حاضری…) با خودِ
/// کشوها جابه‌جا می‌کند: ماهِ جاری ⇒ کهنه‌ترین ماهِ امسال ⇒ سالِ پیش ⇒ سالِ کم‌ردیف
/// ⇒ «همهٔ ماه‌ها» ⇒ برگشت. بخشی که کشو ندارد پس از عوض کردنِ ماه در بخشِ دیگر
/// دوباره باز می‌شود. پس از هر حالت، «همان لحظه» و «پس از ته‌نشینی»:
///
///   ۰) هر نشانِ ثابتِ صفحه (نوشتهٔ ثابت، کشو، دکمه، سرستون، نخستین کارتِ هر فهرست)
///      همان X را دارد که سرِ باز شدن داشت (≤ ۱px)
///   ۱–۳) خانه، خطِ کنارِ خانه و نوارِ «جمله» سرِ لبهٔ سرستونش (≤ ۱px) — هر جدولِ دیدنی
///   ۴) جابه‌جاییِ وسط‌چینیِ ‎RtlTrim‎ همانی است که از چیدمانِ امروز درمی‌آید (≤ ۲px)
///   ۵) جوهرِ سرستون و خانه با پیکسلِ واقعیِ قاب وسطِ کادرش (≤ ۳px)
///
///     dotnet run --project PumpYaqobi.UiTests -c Release -- monthshift all [بخش]
/// </summary>
internal static class MonthShiftAllProbe
{
    private static int _bad;
    private static int TotalCorrections;
    private static int FrameOverlap;
    private static double FrameOverlapMax;
    private static int _checks;
    private static int _shot;
    private static readonly List<string> Summary = new();

    // ══ عکسِ مدرک: هر حالت یک قاب؛ پایانِ هر بخش دو حالتِ «دورترین» کنارِ هم ══
    private sealed record Shot(string Group, string Tag, byte[] Png, Dictionary<string, (double L, double R)> Edges, int Rows, bool Ok);
    private static readonly List<Shot> Shots = new();
    private static string CurTag = "";
    private static string CurGroup = "";
    private static string ShotDir = "";
    private static readonly List<string> ShotList = new();

    private static void Check(string what, bool ok, string? detail = null)
    {
        _checks++;
        Console.WriteLine((ok ? "  ✔ " : "  ✖ ") + what + (detail is null ? "" : " — " + detail));
        if (!ok) _bad++;
    }

    public static int Run(string[] args)
    {
        //  دو سال داده — پیش از نخستین دست زدن به ‎YearsAudit‎ (سازندهٔ ایستایش همین را می‌خواند)
        if (Environment.GetEnvironmentVariable("PUMP_YEARS") is null)
            Environment.SetEnvironmentVariable("PUMP_YEARS", "2");
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "pump-mshiftall-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        PumpYaqobi.App.Services.AppSettings.DirOverride = dir;
        var file = System.IO.Path.Combine(dir, "pump.db");
        Console.WriteLine("ساختنِ دفترِ واقعی‌نما (RealLedger: دو سال، ۱۴۰۴/۱۱ تا ماهِ جاری، با ردِ نسخه‌های پیشین)…");
        var empty = Environment.GetEnvironmentVariable("MS_EMPTY") == "1";
        var st = empty ? null : RealLedger.Build(file);
        if (st is not null) Console.WriteLine($"   دفترِ پول {st.MoneyRows} · رسیدِ تیل {st.RasidFuelRows} · ردیفِ کهنه {st.StaleRows} · کلیدِ صفر {st.ZeroKeys} · سربرگِ کهنه {st.StaleHeads} · فاکتورِ در صف {st.Pending}");
        ShotDir = Environment.GetEnvironmentVariable("MS_SHOTS")
                  ?? System.IO.Path.Combine(FindNative(), "shots-monthshift");
        var parityOn = args.Skip(2).FirstOrDefault() is null or "parity";
        //  سنجهٔ برابری روزی یک بار است؛ مُهرِ «امروز سنجیده شد» پیش از ورود گذاشته می‌شود
        //  تا عکسِ «پیش از» گرفتنی باشد — بعد برداشته می‌شود و همان راهِ نیمه‌شبِ برنامه
        //  (‎MainViewModel.DayChanged ⇒ Parity.StartDaily‎) آن را می‌دواند.
        if (parityOn) StampParityDay(file, set: true);
        AppHost.Start(file);
        FakeLicense.Grant();
        AppBuilder.Configure<PumpYaqobi.App.App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .SetupWithoutStarting();

        var win = new MainWindow { Width = WinW, Height = WinH };
        if (Scale != 1.0) SetScale(win, Scale);
        win.Show();
        Console.WriteLine($"   پنجره {WinW}×{WinH} · RenderScaling {win.RenderScaling} · LayoutScale {Avalonia.Layout.LayoutHelper.GetLayoutScale(win)}");
        Pump(win);
        var vm = (MainViewModel)win.DataContext!;
        Vm = vm;
        LockIn.Wait(vm.Lock);
        var end = DateTime.UtcNow + TimeSpan.FromSeconds(180);
        while (vm.Phase == MainViewModel.AppPhase.Starting && DateTime.UtcNow < end)
        { Dispatcher.UIThread.RunJobs(); win.UpdateLayout(); Thread.Sleep(2); }
        if (!empty) SeedFewYear(AppHost.Current);
        Settle(win);
        AppHost.Current.Toasts.Visible = false;
        if (Environment.GetEnvironmentVariable("MS_THEME") is { Length: > 0 } th
            && PumpYaqobi.App.Themes.PumpTheme.All.FirstOrDefault(t => t.Id == th) is { } pt)
        { vm.SelectedTheme = pt; Settle(win); }
        Console.WriteLine($"   تم {vm.SelectedTheme.Id} · چرخهٔ تم {(ThemeCycle ? "روشن" : "خاموش")} · چرخهٔ تایپ {(EditCycle ? "روشن" : "خاموش")}");

        if (parityOn) ParityPhase(win, vm, file);
        var only = args.Skip(2).FirstOrDefault();
        if (only == "parity") only = "-";
        bool Want(string id) => only is null || only == id;

        //  ── بخش‌هایی که کشوی سال/ماه دارند ──
        foreach (var id in new[] { "waraq", "expenses", "safe", "sarrafi", "chakana", "debtrasid",
                                   "attendance", "monthreport", "profit" })
            if (Want(id)) Run(id, () => PickerSection(win, vm, id));

        //  «ردیف‌های فروشِ ورق را که از گاوصندوق پاک می‌کنم، همه برمی‌گردند وسط»
        if (Want("safedel")) Run("safedel", () => SafeDelete(win, vm));
        //  عکسِ صاحب ریپو (۱۴۰۵/۰۷/۲۲): ماهِ پیش با ردیف‌های خالیِ تازه (فقط تاریخ و «0»)
        foreach (var id in new[] { "expenses", "safe" })
            if (Want("blank-" + id)) Run("blank-" + id, () => BlankRows(win, vm, id));
        if (Want("history")) Run("history", () => History(win, vm));
        if (Want("dashboard")) Run("dashboard", () => Dashboard(win, vm));
        if (Want("shifts")) Run("shifts", () => ParchaReports(win, vm));
        if (Want("debt")) Run("debt", () => Person(win, vm));
        if (Want("noinv")) Run("noinv", () => Company(win, vm));

        //  ── بخش‌هایی که کشو ندارند: پس از عوض شدنِ ماه در بخشِ دیگر ──
        foreach (var id in new[] { "storage", "tanker", "invoices", "invrate", "membership", "oldloans",
                                   "ratehist", "staffshort", "rasid" })
            if (Want(id)) Run(id, () => Revisit(win, vm, id));

        Console.WriteLine();
        Console.WriteLine("════ خلاصه ════");
        foreach (var s in Summary) Console.WriteLine("  " + s);
        if (ShotList.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("════ عکس‌ها (" + ShotDir + ") ════");
            foreach (var l in ShotList) Console.WriteLine("  " + l);
        }
        Console.WriteLine($"   قابِ بدنهٔ بخش‌ها (‎ScaleBody‎) در کلِ اجرا {TotalCorrections} بار بدنهٔ لغزنده را سرِ جایش برگرداند");
        Console.WriteLine($"   ⚠️ خانهٔ نوارِ «جمله» زیرِ خطِ قابِ نوار (همان سرستونِ بریده‌شده): {FrameOverlap} بار، بیشترین {FrameOverlapMax:0.#}px");
        Console.WriteLine(_bad == 0 ? $"✅ {_checks} سنجه، همه سرِ جایش" : $"❌ {_bad} ایراد از {_checks} سنجه");
        return _bad == 0 ? 0 : 1;
    }

    private static string FindNative()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null && !File.Exists(System.IO.Path.Combine(d.FullName, "PumpYaqobi.sln"))) d = d.Parent;
        return d?.FullName ?? Directory.GetCurrentDirectory();
    }

    private static void StampParityDay(string file, bool set)
    {
        using var c = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=" + file + ";Pooling=False");
        c.Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "DELETE FROM Settings WHERE Key = $k";
        cmd.Parameters.AddWithValue("$k", PumpYaqobi.Services.Data.LedgerParityService.DayKey);
        cmd.ExecuteNonQuery();
        if (!set) return;
        var now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
        cmd.CommandText = "INSERT INTO Settings (Key, Value, CreatedAt, UpdatedAt) VALUES ($k, $v, $t, $t)";
        cmd.Parameters.AddWithValue("$v", Shamsi.Today());
        cmd.Parameters.AddWithValue("$t", now);
        cmd.ExecuteNonQuery();
    }

    private record UserNum(long Id, decimal L, decimal? P, decimal R, decimal RF, string? D);

    private static List<UserNum> UserNums()
    {
        using var db = AppHost.Current.Db.Create();
        return Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.AsNoTracking(db.DebtRows)
            .OrderBy(r => r.Id).Select(r => new UserNum(r.Id, r.Liters, r.PricePerLiter, r.Rasid, r.RasidFuel, r.DateShamsi)).ToList();
    }

    /// <summary>
    /// ══ «اون انالیزور کار می‌کنه؟» — روی همین دفتر، با همین پنجره ══════════════
    /// جدولِ ناجورها پیش از سنجه، عکسِ حسابِ قرض‌دار (دفترِ تیل و پول) پیش و پس،
    /// دویدنِ سنجه از راهِ نیمه‌شبِ خودِ برنامه، و ثابت ماندنِ همهٔ عددهای کاربر.
    /// </summary>
    private static void ParityPhase(MainWindow win, MainViewModel vm, string file)
    {
        Console.WriteLine();
        Console.WriteLine("════ سنجهٔ برابری (analyzer) ════");
        var h = AppHost.Current;
        //  راهِ ورود: ‎MainViewModel‎ پس از ورود ‎StartDaily‎ را زد (و چون مُهرِ امروز هست، کاری نکرد)
        Check("ورود سنجهٔ برابری را صدا زد (‎DailyTask‎ ساخته شد)", h.Parity.DailyTask is not null);
        if (h.Parity.DailyTask is { } t0) Wait(win, t0);
        Check("با مُهرِ امروز، بارِ دوم کاری نکرد", h.Parity.DailyTask?.Result == 0, "نتیجه " + h.Parity.DailyTask?.Result);

        var mism = h.Parity.CheckAsync(fix: false).GetAwaiter().GetResult();
        var dates = h.Parity.CheckDatesAsync(fix: false).GetAwaiter().GetResult();
        Console.WriteLine($"   پیش از سنجه: {mism.Count} عددِ مشتقِ ناجور · {dates} کلیدِ تاریخِ ناجور");
        foreach (var g in mism.GroupBy(m => m.Field))
            Console.WriteLine($"     {g.Key,-18}{g.Count(),6} ناجور");
        Console.WriteLine("     حساب    ردیف     ستون               روی دیسک         از روی ردیف‌ها");
        foreach (var m in mism.OrderBy(m => m.AccountId).ThenBy(m => m.RowId).Take(30))
            Console.WriteLine($"     {m.AccountId,-7} {m.RowId?.ToString() ?? "—",-8} {m.Field,-18} {m.Stored,16:0.##} {m.Truth,16:0.##}");
        Check("دفترِ کهنه واقعاً ناجور دارد (سنجه چیزی برای درست کردن دارد)", mism.Count > 50 && dates > 50, $"{mism.Count} · {dates}");
        var users = UserNums();

        //  عکسِ «پیش»: حسابِ قرض‌دارِ بزرگ (سربرگِ کهنه دارد) — دفترِ تیل و پول
        var debt = (DebtSectionViewModel)Open(win, vm, "debt");
        void Snap(string name)
        {
            debt.PersonOpen = false; Settle(win);
            //  کارتِ همان قرض‌دار در فهرست — این‌جا عددها از ستون‌های ذخیره‌شده خوانده می‌شوند
            Wait(win, vm.GoAsync(debt)); WaitRows(win);
            debt.Search = "قرض‌دارِ بزرگ"; WaitRows(win); Settle(win);
            AppHost.Current.Toasts.Visible = false; Settle(win);
            if (debt.Cards.FirstOrDefault(c => c.Name == "قرض‌دارِ بزرگ") is { } card)
                Console.WriteLine($"   [{name} · کارت] پول {card.MoneyText} · پطرول {card.PetrolText} · دیزل {card.DieselText}");
            {
                win.CaptureRenderedFrame()?.Dispose();
                using var cs = win.CaptureRenderedFrame()!;
                Directory.CreateDirectory(ShotDir);
                var cp = System.IO.Path.Combine(ShotDir, $"parity-{name}-card.png");
                cs.Save(cp);
                ShotList.Add($"{(name == "after" ? "✔" : "·")} {System.IO.Path.GetFileName(cp)} — debtor card «قرض‌دارِ بزرگ» {name} the parity pass");
            }
            debt.Search = ""; WaitRows(win); Settle(win);
            Wait(win, debt.OpenByNumberAsync(1)); WaitRows(win); Settle(win);
            var person = debt.ActivePage;
            var cur = person?.GetType().GetProperty("Current")?.GetValue(person);
            foreach (var book in new[] { false, true })
            {
                if (cur?.GetType().GetProperty("IsMoney") is { } pm && (bool)pm.GetValue(cur)! != book)
                { pm.SetValue(cur, book); WaitRows(win); Settle(win); }
                AppHost.Current.Toasts.Visible = false; Settle(win);
                var heads = cur?.GetType().GetProperties()
                    .Where(p => p.PropertyType == typeof(string) && p.Name.EndsWith("Text")
                                && (p.Name.Contains("Rasid") || p.Name.Contains("Baqi") || p.Name.Contains("Remain") || p.Name.Contains("Total")))
                    .Select(p => $"{p.Name}={p.GetValue(cur)}").ToList() ?? new();
                Console.WriteLine($"   [{name} · {(book ? "پول" : "تیل")}] " + string.Join(" · ", heads.Take(14)));
                win.CaptureRenderedFrame()?.Dispose();
                using var shot = win.CaptureRenderedFrame()!;
                Directory.CreateDirectory(ShotDir);
                var path = System.IO.Path.Combine(ShotDir, $"parity-{name}-{(book ? "money" : "fuel")}.png");
                shot.Save(path);
                ShotList.Add($"{(name == "after" ? "✔" : "·")} {System.IO.Path.GetFileName(path)} — debtor #1 ({(book ? "money" : "fuel")} book) {name} the parity pass");
            }
            debt.PersonOpen = false; Settle(win);
        }
        Snap("before");
        var afterOpen = h.Parity.CheckAsync(fix: false).GetAwaiter().GetResult().Count;
        Console.WriteLine($"   پس از باز شدنِ صفحهٔ حساب (پیش از سنجه): {afterOpen} ناجور");

        //  راهِ نیمه‌شبِ خودِ برنامه
        StampParityDay(file, set: false);
        vm.DayChanged();
        Check("نیمه‌شب سنجه را دوباره راه انداخت", h.Parity.DailyTask is not null);
        if (h.Parity.DailyTask is { } t1) Wait(win, t1);
        var fixedN = h.Parity.DailyTask?.Result ?? -1;
        Console.WriteLine($"   سنجه درست کرد: {fixedN} · خطا: «{h.Parity.LastError}»");
        Check("سنجه بی خطا دوید و درست کرد", fixedN > 0 && h.Parity.LastError == "", fixedN.ToString());
        var left = h.Parity.CheckAsync(fix: false).GetAwaiter().GetResult();
        var leftDates = h.Parity.CheckDatesAsync(fix: false).GetAwaiter().GetResult();
        Check("پس از یک پاس: صفر عددِ مشتقِ ناجور و صفر کلیدِ تاریخِ ناجور", left.Count == 0 && leftDates == 0, $"{left.Count} · {leftDates}");
        var users2 = UserNums();
        var changed = users.Zip(users2).Count(z => z.First != z.Second);
        Check("هیچ عددِ کاربر (لیتر، فی، رسید، رسیدِ تیل، تاریخ) عوض نشد", users.Count == users2.Count && changed == 0, $"{changed} ردیف");
        //  پاسِ دوم هیچ کاری نمی‌کند
        Check("پاسِ دوم چیزی پیدا نمی‌کند", h.Parity.CheckAsync(fix: true).GetAwaiter().GetResult().Count == 0
                                             && h.Parity.CheckDatesAsync(fix: true).GetAwaiter().GetResult() == 0);
        Snap("after");
    }

    private static void Run(string id, Action body)
    {
        Console.WriteLine();
        Console.WriteLine($"════ {id} ════");
        var before = _bad;
        Shots.Clear(); CurGroup = ""; CurTag = id;
        try { body(); }
        catch (Exception ex) { Check($"{id}: بی استثنا", false, ex.GetType().Name + ": " + ex.Message); }
        try { Compose(id); } catch (Exception ex) { Console.WriteLine("   ⚠️ عکس ساخته نشد: " + ex.Message); }
        Summary.Add((_bad == before ? "✔ " : "✖ ") + id + (_bad == before ? "" : $" — {_bad - before} ایراد"));
    }

    /// <summary>سالی با چند ردیف و نوشتهٔ بلند، پیش از سال‌های پرِ دفتر.</summary>
    private static void SeedFewYear(AppHost h)
    {
        var y = int.Parse(Shamsi.ThisMonth()[..4]) - 3;
        var d = $"{y}/03/12";
        const string longText = "هارون بابت نان و غیره — مصرفِ ماهِ پیش با توضیحِ بسیار بلندتر از همیشه 12";
        foreach (var t in new[] { longText, "کریم ", "500 افغانی " })
        {
            h.ExpenseLedger.AddAsync(new Expense { DateShamsi = d, Title = t, Amount = 123456789m }).GetAwaiter().GetResult();
            h.SafeLedger.AddAsync(new SafeEntry { DateShamsi = d, Title = t, Amount = 987654321m }).GetAwaiter().GetResult();
            h.ExchangeLedger.AddAsync(new ExchangeRow { DateShamsi = d, Description = t, Amount = 5555555m, Rate = 1m }).GetAwaiter().GetResult();
            h.RetailLedger.AddAsync(new RetailRow { DateShamsi = d, Name = t, Liters = 22222m, PricePerLiter = 60m }).GetAwaiter().GetResult();
        }
    }


    /// <summary>
    /// دو حالتِ همین بخش که بیشترین فرقِ شمارِ ردیف را دارند، کنارِ هم و در همان جای
    /// اسکرول (بالای صفحه). خط‌های سرخ = لبهٔ سرستون‌ها، کشوها، دکمه‌ها و نخستین کارتِ
    /// حالتِ A — روی <b>هر دو</b> قاب؛ سبز = همان لبه‌ها در B. خطِ سبزی که روی سرخ
    /// افتاده یعنی چیزی جابه‌جا نشده؛ سبزِ تنها کنارِ سرخِ تنها یعنی پرش.
    /// </summary>
    private static void Compose(string id)
    {
        if (ShotDir.Length == 0 || Shots.Count < 2) return;
        //  جفتِ مدرک: بیشترین فرقِ شمارِ ردیف در یک گروه؛ اگر شمار فرقی نکرد (بخشِ کارتی)،
        //  آغاز در برابرِ کهنه‌ترین ماهی که دیده شد
        Shot? a = null, b = null; var best = -1.0;
        for (var i = 0; i < Shots.Count; i++)
            for (var j = i + 1; j < Shots.Count; j++)
            {
                if (Shots[i].Group != Shots[j].Group) continue;      //  دفترِ تیل با دفترِ پول ستون‌های دیگری دارد
                var d = Math.Abs(Shots[i].Rows - Shots[j].Rows);
                if (d > best) { best = d; a = Shots[i]; b = Shots[j]; }
            }
        if (best <= 0)
        {
            static int Age(string t)
            {
                var m = System.Text.RegularExpressions.Regex.Match(t, @"(\d{4})/(\d{2})");
                return m.Success ? int.Parse(m.Groups[1].Value) * 100 + int.Parse(m.Groups[2].Value) : 999999;
            }
            var grp = Shots.GroupBy(x => x.Group).Where(x => x.Count() >= 2)
                           .OrderByDescending(x => x.Key is "cards" or "form" ? 0 : 1).ThenByDescending(x => x.Count()).FirstOrDefault();
            if (grp is null) return;
            var list = grp.ToList();
            a = list[0];
            var a0 = a;
            b = list.Skip(1).Where(x => x.Tag != a0.Tag)
                    .OrderBy(x => Age(x.Tag)).ThenByDescending(x => x.Tag.Length).FirstOrDefault()
                ?? list[^1];
        }
        if (a is null || b is null) return;
        using var ba = SkiaSharp.SKBitmap.Decode(a.Png);
        using var bb = SkiaSharp.SKBitmap.Decode(b.Png);
        const int top = 70, gap = 24;
        var W = ba.Width + gap + bb.Width;
        var H = top + Math.Max(ba.Height, bb.Height);
        using var surf = SkiaSharp.SKSurface.Create(new SkiaSharp.SKImageInfo(W, H));
        var cv = surf.Canvas;
        cv.Clear(SkiaSharp.SKColors.White);
        cv.DrawBitmap(ba, 0, top);
        cv.DrawBitmap(bb, ba.Width + gap, top);
        using var red = new SkiaSharp.SKPaint { Color = new SkiaSharp.SKColor(230, 30, 30, 170), StrokeWidth = 1, IsAntialias = false };
        using var green = new SkiaSharp.SKPaint { Color = new SkiaSharp.SKColor(0, 170, 60, 220), StrokeWidth = 1, IsAntialias = false,
                                                 PathEffect = SkiaSharp.SKPathEffect.CreateDash(new float[] { 6, 4 }, 0) };
        foreach (var x in a.Edges.Values.SelectMany(e => new[] { e.L, e.R }).Distinct())
        {
            var px = (float)Math.Round(x) + 0.5f;
            cv.DrawLine(px, top, px, H, red);
            cv.DrawLine(px + ba.Width + gap, top, px + ba.Width + gap, H, red);
        }
        foreach (var x in b.Edges.Values.SelectMany(e => new[] { e.L, e.R }).Distinct())
        {
            var px = (float)Math.Round(x) + 0.5f + ba.Width + gap;
            cv.DrawLine(px, top, px, H, green);
        }
        //  فقط نشان‌هایی که در هر دو هستند (کارتی که فقط در B هست «پرش» نیست)
        var moved = b.Edges.Count(kv => a.Edges.TryGetValue(kv.Key, out var e)
                                        && Math.Max(Math.Abs(e.L - kv.Value.L), Math.Abs(e.R - kv.Value.R)) > 1);
        using var font = new SkiaSharp.SKFont(SkiaSharp.SKTypeface.Default, 22);
        using var ink = new SkiaSharp.SKPaint { Color = SkiaSharp.SKColors.Black, IsAntialias = true };
        cv.DrawText($"{id}   A: {a.Tag}  (rows {a.Rows})", 10, 28, font, ink);
        cv.DrawText($"B: {b.Tag}  (rows {b.Rows})", ba.Width + gap + 10, 28, font, ink);
        var verdict = moved == 0 && a.Ok && b.Ok ? "OK: every red edge of A has a green edge on it in B (no horizontal shift)"
                                                 : $"SHIFT: {moved} edge(s) of B are not on an edge of A";
        ink.Color = moved == 0 && a.Ok && b.Ok ? new SkiaSharp.SKColor(0, 130, 40) : new SkiaSharp.SKColor(200, 0, 0);
        cv.DrawText(verdict + "   red = A edges, dashed green = B edges", 10, 58, font, ink);
        Directory.CreateDirectory(ShotDir);
        var path = System.IO.Path.Combine(ShotDir, $"{id}.png");
        using var img = surf.Snapshot();
        using var data = img.Encode(SkiaSharp.SKEncodedImageFormat.Png, 90);
        using (var fs = File.Create(path)) data.SaveTo(fs);
        var line = $"{(moved == 0 && a.Ok && b.Ok ? "✔" : "✖")} {System.IO.Path.GetFileName(path)} — {a.Tag} ({a.Rows} rows) vs {b.Tag} ({b.Rows} rows), {a.Edges.Count} edges, {moved} moved";
        ShotList.Add(line);
        Console.WriteLine("   🖼 " + line);
    }

    // ══ بخش‌ها ═════════════════════════════════════════════════════════════

    /// <summary>‎MS_FONT=n‎: هر بخش ‎n‎ بار «‎A+‎» (یا با منفی «‎A−‎») — همان دکمهٔ سربرگِ بخش.</summary>
    private static readonly int FontSteps = int.TryParse(Environment.GetEnvironmentVariable("MS_FONT"), out var fs0) ? fs0 : 0;

    private static void ApplyFont(MainWindow win, SectionViewModel s)
    {
        if (FontSteps == 0) return;
        var want = Math.Round(1 + FontSteps * SectionViewModel.FontStep, 2);
        if (Math.Abs(s.FontScale - want) < 0.005) return;
        s.FontResetCommand.Execute(null);
        for (var i = 0; i < Math.Abs(FontSteps); i++)
            (FontSteps > 0 ? s.FontBiggerCommand : s.FontSmallerCommand).Execute(null);
        Settle(win);
    }

    private static SectionViewModel Open(MainWindow win, MainViewModel vm, string id)
    {
        if (vm.Sections.FirstOrDefault(x => x.Id == id) is { } top)
        {
            Wait(win, vm.GoAsync(top));
            ApplyFont(win, top);
            return top;
        }
        var parent = vm.Sections.First(x => x.SubSections.Any(s => s.Id == id));
        Wait(win, vm.GoAsync(parent));
        var sub = parent.SubSections.First(s => s.Id == id);
        parent.ShowSubCommand.Execute(sub);
        Settle(win);
        ApplyFont(win, sub);
        return sub;
    }

    /// <summary>تاریخچهٔ هر بخش کشوی سال و ماهِ خودش را دارد — هر کدام جدا.</summary>
    private static void History(MainWindow win, MainViewModel vm)
    {
        var h = (HistorySectionViewModel)Open(win, vm, "history");
        WaitRows(win);
        Settle(win);
        var onlyKind = Environment.GetEnvironmentVariable("MS_HIST");
        foreach (var kind in h.Cards.Where(c => c.Entity.Count > 0).Select(c => c.Entity.Key)
                              .Where(k => string.IsNullOrEmpty(onlyKind) || onlyKind.Split(',').Contains(k)).ToList())
        {
            Wait(win, h.OpenAsync(kind));
            WaitRows(win);
            CurGroup = "history-" + kind;
            PickerSection(win, vm, "history", reopen: false, title: "تاریخچهٔ " + kind);
            try { Compose("history-" + kind); } catch (Exception ex) { Console.WriteLine("   ⚠️ " + ex.Message); }
            Shots.Clear();
            h.BackCommand.Execute(null);
            Settle(win);
        }
    }

    private static void PickerSection(MainWindow win, MainViewModel vm, string id, bool reopen = true, string? title = null)
    {
        var s = reopen ? Open(win, vm, id) : vm.Sections.First(x => x.Id == id);
        if (s is WaraqSectionViewModel wq) Wait(win, wq.ReloadAsync());
        Settle(win);
        Base = null;
        var name = title ?? s.Title;
        CurTag = "open " + Shamsi.ThisMonth();
        Measure(win, $"{name} (باز شدن)");
        Cycles(win, name);

        var (yearBox, monthBox) = Boxes(win);
        if (monthBox is null) { Check($"{name}: کشوی ماه پیدا شد", false); return; }
        if (Dbg) Console.WriteLine("   سال‌ها: " + string.Join(",", yearBox?.Items.OfType<YearMonthItem>().Select(i => i.Key) ?? Array.Empty<string>())
                                   + " · ماه‌ها: " + string.Join(",", monthBox.Items.OfType<YearMonthItem>().Select(i => i.Key)));
        if (Dbg)
            foreach (var cb in new[] { yearBox, monthBox })
            {
                if (cb is null) continue;
                var tb = cb.GetVisualDescendants().OfType<TextBlock>().FirstOrDefault(t => t.IsEffectivelyVisible && !string.IsNullOrEmpty(t.Text));
                if (tb is null) continue;
                var chrome = cb.Bounds.Width - tb.Bounds.Width;
                var max = cb.Items.OfType<YearMonthItem>().Max(i =>
                {
                    var m = new TextBlock { Text = i.Label, FontFamily = tb.FontFamily, FontSize = tb.FontSize, FontWeight = tb.FontWeight };
                    m.Measure(Size.Infinity); return m.DesiredSize.Width;
                });
                Console.WriteLine($"   کشو: پهنا {cb.Bounds.Width:0} · کمینه {cb.MinWidth:0} · قلم {cb.FontSize}/{tb.FontSize} · قاب {chrome:0} · بلندترین نوشته {max:0} ⇒ لازم {chrome + max + 15:0}");
            }
        var years = yearBox?.Items.OfType<YearMonthItem>().Select(i => i.Key).Where(k => k.Length == 4).ToList()
                    ?? new List<string>();
        var startYear = (yearBox?.SelectedItem as YearMonthItem)?.Key ?? "";
        var startMonth = (monthBox.SelectedItem as YearMonthItem)?.Key ?? "";
        var plan = new List<(string Year, string Month, string Label)>();
        //  خواستهٔ صاحب ریپو: «چهار پنج ماهِ جدید و قدیم» — از ماهِ جاری تا ۱۴۰۴/۱۱، از مرزِ سال
        foreach (var back in new[] { 3, 6, 7, 8 })
        {
            var mk = MonthBack(back);
            plan.Add((mk[..4], mk, "ماهِ " + mk));
        }
        //  همهٔ سال‌ها (اگر کشو دارد)
        if (yearBox?.Items.OfType<YearMonthItem>().Any(i => i.Key == "") == true)
            plan.Add((AllYears, "", "همهٔ سال‌ها"));
        //  کهنه‌ترین ماهِ همین سال
        var thisMonths = MonthKeys(monthBox);
        if (thisMonths.Count > 1) plan.Add((startYear, thisMonths.OrderBy(k => k).First(), "کهنه‌ترین ماهِ همین سال"));
        if (thisMonths.Any(k => k.EndsWith(YearMonthPicker.AllMark))) plan.Add((startYear, thisMonths.First(k => k.EndsWith(YearMonthPicker.AllMark)), "همهٔ ماه‌های امسال"));
        foreach (var y in years.Where(y => y != startYear).OrderByDescending(y => y))
            plan.Add((y, "", "سالِ " + y));
        plan.Add((startYear, startMonth, "برگشت به ماهِ آغاز"));

        foreach (var (y, m, label) in plan)
        {
            CurTag = (y == AllYears ? "all-years" : y) + (m.Length > 0 ? " " + m.Replace(YearMonthPicker.AllMark, "/all") : "");
            Pick(win, y, m);
            for (var f = 0; f < 4; f++) Frame(win);
            Measure(win, $"{name} ⇒ {label} (همان لحظه)", pixels: false);
            Settle(win);
            Measure(win, $"{name} ⇒ {label} (پس از ته‌نشینی)");
            //  هر ماهی که در سالِ دیگر هست هم: کهنه‌ترین و «همه»
            if (m == "" && y != startYear)
            {
                var (_, mb) = Boxes(win);
                if (mb is null) continue;
                var ks = MonthKeys(mb);
                foreach (var k in new[] { ks.Where(x => !x.EndsWith(YearMonthPicker.AllMark)).OrderBy(x => x).FirstOrDefault(),
                                          ks.FirstOrDefault(x => x.EndsWith(YearMonthPicker.AllMark)) })
                {
                    if (k is null) continue;
                    CurTag = y + " " + k.Replace(YearMonthPicker.AllMark, "/all");
                    Pick(win, y, k);
                    for (var f = 0; f < 4; f++) Frame(win);
                    Measure(win, $"{name} ⇒ {y} · {k} (همان لحظه)", pixels: false);
                    Settle(win);
                    Measure(win, $"{name} ⇒ {y} · {k} (پس از ته‌نشینی)");
                }
            }
        }
    }

    private const string AllYears = "*years";

    /// <summary>
    /// گاوصندوقِ ماهِ جاری با ردیف‌های «📝 فروش ورق»ِ خودکار ⇒ پاک کردنِ همان‌ها یکی‌یکی (همان
    /// فرمانِ حذفِ ردیف) — پس از هر حذف همهٔ لبه‌ها با «باز شدن» سنجیده می‌شوند.
    /// </summary>
    private static void SafeDelete(MainWindow win, MainViewModel vm)
    {
        var s = (SafeSectionViewModel)Open(win, vm, "safe");
        WaitRows(win); Settle(win);
        static bool Auto(SafeRowViewModel r) => (r.Entity.SrcKey ?? "").StartsWith("wq-sales", StringComparison.Ordinal);
        foreach (var back in new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8 })
        {
            if (s.Rows.Any(Auto)) break;
            var mk = MonthBack(back);
            Pick(win, mk[..4], mk); Settle(win);
        }
        Base = null;
        CurTag = "safe with waraq rows";
        Measure(win, "گاوصندوق با ردیف‌های فروشِ ورق");
        var auto = s.Rows.Where(Auto).ToList();
        Console.WriteLine($"   {auto.Count} ردیفِ فروشِ ورق از {s.Rows.Count}");
        var n = 0;
        foreach (var r in auto)
        {
            s.DeleteRowCommand.Execute(r);
            WaitRows(win);
            if (++n % Math.Max(1, auto.Count / 4) != 0 && n != auto.Count) continue;
            for (var f = 0; f < 4; f++) Frame(win);
            CurTag = $"safe after deleting {n}/{auto.Count}";
            Measure(win, $"گاوصندوق ⇒ {n} ردیفِ فروشِ ورق پاک شد (همان لحظه)", pixels: false);
            Settle(win);
            Measure(win, $"گاوصندوق ⇒ {n} ردیفِ فروشِ ورق پاک شد (پس از ته‌نشینی)");
        }
    }

    /// <summary>
    /// ══ عکسِ ۱۴۰۵/۰۷/۲۲: «سنبله» با یازده ردیفِ خالی در مصارف و سه در گاوصندوق ══
    /// ماهِ پیش ⇒ «➕ چندتایی» ⇒ رفت‌وبرگشت میانِ ماه‌ها و سال‌ها؛ پس از هر حالت جوهرِ هر
    /// خانه (تاریخ، «0»، …) با پیکسلِ واقعی وسطِ کادرش.
    /// </summary>
    private static void BlankRows(MainWindow win, MainViewModel vm, string id)
    {
        var s = Open(win, vm, id);
        var add = s.GetType().GetMethod("AddRowsAsync")!;
        WaitRows(win); Settle(win);
        var prev = MonthBack(1);
        Pick(win, prev[..4], prev); Settle(win);
        Wait(win, (Task)add.Invoke(s, new object[] { id == "safe" ? 3 : 11 })!);
        WaitRows(win); Settle(win);
        //  دفترِ تهی: ماهِ پیش در کشو نیست — ردیف‌ها در ماهِ جاری ساخته و به آخرین روزِ ماهِ پیش برده می‌شوند
        if (Environment.GetEnvironmentVariable("MS_EMPTY") == "1")
        {
            var last = Shamsi.DateInMonth(prev);
            for (var d = 31; d >= 29; d--) { var c = $"{prev}/{d:00}"; if (Shamsi.Key(c) > 0) { last = c; break; } }
            var rows = ((System.Collections.IEnumerable)s.GetType().GetProperty("Rows")!.GetValue(s)!).Cast<RowViewModel>().ToList();
            foreach (var r in rows)
            {
                r.GetType().GetProperty("DateShamsi")!.SetValue(r, last);
                Wait(win, r.FlushAsync());
            }
            Pick(win, Shamsi.ThisMonth()[..4], Shamsi.ThisMonth()); Settle(win);
            Open(win, vm, "dashboard"); Open(win, vm, id); WaitRows(win); Settle(win);
            Pick(win, prev[..4], prev); Settle(win);
            Console.WriteLine($"   {rows.Count} ردیفِ خالی با تاریخِ {last}");
        }
        Base = null;
        CurTag = "blank " + prev;
        Measure(win, $"{s.Title} ⇒ {prev} با ردیف‌های خالیِ تازه");
        var (yb, mb) = Boxes(win);
        var years = yb?.Items.OfType<YearMonthItem>().Select(i => i.Key).Where(k => k.Length == 4).ToList() ?? new();
        var plan = new List<(string Y, string M)> { (Shamsi.ThisMonth()[..4], Shamsi.ThisMonth()), (prev[..4], prev) };
        foreach (var y in years.OrderBy(y => y)) plan.Add((y, ""));
        foreach (var back in new[] { 2, 3, 6 }) { var mk = MonthBack(back); plan.Add((mk[..4], mk)); }
        plan.Add((prev[..4], prev));
        plan.Add((Shamsi.ThisMonth()[..4], Shamsi.ThisMonth()));
        plan.Add((prev[..4], prev));
        foreach (var (y, m) in plan)
        {
            CurTag = "blank ⇒ " + y + " " + m;
            Pick(win, y, m);
            for (var f = 0; f < 4; f++) Frame(win);
            Measure(win, $"{s.Title} ⇒ {y} {m} (همان لحظه)");
            Settle(win);
            Measure(win, $"{s.Title} ⇒ {y} {m} (پس از ته‌نشینی)");
        }
    }

    private static MainViewModel? Vm;
    private static readonly bool ThemeCycle = Environment.GetEnvironmentVariable("MS_THEMEFLIP") != "0";
    private static readonly bool EditCycle = Environment.GetEnvironmentVariable("MS_EDIT") != "0";

    /// <summary>
    /// ══ همان صفحه، بی عوض شدنِ ماه: تمِ دیگر و برگشت · تایپ در یک خانه و ثبت ══
    /// هر دو با همان ‎Base‎ِ «باز شدن» سنجیده می‌شوند — هیچ لبه‌ای نپرد.
    /// </summary>
    private static void Cycles(MainWindow win, string name)
    {
        if (ThemeCycle && Vm is { } vm)
        {
            var first = vm.SelectedTheme;
            foreach (var t in PumpYaqobi.App.Themes.PumpTheme.All.Where(t => !ReferenceEquals(t, first)).Append(first).ToList())
            {
                var tag = CurTag;
                CurTag = tag + " theme " + t.Id;
                vm.SelectedTheme = t;
                for (var f = 0; f < 4; f++) Frame(win);
                Measure(win, $"{name} ⇒ تمِ {t.Id} (همان لحظه)", pixels: false);
                Settle(win);
                Measure(win, $"{name} ⇒ تمِ {t.Id} (پس از ته‌نشینی)");
                CurTag = tag;
            }
        }
        if (EditCycle) EditOnce(win, name);
    }

    /// <summary>
    /// تایپ در نخستین خانهٔ متنیِ نوشتنیِ نخستین جدولِ دیدنی، ثبت، و برگرداندنِ
    /// همان متن — مثلِ کاربر: دوبار-کلیکِ خانه، تایپ، ‎Enter‎.
    /// </summary>
    private static void EditOnce(MainWindow win, string name)
    {
        var g = Roots(win).SelectMany(r => r.GetVisualDescendants().OfType<ExcelGrid>())
                          .FirstOrDefault(x => x.IsEffectivelyVisible && x.Bounds.Width > 50 && !x.IsReadOnly && InView(x, win));
        if (g is null || g.ItemsSource is not System.Collections.IEnumerable src) return;
        var item = src.Cast<object>().FirstOrDefault();
        if (item is null) return;
        var col = g.Columns.OfType<DataGridTextColumn>().Where(c => c.IsVisible && !c.IsReadOnly)
                   .OrderBy(c => c.DisplayIndex)
                   .FirstOrDefault(c => c.Binding is Avalonia.Data.Binding b && !string.IsNullOrEmpty(b.Path)
                                         && item.GetType().GetProperty(b.Path) is { CanWrite: true, PropertyType: var pt } && pt == typeof(string)
                                         && !b.Path.Contains("Date"));
        if (col is null) return;
        var path = ((Avalonia.Data.Binding)col.Binding!).Path;
        var prop = item.GetType().GetProperty(path)!;
        var orig = prop.GetValue(item) as string ?? "";
        foreach (var (text, label) in new[] { (orig + " 1234567 تایپِ آزمایشی", "تایپ و ثبت"), (orig, "برگرداندنِ متن") })
        {
            g.ScrollIntoView(item, col);
            g.SelectedItem = item;
            g.CurrentColumn = col;
            Settle(win);
            g.BeginEdit();
            Settle(win);
            var box = g.GetVisualDescendants().OfType<TextBox>().FirstOrDefault(t => t.IsEffectivelyVisible && t.FindAncestorOfType<DataGridCell>() is not null);
            if (box is null) { Console.WriteLine($"   ◦ {name}: خانهٔ «{col.Header}» باز نشد"); g.CancelEdit(); return; }
            box.Text = text;
            for (var f = 0; f < 3; f++) Frame(win);
            var tag = CurTag; CurTag = tag + " edit";
            Measure(win, $"{name} ⇒ وسطِ تایپ در «{col.Header}»", pixels: false);
            g.CommitEdit();
            WaitRows(win); Settle(win);
            Measure(win, $"{name} ⇒ {label} در «{col.Header}»");
            CurTag = tag;
        }
    }

    private static string Ascii(string t) =>
        new string(t.Select(ch => ch < 128 ? ch : char.IsDigit(ch) ? (char)('0' + (int)char.GetNumericValue(ch)) : ' ').ToArray()).Trim();

    private static string MonthBack(int back)
    {
        var y = int.Parse(Shamsi.ThisMonth()[..4]);
        var m = int.Parse(Shamsi.ThisMonth()[5..7]) - back;
        while (m <= 0) { m += 12; y--; }
        return $"{y}/{m:00}";
    }

    private static List<string> MonthKeys(ComboBox box) =>
        box.Items.OfType<YearMonthItem>().Select(i => i.Key).Where(k => !string.IsNullOrEmpty(k)).ToList();

    private static (ComboBox? Year, ComboBox? Month) Boxes(MainWindow win)
    {
        var combos = win.GetVisualDescendants().OfType<ComboBox>()
            .Where(c => c.IsEffectivelyVisible && c.Items.OfType<YearMonthItem>().Any()).ToList();
        var yearBox = combos.FirstOrDefault(c => c.Items.OfType<YearMonthItem>().All(i => i.Key.Length is 0 or 4)
                                                 && c.Items.OfType<YearMonthItem>().Any(i => i.Key.Length == 4));
        var monthBox = combos.FirstOrDefault(c => !ReferenceEquals(c, yearBox));
        return (yearBox, monthBox);
    }

    /// <summary>با خودِ کشوهای سال و ماهِ صفحه، مثلِ کلیکِ کاربر. ماهِ خالی یعنی فقط سال.</summary>
    private static void Pick(MainWindow win, string year, string month)
    {
        var (yearBox, monthBox) = Boxes(win);
        var yk = year == AllYears ? "" : year;
        if (yearBox is not null && (yk.Length == 4 || year == AllYears) && (yearBox.SelectedItem as YearMonthItem)?.Key != yk
            && yearBox.Items.OfType<YearMonthItem>().FirstOrDefault(i => i.Key == yk) is { } yi)
        {
            yearBox.SelectedItem = yi;
            WaitRows(win);
            (_, monthBox) = Boxes(win);
        }
        if (month.Length == 0 || monthBox is null) { WaitRows(win); return; }
        if (monthBox.Items.OfType<YearMonthItem>().FirstOrDefault(i => i.Key == month) is { } mi)
        {
            if (!Equals(monthBox.SelectedItem, mi)) monthBox.SelectedItem = mi;
        }
        else Console.WriteLine($"   ◦ ماهِ {month} در این کشو نیست — این بخش در آن ماه ردیفی ندارد");
        WaitRows(win);
    }

    private static void Dashboard(MainWindow win, MainViewModel vm)
    {
        var s = (DashboardSectionViewModel)Open(win, vm, "dashboard");
        Settle(win);
        Base = null;
        CurTag = "range day";
        Measure(win, "داشبورد (امروز)");
        Cycles(win, "داشبورد");
        foreach (var (r, label) in new[] { ("week", "هفته"), ("month", "ماه"), ("year", "سال"), ("day", "برگشت به امروز") })
        {
            CurTag = "range " + r;
            s.SetRangeCommand.Execute(r);
            for (var f = 0; f < 4; f++) Frame(win);
            Measure(win, $"داشبورد ⇒ {label} (همان لحظه)", pixels: false);
            WaitRows(win);
            Settle(win);
            Measure(win, $"داشبورد ⇒ {label} (پس از ته‌نشینی)");
        }
    }

    private static void ParchaReports(MainWindow win, MainViewModel vm)
    {
        var s = (ParchaSectionViewModel)Open(win, vm, "shifts");
        Settle(win);
        Base = null;
        CurGroup = "form"; CurTag = "parcha form";
        Measure(win, "پارچه‌ها (فرم)");
        Cycles(win, "پارچه‌ها");
        Revisit(win, vm, "shifts", openAgain: false);
        Compose("shifts-form"); Shots.Clear();
        s.OpenReportsCommand.Execute(null);
        WaitRows(win);
        Settle(win);
        Base = null;
        CurGroup = "reports"; CurTag = "reports open";
        Measure(win, "گزارش‌های پارچه (باز شدن)");
        var years = s.ReportYears.ToList();
        foreach (var y in years.Skip(1))
        {
            y.IsOpen = true;
            CurTag = "reports open " + Ascii(y.Title);
            Settle(win);
            Measure(win, $"گزارش‌های پارچه ⇒ باز کردنِ {y.Title}");
            foreach (var m in y.Months.Take(2))
            {
                m.IsOpen = !m.IsOpen;
                CurTag = "reports " + Ascii(y.Title) + " month " + Ascii(m.Title);
                Settle(win);
                Measure(win, $"گزارش‌های پارچه ⇒ {y.Title} · {m.Title}");
            }
            y.IsOpen = false;
            CurTag = "reports closed " + Ascii(y.Title);
            Settle(win);
            Measure(win, $"گزارش‌های پارچه ⇒ بستنِ {y.Title}");
        }
        s.ReportsOpen = false;
        Settle(win);
    }

    private static void Person(MainWindow win, MainViewModel vm)
    {
        var debt = (DebtSectionViewModel)Open(win, vm, "debt");
        Settle(win);
        Base = null;
        CurGroup = "cards"; CurTag = "debtor cards";
        Measure(win, "قرض‌داران (کارت‌ها)");
        Revisit(win, vm, "debt", openAgain: false);
        Compose("debt-cards"); Shots.Clear();
        foreach (var n in new[] { 1, 15, 2 })
        {
            debt.PersonOpen = false; Settle(win);
            Wait(win, debt.OpenByNumberAsync(n));
            WaitRows(win);
            Settle(win);
            Base = null;
            var person = debt.ActivePage;
            var cur = person?.GetType().GetProperty("Current")?.GetValue(person);
            string Book(object? c) => c?.GetType().GetProperty("IsMoney")?.GetValue(c) is true ? "money" : "fuel";
            CurGroup = Book(cur); CurTag = $"account #{n} {CurGroup}";
            Measure(win, $"حسابِ شمارهٔ {n} (باز شدن)");
            Cycles(win, $"حسابِ {n}");
            var accounts = (person?.GetType().GetProperty("Accounts")?.GetValue(person) as System.Collections.IEnumerable)?.Cast<object>().ToList();
            if (cur?.GetType().GetProperty("ToggleModeCommand")?.GetValue(cur) is System.Windows.Input.ICommand t)
            {
                t.Execute(null); WaitRows(win); Settle(win);
                var keep = (Base, BaseCounts); Base = null;
                var g0 = CurGroup; CurGroup = Book(cur); CurTag = $"account #{n} {CurGroup}";
                Measure(win, $"حسابِ {n} ⇒ دفترِ دیگر");
                t.Execute(null); WaitRows(win); Settle(win);
                (Base, BaseCounts) = keep; CurGroup = g0; CurTag = $"account #{n} {CurGroup} again";
                Measure(win, $"حسابِ {n} ⇒ برگشت به دفترِ اول");
            }
            if (accounts is { Count: > 1 })
            {
                var cp = person!.GetType().GetProperty("Current")!;
                cp.SetValue(person, accounts[1]); WaitRows(win); Settle(win);
                var keep = (Base, BaseCounts); Base = null;
                var g1 = CurGroup; CurGroup = "sub-" + Book(accounts[1]); CurTag = $"account #{n} sub {Book(accounts[1])}";
                Measure(win, $"حسابِ {n} ⇒ حسابِ فرعی");
                cp.SetValue(person, accounts[0]); WaitRows(win); Settle(win);
                (Base, BaseCounts) = keep; CurGroup = g1; CurTag = $"account #{n} {g1} back";
                Measure(win, $"حسابِ {n} ⇒ برگشت به حسابِ اصلی");
            }
        }
        debt.PersonOpen = false; Settle(win);
    }

    private static void Company(MainWindow win, MainViewModel vm)
    {
        var co = (CompanySectionViewModel)Open(win, vm, "noinv");
        Settle(win);
        Base = null;
        CurGroup = "cards"; CurTag = "company cards";
        Measure(win, "شرکت‌ها (کارت‌ها)");
        var ci = 0;
        Shots.Clear();
        foreach (var card in co.Cards.Take(2).ToList())
        {
            ci++;
            co.OpenCommand.Execute(card);
            WaitRows(win); Settle(win);
            Base = null;
            var page = co.ActivePage;
            var p = page?.GetType().GetProperty("IsDiesel");
            CurGroup = $"company{ci}-" + (p?.GetValue(page) is true ? "diesel" : "petrol"); CurTag = $"company {ci} {CurGroup}";
            Measure(win, $"شرکتِ «{card.GetType().GetProperty("Name")?.GetValue(card)}» (باز شدن)");
            Cycles(win, $"شرکتِ {ci}");
            if (p is not null && p.CanWrite)
            {
                var was = (bool)p.GetValue(page)!;
                p.SetValue(page, !was); WaitRows(win); Settle(win);
                var keep = (Base, BaseCounts); Base = null;
                var g2 = CurGroup; CurGroup = $"company{ci}-" + (was ? "petrol" : "diesel"); CurTag = $"company {ci} {CurGroup}";
                Measure(win, "شرکت ⇒ تیلِ دیگر");
                p.SetValue(page, was); WaitRows(win); Settle(win);
                (Base, BaseCounts) = keep; CurGroup = g2; CurTag = $"company {ci} {g2} back";
                Measure(win, "شرکت ⇒ برگشت");
            }
            co.BackCommand.Execute(null); Settle(win);
        }
    }

    /// <summary>بخشِ بی‌کشو: باز ⇒ رفتن به مصارف و عوض کردنِ ماه ⇒ برگشت — هیچ چیزی نپرد.</summary>
    private static void Revisit(MainWindow win, MainViewModel vm, string id, bool openAgain = true)
    {
        var s = Open(win, vm, id);
        WaitRows(win); Settle(win);
        CurTag = "open " + Shamsi.ThisMonth();
        if (openAgain) { Base = null; Measure(win, $"{s.Title} (باز شدن)"); Cycles(win, s.Title); }
        var keep = (Base, BaseCounts);
        Open(win, vm, "expenses");
        Settle(win);
        var (yb, mb) = Boxes(win);
        var oldY = yb?.Items.OfType<YearMonthItem>().Select(i => i.Key).Where(k => k.Length == 4).OrderBy(k => k).FirstOrDefault() ?? "";
        Pick(win, oldY, "");
        Settle(win);
        Open(win, vm, id);
        for (var f = 0; f < 4; f++) Frame(win);
        (Base, BaseCounts) = keep;
        CurTag = "after expenses -> year " + oldY;
        Measure(win, $"{s.Title} ⇒ پس از عوض شدنِ سال در مصارف (همان لحظه)", pixels: false);
        WaitRows(win); Settle(win);
        Measure(win, $"{s.Title} ⇒ پس از عوض شدنِ سال در مصارف (پس از ته‌نشینی)");
        //  مصارف به ماهِ جاری برگردد تا بخشِ بعدی از همان جای همیشگی شروع کند
        Open(win, vm, "expenses");
        Settle(win);
        (yb, mb) = Boxes(win);
        Pick(win, Shamsi.ThisMonth()[..4], Shamsi.ThisMonth());
        Settle(win);
    }

    // ══ سنجش ═══════════════════════════════════════════════════════════════

    private static readonly bool Dbg = Environment.GetEnvironmentVariable("MS_DEBUG") == "1";
    private static readonly double WinW = double.TryParse(Environment.GetEnvironmentVariable("MS_WIDTH"), out var w0) ? w0 : 1440;
    private static readonly double Scale = double.TryParse(Environment.GetEnvironmentVariable("MS_SCALE"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var s0) ? s0 : 1.0;

    /// <summary>
    /// پنجرهٔ بی‌سرِ آوالونیا ‎RenderScaling‎ را ثابتِ ۱ نگه می‌دارد (ویژگیِ فقط‌خواندنی).
    /// همان فیلدِ پشتیبانش نوشته می‌شود تا گردکردنِ چیدمان (‎UseLayoutRounding‎) مثلِ
    /// ویندوزِ ۱۲۵٪ و ۱۵۰٪ رفتار کند.
    /// </summary>
    private static void SetScale(Window win, double scale)
    {
        var impl = win.PlatformImpl!;
        var f = impl.GetType().GetField("<RenderScaling>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
        if (f is null) { Console.WriteLine("   ⚠️ فیلدِ RenderScaling پیدا نشد — مقیاس سنجیده نمی‌شود"); return; }
        f.SetValue(impl, scale);
        (impl.GetType().GetProperty("ScalingChanged")?.GetValue(impl) as Action<double>)?.Invoke(scale);
    }
    private static readonly double WinH = double.TryParse(Environment.GetEnvironmentVariable("MS_HEIGHT"), out var h0) ? h0 : 900;

    /// <summary>
    /// ══ «یک کادرِ جدول را پاک کردم، همهٔ سربرگ‌ها برگشتند وسط» (۱۴۰۵/۰۷/۲۱) ══
    /// هیچ چیزی در صفحهٔ بخش از جای خودش (پدر) پهن‌تر چیده نشود، صفحه از قابِ
    /// اسکرولِ پنجره بیرون نزند، و هیچ اسکرولِ افقیِ درونی (جدول) بیش از قابش نلغزد.
    /// </summary>
    private static void Overflow(Window win, List<Visual> roots, List<string> bad)
    {
        var page = win.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault(s => s.Name == "PageScroll");
        var vw = page?.Viewport.Width ?? win.Bounds.Width;
        foreach (var root in roots)
        {
            var re = Edges(root, page ?? (Visual)win);
            if (re.L < -1 || re.R > vw + 1) bad.Add($"صفحهٔ بخش {re.L:0}..{re.R:0} از قابِ {vw:0} بیرون زد");
            //  «سربرگ وسطِ قاب»: کادرِ بخش با دو حاشیهٔ برابر — چه ‎MaxWidth‎ داشته باشد چه نه
            if (root is SectionPage && Math.Abs(re.L - (vw - re.R)) > 1)
                bad.Add($"کادرِ بخش وسطِ قاب نیست: چپ {re.L:0.#} · راست {vw - re.R:0.#}");
            foreach (var c in root.GetVisualDescendants().OfType<Control>().Where(c => c.IsEffectivelyVisible && c.Bounds.Width > 1))
            {
                if (c.GetVisualParent() is not Control p) continue;
                if (p.Bounds.Width < 1) continue;   //  ظرفِ بازیافتیِ ‎ItemsRepeater‎ (صفر پهنا، کشیده نمی‌شود)
                if (p is Avalonia.Controls.Presenters.ScrollContentPresenter or Viewbox || c is Popup
                    || c.Classes.Contains("badge") || p.GetType().Name == "ViewboxContainer"
                    || c.FindAncestorOfType<ScrollBar>() is not null
                    //  ردیفِ جدول ستونِ پرکنندهٔ ته را هم دارد و ‎presenter‎ می‌بُردش؛ لبهٔ خانه‌ها جدا سنجیده می‌شود
                    || c is DataGridRow && p is Avalonia.Controls.Primitives.DataGridRowsPresenter) continue;
                //  جای چیده‌شده = لبه‌ها به‌علاوهٔ حاشیهٔ خودِ کنترل — فرزندی که با حاشیه‌اش از پدر
                //  بیرون زده (لبهٔ دیدنی‌اش سرِ جا ولی خودش جابه‌جا) هم گرفته شود
                //  ⚠️ خانهٔ کناریِ نوارِ «جمله» زیرِ سرستونش است و سرستون تا خطِ بیرونیِ جدول می‌رود،
                //  در حالی که نوار درونِ قابِ خودش (خطِ ‎SumBorder‎) است: همان یک خط روی هم می‌افتد
                var tol = 1.0;
                //  ⚠️ خانهٔ نوارِ «جمله» زیرِ سرستونش چیده می‌شود (لبه‌به‌لبه، سنجهٔ «جمله» در پایین) و
                //  سرستونِ ستونِ آخر درونِ خودِ جدول ۱ تا ۲ پیکسل زیرِ خطِ قاب می‌رود (چیدمانِ درونیِ
                //  ‎DataGrid‎ِ آوالونیا، بریده‌شده با قاب) — همان یک خط. شمرده و چاپ می‌شود، سرخ نمی‌کند.
                if (p is TotalsStrip && p.GetVisualAncestors().OfType<Border>().FirstOrDefault(b => b.BorderThickness.Left > 0) is { } fb)
                {
                    var fe = Edges(fb, p); var ce0 = Edges(c, p);
                    var over = Math.Max(fe.L - ce0.L, ce0.R - fe.R);
                    if (over > 0.5) { FrameOverlap++; FrameOverlapMax = Math.Max(FrameOverlapMax, over); }
                    continue;
                }
                if (c.Bounds.X - c.Margin.Left < -tol || c.Bounds.Right + c.Margin.Right > p.Bounds.Width + tol)
                {
                    bad.Add($"«{Label(c)}» در «{Label(p)}»: {c.Bounds.X - c.Margin.Left:0}..{c.Bounds.Right + c.Margin.Right:0} از {p.Bounds.Width:0}");
                    if (Dbg)
                        Console.WriteLine("     ‹زنجیره› " + string.Join(" ← ", new[] { c }.Concat(c.GetVisualAncestors().OfType<Control>().Take(7))
                            .Select(a => $"{Label(a)} b={a.Bounds.Width:0.#} d={a.DesiredSize.Width:0.#} mv={a.IsMeasureValid} av={a.IsArrangeValid}"
                                       + (a is TotalsStrip ts ? " slots=" + string.Join(",", ((System.Collections.IDictionary)typeof(TotalsStrip).GetField("_slots", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(ts)!).Values.Cast<double>().Select(v => v.ToString("0"))) : ""))));
                }
            }
            if (Dbg)
                foreach (var g in root.GetVisualDescendants().OfType<ExcelGrid>().Where(x => x.IsEffectivelyVisible))
                {
                    var rp = g.GetVisualDescendants().OfType<Avalonia.Controls.Primitives.DataGridRowsPresenter>().FirstOrDefault();
                    var rh = g.GetVisualDescendants().OfType<Avalonia.Controls.Primitives.DataGridRowHeader>().FirstOrDefault(x => x.IsEffectivelyVisible);
                    var hs = g.GetVisualDescendants().OfType<ScrollBar>().Where(b => b.Orientation == Avalonia.Layout.Orientation.Horizontal)
                              .Select(b => $"{b.IsVisible}/{b.Maximum:0}/{b.Value:0}");
                    Console.WriteLine($"     ‹جدول {g.Bounds.Width:0.#} · RowHeaderWidth {g.RowHeaderWidth:0.#} · سرِ ردیف {rh?.Bounds.Width:0.#} · ستون‌ها {g.Columns.Where(c => c.IsVisible).Sum(c => c.ActualWidth):0.##} [{string.Join(",", g.Columns.Where(c => c.IsVisible).Select(c => c.ActualWidth.ToString("0.##") + c.Width.UnitType.ToString()[0]))}] · presenter {rp?.Bounds.Width:0.#} · hbar {string.Join(";", hs)}›");
                }
            //  ⚠️ کادرِ تایپ متنِ بلندِ خودش را درونِ خودش می‌لغزاند — آن جابه‌جاییِ چیدمان نیست
            foreach (var sv in root.GetVisualDescendants().OfType<ScrollViewer>()
                         .Where(s => s.IsEffectivelyVisible && s.FindAncestorOfType<TextBox>() is null))
                if (sv.Extent.Width > sv.Viewport.Width + 1)
                    bad.Add($"اسکرولِ افقیِ «{Label(sv.GetVisualParent() as Control ?? sv)}»: {sv.Extent.Width:0} در {sv.Viewport.Width:0}");
        }
    }

    private static string Label(Control c) =>
        c.GetType().Name + (string.IsNullOrEmpty(c.Name) ? "" : "#" + c.Name)
        + (c is TextBlock t ? "«" + (t.Text?.Length > 30 ? t.Text[..30] : t.Text) + "»" : "")
        + (c.Classes.Count > 0 ? "." + string.Join(".", c.Classes) : "");
    private static Dictionary<string, (double L, double R)>? Base;
    private static Dictionary<string, int>? BaseCounts;
    private static byte[]? LastPng;

    private static readonly PropertyInfo? CellCol =
        typeof(DataGridCell).GetProperty("OwningColumn", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
    private static readonly PropertyInfo? HeadCol =
        typeof(DataGridColumnHeader).GetProperty("OwningColumn", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

    /// <summary>صفحه‌های دیدنیِ بخش — نه نوارِ بخش‌ها، نه سربرگِ پنجره (ساعت هر ثانیه عوض می‌شود).</summary>
    private static List<Visual> Roots(Window win)
    {
        var pages = win.GetVisualDescendants().OfType<SectionPage>().Where(p => p.IsEffectivelyVisible && p.Bounds.Width > 100)
           .Where(p => !p.GetVisualAncestors().OfType<SectionPage>().Any(a => a.IsEffectivelyVisible))
           .Cast<Visual>().ToList();
        if (pages.Count > 0) return pages;
        //  بخشی که ‎SectionPage‎ ندارد (داشبورد): خودِ نمای بخش
        return win.GetVisualDescendants().OfType<UserControl>()
            .Where(u => u.IsEffectivelyVisible && u.DataContext is SectionViewModel && u.Bounds.Width > 100)
            .Where(u => !u.GetVisualAncestors().OfType<UserControl>().Any(a => a.IsEffectivelyVisible && a.DataContext is SectionViewModel))
            .Cast<Visual>().ToList();
    }

    private static bool InData(Visual v, Visual root)
    {
        foreach (var a in v.GetVisualAncestors())
        {
            if (ReferenceEquals(a, root)) return false;
            if (a is DataGridRow or DataGridColumnHeader or ItemsRepeater or CardGrid or ComboBoxItem
                || a is ItemsControl ic && ic is not ComboBox && ic.ItemsSource is not null) return true;
        }
        return false;
    }

    private static void Measure(Window win, string what, bool pixels = true)
    {
        //  توستِ «فروشِ ورق‌های قدیمی درست شد» روی جدول می‌نشیند — سنجهٔ جوهر را نپوشاند
        if (AppHost.Current.Toasts.Visible) { AppHost.Current.Toasts.Visible = false; Settle(win); }
        if (Dbg)
            foreach (var cb in win.GetVisualDescendants().OfType<ComboBox>().Where(c => c.IsEffectivelyVisible && c.Items.OfType<YearMonthItem>().Any()))
                Console.WriteLine($"     ‹کشو {cb.Bounds.Width:0.#} کمینه {cb.MinWidth:0.#} «{(cb.SelectedItem as YearMonthItem)?.Label}» نقطه {(cb.SelectedItem as YearMonthItem)?.Dot}›");
        var roots = Roots(win);
        if (roots.Count == 0) { Check(what + ": صفحهٔ بخش پیدا شد", false); return; }
        var bad = new List<string>();
        var now = new Dictionary<string, (double L, double R)>();
        var seen = new Dictionary<string, int>();
        void Add(string k, Visual v)
        {
            seen[k] = seen.GetValueOrDefault(k) + 1;
            now[k + "#" + seen[k]] = Edges(v, win);
        }

        foreach (var root in roots)
        {
            foreach (var v in root.GetVisualDescendants().OfType<Control>().Where(c => c.IsEffectivelyVisible && c.Bounds.Width > 1 && InView(c, win)))
            {
                switch (v)
                {
                    case TextBlock tb when !string.IsNullOrWhiteSpace(tb.Text) && !InData(tb, root)
                                         && tb.FindAncestorOfType<ComboBox>() is null:
                        Add("T|" + tb.Text, tb); break;
                    case ComboBox cb when !InData(cb, root): Add("C|" + (cb.ItemsSource?.GetType().Name ?? ""), cb); break;
                    case Button b when b.Content is string bs && !InData(b, root): Add("B|" + bs, b); break;
                    case TextBox x when !InData(x, root): Add("X|" + x.Watermark, x); break;
                    case DataGridColumnHeader h when h.Bounds.Width > 1 && HeadCol?.GetValue(h) is DataGridColumn hc:
                        Add("H|" + hc.Header, h); break;
                }
            }
            //  نخستین کارتِ هر فهرستِ کارتی — ستون‌بندیِ کارت‌ها
            foreach (var ir in root.GetVisualDescendants().OfType<Control>()
                         .Where(c => c is ItemsRepeater or CardGrid && c.IsEffectivelyVisible))
                if (ir.GetVisualChildren().OfType<Control>().Where(c => c.IsVisible && c.Bounds.Width > 1)
                      .OrderBy(c => c.Bounds.Y).ThenByDescending(c => c.Bounds.X).FirstOrDefault() is { } first
                    && InView(first, win))
                    Add("I|" + ir.GetType().Name, first);
        }

        //  ⚠️ نوشتهٔ تکراری با شماره کلید می‌خورد («0 افغانی#2»)؛ اگر شمارِ همان نوشته
        //  عوض شد (کارتی صفر شد)، «#2»ِ امروز دیگر همان «#2»ِ دیروز نیست — مقایسه نمی‌شود
        var counts = seen;
        if (Base is null) { Base = now; BaseCounts = counts; }
        else
            foreach (var (k, e) in now)
                if (Base.TryGetValue(k, out var b0)
                    && BaseCounts!.GetValueOrDefault(k[..k.LastIndexOf('#')]) == counts.GetValueOrDefault(k[..k.LastIndexOf('#')])
                    && Math.Max(Math.Abs(b0.L - e.L), Math.Abs(b0.R - e.R)) > 1)
                    bad.Add($"«{k}» جابه‌جا شد: {b0.L:0}..{b0.R:0} ⇒ {e.L:0}..{e.R:0}");

        if (Environment.GetEnvironmentVariable("MS_OVERFLOW") != "0")
        {
            var ov = new List<string>();
            Overflow(win, roots, ov);
            var g = ov.GroupBy(x => System.Text.RegularExpressions.Regex.Replace(x, @"-?\d+(\.\d+)?", "N")).ToList();
            foreach (var x in g) bad.Add(x.First() + (x.Count() > 1 ? $" (×{x.Count()})" : ""));
        }
        //  ۱ تا ۳ و ۵) هر جدولِ دیدنی
        int cellN = 0, totN = 0, inkN = 0, fixN = 0;
        SkiaSharp.SKBitmap? frame = null;
        if (pixels)
        {
            win.CaptureRenderedFrame()?.Dispose();
            using var shot = win.CaptureRenderedFrame()!;
            using var ms = new MemoryStream();
            shot.Save(ms);
            ms.Position = 0;
            LastPng = ms.ToArray();
            ms.Position = 0;
            frame = SkiaSharp.SKBitmap.Decode(ms);
        }
        try
        {
            foreach (var g in roots.SelectMany(r => r.GetVisualDescendants().OfType<ExcelGrid>())
                                   .Where(x => x.IsEffectivelyVisible && x.Bounds.Width > 50 && InView(x, win)))
            {
                var heads = new Dictionary<DataGridColumn, (double L, double R)>();
                foreach (var h in g.GetVisualDescendants().OfType<DataGridColumnHeader>().Where(h => h.IsEffectivelyVisible && h.Bounds.Width > 1))
                    if (HeadCol?.GetValue(h) is DataGridColumn c) heads[c] = Edges(h, g);
                var rows = g.GetVisualDescendants().OfType<DataGridRow>().Where(r => r.IsEffectivelyVisible && InView(r, win)).ToList();
                foreach (var r in rows)
                    foreach (var cell in r.GetVisualDescendants().OfType<DataGridCell>().Where(c => c.IsEffectivelyVisible && c.Bounds.Width > 1))
                    {
                        if (CellCol?.GetValue(cell) is not DataGridColumn col || !heads.TryGetValue(col, out var he)) continue;
                        cellN++;
                        var ce = Edges(cell, g);
                        var d = Math.Max(Math.Abs(ce.L - he.L), Math.Abs(ce.R - he.R));
                        if (d > 1) bad.Add($"خانهٔ «{col.Header}» ردیفِ {r.Index + 1}: {d:0.#}px از سرستونش");
                        if (cell.GetVisualDescendants().OfType<Rectangle>().FirstOrDefault(x => x.Name == "PART_RightGridLine") is { IsEffectivelyVisible: true } line
                            && line.Bounds.Width > 0)
                        {
                            var le = Edges(line, g);
                            var dl = Math.Min(Math.Abs(le.L - he.L), Math.Abs(le.R - he.R));
                            if (dl > 1) bad.Add($"خطِ کنارِ «{col.Header}» ردیفِ {r.Index + 1}: {dl:0.#}px");
                        }
                        if (frame is not null && cell.Bounds.Width > 4)
                            foreach (var tb in cell.GetVisualDescendants().OfType<TextBlock>().Where(t => t.IsEffectivelyVisible && !string.IsNullOrEmpty(t.Text)))
                            {
                                inkN++;
                                if (OldMonthProbe.Off(tb, cell, frame) is { } o && o / win.RenderScaling > 3) bad.Add($"جوهرِ «{tb.Text}» {o:0}px" + (Dbg ? $" y={cell.TranslatePoint(default, win)!.Value.Y:0} h={cell.Bounds.Height:0} w={cell.Bounds.Width:0}" : ""));
                            }
                    }
                if (frame is not null)
                    foreach (var h in g.GetVisualDescendants().OfType<DataGridColumnHeader>().Where(h => h.IsEffectivelyVisible && h.Bounds.Width > 4))
                        foreach (var tb in h.GetVisualDescendants().OfType<TextBlock>().Where(t => t.IsEffectivelyVisible && !string.IsNullOrEmpty(t.Text)))
                        {
                            inkN++;
                            if (OldMonthProbe.Off(tb, h, frame) is { } o && o / win.RenderScaling > 3) bad.Add($"جوهرِ سرستونِ «{tb.Text}» {o:0}px");
                        }
                var strip = win.GetVisualDescendants().OfType<TotalsStrip>().FirstOrDefault(t => t.IsEffectivelyVisible
                                && ReferenceEquals(BarGrid(t.FindAncestorOfType<TotalsBar>()), g));
                if (strip is not null)
                    foreach (var ch in strip.Children.OfType<Control>().Where(c => c.IsVisible))
                    {
                        if (ch.DataContext is not TotalCell tc || string.IsNullOrEmpty(tc.Column)) continue;
                        var col = heads.Keys.FirstOrDefault(c => (c.Header?.ToString()?.Trim() ?? "") == tc.Column);
                        if (col is null) continue;
                        totN++;
                        var te = Edges(ch, g);
                        var he = heads[col];
                        var d = Math.Max(Math.Abs(te.L - he.L), Math.Abs(te.R - he.R));
                        if (d > 1) bad.Add($"جملهٔ «{tc.Column}»: {d:0.#}px از سرستونش");
                    }
            }

            //  ۴) وسط‌چینیِ ‎RtlTrim‎ — همهٔ نوشته‌های وسط‌چینِ صفحه
            foreach (var tb in roots.SelectMany(r => r.GetVisualDescendants().OfType<TextBlock>())
                        .Where(t => t.IsEffectivelyVisible && !string.IsNullOrEmpty(t.Text) && t.Bounds.Width > 4 && InView(t, win))
                        .Where(t => t.TextAlignment == TextAlignment.Center || RtlTrim.GetCenter(t) || RtlTrim.GetEnabled(t)))
            {
                fixN++;
                var dx = RtlTrim.CenterFix(tb);
                var want = tb.FlowDirection == FlowDirection.RightToLeft ? -dx : dx;
                var have = (tb.RenderTransform as TranslateTransform)?.X ?? 0;
                if (Math.Abs(want - have) > 2)
                {
                    bad.Add($"وسط‌چینیِ کهنهٔ «{tb.Text}»: دارد {have:0.#} باید {want:0.#}");
                    if (Dbg)
                        Console.WriteLine($"     ‹کهنه› #{System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(tb)} b={tb.Bounds} d={tb.DesiredSize} mv={tb.IsMeasureValid} av={tb.IsArrangeValid} wrap={tb.TextWrapping} max={tb.MaxLines} trim={tb.TextTrimming} fs={tb.FontSize} lines={tb.TextLayout?.TextLines.Count} w0={tb.TextLayout?.TextLines[0].Width:0.#} pad={tb.Padding} parent={(tb.GetVisualParent() as Control)?.Bounds} rt={tb.RenderTransform?.GetType().Name} owned={tb.GetValue((AvaloniaProperty)typeof(RtlTrim).GetField("OwnedProperty", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!)}");
                }
            }
        }
        finally
        {
            if (Dbg && frame is not null && bad.Any(b => b.StartsWith("جوهر", StringComparison.Ordinal)))
            {
                var png = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"mshift-ink-{++_shot}.png");
                using var fs = File.Create(png);
                frame.Encode(fs, SkiaSharp.SKEncodedImageFormat.Png, 90);
                Console.WriteLine("   عکس: " + png);
            }
            frame?.Dispose();
        }

        if (pixels && ShotDir.Length > 0 && LastPng is { } pngBytes)
        {
            var edges = now.Where(kv => kv.Key.StartsWith("H|") || kv.Key.StartsWith("C|") || kv.Key.StartsWith("I|")
                                        || kv.Key.StartsWith("B|") || kv.Key.StartsWith("X|"))
                           .ToDictionary(kv => kv.Key, kv => kv.Value);
            var rowsN = roots.SelectMany(r => r.GetVisualDescendants().OfType<ExcelGrid>())
                             .Where(x => x.IsEffectivelyVisible).Sum(x => x.ItemsSource is System.Collections.ICollection col ? col.Count : 0);
            Shots.Add(new Shot(CurGroup, CurTag, pngBytes, edges, rowsN, bad.Count == 0));
        }
        LastPng = null;
        if (ScaleBody.Corrections > 0)
        {
            Console.WriteLine($"     ‹قابِ بدنه {ScaleBody.Corrections} بار سرِ جایش برگشت · بیشترین لغزشِ گرفته‌شده {ScaleBody.LargestCorrection:0.#}px›");
            TotalCorrections += ScaleBody.Corrections;
            ScaleBody.ResetCorrections();
        }
        Check($"{what}: {now.Count} نشان · {cellN} خانه · {totN} جمله · {fixN} وسط‌چین" + (pixels ? $" · {inkN} جوهر" : ""),
              bad.Count == 0, bad.Count == 0 ? null : $"{bad.Count}: " + string.Join("، ", bad.Take(8)));
    }

    private static object? BarGrid(TotalsBar? b) =>
        b is null ? null : typeof(TotalsBar).GetMethod("ResolveGrid", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(b, null);

    private static bool InView(Visual v, Window win)
    {
        var p = v.TranslatePoint(default, win);
        return p is { } q && q.Y > -v.Bounds.Height && q.Y < win.Bounds.Height;
    }

    private static (double L, double R) Edges(Visual v, Visual to)
    {
        var a = v.TranslatePoint(default, to)!.Value.X;
        var b = v.TranslatePoint(new Point(v.Bounds.Width, 0), to)!.Value.X;
        return (Math.Min(a, b), Math.Max(a, b));
    }

    private static void WaitRows(MainWindow win)
    {
        for (var i = 0; i < 60; i++) { Dispatcher.UIThread.RunJobs(); Thread.Sleep(3); }
    }

    private static void Frame(Window w)
    {
        Dispatcher.UIThread.RunJobs();
        w.UpdateLayout();
        w.CaptureRenderedFrame()?.Dispose();
    }

    private static void Pump(Window w)
    {
        for (var i = 0; i < 8; i++) { Dispatcher.UIThread.RunJobs(); w.UpdateLayout(); }
    }

    private static void Settle(Window w)
    {
        for (var i = 0; i < 40; i++) { Pump(w); Thread.Sleep(5); }
        w.CaptureRenderedFrame()?.Dispose();
        Pump(w);
    }

    private static void Wait(Window w, Task t)
    {
        for (var i = 0; i < 2000 && !t.IsCompleted; i++) { Pump(w); Thread.Sleep(2); }
        Settle(w);
    }
}
