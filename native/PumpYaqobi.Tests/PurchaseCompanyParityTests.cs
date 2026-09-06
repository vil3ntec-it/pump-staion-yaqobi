using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PumpYaqobi.Application.Security;
using PumpYaqobi.Application.Services;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;
using PumpYaqobi.Services.Data;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ خریدِ مخزن ← حسابِ شرکت ═════════════════════════════════════════════════
///
/// دو چیز این‌جا سنجیده می‌شود، هر دو با دادهٔ گرفته‌شده از خودِ
/// ‎index.html‎ در یک کرومیومِ واقعی:
///
///   ۱. ‎_findCompanyByName‎ — چهار پلهٔ تطبیقِ نام. یک اشتباه این‌جا یعنی
///      خریدِ یک شرکت در حسابِ شرکتِ دیگری می‌نشیند، یا شرکتِ تکراری ساخته
///      می‌شود. هر دو را صاحب ریپو صریحاً ممنوع کرده.
///
///   ۲. ‎syncPurchaseToCompany‎ — ردیفی که در حساب می‌نشیند، و این‌که در
///      نخستین ردیفِ **خالی** می‌نشیند نه با بازنویسیِ ردیفِ پُر.
/// </summary>
public class PurchaseCompanyParityTests : IDisposable
{
    private sealed record MatchCase(string q, string? name);

    private sealed record Row(string name, string date, double ton, double usd,
                              double rate, double poul);

    private sealed record SyncCase(
        string seller, string fuel, string date, double kg, double density,
        double priceTon, double usdRate, double ton, double liters,
        double totalUSD, double totalAFN, double perLiter,
        List<bool> preBlanks, bool ok, int landedAt, int rowCount,
        Row? row, List<string> survivors);

    private sealed record Golden(List<MatchCase> match, List<SyncCase> sync);

    private static Golden Load()
    {
        var p = Path.Combine(AppContext.BaseDirectory, "golden-purchase-company.json");
        if (!File.Exists(p)) p = "golden-purchase-company.json";
        return JsonSerializer.Deserialize<Golden>(File.ReadAllText(p))!;
    }

    private static void Close(double expected, decimal actual, string what)
    {
        var a = (double)actual;
        var tol = Math.Max(1e-6, Math.Abs(expected) * 1e-9);
        Assert.True(Math.Abs(expected - a) <= tol, $"{what}: انتظار {expected} بود، {a} آمد");
    }

    private readonly string _file =
        Path.Combine(Path.GetTempPath(), $"pump-buy-{Guid.NewGuid():N}.db");

    public void Dispose() { try { if (File.Exists(_file)) File.Delete(_file); } catch { } }

    private (StorageDataService Storage, CompanyDataService Companies, PumpDbFactory Db) Host()
    {
        var dbf = new PumpDbFactory(_file);
        dbf.EnsureReady();
        var session = new UserSession();
        session.SignIn(UserRole.Admin, "آزمون");
        var perm = new PermissionService(session);
        var trash = new TrashService(dbf, perm, session);
        var settings = new SettingsService(dbf, perm);
        var companies = new CompanyDataService(dbf, perm, trash);
        var storage = new StorageDataService(dbf, perm, trash, new StorageService(), settings, companies);
        return (storage, companies, dbf);
    }

    // ── ۱) تطبیقِ نامِ شرکت ────────────────────────────────────────────────
    [Fact]
    public void FindingACompanyByName_MatchesTheHtmlOnEveryTier()
    {
        var names = new[]
        {
            "ح قادر", "ح قادر و شیر آقا", "شرکت نفت هرات", "نفت هرات",
            "محمد يوسف", "محمد یوسف زاده", "كابل تيل", "کابل تیل",
            "برادران احمدی", "احمدی", "الفت", "شرکت الفت جنوب",
        };
        var companies = names.Select(n => new TilCompany { Name = n }).ToList();

        foreach (var c in Load().match)
        {
            var hit = CompanyDataService.FindByName(companies, c.q);
            Assert.Equal(c.name, hit?.Name);
        }
    }

