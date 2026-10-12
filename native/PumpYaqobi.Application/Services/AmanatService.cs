using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;

namespace PumpYaqobi.Application.Services;

/// <summary>
/// ضریب‌های «تیل امانت». پیش‌فرض‌ها همان AM_DEFAULTSِ نسخهٔ وب‌اند و منابعشان
/// در همان‌جا نوشته شده — دست‌کاری‌شان عددِ بخار را عوض می‌کند.
/// </summary>
public sealed record AmanatSettings(
    decimal BasePct = 0.02m,   // ٪ بخار در ماه، در دمای مرجع
    decimal RefTemp = 20m,     // دمای مرجع (°C)
    decimal TDouble = 10m,     // هر چند درجه، بخار دو برابر می‌شود
    decimal FPetrol = 1.00m,
    decimal FDiesel = 0.02m,   // دیزل در برابر پطرول ناچیز
    decimal TankFactor = 1.0m,
    decimal HandlingPct = 0m,  // خطای اندازه‌گیری و ریخت‌وپاش — بخار نیست
    decimal ProfitPct = 2m,
    decimal DefTemp = 25m,
    decimal SafetyPct = 25m)
{
    public static readonly AmanatSettings Default = new();
}

/// <summary>خروجیِ حسابِ یک ردیفِ امانت — همان چیزی که ‎amRowCalc‎ برمی‌گرداند.</summary>
public readonly record struct AmanatRowCalc(
    decimal Liters, decimal Taken, decimal Days, decimal Temp, decimal Base, bool Closed,
    decimal Loss, decimal LossPct, decimal Rest,
    bool HasActual, decimal? Actual, decimal? RealLoss, decimal? Diff,
    decimal? MyPct, decimal? TargetL, decimal? NeedPct, decimal? AskPct,
    decimal? NetIfMy, decimal? AskL, decimal? NetIfAsk)
{
    /// <summary>«مدت زمان» دستی نوشته شده؟ (‎c.manual‎)</summary>
    public bool Manual { get; init; }
    /// <summary>روزِ خودکار از تاریخ تا امروز/بسته‌شدن (‎c.autoDays‎) — نشانِ خانهٔ خالیِ «مدت زمان».</summary>
    public decimal AutoDays { get; init; }
    /// <summary>فیصدی از خودِ ردیف است، نه ارث از حساب (‎c.rowOwnPct‎).</summary>
    public bool RowOwnPct { get; init; }
}

public readonly record struct AmanatAccountCalc(
    int Count, int Open, decimal Liters, decimal LitersOpen, decimal Taken,
    decimal Loss, decimal LossPct, decimal Rest, decimal Share,
    bool HasActual, decimal Actual, decimal RealLoss, decimal RealDiff,
    // ── همان چیزهایی که ‎amAccCalc‎ در سطحِ حساب می‌سازد و کارتِ حساب نشانشان می‌دهد
    decimal? MyPct, decimal? TargetL, decimal? NeedPct, decimal? AskPct,
    decimal? AskL, decimal? NetIfMy, decimal? NetIfAsk,
    decimal? Rate, decimal? LossMoney, decimal? MyMoney, decimal? DiffMoney);

