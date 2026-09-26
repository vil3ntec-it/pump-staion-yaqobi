using System.Text.Json;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Persistence;
using PumpYaqobi.Services.Data;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ کلیدِ خارجی روی کامپیوترِ دوم — «ده سال داده» (۱۴۰۵/۰۷/۱۴) ═══════════
///
/// سنجهٔ `tensync` (دو کامپیوتر، سرورِ حسابِ واقعی) نشان داد حساب‌ها، ردیف‌های
/// قرض‌دار، گزارش‌ها، حاضری و ردیف‌های شرکت روی کامپیوترِ دوم هیچ‌کدام
/// نمی‌نشستند: کلیدِ خارجی عددِ <c>Id</c>ِ کامپیوترِ اول بود. این‌جا همان
/// رفت‌وبرگشت با دو دفترِ واقعی:
///   • فرزند به پدرِ <b>درست</b> وصل می‌شود، حتی وقتی شماره‌ها فرق دارند
///   • فرزندی که پیش از پدر برسد کنار می‌ماند و بعد می‌نشیند
///   • ترتیبِ «بارِ اول» پدر پیش از فرزند است
/// </summary>
public class SyncParentLinkTests : IDisposable
{
    private readonly string _a = Path.Combine(Path.GetTempPath(), $"pump-fk-a-{Guid.NewGuid():N}.db");
    private readonly string _b = Path.Combine(Path.GetTempPath(), $"pump-fk-b-{Guid.NewGuid():N}.db");

    public void Dispose()
    {
        foreach (var f in new[] { _a, _b })
            foreach (var x in new[] { f, f + "-wal", f + "-shm" })
                try { if (File.Exists(x)) File.Delete(x); } catch { }
        GC.SuppressFinalize(this);
    }

    private static IncomingOp In(SyncOp o)
    {
        using var doc = JsonDocument.Parse(o.FieldsJson);
        return new IncomingOp(o.OpId, o.TableName, o.RowUid, o.OpType, doc.RootElement.Clone(), 0);
    }

    [Fact]
    public void Farzand_BePedareDorost_Vasl_Mishavad_HattaVaghtiPishAzPedarBerasad()
    {
        var fa = new PumpDbFactory(_a); fa.EnsureReady();
        var fb = new PumpDbFactory(_b); fb.EnsureReady();

        //  کامپیوترِ ب از پیش چند قرض‌دار دارد، پس شماره‌ها با الف یکی نیستند
        using (var db = fb.Create())
        {
            for (var i = 0; i < 5; i++) db.Debtors.Add(new Debtor { Name = "از پیش " + i, LegacyId = "b" + i });
            db.SaveChanges();
        }

        //  کامپیوترِ الف: قرض‌دار + حساب + ردیف، همه در **یک** ذخیره (شمارهٔ موقتِ EF)
        string debtorUid, rowUid;
        using (var db = fa.Create())
        {
            var d = new Debtor { Name = "کریم", LegacyId = "a1" };
            d.MainAccount.FuelRows.Add(new DebtRow { Name = "۲۰ لیتر", DateKey = 14050714 });
            db.Debtors.Add(d);
            db.SaveChanges();
            debtorUid = d.SyncUid!;
            rowUid = d.MainAccount.FuelRows[0].SyncUid!;
        }

        var ops = new SyncStore(fa).Take();
        //  پدر پیش از فرزند
        var order = ops.Select(o => o.TableName).ToList();
        Assert.True(order.IndexOf(nameof(Debtor)) < order.IndexOf(nameof(DebtAccount)));
        Assert.True(order.IndexOf(nameof(DebtAccount)) < order.IndexOf(nameof(DebtRow)));
        //  کنارِ کلیدِ خارجی شناسهٔ سراسریِ پدر می‌رود
        var acct = ops.Single(o => o.TableName == nameof(DebtAccount));
        Assert.Contains($"\"MainOfDebtorId@\":\"{debtorUid}\"", acct.FieldsJson);

        //  ⛔ وارونه می‌رسند: فرزند پیش از پدر
        var rep = new SyncStore(fb).ApplyIncoming(ops.Select(In).Reverse().ToList());
        Assert.Equal(0, rep.Failed);
        Assert.Equal(3, rep.Applied);

        using var read = fb.Create();
        var debtor = read.Debtors.Single(x => x.SyncUid == debtorUid);
        var account = read.DebtAccounts.Single(x => x.MainOfDebtorId == debtor.Id);
        var row = read.DebtRows.Single(x => x.SyncUid == rowUid);
        Assert.Equal(account.Id, row.FuelAccountId);
        Assert.NotEqual(1L, debtor.Id);   // شماره فرق داشت و باز درست وصل شد
    }

