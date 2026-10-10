using Microsoft.EntityFrameworkCore;
using PumpYaqobi.App.Services;
using PumpYaqobi.Application.Localization;
using PumpYaqobi.Domain;
using PumpYaqobi.Domain.Entities;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ شورا، ج۶ — تاریخ یک شکلِ کانونی ═════════════════════════════════════════
///
/// ⛔ کلیدهای تاریخ (<c>DateKey</c> و <c>MonthKey</c>) با هر ذخیره از <b>متنِ همان
/// ردیف</b> ساخته می‌شوند — یک قاعده (<see cref="DateKeys"/>)، یک جا
/// (<c>PumpDbContext</c>) — و سنجهٔ برابریِ روزانه ردیف‌های کهنه را درست می‌کند.
/// متنِ کاربر هرگز عوض نمی‌شود.
/// </summary>
public class DateKeyCanonTests
{
    private static AppHost Host()
    {
        var host = new AppHost(Path.Combine(Path.GetTempPath(),
            "pump-dkc-" + Guid.NewGuid().ToString("N"), "pump.db"));
        if (host.Auth.NeedsFirstRun()) host.Auth.CreateFirstAdmin("1234");
        host.Auth.SignIn("admin", "1234");
        return host;
    }

    [Theory]
    [InlineData("1405/07/20", 14050720)]
    [InlineData("۱۴۰۵/۰۷/۲۰", 14050720)]
    [InlineData("1405-7-3", 14050703)]
    [InlineData("1405/13/01", 0)]
    [InlineData("", 0)]
    [InlineData(null, 0)]
    [InlineData("دیروز", 0)]
    public void YekGhaede_HamanShamsiKey(string? text, int key)
    {
        Assert.Equal(key, DateKeys.Key(text));
        Assert.Equal(key, Shamsi.Key(text));                         //  همان قاعده، نه نسخهٔ دوم
        Assert.Equal(key == 0 ? "" : $"{key / 10000:0000}/{key / 100 % 100:00}", Shamsi.MonthKey(text));
    }

    /// <summary>⛔ راهی که کلید را ننوشت، ردیفِ بی‌ماه نمی‌گذارد — ذخیره خودش می‌سازد.</summary>
    [Fact]
    public async Task Zakhire_KelidRa_AzMatneRadif_Misazad()
    {
        var host = Host();
        long id;
        await using (var db = host.Db.Create())
        {
            var e = new Expense { DateShamsi = "۱۴۰۵/۰۳/۱۰", DateKey = 0, MonthKey = null, Title = "x", Amount = 5m };
            db.Expenses.Add(e);
            await db.SaveChangesAsync();
            id = e.Id;
        }
        await using (var db = host.Db.Create())
        {
            var e = await db.Expenses.AsNoTracking().SingleAsync(x => x.Id == id);
            Assert.Equal(14050310, e.DateKey);
            Assert.Equal("1405/03", e.MonthKey);
            Assert.Equal("۱۴۰۵/۰۳/۱۰", e.DateShamsi);                //  متنِ کاربر دست نخورد

            //  ویرایشِ متن ⇒ کلید و ماه همراهش
            var t = await db.Expenses.SingleAsync(x => x.Id == id);
            t.DateShamsi = "1405/04/02";
            await db.SaveChangesAsync();
        }
        await using (var db = host.Db.Create())
        {
            var e = await db.Expenses.AsNoTracking().SingleAsync(x => x.Id == id);
            Assert.Equal(14050402, e.DateKey);
            Assert.Equal("1405/04", e.MonthKey);
        }
    }

    /// <summary>⚠️ متنِ خالی یا ناخوانا کلیدِ موجود را پاک نمی‌کند.</summary>
    [Fact]
    public async Task MatneNakhana_KelideMojud_RaNegahMidarad()
    {
        var host = Host();
        await using var db = host.Db.Create();
        var e = new Expense { DateShamsi = "", DateKey = 14040101, MonthKey = "1404/01", Title = "y" };
        db.Expenses.Add(e);
        await db.SaveChangesAsync();
        var back = await db.Expenses.AsNoTracking().SingleAsync(x => x.Id == e.Id);
        Assert.Equal(14040101, back.DateKey);
        Assert.Equal("1404/01", back.MonthKey);
    }

