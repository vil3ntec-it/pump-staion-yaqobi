using Microsoft.EntityFrameworkCore;
using PumpYaqobi.Application.Security;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;
using PumpYaqobi.Services.Data;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// فاکتورها — همان قاعده‌هایی که در نسخهٔ وب با باگ‌های گزارش‌شده به دست آمدند:
///   • بخشِ پولیِ فاکتور همیشه در دفترِ «واحد پول» می‌نشیند، هرگز در دفترِ تیل.
///   • یک فاکتور می‌تواند هم بخشِ پولی داشته باشد هم بخشِ تیل، و هر دو شمرده شوند.
///   • برگشتِ فاکتور دقیقاً همان مقداری را پس می‌گیرد که اضافه کرده بود.
/// </summary>
public class InvoiceTests : IDisposable
{
    private readonly string _file = Path.Combine(Path.GetTempPath(), $"pump-inv-{Guid.NewGuid():N}.db");
    private readonly PumpDbFactory _dbf;
    private readonly InvoiceService _svc;
    private readonly DebtorService _debtors;

    public InvoiceTests()
    {
        _dbf = new PumpDbFactory(_file);
        _dbf.EnsureReady();
        var session = new Application.Security.UserSession();
        session.SignIn(UserRole.Admin, "admin");
        var perm = new PermissionService(session);
        var trash = new TrashService(_dbf, perm, session);
        _debtors = new DebtorService(_dbf, perm, trash);
        _svc = new InvoiceService(_dbf, perm, trash, _debtors);
    }

    public void Dispose() { try { if (File.Exists(_file)) File.Delete(_file); } catch { } }

    private static Invoice Fuel(string name, decimal liters, decimal price) => new()
    {
        CustomerName = name, Liters = liters, PricePerLiter = price,
        Fuel = FuelType.Petrol, DateShamsi = "1405/06/15",
    };

    [Fact]
    public void NumbersAreUniqueAndIncreasing()
    {
        var a = _svc.AddAsync(Fuel("الف", 100, 60)).Result;
        var b = _svc.AddAsync(Fuel("ب", 100, 60)).Result;
        Assert.True(b.InvoiceNumber > a.InvoiceNumber);
    }

    [Fact]
    public void MoneyOnlyInvoice_IsDetected()
    {
        Assert.True(InvoiceService.IsMoneyOnly(new Invoice { Amount = 5000 }));
        Assert.False(InvoiceService.IsMoneyOnly(new Invoice { Amount = 5000, Liters = 10, PricePerLiter = 60 }));
        Assert.False(InvoiceService.IsMoneyOnly(new Invoice()));
    }

    [Fact]
    public void ApprovingMoneyInvoice_PutsTheRowInTheMoneyLedger_NotTheFuelOne()
    {
        var v = _svc.AddAsync(new Invoice { CustomerName = "کریم", Amount = 5000, DateShamsi = "1405/06/15" }).Result;
        _svc.ApproveAsync(v.Id, todayRate: 62m).Wait();

        using var db = _dbf.Create();
        var row = db.DebtRows.Single(r => r.InvoiceId == v.Id);
        Assert.NotNull(row.MoneyAccountId);
        Assert.Null(row.FuelAccountId);        // ⚠️ باگی که گزارش شده بود
        Assert.True(row.ByMoney);
        Assert.Equal(5000m, row.Rasid);
        Assert.Equal(-5000m, row.Albaqi);
        Assert.Equal(0m, row.Liters);
    }

