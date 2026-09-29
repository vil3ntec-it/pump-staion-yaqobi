using PumpYaqobi.Application.Localization;
using PumpYaqobi.Application.Security;
using PumpYaqobi.Application.Services;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;
using PumpYaqobi.Services.Data;
using Xunit;

namespace PumpYaqobi.Tests;

file sealed class NoRate2 : IUnionRateProvider
{ public decimal UnionRate(FuelType f) => 0m; }

/// <summary>
/// «مدت عضویت قرض‌داران بعضی را حساب می‌کند و بعضی را نه» (۱۴۰۵/۰۷/۱۶).
/// ریشه: راهِ سریعِ ‎MembershipAsync‎ فقط ردیف‌های ‎DateKey > 0‎ را می‌خواند و
/// ردیف‌های کهنه (پیش از آمدنِ آن ستون) ‎DateKey = 0‎ دارند. مرجع همیشه
/// ‎Shamsi.Key(DateShamsi)‎ است؛ این آزمون هر دو را روی دیتابیسِ واقعی برابر می‌خواهد.
/// </summary>
public class MembershipCountTests : IDisposable
{
    private readonly string _file =
        Path.Combine(Path.GetTempPath(), $"pump-member-{Guid.NewGuid():N}.db");

    public void Dispose() { try { if (File.Exists(_file)) File.Delete(_file); } catch { } }

    [Fact]
    public async Task HarGharzdarBaTarikh_Shemorde_Mishavad_HattaBaDateKeyeSefr()
    {
        var dbf = new PumpDbFactory(_file);
        dbf.EnsureReady();
        var session = new UserSession();
        session.SignIn(UserRole.Admin, "آزمون");
        var perm = new PermissionService(session);
        var calc = new DebtCalculationService(new NoRate2());
        var aging = new AgingService(calc);
        var member = new MembershipService();
        var tools = new ToolsDataService(dbf, perm, new TrashService(dbf, perm, session), aging,
                                         new StaffShortService(new WaraqService()),
                                         new MonthReportService(), member,
                                         new DebtSummaryService(aging, member));

        await using (var db = dbf.Create())
        {
            // الف) ردیف‌های تازه با کلید
            var a = new Debtor { Name = "الف", LegacyId = "t1" };
            a.MainAccount.FuelRows.Add(new DebtRow { DateShamsi = "1404/3/5", DateKey = 14040305 });
            // ب) فقط ردیف‌های کهنه — DateKey صفر
            var b = new Debtor { Name = "ب", LegacyId = "t2" };
            b.MainAccount.FuelRows.Add(new DebtRow { DateShamsi = "1402/1/10" });
            b.MainAccount.FuelRows.Add(new DebtRow { DateShamsi = "1403/2/2" });
            // ج) قدیمی‌ترین ردیف در دفترِ پولِ یک حسابِ فرعی، باز بی کلید
            var c = new Debtor { Name = "ج", LegacyId = "t3" };
            c.MainAccount.FuelRows.Add(new DebtRow { DateShamsi = "1404/1/1", DateKey = 14040101 });
            var sub = new DebtAccount { Name = "دکان", LegacySubId = "s1", Mode = LedgerMode.Money };
            sub.MoneyRows.Add(new DebtRow { DateShamsi = "1401/6/18", ByMoney = true });
            c.SubAccounts.Add(sub);
            // د) بی هیچ ردیفِ تاریخ‌دار
            var d = new Debtor { Name = "د", LegacyId = "t4" };
            db.Debtors.AddRange(a, b, c, d);
            await db.SaveChangesAsync();
        }

        var rows = (await tools.MembershipAsync()).ToDictionary(r => r.Person.Name);
        Assert.Equal("1404/3/5", rows["الف"].FromShamsi);
        Assert.Equal("1402/1/10", rows["ب"].FromShamsi);
        Assert.Equal("1401/6/18", rows["ج"].FromShamsi);
        Assert.Equal("", rows["د"].FromShamsi);
        Assert.True(rows["ب"].Days > 0 && rows["ج"].Days > rows["ب"].Days);
        Assert.Equal(-1, rows["د"].Days);
    }
}

/// <summary>
/// «صرافی خیلی کند باز می‌شود» (۱۴۰۵/۰۷/۱۶) — در برنامهٔ واقعی هر دورِ
/// همگام‌سازی حالِ خودش را ذخیره می‌کرد و همان ‎Version‎ را جلو می‌برد، پس ترمزِ
/// «داده عوض نشده» هیچ‌وقت نمی‌گرفت و هر بازگشت به بخش همه را از نو می‌ساخت.
/// </summary>
[CollectionDefinition(nameof(SyncBookkeepingVersionTests), DisableParallelization = true)]
public sealed class SyncBookkeepingSerial { }

/// <remarks>⚠️ ‎Version‎ ایستا است؛ این کلاس جدا و بی‌هم‌زمانی می‌دود تا ذخیرهٔ
/// آزمون‌های دیگر عددش را جابه‌جا نکند.</remarks>
[Collection(nameof(SyncBookkeepingVersionTests))]
public class SyncBookkeepingVersionTests : IDisposable
{
    private readonly string _file =
        Path.Combine(Path.GetTempPath(), $"pump-syncver-{Guid.NewGuid():N}.db");

    public void Dispose() { try { if (File.Exists(_file)) File.Delete(_file); } catch { } }

    [Fact]
    public void HaleHamgamSazi_Version_RaNemibarad_VaDadeMibarad()
    {
        var dbf = new PumpDbFactory(_file);
        dbf.EnsureReady();
        var store = new SyncStore(dbf);
        store.Update(x => x.LastPullAt = 1);          // ردیفِ حال ساخته شود

        var v0 = PumpYaqobi.Persistence.PumpDbContext.Version;
        store.Update(x => x.LastPullAt = 12345);
        store.Update(x => x.LastError = "x");
        Assert.Equal(v0, PumpYaqobi.Persistence.PumpDbContext.Version);

        using (var db = dbf.Create())
        {
            db.Debtors.Add(new Debtor { Name = "تازه", LegacyId = "v1" });
            db.SaveChanges();
        }
        Assert.True(PumpYaqobi.Persistence.PumpDbContext.Version > v0);
    }
}
