using System.Text.Json;

namespace PumpYaqobi.App.Services;

/// <summary>
/// ══ پیامی که یک بار دیده شد، دوباره نمی‌آید ══════════════════════════════════
///
/// گزارشِ صاحب ریپو (۱۴۰۵/۰۷/۱۵): «یک پیامِ رو مخ هی هر بار میاد که می‌گه
/// اشتراکِ شما تمدید شد یا اشتراک ندارین، یا بسته نمی‌شه، یا با هر بار باز شدنِ
/// برنامه میاد، پایینِ صفحه هم هست.»
///
/// دو ریشه، و هر دو همین‌جا بسته شد:
/// <list type="bullet">
/// <item>⛔ پیامِ مدیر (مثلاً «اشتراک تمدید شد») هیچ‌وقت به سرور «خوانده شد»
/// گفته نمی‌شد و فقط در حافظهٔ همان اجرا یادش می‌ماند — پس با هر باز شدنِ
/// برنامه دوباره توست، اعلانِ ویندوز و نوارِ پایین می‌شد. حالا هم روی سرور
/// «خوانده» می‌شود و هم این‌جا (اگر نرسید، باز هم تکرار نمی‌شود).</item>
/// <item>⛔ «بستن»ِ نوارِ پایین همان نوارِ «اشتراک فعال نیست» را دوباره
/// می‌نشاند — یعنی هرگز بسته نمی‌شد. حالا بستن یعنی بستن: همان نوع پیام تا
/// عوض شدنِ حالِ اشتراک برنمی‌گردد (حالش در «پروفایل» همیشه هست).</item>
/// </list>
/// فایلِ کوچکِ <c>notices-seen.json</c> کنارِ تنظیمات؛ خراب یا نبودنش فقط یعنی
/// یک بارِ دیگر دیدن، نه هیچ خرابیِ دیگری.
/// </summary>
public static class SeenNotices
{
    private const int Keep = 200;
    private static readonly object Gate = new();

    /// <summary>این پیامِ سرور قبلاً دیده شده؟</summary>
    public static bool Seen(string id)
    {
        lock (Gate) return Read().Ids.Contains(id);
    }

    /// <summary>دیده شد — تا ابد.</summary>
    public static void MarkSeen(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return;
        lock (Gate)
        {
            var s = Read();
            if (s.Ids.Contains(id)) return;
            s.Ids.Add(id);
            if (s.Ids.Count > Keep) s.Ids.RemoveRange(0, s.Ids.Count - Keep);
            Write(s);
        }
    }

    /// <summary>نوعِ نوارِ اشتراک که کاربر بسته است («closed» · «grace» · «soon»). خالی ⇒ هیچ.</summary>
    public static string DismissedBanner
    {
        get { lock (Gate) return Read().Banner; }
        set { lock (Gate) { var s = Read(); if (s.Banner == value) return; s.Banner = value ?? ""; Write(s); } }
    }

    private sealed class State
    {
        public List<string> Ids { get; set; } = new();
        public string Banner { get; set; } = "";
    }

    private static string FilePath() => Path.Combine(AppSettings.Dir, "notices-seen.json");

    private static State Read()
    {
        try
        {
            var p = FilePath();
            if (!File.Exists(p)) return new State();
            return JsonSerializer.Deserialize<State>(File.ReadAllText(p)) ?? new State();
        }
        catch { return new State(); }
    }

    private static void Write(State s)
    {
        try
        {
            var p = FilePath();
            var tmp = p + ".tmp-" + Environment.ProcessId;
            File.WriteAllText(tmp, JsonSerializer.Serialize(s));
            File.Move(tmp, p, overwrite: true);
        }
        catch { /* بدترین حالت: یک بارِ دیگر دیدن */ }
    }
}
