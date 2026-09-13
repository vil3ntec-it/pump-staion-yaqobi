using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace PumpYaqobi.App.Services;

/// <summary>نشانی و رمز و کدِ پمپ — هر سه با هم، چون هر سه با هم معنی دارند.</summary>
/// <param name="Url">نشانیِ سرورِ خانگی. خالی یعنی «فقط محلی».</param>
/// <param name="Token">رمزِ همین پمپ. خالی یعنی هنوز ثبت نشده.</param>
/// <param name="Station">کدِ همین پمپ بنزین.</param>
public sealed record HomeTarget(string Url, string Token, string Station);

/// <summary>از کدام در وصل شده‌ایم.</summary>
public enum HomeSyncMode
{
    /// <summary>هیچ — سرور تنظیم نشده یا در دسترس نیست.</summary>
    None,

    /// <summary>پوشهٔ اختصاصیِ همین پمپ روی سرور (‎/station‎). راهِ درست.</summary>
    Station,

    /// <summary>دفترِ همه‌کارهٔ قدیمی. فقط برای سرورهایی که هنوز به‌روز نشده‌اند.</summary>
    Legacy,
}

/// <summary>
/// ══ پلِ برنامهٔ نیتیو به سرورِ خانگی ════════════════════════════════════════
///
/// تصمیمِ صاحب ریپو: «**برنامهٔ نیتیو منبعِ اصلی باشد** — هر تغییری که این‌جا
/// می‌شود در اپِ کارمندان و اپِ اندروید و شورت‌کاتِ آیفون هم دیده شود.»
///
///     نیتیو ──set live──▶ پوشهٔ همین پمپ ──sub live──▶ اپِ کارمندان
///           ◀─sub inbox──                ◀─post inbox─ اندروید · آیفون
///
/// ══ دو در، و چرا هر دو ═════════════════════════════════════════════════════
///
///  ۱) <see cref="HomeSyncMode.Station"/> — ‎wss://…/station?station=&lt;کد&gt;&amp;token=&lt;رمز&gt;‎
///     پوشه و رمزِ اختصاصیِ همین پمپ. مسیرها داخلِ همان پوشه‌اند: ‎live‎،
///     ‎inbox‎، ‎station‎. این راهِ درست است و فردا که پمپِ دوم اضافه شود،
///     تنها راهی است که دادهٔ دو پمپ را قاطی نمی‌کند.
///
///  ۲) <see cref="HomeSyncMode.Legacy"/> — ‎wss://…/?token=&lt;رمز&gt;‎ و نوشتن روی
///     ‎stations/&lt;کد&gt;-live‎. ⚠️ این را برنداریم: سرورِ خانگی‌ای که هنوز
///     به‌روز نشده فقط همین را بلد است، و روزی که کاربر برنامه را به‌روز
///     می‌کند ولی سرور را نه، بی این، همان روز از کار می‌افتاد.
///
/// پروتکل در هر دو حالت یکی است (‎homelab-panel/server‎):
///     وصل شدن : ⇒ ‎{op:"connected"}‎ (یا ‎{op:"error", msg}‎)
///     خواندن  : ‎{op:"get",    id, path}‎        ⇒ ‎{op:"result", id, value}‎
///     نوشتن   : ‎{op:"set",    id, path, value}‎ ⇒ ‎{op:"ack", id, ok}‎
///     وصله    : ‎{op:"update", id, path, value}‎
///     شنیدن   : ‎{op:"sub",    subId, path, event}‎ ⇒ ‎{op:"event", subId, …}‎
///
/// ⚠️ هیچ استثنایی بیرون نمی‌دهد. سرورِ خاموش و اینترنتِ قطع، حالتِ عادیِ یک
/// سرورِ خانگی‌اند، نه خطا.
/// </summary>
public sealed class HomeSync : IAsyncDisposable
{
    /// <summary>بیشتر از این منتظرِ جوابِ سرور نمی‌مانیم.</summary>
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(20);

    private readonly Func<HomeTarget> _config;

    /// <summary>یک نفر در یک زمان وصل می‌شود — وگرنه دو اتصالِ موازی می‌سازیم.</summary>
    private readonly SemaphoreSlim _connectGate = new(1, 1);

    /// <summary>‎ClientWebSocket‎ دو ‎SendAsync‎ی هم‌زمان را تحمل نمی‌کند.</summary>
    private readonly SemaphoreSlim _sendGate = new(1, 1);

