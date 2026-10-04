using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace PumpYaqobi.App.Services;

/// <summary>
/// ══ کدِ اشتراکِ آفلاین — «بدونِ نت هم اشتراک داده بشه» ══════════════════
///
/// خواستهٔ صاحب ریپو (۱۴۰۵/۰۷/۱۵): «یک کد برای اشتراک می‌سازی که برای
/// کسانی که نت ندارن هم اشتراک بدم… سه نوع کد… یک گیرنده برای برنامه تا کد
/// رو بزنم درجا قفل‌ها باز بشه… و اگه یارو اینترنت پیدا کرد… سرور همون کد
/// رو ببینه و بگه آره این حساب اشتراک داره.»
///
///   ۱) برنامه «کدِ کامپیوتر» را نشان می‌دهد (<see cref="ComputerCode()"/>)
///   ۲) صاحبِ سامانه همان را با پلن در پنل می‌زند ⇒ سرورِ حساب امضا می‌کند
///   ۳) کد یا فایلِ ‎.pumpkey‎ این‌جا زده می‌شود ⇒ <see cref="Check"/> بی
///      اینترنت می‌سنجد ⇒ <see cref="Entitlements.State"/> قفل‌ها را باز می‌کند
///   ۴) روزی که همین کامپیوتر به حسابی بند شد، <c>CloudLink.RedeemOfflineAsync</c>
///      کد را به سرور می‌برد و اشتراک روی پمپِ همان حساب می‌نشیند.
///
/// ⛔ شکلِ دودویی مو‌به‌مو همان <c>shop/server/src/lib/offline-codes.js</c>
/// است (آن‌جا بایت‌به‌بایت نوشته شده) و آزمونِ <c>OfflineKeyTests</c> با
/// کدهایی می‌سنجد که **خودِ همان کدِ جاوااسکریپت** ساخته — نه با کدی که این
/// طرف ساخته باشد.
///
/// ⛔ ریشهٔ اعتماد: کلیدهای **داخلِ خودِ برنامه** (<see cref="CloudConfig.LicenseKeys"/>
/// و <see cref="CloudConfig.OfflineKeys"/> که ساختِ CI از سرورِ حساب می‌گیرد).
/// فقط وقتی هیچ کلیدی داخلِ برنامه نیست، کلیدِ TOFUِ روی دیسک — همان قاعدهٔ
/// مجوز. کلیدِ روی دیسک را هر کسی با یک ویرایشگرِ متن عوض می‌کند؛ با کلیدِ
/// داخلِ برنامه دیگر اثری ندارد.
/// </summary>
public static class OfflineKey
{
    public const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";
    private const int BodyLen = 34;
    private const int TotalLen = BodyLen + 64;
    private const uint Permanent = 0xFFFFFFFF;
    private static readonly byte[] Domain = Encoding.Latin1.GetBytes("PYOC1\0");

    /// <summary>نامِ پلن در کد ⇒ همان کدِ پلنِ سرورِ حساب.</summary>
    public static string PlanOf(byte b) => b switch { 1 => "std", 2 => "vip", 3 => "perm", _ => "" };

    public static string TitleOf(string plan) => plan switch
    {
        "std" => "استاندارد",
        "vip" => "وی‌آی‌پی",
        "perm" => "دائمی",
        _ => plan,
    };

    /// <summary>
    /// قابلیت‌های هر پلن — همان فهرستِ پلن‌های پمپ روی سرورِ حساب
    /// (<c>migrations/019_pump_plans.sql</c>). ⚠️ این‌جا تصمیمی گرفته نمی‌شود؛
    /// <see cref="EntitlementState.Allows"/> همان هم‌معنی‌ها را می‌فهمد.
    /// </summary>
    public static IReadOnlyList<string> FeaturesOf(string plan) => plan switch
    {
        "std" => new[] { "cloudbackup" },
        "vip" or "perm" => new[]
        {
            "dashboard", "kar_app", "bot", "messenger", "cloud", "cloudbackup", "profit", "history", "multi_device",
        },
        _ => Array.Empty<string>(),
    };

