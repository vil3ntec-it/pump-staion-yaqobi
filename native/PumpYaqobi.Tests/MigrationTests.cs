using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PumpYaqobi.Application.Services;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;
using PumpYaqobi.Infrastructure.Migration;
using PumpYaqobi.Persistence;
using Xunit;

namespace PumpYaqobi.Tests;

file sealed class R56 : IUnionRateProvider
{ public decimal UnionRate(FuelType f) => f == FuelType.Diesel ? 60m : 56m; }

/// <summary>
/// ══ آزمونِ مهاجرت — سخت‌گیرانه‌ترین آزمونِ این پروژه ═══════════════════════
/// یک بکاپِ واقعی از خودِ برنامهٔ HTML گرفته شد (۱۲ قرض‌دار با حساب‌های فرعی،
/// دو دفترِ تیل و پول، فیصدی‌های جدا، گاوصندوق، صرافی، مصارف و چکنه).
///
/// این آزمون آن را وارد SQLite می‌کند و بعد الباقی و حالِ هر قرض‌دار را از روی
/// دیتابیس دوباره حساب می‌کند و با عددی که *خودِ برنامهٔ HTML* داده بود مقایسه
/// می‌کند. یعنی مهاجرت و منطق، هر دو با هم، عدد‌به‌عدد سنجیده می‌شوند.
/// اگر ذره‌ای از داده در راه گم شود، این آزمون قرمز می‌شود.
/// </summary>
public class MigrationTests : IDisposable
{
    private readonly string _file = Path.Combine(Path.GetTempPath(), $"pump-mig-{Guid.NewGuid():N}.db");
    public void Dispose() { try { if (File.Exists(_file)) File.Delete(_file); } catch { } }

    private PumpDbContext Open()
    {
        var db = new PumpDbContext(new DbContextOptionsBuilder<PumpDbContext>()
            .UseSqlite($"Data Source={_file}").Options);
        db.Database.EnsureCreated();
        return db;
    }

    private static string Path2(string n)
    {
        var p = System.IO.Path.Combine(AppContext.BaseDirectory, n);
        return File.Exists(p) ? p : n;
    }

    private sealed record Expected(string id, double money, double petrol, double diesel, double fuel, string st);
    private sealed record ExpectedRoot(List<Expected> perPerson);

    [Fact]
    public void Import_MovesEverythingAndKeepsTheNumbersIdentical()
    {
        var json = File.ReadAllText(Path2("sample-backup.json"));
        var expected = JsonSerializer.Deserialize<ExpectedRoot>(File.ReadAllText(Path2("sample-backup-expected.json")))!;

        var (debtors, safe, exch, exp, ret, rep) = new LegacyBackupImporter().Parse(json);

        // ۱) هیچ چیزی در راه گم نشده باشد
        Assert.Empty(rep.Warnings);
        Assert.Equal(13, rep.Debtors);          // ۱۲ قرض‌دار + ۱ بی‌فاکتور
        Assert.Equal(40, rep.SafeEntries);
        Assert.Equal(30, rep.ExchangeRows);
        Assert.Equal(25, rep.Expenses);
        Assert.Equal(20, rep.RetailRows);
        Assert.True(rep.SubAccounts > 0, "حساب‌های فرعی باید منتقل شده باشند");
        Assert.True(rep.DebtRows > 100, "ردیف‌ها باید منتقل شده باشند");

        // ۲) واقعاً در SQLite بنشیند
        using (var db = Open())
        {
            db.Debtors.AddRange(debtors);
            db.SafeEntries.AddRange(safe);
            db.ExchangeRows.AddRange(exch);
            db.Expenses.AddRange(exp);
            db.RetailRows.AddRange(ret);
            db.SaveChanges();
        }

        // ۳) از روی *دیتابیس* دوباره حساب کن و با عددِ خودِ HTML بسنج
        using (var db = Open())
        {
            var svc = new DebtCalculationService(new R56());
            var loaded = db.Debtors
                .Include(d => d.MainAccount).ThenInclude(a => a.FuelRows)
                .Include(d => d.MainAccount).ThenInclude(a => a.MoneyRows)
                .Include(d => d.SubAccounts).ThenInclude(a => a.FuelRows)
                .Include(d => d.SubAccounts).ThenInclude(a => a.MoneyRows)
                .ToList();

            Assert.Equal(13, loaded.Count);

            foreach (var e in expected.perPerson)
            {
                var p = loaded.Single(x => x.LegacyId == e.id);
                var b = svc.Balances(p.AllAccounts());
                Assert.Equal((decimal)e.money,  b.Money,  2);
                Assert.Equal((decimal)e.petrol, b.Petrol, 2);
                Assert.Equal((decimal)e.diesel, b.Diesel, 2);
                Assert.Equal((decimal)e.fuel,   b.Fuel,   2);

                var st = svc.Status(p.AllAccounts());
                var want = e.st switch
                {
                    "out" => DebtStatus.Out, "low" => DebtStatus.Low,
                    "ok" => DebtStatus.Ok, _ => DebtStatus.None
                };
                Assert.Equal(want, st.Worst);
            }
        }
    }

    [Fact]
    public void Import_DerivesDateKeysAndMonthKeys()
    {
        Assert.Equal(14050614, LegacyBackupImporter.DateKeyOf("1405/6/14"));
        Assert.Equal(14050614, LegacyBackupImporter.DateKeyOf("۱۴۰۵/۶/۱۴"));   // ارقام فارسی
        Assert.Equal(0, LegacyBackupImporter.DateKeyOf(null));
        Assert.Equal(0, LegacyBackupImporter.DateKeyOf("چرند"));
        Assert.Equal("1405/06", LegacyBackupImporter.MonthKeyOf("1405/6/14"));
        // ⚠️ روزِ تک‌رقمی باید کوچک‌تر از دورقمی باشد — همان باگی که مرتب‌سازیِ
        // رشته‌ایِ HTML داشت و ردیف‌ها را جابه‌جا می‌کرد.
        Assert.True(LegacyBackupImporter.DateKeyOf("1405/6/5") < LegacyBackupImporter.DateKeyOf("1405/6/12"));
    }

    [Fact] // دادهٔ خراب کلِ مهاجرت را نشکند
    public void Import_SkipsBrokenRecordsInsteadOfFailing()
    {
        var json = """
        { "debtPersons":[ {"id":"ok","name":"سالم","rows":[{"date":"1405/1/1","fuel":5,"ftype":"petrol"}]},
                          "این یک رشته است نه شیء" ],
          "safeEntries":[ {"date":"1405/1/1","type":"bardagi","amount":"۱۲۳"} ] }
        """;
        var (debtors, safe, _, _, _, rep) = new LegacyBackupImporter().Parse(json);
        Assert.Single(debtors);                 // سالم رد نشد
        Assert.Single(safe);
        Assert.NotEmpty(rep.Warnings);          // ولی خراب گزارش شد
    }
}
