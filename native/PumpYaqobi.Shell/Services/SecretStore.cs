using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace PumpYaqobi.App.Services;

/// <summary>
/// ══ جای امنِ توکن‌ها — نه داخلِ یک فایلِ متنیِ ساده ════════════════════════
///
/// گزارشِ صاحب ریپو: «توکن را داخلِ فایلِ متنیِ ساده یا تنظیماتِ قابلِ مشاهده
/// ذخیره نکن.» و حق داشت: تا امروز <c>settings.json</c> در
/// <c>%APPDATA%\PumpYaqobi</c> این‌ها را <b>خام</b> نگه می‌داشت —
/// توکنِ حساب، توکنِ تازه‌سازی (که روی سرور <b>نود روز</b> عمر دارد) و توکنِ
/// دستگاه. هر کسی که آن پوشه را می‌خواند — یا یک بکاپِ همگام‌شده روی درایوِ
/// ابری — حسابِ کاربر را با خودش می‌برد.
///
/// ── قاعده ──────────────────────────────────────────────────────────────────
/// <code>
///   ویندوز      DPAPI (CryptProtectData، دامنهٔ «همین کاربر»)
///   بقیه        AES-GCM با کلیدی که فقط خودِ کاربر می‌خواند (chmod 600)
/// </code>
///
/// روی ویندوز — که برنامهٔ واقعی همان‌جا می‌دود — کلید مالِ خودِ ویندوز است و
/// هیچ‌جا روی دیسک نمی‌نشیند؛ کپیِ فایل روی کامپیوترِ دیگر باز نمی‌شود. روی
/// لینوکس و مک (سنجه‌ها و توسعه) کلید در فایلی کنارِ خودِ تنظیمات می‌ماند:
/// این «به‌اندازهٔ DPAPI امن» نیست و ادعایش را هم نمی‌کنیم — ولی از متنِ خام
/// بهتر است و کارِ سنجه‌ها را هم نمی‌خواباند.
///
/// ── سه قاعده که باید بمانند ────────────────────────────────────────────────
///
///  • <b>هیچ‌وقت استثنا بیرون نمی‌دهد.</b> پروفایلِ خرابِ ویندوز یا پوشهٔ
///    بی‌اجازه نباید جلوی باز شدنِ برنامه را بگیرد. نشد ⇒ همان متنِ ورودی
///    برمی‌گردد و برنامه کار می‌کند.
///
///  • <b>مقدارِ کهنهٔ خام هم خوانده می‌شود.</b> <see cref="Unprotect"/> هر
///    رشته‌ای که پیشوندِ ما را نداشته باشد همان‌طور که هست پس می‌دهد، پس
///    نصب‌های امروزی با به‌روزرسانی از حساب بیرون نمی‌افتند و کدِ شش‌رقمی را
///    دوباره نمی‌پرسند. با اولین ذخیره خودشان رمز می‌شوند.
///
///  • <b>هیچ توکنی در لاگ نمی‌رود.</b> این‌جا هیچ <c>Console</c> و هیچ
///    نوشتنی در <c>crash.log</c> نیست، حتی در مسیرِ خطا.
/// </summary>
public static class SecretStore
{
    /// <summary>نشانِ «این رمز شده است». نبودنش یعنی مقدارِ کهنهٔ خام.</summary>
    private const string Prefix = "enc:v1:";

    /// <summary>
    /// یک رشتهٔ حساس ⇒ همان، رمز شده. خالی خالی می‌ماند (وگرنه «توکن ندارم»
    /// با «توکنِ رمزشدهٔ خالی» قاطی می‌شد و <c>Activated</c> دروغ می‌گفت).
    /// </summary>
    public static string Protect(string? plain)
    {
        if (string.IsNullOrEmpty(plain)) return "";
        if (plain.StartsWith(Prefix, StringComparison.Ordinal)) return plain;  // از قبل رمز است
        try
        {
            var raw = Encoding.UTF8.GetBytes(plain);
            var sealed_ = OperatingSystem.IsWindows() ? Dpapi.Protect(raw) : Portable.Protect(raw);
            return Prefix + Convert.ToBase64String(sealed_);
        }
        catch
        {
            //  نشد ⇒ خام می‌ماند. یک توکنِ خام بد است، ولی کاربری که
            //  نمی‌تواند وارد شود بدتر است.
            return plain;
        }
    }

