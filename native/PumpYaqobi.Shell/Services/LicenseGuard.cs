using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace PumpYaqobi.App.Services;

/// <summary>نتیجهٔ سنجشِ یک مجوز.</summary>
/// <param name="Valid">آیا مجوز پذیرفته شد.</param>
/// <param name="Reason">اگر نه، چرا — به فارسی، برای نشان دادن به کاربر.</param>
/// <param name="Features">بخش‌هایی که باز می‌شوند.</param>
/// <param name="Core">بخش‌هایی که همیشه بازند، حتی بی مجوز.</param>
/// <param name="SubscriptionEndsAt">پایانِ اشتراک (میلی‌ثانیهٔ یونیکس).</param>
/// <param name="ExpiresAt">پایانِ خودِ مجوز — کوتاه‌تر از اشتراک.</param>
/// <param name="PlanTitle">نامِ پلن، برای نمایش.</param>
/// <param name="HasFeatureList">
/// آیا خودِ مجوز کلیدِ <c>feat</c> را داشت.
///
/// ⚠️ «نبودنِ فهرست» با «فهرستِ خالی» یکی نیست و نباید یکی شود:
///   • نبود  ⇒ مجوزِ نسلِ اول ⇒ پلنِ کامل (وگرنه با آمدنِ قفل، همهٔ
///             مشتری‌های امروز یک‌شبه خاموش می‌شدند).
///   • خالی  ⇒ پلنِ «پایه» ⇒ هیچ‌کدام از کارهای ابری.
/// هر دو در <see cref="Features"/> یک چیز دیده می‌شوند، پس تفاوتشان همین
/// پرچم است.
/// </param>
/// <param name="SignatureOk">
/// امضا و هر چهار قیدِ هویت (صادرکننده · شنونده · دستگاه · پمپ) سالم‌اند —
/// چه مجوز زنده باشد چه منقضی. ⛔ <b>ارفاق فقط از همین می‌آید</b>
/// (<see cref="EntitlementState.InGrace"/>): مجوزی که امضایش سالم است و
/// تازه منقضی شده، همان مشتریِ پول‌داده‌ای است که چند روز آفلاین مانده.
/// هیچ عددِ بی‌امضایی در تنظیمات دیگر نمی‌تواند چیزی را باز کند.
/// </param>
/// <param name="Expired">امضا سالم است و فقط زمانش گذشته.</param>
/// <param name="IssuedAt">
/// <c>iat</c>ِ مجوز — زمانِ <b>امضاشدهٔ</b> سرور، لنگرِ کفِ ساعت
/// (<see cref="LicenseClock.Anchor"/>).
/// </param>
public sealed record LicenseCheck(
    bool Valid,
    string Reason,
    IReadOnlyList<string> Features,
    IReadOnlyList<string> Core,
    long SubscriptionEndsAt,
    long ExpiresAt,
    string PlanTitle,
    bool HasFeatureList = false,
    bool SignatureOk = false,
    bool Expired = false,
    long IssuedAt = 0)
{
    public static LicenseCheck Fail(string why) =>
        new(false, why, Array.Empty<string>(), Array.Empty<string>(), 0, 0, "");
}

