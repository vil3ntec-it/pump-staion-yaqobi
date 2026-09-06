using Microsoft.EntityFrameworkCore;
using PumpYaqobi.Application.Security;
using PumpYaqobi.Application.Services;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;
using PumpYaqobi.Services.Data;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ رسید قرض‌داران ═════════════════════════════════════════════════════════
/// ‎quickAddDebtRasid‎ / ‎undoDebtRasid‎ — پرداختِ نقدیِ مستقیم به حسابِ یک
/// قرض‌دار، بی هیچ تیل و بی هیچ دکمهٔ «ثبت».
///
/// ردیفی که باید در حساب بنشیند، مو‌به‌مو:
/// <code>
/// { name: note || 'رسید', fuel: 0, priceper: 0, bardagi: 0,
///   rasid: amount, albaqi: -amount, src: 'debtQuick', srcKey: 'debtQuick|&lt;id&gt;' }
/// </code>
/// </summary>
public class DebtReceiptTests : IDisposable
{
    private readonly string _file =
        Path.Combine(Path.GetTempPath(), $"pump-dr-{Guid.NewGuid():N}.db");

    public void Dispose() { try { if (File.Exists(_file)) File.Delete(_file); } catch { } }

    private (DebtQuickReceiptService Svc, DebtorService Debtors, PumpDbFactory Db) Host()
    {
        var dbf = new PumpDbFactory(_file);
        dbf.EnsureReady();
        var session = new UserSession();
        session.SignIn(UserRole.Admin, "آزمون");
        var perm = new PermissionService(session);
        var trash = new TrashService(dbf, perm, session);
        return (new DebtQuickReceiptService(dbf, perm, trash),
                new DebtorService(dbf, perm, trash), dbf);
    }

    private static async Task<Debtor> AddPersonAsync(PumpDbFactory dbf, string name)
    {
        await using var db = dbf.Create();
        var p = new Debtor { Name = name, LegacyId = "p" + Guid.NewGuid().ToString("N")[..8] };
        db.Debtors.Add(p);
        await db.SaveChangesAsync();
        return p;
    }

    private static async Task<List<DebtRow>> RowsAsync(PumpDbFactory dbf)
    {
        await using var db = dbf.Create();
        return await db.DebtRows.AsNoTracking().ToListAsync();
    }

    // ── ثبت ────────────────────────────────────────────────────────────────
    [Fact]
    public async Task AReceiptLandsInTheAccountWithExactlyTheRightRow()
    {
        var (svc, _, dbf) = Host();
        await AddPersonAsync(dbf, "کریم جان");

        var (res, person) = await svc.AddAsync("کریم جان", 2500m, "1405/06/12", "قسط اول");
        Assert.Equal(QuickReceiptResult.Ok, res);
        Assert.Equal("کریم جان", person);

        var row = Assert.Single(await RowsAsync(dbf));
        Assert.Equal("قسط اول", row.Name);        // توضیحات، نه نامِ تایپ‌شده
        Assert.Equal(0m, row.Liters);
        Assert.Equal(0m, row.PricePerLiter);
        Assert.Equal(0m, row.Bardagi);
        Assert.Equal(2500m, row.Rasid);
        Assert.Equal(-2500m, row.Albaqi);         // ⚠️ منفی: پرداخت بدهی را کم می‌کند
        Assert.Equal("debtQuick", row.Src);
        Assert.StartsWith("debtQuick|", row.SrcKey);
        Assert.Equal("1405/06/12", row.DateShamsi);

        // و خودِ رسید در دفترچه، با نامِ **واقعیِ** حساب
        var rec = Assert.Single(await svc.ListAsync());
        Assert.Equal("کریم جان", rec.Account);
        Assert.Equal(2500m, rec.Amount);
        Assert.Equal("1405/06", rec.MonthKey);
    }

