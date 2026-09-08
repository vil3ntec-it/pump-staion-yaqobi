using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;

namespace PumpYaqobi.Application.Services;

/// <summary>جمع‌های یک نوع سوخت — معادلِ بسته‌های ‎_splitTotals‎ در HTML.</summary>
public readonly record struct FuelTotals(
    decimal Liters, decimal Rasid, decimal RasidFuel, decimal Albaqi, decimal Bardagi)
{
    public static FuelTotals operator +(FuelTotals a, FuelTotals b) => new(
        a.Liters + b.Liters, a.Rasid + b.Rasid, a.RasidFuel + b.RasidFuel,
        a.Albaqi + b.Albaqi, a.Bardagi + b.Bardagi);
}

/// <summary>سه بستهٔ ‎_splitTotals‎: پطرول، دیزل و جمعِ هر دو.</summary>
public readonly record struct SplitTotals(FuelTotals Petrol, FuelTotals Diesel)
{
    public FuelTotals All => Petrol + Diesel;
}

/// <summary>الباقیِ یک شخص/حساب — معادلِ ‎_debtBalancesFrom‎.</summary>
public readonly record struct DebtBalances(decimal Money, decimal Petrol, decimal Diesel, decimal Fuel);

/// <summary>حالِ یک دفتر روی کارتِ قرض‌دار.</summary>
public enum DebtStatus { None = 0, Ok = 1, Low = 2, Out = 3 }

public readonly record struct DebtStatusInfo(DebtStatus Worst, DebtStatus Petrol, DebtStatus Diesel, DebtStatus Money);

/// <summary>
/// ══ منطقِ مالیِ قرض‌داران ══════════════════════════════════════════════════
/// بندِ ۷ خواستهٔ صاحب ریپو: «هر محاسبه را از HTML استخراج و به یک Service
/// مستقل منتقل کن. UI نباید خودش محاسبات مالی را انجام دهد.»
///
/// هر متد این‌جا رونوشتِ مو‌به‌موی همان تابعِ نسخهٔ HTML است و نامِ اصلی‌اش در
/// کامنت آمده تا هر وقت خواستید بتوانید دو طرف را کنارِ هم بگذارید.
/// ⛔ هیچ فرمولی «بهتر» نشده — بندِ ۷ می‌گوید منطق بدونِ دلیل عوض نشود.
/// </summary>
public sealed class DebtCalculationService
{
    private readonly IUnionRateProvider _rates;

    public DebtCalculationService(IUnionRateProvider rates) => _rates = rates;

    // ── فیصدی ────────────────────────────────────────────────────────────────
    /// <summary>
    /// ‎_acctPct(a, fuel)‎ — فیصدیِ همان سوخت.
    /// اگر برای این سوخت چیزی ثبت نشده باشد، به فیصدیِ مشترکِ قدیمی برمی‌گردد؛
    /// دقیقاً همان رفتاری که حساب‌های قدیمی را دست‌نخورده نگه می‌داشت.
    /// </summary>
    public decimal PercentOf(DebtAccount a, FuelType fuel)
    {
        if (a is null) return 0m;
        var v = fuel == FuelType.Diesel ? a.PercentDiesel : a.PercentPetrol;
        if (v.HasValue) return v.Value;
        return a.PercentLegacy ?? 0m;
    }

