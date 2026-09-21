using Microsoft.EntityFrameworkCore;
using PumpYaqobi.Application.Localization;
using PumpYaqobi.Application.Security;
using PumpYaqobi.Application.Services;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;
using PumpYaqobi.Services.Data;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ «ردیفِ خودکار کجا می‌نشیند» — هر پنج مسیر ═══════════════════════════════
///
/// گزارشِ صاحب ریپو: «از ورق به حسابِ طرف که اتومات می‌ره، می‌ره تهِ جدول در
/// حالی که کادرِ جدولِ اولی هم خالی است… توی گاوصندوق هم همین‌طور و برای
/// مصارف هم همین‌طور. حتی یک عدد هم اشتباه به حساب نره.»
///
/// قاعده یکی است و در <see cref="PostingService.IsBlankRow"/> نوشته شده:
/// نخستین ردیفِ خالی <b>از بالا</b>، وگرنه ردیفِ تازه ته جدول.
/// </summary>
public class AutoRowPlacementTests : IDisposable
{
    private readonly string _file =
        Path.Combine(Path.GetTempPath(), $"pump-arp-{Guid.NewGuid():N}.db");

    public void Dispose() { try { if (File.Exists(_file)) File.Delete(_file); } catch { } }

    // ══ ۱) قاعدهٔ «خالی» — بی دیتابیس ═══════════════════════════════════════

    /// <summary>
    /// ⛔ رسیدِ تیلِ سربرگ ردیفی می‌سازد که جز ‎RasidFuel‎ همه‌چیزش صفر است.
    /// اگر «خالی» شمرده شود، اولین ردیفِ خودکارِ ورق رویش می‌نشیند و رسیدِ
    /// تیلِ مشتری بی‌صدا پاک می‌شود — خرابیِ داده، نه کندی.
    /// </summary>
    [Fact]
    public void RasidFuel_MakesARowNotBlank()
    {
        Assert.True(PostingService.IsBlankRow(new DebtRow()));
        Assert.False(PostingService.IsBlankRow(new DebtRow { RasidFuel = 12m }));
        Assert.False(PostingService.IsBlankRow(new DebtRow { Rasid = 5m }));
        Assert.False(PostingService.IsBlankRow(new DebtRow { Name = "کریم" }));
    }

    /// <summary>ردیفی که کلیدِ منبع دارد مالِ منبعِ دیگری است، پس خالی نیست.</summary>
    [Fact]
    public void ARowWithASourceKeyIsNeverBlank()
        => Assert.False(PostingService.IsBlankRow(new DebtRow { SrcKey = "waraq|1" }));

    /// <summary>«اولی» یعنی بالاترین ردیفِ جدول، نه اولی در فهرستِ حافظه.</summary>
    [Fact]
    public void TheFirstBlankIsTheTopMostOne_NotTheFirstInTheList()
    {
        var rows = new List<DebtRow>
        {
            new() { SortIndex = 7 },
            new() { SortIndex = 2 },
            new() { SortIndex = 4, Name = "پُر" },
        };
        Assert.Equal(2, PostingService.FirstBlank(rows)!.SortIndex);
    }

    /// <summary>ردیفِ رسیدِ تیل دست‌نخورده می‌ماند و ردیفِ تازه کنارش می‌نشیند.</summary>
    [Fact]
    public void AFuelReceiptRowIsNotOverwritten()
    {
        var person = new Debtor { Name = "ولی", MainAccount = new DebtAccount { Name = "ولی" } };
        person.MainAccount.FuelRows.Add(new DebtRow { RasidFuel = 30m, SortIndex = 0 });

        PostingService.PlaceRow(person,
            new DebtRow { Name = "خرید", Liters = 50m, Bardagi = 3100m }, "", null);

        Assert.Equal(2, person.MainAccount.FuelRows.Count);
        Assert.Equal(30m, person.MainAccount.FuelRows[0].RasidFuel);
        Assert.Equal("خرید", person.MainAccount.FuelRows[1].Name);
    }

    // ══ ۲) ورق ⇒ حساب · مصارف · گاوصندوق — با دیتابیسِ واقعی ═════════════════

