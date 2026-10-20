using PumpYaqobi.Application.Localization;
using PumpYaqobi.Domain.Entities;

namespace PumpYaqobi.Application.Services;

/// <summary>
/// ══ ⛽ تیل چکنهٔ حواله — حسابِ برداشت و مانده (۱۴۰۵/۰۷/۲۵) ═══════════════════════
/// سقفِ هر حواله در <see cref="HawalaQuota"/> است؛ برداشت‌ها همان ردیف‌های همان حساب‌اند
/// که ستونِ «حواله»شان همین شماره را دارد — چه دستی در حساب نوشته شده باشند، چه از ورق
/// آمده باشند (پستِ ورق شمارهٔ حواله را از نام بیرون می‌کشد و در ستونِ حواله می‌نشاند).
/// پس یک ردیف هرگز دو بار کم نمی‌شود و ویرایش/حذفِ ردیف همان لحظه مانده را درست می‌کند.
///
/// ⛔ هشدار (نقطهٔ سرخ): از ۷۰٪ برداشت به بعد، و وقتی تمام شد.
/// ⛔ (۱۴۰۵/۰۷/۲۵، بارِ دوم) «اگر تیلِ قرض‌دار تمام شد، حواله هم بگوید تمام — آن تیل
/// می‌سوزد.» حسابِ خودِ قرض‌دار سقفِ حواله است: اگر دفترِ همان تیل (یا دفترِ پول) در
/// حساب «تمام» است (<see cref="DebtCalculationService.Status"/>)، حواله هر چه هم مانده
/// باشد «سوخت» است (<see cref="State.Burned"/>) و مانده‌اش دیگر قابلِ برداشت نیست.
/// </summary>
public static class HawalaCalc
{
    /// <summary>از این سهم به بعد نقطهٔ سرخ — «۷۰ درصدِ تیل رفت».</summary>
    public const decimal WarnShare = 0.70m;

    public enum State { Ok, Warn, Done, Burned }

    public readonly record struct Take(string Date, string Name, decimal Liters, int DateKey,
                                       Domain.Enums.FuelType Fuel = Domain.Enums.FuelType.Petrol);

    public readonly record struct Status(decimal Total, decimal Used, decimal Left, decimal Share, State State)
    {
        public bool Alert => State != State.Ok;
    }

    /// <summary>«۴۲»، «42 »، «حواله 42»، «‏۴۲» ⇒ «42».</summary>
    public static string Norm(string? s)
    {
        //  ⛔ نویسه‌های نامرئی (‎RLM/LRM/ZWNJ‎، هر ‎Format‎) بیرون — همان ‎Shamsi.TryNum‎:
        //  صفحه‌کلیدِ فارسیِ ویندوز پیش از عدد ‎RLM‎ می‌گذارد و «‏۴۲» با حوالهٔ ۴۲ نمی‌خورد (۱۴۰۵/۰۷/۲۸).
        var raw = Shamsi.ToEnDigits(s ?? "");
        var b = new System.Text.StringBuilder(raw.Length);
        foreach (var c in raw)
            if (char.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.Format) b.Append(c);
        var t = b.ToString().Replace("حواله", " ").Trim();
        return System.Text.RegularExpressions.Regex.Replace(t, @"\s+", " ");
    }

