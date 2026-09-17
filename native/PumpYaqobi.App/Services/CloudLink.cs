using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;

namespace PumpYaqobi.App.Services;

/// <summary>نتیجهٔ یک کارِ ابری — هرگز استثنا بیرون نمی‌دهد.</summary>
/// <param name="Ok">شد یا نشد.</param>
/// <param name="Why">اگر نشد، چرا — به فارسیِ خودِ سرور، برای نشان دادن به کاربر.</param>
/// <param name="Code">کدِ ماشینیِ خطا، اگر سرور داده باشد.</param>
/// <summary>یک پیامِ چتِ پشتیبانی، همان‌طور که ابر می‌دهد.</summary>
public sealed record CloudChatMessage(string Id, long Seq, string Acct, string From, string Name,
                                      string Kind, string Text, string? MediaId, long At, bool Deleted)
{
    public bool FromCustomer => From == "c";

    public static CloudChatMessage? Parse(JsonElement m, string acctFallback = "")
    {
        if (m.ValueKind != JsonValueKind.Object) return null;
        string S(string k) => m.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
        long N(string k) => m.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt64(out var n) ? n : 0;
        var id = S("id");
        if (id.Length == 0) return null;
        var acct = S("acct");
        var media = S("mediaId");
        return new CloudChatMessage(id, N("seq"), acct.Length > 0 ? acct : acctFallback, S("from"), S("name"),
            S("kind").Length > 0 ? S("kind") : "text", S("text"), media.Length > 0 ? media : null, N("at"),
            m.TryGetProperty("deleted", out var d) && d.ValueKind == JsonValueKind.True);
    }

    public static List<CloudChatMessage> ParseList(JsonElement json, string acctFallback = "")
    {
        var list = new List<CloudChatMessage>();
        if (json.ValueKind == JsonValueKind.Object && json.TryGetProperty("messages", out var arr)
            && arr.ValueKind == JsonValueKind.Array)
            foreach (var m in arr.EnumerateArray())
                if (Parse(m, acctFallback) is { } x) list.Add(x);
        return list;
    }
}

/// <summary>یک گفت‌وگوی پشتیبانی از دیدِ صاحبِ پمپ.</summary>
public sealed record CloudChatThread(string Acct, string Name, bool Blocked, int Unread, long UpdatedAt,
                                     CloudChatMessage? Last);

public sealed record CloudResult(bool Ok, string Why = "", string Code = "")
{
    public static readonly CloudResult Done = new(true);
    public static CloudResult No(string why, string code = "") => new(false, why, code);
}

/// <summary>وضعیتِ اشتراک، آن‌طور که برنامه نشان می‌دهد.</summary>
/// <param name="Active">اشتراک یا دورهٔ آزمایشی باز است.</param>
/// <param name="Source">subscription | trial | free | none</param>
/// <param name="PlanTitle">نامِ پلن.</param>
/// <param name="DaysLeft">روزهای مانده.</param>
/// <param name="EndsAt">پایان (میلی‌ثانیهٔ یونیکس).</param>
/// <param name="Features">بخش‌هایی که باز است.</param>
public sealed record PumpSubscription(
    bool Active, string Source, string PlanTitle, int DaysLeft, long EndsAt,
    IReadOnlyList<string> Features)
{
    public static readonly PumpSubscription None =
        new(false, "none", "", 0, 0, Array.Empty<string>());
}

