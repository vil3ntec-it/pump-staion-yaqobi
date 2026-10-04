using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace PumpYaqobi.App.Services;

/// <summary>حالِ یکپارچگیِ فایل‌های خودِ برنامه.</summary>
public enum IntegrityState
{
    /// <summary>این ساخت کلیدِ یکپارچگی ندارد (ساختِ محلی، آزمون‌ها) — هیچ سنجشی نیست.</summary>
    NotSigned,
    /// <summary>هر فایل همان است که ساخته شد.</summary>
    Ok,
    /// <summary>فایلی دست خورده یا کم است، یا امضای فهرست نمی‌خورد.</summary>
    Tampered,
}

public readonly record struct IntegrityResult(IntegrityState State, string Why)
{
    public bool Tampered => State == IntegrityState.Tampered;
}

/// <summary>
/// ══ شورا، پ۲ — سنجشِ یکپارچگیِ فایل‌های برنامه ═════════════════════════════
///
/// قفلِ شش بخشِ اشتراکی در خودِ کامپیوتر تصمیم گرفته می‌شود؛ کسی که یک DLL را
/// عوض کند می‌توانست آن را باز کند. راهِ بی‌نقص وجود ندارد — کار این است که دور
/// زدن <b>گران</b> شود: حالا عوض کردنِ هر فایلِ <c>PumpYaqobi*</c> این سنجش را
/// سرخ می‌کند و باید خودِ سنجش (و کلیدِ داخلِ <c>PumpYaqobi.App.dll</c>) هم
/// دست بخورد.
///
/// <b>بی هیچ رازی</b>: ساختِ CI یک جفت‌کلیدِ P-256ِ <b>یک‌بارمصرف</b> می‌سازد،
/// کلیدِ عمومی را در خودِ اسمبلی می‌نشاند (<c>-p:IntegrityKey=…</c> ⇒
/// <c>[assembly: AssemblyMetadata]</c>)، پس از انتشار هشِ هر فایلِ
/// <c>PumpYaqobi*</c> را در <c>PumpYaqobi.integrity.json</c> امضا می‌کند و
/// کلیدِ خصوصی را دور می‌ریزد. نامِ فایل با «PumpYaqobi» شروع می‌شود، پس همراهِ
/// بستهٔ کوچکِ به‌روزرسانی هم می‌رود.
///
/// ⛔ <b>دفتر هرگز قفل نمی‌شود</b>: ناجوری فقط شش دروازهٔ
/// <see cref="Entitlements.Paid"/> را می‌بندد و به سرورِ حساب گزارش می‌شود
/// (با همان اجازهٔ «گزارشِ خطا»). و بی اینترنت هم همین‌طور کار می‌کند.
/// ⚠️ ساختِ بی کلید (محلی، آزمون‌ها، <c>installer-check</c>) <see cref="IntegrityState.NotSigned"/>
/// است و هیچ چیزی را نمی‌بندد.
/// </summary>
public static class Integrity
{
    public const string ManifestName = "PumpYaqobi.integrity.json";

    /// <summary>کلیدِ عمومیِ این ساخت — خالی یعنی سنجشی نیست.</summary>
    public static string BuildKey => KeyOverride ?? CloudConfig.Metadata("IntegrityKey");

    /// <summary>فقط آزمون‌ها.</summary>
    public static string? KeyOverride { get; set; }

    /// <summary>فقط آزمون‌ها — نتیجهٔ ساختگی به‌جای سنجشِ پوشهٔ برنامه.</summary>
    public static IntegrityResult? TestResult { get; set; }

    private static Lazy<IntegrityResult> _here = new(() => Verify(AppContext.BaseDirectory, BuildKey));

    /// <summary>نتیجهٔ پوشهٔ همین برنامه — یک بار حساب می‌شود.</summary>
    public static IntegrityResult Current => TestResult ?? _here.Value;

    /// <summary>دستِ‌کم یک فایلِ برنامه دست خورده.</summary>
    public static bool Tampered => Current.Tampered;

    /// <summary>سرِ بالا آمدن، روی نخِ دیگر — تا نخستین «باز است؟» منتظرِ هش نماند.</summary>
    public static void Warm() => _ = Task.Run(() =>
    {
        try
        {
            var r = _here.Value;
            if (r.Tampered) CrashGuard.Notice("Integrity", "فایل‌های برنامه دست خورده‌اند: " + r.Why);
        }
        catch { /* سنجش هیچ‌وقت برنامه را نمی‌خواباند */ }
    });

