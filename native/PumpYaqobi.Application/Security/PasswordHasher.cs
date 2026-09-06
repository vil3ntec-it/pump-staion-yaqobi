using System.Security.Cryptography;

namespace PumpYaqobi.Application.Security;

/// <summary>
/// ══ هشِ رمز ═══════════════════════════════════════════════════════════════
/// بندِ ۴۰: «Passwordها plaintext ذخیره نشوند، Hash امن استفاده شود.»
///
/// نسخهٔ HTML رمز را با SHA-256ِ خالی و بدون نمک نگه می‌داشت — یعنی یک جدولِ
/// رنگین‌کمانی آن را در چند ثانیه باز می‌کرد. اینجا PBKDF2-SHA256 با نمکِ
/// تصادفیِ ۱۶ بایتی و ۲۱۰٬۰۰۰ دور است (توصیهٔ OWASP برای PBKDF2-SHA256)،
/// و مقایسه در زمانِ ثابت انجام می‌شود تا از روی زمانِ پاسخ چیزی لو نرود.
///
/// قالبِ ذخیره:  <c>pbkdf2$sha256$&lt;دور&gt;$&lt;نمکِ base64&gt;$&lt;هشِ base64&gt;</c>
/// </summary>
public static class PasswordHasher
{
    public const int Iterations = 210_000;
    private const int SaltSize = 16;
    private const int KeySize = 32;
    private const string Prefix = "pbkdf2$sha256$";

    public static string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, KeySize);
        return $"{Prefix}{Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(key)}";
    }

    /// <summary>آیا این رشته اصلاً هشِ ماست؟ (رمزهای خامِ نسخهٔ قدیمی نه.)</summary>
    public static bool IsHashed(string? stored) =>
        !string.IsNullOrWhiteSpace(stored) && stored.StartsWith(Prefix, StringComparison.Ordinal);

    public static bool Verify(string password, string? stored)
    {
        if (string.IsNullOrEmpty(stored)) return false;
        var parts = stored.Split('$');
        if (parts.Length != 5 || parts[0] != "pbkdf2" || parts[1] != "sha256") return false;
        if (!int.TryParse(parts[2], out var iter) || iter <= 0) return false;

        byte[] salt, expected;
        try { salt = Convert.FromBase64String(parts[3]); expected = Convert.FromBase64String(parts[4]); }
        catch (FormatException) { return false; }

        var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iter, HashAlgorithmName.SHA256, expected.Length);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    /// <summary>هشِ کهنه (دورِ کمتر) باید بی‌صدا با همان رمزِ درست تازه شود.</summary>
    public static bool NeedsRehash(string? stored)
    {
        if (!IsHashed(stored)) return true;
        var parts = stored!.Split('$');
        return parts.Length != 5 || !int.TryParse(parts[2], out var iter) || iter < Iterations;
    }
}