/// <summary>
/// ══ سنجشِ مجوز — چرا دور زدن آسان نیست ══════════════════════════════════════
///
/// خواستهٔ صریحِ صاحب ریپو: «کسی نتواند برنامه را کرک کند یا دور بزند.»
///
/// برنامه باید آفلاین هم بداند اشتراک باز است یا نه. اگر جواب فقط یک پرچم در
/// تنظیمات بود، هر کسی فایلِ تنظیمات را باز می‌کرد و <c>true</c> می‌نوشت.
///
/// پس سرور یک برگهٔ <b>امضاشده</b> می‌دهد و این کلاس فقط امضا را می‌سنجد.
/// ساختنِ چنین برگه‌ای بی کلیدِ خصوصیِ سرور شدنی نیست، و کلیدِ خصوصی هرگز از
/// سرور بیرون نمی‌رود.
///
/// ── پنج قیدی که با هم کار می‌کنند ──────────────────────────────────────────
///
///  ۱) <b>امضا</b> — ES256 روی منحنی P-256. بی کلیدِ خصوصی، جعل‌شدنی نیست.
///
///  ۲) <b>کلیدِ عمومی قفل می‌شود</b> — ⚠️ مهم‌ترین قید.
///     کلید را از خودِ پاسخ نمی‌گیریم و کورکورانه باور نمی‌کنیم؛ اولین باری
///     که فعال می‌شویم ذخیره‌اش می‌کنیم و از آن به بعد <b>فقط</b> همان را
///     می‌پذیریم. بی این، کسی می‌توانست سرورِ خودش را بالا بیاورد، کلیدِ
///     خودش را بدهد و مجوزِ خودش را امضا کند — و همهٔ قیدهای دیگر بی‌اثر
///     می‌شدند.
///
///  ۳) <b>شنونده</b> (<c>aud</c>) — مجوزِ بخشِ دکان روی این برنامه نمی‌نشیند.
///
///  ۴) <b>دستگاه</b> (<c>duid</c>) — کپیِ پوشهٔ برنامه روی کامپیوترِ دیگر،
///     اشتراک را با خودش نمی‌برد.
///
///  ۵) <b>پمپ</b> (<c>stn</c>) — کسی که دو پمپ را روی یک کامپیوتر اداره
///     می‌کند نمی‌تواند مجوزِ پمپِ اشتراک‌دار را روی پمپِ بی‌اشتراک بگذارد.
///
/// ── و ساعت ─────────────────────────────────────────────────────────────────
/// تا ۱۴۰۵/۰۷/۱۲ این‌جا نوشته بود «ساعت عمداً سنجیده نمی‌شود». ⛔ ولی عقب
/// بردنِ ساعتِ ویندوز مجوزِ منقضی را زنده می‌کرد و ارفاق را هم از نو شروع
/// می‌کرد. حالا «حالا»ی سنجش از <see cref="LicenseClock"/> می‌آید (کفی که
/// فقط جلو می‌رود) و ارفاق فقط از خودِ مجوزِ امضاشده
/// (<see cref="LicenseCheck.SignatureOk"/>). قفلِ اصلی همچنان روی سرور است:
/// نوشتن روی پوشهٔ ابری هر بار آن‌جا سنجیده می‌شود.
///
/// ⚠️ <see cref="Check"/> خودِ برگه را می‌سنجد؛ برای «مجوزِ ذخیره‌شدهٔ این
/// نصب» همیشه <see cref="CheckStored"/> را بزنید.
/// </summary>
public static class LicenseGuard
{
    /// <summary>ارفاقِ ساعت — یک دقیقه، مثلِ خودِ سرور.</summary>
    private const long SkewMs = 60_000;

