using System.Net.Http;
using System.Text.Json;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Services.Data;

namespace PumpYaqobi.App.Services;

/// <summary>پاسخِ <c>POST /api/sync/v1/push</c>، همان‌طور که برنامه لازمش دارد.</summary>
/// <param name="Ok">درخواست رسید و سرور جواب داد.</param>
/// <param name="Applied">چند op واقعاً نشست.</param>
/// <param name="Cursor">سرِ دفترِ سرور پس از این دسته.</param>
/// <param name="Results">شناسهٔ هر op ⇒ <c>applied</c>/<c>duplicate</c>/دلیلِ رد.</param>
/// <param name="ServerSchema">نسخهٔ schemaی سرور.</param>
/// <param name="UpgradeRequired">۴۲۶ — برنامه جلوتر است و opها نگه داشته می‌شوند.</param>
public sealed record SyncPushResult(bool Ok, int Applied, long Cursor,
                                    IReadOnlyDictionary<string, string> Results,
                                    int ServerSchema, bool UpgradeAvailable,
                                    bool UpgradeRequired, string Why)
{
    public static SyncPushResult No(string why, bool upgradeRequired = false) =>
        new(false, 0, 0, new Dictionary<string, string>(), 0, false, upgradeRequired, why);
}

/// <summary>پاسخِ <c>GET /api/sync/v1/pull</c>.</summary>
public sealed record SyncPullResult(bool Ok, IReadOnlyList<IncomingOp> Ops, long Cursor,
                                    bool HasMore, string Why)
{
    public static SyncPullResult No(string why) =>
        new(false, Array.Empty<IncomingOp>(), 0, false, why);
}

/// <summary>یک اعلانِ مدیر، همان‌طور که سرور می‌دهد.</summary>
public sealed record CloudNotice(string Id, string Title, string Body, long At, bool Read);

/// <summary>پاسخِ تپش — «اشتراکِ من» از همین می‌خواند.</summary>
public sealed record HeartbeatResult(bool Ok, PumpSubscription Subscription, int Unread,
                                     string Color, string Label, string Why)
{
    public static HeartbeatResult No(string why) =>
        new(false, PumpSubscription.None, 0, "", "", why);
}

/// <summary>
/// ══ نیمهٔ برنامه از VILL3N Sync v1 ═══════════════════════════════════════
///
/// بندِ ۲۰ پرامپت، سمتِ کامپیوتر. شرحِ کامل و هر انحراف با دلیلش:
/// <c>native/docs/SYNC-fa.md</c>.
///
/// ── کدام توکن ─────────────────────────────────────────────────────────
/// نگهبانِ سرور (<c>lib/sync-v1-auth.js</c>) هر دو را می‌پذیرد و هر دو به
/// <b>همان</b> پمپ می‌رسند: توکنِ <b>دستگاه</b> (از کدِ شش‌رقمی یا
/// <c>device/bind</c>) و توکنِ <b>حساب</b>. این‌جا اولی جلوتر است، چون
/// نود روز تازه‌سازی نمی‌خواهد و کامپیوترِ پمپ ممکن است ماه‌ها کسی
/// واردش نشود. حساب جانشینِ آن است، برای نصبی که هنوز بند نشده.
///
/// ⛔ <b>نشانی همچنان قفل است</b> — همه‌چیز از <c>CloudConfig.Url</c> می‌رود
/// و هیچ کادرِ متنی‌ای در کار نیست (<c>CloudAddressLockTests</c>).
/// </summary>
public sealed partial class CloudLink
{
    /// <summary>زیرِ همهٔ مسیرهای همگام‌سازی.</summary>
    private const string SyncRoot = "/api/sync/v1";

    /// <summary>
    /// فرستندهٔ جدا — با بازکردنِ خودکارِ gzip و مهلتِ بلندتر.
    ///
    /// ⚠️ <c>snapshot</c> عکسِ کلِ دفتر است و چند مگابایت می‌شود؛ بیست
    /// ثانیهٔ <see cref="Http"/> روی اینترنتِ کند کم می‌آورد. و سرور فقط
    /// وقتی فشرده می‌فرستد که <c>Accept-Encoding: gzip</c> ببیند، که این
    /// دسته خودش می‌گذارد.
    /// </summary>
    private static readonly HttpClient SyncHttp = new(new HttpClientHandler
    {
        AutomaticDecompression = System.Net.DecompressionMethods.GZip
                               | System.Net.DecompressionMethods.Deflate,
    })
    { Timeout = TimeSpan.FromMinutes(2) };

