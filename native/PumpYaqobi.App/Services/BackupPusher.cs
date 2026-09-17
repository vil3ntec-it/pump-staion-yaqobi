using System.Net.Http;
using System.Net.Http.Headers;
using PumpYaqobi.Application.Localization;
// ‎Shamsi‎ برای نامِ روزِ فایل

namespace PumpYaqobi.App.Services;

/// <summary>
/// ══ پشتیبانِ هر شش ساعت — سرورِ خانگی **و** ابر ═══════════════════════════
///
/// خواستهٔ صریحِ صاحب ریپو (۱۴۰۵/۰۶/۲۶): «هر روز بک‌آپ بگیرد و بفرستد به سرورِ
/// برنامه هر ۶ ساعت، و تا سه روز در سرور بماند — یعنی روزِ چهارم که آمد، آن
/// آخرین نسخهٔ بک‌آپ حذف و جدید جایگزین شود. و اگر بک‌آپ گرفته نشده یک پیام به
/// مدیر بدهد که بک‌آپ بگیرد یا نتش را وصل کند تا اتومات برود.»
///
///     هر ۶ ساعت ──▶ VACUUM INTO ──┬─▶ POST /api/stations/&lt;کد&gt;/backup   (خانگی)
///                                  └─▶ POST /api/pump/device/backups   (ابر)
///
/// ⛔ <b>دو مقصد، نه یکی — و این از ۱۴۰۵/۰۶/۳۱ است.</b> تا آن روز پشتیبان فقط
///    روی سرورِ خانگی می‌نشست: مودم که بسوزد یا کامپیوترِ سرور که خراب شود،
///    هر دو با هم می‌رفتند. و نامِ قابلیت هم <c>cloudbackup</c> بود، که
///    گمراه‌کننده بود — هیچ ابری در کار نبود.
///
/// ⚠️ «رفت» یعنی <b>دستِ‌کم یکی</b> رفت. پمپی که هنوز سرورِ خانگی‌اش را راه
///    نینداخته نباید هر شش ساعت هشدارِ «پشتیبان نرفته» ببیند در حالی که
///    نسخه‌اش روی ابر سالم نشسته.
///
/// سه قاعده که باید بمانند:
///
/// ⚠️ <b>هرگز استثنا بیرون نمی‌دهد.</b> پشتیبان کارِ پس‌زمینه است؛ نه باید
///    برنامه را بشکند نه کاربر را معطل کند. هر خطا فقط «نرفت» است.
///
/// ⚠️ <b>سه روز را سرور می‌برد، نه برنامه.</b> برنامه فقط می‌فرستد؛ نگه‌داشتنِ
///    سه روزِ تقویمی قاعدهٔ ‎stations/backups.js‎ی سرور است. اگر این‌جا هم
///    حساب می‌شد، دو جا باید هم‌گام می‌ماند.
///
/// ⚠️ <b>هشدار از روی زمانِ آخرین موفقیت است، نه از روی خطا.</b> یک بار نرفتن
///    (مودم خاموش) هشدار نیست؛ ‎WarnAfter‎ ساعت نرفتن هشدار است.
/// </summary>
public sealed class BackupPusher : IAsyncDisposable
{
    /// <summary>هر شش ساعت یک بار — خواستهٔ صریحِ صاحب ریپو.</summary>
    public static readonly TimeSpan Every = TimeSpan.FromHours(6);

    /// <summary>
    /// اولین دور، کمی بعد از ورود. عکسِ ایستگاه ۱۲ ثانیه صبر می‌کند
    /// (<see cref="StationPublisher.FirstDelay"/>)؛ این یکی بعد از آن، تا
    /// صفحهٔ اولِ کاربر با هیچ‌کدام رقابت نکند.
    /// </summary>
    public static readonly TimeSpan FirstDelay = TimeSpan.FromSeconds(45);

    /// <summary>از این دیرتر که شد، به مدیر می‌گوییم.</summary>
    public static readonly TimeSpan WarnAfter = TimeSpan.FromHours(26);

    private readonly AppHost _host;
    private readonly CancellationTokenSource _life = new();
    private Task? _loop;

    public BackupPusher(AppHost host) => _host = host;

    /// <summary>آخرین باری که پشتیبان واقعاً روی سرور نشست (محلی).</summary>
    public DateTime? LastSentAt { get; private set; }

    /// <summary>چرا آخرین دور نرفت — خالی یعنی رفت.</summary>
    public string LastError { get; private set; } = "";

    /// <summary>پیامی که باید به مدیر نشان داده شود، یا خالی.</summary>
    public string WarningText { get; private set; } = "";

    public void Start()
    {
        if (_loop is not null) return;
        _loop = Task.Run(() => LoopAsync(_life.Token));
    }