    /// <summary>درخواست‌هایی که جوابشان نیامده: ‎id‎ ⇒ کسی که منتظر است.</summary>
    private readonly ConcurrentDictionary<int, TaskCompletionSource<JsonElement>> _pending = new();

    /// <summary>اشتراک‌های خواسته‌شده — با هر بار وصل شدنِ دوباره از نو فرستاده می‌شوند.</summary>
    private readonly ConcurrentDictionary<string, (string Path, Action<JsonElement> OnValue)> _subs = new();

    private ClientWebSocket? _ws;
    private CancellationTokenSource? _pump;
    private int _id;

    public HomeSync(Func<HomeTarget> config) => _config = config;

    /// <summary>خالی بودنِ نشانی یعنی «فقط محلی» — و این خطا نیست.</summary>
    public bool Configured => _config().Url.Trim().Length > 0;

    public bool Connected => _ws is { State: WebSocketState.Open };

    /// <summary>از کدام در وصل شده‌ایم — <see cref="StationPublisher"/> از همین می‌فهمد کجا بنویسد.</summary>
    public HomeSyncMode Mode { get; private set; } = HomeSyncMode.None;

    // ------------------------------ نشانی‌ها --------------------------------

    /// <summary>نشانی هر شکلی داده شده باشد، به ‎ws‎/‎wss‎ی درست تبدیل می‌شود.</summary>
    public static string WsBase(string url)
    {
        var u = (url ?? "").Trim();
        if (u.Length == 0) return "";
        if (u.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) u = "wss://" + u[8..];
        else if (u.StartsWith("http://", StringComparison.OrdinalIgnoreCase)) u = "ws://" + u[7..];
        else if (!u.StartsWith("ws", StringComparison.OrdinalIgnoreCase)) u = "wss://" + u;
        return u.TrimEnd('/');
    }

    /// <summary>درِ تازه — پوشهٔ اختصاصیِ همین پمپ.</summary>
    public static string StationUrl(string url, string station, string token)
    {
        var b = WsBase(url);
        if (b.Length == 0) return "";
        return b + "/station?station=" + Uri.EscapeDataString(station.Trim())
                 + "&token=" + Uri.EscapeDataString(token.Trim());
    }

    /// <summary>درِ قدیمی — دفترِ همه‌کارهٔ ‎site-sync‎.</summary>
    public static string LegacyUrl(string url, string token)
    {
        var b = WsBase(url);
        if (b.Length == 0) return "";
        var t = (token ?? "").Trim();
        return t.Length > 0 ? b + "/?token=" + Uri.EscapeDataString(t) : b;
    }

    // ------------------------------ وصل شدن --------------------------------

    /// <summary>وصل شدن. ‎false‎ یعنی نشد — بی استثنا، تا صدا زدنش هر جا امن باشد.</summary>
    public async Task<bool> ConnectAsync(CancellationToken ct = default)
    {
        if (Connected) return true;

        try { await _connectGate.WaitAsync(ct); }
        catch { return false; }

        try
        {
            if (Connected) return true;

            var target = _config();
            var url = (target.Url ?? "").Trim();
            if (url.Length == 0) { Mode = HomeSyncMode.None; return false; }

            var code = (target.Station ?? "").Trim();
            var token = (target.Token ?? "").Trim();

            var opened = HomeSyncMode.None;
            if (code.Length > 0 && token.Length > 0 && await TryOpenAsync(StationUrl(url, code, token), ct))
                opened = HomeSyncMode.Station;
            else if (await TryOpenAsync(LegacyUrl(url, token), ct))
                opened = HomeSyncMode.Legacy;

            Mode = opened;
            if (opened == HomeSyncMode.None) return false;

            StartPump();
            // اشتراک‌ها با قطعِ شبکه پاک می‌شوند؛ اتصالِ تازه باید همه را از نو بگیرد
            foreach (var (subId, sub) in _subs) await SendSubAsync(subId, sub.Path, ct);
            return true;
        }
        catch { return false; }
        finally { _connectGate.Release(); }
    }

    /// <summary>یک در را امتحان می‌کند. ‎true‎ یعنی سرور ‎connected‎ گفت.</summary>
    private async Task<bool> TryOpenAsync(string url, CancellationToken ct)
    {
        if (url.Length == 0) return false;
        ClientWebSocket? ws = null;
        try
        {
            ws = new ClientWebSocket();
            await ws.ConnectAsync(new Uri(url), ct);

            // سرور اول ‎connected‎ می‌گوید (یا ‎error‎ی با ‎auth_failed‎)
            var hello = await ReadOneAsync(ws, ct);
            if (hello is null || Op(hello.Value) != "connected")
            {
                ws.Dispose();
                return false;
            }
            _ws = ws;
            return true;
        }
        catch
        {
            ws?.Dispose();
            return false;
        }
    }

