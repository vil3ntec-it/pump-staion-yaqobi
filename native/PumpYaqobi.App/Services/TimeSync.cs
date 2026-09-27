using System.Net.Http;
using System.Text.Json;
using PumpYaqobi.Domain;

namespace PumpYaqobi.App.Services;

/// <summary>
/// ══ ساعتِ واقعی از اینترنت ═══════════════════════════════════════════════════
///
/// خواستهٔ صاحب ریپو (۱۴۰۵/۰۷/۱۵): «برنامه خودش بک‌اند بتونه ماه و سال و روز
/// رو از اینترنت ببینه و بفهمه کدوم واقعی است.»
///
/// هر پاسخِ HTTPS از <b>سرورِ حساب</b> (نشانیِ قفل‌شدهٔ <see cref="CloudConfig"/>)
/// یا <b>گیت‌هاب</b> سرآیندِ <c>Date</c> دارد — ساعتِ خودِ آن سرور. همان به
/// <see cref="AppClock.Accept"/> می‌رود. هیچ درخواستِ تازه‌ای برای این ساخته
/// نمی‌شود مگر یک بار سرِ بالا آمدن و هر شش ساعت (<see cref="CheckAsync"/>).
///
/// ⛔ <b>فقط این میزبان‌ها و فقط https</b> — سرورِ خانگی (روی همین شبکه،
/// شاید روی همین کامپیوتر با همین ساعتِ اشتباه) و هر نشانیِ دیگری هیچ‌وقت
/// ساعت نمی‌دهند.
///
/// ⚠️ <b>بسته و باز کردنِ برنامه روی همان روشن‌بودنِ کامپیوتر</b>: آخرین ساعتِ
/// اینترنت همراهِ شمارندهٔ یکنواختِ سیستم در <c>clock.json</c> (کنارِ تنظیمات)
/// می‌نشیند؛ اگر کامپیوتر از آن موقع خاموش نشده (<see cref="BootId"/>)، همان
/// ساعت بی اینترنت هم دقیق برمی‌گردد. پس از خاموش/روشن و بی اینترنت، برنامه
/// جز ساعتِ ویندوز چیزی ندارد — ولی هرگز عقب‌تر از آخرین ساعتِ اینترنت نه.
/// </summary>
public static class TimeSync
{
    /// <summary>پاسخی که رفت‌وبرگشتش از این بیشتر طول کشید ساعت نمی‌دهد.</summary>
    private const long MaxRttMs = 10_000;

    /// <summary>هر چند وقت یک بار ساعت را از نو بپرسیم وقتی هیچ پاسخِ دیگری نیامده.</summary>
    public static readonly TimeSpan Recheck = TimeSpan.FromHours(6);

    private static readonly string[] TrustedHosts =
        new[] { new Uri(CloudConfig.Url("/")).Host }.Concat(Update.UpdateService.TimeHosts).ToArray();

    private static long _lastPersistMono = long.MinValue;