    /// <summary>
    /// ‎_setAcctPct(a, fuel, val)‎ — نوشتنِ فیصدی.
    ///
    /// ⚠️ اول ‎_pctSplit‎ اجرا می‌شود: اولین باری که دست به فیصدی می‌خورد، هر دو
    /// کادر صریح نوشته می‌شوند تا فیصدیِ مشترکِ قدیمی دوباره زنده نشود.
    ///
    /// ⚠️ **خالی کردنِ کادر یعنی صفر، نه «ثبت نشده».** در نسخهٔ وب کادرِ خالی
    /// رشتهٔ ‎''‎ ذخیره می‌شود و ‎_acctPct‎ آن را ‎parseFloat('')||0‎ یعنی صفر
    /// می‌خوانَد — ولی ‎undefined‎ را به فیصدیِ مشترکِ قدیمی برمی‌گردانَد. این
    /// دو حالتِ جدا هستند. اگر خالی را هم ‎null‎ ذخیره کنیم، پاک کردنِ فیصدیِ
    /// پطرول آن را به فیصدیِ قدیمی برمی‌گرداند — یعنی عددی که کاربر همین حالا
    /// پاکش کرده، دوباره زنده می‌شود.
    ///
    /// پس ‎null‎ فقط معنیِ «هرگز دست نخورده» را دارد و خالی به‌صورتِ ‎0‎ ذخیره
    /// می‌شود. برای خواندن هم یکی است، چون ‎parseFloat('')||0 == 0‎.
    /// </summary>
    public void SetPercent(DebtAccount a, FuelType? fuel, decimal? value)
    {
        if (a is null) return;
        SplitLegacyPercent(a);
        var v = value ?? 0m;
        if (fuel is null) { a.PercentPetrol = v; a.PercentDiesel = v; }   // میان‌بُرِ «هر دو»
        else if (fuel == FuelType.Diesel) a.PercentDiesel = v;
        else a.PercentPetrol = v;
        // فیصدیِ تک‌خانهٔ قدیمی هم‌گام می‌ماند (همان خطِ آخرِ _setAcctPct)
        var p = a.PercentPetrol ?? 0m;
        var d = a.PercentDiesel ?? 0m;
        a.PercentLegacy = p != 0m ? p : d;
    }

    /// <summary>‎_pctSplit(a)‎</summary>
    public void SplitLegacyPercent(DebtAccount a)
    {
        if (a is null) return;
        var b = a.PercentLegacy ?? 0m;
        a.PercentPetrol ??= b;
        a.PercentDiesel ??= b;
    }

    // ── بردگیِ یک ردیف ───────────────────────────────────────────────────────
    /// <summary>
    /// ‎_personRowBardagi(r)‎ = ‎fuel > 0 ? fuel * _personRowFee(r) : 0‎
    /// و ‎_personRowFee‎ همان ‎_rowUnionRate‎ است: فیِ دستیِ ردیف، و اگر نبود صفر.
    /// ⚠️ «نرخِ خودکار» عمداً حذف شده بود — اگر فی خالی باشد بردگی صفر می‌ماند.
    /// ردیفِ دفترِ پول بردگی‌اش دستی است و از این فرمول نمی‌آید.
    /// </summary>
    public decimal RowBardagi(DebtRow r)
    {
        if (r is null) return 0m;
        if (r.ByMoney) return r.Bardagi;
        if (r.Liters <= 0m) return 0m;
        return r.Liters * (r.PricePerLiter ?? 0m);
    }

    /// <summary>
    /// ‎_round0(x)‎ = ‎parseFloat(x.toFixed(0))‎.
    /// ⚠️ با Math.Round(…, MidpointRounding.ToEven) عوض نکنید: در جاوااسکریپت
    /// ‎(-2.5).toFixed(0)‎ برابر «-3» است، پس نصفه‌ها از صفر دور می‌شوند.
    /// یک‌بار همین یک نکته عددِ حساب را عوض کرده بود.
    /// </summary>
    public static decimal Round0(decimal v) => Math.Round(v, 0, MidpointRounding.AwayFromZero);

    /// <summary>
    /// ‎خوددرمانیِ ردیف‎ — همان کاری که ‎renderPersonRows‎ پیش از کشیدنِ جدول
    /// روی هر ردیف می‌کند:
    ///   ۱) ردیفی که «پولی» علامت خورده ولی بردگی‌اش صفر و لیتر دارد، در اصل
    ///      ردیفِ تیل است (وگرنه فیِ حساب خراب می‌شد).
    ///   ۲) بردگی و الباقی محاسبه و گرد می‌شوند و روی خودِ ردیف می‌نشینند.
    /// خروجی می‌گوید آیا چیزی عوض شد (تا فقط همان‌وقت ذخیره شود).
    /// </summary>
    public bool NormalizeRow(DebtRow r)
    {
        if (r is null) return false;
        var healed = false;

        if (r.ByMoney && r.Bardagi == 0m && r.Liters > 0m) { r.ByMoney = false; healed = true; }

        var bardagi = Round0(r.ByMoney ? r.Bardagi : RowBardagi(r));
        var albaqi = Round0(bardagi - r.Rasid);
        if (r.Bardagi != bardagi) { r.Bardagi = bardagi; healed = true; }
        if (r.Albaqi != albaqi) { r.Albaqi = albaqi; healed = true; }
        return healed;
    }

