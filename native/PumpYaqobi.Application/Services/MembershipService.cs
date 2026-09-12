using System.Globalization;
using PumpYaqobi.Application.Localization;
using PumpYaqobi.Domain.Entities;

namespace PumpYaqobi.Application.Services;

/// <summary>
/// مدتِ عضویتِ یک قرض‌دار — «چند وقت است مشتریِ ما است».
/// </summary>
/// <param name="FromShamsi">تاریخِ قدیمی‌ترین ردیفش؛ خالی یعنی هیچ ردیفِ تاریخ‌داری ندارد.</param>
/// <param name="Text">«۲ سال و ۴ ماه» — همان ‎_membershipText‎ی نسخهٔ وب.</param>
/// <param name="Days">شمارِ روزها؛ ‎-1‎ یعنی بی‌تاریخ.</param>
public readonly record struct MembershipRow(Debtor Person, string FromShamsi, string Text, int Days);

/// <summary>
/// یک خط از «قرض‌های دسته‌جمعی» — یک قرض‌دار با جمع‌هایش و مدتِ عضویتش.
/// </summary>
public readonly record struct DebtSummaryRow(Debtor Person, DebtSumFigures Figures, MembershipRow Age)
{
    /// <summary>هنوز بدهکار است؟ («الباقیِ مثبت» — همان ‎owes‎ی سایت.)</summary>
    public bool Owes => Figures.Albaqi > 0m;
}

/// <summary>
/// ══ مدتِ عضویت ═════════════════════════════════════════════════════════════
/// رونوشتِ ‎_personCreatedTs‎ · ‎_membershipText‎ · ‎_membershipRows‎ ·
/// ‎_debtAgeInfo‎ی نسخهٔ وب.
///
/// در سایت دو جا به کار می‌رود و هر دو در برنامهٔ نیتیو نبودند:
///   • دکمهٔ «⏳ مدت عضویت همه» بالای قرض‌داران (‎openMembershipList‎)؛
///   • ستونِ «چند وقت قرض‌دار» در «قرض‌های دسته‌جمعی» (‎_debtAgeInfo‎).
///
/// ⚠️ تفاوتش با «قرض‌های کهنه» عمدی است و در خودِ سایت هم همین است:
/// آن‌جا از **آخرین** ردیف حساب می‌شود («چند روز بی‌حرکت»)، این‌جا از
/// **اولین** ردیف («از کِی مشتریِ ما است»).
///
/// ⚠️ این سرویس هیچ چیزی نمی‌نویسد. نسخهٔ وب تاریخِ محاسبه‌شده را روی خودِ
/// شخص ذخیره می‌کرد (‎person.createdAt = ts‎)؛ این‌جا هر بار از روی ردیف‌ها
/// حساب می‌شود تا خواندنِ یک فهرست هیچ‌وقت به دیتابیس ننویسد.
/// </summary>
public sealed class MembershipService
{
    private static readonly PersianCalendar Cal = new();

    /// <summary>
    /// قدیمی‌ترین تاریخِ ردیف‌های شخص — حسابِ اصلی و همهٔ فرعی‌ها، تیل و پول.
    /// خالی یعنی هیچ ردیفِ تاریخ‌داری ندارد.
    /// </summary>
    public string FirstDate(Debtor? p)
    {
        if (p is null) return "";
        var bestK = 0;
        var best = "";
        foreach (var a in p.AllAccounts())
        {
            foreach (var r in a.FuelRows) Take(r?.DateShamsi);
            foreach (var r in a.MoneyRows) Take(r?.DateShamsi);
        }
        return best;

        void Take(string? d)
        {
            var k = Shamsi.Key(d);
            if (k == 0 || (bestK != 0 && k >= bestK)) return;
            bestK = k;
            best = d ?? "";
        }
    }

    /// <summary>‎_membershipRows()‎ — قدیمی‌ترین مشتری اول.</summary>
    /// <remarks>بی‌تاریخ‌ها ته فهرست می‌مانند، نه اولِ آن.</remarks>
    public List<MembershipRow> Rows(IEnumerable<Debtor> people, string today)
    {
        var list = new List<MembershipRow>();
        foreach (var p in people)
        {
            if (p is null) continue;
            list.Add(Row(p, today));
        }
        return list
            .OrderBy(r => r.Days < 0 ? 0 : 1)                    // بی‌تاریخ‌ها آخر
            .ThenByDescending(r => r.Days)
            .ToList();
    }

    /// <summary>‎_debtAgeInfo(p)‎ — تاریخِ شروع، متنِ مدت و شمارِ روزها.</summary>
    public MembershipRow Row(Debtor p, string today)
    {
        var from = FirstDate(p);
        return new MembershipRow(p, from, Text(from, today), Days(from, today));
    }

