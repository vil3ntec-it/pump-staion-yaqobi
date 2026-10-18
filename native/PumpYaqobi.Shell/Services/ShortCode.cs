using System.Security.Cryptography;
using System.Text;

namespace PumpYaqobi.App.Services;

/// <summary>
/// ══ 🔢 کدِ ۸ رقمیِ روزدار (۱۴۰۵/۰۷/۲۶) ══════════════════════════════════════
///
/// «کدش خیلی بزرگ نباشه در حد شش رقمی یا هشت رقمی… مشتری‌هایی که من میرم پیش‌شون
/// نت ندارن و آزمایشی رو نمی‌تونن بگیرن… مقدار روز که می‌خوام رو بزارم و بعد با
/// کد بدم به برنامه و دقیق کار کنه.»
///
/// کد = جای‌گشتِ کلیددارِ (Feistel، HMAC-SHA256) عددِ «روز(۹ بیت)·سریال(۵ بیت)·
/// ۱۲ بیتِ صفر» در فضای ۰…۹۹٬۹۹۹٬۹۹۹. از هر ۸۵۰۰ عددِ تصادفی تقریباً یکی درست
/// است و پس از <see cref="MaxFails"/> کدِ غلط، <see cref="LockMs"/> قفل.
/// مهلت از <b>روزِ زدن</b> شمرده می‌شود و پلن وی‌آی‌پی است.
///
/// ⚠️ صاحب ریپو آگاهانه پذیرفت (۱۴۰۵/۰۷/۲۶): ۸ رقم جای امضا ندارد، پس رازِ ساختن
/// داخلِ همین برنامه است و کسی که برنامه را دیکامپایل کند می‌تواند کد بسازد.
/// کدِ بلندِ امضاشده (<see cref="OfflineKey"/>) برای اشتراکِ بزرگ همچنان هست.
///
/// ⛔ الگوریتم مو‌به‌مو همان <c>shop/server/src/lib/pump-short-codes.js</c> است و
/// آزمونِ <c>ShortCodeTests</c> با کدهایی می‌سنجد که همان کدِ جاوااسکریپت ساخته.
/// </summary>
public static class ShortCode
{
    private static readonly byte[] Key = Convert.FromHexString(
        "7a77d602c8751811aad146e49b754a29886d51b0fef3d880ef84d8ed9cfb3dd6");
    private const int Space = 100_000_000;
    private const int Rounds = 8;
    public const int MaxDays = 365;
    public const int MaxFails = 5;
    public const long LockMs = 60 * 60 * 1000;
    private const long DayMs = 86_400_000;

    /// <summary>ارقامِ فارسی/عربی ⇒ لاتین، بی فاصله و خط تیره. ۸ رقم نیست ⇒ "".</summary>
    public static string Digits(string? raw)
    {
        var sb = new StringBuilder();
        foreach (var ch in raw ?? "")
        {
            if (ch is >= '0' and <= '9') sb.Append(ch);
            else if (ch is >= '۰' and <= '۹') sb.Append((char)('0' + (ch - '۰')));
            else if (ch is >= '٠' and <= '٩') sb.Append((char)('0' + (ch - '٠')));
            else if (ch is ' ' or '-' or '‎' or '‏' or '‌') continue;
            else return "";
        }
        return sb.Length == 8 ? sb.ToString() : "";
    }

    public static string Pretty(string digits) => digits.Length == 8 ? digits[..4] + "-" + digits[4..] : digits;

    private static int F(int round, int half)
    {
        Span<byte> msg = stackalloc byte[8];
        "PYSC1"u8.CopyTo(msg);
        msg[5] = (byte)round;
        msg[6] = (byte)(half >> 8);
        msg[7] = (byte)half;
        Span<byte> mac = stackalloc byte[32];
        HMACSHA256.HashData(Key, msg, mac);
        return ((mac[0] << 8) | mac[1]) & 0x3FFF;
    }

    private static int Dec1(int x)
    {
        int l = x >> 14, r = x & 0x3FFF;
        for (var i = Rounds - 1; i >= 0; i--)
        {
            var nl = r ^ F(i, l);
            r = l;
            l = nl;
        }
        return (l << 14) | r;
    }

    /// <summary>کد ⇒ (روز، سریال) یا null. خالص؛ ساعت نمی‌خواهد.</summary>
    public static (int Days, int Serial)? Open(string digits)
    {
        if (digits.Length != 8) return null;
        var x = int.Parse(digits, System.Globalization.CultureInfo.InvariantCulture);
        do x = Dec1(x); while (x >= Space);
        if ((x & 0xFFF) != 0 || (x >> 26) != 0) return null;
        var days = (x >> 17) & 0x1FF;
        if (days < 1 || days > MaxDays) return null;
        return (days, (x >> 12) & 0x1F);
    }

    // ── شکلِ ذخیره‌شده: «S:ارقام@زمانِ‌زدن#برچسب» ───────────────────────────

    public static bool IsStored(string? s) => s?.StartsWith("S:", StringComparison.Ordinal) == true;

    private static string Tag(string digits, long at, string fingerprint)
    {
        var mac = HMACSHA256.HashData(Key, Encoding.UTF8.GetBytes("PYSC-ST|" + digits + "|" + at + "|" + fingerprint));
        return Convert.ToHexString(mac, 0, 6).ToLowerInvariant();
    }

    public static string Store(string digits, long at, string fingerprint) =>
        "S:" + digits + "@" + at + "#" + Tag(digits, at, fingerprint);

    public static string SerialOf(string digits) => "s" + digits;

    /// <summary>کدِ ذخیره‌شده ⇒ همان شکلِ <see cref="OfflineCheck"/>ِ کدِ بلند.</summary>
    public static OfflineCheck CheckStored(string stored, string fingerprint, long nowMs)
    {
        const string bad = "کدِ ۸ رقمیِ روی این کامپیوتر دست‌کاری شده است — کدِ تازه بزنید";
        var at = stored.IndexOf('@');
        var hash = stored.IndexOf('#');
        if (at < 0 || hash < at) return OfflineCheck.Bad(bad);
        var digits = stored[2..at];
        if (!long.TryParse(stored[(at + 1)..hash], out var when) || when <= 0) return OfflineCheck.Bad(bad);
        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.ASCII.GetBytes(stored[(hash + 1)..]), Encoding.ASCII.GetBytes(Tag(digits, when, fingerprint))))
            return OfflineCheck.Bad(bad);
        return Of(digits, when, nowMs, stored) ?? OfflineCheck.Bad(bad);
    }

    /// <summary>کدِ تازه، زده‌شده در <paramref name="at"/>.</summary>
    public static OfflineCheck? Of(string digits, long at, long nowMs, string canonical)
    {
        if (Open(digits) is not { } o) return null;
        var ends = at + o.Days * DayMs;
        var expired = nowMs >= ends;
        return new OfflineCheck(
            Valid: !expired, Expired: expired,
            Why: expired ? "مهلتِ کدِ ۸ رقمی تمام شده است" : "",
            Plan: "vip", Serial: SerialOf(digits), IssuedAt: at, EndsAt: ends, Permanent: false,
            Canonical: canonical) { Gift = true, Short = true };
    }
}