    /// <summary>رمزشده ⇒ خودش. هر چیزِ دیگری همان‌طور که هست برمی‌گردد.</summary>
    public static string Unprotect(string? stored)
    {
        if (string.IsNullOrEmpty(stored)) return "";
        if (!stored.StartsWith(Prefix, StringComparison.Ordinal)) return stored;  // کهنهٔ خام
        try
        {
            var blob = Convert.FromBase64String(stored[Prefix.Length..]);
            var raw = OperatingSystem.IsWindows() ? Dpapi.Unprotect(blob) : Portable.Unprotect(blob);
            return Encoding.UTF8.GetString(raw);
        }
        catch
        {
            //  کلیدِ عوض‌شده، پروفایلِ دیگر، فایلِ دست‌خورده — همه یعنی «این
            //  توکن دیگر مالِ ما نیست». خالی برمی‌گردانیم تا برنامه دوباره
            //  ورود بخواهد، نه این‌که با یک رشتهٔ بی‌معنا به سرور بزند.
            return "";
        }
    }

    /// <summary>آیا این مقدار واقعاً رمز شده — برای آزمون‌ها.</summary>
    public static bool IsProtected(string? stored) =>
        !string.IsNullOrEmpty(stored) && stored.StartsWith(Prefix, StringComparison.Ordinal);

    // ── ویندوز: DPAPI، بی هیچ بستهٔ تازه‌ای ─────────────────────────────
    //
    //  ⚠️ عمداً P/Invoke است و نه بستهٔ ‎System.Security.Cryptography.ProtectedData‎:
    //  همان کاری که ‎WaveRecorder‎ با ‎winmm‎ می‌کند. یک وابستگیِ کمتر.

    private static class Dpapi
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct DataBlob { public int cbData; public IntPtr pbData; }

        private const int CryptProtectUiForbidden = 0x1;

