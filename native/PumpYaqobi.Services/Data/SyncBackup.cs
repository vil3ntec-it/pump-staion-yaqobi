using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using PumpYaqobi.Persistence;

namespace PumpYaqobi.Services.Data;

/// <summary>
/// ══ پشتیبانِ رمزشده — پیش از هر مهاجرت، و با یک کلیک ═══════════════════
///
/// بندِ ۹ی پرامپتِ ۲۲: «بکاپ محلیِ رمزشده با یک کلیک + بکاپ خودکار قبل از
/// هر Migration.»
///
/// ── چرا رمز ───────────────────────────────────────────────────────────
/// نسخهٔ پشتیبانِ معمولیِ برنامه (<c>BackupService</c>) کنارِ خودِ دیتابیس
/// می‌ماند و رمز نمی‌خواهد — دیتابیسِ زنده هم همان‌جا و رمزنشده است، پس
/// رمز کردنش فقط بازگردانیِ یک‌کلیکی را شکننده می‌کرد. ولی این یکی برای
/// <b>بیرون بردن</b> است (فلش، ایمیل، پوشهٔ ابری) و آن‌جا رمز واجب است.
///
/// ── کلید ──────────────────────────────────────────────────────────────
/// ⚠️ کلید <b>ذخیره نمی‌شود</b>: از همان چیزهایی ساخته می‌شود که
/// <c>CloudConfig.DeviceUid</c> از آن‌ها شناسهٔ دستگاه می‌سازد (نامِ ماشین،
/// نامِ کاربر، مسیرِ نصب) به‌علاوهٔ یک نمک. پس فایل روی <b>همین</b>
/// کامپیوتر باز می‌شود و روی کامپیوترِ دیگری نه.
/// ⛔ و هیچ رمزی در کد نوشته نشده — نمک یک رشتهٔ ثابتِ بی‌راز است و به
/// تنهایی هیچ دری باز نمی‌کند.
///
/// ⚠️ <b>AES-GCM است، پس فایلِ دست‌خورده بی‌صدا باز نمی‌شود</b> — برچسبش
/// خطا می‌دهد. پشتیبانی که نصفه باشد بدتر از نبودنش است.
/// </summary>
public static class SyncBackup
{
    /// <summary>سرآیندِ فایل — تا نسخهٔ فردا بداند با چه چیزی طرف است.</summary>
    private static readonly byte[] Magic = Encoding.ASCII.GetBytes("PYQB1\0\0\0");

    /// <summary>پسوندِ فایلِ رمزشده.</summary>
    public const string Extension = ".pyq";

    /// <summary>سقفِ رمزگذاری — بالاتر از این، نسخهٔ خامِ سالم می‌ماند.</summary>
    public const long MaxEncryptBytes = 256L * 1024 * 1024;

    /// <summary>
    /// یک نسخهٔ سالمِ رمزشده از دیتابیس می‌سازد.
    /// </summary>
    /// <returns>مسیرِ فایل، یا <c>null</c> اگر نشد (هیچ‌وقت استثنا نمی‌دهد).</returns>
    public static string? Write(PumpDbFactory dbf, string? target = null, string? label = null)
    {
        try
        {
            var dir = Path.GetDirectoryName(dbf.DbPath);
            if (string.IsNullOrEmpty(dir)) return null;
            var folder = Path.Combine(dir, "backups");
            Directory.CreateDirectory(folder);

            //  ⚠️ `VACUUM INTO` نسخهٔ **سالمِ** دیتابیس می‌دهد، حتی وقتی
            //  برنامه باز است — کپیِ ساده وسطِ نوشتن نیم‌کاره درمی‌آید.
            var temp = Path.Combine(folder, "tmp-" + Guid.NewGuid().ToString("N")[..8] + ".db");
            using (var db = dbf.Create())
                db.Database.ExecuteSqlRaw($"VACUUM INTO '{temp.Replace("'", "''")}';");

            var name = (label is { Length: > 0 } ? label : "backup")
                     + "-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + Extension;
            var path = target ?? Path.Combine(folder, name);

            //  ⚠️ **دفترِ بزرگ در حافظه نمی‌آید.** رمزگذاریِ AES-GCMِ دات‌نت
            //  کلِ بایت‌ها را یک‌جا می‌خواهد؛ یک دفترِ نیم‌گیگی یعنی نزدیک
            //  یک‌و‌نیم گیگ رم، درست سرِ بالا آمدنِ برنامه. بالای این سقف
            //  همان نسخهٔ سالمِ `VACUUM INTO` می‌ماند — رمز نشده، ولی کنارِ
            //  خودِ دیتابیس که آن هم رمز نشده است، پس چیزی لو نمی‌رود.
            var size = new FileInfo(temp).Length;
            if (size > MaxEncryptBytes)
            {
                var plainPath = Path.ChangeExtension(path, ".db");
                try { File.Move(temp, plainPath, overwrite: true); } catch { return null; }
                return plainPath;
            }

            var plain = File.ReadAllBytes(temp);
            try { File.Delete(temp); } catch { /* بماند، بی‌ضرر */ }

            var nonce = RandomNumberGenerator.GetBytes(12);
            var cipher = new byte[plain.Length];
            var tag = new byte[16];
            using (var aes = new AesGcm(Key(), 16)) aes.Encrypt(nonce, plain, cipher, tag);

            using var outFile = File.Create(path);
            outFile.Write(Magic);
            outFile.Write(nonce);
            outFile.Write(tag);
            outFile.Write(cipher);
            return path;
        }
        catch { return null; }
    }

    /// <summary>
    /// یک پشتیبانِ رمزشده را باز می‌کند و در مسیرِ داده‌شده می‌نویسد.
    /// ⛔ فایلِ دست‌خورده یا مالِ کامپیوترِ دیگر خطا می‌دهد، نه دادهٔ نصفه.
    /// </summary>
    public static bool Read(string source, string target)
    {
        try
        {
            var raw = File.ReadAllBytes(source);
            if (raw.Length < Magic.Length + 12 + 16) return false;
            for (var i = 0; i < Magic.Length; i++) if (raw[i] != Magic[i]) return false;

            var nonce = raw.AsSpan(Magic.Length, 12).ToArray();
            var tag = raw.AsSpan(Magic.Length + 12, 16).ToArray();
            var cipher = raw.AsSpan(Magic.Length + 28).ToArray();
            var plain = new byte[cipher.Length];
            using (var aes = new AesGcm(Key(), 16)) aes.Decrypt(nonce, cipher, tag, plain);

            File.WriteAllBytes(target, plain);
            return true;
        }
        catch { return false; }
    }

    private static byte[] Key()
    {
        var seed = string.Join('|',
            "pump-yaqobi-backup-v1",
            Environment.MachineName,
            Environment.UserName,
            AppContext.BaseDirectory);
        return SHA256.HashData(Encoding.UTF8.GetBytes(seed));
    }
}
