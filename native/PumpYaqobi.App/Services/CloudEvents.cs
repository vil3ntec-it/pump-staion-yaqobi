namespace PumpYaqobi.App.Services;

/// <summary>
/// خبرهای پمپ روی ابر — «برنامه بسته هم باشد، خبر برسد».
///
/// <para>
/// ⛔ <b>چیزی که تا امروز نبود.</b> خواستهٔ صاحبِ ریپو: «برنامه‌ها جوری باشند
/// که بسته هم باشند، هر اتفاقی که در برنامه بیفتد به سرور برود و سرور وقتی
/// برنامه‌ها بسته هم هستند پیام را برایشان بدهد، اگر کاربر نت داشت.»
/// </para>
///
/// <para>
/// تا دیروز «اضافه برد» و «کم مانده» فقط روی <b>سرورِ خانگی</b> می‌نشستند
/// (<c>live.alerts</c>) و گوشیِ کارمند هر پانزده دقیقه از <b>همان شبکه</b>
/// می‌پرسید. یعنی صاحبِ پمپی که بیرون بود — یا مودمش خاموش بود — هیچ‌وقت
/// خبر نمی‌گرفت. حالا همان فهرست به ابر هم می‌رود و ابر گوشی را بیدار
/// می‌کند.
/// </para>
///
/// <para>
/// ⚠️ <b>فهرست جای دیگری ساخته نمی‌شود</b>: ورودیِ این کلاس همان
/// <see cref="StationSnapshot.Alerts"/> است که کارتِ قرض‌دار هم از آن رنگ
/// می‌گیرد. قاعدهٔ جدا ننویسید، وگرنه روزی کارت سرخ است و گوشی ساکت.
/// </para>
/// </summary>
public sealed class CloudEvents
{
    /// <summary>
    /// کلیدهایی که همین حالا در فهرستِ هشدارند، و از کِی.
    ///
    /// <para>
    /// ⚠️ <b>چرا زمانِ نخستین دیدار هم نگه داشته می‌شود:</b> سرور با
    /// <c>clientId</c> ردیفِ تکراری نمی‌سازد — و این برای «هر بیست ثانیه
    /// دوباره نفرست» درست است، ولی برای «حسابِ خراب‌شدهٔ دوباره» غلط
    /// می‌شد: کلیدی که یک بار رفته، تا ابد رفته حساب می‌شد و بارِ دوم
    /// هیچ زنگی نمی‌خورد.
    /// </para>
    /// <para>
    /// پس کلیدی که از فهرست بیفتد فراموش می‌شود، و اگر برگردد
    /// <c>clientId</c>ِ تازه‌ای می‌گیرد (کلید + زمانِ شروعِ همین دوره).
    /// همان قاعدهٔ گیرندهٔ <c>kar/</c>.
    /// </para>
    /// </summary>
    private readonly Dictionary<string, long> _open = new(StringComparer.Ordinal);

    /// <summary>کلیدهایی که در همین دوره فرستاده شده‌اند — دوباره نمی‌روند.</summary>
    private readonly HashSet<string> _sent = new(StringComparer.Ordinal);

    /// <summary>شمارِ خبرهایی که آخرین بار واقعاً فرستاده شد — برای سنجه‌ها.</summary>
    public int LastSent { get; private set; }

    /// <summary>
    /// نوعِ خبر از روی کلید و حالِ هشدار.
    ///
    /// <para>
    /// ⚠️ فقط همین سه نوع روی سرور <b>پوش</b> می‌شوند
    /// (<c>events.PUSH_KINDS</c>). هر نوعِ دیگری در فهرست می‌نشیند و با
    /// باز شدنِ برنامه دیده می‌شود — نه زنگ.
    /// </para>
    /// </summary>
    public static string KindOf(string key, string state)
    {
        //  مخزن: «ته کشید» یعنی کالا تمام شد، «دارد ته می‌کشد» یعنی کم مانده
        if (key.StartsWith("tank-", StringComparison.Ordinal))
            return state == "out" ? "stock_out" : "low_stock";
        //  قرض‌دار: هر دو حال یک چیزند — قرض از حد گذشت
        return "debt";
    }

