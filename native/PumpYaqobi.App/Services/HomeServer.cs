using System.Net.Http;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PumpYaqobi.App.Services;

/// <summary>یک پیامِ رسیده از سرورِ خانگی.</summary>
/// <param name="Topic">موضوعی که پیام رویش آمده.</param>
/// <param name="Title">نامِ فرستنده — سرور فیلدِ اضافه پاس نمی‌دهد، پس نام در ‎title‎ می‌نشیند.</param>
/// <param name="Message">خودِ متن.</param>
/// <param name="Tags">برچسب‌ها؛ شناسهٔ گفت‌وگو همین‌جاست (‎p-…‎).</param>
/// <param name="At">زمانِ سرور (ثانیه). ‎0‎ یعنی سرور نگفت.</param>
public sealed record HomeMessage(string Topic, string Title, string Message,
                                 IReadOnlyList<string> Tags, long At);

/// <summary>
/// ══ سرورِ خانگیِ خودِ صاحب ریپو ═════════════════════════════════════════════
///
/// ⚠️ هیچ سرویسِ بیرونی‌ای این‌جا نیست و نباید بیاید. همان سروری است که نسخهٔ
/// وب هم با آن کار می‌کند، با همان دو مسیر:
///
///     فرستادن : POST ‎{base}/api/notify/{موضوع}‎  با ‎{title, message, tags}‎
///     گرفتن   : وب‌سوکتِ ‎{base}/notify?topic=الف,ب‎
///
/// و همان قاعدهٔ نسخهٔ وب: **نامِ فرستنده در ‎title‎ می‌نشیند و شناسهٔ گفت‌وگو
/// در ‎tags‎** — چون سرور فیلدِ دلخواه پاس نمی‌دهد. این را عوض نکنید، وگرنه
/// پیامِ برنامه و پیامِ کیو‌آرِ همان حساب در دو گفت‌وگوی جدا می‌افتند.
/// </summary>
public sealed class HomeServer
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(12) };

    private readonly Func<string> _baseUrl;

    public HomeServer(Func<string> baseUrl) => _baseUrl = baseUrl;

    public bool Configured => Clean("http").Length > 0;

    /// <summary>نشانیِ پاک — ‎http‎ برای فرستادن، ‎ws‎ برای گوش دادن.</summary>
    private string Clean(string scheme)
    {
        var b = (_baseUrl() ?? "").Trim().TrimEnd('/');
        if (b.Length == 0) return "";
        if (b.StartsWith("http://", StringComparison.OrdinalIgnoreCase)) b = b[7..];
        else if (b.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) b = b[8..];
        else if (b.StartsWith("wss://", StringComparison.OrdinalIgnoreCase)) b = b[6..];
        else if (b.StartsWith("ws://", StringComparison.OrdinalIgnoreCase)) b = b[5..];
        return (scheme == "ws" ? "wss://" : "https://") + b;
    }

    /// <summary>فرستادنِ یک پیام. ‎false‎ یعنی نرفت (و باید در صف بماند).</summary>
    public async Task<bool> SendAsync(string topic, string sender, string text,
                                      IEnumerable<string>? tags = null,
                                      CancellationToken ct = default)
    {
        var b = Clean("http");
        if (b.Length == 0 || string.IsNullOrWhiteSpace(topic)) return false;
        try
        {
            var body = new
            {
                title = sender,
                message = text ?? "",
                priority = 3,
                tags = tags?.ToArray(),
            };
            var res = await Http.PostAsJsonAsync(
                b + "/api/notify/" + Uri.EscapeDataString(topic), body, ct);
            return res.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    /// <summary>
    /// گوش دادن به چند موضوع. تا وقتی ‎ct‎ لغو نشده خودش دوباره وصل می‌شود.
    ///
    /// ⚠️ قطعیِ شبکه خطا نیست، حالتِ عادیِ یک سرورِ خانگی است: هر بار با
    /// مکثِ کوتاه دوباره وصل می‌شود و هیچ استثنایی بیرون نمی‌دهد.
    /// </summary>
    public async Task ListenAsync(IReadOnlyList<string> topics,
                                  Action<HomeMessage> onMessage,
                                  CancellationToken ct)
    {
        var b = Clean("ws");
        if (b.Length == 0 || topics.Count == 0) return;
        var url = new Uri(b + "/notify?topic=" + Uri.EscapeDataString(string.Join(",", topics)));

        var buf = new byte[64 * 1024];
        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var ws = new ClientWebSocket();
                await ws.ConnectAsync(url, ct);

                while (ws.State == WebSocketState.Open && !ct.IsCancellationRequested)
                {
                    var got = await ws.ReceiveAsync(new ArraySegment<byte>(buf), ct);
                    if (got.MessageType == WebSocketMessageType.Close) break;
                    if (got.Count == 0) continue;

                    var text = Encoding.UTF8.GetString(buf, 0, got.Count);
                    if (Parse(text) is { } m) onMessage(m);
                }
            }
            catch (OperationCanceledException) { return; }
            catch { /* قطعِ شبکه — پایین دوباره وصل می‌شود */ }

            try { await Task.Delay(3000, ct); } catch { return; }
        }
    }

    /// <summary>خواندنِ یک پیامِ سرور. شکلِ ناشناس ⇒ ‎null‎، نه استثنا.</summary>
    public static HomeMessage? Parse(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var r = doc.RootElement;
            if (r.ValueKind != JsonValueKind.Object) return null;

            string S(string name) =>
                r.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
                    ? v.GetString() ?? "" : "";

            var msg = S("message");
            if (msg.Length == 0) return null;

            var tags = new List<string>();
            if (r.TryGetProperty("tags", out var t) && t.ValueKind == JsonValueKind.Array)
                foreach (var x in t.EnumerateArray())
                    if (x.ValueKind == JsonValueKind.String) tags.Add(x.GetString() ?? "");

            long at = 0;
            foreach (var name in new[] { "time", "at", "id" })
                if (r.TryGetProperty(name, out var v) && v.TryGetInt64(out var n)) { at = n; break; }

            return new HomeMessage(S("topic"), S("title"), msg, tags, at);
        }
        catch { return null; }
    }

    /// <summary>
    /// پیام روی کدام گفت‌وگو می‌نشیند — همان قاعدهٔ ‎_msgrThreadOf‎ی نسخهٔ وب:
    /// برچسبِ ‎p-…‎ حرفِ آخر را می‌زند، وگرنه خودِ موضوع، وگرنه ‎staff‎.
    /// </summary>
    public static string ThreadOf(HomeMessage m)
    {
        foreach (var raw in m.Tags)
        {
            var t = (raw ?? "").Trim().ToLowerInvariant();
            if (t.Length > 2 && t.StartsWith("p-", StringComparison.Ordinal)) return t;
        }
        return m.Topic.Length > 0 ? m.Topic : "staff";
    }
}
