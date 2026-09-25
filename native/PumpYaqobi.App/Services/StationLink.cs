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
        //  ⛔ **هیچ کدِ پیش‌فرضِ مشترکی نیست** (۱۴۰۵/۰۷/۱۳، «pump1 را حذف کن و
        //  برای هر حساب یک ایدی باشد»). کد از <see cref="CodeFor"/> می‌آید:
        //  شناسهٔ پمپِ همین حساب روی سرورِ حساب، و بی حساب شناسهٔ همین کامپیوتر.
        var saved = (file.StationCode ?? "").Trim();
        var code = CodeFor(file);
        var name = HomeLink.StationName(host);
        var url = HomeLink.Url(host);
        var token = HomeLink.Token(host);
        var readKey = HomeLink.ReadKey(host);
        //  ⛔ پوشهٔ فعلی مالِ این حساب نیست (‎pump1‎ی کهنه، حسابِ دیگر، یا نصبِ
        //  بی‌حسابی که حالا حساب دارد) ⇒ **جابه‌جایی**. رمزِ پوشهٔ قبلی مالِ همان
        //  پوشه است و فرستادنش برای کدِ تازه معنا ندارد. تا ثبتِ تازه نشسته،
        //  پوشهٔ قبلی و رمزش دست نمی‌خورند — نرسیدن به سرور اتصال را نمی‌بُرد.
        var moving = token.Length > 0 && !Same(saved, code);
        if (moving) { token = ""; readKey = ""; }

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

        //  ⛔ کد گرفته شده و ما هیچ رمزی نداریم ⇒ **هیچ‌وقت گیر نمی‌کنیم**
        //  (۱۴۰۵/۰۷/۱۳، «آسان وصل شود»). دو پله، هر کدام یک بار:
        //   ۱) کدِ همین حساب (اگر کدِ دیگری خواسته شده بود).
        //   ۲) کدِ حساب هم گرفته است (نصبِ دیگرِ همین حساب، یا رمزی که با عوض
        //      کردنِ حساب پاک شد) ⇒ کدِ حساب + شناسهٔ همین کامپیوتر، و اگر آن هم
        //      گرفته بود یک پسوندِ تصادفی: پوشهٔ خودِ این نصب. ⚠️ رمزِ پوشهٔ دیگری گرفته نمی‌شود — سرور بی رمز
        //      پوشهٔ موجود را به کسی نمی‌دهد و نباید بدهد؛ برای **شریک** شدن در
        //      همان پوشه راهِ درست همچنان «کدِ جفت‌شدن»ِ پنل است.
        if (!result.Ok && result.Error == "already_taken" && token.Length == 0 && pin.Trim().Length == 0)
        {
            foreach (var alt in Alternatives(file, code))
            {
                var again = await EnrollAsync(url, alt, name, token, pin, ct);
                if (again.Ok) { code = alt; result = again; break; }
                if (again.Error != "already_taken") break;   // خطای دیگری است، نه برخوردِ کد
            }
        }

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
        Save(host, url, result.Token, result.ReadKey, code, found?.Id ?? "", moving);

        return new StationEnrollment(true, url, result.Code, result.Name, result.Created, "");
    }

    /// <summary>
    /// ══ کدِ هر پمپ = شناسهٔ همان حساب — ⛔ «pump1» دیگر نیست (۱۴۰۵/۰۷/۱۳) ═══
    ///
    /// خواستهٔ صریحِ صاحب ریپو: «این pump1 را حذف کن و جای آن برای هر حساب
    /// کاربری یک ایدی باشد تا قاطی نشود، و برای هر حساب، حسابِ خودشان بیاید.»
    ///
    /// سنجهٔ ‎livestack‎ ریشه را نشان داده بود: همهٔ نصب‌ها با یک کدِ پیش‌فرض
    /// (‎pump1‎) ثبت می‌شدند، پس نصبِ دوم برای همیشه ‎already_taken‎ می‌گرفت. و
    /// سه جا — پوشهٔ سرورِ خانگی، کدی که اپِ کارمندان از سرورِ حساب می‌گیرد
    /// (‎/api/pump/public/join‎ ⇒ ‎station.code‎)، و ‎s‎ی کیو‌آرِ زنده — هر کدام
    /// کدِ دیگری داشتند. حالا هر سه یکی‌اند: <b>کدِ پمپِ همین حساب روی سرورِ
    /// حساب</b> (<see cref="AppSettings.CloudStationCode"/>، مثلاً ‎p1a2b3c4d‎) —
    /// که خودِ سرورِ حساب یکتا ساخته و مالِ یک حساب است.
    ///
    /// ترتیب:
    ///   ۱) حساب داریم ⇒ کدِ همان حساب، و هر کدی که «مالِ این حساب» نیست
    ///      (‎pump1‎، کدِ حسابِ قبلی، کدِ بی‌حسابِ همین کامپیوتر) جابه‌جا می‌شود.
    ///   ۲) حساب نداریم ⇒ کدِ ذخیره‌شده، مگر همان ‎pump1‎ی مشترک.
    ///   ۳) هیچ ⇒ شناسهٔ همین کامپیوتر (<see cref="DeviceCode"/>) — تا دو نصبِ
    ///      بی‌حساب هم روی یک سرورِ خانگی قاطی نشوند.
    /// </summary>
    public static string CodeFor(AppSettings file)
    {
        var saved = (file.StationCode ?? "").Trim();
        var mine = UniqueCode(file);
        if (mine.Length > 0) return Belongs(saved, mine) ? saved : mine;
        if (saved.Length > 0 && !Same(saved, LegacySharedCode)) return saved;
        return DeviceCode(file);
    }

    /// <summary>
    /// ⛔ تنها جایی که نامِ آن کدِ کهنه نوشته می‌شود — فقط برای شناختنِ نصب‌هایی
    /// که هنوز روی آن مانده‌اند و باید جابه‌جا شوند. هیچ‌وقت به کسی داده نمی‌شود.
    /// </summary>
    internal const string LegacySharedCode = "pump1";

    /// <summary>کدِ همین حساب — خالی یعنی «هنوز حسابی بند نشده».</summary>
    public static string UniqueCode(AppSettings file)
    {
        //  ⚠️ اول کدِ خودِ سرورِ حساب (همان که اپِ کارمندان و کیو‌آر می‌شناسند)،
        //  و فقط اگر هنوز نیامده، شناسه‌اش — هر دو یکتا و مالِ یک حساب.
        var code = Clean(file.CloudStationCode);
        return code.Length > 0 ? code : Clean(file.CloudStationId);
    }

    /// <summary>شناسهٔ همین کامپیوتر — برای نصبی که هنوز حساب ندارد.</summary>
    public static string DeviceCode(AppSettings file)
    {
        var dev = new string(CloudConfig.DeviceUid(file).ToLowerInvariant()
                             .Where(ch => ch is (>= 'a' and <= 'z') or (>= '0' and <= '9')).ToArray());
        if (dev.Length == 0) dev = Guid.NewGuid().ToString("N");
        return "d-" + (dev.Length > 12 ? dev[^12..] : dev);
    }

    /// <summary>
    /// ⚠️ ‎true‎ یعنی پوشهٔ فعلیِ این نصب روی سرورِ خانگی مالِ این حساب نیست و
    /// باید جابه‌جا شود — حلقهٔ اتصال با همین از <see cref="EnsureAsync"/> می‌خواهد
    /// که پوشهٔ درست را بگیرد.
    /// </summary>
    public static bool NeedsMove(AppSettings file) =>
        (file.ServerToken ?? "").Trim().Length > 0
        && !Same((file.StationCode ?? "").Trim(), CodeFor(file));

    /// <summary>
    /// «این کد مالِ این حساب است؟» — خودِ کد، یا یکی از جایگزین‌های
    /// <see cref="Alternatives"/> که با همان شروع می‌شوند.
    /// </summary>
    private static bool Belongs(string code, string mine) =>
        code.Length > 0 && (Same(code, mine) || code.StartsWith(mine + "-", StringComparison.OrdinalIgnoreCase));

    private static bool Same(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    private static string Clean(string? raw)
    {
        var id = (raw ?? "").Trim().ToLowerInvariant();
        var safe = new string(id.Select(ch => ch is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '_' or '-' ? ch : '-').ToArray()).Trim('-');
        return safe.Length > 48 ? safe[..48] : safe;
    }

    /// <summary>کدهای جایگزین، به ترتیب — هیچ‌کدام برابرِ کدی که همین حالا رد شد نیست.</summary>
    public static IEnumerable<string> Alternatives(AppSettings file, string taken)
    {
        var mine = UniqueCode(file);
        var dev = new string(CloudConfig.DeviceUid(file).ToLowerInvariant()
                             .Where(ch => ch is (>= 'a' and <= 'z') or (>= '0' and <= '9')).ToArray());
        dev = dev.Length > 8 ? dev[^8..] : dev;
        if (dev.Length == 0) dev = Guid.NewGuid().ToString("N")[..8];
        var basis = mine.Length > 0 ? mine : DeviceCode(file);
        var list = new List<string>();
        if (mine.Length > 0) list.Add(mine);
        string Cut(string c) => c.Length > 48 ? c[..48] : c;
        list.Add(Cut(basis[..Math.Min(basis.Length, 39)] + "-" + dev));
        //  ⚠️ و سوم، تصادفی: همین کامپیوتر هم می‌تواند رمزِ پوشهٔ خودش را گم کرده
        //  باشد (عوض کردنِ حساب رمز را پاک می‌کند) — آن‌جا کدِ دومی هم گرفته است.
        list.Add(Cut(basis[..Math.Min(basis.Length, 39)] + "-" + Guid.NewGuid().ToString("N")[..8]));
        return list.Where(c => !string.Equals(c, taken, StringComparison.OrdinalIgnoreCase)).Distinct();
    }

    /// <summary>جوابِ خامِ مسیرِ ثبت.</summary>
    private sealed record EnrollReply(bool Ok, string Code, string Name, string Token, string ReadKey,
                                      bool Created, string Why, string Error = "");

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

            if (!res.IsSuccessStatusCode) return Failed(WhyOf(text, (int)res.StatusCode)) with { Error = ErrorOf(text) };

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
        catch (Exception e) { return Failed("به سرور نرسیدیم — " + ErrorText.Friendly(e)); }
    }

    private static EnrollReply Failed(string why) => new(false, "", "", "", "", false, why);

    private static string ErrorOf(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            return doc.RootElement.TryGetProperty("error", out var e) && e.ValueKind == JsonValueKind.String
                ? e.GetString() ?? "" : "";
        }
        catch { return ""; }
    }

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
    private static void Save(AppHost host, string url, string token, string readKey, string code,
                             string serverId, bool moved = false)
    {
        var file = AppSettings.Load();
        file.ServerUrl = url;
        file.ServerToken = token;
        //  ⚠️ جابه‌جایی ⇒ رمزِ خواندنِ پوشهٔ قبلی هم مالِ همان پوشه بود
        if (readKey.Length > 0 || moved) file.ServerReadKey = readKey;
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
