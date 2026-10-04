namespace PumpYaqobi.App.ViewModels;

/// <summary>
/// ══ ترتیبِ بخش‌های نوار را خودِ کاربر می‌چیند (۱۴۰۵/۰۷/۱۵) ═══════════════════
///
/// خواستهٔ صاحب ریپو: «چرا قابلیتِ تغییرِ جاهای بخش‌ها نیست؟ می‌خواهم صرافی را
/// مثلاً بیاورم اول یا آخر.»
///
/// ⛔ <b>فقط ترتیبِ دیدن</b> عوض می‌شود: ‎MainViewModel.Sections‎ (و شمارهٔ هر
/// بخش در ‎NavOrderTests‎، بخش‌های پنهان، گرم کردن و…) دست نمی‌خورد. هیچ منطقی
/// به جای بخش بسته نیست جز ‎Alt+عدد‎، که از امروز همان ترتیبِ نوار را می‌رود —
/// «بخشِ سوم» یعنی سومی که کاربر می‌بیند.
///
/// ⚠️ شناسه‌ای که در ذخیره هست ولی دیگر بخشی ندارد نادیده گرفته می‌شود، و بخشِ
/// تازه‌ای که هنوز در ذخیره نیست سرِ جای پیش‌فرضش می‌آید — پس نسخهٔ تازه با
/// بخشِ تازه هیچ‌وقت آن را گم نمی‌کند.
/// همه‌چیز خالص است و آزمون دارد (‎NavReorderTests‎).
/// </summary>
public static class NavOrder
{
    public enum Where { First, Earlier, Later, Last }

    /// <summary>ترتیبِ نشان‌دادنی: اول هر چه ذخیره شده، بعد تازه‌ها سرِ جای پیش‌فرضشان.</summary>
    public static List<string> Arrange(IReadOnlyList<string> defaults, string? saved)
    {
        var want = Parse(saved).Where(defaults.Contains).Distinct().ToList();
        if (want.Count == 0) return defaults.ToList();
        var result = new List<string>(want);
        //  بخشی که در ذخیره نیست (نسخهٔ تازه آورده): پس از همسایهٔ پیش‌فرضِ خودش
        for (var i = 0; i < defaults.Count; i++)
        {
            var id = defaults[i];
            if (result.Contains(id)) continue;
            var before = i > 0 ? result.IndexOf(defaults[i - 1]) : -1;
            result.Insert(before + 1, id);
        }
        return result;
    }

    /// <summary>یک بخش را جابه‌جا می‌کند و ترتیبِ تازه را برمی‌گرداند.</summary>
    public static List<string> Move(IReadOnlyList<string> shown, string id, Where where)
    {
        var list = shown.ToList();
        var i = list.IndexOf(id);
        if (i < 0) return list;
        list.RemoveAt(i);
        var j = where switch
        {
            Where.First => 0,
            Where.Earlier => Math.Max(0, i - 1),
            Where.Later => Math.Min(list.Count, i + 1),
            _ => list.Count,
        };
        list.Insert(j, id);
        return list;
    }

    /// <summary>
    /// کشیدن و رها کردن (۱۴۰۵/۰۷/۱۶ — «با کشیدنشان، نه کلیکِ راست»): بخش در
    /// جای ‎index‎ِ فهرستِ <b>پیش از</b> برداشتنش می‌نشیند (همان «پیش از این
    /// خانه» که زیرِ ماوس دیده می‌شود). ‎index = Count‎ یعنی آخرِ نوار.
    /// </summary>
    public static List<string> MoveTo(IReadOnlyList<string> shown, string id, int index)
    {
        var list = shown.ToList();
        var i = list.IndexOf(id);
        if (i < 0) return list;
        index = Math.Clamp(index, 0, list.Count);
        list.RemoveAt(i);
        if (index > i) index--;
        list.Insert(index, id);
        return list;
    }

    /// <summary>ترتیبِ پیش‌فرض ذخیره نمی‌شود (خالی) — تا بخشِ تازهٔ فردا جای درستش را بگیرد.</summary>
    public static string Save(IReadOnlyList<string> defaults, IReadOnlyList<string> shown)
        => shown.SequenceEqual(defaults) ? "" : string.Join(",", shown);

    private static IEnumerable<string> Parse(string? s)
        => (s ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
