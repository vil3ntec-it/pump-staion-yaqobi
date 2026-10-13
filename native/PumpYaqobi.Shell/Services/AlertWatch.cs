using PumpYaqobi.Domain;
namespace PumpYaqobi.App.Services;

/// <summary>یک هشدارِ باز — همان شکلی که <see cref="StationSnapshot.Alerts"/> می‌سازد.</summary>
/// <param name="Key">کلیدِ ثابت، با حال: ‎d7-stP-out‎ · ‎tank-diesel-low‎.</param>
/// <param name="State">‎out‎ (تمام شد) یا ‎low‎ (کم مانده).</param>
/// <param name="Action">دستورِ کار — «به او دیگر تیل ندهید»، «امروز دیزل سفارش بدهید».</param>
public sealed record AlertItem(string Key, string Name, string Fuel, string State, string Text, string Action = "")
{
    public bool IsTank => Key.StartsWith("tank-", StringComparison.Ordinal);
    /// <summary>کسریِ صندوق یا شیفتِ بسته‌نشده (‎cash-…‎ · ‎shift-…‎) — از ورق.</summary>
    public bool IsShift => Key.StartsWith("cash-", StringComparison.Ordinal) || Key.StartsWith("shift-", StringComparison.Ordinal);
    public bool IsOut => State == "out";

    /// <summary>بخشی که با کلیک روی این هشدار باز می‌شود.</summary>
    public string Section => IsTank ? (Key.Contains("diesel", StringComparison.Ordinal) ? "storage-diesel" : "storage")
                           : IsShift ? "waraq" : "debt";
}

/// <summary>
/// ══ هشدارهای بازِ همین پمپ — یک فهرست، برای زنگ، توست، سرور و بات ═══════════
///
/// <para>
/// گزارشِ صاحب ریپو (۱۴۰۵/۰۷/۱۴، با عکسِ زنگِ داشبورد): «توی برنامهٔ پمپ
/// بنزین برای قرض‌داری که حسابش تموم بشه یا قرض‌دار بشه، اون‌جا چرا پیامِ
/// هشدار به میرزا نمیاد… دو سه تا هشدار استن اما سرور و برنامه بی‌توجهی
/// می‌کنن.»
/// </para>
///
/// <para>
/// ⛔ <b>ریشه: دو قاعده برای یک چیز.</b> زنگِ داشبورد مخزن را با قاعدهٔ خودش
/// می‌شمرد (موجودیِ خام زیرِ حد — حتی مخزنی که هرگز خریدی نداشته) و
/// قرض‌داران را اصلاً نمی‌شمرد؛ و آن‌چه به سرور و بات می‌رفت
/// <see cref="StationSnapshot.Alerts"/> بود، با قاعدهٔ خودِ بخشِ مخزن
/// (<c>TankState.IsLow/IsNear</c>) و با قرض‌داران. پس زنگ «۲» می‌گفت و بات
/// «هیچ هشداری نیست» — و هر دو از نگاهِ خودشان راست می‌گفتند.
/// </para>
///
/// <para>
/// حالا فقط همین کلاس فهرست را می‌سازد (از همان <see cref="StationSnapshot.Alerts"/>)
/// و هر سه از آن می‌خوانند: زنگِ داشبورد، توستِ میرزا، و حالِ زنده‌ای که به
/// سرور و بات می‌رود. ⛔ قاعدهٔ جدا ننویسید.
/// </para>
///
/// <para>
/// ⚡ <b>قانونِ سرعت</b>: با ترمزِ <c>PumpDbContext.Version</c> (داده عوض نشده ⇒
/// صفر دستورِ دیتابیس) و کمینهٔ <see cref="MinGap"/> میانِ دو محاسبه — تایپِ
/// پشتِ سرِ همِ میرزا هر حرف یک محاسبه نمی‌سازد. هزینهٔ هر محاسبه ثابت است:
/// یک ‎GROUP BY‎ برای قرض‌داران و سه خواندنِ ستونی برای هر مخزن.
/// </para>
/// </summary>
public sealed class AlertWatch
{
    /// <summary>کمترین فاصلهٔ دو محاسبه.</summary>
    public static readonly TimeSpan MinGap = TimeSpan.FromSeconds(5);

    private readonly object _gate = new();
    private IReadOnlyList<AlertItem> _current = Array.Empty<AlertItem>();
    private long _lastVersion = -1;
    private DateTime _lastRun = DateTime.MinValue;
    /// <summary>
    /// ساعتِ آخرین محاسبه (‎yyyyMMddHH‎). ⚠️ «شیفتِ دیروز بسته نشد» با گذشتِ زمان باز می‌شود،
    /// نه با تغییرِ داده؛ پس ترمزِ ‎Version‎ ساعتی یک بار کنار می‌رود — بیشتر نه.
    /// </summary>
    private long _lastHour = -1;
    private bool _primed;