/// <summary>
/// ══ تیل امانت ═════════════════════════════════════════════════════════════
/// رونوشتِ ‎amTempFactor‎ · ‎amFuelFactor‎ · ‎amLoss‎ · ‎amThermal‎ ·
/// ‎amRowCalc‎ · ‎amAccCalc‎.
///
/// دو قاعده که در نسخهٔ وب با خونِ دل به دست آمده‌اند و اینجا هم دست‌نخورده‌اند:
///
///   ۱) «فیصدیِ لازم = فیصدیِ هدف + درصدِ بخار». بخار از سهمِ شما کم می‌شود،
///      پس فیصدی باید به همان اندازه بالاتر خواسته شود.
///   ۲) «باقی تیل = رسید − برده‌شده − سهمِ شما» — بخار این‌جا کم نمی‌شود.
///      بخار فقط از سهمِ خودتان خورده می‌شود: «به من می‌رسد = سهم − بخار».
///      یک‌بار بخار در هر دو جا کم شده بود و صاحب ریپو گزارشش کرد.
/// </summary>
public sealed class AmanatService
{
    /// <summary>‎amTempFactor(T, s)‎ — هر ‎tDouble‎ درجه، بخار دو برابر.</summary>
    /// <remarks>
    /// ⛔ (شورا، بندِ ۲ — ۱۴۰۵/۰۷/۱۹) تمامِ راه ‎decimal‎ است، نه عددِ شناور: لیتر و پولِ
    /// بخار از همین می‌آیند و عددِ شناور ته‌ماندهٔ ‎…0000004‎ یا ‎…9999999‎ می‌ساخت که
    /// در جمع‌ها و «باقی تیل» می‌نشست. توانِ کسریِ ۲ با سریِ ‎exp‎ در ‎decimal‎
    /// (۲۸ رقم) حساب می‌شود؛ توانِ درست (۱، ۲، ۴، ½…) دقیقاً همان است.
    /// </remarks>
    public decimal TempFactor(decimal temp, AmanatSettings s)
    {
        var step = s.TDouble > 0m ? s.TDouble : 10m;
        return Pow2((temp - s.RefTemp) / step);
    }

    private const decimal Ln2 = 0.6931471805599453094172321215m;

    /// <summary>۲ به توانِ ‎x‎ در ‎decimal‎ — بخشِ صحیح دقیق، بخشِ کسری با سری.</summary>
    public static decimal Pow2(decimal x)
    {
        var n = decimal.Floor(x);
        var f = x - n;                       // ۰ ≤ f < ۱
        if (n > 90m) return MaxFactor;       //  بخار به‌هرحال به خودِ تیل بریده می‌شود
        if (n < -90m) return 0m;
        var whole = 1m;
        for (var i = 0; i < (int)Math.Abs(n); i++) whole = n > 0 ? whole * 2m : whole / 2m;
        if (f == 0m) return whole;
        //  e^(f·ln2) — جمله‌ها تا کمتر از ۱e-۲۸ (حداکثر ۴۰)
        var y = f * Ln2;
        decimal sum = 1m, term = 1m;
        for (var k = 1; k <= 40; k++)
        {
            term = term * y / k;
            if (term == 0m) break;
            sum += term;
        }
        return whole * sum;
    }

    /// <summary>سقفِ ضریب — هرچه بالاتر، بخار به‌هرحال همان «همهٔ تیل» است.</summary>
    private const decimal MaxFactor = 1e12m;

    public decimal FuelFactor(FuelType fuel, AmanatSettings s) =>
        fuel == FuelType.Diesel ? s.FDiesel : s.FPetrol;

    /// <summary>‎amLoss‎ — بخار هرگز از خودِ تیل بیشتر نمی‌شود.</summary>
    public decimal Loss(decimal liters, decimal days, decimal? temp, FuelType fuel,
                        decimal? basePct, AmanatSettings s)
    {
        var l = Math.Max(0m, liters);
        var d = Math.Max(0m, days);
        var p = basePct ?? s.BasePct;
        var t = temp ?? s.DefTemp;

        decimal loss;
        try
        {
            //  ⚠️ تقسیم یک بار و آخر — ‎d / 30‎ جدا (۰٫۲۳۳۳…) گردکردنِ زودرس می‌ساخت
            loss = l * p * d * TempFactor(t, s) * FuelFactor(fuel, s) * s.TankFactor / 3000m;
        }
        catch (OverflowException) { loss = l; }
        if (loss < 0m) loss = 0m;
        return loss > l ? l : loss;
    }

    /// <summary>ضریب‌های انبساطِ گرمایی — بخار نیست و با سرد شدن برمی‌گردد.</summary>
    private const decimal ThermalPetrol = 0.0012m;
    private const decimal ThermalDiesel = 0.00085m;
    private const decimal ThermalRefTemp = 15.6m;

