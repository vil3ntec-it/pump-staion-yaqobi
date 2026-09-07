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
/// </summary>
public sealed class StaffShortService
{
    private readonly WaraqService _waraq;

    public StaffShortService(WaraqService waraq) => _waraq = waraq;

    public const string NoName = "— بی‌نام —";

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
}
