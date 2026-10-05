using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using PumpYaqobi.Application.Localization;
using PumpYaqobi.Domain;

namespace PumpYaqobi.App.Services;

/// <summary>تکه‌ای از <see cref="CloudLink"/> — خبرها، پشتیبانِ ابری، پشتیبانی، چتِ کیو‌آر و کدِ پمپ.</summary>
public sealed partial class CloudLink
{
    // ══ خبرها ═══════════════════════════════════════════════════════════
    //
    // ⛔ چیزی که تا امروز نبود:
    //
    // خواستهٔ صاحبِ ریپوی shop: «برنامه‌ها جوری باشند که بسته هم باشند،
    // هر اتفاقی که در برنامه بیفتد به سرور برود و سرور وقتی برنامه‌ها
    // بسته هم هستند پیام را برایشان بدهد.»
    //
    // بخشِ دکان این را از روزِ اول داشت (`/api/events`). پمپ نداشت:
    // «اضافه برد» و «کم مانده» فقط روی سرورِ **خانگی** می‌نشستند و
    // گوشیِ کارمند هر پانزده دقیقه از **همان شبکه** می‌پرسید. پس
    // صاحبِ پمپی که بیرون بود — یا مودمش خاموش بود — هیچ‌وقت خبر
    // نمی‌گرفت.
    //
    // ⚠️ فهرست از همان `StationSnapshot.Alerts` می‌آید و جای دیگری
    // ساخته نمی‌شود؛ وگرنه روزی کارتِ قرض‌دار سرخ است و گوشی ساکت.

    /// <summary>
    /// فرستادنِ یک دسته خبر به دفترِ ابریِ همین پمپ.
    /// </summary>
    /// <remarks>
    /// ⚠️ <b>کلیدِ هر خبر (<c>clientId</c>) لازم است.</b> سرور با همان
    /// ردیفِ تکراری نمی‌سازد، پس صفی که دو بار برسد دو زنگ نمی‌زند و
    /// خبرِ دیروز فردا دوباره بالا نمی‌آید.
    ///
    /// ⚠️ هیچ‌وقت استثنا بیرون نمی‌دهد — خبر رفاه است، دفتر اصل.
    /// </remarks>
    public async Task<CloudResult> SendEventsAsync(
        IEnumerable<object> events, CancellationToken ct = default)
    {
        if (!Activated) return CloudResult.No("فعال نشده", "not_activated");
        var list = events?.ToList() ?? new List<object>();
        if (list.Count == 0) return CloudResult.Done;

        var (ok, _, why, code) = await DevPostAsync("/api/pump/device/events",
            new { events = list }, ct);
        return ok ? CloudResult.Done : CloudResult.No(why, code);
    }

    /// <summary>
    /// ══ حالِ زندهٔ پمپ ⇒ سرورِ حساب ⇒ بات ══════════════════════════════════
    ///
    /// <para>
    /// <b>همهٔ</b> هشدارهای بازِ همین حالا (نه فقط تازه‌ها)، موجودیِ دو مخزن،
    /// و — اگر داده شد — خلاصهٔ حالِ قرض‌داران برای جست‌وجوی بات. سرور خودش
    /// می‌سنجد چه باز شد و چه بسته شد (<c>lib/pump-state.js</c> در ریپوی
    /// <c>shop</c>)؛ پس بستن و باز کردنِ برنامه دیگر همان هشدارها را «تازه»
    /// نمی‌کند — همان «حرف‌های تکراری»ِ بات.
    /// </para>
    /// <para>
    /// ⚠️ سرورِ کهنه این مسیر را ندارد و ‎not_found‎ می‌دهد؛ صدا‌زننده همان را
    /// می‌بیند و به <see cref="SendEventsAsync"/> برمی‌گردد.
    /// </para>
    /// </summary>
    /// <param name="owe">بدهیِ پمپ به شرکت‌ها (‎[{n, afn, usd}]‎) — ‎null‎ ⇒ همان قبلی می‌ماند.</param>
    public async Task<CloudResult> SendStateAsync(
        IEnumerable<object> alerts, object tank, IEnumerable<object>? debtors, CancellationToken ct = default,
        IEnumerable<object>? owe = null)
    {
        if (!Activated) return CloudResult.No("فعال نشده", "not_activated");
        var body = new Dictionary<string, object?> { ["alerts"] = alerts.ToList(), ["tank"] = tank };
        if (debtors is not null) body["debtors"] = debtors.ToList();
        if (owe is not null) body["owe"] = owe.ToList();
        var (ok, _, why, code) = await DevPostAsync("/api/pump/device/state", body, ct);
        return ok ? CloudResult.Done : CloudResult.No(why, code);
    }