    private async Task LoopAsync(CancellationToken ct)
    {
        try { await Task.Delay(FirstDelay, ct); } catch { return; }
        while (!ct.IsCancellationRequested)
        {
            try { await RunOnceAsync(ct); } catch { /* هیچ خطایی بیرون نمی‌رود */ }
            try { await Task.Delay(Every, ct); } catch { return; }
        }
    }

    /// <summary>
    /// یک دور: عکسِ تازه بگیر، بفرست، و اگر مدت‌هاست نرفته به مدیر بگو.
    /// خروجی: رفت یا نه.
    /// </summary>
    public async Task<bool> RunOnceAsync(CancellationToken ct = default)
    {
        // ══ قفلِ اشتراک ═══════════════════════════════════════════════════
        //  بک‌اپِ خودکارِ روی سرور با اشتراک است. ⚠️ بک‌اپِ **محلی** هرگز قفل
        //  نمی‌شود — نه این‌جا نه جای دیگر: دادهٔ کاربر مالِ خودش است و
        //  «سطل زباله و بک‌اپِ محلی همیشه باز» قاعدهٔ ثابتِ این برنامه است.
        if (!Entitlements.Allows(Entitlements.CloudBackup))
        {
            LastError = Entitlements.Why(Entitlements.CloudBackup);
            //  و هشدارِ «۲۶ ساعت است نرفته» را هم نمی‌دهیم: نرفتنش تصمیمِ
            //  خودِ پلن است، نه خرابی.
            WarningText = "";
            return false;
        }

        var file = _host.Backup.SnapshotToday();
        if (file is null || !File.Exists(file))
        {
            LastError = "عکسِ پشتیبان گرفته نشد";
            Warn();
            return false;
        }

        var sent = await SendAsync(file, ct);

        /*
         *  ══ و همان فایل، روی ابر ═════════════════════════════════════
         *
         *  ⛔ تا امروز پشتیبان **فقط** روی سرورِ خانگی می‌نشست. مودم که
         *  بسوزد یا کامپیوترِ سرور که خراب شود، هر دو با هم می‌روند —
         *  و `data/stations/<کد>/backups/` روی همان یک دیسک است. اسمِ
         *  قابلیت هم `cloudbackup` بود، که گمراه‌کننده بود: هیچ ابری در
         *  کار نبود.
         *
         *  ⚠️ **مقصدِ دوم، نه جانشین.** سرورِ خانگی سریع است و در همان
         *  شبکه؛ ابر جایی است که آتش‌سوزیِ پمپ به آن نمی‌رسد. هر کدام
         *  جدا سنجیده می‌شود و نرفتنِ یکی، دیگری را نمی‌شکند.
         *
         *  ⚠️ و «رفت» یعنی **دستِ‌کم یکی** رفت: پمپی که هنوز سرورِ
         *  خانگی‌اش را راه نینداخته، نباید هر شش ساعت هشدارِ «پشتیبان
         *  نرفته» ببیند در حالی که نسخه‌اش روی ابر سالم نشسته.
         */
        var toCloud = await SendToCloudAsync(file, ct);

        if (sent || toCloud)
        {
            LastSentAt = DateTime.Now;
            LastError = sent && toCloud ? ""
                : sent ? "روی ابر ننشست — فقط سرورِ خانگی"
                : "روی سرورِ خانگی ننشست — فقط ابر";
            var s = AppSettings.Load();
            s.LastBackupSentAt = LastSentAt.Value.ToString("O");
            s.Save();
            WarningText = "";
            return true;
        }

        Warn();
        return false;
    }

    /// <summary>
    /// همان فایل، روی پوشهٔ ابریِ همین پمپ.
    /// </summary>
    /// <remarks>
    /// ⛔ <b>هیچ‌وقت استثنا بیرون نمی‌دهد.</b> پشتیبان کارِ پس‌زمینه است و
    /// نباید نه برنامه را بشکند نه جلوی مقصدِ دیگر را بگیرد.
    ///
    /// ⚠️ برخلافِ سرورِ خانگی، این‌جا فایل یک‌جا خوانده می‌شود چون
    /// <c>CloudLink</c> بایت می‌گیرد. پس سقفِ اندازه سنجیده می‌شود: فایلی
    /// که از سهمِ سرور بزرگ‌تر است، خواندنش در حافظه فقط خرج است و
    /// سرور هم ردش می‌کند.
    /// </remarks>
    private async Task<bool> SendToCloudAsync(string file, CancellationToken ct)
    {
        try
        {
            /*
             *  ⚠️ `CloudLink` همان‌جا ساخته می‌شود، از تنظیماتِ **تازه
             *  خوانده‌شده** — همان الگویی که `StationPublisher` دارد.
             *  نگه داشتنِ یک نمونه در `AppHost` یعنی توکنی که در نشستِ
             *  دیگری عوض شده، این‌جا کهنه می‌ماند.
             */
            var file2 = AppSettings.Load();
            var cloud = new CloudLink(file2, () => { file2.Save(); return Task.CompletedTask; });
            if (!cloud.Activated) return false;

            var info = new FileInfo(file);
            if (!info.Exists || info.Length == 0) return false;
            if (info.Length > CloudMaxBytes) return false;

            var bytes = await File.ReadAllBytesAsync(file, ct);
            var res = await cloud.BackupUploadAsync(
                bytes,
                label: Shamsi.Today(),
                manual: false,
                ext: "db",
                ct: ct);
            return res.Ok;
        }
        catch { return false; }
    }

