using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace PumpYaqobi.Services.Data;

/// <summary>
/// ══ مُهرِ بکاپ — بسته به پمپِ صاحبش (۱۴۰۵/۰۷/۲۰) ═══════════════════════════
///
/// خواستهٔ صاحب ریپو: «بکاپ روی کامپیوترِ جدید قابلِ بازیابی باشد؛ ولی آزمایشی،
/// حسابِ جدید یا آفلاین راهی برای دور زدنِ اشتراک نشود… اگر حسابِ دوم ساخت تا
/// آزمایشیِ دوباره بگیرد، بکاپِ قبلی برایش باز نشود.»
///
/// فایل با کلیدِ <b>همان پمپ</b> رمز می‌شود (AES-256-GCM). کلید را فقط سرورِ حساب
/// به عضوِ همان پمپ می‌دهد (‎lib/backup-key.js‎). حسابِ تازه پمپِ تازه دارد ⇒ کلیدِ
/// پمپِ قبلی را هرگز نمی‌گیرد ⇒ فایل برایش باز نمی‌شود، آنلاین یا آفلاین.
///
/// ⛔ تکه‌تکه (یک مگابایت)، جریانی — دفترِ چندصد مگابایتی دو بار در حافظه نمی‌آید.
/// هر تکه نونسِ خودش و برچسبِ خودش را دارد؛ شماره و «تکهٔ آخر» داخلِ دادهٔ
/// تأییدشده‌اند، پس جابه‌جا کردن یا بریدنِ تهِ فایل هم پیدا می‌شود.
/// ⛔ هیچ کلیدی در برنامه نیست — فقط آن‌چه سرور به همین پمپ داده.
/// </summary>
public static class BackupSeal
{
    private static readonly byte[] Magic = "PYBSEAL1"u8.ToArray();
    private const int Chunk = 1024 * 1024;
    private const int TagLen = 16;

    /// <summary>این فایل مُهر خورده است؟</summary>
    public static bool IsSealed(string path)
    {
        try
        {
            using var f = File.OpenRead(path);
            Span<byte> head = stackalloc byte[8];
            return f.Read(head) == 8 && head.SequenceEqual(Magic);
        }
        catch { return false; }
    }

    /// <summary>پمپِ صاحبِ این فایل — یا <c>null</c> اگر مُهر نخورده یا خراب است.</summary>
    public static string? StationOf(string path)
    {
        try
        {
            using var f = File.OpenRead(path);
            return ReadHeader(f, out _, out _);
        }
        catch { return null; }
    }