    /// <summary>
    /// توکنی که همگام‌سازی با آن حرف می‌زند — خالی یعنی هنوز هیچ‌کدام.
    /// </summary>
    internal string SyncToken =>
        !string.IsNullOrWhiteSpace(_settings.CloudDeviceToken) ? _settings.CloudDeviceToken
        : !string.IsNullOrWhiteSpace(_settings.CloudAccountToken) ? _settings.CloudAccountToken
        : "";

    /// <summary>همگام‌سازی اصلاً می‌تواند شروع شود؟</summary>
    public bool CanSync => SyncToken.Length > 0;

    // ── Push ───────────────────────────────────────────────────────────

    /// <summary>
    /// شناسهٔ همین <b>دفتر</b> نزدِ همگام‌سازی — <see cref="SyncDeviceFor"/>.
    /// خالی ⇒ همان <see cref="DeviceUid"/>.
    /// </summary>
    public string SyncDeviceOverride { get; set; } = "";

    /// <summary>شناسه‌ای که push و pull و status با آن می‌روند.</summary>
    public string SyncDevice => SyncDeviceOverride.Length > 0 ? SyncDeviceOverride : DeviceUid;

    /// <summary>
    /// ⛔ <b>شناسهٔ همگام‌سازی مالِ دفتر است، نه مالِ کامپیوتر.</b>
    ///
    /// سرور opهای خودِ همان دستگاه را در pull برنمی‌گرداند (پژواک نشود). و
    /// <see cref="DeviceUid"/> از نامِ کامپیوتر و کاربر و پوشهٔ نصب ساخته
    /// می‌شود — پس پس از «از نو و خالی»ِ حذفِ برنامه، یا ویندوزِ تازه روی
    /// همان کامپیوتر، نصبِ تازه <b>همان شناسه</b> را می‌گرفت و سرور هیچ‌کدام
    /// از ردیف‌های قبلیِ همین حساب را به او نمی‌داد: دفترِ خالی، برای همیشه.
    /// سنجهٔ `tensync` (۱۴۰۵/۰۷/۱۴، سرورِ حسابِ واقعی) گرفتش.
    ///
    /// ⚠️ نصبی که از قبل همگام شده همان شناسهٔ ثبت‌شده‌اش را نگه می‌دارد
    /// (<c>SyncState.DeviceId</c>) — عوض شدنش یعنی opهای خودش دوباره
    /// برمی‌گشتند و می‌توانستند ویرایشِ تازه‌ترِ همین‌جا را بپوشانند. فقط دفترِ
    /// تازه (که هنوز هیچ‌وقت نفرستاده) شناسهٔ تازه می‌گیرد، از ریشهٔ تصادفیِ
    /// خودِ همان دفتر (<c>UidSeed</c>).
    /// </summary>
    public static string SyncDeviceFor(string stateDeviceId, string uidSeed, string deviceUid)
    {
        if (!string.IsNullOrWhiteSpace(stateDeviceId)) return stateDeviceId.Trim();
        var seed = (uidSeed ?? "").Trim().Trim('-');
        if (seed.Length < 10) return deviceUid;
        return deviceUid + "-" + seed[^10..].ToLowerInvariant();
    }

