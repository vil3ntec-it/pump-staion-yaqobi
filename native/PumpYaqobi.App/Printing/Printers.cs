using System.Runtime.InteropServices;
using System.Text;
using SkiaSharp;

namespace PumpYaqobi.App.Printing;

/// <summary>یک چاپگرِ نصب‌شده روی همین کامپیوتر — همان ردیفِ فهرستِ چاپِ اکسل.</summary>
public sealed record PrinterItem(string Name, bool IsDefault, string StatusText, bool Ready)
{
    /// <summary>نوشتهٔ زیرِ نام: «آماده · پیش‌فرض» یا «آفلاین».</summary>
    public string Line => IsDefault ? StatusText + " · پیش‌فرضِ ویندوز" : StatusText;
}

/// <summary>
/// ══ چاپگرهای واقعیِ ویندوز — «مثلِ اکسل» (۱۴۰۵/۰۷/۱۴) ═════════════════════
///
/// گزارشِ صاحب ریپو با عکس: «پرینتر سیم‌اش وصل بود، اکسل همان را انتخاب و
/// پیشنهاد کرده بود، ولی برنامهٔ من اصلاً نمی‌آید که بگوید این را پرینت کن…
/// باید ساعت‌ها دنبالِ پرینتر بگردم در حالی که وصل است.»
///
/// ⛔ <b>دو ریشه، هر دو بسته شد:</b>
///  ۱) برنامه هیچ چاپگری فهرست نمی‌کرد — فقط یک کادرِ ثابتِ «چاپگرِ پیش‌فرضِ
///     ویندوز» بود. حالا <see cref="List"/> همان فهرستِ خودِ ویندوز را می‌دهد
///     (‎EnumPrinters‎)، با حال (آماده/آفلاین/کاغذ تمام) و پیش‌فرض.
///  ۲) چاپ یعنی «PDF را با فعلِ ‎print‎ به ویندوز بده». آن فعل را فقط برنامهٔ
///     PDFخوانی ثبت می‌کند که «چاپ» بلد باشد (آکروبات)؛ روی کامپیوتری که PDF
///     را با اج باز می‌کند <b>هیچ اتفاقی نمی‌افتاد</b> — نه خطا، نه چاپ.
///     حالا <see cref="Print"/> ورق‌ها را مستقیم با GDIِ خودِ ویندوز به همان
///     چاپگر می‌فرستد و به هیچ برنامهٔ دیگری بند نیست.
///
/// ⚠️ بی هیچ بستهٔ تازه‌ای: ‎winspool.drv‎ و ‎gdi32‎ خودِ ویندوزند (همان راهِ
/// ‎winmm‎ِ ‎WaveRecorder‎)، و ‎SkiaSharp‎ از قبل زیرِ آوالونیاست.
/// </summary>
public static class Printers
{
    /// <summary>⚠️ فقط برای آزمون — جای فهرستِ ویندوز.</summary>
    public static Func<IReadOnlyList<PrinterItem>>? ListOverride { get; set; }

    /// <summary>
    /// چاپگرهای نصب‌شده، پیش‌فرض اول. روی غیرِ ویندوز یا هر خطایی ⇒ فهرستِ
    /// خالی (پنجرهٔ چاپ آن‌وقت همان راهِ قدیم را می‌رود). هیچ‌وقت استثنا نمی‌دهد.
    /// </summary>
    public static IReadOnlyList<PrinterItem> List()
    {
        if (ListOverride is { } hook) return hook();
        if (!OperatingSystem.IsWindows()) return Array.Empty<PrinterItem>();
        try
        {
            var def = DefaultName();
            var list = new List<PrinterItem>();
            const uint flags = PRINTER_ENUM_LOCAL | PRINTER_ENUM_CONNECTIONS;
            EnumPrinters(flags, null, 2, IntPtr.Zero, 0, out var needed, out _);
            if (needed == 0) return list;
            var buf = Marshal.AllocHGlobal((int)needed);
            try
            {
                if (!EnumPrinters(flags, null, 2, buf, needed, out _, out var count)) return list;
                var size = Marshal.SizeOf<PRINTER_INFO_2>();
                for (var i = 0; i < count; i++)
                {
                    var info = Marshal.PtrToStructure<PRINTER_INFO_2>(buf + i * size);
                    var name = info.pPrinterName ?? "";
                    if (name.Length == 0) continue;
                    var (text, ready) = StatusOf(info.Status, info.Attributes);
                    list.Add(new PrinterItem(name,
                        string.Equals(name, def, StringComparison.OrdinalIgnoreCase), text, ready));
                }
            }
            finally { Marshal.FreeHGlobal(buf); }
            return Order(list);
        }
        catch { return Array.Empty<PrinterItem>(); }
    }

