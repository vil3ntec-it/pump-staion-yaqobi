using System.Security.Cryptography;

namespace PumpYaqobi.Domain;

/// <summary>
/// ══ ULID — شناسهٔ یکتا که با زمان مرتب هم می‌شود ══════════════════════════
///
/// بندِ ۲۰٫۱ پرامپت: «شناسهٔ همهٔ رکوردها ULID/UUID است که خودِ برنامه
/// می‌سازد، نه شمارهٔ خودافزا؛ تا دو دستگاهِ آفلاین با هم تصادم نکنند.»
///
/// ── چرا ULID و نه <c>Guid</c> ───────────────────────────────────────────
/// هر دو یکتا هستند، ولی ULID <b>مرتب</b> است: بیست‌وشش نویسه که ده‌تای
/// اولش زمانِ ساخت است. پس ردیفِ دفترِ تغییرات به ترتیبِ ساخت مرتب
/// می‌شود، ایندکسِ دیتابیس تکه‌تکه نمی‌شود، و خودِ سرور هم همین را
/// می‌خواهد (<c>OP_ID_RE</c> در <c>sync-v1.js</c> فقط حرف و رقم و
/// <c>-</c>/<c>_</c> می‌پذیرد — الفبای این‌جا هر دو شرط را دارد).
///
/// ⚠️ <b>هیچ بستهٔ تازه‌ای برای این اضافه نشد.</b> الفبای Crockford و
/// <see cref="RandomNumberGenerator"/>ِ خودِ دات‌نت کافی‌اند، و پروژه
/// عمداً وابستگیِ اضافه ندارد.
///
/// ⚠️ <b>در یک میلی‌ثانیه هم تکراری نمی‌سازد</b>: اگر دو شناسه در همان
/// میلی‌ثانیه خواسته شوند، بخشِ تصادفی یکی بالا می‌رود (monotonic) به‌جای
/// این‌که از نو قرعه بیفتد. بی این، صد ردیفِ یک «ذخیره»ی دسته‌ای می‌توانست
/// دو شناسهٔ برابر بسازد — و آن‌جا سرور یکی‌شان را «تکراری» می‌شمرد و
/// ردیفِ دوم برای همیشه گم می‌شد.
/// </summary>
public static class Ulid
{
    /// <summary>الفبای Crockford Base32 — بی <c>I</c>، <c>L</c>، <c>O</c> و <c>U</c>.</summary>
    private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

    private static readonly object Gate = new();
    private static readonly byte[] LastRandom = new byte[10];
    private static long _lastMs = -1;

    /// <summary>یک شناسهٔ تازه — بیست‌وشش نویسه.</summary>
    public static string New() => New(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

    /// <summary>همان، با زمانِ داده‌شده — فقط برای آزمون‌ها.</summary>
    public static string New(long unixMs)
    {
        var random = new byte[10];
        lock (Gate)
        {
            if (unixMs == _lastMs)
            {
                Increment(LastRandom);
            }
            else
            {
                RandomNumberGenerator.Fill(LastRandom);
                _lastMs = unixMs;
            }
            Array.Copy(LastRandom, random, 10);
        }
        return Encode(unixMs, random);
    }

    /// <summary>این رشته شکلِ یک ULID را دارد؟ (برای سنجه‌ها و اعتبارسنجی.)</summary>
    public static bool IsUlid(string? s)
    {
        if (s is null || s.Length != 26) return false;
        foreach (var c in s)
            if (Alphabet.IndexOf(char.ToUpperInvariant(c)) < 0) return false;
        return true;
    }

    private static void Increment(byte[] bytes)
    {
        for (var i = bytes.Length - 1; i >= 0; i--)
        {
            //  ۲۵۵ ⇒ صفر و سرریز به بایتِ قبلی؛ وگرنه همین‌جا تمام
            if (bytes[i] != 0xFF) { bytes[i]++; return; }
            bytes[i] = 0;
        }
        //  سرریزِ کاملِ هشتاد بیت — عملاً شدنی نیست، ولی سکوت هم نمی‌کنیم:
        //  قرعهٔ تازه می‌اندازیم تا دستِ‌کم تکراری نماند.
        RandomNumberGenerator.Fill(bytes);
    }

    private static string Encode(long unixMs, byte[] random)
    {
        var chars = new char[26];

        //  ده نویسهٔ اول: چهل‌وهشت بیتِ زمان (دو بیتِ بالا همیشه صفر است)
        for (var i = 0; i < 10; i++)
            chars[i] = Alphabet[(int)((unixMs >> (45 - (i * 5))) & 31)];

        //  شانزده نویسهٔ بعد: هشتاد بیتِ تصادفی، پنج‌بیت‌پنج‌بیت
        for (var i = 0; i < 16; i++)
        {
            var bit = i * 5;
            var index = bit >> 3;
            var shift = bit & 7;
            var window = (random[index] << 8) | (index + 1 < random.Length ? random[index + 1] : 0);
            chars[10 + i] = Alphabet[(window >> (11 - shift)) & 31];
        }

        return new string(chars);
    }
}
