using System.Net;
using System.Net.Http;
using System.Text.Json;
using PumpYaqobi.App.Services;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ خبرهای پمپ روی ابر — «برنامه بسته هم باشد، خبر برسد» ══════════════════
///
/// خواستهٔ صاحبِ ریپو: «برنامه‌ها جوری باشند که بسته هم باشند، هر اتفاقی که
/// در برنامه بیفتد به سرور برود و سرور وقتی برنامه‌ها بسته هم هستند پیام را
/// برایشان بدهد، اگر کاربر نت داشت.»
///
/// ⛔ <b>تا امروز این فقط برای بخشِ دکان بود.</b> «اضافه برد» و «کم مانده»
/// فقط روی سرورِ <b>خانگی</b> می‌نشستند و گوشیِ کارمند هر پانزده دقیقه از
/// همان شبکه می‌پرسید — پس صاحبِ پمپی که بیرون بود، یا مودمش خاموش بود،
/// هیچ‌وقت خبر نمی‌گرفت.
///
/// چهار چیزی که این پرونده قفل می‌کند:
///   ۱) نوعِ هر خبر — فقط سه نوع روی سرور زنگ می‌زنند
///   ۲) یک خبر در هر دوره، نه هر بیست ثانیه یکی
///   ۳) هشداری که برطرف و دوباره پیدا شود، خبرِ <b>تازه</b> می‌دهد
///   ۴) درخواستی که نرفت، کلیدش پس گرفته می‌شود
/// </summary>
//  ⚠️ `AppSettings.DirOverride` استاتیک است و xUnit کلاس‌ها را موازی
//  می‌دواند — بی این نشان، این کلاس و هر کلاسِ دیگری که پوشه را عوض
//  می‌کند روی هم می‌نویسند.
[Collection(AppHostCollection.Name)]
public class CloudEventsTests : IDisposable
{
    private readonly string _dir;
    private readonly string? _was;
    private readonly List<string> _bodies = new();