    // ══ base32ِ کراکفورد ════════════════════════════════════════════════════

    public static string Encode(ReadOnlySpan<byte> buf)
    {
        var sb = new StringBuilder((buf.Length * 8 + 4) / 5);
        int bits = 0, value = 0;
        foreach (var b in buf)
        {
            value = (value << 8) | b; bits += 8;
            while (bits >= 5) { sb.Append(Alphabet[(value >> (bits - 5)) & 31]); bits -= 5; }
            value &= (1 << bits) - 1;
        }
        if (bits > 0) sb.Append(Alphabet[(value << (5 - bits)) & 31]);
        return sb.ToString();
    }

    private static int ValueOf(char ch)
    {
        var c = char.ToUpperInvariant(ch);
        if (c == 'O') return 0;
        if (c is 'I' or 'L') return 1;
        return Alphabet.IndexOf(c);
    }

    /// <summary>فقط حرف و رقم؛ رقمِ فارسی و عربی هم (کد از واتساپ چسبانده می‌شود).</summary>
    public static string Clean(string? raw)
    {
        var sb = new StringBuilder();
        foreach (var ch in raw ?? "")
        {
            if (ch is >= '۰' and <= '۹') sb.Append((char)('0' + (ch - '۰')));
            else if (ch is >= '٠' and <= '٩') sb.Append((char)('0' + (ch - '٠')));
            else if (ch is >= '0' and <= '9' or >= 'a' and <= 'z' or >= 'A' and <= 'Z') sb.Append(ch);
        }
        return sb.ToString();
    }

    public static byte[]? Decode(string? str, int byteLen)
    {
        var s = Clean(str);
        if (s.Length != (byteLen * 8 + 4) / 5) return null;
        var outp = new byte[byteLen];
        int bits = 0, value = 0, i = 0;
        foreach (var ch in s)
        {
            var v = ValueOf(ch);
            if (v < 0) return null;
            value = (value << 5) | v; bits += 5;
            if (bits >= 8)
            {
                if (i < byteLen) outp[i++] = (byte)((value >> (bits - 8)) & 0xff);
                bits -= 8;
                value &= (1 << bits) - 1;
            }
        }
        return i == byteLen && value == 0 ? outp : null;
    }

    private static string Group(string s, int n)
    {
        var sb = new StringBuilder();
        for (var i = 0; i < s.Length; i += n)
        {
            if (sb.Length > 0) sb.Append('-');
            sb.Append(s, i, Math.Min(n, s.Length - i));
        }
        return sb.ToString();
    }

    // ══ کدِ کامپیوتر ════════════════════════════════════════════════════════

    private static int CheckOf(IReadOnlyList<int> values)
    {
        var sum = 0;
        for (var i = 0; i < values.Count; i++) sum += (i + 1) * values[i];
        return sum % 29;
    }

    /// <summary>۱۰ بایتِ این کامپیوتر (۷۵ بیت + ۵ بیتِ صفر) — خالی اگر سیستم شناسه‌ای نداد.</summary>
    public static byte[] MachineBytes(string fingerprint)
    {
        if (string.IsNullOrWhiteSpace(fingerprint)) return Array.Empty<byte>();
        var h = SHA256.HashData(Encoding.UTF8.GetBytes("pump-yaqobi|offline-pc|v1|" + fingerprint));
        var m = h[..10];
        m[9] &= 0xE0;
        return m;
    }