    /// <summary>‎amThermal‎ — مثبت یعنی گرم‌تر از مرجع و لیترِ بیشتر.</summary>
    public (decimal Liters, decimal Pct, decimal Temp) Thermal(
        decimal liters, decimal? temp, FuelType fuel, AmanatSettings s)
    {
        var l = Math.Max(0m, liters);
        var t = temp ?? s.DefTemp;
        var k = fuel == FuelType.Diesel ? ThermalDiesel : ThermalPetrol;
        return (l * k * (t - ThermalRefTemp), k * (t - ThermalRefTemp) * 100m, t);
    }

    /// <summary>
    /// ‎amRowCalc‎. «مدت زمان» اگر دستی نوشته نشده باشد از تاریخِ ردیف تا
    /// امروز (یا تا تاریخِ بسته‌شدن) شمرده می‌شود.
    /// </summary>
    public AmanatRowCalc RowCalc(AmanatRow row, AmanatAccount acc, AmanatSettings s, decimal autoDays)
    {
        var fuel = acc?.Fuel ?? FuelType.Petrol;
        var closed = row.State == AmanatRowState.Closed;
        var days = Math.Max(0m, row.Days ?? autoDays);

        var liters = Math.Max(0m, row.Liters ?? 0m);
        var taken = Math.Max(0m, row.Taken ?? 0m);
        var temp = row.Temp ?? s.DefTemp;
        var basePct = row.BasePct ?? s.BasePct;

        // بخار روی «رسیدِ تیل» حساب می‌شود، نه روی باقی‌مانده
        var loss = Loss(liters, days, temp, fuel, basePct, s);
        var lossPct = liters > 0m ? loss / liters * 100m : 0m;

        var hasActual = row.Actual.HasValue;
        decimal? actual = hasActual ? row.Actual!.Value : null;
        decimal? realLoss = hasActual ? liters - taken - actual!.Value : null;
        decimal? diff = hasActual ? realLoss!.Value - loss : null;

        //  ‎myRaw‎ی سایت: فیصدیِ خودِ ردیف، وگرنه فیصدیِ حساب (در جدول با «↑»)
        decimal? myPct = row.MyPct ?? acc?.MyPct;
        decimal? targetL = myPct is null ? null : liters * myPct.Value / 100m;
        decimal? needPct = myPct is null ? null : myPct.Value + lossPct + s.HandlingPct;
        decimal? askPct = needPct is null ? null : Math.Ceiling(needPct.Value * 10m) / 10m;
        decimal? netIfMy = targetL is null ? null : targetL.Value - loss;
        decimal? askL = askPct is null ? null : liters * askPct.Value / 100m;
        decimal? netIfAsk = askL is null ? null : askL.Value - loss;

        // ⚠️ بخار این‌جا کم نمی‌شود — فقط سهمِ شما
        var rest = liters - taken - (targetL ?? 0m);

        return new AmanatRowCalc(liters, taken, days, temp, basePct, closed,
            loss, lossPct, rest, hasActual, actual, realLoss, diff,
            myPct, targetL, needPct, askPct, netIfMy, askL, netIfAsk)
        {
            Manual = row.Days.HasValue,
            AutoDays = autoDays,
            RowOwnPct = row.MyPct.HasValue,
        };
    }

    /// <summary>شمارِ خودکارِ روز — از تاریخِ ردیف تا امروز، یا تا روزِ بسته‌شدن.</summary>
    public static decimal AutoDays(DateTime? start, DateTime? close, DateTime today)
    {
        if (start is null) return 0m;
        var end = close ?? today;
        if (end < start.Value) end = start.Value;
        return (decimal)Math.Round((end - start.Value).TotalDays);
    }