    /// <summary>
    /// یک دستهٔ opها را می‌فرستد.
    ///
    /// ⚠️ <b>۴۲۶ خطا نیست، یک حالِ واقعی است</b>: نسخهٔ برنامه از سرور
    /// جلوتر است و سرور <b>هیچ چیزی</b> ننشانده. opها باید همان‌جا بمانند
    /// تا سرور به‌روز شود؛ دور ریختنشان یعنی دادهٔ گم‌شده.
    /// </summary>
    public async Task<SyncPushResult> SyncPushAsync(IReadOnlyList<SyncOp> ops, int queued,
                                                    CancellationToken ct = default)
    {
        var token = SyncToken;
        if (token.Length == 0) return SyncPushResult.No("هنوز به سرورِ حساب بند نشده‌ایم");
        if (ops.Count == 0) return new SyncPushResult(true, 0, 0, new Dictionary<string, string>(), 0, false, false, "");

        var body = new
        {
            device_id = SyncDevice,
            schema_version = PumpYaqobi.Persistence.OpLog.SchemaVersion,
            queued,
            ops = ops.Select(Wire).ToArray(),
        };

        var res = await SendSync(Build(HttpMethod.Post, SyncRoot + "/push", body, token), ct);
        if (!res.Ok)
        {
            //  سرور صریح گفته «برنامه جلوتر است»
            if (res.Status == 426) return SyncPushResult.No(res.Why, upgradeRequired: true);
            return SyncPushResult.No(res.Why);
        }

        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        if (res.Json.TryGetProperty("results", out var arr) && arr.ValueKind == JsonValueKind.Array)
            foreach (var r in arr.EnumerateArray())
            {
                var id = Str(r, "op_id");
                if (id.Length == 0) continue;
                var status = Str(r, "status");
                //  دلیلِ رد مهم‌تر از خودِ «rejected» است: همان در «جزئیات» دیده می‌شود
                map[id] = status == "rejected" ? (Str(r, "reason") is { Length: > 0 } why ? why : "rejected") : status;
            }

        return new SyncPushResult(
            true,
            (int)Num(res.Json, "applied"),
            Num(res.Json, "cursor"),
            map,
            (int)Num(res.Json, "schema_version"),
            res.Json.TryGetProperty("upgrade_available", out var up) && up.ValueKind == JsonValueKind.True,
            false,
            "");
    }

    /// <summary>
    /// یک op به شکلی که سرور می‌خواند.
    ///
    /// ⚠️ <c>hash</c> <b>فرستاده نمی‌شود</b> و این عمدی است: سرور آن را
    /// اختیاری گرفته و ناجورش کلِ op را رد می‌کند
    /// (<c>hash_mismatch</c>). اثرِ انگشتِ جاوااسکریپت و دات‌نت روی عددِ
    /// اعشاری مو‌به‌مو یکی نیست، پس فرستادنش فقط ریسکِ دادهٔ گم‌شده بود.
    /// </summary>
    private static object Wire(SyncOp op) => new
    {
        op_id = op.OpId,
        table = op.TableName,
        row_id = op.RowUid,
        type = op.OpType,
        ts = op.ClientTs,
        fields = Fields(op.FieldsJson),
    };