/// <summary>
/// ══ ابر — «اشتراک از سرور می‌آید» ═══════════════════════════════════════════
///
/// خواستهٔ صریحِ صاحب ریپو: «اشتراک هم از سرور بره براش.»
///
/// ── راه ────────────────────────────────────────────────────────────────────
/// <code>
///   صاحبِ پمپ شش رقم را در تنظیمات می‌زند
///        │
///        ▼  POST /api/pump/device/activate
///   توکنِ دستگاه + مجوزِ امضاشده + کلیدِ عمومیِ سرور
///        │
///        ▼  هر بار بالا آمدن: /api/pump/device/me و /license
///   اشتراک تازه می‌شود، بی این‌که کسی چیزی بپرسد
/// </code>
///
/// نه حسابی، نه مرورگری، نه نشانی‌ای. نشانی در
/// <see cref="CloudConfig.BaseUrl"/> قفل است و از تنظیمات خوانده نمی‌شود.
///
/// ── سه قاعده ───────────────────────────────────────────────────────────────
///
///  • <b>هیچ استثنایی بیرون نمی‌دهد.</b> نبودِ اینترنت، سرورِ خاموش و فایروال
///    همه یعنی «نشد»، نه «خطا». برنامهٔ پمپ باید آفلاین هم باز شود.
///
///  • <b>کلیدِ عمومی فقط یک بار قفل می‌شود.</b> ⚠️ اگر سرور روزی کلیدِ
///    دیگری بدهد، <b>نمی‌پذیریم</b> — چون همان یعنی یا سرور عوض شده یا
///    کسی وسط نشسته. بی این قید، کلِ قفل با یک سرورِ ساختگی دور می‌خورد.
///
///  • <b>مجوز ذخیره می‌شود.</b> تصمیمِ آفلاین از روی همان گرفته می‌شود، و
///    <see cref="LicenseGuard"/> امضایش را می‌سنجد — نه یک پرچمِ ساده که
///    هر کسی در فایلِ تنظیمات عوضش کند.
/// </summary>
public sealed class CloudLink
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(20) };

    /// <summary>
    /// ⚠️ **تنها راهِ سنجش** — یک شنوندهٔ ساختگی به جای شبکه، تا سنجشِ
    /// `cloudlogin` بتواند «ثبت‌نام، ورود، و زدنِ کدِ شش‌رقمی» را واقعاً تا
    /// تهِ کار ببرد بی این‌که به سرورِ واقعیِ اشتراک دست بزند.
    ///
    /// ⛔ **این قفلِ نشانی را باز نمی‌کند**: نشانی همچنان
    /// <see cref="CloudConfig.BaseUrl"/> است و از تنظیمات یا محیط خوانده
    /// نمی‌شود. این فقط با یک خطِ **کد** مقدار می‌گیرد (و فقط در
    /// `PumpYaqobi.UiTests`)، پس کسی که فایلِ تنظیمات را عوض می‌کند از
    /// این راه هیچ کاری نمی‌تواند بکند. در برنامهٔ واقعی همیشه `null` است.
    /// </summary>
    public static Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>? TestTransport { get; set; }

    private readonly AppSettings _settings;
    private readonly Func<Task> _save;

    public CloudLink(AppSettings settings, Func<Task> save)
    {
        _settings = settings;
        _save = save;
    }

    /// <summary>آیا این برنامه از قبل فعال شده است.</summary>
    public bool Activated => !string.IsNullOrWhiteSpace(_settings.CloudDeviceToken);

    /// <summary>شناسهٔ این کامپیوتر.</summary>
    public string DeviceUid => CloudConfig.DeviceUid(_settings);

    /// <summary>آخرین وضعیتِ اشتراک که از سرور گرفته شده.</summary>
    public PumpSubscription Subscription { get; private set; } = PumpSubscription.None;

    /// <summary>
    /// حالِ اشتراک برای قفلِ سه چیزِ ابری — همان چیزی که صفحهٔ پروفایل
    /// نشان می‌دهد (<see cref="Entitlements"/>).
    /// </summary>
    public EntitlementState Entitlement => Entitlements.State(_settings, Subscription);

    /// <summary>سنجشِ مجوزِ ذخیره‌شده — همان چیزی که آفلاین هم کار می‌کند.</summary>
    public LicenseCheck Verify(long? nowMs = null) => LicenseGuard.Check(
        _settings.CloudLicense,
        _settings.CloudPublicKey,
        DeviceUid,
        _settings.CloudStationId,
        nowMs ?? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

    // ── فعال‌سازی ──────────────────────────────────────────────────────

    /// <summary>
    /// کدِ شش‌رقمی ⇒ پمپ، اشتراک، توکنِ دستگاه و مجوز.
    ///
    /// کدِ پمپ و نامش از تنظیماتِ خودِ برنامه می‌روند، پس کاربر جز همان شش
    /// رقم چیزی تایپ نمی‌کند.
    /// </summary>
    /// <param name="stationName">نامِ پمپ — تا ابر همان نامی را بشناسد که کاربر نوشته.</param>
    /// <param name="stationLocation">لوکیشنِ پمپ (نشانی) — همان کادرِ گامِ دومِ ثبت.</param>
    public async Task<CloudResult> ActivateAsync(string code, string stationName = "",
                                                 string stationLocation = "",
                                                 CancellationToken ct = default)
    {
        var clean = new string((code ?? "").Where(char.IsDigit).ToArray());
        if (clean.Length != 6) return CloudResult.No("کد باید شش رقم باشد");

        var body = new
        {
            code = clean,
            device = new
            {
                uid = DeviceUid,
                name = Environment.MachineName,
                platform = "windows",
            },
            station = new
            {
                code = _settings.StationCode ?? "",
                name = (stationName ?? "").Trim(),
                location = (stationLocation ?? "").Trim(),
            },
        };

        var (ok, json, why, errCode) = await PostAsync("/api/pump/device/activate", body, null, ct);
        if (!ok) return CloudResult.No(why, errCode);

        //  ⚠️ کلیدِ عمومی فقط همین یک بار قفل می‌شود. اگر از قبل کلیدی
        //  داریم و سرور کلیدِ دیگری داد، نمی‌پذیریم.
        var serverKey = Str(json, "publicKey");
        if (string.IsNullOrWhiteSpace(_settings.CloudPublicKey))
        {
            if (string.IsNullOrWhiteSpace(serverKey))
                return CloudResult.No("سرور کلیدِ عمومی نداد؛ فعال‌سازی نیمه‌کاره ماند");
            _settings.CloudPublicKey = serverKey;
        }
        else if (!string.IsNullOrWhiteSpace(serverKey) && serverKey != _settings.CloudPublicKey)
        {
            return CloudResult.No(
                "کلیدِ سرور با آن‌چه این برنامه قفل کرده فرق دارد. اگر سرور را واقعاً "
                + "عوض کرده‌اید، با پشتیبانی تماس بگیرید.", "key_mismatch");
        }

        _settings.CloudDeviceToken = Str(json, "deviceToken");
        _settings.CloudStationId = StationId(json);
        _settings.CloudLicense = Str(json, "license");
        _settings.CloudSyncedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        ReadSubscription(json);
        //  مُهرِ «دیدیم که باز است» — پایهٔ ارفاق (‎Entitlements.Grace‎). بی این،
        //  یک روزِ بی‌اینترنت می‌توانست کیو‌آر و اپِ کارمندانِ مشتریِ پول‌داده
        //  را خاموش کند.
        Entitlements.Remember(_settings, Subscription, Verify());
        await SaveQuiet();

        return CloudResult.Done;
    }

    /// <summary>تمدید با کدِ تازه — بی فعال‌سازیِ دوباره.</summary>
    public async Task<CloudResult> RedeemAsync(string code, string stationName = "",
                                               string stationLocation = "",
                                               CancellationToken ct = default)
    {
        if (!Activated) return await ActivateAsync(code, stationName, stationLocation, ct);

        var clean = new string((code ?? "").Where(char.IsDigit).ToArray());
        if (clean.Length != 6) return CloudResult.No("کد باید شش رقم باشد");

        var (ok, json, why, errCode) =
            await PostAsync("/api/pump/device/redeem", new { code = clean }, _settings.CloudDeviceToken, ct);
        if (!ok) return CloudResult.No(why, errCode);

        _settings.CloudLicense = Str(json, "license");
        _settings.CloudSyncedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        ReadSubscription(json);
        //  مُهرِ «دیدیم که باز است» — پایهٔ ارفاق (‎Entitlements.Grace‎). بی این،
        //  یک روزِ بی‌اینترنت می‌توانست کیو‌آر و اپِ کارمندانِ مشتریِ پول‌داده
        //  را خاموش کند.
        Entitlements.Remember(_settings, Subscription, Verify());
        await SaveQuiet();
        return CloudResult.Done;
    }

    // ── تازه‌سازی ──────────────────────────────────────────────────────

    /// <summary>
    /// وضعیتِ اشتراک و مجوزِ تازه.
    ///
    /// هر بار که برنامه بالا می‌آید صدا زده می‌شود. نشدنش اشکالی ندارد:
    /// مجوزِ ذخیره‌شده تا ده روز آفلاین کار می‌کند.
    /// </summary>
    public async Task<CloudResult> RefreshAsync(CancellationToken ct = default)
    {
        if (!Activated) return CloudResult.No("این برنامه هنوز فعال نشده است", "not_activated");

        var (ok, me, why, errCode) = await GetAsync("/api/pump/device/me", _settings.CloudDeviceToken, ct);
        if (!ok) return CloudResult.No(why, errCode);

        //  ⚠️ **شناسهٔ پمپ هم مثلِ کلیدِ عمومی قفل است.** پیش از این هر چه
        //  سرور می‌گفت بی سنجش روی تنظیمات می‌نشست؛ یعنی اگر روزی همین
        //  توکن به پمپِ دیگری می‌خورد، برنامه بی‌صدا پمپش را عوض می‌کرد و
        //  از آن به بعد عکسِ حساب‌های این پمپ به پوشهٔ پمپِ دیگری می‌رفت.
        //  `stn`ِ مجوز هم روی همین سنجیده می‌شود (`LicenseGuard`)، پس
        //  عوض شدنش یعنی «یا دستگاه جابه‌جا شده یا کسی وسط نشسته».
        var seen = StationId(me);
        if (seen.Length > 0 && _settings.CloudStationId.Length > 0
            && !string.Equals(seen, _settings.CloudStationId, StringComparison.Ordinal))
            return CloudResult.No(
                "این دستگاه روی پمپِ دیگری ثبت شده است. اگر واقعاً پمپ را عوض کرده‌اید، "
                + "دوباره با کدِ شش‌رقمیِ همان پمپ فعال کنید.", "station_mismatch");
        if (seen.Length > 0) _settings.CloudStationId = seen;
        ReadSubscription(me);

        //  مجوزِ تازه — جدا، چون ممکن است اشتراک تمام شده باشد و مجوزی
        //  صادر نشود. آن هم یک جوابِ درست است، نه خطا.
        var (licOk, lic, _, _) =
            await PostAsync("/api/pump/device/license", new { }, _settings.CloudDeviceToken, ct);
        if (licOk)
        {
            var token = Str(lic, "license");
            //  ⚠️ خالی بودن یعنی «اشتراک ندارد» — مجوزِ قبلی را پاک می‌کنیم،
            //  وگرنه تا ده روز با مجوزِ کهنه باز می‌ماند.
            _settings.CloudLicense = token;
            var key = Str(lic, "publicKey");
            if (!string.IsNullOrWhiteSpace(key) && string.IsNullOrWhiteSpace(_settings.CloudPublicKey))
                _settings.CloudPublicKey = key;
        }

        _settings.CloudSyncedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        //  مُهرِ «دیدیم که باز است» — پایهٔ ارفاق (‎Entitlements.Grace‎). بی این،
        //  یک روزِ بی‌اینترنت می‌توانست کیو‌آر و اپِ کارمندانِ مشتریِ پول‌داده
        //  را خاموش کند.
        Entitlements.Remember(_settings, Subscription, Verify());
        await SaveQuiet();
        return CloudResult.Done;
    }

    /// <summary>
    /// سپردنِ نشانی و رمزِ فقط‌خواندنیِ سرورِ خانگی به ابر.
    ///
    /// همان چیزی که اپِ کارمند را از پرسیدنِ آدرس بی‌نیاز می‌کند: آی‌پیِ
    /// خانگی با هر بار روشن شدنِ مودم عوض می‌شود، پس این باید پرتکرار و
    /// ارزان باشد.
    /// </summary>
    public async Task<CloudResult> PublishHomeAsync(string homeUrl, string readKey,
        CancellationToken ct = default)
    {
        if (!Activated) return CloudResult.No("فعال نشده", "not_activated");
        if (string.IsNullOrWhiteSpace(homeUrl)) return CloudResult.No("نشانیِ خانگی خالی است");

        var (ok, _, why, code) = await PostAsync("/api/pump/device/home",
            new { homeUrl, readKey = readKey ?? "" }, _settings.CloudDeviceToken, ct);
        return ok ? CloudResult.Done : CloudResult.No(why, code);
    }

    /// <summary>
    /// نوشتنِ یک فایل در پوشهٔ ابریِ همین پمپ — ‎PUT /api/pump/device/files/&lt;نام&gt;‎.
    /// برای کیو‌آرِ زنده (‎AcctLive‎). سرور خودش اشتراک را می‌سنجد؛ اگر تمام
    /// شده باشد ‎subscription_required‎ برمی‌گردد و این‌جا فقط ‎false‎ می‌شود.
    /// </summary>
    public async Task<CloudResult> PutFileAsync(string name, object data, CancellationToken ct = default)
    {
        if (!Activated) return CloudResult.No("فعال نشده", "not_activated");
        if (string.IsNullOrWhiteSpace(name)) return CloudResult.No("نامِ فایل خالی است");

        var (ok, _, why, code) = await PutAsync("/api/pump/device/files/" + Uri.EscapeDataString(name.Trim()),
            new { data }, _settings.CloudDeviceToken, ct);
        return ok ? CloudResult.Done : CloudResult.No(why, code);
    }

    // ── چتِ پشتیبانی — مشتریِ کیو‌آر ↔ صاحبِ پمپ ────────────────────────
    //
    // خواستهٔ صاحب ریپو: «داخلِ کیو‌آر یک چتِ پشتیبانی با من داشته باشد… عینِ
    // واتساپ.» مشتری با رمزِ حسابش روی ابر می‌نویسد؛ این‌ها همان درها از
    // سمتِ برنامه‌اند (‎/api/pump/device/chat/…‎ با توکنِ دستگاه).

    /// <summary>گفت‌وگوها با نخوانده‌ها و آخرین پیام.</summary>
    public async Task<(bool Ok, List<CloudChatThread> Threads, string Why)> ChatThreadsAsync(CancellationToken ct = default)
    {
        if (!Activated) return (false, new(), "فعال نشده");
        var (ok, json, why, _) = await GetAsync("/api/pump/device/chat/threads", _settings.CloudDeviceToken, ct);
        if (!ok) return (false, new(), why);
        var list = new List<CloudChatThread>();
        if (json.TryGetProperty("threads", out var arr) && arr.ValueKind == JsonValueKind.Array)
            foreach (var t in arr.EnumerateArray())
            {
                CloudChatMessage? last = t.TryGetProperty("last", out var l) && l.ValueKind == JsonValueKind.Object
                    ? CloudChatMessage.Parse(l, Str(t, "acct")) : null;
                list.Add(new CloudChatThread(Str(t, "acct"), Str(t, "name"),
                    t.TryGetProperty("blocked", out var b) && b.ValueKind == JsonValueKind.True,
                    (int)Num(t, "unread"), Num(t, "updatedAt"), last));
            }
        return (true, list, "");
    }

    /// <summary>همهٔ پیام‌های تازهٔ همهٔ گفت‌وگوها بعد از ‎after‎.</summary>
    public async Task<(bool Ok, List<CloudChatMessage> Messages, string Why)> ChatInboxAsync(long after, CancellationToken ct = default)
    {
        if (!Activated) return (false, new(), "فعال نشده");
        var (ok, json, why, _) = await GetAsync("/api/pump/device/chat/inbox?after=" + after, _settings.CloudDeviceToken, ct);
        if (!ok) return (false, new(), why);
        return (true, CloudChatMessage.ParseList(json), "");
    }

    /// <summary>پیامِ متنی یا رسانه‌ای به یک حساب. ‎kind‎ی خالی یعنی متن.</summary>
    public async Task<(bool Ok, CloudChatMessage? Message, string Why)> ChatSendAsync(
        string acct, string name, string text, string kind = "", string? mediaId = null, CancellationToken ct = default)
    {
        if (!Activated) return (false, null, "فعال نشده");
        object body = string.IsNullOrEmpty(kind)
            ? new { name, text }
            : new { name, kind, mediaId };
        var (ok, json, why, _) = await PostAsync("/api/pump/device/chat/" + Uri.EscapeDataString(acct), body, _settings.CloudDeviceToken, ct);
        if (!ok) return (false, null, why);
        return (true, json.TryGetProperty("message", out var m) ? CloudChatMessage.Parse(m, acct) : null, "");
    }

    /// <summary>بالا بردنِ عکس/ویدیو/صدا — خام، با نوعش. خروجی شناسهٔ رسانه.</summary>
    public async Task<(bool Ok, string MediaId, string Why)> ChatUploadAsync(
        string acct, byte[] bytes, string mime, CancellationToken ct = default)
    {
        if (!Activated) return (false, "", "فعال نشده");
        var req = new HttpRequestMessage(HttpMethod.Post,
            CloudConfig.BaseUrl + "/api/pump/device/chat/" + Uri.EscapeDataString(acct) + "/media")
        {
            Content = new ByteArrayContent(bytes),
        };
        req.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(mime);
        req.Headers.Add("Authorization", $"Bearer {_settings.CloudDeviceToken}");
        var (ok, json, why, _) = await Send(req, ct);
        return ok ? (true, Str(json, "mediaId"), "") : (false, "", why);
    }

    /// <summary>خودِ رسانه — بایت‌ها و نوعش. ‎null‎ یعنی نرسید.</summary>
    public async Task<(byte[] Bytes, string Mime)?> ChatMediaAsync(string mediaId, CancellationToken ct = default)
    {
        if (!Activated || string.IsNullOrWhiteSpace(mediaId)) return null;
        try
        {
            var req = new HttpRequestMessage(HttpMethod.Get,
                CloudConfig.BaseUrl + "/api/pump/device/chat/media/" + Uri.EscapeDataString(mediaId));
            req.Headers.Add("Authorization", $"Bearer {_settings.CloudDeviceToken}");
            using var res = TestTransport is null
                ? await Http.SendAsync(req, ct)
                : await TestTransport(req, ct);
            if (!res.IsSuccessStatusCode) return null;
            var bytes = await res.Content.ReadAsByteArrayAsync(ct);
            return (bytes, res.Content.Headers.ContentType?.MediaType ?? "application/octet-stream");
        }
        catch { return null; }
    }

    /// <summary>پاک کردنِ نرمِ یک پیام (هر پیامی — صاحبِ پمپ است).</summary>
    public async Task<CloudResult> ChatDeleteAsync(string messageId, CancellationToken ct = default)
    {
        if (!Activated) return CloudResult.No("فعال نشده", "not_activated");
        var req = new HttpRequestMessage(HttpMethod.Delete,
            CloudConfig.BaseUrl + "/api/pump/device/chat/message/" + Uri.EscapeDataString(messageId));
        req.Headers.Add("Authorization", $"Bearer {_settings.CloudDeviceToken}");
        var (ok, _, why, code) = await Send(req, ct);
        return ok ? CloudResult.Done : CloudResult.No(why, code);
    }

    /// <summary>بلاک / رفعِ بلاکِ یک مشتری.</summary>
    public async Task<CloudResult> ChatBlockAsync(string acct, bool blocked, CancellationToken ct = default)
    {
        if (!Activated) return CloudResult.No("فعال نشده", "not_activated");
        var req = new HttpRequestMessage(blocked ? HttpMethod.Post : HttpMethod.Delete,
            CloudConfig.BaseUrl + "/api/pump/device/chat/" + Uri.EscapeDataString(acct) + "/block")
        {
            Content = blocked ? JsonContent.Create(new { }) : null,
        };
        req.Headers.Add("Authorization", $"Bearer {_settings.CloudDeviceToken}");
        var (ok, _, why, code) = await Send(req, ct);
        return ok ? CloudResult.Done : CloudResult.No(why, code);
    }

    /// <summary>«تا این‌جا خواندم» — نخوانده‌های همان گفت‌وگو صفر می‌شود.</summary>
    public async Task<bool> ChatSeenAsync(string acct, long seq, CancellationToken ct = default)
    {
        if (!Activated) return false;
        var (ok, _, _, _) = await PostAsync("/api/pump/device/chat/" + Uri.EscapeDataString(acct) + "/seen",
            new { seq }, _settings.CloudDeviceToken, ct);
        return ok;
    }

    /// <summary>کدِ کوتاهی که کارمند با آن به این پمپ می‌پیوندد.</summary>
    public async Task<(bool Ok, string Code, string Why)> JoinCodeAsync(CancellationToken ct = default)
    {
        if (!Activated) return (false, "", "این برنامه هنوز فعال نشده است");
        var (ok, json, why, _) = await PostAsync("/api/pump/device/join-code",
            new { role = "staff", hours = 24, maxUses = 10 }, _settings.CloudDeviceToken, ct);
        return ok ? (true, Str(json, "code"), "") : (false, "", why);
    }

    /// <summary>
    /// کدِ دسترسیِ پمپ — همان کدی که هر کسی اپِ گوشی را نصب می‌کند باید بزند.
    ///
    /// خواستهٔ صریحِ صاحب ریپو: «برای هر پمپ یک کد باشد… با پمپ‌های دیگر قاطی
    /// نشود.» سرور برای هر پمپ یک کدِ دائمی دارد؛ ‎rotate‎ کدِ تازه می‌سازد و
    /// کدِ قبلی همان لحظه از کار می‌افتد. نسخهٔ آخر در تنظیمات می‌ماند تا
    /// بی‌اینترنت هم دیده شود.
    /// </summary>
    public async Task<(bool Ok, string Code, string Why)> AccessCodeAsync(bool rotate = false,
                                                                          CancellationToken ct = default)
    {
        if (!Activated) return (false, _settings.CloudAccessCode, "این برنامه هنوز فعال نشده است");
        var (ok, json, why, _) = rotate
            ? await PostAsync("/api/pump/device/access-code/rotate", new { }, _settings.CloudDeviceToken, ct)
            : await GetAsync("/api/pump/device/access-code", _settings.CloudDeviceToken, ct);
        if (!ok) return (false, _settings.CloudAccessCode, why);
        var code = Str(json, "code");
        if (code.Length > 0 && code != _settings.CloudAccessCode)
        {
            _settings.CloudAccessCode = code;
            await _save();
        }
        return (true, code, "");
    }

    /// <summary>برای نمایش: ‎K7PM-3XQ2‎ — همان قاعدهٔ سرور و اپِ گوشی.</summary>
    public static string FormatAccessCode(string code)
    {
        var c = (code ?? "").Trim().ToUpperInvariant();
        return c.Length == 8 ? c[..4] + "-" + c[4..] : c;
    }

    // ── حساب: ورود با گوگل ─────────────────────────────────────────────

    /// <summary>آیا با حسابِ گوگل وارد شده‌ایم.</summary>
    public bool SignedIn => !string.IsNullOrWhiteSpace(_settings.CloudAccountToken);

    /// <summary>ایمیلِ حساب — برای نشان دادن در صفحهٔ حساب.</summary>
    public string Email => _settings.CloudEmail;

    /// <summary>نامِ حساب — برای نشان دادن در صفحهٔ حساب.</summary>
    public string Name => _settings.CloudName;

    /// <summary>
    /// شناسهٔ کلاینتِ گوگل را از خودِ سرور می‌پرسد.
    ///
    /// ⚠️ از داخلِ کد نمی‌آید تا عوض شدنش نسخهٔ تازهٔ برنامه نخواهد — همان
    /// قاعده‌ای که اپِ کارمندان هم دارد.
    /// </summary>
    public async Task<string> GoogleClientIdAsync(CancellationToken ct = default)
    {
        var (ok, json, _, _) = await GetAsync("/api/config", null, ct);
        if (!ok) return "";
        //  شناسهٔ Desktop مالِ همین برنامه است؛ شناسهٔ Web مالِ سایت و اپِ
        //  کارمندان. اگر سرور هنوز جدا نکرده باشد، به همان یکی برمی‌گردیم.
        var desktop = Str(json, "googleDesktopClientId");
        return desktop.Length > 0 ? desktop : Str(json, "googleClientId");
    }

    /// <summary>
    /// توکنِ هویتِ گوگل را به سرور می‌دهد و نشستِ حساب می‌گیرد.
    /// </summary>
    public async Task<CloudResult> SignInAsync(string idToken, CancellationToken ct = default)
    {
        var (ok, json, why, code) = await PostAsync(
            "/api/auth/google", new { idToken, app = "pump" }, null, ct);
        if (!ok) return CloudResult.No(why, code);

        return await SeatAsync(json);
    }

    /// <summary>
    /// نشستی که سرور داد را می‌نشاند.
    ///
    /// ⚠️ **سرور `accessToken` می‌دهد، نه `token`** — و برنامه تا امروز فقط
    /// دنبالِ `token` بود، پس هر ورودِ **موفقی** را «سرور نشست نداد»
    /// می‌خواند. با آزمونِ واقعیِ سرور دیده شد
    /// (`shop/server/test/pump-account.test.js`). هر دو نام خوانده می‌شوند
    /// تا نسخهٔ قدیمیِ سرور هم کار کند.
    /// </summary>
    private async Task<CloudResult> SeatAsync(JsonElement json)
    {
        var token = Str(json, "accessToken");
        if (token.Length == 0) token = Str(json, "token");
        if (token.Length == 0) return CloudResult.No("سرور نشست نداد");

        _settings.CloudAccountToken = token;
        var refresh = Str(json, "refreshToken");
        if (refresh.Length > 0) _settings.CloudRefreshToken = refresh;
        //  ⚠️ عمرِ توکن را از خودِ سرور برمی‌داریم (یک ساعت)، تا کمی پیش از
        //  انقضا خودمان تازه کنیم و کاربر هیچ‌وقت به دیوارِ ۴۰۱ نخورد.
        _settings.CloudAccessExpiresAt = Num(json, "accessExpiresAt");
        if (json.TryGetProperty("user", out var u) && u.ValueKind == JsonValueKind.Object)
        {
            var em = Str(u, "email"); if (em.Length > 0) _settings.CloudEmail = em;
            var nm = Str(u, "name");  if (nm.Length > 0) _settings.CloudName = nm;
        }
        await SaveQuiet();
        return CloudResult.Done;
    }

    // ── حساب با ایمیل و رمز ─────────────────────────────────────────────
    //
    //  خواستهٔ صریحِ صاحب ریپو (۱۴۰۵/۰۶/۲۸): «نه می‌گوید حساب داری یا نه، نه
    //  ثبت یا ساختنِ حساب دارد و نه رمز دارد و می‌خواهد… اول اسم، ایمیل،
    //  رمز، تکرارِ رمز؛ بعد برود بخشِ بعدی: کدِ شش‌رقمی و تاییدش از سرور و
    //  اسمِ پمپ و لوکیشنِ پمپ. همین.»
    //
    //  ⚠️ **رمز هیچ‌جا روی این کامپیوتر ذخیره نمی‌شود** — نه خام، نه هش.
    //  فقط همان یک بار به ابر می‌رود و آن‌جا با توکنِ نشست جواب می‌آید،
    //  دقیقاً مثلِ راهِ گوگل. چیزی که ذخیره می‌شود همان توکن است.
    //
    //  ⚠️ پاسخِ سرور **همان شکلِ راهِ گوگل** است (`{token, refreshToken,
    //  user{email,name}}`) تا هیچ جای دیگرِ برنامه فرق نفهمد.

    // ── ثبت‌نام سه‌پله‌ای: نام و ایمیل و رمز ⇒ کدِ ایمیل ⇒ حساب ─────────
    //
    //  گزارشِ صاحب ریپو (۱۴۰۵/۰۶/۲۹): «می‌خواهم با ایمیل خودم حسابی بسازم،
    //  نمی‌شود.»
    //
    //  ⛔ **درِ یک‌مرحله‌ایِ `/api/auth/register` عمداً بسته است** و
    //  `verification_required` می‌دهد: حساب بی تأییدِ ایمیل ساخته نمی‌شود.
    //  همین را با آزمونِ واقعیِ سرور دیدیم، نه با حدس
    //  (`shop/server/test/pump-account.test.js`). پس راهِ درست سه‌پله است:
    //
    //      ۱) /register/start     نام · ایمیل · رمز · تکرارِ رمز ⇒ کد به ایمیل
    //      ۲) /register/verify    کدِ شش‌رقمیِ ایمیل ⇒ «بلیتِ ثبت‌نام»
    //      ۳) /register/complete  بلیت + پذیرشِ شرایط ⇒ حساب و نشست
    //
    //  ⚠️ **بلیت و رمز هیچ‌وقت روی دیسک نمی‌نشینند** — بلیت در حافظهٔ همین
    //  شیء است و رمز را ویومدل در همان صفحه نگه می‌دارد و بعد پاکش می‌کند.

    /// <summary>بلیتِ ثبت‌نام — فقط در حافظه، تا پلهٔ سوم.</summary>
    private string _registerTicket = "";

    /// <summary>نسخهٔ شرایطی که همراهِ بلیت آمد — همان را برمی‌گردانیم.</summary>
    private string _termsVersion = "";

    /// <summary>پلهٔ دوم را رفته‌ایم و بلیت داریم؟</summary>
    public bool HasRegisterTicket => _registerTicket.Length > 0;

    /// <summary>پلهٔ یک — کد به ایمیل فرستاده می‌شود. حسابی ساخته نمی‌شود.</summary>
    public async Task<CloudResult> RegisterStartAsync(string name, string email, string password,
                                                      CancellationToken ct = default)
    {
        var (ok, _, why, code) = await PostAsync("/api/auth/register/start", new
        {
            name = (name ?? "").Trim(),
            email = (email ?? "").Trim(),
            password,
            passwordConfirm = password,
            app = "pump",
        }, null, ct);
        return ok ? CloudResult.Done : CloudResult.No(why, code);
    }

    /// <summary>پلهٔ دو — کدِ ایمیل. جوابش بلیتِ بیست‌دقیقه‌ای است.</summary>
    public async Task<CloudResult> RegisterVerifyAsync(string email, string emailCode,
                                                       CancellationToken ct = default)
    {
        var clean = new string((emailCode ?? "").Where(char.IsDigit).ToArray());
        if (clean.Length != 6) return CloudResult.No("کدِ ایمیل باید شش رقم باشد");

        var (ok, json, why, code) = await PostAsync("/api/auth/register/verify",
            new { email = (email ?? "").Trim(), code = clean, app = "pump" }, null, ct);
        if (!ok) return CloudResult.No(why, code);

        _registerTicket = Str(json, "ticket");
        if (_registerTicket.Length == 0) return CloudResult.No("سرور بلیتِ ثبت‌نام نداد");
        if (json.TryGetProperty("terms", out var t) && t.ValueKind == JsonValueKind.Object)
            _termsVersion = Str(t, "version");
        return CloudResult.Done;
    }

    /// <summary>
    /// پلهٔ سه — حساب ساخته می‌شود و نشست می‌آید.
    ///
    /// ⚠️ پذیرشِ شرایط **اجباریِ خودِ سرور** است (`terms_required`)، پس
    /// برنامه باید واقعاً از کاربر پرسیده باشد.
    /// </summary>
    public async Task<CloudResult> RegisterCompleteAsync(string name, string password,
                                                         bool termsAccepted,
                                                         CancellationToken ct = default)
    {
        if (_registerTicket.Length == 0) return CloudResult.No("اول کدِ ایمیل را بزنید");
        if (!termsAccepted) return CloudResult.No("برای ساختنِ حساب باید شرایط را بپذیرید", "terms_required");

        var (ok, json, why, code) = await PostAsync("/api/auth/register/complete", new
        {
            ticket = _registerTicket,
            name = (name ?? "").Trim(),
            password,
            terms = new { accepted = true, version = _termsVersion },
            device = new { uid = DeviceUid, name = Environment.MachineName, platform = "windows" },
            app = "pump",
        }, null, ct);
        if (!ok) return CloudResult.No(why, code);

        _registerTicket = "";      // بلیت خرج شد
        return await SeatAsync(json);
    }

    /// <summary>متنِ شرایط و ضوابط — همان چیزی که کاربر می‌پذیرد.</summary>
    public async Task<(bool Ok, string Text, string Why)> TermsAsync(CancellationToken ct = default)
    {
        var (ok, json, why, _) = await GetAsync("/api/auth/terms", null, ct);
        if (!ok) return (false, "", why);

        var sb = new System.Text.StringBuilder();
        var title = Str(json, "title");
        if (title.Length > 0) sb.AppendLine(title).AppendLine();
        if (json.TryGetProperty("sections", out var arr) && arr.ValueKind == JsonValueKind.Array)
            foreach (var sec in arr.EnumerateArray())
            {
                var st = Str(sec, "title");
                var sb2 = Str(sec, "body");
                if (st.Length > 0) sb.AppendLine("• " + st);
                if (sb2.Length > 0) sb.AppendLine(sb2).AppendLine();
            }
        var text = sb.ToString().Trim();
        return (text.Length > 0, text, text.Length > 0 ? "" : "متنِ شرایط نیامد");
    }

    /// <summary>ورود به حسابی که از قبل ساخته شده.</summary>
    public Task<CloudResult> SignInWithPasswordAsync(string email, string password,
                                                     CancellationToken ct = default) =>
        AuthAsync("/api/auth/login",
                  new { email = (email ?? "").Trim(), password, app = "pump" },
                  ct);

    private async Task<CloudResult> AuthAsync(string path, object body, CancellationToken ct)
    {
        var res = await SendFull(Build(HttpMethod.Post, path, body, null), ct);
        if (!res.Ok)
        {
            //  ⚠️ صادق باش: اگر سرورِ ابر این راه را نداشت، «رمز غلط» نگو.
            //  ⚠️ از روی **کدِ خودِ HTTP** تصمیم می‌گیریم، نه از روی گشتنِ
            //  رشتهٔ «404» در متنِ فارسیِ خطا: سرورِ به‌روز برای مسیرِ نبوده
            //  پیامِ خودش را می‌دهد («این مسیر وجود ندارد») و آن گشتن دیگر
            //  نمی‌گرفت.
            if (res.Status == 404)
                return CloudResult.No(
                    "سرورِ حساب این راه را ندارد — برنامه را به‌روز کنید یا «بعداً» را بزنید.",
                    "no_route");
            return CloudResult.No(res.Why, res.Code);
        }

        return await SeatAsync(res.Json);
    }

    // ── رمزِ فراموش‌شده ─────────────────────────────────────────────────
    //
    //  ⚠️ این راه **ساخته نشد، پیدا شد**: سرور از قبل هر دو مسیر را دارد
    //  (`shop/server/src/routes/auth.js` و `test/password-reset.test.js`) و
    //  فقط برنامهٔ نیتیو هیچ‌وقت صدایشان نزده بود. پس کسی که رمزش را گم
    //  می‌کرد هیچ راهی جز ساختنِ حسابِ تازه نداشت.
    //
    //      POST /api/auth/password/forgot  { email }         ⇒ کد به ایمیل
    //      POST /api/auth/password/reset   { email, code, password }
    //                                      ⇒ رمزِ تازه + نشست، یک‌جا
    //
    //  ⚠️ **جوابِ «این ایمیل هست» و «نیست» عمداً یکی است** (خودِ سرور
    //  این‌طور نوشته). پس این‌جا هم نباید چیزی به آن اضافه کرد: پیامی مثلِ
    //  «چنین حسابی نیست» فهرستِ ایمیل‌های مشتری‌ها را لو می‌دهد.

    /// <summary>پلهٔ یک — کدِ یک‌بارمصرف به همان ایمیل می‌رود.</summary>
    public async Task<CloudResult> ForgotPasswordAsync(string email, CancellationToken ct = default)
    {
        var clean = (email ?? "").Trim();
        if (clean.Length == 0) return CloudResult.No("ایمیل را بنویسید");

        var (ok, _, why, code) = await PostAsync("/api/auth/password/forgot",
            new { email = clean, app = "pump" }, null, ct);
        return ok ? CloudResult.Done : CloudResult.No(why, code);
    }

    /// <summary>
    /// پلهٔ دو — کدِ ایمیل و رمزِ تازه. سرور خودش همان‌جا وارد هم می‌کند، پس
    /// کاربر رمزِ تازه‌اش را دوباره تایپ نمی‌کند.
    ///
    /// ⚠️ سرور پس از عوض شدنِ رمز **همهٔ نشست‌های باز را می‌بندد**
    /// (<c>revokeAllForSubject</c>) — یعنی اگر رمز را گم کرده بودید چون
    /// گوشی‌تان دستِ دیگری افتاده، آن نشست هم همان لحظه می‌میرد.
    /// </summary>
    public async Task<CloudResult> ResetPasswordAsync(string email, string emailCode, string newPassword,
                                                      CancellationToken ct = default)
    {
        var clean = new string((emailCode ?? "").Where(char.IsDigit).ToArray());
        if (clean.Length != 6) return CloudResult.No("کدِ ایمیل باید شش رقم باشد");

        var (ok, json, why, code) = await PostAsync("/api/auth/password/reset", new
        {
            email = (email ?? "").Trim(),
            code = clean,
            password = newPassword,
            device = new { uid = DeviceUid, name = Environment.MachineName, platform = "windows" },
            app = "pump",
        }, null, ct);
        if (!ok) return CloudResult.No(why, code);

        return await SeatAsync(json);
    }

    // ── نشستِ تازه، و ۴۰۱ ────────────────────────────────────────────────
    //
    //  ⛔ **باگی که برنامه را یک‌ساعته خاموش می‌کرد.** سرور به توکنِ دسترسی
    //  دقیقاً **یک ساعت** عمر می‌دهد (`ACCESS_TOKEN_TTL_MIN = 60`) و
    //  `refreshToken` را برای نود روز می‌دهد. برنامه `refreshToken` را
    //  ذخیره می‌کرد ولی **هیچ‌جا نمی‌خواندش** و هیچ ۴۰۱ی را هم نمی‌فهمید.
    //  یعنی یک ساعت پس از ورود، `/api/pump/me` و کدِ اپِ کارمندان و هر کارِ
    //  حسابیِ دیگر بی‌صدا شکست می‌خوردند و **دیگر هیچ‌وقت درست نمی‌شدند** —
    //  در حالی که صفحهٔ پروفایل همچنان «وارد شده‌اید» می‌گفت.
    //
    //  همان کاری که `kar/cloud.js` از اول می‌کرد (`authed()`): یک بار تازه
    //  کن، یک بار دوباره بزن. ⚠️ **فقط یک بار** — حلقه زدن جز پنهان کردنِ
    //  مشکل کاری نمی‌کند.

    /// <summary>
    /// چند دقیقه پیش از انقضا خودمان تازه می‌کنیم — یک درخواستِ حتماً-شکست
    /// کمتر، و کاربری که وسطِ کار پیامِ خطا نمی‌بیند.
    /// </summary>
    private const long RefreshSkewMs = 60_000;

    /// <summary>دو کارِ هم‌زمان نباید دو بار تازه کنند (بندِ «Race Condition»).</summary>
    private readonly SemaphoreSlim _refreshGate = new(1, 1);

    /// <summary>توکنِ دسترسی نزدیکِ انقضاست؟</summary>
    private bool AccessNearlyExpired =>
        _settings.CloudAccessExpiresAt > 0
        && DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + RefreshSkewMs >= _settings.CloudAccessExpiresAt;

    /// <summary>
    /// نشستِ تازه از روی <c>refreshToken</c>.
    ///
    /// سه پایان دارد و هر سه مهم‌اند:
    ///  • شد ⇒ توکنِ تازه می‌نشیند.
    ///  • سرور گفت این توکن باطل است (۴۰۱/۴۰۳) ⇒ نشست <b>پاک</b> می‌شود، تا
    ///    صفحهٔ پروفایل دیگر دروغ نگوید و کاربر دوباره وارد شود.
    ///  • ⚠️ <b>نرسیدیم</b> (بی‌اینترنت، سرورِ خاموش، تایم‌اوت) ⇒ هیچ چیزی
    ///    پاک نمی‌شود. وگرنه یک قطعیِ اینترنت کاربر را از حسابش بیرون
    ///    می‌انداخت — و این برنامه اساساً آفلاین است.
    /// </summary>
    private async Task<bool> RefreshSessionAsync(CancellationToken ct)
    {
        await _refreshGate.WaitAsync(ct);
        try
        {
            if (string.IsNullOrWhiteSpace(_settings.CloudRefreshToken))
            {
                await ClearSessionAsync();
                return false;
            }

            var res = await SendFull(Build(HttpMethod.Post, "/api/auth/refresh",
                new { refreshToken = _settings.CloudRefreshToken, device = new { deviceId = DeviceUid } },
                null), ct);

            if (res.Ok)
            {
                var token = Str(res.Json, "accessToken");
                if (token.Length == 0) return false;
                _settings.CloudAccountToken = token;
                //  ⚠️ این مسیر `refreshToken`ِ تازه **نمی‌دهد** — همان قبلی
                //  می‌ماند و درست هم هست. ولی اگر روزی داد، برمی‌داریم.
                var again = Str(res.Json, "refreshToken");
                if (again.Length > 0) _settings.CloudRefreshToken = again;
                _settings.CloudAccessExpiresAt = Num(res.Json, "accessExpiresAt");
                await SaveQuiet();
                return true;
            }

            if (res.Status is 401 or 403) await ClearSessionAsync();
            return false;
        }
        catch (OperationCanceledException) { return false; }
        finally { _refreshGate.Release(); }
    }

    /// <summary>
    /// نشست را پاک می‌کند — ولی <b>نام و ایمیل می‌مانند</b>، تا صفحهٔ ورود
    /// کادرها را پر کند و کاربر فقط رمز بزند.
    /// ⛔ و دفترِ روی کامپیوتر و اشتراکِ دستگاه دست نمی‌خورند.
    /// </summary>
    private async Task ClearSessionAsync()
    {
        _settings.CloudAccountToken = "";
        _settings.CloudRefreshToken = "";
        _settings.CloudAccessExpiresAt = 0;
        await SaveQuiet();
    }

    /// <summary>
    /// درخواستی با توکنِ حساب — با یک تازه‌سازی و یک تلاشِ دوباره.
    ///
    /// ⚠️ هر تلاش <b>پیامِ تازه‌ای</b> می‌سازد: یک
    /// <see cref="HttpRequestMessage"/> فقط یک بار فرستادنی است.
    /// </summary>
    private async Task<CloudReply> AccountAsync(HttpMethod method, string path, object? body,
                                                CancellationToken ct)
    {
        if (!SignedIn) return CloudReply.Fail("اول وارد حساب شوید", "signed_out");

        //  زودتر از انقضا، بی این‌که منتظرِ یک ۴۰۱ِ حتمی بمانیم
        if (AccessNearlyExpired) await RefreshSessionAsync(ct);
        if (!SignedIn) return CloudReply.Fail("نشست منقضی شده — دوباره وارد شوید", "signed_out");

        var first = await SendFull(Build(method, path, body, _settings.CloudAccountToken), ct);
        if (first.Status != 401) return first;

        if (!await RefreshSessionAsync(ct)) return first;
        return await SendFull(Build(method, path, body, _settings.CloudAccountToken), ct);
    }

    /// <summary>
    /// خروج از حساب — دفترِ روی کامپیوتر دست نمی‌خورد.
    ///
    /// ⛔ **پیش از این فقط محلی بود.** توکنِ دسترسی و توکنِ تازه‌سازی روی
    /// سرور زنده می‌ماندند (تازه‌سازی تا نود روز)، پس هر کسی که یک بار
    /// آن رشته را برداشته بود، «خروج»ِ کاربر جلویش را نمی‌گرفت. حالا اول
    /// از سرور باطل می‌شوند (<c>POST /api/auth/logout</c>).
    ///
    /// ⚠️ ولی نشدنِ آن هیچ‌وقت جلوی خروجِ محلی را نمی‌گیرد: کسی که
    /// اینترنت ندارد هم باید بتواند از حسابش بیرون بیاید.
    /// </summary>
    public async Task SignOutAsync(CancellationToken ct = default)
    {
        if (SignedIn)
        {
            try
            {
                await SendFull(Build(HttpMethod.Post, "/api/auth/logout",
                    new { refreshToken = _settings.CloudRefreshToken }, _settings.CloudAccountToken), ct);
            }
            catch { /* خروجِ محلی گروگانِ سرور نیست */ }
        }

        _settings.CloudAccountToken = "";
        _settings.CloudRefreshToken = "";
        _settings.CloudAccessExpiresAt = 0;
        _settings.CloudEmail = "";
        _settings.CloudName = "";
        await SaveQuiet();
    }

    /// <summary>
    /// ══ «این دستگاه دیگر مالِ این پمپ نیست» ═════════════════════════════
    ///
    /// ⛔ **نشتی که این می‌بندد**: خروج از حساب تا امروز فقط چهار فیلدِ
    /// حساب را پاک می‌کرد. توکنِ **دستگاه**، شناسهٔ پمپ، مجوز، کلیدِ عمومی،
    /// کدِ اپِ کارمندان، نشانی و رمزِ سرورِ خانگی و مُهرِ ارفاق همه سرِ جا
    /// می‌ماندند. یعنی اگر همین کامپیوتر به حسابِ پمپِ دیگری می‌رفت:
    ///   • عکسِ حساب‌ها به پوشهٔ ابریِ **پمپِ قبلی** می‌رفت (توکنِ دستگاهِ او)،
    ///   • کدِ اپِ کارمندان و اشتراکِ پمپِ قبلی روی صفحهٔ پروفایل دیده می‌شد،
    ///   • و ارفاقِ اشتراکِ او برای این یکی خرج می‌شد.
    ///
    /// ⚠️ **دفترِ روی کامپیوتر دست نمی‌خورد.** این تابع یک بیت از دیتابیس
    /// را هم لمس نمی‌کند — دفترِ صاحبِ پمپ مالِ خودش است و گروگان گرفتنش
    /// (یا پاک کردنش) هیچ‌وقت کارِ ما نیست. فقط بندهای «این نصب به کدام پمپ
    /// وصل است» باز می‌شوند.
    ///
    /// ⚠️ کلیدِ عمومی هم پاک می‌شود، و این عمدی است: قفلِ TOFU برای «همین
    /// دستگاه، همین سرور» است و با عوض شدنِ پمپ باید از نو قفل شود. نشانیِ
    /// ابر همچنان در کد قفل است، پس این هیچ دری را باز نمی‌کند.
    /// </summary>
    public async Task ForgetStationAsync()
    {
        _settings.CloudDeviceToken = "";
        _settings.CloudStationId = "";
        _settings.CloudLicense = "";
        _settings.CloudPublicKey = "";
        _settings.CloudAccessCode = "";
        _settings.CloudSyncedAt = 0;
        _settings.EntitledUntil = 0;
        _settings.EntitledPlan = "";
        //  نشانی و رمزهای سرورِ خانگی هم مالِ همان پمپ بودند
        _settings.ServerUrl = "";
        _settings.ServerToken = "";
        _settings.ServerReadKey = "";
        _settings.ServerId = "";
        Subscription = PumpSubscription.None;
        await SaveQuiet();
    }

    /// <summary>
    /// نشانیِ سرورِ خانگی و رمزِ خواندن را از حساب می‌گیرد.
    ///
    /// ⚠️ همین است که کادرِ «نشانیِ سرور» و «رمزِ سرور» را از تنظیمات
    /// برداشت: کاربر هیچ‌کدام را تایپ نمی‌کند، از حسابش می‌آید.
    /// </summary>
    public async Task<(bool Ok, string Url, string ReadKey, string Station, string Why)>
        HomeFromAccountAsync(CancellationToken ct = default)
    {
        if (!SignedIn) return (false, "", "", "", "اول وارد حساب شوید");

        //  ⚠️ از راهِ `AccountAsync` می‌رود، نه `GetAsync`ِ خام: توکنِ دسترسی
        //  یک ساعت عمر دارد و این همان جایی بود که بی‌صدا می‌مرد.
        var res = await AccountAsync(HttpMethod.Get, "/api/pump/me", null, ct);
        if (!res.Ok) return (false, "", "", "", res.Why);
        var json = res.Json;

        if (!json.TryGetProperty("home", out var home) || home.ValueKind != JsonValueKind.Object)
            return (false, "", "", "", "هنوز پمپی به این حساب وصل نشده است");

        //  ⛔ **پمپِ این حساب با پمپی که این دستگاه رویش قفل شده یکی نیست.**
        //  بی این سنجش، ورود با حسابِ پمپِ دیگر نشانی و رمزِ **آن** پمپ را
        //  روی تنظیماتِ این یکی می‌نشاند: از آن لحظه دفترِ این پمپ به سرورِ
        //  خانگیِ پمپِ دیگری می‌رفت. راهِ درستِ جابه‌جایی
        //  `ForgetStationAsync` است.
        //
        //  ⚠️ **سنجش روی شناسهٔ ابری است، نه کدِ پمپ.** یک بار با کدِ پمپ
        //  نوشتمش و سنجهٔ `cloudlogin` گرفتش: کدِ ابر لازم نیست با
        //  `AppSettings.StationCode`ی محلی یکی باشد (ابر می‌تواند
        //  هنجارش کند یا خودش بسازدش)، پس آن مقایسه نصبِ سالم را هم رد
        //  می‌کرد. شناسه همان چیزی است که مجوز (`stn`) هم رویش قفل است.
        var acctStation = StationId(json);
        var locked = (_settings.CloudStationId ?? "").Trim();
        if (locked.Length > 0 && acctStation.Length > 0
            && !string.Equals(acctStation, locked, StringComparison.Ordinal))
            return (false, "", "", "", "این حساب مالِ پمپِ دیگری است. برای جابه‌جایی، "
                + "این دستگاه را از پمپِ فعلی جدا کنید.");

        return (true, Str(home, "url"), Str(home, "readKey"), Str(home, "station"), "");
    }

    // ── گفت‌وگو با ابر ─────────────────────────────────────────────────

    /// <summary>
    /// ══ هویتِ نسخه روی هر درخواست ═══════════════════════════════════════
    ///
    /// تا پیش از این هیچ درخواستی نمی‌گفت از کدام نسخهٔ برنامه آمده — نه
    /// ‎User-Agent‎ی، نه هدرِ نسخه‌ای. نتیجه‌اش دو چیز بود: سرور نمی‌توانست
    /// نسخهٔ قدیم را از تازه جدا کند (پس هر تغییرِ سرور خطرِ شکستنِ نصب‌های
    /// قدیم داشت)، و در لاگِ خطا معلوم نبود کدام نسخه خطا داده.
    ///
    /// ⚠️ **هیچ چیزِ شناسایی‌کنندهٔ کاربر این‌جا نمی‌رود** — نه ایمیل، نه نامِ
    /// ماشین، نه شناسهٔ دستگاه. فقط شمارهٔ نسخه و نامِ برنامه. خودِ توکن
    /// می‌گوید این کدام دستگاه است.
    ///
    /// ⚠️ جایش عمداً داخلِ <see cref="Build"/> است، نه در فرستنده: هر
    /// درخواست — و هر **تلاشِ دوم** پس از تازه‌سازیِ توکن — از همان یک جا
    /// ساخته می‌شود، پس هیچ مسیری بی هدر نمی‌ماند.
    /// </summary>
    public static void Stamp(HttpRequestMessage req)
    {
        try
        {
            var v = PumpYaqobi.App.Update.AppVersion.Current;
            req.Headers.TryAddWithoutValidation("User-Agent", "PumpYaqobi/" + v);
            req.Headers.TryAddWithoutValidation("X-App-Version", v);
            req.Headers.TryAddWithoutValidation("X-App-Platform", "windows-native");
        }
        catch { /* هدر نرفتن هیچ‌وقت نباید جلوی درخواست را بگیرد */ }
    }

    /// <summary>
    /// پاسخِ خامِ ابر — مثلِ قبل، به‌علاوهٔ <b>خودِ کدِ HTTP</b>.
    ///
    /// ⚠️ بی این، «۴۰۱» از «۴۰۰» جدا نمی‌شد و تنها راهِ تشخیصش گشتن دنبالِ
    /// رشتهٔ «404» داخلِ متنِ فارسیِ خطا بود — که هم شکننده بود و هم اجازه
    /// نمی‌داد نشستِ منقضی از رمزِ غلط جدا شود.
    /// </summary>
    private readonly record struct CloudReply(bool Ok, JsonElement Json, string Why, string Code, int Status)
    {
        public static CloudReply Fail(string why, string code, int status = 0) =>
            new(false, default, why, code, status);
    }

    /// <summary>
    /// ⚠️ یک <see cref="HttpRequestMessage"/> فقط <b>یک بار</b> فرستادنی
    /// است، پس هر تلاش باید پیامِ خودش را بسازد — همین است که تلاشِ دوم پس
    /// از تازه‌سازیِ توکن را ممکن می‌کند.
    /// </summary>
    private static HttpRequestMessage Build(HttpMethod method, string path, object? body, string? token)
    {
        var req = new HttpRequestMessage(method, CloudConfig.BaseUrl + path);
        if (body is not null) req.Content = JsonContent.Create(body);
        if (!string.IsNullOrWhiteSpace(token)) req.Headers.Add("Authorization", $"Bearer {token}");
        Stamp(req);
        return req;
    }

    private static async Task<CloudReply> SendFull(HttpRequestMessage req, CancellationToken ct)
    {
        using var _ = req;
        try
        {
            using var res = TestTransport is null
                ? await Http.SendAsync(req, ct)
                : await TestTransport(req, ct);
            var text = await res.Content.ReadAsStringAsync(ct);
            var status = (int)res.StatusCode;
            JsonElement json = default;
            try
            {
                using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(text) ? "{}" : text);
                json = doc.RootElement.Clone();
            }
            catch { /* پاسخِ بی‌شکل */ }

            if (res.IsSuccessStatusCode) return new CloudReply(true, json, "", "", status);

            var why = "";
            var code = "";
            if (json.ValueKind == JsonValueKind.Object && json.TryGetProperty("error", out var e))
            {
                why = Str(e, "message");
                code = Str(e, "code");
            }
            //  ⚠️ سرور برای «تلاشِ زیاد» ۴۲۹ می‌دهد (ده ورود در ربع ساعت).
            //  بی این، کاربر «سرور جواب نداد (429)» می‌دید و فکر می‌کرد
            //  برنامه خراب است.
            if (string.IsNullOrWhiteSpace(why))
                why = status switch
                {
                    429 => "تلاشِ زیاد — چند دقیقه صبر کنید و دوباره بزنید",
                    401 => "نشست منقضی شده — دوباره وارد شوید",
                    >= 500 => "سرور همین حالا مشکل دارد — کمی بعد دوباره",
                    _ => $"سرور جواب نداد ({status})",
                };
            //  کدِ ماشینی: اگر سرور نداد، خودِ شمارهٔ HTTP. (`AuthAsync` از
            //  همین برای تشخیصِ «این راه روی سرور نیست» استفاده می‌کند.)
            if (code.Length == 0) code = status.ToString();
            return new CloudReply(false, json, why, code, status);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            //  ⚠️ `HttpClient.Timeout` هم همین را پرت می‌کند. جدا کردنش از
            //  «کاربر خودش لغو کرد» مهم است، وگرنه تایم‌اوت «لغو شد» دیده
            //  می‌شد.
            return CloudReply.Fail("سرور دیر جواب داد — دوباره بزنید", "timeout");
        }
        catch (OperationCanceledException) { return CloudReply.Fail("لغو شد", "cancelled"); }
        catch (HttpRequestException) { return CloudReply.Fail("به سرور نرسیدیم — اینترنت را بررسی کنید", "offline"); }
        //  ⚠️ پیامِ خامِ استثنا به کاربر نشان داده نمی‌شود: ممکن است نشانی،
        //  نامِ میزبان یا جزئیاتِ TLS داشته باشد.
        catch { return CloudReply.Fail("ارتباط با سرور برقرار نشد", "error"); }
    }

    private static async Task<(bool, JsonElement, string, string)> Send(
        HttpRequestMessage req, CancellationToken ct)
    {
        var r = await SendFull(req, ct);
        return (r.Ok, r.Json, r.Why, r.Code);
    }

    private static Task<(bool, JsonElement, string, string)> PostAsync(
        string path, object body, string? token, CancellationToken ct) =>
        Send(Build(HttpMethod.Post, path, body, token), ct);

    private static Task<(bool, JsonElement, string, string)> PutAsync(
        string path, object body, string? token, CancellationToken ct) =>
        Send(Build(HttpMethod.Put, path, body, token), ct);

    private static Task<(bool, JsonElement, string, string)> GetAsync(
        string path, string? token, CancellationToken ct) =>
        Send(Build(HttpMethod.Get, path, null, token), ct);

    // ── خواندنِ پاسخ ───────────────────────────────────────────────────

    private void ReadSubscription(JsonElement json)
    {
        if (json.ValueKind != JsonValueKind.Object) return;
        if (!json.TryGetProperty("entitlement", out var ent) || ent.ValueKind != JsonValueKind.Object)
            return;

        var source = Str(ent, "source");
        var feats = new List<string>();
        if (ent.TryGetProperty("features", out var f) && f.ValueKind == JsonValueKind.Array)
            foreach (var x in f.EnumerateArray())
                if (x.ValueKind == JsonValueKind.String) feats.Add(x.GetString() ?? "");

        var days = 0;
        long endsAt = 0;
        var plan = "";
        if (source == "trial" && ent.TryGetProperty("trial", out var tr) && tr.ValueKind == JsonValueKind.Object)
        {
            days = (int)Num(tr, "daysLeft");
            endsAt = Num(tr, "endsAt");
            plan = "دورهٔ آزمایشی";
        }
        else if (ent.TryGetProperty("subscription", out var sub) && sub.ValueKind == JsonValueKind.Object)
        {
            days = (int)Num(sub, "daysLeft");
            endsAt = Num(sub, "endsAt");
            plan = Str(sub, "plan");
        }

        Subscription = new PumpSubscription(
            source is "subscription" or "trial", source, plan, days, endsAt, feats);
    }

    private static string StationId(JsonElement json) =>
        json.ValueKind == JsonValueKind.Object
        && json.TryGetProperty("station", out var st)
        && st.ValueKind == JsonValueKind.Object
            ? Str(st, "id") : "";

    private static string Str(JsonElement e, string k) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(k, out var v)
        && v.ValueKind == JsonValueKind.String ? (v.GetString() ?? "") : "";

    private static long Num(JsonElement e, string k) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(k, out var v)
        && v.ValueKind == JsonValueKind.Number && v.TryGetInt64(out var n) ? n : 0;

    private async Task SaveQuiet()
    {
        try { await _save(); } catch { /* ذخیره نشدنِ تنظیمات نباید کار را بشکند */ }
    }
}