    /// <summary>
    /// دو پلهٔ میانی عمداً خلافِ هم‌اند و آسان است که کسی «مرتبشان کند» و
    /// خرابشان کند: در «شروع می‌شود» کوتاه‌ترین برنده است (خاص‌ترین)، ولی در
    /// «زیررشته» بلندترین.
    /// </summary>
    [Fact]
    public void ShortestWinsOnPrefix_ButLongestWinsOnSubstring()
    {
        var list = new List<TilCompany>
        {
            new() { Name = "ح قادر" },
            new() { Name = "ح قادر و شیر آقا" },
        };
        // «ح قادر» تطابقِ دقیق است
        Assert.Equal("ح قادر", CompanyDataService.FindByName(list, "ح قادر")?.Name);
        // «ح قادر و» فقط با نامِ بلند شروع می‌شود
        Assert.Equal("ح قادر و شیر آقا", CompanyDataService.FindByName(list, "ح قادر و")?.Name);
        // «قادر» زیررشتهٔ هر دو است — بلندترین برنده
        Assert.Equal("ح قادر و شیر آقا", CompanyDataService.FindByName(list, "قادر")?.Name);
    }

    /// <summary>یک نویسه هرگز تطبیق نمی‌دهد، وگرنه «ح» به هر چیزی می‌چسبید.</summary>
    [Theory]
    [InlineData("ح")]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void OneLetterNeverMatches(string? q)
        => Assert.Null(CompanyDataService.FindByName(
               new[] { new TilCompany { Name = "ح قادر" } }, q));

    // ── ۲) نشستنِ خرید در حسابِ شرکت ───────────────────────────────────────
    [Fact]
    public async Task APurchaseLandsInTheCompanyAccountExactlyLikeTheHtml()
    {
        var (storage, companies, dbf) = Host();

        foreach (var c in Load().sync)
        {
            var fuel = c.fuel == "diesel" ? FuelType.Diesel : FuelType.Petrol;

            // حسابِ شرکت را با همان ردیف‌های پیشین بساز
            var company = await companies.AddAsync(c.seller);
            for (var i = 0; i < c.preBlanks.Count; i++)
            {
                var blank = c.preBlanks[i];
                await companies.SaveRowAsync(new CompanyRow
                {
                    CompanyId = company.Id,
                    Fuel = fuel,
                    SortIndex = i,
                    Name = blank ? "" : "قبلی" + i,
                    DateShamsi = blank ? "" : "1405/06/01",
                    Ton = blank ? 0m : 10m + i,
                    Usd = blank ? 0m : 500m,
                    Rate = blank ? 0m : 70m,
                });
            }

            var p = await storage.AddPurchaseAsync(new FuelPurchase
            {
                Fuel = fuel,
                DateShamsi = c.date,
                Seller = c.seller,
                Kg = (decimal)c.kg,
                Density = (decimal)c.density,
                PriceTon = (decimal)c.priceTon,
                UsdRate = (decimal)c.usdRate,
            });

            // فرمول‌های خودِ خرید
            Close(c.ton, p.Ton, "تن");
            Close(c.liters, p.Liters, "لیتر");
            Close(c.totalUSD, p.TotalUsd, "دالر");
            Close(c.totalAFN, p.TotalAfn, "افغانی");
            Close(c.perLiter, p.PerLiter, "فی لیتر");

            await using var db = dbf.Create();
            var rows = await db.CompanyRows.AsNoTracking()
                               .Where(r => r.CompanyId == company.Id && r.Fuel == fuel)
                               .OrderBy(r => r.SortIndex).ThenBy(r => r.Id).ToListAsync();

            Assert.Equal(c.rowCount, rows.Count);

            var landed = rows.SingleOrDefault(r => r.SourcePurchaseId == p.LegacyId);
            Assert.NotNull(landed);
            Assert.Equal(c.landedAt, rows.IndexOf(landed!));

            // ⚠️ همان خانه‌هایی که یک‌بار سرِ راه گم می‌شدند
            Assert.Equal(c.row!.name, landed!.Name);
            Assert.Equal(c.row.date, landed.DateShamsi);
            Close(c.row.ton, landed.Ton, "تنِ ردیف");
            Close(c.row.usd, landed.Usd, "فیِ تنِ ردیف");
            Close(c.row.rate, landed.Rate, "نرخِ دالرِ ردیف");
            Close(c.row.poul, landed.Poul, "پولِ ردیف");

            // ردیف‌های پُرِ پیشین دست‌نخورده مانده‌اند
            var survivors = rows.Where(r => (r.Name ?? "").StartsWith("قبلی"))
                                .Select(r => r.Name!).ToList();
            Assert.Equal(c.survivors, survivors);

            await companies.DeleteAsync(company.Id);
        }
    }