    /// <summary>هشدارهای بازِ همین حالا.</summary>
    public IReadOnlyList<AlertItem> Current { get { lock (_gate) return _current; } }

    /// <summary>موجودیِ دو مخزن، همان شکلِ <c>snap["tank"]</c>.</summary>
    public Dictionary<string, object?> Tank { get; private set; } = new();

    /// <summary>حال و الباقیِ قرض‌داران (بی جدول) — برای جست‌وجوی بات.</summary>
    public List<object?> Debtors { get; private set; } = new();

    /// <summary>شمارهٔ دورِ محاسبه — با هر محاسبهٔ تازه یکی بالا می‌رود.</summary>
    public long Revision { get; private set; }

    /// <summary>آیا دستِ‌کم یک بار حساب شده است.</summary>
    public bool Ready => _primed;

    /// <summary>
    /// فهرست عوض شد. <c>opened</c> هشدارهایی است که <b>تازه</b> باز شدند؛
    /// <c>initial</c> یعنی نخستین محاسبهٔ این اجرا (همه‌چیز «تازه» است).
    /// ⚠️ روی نخِ پس‌زمینه شلیک می‌شود — شنونده خودش به نخِ رابط برود.
    /// </summary>
    public event Action<IReadOnlyList<AlertItem>, bool>? Changed;

    /// <summary>
    /// اگر داده عوض شده (و <see cref="MinGap"/> گذشته)، از نو حساب می‌کند.
    /// خروجی: آیا حساب شد. هیچ‌وقت استثنا بیرون نمی‌دهد جز لغو.
    /// </summary>
    public async Task<bool> CheckAsync(AppHost host, bool force = false, CancellationToken ct = default)
    {
        var version = PumpYaqobi.Persistence.PumpDbContext.Version;
        var hour = HourKey(AppClock.Now);
        if (!force && _primed && hour == _lastHour && version == _lastVersion) return false;
        if (!force && AppClock.Mono - _lastRun < MinGap) return false;
        _lastRun = AppClock.Mono;
        try
        {
            var tank = await StationSnapshot.TankAsync(host, ct);
            var people = await StationSnapshot.DebtorsLiteAsync(host, ct);
            var list = FromSnapshot(StationSnapshot.Alerts(people, tank)).ToList();
            //  مخزن‌های شماره‌دار (۱۴۰۵/۰۷/۲۲): «تیلش کم شد هشدار برود» — و وقتی تمام شد و
            //  نوبت به مخزنِ بعدی رسید، گفته شود. ⚠️ جدا از هشدارِ کلِ تیل: آن سرِ جایش است.
            try { list.AddRange(await NumberedTankAlertsAsync(host, ct)); } catch (OperationCanceledException) { throw; } catch { }
            //  کسریِ صندوق و شیفتِ بسته‌نشده (۱۴۰۵/۰۷/۲۱) — از ورقِ چند روزِ آخر
            try { list.AddRange(await ShiftAlertsAsync(host, ct)); } catch (OperationCanceledException) { throw; } catch { }
            Accept(list, tank, people);
            _lastVersion = version;
            _lastHour = hour;
            return true;
        }
        catch (OperationCanceledException) { throw; }
        catch { return false; }
    }

    /// <summary>
    /// فهرستِ تازه را می‌نشاند و فرقش را خبر می‌دهد — جدا از خواندنِ دیتابیس
    /// تا آزمون‌پذیر باشد.
    /// </summary>
    /// <summary>هشدارِ مخزن‌های شماره‌دار — فقط مخزنِ «در حالِ کشیدن» و مخزن‌هایی که پیش از آن تمام شدند.</summary>
    internal static async Task<List<AlertItem>> NumberedTankAlertsAsync(AppHost host, CancellationToken ct)
    {
        var res = new List<AlertItem>();
        var threshold = host.Settings.GetDecimal(PumpYaqobi.Services.Data.SettingsService.LowStockThreshold, 1000m);
        foreach (var fuel in new[] { PumpYaqobi.Domain.Enums.FuelType.Petrol, PumpYaqobi.Domain.Enums.FuelType.Diesel })
        {
            var levels = await host.StorageData.TankLevelsAsync(fuel, ct);
            if (levels.Count == 0) continue;
            var f = fuel == PumpYaqobi.Domain.Enums.FuelType.Diesel ? "diesel" : "petrol";
            var name = fuel == PumpYaqobi.Domain.Enums.FuelType.Diesel ? "دیزل" : "پطرول";
            var act = levels.FirstOrDefault(l => l.Active);
            foreach (var l in levels.TakeWhile(l => !l.Active).Where(l => l.Display <= 0m))
                if (act.Display > 0m)
                    res.Add(new AlertItem($"tank-{f}-n{l.Num}-switched", $"مخزنِ {l.Num} {name}", f, "low",
                        $"مخزنِ {l.Num} {name} تمام شد — حالا از مخزنِ {act.Num} کشیده می‌شود"));
            if (act.Display > 0m && act.Display <= threshold)
                res.Add(new AlertItem($"tank-{f}-n{act.Num}-low", $"مخزنِ {act.Num} {name}", f, "low",
                    $"مخزنِ {act.Num} {name} کم مانده: {Math.Round(act.Display):N0} لیتر"));
        }
        return res;
    }