    public CloudEventsTests()
    {
        _was = AppSettings.DirOverride;
        _dir = Path.Combine(Path.GetTempPath(), "pump-evt-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_dir);
        AppSettings.DirOverride = _dir;
    }

    public void Dispose()
    {
        CloudLink.TestTransport = null;
        AppSettings.DirOverride = _was;
        try { Directory.Delete(_dir, true); } catch { }
        GC.SuppressFinalize(this);
    }

    /// <summary>یک هشدار، همان شکلی که <c>StationSnapshot.Alerts</c> می‌سازد.</summary>
    private static Dictionary<string, object?> Alert(string key, string state, string who = "کریم") =>
        new()
        {
            ["k"] = key,
            ["n"] = who,
            ["f"] = "پطرول",
            ["s"] = state,
            ["t"] = who + " — پطرولِ حسابش " + (state == "out" ? "تمام شد" : "کم مانده"),
        };

    /// <summary>ابرِ ساختگی که هر بدنه را نگه می‌دارد.</summary>
    private void Serve(HttpStatusCode code = HttpStatusCode.Created)
    {
        CloudLink.TestTransport = async (req, ct) =>
        {
            _bodies.Add(req.Content is null ? "" : await req.Content.ReadAsStringAsync(ct));
            return new HttpResponseMessage(code)
            {
                Content = new StringContent("{\"ok\":true,\"saved\":1}",
                    System.Text.Encoding.UTF8, "application/json"),
            };
        };
    }

    private CloudLink Link()
    {
        var f = AppSettings.Load();
        f.CloudDeviceToken = "dev-token";
        f.Save();
        return new CloudLink(f, () => { f.Save(); return Task.CompletedTask; });
    }

    // ── ۱) نوعِ خبر ───────────────────────────────────────────────────────

    /// <summary>
    /// ⚠️ فقط <c>stock_out</c> · <c>low_stock</c> · <c>debt</c> روی سرور پوش
    /// می‌شوند. مخزن به دو تای اول می‌خورد و قرض‌دار به سومی.
    /// </summary>
    [Fact]
    public void NoeKhabar_Az_Kelid_Va_Hal_Miayad()
    {
        Assert.Equal("stock_out", CloudEvents.KindOf("tank-petrol-out", "out"));
        Assert.Equal("low_stock", CloudEvents.KindOf("tank-diesel-low", "low"));
        //  قرض‌دار هر دو حالش یک چیز است: قرض از حد گذشت
        Assert.Equal("debt", CloudEvents.KindOf("d7-stP-out", "out"));
        Assert.Equal("debt", CloudEvents.KindOf("d7-stM-low", "low"));
    }

    // ── ۲) یک خبر در هر دوره ─────────────────────────────────────────────

    /// <summary>
    /// همان هشدار در دورِ بعد خبرِ تازه نمی‌سازد — وگرنه هر بیست ثانیه یک
    /// زنگ روی گوشیِ صاحبِ پمپ می‌خورد و روزِ دوم اعلان‌ها خاموش می‌شوند.
    /// </summary>
    [Fact]
    public void Hamin_Hoshdar_DarDoreyeBad_Dobare_Nemiravad()
    {
        var ev = new CloudEvents();
        var alerts = new List<object?> { Alert("d7-stP-out", "out") };

        var first = ev.NewFrom(alerts, 1000);
        var second = ev.NewFrom(alerts, 2000);

        Assert.Equal(1, first.Count);
        Assert.Equal(0, second.Count);
    }

    /// <summary>هر هشدارِ تازه‌ای خبرِ خودش را دارد.</summary>
    [Fact]
    public void Hoshdare_Taze_Khabare_Khodash_Ra_Darad()
    {
        var ev = new CloudEvents();
        var one = ev.NewFrom(new List<object?> { Alert("d7-stP-out", "out") }, 1000);
        var two = ev.NewFrom(new List<object?>
        {
            Alert("d7-stP-out", "out"),
            Alert("tank-petrol-out", "out", "مخزنِ پطرول"),
        }, 2000);

        Assert.Equal(1, one.Count);
        Assert.Equal(1, two.Count);
        Assert.Contains("tank-petrol-out", two.Ids[0]);
    }

    /// <summary>
    /// «کم مانده ⇒ تمام شد» خبرِ تازه است، چون حال داخلِ خودِ کلید است.
    /// </summary>
    [Fact]
    public void Az_KamMande_Be_TamamShod_Khabare_Taze_Ast()
    {
        var ev = new CloudEvents();
        ev.NewFrom(new List<object?> { Alert("d7-stP-low", "low") }, 1000);
        var worse = ev.NewFrom(new List<object?> { Alert("d7-stP-out", "out") }, 2000);

        Assert.Equal(1, worse.Count);
    }

    // ── ۳) دوره‌ای که تمام شود و دوباره شروع شود ─────────────────────────

    /// <summary>
    /// ⛔ <b>مهم‌ترین قاعدهٔ این فایل.</b> حسابی که درست شد و دوباره خراب شد
    /// باید دوباره خبر بدهد. سرور با <c>clientId</c> ردیفِ تکراری نمی‌سازد،
    /// پس اگر کلید ثابت می‌ماند، بارِ دوم برای همیشه ساکت می‌شد — همان
    /// قاعده‌ای که گیرندهٔ <c>kar/</c> از روزِ اول دارد.
    /// </summary>
    [Fact]
    public void Hesabe_KharabShodeyeDobare_Saket_Nemimanad()
    {
        var ev = new CloudEvents();
        var alert = new List<object?> { Alert("d7-stP-out", "out") };

        var first = ev.NewFrom(alert, 1000);
        //  حساب درست شد: هشدار از فهرست افتاد
        ev.NewFrom(new List<object?>(), 2000);
        //  و دوباره خراب شد
        var again = ev.NewFrom(alert, 3000);

        Assert.Equal(1, first.Count);
        Assert.Equal(1, again.Count);
        Assert.NotEqual(first.Ids[0], again.Ids[0]);
    }

    // ── ۴) شکلِ بدنه ──────────────────────────────────────────────────────

    /// <summary>
    /// ⚠️ نامِ فیلدها باید همان چیزی باشد که سرور می‌خواند
    /// (<c>kind</c> · <c>title</c> · <c>clientId</c>) — نه با حرفِ بزرگ.
    /// </summary>
    [Fact]
    public async Task Badane_Haman_Shekli_Ast_Ke_Server_Mikhanad()
    {
        Serve();
        var ev = new CloudEvents();
        var sent = await ev.PublishAsync(Link(),
            new List<object?> { Alert("tank-diesel-low", "low", "مخزنِ دیزل") });

        Assert.Equal(1, sent);
        Assert.Single(_bodies);

        using var doc = JsonDocument.Parse(_bodies[0]);
        var first = doc.RootElement.GetProperty("events")[0];
        Assert.Equal("low_stock", first.GetProperty("kind").GetString());
        Assert.Contains("مخزنِ دیزل", first.GetProperty("title").GetString());
        Assert.StartsWith("tank-diesel-low:", first.GetProperty("clientId").GetString());
        Assert.Equal("low", first.GetProperty("data").GetProperty("state").GetString());
    }

    /// <summary>فهرستِ خالی هیچ درخواستی نمی‌زند — تپشِ بی‌خود ممنوع.</summary>
    [Fact]
    public async Task Fehreste_Khali_Hich_Darkhasti_Nemizanad()
    {
        Serve();
        var ev = new CloudEvents();

        Assert.Equal(0, await ev.PublishAsync(Link(), new List<object?>()));
        Assert.Empty(_bodies);
    }

    // ── ۵) نرفت ⇒ دوباره می‌رود ──────────────────────────────────────────

    /// <summary>
    /// ⛔ اگر درخواست نرفت، کلید پس گرفته می‌شود تا دورِ بعد دوباره تلاش
    /// شود. بی این، یک قطعیِ لحظه‌ایِ اینترنت یعنی خبری که هیچ‌وقت نرفت.
    /// </summary>
    [Fact]
    public async Task Khabari_Ke_Naraft_DoreyeBad_Dobare_Miravad()
    {
        var ev = new CloudEvents();
        var alerts = new List<object?> { Alert("d9-stD-out", "out") };

        Serve(HttpStatusCode.InternalServerError);
        Assert.Equal(0, await ev.PublishAsync(Link(), alerts));

        _bodies.Clear();
        Serve();
        Assert.Equal(1, await ev.PublishAsync(Link(), alerts));
        Assert.Single(_bodies);
    }

    /// <summary>بی‌اینترنت هم استثنا بیرون نمی‌دهد — خبر رفاه است، دفتر اصل.</summary>
    [Fact]
    public async Task Bi_Internet_Esteqna_Birun_Nemidahad()
    {
        CloudLink.TestTransport = (_, _) => throw new HttpRequestException("no network");
        var ev = new CloudEvents();

        var sent = await ev.PublishAsync(Link(),
            new List<object?> { Alert("d1-stP-out", "out") });

        Assert.Equal(0, sent);
    }

    /// <summary>
    /// برنامه‌ای که هنوز با حساب یا کد بند نشده، هیچ خبری نمی‌فرستد —
    /// توکنِ دستگاه ندارد و سرور هم نمی‌شناسدش.
    /// </summary>
    [Fact]
    public async Task Barnameye_BandNashode_Khabari_Nemifrestad()
    {
        Serve();
        var f = AppSettings.Load();
        f.CloudDeviceToken = "";
        f.Save();
        var link = new CloudLink(f, () => { f.Save(); return Task.CompletedTask; });

        var ev = new CloudEvents();
        Assert.Equal(0, await ev.PublishAsync(link,
            new List<object?> { Alert("d1-stP-out", "out") }));
        Assert.Empty(_bodies);
    }

    // ── ۶) سیم‌کشی — روی خودِ سورس ────────────────────────────────────────

    private static readonly string Root =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    private static string Src(params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { Root }.Concat(parts).ToArray()));