    [Fact]
    public void PedareNarasideh_BaAdadeHadsi_Nemineshinad()
    {
        var fb = new PumpDbFactory(_b); fb.EnsureReady();
        using (var db = fb.Create()) { db.Debtors.Add(new Debtor { Name = "کسِ دیگر", LegacyId = "b9" }); db.SaveChanges(); }

        using var doc = JsonDocument.Parse("{\"MainOfDebtorId\":1,\"MainOfDebtorId@\":\"uid-nabude\",\"Name\":\"یتیم\"}");
        var rep = new SyncStore(fb).ApplyIncoming(new[]
        {
            new IncomingOp("op-1", nameof(DebtAccount), "uid-acct", "insert", doc.RootElement.Clone(), 1),
        });
        Assert.Equal(1, rep.Failed);
        Assert.Equal(0, rep.Applied);
        using var read = fb.Create();
        Assert.Empty(read.DebtAccounts.Where(x => x.SyncUid == "uid-acct").ToList());   // ⛔ به «کسِ دیگر» وصل نشد
    }

    /// <summary>
    /// ⛔ پیوندِ <b>اعلام‌نشده</b> (مثلِ معاشِ کارمند در «مصارف») هم ترجمه می‌شود —
    /// وگرنه روی کامپیوترِ دوم بی هیچ خطایی به کارمندِ دیگری وصل می‌شد.
    /// </summary>
    [Fact]
    public void PeyvandeElamNashode_Ham_Tarjome_Mishavad()
    {
        var fa = new PumpDbFactory(_a); fa.EnsureReady();
        var fb = new PumpDbFactory(_b); fb.EnsureReady();
        using (var db = fb.Create())
        {
            for (var i = 0; i < 3; i++) db.StaffMembers.Add(new StaffMember { Name = "کارمندِ ب " + i });
            db.SaveChanges();
        }

        string staffUid, expUid;
        using (var db = fa.Create())
        {
            var s = new StaffMember { Name = "حمید" };
            db.StaffMembers.Add(s);
            db.SaveChanges();
            var e = new Expense { Title = "معاش", Amount = 9000m, DateKey = 14050714, MonthKey = "1405/07", SalaryStaffId = s.Id };
            db.Expenses.Add(e);
            db.SaveChanges();
            staffUid = s.SyncUid!; expUid = e.SyncUid!;
        }

        var ops = new SyncStore(fa).Take();
        Assert.Contains($"\"SalaryStaffId@\":\"{staffUid}\"", ops.Single(o => o.TableName == nameof(Expense)).FieldsJson);

        var rep = new SyncStore(fb).ApplyIncoming(ops.Select(In).Reverse().ToList());
        Assert.Equal(0, rep.Failed);

        using var read = fb.Create();
        var staff = read.StaffMembers.Single(x => x.SyncUid == staffUid);
        Assert.Equal(staff.Id, read.Expenses.Single(x => x.SyncUid == expUid).SalaryStaffId);
        Assert.NotEqual(1L, staff.Id);
    }

    [Fact]
    public void BareAval_PedarPishAzFarzand_Ast()
    {
        var fa = new PumpDbFactory(_a); fa.EnsureReady();
        using var db = fa.Create();
        var tables = SyncStore.DataTables(db).Select(t => t.Entity).ToList();
        Assert.True(tables.IndexOf(nameof(Debtor)) < tables.IndexOf(nameof(DebtAccount)));
        Assert.True(tables.IndexOf(nameof(DebtAccount)) < tables.IndexOf(nameof(DebtRow)));
        Assert.True(tables.IndexOf(nameof(WaraqShift)) < tables.IndexOf(nameof(WaraqPump)));
        //  پیوندهای اعلام‌نشده هم ترتیب را می‌سازند
        Assert.True(tables.IndexOf(nameof(Invoice)) < tables.IndexOf(nameof(DebtRow)));
        Assert.True(tables.IndexOf(nameof(DebtAccount)) < tables.IndexOf(nameof(Invoice)));
        Assert.True(tables.IndexOf(nameof(StaffMember)) < tables.IndexOf(nameof(Expense)));
    }
}