    /// <summary>همان کار روی همهٔ ردیف‌های یک حساب (هر دو دفتر).</summary>
    public bool NormalizeAccount(DebtAccount a)
    {
        if (a is null) return false;
        var healed = false;
        foreach (var r in a.FuelRows) healed |= NormalizeRow(r);
        foreach (var r in a.MoneyRows) healed |= NormalizeRow(r);
        return healed;
    }

    // ── جمع‌ها ───────────────────────────────────────────────────────────────
    /// <summary>‎_splitTotals(rows)‎ — پطرول و دیزل جدا، بعد جمعِ کل.</summary>
    public SplitTotals SplitTotals(IEnumerable<DebtRow> rows)
    {
        decimal pf = 0, pr = 0, prf = 0, pa = 0, pb = 0;
        decimal df = 0, dr = 0, drf = 0, da = 0, db = 0;
        foreach (var r in rows)
        {
            if (r is null) continue;
            if (r.Fuel == FuelType.Diesel)
            { df += r.Liters; dr += r.Rasid; drf += r.RasidFuel; da += r.Albaqi; db += r.Bardagi; }
            else
            { pf += r.Liters; pr += r.Rasid; prf += r.RasidFuel; pa += r.Albaqi; pb += r.Bardagi; }
        }
        return new SplitTotals(new FuelTotals(pf, pr, prf, pa, pb), new FuelTotals(df, dr, drf, da, db));
    }

    /// <summary>
    /// ‎_debtBalancesFrom(t, list)‎ — الباقیِ پول و لیترِ هر سوخت.
    /// ⚠️ لیترِ کل از ‎t.all.fuel‎ می‌آید نه ‎t.fuel‎ — یک‌بار همین اشتباه شد و چون
    /// ‎undefined − عدد = NaN‎ و ‎NaN > 0‎ هم false است، همهٔ کارت‌ها «تسویه» نشان
    /// می‌دادند. این‌جا تایپ جلویش را می‌گیرد، ولی قاعده همان است.
    /// </summary>
    public DebtBalances Balances(IEnumerable<DebtAccount> accounts)
    {
        var list = accounts.Where(a => a is not null).ToList();
        var t = SumTotals(list);
        decimal rP = 0, rD = 0;
        foreach (var a in list) { rP += a.RasidFuelPetrol; rD += a.RasidFuelDiesel; }
        static decimal R2(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);
        return new DebtBalances(
            Money:  R2(t.All.Albaqi),
            Petrol: R2(t.Petrol.Liters - rP),
            Diesel: R2(t.Diesel.Liters - rD),
            Fuel:   R2(t.All.Liters - rP - rD));
    }

    /// <summary>
    /// ‎_addAcctTotals(t, a)‎ — جمع‌های یک حساب، هر سوخت جدا.
    /// ⚠️ ردیف‌های دفترِ پول عمداً «لیتر» و «رسیدِ تیل» را زیاد نمی‌کنند؛ فقط
    /// rasid / albaqi / bardagi را. دو دفترِ جدا، همان قاعدهٔ همیشگی.
    /// </summary>
    public SplitTotals AccountTotals(DebtAccount a)
    {
        decimal pf = 0, pr = 0, prf = 0, pa = 0, pb = 0;
        decimal df = 0, dr = 0, drf = 0, da = 0, db = 0;
        foreach (var r in a.FuelRows)
        {
            if (r is null) continue;
            if (r.Fuel == FuelType.Diesel)
            { df += r.Liters; drf += r.RasidFuel; dr += r.Rasid; da += r.Albaqi; db += r.Bardagi; }
            else
            { pf += r.Liters; prf += r.RasidFuel; pr += r.Rasid; pa += r.Albaqi; pb += r.Bardagi; }
        }
        foreach (var r in a.MoneyRows)
        {
            if (r is null) continue;
            if (r.Fuel == FuelType.Diesel) { dr += r.Rasid; da += r.Albaqi; db += r.Bardagi; }
            else                            { pr += r.Rasid; pa += r.Albaqi; pb += r.Bardagi; }
        }
        return new SplitTotals(new FuelTotals(pf, pr, prf, pa, pb), new FuelTotals(df, dr, drf, da, db));
    }