    /// <summary>پیش‌فرض اول، بعد آماده‌ها، بعد به ترتیبِ نام — خالص و آزمون‌پذیر.</summary>
    public static IReadOnlyList<PrinterItem> Order(IEnumerable<PrinterItem> items) =>
        items.OrderByDescending(p => p.IsDefault)
             .ThenByDescending(p => p.Ready)
             .ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
             .ToList();

    /// <summary>
    /// کدام را از اول انتخاب کنیم: همانی که کاربر بارِ پیش زد (اگر هنوز هست)،
    /// وگرنه پیش‌فرضِ ویندوز، وگرنه اولی — همان کارِ اکسل.
    /// </summary>
    public static PrinterItem? Pick(IReadOnlyList<PrinterItem> items, string? remembered)
    {
        if (items.Count == 0) return null;
        if (!string.IsNullOrWhiteSpace(remembered))
        {
            var r = items.FirstOrDefault(p => string.Equals(p.Name, remembered, StringComparison.OrdinalIgnoreCase));
            if (r is not null) return r;
        }
        return items.FirstOrDefault(p => p.IsDefault) ?? items[0];
    }

    /// <summary>حالِ چاپگر به زبانِ آدم — از همان بیت‌های خودِ ویندوز.</summary>
    public static (string Text, bool Ready) StatusOf(uint status, uint attributes)
    {
        if ((attributes & PRINTER_ATTRIBUTE_WORK_OFFLINE) != 0 || (status & PRINTER_STATUS_OFFLINE) != 0)
            return ("آفلاین — روشن و وصل است؟", false);
        if ((status & PRINTER_STATUS_PAPER_OUT) != 0) return ("کاغذ تمام شده", false);
        if ((status & PRINTER_STATUS_PAPER_JAM) != 0) return ("کاغذ گیر کرده", false);
        if ((status & PRINTER_STATUS_PAUSED) != 0) return ("متوقف شده", false);
        if ((status & PRINTER_STATUS_ERROR) != 0) return ("خطا دارد", false);
        if ((status & PRINTER_STATUS_NOT_AVAILABLE) != 0) return ("در دسترس نیست", false);
        if ((status & PRINTER_STATUS_DOOR_OPEN) != 0) return ("درِ چاپگر باز است", false);
        if ((status & PRINTER_STATUS_TONER_LOW) != 0) return ("آماده · جوهر کم است", true);
        if ((status & PRINTER_STATUS_PRINTING) != 0) return ("در حالِ چاپ", true);
        return ("آماده", true);
    }

    private static string DefaultName()
    {
        try
        {
            var size = 0;
            GetDefaultPrinter(null, ref size);
            if (size <= 0) return "";
            var sb = new StringBuilder(size);
            return GetDefaultPrinter(sb, ref size) ? sb.ToString() : "";
        }
        catch { return ""; }
    }

    /// <summary>
    /// اندازه و جای ورق روی کاغذِ چاپگر — خالص و آزمون‌پذیر.
    ///
    /// ورق به اندازهٔ <b>واقعیِ</b> خودش چاپ می‌شود (پیکسل ÷ نقطه‌در‌اینچ)؛
    /// فقط اگر کاغذِ چاپگر کوچک‌تر بود (مثلاً Letter به‌جای A4) کوچک و وسط
    /// می‌شود — هیچ‌وقت بزرگ‌تر از اندازهٔ خودش و هیچ‌وقت بریده.
    /// مختصات نسبت به گوشهٔ جای‌چاپ‌پذیر است، پس حاشیهٔ سختِ چاپگر کم می‌شود.
    /// </summary>
    public static (int X, int Y, int W, int H) Place(int imgW, int imgH, int imgDpi,
        int devDpiX, int devDpiY, int paperW, int paperH, int offX, int offY)
    {
        double w = imgW / (double)imgDpi * devDpiX;
        double h = imgH / (double)imgDpi * devDpiY;
        var s = Math.Min(1.0, Math.Min(paperW / w, paperH / h));
        w *= s; h *= s;
        var x = (paperW - w) / 2 - offX;
        var y = (paperH - h) / 2 - offY;
        return ((int)Math.Round(x), (int)Math.Round(y), (int)Math.Round(w), (int)Math.Round(h));
    }