    /// <summary>متنِ امضاشده — هر خط «نام⇥هش»، مرتب با ترتیبِ ثابت.</summary>
    public static string Canonical(IReadOnlyDictionary<string, string> files)
    {
        var sb = new StringBuilder();
        foreach (var kv in files.OrderBy(k => k.Key, StringComparer.Ordinal))
            sb.Append(kv.Key).Append('\t').Append(kv.Value.ToLowerInvariant()).Append('\n');
        return sb.ToString();
    }

    /// <summary>هشِ SHA-256ِ یک فایل، به hexِ کوچک.</summary>
    public static string HashOf(string path)
    {
        using var s = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(s)).ToLowerInvariant();
    }

    /// <summary>
    /// فایل‌هایی که امضا می‌شوند: هر <c>PumpYaqobi*.dll/.exe/.json</c>ِ کنارِ برنامه،
    /// جز خودِ فهرست. ⚠️ <c>.json</c> یعنی <c>deps.json</c> و <c>runtimeconfig.json</c> —
    /// دات‌نت فقط اسمبلی‌هایی را بار می‌کند که <c>deps.json</c> نام می‌برد، پس DLLِ
    /// ناشناسِ کنارِ برنامه بی دست زدن به آن (که امضا شده) بار نمی‌شود. به همین دلیل
    /// «فایلِ اضافه» عمداً ایراد شمرده نمی‌شود: باقی‌ماندهٔ نسخه‌های قدیمی نباید
    /// مشتریِ درستکار را ببندد. <c>PumpYaqobi.ico</c>ِ نصاب هم بیرون است.
    /// </summary>
    public static bool Covered(string name) =>
        name.StartsWith("PumpYaqobi", StringComparison.OrdinalIgnoreCase)
        && !name.Equals(ManifestName, StringComparison.OrdinalIgnoreCase)
        && (name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith(".json", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// پوشهٔ <paramref name="dir"/> با کلیدِ <paramref name="keyB64"/> (SPKIِ base64).
    /// هیچ‌وقت استثنا بیرون نمی‌دهد.
    /// </summary>
    public static IntegrityResult Verify(string dir, string? keyB64)
    {
        if (string.IsNullOrWhiteSpace(keyB64)) return new(IntegrityState.NotSigned, "");
        try
        {
            var path = Path.Combine(dir, ManifestName);
            if (!File.Exists(path)) return new(IntegrityState.Tampered, "فهرستِ امضا نیست");

            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            var files = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var p in doc.RootElement.GetProperty("files").EnumerateObject())
                files[p.Name] = p.Value.GetString() ?? "";
            var sig = Convert.FromBase64String(doc.RootElement.GetProperty("sig").GetString() ?? "");

            using var ec = ECDsa.Create();
            ec.ImportSubjectPublicKeyInfo(Convert.FromBase64String(keyB64.Trim()), out _);
            if (!ec.VerifyData(Encoding.UTF8.GetBytes(Canonical(files)), sig, HashAlgorithmName.SHA256))
                return new(IntegrityState.Tampered, "امضای فهرست نمی‌خورد");

            foreach (var (name, hash) in files)
            {
                if (name.Contains('/') || name.Contains('\\') || name.Contains("..") || !Covered(name))
                    return new(IntegrityState.Tampered, "نامِ ناجور در فهرست: " + name);
                var f = Path.Combine(dir, name);
                if (!File.Exists(f)) return new(IntegrityState.Tampered, "فایل نیست: " + name);
                if (!string.Equals(HashOf(f), hash, StringComparison.OrdinalIgnoreCase))
                    return new(IntegrityState.Tampered, "دست خورده: " + name);
            }

            //  ⛔ فهرستِ خالی چیزی را نمی‌سنجد — فهرستِ ساختگیِ «بی فایل» پذیرفته نشود
            if (!files.Keys.Any(k => k.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)))
                return new(IntegrityState.Tampered, "فهرستِ امضا خالی است");
            return new(IntegrityState.Ok, "");
        }
        catch (Exception ex)
        {
            return new(IntegrityState.Tampered, "فهرستِ امضا خوانده نشد (" + ex.GetType().Name + ")");
        }
    }

    /// <summary>
    /// <c>PumpYaqobi.exe --integrity</c> — برای سنجهٔ CI. کدِ بیرون آمدن: ۰ سالم ·
    /// ۲ بی کلید · ۳ دست‌خورده. هیچ پنجره‌ای باز نمی‌شود.
    /// </summary>
    public static int SelfTestExitCode()
    {
        //  ⛔ همان ‎Current‎ که ‎Entitlements.Allows‎ می‌خواند — نه سنجشِ جدا
        var r = Current;
        try { File.WriteAllText(Path.Combine(Path.GetTempPath(), "pump-integrity.txt"), r.State + " " + r.Why); } catch { }
        return r.State switch { IntegrityState.Ok => 0, IntegrityState.NotSigned => 2, _ => 3 };
    }
}