    /// <summary>
    /// رسیدِ تیلِ فاکتور هم یک **ردیفِ واقعی** است، مثلِ هر رسیدِ دیگری — نه
    /// عددی که فقط روی خودِ حساب بنشیند.
    ///
    /// ⚠️ پیش از این ‎Assert.Empty(db.DebtRows)‎ بود و همان درست بود، چون
    /// رسیدِ تیل جایی جز عددِ حساب نداشت. حالا رسید یک انبار بیشتر ندارد، پس
    /// ردیفش هست و در جدولِ شخص هم دیده می‌شود.
    /// </summary>
    [Fact]
    public void ApprovingFuelInvoice_AddsFuelReceiptToTheAccount()
    {
        var v = _svc.AddAsync(Fuel("نعیم", 250, 62)).Result;
        _svc.ApproveAsync(v.Id, 62m).Wait();

        using var db = _dbf.Create();
        var acc = db.DebtAccounts.Single();
        Assert.Equal(250m, acc.RasidFuelPetrol);

        var row = db.DebtRows.Single(r => r.InvoiceId == v.Id);
        Assert.Equal(250m, row.RasidFuel);     // در ستونِ «رسید تیل»
        Assert.NotNull(row.FuelAccountId);     // در دفترِ تیل
        Assert.Equal(0m, row.Liters);          // رسید است، نه بردگی
    }

    [Fact]
    public void OneInvoiceCanCarryBothMoneyAndFuel()
    {
        var v = _svc.AddAsync(new Invoice
        {
            CustomerName = "هردو", Liters = 100, PricePerLiter = 62, Amount = 3000,
            DateShamsi = "1405/06/15",
        }).Result;
        _svc.ApproveAsync(v.Id, 62m).Wait();

        using var db = _dbf.Create();
        Assert.Equal(100m, db.DebtAccounts.Single().RasidFuelPetrol);

        // دو ردیف: بخشِ پولی در دفترِ پول، و رسیدِ تیل در دفترِ تیل
        var rows = db.DebtRows.Where(r => r.InvoiceId == v.Id).ToList();
        Assert.Equal(2, rows.Count);
        Assert.Equal(3000m, rows.Single(r => r.MoneyAccountId != null).Rasid);
        Assert.Equal(100m, rows.Single(r => r.FuelAccountId != null).RasidFuel);
    }

    [Fact]
    public void Reverting_TakesBackExactlyWhatWasAdded()
    {
        var v = _svc.AddAsync(new Invoice
        {
            CustomerName = "برگشتی", Liters = 400, PricePerLiter = 62, Amount = 1000,
            DateShamsi = "1405/06/15",
        }).Result;
        _svc.ApproveAsync(v.Id, 62m).Wait();
        _svc.RevertAsync(v.Id).Wait();

        using var db = _dbf.Create();
        Assert.Equal(0m, db.DebtAccounts.Single().RasidFuelPetrol);
        Assert.Empty(db.DebtRows);
        Assert.Equal(InvoiceStatus.Pending, db.Invoices.Single().Status);
    }

    [Fact]
    public void ApprovingTwice_DoesNotDoubleThePosting()
    {
        var v = _svc.AddAsync(Fuel("دوباره", 100, 62)).Result;
        _svc.ApproveAsync(v.Id, 62m).Wait();
        _svc.ApproveAsync(v.Id, 62m).Wait();

        using var db = _dbf.Create();
        Assert.Equal(100m, db.DebtAccounts.Single().RasidFuelPetrol);
    }

    [Fact]
    public void ApprovalLocksBothRates_ForThePriceLossReport()
    {
        var v = _svc.AddAsync(Fuel("نرخ", 100, 60)).Result;
        _svc.ApproveAsync(v.Id, todayRate: 68m).Wait();

        using var db = _dbf.Create();
        var saved = db.Invoices.Single();
        Assert.Equal(60m, saved.RateOnCreate);
        Assert.Equal(68m, saved.RateOnApprove);
    }

    [Fact]
    public void ApprovingReusesTheSamePerson_WhenTheNameMatches()
    {
        _svc.ApproveAsync(_svc.AddAsync(Fuel("حاجی رحیم", 100, 62)).Result.Id, 62m).Wait();
        _svc.ApproveAsync(_svc.AddAsync(Fuel("حاجي رحيم", 50, 62)).Result.Id, 62m).Wait();  // ی/ي عربی

        using var db = _dbf.Create();
        Assert.Single(db.Debtors);
        Assert.Equal(150m, db.DebtAccounts.Single().RasidFuelPetrol);
    }
}
