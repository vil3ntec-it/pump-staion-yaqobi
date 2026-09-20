using System.Runtime.InteropServices;

namespace PumpYaqobi.App.Services;

/// <summary>
/// ══ اعلانِ خودِ ویندوز ═══════════════════════════════════════════════════
///
/// بندِ ۶ی پرامپتِ ۲۲: «بنرِ داخلِ برنامه + اعلانِ سیستم‌عامل از WebSocket؛
/// بدونِ سرویسِ پولی.»
///
/// ── چرا P/Invoke و نه یک بسته ─────────────────────────────────────────
/// همان دلیلی که <c>WaveRecorder</c> با <c>winmm</c> نوشته شد: این پروژه
/// عمداً وابستگیِ اضافه ندارد و اعلانِ ویندوز یک فراخوانِ ساده است
/// (<c>Shell_NotifyIcon</c>). آوردنِ یک بستهٔ UWP برای دو خط، حجمِ نصاب را
/// بالا می‌برد و نصبِ بی‌اینترنت را شکننده می‌کند.
///
/// ⚠️ <b>فقط ویندوز.</b> روی لینوکس (که CI آن‌جا می‌دود) و هر جای دیگر،
/// <see cref="Show"/> بی‌صدا هیچ کاری نمی‌کند و <b>بنرِ داخلِ برنامه</b>
/// تنها راهِ خبر است — که خودش کافی است.
///
/// ⛔ <b>هیچ‌وقت استثنا بیرون نمی‌دهد.</b> اعلان رفاه است؛ نرفتنش نباید
/// هیچ کاری را بشکند.
///
/// ⚠️ و نشانِ سینیِ سیستم <b>ماندگار نمی‌شود</b>: فقط برای نشان دادنِ
/// همان یک پیام ساخته و بعد برداشته می‌شود. یک آیکونِ همیشگی در سینی،
/// چیزی است که کاربر نخواسته.
/// </summary>
public static class NativeNotice
{
    /// <summary>
    /// یک اعلانِ سیستمی نشان می‌دهد.
    /// </summary>
    /// <param name="title">سرخط — کوتاه.</param>
    /// <param name="body">متن.</param>
    /// <param name="owner">دستگیرهٔ پنجرهٔ برنامه؛ صفر یعنی «نمی‌شود».</param>
    /// <returns>رفت یا نه — هیچ‌جا اجباری نیست.</returns>
    public static bool Show(string title, string body, IntPtr owner)
    {
        if (!OperatingSystem.IsWindows() || owner == IntPtr.Zero) return false;

        try
        {
            var data = new NotifyIconData
            {
                cbSize = Marshal.SizeOf<NotifyIconData>(),
                hWnd = owner,
                uID = IconId,
                uFlags = NIF_INFO | NIF_ICON | NIF_TIP,
                //  آیکونِ خودِ پنجره — بی آن، اعلان در ویندوز ۱۰ دیده نمی‌شود
                hIcon = LoadIcon(IntPtr.Zero, IDI_APPLICATION),
                szTip = Cut("پمپ یعقوبی", 127),
                szInfoTitle = Cut(title, 63),
                szInfo = Cut(body, 255),
                dwInfoFlags = NIIF_INFO,
            };

            //  ⚠️ افزودن، نشان دادن، برداشتن — به همین ترتیب. بی `ADD`،
            //  `MODIFY` روی نشانِ نبوده کاری نمی‌کند و پیام بی‌صدا گم می‌شود.
            var ok = Shell_NotifyIcon(NIM_ADD, ref data)
                  && Shell_NotifyIcon(NIM_MODIFY, ref data);
            Shell_NotifyIcon(NIM_DELETE, ref data);
            return ok;
        }
        catch { return false; }
    }

    private static string Cut(string? s, int max)
    {
        var t = (s ?? "").Replace('\r', ' ').Replace('\n', ' ');
        return t.Length <= max ? t : t[..max];
    }

    // ── امضاهای ویندوز ────────────────────────────────────────────────

    private const int IconId = 0x5059;      // «PY»
    private const int NIM_ADD = 0x0000;
    private const int NIM_MODIFY = 0x0001;
    private const int NIM_DELETE = 0x0002;
    private const int NIF_ICON = 0x0002;
    private const int NIF_TIP = 0x0004;
    private const int NIF_INFO = 0x0010;
    private const int NIIF_INFO = 0x0001;
    private static readonly IntPtr IDI_APPLICATION = new(32512);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NotifyIconData
    {
        public int cbSize;
        public IntPtr hWnd;
        public int uID;
        public int uFlags;
        public int uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string szTip;
        public int dwState;
        public int dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string szInfo;
        public int uTimeoutOrVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string szInfoTitle;
        public int dwInfoFlags;
        public Guid guidItem;
        public IntPtr hBalloonIcon;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool Shell_NotifyIcon(int message, ref NotifyIconData data);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr LoadIcon(IntPtr hInstance, IntPtr iconName);
}
