using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace PumpYaqobi.App.Update;

/// <summary>نتیجهٔ بررسیِ به‌روزرسانی.</summary>
public sealed record UpdateInfo(
    bool Available, string CurrentVersion, string LatestVersion,
    string? DownloadUrl, long SizeBytes, string? Notes);

/// <summary>
/// ══ به‌روزرسانیِ خودکار ══════════════════════════════════════════════════════
/// برنامه خودش نسخهٔ تازه را می‌گیرد و نصب می‌کند.
///
/// ⚠️ خواستهٔ صریحِ صاحب ریپو: «توش نوشته نباشه از مخزن فلان فلان.»
/// پس نشانیِ منبع فقط همین‌جا، به‌صورت ثابتِ داخلی، هست و **هیچ‌جای رابط
/// کاربری** نامِ مخزن، نامِ کاربری یا نشانی دیده نمی‌شود. پیام‌هایی که کاربر
/// می‌بیند فقط این‌هایند: «نسخهٔ تازه هست»، «در حال گرفتن…»، «آماده نصب است».
/// اگر روزی این نشانی عوض شد، فقط همین خط عوض می‌شود.
/// </summary>
public sealed class UpdateService
{
    // نشانیِ داخلی — هرگز به رابط کاربری راه پیدا نمی‌کند
    private const string FeedUrl =
        "https://api.github.com/repos/vil3ntec-it/pump-staion-yaqobi/releases/latest";

    private static readonly HttpClient Http = CreateClient();

    private static HttpClient CreateClient()
    {
        var c = new HttpClient { Timeout = TimeSpan.FromSeconds(25) };
        // GitHub بدونِ User-Agent پاسخ نمی‌دهد. نامِ برنامه، نه نامِ مخزن.
        c.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("PumpYaqobi", AppVersion.Current));
        c.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return c;
    }

    /// <summary>آیا نسخهٔ تازه‌ای هست؟ خطای شبکه «نسخهٔ تازه‌ای نیست» می‌شود، نه خرابی.</summary>
    public async Task<UpdateInfo> CheckAsync(CancellationToken ct = default)
    {
        var current = AppVersion.Current;
        try
        {
            using var res = await Http.GetAsync(FeedUrl, ct);
            if (!res.IsSuccessStatusCode) return None(current);

            using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
            var root = doc.RootElement;

            var tag = root.TryGetProperty("tag_name", out var t) ? t.GetString() ?? "" : "";
            var latest = NormalizeVersion(tag);
            if (latest.Length == 0) return None(current);

            string? url = null; long size = 0;
            if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
                foreach (var a in assets.EnumerateArray())
                {
                    var name = a.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                    // فقط بستهٔ نصبِ ویندوز
                    if (!name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                        && !name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) continue;
                    url = a.TryGetProperty("browser_download_url", out var u) ? u.GetString() : null;
                    size = a.TryGetProperty("size", out var s) ? s.GetInt64() : 0;
                    if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) break;  // نصاب اولویت دارد
                }

            var notes = root.TryGetProperty("body", out var b) ? b.GetString() : null;
            var newer = Compare(latest, current) > 0;
            return new UpdateInfo(newer && url is not null, current, latest, url, size, notes);
        }
        catch (Exception)
        {
            // نبودِ اینترنت هرگز نباید به‌صورتِ خطا جلوی کاربر بیاید
            return None(current);
        }
    }

    private static UpdateInfo None(string current) => new(false, current, current, null, 0, null);

    /// <summary>«v2.9.430» یا «2.9.430» → «2.9.430».</summary>
    private static string NormalizeVersion(string tag)
    {
        var s = (tag ?? "").Trim();
        if (s.StartsWith("v", StringComparison.OrdinalIgnoreCase)) s = s[1..];
        return Version.TryParse(s, out _) ? s : "";
    }

    /// <summary>مقایسهٔ نسخه — نه رشته‌ای، وگرنه «2.9.9» بزرگ‌تر از «2.9.10» می‌شد.</summary>
    public static int Compare(string a, string b)
    {
        if (!Version.TryParse(a, out var va)) return 0;
        if (!Version.TryParse(b, out var vb)) return 0;
        return va.CompareTo(vb);
    }

    /// <summary>
    /// گرفتنِ بستهٔ نصب. پیشرفت گزارش می‌شود تا کاربر بداند چه خبر است، و
    /// فایل فقط وقتی «آماده» شمرده می‌شود که کاملاً و به همان اندازه رسیده باشد.
    /// </summary>
    public async Task<string?> DownloadAsync(UpdateInfo info, IProgress<double>? progress,
                                             CancellationToken ct = default)
    {
        if (!info.Available || info.DownloadUrl is null) return null;

        var dir = Path.Combine(Services.AppSettings.Dir, "updates");
        Directory.CreateDirectory(dir);
        var name = "PumpYaqobi-" + info.LatestVersion + Path.GetExtension(new Uri(info.DownloadUrl).AbsolutePath);
        var path = Path.Combine(dir, name);
        var partial = path + ".part";

        using (var res = await Http.GetAsync(info.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, ct))
        {
            if (!res.IsSuccessStatusCode) return null;
            var total = res.Content.Headers.ContentLength ?? info.SizeBytes;
            await using var src = await res.Content.ReadAsStreamAsync(ct);
            await using var dst = File.Create(partial);

            var buf = new byte[81920];
            long done = 0;
            int read;
            while ((read = await src.ReadAsync(buf, ct)) > 0)
            {
                await dst.WriteAsync(buf.AsMemory(0, read), ct);
                done += read;
                if (total > 0) progress?.Report(done * 100.0 / total);
            }
        }

        // ⚠️ فایلِ نیمه‌کاره هرگز جای فایلِ نهایی را نمی‌گیرد
        if (info.SizeBytes > 0 && new FileInfo(partial).Length != info.SizeBytes)
        {
            try { File.Delete(partial); } catch { }
            return null;
        }

        if (File.Exists(path)) File.Delete(path);
        File.Move(partial, path);
        return path;
    }

    /// <summary>اجرای نصاب. برنامه پس از این بسته می‌شود تا فایل‌هایش آزاد شوند.</summary>
    public static bool Launch(string installerPath)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(installerPath)
            {
                UseShellExecute = true,
            });
            return true;
        }
        catch { return false; }
    }
}

/// <summary>نسخهٔ همین ساخت — از خودِ اسمبلی خوانده می‌شود، نه دستی.</summary>
public static class AppVersion
{
    public static string Current
    {
        get
        {
            var v = typeof(AppVersion).Assembly.GetName().Version;
            return v is null ? "1.0.0" : $"{v.Major}.{v.Minor}.{v.Build}";
        }
    }
}