    /// <summary>
    /// ⛔ <b>خبرها پشتِ اشتراک نیستند</b> — همان قاعده‌ای که سرور دارد:
    /// خبر پیام است، نه دادهٔ فروشی. کسی که اشتراکش تمام شده بیشتر از همه
    /// لازم دارد بداند تیلش تمام شده.
    ///
    /// ⚠️ ولی قفلِ <b>عکسِ زنده</b> سرِ جایش می‌ماند — آن یکی واقعاً
    /// اشتراک می‌خواهد.
    /// </summary>
    [Fact]
    public void Khabarha_Poshte_Eshterak_Nistand_Vali_Akse_Zende_Hast()
    {
        var src = Src("PumpYaqobi.App", "Services", "StationPublisher.cs");

        //  ⛔ از ۱۴۰۵/۰۷/۱۴ خبر از عکس نمی‌رود: هر تیکِ پنج‌ثانیه‌ای، کلِ فهرستِ
        //  باز به «حالِ زنده» (سرور خودش تازه‌ها را می‌سنجد) — و پشتِ هیچ قفلی نیست.
        Assert.DoesNotContain("PublishAlertsAsync(snap, ct)", src);
        Assert.Contains("try { await AlertTickAsync(ct); }", src);
        var push = src[src.IndexOf("internal async Task<bool> PushStateAsync", StringComparison.Ordinal)..];
        push = push[..push.IndexOf("private static List<object?> AsSnapshot", StringComparison.Ordinal)];
        Assert.DoesNotContain("Entitlements.Allows(Entitlements.QrLive)", push);
        Assert.Contains("SendStateAsync(", push);
        //  سرورِ کهنه ⇒ همان راهِ قدیمی، نه سکوت
        Assert.Contains("res.Code == \"not_found\"", push);
        Assert.Contains("_events.PublishAsync(cloud, AsSnapshot(alerts), ct)", push);
        //  ⛔ و قفلِ عکسِ زنده برداشته نشده
        Assert.Contains("Entitlements.Allows(Entitlements.Kar)", src);
        Assert.Contains("Entitlements.Allows(Entitlements.QrLive)", src);
    }

