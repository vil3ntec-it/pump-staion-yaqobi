using Microsoft.EntityFrameworkCore;
using PumpYaqobi.Application.Localization;
using PumpYaqobi.Application.Services;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;
using PumpYaqobi.Persistence;

namespace PumpYaqobi.Services.Data;

/// <summary>
/// ══ هم‌سطح نگه داشتنِ چهار عددِ رسیدِ حساب ═══════════════════════════════════
///
/// رسید یک جا بیشتر زندگی نمی‌کند: ستونِ رسیدِ خودِ ردیف‌های جدول
/// (‎DebtRow.RasidFuel‎ در دفترِ تیل، ‎DebtRow.Rasid‎ در دفترِ پول).
///
/// چهار عددِ <c>RasidFuelP/D</c> و <c>RasidMoneyP/D</c>ی حساب فقط «کش»اند —
/// کارتِ حساب، PDF، آرشیو و هشدارها از همان‌ها می‌خوانند و باید همیشه برابرِ
/// جمعِ ردیف‌ها بمانند.
///
/// این کلاس همان کار را روی خودِ دیتابیس می‌کند، برای جاهایی که ردیف‌های حساب
/// در حافظه نیستند (مثلِ تایید و برگرداندنِ فاکتور).
/// </summary>
public static class ReceiptSync
{
    private sealed class NoRates : IUnionRateProvider
    {
        public static readonly NoRates Instance = new();
        public decimal UnionRate(FuelType fuel) => 0m;
    }

    /// <summary>
    /// ⛔ حسابِ «مهاجرت‌نکرده» پیش از هر نوشتنِ خودکاری مهاجرت می‌کند (۱۴۰۵/۰۷/۱۶).
    ///
    /// حسابی که از نسخهٔ وب آمده و هنوز در برنامه باز نشده، رسیدش فقط در چهار
    /// عددِ کش است (بی ردیف). تا امروز تاییدِ فاکتور همان کش را از روی ردیف‌ها
    /// **بازنویسی** می‌کرد ⇒ رسیدِ کهنه (مثلاً ۳۰۰ لیتر) برای همیشه گم می‌شد؛ و
    /// رسیدِ پولی در کارت دو بار شمرده می‌شد (یک بار از کش، یک بار از ردیف).
    ///
    /// همان ‎MigrateReceiptsToRows‎ِ صفحهٔ حساب است — قاعدهٔ دوم ساخته نشد — فقط
    /// این بار روی ردیف‌های دیتابیس و <b>پیش از</b> افزودنِ ردیفِ تازه، تا «فاصلهٔ
    /// کش با ردیف‌ها» درست همان رسیدِ کهنه باشد.
    /// </summary>
    public static async Task MigrateAsync(PumpDbContext db, DebtAccount acc, CancellationToken ct = default)
    {
        if (acc is null || acc.ReceiptsMigrated) return;
        if (acc.Id == 0) { acc.ReceiptsMigrated = true; return; }
        var e = db.Entry(acc);
        await e.Collection(x => x.FuelRows).LoadAsync(ct);
        await e.Collection(x => x.MoneyRows).LoadAsync(ct);
        await e.Collection(x => x.RasidLog).LoadAsync(ct);
        var old = acc.RasidLog.ToList();
        var made = new DebtCalculationService(NoRates.Instance).MigrateReceiptsToRows(acc, Shamsi.Today());
        foreach (var r in made) r.DateKey = Shamsi.Key(r.DateShamsi);
        if (old.Count > 0) db.RasidEntries.RemoveRange(old);
    }

    /// <summary>
    /// چهار عددِ حساب را از روی ردیف‌های خودش در دیتابیس از نو حساب می‌کند.
    ///
    /// ⚠️ پیش از این صدا زدن باید ردیف‌های تازه/حذف‌شده در همان
    /// <paramref name="db"/> ثبت شده باشند (‎SaveChanges‎ یا دستِ‌کم در
    /// ‎ChangeTracker‎)، وگرنه جمع، دنیای پیش از تغییر را می‌بیند.
    /// </summary>
    public static async Task FromRowsAsync(PumpDbContext db, DebtAccount acc,
                                           CancellationToken ct = default)
    {
        if (acc is null) return;
        await db.SaveChangesAsync(ct);

        var id = acc.Id;
        var fuel = await db.DebtRows.Where(r => r.FuelAccountId == id).ToListAsync(ct);
        var money = await db.DebtRows.Where(r => r.MoneyAccountId == id).ToListAsync(ct);

        decimal S(List<DebtRow> rows, FuelType f, bool asMoney) =>
            rows.Where(r => r.Fuel == f).Sum(r => asMoney ? r.Rasid : r.RasidFuel);

        acc.RasidFuelPetrol = S(fuel, FuelType.Petrol, false);
        acc.RasidFuelDiesel = S(fuel, FuelType.Diesel, false);
        acc.RasidMoneyPetrol = S(money, FuelType.Petrol, true);
        acc.RasidMoneyDiesel = S(money, FuelType.Diesel, true);
    }
}