    /// <summary>شمارِ روزهای گذشته — ‎-1‎ یعنی بی‌تاریخ، ‎0‎ یعنی امروز یا آینده.</summary>
    public static int Days(string? fromShamsi, string? todayShamsi)
    {
        var a = Shamsi.ToDate(fromShamsi);
        var b = Shamsi.ToDate(todayShamsi) ?? DateTime.Today;
        if (a is null) return -1;
        return Math.Max(0, (int)(b.Date - a.Value.Date).TotalDays);
    }

    /// <summary>
    /// ‎_membershipText(ts)‎ — «۲ سال و ۴ ماه» / «۸ ماه» / «۱ سال و ۱۷ روز».
    ///
    /// ⚠️ سه ریزه‌کاری از خودِ سایت، که اگر نباشند متن فرق می‌کند:
    ///   • تفریق در تقویمِ **شمسی** انجام می‌شود، نه با تقسیمِ روزها بر ۳۶۵؛
    ///   • بیشترین **دو** تکه نوشته می‌شود («۲ سال و ۴ ماه»، نه «… و ۳ روز»)؛
    ///   • هیچ تکه‌ای نماند ⇒ «امروز»، و تاریخِ آینده ⇒ «تازه».
    /// </summary>
    public static string Text(string? fromShamsi, string? todayShamsi)
    {
        var fk = Shamsi.Key(fromShamsi);
        var tk = todayShamsi is null || Shamsi.Key(todayShamsi) == 0
            ? Shamsi.Key(Shamsi.Today())
            : Shamsi.Key(todayShamsi);
        if (fk == 0) return "";
        if (fk > tk) return "تازه";

        int ay = fk / 10000, am = fk / 100 % 100, ad = fk % 100;
        int by = tk / 10000, bm = tk / 100 % 100, bd = tk % 100;

        int y = by - ay, m = bm - am, d = bd - ad;
        if (d < 0)
        {
            m -= 1;
            int pm = bm - 1, py = by;
            if (pm < 1) { pm = 12; py -= 1; }
            d += MonthLen(py, pm);
        }
        if (m < 0) { y -= 1; m += 12; }

        var parts = new List<string>(3);
        if (y > 0) parts.Add(Shamsi.Money(y) + " سال");
        if (m > 0) parts.Add(Shamsi.Money(m) + " ماه");
        if (d > 0) parts.Add(Shamsi.Money(d) + " روز");
        if (parts.Count == 0) return "امروز";
        return string.Join(" و ", parts.Take(2));
    }

    /// <summary>درازای یک ماهِ شمسی — ‎_jMonthLen‎ی سایت.</summary>
    private static int MonthLen(int y, int m)
    {
        if (m is < 1 or > 12) return 30;
        try { return Cal.GetDaysInMonth(y, m); }
        catch { return m <= 6 ? 31 : 30; }
    }
}

/// <summary>
/// ══ قرض‌های دسته‌جمعی ═══════════════════════════════════════════════════════
/// رونوشتِ ‎renderDebtSummary(kind)‎ی نسخهٔ وب.
///
/// «فقط قرض‌دارانِ واحد تیل — هر کدام یک خط. با کلیک روی هر خط، حساب کامل همان
/// شخص باز می‌شود. ستونِ آخر می‌گوید چند وقت است قرض‌دار است.» و همتای پولش.
///
/// ⚠️ دو فهرستِ کاملاً جدا هستند «تا پول و تیل هرگز در یک جدول قاطی نشوند» —
/// جملهٔ خودِ سایت. واحدِ حسابِ **اصلی** تعیین می‌کند هر شخص در کدام فهرست است.
///
/// ⚠️ عددها از همان‌جایی می‌آیند که کارت‌ها و «قرض‌های کهنه» می‌آیند
/// (<see cref="AgingService.Figures"/>) — نه یک محاسبهٔ دوم.
/// </summary>
public sealed class DebtSummaryService
{
    private readonly AgingService _aging;
    private readonly MembershipService _member;

    public DebtSummaryService(AgingService aging, MembershipService member)
    { _aging = aging; _member = member; }

    /// <param name="money">‎true‎ ⇒ فهرستِ «واحد پول»، وگرنه «واحد تیل».</param>
    public List<DebtSummaryRow> Rows(IEnumerable<Debtor> people, bool money, string today)
    {
        var list = new List<DebtSummaryRow>();
        foreach (var p in people)
        {
            if (p is null) continue;
            var f = _aging.Figures(p);
            if (f.IsMoney != money) continue;
            list.Add(new DebtSummaryRow(p, f, _member.Row(p, today)));
        }
        return list;
    }
}
