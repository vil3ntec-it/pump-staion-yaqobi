using Microsoft.EntityFrameworkCore;
using PumpYaqobi.Application.Security;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;
using PumpYaqobi.Services.Data;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ فاکتورها ═══════════════════════════════════════════════════════════════
/// ‎invSubmit‎ · ‎invApprove‎ · ‎invRevert‎ · ‎invSaveEdits‎ · ‎invDelete‎ و
/// مهم‌تر از همه ‎_invPostToDebt‎ / ‎_invRemoveFromDebt‎ — نشستنِ فاکتور روی
/// حسابِ قرض‌دار و پس گرفتنِ دقیقاً همان مقدار.
/// </summary>
public class InvoiceParityTests : IDisposable
{
    private readonly string _file =
        Path.Combine(Path.GetTempPath(), $"pump-inv-{Guid.NewGuid():N}.db");

    public void Dispose() { try { if (File.Exists(_file)) File.Delete(_file); } catch { } }

    private (InvoiceService Svc, DebtorService Debtors, PumpDbFactory Db) Host(
        UserRole role = UserRole.Admin)
    {
        var dbf = new PumpDbFactory(_file);
        dbf.EnsureReady();
        var session = new UserSession();
        session.SignIn(role, "آزمون");
        var perm = new PermissionService(session);
        var trash = new TrashService(dbf, perm, session);
        var debtors = new DebtorService(dbf, perm, trash);
        return (new InvoiceService(dbf, perm, trash, debtors), debtors, dbf);
    }

    /// <summary>شیءِ تازه از دیتابیس — همان کاری که صفحه پیش از ویرایش می‌کند.</summary>
    private static async Task<Invoice> ReloadAsync(PumpDbFactory dbf, long id)
    {
        await using var db = dbf.Create();
        return await db.Invoices.AsNoTracking().SingleAsync(x => x.Id == id);
    }

    private static Invoice Fuel(string customer, decimal liters, decimal price,
                                FuelType f = FuelType.Petrol) => new()
    {
        CustomerName = customer, DateShamsi = "1405/06/12",
        Fuel = f, Liters = liters, PricePerLiter = price,
    };

    private static Invoice Money(string customer, decimal amount) => new()
    {
        CustomerName = customer, DateShamsi = "1405/06/12", Amount = amount,
    };

    private static async Task<List<DebtRow>> RowsAsync(PumpDbFactory dbf)
    {
        await using var db = dbf.Create();
        return await db.DebtRows.AsNoTracking().ToListAsync();
    }

    private static async Task<DebtAccount> AccountAsync(PumpDbFactory dbf)
    {
        await using var db = dbf.Create();
        return await db.DebtAccounts.AsNoTracking().FirstAsync();
    }

    // ── ثبت ────────────────────────────────────────────────────────────────
    /// <summary>فاکتورِ تازه همیشه «در صفِ تایید» است و هنوز روی حساب نمی‌نشیند.</summary>
    [Fact]
    public async Task ANewInvoiceIsPendingAndTouchesNothing()
    {
        var (svc, debtors, dbf) = Host();
        var v = await svc.AddAsync(Fuel("کریم", 100m, 60m));

        Assert.Equal(InvoiceStatus.Pending, v.Status);
        Assert.Equal(1, v.InvoiceNumber);
        Assert.Empty(await RowsAsync(dbf));
        Assert.Empty(await debtors.ListAsync());
    }