    // ------------------------------ خواندن ---------------------------------

    /// <summary>
    /// حلقهٔ خواندن. تنها جایی است که از سوکت می‌خواند — وگرنه جوابِ یک
    /// درخواست را کسِ دیگری برمی‌داشت و هر دو بی‌صدا خراب می‌شدند.
    /// </summary>
    private void StartPump()
    {
        var old = _pump;
        if (old is not null)
        {
            try { old.Cancel(); } catch { /* از قبل بسته */ }
            old.Dispose();
        }
        var cts = new CancellationTokenSource();
        _pump = cts;
        var ws = _ws;
        if (ws is null) return;
        _ = Task.Run(() => PumpAsync(ws, cts.Token), cts.Token);
    }

    private async Task PumpAsync(ClientWebSocket ws, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && ws.State == WebSocketState.Open)
        {
            JsonElement? msg;
            try { msg = await ReadOneAsync(ws, ct); }
            catch { break; }
            if (msg is null) break;
            Dispatch(msg.Value);
        }
        // اتصال مرد: هر کسی که منتظرِ جواب است باید همین حالا «نشد» بگیرد،
        // نه این‌که تا ابد منتظر بماند
        FailPending();
        if (ReferenceEquals(_ws, ws))
        {
            _ws = null;
            Mode = HomeSyncMode.None;
        }
        try { ws.Dispose(); } catch { /* بسته شده */ }
    }

    private void Dispatch(JsonElement m)
    {
        switch (Op(m))
        {
            case "ack":
            case "result":
                if (m.TryGetProperty("id", out var idEl) && idEl.TryGetInt32(out var id)
                    && _pending.TryRemove(id, out var waiter))
                    waiter.TrySetResult(m);
                break;

            case "event":
                if (m.TryGetProperty("subId", out var sidEl) && sidEl.ValueKind == JsonValueKind.String
                    && _subs.TryGetValue(sidEl.GetString() ?? "", out var sub))
                {
                    var value = m.TryGetProperty("value", out var v) ? v.Clone() : default;
                    // ⚠️ کارِ گیرنده نباید حلقهٔ خواندن را بخواباند یا بشکند
                    try { sub.OnValue(value); }
                    catch { /* اشتباهِ گیرنده، اتصال را نکشد */ }
                }
                break;
        }
    }

    private void FailPending()
    {
        foreach (var key in _pending.Keys)
            if (_pending.TryRemove(key, out var waiter)) waiter.TrySetResult(default);
    }

    /// <summary>
    /// یک پیامِ کامل. ⚠️ پیامِ بزرگ چندتکه می‌آید و باید تا ‎EndOfMessage‎
    /// جمع شود — وگرنه ‎JSON‎ی نصفه خوانده می‌شود و همه‌چیز بی‌صدا خراب.
    /// </summary>
    private static async Task<JsonElement?> ReadOneAsync(ClientWebSocket ws, CancellationToken ct)
    {
        var buf = new byte[32 * 1024];
        using var all = new MemoryStream();
        try
        {
            while (true)
            {
                var got = await ws.ReceiveAsync(new ArraySegment<byte>(buf), ct);
                if (got.MessageType == WebSocketMessageType.Close) return null;
                all.Write(buf, 0, got.Count);
                if (got.EndOfMessage) break;
            }
            using var doc = JsonDocument.Parse(Encoding.UTF8.GetString(all.ToArray()));
            return doc.RootElement.Clone();
        }
        catch { return null; }
    }

    // ------------------------------ نوشتن ----------------------------------

    /// <summary>نوشتنِ یک شاخه. ‎false‎ یعنی نرفت.</summary>
    public Task<bool> SetAsync(string path, object value, CancellationToken ct = default)
        => WriteAsync("set", path, value, ct);

    /// <summary>وصلهٔ یک شاخه — فقط کلیدهای داده‌شده عوض می‌شوند.</summary>
    public Task<bool> UpdateAsync(string path, object value, CancellationToken ct = default)
        => WriteAsync("update", path, value, ct);

    /// <summary>پاک کردنِ یک شاخه — مثلاً پیامی که خوانده شد.</summary>
    public async Task<bool> RemoveAsync(string path, CancellationToken ct = default)
    {
        if (!await ConnectAsync(ct)) return false;
        var id = NextId();
        var reply = await AskAsync(id, new { op = "remove", id, path }, ct);
        return IsOk(reply);
    }

    /// <summary>خواندنِ یک شاخه. ‎null‎ یعنی نبود یا نشد.</summary>
    public async Task<JsonElement?> GetAsync(string path, CancellationToken ct = default)
    {
        if (!await ConnectAsync(ct)) return null;
        var id = NextId();
        var reply = await AskAsync(id, new { op = "get", id, path }, ct);
        if (reply is null || Op(reply.Value) != "result") return null;
        return reply.Value.TryGetProperty("value", out var v) ? v.Clone() : null;
    }

    private async Task<bool> WriteAsync(string op, string path, object value, CancellationToken ct)
    {
        if (!await ConnectAsync(ct)) return false;
        var id = NextId();
        var reply = await AskAsync(id, new { op, id, path, value }, ct);
        return IsOk(reply);
    }

    /// <summary>‎ack‎ی بی ‎ok:false‎ یعنی رفت. ‎read_only‎ هم همین‌جا «نرفت» می‌شود.</summary>
    private static bool IsOk(JsonElement? reply) =>
        reply is { } a && Op(a) == "ack"
            && (!a.TryGetProperty("ok", out var ok) || ok.ValueKind != JsonValueKind.False);

    private int NextId() => Interlocked.Increment(ref _id);

    /// <summary>می‌فرستد و منتظرِ جوابِ همان ‎id‎ می‌ماند. ‎null‎ یعنی نشد.</summary>
    private async Task<JsonElement?> AskAsync(int id, object msg, CancellationToken ct)
    {
        var waiter = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[id] = waiter;
        try
        {
            if (!await SendAsync(msg, ct))
            {
                _pending.TryRemove(id, out _);
                return null;
            }

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(RequestTimeout);
            using (timeout.Token.Register(() => waiter.TrySetResult(default)))
            {
                var reply = await waiter.Task;
                // ‎default‎ یعنی اتصال مرد یا وقت تمام شد
                return reply.ValueKind == JsonValueKind.Undefined ? null : reply;
            }
        }
        catch
        {
            _pending.TryRemove(id, out _);
            return null;
        }
    }

    private async Task<bool> SendAsync(object msg, CancellationToken ct)
    {
        var ws = _ws;
        if (ws is null || ws.State != WebSocketState.Open) return false;
        try { await _sendGate.WaitAsync(ct); }
        catch { return false; }
        try
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(msg);
            await ws.SendAsync(bytes, WebSocketMessageType.Text, true, ct);
            return true;
        }
        catch { return false; }
        finally { _sendGate.Release(); }
    }

    // ------------------------------ شنیدن ----------------------------------

    /// <summary>
    /// گوش دادن به یک شاخه — راهِ برگشتِ داده.
    ///
    /// همان ‎subId‎ی تکراری، اشتراکِ قبلی را جایگزین می‌کند. اشتراک‌ها با هر
    /// قطعیِ شبکه از نو فرستاده می‌شوند، پس یک‌بار صدا زدن کافی است.
    /// </summary>
    public async Task<bool> SubscribeAsync(string subId, string path, Action<JsonElement> onValue,
                                           CancellationToken ct = default)
    {
        _subs[subId] = (path, onValue);
        if (!await ConnectAsync(ct)) return false;
        return await SendSubAsync(subId, path, ct);
    }

    private Task<bool> SendSubAsync(string subId, string path, CancellationToken ct) =>
        SendAsync(new { op = "sub", subId, path, @event = "value" }, ct);

    /// <summary>دیگر گوش نده.</summary>
    public async Task UnsubscribeAsync(string subId, CancellationToken ct = default)
    {
        _subs.TryRemove(subId, out _);
        await SendAsync(new { op = "unsub", subId }, ct);
    }

    // ------------------------------ خاموشی ---------------------------------

    private static string Op(JsonElement e) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty("op", out var v)
            && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";

    /// <summary>اتصال را می‌بندد. اشتراک‌های خواسته‌شده می‌مانند تا دفعهٔ بعد.</summary>
    public async Task DropAsync()
    {
        var pump = _pump;
        _pump = null;
        if (pump is not null)
        {
            await pump.CancelAsync();
            pump.Dispose();
        }

        var ws = _ws;
        _ws = null;
        Mode = HomeSyncMode.None;
        FailPending();
        if (ws is null) return;
        try { await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None); }
        catch { /* از قبل بسته بود */ }
        try { ws.Dispose(); } catch { /* بسته شده */ }
    }

    public async ValueTask DisposeAsync() => await DropAsync();
}