    /// <summary>این میزبان ساعتِ مطمئن می‌دهد؟</summary>
    public static bool IsTrusted(Uri? uri) =>
        uri is { Scheme: "https" } && Array.Exists(TrustedHosts, h => string.Equals(h, uri.Host, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// ساعتِ یک پاسخ. <paramref name="sentMono"/> شمارندهٔ یکنواخت در لحظهٔ
    /// فرستادن است؛ لحظهٔ درستیِ ساعتِ سرور میانهٔ رفت و برگشت گرفته می‌شود.
    /// هیچ‌وقت استثنا بیرون نمی‌دهد.
    /// </summary>
    public static void From(HttpRequestMessage? req, HttpResponseMessage? res, long sentMono)
    {
        try
        {
            if (res?.Headers.Date is not { } date || !IsTrusted(req?.RequestUri)) return;
            var got = AppClock.MonoSource();
            if (got - sentMono > MaxRttMs || got < sentMono) return;
            //  ⚠️ `Date` تا ثانیه گرد شده؛ نیم ثانیه وسطِ همان ثانیه است
            var server = date.ToUnixTimeMilliseconds() + 500;
            if (AppClock.Accept(server, sentMono + (got - sentMono) / 2)) Persist();
        }
        catch { /* ساعت رفاه است؛ هیچ درخواستی نباید برای آن بشکند */ }
    }

    /// <summary>
    /// یک پرسشِ سبک (فقط سرآیند) — سرورِ حساب، وگرنه گیت‌هاب. از حلقهٔ
    /// پس‌زمینه سرِ بالا آمدن و هر <see cref="Recheck"/>، و از پنجرهٔ ساعت.
    /// </summary>
    public static async Task<bool> CheckAsync(CancellationToken ct = default)
    {
        foreach (var url in new[] { CloudConfig.Url("/api/health"), Update.UpdateService.TimeProbeUrl })
        {
            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Head, url);
                var sent = AppClock.MonoSource();
                using var res = CloudLink.TestTransport is { } t
                    ? await t(req, ct)
                    : await Http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
                From(req, res, sent);
                if (res.Headers.Date is not null && AppClock.TrustedAgeMs is >= 0 and < 60_000) return true;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch { /* درِ بعدی */ }
        }
        return false;
    }

    /// <summary>
    /// سرِ بالا آمدن: اگر کامپیوتر از آخرین ساعتِ اینترنت خاموش نشده، همان
    /// لنگر برمی‌گردد؛ وگرنه دستِ‌کم «عقب‌تر از آخرین ساعتِ اینترنت نه».
    /// </summary>
    public static void Restore()
    {
        try
        {
            var path = FilePath();
            if (!File.Exists(path)) return;
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            var r = doc.RootElement;
            var server = r.TryGetProperty("server", out var s) ? s.GetInt64() : 0;
            var tick = r.TryGetProperty("tick", out var t) ? t.GetInt64() : -1;
            var boot = r.TryGetProperty("boot", out var b) ? b.GetString() ?? "" : "";
            if (server <= 0) return;

            var now = AppClock.MonoSource();
            var sameBoot = boot.Length > 0 && boot == BootId() && tick >= 0 && now >= tick;
            if (sameBoot) AppClock.Accept(server, tick);
            else AppClock.AtLeast(server);
        }
        catch { /* فایلِ خراب ⇒ همان ساعتِ ویندوز */ }
    }

    private static void Persist()
    {
        var mono = AppClock.MonoSource();
        if (_lastPersistMono != long.MinValue && mono - _lastPersistMono < 10 * 60_000) return;
        _lastPersistMono = mono;
        try
        {
            var json = JsonSerializer.Serialize(new
            {
                server = AppClock.UnixMs,
                tick = mono,
                boot = BootId(),
            });
            var path = FilePath();
            var tmp = path + ".tmp-" + Environment.ProcessId;
            File.WriteAllText(tmp, json);
            File.Move(tmp, path, overwrite: true);
        }
        catch { /* بدترین حالت: پس از بسته و باز کردن یک پرسشِ دیگر */ }
    }

    /// <summary>
    /// شناسهٔ همین روشن‌بودنِ ویندوز — با هر خاموش/روشن عوض می‌شود. خالی یعنی
    /// «نمی‌دانیم» و آن‌وقت لنگرِ قبلی هرگز دقیق شمرده نمی‌شود.
    /// </summary>
    public static Func<string> BootId { get; set; } = ReadBootId;

    private static string ReadBootId()
    {
        if (!OperatingSystem.IsWindows()) return "";
        try
        {
            using var k = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                @"SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management\PrefetchParameters");
            return k?.GetValue("BootId") is int id ? "w" + id : "";
        }
        catch { return ""; }
    }

    private static string FilePath() => Path.Combine(AppSettings.Dir, "clock.json");

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(8) };

    /// <summary>⚠️ فقط برای آزمون.</summary>
    public static void ResetForTests() => _lastPersistMono = long.MinValue;
}
