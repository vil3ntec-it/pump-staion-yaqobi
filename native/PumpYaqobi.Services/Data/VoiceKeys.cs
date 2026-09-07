namespace PumpYaqobi.Services.Data;

/// <summary>
/// ══ کلیدِ صدای هر حساب ═════════════════════════════════════════════════════
/// رونوشتِ ‎_vxKeyOf(pid, sid)‎ و ‎_vxLiveKey(key)‎ی نسخهٔ وب:
///
///     ‎p&lt;شخص&gt;‎            → حسابِ اصلی
///     ‎p&lt;شخص&gt;|s&lt;حساب&gt;‎  → یک زیرحساب
///
/// ⚠️ زیرحساب کلیدِ **خودش** را دارد، نه کلیدِ شخص. در نسخهٔ وب هم همین بود و
/// دلیلش روشن است: یک قرض‌دار می‌تواند چند موتر داشته باشد و هر کدام دفترِ
/// جدا؛ اگر همه یک کلید داشتند، صدا فقط حسابِ اصلی را باز می‌کرد و کاربر باید
/// باز هم دستی زیرحساب را می‌گشت.
///
/// شکلِ رشته را عوض نکنید: صداهایی که کاربر تا امروز ثبت کرده با همین کلیدها
/// در دیتابیس نشسته‌اند و کلیدِ تازه یعنی «هیچ‌کدام شناخته نمی‌شوند».
/// </summary>
public static class VoiceKeys
{
    public static string Of(long debtorId) => "p" + debtorId;

    public static string Of(long debtorId, long accountId) => "p" + debtorId + "|s" + accountId;

    /// <summary>
    /// کلید → (شخص، زیرحساب). ‎null‎ یعنی کلید شکلِ درستی ندارد؛ ‎Account‎ی
    /// ‎null‎ یعنی حسابِ اصلی.
    /// </summary>
    public static (long Debtor, long? Account)? Parse(string? key)
    {
        if (string.IsNullOrEmpty(key) || key[0] != 'p') return null;

        var bar = key.IndexOf('|');
        var head = bar < 0 ? key[1..] : key[1..bar];
        if (!long.TryParse(head, out var debtor)) return null;

        if (bar < 0) return (debtor, null);

        var tail = key[(bar + 1)..];
        if (tail.Length < 2 || tail[0] != 's') return null;
        return long.TryParse(tail[1..], out var acct) ? (debtor, acct) : null;
    }
}