    /// <summary>
    /// «XXXX-XXXX-XXXX-XXXX» — ۱۵ نویسه از خودِ کامپیوتر + یک نویسهٔ سنجش.
    ///
    /// ⚠️ از <see cref="CloudConfig.MachineFingerprint"/> (<c>MachineGuid</c>ِ
    /// ویندوز) ساخته می‌شود، نه از <c>DeviceUid</c>: آن یکی در <c>settings.json</c>
    /// می‌نشیند و با کپیِ پوشه به کامپیوترِ دیگر می‌رود — «فقط یک کامپیوتر»
    /// آن‌وقت دروغ بود. خالی ⇒ این سیستم شناسه‌ای نداد.
    /// </summary>
    public static string ComputerCode(string fingerprint)
    {
        var m = MachineBytes(fingerprint);
        if (m.Length == 0) return "";
        var data = Encode(m)[..15];
        var values = data.Select(ValueOf).ToArray();
        return Group(data + Alphabet[CheckOf(values)], 4);
    }

    public static string ComputerCode() => ComputerCode(CloudConfig.MachineFingerprint());

    // ══ خودِ کد ═════════════════════════════════════════════════════════════

    /// <summary>
    /// کلیدهایی که یک کد با آن‌ها پذیرفته می‌شود، به ازای <c>kid</c>ِ
    /// ۱۶نویسه‌ای (همان <c>keyId</c>ِ سرور = ۸ بایتِ نخستِ SHA-256ِ SPKI).
    /// </summary>
    public static IReadOnlyDictionary<string, string> TrustedKeys(AppSettings? f = null)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var spki in CloudConfig.LicenseKeys.Values.Concat(CloudConfig.OfflineKeys.Values))
            if (KidOf(spki) is { Length: > 0 } kid) map[kid] = spki;
        if (map.Count > 0) return map;

        //  هیچ کلیدی داخلِ برنامه نیست ⇒ همان TOFUِ مجوز
        var tofu = (f ?? AppSettings.Load()).CloudPublicKey;
        if (KidOf(tofu) is { Length: > 0 } k) map[k] = tofu.Trim();
        return map;
    }

    public static string KidOf(string? spki)
    {
        if (string.IsNullOrWhiteSpace(spki)) return "";
        try
        {
            return Convert.ToHexString(SHA256.HashData(Convert.FromBase64String(spki.Trim()))[..8]).ToLowerInvariant();
        }
        catch { return ""; }
    }

    /// <summary>کد را برای نشان دادن گروه‌بندی می‌کند (پنج‌تا‌پنج‌تا).</summary>
    public static string Pretty(string? code)
    {
        var s = Clean(code).ToUpperInvariant();
        return s.Length == 0 ? "" : Group(s, 5);
    }

    /// <summary>
    /// متنی که کاربر آورده (کدِ خام، یا محتوای فایلِ ‎.pumpkey‎) ⇒ خودِ کد.
    /// ⚠️ از فایل فقط <c>code</c> خوانده می‌شود؛ بقیهٔ فیلدها برای چشم‌اند و
    /// هیچ تصمیمی از آن‌ها گرفته نمی‌شود.
    /// </summary>
    public static string Extract(string? text)
    {
        var t = (text ?? "").Trim();
        if (t.StartsWith('{'))
        {
            try
            {
                using var doc = JsonDocument.Parse(t);
                if (doc.RootElement.TryGetProperty("code", out var c) && c.ValueKind == JsonValueKind.String)
                    return c.GetString() ?? "";
            }
            catch { }
            return "";
        }
        return t;
    }

    /// <summary>
    /// سنجش — خالص: امضا، <c>kid</c>، کامپیوتر، تاریخ. هیچ‌وقت استثنا نمی‌دهد.
    /// </summary>
    public static OfflineCheck Check(string? code, string fingerprint, long nowMs,
        IReadOnlyDictionary<string, string> keys)
    {
        var raw = Clean(code);
        if (raw.Length == 0) return OfflineCheck.None;
        var buf = Decode(raw, TotalLen);
        if (buf is null || buf[0] != 1) return OfflineCheck.Bad("این کلیدِ اشتراکِ بی‌اینترنت خوانده نشد — کد را کامل و درست بزنید");
        var plan = PlanOf(buf[1]);
        if (plan.Length == 0) return OfflineCheck.Bad("این کلیدِ اشتراکِ بی‌اینترنت خوانده نشد — کد را کامل و درست بزنید");

        var kid = Convert.ToHexString(buf, 26, 8).ToLowerInvariant();
        if (!keys.TryGetValue(kid, out var spki))
            return OfflineCheck.Bad(keys.Count == 0
                ? "این نسخهٔ برنامه کلیدِ سرورِ حساب را ندارد — نسخهٔ تازه را نصب کنید"
                : "این کد با کلیدِ سرورِ دیگری امضا شده است");

        try
        {
            using var ec = ECDsa.Create();
            ec.ImportSubjectPublicKeyInfo(Convert.FromBase64String(spki), out _);
            var signed = new byte[Domain.Length + BodyLen];
            Domain.CopyTo(signed, 0);
            Array.Copy(buf, 0, signed, Domain.Length, BodyLen);
            if (!ec.VerifyData(signed, buf.AsSpan(BodyLen, 64), HashAlgorithmName.SHA256,
                    DSASignatureFormat.IeeeP1363FixedFieldConcatenation))
                return OfflineCheck.Bad("این کلیدِ اشتراکِ بی‌اینترنت دست‌کاری شده یا اشتباه است");
        }
        catch { return OfflineCheck.Bad("این کلیدِ اشتراکِ بی‌اینترنت دست‌کاری شده یا اشتباه است"); }

        var mine = MachineBytes(fingerprint);
        if (mine.Length == 0 || !buf.AsSpan(2, 10).SequenceEqual(mine))
            return OfflineCheck.Bad("این کد برای کامپیوترِ دیگری ساخته شده است — کدِ کامپیوترِ همین‌جا را بفرستید");

        var issued = (long)System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(buf.AsSpan(18, 4)) * 1000;
        var endsRaw = System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(buf.AsSpan(22, 4));
        var permanent = endsRaw == Permanent;
        var ends = permanent ? 0 : (long)endsRaw * 1000;
        var serial = Convert.ToHexString(buf, 12, 6).ToLowerInvariant();

        return new OfflineCheck(
            Valid: permanent || nowMs < ends,
            Expired: !permanent && nowMs >= ends,
            Why: !permanent && nowMs >= ends ? "مهلتِ این کلیدِ اشتراکِ بی‌اینترنت تمام شده است" : "",
            Plan: plan, Serial: serial, IssuedAt: issued, EndsAt: ends, Permanent: permanent,
            Canonical: Pretty(raw));
    }

    private static readonly object CacheGate = new();
    private static (string Key, OfflineCheck At0)? _cache;

    /// <summary>
    /// کدِ ذخیره‌شدهٔ همین نصب، با کلیدها و ساعتِ همین نصب.
    ///
    /// ⚡ <see cref="Entitlements.State"/> با هر دروازه صدا زده می‌شود و سنجشِ
    /// امضا (ECDSA) هر بار گران است؛ پس نتیجهٔ **مستقل از زمان** (امضا،
    /// کامپیوتر، پلن) کَش می‌شود و فقط «هنوز تمام نشده؟» با ساعتِ همان لحظه.
    /// کلیدِ کَش خودِ کد، اثرِ انگشت و کلیدهاست — عوض شدنِ هر کدام یعنی سنجشِ تازه.
    /// </summary>
    public static OfflineCheck Stored(AppSettings f, long nowMs)
    {
        if (string.IsNullOrWhiteSpace(f.OfflineCode)) return OfflineCheck.None;
        var fp = CloudConfig.MachineFingerprint();
        var keys = TrustedKeys(f);
        var key = f.OfflineCode + "|" + fp + "|" + string.Join(",", keys.Keys.OrderBy(k => k, StringComparer.Ordinal));
        OfflineCheck at0;
        lock (CacheGate)
        {
            if (_cache is { } c && c.Key == key) at0 = c.At0;
            else { at0 = Check(f.OfflineCode, fp, 0, keys); _cache = (key, at0); }
        }
        return At(at0, nowMs);
    }

    /// <summary>
    /// زدنِ کد (یا محتوای فایلِ ‎.pumpkey‎) ⇒ سنجش ⇒ اگر درست بود، همین حالا
    /// روی دیسک و قفل‌ها باز. ⛔ کدِ نادرست هیچ چیزی را عوض نمی‌کند — نه
    /// کدِ قبلی را پاک می‌کند، نه چیزی می‌نویسد.
    /// </summary>
    public static OfflineCheck Apply(AppSettings f, string? text)
    {
        var code = Extract(text);
        if (Clean(code).Length == 0) return OfflineCheck.Bad("کد را بچسبانید یا فایلِ ‎.pumpkey‎ را انتخاب کنید");
        var c = Check(code, CloudConfig.MachineFingerprint(), LicenseClock.Now(f), TrustedKeys(f));
        if (!c.Valid)
        {
            if (c.Expired)
                return c with { Why = "مهلتِ این کد " + Localize(c.EndsAt) + " تمام شده است — "
                                      + "اگر ساعت و تاریخِ این کامپیوتر درست نیست، اول درستش کنید" };
            return c;
        }
        f.OfflineCode = c.Canonical;
        f.OfflineCodeRedeemed = "";
        //  ساعتِ کامپیوترِ قدیمی گاهی عقب است؛ کفِ ساعتِ مجوز دستِ‌کم روزِ
        //  صدورِ همین کد می‌شود تا عقب بردنِ ساعت مهلت را دراز نکند.
        LicenseClock.Anchor(f, c.IssuedAt, fresh: false);
        f.Save();
        CloudLink.NotifyLicenseChanged();
        return c;
    }

    /// <summary>برداشتنِ کد از همین کامپیوتر (اشتراکِ سرور دست نمی‌خورد).</summary>
    public static void Remove(AppSettings f)
    {
        if (f.OfflineCode.Length == 0) return;
        f.OfflineCode = "";
        f.OfflineCodeRedeemed = "";
        f.Save();
        CloudLink.NotifyLicenseChanged();
    }

    /// <summary>تاریخِ شمسی برای پیام‌ها.</summary>
    public static string Localize(long ms)
    {
        if (ms <= 0) return "";
        try { return PumpYaqobi.Application.Localization.Shamsi.Of(
            DateTimeOffset.FromUnixTimeMilliseconds(ms).LocalDateTime); }
        catch { return ""; }
    }

    /// <summary>همان نتیجه، با ساعتِ تازه.</summary>
    public static OfflineCheck At(OfflineCheck c, long nowMs)
    {
        if (!c.Genuine) return c;
        var expired = !c.Permanent && nowMs >= c.EndsAt;
        return c with { Valid = !expired, Expired = expired, Why = expired ? "مهلتِ این کلیدِ اشتراکِ بی‌اینترنت تمام شده است" : "" };
    }
}

public sealed record OfflineCheck(
    bool Valid,
    bool Expired,
    string Why,
    string Plan,
    string Serial,
    long IssuedAt,
    long EndsAt,
    bool Permanent,
    string Canonical)
{
    public static readonly OfflineCheck None = new(false, false, "", "", "", 0, 0, false, "");

    public static OfflineCheck Bad(string why) => new(false, false, why, "", "", 0, 0, false, "");

    /// <summary>امضا و کامپیوتر درست‌اند (شاید منقضی)؟</summary>
    public bool Genuine => Plan.Length > 0;

    public string PlanTitle => OfflineKey.TitleOf(Plan);

    public IReadOnlyList<string> Features => OfflineKey.FeaturesOf(Plan);

    public int DaysLeft(long nowMs) => Permanent ? int.MaxValue
        : Math.Max(0, (int)Math.Ceiling((EndsAt - nowMs) / 86_400_000d));
}
