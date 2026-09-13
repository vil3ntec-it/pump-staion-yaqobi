using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace PumpYaqobi.App.Services;

/// <summary>
/// ══ پلِ برنامهٔ نیتیو به سرورِ خانگی ════════════════════════════════════════
///
/// تصمیمِ صاحب ریپو: «**برنامهٔ نیتیو منبعِ اصلی باشد** — هر تغییری که این‌جا
/// می‌شود در سایت و اپِ اندروید و شورت‌کاتِ آیفون هم دیده شود.»
///
/// راهش از قبل آماده بود و لازم نیست چیزِ تازه‌ای اختراع شود: سرورِ خانگیِ خودِ
/// صاحب ریپو (‎server/server.js‎) یک **پایگاهِ دادهٔ زندهٔ درختی روی وب‌سوکت**
/// است و نسخهٔ وب همین حالا از آن می‌خواند و در آن می‌نویسد. پس:
///
///     نیتیو  ──set──▶  stations/&lt;کد&gt;  ──on('value')──▶  سایت
///                                                     ├─▶ اپِ اندروید (همان سایت)
///                                                     └─▶ شورت‌کاتِ آیفون (همان سایت)
///
/// ⚠️ مسیر و شکلِ داده را از خودتان درنیاورید: نسخهٔ وب روی
/// ‎stations/&lt;کد ایستگاه&gt;‎ می‌نویسد و کلِ دادهٔ برنامه را یک‌جا ‎set‎ می‌کند،
/// با یک ‎_seq‎ی صعودی. اگر نیتیو جای دیگری بنویسد، سایت آن را نمی‌بیند.
///
/// پروتکل (از ‎server/server.js‎):
///     وصل شدن : ‎wss://&lt;میزبان&gt;/?token=&lt;رمز&gt;‎ ⇒ ‎{op:"connected"}‎
///     خواندن  : ‎{op:"get",    id, path}‎         ⇒ ‎{op:"result", id, value}‎
///     نوشتن   : ‎{op:"set",    id, path, value}‎  ⇒ ‎{op:"ack", id, ok}‎
///     وصله    : ‎{op:"update", id, path, value}‎
///     شنیدن   : ‎{op:"sub",    subId, path, event}‎
/// </summary>
public sealed class HomeSync : IAsyncDisposable
{
    private readonly Func<(string Url, string Token)> _config;
    private ClientWebSocket? _ws;
    private int _id;

    public HomeSync(Func<(string Url, string Token)> config) => _config = config;

    /// <summary>خالی بودنِ نشانی یعنی «فقط محلی» — و این خطا نیست.</summary>
    public bool Configured => _config().Url.Trim().Length > 0;

    public bool Connected => _ws is { State: WebSocketState.Open };

    /// <summary>وصل شدن. ‎false‎ یعنی نشد — بی استثنا، تا صدا زدنش هر جا امن باشد.</summary>
    public async Task<bool> ConnectAsync(CancellationToken ct = default)
    {
        if (Connected) return true;
        var (url, token) = _config();
        url = url.Trim();
        if (url.Length == 0) return false;

        // نشانی هر شکلی داده شده باشد، به ‎wss‎ی درست تبدیل می‌شود
        if (url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) url = "wss://" + url[8..];
        else if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)) url = "ws://" + url[7..];
        else if (!url.StartsWith("ws", StringComparison.OrdinalIgnoreCase)) url = "wss://" + url;
        url = url.TrimEnd('/');
        if (token.Trim().Length > 0) url += "/?token=" + Uri.EscapeDataString(token.Trim());

        try
        {
            var ws = new ClientWebSocket();
            await ws.ConnectAsync(new Uri(url), ct);

            // سرور اول ‎connected‎ می‌گوید (یا ‎error‎ی با ‎auth_failed‎)
            var hello = await ReadAsync(ws, ct);
            if (hello is null || Op(hello.Value) != "connected") { ws.Dispose(); return false; }

            _ws = ws;
            return true;
        }
        catch { return false; }
    }

    /// <summary>نوشتنِ یک شاخه. ‎false‎ یعنی نرفت.</summary>
    public Task<bool> SetAsync(string path, object value, CancellationToken ct = default)
        => WriteAsync("set", path, value, ct);

    /// <summary>وصلهٔ یک شاخه — فقط کلیدهای داده‌شده عوض می‌شوند.</summary>
    public Task<bool> UpdateAsync(string path, object value, CancellationToken ct = default)
        => WriteAsync("update", path, value, ct);

    /// <summary>خواندنِ یک شاخه. ‎null‎ یعنی نبود یا نشد.</summary>
    public async Task<JsonElement?> GetAsync(string path, CancellationToken ct = default)
    {
        if (!await ConnectAsync(ct) || _ws is null) return null;
        var id = ++_id;
        if (!await SendAsync(new { op = "get", id, path }, ct)) return null;

        var got = await ReadAsync(_ws, ct);
        if (got is null) return null;
        return got.Value.TryGetProperty("value", out var v) ? v.Clone() : null;
    }

    private async Task<bool> WriteAsync(string op, string path, object value, CancellationToken ct)
    {
        if (!await ConnectAsync(ct) || _ws is null) return false;
        var id = ++_id;
        if (!await SendAsync(new { op, id, path, value }, ct)) return false;

        var ack = await ReadAsync(_ws, ct);
        return ack is { } a && Op(a) == "ack"
            && (!a.TryGetProperty("ok", out var ok) || ok.ValueKind != JsonValueKind.False);
    }

    private async Task<bool> SendAsync(object msg, CancellationToken ct)
    {
        if (_ws is null) return false;
        try
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(msg);
            await _ws.SendAsync(bytes, WebSocketMessageType.Text, true, ct);
            return true;
        }
        catch { await DropAsync(); return false; }
    }

    /// <summary>
    /// یک پیامِ کامل. ⚠️ پیامِ بزرگ چندتکه می‌آید و باید تا ‎EndOfMessage‎
    /// جمع شود — وگرنه ‎JSON‎ی نصفه خوانده می‌شود و همه‌چیز بی‌صدا خراب.
    /// </summary>
    private async Task<JsonElement?> ReadAsync(ClientWebSocket ws, CancellationToken ct)
    {
        var buf = new byte[32 * 1024];
        var all = new MemoryStream();
        try
        {
            while (true)
            {
                var got = await ws.ReceiveAsync(new ArraySegment<byte>(buf), ct);
                if (got.MessageType == WebSocketMessageType.Close) { await DropAsync(); return null; }
                all.Write(buf, 0, got.Count);
                if (got.EndOfMessage) break;
            }
            using var doc = JsonDocument.Parse(Encoding.UTF8.GetString(all.ToArray()));
            return doc.RootElement.Clone();
        }
        catch { await DropAsync(); return null; }
    }

    private static string Op(JsonElement e) =>
        e.TryGetProperty("op", out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";

    private async Task DropAsync()
    {
        var ws = _ws;
        _ws = null;
        if (ws is null) return;
        try { await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None); }
        catch { }
        ws.Dispose();
    }

    public async ValueTask DisposeAsync() => await DropAsync();
}