    /// <summary>
    /// ورق‌ها را مستقیم به همان چاپگر می‌فرستد. خالی ⇒ درست رفت؛ وگرنه
    /// جملهٔ آدمیزادِ خطا. ⚠️ روی نخِ دیگر صدا بزنید — هر ورقِ ۳۰۰ نقطه‌ای
    /// چند ده مگابایت است و روی نخِ رابط پنجره را می‌خشکاند.
    /// </summary>
    public static string Print(string printer, IEnumerable<byte[]> pages, int dpi, string title)
    {
        if (!OperatingSystem.IsWindows()) return "چاپِ مستقیم فقط روی ویندوز است";
        var hdc = CreateDC("WINSPOOL", printer, null, IntPtr.Zero);
        if (hdc == IntPtr.Zero) return "چاپگرِ «" + printer + "» باز نشد — روشن و وصل است؟";
        var started = false;
        try
        {
            var doc = new DOCINFO
            {
                cbSize = Marshal.SizeOf<DOCINFO>(),
                lpszDocName = string.IsNullOrWhiteSpace(title) ? "پمپ یعقوبی" : title,
            };
            if (StartDoc(hdc, ref doc) <= 0)
            {
                var err = Marshal.GetLastWin32Error();
                return err == ERROR_CANCELLED ? "چاپ لغو شد" : "چاپگر کار را نپذیرفت (کدِ " + err + ")";
            }
            started = true;

            int dx = GetDeviceCaps(hdc, LOGPIXELSX), dy = GetDeviceCaps(hdc, LOGPIXELSY);
            int pw = GetDeviceCaps(hdc, PHYSICALWIDTH), ph = GetDeviceCaps(hdc, PHYSICALHEIGHT);
            int ox = GetDeviceCaps(hdc, PHYSICALOFFSETX), oy = GetDeviceCaps(hdc, PHYSICALOFFSETY);
            if (pw <= 0 || ph <= 0) { pw = GetDeviceCaps(hdc, HORZRES); ph = GetDeviceCaps(hdc, VERTRES); ox = oy = 0; }
            if (dx <= 0) dx = 300;
            if (dy <= 0) dy = dx;

            var n = 0;
            foreach (var png in pages)
            {
                using var decoded = SKBitmap.Decode(png);
                if (decoded is null) continue;
                //  ⚠️ ورقِ خوابیده روی کاغذِ ایستاده می‌چرخد، وگرنه کوچک و وسطِ
                //  کاغذ می‌افتاد.
                using var bmp = (decoded.Width > decoded.Height) != (pw > ph) ? Rotate(decoded) : decoded.Copy();
                using var bgra = bmp.ColorType == SKColorType.Bgra8888 ? bmp.Copy() : bmp.Copy(SKColorType.Bgra8888);
                if (bgra is null) continue;

                var (x, y, w, h) = Place(bgra.Width, bgra.Height, dpi, dx, dy, pw, ph, ox, oy);
                if (StartPage(hdc) <= 0) return "چاپگر ورقِ تازه را نپذیرفت";
                var bi = new BITMAPINFOHEADER
                {
                    biSize = Marshal.SizeOf<BITMAPINFOHEADER>(),
                    biWidth = bgra.Width,
                    biHeight = -bgra.Height,          // از بالا به پایین
                    biPlanes = 1,
                    biBitCount = 32,
                    biCompression = 0,                // BI_RGB
                };
                SetStretchBltMode(hdc, HALFTONE);
                SetBrushOrgEx(hdc, 0, 0, IntPtr.Zero);
                var drawn = StretchDIBits(hdc, x, y, w, h, 0, 0, bgra.Width, bgra.Height,
                                          bgra.GetPixels(), ref bi, DIB_RGB_COLORS, SRCCOPY);
                EndPage(hdc);
                if (drawn == 0) return "چاپگر تصویرِ ورق را نپذیرفت";
                n++;
            }
            if (n == 0) return "هیچ ورقی برای چاپ نبود";
            EndDoc(hdc);
            started = false;
            return "";
        }
        catch (Exception ex) { return "چاپ انجام نشد — " + ex.GetType().Name; }
        finally
        {
            if (started) AbortDoc(hdc);
            DeleteDC(hdc);
        }
    }

