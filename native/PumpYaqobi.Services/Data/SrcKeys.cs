using System.Globalization;
using System.Text.RegularExpressions;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;

namespace PumpYaqobi.Services.Data;

/// <summary>
/// ══ شورا، ب۲ — کلیدهای منبع بی شمارهٔ محلی ════════════════════════════════
///
/// کلیدِ منبع (‎SrcKey‎) می‌گوید یک ردیفِ خودکار از کدام ورق/پارچه/رسید آمده.
/// تا ۳.۱.۲۴۰ شمارهٔ <b>محلیِ</b> همان منبع داخلش بود (‎"id17|day|0"‎ ·
/// ‎"p-12-day"‎ · ‎"wq-sales-17-day"‎ · ‎"parcha|id5"‎) — و آن شماره روی کامپیوترِ
/// دوم به ردیفِ <b>دیگری</b> اشاره می‌کرد، پس ثبتِ دوبارهٔ همان ورق ردیفِ
/// دوتایی می‌ساخت. از این نسخه جای شماره «u» + شناسهٔ سراسری (‎SyncUid‎) است.
///
/// ⛔ همهٔ سازنده‌های کلید از همین‌جا می‌گذرند؛ و
/// <see cref="Migrate(string, Func{string, long, string?})"/> تنها جای ترجمهٔ
/// شکلِ کهنه است (مهاجرتِ یک‌بارهٔ ‎PumpDbFactory.MigrateSrcKeys‎).
/// ⚠️ ‎SyncUid‎ِ ردیفِ ذخیره‌شده همیشه هست (‎PatchSyncUid‎ و ‎Stamp‎)؛ اگر نبود
/// (شیءِ کهنه‌ای که هرگز ذخیره نشده) به شمارهٔ محلی برمی‌گردد و مهاجرتِ بعدی
/// آن را هم درست می‌کند.
/// </summary>
public static class SrcKeys
{
    /// <summary>نشانهٔ یک منبع در کلید: ‎"u&lt;SyncUid&gt;"‎ (یا شمارهٔ محلی برای شیءِ نوشته‌نشده).</summary>
    public static string Tok(EntityBase e) =>
        !string.IsNullOrEmpty(e.SyncUid) ? "u" + e.SyncUid : e.Id.ToString(CultureInfo.InvariantCulture);

    private static string Kind(ShiftKind k) => k == ShiftKind.Night ? "night" : "day";

    /// <summary>شناسهٔ ورق در کلیدهای ثبتِ ورق — ورقی که از سایت آمده با شناسهٔ خودِ سایت.</summary>
    public static string Waraq(WaraqEntry w) =>
        !string.IsNullOrWhiteSpace(w.LegacyId) ? w.LegacyId!
        : !string.IsNullOrEmpty(w.SyncUid) ? "u" + w.SyncUid
        : "id" + w.Id.ToString(CultureInfo.InvariantCulture);

    /// <summary>ردیفِ «فروش ورق»ِ گاوصندوق.</summary>
    public static string WaraqSales(WaraqEntry w, ShiftKind k) => "wq-sales-" + Tok(w) + "-" + Kind(k);

    /// <summary>پایهٔ ورقی که از یک شیفتِ پارچه آمده: ‎"p-u…-day"‎.</summary>
    public static string Shift(FuelType fuel, ParchaReport rep, ShiftKind k) =>
        (fuel == FuelType.Diesel ? "d-" : "p-") + Tok(rep) + "-" + Kind(k);

    /// <summary>ردیفِ حسابی که از «رسید پارچه‌ها» آمده.</summary>
    public static string ParchaReceipt(ParchaReceipt r) =>
        "parcha|" + (r.LegacyId is { Length: > 0 } ? r.LegacyId
                    : !string.IsNullOrEmpty(r.SyncUid) ? "u" + r.SyncUid
                    : "id" + r.Id.ToString(CultureInfo.InvariantCulture));

    private static readonly Regex OldWaraq = new(@"^id(\d+)(\|(?:day|night)\|\d+)$", RegexOptions.CultureInvariant);
    private static readonly Regex OldSales = new(@"^wq-sales-(\d+)(-(?:day|night))$", RegexOptions.CultureInvariant);
    private static readonly Regex OldShift = new(@"^([pd]-)(\d+)(-(?:day|night))$", RegexOptions.CultureInvariant);
    private static readonly Regex OldParcha = new(@"^parcha\|id(\d+)$", RegexOptions.CultureInvariant);

    /// <summary>این کلید هنوز شمارهٔ محلی دارد؟</summary>
    public static bool IsLocal(string? key) =>
        key is { Length: > 0 } && (OldWaraq.IsMatch(key) || OldSales.IsMatch(key) || OldShift.IsMatch(key) || OldParcha.IsMatch(key));

    /// <summary>
    /// شکلِ کهنه ⇒ شکلِ تازه. <paramref name="uidOf"/>(جدول، شماره) شناسهٔ سراسریِ
    /// همان منبع را می‌دهد؛ منبعِ نبوده (پاک‌شده) ⇒ همان کلید، دست‌نخورده.
    /// جدول‌ها: ‎"WaraqEntries"‎ · ‎"Reports"‎ · ‎"ParchaReceipts"‎.
    /// </summary>
    public static string Migrate(string key, Func<string, long, string?> uidOf)
    {
        Match m;
        if ((m = OldWaraq.Match(key)).Success && Uid("WaraqEntries", m.Groups[1].Value) is { } a) return "u" + a + m.Groups[2].Value;
        if ((m = OldSales.Match(key)).Success && Uid("WaraqEntries", m.Groups[1].Value) is { } b) return "wq-sales-u" + b + m.Groups[2].Value;
        if ((m = OldShift.Match(key)).Success && Uid("Reports", m.Groups[2].Value) is { } c) return m.Groups[1].Value + "u" + c + m.Groups[3].Value;
        if ((m = OldParcha.Match(key)).Success && Uid("ParchaReceipts", m.Groups[1].Value) is { } d) return "parcha|u" + d;
        return key;

        string? Uid(string table, string id) =>
            long.TryParse(id, NumberStyles.None, CultureInfo.InvariantCulture, out var n) && uidOf(table, n) is { Length: > 0 } u ? u : null;
    }

    /// <summary>
    /// ‎"p-…-day"‎ ⇒ (تیل، نشانهٔ پارچه، شب؟). نشانه یا «u…» است یا شمارهٔ کهنه.
    /// </summary>
    public static bool TryParseShift(string? key, out FuelType fuel, out string tok, out bool night)
    {
        var m = Regex.Match(key ?? "", @"^([pd])-(.+)-(day|night)$", RegexOptions.CultureInvariant);
        fuel = m.Success && m.Groups[1].Value == "d" ? FuelType.Diesel : FuelType.Petrol;
        tok = m.Success ? m.Groups[2].Value : "";
        night = m.Success && m.Groups[3].Value == "night";
        return m.Success && tok.Length > 0 && !tok.Contains("live", StringComparison.Ordinal);
    }
}
