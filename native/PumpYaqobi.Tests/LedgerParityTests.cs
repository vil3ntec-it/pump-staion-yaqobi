using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PumpYaqobi.Application.Services;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;
using PumpYaqobi.Services.Data;
using Xunit;

namespace PumpYaqobi.Tests;

file sealed class NoRatesP : IUnionRateProvider
{ public decimal UnionRate(FuelType f) => 0m; }

/// <summary>
/// ══ شورا، الف۳ — عددهای مشتقِ ذخیره‌شده هرگز از ردیف‌ها جدا نمانند ══════════
///
/// دفترِ واقعیِ SQLite با پنجاه حساب؛ بردگی/الباقیِ ردیف‌ها و چهار رسیدِ حساب
/// عمداً خراب نوشته می‌شوند (همان ‎UPDATE‎ِ خامی که یک opِ کهنه یا نسخهٔ پیشین
/// می‌توانست بگذارد). سنجه همه را پیدا و درست می‌کند — و هیچ عددِ کاربری
/// (لیتر، فی، رسیدِ ردیف) عوض نمی‌شود.
/// ⚠️ این کلاس ‎AppSettings‎ و ‎AppHost‎ را لمس نمی‌کند.
/// </summary>
public class LedgerParityTests : IDisposable
{
    private readonly string _file = Path.Combine(Path.GetTempPath(), $"pump-parity-{Guid.NewGuid():N}.db");

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

    private record UserNumbers(long Id, decimal Liters, decimal? Price, decimal Rasid, decimal RasidFuel);

    private static List<UserNumbers> Users(PumpDbFactory dbf)
    {
        using var db = dbf.Create();
        return db.DebtRows.AsNoTracking().OrderBy(r => r.Id)
                 .Select(r => new UserNumbers(r.Id, r.Liters, r.PricePerLiter, r.Rasid, r.RasidFuel)).ToList();
    }

    [Fact]
    public async Task PanjahHesab_KharabShode_HameDorostMishavand_VaRadifeKarbarDastNemikhorad()
    {
        var dbf = new PumpDbFactory(_file);
        dbf.EnsureReady();
        var calc = new DebtCalculationService(new NoRatesP());
        var parity = new LedgerParityService(dbf, calc);

        using (var db = dbf.Create())
        {
            for (var i = 0; i < 50; i++)
            {
                var d = new Debtor { LegacyId = "p" + i, Name = "مشتری " + i, MainAccount = new DebtAccount { Name = "مشتری " + i, ReceiptsMigrated = true } };
                for (var k = 0; k < 4; k++)
                {
                    var fuel = (k % 2 == 0) ? FuelType.Petrol : FuelType.Diesel;
                    d.MainAccount.FuelRows.Add(new DebtRow { DateShamsi = "1405/07/1" + k, Fuel = fuel,
                        Liters = 100 + i + k, PricePerLiter = 70.5m, RasidFuel = k * 3, Rasid = k * 1000 });
                    d.MainAccount.MoneyRows.Add(new DebtRow { DateShamsi = "1405/07/1" + k, Fuel = fuel, ByMoney = true,
                        Bardagi = 5000 + i, Rasid = 200 * k });
                }
                foreach (var r in d.MainAccount.FuelRows.Concat(d.MainAccount.MoneyRows)) calc.NormalizeRow(r);
                await RasidOf(d.MainAccount);
                db.Debtors.Add(d);
            }
            db.SaveChanges();
        }

        //  پیش از خراب کردن: هیچ ناجوری نیست
        Assert.Empty(await parity.CheckAsync(fix: false));
        var userBefore = Users(dbf);

        //  خراب کردنِ عمدیِ عددهای مشتق — فقط همان‌ها
        Raw("UPDATE DebtRows SET Albaqi = Albaqi + 7 WHERE Id % 3 = 0");
        Raw("UPDATE DebtRows SET Bardagi = 1 WHERE Id % 5 = 0 AND ByMoney = 0");
        Raw("UPDATE DebtAccounts SET RasidFuelPetrol = 999999 WHERE Id % 2 = 0");
        Raw("UPDATE DebtAccounts SET RasidMoneyDiesel = -4 WHERE Id % 7 = 0");

        var seen = await parity.CheckAsync(fix: false);
        Assert.True(seen.Count > 60, "ناجورها دیده شدند: " + seen.Count);
        Assert.Contains(seen, m => m.Field == "Albaqi");
        Assert.Contains(seen, m => m.Field == "Bardagi");
        Assert.Contains(seen, m => m.Field == "RasidFuelPetrol" && m.Stored == 999999m);
        //  «فقط ببین» چیزی ننوشت
        Assert.Equal(seen.Count, (await parity.CheckAsync(fix: false)).Count);

        var fixedList = await parity.CheckAsync(fix: true);
        Assert.Equal(seen.Count, fixedList.Count);
        Assert.Empty(await parity.CheckAsync(fix: false));

        //  ⛔ هیچ عددِ کاربری عوض نشد
        Assert.Equal(userBefore, Users(dbf));
        using (var db = dbf.Create())
            Assert.True(db.Audit.Count(a => a.Action == "parity.fix") > 0);

        //  روزی یک بار: بارِ دوم همان روز هیچ کاری نمی‌کند
        Raw("UPDATE DebtRows SET Albaqi = 1 WHERE Id = 1");
        Assert.Equal(1, await parity.DailyOnceAsync());
        Raw("UPDATE DebtRows SET Albaqi = 1 WHERE Id = 1");
        Assert.Equal(0, await parity.DailyOnceAsync());
    }

    private static Task RasidOf(DebtAccount a)
    {
        decimal S(IEnumerable<DebtRow> rs, FuelType f, bool m) => rs.Where(r => r.Fuel == f).Sum(r => m ? r.Rasid : r.RasidFuel);
        a.RasidFuelPetrol = S(a.FuelRows, FuelType.Petrol, false);
        a.RasidFuelDiesel = S(a.FuelRows, FuelType.Diesel, false);
        a.RasidMoneyPetrol = S(a.MoneyRows, FuelType.Petrol, true);
        a.RasidMoneyDiesel = S(a.MoneyRows, FuelType.Diesel, true);
        return Task.CompletedTask;
    }

    /// <summary>حسابِ مهاجرت‌نکرده: چهار عددِ حساب هنوز منبع‌اند و دست نمی‌خورند.</summary>
    [Fact]
    public async Task HesabeMohajeratNakarde_ChaharAdadDastNemikhorad()
    {
        var dbf = new PumpDbFactory(_file);
        dbf.EnsureReady();
        var parity = new LedgerParityService(dbf, new DebtCalculationService(new NoRatesP()));
        using (var db = dbf.Create())
        {
            db.Debtors.Add(new Debtor { LegacyId = "old", Name = "کهنه", MainAccount = new DebtAccount { Name = "کهنه", ReceiptsMigrated = false, RasidFuelPetrol = 500 } });
            db.SaveChanges();
        }
        Assert.Empty(await parity.CheckAsync(fix: true));
        using var db2 = dbf.Create();
        Assert.Equal(500m, db2.DebtAccounts.Single().RasidFuelPetrol);
    }
}
