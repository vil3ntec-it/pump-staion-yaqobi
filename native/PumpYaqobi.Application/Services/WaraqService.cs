using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;

namespace PumpYaqobi.Application.Services;

public readonly record struct WaraqShiftTotals(
    decimal PetrolLiters, decimal DieselLiters, decimal Sales,
    decimal Debt, decimal Expenses, decimal DeclaredDebt);

public readonly record struct WaraqShortage(
    decimal Shortage, decimal Excess, decimal Declared, decimal Covered);

/// <summary>
/// ══ ورقِ روزانه ════════════════════════════════════════════════════════════
/// رونوشتِ ‎_waraqRepPrice‎ · ‎_waraqTxnAmount‎ · ‎_normalizeWaraqTxns‎ ·
/// ‎computeShiftTotals‎ · ‎computeWaraqShortage‎.
///
/// ⚠️ «فیِ ورق» فیِ پرتکرارترین پایهٔ همان سوخت است، نه میانگین و نه فیِ
/// پایهٔ اول. اگر این را عوض کنید، مبلغِ خودکارِ همهٔ ردیف‌های قرض عوض می‌شود.
///
/// ⚠️ ‎AmountAuto‎ سه‌حالته است. ردیفی که کاربر دستی نوشته (false) هرگز
/// بازحساب نمی‌شود؛ ردیفِ کهنه (null) فقط وقتی خودکار شمرده می‌شود که مبلغش
/// صفر باشد یا دقیقاً برابرِ «لیتر × فیِ کهنه» — همان تشخیصی که نسخهٔ وب داشت.
/// </summary>
public sealed class WaraqService
{
    /// <summary>
    /// ‎_waraqRepPrice(sd, fuel)‎ — فیِ پرتکرارترین پایهٔ همان سوخت.
    ///
    /// ⚠️ گره‌گشاییِ تساوی عمداً مثلِ جاوااسکریپت است: آن‌جا شمارنده یک شیء بود
    /// و ‎Object.keys‎ کلیدهای «عددِ صحیح» را صعودی برمی‌گرداند و بقیه را به
    /// ترتیبِ افزوده‌شدن. چون شرط ‎>‎ است (نه ‎>=‎)، وقتی دو فی به یک اندازه
    /// تکرار شده باشند، کوچک‌ترینِ صحیح برنده می‌شود. اگر این را با ترتیبِ
    /// پایه‌ها جایگزین کنیم، مبلغِ خودکارِ ردیف‌های قرض با نسخهٔ وب فرق می‌کند —
    /// آزمونِ برابری همین را گرفت.
    /// </summary>
    public decimal RepPrice(WaraqShift sd, FuelType fuel)
    {
        if (sd is null) return 0m;

        // ══ همان حساب، ولی یک بار به‌ازای هر شیفت ════════════════════════════
        //
        // ⚠️ این تابع از ‎TxnAmount‎ و ‎NormalizeTxns‎ **به‌ازای هر ردیف** صدا
        // زده می‌شود، و هر بار یک ‎Dictionary‎ و یک ‎List‎ و چهار عملگرِ لینک
        // می‌ساخت. اندازه‌گیری: باز کردنِ یک ورقِ ۶۰ ردیفی ۳٬۸۱۴ میلی‌ثانیه در
        // ویومدل می‌سوزاند، در حالی که خواندنش از دیتابیس ۳ میلی‌ثانیه بود و
        // چیدنِ صفحه ۸۴. فهرستِ ورق‌ها هم ‎ShiftTotals‎ را ۴۸ بار صدا می‌زند،
        // یعنی هزاران بار ساختنِ همان دیکشنری.
        //
        // ⚠️ نتیجه مو‌به‌مو همان می‌ماند: خودِ تابع دست نخورده و فقط پاسخش
        // نگه داشته می‌شود. اعتبارِ کَش با **مقایسهٔ دقیق** سنجیده می‌شود، نه
        // با هش — این‌جا پای پول در میان است و یک برخوردِ نادر هم پذیرفتنی
        // نیست. مقایسه بی هیچ تخصیصِ حافظه‌ای است و به شمارِ پایه‌هاست (شش‌تا)،
        // نه به شمارِ ردیف‌ها.
        var c = _repCache.GetValue(sd, _ => new RepCache());
        if (!c.Matches(sd))
        {
            c.Petrol = RepPriceCore(sd, FuelType.Petrol);
            c.Diesel = RepPriceCore(sd, FuelType.Diesel);
            c.Remember(sd);
        }
        return fuel == FuelType.Diesel ? c.Diesel : c.Petrol;
    }

    private readonly System.Runtime.CompilerServices.ConditionalWeakTable<WaraqShift, RepCache>
        _repCache = new();

    /// <summary>عکسِ همان چیزهایی که <see cref="RepPriceCore"/> می‌خواند.</summary>
    private sealed class RepCache
    {
        private FuelType[] _fuels = Array.Empty<FuelType>();
        private decimal[] _prices = Array.Empty<decimal>();
        private decimal _basePetrol, _baseDiesel;
        private bool _valid;

        public decimal Petrol, Diesel;

        public bool Matches(WaraqShift sd)
        {
            if (!_valid) return false;
            var n = sd.Pumps.Count;
            if (_fuels.Length != n) return false;
            if (_basePetrol != sd.PricePerLiter || _baseDiesel != sd.PricePerLiterDiesel) return false;
            for (var i = 0; i < n; i++)
            {
                var p = sd.Pumps[i];
                if (_fuels[i] != p.Fuel || _prices[i] != p.PricePerLiter) return false;
            }
            return true;
        }

