using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace PumpYaqobi.App.Services;

/// <summary>نتیجهٔ ورود با گوگل.</summary>
/// <param name="Ok">آیا کاربر تا آخر رفت.</param>
/// <param name="IdToken">توکنِ هویتِ گوگل — همان که سرور می‌سنجد.</param>
/// <param name="Why">اگر نشد، چرا — به فارسی.</param>
public sealed record GoogleResult(bool Ok, string IdToken = "", string Why = "")
{
    public static GoogleResult No(string why) => new(false, "", why);
}

/// <summary>
/// ══ ورود با گوگل در برنامهٔ کامپیوتر ═══════════════════════════════════════
///
/// خواستهٔ صریحِ صاحب ریپو: «صفحهٔ لاگین با جیمیل هم داخلِ اپ نیست… مثلِ
/// برنامهٔ شاپ باشد که بی اینکه من رمز یا چیزی بزنم، اطلاعات از حسابش به
/// سرور بیاید.»
///
/// ── چرا این‌طوری و نه یک کادرِ کاربر/رمز ──────────────────────────────────
/// گوگل برای برنامهٔ نصبی فقط یک راه را می‌پذیرد و بقیه را رد می‌کند:
/// مرورگرِ خودِ سیستم باز می‌شود، کاربر آن‌جا وارد می‌شود، و گوگل نتیجه را
/// به یک نشانیِ <c>127.0.0.1</c> برمی‌گرداند که خودِ برنامه چند ثانیه به آن
/// گوش می‌دهد. هرگز رمزِ گوگل داخلِ برنامه تایپ نمی‌شود — اصلاً برنامه
/// رمز را نمی‌بیند.
///
/// ⚠️ <b>PKCE</b> اجباری است و رمزِ کلاینت (<c>client_secret</c>) در کار
/// نیست: برنامه‌ای که روی کامپیوترِ مردم نصب می‌شود هیچ رازی نمی‌تواند نگه
/// دارد — هر کسی فایلش را باز می‌کند و رمز را برمی‌دارد. به‌جایش هر بار یک
/// رشتهٔ تصادفی ساخته می‌شود و فقط همان یک بار به درد می‌خورد.
///
/// ⚠️ <b>پورت ثابت نیست.</b> پورتِ آزاد از سیستم گرفته می‌شود، چون پورتِ
/// ثابت روی کامپیوترِ کاربر ممکن است گرفته باشد. گوگل برای نوعِ Desktop
/// هر پورتِ لوکال‌هاست را می‌پذیرد، پس این مشکلی نمی‌سازد.
///
/// ⚠️ <b>شناسهٔ کلاینت از سرور می‌آید</b>، نه از داخلِ کد — دقیقاً مثلِ اپِ
/// کارمندان. عوض شدنش نباید نسخهٔ تازهٔ برنامه بخواهد.
/// </summary>
public static class GoogleSignIn
{
    private const string Auth  = "https://accounts.google.com/o/oauth2/v2/auth";
    private const string Token = "https://oauth2.googleapis.com/token";

    /// <summary>چقدر منتظرِ کاربر بمانیم — دو دقیقه، بعد بی‌خیال.</summary>
    private static readonly TimeSpan Patience = TimeSpan.FromMinutes(2);

    /// <summary>
    /// مرورگر را باز می‌کند و منتظر می‌ماند تا کاربر وارد شود.
    /// </summary>
    /// <param name="clientId">شناسهٔ Desktop که سرور داده.</param>
    /// <param name="open">باز کردنِ مرورگر — تزریق‌پذیر تا آزمون مرورگر باز نکند.</param>
    public static async Task<GoogleResult> RunAsync(
        string clientId, Action<string>? open = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(clientId))
            return GoogleResult.No("ورود با گوگل روی این سرور روشن نیست");

        var verifier  = Rand(64);
        var challenge = B64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        var state     = Rand(24);

        HttpListener listener;
        string redirect;
        try
        {
            (listener, redirect) = Listen();
        }
        catch (Exception ex)
        {
            CrashGuard.Write("ورود با گوگل", ex, report: false);
            return GoogleResult.No("درِ ورود روی این کامپیوتر باز نشد — " + ErrorText.Friendly(ex));
        }