    /// <summary>‎amAccCalc‎ — جمعِ سربرگِ کادرِ حساب.</summary>
    public AmanatAccountCalc AccountCalc(AmanatAccount acc, AmanatSettings s,
                                         Func<AmanatRow, decimal> autoDaysOf)
    {
        int n = 0, open = 0;
        decimal liters = 0, litersOpen = 0, taken = 0, loss = 0, rest = 0, share = 0;
        bool hasActual = false;
        decimal actual = 0, realLoss = 0, realDiff = 0;

        foreach (var r in acc.Rows)
        {
            var c = RowCalc(r, acc, s, autoDaysOf(r));
            n++;
            if (!c.Closed) { open++; litersOpen += c.Liters; }
            liters += c.Liters; taken += c.Taken; loss += c.Loss; rest += c.Rest;
            share += c.TargetL ?? 0m;
            if (c.HasActual)
            {
                hasActual = true;
                actual += c.Actual!.Value; realLoss += c.RealLoss!.Value; realDiff += c.Diff!.Value;
            }
        }

        var lossPct = liters > 0m ? loss / liters * 100m : 0m;

        // ⚠️ در سطحِ حساب، «فیصدی» از خودِ حساب خوانده می‌شود و «فیصدیِ لازم»
        // روی درصدِ بخارِ کلِ حساب حساب می‌شود — نه جمعِ ردیف‌به‌ردیف.
        var myPct = acc.MyPct;
        decimal? targetL = myPct is null ? null : liters * myPct.Value / 100m;
        decimal? needPct = myPct is null ? null : myPct.Value + lossPct + s.HandlingPct;
        decimal? askPct = needPct is null ? null : Math.Ceiling(needPct.Value * 10m) / 10m;
        decimal? askL = askPct is null ? null : liters * askPct.Value / 100m;
        decimal? netIfMy = targetL is null ? null : targetL.Value - loss;
        decimal? netIfAsk = askL is null ? null : askL.Value - loss;

        // نرخ فقط برای «به پول گفتن» است و روی هیچ حسابِ لیتری اثر ندارد
        var rate = acc.Rate;
        decimal? lossMoney = rate is null ? null : loss * rate.Value;
        decimal? myMoney = rate is null || targetL is null ? null : targetL.Value * rate.Value;
        decimal? diffMoney = rate is null || netIfMy is null ? null : netIfMy.Value * rate.Value;

        return new AmanatAccountCalc(n, open, liters, litersOpen, taken, loss, lossPct,
            rest, share, hasActual, actual, realLoss, realDiff,
            myPct, targetL, needPct, askPct, askL, netIfMy, netIfAsk,
            rate, lossMoney, myMoney, diffMoney);
    }

    // ══ چیزهایی که سایت در «خلاصه و ابزارها» و «برآوردِ دستی» حساب می‌کند ══════

    /// <summary>‎Math.ceil(x * 10) / 10‎ — گِرد به یک‌دهمِ بالا.</summary>
    public static decimal CeilTenth(decimal x) => Math.Ceiling(x * 10m) / 10m;

    /// <summary>
    /// ‎amSuggest‎ — سربه‌سر = بخار٪ + اندازه‌گیری٪؛ پیشنهاد = گِردِ بالای (سربه‌سر +
    /// هدف). هدف = ‎wantPct‎ (فیصدیِ من) اگر هست، وگرنه «سودِ هدف»ِ تنظیمات.
    /// </summary>
    public AmanatSuggest Suggest(decimal liters, decimal days, decimal? temp, FuelType fuel,
                                 decimal? basePct, AmanatSettings s, decimal? wantPct)
    {
        var l = Math.Max(0m, liters);
        var loss = Loss(l, days, temp, fuel, basePct, s);
        var lossPct = l > 0m ? loss / l * 100m : 0m;
        var even = lossPct + s.HandlingPct;
        var want = wantPct ?? s.ProfitPct;
        var pct = CeilTenth(even + Math.Max(0m, want));
        return new AmanatSuggest(l, loss, lossPct, s.HandlingPct, even, want, pct);
    }