    /// <summary>
    /// بزرگ‌ترین فایلی که ارزشِ خواندن در حافظه را دارد.
    /// همان سقفی که سرور دارد (<c>BACKUP_ACCOUNT_MAX_MB</c>ِ پیش‌فرض).
    /// </summary>
    private const long CloudMaxBytes = 64L * 1024 * 1024;

    /// <summary>
    /// همان فایل، با رمزِ برنامه، روی سرورِ خانگی.
    /// ⚠️ ‎POST‎ی ساده و بدنهٔ خام: فایلِ SQLite چند مگابایت است و داخلِ JSON
    /// نمی‌گنجد. وب‌سوکتِ دفتر هم برای فایل ساخته نشده.
    /// </summary>
    private async Task<bool> SendAsync(string file, CancellationToken ct)
    {
        var url = HomeLink.Url(_host).Trim();
        var token = HomeLink.Token(_host).Trim();
        var code = HomeLink.StationCode(_host);
        if (url.Length == 0 || token.Length == 0)
        {
            LastError = "سرورِ خانگی تنظیم نشده";
            return false;
        }

        try
        {
            var target = url.TrimEnd('/') + "/api/stations/" + Uri.EscapeDataString(code) + "/backup";
            using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
            //  ⚠️ **فایل جریانی می‌رود، نه یک‌جا در حافظه.** پیش از این
            //  ‎File.ReadAllBytesAsync‎ بود: دفترِ چندصد مگابایتیِ یک پمپِ
            //  چندساله همان‌قدر رم می‌خواست، یک‌جا، هر شش ساعت. حالا هرچه
            //  دفتر بزرگ‌تر شود، حافظهٔ این کار همان اندازه می‌ماند.
            await using var stream = new FileStream(
                file, FileMode.Open, FileAccess.Read, FileShare.Read,
                bufferSize: 64 * 1024, useAsync: true);
            using var body = new StreamContent(stream, 64 * 1024);
            body.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            body.Headers.ContentLength = stream.Length;

            using var req = new HttpRequestMessage(HttpMethod.Post, target) { Content = body };
            CloudLink.Stamp(req);
            req.Headers.TryAddWithoutValidation("x-station-token", token);
            // نامِ فایل همان نامِ روزِ عکس + ساعت، تا چهار پشتیبانِ یک روز روی هم نیفتند
            req.Headers.TryAddWithoutValidation(
                "x-backup-name",
                "pump-" + Shamsi.Today().Replace('/', '-') + "-" + DateTime.Now.ToString("HHmm") + ".db");

            using var res = await http.SendAsync(req, ct);
            if (res.IsSuccessStatusCode) return true;
            LastError = "سرور نپذیرفت (" + (int)res.StatusCode + ")";
            return false;
        }
        catch (Exception e)
        {
            LastError = "به سرور نرسید: " + e.Message;
            return false;
        }
    }

    /// <summary>
    /// «بک‌آپ نرفته» — فقط وقتی واقعاً مدتی گذشته باشد، نه با یک بار نرفتن.
    /// </summary>
    private void Warn()
    {
        var last = LastSentAt ?? Parse(AppSettings.Load().LastBackupSentAt);
        if (last is { } when && DateTime.Now - when < WarnAfter) { WarningText = ""; return; }

        WarningText = last is null
            ? "پشتیبان هنوز روی سرور نرفته — اینترنت/شبکهٔ پمپ را وصل کنید یا از تنظیمات دستی بکاپ بگیرید."
            : "پشتیبان از " + last.Value.ToString("yyyy/MM/dd HH:mm") + " به بعد روی سرور نرفته — شبکه را وصل کنید یا دستی بکاپ بگیرید.";
        try { _host.Toast(WarningText, ToastKind.Warn); } catch { }
    }

    private static DateTime? Parse(string raw) =>
        DateTime.TryParse(raw, out var v) ? v : null;

    public async ValueTask DisposeAsync()
    {
        _life.Cancel();
        if (_loop is not null) { try { await _loop; } catch { } }
        _life.Dispose();
    }
}