    /// <summary>
    /// همان فروشنده دو بار → یک شرکت، دو ردیف. شرکتِ تکراری ساخته نمی‌شود
    /// حتی اگر نام با ی/ك عربی یا فاصلهٔ اضافه نوشته شده باشد.
    /// </summary>
    [Fact]
    public async Task TheSameSellerNeverCreatesASecondCompany()
    {
        var (storage, companies, _) = Host();

        foreach (var name in new[] { "كابل تيل", "کابل تیل", "کابل   تیل" })
            await storage.AddPurchaseAsync(new FuelPurchase
            {
                Fuel = FuelType.Petrol, DateShamsi = "1405/06/10", Seller = name,
                Kg = 1000m, Density = 0.8m, PriceTon = 500m, UsdRate = 70m,
            });

        var all = await companies.ListAsync();
        Assert.Single(all);
        Assert.Equal(3, all[0].Rows.Count(r => r.SourcePurchaseId != null));
    }

    /// <summary>
    /// ‎syncAllPurchasesToCompanies‎ — خریدهای جامانده وصل می‌شوند، ولی
    /// خریدی که دو بار وصل شود یعنی حسابِ شرکت دوبرابر شده.
    /// </summary>
    [Fact]
    public async Task BackfillLinksOrphansOnce_AndIsSafeToRunAgain()
    {
        var (storage, companies, dbf) = Host();

        // خریدِ جامانده: مستقیم در دیتابیس، بی گذر از AddPurchase
        await using (var db = dbf.Create())
        {
            db.FuelPurchases.Add(new FuelPurchase
            {
                Fuel = FuelType.Petrol, DateShamsi = "1405/06/09", DateKey = 14050609,
                Seller = "شرکت الفت جنوب", LegacyId = "fe-orphan",
                Kg = 2000m, Density = 0.8m, PriceTon = 400m, UsdRate = 70m,
                Ton = 2m, Liters = 2500m, TotalUsd = 800m, TotalAfn = 56000m, PerLiter = 22.4m,
            });
            await db.SaveChangesAsync();
        }

        Assert.Equal(1, await storage.SyncAllPurchasesToCompaniesAsync());
        Assert.Equal(0, await storage.SyncAllPurchasesToCompaniesAsync());   // بارِ دوم هیچ

        var all = await companies.ListAsync();
        var row = Assert.Single(all[0].Rows);
        Assert.Equal("fe-orphan", row.SourcePurchaseId);
        Assert.Equal(2m, row.Ton);
    }

    /// <summary>
    /// ⚠️ ردیفی که کاربر خودش از حساب شرکت پاک کرده نباید با هم‌گام‌سازیِ
    /// خودکار برگردد — وگرنه هرگز نمی‌شد از دستش خلاص شد.
    /// و خودِ خرید در بخشِ مخزن دست‌نخورده می‌ماند.
    /// </summary>
    [Fact]
    public async Task ARowTheUserDeletedDoesNotComeBack()
    {
        var (storage, companies, dbf) = Host();

        var p = await storage.AddPurchaseAsync(new FuelPurchase
        {
            Fuel = FuelType.Petrol, DateShamsi = "1405/06/10", Seller = "برادران احمدی",
            Kg = 1000m, Density = 0.8m, PriceTon = 500m, UsdRate = 70m,
        });

        var all = await companies.ListAsync();
        var row = Assert.Single(all[0].Rows);
        await companies.DeleteRowAsync(row.Id);

        Assert.Equal(0, await storage.SyncAllPurchasesToCompaniesAsync());
        Assert.Contains(p.LegacyId!, await companies.UnlinkedPurchasesAsync());

        await using var db = dbf.Create();
        Assert.Empty(await db.CompanyRows.AsNoTracking().ToListAsync());
        // خودِ خرید سرِ جایش
        Assert.Single(await db.FuelPurchases.AsNoTracking().ToListAsync());
    }