    /// <summary>
    /// ستونِ حوالهٔ ردیف همین شماره است؟ — برابرِ کامل، یا یکی از تکه‌هایش («42 · 1234»
    /// پس از یکی شدنِ حواله و پلاک).
    /// </summary>
    public static bool Matches(string? rowHawala, string number)
    {
        var n = Norm(number);
        if (n.Length == 0) return false;
        var r = Norm(rowHawala);
        if (r.Length == 0) return false;
        if (string.Equals(r, n, StringComparison.OrdinalIgnoreCase)) return true;
        return r.Split(new[] { ' ', '·', ',', '،', '|' }, StringSplitOptions.RemoveEmptyEntries)
                .Any(p => string.Equals(p, n, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>برداشت‌های یک حواله از ردیف‌های حساب، به ترتیبِ تاریخ.</summary>
    public static List<Take> Takings(string number, IEnumerable<DebtRow> rows) =>
        rows.Where(r => r.DeletedAt is null && r.Liters > 0m && Matches(r.Hawala, number))
            .Select(r => new Take(r.DateShamsi ?? "", r.Name ?? "", r.Liters, r.DateKey, r.Fuel))
            .OrderBy(t => t.DateKey).ToList();

    public static Status Of(decimal total, decimal used)
    {
        var left = total - used;
        var share = total > 0m ? used / total : (used > 0m ? 1m : 0m);
        var st = total > 0m && left <= 0m ? State.Done
               : share >= WarnShare ? State.Warn
               : State.Ok;
        return new Status(total, used, left, share, st);
    }

    public static Status Of(HawalaQuota q, IEnumerable<DebtRow> rows, DebtStatusInfo? account = null)
    {
        var takes = Takings(q.Number, rows);
        var s = Of(q.Liters, takes.Sum(t => t.Liters));
        return account is { } a && AccountOut(a, takes) ? Burn(s) : s;
    }

    /// <summary>
    /// حسابِ قرض‌دار برای همین حواله تیل ندارد؟ تیلِ حواله از برداشت‌هایش؛ بی برداشت،
    /// فقط وقتی <b>هیچ</b> دفترِ تیلِ در کار تیل ندارد. دفترِ پولِ تمام‌شده هم یعنی تیلی
    /// نمانده — ⚠️ فقط در حسابِ پولی؛ حالِ حساب را با <see cref="ForMode"/> بدهید.
    /// </summary>
    public static bool AccountOut(DebtStatusInfo a, IReadOnlyCollection<Take> takes)
    {
        if (a.Money == DebtStatus.Out) return true;
        var fuels = takes.Select(t => t.Fuel).Distinct().ToList();
        if (fuels.Count == 0)
        {
            //  ⛔ (۱۴۰۵/۰۷/۲۸) «تیل دارد اما می‌گوید ندارد»: پطرولِ تمام‌شده حوالهٔ کسی را که
            //  دیزل دارد نمی‌سوزاند — بی برداشت معلوم نیست حواله کدام تیل است.
            var inUse = new[] { a.Petrol, a.Diesel }.Where(s => s != DebtStatus.None).ToList();
            return inUse.Count > 0 && inUse.All(s => s == DebtStatus.Out);
        }
        return fuels.Any(f => (f == Domain.Enums.FuelType.Diesel ? a.Diesel : a.Petrol) == DebtStatus.Out);
    }

    /// <summary>
    /// ══ «تیل دارد، هم حواله هم طرف، اما می‌گوید تیل ندارد» (۱۴۰۵/۰۷/۲۸) ═════════════
    /// فقط دفترِ <b>خودِ</b> حساب سقفِ حواله است: حسابِ تیلی (رسیدِ تیل، برداشت با فی)
    /// رسیدِ پول ندارد، پس دفترِ پولش همیشه «تمام» است و همان حواله را «سوخت» می‌خواند.
    /// تیلی ⇒ دفترِ پول کنار؛ پولی ⇒ دو دفترِ تیل کنار (رسیدِ تیل ندارد، همیشه «تمام»).
    /// </summary>
    public static DebtStatusInfo ForMode(DebtStatusInfo st, Domain.Enums.LedgerMode mode) =>
        Domain.Enums.LedgerModeExtensions.IsMoney(mode)
            ? st with { Petrol = DebtStatus.None, Diesel = DebtStatus.None }
            : st with { Money = DebtStatus.None };

    /// <summary>حواله‌ای که هنوز مانده داشت «سوخت»؛ تمام‌شده همان تمام‌شده می‌ماند.</summary>
    public static Status Burn(Status s) =>
        s.State == State.Done ? s : s with { State = State.Burned };
}