    private static SKBitmap Rotate(SKBitmap src)
    {
        var dst = new SKBitmap(src.Height, src.Width, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var c = new SKCanvas(dst);
        c.Clear(SKColors.White);
        c.Translate(0, dst.Height);
        c.RotateDegrees(-90);
        c.DrawBitmap(src, 0, 0);
        return dst;
    }

    // ── ویندوز ────────────────────────────────────────────────────────────

    private const uint PRINTER_ENUM_LOCAL = 0x2, PRINTER_ENUM_CONNECTIONS = 0x4;
    private const uint PRINTER_ATTRIBUTE_WORK_OFFLINE = 0x400;
    private const uint PRINTER_STATUS_PAUSED = 0x1, PRINTER_STATUS_ERROR = 0x2,
        PRINTER_STATUS_PAPER_JAM = 0x8, PRINTER_STATUS_PAPER_OUT = 0x10,
        PRINTER_STATUS_OFFLINE = 0x80, PRINTER_STATUS_PRINTING = 0x400,
        PRINTER_STATUS_NOT_AVAILABLE = 0x1000, PRINTER_STATUS_TONER_LOW = 0x20000,
        PRINTER_STATUS_DOOR_OPEN = 0x400000;
    private const int LOGPIXELSX = 88, LOGPIXELSY = 90, HORZRES = 8, VERTRES = 10,
        PHYSICALWIDTH = 110, PHYSICALHEIGHT = 111, PHYSICALOFFSETX = 112, PHYSICALOFFSETY = 113;
    private const int HALFTONE = 4, DIB_RGB_COLORS = 0, ERROR_CANCELLED = 1223;
    private const uint SRCCOPY = 0x00CC0020;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct PRINTER_INFO_2
    {
        public string? pServerName, pPrinterName, pShareName, pPortName, pDriverName,
            pComment, pLocation;
        public IntPtr pDevMode;
        public string? pSepFile, pPrintProcessor, pDatatype, pParameters;
        public IntPtr pSecurityDescriptor;
        public uint Attributes, Priority, DefaultPriority, StartTime, UntilTime, Status, cJobs, AveragePPM;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DOCINFO
    {
        public int cbSize;
        public string lpszDocName;
        public string? lpszOutput;
        public string? lpszDatatype;
        public int fwType;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFOHEADER
    {
        public int biSize, biWidth, biHeight;
        public short biPlanes, biBitCount;
        public int biCompression, biSizeImage, biXPelsPerMeter, biYPelsPerMeter, biClrUsed, biClrImportant;
    }

    [DllImport("winspool.drv", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "EnumPrintersW")]
    private static extern bool EnumPrinters(uint flags, string? name, uint level, IntPtr buf, uint cb,
                                            out uint needed, out uint returned);

    [DllImport("winspool.drv", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "GetDefaultPrinterW")]
    private static extern bool GetDefaultPrinter(StringBuilder? buf, ref int size);

    [DllImport("gdi32.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "CreateDCW")]
    private static extern IntPtr CreateDC(string driver, string device, string? output, IntPtr devMode);

    [DllImport("gdi32.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "StartDocW")]
    private static extern int StartDoc(IntPtr hdc, ref DOCINFO doc);

    [DllImport("gdi32.dll", SetLastError = true)] private static extern int StartPage(IntPtr hdc);
    [DllImport("gdi32.dll", SetLastError = true)] private static extern int EndPage(IntPtr hdc);
    [DllImport("gdi32.dll", SetLastError = true)] private static extern int EndDoc(IntPtr hdc);
    [DllImport("gdi32.dll", SetLastError = true)] private static extern int AbortDoc(IntPtr hdc);
    [DllImport("gdi32.dll", SetLastError = true)] private static extern bool DeleteDC(IntPtr hdc);
    [DllImport("gdi32.dll")] private static extern int GetDeviceCaps(IntPtr hdc, int index);
    [DllImport("gdi32.dll")] private static extern int SetStretchBltMode(IntPtr hdc, int mode);
    [DllImport("gdi32.dll")] private static extern bool SetBrushOrgEx(IntPtr hdc, int x, int y, IntPtr old);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern int StretchDIBits(IntPtr hdc, int xDest, int yDest, int wDest, int hDest,
        int xSrc, int ySrc, int wSrc, int hSrc, IntPtr bits, ref BITMAPINFOHEADER info, int usage, uint rop);
}