    /// <summary>
    /// ⚠️ فهرست از همان <c>StationSnapshot.Alerts</c> می‌آید و این‌جا قاعدهٔ
    /// تازه‌ای ساخته نمی‌شود — وگرنه روزی کارتِ قرض‌دار سرخ است و گوشی ساکت.
    /// </summary>
    [Fact]
    public void Hoshdarhaye_Vaqeie_StationSnapshot_Shenakhte_Mishavand()
    {
        var people = new List<object?>
        {
            new Dictionary<string, object?> { ["id"] = 7, ["name"] = "کریم", ["stP"] = "out" },
            new Dictionary<string, object?> { ["id"] = 9, ["name"] = "احمد", ["stD"] = "low" },
            new Dictionary<string, object?> { ["id"] = 4, ["name"] = "سالم", ["stP"] = "ok" },
        };
        var tank = new Dictionary<string, object?>
        {
            ["petrol"] = new Dictionary<string, object?> { ["low"] = true, ["show"] = 12 },
        };

        var alerts = StationSnapshot.Alerts(people, tank);
        var batch = new CloudEvents().NewFrom(alerts, 1000);

        //  دو قرض‌دار و یک مخزن — و «سالم» هیچ خبری نمی‌سازد
        Assert.Equal(3, batch.Count);
        Assert.Contains(batch.Ids, id => id.StartsWith("d7-stP-out:", StringComparison.Ordinal));
        Assert.Contains(batch.Ids, id => id.StartsWith("d9-stD-low:", StringComparison.Ordinal));
        Assert.Contains(batch.Ids, id => id.StartsWith("tank-petrol-out:", StringComparison.Ordinal));
    }
}