    /// <summary>
    /// شمارهٔ تکراری رد می‌شود — شمارهٔ فاکتور سندِ کاغذیِ مشتری است و دو
    /// فاکتور با یک شماره یعنی حسابِ قابلِ دفاع نداریم.
    /// </summary>
    [Fact]
    public async Task ADuplicateNumberIsRefused()
    {
        var (svc, _, _) = Host();
        await svc.AddAsync(Fuel("کریم", 10m, 60m));

        var dup = Fuel("احمد", 20m, 60m);
        dup.InvoiceNumber = 1;
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.AddAsync(dup));
    }

    [Fact]
    public async Task NumbersKeepCounting()
    {
        var (svc, _, _) = Host();
        Assert.Equal(1, (await svc.AddAsync(Fuel("الف", 1m, 1m))).InvoiceNumber);
        Assert.Equal(2, (await svc.AddAsync(Fuel("ب", 1m, 1m))).InvoiceNumber);
        Assert.Equal(3, (await svc.AddAsync(Fuel("ج", 1m, 1m))).InvoiceNumber);
    }

    // ── تایید ──────────────────────────────────────────────────────────────
    /// <summary>
    /// فاکتورِ تیل: لیتر به «مقدار رسیدِ تیل»ِ حساب می‌رود **و** یک ردیفِ
    /// نمایشی در جدول می‌سازد.
    ///
    /// ⚠️ ردیفِ نمایشی گلایهٔ صریحِ صاحب ریپو بود: «قبلاً فقط کادرِ مقدار رسید
    /// تیل بی‌صدا عدد می‌گرفت و در جدول هیچ ردیفی نمی‌آمد». و عمداً فقط ستونِ
    /// «رسید تیل» را پر می‌کند تا هیچ محاسبه‌ای عوض نشود.
    /// </summary>
    [Fact]
    public async Task ApprovingAFuelInvoiceAddsTheCreditAndAVisibleRow()
    {
        var (svc, _, dbf) = Host();
        var v = await svc.AddAsync(Fuel("کریم", 100m, 60m));
        await svc.ApproveAsync(v.Id, 62m);

        var acc = await AccountAsync(dbf);
        Assert.Equal(100m, acc.RasidFuelPetrol);
        Assert.Equal(0m, acc.RasidFuelDiesel);

        var row = Assert.Single(await RowsAsync(dbf));
        Assert.Equal(v.Id, row.InvoiceFuelId);
        Assert.Equal(100m, row.RasidFuel);
        // ⚠️ هیچ عددِ دیگری — وگرنه محاسبه عوض می‌شود
        Assert.Equal(0m, row.Liters);
        Assert.Equal(0m, row.Bardagi);
        Assert.Equal(0m, row.Rasid);
        Assert.Equal(0m, row.Albaqi);
        Assert.Contains("فاکتور شماره 1", row.Name);
        Assert.Equal(acc.Id, row.FuelAccountId);
    }

    /// <summary>فاکتورِ «فقط مبلغ»: ردیفِ رسید در دفترِ **پول**، نه تیل.</summary>
    [Fact]
    public async Task AMoneyInvoiceLandsInTheMoneyLedgerOnly()
    {
        var (svc, _, dbf) = Host();
        var v = await svc.AddAsync(Money("کریم", 5000m));
        Assert.True(v.ByMoney);

        await svc.ApproveAsync(v.Id, 62m);

        var row = Assert.Single(await RowsAsync(dbf));
        Assert.Equal(v.Id, row.InvoiceId);
        Assert.True(row.ByMoney);
        Assert.NotNull(row.MoneyAccountId);
        Assert.Null(row.FuelAccountId);          // ⚠️ هرگز دفترِ تیل
        Assert.Equal(5000m, row.Rasid);
        Assert.Equal(-5000m, row.Albaqi);

        var acc = await AccountAsync(dbf);
        Assert.Equal(0m, acc.RasidFuelPetrol);   // بخشِ تیل ندارد
    }

    /// <summary>یک فاکتور می‌تواند **هر دو** بخش را داشته باشد و هر دو بشمارند.</summary>
    [Fact]
    public async Task OneInvoiceCanCarryBothMoneyAndFuel()
    {
        var (svc, _, dbf) = Host();
        var v = Fuel("کریم", 100m, 60m);
        v.Amount = 2000m;
        v = await svc.AddAsync(v);
        Assert.False(v.ByMoney);                 // «فقط مبلغ» نیست

        await svc.ApproveAsync(v.Id, 62m);

        var rows = await RowsAsync(dbf);
        Assert.Equal(2, rows.Count);
        Assert.Single(rows, r => r.InvoiceId == v.Id && r.Rasid == 2000m);
        Assert.Single(rows, r => r.InvoiceFuelId == v.Id && r.RasidFuel == 100m);
        Assert.Equal(100m, (await AccountAsync(dbf)).RasidFuelPetrol);
    }

    /// <summary>تاییدِ دوباره چیزی را دوبرابر نمی‌کند.</summary>
    [Fact]
    public async Task ApprovingTwiceDoesNotDoubleAnything()
    {
        var (svc, _, dbf) = Host();
        var v = await svc.AddAsync(Fuel("کریم", 100m, 60m));
        await svc.ApproveAsync(v.Id, 62m);
        await svc.ApproveAsync(v.Id, 62m);

        Assert.Equal(100m, (await AccountAsync(dbf)).RasidFuelPetrol);
        Assert.Single(await RowsAsync(dbf));
    }

    /// <summary>نرخِ روزِ ثبت هرگز جایگزین نمی‌شود؛ نرخِ تایید جدا قفل می‌شود.</summary>
    [Fact]
    public async Task BothRatesAreLockedSeparately()
    {
        var (svc, _, dbf) = Host();
        var v = await svc.AddAsync(Fuel("کریم", 100m, 60m));
        await svc.ApproveAsync(v.Id, 75m);

        await using var db = dbf.Create();
        var saved = await db.Invoices.AsNoTracking().SingleAsync();
        Assert.Equal(60m, saved.RateOnCreate);
        Assert.Equal(75m, saved.RateOnApprove);
    }

    /// <summary>شمارهٔ تماسِ فاکتور به حساب می‌رود، ولی شمارهٔ موجود را بازنویسی نمی‌کند.</summary>
    [Fact]
    public async Task ThePhoneIsCopiedButNeverOverwritten()
    {
        var (svc, debtors, dbf) = Host();

        var v = Fuel("کریم", 10m, 60m);
        v.Phone = "0700000000";
        v = await svc.AddAsync(v);
        await svc.ApproveAsync(v.Id, 60m);
        Assert.Equal("0700000000", (await debtors.ListAsync())[0].Phone);

        var v2 = Fuel("کریم", 10m, 60m);
        v2.Phone = "0799999999";
        v2 = await svc.AddAsync(v2);
        await svc.ApproveAsync(v2.Id, 60m);
        Assert.Equal("0700000000", (await debtors.ListAsync())[0].Phone);   // دست‌نخورده
    }

    // ── برگشت و حذف ────────────────────────────────────────────────────────
    /// <summary>برگشت دقیقاً همان مقداری را پس می‌گیرد که تایید اضافه کرده بود.</summary>
    [Fact]
    public async Task RevertingGivesBackExactlyWhatWasAdded()
    {
        var (svc, _, dbf) = Host();
        var v = Fuel("کریم", 100m, 60m);
        v.Amount = 2000m;
        v = await svc.AddAsync(v);
        await svc.ApproveAsync(v.Id, 62m);
        await svc.RevertAsync(v.Id);

        Assert.Equal(0m, (await AccountAsync(dbf)).RasidFuelPetrol);
        Assert.Empty(await RowsAsync(dbf));

        await using var db = dbf.Create();
        var saved = await db.Invoices.AsNoTracking().SingleAsync();
        Assert.Equal(InvoiceStatus.Pending, saved.Status);
        Assert.Null(saved.ApprovedAtUtc);
        Assert.Null(saved.PostedFuelLiters);
    }

    /// <summary>حذف هم همان — چیزی در حساب جا نمی‌مانَد.</summary>
    [Fact]
    public async Task DeletingTakesItsEffectWithIt()
    {
        var (svc, _, dbf) = Host();
        var v = await svc.AddAsync(Fuel("کریم", 100m, 60m));
        await svc.ApproveAsync(v.Id, 62m);
        await svc.DeleteAsync(v.Id);

        Assert.Equal(0m, (await AccountAsync(dbf)).RasidFuelPetrol);
        Assert.Empty(await RowsAsync(dbf));
    }

    // ── ویرایش ─────────────────────────────────────────────────────────────
    /// <summary>
    /// ⚠️ ویرایشِ یک فاکتورِ **تاییدشده** باید حساب را هم هم‌گام کند. بی این،
    /// عوض کردنِ لیتر عددِ کهنه را در حساب جا می‌گذاشت.
    /// </summary>
    [Fact]
    public async Task EditingAnApprovedInvoiceResyncsTheAccount()
    {
        var (svc, _, dbf) = Host();
        var v = await svc.AddAsync(Fuel("کریم", 100m, 60m));
        await svc.ApproveAsync(v.Id, 62m);
        Assert.Equal(100m, (await AccountAsync(dbf)).RasidFuelPetrol);

        v.Liters = 40m;
        await svc.UpdateAsync(v);

        Assert.Equal(40m, (await AccountAsync(dbf)).RasidFuelPetrol);
        Assert.Equal(40m, (await RowsAsync(dbf)).Single(r => r.InvoiceFuelId == v.Id).RasidFuel);
    }

    /// <summary>
    /// ⚠️ عوض شدنِ نوعِ تیل: لیتر باید از سمتِ **ثبت‌شده** پس گرفته شود، نه
    /// سمتِ تازه — وگرنه پطرول کم می‌مانَد و دیزل زیادی، بی آنکه کسی بفهمد.
    /// </summary>
    [Fact]
    public async Task ChangingTheFuelTypeGivesBackFromTheRightSide()
    {
        var (svc, _, dbf) = Host();
        var v = await svc.AddAsync(Fuel("کریم", 100m, 60m));      // پطرول
        await svc.ApproveAsync(v.Id, 62m);

        v.Fuel = FuelType.Diesel;
        await svc.UpdateAsync(v);

        var acc = await AccountAsync(dbf);
        Assert.Equal(0m, acc.RasidFuelPetrol);     // پس گرفته شد
        Assert.Equal(100m, acc.RasidFuelDiesel);   // و به سمتِ تازه رفت
    }

    /// <summary>صفر شدنِ مبلغ یعنی ردیفِ پولی باید برداشته شود، نه ردیفِ صفر بماند.</summary>
    [Fact]
    public async Task ZeroingTheAmountRemovesItsRow()
    {
        var (svc, _, dbf) = Host();
        var v = Fuel("کریم", 100m, 60m);
        v.Amount = 2000m;
        v = await svc.AddAsync(v);
        await svc.ApproveAsync(v.Id, 62m);
        Assert.Equal(2, (await RowsAsync(dbf)).Count);

        v.Amount = 0m;
        await svc.UpdateAsync(v);

        var row = Assert.Single(await RowsAsync(dbf));
        Assert.Equal(v.Id, row.InvoiceFuelId);     // فقط ردیفِ تیل ماند
    }

    /// <summary>و صفر شدنِ لیتر، ردیفِ نمایشیِ تیل را می‌بَرد.</summary>
    [Fact]
    public async Task ZeroingTheLitersRemovesTheFuelRow()
    {
        var (svc, _, dbf) = Host();
        var v = Fuel("کریم", 100m, 60m);
        v.Amount = 2000m;
        v = await svc.AddAsync(v);
        await svc.ApproveAsync(v.Id, 62m);

        v.Liters = 0m;
        await svc.UpdateAsync(v);

        var row = Assert.Single(await RowsAsync(dbf));
        Assert.Equal(v.Id, row.InvoiceId);
        Assert.Equal(0m, (await AccountAsync(dbf)).RasidFuelPetrol);
    }

    /// <summary>
    /// عوض کردنِ نامِ مشتری، فاکتور را به حسابِ درست می‌بَرد — چون
    /// ‎invSaveEdits‎ حسابِ مقصد را دوباره از روی نام پیدا می‌کند.
    /// </summary>
    [Fact]
    public async Task RenamingTheCustomerMovesTheInvoiceToTheRightAccount()
    {
        var (svc, debtors, dbf) = Host();
        var v = await svc.AddAsync(Fuel("کریم", 100m, 60m));
        await svc.ApproveAsync(v.Id, 62m);

        v.CustomerName = "احمد";
        await svc.UpdateAsync(v);

        var people = await debtors.ListAsync();
        var karim = people.Single(p => p.Name == "کریم");
        var ahmad = people.Single(p => p.Name == "احمد");
        Assert.Equal(0m, karim.MainAccount.RasidFuelPetrol);
        Assert.Equal(100m, ahmad.MainAccount.RasidFuelPetrol);
    }

    // ── اجازه ──────────────────────────────────────────────────────────────
    /// <summary>
    /// همهٔ کارهای فاکتور در نسخهٔ وب ‎requireAdmin()‎ دارند: ثبت، تایید،
    /// برگشت، ویرایش و حذف. کارمند هیچ‌کدام را نمی‌تواند.
    /// </summary>
    [Fact]
    public async Task AStaffUserCannotTouchInvoices()
    {
        var (admin, _, _) = Host();
        var v = await admin.AddAsync(Fuel("کریم", 10m, 60m));

        var (staff, _, _) = Host(UserRole.Staff);
        await Assert.ThrowsAsync<PermissionDeniedException>(() => staff.AddAsync(Fuel("ب", 1m, 1m)));
        await Assert.ThrowsAsync<PermissionDeniedException>(() => staff.ApproveAsync(v.Id, 60m));
        await Assert.ThrowsAsync<PermissionDeniedException>(() => staff.RevertAsync(v.Id));
        await Assert.ThrowsAsync<PermissionDeniedException>(() => staff.UpdateAsync(v));
        await Assert.ThrowsAsync<PermissionDeniedException>(() => staff.DeleteAsync(v.Id));
    }

    /// <summary>ولی دیدنشان آزاد است.</summary>
    [Fact]
    public async Task AStaffUserCanStillSeeInvoices()
    {
        var (admin, _, _) = Host();
        await admin.AddAsync(Fuel("کریم", 10m, 60m));

        var (staff, _, _) = Host(UserRole.Staff);
        Assert.Single(await staff.ListAsync());
    }
}
