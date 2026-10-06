using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PumpYaqobi.Application.Services;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;
using PumpYaqobi.Services.Data;
using Xunit;

namespace PumpYaqobi.Tests;

file sealed class NoRatesOld : IUnionRateProvider
{ public decimal UnionRate(FuelType f) => 0m; }

/// <summary>
/// ══ «اون انالیزور کار می‌کنه که حساب‌های قدیم رو درست کنه؟» (۱۴۰۵/۰۷/۲۱) ══════
///
/// دفترِ کهنهٔ واقعی‌نما روی SQLiteِ روی دیسک: چهار سال (۱۴۰۲ تا ۱۴۰۵)، حسابِ اصلی و
/// فرعی، دفترِ تیل و پول، ردیفِ «پولی»ِ خراب (‎ByMoney‎ با بردگیِ صفر)، لیتر و فیِ
/// اعشاری که نسخهٔ وب گرد نکرده ذخیره کرده بود، ‎DateKey = 0‎ و ماهِ کهنه، تاریخ با
/// رقمِ فارسی و ماهِ تک‌رقمی — و حسابی که هنوز مهاجرت نکرده.
///
/// ⛔ حقیقت در این آزمون <b>مستقل</b> حساب می‌شود (لیتر × فی، گرد از صفر دور؛
/// الباقی = بردگی − رسید؛ رسیدِ حساب = جمعِ رسیدِ ردیف‌ها)، نه با همان
/// ‎NormalizeRow‎ — وگرنه سنجه خودش را می‌سنجید.
/// </summary>
public class LedgerParityOldDataTests : IDisposable
{
    private readonly string _file = Path.Combine(Path.GetTempPath(), $"pump-parity-old-{Guid.NewGuid():N}.db");

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try { if (File.Exists(_file)) File.Delete(_file); } catch { }
    }

    private void Raw(string sql)
    {
        using var c = new SqliteConnection("Data Source=" + _file);
        c.Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    private record UserRow(long Id, long? F, long? M, string? Date, string? Name, string? Hawala, FuelType Fuel,
                           decimal Liters, decimal? Price, decimal Rasid, decimal RasidFuel, int Sort);

    private static List<UserRow> UserRows(PumpDbFactory dbf)
    {
        using var db = dbf.Create();
        return db.DebtRows.AsNoTracking().OrderBy(r => r.Id)
                 .Select(r => new UserRow(r.Id, r.FuelAccountId, r.MoneyAccountId, r.DateShamsi, r.Name, r.Hawala,
                                          r.Fuel, r.Liters, r.PricePerLiter, r.Rasid, r.RasidFuel, r.SortIndex)).ToList();
    }

    private static decimal R0(decimal v) => Math.Round(v, 0, MidpointRounding.AwayFromZero);

    /// <summary>همهٔ عددهای مشتقِ روی دیسک — برای «پاسِ دوم هیچ چیزی را عوض نکرد».</summary>
    private static string Derived(PumpDbFactory dbf)
    {
        using var db = dbf.Create();
        var rows = db.DebtRows.AsNoTracking().OrderBy(r => r.Id)
                     .Select(r => $"{r.Id}:{r.Bardagi}:{r.Albaqi}:{r.ByMoney}:{r.DateKey}").ToList();
        var accts = db.DebtAccounts.AsNoTracking().OrderBy(a => a.Id)
                      .Select(a => $"{a.Id}:{a.RasidFuelPetrol}:{a.RasidFuelDiesel}:{a.RasidMoneyPetrol}:{a.RasidMoneyDiesel}").ToList();
        var safe = db.SafeEntries.AsNoTracking().OrderBy(s => s.Id).Select(s => $"{s.Id}:{s.DateKey}:{s.MonthKey}").ToList();
        var exp = db.Expenses.AsNoTracking().OrderBy(s => s.Id).Select(s => $"{s.Id}:{s.DateKey}:{s.MonthKey}").ToList();
        return string.Join("|", rows.Concat(accts).Concat(safe).Concat(exp));
    }

    [Fact]
    public async Task DaftareKohne_ChaharSal_HameAdadhayeMoshtagh_DarYekPas_DorostMishavand()
    {
        var dbf = new PumpDbFactory(_file);
        dbf.EnsureReady();
        var parity = new LedgerParityService(dbf, new DebtCalculationService(new NoRatesOld()));
        var rnd = new Random(1405);
        var years = new[] { "1402", "1403", "1404", "1405" };

        using (var db = dbf.Create())
        {
            for (var i = 0; i < 40; i++)
            {
                var d = new Debtor { LegacyId = "old" + i, Name = "قرض‌دار " + i,
                    MainAccount = new DebtAccount { Name = "قرض‌دار " + i, ReceiptsMigrated = i % 10 != 9, RasidFuelPetrol = 123 } };
                if (i % 3 == 0)
                    d.SubAccounts.Add(new DebtAccount { Name = "فرعی " + i, LegacySubId = "s" + i, ReceiptsMigrated = true });
                foreach (var a in d.AllAccounts())
                    for (var k = 0; k < 12; k++)
                    {
                        var y = years[k % 4];
                        var date = $"{y}/{1 + k % 12:00}/{1 + (k * 7 + i) % 28:00}";
                        var fuel = k % 3 == 0 ? FuelType.Diesel : FuelType.Petrol;
                        a.FuelRows.Add(new DebtRow { DateShamsi = date, Fuel = fuel, Name = "ورق",
                            Liters = 10.5m + rnd.Next(0, 400) + k * 0.25m,
                            PricePerLiter = k % 5 == 4 ? null : 70.3m + k * 0.15m,
                            Rasid = rnd.Next(0, 3) * 500, RasidFuel = rnd.Next(0, 4) * 2.5m, SortIndex = k });
                        a.MoneyRows.Add(new DebtRow { DateShamsi = date, Fuel = fuel, ByMoney = true, Name = "پول",
                            Bardagi = 4000 + rnd.Next(0, 9000) + 0.5m, Rasid = rnd.Next(0, 4) * 750, SortIndex = k });
                    }
                db.Debtors.Add(d);
            }
            for (var k = 0; k < 30; k++)
            {
                db.SafeEntries.Add(new SafeEntry { DateShamsi = $"{years[k % 4]}/{1 + k % 12:00}/15", Title = "فروش ورق", Amount = 1000 + k });
                db.Expenses.Add(new Expense { DateShamsi = $"{years[k % 4]}/{1 + k % 12:00}/03", Amount = 50 + k });
            }
            db.SaveChanges();
        }

        //  ── دادهٔ کهنه، همان شکلی که نسخهٔ وب و نسخه‌های پیشین روی دیسک گذاشته‌اند ──
        //  بردگی و الباقیِ گردنشده یا کهنه، و ردیفِ پولیِ خراب (پولی با بردگیِ صفر و لیتر)
        Raw("UPDATE DebtRows SET Bardagi = Liters * PricePerLiter, Albaqi = 0 WHERE ByMoney = 0 AND PricePerLiter IS NOT NULL");
        Raw("UPDATE DebtRows SET Albaqi = Bardagi + 13 WHERE ByMoney = 1 AND Id % 4 = 0");
        Raw("UPDATE DebtRows SET ByMoney = 1, Bardagi = 0 WHERE ByMoney = 0 AND Id % 11 = 0");
        //  چهار رسیدِ حساب کهنه (حسابِ مهاجرت‌نکرده هم — آن یکی باید دست نخورد)
        Raw("UPDATE DebtAccounts SET RasidFuelPetrol = 999999, RasidMoneyDiesel = -4");
        //  کلیدِ تاریخِ کهنه: صفر، ماهِ غلط، و تاریخِ رقمِ فارسی با ماهِ تک‌رقمی
        Raw("UPDATE DebtRows SET DateKey = 0 WHERE Id % 2 = 0");
        Raw("UPDATE DebtRows SET DateShamsi = '۱۴۰۳/۴/۹', DateKey = 0 WHERE Id % 17 = 0");
        Raw("UPDATE SafeEntries SET DateKey = 0, MonthKey = NULL WHERE Id % 2 = 0");
        Raw("UPDATE SafeEntries SET MonthKey = '1399/01' WHERE Id % 2 = 1");
        Raw("UPDATE Expenses SET DateKey = 0, MonthKey = '' ");

        var userBefore = UserRows(dbf);
        decimal notMigratedBefore;
        using (var db = dbf.Create())
            notMigratedBefore = db.DebtAccounts.AsNoTracking().Where(a => !a.ReceiptsMigrated).ToList().Sum(a => a.RasidFuelPetrol);

        //  ── یک پاس، همان که برنامه پس از ورود و نیمه‌شب می‌زند ──
        var fixedCount = await parity.DailyOnceAsync();
        Assert.True(fixedCount > 500, "ناجورها درست شدند: " + fixedCount);
        Assert.Equal("", parity.LastError);

        using (var db = dbf.Create())
        {
            var rows = db.DebtRows.AsNoTracking().ToList();
            var accts = db.DebtAccounts.AsNoTracking().ToList();
            foreach (var r in rows)
            {
                //  ⛔ ردیفِ «پولی»ِ خراب (بردگیِ صفر با لیتر) در اصل ردیفِ تیل است
                var byMoney = r.ByMoney;
                if (byMoney) Assert.False(r.Bardagi == 0m && r.Liters > 0m, $"ردیفِ {r.Id} هنوز پولیِ خراب است");
                var bardagi = byMoney ? R0(r.Bardagi) : (r.Liters <= 0 ? 0 : R0(r.Liters * (r.PricePerLiter ?? 0m)));
                Assert.True(bardagi == r.Bardagi, $"بردگیِ ردیفِ {r.Id}: {r.Bardagi} ≠ {bardagi}");
                Assert.True(R0(bardagi - r.Rasid) == r.Albaqi, $"الباقیِ ردیفِ {r.Id}: {r.Albaqi}");
                var key = PumpYaqobi.Domain.DateKeys.Key(r.DateShamsi);
                Assert.True(key != 0 && key == r.DateKey, $"کلیدِ تاریخِ ردیفِ {r.Id}: {r.DateKey} ≠ {key}");
            }
            Assert.Contains(rows, r => r.DateShamsi == "۱۴۰۳/۴/۹" && r.DateKey == 14030409);
            foreach (var a in accts.Where(a => a.ReceiptsMigrated))
            {
                var f = rows.Where(r => r.FuelAccountId == a.Id).ToList();
                var m = rows.Where(r => r.MoneyAccountId == a.Id).ToList();
                Assert.Equal(f.Where(r => r.Fuel == FuelType.Petrol).Sum(r => r.RasidFuel), a.RasidFuelPetrol);
                Assert.Equal(f.Where(r => r.Fuel == FuelType.Diesel).Sum(r => r.RasidFuel), a.RasidFuelDiesel);
                Assert.Equal(m.Where(r => r.Fuel == FuelType.Petrol).Sum(r => r.Rasid), a.RasidMoneyPetrol);
                Assert.Equal(m.Where(r => r.Fuel == FuelType.Diesel).Sum(r => r.Rasid), a.RasidMoneyDiesel);
            }
            Assert.Contains(accts, a => !a.IsMain);          //  فرعی‌ها هم در فهرست بودند
            //  ⛔ حسابِ مهاجرت‌نکرده: چهار عدد هنوز منبع‌اند و دست نخوردند
            Assert.Equal(notMigratedBefore, accts.Where(a => !a.ReceiptsMigrated).Sum(a => a.RasidFuelPetrol));
            Assert.All(accts.Where(a => !a.ReceiptsMigrated), a => Assert.Equal(-4m, a.RasidMoneyDiesel));

            foreach (var s in db.SafeEntries.AsNoTracking().ToList())
            {
                var k = PumpYaqobi.Domain.DateKeys.Key(s.DateShamsi);
                Assert.Equal(k, s.DateKey);
                Assert.Equal(PumpYaqobi.Domain.DateKeys.Month(k), s.MonthKey);
            }
            foreach (var e in db.Expenses.AsNoTracking().ToList())
            {
                var k = PumpYaqobi.Domain.DateKeys.Key(e.DateShamsi);
                Assert.Equal(k, e.DateKey);
                Assert.Equal(PumpYaqobi.Domain.DateKeys.Month(k), e.MonthKey);
            }
        }

        //  ⛔ هیچ عددِ کاربری عوض نشد — لیتر، فی، رسید، رسیدِ تیل، نام، تاریخ، ترتیب
        Assert.Equal(userBefore, UserRows(dbf));

        //  ── پاسِ دوم هیچ چیزی را عوض نمی‌کند ──
        var snap = Derived(dbf);
        Assert.Empty(await parity.CheckAsync(fix: false));
        Assert.Equal(0, await parity.CheckDatesAsync(fix: false));
        Assert.Empty(await parity.CheckAsync(fix: true));
        Assert.Equal(0, await parity.CheckDatesAsync(fix: true));
        Assert.Equal(snap, Derived(dbf));
        Assert.Equal(userBefore, UserRows(dbf));
    }

    /// <summary>
    /// ⛔ جدولِ آرشیوِ قرض‌دار (‎RowsJson‎) هم بردگی و الباقیِ ذخیره‌شده دارد و سربرگِ
    /// آرشیو «برد» را از همان جمع می‌زند. تا امروز سنجهٔ برابری به آن نمی‌رسید و صفحهٔ
    /// آرشیو ردیف‌ها را بی خوددرمانی نشان می‌داد — پس آرشیوِ کهنه عددِ کهنه چاپ می‌کرد.
    /// حالا ‎ArchiveRows‎ همان قاعدهٔ همیشگی را روی هر ردیف می‌زند (فقط در حافظه).
    /// </summary>
    [Fact]
    public void ArshiveKohne_RadifhayeAnHamDorostKhandeMishavand()
    {
        var json = System.Text.Json.JsonSerializer.Serialize(new List<DebtRow>
        {
            new() { DateShamsi = "1403/02/10", Fuel = FuelType.Petrol, Liters = 12.5m, PricePerLiter = 70.3m,
                    Bardagi = 878.75m, Rasid = 100, Albaqi = 0 },
            new() { DateShamsi = "1403/02/11", Fuel = FuelType.Diesel, ByMoney = true, Bardagi = 5000.5m, Rasid = 1000, Albaqi = 1 },
            new() { DateShamsi = "1403/02/12", Fuel = FuelType.Petrol, ByMoney = true, Bardagi = 0, Liters = 10, PricePerLiter = 70, Albaqi = 5 },
        });
        var rows = DebtorService.ArchiveRows(new DebtTableArchive { RowsJson = json });
        Assert.Equal(879m, rows[0].Bardagi);
        Assert.Equal(779m, rows[0].Albaqi);
        Assert.Equal(12.5m, rows[0].Liters);               //  ⛔ عددِ کاربر همان
        Assert.Equal(5001m, rows[1].Bardagi);
        Assert.Equal(4001m, rows[1].Albaqi);
        Assert.False(rows[2].ByMoney);                     //  پولیِ خراب ⇒ ردیفِ تیل
        Assert.Equal(700m, rows[2].Bardagi);
        Assert.Equal(700m, rows[2].Albaqi);
    }
}
