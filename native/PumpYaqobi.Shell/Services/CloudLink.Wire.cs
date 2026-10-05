using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using PumpYaqobi.Application.Localization;
using PumpYaqobi.Domain;

namespace PumpYaqobi.App.Services;

/// <summary>تکه‌ای از <see cref="CloudLink"/> — گفت‌وگوی خام با سرور و خواندنِ پاسخ.</summary>
public sealed partial class CloudLink
{
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
            var v = CloudConfig.ApplicationVersion;
            req.Headers.TryAddWithoutValidation("User-Agent", "PumpYaqobi/" + v);
            req.Headers.TryAddWithoutValidation("X-App-Version", v);
            req.Headers.TryAddWithoutValidation("X-App-Platform", "windows-native");
            //  ⚠️ سرورِ مرکزی مالِ چند برنامه است؛ بی این، لاگِ خطا نمی‌گوید
            //  کدام برنامه زده. همان `aud`ی است که مجوز هم با آن سنجیده
            //  می‌شود — نه یک نامِ تازه، و نه چیزی که کاربر را بشناساند.
            req.Headers.TryAddWithoutValidation("X-App-Id", CloudConfig.ApplicationId);
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
        var req = new HttpRequestMessage(method, CloudConfig.Url(path));
        if (body is not null) req.Content = JsonContent.Create(body, options: WireJson);
        if (!string.IsNullOrWhiteSpace(token)) req.Headers.Add("Authorization", $"Bearer {token}");
        Stamp(req);
        return req;
    }

    /// <summary>
    /// همان پیش‌فرضِ <c>JsonContent</c> (نام‌های camelCase)، فقط نویسهٔ فارسی خودش
    /// می‌رود نه <c>\uXXXX</c>. ⚠️ معنای JSON یکی است؛ بدنه ولی تا سه برابر کوچک‌تر —
    /// و سرورِ حساب بدنهٔ بیش از دو مگابایت را رد می‌کند (سنجهٔ ده‌ساله گرفتش).
    /// </summary>
    private static readonly System.Text.Json.JsonSerializerOptions WireJson =
        new(System.Text.Json.JsonSerializerDefaults.Web)
        {
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };

    private static Task<CloudReply> SendFull(HttpRequestMessage req, CancellationToken ct) =>
        SendOn(Http, req, ct);

    /// <summary>
    /// همان فرستنده، روی یک <see cref="HttpClient"/>ِ دلخواه.
    ///
    /// ⚠️ نیمهٔ همگام‌سازی فرستندهٔ خودش را دارد (gzip و مهلتِ بلندتر) ولی
    /// باید <b>دقیقاً همین</b> خواندنِ پاسخ و همین تصمیمِ «وصل‌ایم یا نه» را
    /// داشته باشد — وگرنه چراغِ سرورِ حساب دو حقیقتِ جدا پیدا می‌کرد.
    /// </summary>
    private static async Task<CloudReply> SendOn(HttpClient client, HttpRequestMessage req, CancellationToken ct)
    {
        using var _ = req;
        try
        {
            var sent = PumpYaqobi.Domain.AppClock.MonoSource();
            using var res = TestTransport is null
                ? await client.SendAsync(req, ct)
                : await TestTransport(req, ct);
            //  ⛔ ساعتِ واقعی از همین پاسخ (سرآیندِ Date) — ‎TimeSync‎
            TimeSync.From(req, res, sent);
            var text = await res.Content.ReadAsStringAsync(ct);
            var status = (int)res.StatusCode;
            JsonElement json = default;
            try
            {
                using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(text) ? "{}" : text);
                json = doc.RootElement.Clone();
            }
            catch { /* پاسخِ بی‌شکل */ }

            if (res.IsSuccessStatusCode)
            {
                NoteOnline();
                return new CloudReply(true, json, "", "", status);
            }

            var why = "";
            var code = "";
            if (json.ValueKind == JsonValueKind.Object && json.TryGetProperty("error", out var e))
            {
                why = Str(e, "message");
                code = Str(e, "code");
                //  ⛔ درگاهِ سرورِ خانگی وقتی خودِ سرورِ حساب روشن نیست، ۵۰۳ی
                //  با شکلِ خودمان می‌دهد (`account_server_down`) — و این یعنی
                //  **نرسیدیم**، نه «وصل‌ایم». با عکس دیده شد (۱۴۰۵/۰۷/۰۲):
                //  چراغِ دوم سبز بود در حالی که هر ورودی همین خطا را می‌گرفت.
                if (IsDownCode(code))
                    NoteOffline(why.Length > 0 ? why : "سرورِ حساب روی سرورِ خانگی روشن نیست");
                else
                    //  ⚠️ خطای **خودِ سرورِ ما** هم یعنی وصل‌ایم: «رمز غلط» یا
                    //  «این مسیر وجود ندارد» را فقط سرورِ ما به این شکل می‌گوید.
                    NoteOnline();
            }
            else
            {
                //  ⛔ پاسخی که شکلِ خطای ما را ندارد، از سرورِ ما نیامده —
                //  پروکسی، تونل، یا دامنه‌ای که به سرورِ حساب نمی‌رسد. چراغ
                //  نباید این را «وصل» بشمارد. (همان ۴۰۴ِ بی‌بدنهٔ ۱۴۰۵/۰۷/۰۱.)
                NoteOffline($"پاسخِ ناشناس از نشانیِ سرورِ حساب ({status})");
            }
            //  ⚠️ **۴۲۹ همیشه پیامِ خودمان را می‌گیرد، حتی اگر سرور متنی
            //  داده باشد.** متنِ سرور («تعداد درخواست بیش از حد مجاز است»)
            //  درست است ولی نمی‌گوید کاربر باید چه کند؛ و بی این، پیام
            //  «سرور جواب نداد (429)» می‌شد و کاربر فکر می‌کرد برنامه خراب
            //  است (با عکس دیده شد، ۱۴۰۵/۰۶/۳۰).
            if (status == 429)
                why = "تلاشِ زیاد — چند دقیقه صبر کنید و دوباره بزنید";

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
            //  ⛔ سرور گفت «این پمپ خدماتِ سرور ندارد» ⇒ اگر کدِ بی‌اینترنتِ وی‌آی‌پی این‌جاست،
            //  یک بار دیگر به سرور برود (‎CloudLink.License‎ ⇒ ‎NoteServicesDenied‎)
            if (code is "plan_no_services" or "subscription_required") NoteServicesDenied();
            return new CloudReply(false, json, why, code, status);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            //  ⚠️ `HttpClient.Timeout` هم همین را پرت می‌کند. جدا کردنش از
            //  «کاربر خودش لغو کرد» مهم است، وگرنه تایم‌اوت «لغو شد» دیده
            //  می‌شد.
            NoteOffline("سرور دیر جواب داد");
            return CloudReply.Fail("سرور دیر جواب داد — دوباره بزنید", "timeout");
        }
        catch (OperationCanceledException) { return CloudReply.Fail("لغو شد", "cancelled"); }
        catch (HttpRequestException)
        {
            NoteOffline("به سرورِ حساب نرسیدیم — اینترنت یا نشانی");
            return CloudReply.Fail("به سرور نرسیدیم — اینترنت را بررسی کنید", "offline");
        }
        //  ⚠️ پیامِ خامِ استثنا به کاربر نشان داده نمی‌شود: ممکن است نشانی،
        //  نامِ میزبان یا جزئیاتِ TLS داشته باشد.
        catch
        {
            NoteOffline("ارتباط با ابر برقرار نشد");
            return CloudReply.Fail("ارتباط با سرور برقرار نشد", "error");
        }
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

    // ── درخواست با توکنِ دستگاه — و «این دستگاه از پمپ جدا شده» ──────────
    //
    //  ⛔ تا امروز اگر مدیر یا صاحبِ پمپ این کامپیوتر را از «دستگاه‌ها» جدا
    //  می‌کرد، هر درخواستِ دستگاه ۴۰۱ می‌گرفت و برنامه **برای همیشه** همان
    //  توکنِ مرده و مجوزِ کهنه را نگه می‌داشت: پروفایل «فعال» می‌گفت،
    //  ارفاق جلو می‌رفت و هیچ‌کس نمی‌فهمید چرا هیچ چیزی به سرور نمی‌رسد.
    //
    //  ⚠️ **محافظه‌کار است و باید بماند**: فقط با همین دو کدِ صریحِ سرور
    //  (`device_not_registered` · `device_revoked`)، و فقط توکنِ دستگاه و
    //  مجوز پاک می‌شوند. ⛔ خطای شبکه، تایم‌اوت، ۵۰۰ و هر ۴۰۱ِ بی‌کد هیچ
    //  چیزی را پاک نمی‌کنند — بی‌اینترنت نباید کسی را از پمپش جدا کند. و
    //  ⛔ یک بیت از دفتر، شناسهٔ پمپ و کلیدِ قفل‌شده دست نمی‌خورد: ورودِ
    //  دوباره یا کدِ تازه همان‌جا را از نو بند می‌کند.

    private static readonly string[] DetachedCodes = { "device_not_registered", "device_revoked" };

    /// <summary>
    /// چرا این دستگاه دیگر به پمپ بند نیست — خالی یعنی مشکلی نیست. ایستا،
    /// همان الگوی <see cref="LastBindWhy"/>: هر درخواست نمونهٔ تازه می‌سازد.
    /// </summary>
    public static string DeviceDetachedWhy { get; private set; } = "";

    private async Task<(bool, JsonElement, string, string)> DevSendAsync(
        HttpMethod method, string path, object? body, CancellationToken ct)
    {
        var used = _settings.CloudDeviceToken;
        var r = await SendFull(Build(method, path, body, used), ct);
        if (!r.Ok && (r.Status is 401 or 403) && Array.IndexOf(DetachedCodes, r.Code) >= 0 && Activated)
            await DetachDeviceAsync(used);
        return (r.Ok, r.Json, r.Why, r.Code);
    }

    private Task<(bool, JsonElement, string, string)> DevPostAsync(string path, object body, CancellationToken ct) =>
        DevSendAsync(HttpMethod.Post, path, body, ct);

    private Task<(bool, JsonElement, string, string)> DevPutAsync(string path, object body, CancellationToken ct) =>
        DevSendAsync(HttpMethod.Put, path, body, ct);

    private Task<(bool, JsonElement, string, string)> DevGetAsync(string path, CancellationToken ct) =>
        DevSendAsync(HttpMethod.Get, path, null, ct);

    /// <summary>فقط توکنِ دستگاه و مجوز — شرحش بالای <see cref="DetachedCodes"/>.</summary>
    private async Task DetachDeviceAsync(string failed)
    {
        //  ⛔ **توکنِ کهنه، توکنِ تازه را پاک نکند** (۱۴۰۵/۰۷/۱۴، سنجهٔ `oldacct`
        //  روی پشتهٔ واقعی): بند شدنِ دوباره (`BindAsync`) توکنِ تازه می‌گیرد و
        //  سرور همان لحظه توکنِ قبلی را باطل می‌کند. بخشِ دیگری از برنامه که هنوز
        //  توکنِ قبلی را در دست داشت «ثبت نشده» می‌گرفت و **توکنِ تازه را روی
        //  دیسک خالی می‌کرد** — یعنی کامپیوتری که همین حالا وصل شده بود دوباره
        //  «فعال نشده» می‌شد. حالا اگر دیسک توکنِ دیگری دارد، همان برداشته می‌شود.
        try
        {
            var disk = AppSettings.Load();
            if (!string.IsNullOrWhiteSpace(disk.CloudDeviceToken)
                && !string.Equals(disk.CloudDeviceToken, failed, StringComparison.Ordinal))
            {
                _settings.CloudDeviceToken = disk.CloudDeviceToken;
                _settings.CloudLicense = disk.CloudLicense;
                _settings.CloudStationId = disk.CloudStationId;
                _settings.CloudStationCode = disk.CloudStationCode;
                return;
            }
        }
        catch { /* خواندنِ دیسک نشد ⇒ همان رفتارِ همیشگی */ }

        _settings.CloudDeviceToken = "";
        _settings.CloudLicense = "";
        Subscription = PumpSubscription.None;
        DeviceDetachedWhy = "این دستگاه از پمپ جدا شده است — برای وصلِ دوباره وارد حساب شوید "
                          + "یا از صاحبِ پمپ بخواهید دوباره اجازه‌اش را بدهد.";
        await SaveQuiet();
    }

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
            source is "subscription" or "trial", source, plan, days, endsAt, feats)
        { TrialNote = TrialNoteOf(source, ent) };
    }

    /// <summary>
    /// ‎trial.enabled = false‎ ⇒ «روی سرور خاموش است»؛ ‎used/consumed‎ ⇒ «مصرف
    /// شده». فقط وقتی سرور هیچ اشتراک و آزمایشی نمی‌گوید.
    /// </summary>
    public static string TrialNoteOf(string source, JsonElement ent)
    {
        if (source is "subscription" or "trial") return "";
        if (!ent.TryGetProperty("trial", out var t) || t.ValueKind != JsonValueKind.Object) return "";
        if (t.TryGetProperty("enabled", out var en) && en.ValueKind == JsonValueKind.False)
            return "دورهٔ آزمایشی روی سرورِ حساب خاموش است (در پنل: اشتراک‌ها ← پلن‌ها و قیمت‌ها ← «دورهٔ آزمایشیِ حسابِ تازه» صفر است)";
        var consumed = t.TryGetProperty("consumed", out var c) && c.ValueKind == JsonValueKind.True;
        if (consumed) return "دورهٔ آزمایشیِ این پمپ با نخستین اشتراکش تمام شده";
        var ends = Num(t, "endsAt");
        return ends > 0
            ? "دورهٔ آزمایشیِ این پمپ " + Shamsi.Of(DateTimeOffset.FromUnixTimeMilliseconds(ends).LocalDateTime) + " تمام شده"
            : "";
    }

    private static string StationId(JsonElement json) =>
        json.ValueKind == JsonValueKind.Object
        && json.TryGetProperty("station", out var st)
        && st.ValueKind == JsonValueKind.Object
            ? Str(st, "id") : "";

    /// <summary>
    /// کدِ پمپِ همین حساب — همان ‎station.code‎ی سرورِ حساب. ⛔ از امروز همین
    /// کدِ پوشهٔ سرورِ خانگی هم هست (‎StationLink.CodeFor‎)، پس «هر حساب،
    /// پوشهٔ خودش» روی یک عددِ یکتای سرور بند است، نه روی یک پیش‌فرضِ مشترک.
    /// </summary>
    private static string StationCodeOf(JsonElement json) =>
        json.ValueKind == JsonValueKind.Object
        && json.TryGetProperty("station", out var st)
        && st.ValueKind == JsonValueKind.Object
            ? AcctLive.CloudCode(Str(st, "code")) : "";

    private static string Str(JsonElement e, string k) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(k, out var v)
        && v.ValueKind == JsonValueKind.String ? (v.GetString() ?? "") : "";

    /// <summary>
    /// شناسه — چه سرور رشته بدهد چه عدد.
    ///
    /// ⚠️ <c>users.id</c>ی ابر امروز <c>text</c> است (سنجیده شد، نه حدس:
    /// <c>shop/server/migrations/001_core.sql</c>)، ولی مقایسهٔ «همان حساب
    /// است؟» چیزی است که خرابیِ بی‌صدا می‌دهد، پس عددی بودنِ روزی‌اش هم
    /// از همین‌جا می‌گذرد.
    /// </summary>
    private static string Ident(JsonElement e, string k)
    {
        if (e.ValueKind != JsonValueKind.Object || !e.TryGetProperty(k, out var v)) return "";
        return v.ValueKind switch
        {
            JsonValueKind.String => v.GetString() ?? "",
            JsonValueKind.Number => v.GetRawText(),
            _ => "",
        };
    }

    private static long Num(JsonElement e, string k) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(k, out var v)
        && v.ValueKind == JsonValueKind.Number && v.TryGetInt64(out var n) ? n : 0;

    private async Task SaveQuiet()
    {
        try { await _save(); } catch { /* ذخیره نشدنِ تنظیمات نباید کار را بشکند */ }
    }
}
