using PumpYaqobi.Domain.Entities;

namespace PumpYaqobi.Application.Services;

/// <summary>یک کارمند در جدولِ «کمبودی/اضافی».</summary>
/// <param name="Key">نامِ نرمال‌شده — کلیدِ گروه‌بندی و بندِ تسویه‌ها.</param>
/// <param name="Name">نام همان‌طور که بارِ اول در ورق نوشته شده.</param>
/// <param name="Shifts">چند شیفتِ شمرده‌شده — شیفتِ کاملاً خالی شمرده نمی‌شود.</param>
public readonly record struct StaffShortRow(
    string Key, string Name, int Shifts,
    decimal Short, decimal Excess,
    decimal PaidShort, decimal PaidExcess,
    decimal RemainShort, decimal RemainExcess);

/// <summary>
/// سهمِ **یک شیفتِ یک ورق** در کمبودی/اضافیِ یک کارمند (۱۴۰۵/۰۷/۱۸) — تاریخ، شیفت و دلیل:
/// قرضِ اعلام‌شده در پارچه منهای (قرض‌های ثبت‌شده + مصرف‌های همین ورق).
/// </summary>
public readonly record struct StaffShortLine(
    string Key, string Name, long WaraqId, string DateShamsi, int DateKey, ShiftKind Kind,
    decimal Declared, decimal Debt, decimal Expenses, decimal Shortage, decimal Excess);

/// <summary>
/// ══ کمبودی و اضافیِ کارمندان ═══════════════════════════════════════════════
/// رونوشتِ جمعِ ‎_renderStaffShortPanel‎.
///
///   🔴 کمبودی = قرضی که در پارچه نوشته شده ولی در ورق ثبت نشده → کارمند بدهکار
///   🟢 اضافی  = برعکسش → پمپ بدهکار
///
/// خودِ عددها هیچ‌جا ذخیره نمی‌شوند: هر بار از ورق‌های روزانه حساب می‌شوند
/// (‎computeWaraqShortage‎). تنها چیزی که ذخیره می‌شود «تسویه»هاست، و آن‌ها فقط
/// از باقی‌مانده کم می‌کنند — به هیچ ورقی دست نمی‌زنند.
///
/// ⚠️ گروه‌بندی با نامِ نرمال‌شده است نه شناسهٔ کارمند: در ورق فقط نام تایپ
/// می‌شود، پس «علي  احمد» و «علی احمد» باید یک نفر باشند.
///
/// ⛔ (۱۴۰۵/۰۷/۱۸) دوره: ‎RowsFor‎ فقط ورق‌های همان ماه/سال را می‌شمارد و فقط رسیدهایی که
/// برای همان دوره گرفته شده‌اند (‎SettleIn‎) — دوره‌ها با هم قاطی نمی‌شوند. «همهٔ ماه‌ها» همان
/// ‎Rows‎ِ پیشین است، مو‌به‌مو.
/// </summary>
public sealed class StaffShortService
{
    private readonly WaraqService _waraq;

    public StaffShortService(WaraqService waraq) => _waraq = waraq;

    public const string NoName = "— بی‌نام —";

    /// <summary>هر شیفتِ هر ورق که کمبودی یا اضافی یا قرضِ اعلام‌شده دارد — یک سطر، به ترتیبِ تاریخ.</summary>
    public List<StaffShortLine> Lines(IEnumerable<WaraqEntry> entries)
    {
        var list = new List<StaffShortLine>();
        foreach (var w in entries)
        {
            if (w is null) continue;
            foreach (var sd in w.Shifts)
            {
                if (sd is null) continue;
                var t = _waraq.ShiftTotals(sd);
                var r = _waraq.Shortage(t);
                if (r.Shortage == 0m && r.Excess == 0m && r.Declared == 0m) continue;
                var name = (sd.WorkerName ?? "").Trim();
                if (name.Length == 0) name = NoName;
                list.Add(new StaffShortLine(PostingService.NormFa(name), name, w.Id, w.DateShamsi ?? "",
                                            w.DateKey, sd.Kind, r.Declared, t.Debt, t.Expenses,
                                            r.Shortage, r.Excess));
            }
        }
        return list.OrderBy(l => l.DateKey).ThenBy(l => l.Kind).ToList();
    }

