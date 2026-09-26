namespace PumpYaqobi.App.Services;

/// <summary>یک هشدارِ باز — همان شکلی که <see cref="StationSnapshot.Alerts"/> می‌سازد.</summary>
/// <param name="Key">کلیدِ ثابت، با حال: ‎d7-stP-out‎ · ‎tank-diesel-low‎.</param>
/// <param name="State">‎out‎ (تمام شد) یا ‎low‎ (کم مانده).</param>
/// <param name="Action">دستورِ کار — «به او دیگر تیل ندهید»، «امروز دیزل سفارش بدهید».</param>
public sealed record AlertItem(string Key, string Name, string Fuel, string State, string Text, string Action = "")
{
    public bool IsTank => Key.StartsWith("tank-", StringComparison.Ordinal);
    public bool IsOut => State == "out";

    /// <summary>بخشی که با کلیک روی این هشدار باز می‌شود.</summary>
    public string Section => IsTank ? (Key.Contains("diesel", StringComparison.Ordinal) ? "storage-diesel" : "storage") : "debt";
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
        if (!force && _primed && version == _lastVersion) return false;
        if (!force && DateTime.UtcNow - _lastRun < MinGap) return false;
        _lastRun = DateTime.UtcNow;
        try
        {
            var tank = await StationSnapshot.TankAsync(host, ct);
            var people = await StationSnapshot.DebtorsLiteAsync(host, ct);
            var list = FromSnapshot(StationSnapshot.Alerts(people, tank));
            Accept(list, tank, people);
            _lastVersion = version;
            return true;
        }
        catch (OperationCanceledException) { throw; }
        catch { return false; }
    }

    /// <summary>
    /// فهرستِ تازه را می‌نشاند و فرقش را خبر می‌دهد — جدا از خواندنِ دیتابیس
    /// تا آزمون‌پذیر باشد.
    /// </summary>
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
