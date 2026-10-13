using System.Net.Http;
using System.Text.Json;

namespace PumpYaqobi.App.Services;

/// <summary>
/// ══ درخواستِ عکس/PDFِ بات و جوابِ آن (۱۴۰۵/۰۷/۲۱) ═══════════════════════════
///
/// درخواست روی همان پاسخِ <c>/rate</c> می‌آید (‎reqs[]‎) — اتصالِ تازه‌ای نیست؛
/// جواب با <c>POST /api/pump/device/report/:id</c> می‌رود. سمتِ سرور:
/// <c>lib/station-reports.js</c> در ریپوی <c>shop</c> — هیچ چیز آن‌جا ذخیره نمی‌شود.
/// ⛔ فقط با توکنِ دستگاهِ همین پمپ؛ سرور درخواستِ پمپِ دیگری را این‌جا نمی‌دهد.
/// </summary>
public sealed partial class CloudLink
{
    /// <summary>بیشترین درخواستی که از یک پاسخ برداشته می‌شود.</summary>
    public const int MaxRequests = 4;

    /// <summary>‎{reqs: [{id, kind, arg}]}‎ ⇒ درخواست‌ها. خالص — آزمون دارد.</summary>
    public static List<BotRequest> ParseBotRequests(JsonElement json)
    {
        var list = new List<BotRequest>();
        if (json.ValueKind != JsonValueKind.Object || !json.TryGetProperty("reqs", out var a)
            || a.ValueKind != JsonValueKind.Array) return list;
        foreach (var r in a.EnumerateArray())
        {
            if (list.Count >= MaxRequests) break;
            if (r.ValueKind != JsonValueKind.Object) continue;
            var id = r.TryGetProperty("id", out var i) && i.ValueKind == JsonValueKind.String ? i.GetString() ?? "" : "";
            var kind = r.TryGetProperty("kind", out var k) && k.ValueKind == JsonValueKind.String ? k.GetString() ?? "" : "";
            if (id.Length == 0 || id.Length > 64 || kind.Length == 0) continue;
            var arg = r.TryGetProperty("arg", out var g) && g.ValueKind == JsonValueKind.Object ? g.Clone() : default;
            list.Add(new BotRequest(id, kind, arg));
        }
        return list;
    }

    /// <summary>
    /// جوابِ یک درخواست. ⚠️ با مهلتِ بلند (‎BigHttp‎)، نه بیست ثانیه: چند PDF روی
    /// اینترنتِ کند بیش از آن طول می‌کشد. هیچ‌وقت استثنا بیرون نمی‌دهد.
    /// </summary>
    public async Task<CloudResult> ReportAnswerAsync(string id, BotReport report, CancellationToken ct = default)
    {
        if (!Activated || string.IsNullOrWhiteSpace(id)) return CloudResult.No("فعال نشده", "not_activated");
        object body = report.Ok
            ? new
            {
                ok = true,
                caption = report.Caption,
                files = report.Files.Select(f => new { name = f.Name, mime = f.Mime, b64 = Convert.ToBase64String(f.Data) }).ToList(),
                extra = report.Extra,
            }
            : new { ok = false, error = report.Error };
        var req = Build(HttpMethod.Post, "/api/pump/device/report/" + Uri.EscapeDataString(id), body,
                        _settings.CloudDeviceToken);
        var r = await SendOn(BigHttp, req, ct);
        return r.Ok ? CloudResult.Done : CloudResult.No(r.Why, r.Code);
    }
}