    // ══ پشتیبانِ ابری ═══════════════════════════════════════════════════
    //
    // ⛔ چیزی که تا امروز نبود:
    //
    // `BackupPusher` هر شش ساعت پشتیبان می‌گرفت و فقط به **سرورِ خانگی**
    // می‌فرستاد. مودم که بسوزد، یا کامپیوترِ سرور که خراب شود، همان‌جا
    // با هم می‌روند — و `data/stations/<کد>/backups/` روی همان یک دیسک
    // است. قابلیتش هم `cloudbackup` نام داشت، که گمراه‌کننده بود: هیچ
    // ابری در کار نبود.
    //
    // حالا همان فایل به ابر هم می‌رود: پوشهٔ همین پمپ، سهمِ همین پمپ.
    //
    // ⚠️ بدنه **خام** است، نه JSON. پشتیبانِ یک پمپِ پنج‌ساله چند صد
    // مگابایت است؛ داخلِ JSON باید base64 می‌شد — یک‌سوم بزرگ‌تر و کلِ
    // فایل دو بار در حافظه.

    /// <summary>یک نسخهٔ پشتیبان روی پوشهٔ ابریِ همین پمپ.</summary>
    /// <param name="file">مسیرِ فایل — همان چیزی که ‎VACUUM INTO‎ ساخته</param>
    /// <remarks>
    /// ⛔ <b>فایل جریانی می‌رود، نه یک‌جا در حافظه.</b> همان قاعده‌ای که
    /// مقصدِ خانگی دارد و <c>InfraTests.Poshtiban_FileRa_YekJa_DarHafeze_Nemikhanad</c>
    /// قفلش کرده: دفترِ چندصد مگابایتیِ یک پمپِ چندساله نباید هر شش ساعت
    /// همان‌قدر رم بخواهد. یک بار همین‌جا با <c>ReadAllBytesAsync</c>
    /// نوشته شد و همان آزمون گرفتش.
    ///
    /// ⚠️ به همین دلیل مسیرِ فایل می‌گیرد، نه <c>byte[]</c>: امضایی که
    /// آرایه بخواهد، خودش دعوت به خواندنِ کلِ فایل در حافظه است.
    /// </remarks>
    public async Task<CloudResult> BackupUploadAsync(
        string file, string label = "", bool manual = false, string ext = "db",
        CancellationToken ct = default)
    {
        if (!Activated) return CloudResult.No("فعال نشده", "not_activated");
        if (string.IsNullOrWhiteSpace(file) || !File.Exists(file))
            return CloudResult.No("فایلِ پشتیبان پیدا نشد", "empty_backup");

        var path = "/api/pump/device/backups"
            + "?ext=" + Uri.EscapeDataString(ext)
            + "&kind=" + (manual ? "manual" : "auto")
            + "&label=" + Uri.EscapeDataString(label ?? "");

        await using var stream = new FileStream(
            file, FileMode.Open, FileAccess.Read, FileShare.Read,
            bufferSize: 64 * 1024, useAsync: true);
        if (stream.Length == 0) return CloudResult.No("فایلِ پشتیبان خالی است", "empty_backup");

        using var body = new StreamContent(stream, 64 * 1024);
        body.Headers.ContentType =
            new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
        body.Headers.ContentLength = stream.Length;

        using var req = new HttpRequestMessage(HttpMethod.Post, CloudConfig.Url(path)) { Content = body };
        req.Headers.Add("Authorization", $"Bearer {_settings.CloudDeviceToken}");
        //  ⛔ با مهلتِ بلند (‎BigHttp‎)، نه بیست ثانیه — شرحش بالای ‎BigHttp‎.
        var r = await SendOn(BigHttp, req, ct);
        return r.Ok ? CloudResult.Done : CloudResult.No(r.Why, r.Code);
    }

