using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace PumpYaqobi.App.Services;

/// <summary>
/// ══ «حساب‌ها»ی گوشی فقط با رمزِ برنامه — نه با رمزِ خواندنِ کارمند ══════════
///
/// ⛔ (۱۴۰۵/۰۷/۲۳، شورای آمادگیِ عرضه) عکسِ زنده تا امروز همهٔ دفترِ مالی
/// (‎sections‎: گاوصندوق، صرافی، مصارف، مفاد، معاش) و چهار عددِ نوار را **خام**
/// می‌برد، به‌علاوهٔ خودِ هشِ رمزِ مدیر (‎gate‎). همان عکس با رمزِ خواندنِ
/// کارمند (و با کدِ هشت‌رقمی از سرورِ حساب) خوانده می‌شد و قفلِ «حساب‌ها»
/// فقط در خودِ مرورگر بود — یک ‎curl‎ همه را می‌داد، و هشِ رمز را می‌شد
/// بی‌محدودیت آفلاین حدس زد.
///
/// حالا آن بخشِ مالی فقط **رمزشده** بیرون می‌رود (‎seal‎، AES-256-GCM)، و
/// ‎gate‎ فقط «روش + نمک» است، نه خودِ هش:
///
///     gate = pbkdf2$sha256$&lt;دور&gt;$&lt;نمک&gt;          (بی هش)
///     کلید = SHA-256("pump-owner-seal-v1" ‖ PBKDF2(رمز، نمک، دور))
///
/// ‎PBKDF2(رمز، …)‎ همان چیزی است که برنامه از پیش در هشِ ذخیره‌شده دارد،
/// پس کامپیوتر بی دانستنِ خودِ رمز کلید را می‌سازد؛ گوشی با رمزی که صاحبِ
/// پمپ می‌زند همان را می‌سازد و باز می‌کند. رمزِ غلط ⇒ برچسبِ GCM نمی‌خورد.
///
/// ⛔ قرض‌داران، مخزن و هشدارها **خام می‌مانند** — درِ «⛽ کارمندان» بی رمز
/// است و هشدارِ سرور و پوش و بات از همان‌ها می‌خوانند.
/// ⛔ بی رمزِ برنامه بخشِ مالی **اصلاً** بیرون نمی‌رود (گوشی هم بی رمز
/// «حساب‌ها» را باز نمی‌کرد).
/// </summary>
public static class OwnerSeal
{
    public const string Info = "pump-owner-seal-v1";

    /// <summary>کلیدهای عکس که فقط صاحبِ پمپ باید ببیند.</summary>
    public static readonly string[] OwnerOnly = { "sections", "banner" };

    /// <summary>
    /// نسخهٔ گوشی‌ها از همان عکس: بخشِ مالی رمزشده، ‎gate‎ بی هش. ورودی دست
    /// نمی‌خورد (هشِ «چیزی عوض شد؟» از روی همان ورودی است).
    /// </summary>
    public static Dictionary<string, object?> ForPhones(Dictionary<string, object?> snap, string? adminHash)
    {
        var copy = new Dictionary<string, object?>(snap);
        copy.Remove("gate");
        var owner = new Dictionary<string, object?>();
        foreach (var k in OwnerOnly)
        {
            if (copy.TryGetValue(k, out var v)) owner[k] = v;
            copy.Remove(k);
        }

        if (!TryParse(adminHash, out var kdf, out var dk)) return copy;
        try
        {
            copy["gate"] = kdf;
            copy["seal"] = Seal(dk, JsonSerializer.SerializeToUtf8Bytes(owner));
        }
        finally { CryptographicOperations.ZeroMemory(dk); }
        return copy;
    }

    /// <summary>‎pbkdf2$sha256$دور$نمک$هش‎ ⇒ («روش + نمک»، هش). هر شکلِ دیگر ⇒ نه.</summary>
    public static bool TryParse(string? stored, out string kdf, out byte[] dk)
    {
        kdf = ""; dk = Array.Empty<byte>();
        var p = (stored ?? "").Split('$');
        if (p.Length != 5 || p[0] != "pbkdf2" || p[1] != "sha256") return false;
        if (!int.TryParse(p[2], out var iter) || iter <= 0) return false;
        try { Convert.FromBase64String(p[3]); dk = Convert.FromBase64String(p[4]); }
        catch (FormatException) { return false; }
        if (dk.Length < 16) return false;
        kdf = string.Join('$', p[0], p[1], p[2], p[3]);
        return true;
    }

    /// <summary>کلیدِ AES از ‎PBKDF2(رمز)‎ — همان که گوشی با ‎deriveBits‎ می‌سازد.</summary>
    public static byte[] KeyOf(byte[] dk)
    {
        var info = Encoding.UTF8.GetBytes(Info);
        var buf = new byte[info.Length + dk.Length];
        info.CopyTo(buf, 0);
        dk.CopyTo(buf, info.Length);
        try { return SHA256.HashData(buf); }
        finally { CryptographicOperations.ZeroMemory(buf); }
    }

    /// <summary>‎{ v, iv, ct }‎ — ‎ct‎ = متنِ رمزشده ‖ برچسبِ ۱۶ بایتی (همان شکلِ WebCrypto).</summary>
    public static Dictionary<string, object?> Seal(byte[] dk, byte[] plain)
    {
        var key = KeyOf(dk);
        try
        {
            var iv = RandomNumberGenerator.GetBytes(12);
            var ct = new byte[plain.Length + 16];
            using (var aes = new AesGcm(key, 16))
                aes.Encrypt(iv, plain, ct.AsSpan(0, plain.Length), ct.AsSpan(plain.Length, 16));
            return new() { ["v"] = 1, ["iv"] = Convert.ToBase64String(iv), ["ct"] = Convert.ToBase64String(ct) };
        }
        finally { CryptographicOperations.ZeroMemory(key); }
    }

    /// <summary>باز کردن با همان کلید — برای آزمون و برای هر خوانندهٔ نیتیوِ آینده.</summary>
    public static byte[]? Open(byte[] dk, string iv64, string ct64)
    {
        var key = KeyOf(dk);
        try
        {
            var iv = Convert.FromBase64String(iv64);
            var ct = Convert.FromBase64String(ct64);
            if (iv.Length != 12 || ct.Length < 16) return null;
            var plain = new byte[ct.Length - 16];
            using var aes = new AesGcm(key, 16);
            aes.Decrypt(iv, ct.AsSpan(0, plain.Length), ct.AsSpan(plain.Length), plain);
            return plain;
        }
        catch (CryptographicException) { return null; }
        catch (FormatException) { return null; }
        finally { CryptographicOperations.ZeroMemory(key); }
    }
}