    /// <summary>
    /// سنجشِ یک مجوز در برابرِ کلیدِ عمومیِ قفل‌شده.
    /// </summary>
    /// <param name="token">خودِ مجوز، به شکلِ <c>header.payload.signature</c>.</param>
    /// <param name="pinnedPublicKey">کلیدِ عمومیِ base64 (SPKI DER) که قبلاً قفل شده.</param>
    /// <param name="deviceUid">شناسهٔ این کامپیوتر.</param>
    /// <param name="stationId">شناسهٔ پمپی که این برنامه اداره‌اش می‌کند.</param>
    /// <param name="nowMs">حالا — تزریق‌پذیر تا آزمون بتواند زمان را جلو ببرد.</param>
    public static LicenseCheck Check(
        string? token, string? pinnedPublicKey, string deviceUid, string stationId, long nowMs)
    {
        if (string.IsNullOrWhiteSpace(token)) return LicenseCheck.Fail("مجوزی ذخیره نشده است");

        //  ⛔ ریشهٔ اعتماد: اگر کلیدهای داخلِ خودِ برنامه هست
        //  (<see cref="CloudConfig.LicenseKeys"/>) **همان** است و کلیدِ روی
        //  دیسک هیچ اثری ندارد — وگرنه یک خطِ ویرایش‌شده در `settings.json`
        //  کلِ این کلاس را بی‌اثر می‌کرد. خالی بود ⇒ همان TOFUِ همیشگی.
        var trusted = CloudConfig.LicenseKeys;
        if (trusted.Count == 0 && string.IsNullOrWhiteSpace(pinnedPublicKey))
            return LicenseCheck.Fail("کلیدِ سرور هنوز قفل نشده — یک بار آنلاین شوید");

        var parts = token.Split('.');
        if (parts.Length != 3) return LicenseCheck.Fail("شکلِ مجوز درست نیست");

        JsonElement payload;
        var kid = "";
        try
        {
            using var doc = JsonDocument.Parse(FromB64Url(parts[1]));
            payload = doc.RootElement.Clone();
            //  ⚠️ `kid` فقط **انتخابِ کلید** است، نه اعتماد: کلیدِ انتخاب‌شده
            //  باید از قبل در فهرستِ داخلِ برنامه باشد.
            using var head = JsonDocument.Parse(FromB64Url(parts[0]));
            kid = Str(head.RootElement, "kid");
        }
        catch { return LicenseCheck.Fail("مجوز خوانده نشد"); }

        // ── ۱) امضا، پیش از هر چیزِ دیگر ─────────────────────────────────
        //  ⚠️ ترتیب مهم است: تا امضا سنجیده نشده، هیچ فیلدی از payload
        //  قابلِ اعتماد نیست. سنجیدنِ اول محتوا و بعد امضا، همان اشتباهی
        //  است که قفل را عملاً برمی‌دارد.
        IEnumerable<string> keys;
        if (trusted.Count == 0) keys = new[] { pinnedPublicKey! };
        else if (kid.Length > 0)
        {
            if (!trusted.TryGetValue(kid, out var one))
                return LicenseCheck.Fail("کلیدِ امضای این مجوز برای این برنامه شناخته نیست");
            keys = new[] { one };
        }
        else keys = trusted.Values;

        var signed = Encoding.UTF8.GetBytes($"{parts[0]}.{parts[1]}");
        byte[] sig;
        try { sig = FromB64Url(parts[2]); }
        catch { return LicenseCheck.Fail("امضای مجوز سنجیده نشد"); }

        var verified = false;
        foreach (var k in keys)
        {
            try
            {
                using var ecdsa = ECDsa.Create();
                ecdsa.ImportSubjectPublicKeyInfo(Convert.FromBase64String(k), out _);
                //  امضای خام (r||s) نه DER — همان چیزی که سرور می‌سازد
                if (ecdsa.VerifyData(signed, sig, HashAlgorithmName.SHA256,
                                     DSASignatureFormat.IeeeP1363FixedFieldConcatenation))
                { verified = true; break; }
            }
            catch { /* کلیدِ خراب ⇒ کلیدِ بعدی */ }
        }
        if (!verified) return LicenseCheck.Fail("امضای مجوز درست نیست");

        // ── ۲) این مجوز مالِ همین برنامه است؟ ────────────────────────────
        if (Str(payload, "iss") != CloudConfig.Issuer) return LicenseCheck.Fail("صادرکنندهٔ مجوز ناشناس است");
        if (Str(payload, "aud") != CloudConfig.Audience) return LicenseCheck.Fail("این مجوز برای برنامهٔ دیگری است");

        // ── ۳) همین کامپیوتر؟ ────────────────────────────────────────────
        var duid = Str(payload, "duid");
        if (string.IsNullOrEmpty(duid) || !Fixed(duid, deviceUid))
            return LicenseCheck.Fail("این مجوز برای کامپیوترِ دیگری صادر شده است");

        // ── ۴) همین پمپ؟ ─────────────────────────────────────────────────
        //  خالی بودنِ `stn` در مجوزهای نسلِ اول پذیرفته می‌شود؛ اگر پر باشد
        //  باید بخورد.
        var stn = Str(payload, "stn");
        if (!string.IsNullOrEmpty(stn) && !string.IsNullOrEmpty(stationId) && !Fixed(stn, stationId))
            return LicenseCheck.Fail("این مجوز برای پمپِ دیگری صادر شده است");

        // ── ۵) هنوز زنده است؟ ───────────────────────────────────────────
        var nbf = Num(payload, "nbf");
        var exp = Num(payload, "exp");
        var hasFeat = payload.TryGetProperty("feat", out var featProp)
                      && featProp.ValueKind == JsonValueKind.Array;

        //  ⚠️ از این‌جا به بعد امضا و هویت سالم‌اند؛ پس فهرستِ پلن و زمان‌ها
        //  **حتی در شکست** پر می‌روند — ارفاق همان فهرستِ همین مجوز را
        //  اعمال می‌کند و هیچ‌وقت پلن را گشادتر نمی‌کند.
        LicenseCheck Signed(bool valid, string why, bool expired) => new(
            valid, why,
            Arr(payload, "feat"),
            Arr(payload, "core"),
            Num(payload, "sub_ends"),
            exp,
            Str(payload, "plan_title"),
            hasFeat,
            SignatureOk: true,
            Expired: expired,
            IssuedAt: Num(payload, "iat"));

        if (nbf > 0 && nowMs + SkewMs < nbf) return Signed(false, "زمانِ مجوز هنوز نرسیده است", false);
        if (exp > 0 && nowMs - SkewMs > exp) return Signed(false, "مجوز منقضی شده — یک بار آنلاین شوید", true);

        return Signed(true, "", false);
    }

