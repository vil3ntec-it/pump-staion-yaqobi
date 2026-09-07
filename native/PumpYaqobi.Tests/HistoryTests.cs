using Microsoft.EntityFrameworkCore;
using PumpYaqobi.Application.Security;
using PumpYaqobi.Application.Services;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;
using PumpYaqobi.Services.Data;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ تاریخچه‌ها ═════════════════════════════════════════════════════════════
/// ‎_hcFeed‎ / ‎renderHistoryCenter‎ / ‎renderHistoryView‎.
///
/// دو چیز باید درست باشد و بس: **کدام ردیف‌ها** می‌آیند و **با چه عددی**.
/// عددها از همان سرویس‌های محاسباتیِ خودِ برنامه می‌آیند، پس اگر این‌جا با
/// آن‌ها یکی نباشند یعنی تاریخچه دارد عددِ دومی می‌سازد — بدترین حالت.
/// </summary>
public class HistoryTests : IDisposable
{
    private readonly string _file =
        Path.Combine(Path.GetTempPath(), $"pump-hist-{Guid.NewGuid():N}.db");

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try { if (File.Exists(_file)) File.Delete(_file); } catch { }
    }

    private (HistoryService H, PumpDbFactory Db) Host()
    {
        var dbf = new PumpDbFactory(_file);
        dbf.EnsureReady();
        var session = new UserSession();
        session.SignIn(UserRole.Admin, "آزمون");
        var perm = new PermissionService(session);
        var settings = new SettingsService(dbf, perm);
        var trash = new TrashService(dbf, perm, session);
        var amanatData = new AmanatDataService(dbf, perm, trash, settings);
        return (new HistoryService(dbf, perm, new ExchangeService(), new RetailService(),
                                   new CompanyService(), new AmanatService(), amanatData), dbf);
    }

    /// <summary>سیزده کارت، به همان ترتیبِ ‎HC_KINDS‎.</summary>
    [Fact]
    public void TheThirteenSectionsAreTheSameOnesTheWebVersionHad()
    {
        var keys = HistoryService.Kinds.Select(k => k.Key).ToArray();
        Assert.Equal(new[]
        {
            "safe", "amanat", "sarrafi", "storage", "debt", "rasid", "chakana",
            "company", "expense", "waraq", "invoice", "shift", "attend",
        }, keys);
    }

    /// <summary>«بردگی» پولِ آمده است (سبز) و «ماندگی» پولِ رفته (نارنجی).</summary>
    [Fact]
    public async Task SafeRowsCarryTheirDirection()
    {
        var (h, dbf) = Host();
        await using (var db = dbf.Create())
        {
            db.SafeEntries.Add(new SafeEntry
            {
                DateShamsi = "1405/06/12", DateKey = 14050612, Kind = SafeEntryKind.Bardagi,
                Title = "فروشِ روز", Amount = 12000m,
            });
            db.SafeEntries.Add(new SafeEntry
            {
                DateShamsi = "1405/06/13", DateKey = 14050613, Kind = SafeEntryKind.Mandagi,
                Title = "پرداخت", Amount = 5000m,
            });
            await db.SaveChangesAsync();
        }

        var rows = await h.FeedAsync("safe");
        Assert.Equal(2, rows.Count);

        // از تازه به کهنه
        Assert.Equal("پرداخت", rows[0].Title);
        Assert.Equal("out", rows[0].Tone);
        Assert.Equal("فروشِ روز", rows[1].Title);
        Assert.Equal("in", rows[1].Tone);
        Assert.Equal(12000m, rows[1].Amount);
    }

    /// <summary>
    /// ردیفِ قرض‌دار باید بردگیِ درست و نامِ درستِ حساب را داشته باشد — و
    /// «واحد تیل» با «واحد پول» قاطی نشود.
    /// </summary>
    [Fact]
    public async Task DebtRowsShowTheAccountAndTheRightBardagi()
    {
        var (h, dbf) = Host();
        await using (var db = dbf.Create())
        {
            var p = new Debtor { Name = "کریم جان", LegacyId = "p1" };
            p.MainAccount.FuelRows.Add(new DebtRow
            {
                DateShamsi = "1405/06/12", DateKey = 14050612,
                Name = "بردگی", Liters = 40m, PricePerLiter = 80m,
            });
            p.SubAccounts.Add(new DebtAccount
            {
                Name = "موتر دوم", LegacySubId = "s1", Mode = LedgerMode.Money,
                MoneyRows =
                {
                    new DebtRow
                    {
                        DateShamsi = "1405/06/11", DateKey = 14050611,
                        Name = "قرضِ نقدی", ByMoney = true, Bardagi = 2500m,
                    },
                },
            });
            db.Debtors.Add(p);
            await db.SaveChangesAsync();
        }

        var rows = await h.FeedAsync("debt");
        Assert.Equal(2, rows.Count);

        var fuel = rows.Single(r => r.Title.Contains("بردگی"));
        Assert.Equal(3200m, fuel.Amount);                 // ۴۰ × ۸۰
        Assert.StartsWith("کریم جان", fuel.Title);
        Assert.DoesNotContain("›", fuel.Title);           // حسابِ اصلی، نه زیرحساب
        Assert.Contains("واحد تیل", fuel.Detail);

        var money = rows.Single(r => r.Title.Contains("قرضِ نقدی"));
        Assert.Equal(2500m, money.Amount);                // دستی، نه لیتر×فی
        Assert.Contains("کریم جان › موتر دوم", money.Title);
        Assert.Contains("واحد پول", money.Detail);
    }

    /// <summary>
    /// یک سطرِ صرافی می‌تواند دو ردیفِ تاریخچه بدهد — رسید و بردگی — و رسید
    /// باید همان دالری باشد که خودِ بخشِ صرافی نشان می‌دهد.
    /// </summary>
    [Fact]
    public async Task AnExchangeRowSplitsIntoReceiptAndBardagi()
    {
        var (h, dbf) = Host();
        var row = new ExchangeRow
        {
            DateShamsi = "1405/06/12", DateKey = 14050612, Description = "صرافیِ نور",
            Amount = 1000m, Rate = 50m, Bardagi = 4m, Currency = ExchangeCurrency.Toman,
        };
        await using (var db = dbf.Create()) { db.ExchangeRows.Add(row); await db.SaveChangesAsync(); }

        var rows = await h.FeedAsync("sarrafi");
        Assert.Equal(2, rows.Count);

        var expected = new ExchangeService().ToUsd(row);
        Assert.Equal(Math.Round(expected, 2), rows.Single(r => r.Tone == "in").Amount);
        Assert.Equal(4m, rows.Single(r => r.Tone == "out").Amount);
    }

    /// <summary>پارچه دو ردیف می‌دهد — روز و شب — نه یکی.</summary>
    [Fact]
    public async Task AParchaGivesOneRowPerShift()
    {
        var (h, dbf) = Host();
        await using (var db = dbf.Create())
        {
            db.Reports.Add(new ParchaReport
            {
                DateShamsi = "1405/06/12", DateKey = 14050612, ReportNum = 1,
                Fuel = FuelType.Petrol,
                DayShift = new ShiftData { Name = "احمد", Sale = 300m, Money = 24000m },
                NightShift = new ShiftData { Name = "محمود", Sale = 200m, Money = 16000m },
            });
            await db.SaveChangesAsync();
        }

        var rows = await h.FeedAsync("shift");
        Assert.Equal(2, rows.Count);
        Assert.Contains(rows, r => r.Detail.Contains("احمد") && r.Amount == 24000m);
        Assert.Contains(rows, r => r.Detail.Contains("محمود") && r.Amount == 16000m);
    }

    /// <summary>کارت‌ها شمار و تازه‌ترین تاریخِ هر بخش را درست می‌دهند.</summary>
    [Fact]
    public async Task TheCardsCountRowsAndNameTheNewestDate()
    {
        var (h, dbf) = Host();
        await using (var db = dbf.Create())
        {
            db.Expenses.Add(new Expense { DateShamsi = "1405/06/10", DateKey = 14050610, Title = "برق", Amount = 900m });
            db.Expenses.Add(new Expense { DateShamsi = "1405/06/14", DateKey = 14050614, Title = "نان", Amount = 300m });
            await db.SaveChangesAsync();
        }

        var cards = await h.CardsAsync();
        Assert.Equal(HistoryService.Kinds.Length, cards.Count);

        var exp = cards.Single(c => c.Key == "expense");
        Assert.Equal(2, exp.Count);
        Assert.Equal("1405/06/14", exp.LatestDate);

        Assert.Equal(0, cards.Single(c => c.Key == "waraq").Count);
    }

    /// <summary>گروهِ ماه از کلیدِ تاریخ می‌آید — خوراکِ کشویِ «سال و ماه».</summary>
    [Fact]
    public void MonthKeyComesFromTheDateKey()
    {
        var withDate = new HistoryRow("safe", "1405/06/12", 14050612, "", "", null, "", "");
        Assert.Equal("1405/06", withDate.MonthKey);

        var noDate = new HistoryRow("safe", "", 0, "", "", null, "", "");
        Assert.Equal("", noDate.MonthKey);
    }

    /// <summary>بیننده می‌تواند ببیند؛ تاریخچه فقط خواندنی است.</summary>
    [Fact]
    public async Task AViewerCanReadTheHistory()
    {
        var dbf = new PumpDbFactory(_file);
        dbf.EnsureReady();
        var session = new UserSession();
        session.SignIn(UserRole.Viewer, "بیننده");
        var perm = new PermissionService(session);
        var h = new HistoryService(dbf, perm, new ExchangeService(), new RetailService(),
                                   new CompanyService(), new AmanatService(),
                                   new AmanatDataService(dbf, perm, new TrashService(dbf, perm, session),
                                                         new SettingsService(dbf, perm)));

        Assert.Empty(await h.FeedAsync("safe"));
    }
}