    /// <summary>بی توضیحات، نامِ ردیف «رسید» می‌شود — ‎note || 'رسید'‎.</summary>
    [Fact]
    public async Task WithoutANoteTheRowIsCalledRasid()
    {
        var (svc, _, dbf) = Host();
        await AddPersonAsync(dbf, "احمد");
        await svc.AddAsync("احمد", 100m, "1405/06/12");
        Assert.Equal("رسید", (await RowsAsync(dbf))[0].Name);
    }

    /// <summary>
    /// ⚠️ کادرِ نیمه‌کاره **خطا نیست**. نسخهٔ وب با هر تایپ صدا زده می‌شود و تا
    /// نام و مبلغ هر دو پر نشوند بی‌صدا برمی‌گردد. اگر این‌جا خطا می‌داد،
    /// کاربر با هر حرفی که می‌نوشت یک پیامِ سرخ می‌گرفت.
    /// </summary>
    [Theory]
    [InlineData("", 100)]
    [InlineData("   ", 100)]
    [InlineData("کریم", 0)]
    [InlineData("", 0)]
    public async Task AHalfFilledFormDoesNothingAndIsNotAnError(string name, double amount)
    {
        var (svc, _, dbf) = Host();
        await AddPersonAsync(dbf, "کریم");

        var (res, _) = await svc.AddAsync(name, (decimal)amount);
        Assert.Equal(QuickReceiptResult.Incomplete, res);
        Assert.Empty(await RowsAsync(dbf));
        Assert.Empty(await svc.ListAsync());
    }

    /// <summary>
    /// حسابِ نبوده ساخته نمی‌شود. اگر می‌شد، یک غلطِ املایی حسابِ تازه‌ای
    /// می‌ساخت و پول در حسابِ کسی می‌رفت که وجود ندارد.
    /// </summary>
    [Fact]
    public async Task AnUnknownNameIsRefusedInsteadOfCreatingAnAccount()
    {
        var (svc, debtors, dbf) = Host();
        await AddPersonAsync(dbf, "کریم");

        var (res, _) = await svc.AddAsync("محمود", 500m);
        Assert.Equal(QuickReceiptResult.NotFound, res);
        Assert.Empty(await RowsAsync(dbf));
        Assert.Empty(await svc.ListAsync());
        Assert.Single(await debtors.ListAsync());     // هیچ حسابی ساخته نشد
    }

    /// <summary>
    /// تطبیقِ نام همان تطبیقِ همیشگی است — نوشتنِ بخشی از نام هم کافی است.
    /// </summary>
    [Fact]
    public async Task PartOfTheNameIsEnough()
    {
        var (svc, _, dbf) = Host();
        await AddPersonAsync(dbf, "حاجی کریم");

        var (res, person) = await svc.AddAsync("حاجی کریم جان", 300m);
        Assert.Equal(QuickReceiptResult.Ok, res);
        Assert.Equal("حاجی کریم", person);
    }

    /// <summary>رسید به حسابِ فرعیِ هم‌نام می‌رود، نه به حسابِ اصلی.</summary>
    [Fact]
    public async Task AReceiptGoesToTheMatchingSubAccount()
    {
        var (svc, _, dbf) = Host();
        long subId;
        await using (var db = dbf.Create())
        {
            var p = new Debtor { Name = "کریم", LegacyId = "p1" };
            p.SubAccounts.Add(new DebtAccount { LegacySubId = "s1", Name = "دکان کریم" });
            db.Debtors.Add(p);
            await db.SaveChangesAsync();
            subId = p.SubAccounts[0].Id;
        }

        var (res, _) = await svc.AddAsync("دکان کریم", 700m);
        Assert.Equal(QuickReceiptResult.Ok, res);

        var row = Assert.Single(await RowsAsync(dbf));
        Assert.Equal(subId, row.FuelAccountId);
    }