    /// <summary>‎amFeeAdvice‎ — ماشین‌حسابِ فیصدیِ صفحهٔ «خلاصه و ابزارها».</summary>
    public AmanatFeeAdvice FeeAdvice(decimal liters, decimal days, decimal? temp, FuelType fuel,
                                     decimal? myPct, AmanatSettings s)
    {
        var l = Math.Max(0m, liters);
        var loss = Loss(l, days, temp, fuel, null, s);
        var need = l > 0m ? loss / l * 100m : 0m;
        var safe = need * (1m + s.SafetyPct / 100m);
        decimal? myL = myPct is null ? null : l * myPct.Value / 100m;
        decimal? diff = myL is null ? null : myL.Value - loss;
        return new AmanatFeeAdvice(l, loss, need, safe, myPct, myL, diff);
    }

    /// <summary>مدت‌های جدولِ راهنمای ماشین‌حساب — ‎AM_GUIDE_DAYS‎.</summary>
    public static readonly int[] GuideDays = { 7, 15, 30, 45, 60, 90, 120, 180 };

    /// <summary>نزدیک‌ترین مدتِ جدولِ راهنما به روزِ نوشته‌شده (ردیفِ نشان‌دار).</summary>
    public static int NearestGuideDay(decimal days) =>
        GuideDays.Aggregate(GuideDays[0], (p, c) => Math.Abs(c - days) < Math.Abs(p - days) ? c : p);

    /// <summary>
    /// ‎_amWhatIfPaint‎ — «🧾 برآوردِ دستی». هیچ چیزی از جدول نمی‌خواند و در آن نمی‌نویسد.
    /// </summary>
    public AmanatWhatIf WhatIf(decimal liters, decimal days, decimal? temp, FuelType fuel,
                               decimal? myPct, decimal? rate, AmanatSettings s)
    {
        var l = Math.Max(0m, liters);
        var d = Math.Max(0m, days);
        var t = temp ?? s.DefTemp;
        var loss = Loss(l, d, t, fuel, null, s);
        var lossPct = l > 0m ? loss / l * 100m : 0m;
        decimal? share = myPct is null ? null : l * myPct.Value / 100m;
        decimal? net = share is null ? null : share.Value - loss;
        var rest = l - (share ?? 0m);
        decimal? need = myPct is null ? null : myPct.Value + lossPct + s.HandlingPct;
        decimal? ask = need is null ? null : CeilTenth(need.Value);
        decimal? askL = ask is null ? null : l * ask.Value / 100m;
        decimal? netIfAsk = askL is null ? null : askL.Value - loss;
        var sug = Suggest(l, d, t, fuel, null, s, myPct);

        // «اگر رویِ‌هم این‌قدر بماند» — روزِ کل، نه «+چند روز»
        var future = new List<AmanatWhatIfDay>();
        foreach (var add in new[] { 0, 30, 60, 90, 180 })
        {
            var dd = d + add;
            var lo = Loss(l, dd, t, fuel, null, s);
            var lp = l > 0m ? lo / l * 100m : 0m;
            var ev = lp + s.HandlingPct;
            var sg = CeilTenth(ev + Math.Max(0m, myPct ?? s.ProfitPct));
            decimal? n2 = myPct is null ? null : l * myPct.Value / 100m - lo;
            future.Add(new AmanatWhatIfDay(dd, add == 0, lo, ev, sg, n2));
        }
        return new AmanatWhatIf(l, d, t, loss, lossPct, myPct, share, net, rest, need, ask,
                                askL, netIfAsk, rate, sug, future);
    }

    /// <summary>
    /// ‎amAccounts()‎: ردیفِ بسته‌ای که فیصدیِ خودش را ننوشته، فیصدیِ فعلیِ حساب را
    /// می‌گیرد — هیچ عددی امروز عوض نمی‌شود، فقط قفل می‌شود تا با عوض شدنِ فیصدیِ
    /// حساب عقب‌گرد نکند. ردیف‌های عوض‌شده را برمی‌گرداند (برای ذخیره).
    /// </summary>
    public static List<AmanatRow> LockClosedRowPcts(AmanatAccount acc)
    {
        var changed = new List<AmanatRow>();
        if (acc.MyPct is not { } my) return changed;
        foreach (var r in acc.Rows)
            if (r.State == AmanatRowState.Closed && r.MyPct is null) { r.MyPct = my; changed.Add(r); }
        return changed;
    }