    /// <summary>
    /// ══ گرفتنِ یک پشتیبانِ ابریِ همین پمپ — به یک فایلِ محلی (۱۴۰۵/۰۷/۲۰) ══
    /// تا امروز برنامه فقط می‌فرستاد و <b>هیچ راهی برای پس گرفتن نداشت</b>.
    /// </summary>
    /// <remarks>
    /// ⛔ جریانی، و اول در ‎.part‎؛ فقط وقتی هشِ سرور (‎X-Backup-Sha256‎) خواند
    /// جای خودش می‌نشیند. فایلِ نیمه یا دست‌خورده هرگز به بازگردانی نمی‌رسد.
    /// ⛔ با توکنِ <b>دستگاهِ همین پمپ</b>: سرور فقط پشتیبانِ همین پمپ را می‌دهد.
    /// </remarks>
    public async Task<CloudResult> BackupDownloadAsync(string id, string target, CancellationToken ct = default)
    {
        if (!Activated) return CloudResult.No("فعال نشده", "not_activated");
        if (string.IsNullOrWhiteSpace(id)) return CloudResult.No("پشتیبان پیدا نشد", "not_found");
        var part = target + ".part";
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get,
                CloudConfig.Url("/api/pump/device/backups/" + Uri.EscapeDataString(id)));
            Stamp(req);
            req.Headers.Add("Authorization", $"Bearer {_settings.CloudDeviceToken}");
            using var res = TestTransport is null
                ? await BigHttp.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct)
                : await TestTransport(req, ct);
            if (!res.IsSuccessStatusCode)
                return CloudResult.No($"سرور پشتیبان را نداد ({(int)res.StatusCode})", ((int)res.StatusCode).ToString());
            var want = res.Headers.TryGetValues("X-Backup-Sha256", out var v) ? v.FirstOrDefault() ?? "" : "";

            string got;
            await using (var src = await res.Content.ReadAsStreamAsync(ct))
            await using (var dst = new FileStream(part, FileMode.Create, FileAccess.Write, FileShare.None, 64 * 1024, true))
            using (var sha = System.Security.Cryptography.IncrementalHash.CreateHash(System.Security.Cryptography.HashAlgorithmName.SHA256))
            {
                var buf = new byte[64 * 1024];
                int n;
                while ((n = await src.ReadAsync(buf, ct)) > 0)
                {
                    sha.AppendData(buf, 0, n);
                    await dst.WriteAsync(buf.AsMemory(0, n), ct);
                }
                got = Convert.ToHexString(sha.GetHashAndReset()).ToLowerInvariant();
            }
            if (new FileInfo(part).Length == 0 || (want.Length > 0 && !string.Equals(want, got, StringComparison.OrdinalIgnoreCase)))
            {
                try { File.Delete(part); } catch { }
                return CloudResult.No("فایلِ گرفته‌شده با نسخهٔ سرور نمی‌خواند — دوباره بزنید", "hash_mismatch");
            }
            File.Move(part, target, overwrite: true);
            return CloudResult.Done;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            try { File.Delete(part); } catch { }
            return CloudResult.No("سرور دیر جواب داد — دوباره بزنید", "timeout");
        }
        catch (Exception)
        {
            try { File.Delete(part); } catch { }
            return CloudResult.No("به سرور نرسیدیم — اینترنت را بررسی کنید", "offline");
        }
    }

    /// <summary>فهرستِ پشتیبان‌های ابریِ همین پمپ، تازه‌ترین اول.</summary>
    public async Task<(bool Ok, List<CloudBackup> Items, CloudBackupStats Stats, string Why)>
        BackupListAsync(CancellationToken ct = default)
    {
        if (!Activated) return (false, new(), CloudBackupStats.None, "فعال نشده");
        var (ok, json, why, _) = await DevGetAsync("/api/pump/device/backups", ct);
        if (!ok) return (false, new(), CloudBackupStats.None, why);

        var list = new List<CloudBackup>();
        if (json.TryGetProperty("backups", out var arr) && arr.ValueKind == JsonValueKind.Array)
            foreach (var b in arr.EnumerateArray())
                list.Add(new CloudBackup(Str(b, "id"), Str(b, "name"), Num(b, "bytes"),
                    Str(b, "kind"), Str(b, "label"), Num(b, "createdAt")));

        var stats = CloudBackupStats.None;
        if (json.TryGetProperty("stats", out var st) && st.ValueKind == JsonValueKind.Object)
            stats = new CloudBackupStats((int)Num(st, "count"), (int)Num(st, "keep"),
                Num(st, "usedBytes"), Num(st, "quotaBytes"), Num(st, "lastAt"),
                st.TryGetProperty("paid", out var p) && p.ValueKind == JsonValueKind.True);

        return (true, list, stats, "");
    }

    // ══ پشتیبانیِ صاحبِ پمپ ↔ مدیرِ سامانه ═══════════════════════════════
    //
    // ⚠️ **این با چتِ پایین یکی نیست و نباید قاطی شود.**
    //
    //   چتِ پایین  = مشتریِ کیو‌آر ↔ صاحبِ پمپ   (‎/chat/…‎)
    //   این یکی    = صاحبِ پمپ ↔ کسی که برنامه را ساخته (‎/support/…‎)
    //
    // ⛔ تا امروز پمپ‌داری که گیر می‌کرد **هیچ دری** نداشت: `/api/support`
    // مالِ بخشِ دکان بود و این برنامه حساب ندارد. حالا رشته به خودِ پمپ
    // بسته است (`station_id`)، پس گوشیِ صاحب و این کامپیوتر به **یک**
    // گفت‌وگو می‌رسند.
    //
    // ⛔ و هیچ‌وقت پشتِ اشتراک نمی‌رود: «پشتیبانی یکی از واجبات است.»
    // کسی که اشتراکش تمام شده، بیشتر از همه لازم دارد بپرسد چرا.

    /// <summary>گفت‌وگو با پشتیبانی — پیام‌های بعد از ‎after‎.</summary>
    public async Task<(bool Ok, List<CloudChatMessage> Messages, int Unread, string Why)>
        SupportThreadAsync(long after = 0, CancellationToken ct = default)
    {
        if (!Activated) return (false, new(), 0, "فعال نشده");
        var (ok, json, why, _) = await DevGetAsync(
            "/api/pump/device/support/thread?after=" + after, ct);
        if (!ok) return (false, new(), 0, why);

        var list = new List<CloudChatMessage>();
        if (json.TryGetProperty("messages", out var arr) && arr.ValueKind == JsonValueKind.Array)
            foreach (var m in arr.EnumerateArray())
            {
                var parsed = CloudChatMessage.ParseSupport(m);
                if (parsed is not null) list.Add(parsed);
            }

        var unread = 0;
        if (json.TryGetProperty("thread", out var th) && th.ValueKind == JsonValueKind.Object)
            unread = (int)Num(th, "unreadUser");

        return (true, list, unread, "");
    }

    /// <summary>پیام به پشتیبانی. ‎kind‎ی خالی یعنی متن؛ رسانه با ‎mediaId‎ِ <see cref="SupportUploadAsync"/>.</summary>
    public async Task<CloudResult> SupportSendAsync(string text, CancellationToken ct = default,
                                                    string kind = "", string? mediaId = null)
    {
        if (!Activated) return CloudResult.No("فعال نشده", "not_activated");
        var media = kind is "image" or "video" or "audio";
        if (!media && string.IsNullOrWhiteSpace(text)) return CloudResult.No("پیام خالی است", "empty_message");
        object body = media
            ? new { body = (text ?? "").Trim(), kind, mediaId }
            : new { body = text.Trim() };
        var (ok, _, why, code) = await DevPostAsync("/api/pump/device/support/messages", body, ct);
        return ok ? CloudResult.Done : CloudResult.No(why, code);
    }

    /// <summary>
    /// عکس/ویدیو/صدا برای پشتیبانی — خام، با نوعش (۱۴۰۵/۰۷/۱۶). ⛔ سرورِ حساب فقط
    /// رد می‌کند: همین که طرفِ دیگر گرفت پاکش می‌کند؛ نسخهٔ ماندگار روی همین
    /// کامپیوتر است (‎ChatStore‎).
    /// </summary>
    public async Task<(bool Ok, string MediaId, string Why)> SupportUploadAsync(
        byte[] bytes, string mime, CancellationToken ct = default)
    {
        if (!Activated) return (false, "", "فعال نشده");
        var req = new HttpRequestMessage(HttpMethod.Post, CloudConfig.Url("/api/pump/device/support/media"))
        {
            Content = new ByteArrayContent(bytes),
        };
        req.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(mime);
        req.Headers.Add("Authorization", $"Bearer {_settings.CloudDeviceToken}");
        var (ok, json, why, _) = await Send(req, ct);
        return ok ? (true, Str(json, "mediaId"), "") : (false, "", why);
    }

    /// <summary>رسانهٔ پشتیبانی — یک بار گرفته و این‌جا نگه داشته می‌شود (سرور پس از آن پاکش می‌کند).</summary>
    public async Task<(byte[] Bytes, string Mime)?> SupportMediaAsync(string mediaId, CancellationToken ct = default)
    {
        if (!Activated || string.IsNullOrWhiteSpace(mediaId)) return null;
        try
        {
            var req = new HttpRequestMessage(HttpMethod.Get,
                CloudConfig.Url("/api/pump/device/support/media/" + Uri.EscapeDataString(mediaId)));
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

    /// <summary>«خواندم» — نقطهٔ قرمز را پاک می‌کند.</summary>
    public async Task<bool> SupportSeenAsync(CancellationToken ct = default)
    {
        if (!Activated) return false;
        var (ok, _, _, _) = await DevPostAsync("/api/pump/device/support/read",
            new { }, ct);
        return ok;
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
        var (ok, json, why, _) = await DevGetAsync("/api/pump/device/chat/threads", ct);
        if (!ok) return (false, new(), why);
        var list = new List<CloudChatThread>();
        if (json.TryGetProperty("threads", out var arr) && arr.ValueKind == JsonValueKind.Array)
            foreach (var t in arr.EnumerateArray())
            {
                CloudChatMessage? last = t.TryGetProperty("last", out var l) && l.ValueKind == JsonValueKind.Object
                    ? CloudChatMessage.Parse(l, Str(t, "acct")) : null;
                list.Add(new CloudChatThread(Str(t, "acct"), Str(t, "name"),
                    t.TryGetProperty("blocked", out var b) && b.ValueKind == JsonValueKind.True,
                    (int)Num(t, "unread"), Num(t, "updatedAt"), last, Num(t, "custSeenSeq")));
            }
        return (true, list, "");
    }

    /// <summary>همهٔ پیام‌های تازهٔ همهٔ گفت‌وگوها بعد از ‎after‎.</summary>
    public async Task<(bool Ok, List<CloudChatMessage> Messages, string Why)> ChatInboxAsync(long after, CancellationToken ct = default)
    {
        if (!Activated) return (false, new(), "فعال نشده");
        var (ok, json, why, _) = await DevGetAsync("/api/pump/device/chat/inbox?after=" + after, ct);
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
        var (ok, json, why, _) = await DevPostAsync("/api/pump/device/chat/" + Uri.EscapeDataString(acct), body, ct);
        if (!ok) return (false, null, why);
        return (true, json.TryGetProperty("message", out var m) ? CloudChatMessage.Parse(m, acct) : null, "");
    }

    /// <summary>بالا بردنِ عکس/ویدیو/صدا — خام، با نوعش. خروجی شناسهٔ رسانه.</summary>
    public async Task<(bool Ok, string MediaId, string Why)> ChatUploadAsync(
        string acct, byte[] bytes, string mime, CancellationToken ct = default)
    {
        if (!Activated) return (false, "", "فعال نشده");
        var req = new HttpRequestMessage(HttpMethod.Post,
            CloudConfig.Url("/api/pump/device/chat/" + Uri.EscapeDataString(acct) + "/media"))
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
                CloudConfig.Url("/api/pump/device/chat/media/" + Uri.EscapeDataString(mediaId)));
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
            CloudConfig.Url("/api/pump/device/chat/message/" + Uri.EscapeDataString(messageId)));
        req.Headers.Add("Authorization", $"Bearer {_settings.CloudDeviceToken}");
        var (ok, _, why, code) = await Send(req, ct);
        return ok ? CloudResult.Done : CloudResult.No(why, code);
    }

    /// <summary>بلاک / رفعِ بلاکِ یک مشتری.</summary>
    public async Task<CloudResult> ChatBlockAsync(string acct, bool blocked, CancellationToken ct = default)
    {
        if (!Activated) return CloudResult.No("فعال نشده", "not_activated");
        var req = new HttpRequestMessage(blocked ? HttpMethod.Post : HttpMethod.Delete,
            CloudConfig.Url("/api/pump/device/chat/" + Uri.EscapeDataString(acct) + "/block"))
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
        var (ok, _, _, _) = await DevPostAsync("/api/pump/device/chat/" + Uri.EscapeDataString(acct) + "/seen",
            new { seq }, ct);
        return ok;
    }

    /// <summary>کدِ کوتاهی که کارمند با آن به این پمپ می‌پیوندد.</summary>
    public async Task<(bool Ok, string Code, string Why)> JoinCodeAsync(CancellationToken ct = default)
    {
        if (!Activated) return (false, "", "این برنامه هنوز فعال نشده است");
        var (ok, json, why, _) = await DevPostAsync("/api/pump/device/join-code",
            new { role = "staff", hours = 24, maxUses = 10 }, ct);
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
            ? await DevPostAsync("/api/pump/device/access-code/rotate", new { }, ct)
            : await DevGetAsync("/api/pump/device/access-code", ct);
        if (!ok) return (false, _settings.CloudAccessCode, why);
        var code = Str(json, "code");
        if (code.Length > 0 && code != _settings.CloudAccessCode)
        {
            _settings.CloudAccessCode = code;
            await _save();
        }
        return (true, code, "");
    }

    /*
     *  ══ کدِ هشت‌رقمیِ اپِ گوشی، بی زدنِ هیچ دکمه‌ای (۱۴۰۵/۰۷/۱۴) ═══════════
     *
     *  خواستهٔ صریحِ صاحب سامانه: «برای هر حساب کاربری یک کد هشت‌رقمی درست
     *  بشه.» سرورِ حساب (۲.۱۱.۷) کد را همان لحظهٔ ساختنِ پمپ می‌سازد؛ ولی
     *  برنامه تا امروز فقط با «گرفتنِ کد»ِ پروفایل آن را می‌پرسید، پس پروفایل
     *  «هنوز گرفته نشده» می‌گفت و نصبی که کدِ حرفیِ پیشین را نگه داشته بود
     *  هیچ‌وقت کدِ هشت‌رقمی را نمی‌دید.
     *
     *  ⛔ حلقهٔ پس‌زمینه حالا خودش می‌پرسد — **یک بار در هر اجرا**، و تا
     *  کدِ روی دیسک هشت رقم نیست هر ده دقیقه یک بار (یک GETِ سبک). هیچ کدی
     *  این‌جا ساخته یا عوض نمی‌شود؛ فقط خوانده می‌شود.
     */
    private static DateTime _codeCheckedAt = DateTime.MinValue;
    private static readonly TimeSpan CodeRecheck = TimeSpan.FromMinutes(10);

    /// <summary>کدِ امروزی: هشت رقم.</summary>
    public static bool IsDigitCode(string? code)
    {
        var c = (code ?? "").Replace("-", "").Trim();
        return c.Length == 8 && c.All(char.IsAsciiDigit);
    }

    public async Task KeepAccessCodeAsync(CancellationToken ct = default)
    {
        if (!Activated) return;
        var fresh = _codeCheckedAt != DateTime.MinValue && IsDigitCode(_settings.CloudAccessCode);
        if (fresh || AppClock.Mono - _codeCheckedAt < CodeRecheck) return;
        _codeCheckedAt = AppClock.Mono;
        try { await AccessCodeAsync(false, ct); }
        catch { /* بی‌اینترنت خطا نیست — کدِ روی دیسک سرِ جایش است */ }
    }

    /// <summary>برای نمایش: ‎4829-1736‎ — همان قاعدهٔ سرور و اپِ گوشی.</summary>
    public static string FormatAccessCode(string code)
    {
        var c = (code ?? "").Trim().ToUpperInvariant();
        return c.Length == 8 ? c[..4] + "-" + c[4..] : c;
    }
}
