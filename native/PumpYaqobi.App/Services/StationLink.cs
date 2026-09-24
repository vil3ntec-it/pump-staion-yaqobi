using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using PumpYaqobi.Services.Data;

namespace PumpYaqobi.App.Services;

/// <summary>نتیجهٔ «سرور را پیدا کن و خودت را ثبت کن».</summary>
/// <param name="Ok">آیا حالا نشانی و رمز داریم.</param>
/// <param name="Url">نشانیِ سرور.</param>
/// <param name="Code">کدِ همین پمپ.</param>
/// <param name="Name">نامی که سرور برای این پمپ ثبت کرد.</param>
/// <param name="Created">پوشهٔ این پمپ همین حالا ساخته شد یا از قبل بود.</param>
/// <param name="Why">اگر نشد، چرا نشد — به فارسی، برای نشان دادن به کاربر.</param>
public sealed record StationEnrollment(bool Ok, string Url, string Code, string Name, bool Created, string Why);

/// <summary>
/// ══ «اگر آدرس نداشت، برایش بساز» ═══════════════════════════════════════════
///
/// خواستهٔ صریحِ صاحب ریپو. تا امروز برنامه تا وقتی کسی دستی نشانیِ سرور را
/// در تنظیمات نمی‌نوشت، هیچ داده‌ای منتشر نمی‌کرد — و از آن بدتر، آی‌پیِ سرورِ
/// خانگی با هر بار روشن شدنِ مودم عوض می‌شد و همان نشانیِ دستی بی‌صدا از کار
/// می‌افتاد. نتیجه‌اش این بود که گوشیِ کارمند دادهٔ دیروز را نشان می‌داد و
/// هیچ‌کس نمی‌فهمید چرا.
///
/// حالا سه گام، همه‌شان بی این‌که کاربر چیزی تایپ کند:
///
///   ۱) سرور را در شبکهٔ خانگی پیدا کن     (<see cref="ServerFinder"/>)
///   ۲) خودت را ثبت کن و رمزِ همین پمپ را بگیر
///        ‎POST {سرور}/api/stations/enroll  {code, name, token?, pin?}‎
///   ۳) هر دو جای تنظیمات را بنویس        (<see cref="HomeLink"/>)
///
/// ── سه قاعده ───────────────────────────────────────────────────────────────
///
///  • <b>رمزی که داریم را از دست نمی‌دهیم.</b> اگر از قبل رمز داشته باشیم،
///    همان را در ‎enroll‎ می‌فرستیم؛ سرور همان پوشه و همان رمز را پس می‌دهد.
///    پس نصبِ دوبارهٔ برنامه، دادهٔ پمپ را گم نمی‌کند.
///
///  • <b>پمپِ اشتباهی دزدیده نمی‌شود.</b> اگر کدِ پمپ دستِ کسِ دیگری باشد،
///    سرور ‎409‎ می‌دهد و ما رمزِ خودمان را دور نمی‌ریزیم.
///
///  • <b>هیچ استثنایی بیرون نمی‌دهد.</b> سرورِ خاموش، شبکهٔ قطع و فایروال
///    همه یعنی «نشد»، نه «خطا».
/// </summary>
public static class StationLink
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(12) };

    /// <summary>نشانیِ ‎http(s)‎ی تمیز — خالی یعنی نشانی نداریم.</summary>
    public static string HttpBase(string url)
    {
        var u = (url ?? "").Trim().TrimEnd('/');
        if (u.Length == 0) return "";
        if (u.StartsWith("wss://", StringComparison.OrdinalIgnoreCase)) return "https://" + u[6..];
        if (u.StartsWith("ws://", StringComparison.OrdinalIgnoreCase)) return "http://" + u[5..];
        if (u.StartsWith("http://", StringComparison.OrdinalIgnoreCase)) return u;
        if (u.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) return u;
        return "https://" + u;
    }

    /// <summary>
    /// نشانیِ ‎GET‎ی سادهٔ عکسِ زنده — همان چیزی که شورت‌کاتِ آیفون و هر
    /// کلاینتِ بی‌وب‌سوکتی می‌خواهد. ‎null‎ یعنی هنوز چیزی برای ساختن نیست.
    /// </summary>
    public static string? LiveUrl(string serverUrl, string code, string readKey)
    {
        var b = HttpBase(serverUrl);
        var k = (readKey ?? "").Trim();
        var c = (code ?? "").Trim();
        if (b.Length == 0 || k.Length == 0 || c.Length == 0) return null;
        return $"{b}/api/stations/{Uri.EscapeDataString(c)}/live?token={Uri.EscapeDataString(k)}";
    }

    /// <summary>
    /// نشانی و رمزِ همین پمپ را آماده می‌کند و برمی‌گرداند.
    ///
    /// اگر از قبل همه‌چیز هست و <paramref name="force"/> خالی است، هیچ
    /// درخواستی به شبکه نمی‌زند.
    /// </summary>
    /// <param name="pin">کدِ شش‌رقمیِ جفت‌شدنِ پنل — برای سروری که در شبکهٔ خانگی نیست.</param>
    /// <param name="force">حتی اگر همه‌چیز هست، دوباره با سرور چک کن.</param>
    public static async Task<StationEnrollment> EnsureAsync(
        AppHost host, string pin = "", bool force = false, CancellationToken ct = default)
    {
        var file = AppSettings.Load();
        var code = HomeLink.StationCode(host);
        var name = HomeLink.StationName(host);
        var url = HomeLink.Url(host);
        var token = HomeLink.Token(host);
        var readKey = HomeLink.ReadKey(host);

        var complete = url.Length > 0 && token.Length > 0 && readKey.Length > 0;
        if (complete && !force && pin.Trim().Length == 0)
            return new StationEnrollment(true, url, code, name, false, "");

        if (!file.AutoEnroll && url.Length == 0)
            return new StationEnrollment(false, "", code, name, false,
                "ثبتِ خودکار خاموش است و نشانیِ سرور هم نوشته نشده.");

        // ── گام ۱: نشانی. نداریم؟ در شبکه پیدایش کن ─────────────────────────
        var found = (FoundServer?)null;
        if (url.Length == 0)
        {
            found = await ServerFinder.FindFirstAsync(ct: ct);
            if (found is null)
                return new StationEnrollment(false, "", code, name, false,
                    "سرورِ خانگی در این شبکه پیدا نشد. مطمئن شوید سرور روشن است و هر دو روی همان وای‌فای‌اند.");
            url = found.Url;
        }

        // ── گام ۲: ثبت ──────────────────────────────────────────────────────
        var result = await EnrollAsync(url, code, name, token, pin, ct);

        // نشانیِ دستیِ کهنه — شاید سرور جابه‌جا شده. یک‌بار در شبکه بگرد.
        if (!result.Ok && found is null && file.AutoEnroll)
        {
            var moved = await ServerFinder.FindFirstAsync(ct: ct);
            if (moved is not null && !string.Equals(moved.Url, url, StringComparison.OrdinalIgnoreCase))
            {
                found = moved;
                var retry = await EnrollAsync(moved.Url, code, name, token, pin, ct);
                if (retry.Ok) { url = moved.Url; result = retry; }
            }
        }

        if (!result.Ok) return new StationEnrollment(false, url, code, name, false, result.Why);

        // ── گام ۳: هر دو جای تنظیمات ────────────────────────────────────────
        Save(host, url, result.Token, result.ReadKey, code, found?.Id ?? "");

        return new StationEnrollment(true, url, result.Code, result.Name, result.Created, "");
    }

    /// <summary>جوابِ خامِ مسیرِ ثبت.</summary>
    private sealed record EnrollReply(bool Ok, string Code, string Name, string Token, string ReadKey,
                                      bool Created, string Why);

    private static async Task<EnrollReply> EnrollAsync(
        string url, string code, string name, string token, string pin, CancellationToken ct)
    {
        var b = HttpBase(url);
        if (b.Length == 0) return Failed("نشانیِ سرور خوانده نشد.");

        try
        {
            var body = new
            {
                code,
                name,
                // ⚠️ رمزی که داریم را می‌فرستیم تا سرور «همین پمپ خودم است» را
                // بشناسد و همان پوشه و همان رمز را پس بدهد
                token = token.Length > 0 ? token : null,
                pin = pin.Trim().Length > 0 ? pin.Trim() : null,
            };

            var res = await Http.PostAsJsonAsync(b + "/api/stations/enroll", body, ct);
            var text = await res.Content.ReadAsStringAsync(ct);

            if (!res.IsSuccessStatusCode) return Failed(WhyOf(text, (int)res.StatusCode));

            using var doc = JsonDocument.Parse(text);
            var r = doc.RootElement;
            string S(string k) =>
                r.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
            var gotToken = S("token");
            if (gotToken.Length == 0) return Failed("سرور رمزی نداد.");

            return new EnrollReply(
                true,
                S("code").Length > 0 ? S("code") : code,
                S("name").Length > 0 ? S("name") : name,
                gotToken,
                S("readKey"),
                r.TryGetProperty("created", out var c) && c.ValueKind == JsonValueKind.True,
                "");
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception e) { return Failed("به سرور نرسیدیم: " + e.Message); }
    }

    private static EnrollReply Failed(string why) => new(false, "", "", "", "", false, why);

    /// <summary>خطای سرور، به زبانی که کاربر بفهمد چه کاری باید بکند.</summary>
    private static string WhyOf(string body, int status)
    {
        var error = "";
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("error", out var e) && e.ValueKind == JsonValueKind.String)
                error = e.GetString() ?? "";
        }
        catch { /* جوابِ غیرِ JSON */ }

        return error switch
        {
            "already_taken" =>
                "این کدِ پمپ روی سرور مالِ برنامهٔ دیگری است. یا کدِ پمپ را عوض کنید، یا از پنلِ سرور «کدِ جفت‌شدن» بگیرید.",
            "bad_pin" => "کدِ جفت‌شدن درست نیست یا وقتش گذشته. از پنلِ سرور یکی تازه بگیرید.",
            "enroll_lan_only" =>
                "سرور اجازهٔ ثبتِ پمپِ تازه را فقط از شبکهٔ خانگی می‌دهد. از پنلِ سرور «کدِ جفت‌شدن» بگیرید.",
            "enroll_closed" => "ثبتِ پمپِ تازه روی سرور بسته است. پمپ را از خودِ پنلِ سرور بسازید.",
            "stations_disabled" => "بخشِ پمپ‌بنزین‌ها روی سرور خاموش است.",
            "not_found" => "این مسیر روی سرور نیست — سرور را به نسخهٔ تازه‌تر به‌روز کنید.",
            _ => $"سرور جواب نداد (کد {status}).",
        };
    }

    /// <summary>
    /// نوشتن در <b>هر دو</b> جا.
    ///
    /// ⚠️ فایل همیشه نوشته می‌شود و دیتابیس فقط اگر اجازه‌اش باشد: ثبت معمولاً
    /// پیش از ورودِ کاربر انجام می‌شود و آن‌جا نوشتن در تنظیماتِ دیتابیس
    /// استثنا می‌دهد. اگر فقط دیتابیس را می‌نوشتیم، ثبتِ خودکار هیچ‌وقت
    /// نمی‌نشست و برنامه هر بار از نو ثبت می‌شد.
    /// </summary>
    private static void Save(AppHost host, string url, string token, string readKey, string code, string serverId)
    {
        var file = AppSettings.Load();
        file.ServerUrl = url;
        file.ServerToken = token;
        if (readKey.Length > 0) file.ServerReadKey = readKey;
        file.StationCode = code;
        if (serverId.Length > 0) file.ServerId = serverId;
        file.Save();

        //  ⛔ **رمز فقط در تنظیماتِ رمزشده** — نه در دیتابیس. `pump.db` داخلِ
        //  هر پشتیبانی است که به سرور می‌رود (شرحش بالای `HomeLink.Token`).
        try { host.Settings.Set(SettingsService.ServerUrl, url); }
        catch { /* هنوز وارد نشده — فایل نوشته شد و همان کافی است */ }
        HomeLink.MigrateDbToken(host);
    }
}
