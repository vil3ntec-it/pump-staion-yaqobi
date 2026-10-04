namespace PumpYaqobi.App.Services;

/// <summary>
/// ══ شورا، ج۷ — حاشیه جداشدنی ══════════════════════════════════════════════════
///
/// دفتر (پارچه، ورق، حساب‌ها) اصل است؛ این‌ها <b>حاشیه</b>‌اند: عکسِ زندهٔ پمپ،
/// هشدارها، نرخِ اتحادیه از تلگرام، تنظیماتِ زنده، به‌روزرسانیِ خودکار و
/// پشتیبانِ سرور. تا امروز استثنای یک باگ در هر کدام یا در همان حلقه بی‌صدا
/// بلعیده می‌شد (و با «بی‌اینترنت» یکی شمرده می‌شد)، یا — اگر سرِ راه‌اندازی بود —
/// همهٔ کارهای بعد از ورود را با خودش می‌برد.
///
/// ⛔ <b>هر ماژول از این‌جا روشن می‌شود و هر خطای واقعی‌اش فقط خودش را خاموش
/// می‌کند</b> (<see cref="Start"/> · <see cref="Run"/>)، و پروفایل می‌گوید «این بخش
/// کار نکرد». دفتر هیچ‌وقت به هیچ‌کدام بند نیست.
///
/// ⚠️ <b>قطعیِ شبکه خطا نیست</b> (<see cref="Transient"/>): بی‌اینترنت، تایم‌اوت و
/// لغو ماژول را خاموش نمی‌کنند — وگرنه یک شبِ بی‌مودم همهٔ حاشیه را برای همیشه
/// می‌بست.
/// </summary>
public static class Modules
{
    public sealed record Info(string Id, string Title);

    /// <summary>فهرستِ بسته — نامِ تایپی‌شده خطا می‌دهد، نه سکوت.</summary>
    public static readonly IReadOnlyList<Info> All = new[]
    {
        new Info("publisher", "عکسِ زندهٔ پمپ برای اپِ گوشی"),
        new Info("alerts", "هشدارهای قرض‌دار و مخزن"),
        new Info("rate", "نرخِ اتحادیه از تلگرام"),
        new Info("liveconfig", "تنظیماتِ زنده"),
        new Info("autoupdate", "به‌روزرسانیِ خودکار"),
        new Info("backup-push", "پشتیبانِ سرور"),
        //  ⛔ شورا د۵ — پیام‌رسان هم حاشیه است: باگِ گرفتنِ پیام فقط خودش را خاموش می‌کند
        new Info("chat", "گرفتنِ پیام‌های پیام‌رسان"),
    };

    private static readonly object Gate = new();
    private static readonly Dictionary<string, string> _failed = new(StringComparer.Ordinal);

    /// <summary>برای آزمون: همین ماژول سرِ روشن شدن یا کارِ دوره‌ای عمداً بشکند.</summary>
    public static Func<string, Exception?>? BreakHook { get; set; }

    public static event Action? Changed;

    private static Info Of(string id) =>
        All.FirstOrDefault(m => m.Id == id) ?? throw new ArgumentException("ماژولِ ناشناخته: " + id, nameof(id));

    /// <summary>ماژول هنوز سالم است؟ (خاموش‌شده دیگر هیچ کاری نمی‌کند.)</summary>
    public static bool Alive(string id)
    {
        Of(id);
        lock (Gate) return !_failed.ContainsKey(id);
    }

    /// <summary>ماژول‌های خاموش‌شده و دلیلشان.</summary>
    public static IReadOnlyDictionary<string, string> Failed
    {
        get { lock (Gate) return new Dictionary<string, string>(_failed); }
    }

    /// <summary>جملهٔ پروفایل؛ خالی یعنی همه سالم‌اند.</summary>
    public static string FailedLine()
    {
        var f = Failed;
        return f.Count == 0 ? ""
            : "⚠️ این بخش کار نکرد و خاموش ماند (دفتر سالم است): "
              + string.Join(" · ", All.Where(m => f.ContainsKey(m.Id)).Select(m => m.Title));
    }

    /// <summary>قطعیِ شبکه، تایم‌اوت و لغو — «خطای ماژول» نیستند.</summary>
    public static bool Transient(Exception e) => e is OperationCanceledException
        or System.Net.Http.HttpRequestException or System.Net.Sockets.SocketException
        or System.IO.IOException or TimeoutException
        //  ⛔ قفلِ «فقط‌خواندنی» (۱۴۰۵/۰۷/۲۰) خرابی نیست — با تمدید خودش باز می‌شود
        or PumpYaqobi.Application.Security.PermissionDeniedException;

    public static void Fail(string id, Exception e)
    {
        Of(id);
        lock (Gate)
        {
            if (_failed.ContainsKey(id)) return;
            _failed[id] = e.GetType().Name;
        }
        try { Changed?.Invoke(); } catch { }
    }

    /// <summary>روشن کردنِ یک ماژول — شکستنش فقط خودش را خاموش می‌کند.</summary>
    public static void Start(string id, Action start)
    {
        if (!Alive(id)) return;
        try
        {
            if (BreakHook?.Invoke(id) is { } broken) throw broken;
            start();
        }
        catch (Exception e) { Fail(id, e); }
    }

    /// <summary>
    /// یک دورِ کارِ ماژول. خاموش ⇒ هیچ. خطای شبکه ⇒ همان «بی‌اینترنت»ِ همیشه، بی
    /// خاموش شدن. هر خطای دیگر ⇒ فقط همین ماژول خاموش. لغوِ خودِ حلقه بیرون می‌رود.
    /// </summary>
    public static async Task Run(string id, Func<Task> step, CancellationToken ct)
    {
        if (!Alive(id)) return;
        try
        {
            if (BreakHook?.Invoke(id) is { } broken) throw broken;
            await step();
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception e) when (Transient(e)) { /* بی‌اینترنت خطا نیست */ }
        catch (Exception e) { Fail(id, e); }
    }

    /// <summary>برای آزمون و برای «راه‌اندازیِ دوباره»ی دستی.</summary>
    public static void ResetForTests()
    {
        lock (Gate) _failed.Clear();
        BreakHook = null;
    }
}
