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

            string? url = null, fullUrl = null;
            long size = 0, fullSize = 0;
            var localBase = AppBase.LocalId;

            if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
                foreach (var a in assets.EnumerateArray())
                {
                    var name = a.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                    if (!name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)
                        && !name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) continue;

                    var u = a.TryGetProperty("browser_download_url", out var uu) ? uu.GetString() : null;
                    var s = a.TryGetProperty("size", out var ss) ? ss.GetInt64() : 0;
                    if (u is null) continue;

                    // بستهٔ کوچک: فقط فایل‌های خودِ برنامه. نامش شناسهٔ «پایه»
                    // را با خود دارد؛ فقط وقتی به کار می‌آید که پایهٔ نصب‌شده
                    // دقیقاً همان باشد، وگرنه نیمی از فایل‌ها ناجور می‌شوند.
                    var baseId = AppBase.IdInAssetName(name);
                    if (baseId is not null)
                    {
                        if (localBase.Length > 0 && baseId == localBase) { url = u; size = s; }
                        continue;
                    }

                    fullUrl ??= u;
                    if (fullSize == 0) fullSize = s;
                }

            if (url is null) { url = fullUrl; size = fullSize; }

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

    /// <summary>
    /// نصبِ نسخهٔ گرفته‌شده و راه‌اندازیِ دوباره.
    ///
    /// دو حالت دارد:
    ///   • ‎.exe‎ — نصاب است، همان اجرا می‌شود.
    ///   • ‎.zip‎ — بستهٔ بی‌نصب. کنارِ برنامه باز می‌شود و یک دستورِ کوچک
    ///     می‌نویسیم که صبر کند تا برنامه بسته شود، فایل‌های تازه را جای
    ///     فایل‌های کهنه بگذارد و دوباره برنامه را باز کند.
    ///
    /// ⚠️ خودِ برنامه نمی‌تواند فایل‌های در حالِ اجرای خودش را جابه‌جا کند —
    /// برای همین کار به آن دستورِ بیرونی سپرده می‌شود و برنامه بلافاصله بسته
    /// می‌شود. اگر بستن را فراموش کنید، جابه‌جایی شکست می‌خورد.
    /// </summary>
    public static bool Launch(string packagePath)
    {
        try
        {
            if (packagePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                return LaunchZip(packagePath);

            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(packagePath)
            {
                UseShellExecute = true,
            });
            return true;
        }
        catch { return false; }
    }

    private static bool LaunchZip(string zipPath)
    {
        var exe = Environment.ProcessPath;
        if (exe is null) return false;
        var appDir = Path.GetDirectoryName(exe)!;

        // بسته در یک پوشهٔ کنارِ فایلِ زیپ باز می‌شود، نه روی خودِ برنامه
        var staging = Path.Combine(Path.GetDirectoryName(zipPath)!, "staging");
        if (Directory.Exists(staging)) Directory.Delete(staging, true);
        System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, staging);

        // بعضی بسته‌ها یک پوشهٔ تکیِ بیرونی دارند — همان پوشه منبع است
        var top = Directory.GetDirectories(staging);
        var src = top.Length == 1 && Directory.GetFiles(staging).Length == 0 ? top[0] : staging;

        var pid = Environment.ProcessId;
        var script = Path.Combine(Path.GetDirectoryName(zipPath)!, "apply-update.cmd");
        // ‎robocopy‎ کدِ خروجیِ ۰ تا ۷ را «موفق» می‌شمارد، پس با ‎exit /b 0‎ بسته می‌شود
        File.WriteAllText(script, $"""
            @echo off
            chcp 65001 >nul
            :wait
            tasklist /FI "PID eq {pid}" | find "{pid}" >nul
            if not errorlevel 1 (
              timeout /t 1 /nobreak >nul
              goto wait
            )
            robocopy "{src}" "{appDir}" /E /IS /IT /R:3 /W:2 >nul
            start "" "{exe}"
            rmdir /s /q "{staging}" >nul 2>&1
            exit /b 0
            """, System.Text.Encoding.UTF8);

        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = "/c \"" + script + "\"",
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = appDir,
        });
        return true;
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

/// <summary>
/// ══ «پایه»ی نصب ═════════════════════════════════════════════════════════════
/// بستهٔ کامل ۵۷ مگابایت است، ولی از هر ساخت به ساختِ بعدی معمولاً فقط دو
/// مگابایتش عوض می‌شود: خودِ فایل‌های برنامه. بقیه — خودِ دات‌نت، اِوالونیا،
/// اسکیا و بقیهٔ کتابخانه‌ها — همان‌اند و تا وقتی نسخهٔ بسته‌ها عوض نشود
/// دست‌نخورده می‌مانند.
///
/// پس هر ساخت یک «شناسهٔ پایه» دارد که از همان فایل‌های ثابت ساخته می‌شود و
/// در فایلِ ‎base.id‎ کنارِ برنامه می‌نشیند. بستهٔ کوچک هم همان شناسه را در
/// نامش دارد. برنامه فقط وقتی بستهٔ کوچک را می‌گیرد که دو شناسه یکی باشند؛
/// اگر پایه عوض شده باشد، خودبه‌خود می‌رود سراغِ بستهٔ کامل.
///
/// ⚠️ بستهٔ کوچک عمداً ‎base.id‎ ندارد — یعنی شناسهٔ روی دیسک همان می‌ماند و
/// درست هم همین است: پایه که عوض نشده.
/// </summary>
public static class AppBase
{
    /// <summary>شناسهٔ پایهٔ همین نصب. خالی یعنی «نمی‌دانم» → بستهٔ کامل.</summary>
    public static string LocalId
    {
        get
        {
            try
            {
                var dir = Path.GetDirectoryName(Environment.ProcessPath);
                if (dir is null) return "";
                var f = Path.Combine(dir, "base.id");
                return File.Exists(f) ? File.ReadAllText(f).Trim() : "";
            }
            catch { return ""; }
        }
    }

    /// <summary>«PumpYaqobi-app-1a2b3c4d.zip» → «1a2b3c4d»؛ وگرنه ‎null‎.</summary>
    public static string? IdInAssetName(string name)
    {
        const string prefix = "PumpYaqobi-app-";
        if (!name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return null;
        var rest = Path.GetFileNameWithoutExtension(name)[prefix.Length..];
        return rest.Length > 0 ? rest : null;
    }
}
