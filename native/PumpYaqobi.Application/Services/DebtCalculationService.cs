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
            // ⚠️ یک‌بار، نه دو بار. تا دیروز رسیدِ سربرگ و رسیدِ جدول دو انبارِ
            // جدا بودند و این خط جمعشان می‌کرد؛ حالا که رسید فقط یک جا زندگی
            // می‌کند، ‎a.RasidMoneyPetrol‎ خودش همان ‎t.Petrol.Rasid‎ است و
            // جمع کردنشان یعنی دو برابر شمردنِ همان رسید.
            Check(t.Petrol.Rasid * effP, t.Petrol.Bardagi, "money");
            Check(t.Diesel.Rasid * effD, t.Diesel.Bardagi, "money");
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
    //  رسید — یک رکوردِ واقعی، در خودِ جدول
    // ══════════════════════════════════════════════════════════════════════
    //
    //  خواستهٔ صریحِ صاحب ریپو: «رسید باید یک دادهٔ واقعی باشد. سربرگ رسیدهای
    //  واقعی را نمایش دهد، جدول همان رسیدهای واقعی را نمایش دهد، جمله همان‌ها
    //  را حساب کند. برای هر قسمت یک مقدارِ جداگانه نساز.»
    //
    //  پیش از این دو انبارِ جدا بود و همین ریشهٔ ناهم‌گامی بود:
    //    • رسیدِ سربرگ در چهار عددِ ‎RasidFuelP/D‎ و ‎RasidMoneyP/D‎ی حساب
    //    • رسیدِ جدول در ستونِ ‎Rasid‎/‎RasidFuel‎ی خودِ ردیف‌ها
    //  پس رسیدی که در جدول نوشته می‌شد هرگز در کادرِ سربرگ نمی‌آمد و برعکس.
    //
    //  حالا **رسید فقط یک جا زندگی می‌کند**: ستونِ رسیدِ همان ردیفِ جدول.
    //  چهار عددِ حساب از روی همان ردیف‌ها حساب می‌شوند و فقط «کش» هستند تا
    //  PDF، آرشیو، کارتِ حساب و هشدارها — که همه از آن‌ها می‌خوانند —
    //  دست‌نخورده بمانند. تنها جای نوشتنشان <see cref="SyncReceiptTotals"/>
    //  است.
    //
    //  ⚠️ کدام ستون «رسید» است به دفترِ باز بستگی دارد — همان
    //  ‎_rasidLogKey(fuel, money)‎ی سایت:
    //      دفترِ تیل ⇒ ستونِ «رسید تیل» (‎RasidFuel‎)
    //      دفترِ پول ⇒ ستونِ «رسید»     (‎Rasid‎)

    /// <summary>رسیدِ همین ردیف، در دفترِ داده‌شده.</summary>
    public static decimal ReceiptOf(DebtRow r, bool money) =>
        r is null ? 0m : (money ? r.Rasid : r.RasidFuel);

    /// <summary>نوشتنِ رسید در ستونِ درستِ همان دفتر.</summary>
    public static void SetReceipt(DebtRow r, bool money, decimal value)
    {
        if (r is null) return;
        if (money) r.Rasid = value; else r.RasidFuel = value;
    }

    /// <summary>
    /// ردیفی که فقط یک رسید است — بی لیتر و بی بردگی.
    ///
    /// ⚠️ هیچ فرقی با ردیفی که کاربر خودش در جدول می‌سازد ندارد: خواستهٔ صریحِ
    /// صاحب ریپو بود که «رسیدی که از سربرگ ساخته می‌شود نباید ساختارِ متفاوتی
    /// از رسیدی که از جدول ساخته می‌شود داشته باشد».
    /// </summary>
    public static DebtRow NewReceiptRow(DebtAccount a, FuelType fuel, decimal value,
                                        string? dateShamsi = null)
    {
        var money = a.Mode.IsMoney();
        var r = new DebtRow
        {
            Fuel = fuel,
            ByMoney = money,
            DateShamsi = dateShamsi,
            SortIndex = a.ActiveRows().Count,
        };
        if (money) r.MoneyAccountId = a.Id; else r.FuelAccountId = a.Id;
        SetReceipt(r, money, value);
        return r;
    }

    /// <summary>جمعِ رسیدهای یک دفتر و یک تیل — تنها راهِ حسابِ «مقدار رسید».</summary>
    public decimal ReceiptTotal(DebtAccount a, LedgerMode unit, FuelType fuel)
    {
        if (a is null) return 0m;
        var money = unit.IsMoney();
        var rows = money ? a.MoneyRows : a.FuelRows;
        var sum = 0m;
        foreach (var r in rows)
            if (r is not null && r.DeletedAt is null && r.Fuel == fuel)
                sum += ReceiptOf(r, money);
        return sum;
    }

    /// <summary>
    /// چهار عددِ حساب را با ردیف‌ها یکی می‌کند. ‎true‎ یعنی عددی واقعاً عوض شد.
    ///
    /// ⚠️ تنها جایی است که این چهار عدد نوشته می‌شوند. هیچ‌جای دیگری مستقیم
    /// در آن‌ها ننویسید — همان کاری بود که دو انبارِ ناهم‌گام می‌ساخت.
    /// </summary>
    public bool SyncReceiptTotals(DebtAccount a)
    {
        if (a is null) return false;
        var fp = ReceiptTotal(a, LedgerMode.Fuel, FuelType.Petrol);
        var fd = ReceiptTotal(a, LedgerMode.Fuel, FuelType.Diesel);
        var mp = ReceiptTotal(a, LedgerMode.Money, FuelType.Petrol);
        var md = ReceiptTotal(a, LedgerMode.Money, FuelType.Diesel);

        var changed = false;
        if (a.RasidFuelPetrol != fp) { a.RasidFuelPetrol = fp; changed = true; }
        if (a.RasidFuelDiesel != fd) { a.RasidFuelDiesel = fd; changed = true; }
        if (a.RasidMoneyPetrol != mp) { a.RasidMoneyPetrol = mp; changed = true; }
        if (a.RasidMoneyDiesel != md) { a.RasidMoneyDiesel = md; changed = true; }
        return changed;
    }

    /// <summary>
    /// ══ مهاجرتِ امن ═══════════════════════════════════════════════════════
    /// حساب‌های قدیمی رسیدشان را در چهار عددِ حساب دارند (و نسخهٔ پیشین، در
    /// <see cref="RasidEntry"/>). یک‌بار همان‌ها به ردیفِ واقعی تبدیل می‌شوند
    /// تا هیچ عددی گم نشود و از آن پس فقط یک انبار بماند.
    ///
    /// ردیف‌های تازه برگردانده می‌شوند تا صداکننده ذخیره‌شان کند.
    ///
    /// ⚠️ «چقدر کم است» از تفاوتِ عددِ حساب با جمعِ ردیف‌ها درمی‌آید، نه از
    /// خودِ عدد — وگرنه حسابی که رسیدش همین حالا هم ردیف دارد، دو برابر
    /// می‌شد. پس این تابع هر بار صدا زده شود، بارِ دوم چیزی نمی‌سازد.
    /// </summary>
    public List<DebtRow> MigrateReceiptsToRows(DebtAccount a, string? dateShamsi = null)
    {
        var made = new List<DebtRow>();
        if (a is null || a.ReceiptsMigrated) return made;
        a.ReceiptsMigrated = true;

        // نسخهٔ پیشین: دفترِ جدا. هر رکوردش یک ردیفِ واقعی می‌شود.
        if (a.RasidLog is { Count: > 0 })
        {
            foreach (var e in a.RasidLog.Where(x => x is not null && x.Value != 0m).ToList())
                made.Add(AddReceiptRow(a, e.Unit, e.Fuel, e.Value, e.DateShamsi ?? dateShamsi));
            a.RasidLog.Clear();
        }

        // و عددهای قدیمیِ خودِ حساب، به‌اندازهٔ آن‌چه ردیف‌ها توضیح نمی‌دهند
        var want = new (LedgerMode Unit, FuelType Fuel, decimal Value)[]
        {
            (LedgerMode.Fuel,  FuelType.Petrol, a.RasidFuelPetrol),
            (LedgerMode.Fuel,  FuelType.Diesel, a.RasidFuelDiesel),
            (LedgerMode.Money, FuelType.Petrol, a.RasidMoneyPetrol),
            (LedgerMode.Money, FuelType.Diesel, a.RasidMoneyDiesel),
        };

        foreach (var (unit, fuel, value) in want)
        {
            var gap = value - ReceiptTotal(a, unit, fuel);
            if (gap == 0m) continue;
            made.Add(AddReceiptRow(a, unit, fuel, gap, dateShamsi));
        }

        SyncReceiptTotals(a);
        return made;
    }

    /// <summary>
    /// ردیفِ رسید را در دفترِ خواسته‌شده می‌سازد و همان‌جا می‌نشاند.
    /// ‎Mode‎ی حساب موقتاً عوض می‌شود تا ردیف در ستون و دفترِ درست بنشیند و
    /// بعد سرِ جایش برمی‌گردد — خودِ حساب دست‌نخورده می‌ماند.
    /// </summary>
    public DebtRow AddReceiptRow(DebtAccount a, LedgerMode unit, FuelType fuel,
                                 decimal value, string? dateShamsi = null)
    {
        var was = a.Mode;
        a.Mode = unit;
        var row = NewReceiptRow(a, fuel, value, dateShamsi);
        a.Mode = was;
        (unit.IsMoney() ? a.MoneyRows : a.FuelRows).Add(row);
        NormalizeRow(row);
        SyncReceiptTotals(a);
        return row;
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
