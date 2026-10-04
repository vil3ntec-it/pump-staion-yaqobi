using System.Globalization;
using System.Net.Http;
using System.Text.Json;

namespace PumpYaqobi.App.Services;

/// <summary>یک فرمانِ «نرخِ اتحادیه» که صاحبِ پمپ در تلگرام نوشت. ‎null‎ یعنی این تیل را دست نزن.</summary>
public sealed record RateCommand(string Id, decimal? Petrol, decimal? Diesel, string By);

/// <summary>
/// یک پرسشِ <c>/rate</c>: رسید؟ فرمان؟ و <c>WaitMax</c> — سقفِ «درجا»ی سرور؛
/// صفر یعنی سرورِ حسابِ کهنه که پرسشِ باز را نمی‌شناسد.
/// </summary>
public sealed record RatePoll(bool Ok, RateCommand? Cmd, int WaitMax)
{
    public static readonly RatePoll None = new(false, null, 0);
}

/// <summary>
/// ══ نرخِ اتحادیه از تلگرام ═══════════════════════════════════════════════
///
/// خواستهٔ صاحب ریپو (۱۴۰۵/۰۷/۱۶): «نرخِ اتحادیه رو توی تلگرام بنویسم —
/// پطرول ۴۵ دیزل ۹۹ — و اتومات توی برنامهٔ کامپیوتر لایف آپدیت کنه… روی
/// حساب‌های دیگهٔ کاربران تأثیری نذاره.»
///
/// ⛔ فقط با <b>توکنِ دستگاهِ همین پمپ</b>: سرور فرمان را از خودِ توکن
/// پیدا می‌کند، پس هیچ نرخی از پمپِ دیگری این‌جا نمی‌نشیند. سمتِ سرور:
/// <c>lib/station-rates.js</c> در ریپوی <c>shop</c>.
/// </summary>
public sealed partial class CloudLink
{
    /// <summary>فرمانِ در صفِ همین پمپ — یا ‎null‎. هیچ‌وقت استثنا بیرون نمی‌دهد.</summary>
    public async Task<RateCommand?> RateCommandAsync(CancellationToken ct = default) =>
        (await RatePollAsync(0, ct)).Cmd;

    /// <summary>
    /// «درجا»: با <paramref name="waitSeconds"/> بزرگ‌تر از صفر پرسش باز می‌ماند و
    /// سرور همان لحظه‌ای که بات فرمان ساخت جواب می‌دهد. ⚠️ باید کمتر از مهلتِ
    /// ۲۰ ثانیه‌ایِ <c>Http</c> بماند. هیچ‌وقت استثنا بیرون نمی‌دهد.
    /// </summary>
    public async Task<RatePoll> RatePollAsync(int waitSeconds, CancellationToken ct = default)
    {
        if (!Activated) return RatePoll.None;
        var path = "/api/pump/device/rate" + (waitSeconds > 0 ? "?wait=" + waitSeconds : "");
        var (ok, json, _, _) = await DevGetAsync(path, ct);
        if (!ok || json.ValueKind != JsonValueKind.Object) return RatePoll.None;
        //  نسخهٔ «تنظیماتِ زنده» روی همین پاسخ می‌آید — درخواستِ جدایی نیست
        LiveConfig.NoteServerVersion(json);
        var max = json.TryGetProperty("waitMax", out var w) && w.ValueKind == JsonValueKind.Number
                  && w.TryGetInt32(out var n) ? Math.Max(0, n) : 0;
        return new RatePoll(true, ParseRateCommand(json), max);
    }

    /// <summary>‎{cmd: {id, petrol, diesel, by}}‎ ⇒ فرمان. خالص — آزمون دارد.</summary>
    public static RateCommand? ParseRateCommand(JsonElement json)
    {
        if (!json.TryGetProperty("cmd", out var c) || c.ValueKind != JsonValueKind.Object) return null;
        var id = c.TryGetProperty("id", out var i) && i.ValueKind == JsonValueKind.String ? i.GetString() ?? "" : "";
        if (id.Length == 0) return null;
        decimal? Num(string k) =>
            c.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetDecimal(out var d)
                ? d : null;
        var by = c.TryGetProperty("by", out var b) && b.ValueKind == JsonValueKind.String ? b.GetString() ?? "" : "";
        var p = Num("petrol");
        var dsl = Num("diesel");
        //  همان مرزِ سرور — نرخِ ناممکن این‌جا هم نمی‌نشیند
        if (p is not null && (p < 10 || p > 500)) p = null;
        if (dsl is not null && (dsl < 10 || dsl > 500)) dsl = null;
        if (p is null && dsl is null) return null;
        return new RateCommand(id, p, dsl, by);
    }

    /// <summary>«نشست» یا «ننشست» — تا بات به صاحبِ پمپ بگوید.</summary>
    public async Task<CloudResult> RateAckAsync(string id, bool applied, string note = "",
        CancellationToken ct = default)
    {
        if (!Activated || string.IsNullOrWhiteSpace(id)) return CloudResult.No("فعال نشده", "not_activated");
        var (ok, _, why, code) = await DevPostAsync(
            "/api/pump/device/rate/" + Uri.EscapeDataString(id) + "/ack",
            new { applied, note }, ct);
        return ok ? CloudResult.Done : CloudResult.No(why, code);
    }

    /// <summary>برای پیامِ برنامه: «پطرول 79 · دیزل 80».</summary>
    public static string RateLine(RateCommand c)
    {
        var parts = new List<string>();
        if (c.Petrol is { } p) parts.Add("پطرول " + p.ToString("0.##", CultureInfo.InvariantCulture));
        if (c.Diesel is { } d) parts.Add("دیزل " + d.ToString("0.##", CultureInfo.InvariantCulture));
        return string.Join(" · ", parts);
    }
}