    private (WaraqPostingService Post, WaraqDataService Data, PumpDbFactory Db,
             StorageDataService Storage, CompanyDataService Companies) Host()
    {
        var dbf = new PumpDbFactory(_file);
        dbf.EnsureReady();
        var session = new UserSession();
        session.SignIn(UserRole.Admin, "آزمون");
        var perm = new PermissionService(session);
        var trash = new TrashService(dbf, perm, session);
        var settings = new SettingsService(dbf, perm);
        var calc = new WaraqService();
        var safe = new ShiftWaraqSyncService(dbf, perm, calc, settings);
        var companies = new CompanyDataService(dbf, perm, trash);
        return (new WaraqPostingService(dbf, perm, calc, safe),
                new WaraqDataService(dbf, perm, trash), dbf,
                new StorageDataService(dbf, perm, trash, new StorageService(), settings, companies),
                companies);
    }

    private const string Day = "1405/06/18";

    private static async Task<Debtor> PersonAsync(PumpDbFactory dbf, string name)
    {
        await using var db = dbf.Create();
        var p = new Debtor { Name = name, LegacyId = "p" + Guid.NewGuid().ToString("N")[..8] };
        p.MainAccount.Name = name;
        db.Debtors.Add(p);
        await db.SaveChangesAsync();
        return p;
    }

    /// <summary>ورقی با یک ردیف — «قرض» یا «مصرف».</summary>
    private static async Task<WaraqEntry> SheetAsync(WaraqDataService data, PumpDbFactory dbf,
                                                     string rowName, decimal liters,
                                                     WaraqTxnType type = WaraqTxnType.Debt)
    {
        var w = await data.OpenOrCreateAsync(Day, "پمپ یعقوبی");
        await using var db = dbf.Create();
        var shift = await db.WaraqShifts.Include(s => s.Transactions)
                            .FirstAsync(s => s.WaraqId == w.Id && s.Kind == ShiftKind.Day);
        shift.PricePerLiter = 50m;
        var t = shift.Transactions.OrderBy(x => x.SortIndex).First();
        t.Name = rowName;
        t.Liters = liters;
        t.Type = type;
        await db.SaveChangesAsync();
        return w;
    }

    /// <summary>یک پایه با فروشِ واقعی، تا «جملهٔ فروش» به گاوصندوق برود.</summary>
    private static async Task SalesAsync(PumpDbFactory dbf, long waraqId)
    {
        await using var db = dbf.Create();
        var shift = await db.WaraqShifts.Include(s => s.Pumps)
                            .FirstAsync(s => s.WaraqId == waraqId && s.Kind == ShiftKind.Day);
        var p = shift.Pumps.FirstOrDefault();
        if (p is null) { p = new WaraqPump { ShiftId = shift.Id, Num = 1 }; db.WaraqPumps.Add(p); }
        p.Fuel = FuelType.Petrol;
        p.Start = 0m; p.End = 100m; p.PricePerLiter = 50m;
        await db.SaveChangesAsync();
    }

    /// <summary>ردیفِ خالیِ دستیِ کاربر در دفترِ تیلِ حسابِ اصلی.</summary>
    private static async Task<long> BlankDebtRowAsync(PumpDbFactory dbf, long debtorId)
    {
        await using var db = dbf.Create();
        // ⚠️ حسابِ اصلی با ‎MainOfDebtorId‎ به شخص بند است، نه ‎DebtorId‎
        // (آن یکی مالِ حساب‌های فرعی است).
        var acct = await db.DebtAccounts.FirstAsync(a => a.MainOfDebtorId == debtorId);
        var r = new DebtRow { FuelAccountId = acct.Id, SortIndex = 0, DateShamsi = Day };
        db.DebtRows.Add(r);
        await db.SaveChangesAsync();
        return r.Id;
    }

    /// <summary>
    /// خواستهٔ اصلی: ردیفِ ورق در همان کادرِ خالیِ بالای جدول می‌نشیند، نه ته آن.
    /// </summary>
    [Fact]
    public async Task TheWaraqRowFillsTheAccountsFirstBlankRow()
    {
        var (post, data, dbf, _, _) = Host();
        var person = await PersonAsync(dbf, "محمد هارون");
        var blankId = await BlankDebtRowAsync(dbf, person.Id);

        var w = await SheetAsync(data, dbf, "هارون", 10m);
        await post.SyncAsync(w.Id);

        await using var db = dbf.Create();
        var rows = await db.DebtRows.AsNoTracking()
                           .Where(r => r.FuelAccountId != null).ToListAsync();
        var row = Assert.Single(rows);
        Assert.Equal(blankId, row.Id);          // همان ردیف، نه ردیفِ تازه
        Assert.Equal(500m, row.Bardagi);
        Assert.Equal(0, row.SortIndex);         // سرِ جایش ماند
    }

