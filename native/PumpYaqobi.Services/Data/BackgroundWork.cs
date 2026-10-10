using PumpYaqobi.Application.Localization;

namespace PumpYaqobi.Services.Data;

/// <summary>
/// ══ «برنامه الان در پس‌زمینه چه می‌کند» (۱۴۰۵/۰۷/۲۲) ══
/// بندِ ۴ِ کندی‌ها: وقتی کارِ سنگینی پشتِ صحنه است (مثلِ مرتب کردنِ فروشِ ورق‌های قدیم)، کاربر
/// می‌دید برنامه کمی کُند است و نمی‌دانست چرا. حالا یک نشانِ کوچک در سربرگ همان را می‌گوید —
/// با درصد اگر کار درصد دارد. ⛔ فقط نمایش است؛ هیچ کاری از این‌جا شروع یا متوقف نمی‌شود.
/// </summary>
public static class BackgroundWork
{
    private static readonly object Gate = new();
    private static readonly List<(string Key, string Label, int Done, int Total)> Jobs = new();

    /// <summary>هر تغییر — روی همان نخی که کار است؛ شنونده خودش به نخِ رابط می‌برد.</summary>
    public static event Action? Changed;

    public static void Set(string key, string label, int done = 0, int total = 0)
    {
        lock (Gate)
        {
            var i = Jobs.FindIndex(j => j.Key == key);
            if (i >= 0) Jobs[i] = (key, label, done, total);
            else Jobs.Add((key, label, done, total));
        }
        Changed?.Invoke();
    }

    public static void Clear(string key)
    {
        bool removed;
        lock (Gate) removed = Jobs.RemoveAll(j => j.Key == key) > 0;
        if (removed) Changed?.Invoke();
    }

    /// <summary>«⏳ مرتب کردنِ فروشِ ورق‌های قدیم… ۴۰٪» — خالی یعنی کاری نیست.</summary>
    public static string Text
    {
        get
        {
            lock (Gate)
            {
                if (Jobs.Count == 0) return "";
                var j = Jobs[0];
                var pct = j.Total > 0 ? " " + Shamsi.Money(Math.Min(100, j.Done * 100 / j.Total)) + "٪" : "";
                var more = Jobs.Count > 1 ? " · و " + Shamsi.Money(Jobs.Count - 1) + " کارِ دیگر" : "";
                return "⏳ " + j.Label + "…" + pct + more;
            }
        }
    }
}