        public void Remember(WaraqShift sd)
        {
            var n = sd.Pumps.Count;
            if (_fuels.Length != n) { _fuels = new FuelType[n]; _prices = new decimal[n]; }
            for (var i = 0; i < n; i++)
            {
                _fuels[i] = sd.Pumps[i].Fuel;
                _prices[i] = sd.Pumps[i].PricePerLiter;
            }
            _basePetrol = sd.PricePerLiter;
            _baseDiesel = sd.PricePerLiterDiesel;
            _valid = true;
        }
    }

    /// <summary>خودِ حساب — دست‌نخورده از روزِ اول.</summary>
    private static decimal RepPriceCore(WaraqShift sd, FuelType fuel)
    {
        var counts = new Dictionary<decimal, int>();
        var order = new List<decimal>();
        foreach (var p in sd.Pumps)
        {
            if (p.Fuel != fuel) continue;
            if (p.PricePerLiter <= 0m) continue;
            if (counts.TryGetValue(p.PricePerLiter, out var c)) counts[p.PricePerLiter] = c + 1;
            else { counts[p.PricePerLiter] = 1; order.Add(p.PricePerLiter); }
        }

        // ترتیبِ کلیدهای شیء در جاوااسکریپت
        var keys = order.Where(k => k == decimal.Truncate(k) && k >= 0).OrderBy(k => k)
                        .Concat(order.Where(k => !(k == decimal.Truncate(k) && k >= 0)))
                        .ToList();

        decimal best = 0m; var bestCount = 0;
        foreach (var k in keys)
            if (counts[k] > bestCount) { bestCount = counts[k]; best = k; }

        if (best > 0m) return best;
        return fuel == FuelType.Diesel ? sd.PricePerLiterDiesel : sd.PricePerLiter;
    }

    private static decimal LegacyPrice(WaraqShift sd, FuelType fuel) =>
        fuel == FuelType.Diesel ? sd.PricePerLiterDiesel : sd.PricePerLiter;

    /// <summary>‎_waraqTxnAmount(sd, t)‎</summary>
    public decimal TxnAmount(WaraqShift sd, WaraqTransaction t)
    {
        if (t is null) return 0m;
        var auto = Math.Round(t.Liters * RepPrice(sd, t.Fuel), 0, MidpointRounding.AwayFromZero);

        if (t.AmountAuto == false) return t.Amount;
        if (t.AmountAuto == true) return auto;

        var stored = t.Amount;
        if (stored == 0m) return auto;

        var legacy = LegacyPrice(sd, t.Fuel);
        if (t.Liters > 0m && legacy > 0m &&
            stored == Math.Round(t.Liters * legacy, 0, MidpointRounding.AwayFromZero))
            return auto;
        return stored;
    }

    /// <summary>‎_normalizeWaraqTxns(sd)‎ — مبلغِ خودکار را با فیِ درست بازحساب می‌کند.</summary>
    public void NormalizeTxns(WaraqShift sd)
    {
        if (sd is null) return;
        foreach (var t in sd.Transactions)
        {
            if (t.AmountAuto == true)
            {
                t.Amount = Math.Round(t.Liters * RepPrice(sd, t.Fuel), 0, MidpointRounding.AwayFromZero);
                continue;
            }
            if (t.AmountAuto == false) continue;

            var stored = t.Amount;
            var legacy = LegacyPrice(sd, t.Fuel);
            var wasAuto = stored == 0m ||
                (t.Liters > 0m && legacy > 0m &&
                 stored == Math.Round(t.Liters * legacy, 0, MidpointRounding.AwayFromZero));
            if (wasAuto)
            {
                t.AmountAuto = true;
                t.Amount = Math.Round(t.Liters * RepPrice(sd, t.Fuel), 0, MidpointRounding.AwayFromZero);
            }
            else t.AmountAuto = false;
        }
    }

    /// <summary>‎computeShiftTotals(sd)‎</summary>
    public WaraqShiftTotals ShiftTotals(WaraqShift sd)
    {
        if (sd is null) return default;
        decimal petrol = 0, diesel = 0, sales = 0, declared = 0;
        foreach (var p in sd.Pumps)
        {
            var l = Math.Max(0m, p.End - p.Start);
            if (p.Fuel == FuelType.Diesel) diesel += l; else petrol += l;
            sales += l * p.PricePerLiter;
            declared += p.Debt;
        }

        NormalizeTxns(sd);
        decimal debt = sd.FabricDebt, expenses = 0;
        foreach (var t in sd.Transactions)
        {
            if (t.Type == WaraqTxnType.Debt) debt += TxnAmount(sd, t);
            else if (!string.IsNullOrWhiteSpace(t.Name)) expenses += TxnAmount(sd, t);
        }
        return new WaraqShiftTotals(petrol, diesel, sales, debt, expenses, declared);
    }

    /// <summary>
    /// ‎computeWaraqShortage‎ — کمبودی = قرضِ نوشته‌شده در پارچه منهای
    /// قرض‌ها و مصرف‌هایی که در همین ورق ثبت شده‌اند. منفی یعنی «اضافی».
    /// </summary>
    public WaraqShortage Shortage(WaraqShiftTotals t)
    {
        var declared = t.DeclaredDebt;
        var covered = t.Debt + t.Expenses;
        var net = declared - covered;
        return new WaraqShortage(net > 0 ? net : 0m, net < 0 ? -net : 0m, declared, covered);
    }
}
