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
/// ══ شورا، الف۵ — ثبتِ خودکارِ ورق به حساب‌ها: آزمونِ تصادفی با بذرِ ثابت ═════
///
/// هر بذر ۵۰۰ کارِ تصادفی روی یک ورق: افزودنِ ردیف، عوض کردنِ نام و مبلغ، حذف،
/// عوض کردنِ واحد، و «جدول جدید» (آرشیوِ حساب). پس از هر کار همان ‎SyncAsync‎ِ
/// برنامه، و این ثابت‌ها:
/// <list type="bullet">
/// <item>هر ردیفِ ورق دقیقاً در جایی است که نامش می‌گوید (یک حساب، یک دفتر،
///   چکنه، مصارف) یا هیچ‌جا — نه دو جا. پیشگو جدا از برنامه است: نامِ هر ردیف
///   از فهرستِ ثابتی می‌آید که مقصدش از پیش معلوم است.</item>
/// <item>بردگیِ ردیفِ حساب = مبلغِ همان ردیفِ ورق.</item>
/// <item>هیچ ردیفِ دستیِ کاربر پاک یا عوض نشده (یا زنده است یا در آرشیو).</item>
/// <item>جمعِ رسیدِ ردیف‌های دستی گم نشده.</item>
/// </list>
/// بذرِ شکسته در پیام چاپ می‌شود تا بازسازی‌پذیر باشد.
/// </summary>
[Collection(OpLogCollection.Name)]
public class WaraqPostingFuzzTests : IDisposable
{
    private readonly string _file = Path.Combine(Path.GetTempPath(), $"pump-fuzz-{Guid.NewGuid():N}.db");

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        foreach (var f in new[] { _file, _file + "-wal", _file + "-shm" })
            try { if (File.Exists(f)) File.Delete(f); } catch { }
    }

    public const int Ops = 500;

    public static IEnumerable<object[]> Seeds() => Enumerable.Range(1, 20).Select(s => new object[] { s * 7919 });

    /// <summary>نام ⇐ مقصد. مقصدِ هر نام را این فهرست می‌گوید، نه برنامه.</summary>
    private enum Dest { Karim, Shop, Rahim, Salim, None, Retail }

    private static readonly (string Name, Dest To, FuelType Fuel)[] Names =
    {
        ("کریم", Dest.Karim, FuelType.Petrol),
        ("/ کریم دکان بابت نان", Dest.Shop, FuelType.Petrol),
        ("رحیم د", Dest.Rahim, FuelType.Diesel),
        ("سلیم", Dest.Salim, FuelType.Petrol),
        ("فلانی‌ناشناس", Dest.None, FuelType.Petrol),
        ("/چکنه", Dest.Retail, FuelType.Petrol),
    };

    [Theory]
    [MemberData(nameof(Seeds))]
    public async Task PansadKareTasadofi_SabetHaMimanand(int seed)
    {
        var dbf = new PumpDbFactory(_file);
        dbf.EnsureReady();
        var session = new UserSession();
        session.SignIn(UserRole.Admin, "آزمون");
        var perm = new PermissionService(session);
        var trash = new TrashService(dbf, perm, session);
        var calc = new WaraqService();
        var sync = new ShiftWaraqSyncService(dbf, perm, calc, new SettingsService(dbf, perm));
        var post = new WaraqPostingService(dbf, perm, calc, sync);
        var data = new WaraqDataService(dbf, perm, trash);
        var debtors = new DebtorService(dbf, perm, trash);

        //  ── چهار حساب، هر کدام با ردیف‌های دستی (و رسید) که نباید هیچ‌وقت عوض شوند
        var acct = new Dictionary<Dest, long>();
        decimal manualRasid = 0m;
        var manualCount = 0;
        await using (var db = dbf.Create())
        {
            Debtor P(string n, string id)
            {
                var p = new Debtor { LegacyId = id, Name = n };
                p.MainAccount.Name = n;
                p.MainAccount.ReceiptsMigrated = true;
                for (var k = 0; k < 3; k++)
                {
                    var rasid = 100m * (k + 1);
                    p.MainAccount.FuelRows.Add(new DebtRow { DateShamsi = "1405/05/0" + (k + 1), Name = "دستی-" + n + k,
                        Liters = 10 + k, PricePerLiter = 60m, Bardagi = (10 + k) * 60m, Rasid = rasid,
                        Albaqi = (10 + k) * 60m - rasid });
                    manualRasid += rasid; manualCount++;
                }
                db.Debtors.Add(p);
                return p;
            }
            var karim = P("کریم", "pk"); var rahim = P("رحیم", "pr"); var salim = P("سلیم", "ps");
            await db.SaveChangesAsync();
            var shop = new DebtAccount { DebtorId = karim.Id, LegacySubId = "sd", Name = "دکان", ReceiptsMigrated = true };
            db.DebtAccounts.Add(shop);
            await db.SaveChangesAsync();
            (acct[Dest.Karim], acct[Dest.Rahim], acct[Dest.Salim], acct[Dest.Shop]) = (karim.MainAccount.Id, rahim.MainAccount.Id, salim.MainAccount.Id, shop.Id);
        }

        var w = await data.OpenOrCreateAsync("1405/06/18", "پمپ");
        long shiftId;
        await using (var db = dbf.Create())
        {
            var sd = await db.WaraqShifts.Include(s => s.Transactions).FirstAsync(s => s.WaraqId == w.Id && s.Kind == ShiftKind.Day);
            shiftId = sd.Id;
            db.WaraqTransactions.RemoveRange(sd.Transactions);   // ردیف‌های خالیِ آغازین
            await db.SaveChangesAsync();
        }

        var rnd = new Random(seed);
        var archived = new HashSet<string>(StringComparer.Ordinal);   // ‎SrcTxn‎ِ هر ثبتِ آرشیوشده
        var sort = 0;
        for (var op = 0; op < Ops; op++)
        {
            string what;
            await using (var db = dbf.Create())
            {
                var txns = await db.WaraqTransactions.Where(t => t.ShiftId == shiftId).OrderBy(t => t.SortIndex).ThenBy(t => t.Id).ToListAsync();
                var pick = rnd.Next(100);
                if (txns.Count == 0 || pick < 30)
                {
                    var n = Names[rnd.Next(Names.Length)];
                    var expense = rnd.Next(8) == 0;
                    db.WaraqTransactions.Add(new WaraqTransaction { ShiftId = shiftId, SortIndex = ++sort, Name = n.Name,
                        Liters = rnd.Next(0, 40), Amount = rnd.Next(1, 50) * 100m, AmountAuto = false, Fuel = n.Fuel,
                        Type = expense ? WaraqTxnType.Expense : WaraqTxnType.Debt,
                        Unit = rnd.Next(4) == 0 ? LedgerMode.Money : LedgerMode.Fuel });
                    what = "افزودن";
                }
                else
                {
                    var t = txns[rnd.Next(txns.Count)];
                    if (pick < 50) { var n = Names[rnd.Next(Names.Length)]; t.Name = n.Name; t.Fuel = n.Fuel; what = "نام"; }
                    else if (pick < 68) { t.Amount = rnd.Next(0, 50) * 100m; what = "مبلغ"; }
                    else if (pick < 82) { db.WaraqTransactions.Remove(t); what = "حذف"; }
                    else if (pick < 94) { t.Unit = t.Unit == LedgerMode.Money ? LedgerMode.Fuel : LedgerMode.Money; what = "واحد"; }
                    else
                    {
                        //  «جدول جدید» برای یک حساب — آن‌چه به آرشیو رفت دوباره ساخته نمی‌شود
                        await db.SaveChangesAsync();
                        var to = new[] { Dest.Karim, Dest.Rahim, Dest.Salim, Dest.Shop }[rnd.Next(4)];
                        var keys = await db.DebtRows.AsNoTracking()
                                           .Where(r => r.FuelAccountId == acct[to] && r.SrcKey != null)   // آرشیو فقط دفترِ تیل را می‌برد
                                           .Select(r => r.SrcTxn).ToListAsync();
                        Assert.All(keys, k => Assert.False(string.IsNullOrEmpty(k), "ردیفِ ورق بی شناسهٔ تراکنش"));
                        foreach (var k in keys) archived.Add(k!);
                        await debtors.ArchiveTableAsync(acct[to], "1405/06/18");
                        what = "آرشیوِ " + to;
                    }
                }
                await db.SaveChangesAsync();
            }

            await post.SyncAsync(w.Id);
            await Check(dbf, w, shiftId, acct, archived, manualCount, manualRasid, $"بذر {seed} · کارِ {op} ({what})");
        }
    }

    private static async Task Check(PumpDbFactory dbf, WaraqEntry w, long shiftId, Dictionary<Dest, long> acct,
                                    HashSet<string> archived, int manualCount, decimal manualRasid, string where)
    {
        await using var db = dbf.Create();
        var txns = await db.WaraqTransactions.AsNoTracking().Where(t => t.ShiftId == shiftId)
                           .OrderBy(t => t.SortIndex).ThenBy(t => t.Id).ToListAsync();
        var prefix = WaraqPostingService.WaraqKey(w) + "|";
        var rows = await db.DebtRows.AsNoTracking().Where(r => r.SrcKey != null && r.SrcKey.StartsWith(prefix)).ToListAsync();
        var retail = await db.RetailRows.AsNoTracking().Where(r => r.SrcKey != null && r.SrcKey.StartsWith(prefix)).ToListAsync();
        var exp = await db.Expenses.AsNoTracking().Where(r => r.SrcKey != null && r.SrcKey.StartsWith(prefix)).ToListAsync();

        var expectedRows = 0; var expectedRetail = 0; var expectedExp = 0;
        for (var i = 0; i < txns.Count; i++)
        {
            var t = txns[i];
            var key = WaraqPostingService.SrcKeyOf(w, ShiftKind.Day, i);
            var n = Names.First(x => x.Name == t.Name);
            var mineRows = rows.Where(r => r.SrcKey == key).ToList();
            var mineRetail = retail.Where(r => r.SrcKey == key).ToList();
            var mineExp = exp.Where(r => r.SrcKey == key).ToList();
            string What() => $"{where}: ردیفِ {i} «{t.Name}» {t.Type} {t.Unit} {t.Amount}";

            if (t.Amount == 0m)
            {
                Assert.True(mineRows.Count + mineRetail.Count + mineExp.Count == 0, What() + " — مبلغِ صفر جایی نشست");
                continue;
            }
            if (t.Type == WaraqTxnType.Expense)
            {
                Assert.True(mineExp.Count == 1 && mineRows.Count == 0 && mineRetail.Count == 0, What() + " — مصرف یک جا نیست");
                Assert.Equal(t.Amount, mineExp[0].Amount);
                expectedExp++;
                continue;
            }
            Assert.True(mineExp.Count == 0, What() + " — قرض در مصارف هم هست: "
                + string.Join(" ; ", mineExp.Select(e => $"#{e.Id} «{e.Title}» {e.Amount} {e.SrcKey}"))
                + " | ردیف‌ها: " + string.Join(" ; ", txns.Select((x, j) => $"{j}:{x.Name}/{x.Type}/{x.Amount}")));
            //  ⛔ (۱۴۰۵/۰۷/۲۲) آرشیو فقط **همان** تراکنش را نگه می‌دارد، در هر جایی که حالا
            //  نشسته — ردیفِ دیگری (حتی همان شخص و همان مبلغ) که به کلیدِ آرشیوشده رسید ثبت می‌شود
            if (t.SyncUid is { } uid && archived.Contains(uid) && n.To is not Dest.Retail)
            {
                Assert.True(mineRows.Count + mineRetail.Count == 0, What() + " — کلیدِ آرشیوشده دوباره ساخته شد");
                continue;
            }
            switch (n.To)
            {
                case Dest.None:
                    Assert.True(mineRows.Count + mineRetail.Count == 0, What() + " — نامِ ناشناس به حسابی رفت");
                    break;
                case Dest.Retail:
                    Assert.True(mineRetail.Count == 1 && mineRows.Count == 0, What() + " — چکنه یک جا نیست");
                    Assert.Equal(t.Amount, mineRetail[0].Bardagi);
                    expectedRetail++;
                    break;
                default:
                    Assert.True(mineRows.Count == 1 && mineRetail.Count == 0, What() + $" — {mineRows.Count} ردیفِ حساب");
                    var r = mineRows[0];
                    var money = t.Unit == LedgerMode.Money;
                    var accountId = money ? r.MoneyAccountId : r.FuelAccountId;
                    Assert.True(accountId == acct[n.To] && (money ? r.FuelAccountId : r.MoneyAccountId) is null,
                                What() + $" — در حساب/دفترِ اشتباه (تیل {r.FuelAccountId} · پول {r.MoneyAccountId})");
                    Assert.Equal(t.Amount, r.Bardagi);
                    Assert.Equal(n.Fuel, r.Fuel);
                    expectedRows++;
                    break;
            }
        }
        Assert.True(rows.Count == expectedRows, $"{where}: {rows.Count} ردیفِ حساب از این ورق، باید {expectedRows}");
        Assert.True(retail.Count == expectedRetail, $"{where}: {retail.Count} ردیفِ چکنه، باید {expectedRetail}");
        Assert.True(exp.Count == expectedExp, $"{where}: {exp.Count} مصرف، باید {expectedExp}");

        //  ⛔ ردیفِ دستی و رسیدش: یا زنده و دست‌نخورده، یا در آرشیو
        var live = await db.DebtRows.AsNoTracking().Where(r => r.Src == null && r.Name != null && r.Name.StartsWith("دستی-")).ToListAsync();
        var inArchive = new List<DebtRow>();
        foreach (var json in await db.DebtTableArchives.AsNoTracking().Select(a => a.RowsJson).ToListAsync())
            inArchive.AddRange((JsonSerializer.Deserialize<List<DebtRow>>(json ?? "[]") ?? new())
                               .Where(r => r.Src == null && r.Name != null && r.Name.StartsWith("دستی-")));
        var all = live.Concat(inArchive).ToList();
        Assert.True(all.Count == manualCount, $"{where}: ردیفِ دستی {all.Count} ≠ {manualCount}");
        Assert.True(all.Sum(r => r.Rasid) == manualRasid, $"{where}: رسیدِ دستی گم شد");
        Assert.All(all, r => Assert.Equal(r.Liters * 60m, r.Bardagi));
    }
}
