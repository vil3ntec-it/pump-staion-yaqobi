using System.Text.Json;
using PumpYaqobi.App.Services;
using PumpYaqobi.Application.Localization;

namespace PumpYaqobi.App.ViewModels;

/// <summary>
/// ══ نقطهٔ سرخِ «ماهِ تازه» — جای نوارِ «ماهِ فلان شروع شد» ═══════════════════
///
/// خواستهٔ صریحِ صاحب ریپو (۱۴۰۵/۰۷/۱۳): «ماه که عوض می‌شود نمی‌خواهم آن
/// مدل که الان نشان می‌دهد باشد که بگوید این ماه فلان‌فلان شده… روی همان کادرِ
/// کشوییِ ماه‌ها یا سال‌ها یک نقطهٔ سرخ بیاید، و رویش که زد همان ماهی که قبلاً
/// بود را با یک نقطهٔ سرخ نشان بدهد… و وقتی رفت رویشان آن نقطه‌ها بروند و دیگر
/// دیده نشوند، مگر این‌که ماهِ دیگر عوض شود.»
///
/// <para>
/// ⛔ <b>یک قاعده، یک جا</b> (<see cref="MarkOf"/>): «ماهِ نقطه‌دار» تازه‌ترین
/// ماهی است که داده دارد و <b>پیش از</b> ماهِ جاری است — همان ماهی که با
/// عوض شدنِ ماه از جلوی چشم رفت. ماهِ جاری خودش هیچ‌وقت نقطه نمی‌گیرد، و
/// بخشی که پیش از این ماه هیچ داده‌ای ندارد هم نه (چیزی برای پیدا کردن نیست).
/// </para>
///
/// <para>
/// ⛔ <b>«دیدم» یعنی رفتن به همان ماه</b>، نه باز کردنِ کشویی: نقطه برای این
/// است که کاربر ماهِ پیش را پیدا کند، پس تا پیدایش نکرده می‌ماند. با رفتن،
/// ماهِ جاری در <see cref="MonthDotStore"/> ثبت می‌شود و تا ماهِ بعد هیچ
/// نقطه‌ای برنمی‌گردد — نه با بستن و باز کردنِ برنامه.
/// </para>
///
/// ⚠️ هیچ منطقِ ماهی عوض نمی‌شود: کدام ماه نشان داده شود، کِی خودکار عوض
/// شود و هر چیزِ دیگر همان است که بود. نقطه فقط <b>نشان</b> است.
/// </summary>
public static class MonthDot
{
    /// <summary>
    /// ماهی که نقطه می‌گیرد — یا خالی. <paramref name="seen"/> ماهی است که
    /// کاربر عوض شدنش را دیده (<see cref="MonthDotStore.SeenOf"/>).
    /// </summary>
    public static string MarkOf(IEnumerable<string> dataMonths, string now, string seen)
    {
        now = Shamsi.ToEnDigits(now ?? "");
        if (now.Length == 0 || Shamsi.ToEnDigits(seen ?? "") == now) return "";
        var best = "";
        foreach (var raw in dataMonths ?? Array.Empty<string>())
        {
            var k = Shamsi.ToEnDigits(raw ?? "");
            if (!IsMonthKey(k)) continue;                          // «همه»، خالی، کلیدِ خراب
            if (string.CompareOrdinal(k, now) >= 0) continue;      // ماهِ جاری و آینده نه
            if (string.CompareOrdinal(k, best) > 0) best = k;
        }
        return best;
    }

    /// <summary>«1405/07» — چهار رقم، خط، دو رقم.</summary>
    public static bool IsMonthKey(string k) =>
        k.Length == 7 && k[4] == '/' && k.Take(4).All(char.IsDigit) && k.Skip(5).All(char.IsDigit);
}

/// <summary>
/// ══ «کدام بخش عوض شدنِ ماه را دیده» — یک فایلِ کوچکِ کنارِ تنظیمات ═════════
///
/// ⚠️ <b>چرا فایلِ جدا، نه ‎settings.json‎ و نه جدولِ ‎Settings‎ی دفتر:</b>
/// <list type="bullet">
/// <item>جدولِ ‎Settings‎ اجازهٔ ‎ManageSettings‎ می‌خواهد — کارمندی که ماهِ پیش را
///   باز کند استثنا می‌گرفت — و هر نوشتنش ‎PumpDbContext.Version‎ را بالا می‌برد.</item>
/// <item>‎settings.json‎ از چند نمونهٔ هم‌زمان نوشته می‌شود و نوبتِ «در صف»ش فقط
///   یک شیء نگه می‌دارد؛ این مقدار می‌توانست زیرِ یک عکسِ کهنه گم شود.</item>
/// </list>
/// این فایل فقط همین را دارد، ماهی یک بار نوشته می‌شود، و نشدنش هیچ‌چیز را
/// نمی‌شکند — بدترین حالت یک نقطهٔ سرخِ دوباره است.
/// </summary>
public static class MonthDotStore
{
    private static readonly object Gate = new();
    private static string? _dir;
    private static Dictionary<string, string> _map = new(StringComparer.Ordinal);

    private static string FileOf(string dir) => Path.Combine(dir, "month-dots.json");

    /// <summary>نقشهٔ همان پوشه — یک بار خوانده و نگه داشته می‌شود.</summary>
    private static Dictionary<string, string> Map()
    {
        var dir = AppSettings.Dir;
        if (_dir == dir) return _map;
        _dir = dir;
        _map = new(StringComparer.Ordinal);
        try
        {
            var path = FileOf(dir);
            if (File.Exists(path)
                && JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path)) is { } got)
                foreach (var kv in got) _map[kv.Key] = kv.Value ?? "";
        }
        catch { /* خراب یا قفل ⇒ نقشهٔ خالی؛ بدترین حالت یک نقطهٔ دوباره */ }
        return _map;
    }

    /// <summary>ماهی که این بخش آخرین بار عوض شدنش را دید — یا خالی.</summary>
    public static string SeenOf(string section)
    {
        lock (Gate) return Map().TryGetValue(section, out var v) ? v : "";
    }

    /// <summary>کاربر به ماهِ نقطه‌دار رفت ⇒ تا ماهِ بعد دیگر نقطه‌ای نیست.</summary>
    public static void Ack(string section, string month)
    {
        lock (Gate)
        {
            var map = Map();
            if (map.TryGetValue(section, out var v) && v == month) return;
            map[section] = month;
            try
            {
                var path = FileOf(_dir!);
                Directory.CreateDirectory(_dir!);
                var tmp = path + "." + Guid.NewGuid().ToString("N")[..8] + ".tmp";
                File.WriteAllText(tmp, JsonSerializer.Serialize(map));
                File.Move(tmp, path, overwrite: true);
            }
            catch { /* رفاه است — در حافظه ثبت شد؛ اجرای بعد شاید یک بار دیگر بپرسد */ }
        }
    }
}