    private static long HourKey(DateTime t) => t.Year * 1_000_000L + t.Month * 10_000L + t.Day * 100L + t.Hour;

    /// <summary>چند روزِ آخر که ورقش سنجیده می‌شود (امروز هم).</summary>
    public const int ShiftDays = 3;

    /// <summary>شیفتِ شبِ دیروز تا این ساعتِ امروز فرصت دارد بسته شود.</summary>
    public const int NightGraceHour = 12;

    internal static async Task<List<AlertItem>> ShiftAlertsAsync(AppHost host, CancellationToken ct)
    {
        var now = AppClock.Now;
        var from = PumpYaqobi.Application.Localization.Shamsi.Key(now.Date.AddDays(-ShiftDays));
        var entries = await host.WaraqData.SinceAsync(from, ct);
        return ShiftAlerts(entries, now, host.Waraq);
    }

    /// <summary>
    /// ══ کسریِ صندوق و شیفتِ بسته‌نشده — از ورق (۱۴۰۵/۰۷/۲۱) ══════════════════════
    ///
    /// خواستهٔ صاحب ریپو: «کسری صندوق و شیفت بسته‌نشده رو هم درست کن.»
    ///
    /// ⛔ قاعدهٔ تازه‌ای برای پول ساخته نمی‌شود:
    ///   • <b>کسری</b> همان <c>WaraqService.Shortage</c> است — همان «⚠️ کمبودی»ِ ورق و
    ///     ستونِ «کمبودی»ِ گزارش (قرضِ نوشته‌شده در پایه‌ها منهای قرض و مصرفِ ثبت‌شده).
    ///   • <b>بسته‌نشده</b> یعنی پایه‌ای که شروع دارد و ختمش خالی است (همان خانهٔ خالیِ ورق).
    ///
    /// ⚠️ شیفتِ در حالِ کار هشدار نمی‌دهد:
    ///   • شیفتِ امروز: روز فقط وقتی شیفتِ فعالِ ورق «شب» شده (تحویل شده)؛ شبِ امروز هرگز.
    ///   • شبِ دیروز تا ساعتِ <see cref="NightGraceHour"/>ِ امروز فرصت دارد.
    ///   • کسری فقط برای شیفتِ بسته — نیمه‌کاره هنوز قرض‌هایش را ننوشته.
    /// ⚠️ کلید شناسهٔ ورق و شیفت را دارد، پس هر شیفت یک بار خبر می‌دهد؛ و با درست شدنِ
    /// ورق از فهرست می‌افتد («برطرف شد»).
    /// </summary>
    public static List<AlertItem> ShiftAlerts(IEnumerable<PumpYaqobi.Domain.Entities.WaraqEntry> entries,
                                                DateTime now, PumpYaqobi.Application.Services.WaraqService calc)
    {
        var res = new List<AlertItem>();
        var today = PumpYaqobi.Application.Localization.Shamsi.Key(now.Date);
        var yesterday = PumpYaqobi.Application.Localization.Shamsi.Key(now.Date.AddDays(-1));
        foreach (var w in entries.OrderByDescending(e => e.DateKey))
        {
            if (w.DateKey <= 0 || w.DateKey > today) continue;
            foreach (var sh in w.Shifts.OrderBy(s => s.Kind))
            {
                var night = sh.Kind == PumpYaqobi.Domain.Entities.ShiftKind.Night;
                var done = night
                    ? w.DateKey < yesterday || (w.DateKey == yesterday && now.Hour >= NightGraceHour)
                    : w.DateKey < today || w.ActiveShift == PumpYaqobi.Domain.Entities.ShiftKind.Night;
                if (!done) continue;

                var open = sh.Pumps.Count(p => p.Start > 0m && p.End <= 0m);
                var kind = night ? "night" : "day";
                var label = "شیفتِ " + (night ? "شبِ " : "روزِ ") + (w.DateShamsi ?? "");
                var who = (sh.WorkerName ?? "").Trim();
                var whoPart = who.Length > 0 ? " (" + who + ")" : "";
                if (open > 0)
                {
                    res.Add(new AlertItem("shift-" + w.Id + "-" + kind, who.Length > 0 ? who : label, "", "low",
                        label + whoPart + " بسته نشده — ختمِ " + open + " پایه نوشته نشده",
                        "ختمِ پایه‌ها را در ورق بنویسید تا فروش و گاوصندوق درست شوند."));
                    continue;
                }
                if (sh.Pumps.Count == 0) continue;
                var gap = Math.Round(calc.Shortage(calc.ShiftTotals(sh)).Shortage, 0, MidpointRounding.AwayFromZero);
                if (gap < 1m) continue;
                var amount = PumpYaqobi.Application.Localization.Shamsi.Money(gap) + " افغانی";
                res.Add(new AlertItem("cash-" + w.Id + "-" + kind, who.Length > 0 ? who : label, "", "low",
                    "کسریِ صندوق — " + label + whoPart + ": " + amount + " کم است",
                    "قرض یا مصرفِ جامانده را در ورق ثبت کنید؛ وگرنه " + amount + " را از "
                    + (who.Length > 0 ? who : "کارمندِ شیفت") + " بگیرید."));
            }
        }
        return res;
    }