    /// <summary>
    /// ══ سنجشِ مجوزِ <b>ذخیره‌شده</b> — تنها راهِ درستِ پرسیدنِ «مجوز سالم است؟» ═
    ///
    /// همان <see cref="Check"/>، به‌علاوهٔ دو قیدی که فقط روی مجوزِ روی دیسک
    /// معنا دارند:
    ///
    ///  • <b>کفِ ساعت</b> (<see cref="LicenseClock"/>) — «حالا» هرگز عقب‌تر از
    ///    بالاترین زمانی نیست که این نصب دیده، پس عقب بردنِ ساعتِ ویندوز
    ///    مجوزِ منقضی را زنده نمی‌کند.
    ///  • <b>اثرِ انگشتِ کامپیوتر</b> (<see cref="CloudConfig.MachineMoved"/>) —
    ///    تنظیماتی که از کامپیوترِ دیگری کپی شده‌اند مجوزشان را با خودشان
    ///    نمی‌برند. ⛔ و هیچ چیزی پاک نمی‌شود؛ فقط پذیرفته نمی‌شود.
    ///
    /// ⚠️ هر جایی که «مجوزِ این نصب» را می‌سنجد باید از همین در برود، نه از
    /// <see cref="Check"/>ِ خام — وگرنه یکی از این دو قید جا می‌افتد.
    /// </summary>
    public static LicenseCheck CheckStored(AppSettings f, long? nowMs = null)
    {
        if (CloudConfig.MachineMoved(f)) return LicenseCheck.Fail(CloudConfig.MachineMovedWhy);
        return Check(f.CloudLicense, f.CloudPublicKey, CloudConfig.DeviceUid(f), f.CloudStationId,
                     nowMs ?? LicenseClock.Now(f));
    }

    /// <summary>
    /// آیا این بخش باز است.
    ///
    /// بخش‌های <c>core</c> حتی با مجوزِ نامعتبر هم بازند — صاحبِ پمپ باید
    /// همیشه بتواند دفترِ خودش را ببیند. گروگان گرفتنِ داده سریع‌ترین راهِ
    /// از دست دادنِ اعتماد است.
    /// </summary>
    public static bool Allows(LicenseCheck check, string feature, IReadOnlyList<string> coreFallback)
    {
        if (coreFallback.Contains(feature)) return true;
        if (!check.Valid) return false;
        return check.Features.Contains(feature) || check.Core.Contains(feature);
    }

    // ── ریزه‌کاری‌ها ───────────────────────────────────────────────────

    private static byte[] FromB64Url(string s)
    {
        var t = s.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(t.PadRight(t.Length + (4 - t.Length % 4) % 4, '='));
    }

    /// <summary>مقایسهٔ زمان‌ثابت — تا از روی زمانِ پاسخ چیزی حدس زده نشود.</summary>
    private static bool Fixed(string a, string b)
    {
        var x = Encoding.UTF8.GetBytes(a);
        var y = Encoding.UTF8.GetBytes(b);
        return x.Length == y.Length && CryptographicOperations.FixedTimeEquals(x, y);
    }

    private static string Str(JsonElement e, string k) =>
        e.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String ? (v.GetString() ?? "") : "";

    private static long Num(JsonElement e, string k) =>
        e.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt64(out var n) ? n : 0;

    private static IReadOnlyList<string> Arr(JsonElement e, string k)
    {
        if (!e.TryGetProperty(k, out var v) || v.ValueKind != JsonValueKind.Array)
            return Array.Empty<string>();
        var list = new List<string>();
        foreach (var item in v.EnumerateArray())
            if (item.ValueKind == JsonValueKind.String) list.Add(item.GetString() ?? "");
        return list;
    }
}