    /// <summary>جمعِ چند حساب — ‎_sumAcctTotals‎.</summary>
    public SplitTotals SumTotals(IEnumerable<DebtAccount> accounts)
    {
        var p = new FuelTotals(); var d = new FuelTotals();
        foreach (var a in accounts)
        {
            if (a is null) continue;
            var t = AccountTotals(a);
            p += t.Petrol; d += t.Diesel;
        }
        return new SplitTotals(p, d);
    }

    // ── حالِ کارت ────────────────────────────────────────────────────────────
    /// <summary>
    /// ‎debtFuelStatus(p, acctList, cache)‎ — حالِ هر دفتر جدا (پطرول/دیزل/پول).
    ///
    /// سه ریزه‌کاری که اگر نباشند نتیجه فرق می‌کند (و آزمونِ برابری همان‌ها را گرفت):
    ///  ۱) اعتبار با فیصدیِ همان سوخت تخفیف می‌خورد: ‎eff = 1 − pct/100‎ .
    ///  ۲) دفترِ پول *دو بار* سنجیده می‌شود، یک‌بار برای هر سوخت — و رسیدهای
    ///     داخلِ جدول هم به اعتبارِ پول اضافه می‌شوند.
    ///  ۳) «تسویه‌شده سرخ نمی‌ماند»: اگر حال 'out' شد ولی الباقیِ همان دفتر
    ///     مثبت نبود، به 'ok' برمی‌گردد. هشدارِ زردِ «کم مانده» دست نمی‌خورد.
    ///     ⚠️ هر دفتر با الباقیِ خودش سنجیده می‌شود، نه با جمعشان — وگرنه بدهیِ
    ///     دیزل با پیش‌پرداختِ پطرول پاک می‌شود.
    /// </summary>
    public DebtStatusInfo Status(IEnumerable<DebtAccount> accounts)
    {
        var list = accounts.Where(a => a is not null).ToList();
        var per = new Dictionary<string, DebtStatus>
            { ["petrol"] = DebtStatus.None, ["diesel"] = DebtStatus.None, ["money"] = DebtStatus.None };

        void Check(decimal deposit, decimal used, string kind)
        {
            if (deposit <= 0m && used <= 0m) return;
            var rem = deposit - used;
            var st = rem <= 0m ? DebtStatus.Out
                   : (rem <= deposit * 0.2m ? DebtStatus.Low : DebtStatus.Ok);
            if (st > per[kind]) per[kind] = st;
        }

        foreach (var a in list)
        {
            var t = AccountTotals(a);
            var effP = 1m - (PercentOf(a, FuelType.Petrol) / 100m);
            var effD = 1m - (PercentOf(a, FuelType.Diesel) / 100m);
            Check(a.RasidFuelPetrol * effP, t.Petrol.Liters, "petrol");
            Check(a.RasidFuelDiesel * effD, t.Diesel.Liters, "diesel");
            Check(a.RasidMoneyPetrol * effP + t.Petrol.Rasid, t.Petrol.Bardagi, "money");
            Check(a.RasidMoneyDiesel * effD + t.Diesel.Rasid, t.Diesel.Bardagi, "money");
        }

        // حسابِ تسویه‌شده «تمام‌شده» نیست
        var b = Balances(list);
        if (per["petrol"] == DebtStatus.Out && !(b.Petrol > 0m)) per["petrol"] = DebtStatus.Ok;
        if (per["diesel"] == DebtStatus.Out && !(b.Diesel > 0m)) per["diesel"] = DebtStatus.Ok;
        if (per["money"]  == DebtStatus.Out && !(b.Money  > 0m)) per["money"]  = DebtStatus.Ok;

        var fuelSt = per["petrol"] >= per["diesel"] ? per["petrol"] : per["diesel"];
        var worst  = fuelSt >= per["money"] ? fuelSt : per["money"];
        return new DebtStatusInfo(worst, per["petrol"], per["diesel"], per["money"]);
    }

