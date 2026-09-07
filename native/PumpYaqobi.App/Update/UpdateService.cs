using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace PumpYaqobi.App.Update;

/// <summary>نتیجهٔ بررسیِ به‌روزرسانی.</summary>
/// <param name="IsSmallPackage">
/// بستهٔ کوچک است (فقط فایل‌های خودِ برنامه، چند مگابایت) نه بستهٔ کامل.
/// این را کاربر باید ببیند — همان چیزی که «هر بار از سر دانلود نکنم» یعنی.
/// </param>
public sealed record UpdateInfo(
    bool Available, string CurrentVersion, string LatestVersion,
    string? DownloadUrl, long SizeBytes, string? Notes, bool IsSmallPackage = false)
{
    /// <summary>«۲٫۱ مگابایت» — اندازهٔ خواندنی.</summary>
    public string SizeText => SizeBytes <= 0
        ? ""
        : (SizeBytes / 1048576.0).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)
          + " مگابایت";

    /// <summary>«به‌روزرسانیِ کوچک — ۲٫۱ مگابایت» یا «بستهٔ کامل — ۵۷ مگابایت».</summary>
    public string PackageText => !Available
        ? ""
        : (IsSmallPackage ? "به‌روزرسانیِ کوچک" : "بستهٔ کامل")
          + (SizeText.Length > 0 ? " — " + SizeText : "");
}

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
            var fullIsSetup = false;
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

                    // بستهٔ کامل. نصاب بر زیپ ترجیح دارد: نصاب میان‌برها و
                    // ثبتِ «برنامه‌ها و قابلیت‌ها» را هم تازه می‌کند، ولی زیپ
                    // فقط فایل‌ها را جابه‌جا می‌کند. پس اگر نصاب در انتشار
                    // باشد، همان برداشته می‌شود — حتی اگر زیپ زودتر آمده باشد.
                    var isSetup = name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase);
                    if (fullUrl is null || (isSetup && !fullIsSetup))
                    {
                        fullUrl = u; fullSize = s; fullIsSetup = isSetup;
                    }
                }

            var small = url is not null;      // بستهٔ کوچکِ هم‌پایه پیدا شد
            if (url is null) { url = fullUrl; size = fullSize; }

            var notes = root.TryGetProperty("body", out var b) ? b.GetString() : null;
            var newer = Compare(latest, current) > 0;
            return new UpdateInfo(newer && url is not null, current, latest, url, size, notes, small);
        }
        catch (Exception)
        {
            // نبودِ اینترنت هرگز نباید به‌صورتِ خطا جلوی کاربر بیاید
            return None(current);
        }
    }

    private static UpdateInfo None(string current) => new(false, current, current, null, 0, null);

    // ══ پوشهٔ نصب ═══════════════════════════════════════════════════════════
    // کاربر خودش انتخاب می‌کند برنامه کجا نصب شود. اگر جایی را انتخاب کند که
    // نوشتن در آن اجازهٔ مدیر می‌خواهد (‎Program Files‎)، به‌روزرسانی باید
    // اجازه بگیرد — نه اینکه بی‌صدا هیچ نکند و کاربر خیال کند به‌روز شده.

    /// <summary>پوشه‌ای که فایل‌های برنامه در آن نشسته‌اند.</summary>
    public static string InstallDir =>
        Path.GetDirectoryName(Environment.ProcessPath) ?? AppContext.BaseDirectory;

    /// <summary>آیا می‌شود بی اجازهٔ مدیر در پوشهٔ نصب نوشت؟</summary>
    public static bool InstallDirWritable => IsWritable(InstallDir);

    /// <summary>
    /// آیا می‌شود بی اجازهٔ مدیر در این پوشه نوشت؟ با نوشتنِ واقعیِ یک فایلِ
    /// کوچک سنجیده می‌شود، نه با نگاه به مسیر — چون اجازه‌ها را می‌شود دستی
    /// عوض کرد و مسیر تنها چیزی را ثابت نمی‌کند.
    /// </summary>
    public static bool IsWritable(string dir)
    {
        try
        {
            var probe = Path.Combine(dir, ".pump-write-test-" + Guid.NewGuid().ToString("N")[..8]);
            using (File.Create(probe, 1, FileOptions.DeleteOnClose)) { }
            return true;
        }
        catch { return false; }
    }

    /// <summary>مسیرِ فایلی که نتیجهٔ آخرین جای‌گزینی در آن نوشته می‌شود.</summary>
    private static string ResultFile =>
        Path.Combine(Services.AppSettings.Dir, "updates", "apply-result.txt");

    /// <summary>
    /// نسخه‌ای که آخرین بار «قرار بود» نصب شود. بعد از باز شدنِ دوبارهٔ
    /// برنامه، همین با نسخهٔ واقعی سنجیده می‌شود.
    /// </summary>
    private static string PendingFile =>
        Path.Combine(Services.AppSettings.Dir, "updates", "pending.txt");

    /// <summary>
    /// ══ «قرار است به این نسخه برویم» ═══════════════════════════════════════
    /// پیش از هر تلاشی نوشته می‌شود. تنها راهِ فهمیدنِ اینکه به‌روزرسانی
    /// **واقعاً** گرفت یا نه، همین است: بعد از باز شدنِ دوباره، نسخهٔ در حالِ
    /// اجرا با این عدد سنجیده می‌شود.
    /// </summary>
    public static void MarkPending(string targetVersion)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(PendingFile)!);
            File.WriteAllText(PendingFile, targetVersion);
        }
        catch { /* ننوشتنش نباید جلوی خودِ به‌روزرسانی را بگیرد */ }
    }

    /// <summary>
    /// نتیجهٔ آخرین به‌روزرسانی: گرفت یا نگرفت، و چه بگوییم.
    /// ‎null‎ یعنی اصلاً به‌روزرسانی‌ای در کار نبوده.
    /// </summary>
    public sealed record Outcome(bool Ok, string Message);

    /// <summary>
    /// ══ واقعاً به‌روز شد؟ ══════════════════════════════════════════════════
    /// برنامه هنگامِ باز شدن این را می‌پرسد.
    ///
    /// ⚠️ سنجش با **نسخه** است، نه با کدِ خروجیِ جای‌گزینی. دلیلش یک سوراخِ
    /// واقعی بود: کدِ خروجی را فقط مسیرِ زیپ می‌نوشت. اگر به‌روزرسانی از راهِ
    /// نصاب (‎.exe‎) می‌رفت و شکست می‌خورد — کاربر پنجرهٔ اجازهٔ مدیر را رد
    /// می‌کرد، یا نصابِ بی‌صدا نمی‌توانست در ‎Program Files‎ بنویسد — هیچ
    /// فایلی نوشته نمی‌شد، برنامه با نسخهٔ کهنه باز می‌شد و **هیچ نمی‌گفت**.
    /// یعنی دقیقاً همان چیزی که قرار بود جلویش گرفته شود: کاربر خیال می‌کرد
    /// به‌روز شده است.
    ///
    /// مقایسهٔ نسخه هر دو مسیر را با هم می‌پوشاند و به هیچ جزئیاتِ درونیِ
    /// نصاب یا اسکریپت بند نیست.
    /// </summary>
    public static Outcome? ConsumeLastResult()
    {
        try
        {
            var pendingPath = PendingFile;
            if (!File.Exists(pendingPath)) return null;

            var target = File.ReadAllText(pendingPath).Trim();
            File.Delete(pendingPath);

            // کدِ خروجیِ اسکریپتِ زیپ، اگر بود — فقط برای پیامِ دقیق‌تر
            var code = "";
            try
            {
                if (File.Exists(ResultFile)) { code = File.ReadAllText(ResultFile).Trim(); File.Delete(ResultFile); }
            }
            catch { }

            if (target.Length == 0) return null;

            // گرفت؟ نسخهٔ در حالِ اجرا باید به هدف رسیده باشد (یا از آن جلوتر)
            if (Compare(AppVersion.Current, target) >= 0)
                return new Outcome(true, "✅ برنامه به نسخهٔ " + AppVersion.Current + " به‌روز شد");

            var why = code.Length > 0 && code != "0"
                ? " (فایل‌ها جای‌گزین نشدند)"
                : "";

            return new Outcome(false,
                "به‌روزرسانی کامل نشد" + why + " — برنامه هنوز روی نسخهٔ "
                + AppVersion.Current + " است، نه " + target + ". "
                + "اگر برنامه در پوشه‌ای نصب است که اجازهٔ مدیر می‌خواهد، "
                + "یک‌بار برنامه را «به‌عنوان مدیر» باز کنید و دوباره بزنید.");
        }
        catch { return null; }
    }

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
    ///   • ‎.exe‎ — نصاب است، بی‌صدا اجرا می‌شود (خودش برنامه را می‌بندد،
    ///     جایگزین می‌کند و باز می‌کند).
    ///   • ‎.zip‎ — بستهٔ بی‌نصب. کنارِ برنامه باز می‌شود و یک دستورِ کوچک
    ///     می‌نویسیم که صبر کند تا برنامه بسته شود، فایل‌های تازه را جای
    ///     فایل‌های کهنه بگذارد و دوباره برنامه را باز کند.
    ///
    /// ⚠️ خودِ برنامه نمی‌تواند فایل‌های در حالِ اجرای خودش را جابه‌جا کند —
    /// برای همین کار به آن دستورِ بیرونی سپرده می‌شود و برنامه بلافاصله بسته
    /// می‌شود. اگر بستن را فراموش کنید، جابه‌جایی شکست می‌خورد.
    /// </summary>
    public static bool Launch(string packagePath, string? targetVersion = null)
    {
        try
        {
            // ⚠️ پیش از هر کاری: «قرار است به این نسخه برویم». اگر این تلاش
            // بگیرد، دفعهٔ بعد که برنامه باز شود خودش می‌فهمد؛ و اگر نگیرد،
            // به کاربر گفته می‌شود به‌جای آنکه بی‌صدا روی نسخهٔ کهنه بماند.
            if (!string.IsNullOrWhiteSpace(targetVersion)) MarkPending(targetVersion!);

            if (packagePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                return LaunchZip(packagePath);

            // ── نصاب ──
            // ‎/SILENT‎ تا کاربر وسطِ به‌روزرسانی با پنجرهٔ ویزارد روبه‌رو نشود؛
            // نصابِ ما ‎CloseApplications=yes‎ دارد، پس خودش برنامهٔ باز را
            // می‌بندد، فایل‌ها را عوض می‌کند و دوباره بازش می‌کند.
            // (بارِ اول که کاربر خودش ‎setup.exe‎ را می‌زند، بی‌آرگومان اجرا
            //  می‌شود و ویزارد کامل را می‌بیند — این مسیر فقط به‌روزرسانی است.)
            var psi = new System.Diagnostics.ProcessStartInfo(packagePath)
            {
                Arguments = "/SILENT /NORESTART /RESTARTAPPLICATIONS",
                UseShellExecute = true,
            };

            // ⚠️ اگر پوشهٔ نصب اجازهٔ مدیر بخواهد (‎Program Files‎)، نصابِ
            // بی‌صدا **نمی‌تواند** خودش پنجرهٔ اجازه را بالا بیاورد و کارش
            // نیمه‌کاره می‌ماند. مسیرِ زیپ این را از اول رعایت می‌کرد و این
            // مسیر نه — پس به‌روزرسانیِ نصابی در چنان پوشه‌ای همیشه شکست
            // می‌خورد، بی آنکه کسی بفهمد.
            if (!InstallDirWritable)
            {
                psi.Verb = "runas";
                psi.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
            }

            System.Diagnostics.Process.Start(psi);
            return true;
        }
        catch
        {
            // کاربر پنجرهٔ اجازهٔ مدیر را رد کرد، یا نصاب اصلاً بالا نیامد
            return false;
        }
    }

    /// <summary>
    /// جای‌گزینیِ فایل‌ها با بستهٔ زیپ.
    ///
    /// اگر پوشهٔ نصب اجازهٔ نوشتن ندهد (کاربر برنامه را در ‎Program Files‎
    /// گذاشته)، همان دستور با اجازهٔ مدیر اجرا می‌شود. و نتیجه‌اش — چه موفق
    /// چه نه — در فایلی نوشته می‌شود که برنامه هنگامِ باز شدنِ بعدی می‌خواند.
    /// </summary>
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
        var result = ResultFile;
        Directory.CreateDirectory(Path.GetDirectoryName(result)!);
        // نشانهٔ کهنه پاک شود تا نتیجهٔ همین بار خوانده شود، نه نتیجهٔ دفعهٔ پیش
        try { if (File.Exists(result)) File.Delete(result); } catch { }

        // ‎robocopy‎ کدِ خروجیِ ۰ تا ۷ را «موفق» می‌شمارد و ۸ به بالا یعنی
        // شکست؛ پس همان عدد نوشته می‌شود و اسکریپت با ‎exit /b 0‎ بسته می‌شود.
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
            set RC=%ERRORLEVEL%
            if %RC% GEQ 8 (>"{result}" echo %RC%) else (del "{result}" >nul 2>&1)
            start "" "{exe}"
            rmdir /s /q "{staging}" >nul 2>&1
            exit /b 0
            """, System.Text.Encoding.UTF8);

        var writable = IsWritable(appDir);
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = "/c \"" + script + "\"",
            WorkingDirectory = appDir,
        };

        if (writable)
        {
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
        }
        else
        {
            // پوشهٔ نصب اجازهٔ مدیر می‌خواهد — بی این، ‎robocopy‎ بی‌صدا
            // شکست می‌خورد و برنامه با نسخهٔ کهنه باز می‌شود.
            psi.UseShellExecute = true;
            psi.Verb = "runas";
            psi.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
        }

        try { System.Diagnostics.Process.Start(psi); }
        catch
        {
            // کاربر پنجرهٔ اجازهٔ مدیر را رد کرد
            try { File.WriteAllText(result, "8"); } catch { }
            return false;
        }
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