    /// <summary>
    /// ‎_amSortedRows‎ — به ترتیبِ تاریخ؛ ردیفِ بی‌تاریخ آخر، هم‌تاریخ‌ها به ترتیبِ
    /// خودشان (مرتب‌سازیِ پایدار).
    /// </summary>
    public static List<AmanatRow> SortedRows(IEnumerable<AmanatRow> rows) =>
        rows.Select((r, i) => (r, i))
            .OrderBy(x => x.r.DateKey > 0 ? x.r.DateKey : int.MaxValue)
            .ThenBy(x => x.i)
            .Select(x => x.r).ToList();

    /// <summary>
    /// ‎_amAccMatch‎ — فیلترِ سوخت و جست‌وجو. جست‌وجو نامِ حساب، یادداشتش و نام و
    /// «به حسابهٔ»ِ همهٔ ردیف‌ها را می‌گردد (‎normFa‎).
    /// </summary>
    public static bool Matches(AmanatAccount a, string fuelFilter, string? search)
    {
        if (fuelFilter == "petrol" && a.Fuel != FuelType.Petrol) return false;
        if (fuelFilter == "diesel" && a.Fuel != FuelType.Diesel) return false;
        var q = PostingService.NormFa(search);
        if (q.Length == 0) return true;
        var parts = new List<string> { a.Name ?? "", a.Note ?? "" };
        parts.AddRange(a.Rows.Select(r => (r.Name ?? "") + " " + (r.ToAccount ?? "")));
        return PostingService.NormFa(string.Join(" ", parts)).Contains(q, StringComparison.Ordinal);
    }

    /// <summary>‎amAccLabel‎ — «نام (پطرول)».</summary>
    public static string AccLabel(AmanatAccount a) =>
        (string.IsNullOrWhiteSpace(a.Name) ? "حسابِ بی‌نام" : a.Name!.Trim())
        + " (" + (a.Fuel == FuelType.Diesel ? "دیزل" : "پطرول") + ")";

    /// <summary>
    /// ‎_amPaint‎ — کادرهای خلاصهٔ پطرول/دیزل (همهٔ حساب‌ها، بی فیلتر) و هشدارها
    /// (فقط حساب‌هایی که با فیلتر جورند).
    /// </summary>
    public AmanatOverview Overview(IReadOnlyList<AmanatAccount> accounts, AmanatSettings s,
                                   Func<AmanatAccount, AmanatRow, decimal> autoDaysOf,
                                   Func<AmanatAccount, bool> matches)
    {
        var petrol = new AmanatFuelTotals();
        var diesel = new AmanatFuelTotals();
        var errors = new List<string>();
        var warns = new List<string>();
        foreach (var a in accounts)
        {
            var t = AccountCalc(a, s, r => autoDaysOf(a, r));
            var p = a.Fuel == FuelType.Diesel ? diesel : petrol;
            p.Count++; p.Liters += t.Liters; p.Taken += t.Taken; p.Loss += t.Loss;
            p.Rest += t.Rest; p.Share += t.Share;
            if (!matches(a)) continue;
            var label = AccLabel(a);
            var ri = 0;
            foreach (var r in a.Rows.OrderBy(x => x.SortIndex).ThenBy(x => x.Id))
            {
                ri++;
                var c = RowCalc(r, a, s, autoDaysOf(a, r));
                var at = label + " — ردیفِ " + ri + ": ";
                if ((r.Liters ?? 0m) < 0m) errors.Add(at + "رسید تیل نمی‌تواند کمتر از صفر باشد.");
                if (c.Taken > c.Liters + 0.0001m) errors.Add(at + "برده شده از رسید تیل بیشتر است.");
                if (c.Liters > 0m && r.DateKey <= 0 && !c.Manual)
                    warns.Add(at + "تاریخ ثبت نشده، پس مدت زمان خودکار شمرده نمی‌شود.");
                if (r.Temp is { } tt && (tt < -50m || tt > 80m)) warns.Add(at + "درجه گرمای واردشده غیرعادی است.");
                if (c.LossPct > 1m)
                    warns.Add(at + "تبخیرِ " + Fmt(c.LossPct, 2)
                        + "٪ خیلی بالاتر از اندازه‌گیری‌های منتشرشده است — «پایه»، «مدت زمان» یا «درجه گرما» را یک بار ببینید.");
            }
        }
        return new AmanatOverview(petrol, diesel, errors, warns);
    }