    /// <summary>دو رسیدِ پشتِ هم، دو ردیفِ جدا — هیچ‌کدام دیگری را بازنویسی نمی‌کند.</summary>
    [Fact]
    public async Task TwoReceiptsInARowStayTwoRows()
    {
        var (svc, _, dbf) = Host();
        await AddPersonAsync(dbf, "کریم");

        await svc.AddAsync("کریم", 100m, "1405/06/12", "یک");
        await svc.AddAsync("کریم", 200m, "1405/06/13", "دو");

        var rows = await RowsAsync(dbf);
        Assert.Equal(2, rows.Count);
        Assert.Equal(2, rows.Select(r => r.SrcKey).Distinct().Count());
        Assert.Equal(300m, rows.Sum(r => r.Rasid));
        Assert.Equal(2, (await svc.ListAsync()).Count);
    }

    // ── برگرداندن ──────────────────────────────────────────────────────────
    /// <summary>
    /// ⚠️ برگرداندنِ رسید باید **هر دو** را ببرد: خودِ رسید و ردیفش در حساب.
    /// اگر فقط رسید پاک می‌شد، پول در حسابِ طرف می‌ماند و در هیچ فهرستی دیده
    /// نمی‌شد — بدهیِ پاک‌شده‌ای که هیچ‌کس نمی‌داند از کجا آمده.
    /// </summary>
    [Fact]
    public async Task UndoingTakesBothTheReceiptAndItsRowInTheAccount()
    {
        var (svc, _, dbf) = Host();
        await AddPersonAsync(dbf, "کریم");
        await svc.AddAsync("کریم", 900m, "1405/06/12", "قسط");

        var rec = Assert.Single(await svc.ListAsync());
        Assert.True(await svc.UndoAsync(rec.Id));

        Assert.Empty(await svc.ListAsync());
        Assert.Empty(await RowsAsync(dbf));
    }

    /// <summary>برگرداندنِ یکی، دیگری را دست نمی‌زند.</summary>
    [Fact]
    public async Task UndoingOneLeavesTheOthersAlone()
    {
        var (svc, _, dbf) = Host();
        await AddPersonAsync(dbf, "کریم");
        await svc.AddAsync("کریم", 100m, "1405/06/12", "یک");
        await svc.AddAsync("کریم", 200m, "1405/06/12", "دو");

        var list = await svc.ListAsync();
        await svc.UndoAsync(list.First(r => r.Amount == 100m).Id);

        var rows = await RowsAsync(dbf);
        Assert.Single(rows);
        Assert.Equal(200m, rows[0].Rasid);
    }

    // ── ماه‌ها ─────────────────────────────────────────────────────────────
    [Fact]
    public async Task ReceiptsAreGroupedByMonth()
    {
        var (svc, _, dbf) = Host();
        await AddPersonAsync(dbf, "کریم");
        await svc.AddAsync("کریم", 100m, "1405/06/12");
        await svc.AddAsync("کریم", 200m, "1405/07/03");

        var months = await svc.MonthsAsync();
        Assert.Contains("1405/06", months);
        Assert.Contains("1405/07", months);
        Assert.Equal(months.OrderByDescending(m => m), months);   // تازه‌ترین اول

        Assert.Single(await svc.ListAsync("1405/06"));
        Assert.Equal(2, (await svc.ListAsync()).Count);
    }

    /// <summary>ماهِ جاری همیشه در منو هست، حتی اگر هنوز رسیدی نداشته باشد.</summary>
    [Fact]
    public async Task TheCurrentMonthIsAlwaysOffered()
    {
        var (svc, _, _) = Host();
        Assert.Contains(Application.Localization.Shamsi.MonthKey(
            Application.Localization.Shamsi.Today()), await svc.MonthsAsync());
    }

    /// <summary>ثبت پشتِ اجازه است — بینندهٔ ساده نمی‌تواند در حساب پول بریزد.</summary>
    [Fact]
    public async Task AViewerCannotPostAReceipt()
    {
        var dbf = new PumpDbFactory(_file);
        dbf.EnsureReady();
        await AddPersonAsync(dbf, "کریم");

        var session = new UserSession();                 // پیش‌فرض: بیننده
        var perm = new PermissionService(session);
        var svc = new DebtQuickReceiptService(dbf, perm, new TrashService(dbf, perm, session));

        await Assert.ThrowsAnyAsync<Exception>(() => svc.AddAsync("کریم", 100m));
    }
}