    /// <summary>
    /// خبرهای تازهٔ یک دور: بدنه‌ای که به سرور می‌رود، و کلیدهایش.
    ///
    /// <para>
    /// ⚠️ بدنه عمداً شیءِ بی‌نام است، نه یک رکورد: سریال‌سازِ پیش‌فرض نامِ
    /// خاصیت را همان‌طور که نوشته شده می‌برد، و سرور نامِ کوچک می‌خواهد
    /// (<c>kind</c>، نه <c>Kind</c>). کلیدها جدا نگه داشته می‌شوند تا
    /// اگر درخواست نرفت بشود پسشان گرفت.
    /// </para>
    /// </summary>
    public sealed record Batch(List<object> Payload, List<string> Ids)
    {
        public int Count => Payload.Count;
    }

    /// <summary>
    /// فهرستِ هشدارها ⇒ خبرهایی که باید <b>تازه</b> فرستاده شوند.
    ///
    /// <para>هشدارهایی که از فهرست افتاده‌اند فراموش می‌شوند.</para>
    /// </summary>
    public Batch NewFrom(IEnumerable<object?>? alerts, long nowMs)
    {
        var alive = new HashSet<string>(StringComparer.Ordinal);
        var payload = new List<object>();
        var ids = new List<string>();

        foreach (var a in alerts ?? Enumerable.Empty<object?>())
        {
            if (a is not Dictionary<string, object?> d) continue;
            var key = d.TryGetValue("k", out var k) ? k as string ?? "" : "";
            if (key.Length == 0) continue;
            alive.Add(key);

            if (!_open.TryGetValue(key, out var since))
            {
                since = nowMs;
                _open[key] = since;
            }

            var clientId = key + ":" + since;
            if (!_sent.Add(clientId)) continue;      // در همین دوره رفته

            var state = d.TryGetValue("s", out var s) ? s as string ?? "" : "";
            ids.Add(clientId);
            payload.Add(new
            {
                kind = KindOf(key, state),
                title = d.TryGetValue("t", out var t) ? t as string ?? "" : "",
                clientId,
                data = new
                {
                    who = d.TryGetValue("n", out var n) ? n as string ?? "" : "",
                    fuel = d.TryGetValue("f", out var f) ? f as string ?? "" : "",
                    state,
                },
            });
        }

        //  کلیدی که دیگر در فهرست نیست، دوره‌اش تمام شده
        foreach (var gone in _open.Keys.Where(x => !alive.Contains(x)).ToList())
        {
            _sent.Remove(gone + ":" + _open[gone]);
            _open.Remove(gone);
        }

        return new Batch(payload, ids);
    }

    /// <summary>
    /// هشدارهای تازهٔ این عکس را به ابر می‌فرستد.
    ///
    /// <para>
    /// ⚠️ اگر نرفت، کلیدها را پس می‌گیرد تا دورِ بعد دوباره تلاش شود —
    /// وگرنه یک قطعیِ لحظه‌ایِ اینترنت یعنی خبری که هیچ‌وقت نرفت.
    /// </para>
    /// <para>⚠️ و هیچ‌وقت استثنا بیرون نمی‌دهد: خبر رفاه است، دفتر اصل.</para>
    /// </summary>
    public async Task<int> PublishAsync(CloudLink cloud, IEnumerable<object?>? alerts,
                                        CancellationToken ct = default)
    {
        LastSent = 0;
        Batch fresh;
        try { fresh = NewFrom(alerts, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()); }
        catch { return 0; }
        if (fresh.Count == 0) return 0;

        try
        {
            var res = await cloud.SendEventsAsync(fresh.Payload, ct);
            if (!res.Ok) { Rollback(fresh); return 0; }
            LastSent = fresh.Count;
            return fresh.Count;
        }
        catch (OperationCanceledException) { Rollback(fresh); throw; }
        catch { Rollback(fresh); return 0; }
    }

    /// <summary>کلیدهای یک دستهٔ نرفته را پس می‌گیرد تا دورِ بعد دوباره برود.</summary>
    private void Rollback(Batch fresh)
    {
        foreach (var id in fresh.Ids) _sent.Remove(id);
    }

    /// <summary>فراموش کردنِ همه‌چیز — برای جابه‌جاییِ پمپ و سنجه‌ها.</summary>
    public void Reset()
    {
        _open.Clear();
        _sent.Clear();
        LastSent = 0;
    }
}