    public IReadOnlyList<AlertItem> Accept(IReadOnlyList<AlertItem> list,
                                           Dictionary<string, object?>? tank = null,
                                           List<object?>? people = null)
    {
        IReadOnlyList<AlertItem> opened;
        bool initial;
        bool changed;
        lock (_gate)
        {
            var before = new HashSet<string>(_current.Select(a => a.Key), StringComparer.Ordinal);
            opened = list.Where(a => !before.Contains(a.Key)).ToList();
            initial = !_primed;
            changed = initial || opened.Count > 0 || before.Count != list.Count
                      || list.Any(a => !before.Contains(a.Key));
            _current = list;
            if (tank is not null) Tank = tank;
            if (people is not null) Debtors = people;
            _primed = true;
            Revision++;
        }
        if (changed) Changed?.Invoke(opened, initial);
        return opened;
    }

    /// <summary>دفترِ دیگر (حسابِ دیگر) — از نو، و نخستین محاسبهٔ بعد دوباره «آغاز» است.</summary>
    public void Reset()
    {
        lock (_gate)
        {
            _current = Array.Empty<AlertItem>();
            Tank = new();
            Debtors = new();
            _primed = false;
            _lastVersion = -1;
            _lastHour = -1;
            _lastRun = DateTime.MinValue;
        }
    }

    /// <summary>خروجیِ <see cref="StationSnapshot.Alerts"/> ⇒ فهرستِ نوع‌دار.</summary>
    public static IReadOnlyList<AlertItem> FromSnapshot(IEnumerable<object?>? alerts)
    {
        var list = new List<AlertItem>();
        foreach (var a in alerts ?? Enumerable.Empty<object?>())
        {
            if (a is not Dictionary<string, object?> d) continue;
            string S(string k) => d.TryGetValue(k, out var v) ? v as string ?? "" : "";
            var key = S("k");
            if (key.Length == 0) continue;
            list.Add(new AlertItem(key, S("n"), S("f"), S("s") == "out" ? "out" : "low", S("t"), S("a")));
        }
        return list;
    }

    /// <summary>متنِ توستِ میرزا برای هشدارهای تازه — یک پیام، نه یکی برای هر هشدار.</summary>
    public static string ToastText(IReadOnlyList<AlertItem> opened, bool initial)
    {
        if (opened.Count == 0) return "";
        //  ⛔ یک خط برای هر هشدار (۱۴۰۵/۰۷/۱۴) — پیش از این همه با «·» در یک
        //  خطِ دراز می‌آمدند و «نه خوانده می‌شد نه جدا جدا بود». فهرستِ کامل
        //  پشتِ زنگِ داشبورد است.
        if (opened.Count == 1)
            return (initial ? "🔔 1 هشدارِ باز: " : "🔔 هشدارِ تازه: ") + opened[0].Text;
        var head = initial ? "🔔 " + opened.Count + " هشدارِ باز:" : "🔔 هشدارِ تازه:";
        var shown = opened.Take(3).Select(a => "• " + a.Text);
        var more = opened.Count > 3 ? "\n• و " + (opened.Count - 3) + " هشدارِ دیگر — فهرستِ کامل پشتِ زنگِ داشبورد" : "";
        return head + "\n" + string.Join("\n", shown) + more;
    }
}