    /// <summary>جدولِ کارمندان — بدهکارترین‌ها اول.</summary>
    public List<StaffShortRow> Rows(IEnumerable<WaraqEntry> entries,
                                    IEnumerable<StaffShortSettle> settles)
    {
        // ترتیبِ ورود نگه داشته می‌شود: ‎Object.values‎ در جاوااسکریپت هم برای
        // کلیدهای غیرعددی همان ترتیبِ اولین دیده شدن را می‌دهد.
        var order = new List<string>();
        var agg = new Dictionary<string, (string Name, int Shifts, decimal Short, decimal Excess)>();

        foreach (var w in entries)
        {
            if (w is null) continue;
            foreach (var sd in w.Shifts)
            {
                if (sd is null) continue;
                var r = _waraq.Shortage(_waraq.ShiftTotals(sd));
                if (r.Shortage == 0m && r.Excess == 0m && r.Declared == 0m) continue;

                var name = (sd.WorkerName ?? "").Trim();
                if (name.Length == 0) name = NoName;
                var key = PostingService.NormFa(name);

                if (!agg.TryGetValue(key, out var cur))
                { order.Add(key); cur = (name, 0, 0m, 0m); }

                agg[key] = (cur.Name, cur.Shifts + 1,
                            cur.Short + r.Shortage, cur.Excess + r.Excess);
            }
        }

        var paid = settles.Where(s => s is not null).ToList();
        decimal SumOf(string key, StaffSettleKind kind) =>
            paid.Where(s => s.NameKey == key && s.Kind == kind).Sum(s => s.Amount);

        var list = new List<StaffShortRow>(order.Count);
        foreach (var key in order)
        {
            var x = agg[key];
            var ps = SumOf(key, StaffSettleKind.Short);
            var pe = SumOf(key, StaffSettleKind.Excess);
            list.Add(new StaffShortRow(key, x.Name, x.Shifts, x.Short, x.Excess, ps, pe,
                RemainShort:  Math.Max(0m, TankDipService.JsRound(x.Short)  - TankDipService.JsRound(ps)),
                RemainExcess: Math.Max(0m, TankDipService.JsRound(x.Excess) - TankDipService.JsRound(pe))));
        }

        return list.OrderByDescending(x => x.RemainShort + x.RemainExcess).ToList();
    }

    /// <summary>همان جدول، فقط ورق‌ها و رسیدهای یک دوره.</summary>
    public List<StaffShortRow> RowsFor(IEnumerable<WaraqEntry> entries, IEnumerable<StaffShortSettle> settles,
                                       ProfitPeriod period) =>
        Rows(entries.Where(w => w is not null && period.Contains(w.DateKey)),
             settles.Where(s => s is not null && SettleIn(s, period)));

    /// <summary>
    /// این رسید مالِ این دوره است؟ «همه» همه را می‌پذیرد. رسیدی که برای یک ماه گرفته شده
    /// (‎ForMonth‎) فقط در همان ماه و سالِ آن؛ رسیدِ کهنهٔ بی‌ماه با تاریخِ خودش.
    /// </summary>
    public static bool SettleIn(StaffShortSettle s, ProfitPeriod period)
    {
        if (period.IsAll) return true;
        var f = PumpYaqobi.Application.Localization.Shamsi.ToEnDigits(s.ForMonth ?? "").Trim();
        if (f.Length == 7 && f[4] == '/')
            return f[..4] == period.Year && (period.Month.Length == 0 || f[5..] == period.Month);
        //  رسیدِ گرفته‌شده روی «یک سال» فقط در همان سال (نه در ماه‌هایش — مالِ ماهِ خاصی نیست)
        if (f.Length == 4 && f.All(char.IsDigit))
            return period.Month.Length == 0 && f == period.Year;
        return period.Contains(s.DateKey);
    }
}
