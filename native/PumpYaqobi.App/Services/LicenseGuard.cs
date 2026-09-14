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
public sealed record LicenseCheck(
    bool Valid,
    string Reason,
    IReadOnlyList<string> Features,
    IReadOnlyList<string> Core,
    long SubscriptionEndsAt,
    long ExpiresAt,
    string PlanTitle)
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
/// ── و یک چیز که عمداً این‌جا نیست ──────────────────────────────────────────
/// ساعت. مجوز <c>exp</c> دارد و می‌سنجیمش، ولی اگر کسی ساعتِ ویندوز را عقب
/// ببرد فقط چند روز جلو می‌افتد — چون مجوز کوتاه‌عمر است و برنامه هر چند روز
/// از سرور تازه‌اش می‌گیرد. قفلِ اصلی روی سرور است، نه این‌جا: نوشتن روی
/// پوشهٔ ابری هر بار آن‌جا سنجیده می‌شود.
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
        if (string.IsNullOrWhiteSpace(pinnedPublicKey))
            return LicenseCheck.Fail("کلیدِ سرور هنوز قفل نشده — یک بار آنلاین شوید");

        var parts = token.Split('.');
        if (parts.Length != 3) return LicenseCheck.Fail("شکلِ مجوز درست نیست");

        JsonElement payload;
        try
        {
            using var doc = JsonDocument.Parse(FromB64Url(parts[1]));
            payload = doc.RootElement.Clone();
        }
        catch { return LicenseCheck.Fail("مجوز خوانده نشد"); }

        // ── ۱) امضا، پیش از هر چیزِ دیگر ─────────────────────────────────
        //  ⚠️ ترتیب مهم است: تا امضا سنجیده نشده، هیچ فیلدی از payload
        //  قابلِ اعتماد نیست. سنجیدنِ اول محتوا و بعد امضا، همان اشتباهی
        //  است که قفل را عملاً برمی‌دارد.
        try
        {
            using var ecdsa = ECDsa.Create();
            ecdsa.ImportSubjectPublicKeyInfo(Convert.FromBase64String(pinnedPublicKey), out _);
            var signed = Encoding.UTF8.GetBytes($"{parts[0]}.{parts[1]}");
            //  امضای خام (r||s) نه DER — همان چیزی که سرور می‌سازد
            if (!ecdsa.VerifyData(signed, FromB64Url(parts[2]), HashAlgorithmName.SHA256,
                                  DSASignatureFormat.IeeeP1363FixedFieldConcatenation))
            {
                return LicenseCheck.Fail("امضای مجوز درست نیست");
            }
        }
        catch { return LicenseCheck.Fail("امضای مجوز سنجیده نشد"); }

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
        if (nbf > 0 && nowMs + SkewMs < nbf) return LicenseCheck.Fail("زمانِ مجوز هنوز نرسیده است");
        if (exp > 0 && nowMs - SkewMs > exp) return LicenseCheck.Fail("مجوز منقضی شده — یک بار آنلاین شوید");

        return new LicenseCheck(
            true, "",
            Arr(payload, "feat"),
            Arr(payload, "core"),
            Num(payload, "sub_ends"),
            exp,
            Str(payload, "plan_title"));
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
