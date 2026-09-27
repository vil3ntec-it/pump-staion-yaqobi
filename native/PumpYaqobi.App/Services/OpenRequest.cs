namespace PumpYaqobi.App.Services;

/// <summary>
/// ══ باز کردنِ فایل با دوبار-کلیک (۱۴۰۵/۰۷/۱۵) ════════════════════════════
///
/// گزارشِ صاحب ریپو با عکس: «چرا فایلِ برنامه رو که گرفتم و می‌خوام باز کنم،
/// برنامهٔ من پیشنهاد نمی‌شه؟» ⇒ نصاب دو پسوند را به برنامه می‌سپارد و
/// ویندوز مسیرِ فایل را به <c>PumpYaqobi.exe</c> می‌دهد:
///
/// <code>
/// .pumpyaqobi  ⇒ «فایلِ کاملِ برنامه» ⇒ همان «آوردنِ فایلِ کامل» (با همهٔ سنجش‌ها و پرسش)
/// .pumpkey     ⇒ کدِ اشتراکِ آفلاین   ⇒ همان «انتخابِ فایل» در «اشتراک و پلن‌ها»
/// </code>
///
/// ⛔ <b>هیچ کاری بی پرسش انجام نمی‌شود</b>: فایلِ کامل از همان درِ دکمه
/// می‌گذرد (سنجش ⇒ خلاصه ⇒ «بله، بیاور»). دوبار-کلیک فقط راهِ رسیدن است.
///
/// ⚠️ <b>برنامه از قبل باز است</b>: نمونهٔ دوم مسیر را در یک فایلِ کوچک کنارِ
/// تنظیمات می‌گذارد و نمونهٔ اول را بیدار می‌کند (<see cref="SingleInstance"/>).
/// </summary>
public static class OpenRequest
{
    public const string FullExt = ".pumpyaqobi";
    public const string KeyExt = ".pumpkey";

    private static readonly object Gate = new();
    private static readonly Queue<string> Pending = new();

    /// <summary>پس از ورود (<c>Ready</c>) خبر می‌دهد که درخواستی رسید.</summary>
    public static event Action? Arrived;

    private static string Inbox => Path.Combine(AppSettings.Dir, "open-request.txt");

    /// <summary>از آرگومان‌های خطِ فرمان، فقط فایلی که هست و پسوندش مالِ ماست.</summary>
    public static string? FromArgs(string[]? args)
    {
        foreach (var a in args ?? Array.Empty<string>())
        {
            if (string.IsNullOrWhiteSpace(a) || a.StartsWith('/') && !File.Exists(a)) continue;
            var ext = Path.GetExtension(a);
            if ((ext.Equals(FullExt, StringComparison.OrdinalIgnoreCase)
                 || ext.Equals(KeyExt, StringComparison.OrdinalIgnoreCase)) && File.Exists(a))
                return Path.GetFullPath(a);
        }
        return null;
    }

    /// <summary>نمونهٔ اول: درخواست را نگه دار تا برنامه آماده شود.</summary>
    public static void Add(string path)
    {
        lock (Gate) Pending.Enqueue(path);
        try { Arrived?.Invoke(); } catch { }
    }

    /// <summary>نمونهٔ دوم: مسیر را برای نمونهٔ اول بنویس.</summary>
    public static void Hand(string path)
    {
        try
        {
            Directory.CreateDirectory(AppSettings.Dir);
            File.AppendAllText(Inbox, path + Environment.NewLine);
        }
        catch { }
    }

    /// <summary>نمونهٔ اول با بیدار شدن: هر چه نمونهٔ دوم گذاشته برداشته شود.</summary>
    public static void CollectInbox()
    {
        string[] lines;
        try
        {
            if (!File.Exists(Inbox)) return;
            lines = File.ReadAllLines(Inbox);
            File.Delete(Inbox);
        }
        catch { return; }
        foreach (var l in lines)
            if (!string.IsNullOrWhiteSpace(l) && File.Exists(l.Trim())) Add(l.Trim());
    }

    /// <summary>یکی را بردار — تهی یعنی چیزی نمانده.</summary>
    public static string? Take()
    {
        lock (Gate) return Pending.Count > 0 ? Pending.Dequeue() : null;
    }
}