    /// <summary>
    /// ⛔ سنجهٔ برابریِ روزانه: ردیفِ کهنه‌ای که کلیدش ناجور است پیدا و درست
    /// می‌شود، بارِ دوم هیچ — و متنِ کاربر دست نمی‌خورد.
    /// </summary>
    [Fact]
    public async Task BarabariyeRoozane_KelideKohne_RaDorostMikonad()
    {
        var host = Host();
        long id, onlyMonth;
        await using (var db = host.Db.Create())
        {
            var e = new Expense { DateShamsi = "1405/05/09", Title = "z", Amount = 1m };
            var f = new Expense { DateShamsi = "1405/06/01", Title = "w", Amount = 1m };
            db.Expenses.AddRange(e, f);
            await db.SaveChangesAsync();
            (id, onlyMonth) = (e.Id, f.Id);
            //  همان ردیف‌های کهنه‌ای که راه‌های پیش از ج۶ گذاشته بودند — بی گذشتن از ذخیره:
            //  یکی بی کلید، یکی با کلیدِ درست ولی ماهِ غلط
            await db.Database.ExecuteSqlRawAsync(
                "UPDATE \"Expenses\" SET \"DateKey\" = 0, \"MonthKey\" = '1404/12' WHERE \"Id\" = {0}", id);
            await db.Database.ExecuteSqlRawAsync(
                "UPDATE \"Expenses\" SET \"MonthKey\" = '1405/05' WHERE \"Id\" = {0}", onlyMonth);
        }

        Assert.True(await host.Parity.CheckDatesAsync(fix: false) >= 2);
        Assert.True(await host.Parity.CheckDatesAsync(fix: true) >= 2);
        Assert.Equal(0, await host.Parity.CheckDatesAsync(fix: false));

        await using (var db = host.Db.Create())
        {
            var e = await db.Expenses.AsNoTracking().SingleAsync(x => x.Id == id);
            Assert.Equal(14050509, e.DateKey);
            Assert.Equal("1405/05", e.MonthKey);
            Assert.Equal("1405/05/09", e.DateShamsi);
            Assert.Equal("1405/06", (await db.Expenses.AsNoTracking().SingleAsync(x => x.Id == onlyMonth)).MonthKey);
        }
    }

    /// <summary>
    /// ⛔ جدول‌هایی که هم متن و کلید دارند و هم ماه — فهرستِ دقیق. جدولِ تازه‌ای که
    /// <c>MonthKey</c>ش معنای دیگری دارد (مثلِ ماهِ معاش) نباید بی‌صدا مشتق شود:
    /// همین‌جا سرخ می‌شود تا کسی آگاهانه تصمیم بگیرد.
    /// </summary>
    [Fact]
    public void JadvalhayeBaMah_FehresteDaghigh()
    {
        var host = Host();
        using var db = host.Db.Create();
        var withMonth = db.Model.GetEntityTypes()
            .Where(t => t.FindProperty("DateShamsi") is not null && t.FindProperty("DateKey") is not null
                        && t.FindProperty("MonthKey") is not null)
            .Select(t => t.ClrType.Name).OrderBy(n => n, StringComparer.Ordinal).ToArray();
        Assert.Equal(new[]
        {
            "DebtQuickReceipt", "ExchangeRow", "Expense", "ExtraIncome", "FuelConversion", "RateHistoryEntry",
            "RetailRow", "SafeEntry", "StaffShortSettle", "TankDip", "TankerUnload",
        }, withMonth);

        //  و ماهِ معاش کلیدِ تاریخ ندارد، پس هرگز مشتق نمی‌شود
        var salary = db.Model.FindEntityType(typeof(SalaryPayment))!;
        Assert.NotNull(salary.FindProperty("MonthKey"));
        Assert.Null(salary.FindProperty("DateKey"));
    }

    /// <summary>⛔ شورا د۴ — «امروز» با کلید: رقمِ فارسی و صفرِ جاافتاده همان روزند.</summary>
    [Fact]
    public void Emruz_BaKelid_NaBaMatn()
    {
        Assert.True(PumpYaqobi.Application.Localization.Shamsi.SameDay("۱۴۰۵/۰۷/۲۰", "1405/07/20"));
        Assert.True(PumpYaqobi.Application.Localization.Shamsi.SameDay("1405/7/20", "1405/07/20"));
        Assert.False(PumpYaqobi.Application.Localization.Shamsi.SameDay("1405/07/21", "1405/07/20"));
        Assert.False(PumpYaqobi.Application.Localization.Shamsi.SameDay("خراب", "خراب"));
        Assert.False(PumpYaqobi.Application.Localization.Shamsi.SameDay(null, null));

        //  رفتار: «مصارف امروز»ِ PDF ردیفِ رقم‌فارسی را می‌شمارد
        var rows = new[]
        {
            new Expense { DateShamsi = "۱۴۰۵/۰۷/۲۰", Amount = 300m },
            new Expense { DateShamsi = "1405/07/20", Amount = 200m },
            new Expense { DateShamsi = "1405/07/19", Amount = 999m },
        };
        Assert.Equal(500m, rows.Where(e => PumpYaqobi.Application.Localization.Shamsi.SameDay(e.DateShamsi, "1405/07/20")).Sum(e => e.Amount));

        //  ⛔ ممنوعه — رفتارش بالاست: هیچ «امروز»ی با برابریِ متن
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "PumpYaqobi.App", "PumpYaqobi.App.csproj"))) root = root.Parent;
        foreach (var f in new[] { "PumpYaqobi.App/ViewModels/Sections/ExpenseSectionViewModel.cs",
                                  "PumpYaqobi.App/ViewModels/MainViewModel.Ledger.cs",
                                  "PumpYaqobi.Shell/Services/StationSnapshot.cs",
                                  "PumpYaqobi.Reporting/Pdf/ExpenseReport.cs" })
        {
            var src = File.ReadAllText(Path.Combine(root!.FullName, f));
            Assert.DoesNotContain("DateShamsi == today", src);
            Assert.DoesNotContain("DateShamsi == _in.TodayShamsi", src);
        }
    }
}