    /// <summary>حذفِ خرید از مخزن نباید ردیفِ حساب شرکت را ببرد.</summary>
    [Fact]
    public async Task DeletingAPurchaseLeavesTheCompanyRowAlone()
    {
        var (storage, companies, _) = Host();

        var p = await storage.AddPurchaseAsync(new FuelPurchase
        {
            Fuel = FuelType.Diesel, DateShamsi = "1405/06/10", Seller = "الفت",
            Kg = 1000m, Density = 0.8m, PriceTon = 500m, UsdRate = 70m,
        });
        await storage.DeletePurchaseAsync(p.Id);

        var all = await companies.ListAsync();
        Assert.Single(all[0].Rows);
    }

    /// <summary>
    /// فیِ خریدِ پطرول و دیزل دو کلیدِ کاملاً جدا هستند — «نباید Petrol و
    /// Diesel در یک نرخ ذخیره شوند».
    /// </summary>
    [Fact]
    public async Task PetrolAndDieselBuyPricesNeverShareAKey()
    {
        var (storage, _, dbf) = Host();
        var session = new UserSession();
        session.SignIn(UserRole.Admin, "آزمون");
        var settings = new SettingsService(dbf, new PermissionService(session));

        await storage.AddPurchaseAsync(new FuelPurchase
        {
            Fuel = FuelType.Petrol, DateShamsi = "1405/06/10", Seller = "",
            Kg = 1000m, Density = 1m, PriceTon = 1000m, UsdRate = 70m,
        });   // فی لیتر = 70

        await storage.AddPurchaseAsync(new FuelPurchase
        {
            Fuel = FuelType.Diesel, DateShamsi = "1405/06/10", Seller = "",
            Kg = 1000m, Density = 1m, PriceTon = 1000m, UsdRate = 50m,
        });   // فی لیتر = 50

        settings.Invalidate();
        Assert.Equal(70m, settings.GetDecimal(SettingsService.BuyPerLiterPetrol));
        Assert.Equal(50m, settings.GetDecimal(SettingsService.BuyPerLiterDiesel));
    }

    /// <summary>
    /// ‎_fuelStock‎ سه جزء دارد، نه دو: اصلاحِ میله‌زنی هم در موجودی شمرده
    /// می‌شود. بی آن، «برابر کردنِ دفتر با عددِ واقعی» هیچ اثری نداشت.
    /// </summary>
    [Fact]
    public void TankStockCountsTheDipCorrection()
    {
        var svc = new StorageService();
        var buys = new[] { new FuelPurchase { Liters = 1000m } };
        var reps = new[]
        {
            new ParchaReport { DayShift = new ShiftData { Sale = 300m } },
        };

        Assert.Equal(700m, svc.Tank(buys, reps, 100m).Current);

        // میله‌زنی گفت مخزن ۵۰ لیتر کمتر دارد و مدیر «دفتر برابر شود» را زد
        var dips = new[] { new TankDip { BookAdjust = -50m }, new TankDip { BookAdjust = 0m } };
        var t = svc.Tank(buys, reps, 100m, dips);
        Assert.Equal(650m, t.Current);
        Assert.Equal(-50m, t.DipAdjust);
    }

    /// <summary>
    /// هشدارِ کمبود فقط برای سوختی است که خریدی برایش ثبت شده، و «نزدیکِ حد»
    /// حالتِ جداست (تا ۲۰٪ بالای آستانه).
    /// </summary>
    [Fact]
    public void NearTheThresholdIsItsOwnState_AndNeedsAPurchase()
    {
        var svc = new StorageService();
        var empty = svc.Tank(Array.Empty<FuelPurchase>(), Array.Empty<ParchaReport>(), 100m);
        Assert.False(empty.HasPurchases);
        Assert.False(empty.IsNear);

        // ۱۱۵ لیتر با آستانهٔ ۱۰۰ → کم نیامده ولی نزدیک است
        var near = svc.Tank(new[] { new FuelPurchase { Liters = 115m } },
                            Array.Empty<ParchaReport>(), 100m);
        Assert.True(near.HasPurchases);
        Assert.False(near.IsLow);
        Assert.True(near.IsNear);

        // ۹۰ لیتر → کم آمده، و «نزدیک» دیگر معنا ندارد
        var low = svc.Tank(new[] { new FuelPurchase { Liters = 90m } },
                           Array.Empty<ParchaReport>(), 100m);
        Assert.True(low.IsLow);
        Assert.False(low.IsNear);
    }
}
