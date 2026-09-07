using System.Text.RegularExpressions;

namespace PumpYaqobi.Application.Services;

/// <summary>
/// نوعِ لینکِ یک دوربین — همان تصمیمی که ‎_camMount‎ می‌گیرد.
/// </summary>
public enum CameraKind
{
    /// <summary>لینکی ثبت نشده.</summary>
    None = 0,
    /// <summary>‎rtsp://…‎ — پخشِ مستقیمش رمزگشای ویدیو می‌خواهد.</summary>
    Rtsp = 1,
    /// <summary>‎…​.m3u8‎ — پخشِ زندهٔ HLS.</summary>
    Hls = 2,
    /// <summary>فایلِ ویدیوی معمولی (mp4/webm/ogv/ogg).</summary>
    Video = 3,
    /// <summary>عکسِ لحظه‌ای یا MJPEG — همانی که برنامه خودش نشان می‌دهد.</summary>
    Image = 4,
    /// <summary>صفحهٔ وبِ خودِ دوربین یا NVR.</summary>
    WebPage = 5,
    /// <summary>‎usb:&lt;شماره&gt;‎ — وب‌کمِ خودِ کامپیوتر.</summary>
    Usb = 6,
}

/// <summary>
/// ══ دوربین‌های مداربسته ═════════════════════════════════════════════════════
/// رونوشتِ تصمیم‌های ‎_camMount‎ · ‎camConfirmAdd‎ · ‎camReload‎.
///
/// همهٔ کارِ این کلاس یک تصمیم است: «این لینک چه‌جور چیزی است؟» — و همان یک
/// تصمیم است که تعیین می‌کند کاربر تصویر می‌بیند یا پیامِ خطا. پس مو‌به‌مو از
/// نسخهٔ وب برداشته شده، با همان ترتیب.
///
/// ⚠️ ترتیبِ شرط‌ها به‌اندازهٔ خودِ الگوها مهم است: لینکی مثل
/// ‎…/cgi-bin/hls/index.m3u8‎ هم به HLS می‌خورد هم به «عکس»، و چون HLS جلوتر
/// است HLS برنده می‌شود. با جابه‌جا کردنِ دو شرط، دوربینِ کاربر خاموش می‌شود.
/// </summary>
public sealed class CameraService
{
    private const RegexOptions Rx = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

    // ⚠️ ‎\z‎ نه ‎$‎ : در دات‌نت ‎$‎ پیش از یک ‎\n‎ِ پایانی هم می‌گیرد، ولی
    // ‎$‎ِ جاوااسکریپت (بی‌پرچمِ m) فقط تهِ رشته است.
    private static readonly Regex UsbRx = new(@"^usb:\d+\z", Rx);
    private static readonly Regex RtspRx = new(@"^rtsp:", Rx);
    private static readonly Regex HlsRx = new(@"\.m3u8(\?|#|\z)", Rx);
    private static readonly Regex VideoRx = new(@"\.(mp4|webm|ogv|ogg)(\?|#|\z)", Rx);
    private static readonly Regex ImageExtRx = new(@"\.(jpe?g|png|gif|webp)(\?|#|\z)", Rx);
    private static readonly Regex ImageHintRx =
        new(@"mjpg|mjpeg|snapshot|faststream|videostream|/cgi-bin/", Rx);

    /// <summary>‎_camMount(c)‎ — این لینک چه‌جور تصویری است؟</summary>
    public static CameraKind KindOf(string? url)
    {
        var u = (url ?? "").Trim();
        if (u.Length == 0) return CameraKind.None;
        // ⚠️ اولِ همه: ‎usb:‎ لینکِ شبکه‌ای نیست و هیچ‌کدام از الگوهای زیر
        // نباید رویش بیفتد.
        if (UsbRx.IsMatch(u)) return CameraKind.Usb;
        if (RtspRx.IsMatch(u)) return CameraKind.Rtsp;
        if (HlsRx.IsMatch(u)) return CameraKind.Hls;
        if (VideoRx.IsMatch(u)) return CameraKind.Video;
        if (ImageExtRx.IsMatch(u) || ImageHintRx.IsMatch(u)) return CameraKind.Image;
        return CameraKind.WebPage;
    }

    /// <summary>
    /// این نوع را برنامه **بی هیچ پخش‌کننده‌ای** نشان می‌دهد: عکسِ لحظه‌ای و
    /// MJPEG فقط یک زنجیرهٔ JPEG هستند و رمزگشایشان در خودِ برنامه هست.
    /// </summary>
    public static bool ShowsInApp(CameraKind kind) =>
        kind is CameraKind.Image or CameraKind.Usb;

    /// <summary>
    /// این نوع رمزگشای ویدیو می‌خواهد — RTSP، HLS و فایلِ ویدیو.
    ///
    /// ⚠️ صفحهٔ وب این‌جا نیست: آن اصلاً تصویر نیست، یک صفحهٔ HTML است و
    /// جایش مرورگر است نه پخش‌کننده.
    /// </summary>
    public static bool NeedsPlayer(CameraKind kind) =>
        kind is CameraKind.Rtsp or CameraKind.Hls or CameraKind.Video;

    /// <summary>
    /// ‎_t=…‎ — بی این، «تازه کردن» عکسِ کشِ قبلی را دوباره نشان می‌داد.
    /// </summary>
    public static string CacheBusted(string url, long stamp) =>
        url + (url.Contains('?') ? "&" : "?") + "_t=" + stamp;

    /// <summary>
    /// نامِ پیش‌فرضِ دوربینِ تازه — «دوربین 3».
    ///
    /// ⚠️ عدد با فرهنگِ ثابت نوشته می‌شود: برنامه با فرهنگِ فارسی بالا می‌آید
    /// (‎InvariantGlobalization=false‎) و بی این، رقم‌ها فارسی می‌شدند —
    /// برخلافِ قاعدهٔ پروژه که هیچ رقمِ فارسی نمایش داده نمی‌شود.
    /// </summary>
    public static string DefaultName(int existingCount) =>
        "دوربین " + (existingCount + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>پیامی که به‌جای تصویر نشان داده می‌شود.</summary>
    public static string NoteFor(CameraKind kind) => kind switch
    {
        CameraKind.None => "لینکی ثبت نشده",
        // این سه پیام فقط وقتی دیده می‌شوند که پخش‌کنندهٔ ویدیو بالا نیامده
        // باشد؛ وگرنه خودِ تصویر جایشان می‌نشیند.
        CameraKind.Rtsp => "پخش‌کنندهٔ ویدیو بالا نیامد — لینکِ RTSP را با دکمهٔ زیر "
                         + "در پخش‌کنندهٔ ویندوز باز کنید، یا از دستگاهِ ضبط (NVR/DVR) "
                         + "لینکِ عکسِ لحظه‌ای بگیرید",
        CameraKind.Hls => "پخش‌کنندهٔ ویدیو بالا نیامد — با دکمهٔ زیر در پخش‌کنندهٔ ویندوز باز می‌شود",
        CameraKind.Video => "فایلِ ویدیو — با دکمهٔ زیر در پخش‌کنندهٔ ویندوز باز می‌شود",
        CameraKind.WebPage => "صفحهٔ وبِ خودِ دوربین — با دکمهٔ زیر در مرورگر باز می‌شود",
        CameraKind.Usb => "دوربینِ USB باز نشد — کابلش را بررسی کنید و در تنظیماتِ "
                        + "«حریم خصوصی › دوربین»ِ ویندوز اجازه بدهید",
        _ => "",
    };
}