    /// <summary>فایلِ <paramref name="src"/> را با کلیدِ پمپ در <paramref name="dst"/> مُهر می‌کند.</summary>
    public static void Seal(string src, string dst, string stationId, byte[] key)
    {
        if (key.Length != 32) throw new ArgumentException("کلیدِ بکاپ باید ۳۲ بایت باشد", nameof(key));
        var station = Encoding.UTF8.GetBytes(stationId ?? "");
        if (station.Length is 0 or > 1024) throw new ArgumentException("شناسهٔ پمپ نامعتبر است", nameof(stationId));

        var tmp = dst + ".seal";
        using (var input = new FileStream(src, FileMode.Open, FileAccess.Read, FileShare.Read, Chunk))
        using (var output = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None, Chunk))
        using (var gcm = new AesGcm(key, TagLen))
        {
            var baseNonce = RandomNumberGenerator.GetBytes(8);
            var header = new MemoryStream();
            header.Write(Magic);
            header.WriteByte(1);
            Span<byte> len = stackalloc byte[2];
            BinaryPrimitives.WriteUInt16BigEndian(len, (ushort)station.Length);
            header.Write(len);
            header.Write(station);
            header.Write(baseNonce);
            var headerBytes = header.ToArray();
            output.Write(headerBytes);

            var plain = new byte[Chunk];
            var next = new byte[Chunk];
            var cipher = new byte[Chunk];
            var tag = new byte[TagLen];
            int n = Fill(input, plain);
            uint index = 0;
            while (true)
            {
                int m = n == Chunk ? Fill(input, next) : 0;
                var final = m == 0;
                var nonce = Nonce(baseNonce, index);
                var aad = Aad(headerBytes, index, final);
                gcm.Encrypt(nonce, plain.AsSpan(0, n), cipher.AsSpan(0, n), tag, aad);
                Span<byte> hdr = stackalloc byte[5];
                BinaryPrimitives.WriteInt32BigEndian(hdr, n);
                hdr[4] = final ? (byte)1 : (byte)0;
                output.Write(hdr);
                output.Write(cipher, 0, n);
                output.Write(tag);
                if (final) break;
                (plain, next) = (next, plain);
                n = m;
                index++;
            }
        }
        File.Move(tmp, dst, overwrite: true);
    }

    /// <summary>
    /// فایلِ مُهرخورده را با کلیدِ پمپ باز می‌کند. ‎false‎ یعنی کلید نمی‌خورد یا فایل
    /// دست خورده — و در آن صورت هیچ فایلی در <paramref name="dst"/> نمی‌ماند.
    /// </summary>
    public static bool Open(string src, string dst, byte[] key)
    {
        var tmp = dst + ".open";
        try
        {
            using (var input = new FileStream(src, FileMode.Open, FileAccess.Read, FileShare.Read, Chunk))
            using (var output = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None, Chunk))
            using (var gcm = new AesGcm(key, TagLen))
            {
                if (ReadHeader(input, out var headerBytes, out var baseNonce) is null) return Fail(tmp);
                var cipher = new byte[Chunk];
                var plain = new byte[Chunk];
                var tag = new byte[TagLen];
                Span<byte> hdr = stackalloc byte[5];
                uint index = 0;
                while (true)
                {
                    if (input.Read(hdr) != 5) return Fail(tmp);              // ⛔ بریده شده
                    var n = BinaryPrimitives.ReadInt32BigEndian(hdr);
                    var final = hdr[4] == 1;
                    if (n < 0 || n > Chunk) return Fail(tmp);
                    if (Fill(input, cipher, n) != n || Fill(input, tag) != TagLen) return Fail(tmp);
                    gcm.Decrypt(Nonce(baseNonce, index), cipher.AsSpan(0, n), tag, plain.AsSpan(0, n),
                                Aad(headerBytes, index, final));
                    output.Write(plain, 0, n);
                    if (final) break;
                    index++;
                }
                if (input.Position != input.Length) return Fail(tmp);        // ⛔ دنباله‌ای پس از تکهٔ آخر
            }
            File.Move(tmp, dst, overwrite: true);
            return true;
        }
        catch { return Fail(tmp); }
    }

    private static bool Fail(string tmp)
    {
        try { File.Delete(tmp); } catch { }
        return false;
    }

    private static string? ReadHeader(Stream f, out byte[] headerBytes, out byte[] baseNonce)
    {
        headerBytes = Array.Empty<byte>();
        baseNonce = Array.Empty<byte>();
        var magic = new byte[8];
        if (Fill(f, magic) != 8 || !magic.AsSpan().SequenceEqual(Magic)) return null;
        var ver = f.ReadByte();
        if (ver != 1) return null;
        var len = new byte[2];
        if (Fill(f, len) != 2) return null;
        var n = BinaryPrimitives.ReadUInt16BigEndian(len);
        if (n is 0 or > 1024) return null;
        var station = new byte[n];
        if (Fill(f, station) != n) return null;
        baseNonce = new byte[8];
        if (Fill(f, baseNonce) != 8) return null;
        var ms = new MemoryStream();
        ms.Write(Magic); ms.WriteByte(1); ms.Write(len); ms.Write(station); ms.Write(baseNonce);
        headerBytes = ms.ToArray();
        return Encoding.UTF8.GetString(station);
    }

    private static byte[] Nonce(byte[] baseNonce, uint index)
    {
        var nonce = new byte[12];
        baseNonce.CopyTo(nonce, 0);
        BinaryPrimitives.WriteUInt32BigEndian(nonce.AsSpan(8), index);
        return nonce;
    }

    private static byte[] Aad(byte[] header, uint index, bool final)
    {
        var aad = new byte[header.Length + 5];
        header.CopyTo(aad, 0);
        BinaryPrimitives.WriteUInt32BigEndian(aad.AsSpan(header.Length), index);
        aad[^1] = final ? (byte)1 : (byte)0;
        return aad;
    }

    private static int Fill(Stream s, byte[] buf, int count = -1)
    {
        if (count < 0) count = buf.Length;
        var got = 0;
        while (got < count)
        {
            var r = s.Read(buf, got, count - got);
            if (r <= 0) break;
            got += r;
        }
        return got;
    }
}
