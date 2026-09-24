namespace PumpYaqobi.App.ViewModels;

/// <summary>
/// ══ جدولی که میانبرِ صفحه‌کلید می‌تواند ردیف به آن بیفزاید یا از آن بردارد ══
///
/// در نسخهٔ وب این کار با <c>_kbAddRows</c> / <c>_kbDelRows</c> و یک نقشهٔ
/// دستی از شناسهٔ بخش‌ها انجام می‌شد. این‌جا به‌جای نقشه، هر بخشی که واقعاً
/// جدولِ ردیفی دارد خودش این قرارداد را امضا می‌کند — پس هیچ فهرستی نیست که
/// با افزودنِ بخشِ تازه از قلم بیفتد.
/// </summary>
public interface IRowBatchHost
{
    /// <summary>شمارِ ردیف‌های همین حالا — تا میانبر بفهمد کارش گرفت یا نه.</summary>
    int RowCount { get; }

    /// <summary>‎n‎ ردیفِ تازه به انتهای همین جدول.</summary>
    Task AddRowsAsync(int count);

    /// <summary>
    /// ‎n‎ ردیفِ آخر را بردارد. اگر ردیفِ کافی نباشد <b>هیچ کاری نمی‌کند</b> —
    /// نه خطا، نه حذفِ ناقص. همان قاعدهٔ نسخهٔ وب.
    /// </summary>
    Task DeleteRowsAsync(int count);

    /// <summary>
    /// همان ‎n‎ ردیفِ آخری که ‎Shift+عدد‎ برمی‌دارد — تا پیش از برداشتن بپرسد
    /// «این‌ها چیزی دارند؟» (<see cref="RowData.HasData"/>).
    /// </summary>
    IReadOnlyList<object> LastRows(int count);
}

/// <summary>
/// ══ «این ردیف چیزی دارد؟» — برای ‎Shift+عدد‎ ═════════════════════════════════
///
/// خواستهٔ صاحب ریپو (۱۴۰۵/۰۷/۱۲): «اگه جدول یا کادرِ جدول پر بود اون‌جا
/// تایید بخواد، اگه خالی بود حذف کنه بدونِ گفتن یا سوال شدن.»
///
/// ⚠️ «پر» یعنی چیزی که <b>کاربر</b> نوشته، نه چیزی که ردیفِ تازه خودش
/// دارد: تاریخِ امروز، فیِ پیش‌فرض، نرخِ روز، ارز و نوعِ تیل. آن‌ها در هر
/// ردیفِ خالی هم هستند و شمردنشان یعنی برای هر ردیفِ خالی پرسیدن.
/// ⚠️ ردیفِ قرض‌دار قاعدهٔ خودش را دارد و همان به کار می‌رود
/// (‎PostingService.IsBlankRow‎) — دو قاعده یعنی روزی یکی «خالی» بگوید و
/// دیگری «پر».
/// ⚠️ و اشتباه در این‌جا ارزان است: «پر» دیدنِ ردیفِ خالی فقط یک پرسشِ
/// اضافه است؛ و حذفِ ناخواسته هم با ‎Ctrl+Z‎ برمی‌گردد.
/// </summary>
public static class RowData
{
    public static bool HasData(object? row)
    {
        if (row is null) return false;
        var e = row.GetType().GetProperty("Entity")?.GetValue(row) ?? row;
        if (e is PumpYaqobi.Domain.Entities.DebtRow dr)
            return !PumpYaqobi.Application.Services.PostingService.IsBlankRow(dr);

        foreach (var p in e.GetType().GetProperties(System.Reflection.BindingFlags.Public
                                                    | System.Reflection.BindingFlags.Instance))
        {
            if (!p.CanRead || p.GetIndexParameters().Length > 0) continue;
            if (p.DeclaringType == typeof(PumpYaqobi.Domain.Entities.EntityBase)) continue;
            var n = p.Name;
            if (n == "Id" || n.EndsWith("Id") || n == "SortIndex" || n.Contains("Date")
                || n == "MonthKey" || n == "SyncUid" || n.Contains("Price") || n.Contains("Rate")
                || n.Contains("Per")) continue;
            object? v;
            try { v = p.GetValue(e); } catch { continue; }
            switch (v)
            {
                case string str when !string.IsNullOrWhiteSpace(str): return true;
                case decimal d when d != 0m: return true;
                case double f when f != 0d: return true;
                case int i when i != 0: return true;
                case long l when l != 0: return true;
            }
        }
        return false;
    }
}

/// <summary>
/// ══ فهرستِ کارتی که با ‎Alt+عدد‎ باز می‌شود ══════════════════════════════
/// شماره‌ها همان عددی است که زیرِ هر کارت نوشته شده (به ترتیبِ نمایش، پس با
/// جست‌وجو هم خودکار عوض می‌شود).
/// </summary>
public interface ICardGridHost
{
    /// <summary>کارتِ شمارهٔ ‎n‎ (از ۱) را باز کند؛ نبود، هیچ.</summary>
    Task OpenByNumberAsync(int number);
}
