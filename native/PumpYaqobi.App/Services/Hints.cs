using System.Text.Json;

namespace PumpYaqobi.App.Services;

/// <summary>
/// ══ شورا، ث۲ — «قاعده‌های پنهان دیده شوند» ═════════════════════════════════
///
/// «/نام»، «پ/د»، Ctrl+Tab، Shift+عدد و Ctrl+عدد تا امروز فقط با F1 دیده
/// می‌شدند. سه راه، و ⛔ <b>هیچ رفتاری عوض نمی‌شود</b> — فقط گفته می‌شود:
///   ۱) نوشتهٔ راهنمای کم‌رنگ در خودِ کادر (<see cref="Watermark"/>)
///   ۲) نوارِ کوچکِ میانبرهای همان بخش زیرِ جدول — بسته‌شدنی (<see cref="Strip"/>)
///   ۳) نخستین بارِ هر قاعده یک «💡 می‌دانستید؟» (<see cref="DidYouKnow"/>)
///
/// ⛔ متن‌ها فقط همین‌جا نوشته می‌شوند (آزمونِ <c>HintsTests</c> هر کلید را با
/// <c>ShortcutsWindow.All</c> و رفتارِ واقعی می‌سنجد).
/// ⚠️ «دیده شد» یادِ <b>همین کامپیوتر</b> است (<c>hints.json</c> کنارِ تنظیمات) —
/// نه در دفتر، نه در فایلِ کاملِ برنامه. خراب یا قفل ⇒ بدترین حالت یک راهنمای دوباره.
/// </summary>
public static class Hints
{
    /// <summary>نوشتهٔ کم‌رنگِ داخلِ کادرِ خالی، با کلیدِ ستون.</summary>
    public static readonly IReadOnlyDictionary<string, string> Watermark = new Dictionary<string, string>
    {
        ["waraq-name"] = "/ برای حساب · پ یا د برای تیل",
    };

    /// <summary>نوارِ زیرِ جدولِ هر بخش.</summary>
    public static readonly IReadOnlyDictionary<string, string> Strip = new Dictionary<string, string>
    {
        ["rows"] = "⌨️ Ctrl+عدد = همان‌قدر ردیف · Shift+عدد = برداشتنِ ردیف‌های آخر · Ctrl+Z برگشت · F1 همهٔ میانبرها",
        ["waraq"] = "⌨️ در «نام»: «/ هارون» ردیف را به حسابِ هارون می‌برد · «پ» یا «د» تیل را می‌گوید · Ctrl+Tab روز ⇄ شب · F1 همه",
        ["parcha"] = "⌨️ Enter = ذخیرهٔ هر کارتِ پر و پارچهٔ جدید · Tab یا Ctrl+Tab = تیلِ دیگر · F1 همه",
    };

    /// <summary>«💡 می‌دانستید؟» — یک بار برای هر کلید.</summary>
    public static readonly IReadOnlyDictionary<string, string> Tips = new Dictionary<string, string>
    {
        ["waraq-name"] = "💡 می‌دانستید؟ اگر در «نام» بنویسید «/ هارون بابت نان»، ردیف به حسابِ هارون می‌رود و در ورق فقط «بابت نان» دیده می‌شود. «پ» یا «د» در نام هم تیل را پطرول یا دیزل می‌کند.",
        ["parcha"] = "💡 می‌دانستید؟ در پارچه Enter هر کارتِ پر را ذخیره می‌کند و پارچهٔ جدید باز می‌شود؛ Tab یا Ctrl+Tab تیل را عوض می‌کند.",
        ["rows"] = "💡 می‌دانستید؟ Ctrl+عدد همان‌قدر ردیف می‌افزاید و Shift+عدد همان‌قدر ردیفِ آخر را برمی‌دارد (ردیفِ پر پیش از برداشتن پرسیده می‌شود).",
    };

    /// <summary>در آزمون‌ها و سنجه‌ها هیچ توستی نمی‌آید (یادها همچنان نوشته می‌شوند).</summary>
    public static bool Quiet { get; set; }

    /// <summary>جای نشان دادنِ توست — پوسته می‌نشاندش.</summary>
    public static Action<string>? Show { get; set; }

    private static readonly object Gate = new();
    private static string? _dir;
    private static HashSet<string> _seen = new(StringComparer.Ordinal);

    private static string FileOf(string dir) => Path.Combine(dir, "hints.json");

    private static HashSet<string> Seen()
    {
        var dir = AppSettings.Dir;
        if (_dir == dir) return _seen;
        _dir = dir;
        _seen = new(StringComparer.Ordinal);
        try
        {
            var p = FileOf(dir);
            if (File.Exists(p) && JsonSerializer.Deserialize<List<string>>(File.ReadAllText(p)) is { } got)
                foreach (var k in got) if (!string.IsNullOrEmpty(k)) _seen.Add(k);
        }
        catch { /* خراب ⇒ خالی */ }
        return _seen;
    }

    private static void Save(HashSet<string> set)
    {
        try
        {
            var p = FileOf(AppSettings.Dir);
            var tmp = p + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(set.OrderBy(x => x, StringComparer.Ordinal).ToList()));
            File.Move(tmp, p, overwrite: true);
        }
        catch { /* نشد ⇒ بارِ بعد دوباره گفته می‌شود */ }
    }

    public static bool IsSeen(string key) { lock (Gate) return Seen().Contains(key); }

    /// <summary>علامتِ «دیده شد»؛ <c>true</c> یعنی همین حالا بار اول بود.</summary>
    public static bool Mark(string key)
    {
        lock (Gate)
        {
            var s = Seen();
            if (!s.Add(key)) return false;
            Save(s);
            return true;
        }
    }

    /// <summary>نوارِ زیرِ جدول بسته شد — تا همیشه روی همین کامپیوتر.</summary>
    /// <summary>
    /// بستنِ نوارِ یک کلید — ⛔ همهٔ نوارهای همان کلید (بخش‌های دیگر هم)
    /// همان لحظه پنهان می‌شوند (<see cref="StripDismissed"/>).
    /// </summary>
    public static void DismissStrip(string key)
    {
        Mark("strip:" + key);
        try { StripDismissed?.Invoke(key); } catch { /* رفاه است */ }
    }

    /// <summary>نوارِ این کلید بسته شد — هر ‎HintStrip‎ی با همین کلید پنهان شود.</summary>
    public static event Action<string>? StripDismissed;
    public static bool StripVisible(string key) => Strip.ContainsKey(key) && !IsSeen("strip:" + key);

    /// <summary>نخستین بار ⇒ یک توست؛ بعد هرگز.</summary>
    public static bool DidYouKnow(string key)
    {
        if (!Tips.TryGetValue(key, out var text)) return false;
        if (!Mark("tip:" + key)) return false;
        if (!Quiet) try { Show?.Invoke(text); } catch { /* توست رفاه است */ }
        return true;
    }

    /// <summary>فقط آزمون: حافظه را فراموش می‌کند (فایل دست نمی‌خورد).</summary>
    public static void Forget() { lock (Gate) { _dir = null; _seen = new(StringComparer.Ordinal); } }
}