    /// <summary>و مصرفِ ورق هم در نخستین مصرفِ خالیِ همان ماه.</summary>
    [Fact]
    public async Task TheWaraqExpenseFillsTheFirstBlankExpenseOfTheMonth()
    {
        var (post, data, dbf, _, _) = Host();

        long blankId;
        await using (var db = dbf.Create())
        {
            var e = new Expense
            {
                DateShamsi = "1405/06/02",
                DateKey = Shamsi.Key("1405/06/02"),
                MonthKey = Shamsi.MonthKey("1405/06/02"),
            };
            db.Expenses.Add(e);
            await db.SaveChangesAsync();
            blankId = e.Id;
        }

        var w = await SheetAsync(data, dbf, "چای", 10m, WaraqTxnType.Expense);
        await post.SyncAsync(w.Id);

        await using var db2 = dbf.Create();
        var all = await db2.Expenses.AsNoTracking().ToListAsync();
        var one = Assert.Single(all);
        Assert.Equal(blankId, one.Id);
        Assert.Equal("چای", one.Title);
        Assert.Equal(500m, one.Amount);
    }

    /// <summary>
    /// ⛔ ردیفِ خالیِ ماهِ <b>دیگر</b> برداشته نمی‌شود — وگرنه از جدولِ ماهِ
    /// خودش ناپدید می‌شد.
    /// </summary>
    [Fact]
    public async Task TheSafeSalesRowNeverStealsABlankFromAnotherMonth()
    {
        var (post, data, dbf, _, _) = Host();

        long otherId;
        await using (var db = dbf.Create())
        {
            var e = new SafeEntry
            {
                DateShamsi = "1405/05/03",
                DateKey = Shamsi.Key("1405/05/03"),
                MonthKey = Shamsi.MonthKey("1405/05/03"),
            };
            db.SafeEntries.Add(e);
            await db.SaveChangesAsync();
            otherId = e.Id;
        }

        var w = await SheetAsync(data, dbf, "هارون", 0m);
        await SalesAsync(dbf, w.Id);
        await post.SyncAsync(w.Id);

        await using var db2 = dbf.Create();
        var other = await db2.SafeEntries.AsNoTracking().FirstAsync(e => e.Id == otherId);
        Assert.Equal("1405/05", other.MonthKey);       // دست نخورد
        Assert.Null(other.SrcKey);

        var sales = await db2.SafeEntries.AsNoTracking()
                             .Where(e => e.SrcKey != null).ToListAsync();
        Assert.Single(sales);
    }

    /// <summary>
    /// ⛔ و ارز صریح نوشته می‌شود: فروشِ ورق افغانی است، حتی اگر ردیفِ خالیِ
    /// برداشته‌شده روی دالر بوده باشد.
    /// </summary>
    [Fact]
    public async Task TheAdoptedSafeRowIsAlwaysInAfghani()
    {
        var (post, data, dbf, _, _) = Host();

        long blankId;
        await using (var db = dbf.Create())
        {
            var e = new SafeEntry
            {
                DateShamsi = Day,
                DateKey = Shamsi.Key(Day),
                MonthKey = Shamsi.MonthKey(Day),
                Currency = Currency.Usd,
            };
            db.SafeEntries.Add(e);
            await db.SaveChangesAsync();
            blankId = e.Id;
        }

        var w = await SheetAsync(data, dbf, "هارون", 0m);
        await SalesAsync(dbf, w.Id);
        await post.SyncAsync(w.Id);

        await using var db2 = dbf.Create();
        var row = Assert.Single(await db2.SafeEntries.AsNoTracking().ToListAsync());
        Assert.Equal(blankId, row.Id);                 // همان خانهٔ خالی پر شد
        Assert.Equal(Currency.Afn, row.Currency);      // و افغانی شد
        Assert.Equal(5000m, row.Amount);
    }

    // ══ ۳) مخزن ⇒ حسابِ شرکت ════════════════════════════════════════════════