        try
        {
            var url = $"{Auth}?client_id={Uri.EscapeDataString(clientId)}"
                    + $"&redirect_uri={Uri.EscapeDataString(redirect)}"
                    + "&response_type=code&scope=openid%20email%20profile"
                    + $"&code_challenge={challenge}&code_challenge_method=S256"
                    + $"&state={state}";

            if (open is not null) open(url); else Browser(url);

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(Patience);

            var code = await WaitForCode(listener, state, timeout.Token);
            if (!code.Ok) return GoogleResult.No(code.Why);

            return await Exchange(clientId, code.Value, verifier, redirect, ct);
        }
        catch (OperationCanceledException)
        {
            return GoogleResult.No("ورود نیمه‌کاره ماند");
        }
        finally
        {
            try { listener.Stop(); } catch { }
        }
    }

    // ── درِ برگشت ───────────────────────────────────────────────────────

    /// <summary>یک پورتِ آزاد می‌گیرد و رویش گوش می‌دهد.</summary>
    private static (HttpListener, string) Listen()
    {
        //  پورت را از خودِ سیستم می‌گیریم: پورتِ ثابت ممکن است گرفته باشد.
        var probe = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();

        var redirect = $"http://127.0.0.1:{port}/";
        var listener = new HttpListener();
        listener.Prefixes.Add(redirect);
        listener.Start();
        return (listener, redirect);
    }

    private static async Task<(bool Ok, string Value, string Why)> WaitForCode(
        HttpListener listener, string state, CancellationToken ct)
    {
        using var reg = ct.Register(() => { try { listener.Stop(); } catch { } });

        HttpListenerContext ctx;
        try { ctx = await listener.GetContextAsync(); }
        catch { return (false, "", ct.IsCancellationRequested ? "ورود به‌موقع تمام نشد" : "درِ ورود بسته شد"); }

        var q     = ctx.Request.QueryString;
        var code  = q["code"]  ?? "";
        var back  = q["state"] ?? "";
        var error = q["error"] ?? "";

        var ok = error.Length == 0 && code.Length > 0 && Fixed(back, state);
        await Reply(ctx, ok);

        if (error.Length > 0) return (false, "", "گوگل اجازه نداد: " + error);
        if (code.Length == 0) return (false, "", "گوگل چیزی برنگرداند");
        //  ⚠️ اگر state نخواند یعنی این پاسخ مالِ درخواستِ ما نیست.
        if (!Fixed(back, state)) return (false, "", "پاسخِ ورود معتبر نیست");
        return (true, code, "");
    }

    /// <summary>صفحه‌ای که کاربر در مرورگر می‌بیند — و بعد می‌بندد.</summary>
    private static async Task Reply(HttpListenerContext ctx, bool ok)
    {
        var msg  = ok ? "وارد شدید ✅" : "ورود انجام نشد";
        var note = ok ? "می‌توانید این صفحه را ببندید و به برنامه برگردید."
                      : "به برنامه برگردید و دوباره امتحان کنید.";
        var html = "<!doctype html><html lang=\"fa\" dir=\"rtl\"><meta charset=\"utf-8\">"
                 + "<title>پمپ یعقوبی</title><body style=\"margin:0;display:grid;place-items:center;"
                 + "height:100vh;font:16px system-ui,Segoe UI,sans-serif;background:#0f172a;color:#e2e8f0\">"
                 + $"<div style=\"text-align:center\"><h1 style=\"margin:0 0 8px\">{msg}</h1>"
                 + $"<p style=\"margin:0;opacity:.7\">{note}</p></div></body></html>";

        var bytes = Encoding.UTF8.GetBytes(html);
        ctx.Response.ContentType = "text/html; charset=utf-8";
        ctx.Response.ContentLength64 = bytes.Length;
        try
        {
            await ctx.Response.OutputStream.WriteAsync(bytes);
            ctx.Response.Close();
        }
        catch { }
    }

    // ── تبدیلِ «کد» به «توکنِ هویت» ─────────────────────────────────────

    private static async Task<GoogleResult> Exchange(
        string clientId, string code, string verifier, string redirect, CancellationToken ct)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        using var body = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"]     = clientId,
            ["code"]          = code,
            ["code_verifier"] = verifier,
            ["grant_type"]    = "authorization_code",
            ["redirect_uri"]  = redirect,
        });

        try
        {
            using var res = await http.PostAsync(Token, body, ct);
            var text = await res.Content.ReadAsStringAsync(ct);
            if (!res.IsSuccessStatusCode) return GoogleResult.No("گوگل توکن نداد");

            using var doc = JsonDocument.Parse(text);
            var id = doc.RootElement.TryGetProperty("id_token", out var v) ? v.GetString() ?? "" : "";
            return id.Length > 0 ? new GoogleResult(true, id) : GoogleResult.No("توکنِ هویت نیامد");
        }
        catch (OperationCanceledException) { throw; }
        catch { return GoogleResult.No("به گوگل نرسیدیم — اینترنت را ببینید"); }
    }

    // ── ریزه‌کاری‌ها ────────────────────────────────────────────────────

    /// <summary>مرورگرِ خودِ سیستم — نه هیچ پنجرهٔ داخلی.</summary>
    private static void Browser(string url)
    {
        //  ⛔ فقط نشانیِ وب (`SafeOpen`)؛ اگر باز نشد، پیامِ خطا از مسیرِ انتظار می‌آید.
        SafeOpen.Url(url);
    }

    private static string Rand(int bytes) => B64Url(RandomNumberGenerator.GetBytes(bytes));

    private static string B64Url(byte[] raw) =>
        Convert.ToBase64String(raw).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    /// <summary>مقایسهٔ زمان‌ثابت — <c>state</c> یک راز است.</summary>
    private static bool Fixed(string a, string b)
    {
        var x = Encoding.UTF8.GetBytes(a);
        var y = Encoding.UTF8.GetBytes(b);
        return x.Length == y.Length && CryptographicOperations.FixedTimeEquals(x, y);
    }
}