    private static JsonElement Fields(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
            return doc.RootElement.Clone();
        }
        catch
        {
            using var doc = JsonDocument.Parse("{}");
            return doc.RootElement.Clone();
        }
    }

    // ── Pull ───────────────────────────────────────────────────────────

    /// <summary>
    /// تغییرهای <b>دستگاه‌های دیگر</b> پس از cursor.
    /// ⚠️ سرور opهای خودِ همین دستگاه را برنمی‌گرداند — پس پژواکی در کار
    /// نیست و لازم نیست برنامه خودش فیلتر کند.
    /// </summary>
    public async Task<SyncPullResult> SyncPullAsync(long since, CancellationToken ct = default)
    {
        var token = SyncToken;
        if (token.Length == 0) return SyncPullResult.No("هنوز به سرورِ حساب بند نشده‌ایم");

        var path = $"{SyncRoot}/pull?device_id={Uri.EscapeDataString(SyncDevice)}&since={since}&limit=500";
        var res = await SendSync(Build(HttpMethod.Get, path, null, token), ct);
        if (!res.Ok) return SyncPullResult.No(res.Why);

        var ops = new List<IncomingOp>();
        if (res.Json.TryGetProperty("ops", out var arr) && arr.ValueKind == JsonValueKind.Array)
            foreach (var o in arr.EnumerateArray())
            {
                var id = Str(o, "op_id");
                var table = Str(o, "table");
                var row = Str(o, "row_id");
                if (id.Length == 0 || table.Length == 0 || row.Length == 0) continue;
                var fields = o.TryGetProperty("fields", out var f) ? f.Clone() : Fields("{}");
                ops.Add(new IncomingOp(id, table, row, Str(o, "type"), fields, Num(o, "server_seq")));
            }

        return new SyncPullResult(true, ops, Num(res.Json, "cursor"),
            res.Json.TryGetProperty("has_more", out var more) && more.ValueKind == JsonValueKind.True, "");
    }

    // ── Snapshot ───────────────────────────────────────────────────────

    /// <summary>
    /// عکسِ کلِ دفترِ این پمپ — فقط برای نصبِ تازه یا «بازیابی از سرور».
    /// خروجی همان JSONِ خامِ سرور است تا لایهٔ بالاتر خودش بنشاندش.
    /// </summary>
    public async Task<(bool Ok, JsonElement Json, string Why)> SyncSnapshotAsync(CancellationToken ct = default)
    {
        var token = SyncToken;
        if (token.Length == 0) return (false, default, "هنوز به سرورِ حساب بند نشده‌ایم");

        var req = Build(HttpMethod.Get, SyncRoot + "/snapshot", null, token);
        req.Headers.TryAddWithoutValidation("Accept-Encoding", "gzip");
        var res = await SendSync(req, ct);
        return res.Ok ? (true, res.Json, "") : (false, default, res.Why);
    }

    /// <summary>حالِ همین دستگاه روی سرور — برای صفحهٔ «جزئیاتِ همگام‌سازی».</summary>
    public async Task<(bool Ok, long Head, int Devices, int Conflicts, string Why)>
        SyncStatusAsync(CancellationToken ct = default)
    {
        var token = SyncToken;
        if (token.Length == 0) return (false, 0, 0, 0, "هنوز به سرورِ حساب بند نشده‌ایم");

        var path = $"{SyncRoot}/status?device_id={Uri.EscapeDataString(SyncDevice)}";
        var res = await SendSync(Build(HttpMethod.Get, path, null, token), ct);
        return res.Ok
            ? (true, Num(res.Json, "head"), (int)Num(res.Json, "devices"), (int)Num(res.Json, "conflicts"), "")
            : (false, 0, 0, 0, res.Why);
    }

    // ── تپش و اعلان ────────────────────────────────────────────────────

    /// <summary>
    /// ══ «اشتراکِ من» — بندِ ۲۰٫۷ ═══════════════════════════════════════
    ///
    /// یک درخواستِ ارزان که همهٔ آن‌چه صفحهٔ اشتراک لازم دارد را یک‌جا
    /// می‌دهد. عوض شدنِ پلن در پنل، همین‌جا و همان لحظه دیده می‌شود.
    ///
    /// ⛔ <b>هیچ قیمتی از این‌جا نمی‌آید و نباید بیاید.</b> قیمت فقط در
    /// صفحهٔ پلن‌ها و فقط از سرور — همان قاعدهٔ همیشگی.
    ///
    /// ⚠️ مسیرش <c>/api/pump/heartbeat</c> است، نه <c>/api/me/heartbeat</c>:
    /// آن یکی نشستِ <b>دکان</b> می‌خواهد و به توکنِ پمپ «چنین نشستی نیست»
    /// می‌دهد. همان تابعِ سرور است (<c>portal.heartbeatHandler</c>)، فقط با
    /// هویتِ بخشِ پمپ.
    /// </summary>
    public async Task<HeartbeatResult> HeartbeatAsync(CancellationToken ct = default)
    {
        if (!SignedIn) return HeartbeatResult.No("اول وارد حساب شوید");
        var res = await AccountAsync(HttpMethod.Get, "/api/pump/heartbeat", null, ct);
        if (!res.Ok) return HeartbeatResult.No(res.Why);

        var feats = new List<string>();
        if (res.Json.TryGetProperty("entitlement", out var ent) && ent.ValueKind == JsonValueKind.Object
            && ent.TryGetProperty("features", out var f) && f.ValueKind == JsonValueKind.Array)
            foreach (var x in f.EnumerateArray())
                if (x.ValueKind == JsonValueKind.String) feats.Add(x.GetString() ?? "");

        var color = "";
        var label = "";
        var sub = PumpSubscription.None;
        if (res.Json.TryGetProperty("subscription", out var s) && s.ValueKind == JsonValueKind.Object)
        {
            color = Str(s, "color");
            label = Str(s, "label");
            sub = new PumpSubscription(
                s.TryGetProperty("active", out var act) && act.ValueKind == JsonValueKind.True,
                Str(s, "source"),
                Str(s, "plan"),
                //  ⚠️ «دائمی» روزِ مانده **ندارد** و سرور آن‌جا `null` می‌دهد؛
                //  پس صفر می‌ماند و نوشتهٔ «دائمی ✓» از `Label` می‌آید. عددِ
                //  ساختگی گذاشتن یعنی شمارشِ معکوسِ دروغ.
                (int)Num(s, "daysLeft"),
                Num(s, "endsAt"),
                feats);
        }

        //  همان چیزی که بقیهٔ برنامه از `Subscription` می‌خواند
        if (sub.Active || sub.Source.Length > 0) Subscription = sub;
        Entitlements.Remember(_settings, sub, null);
        await SaveQuiet();

        return new HeartbeatResult(true, sub, (int)Num(res.Json, "unreadNotices"), color, label, "");
    }

    /// <summary>اعلان‌های مدیر — بنرِ داخلِ برنامه از همین می‌خواند.</summary>
    public async Task<(bool Ok, IReadOnlyList<CloudNotice> Notices, int Unread, string Why)>
        NoticesAsync(CancellationToken ct = default)
    {
        if (!SignedIn) return (false, Array.Empty<CloudNotice>(), 0, "اول وارد حساب شوید");
        var res = await AccountAsync(HttpMethod.Get, "/api/pump/notices?limit=20", null, ct);
        if (!res.Ok) return (false, Array.Empty<CloudNotice>(), 0, res.Why);

        var list = new List<CloudNotice>();
        if (res.Json.TryGetProperty("notices", out var arr) && arr.ValueKind == JsonValueKind.Array)
            foreach (var n in arr.EnumerateArray())
            {
                var id = Str(n, "id");
                if (id.Length == 0) continue;
                list.Add(new CloudNotice(id, Str(n, "title"), Str(n, "body"),
                    Num(n, "createdAt"), Str(n, "status") == "read"));
            }
        return (true, list, (int)Num(res.Json, "unread"), "");
    }

    /// <summary>«خواندمش» — تا بنر دوباره بالا نیاید.</summary>
    public async Task<bool> NoticeReadAsync(string id, CancellationToken ct = default)
    {
        if (!SignedIn || string.IsNullOrWhiteSpace(id)) return false;
        var res = await AccountAsync(HttpMethod.Post,
            "/api/pump/notices/" + Uri.EscapeDataString(id) + "/read", new { }, ct);
        return res.Ok;
    }

    // ── گزارشِ خطا ─────────────────────────────────────────────────────

    /// <summary>
    /// بندِ ۲۰٫۸ — خطای مهم به سرور می‌رود، <b>با اجازهٔ کاربر</b>.
    ///
    /// ⛔ هیچ دادهٔ مشتری نمی‌رود: فقط پیام، نوعِ استثنا و چند خطِ آخرِ
    /// ردپا. خودِ سرور هم هر چیزی شبیهِ ایمیل و شماره و توکن را می‌پوشاند.
    /// ⚠️ توکن اختیاری است — خطایی که سرِ خودِ ورود بیفتد نشستی ندارد و
    /// دقیقاً همانی است که باید دیده شود.
    /// </summary>
    public async Task<bool> ReportErrorAsync(string message, string stack, string logTail = "",
                                             CancellationToken ct = default)
    {
        var body = new
        {
            app = "pump",
            version = CloudConfig.ApplicationVersion,
            platform = "windows-native",
            schema_version = PumpYaqobi.Persistence.OpLog.SchemaVersion,
            device_id = DeviceUid,
            message = (message ?? "").Length > 500 ? message![..500] : message ?? "",
            stack = (stack ?? "").Length > 4000 ? stack![..4000] : stack ?? "",
            log = (logTail ?? "").Length > 4000 ? logTail![..4000] : logTail ?? "",
        };
        var token = SyncToken;
        var res = await SendSync(Build(HttpMethod.Post, "/api/errors", body,
            token.Length > 0 ? token : null), ct);
        return res.Ok;
    }

    // ── ورود با کدِ شش‌رقمیِ ایمیلی (بندِ ۲۰٫۷) ─────────────────────────
    //
    //  ⚠️ این **جانشینِ** راهِ رمز نیست، یک درِ دوم کنارِ آن است: قراردادِ
    //  ثابتِ پرامپت که هر سه برنامه با همان نوشته می‌شوند
    //  (`shop/server/src/routes/app-auth.js`). راهِ رمز و ثبت‌نامِ سه‌پله‌ای
    //  دست‌نخورده ماندند، چون مشتری‌های امروز با همان وارد می‌شوند.
    //
    //  ⛔ پاسخِ `request-code` **همیشه** ۲۰۰ است مگر سقفِ نرخ — تا کسی
    //  نتواند با آن بفهمد کدام ایمیل حساب دارد. پس برنامه هم هیچ‌وقت
    //  نمی‌گوید «چنین حسابی نیست».

    /// <summary>شناسهٔ درخواستِ کد — تا پلهٔ دوم، فقط در حافظه.</summary>
    public string CodeRequestId { get; private set; } = "";

    /// <summary>ایمیلِ پوشانده، همان‌طور که سرور برمی‌گرداند («a***@b.com»).</summary>
    public string CodeMaskedEmail { get; private set; } = "";

    /// <summary>چند ثانیه تا «دوباره بفرست» — از خودِ سرور، نه حدسِ ما.</summary>
    public int CodeResendAfter { get; private set; } = ResendWaitSeconds;

    /// <summary>پلهٔ یک: ایمیل ⇒ کدِ شش‌رقمی.</summary>
    public async Task<CloudResult> RequestCodeAsync(string email, CancellationToken ct = default)
    {
        var clean = (email ?? "").Trim();
        if (LoginRules.BadEmail(clean) is { } why) return CloudResult.No(why);
        if (TooSoon("auth/request-code", clean) is { } wait) return CloudResult.No(wait, "too_soon");

        var res = await SendFull(Build(HttpMethod.Post, "/api/auth/pump/request-code",
            new { email = clean, device_id = DeviceUid, device_name = Environment.MachineName }, null), ct);
        if (!res.Ok)
        {
            if (res.Status == 404) return await NoRouteAsync(ct);
            return CloudResult.No(res.Why, res.Code);
        }

        CodeRequestId = Str(res.Json, "request_id");
        CodeMaskedEmail = Str(res.Json, "masked_email");
        var after = (int)Num(res.Json, "resend_after");
        CodeResendAfter = after > 0 ? after : ResendWaitSeconds;
        if (CodeRequestId.Length == 0) return CloudResult.No("سرور شناسهٔ درخواست نداد");

        MailSent("auth/request-code", clean);
        return CloudResult.Done;
    }

    /// <summary>
    /// «ایمیل رفت یا نه؟» — سرور خودش می‌گوید، و همین است که کاربر را از
    /// انتظارِ کور بیرون می‌آورد.
    /// </summary>
    public async Task<(bool Ok, string State, string Reason, int CanResendIn)>
        RequestStatusAsync(CancellationToken ct = default)
    {
        if (CodeRequestId.Length == 0) return (false, "", "", 0);
        var res = await SendFull(Build(HttpMethod.Get,
            "/api/auth/pump/request-status?request_id=" + Uri.EscapeDataString(CodeRequestId), null, null), ct);
        if (!res.Ok) return (false, "", res.Why, 0);
        return (true, Str(res.Json, "state"), Str(res.Json, "reason"), (int)Num(res.Json, "can_resend_in"));
    }

    /// <summary>
    /// پلهٔ دو: کد ⇒ نشست.
    /// ⚠️ ثبت‌نام و ورود یکی‌اند — حسابِ نبوده همین‌جا ساخته می‌شود، پس
    /// کاربر رمزی نمی‌سازد و رمزی هم گم نمی‌کند.
    /// </summary>
    public async Task<CloudResult> VerifyCodeAsync(string code, CancellationToken ct = default)
    {
        if (CodeRequestId.Length == 0) return CloudResult.No("اول کد را بخواهید");
        var digits = LoginRules.Digits(code);
        if (digits.Length != 6) return CloudResult.No("کد باید شش رقم باشد");

        var res = await SendFull(Build(HttpMethod.Post, "/api/auth/pump/verify",
            new { request_id = CodeRequestId, code = digits, device_id = DeviceUid, device_name = Environment.MachineName },
            null), ct);
        if (!res.Ok)
        {
            if (res.Status == 404) return await NoRouteAsync(ct);
            return CloudResult.No(res.Why, res.Code);
        }

        CodeRequestId = "";
        return await SeatAsync(res.Json);
    }

    // ── فرستنده ────────────────────────────────────────────────────────

    /// <summary>
    /// مثلِ <see cref="SendFull"/>، ولی روی فرستندهٔ همگام‌سازی.
    ///
    /// ⚠️ <see cref="TestTransport"/> این‌جا هم جلوتر است، وگرنه سنجه‌ها
    /// به شبکهٔ واقعی می‌رفتند.
    /// </summary>
    private static async Task<CloudReply> SendSync(HttpRequestMessage req, CancellationToken ct)
    {
        if (TestTransport is not null) return await SendFull(req, ct);
        return await SendOn(SyncHttp, req, ct);
    }
}