    private static FuelPurchase Buy(string seller, decimal ton, FuelType fuel = FuelType.Petrol) =>
        new()
        {
            Seller = seller,
            Fuel = fuel,
            DateShamsi = Day,
            Ton = ton,
            Kg = ton * 1000m,
            PriceTon = 700m,
            UsdRate = 70m,
            Density = 0.75m,
        };

    private static async Task<List<CompanyRow>> CompanyRowsAsync(PumpDbFactory dbf)
    {
        await using var db = dbf.Create();
        return await db.CompanyRows.AsNoTracking()
                       .Where(r => r.SourcePurchaseId != null).ToListAsync();
    }

    /// <summary>ویرایشِ خرید باید به حسابِ شرکت هم برسد — سرِ همان ردیف.</summary>
    [Fact]
    public async Task EditingAPurchaseUpdatesTheCompanyRowInPlace()
    {
        var (_, _, dbf, storage, _) = Host();
        var p = await storage.AddPurchaseAsync(Buy("شرکتِ الف", 10m));

        var before = Assert.Single(await CompanyRowsAsync(dbf));
        Assert.Equal(10m, before.Ton);

        p.Ton = 25m;
        p.Kg = 25000m;
        await storage.UpdatePurchaseAsync(p);

        var after = Assert.Single(await CompanyRowsAsync(dbf));
        Assert.Equal(before.Id, after.Id);        // همان ردیف، نه ردیفِ تازه
        Assert.Equal(25m, after.Ton);
    }

    /// <summary>
    /// عوض شدنِ فروشنده ردیف را جابه‌جا می‌کند — وگرنه همان پول در حسابِ
    /// شرکتِ قبلی هم می‌ماند و دو بار شمرده می‌شد.
    /// </summary>
    [Fact]
    public async Task ChangingTheSellerMovesTheRowAndLeavesNothingBehind()
    {
        var (_, _, dbf, storage, companies) = Host();
        var p = await storage.AddPurchaseAsync(Buy("شرکتِ الف", 10m));

        p.Seller = "شرکتِ ب";
        await storage.UpdatePurchaseAsync(p);

        var rows = await CompanyRowsAsync(dbf);
        Assert.Single(rows);

        var list = await companies.ListAsync();
        var alef = list.First(c => c.Name == "شرکتِ الف");
        var be = list.First(c => c.Name == "شرکتِ ب");
        Assert.Equal(be.Id, rows[0].CompanyId);
        Assert.NotEqual(alef.Id, rows[0].CompanyId);
    }

    /// <summary>
    /// ⛔ حذفِ خرید از مخزن، ردیفِ حسابِ شرکت را <b>نمی‌برد</b> — همان
    /// چیزی که نسخهٔ وب در پرسشِ پیش از حذف صریح به کاربر می‌گوید.
    ///
    /// ⚠️ یک بار برعکسش نوشته شد و سنجهٔ خودِ ریپو گرفتش — دفترِ شرکت
    /// حسابِ دادوستد است، نه آینهٔ فهرستِ مخزن؛ برداشتنِ خودکارش یعنی
    /// بدهیِ واقعیِ شرکت بی‌خبر کم شود. راهِ کاربر همان حذفِ دستیِ همان ردیف است.
    /// </summary>
    [Fact]
    public async Task DeletingAPurchaseLeavesItsCompanyRowAlone()
    {
        var (_, _, dbf, storage, _) = Host();
        var p = await storage.AddPurchaseAsync(Buy("شرکتِ الف", 10m));
        Assert.Single(await CompanyRowsAsync(dbf));

        await storage.DeletePurchaseAsync(p.Id);

        Assert.Single(await CompanyRowsAsync(dbf));
    }

    /// <summary>عوض شدنِ نوعِ تیل هم ردیف را به دفترِ درست می‌برد.</summary>
    [Fact]
    public async Task ChangingTheFuelMovesTheRowToTheOtherLedger()
    {
        var (_, _, dbf, storage, _) = Host();
        var p = await storage.AddPurchaseAsync(Buy("شرکتِ الف", 10m));
        Assert.Equal(FuelType.Petrol, (await CompanyRowsAsync(dbf))[0].Fuel);

        p.Fuel = FuelType.Diesel;
        await storage.UpdatePurchaseAsync(p);

        var row = Assert.Single(await CompanyRowsAsync(dbf));
        Assert.Equal(FuelType.Diesel, row.Fuel);
    }
}
