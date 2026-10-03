using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;
using PumpYaqobi.Services.Data;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ خروجیِ یک حساب یا بخش برای بازهٔ ماه/سال، و آوردنِ دوباره‌اش (۱۴۰۵/۰۷/۱۹) ══
/// «فایلِ خروجی دوباره وارد شود بدونِ خراب شدن». هر سناریو روی SQLiteِ واقعی و
/// از راهِ همان «فایلِ حساب» (‎PortableFile‎) — نه فقط از JSONِ درونِ حافظه.
/// </summary>
public class PortableExportTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"pump-port-{Guid.NewGuid():N}");
    public PortableExportTests() => Directory.CreateDirectory(_dir);
    public void Dispose() { try { Directory.Delete(_dir, true); } catch { } }

    private PumpDbFactory Db(string name)
    {
        var dbf = new PumpDbFactory(Path.Combine(_dir, name + ".db"));
        dbf.EnsureReady();
        return dbf;
    }

    private static long SeedDebtor(PumpDbFactory dbf)
    {
        using var db = dbf.Create();
        var d = new Debtor { Name = "کریم احمدی", MainAccount = new DebtAccount { Name = "کریم احمدی" } };
        db.Debtors.Add(d); db.SaveChanges();
        var a = d.MainAccount;
        db.DebtRows.Add(new DebtRow { FuelAccountId = a.Id, DateShamsi = "1405/06/10", DateKey = 14050610, Name = "تیل", Liters = 12960m, PricePerLiter = 60.14m, SortIndex = 1 });
        db.DebtRows.Add(new DebtRow { FuelAccountId = a.Id, DateShamsi = "1405/07/03", DateKey = 14050703, Name = "حوالهٔ 7 نان", Liters = 50m, PricePerLiter = 60.14m, SortIndex = 2 });
        db.DebtRows.Add(new DebtRow { FuelAccountId = a.Id, DateShamsi = "1404/01/01", DateKey = 14040101, Name = "کهنه", Liters = 1m, SortIndex = 3 });
        db.SaveChanges();
        return d.Id;
    }

    private string RoundTripFile(PumpDbFactory from, PortablePick pick, out PortableExport ex)
    {
        ex = new SyncStore(from).ExportPortable(pick);
        var path = Path.Combine(_dir, $"part-{Guid.NewGuid():N}{PortableFile.Extension}");
        PortableFile.Write(path, "آزمون", ex);
        return path;
    }

    private static PortableImport Import(PumpDbFactory to, string path)
    {
        var json = PortableFile.ReadSnapshot(path);
        Assert.NotNull(json);
        using var doc = JsonDocument.Parse(json!);
        return new SyncStore(to).ImportPortable(doc.RootElement);
    }

    [Fact]
    public void Gharzdar_BaBazeyeMah_DarDaftareTaze_DaghighMineshinad()
    {
        var a = Db("a");
        var id = SeedDebtor(a);
        var path = RoundTripFile(a, new PortablePick("debtor", id, 14050601, 14050731), out var ex);
        Assert.True(ex.RowCount > 0);
        if (Environment.GetEnvironmentVariable("PUMP_PORTABLE_KEEP") is { Length: > 0 } keep) File.Copy(path, keep, true);

        var b = Db("b");
        var rep = Import(b, path);
        Assert.Equal(0, rep.Failed);
        using var db = b.Create();
        var d = db.Debtors.AsNoTracking().Single();
        Assert.Equal("کریم احمدی", d.Name);
        var acct = db.DebtAccounts.AsNoTracking().Single();
        Assert.Equal(d.Id, acct.MainOfDebtorId);
        var rows = db.DebtRows.AsNoTracking().OrderBy(r => r.SortIndex).ToList();
        Assert.Equal(2, rows.Count);                         // ردیفِ ۱۴۰۴ بیرونِ بازه است
        Assert.All(rows, r => Assert.Equal(acct.Id, r.FuelAccountId));
        Assert.Equal(12960m, rows[0].Liters);                 // نه 12906، نه 2960
        Assert.Equal(60.14m, rows[0].PricePerLiter);
        Assert.Equal("حوالهٔ 7 نان", rows[1].Name);          // نوشتهٔ آمیخته دست نخورد
    }

    [Fact]
    public void AvardaneDobare_ChiziRaDoTaNemikonad_VaPakNemikonad()
    {
        var a = Db("a");
        var id = SeedDebtor(a);
        var path = RoundTripFile(a, new PortablePick("debtor", id), out _);

        var first = Import(a, path);                          // روی همان دفتر
        Assert.Equal(0, first.Added);
        using (var db = a.Create())
        {
            Assert.Equal(1, db.Debtors.Count());
            Assert.Equal(3, db.DebtRows.Count());
            //  ردیفی که این‌جا تازه آمده و در فایل نیست پاک نمی‌شود
            db.DebtRows.Add(new DebtRow { FuelAccountId = db.DebtAccounts.First().Id, DateShamsi = "1405/07/05", DateKey = 14050705, Name = "تازه", SortIndex = 9 });
            db.SaveChanges();
        }
        Import(a, path);
        using (var db = a.Create()) Assert.Equal(4, db.DebtRows.Count());
    }

    [Fact]
    public void RadifePakShode_BaAvardan_ZendeNemishavad()
    {
        var a = Db("a");
        var id = SeedDebtor(a);
        var path = RoundTripFile(a, new PortablePick("debtor", id), out _);
        using (var db = a.Create())
        {
            var r = db.DebtRows.First(x => x.Name == "کهنه");
            r.DeletedAt = DateTime.UtcNow;
            db.SaveChanges();
        }
        Import(a, path);
        using var read = a.Create();
        Assert.Equal(2, read.DebtRows.Count(x => x.DeletedAt == null));
    }

    [Fact]
    public void FayleGharibe_Rad_Mishavad_VaChiziNemineshinad()
    {
        var b = Db("b");
        using var doc = JsonDocument.Parse("{\"format\":\"x\",\"tables\":{}}");
        Assert.Throws<InvalidDataException>(() => new SyncStore(b).ImportPortable(doc.RootElement));
        var junk = Path.Combine(_dir, "junk" + PortableFile.Extension);
        File.WriteAllText(junk, "not a zip");
        Assert.Null(PortableFile.ReadSnapshot(junk));
    }

    [Fact]
    public void Gavsandoogh_BaBazeyeSal()
    {
        var a = Db("a");
        using (var db = a.Create())
        {
            db.SafeEntries.Add(new SafeEntry { Title = "۱۴۰۵", Amount = 12960, DateShamsi = "1405/02/01", DateKey = 14050201, MonthKey = "1405/02", Currency = Currency.Usd });
            db.SafeEntries.Add(new SafeEntry { Title = "۱۴۰۴", Amount = 5, DateShamsi = "1404/02/01", DateKey = 14040201, MonthKey = "1404/02" });
            db.SaveChanges();
        }
        var path = RoundTripFile(a, new PortablePick("safe", 0, 14050101, 14051230), out _);
        var b = Db("b");
        Import(b, path);
        using var read = b.Create();
        var e = read.SafeEntries.AsNoTracking().Single();
        Assert.Equal(12960m, e.Amount);
        Assert.Equal(Currency.Usd, e.Currency);
    }

    [Fact]
    public void FayleHesab_MaleKhodeBarname_Ast_NaEksel_VaDastKhordeRaRadMikonad()
    {
        //  «مثلِ اکسل، نه خودِ اکسل — اسمِ اکسل را بردار» (۱۴۰۵/۰۷/۱۹)
        Assert.Equal(".pumphesab", PortableFile.Extension);
        var a = Db("a");
        var id = SeedDebtor(a);
        var path = RoundTripFile(a, new PortablePick("debtor", id), out var ex);
        using (var zip = System.IO.Compression.ZipFile.OpenRead(path))
        {
            Assert.Equal(new[] { "manifest.json", "part.json" }, zip.Entries.Select(e => e.FullName).OrderBy(x => x));
            Assert.DoesNotContain(zip.Entries, e => e.FullName.StartsWith("xl/"));
        }
        Assert.Equal(ex.SnapshotJson, PortableFile.ReadSnapshot(path));

        //  ⛔ یک بایتِ دست‌خورده در عکس ⇒ هیچ چیزی خوانده نمی‌شود (هش)
        var bad = Path.Combine(_dir, "bad" + PortableFile.Extension);
        File.Copy(path, bad);
        using (var zip = System.IO.Compression.ZipFile.Open(bad, System.IO.Compression.ZipArchiveMode.Update))
        {
            var e = zip.GetEntry("part.json")!;
            string text;
            using (var r = new StreamReader(e.Open())) text = r.ReadToEnd();
            e.Delete();
            using var w = new StreamWriter(zip.CreateEntry("part.json").Open());
            w.Write(text.Replace("12960", "12906"));
        }
        Assert.Null(PortableFile.ReadSnapshot(bad));

        //  و در کلِ سورسِ این راه هیچ «اکسل»ی نیست
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../"));
        foreach (var f in new[] { "PumpYaqobi.Services/Data/PortableFile.cs", "PumpYaqobi.App/Views/Sections/BackupSectionView.axaml" })
        {
            var src = File.ReadAllText(Path.Combine(root, f));
            Assert.DoesNotContain(".xlsx", src);
            Assert.DoesNotContain("فایلِ اکسل", src);
        }
    }

    [Fact]
    public void YekMah_FaghatHamanMah()
    {
        var a = Db("a");
        using (var db = a.Create())
        {
            db.Expenses.Add(new Expense { Title = "سنبله", Amount = 100, DateShamsi = "1405/06/05", DateKey = 14050605, MonthKey = "1405/06" });
            db.Expenses.Add(new Expense { Title = "میزان", Amount = 200, DateShamsi = "1405/07/05", DateKey = 14050705, MonthKey = "1405/07" });
            db.SaveChanges();
        }
        var path = RoundTripFile(a, new PortablePick("expenses", 0, 14050601, 14050631), out _);
        var b = Db("b");
        Import(b, path);
        using var read = b.Create();
        Assert.Equal("سنبله", read.Expenses.AsNoTracking().Single().Title);
    }

    [Theory]
    [InlineData("", false, 0)]
    [InlineData("1405/03", false, 14050301)]
    [InlineData("1405/03", true, 14050331)]
    [InlineData("۱۴۰۵/۰۶", true, 14050631)]
    [InlineData("1405", false, 14050101)]
    [InlineData("1405", true, 14051231)]
    [InlineData("1405/13", false, -1)]
    [InlineData("abc", false, -1)]
    public void BazeyeMah_Khande_Mishavad(string text, bool end, int key) =>
        Assert.Equal(key, PumpYaqobi.App.ViewModels.Sections.BackupSectionViewModel.MonthKeyOf(text, end));

    [Fact]
    public void PorseshePishAzBastan_FaghatVaghtiDaftarAvazShode()
    {
        Assert.False(PumpYaqobi.App.Services.ExitBackup.Changed(-1, 5));   // پیش از ورود
        Assert.False(PumpYaqobi.App.Services.ExitBackup.Changed(5, 5));    // فقط نگاه کرد
        Assert.True(PumpYaqobi.App.Services.ExitBackup.Changed(5, 6));     // نوشت
    }
}