    // ══════════════════════════════════════════════════════════════════════
    //  دفترِ رسیدهای سربرگ — سربرگ و جدول و جمع، همه از یک منبع
    // ══════════════════════════════════════════════════════════════════════
    //
    //  خواستهٔ صریحِ صاحب ریپو: «سربرگ، جدول و Summary باید از یک Data Source
    //  بخوانند؛ هیچ State تکراری و مستقلی نباشد. رسیدی که در سربرگ وارد
    //  می‌شود باید ردیفِ خودش را در جدول داشته باشد و برعکس.»
    //
    //  سایت همین را دارد (‎acct.rasidLog‎، خطِ ۳۴۶۲۰ی ‎index.html‎) و منطقش
    //  مو‌به‌مو همین است:
    //
    //    • هر عددی که در سربرگ نوشته شود یک **رسیدِ تازه** است (‎PushRasid‎)،
    //      نه ویرایشِ عددِ قبلی. پس رسیدِ قبلی پاک نمی‌شود.
    //    • کادرِ سربرگ **جمعِ** دفتر را نشان می‌دهد (‎RasidLogSum‎).
    //    • هر رسید ردیفِ خودش را در جدول دارد و با 🗑️ همان‌جا پاک می‌شود.
    //    • چهار عددِ قدیمیِ ‎Rasid…‎ی حساب همیشه برابرِ جمعِ دفتر نگه داشته
    //      می‌شوند (‎RasidLogSync‎) تا PDF، آرشیو، نمای انباشته و هشدارها —
    //      که همه از آن‌ها می‌خوانند — هیچ تغییری نبینند.
    //
    //  ⚠️ ردیف‌های دفتر هرگز در ‎SplitTotals‎ی ردیف‌های جدول شمرده نمی‌شوند؛
    //  دو دفترِ جدا هستند و جمعِ هر کدام سرِ جای خودش می‌نشیند.

    /// <summary>
    /// حساب‌های قدیمی دفتر ندارند — یک‌بار از روی همان چهار عددِ قبلی ساخته
    /// می‌شود، دقیقاً مثلِ ‎_rasidLogInit‎ی سایت. ‎true‎ یعنی چیزی ساخته شد و
    /// باید ذخیره شود.
    /// </summary>
    public bool RasidLogInit(DebtAccount a)
    {
        if (a is null) return false;
        a.RasidLog ??= new List<RasidEntry>();
        if (a.RasidLog.Count > 0) return false;

        var seeds = new (LedgerMode Unit, FuelType Fuel, decimal Value)[]
        {
            (LedgerMode.Fuel,  FuelType.Petrol, a.RasidFuelPetrol),
            (LedgerMode.Fuel,  FuelType.Diesel, a.RasidFuelDiesel),
            (LedgerMode.Money, FuelType.Petrol, a.RasidMoneyPetrol),
            (LedgerMode.Money, FuelType.Diesel, a.RasidMoneyDiesel),
        };

        var made = false;
        foreach (var (unit, fuel, v) in seeds)
        {
            if (v == 0m) continue;
            a.RasidLog.Add(new RasidEntry
            {
                AccountId = a.Id, Unit = unit, Fuel = fuel, Value = v,
                SortIndex = a.RasidLog.Count,
            });
            made = true;
        }
        return made;
    }

