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
/// ══ شورا، الف۴ — آزمونِ عددیِ طلایی برای کلِ زنجیره ═══════════════════════════
///
/// سی روز: پارچهٔ پطرول (روز و شب، یک پایهٔ زنجیره‌ای) و دیزل (روز) ⇐ ورقِ همان
/// روز (پایه‌ها خودکار) ⇐ ردیف‌های قرض («کریم»، «/ کریم دکان بابت نان» در واحدِ
/// پول، «رحیم د» دیزل) و یک مصرف ⇐ ثبت به حساب‌ها و مصارف ⇐ «فروش ورق»ِ
/// گاوصندوق (منهای قرض) ⇐ یک خریدِ مخزن ⇐ حسابِ شرکت. بعد ویرایشِ یک پایه، حذفِ
/// یک پارچه و برگشتش از سطلِ زباله (همان Ctrl+Z).
///
/// ⛔ عددهای مرجع در ‎golden-chain.json‎ <b>جدا از برنامه</b> نوشته شده‌اند (حسابِ
/// دستیِ همین سناریو) — نه از خروجیِ خودِ برنامه. همه از راهِ سرویس‌های واقعی و
/// SQLiteِ واقعی.
/// </summary>
[Collection(OpLogCollection.Name)]
public class GoldenChainTests : IDisposable
{
    private readonly string _file = Path.Combine(Path.GetTempPath(), $"pump-chain-{Guid.NewGuid():N}.db");

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        foreach (var f in new[] { _file, _file + "-wal", _file + "-shm" })
            try { if (File.Exists(f)) File.Delete(f); } catch { }
    }

    private sealed record Ref(int days, decimal safeSales, decimal karimFuelAlbaqi, decimal karimShopMoneyAlbaqi,
                              decimal rahimDieselAlbaqi, decimal rahimDieselLiters, decimal expensesTotal,
                              int editDay, decimal editExtraLiters, decimal editSafeDelta,
                              int deleteDay, decimal deleteSafeDelta, decimal companyTon, decimal companyUsd,
                              decimal karimMoneyReceiptPerDay, decimal karimMainMoneyAlbaqi, decimal karimRasidMoneyPetrol,
                              decimal karimMoneyInvoice, decimal rahimFuelInvoiceLiters, decimal rahimRasidFuelPetrol);

    private static Ref Load()
    {
        var p = Path.Combine(AppContext.BaseDirectory, "golden-chain.json");
        return JsonSerializer.Deserialize<Ref>(File.ReadAllText(p))!;
    }

    private sealed record H(PumpDbFactory Db, ParchaDataService Parcha, ShiftWaraqSyncService Sync,
                            WaraqPostingService Post, TrashService Trash, StorageDataService Storage,
                            DebtQuickReceiptService Quick, InvoiceService Invoices);

    private H Make()
    {
        var dbf = new PumpDbFactory(_file);
        dbf.EnsureReady();
        var session = new UserSession();
        session.SignIn(UserRole.Admin, "آزمون");
        var perm = new PermissionService(session);
        var trash = new TrashService(dbf, perm, session);
        var settings = new SettingsService(dbf, perm);
        var calc = new WaraqService();
        var sync = new ShiftWaraqSyncService(dbf, perm, calc, settings);
        trash.ResyncWaraqSales = sync.ResyncSalesAsync;
        var companies = new CompanyDataService(dbf, perm, trash);
        return new H(dbf, new ParchaDataService(dbf, perm, trash, new ParchaService(), sync), sync,
                     new WaraqPostingService(dbf, perm, calc, sync), trash,
                     new StorageDataService(dbf, perm, trash, new StorageService(), settings, companies),
                     new DebtQuickReceiptService(dbf, perm, trash),
                     new InvoiceService(dbf, perm, trash, new DebtorService(dbf, perm, trash)));
    }

    private static string Date(int d) => $"1405/06/{d:00}";

    private static async Task<decimal> SafeSales(PumpDbFactory dbf)
    {
        await using var db = dbf.Create();
        return (await db.SafeEntries.AsNoTracking().Where(e => e.SrcKey != null && e.SrcKey.StartsWith("wq-sales-"))
                        .Select(e => e.Amount).ToListAsync()).Sum();
    }

    private sealed record Acc(decimal FuelAlbaqi, decimal MoneyAlbaqi, decimal DieselAlbaqi, decimal DieselLiters);

    private static async Task<Acc> Account(PumpDbFactory dbf, string name)
    {
        await using var db = dbf.Create();
        var a = await db.DebtAccounts.AsNoTracking().FirstAsync(x => x.Name == name);
        var fuel = await db.DebtRows.AsNoTracking().Where(r => r.FuelAccountId == a.Id).ToListAsync();
        var money = await db.DebtRows.AsNoTracking().Where(r => r.MoneyAccountId == a.Id).ToListAsync();
        return new Acc(fuel.Where(r => r.Fuel == FuelType.Petrol).Sum(r => r.Albaqi),
                       money.Sum(r => r.Albaqi),
                       fuel.Where(r => r.Fuel == FuelType.Diesel).Sum(r => r.Albaqi),
                       fuel.Where(r => r.Fuel == FuelType.Diesel).Sum(r => r.Liters));
    }

    [Fact]
    public async Task SiRuz_ZanjireyeKamel_BaAdadeMarja_Va_VirayeshHazfBargasht()
    {
        var R = Load();
        var h = Make();

        //  ── قرض‌دارها: «کریم» با فرعیِ «دکان»، و «رحیم»
        await using (var db = h.Db.Create())
        {
            var karim = new Debtor { LegacyId = "pk", Name = "کریم" };
            karim.MainAccount.Name = "کریم";
            karim.MainAccount.ReceiptsMigrated = true;
            db.Debtors.Add(karim);
            var rahim = new Debtor { LegacyId = "pr", Name = "رحیم" };
            rahim.MainAccount.Name = "رحیم";
            rahim.MainAccount.ReceiptsMigrated = true;
            db.Debtors.Add(rahim);
            await db.SaveChangesAsync();
            db.DebtAccounts.Add(new DebtAccount { DebtorId = karim.Id, LegacySubId = "sd", Name = "دکان", ReceiptsMigrated = true });
            await db.SaveChangesAsync();
        }

        var reports = new Dictionary<int, long>();
        decimal petrolBase = 1000m;
        for (var d = 1; d <= R.days; d++)
        {
            var day = await h.Parcha.SaveShiftFlowAsync(new ShiftSaveRequest(FuelType.Petrol, ShiftKind.Day,
                Date(d), "احمد", 1, petrolBase, petrolBase + 300 + d, 60m, 0m, 0m, 0m, "", 50m, d > 1));
            Assert.True(day.Ok, day.Error);
            petrolBase += 300 + d;
            reports[d] = day.Report!.Id;
            var night = await h.Parcha.SaveShiftFlowAsync(new ShiftSaveRequest(FuelType.Petrol, ShiftKind.Night,
                Date(d), "محمود", 1, petrolBase, petrolBase + 200 + d, 60m, 0m, 0m, 0m, "", 50m, false));
            Assert.True(night.Ok, night.Error);
            petrolBase += 200 + d;
            var dz = await h.Parcha.SaveShiftFlowAsync(new ShiftSaveRequest(FuelType.Diesel, ShiftKind.Day,
                Date(d), "احمد", 2, 5000m + d * 1000m, 5000m + d * 1000m + 150 + d, 70m, 0m, 0m, 0m, "", 60m, true));
            Assert.True(dz.Ok, dz.Error);

            long wid;
            await using (var db = h.Db.Create())
            {
                var w = await db.WaraqEntries.Include(x => x.Shifts).FirstAsync(x => x.DateShamsi == Date(d));
                wid = w.Id;
                var sd = w.Shifts.First(s => s.Kind == ShiftKind.Day);
                var n = 100;
                void T(string name, decimal liters, decimal amount, WaraqTxnType type, LedgerMode unit, FuelType fuel) =>
                    db.WaraqTransactions.Add(new WaraqTransaction { ShiftId = sd.Id, SortIndex = n++, Name = name,
                        Liters = liters, Amount = amount, AmountAuto = false, Type = type, Unit = unit, Fuel = fuel });
                T("کریم", 20m, 1200m, WaraqTxnType.Debt, LedgerMode.Fuel, FuelType.Petrol);
                T("/ کریم دکان بابت نان", 0m, 500m, WaraqTxnType.Debt, LedgerMode.Money, FuelType.Petrol);
                T("رحیم د", 10m, 700m, WaraqTxnType.Debt, LedgerMode.Fuel, FuelType.Diesel);
                T("چای", 0m, 150m, WaraqTxnType.Expense, LedgerMode.Money, FuelType.Petrol);
                await db.SaveChangesAsync();
            }
            await h.Post.SyncAsync(wid);
            //  رسیدِ پولیِ «کریم» از «رسید قرض‌داران»
            var (qr, _) = await h.Quick.AddAsync("کریم", R.karimMoneyReceiptPerDay, Date(d), null, LedgerMode.Money);
            Assert.Equal(QuickReceiptResult.Ok, qr);
            await h.Sync.ResyncSalesAsync(new[] { wid });
        }

        //  ── یک خریدِ مخزن ⇐ حسابِ شرکت
        await h.Storage.AddPurchaseAsync(new FuelPurchase { Fuel = FuelType.Petrol, DateShamsi = Date(5),
            Seller = "شرکتِ الف", Kg = R.companyTon * 1000m, Density = 0.74m, PriceTon = R.companyUsd, UsdRate = 70m });

        //  ── دو فاکتورِ تاییدشده
        var vm = await h.Invoices.AddAsync(new Invoice { CustomerName = "کریم", Amount = R.karimMoneyInvoice, DateShamsi = Date(9) });
        await h.Invoices.ApproveAsync(vm.Id, 60m);
        var vf = await h.Invoices.AddAsync(new Invoice { CustomerName = "رحیم", Liters = R.rahimFuelInvoiceLiters,
            PricePerLiter = 60m, Fuel = FuelType.Petrol, DateShamsi = Date(9) });
        await h.Invoices.ApproveAsync(vf.Id, 60m);

        async Task AssertAll(decimal safe, string when)
        {
            Assert.True(safe == await SafeSales(h.Db), $"{when}: گاوصندوق {await SafeSales(h.Db)} ≠ {safe}");
            var k = await Account(h.Db, "کریم");
            Assert.Equal(R.karimFuelAlbaqi, k.FuelAlbaqi);
            Assert.Equal(R.karimMainMoneyAlbaqi, k.MoneyAlbaqi);
            Assert.Equal(R.karimShopMoneyAlbaqi, (await Account(h.Db, "دکان")).MoneyAlbaqi);
            var r = await Account(h.Db, "رحیم");
            Assert.Equal(R.rahimDieselAlbaqi, r.DieselAlbaqi);
            Assert.Equal(R.rahimDieselLiters, r.DieselLiters);
            Assert.Equal(0m, r.FuelAlbaqi);
            await using var db = h.Db.Create();
            Assert.Equal(R.expensesTotal, (await db.Expenses.AsNoTracking().Select(e => e.Amount).ToListAsync()).Sum());
            //  ⛔ «/ کریم …»: نامِ حساب در ردیف نیست
            var shopId = (await db.DebtAccounts.AsNoTracking().FirstAsync(x => x.Name == "دکان")).Id;
            var shopNames = await db.DebtRows.AsNoTracking().Where(x => x.MoneyAccountId == shopId).Select(x => x.Name).ToListAsync();
            Assert.Equal(R.days, shopNames.Count);
            Assert.All(shopNames, n => Assert.Equal("بابت نان", n));
            var co = await db.CompanyRows.AsNoTracking().SingleAsync(c => c.SourcePurchaseId != null);
            Assert.Equal((R.companyTon, R.companyUsd), (co.Ton, co.Usd));
        }

        await AssertAll(R.safeSales, "پس از سی روز");

        //  ── ویرایشِ پایهٔ روزِ ۱۵ (ختم +۱۰ لیتر) از راهِ همان ویرایشِ گزارش
        await using (var db = h.Db.Create())
        {
            var rep = await db.Reports.Include(r => r.DayShift).AsNoTracking().FirstAsync(r => r.Id == reports[R.editDay]);
            var s = rep.DayShift!;
            var res = await h.Parcha.EditShiftAsync(rep.Id, ShiftKind.Day, s.Name, s.PumpNum, s.Start, s.End + R.editExtraLiters, s.Price, s.Debt);
            Assert.True(res.Ok, res.Error);
        }
        await AssertAll(R.safeSales + R.editSafeDelta, "پس از ویرایش");

        //  ── حذفِ پارچهٔ پطرولِ روزِ ۳۰، بعد برگشت از سطل (همان Ctrl+Z)
        await h.Parcha.DeleteAsync(reports[R.deleteDay]);
        await AssertAll(R.safeSales + R.editSafeDelta + R.deleteSafeDelta, "پس از حذف");
        var item = (await h.Trash.ListAsync()).First(t => t.Kind == "parcha");
        var (err, _) = await h.Trash.RestoreTracedAsync(item.Id);
        Assert.Null(err);
        await AssertAll(R.safeSales + R.editSafeDelta, "پس از برگشت");

        //  چهار رسیدِ ذخیره‌شدهٔ حساب و هر عددِ مشتق با ردیف‌ها می‌خوانند (الف۳)
        await using (var db = h.Db.Create())
            Assert.Equal(R.karimRasidMoneyPetrol,
                (await db.DebtAccounts.AsNoTracking().FirstAsync(a => a.Name == "کریم")).RasidMoneyPetrol);
        await using (var db = h.Db.Create())
            Assert.Equal(R.rahimRasidFuelPetrol,
                (await db.DebtAccounts.AsNoTracking().FirstAsync(a => a.Name == "رحیم")).RasidFuelPetrol);
        var parity = new LedgerParityService(h.Db, new DebtCalculationService(new SettingsService(h.Db,
            new PermissionService(new UserSession()))));
        Assert.Empty(await parity.CheckAsync(fix: false));
    }
}