        //  ⛔ `System32` فقط — همان قاعدهٔ `WaveRecorder`: DLLِ هم‌نامی که کنارِ
        //  برنامه گذاشته شود نباید به‌جای DPAPIِ خودِ ویندوز رازها را ببیند.
        [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        private static extern bool CryptProtectData(ref DataBlob pDataIn, string? szDataDescr,
            IntPtr pOptionalEntropy, IntPtr pvReserved, IntPtr pPromptStruct, int dwFlags,
            out DataBlob pDataOut);

        [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        private static extern bool CryptUnprotectData(ref DataBlob pDataIn, IntPtr ppszDataDescr,
            IntPtr pOptionalEntropy, IntPtr pvReserved, IntPtr pPromptStruct, int dwFlags,
            out DataBlob pDataOut);

        [DllImport("kernel32.dll")]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        private static extern IntPtr LocalFree(IntPtr hMem);

        public static byte[] Protect(byte[] raw) => Run(raw, encrypt: true);
        public static byte[] Unprotect(byte[] blob) => Run(blob, encrypt: false);

        private static byte[] Run(byte[] input, bool encrypt)
        {
            var inBlob = new DataBlob();
            var outBlob = new DataBlob();
            try
            {
                inBlob.cbData = input.Length;
                inBlob.pbData = Marshal.AllocHGlobal(input.Length);
                Marshal.Copy(input, 0, inBlob.pbData, input.Length);

                var ok = encrypt
                    ? CryptProtectData(ref inBlob, null, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero,
                                       CryptProtectUiForbidden, out outBlob)
                    : CryptUnprotectData(ref inBlob, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero,
                                         CryptProtectUiForbidden, out outBlob);
                //  ⚠️ پیامِ خطای ویندوز عمداً خوانده نمی‌شود: چیزی برای گفتن
                //  به کاربر ندارد و فقط راهِ درز کردنِ جزئیات است.
                if (!ok) throw new CryptographicException("DPAPI");

                var result = new byte[outBlob.cbData];
                Marshal.Copy(outBlob.pbData, result, 0, outBlob.cbData);
                return result;
            }
            finally
            {
                if (inBlob.pbData != IntPtr.Zero) Marshal.FreeHGlobal(inBlob.pbData);
                if (outBlob.pbData != IntPtr.Zero) LocalFree(outBlob.pbData);
            }
        }
    }

    // ── بقیهٔ سیستم‌عامل‌ها: AES-GCM با کلیدِ فقط-خودِ-کاربر ─────────────

    private static class Portable
    {
        private const int NonceSize = 12;
        private const int TagSize = 16;

        public static byte[] Protect(byte[] raw)
        {
            var nonce = RandomNumberGenerator.GetBytes(NonceSize);
            var cipher = new byte[raw.Length];
            var tag = new byte[TagSize];
            using var aes = new AesGcm(Key(), TagSize);
            aes.Encrypt(nonce, raw, cipher, tag);

            var outp = new byte[NonceSize + TagSize + cipher.Length];
            Buffer.BlockCopy(nonce, 0, outp, 0, NonceSize);
            Buffer.BlockCopy(tag, 0, outp, NonceSize, TagSize);
            Buffer.BlockCopy(cipher, 0, outp, NonceSize + TagSize, cipher.Length);
            return outp;
        }

        public static byte[] Unprotect(byte[] blob)
        {
            if (blob.Length < NonceSize + TagSize) throw new CryptographicException("short");
            var nonce = blob.AsSpan(0, NonceSize).ToArray();
            var tag = blob.AsSpan(NonceSize, TagSize).ToArray();
            var cipher = blob.AsSpan(NonceSize + TagSize).ToArray();
            var raw = new byte[cipher.Length];
            using var aes = new AesGcm(Key(), TagSize);
            aes.Decrypt(nonce, cipher, tag, raw);   // برچسبِ غلط ⇒ استثنا، و بالا خالی می‌شود
            return raw;
        }

        //  ⛔ **کلید به پوشه‌اش بسته است، نه به «آخرین باری که پرسیدیم».**
        //
        //  پیش از این یک `_key`ِ تنها بود و عوض شدنِ پوشه آن را دور
        //  می‌ریخت. در خودِ برنامه پوشه هیچ‌وقت عوض نمی‌شود، ولی یک کَشِ
        //  بی‌نام ذاتاً شکننده است: هر کسی که پوشه را عوض کند، کلیدِ
        //  پوشهٔ دیگری را هم باطل می‌کند. حالا هر پوشه کلیدِ خودش را دارد
        //  و باطل کردن بی‌معنا شده.
        private static readonly Dictionary<string, byte[]> Keys = new(StringComparer.Ordinal);
        private static readonly object Gate = new();

        /// <summary>
        /// کلیدِ همین کاربر. یک بار ساخته می‌شود و کنارِ خودِ تنظیمات می‌ماند
        /// با اجازهٔ ۶۰۰ — یعنی فقط خودِ کاربر می‌خواندش.
        /// </summary>
        private static byte[] Key()
        {
            var dir = AppSettings.Dir;
            lock (Gate)
            {
                if (Keys.TryGetValue(dir, out var have)) return have;

                var path = Path.Combine(dir, "secret.key");
                if (File.Exists(path))
                {
                    var found = Convert.FromBase64String(File.ReadAllText(path).Trim());
                    if (found.Length == 32) return Keys[dir] = found;
                }

                var made = RandomNumberGenerator.GetBytes(32);
                Directory.CreateDirectory(dir);
                File.WriteAllText(path, Convert.ToBase64String(made));
                if (!OperatingSystem.IsWindows())
                    try { File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite); }
                    catch { }
                return Keys[dir] = made;
            }
        }

        public static void Forget() { lock (Gate) Keys.Clear(); }
    }

    /// <summary>
    /// ⚠️ دیگر لازم نیست — کلید به پوشه‌اش بسته است. فقط برای آزمونی که
    /// می‌خواهد «انگار تازه بالا آمده‌ایم» را بسازد نگه داشته شده.
    /// </summary>
    public static void ForgetKey() => Portable.Forget();
}