    /// <summary>جمعِ رسیدهای یک دفتر و یک تیل.</summary>
    public decimal RasidLogSum(DebtAccount a, LedgerMode unit, FuelType fuel)
    {
        if (a?.RasidLog is null) return 0m;
        var sum = 0m;
        foreach (var e in a.RasidLog)
            if (e is not null && e.DeletedAt is null && e.Unit == unit && e.Fuel == fuel)
                sum += e.Value;
        return sum;
    }

    /// <summary>
    /// چهار عددِ حساب همیشه برابرِ جمعِ دفتر نگه داشته می‌شوند.
    /// ‎true‎ یعنی عددی واقعاً عوض شد و ذخیره لازم است.
    /// </summary>
    public bool RasidLogSync(DebtAccount a)
    {
        if (a is null) return false;
        var changed = RasidLogInit(a);

        var fp = RasidLogSum(a, LedgerMode.Fuel, FuelType.Petrol);
        var fd = RasidLogSum(a, LedgerMode.Fuel, FuelType.Diesel);
        var mp = RasidLogSum(a, LedgerMode.Money, FuelType.Petrol);
        var md = RasidLogSum(a, LedgerMode.Money, FuelType.Diesel);

        if (a.RasidFuelPetrol != fp) { a.RasidFuelPetrol = fp; changed = true; }
        if (a.RasidFuelDiesel != fd) { a.RasidFuelDiesel = fd; changed = true; }
        if (a.RasidMoneyPetrol != mp) { a.RasidMoneyPetrol = mp; changed = true; }
        if (a.RasidMoneyDiesel != md) { a.RasidMoneyDiesel = md; changed = true; }
        return changed;
    }

    /// <summary>
    /// ثبتِ یک رسیدِ تازه در دفتر — تنها راهِ نوشتنِ رسیدِ سربرگ
    /// (همتای ‎_pmPushRasid‎). عددِ صفر چیزی ثبت نمی‌کند.
    /// </summary>
    public RasidEntry? PushRasid(DebtAccount a, LedgerMode unit, FuelType fuel,
                                 decimal value, string? dateShamsi = null)
    {
        if (a is null || value == 0m) return null;
        RasidLogInit(a);
        var e = new RasidEntry
        {
            AccountId = a.Id, Unit = unit, Fuel = fuel, Value = value,
            DateShamsi = dateShamsi,
            SortIndex = a.RasidLog.Count,
        };
        a.RasidLog.Add(e);
        RasidLogSync(a);
        return e;
    }

    /// <summary>
    /// پاک کردنِ یک رسیدِ ثبت‌شده (اگر اشتباه نوشته شده باشد) — همتای
    /// ‎deletePersonRasid‎. ‎true‎ یعنی پیدا شد و برداشته شد.
    /// </summary>
    public bool RemoveRasid(DebtAccount a, RasidEntry? entry)
    {
        if (a?.RasidLog is null || entry is null) return false;
        if (!a.RasidLog.Remove(entry)) return false;
        RasidLogSync(a);
        return true;
    }

    // ── معادلِ سوخت (بندِ ۸) ─────────────────────────────────────────────────
    /// <summary>
    /// بندِ ۸: موجودیِ پول به افغانی ذخیره می‌شود و معادلِ سوخت *محاسبه* می‌شود،
    /// نه ذخیره — پس با عوض شدنِ نرخِ اتحادیه خودش تازه می‌شود و هیچ‌وقت کهنه
    /// نمی‌ماند.  ۱۵۰۰۰ ÷ ۵۶ = ۲۶۷٫۸۶ لیتر
    /// نرخِ صفر ⇒ صفر (نه تقسیم بر صفر).
    /// </summary>
    public decimal MoneyToLiters(decimal afn, FuelType fuel)
    {
        var rate = _rates.UnionRate(fuel);
        if (rate <= 0m) return 0m;
        return Math.Round(afn / rate, 2, MidpointRounding.AwayFromZero);
    }
}

/// <summary>نرخِ اتحادیه — از تنظیمات می‌آید (DB.unionRatePetrol / unionRateDiesel).</summary>
public interface IUnionRateProvider
{
    decimal UnionRate(FuelType fuel);
}
