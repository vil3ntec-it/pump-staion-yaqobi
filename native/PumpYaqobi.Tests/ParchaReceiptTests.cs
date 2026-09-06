using PumpYaqobi.Application.Security;
using PumpYaqobi.Application.Services;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;
using PumpYaqobi.Services.Data;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// «رسید پارچه‌ها» تا آخرِ راه: از صف تا داخلِ حسابِ صاحبش.
///
/// این‌جا همان چیزی آزموده می‌شود که در نسخهٔ وب چند بار خراب شده بود —
/// رسید باید به حسابِ درست برود، در حسابِ فرعیِ هم‌نام بنشیند، و اگر پیش‌تر
/// از راهِ ورق آمده باشد دوباره ثبت نشود.
/// </summary>
public class ParchaReceiptTests : IDisposable
{
    private readonly string _file = Path.Combine(Path.GetTempPath(), $"pump-pr-{Guid.NewGuid():N}.db");
    private readonly PumpDbFactory _dbf;
    private readonly DebtorService _debtors;
    private readonly ParchaReceiptService _svc;

    public ParchaReceiptTests()
    {
        _dbf = new PumpDbFactory(_file);
        _dbf.EnsureReady();
        var session = new UserSession();
        session.SignIn(UserRole.Admin, "admin");
        var perm = new PermissionService(session);
        var trash = new TrashService(_dbf, perm, session);
        _debtors = new DebtorService(_dbf, perm, trash);
        _svc = new ParchaReceiptService(_dbf, perm, trash, _debtors);
    }

    public void Dispose() { try { if (File.Exists(_file)) File.Delete(_file); } catch { } }

    private async Task<ParchaReceipt> Queue(string account, string name, decimal liters,
                                            decimal price, decimal rasid = 0, string hawala = "")
    {
        var r = await _svc.AddAsync();
        r.DateShamsi = "1405/06/15";
        r.Account = account; r.Name = name; r.Hawala = hawala;
        r.Liters = liters; r.PricePerLiter = price; r.Rasid = rasid;
        await _svc.SaveAsync(r);
        return r;
    }

    [Fact]
    public async Task Posting_puts_the_row_in_the_right_persons_ledger_and_clears_the_queue()
    {
        await _debtors.AddDebtorAsync("احمد خان", "0700000001", false);

        var r = await Queue("احمد خان", "بردگیِ روز", 100, 62, rasid: 1000, hawala: "ح7");
        var (res, person) = await _svc.PostAsync(r.Id);

        Assert.Equal(PostResult.Ok, res);
        Assert.Equal("احمد خان", person);
        Assert.Empty(await _svc.ListAsync());          // از صف برداشته شد

        var full = (await _debtors.ListAsync()).Single();
        var loaded = (await _debtors.LoadFullAsync(full.Id))!;
        var row = loaded.MainAccount.FuelRows.Single();
        Assert.Equal(100m, row.Liters);
        Assert.Equal(6200m, row.Bardagi);              // ۱۰۰ × ۶۲
        Assert.Equal(1000m, row.Rasid);
        Assert.Equal(5200m, row.Albaqi);
        Assert.Equal("ح7", row.Hawala);
        Assert.Equal("parcha", row.Src);
    }

    /// <summary>نوعِ سوخت از متن خوانده می‌شود و همان کلمه از نامِ نمایشی می‌افتد.</summary>
    [Fact]
    public async Task The_fuel_type_is_read_from_the_text_and_stripped_from_the_shown_name()
    {
        await _debtors.AddDebtorAsync("نور محمد", null, false);

        var r = await Queue("نور محمد", "دیزل حواله", 50, 58);
        Assert.Equal(PostResult.Ok, (await _svc.PostAsync(r.Id)).Result);

        var id = (await _debtors.ListAsync()).Single().Id;
        var row = (await _debtors.LoadFullAsync(id))!.MainAccount.FuelRows.Single();
        Assert.Equal(FuelType.Diesel, row.Fuel);
        Assert.Equal("حواله", row.Name);               // «دیزل» از نام برداشته شد
    }

    [Fact]
    public async Task A_receipt_already_taken_from_the_waraq_is_refused()
    {
        var d = await _debtors.AddDebtorAsync("ولی جان", null, false);
        var full = (await _debtors.LoadFullAsync(d.Id))!;
        await _debtors.SaveRowAsync(new DebtRow
        {
            FuelAccountId = full.MainAccount.Id,
            Src = "waraq", Liters = 80, Bardagi = 4960, Hawala = "ح3",
            DateShamsi = "1405/06/14",
        });

        var r = await Queue("ولی جان", "بردگی", 80, 62, hawala: "ح3");
        var (res, _) = await _svc.PostAsync(r.Id);

        Assert.Equal(PostResult.Duplicate, res);
        Assert.Single(await _svc.ListAsync());          // در صف ماند
    }

    [Fact]
    public async Task A_name_nobody_owns_is_reported_not_silently_dropped()
    {
        await _debtors.AddDebtorAsync("احمد", null, false);
        var r = await Queue("کسی که نیست", "بردگی", 10, 60);
        var (res, _) = await _svc.PostAsync(r.Id);

        Assert.Equal(PostResult.NotFound, res);
        Assert.Single(await _svc.ListAsync());
    }

    /// <summary>«ثبت همه» ردیف‌های بی‌نام را دست نمی‌زند.</summary>
    [Fact]
    public async Task Post_all_leaves_the_unnamed_rows_alone()
    {
        await _debtors.AddDebtorAsync("سید آغا", null, false);

        await Queue("سید آغا", "بردگی", 20, 60);
        await Queue("", "", 0, 0);                      // ردیفِ خالیِ تازه‌ساخته
        await Queue("هیچ‌کس", "بردگی", 10, 60);

        var rep = await _svc.PostAllAsync();
        Assert.Equal(1, rep.Ok);
        Assert.Equal(0, rep.Duplicate);
        Assert.Equal(1, rep.NotFound);
        Assert.Equal(2, (await _svc.ListAsync()).Count);   // خالی + ناشناس
    }
}