    /// <summary>
    /// ‎_amFmt(n, dec)‎ — ‎toLocaleString('en-US')‎: جداکنندهٔ هزارگان، تا ‎dec‎ رقمِ اعشار،
    /// بی صفرِ انتهایی؛ گردِ نیمه رو به دورتر از صفر.
    /// </summary>
    public static string Fmt(decimal v, int dec = 2)
    {
        var r = Math.Round(v, dec, MidpointRounding.AwayFromZero);
        if (r == 0m) r = 0m;   // «-0» نشود
        return r.ToString(dec <= 0 ? "#,##0" : "#,##0." + new string('#', dec),
                          System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>‎_amFmt2‎ — همیشه دقیقاً دو رقمِ اعشار.</summary>
    public static string Fmt2(decimal v)
    {
        var r = Math.Round(v, 2, MidpointRounding.AwayFromZero);
        if (r == 0m) r = 0m;
        return r.ToString("#,##0.00", System.Globalization.CultureInfo.InvariantCulture);
    }
}

/// <summary>خروجیِ ‎amSuggest‎.</summary>
public readonly record struct AmanatSuggest(
    decimal Liters, decimal Loss, decimal LossPct, decimal Handling, decimal EvenPct,
    decimal Want, decimal Pct)
{
    /// <summary>‎netAt(p)‎ — با فیصدیِ ‎p‎، بعد از بخار چقدر می‌ماند.</summary>
    public decimal NetAt(decimal p) => Liters * p / 100m - Loss;
}

/// <summary>خروجیِ ‎amFeeAdvice‎.</summary>
public readonly record struct AmanatFeeAdvice(
    decimal Liters, decimal Loss, decimal NeedPct, decimal SafePct,
    decimal? MyPct, decimal? MyLiters, decimal? Diff);

/// <summary>یک ردیفِ جدولِ «اگر رویِ‌هم این‌قدر بماند».</summary>
public readonly record struct AmanatWhatIfDay(
    decimal Days, bool IsNow, decimal Loss, decimal EvenPct, decimal SuggestPct, decimal? Net);

/// <summary>خروجیِ «🧾 برآوردِ دستی».</summary>
public sealed record AmanatWhatIf(
    decimal Liters, decimal Days, decimal Temp, decimal Loss, decimal LossPct,
    decimal? MyPct, decimal? Share, decimal? Net, decimal Rest,
    decimal? NeedPct, decimal? AskPct, decimal? AskL, decimal? NetIfAsk,
    decimal? Rate, AmanatSuggest Suggest, IReadOnlyList<AmanatWhatIfDay> Future);

/// <summary>جمعِ یک سوخت در کادرِ خلاصه.</summary>
public sealed class AmanatFuelTotals
{
    public int Count { get; set; }
    public decimal Liters { get; set; }
    public decimal Taken { get; set; }
    public decimal Loss { get; set; }
    public decimal Rest { get; set; }
    public decimal Share { get; set; }
    public decimal LossPct => Liters > 0m ? Loss / Liters * 100m : 0m;
}

/// <summary>کادرهای خلاصه و هشدارهای صفحهٔ «خلاصه و ابزارها».</summary>
public sealed record AmanatOverview(
    AmanatFuelTotals Petrol, AmanatFuelTotals Diesel,
    IReadOnlyList<string> Errors, IReadOnlyList<string> Warnings);
