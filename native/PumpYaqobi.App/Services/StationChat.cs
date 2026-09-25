using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;

namespace PumpYaqobi.App.Services;

/// <summary>یک پیامِ گروهِ کارکنان — شکلِ پاسخِ سرورِ خانگی.</summary>
public sealed record GroupMessage(long Seq, string Cid, string From, string Role, string Text, long At)
{
    /// <summary>برچسبِ نقش برای کنارِ نام.</summary>
    public string RoleText => Role switch
    {
        "admin" => "مدیر", "mirza" => "میرزا", _ => "کارمند",
    };

    public static GroupMessage? Parse(JsonElement m)
    {
        if (m.ValueKind != JsonValueKind.Object) return null;
        string S(string k) => m.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
        long N(string k) => m.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt64(out var n) ? n : 0;
        var seq = N("seq");
        var text = S("text");
        if (seq <= 0 || text.Length == 0) return null;
        return new GroupMessage(seq, S("cid"), S("from"), S("role").Length > 0 ? S("role") : "staff", text, N("at"));
    }
}

/// <summary>
/// ══ گروهِ کارکنانِ همین پمپ — روی سرورِ خانگی ════════════════════════════════
///
/// خواستهٔ صاحب ریپو: «گروپ چت هم برای کارمندان و مدیر و میرزا استن و اون‌ها
/// بتونن باهم یک گروه تشکیل بدن، نه کاربران.»
///
/// <list type="bullet">
/// <item>⛔ <b>به پوشهٔ همین پمپ بسته است</b> (‎/api/stations/&lt;کد&gt;/chat‎)، نه
/// به یک موضوعِ سراسری: موضوعِ ‎staff‎ی پیام‌رسانِ قدیم روی سرورِ خانگی مالِ
/// <i>همهٔ</i> پمپ‌هایی بود که روی همان سرور بودند — گروهِ یک پمپ پیامِ پمپِ
/// دیگر را می‌دید.</item>
/// <item>⛔ مشتری هیچ راهی به آن ندارد: رمزِ نوشتنِ برنامه یا رمزِ خواندنِ
/// کارمندان لازم است، و کیو‌آرِ مشتری هیچ‌کدام را ندارد.</item>
/// <item>دو در: نخست نشانیِ شبکهٔ پمپ، بعد همان سرور از راهِ تونل
/// (‎CloudConfig.Url‎) — تا بیرون از پمپ هم برسد.</item>
/// <item>⚠️ سرور پیام را فقط ۱۵ روز نگه می‌دارد؛ نسخهٔ ماندگار در
/// <see cref="ChatStore"/>ِ همین کامپیوتر است.</item>
/// </list>
/// </summary>
public sealed class StationChat
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(12) };

    /// <summary>درگاهِ آزمون — فقط از آزمون‌ها مقدار می‌گیرد.</summary>
    public static Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>? TestTransport { get; set; }

    private readonly Func<AppSettings> _settings;

    public StationChat(Func<AppSettings> settings) => _settings = settings;

    /// <summary>کد و رمزِ این پمپ روی سرورِ خانگی هست؟</summary>
    public bool Ready
    {
        get
        {
            var s = _settings();
            return s.StationCode.Trim().Length > 0 && s.ServerToken.Trim().Length > 0;
        }
    }

    /// <summary>نشانی‌هایی که به ترتیب امتحان می‌شوند.</summary>
    public IReadOnlyList<string> Doors(string path)
    {
        var s = _settings();
        var list = new List<string>();
        var lan = (s.ServerUrl ?? "").Trim().TrimEnd('/');
        if (lan.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || lan.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            list.Add(lan + path);
        list.Add(CloudConfig.Url(path));
        return list;
    }

    private string Route(string tail = "")
    {
        var code = Uri.EscapeDataString(_settings().StationCode.Trim());
        return "/api/stations/" + code + "/chat" + tail;
    }

    /// <summary>بیشترین پیامی که سرور در یک پاسخ می‌دهد (سقفِ خودِ سرور ۵۰۰ است).</summary>
    public const int PageSize = 200;

    /// <summary>
    /// پیام‌های بعد از ‎since‎. ‎Last‎ بزرگ‌ترین شماره‌ای است که سرور تا حالا
    /// داده — ⚠️ اگر از ‎since‎ کمتر بود، پوشهٔ پمپ روی سرور از نو ساخته شده و
    /// صداکننده باید از صفر بپرسد.
    /// </summary>
    public async Task<(bool Ok, List<GroupMessage> Messages, string Why, long Last)> FetchAsync(
        long since, CancellationToken ct = default)
    {
        if (!Ready) return (false, new(), "این پمپ هنوز به سرورِ خانگی وصل نشده", 0);
        var (ok, json, why) = await SendAsync(HttpMethod.Get,
            Route("?since=" + Math.Max(0, since) + "&limit=" + PageSize), null, ct);
        if (!ok) return (false, new(), why, 0);
        var list = new List<GroupMessage>();
        if (json.TryGetProperty("messages", out var arr) && arr.ValueKind == JsonValueKind.Array)
            foreach (var m in arr.EnumerateArray())
                if (GroupMessage.Parse(m) is { } g) list.Add(g);
        var last = json.TryGetProperty("last", out var l) && l.ValueKind == JsonValueKind.Number && l.TryGetInt64(out var n)
            ? n : list.Select(m => m.Seq).DefaultIfEmpty(since).Max();
        return (true, list.OrderBy(m => m.Seq).ToList(), "", last);
    }

    /// <summary>فرستادن. ‎cid‎ همان پیام را در تلاشِ دوباره یکی نگه می‌دارد.</summary>
    public async Task<(bool Ok, GroupMessage? Message, string Why)> PostAsync(
        string cid, string from, string role, string text, CancellationToken ct = default)
    {
        if (!Ready) return (false, null, "این پمپ هنوز به سرورِ خانگی وصل نشده");
        var (ok, json, why) = await SendAsync(HttpMethod.Post, Route(),
            new { cid, from, role, text }, ct);
        if (!ok) return (false, null, why);
        return (true, json.TryGetProperty("message", out var m) ? GroupMessage.Parse(m) : null, "");
    }

    private async Task<(bool Ok, JsonElement Json, string Why)> SendAsync(
        HttpMethod method, string path, object? body, CancellationToken ct)
    {
        var why = "به سرورِ خانگی نرسیدیم";
        foreach (var url in Doors(path))
        {
            try
            {
                using var req = new HttpRequestMessage(method, url);
                req.Headers.Add("Authorization", "Bearer " + _settings().ServerToken.Trim());
                if (body is not null) req.Content = JsonContent.Create(body);
                using var res = TestTransport is null ? await Http.SendAsync(req, ct) : await TestTransport(req, ct);
                var text = await res.Content.ReadAsStringAsync(ct);
                JsonElement json = default;
                try { json = JsonDocument.Parse(text.Length > 0 ? text : "{}").RootElement.Clone(); } catch { }
                if (res.IsSuccessStatusCode && json.ValueKind == JsonValueKind.Object) return (true, json, "");
                //  ⚠️ جوابِ «رمز غلط» یا «چنین پمپی نیست» از خودِ سرور است — درِ
                //  دوم همان را می‌گوید، پس امتحانش فقط وقت می‌برد.
                //  ⚠️ سرورِ خانگی برای «رمزِ غلط» و «چنین پمپی نیست» عمداً یک جواب
                //  می‌دهد (۴۰۴ِ ‎not_found‎) تا کدِ پمپ‌ها حدس‌زدنی نباشد؛ و سرورِ
                //  کهنه این مسیر را اصلاً ندارد. پس هر سه در یک جمله گفته می‌شوند.
                if ((int)res.StatusCode is 401 or 403 or 404 && json.ValueKind == JsonValueKind.Object)
                    return (false, json, "گروه روی سرورِ خانگی باز نشد — سرورِ خانگی کهنه است یا رمزِ این پمپ را نپذیرفت");
                why = "سرورِ خانگی جواب نداد (" + (int)res.StatusCode + ")";
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch { why = "به سرورِ خانگی نرسیدیم"; }
        }
        return (false, default, why);
    }
}
