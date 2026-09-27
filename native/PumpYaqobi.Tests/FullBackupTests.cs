using System.IO.Compression;
using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;
using PumpYaqobi.Application.Security;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;
using PumpYaqobi.Services.Data;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ «فایلِ کاملِ برنامه» (‎.pumpyaqobi‎) — ۱۴۰۵/۰۷/۱۵ ══════════════════════
///
/// خواستهٔ صاحب ریپو: «اطلاعاتِ همان فایل همه‌شان بیاید… ببین مشکل یا باگی
/// نداشته باشد، اطلاعات است و خیلی مهم.» پس این‌جا روی <b>فایلِ واقعیِ
/// SQLite</b>، هر خانهٔ هر ردیفِ هر جدول پیش و پس از رفت‌وبرگشت مقایسه
/// می‌شود — و هر شکلِ خرابِ فایل (نیمه‌کپی، دست‌خورده، غریبه، تازه‌تر)
/// باید رد شود <b>بی آن‌که یک بیت از دفترِ فعلی عوض شود</b>.
/// </summary>
public class FullBackupTests : IDisposable
{
    private const string Ver = "3.1.200";

    private readonly string _dir =
        Path.Combine(Path.GetTempPath(), "pump-full-" + Guid.NewGuid().ToString("N"));

    public FullBackupTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(_dir, recursive: true); } catch { }
    }

    private (PumpDbFactory Db, BackupService Backup) Host(string name, UserRole role = UserRole.Admin)
    {
        var d = Path.Combine(_dir, name);
        Directory.CreateDirectory(d);
        var dbf = new PumpDbFactory(Path.Combine(d, "pump.db"));
        dbf.EnsureReady();
        var session = new UserSession();
        session.SignIn(role, "آزمون");
        return (dbf, new BackupService(dbf, new PermissionService(session)));
    }

    /// <summary>دادهٔ «سخت»: رقمِ اعشاریِ بلند، فارسی، تهی، و چند جدولِ به‌هم‌بسته.</summary>
    private static async Task SeedAsync(PumpDbFactory dbf, string tag, int people)
    {
        await using var db = dbf.Create();
        for (var i = 0; i < people; i++)
        {
            var p = new Debtor { Name = $"{tag} کریم‌جان {i}", LegacyId = tag + i };
            for (var r = 0; r < 25; r++)
                p.MainAccount.FuelRows.Add(new DebtRow
                {
                    DateShamsi = $"1405/0{1 + r % 9}/1{r % 10}",
                    Name = r % 3 == 0 ? "بردگی «پطرول»" : "رسید",
                    Liters = 12.345678m + r,
                    PricePerLiter = 79.99m,
                });
            db.Debtors.Add(p);
        }
        db.SafeEntries.Add(new SafeEntry { DateShamsi = "1405/07/15", MonthKey = "1405/07", Title = tag + " گاوصندوق", Amount = 1234567.89m });
        db.Expenses.Add(new Expense { DateShamsi = "1405/07/15", MonthKey = "1405/07", Title = tag + " مصرف", Amount = 250m, Note = null });
        await db.SaveChangesAsync();
    }

    /// <summary>همهٔ خانه‌های همهٔ ردیف‌های همهٔ جدول‌ها، به ترتیبِ ‎rowid‎.</summary>
    private static Dictionary<string, List<string>> Dump(string dbPath)
    {
        var cs = new SqliteConnectionStringBuilder { DataSource = dbPath, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString();
        using var con = new SqliteConnection(cs);
        con.Open();
        var tables = new List<string>();
        using (var cmd = con.CreateCommand())
        {
            cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%';";
            using var r = cmd.ExecuteReader();
            while (r.Read()) tables.Add(r.GetString(0));
        }
        var all = new Dictionary<string, List<string>>();
        foreach (var t in tables)
        {
            if (FullBackup.Housekeeping.Contains(t)) continue;
            var rows = new List<string>();
            using var cmd = con.CreateCommand();
            cmd.CommandText = $"SELECT * FROM \"{t}\" ORDER BY rowid;";
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                var cells = new string[r.FieldCount];
                for (var c = 0; c < r.FieldCount; c++)
                    cells[c] = r.IsDBNull(c) ? "∅" : Convert.ToString(r.GetValue(c), System.Globalization.CultureInfo.InvariantCulture)!;
                rows.Add(string.Join("|", cells));
            }
            all[t] = rows;
        }
        return all;
    }

    private string Export(BackupService backup, string settings = "{\"v\":1,\"ThemeId\":\"gold\"}")
    {
        var file = Path.Combine(_dir, "flash", FullBackup.SuggestedFileName("پمپ آزمون"));
        var info = FullBackup.Write(backup, file, Ver, settings, "پمپ آزمون");
        Assert.True(info.Ok, info.Why);
        return file;
    }

    // ══ رفت‌وبرگشت ══════════════════════════════════════════════════════════

    [Fact]
    public async Task RaftoBargasht_HarKhaneyeHarRadif_MooBeMoo()
    {
        var (a, ba) = Host("a");
        await SeedAsync(a, "الف", 12);
        var file = Export(ba, "{\"v\":1,\"ThemeId\":\"gold\",\"NavOrder\":\"safe,dashboard\"}");
        Assert.EndsWith(FullBackup.Extension, file);
        Assert.False(File.Exists(file + ".part"), "فایلِ نیمه‌کاره ماند");

        // کامپیوترِ دوم: دفترِ دیگری دارد که باید کاملاً جایگزین شود
        var (b, bb) = Host("b");
        await SeedAsync(b, "ب", 3);

        using var info = FullBackup.Read(file, Ver);
        Assert.True(info.Ok, info.Why);
        Assert.Equal(Ver, info.AppVersion);
        Assert.Equal("پمپ آزمون", info.PumpName);
        Assert.Equal(12, info.Rows("Debtors"));
        Assert.Equal(12 * 25, info.Rows("DebtRows"));
        Assert.Contains("\"gold\"", info.SettingsJson);
        Assert.Contains("safe,dashboard", info.SettingsJson);

        var restored = FullBackup.Restore(bb, info);
        Assert.True(restored.Ok, restored.Message);
        Assert.NotNull(restored.SafetyCopy);                                  // راهِ برگشت هست
        Assert.Null(FullBackup.VerifyRestored(b.DbPath, info));

        SqliteConnection.ClearAllPools();
        var want = Dump(a.DbPath);
        var got = Dump(b.DbPath);
        Assert.Equal(want.Keys.OrderBy(x => x), got.Keys.OrderBy(x => x));
        foreach (var (t, rows) in want)
            Assert.True(rows.SequenceEqual(got[t]), $"جدولِ «{t}» مو‌به‌مو نیامد");

        //  و عکسِ ایمنی همان دفترِ قبلیِ «ب» را دارد
        Assert.Equal(3, FullBackup.CountRows(restored.SafetyCopy!)!["Debtors"]);

        //  دفترِ موقتِ بازشده پس از کار پاک می‌شود
        var tmp = info.DbPath!;
        info.Dispose();
        Assert.False(File.Exists(tmp), "دفترِ موقت پاک نشد");
    }

    [Fact]
    public async Task FileBiTanzimat_HamKarMikonad()
    {
        var (a, ba) = Host("a");
        await SeedAsync(a, "الف", 2);
        var file = Path.Combine(_dir, "x" + FullBackup.Extension);
        Assert.True(FullBackup.Write(ba, file, Ver, null).Ok);
        using var info = FullBackup.Read(file, Ver);
        Assert.True(info.Ok, info.Why);
        Assert.Null(info.SettingsJson);
    }

    // ══ فایلِ خراب — هیچ چیزی عوض نمی‌شود ══════════════════════════════════

    private async Task<(PumpDbFactory B, BackupService Bb, Dictionary<string, List<string>> Before)> TargetAsync()
    {
        var (b, bb) = Host("b");
        await SeedAsync(b, "ب", 3);
        SqliteConnection.ClearAllPools();
        return (b, bb, Dump(b.DbPath));
    }

    private static void RewriteEntry(string zipPath, string entry, Func<byte[], byte[]> change)
    {
        using var z = ZipFile.Open(zipPath, ZipArchiveMode.Update);
        var e = z.GetEntry(entry)!;
        byte[] bytes;
        using (var s = e.Open()) { using var m = new MemoryStream(); s.CopyTo(m); bytes = m.ToArray(); }
        e.Delete();
        var n = z.CreateEntry(entry);
        using var w = n.Open();
        var nb = change(bytes);
        w.Write(nb, 0, nb.Length);
    }

    private void AssertRejectedAndUntouched(string file, string why, Dictionary<string, List<string>> before, PumpDbFactory b)
    {
        using var info = FullBackup.Read(file, Ver);
        Assert.False(info.Ok, "فایلِ خراب پذیرفته شد");
        Assert.Contains(why, info.Why);
        Assert.Null(info.DbPath);
        //  ⛔ رد شد ⇒ یک بیت از دفترِ فعلی عوض نشد
        SqliteConnection.ClearAllPools();
        var after = Dump(b.DbPath);
        foreach (var (t, rows) in before) Assert.True(rows.SequenceEqual(after[t]), $"«{t}» عوض شد");
    }

    [Fact]
    public async Task DaftareDastKhorde_RadMishavad()
    {
        var (a, ba) = Host("a");
        await SeedAsync(a, "الف", 4);
        var file = Export(ba);
        var (b, _, before) = await TargetAsync();
        //  یک بایت از وسطِ دفتر عوض شود — اندازه همان، اثرِ انگشت نه
        RewriteEntry(file, "pump.db", x => { x[x.Length / 2] ^= 0x5A; return x; });
        AssertRejectedAndUntouched(file, "اثرِ انگشت", before, b);
    }

    [Fact]
    public async Task KopiyeNimekare_RadMishavad()
    {
        var (a, ba) = Host("a");
        await SeedAsync(a, "الف", 4);
        var file = Export(ba);
        var (b, _, before) = await TargetAsync();
        var bytes = File.ReadAllBytes(file);
        File.WriteAllBytes(file, bytes[..(bytes.Length / 2)]);                // فلش وسطِ کپی کشیده شد
        using var info = FullBackup.Read(file, Ver);
        Assert.False(info.Ok);
        SqliteConnection.ClearAllPools();
        var after = Dump(b.DbPath);
        foreach (var (t, rows) in before) Assert.True(rows.SequenceEqual(after[t]));
    }

    [Fact]
    public async Task FileGharibe_RadMishavad()
    {
        var (b, _, before) = await TargetAsync();
        var junk = Path.Combine(_dir, "junk" + FullBackup.Extension);
        File.WriteAllText(junk, "این یک فایلِ متنی است");
        AssertRejectedAndUntouched(junk, "نیست", before, b);

        //  زیپی که فهرست ندارد
        var zip = Path.Combine(_dir, "nozip" + FullBackup.Extension);
        using (var z = ZipFile.Open(zip, ZipArchiveMode.Create)) z.CreateEntry("pump.db");
        AssertRejectedAndUntouched(zip, "نیست", before, b);

        //  دفترِ SQLiteی که مالِ این برنامه نیست — با فهرستِ «درست»
        var other = Path.Combine(_dir, "other.db");
        using (var con = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = other, Pooling = false }.ToString()))
        {
            con.Open();
            using var cmd = con.CreateCommand();
            cmd.CommandText = "CREATE TABLE Foo(x); INSERT INTO Foo VALUES(1);";
            cmd.ExecuteNonQuery();
        }
        var fake = Path.Combine(_dir, "fake" + FullBackup.Extension);
        using (var z = ZipFile.Open(fake, ZipArchiveMode.Create))
        {
            var m = new JsonObject
            {
                ["format"] = FullBackup.FormatName, ["formatVersion"] = 1, ["appVersion"] = Ver,
                ["db"] = new JsonObject { ["sha256"] = FullBackup.Sha256(other), ["bytes"] = new FileInfo(other).Length },
                ["tables"] = new JsonObject { ["Foo"] = 1 },
            };
            using (var w = new StreamWriter(z.CreateEntry("manifest.json").Open())) w.Write(m.ToJsonString());
            z.CreateEntryFromFile(other, "pump.db");
        }
        AssertRejectedAndUntouched(fake, "دفترِ برنامهٔ پمپ ندارد", before, b);
    }

    [Fact]
    public async Task NoskheyeTazeTar_RadMishavad()
    {
        var (a, ba) = Host("a");
        await SeedAsync(a, "الف", 2);
        var file = Export(ba);
        var (b, _, before) = await TargetAsync();

        //  برنامهٔ کهنه‌تر فایلِ نسخهٔ تازه‌تر را نمی‌خواند (ستونی را نمی‌شناسد)
        using (var info = FullBackup.Read(file, "3.1.150"))
        {
            Assert.False(info.Ok);
            Assert.Contains("به‌روز کنید", info.Why);
        }

        //  قالبِ تازه‌تر هم
        RewriteEntry(file, "manifest.json", x =>
        {
            var m = JsonNode.Parse(x)!.AsObject();
            m["formatVersion"] = FullBackup.FormatVersion + 1;
            return System.Text.Encoding.UTF8.GetBytes(m.ToJsonString());
        });
        AssertRejectedAndUntouched(file, "تازه‌ترِ برنامه", before, b);
    }

    [Fact]
    public async Task ShomareRadifha_NakhanadRadMishavad()
    {
        var (a, ba) = Host("a");
        await SeedAsync(a, "الف", 3);
        var file = Export(ba);
        var (b, _, before) = await TargetAsync();
        //  فهرست دست بخورد (اثرِ انگشتِ دفتر همان) — شمار باید بگیردش
        RewriteEntry(file, "manifest.json", x =>
        {
            var m = JsonNode.Parse(x)!.AsObject();
            m["tables"]!["DebtRows"] = 999;
            return System.Text.Encoding.UTF8.GetBytes(m.ToJsonString());
        });
        AssertRejectedAndUntouched(file, "DebtRows", before, b);
    }

    /// <summary>⛔ نامِ ‎../‎ داخلِ زیپ هیچ‌جا نوشته نمی‌شود — فقط نامِ ثابتِ خودمان باز می‌شود.</summary>
    [Fact]
    public async Task NameKhatarnakDarZip_HichJaNeveshteNemishavad()
    {
        var (a, ba) = Host("a");
        await SeedAsync(a, "الف", 2);
        var file = Export(ba);
        var evilName = "evil-" + Guid.NewGuid().ToString("N") + ".txt";
        using (var z = ZipFile.Open(file, ZipArchiveMode.Update))
        using (var w = new StreamWriter(z.CreateEntry("../../" + evilName).Open())) w.Write("x");

        using var info = FullBackup.Read(file, Ver);
        Assert.True(info.Ok, info.Why);
        var tmp = Path.GetTempPath();
        Assert.False(File.Exists(Path.Combine(tmp, evilName)));
        Assert.False(File.Exists(Path.GetFullPath(Path.Combine(tmp, "..", evilName))));
        Assert.False(File.Exists(Path.GetFullPath(Path.Combine(tmp, "..", "..", evilName))));
    }

    [Fact]
    public async Task BiEjazeyeBackup_HichFayliSakhteNemishavad()
    {
        var (a, _) = Host("a");
        await SeedAsync(a, "الف", 2);
        var (_, staffBackup) = Host("a", UserRole.Staff);
        var file = Path.Combine(_dir, "no" + FullBackup.Extension);
        Assert.Throws<PermissionDeniedException>(() => FullBackup.Write(staffBackup, file, Ver, null));
        Assert.False(File.Exists(file));
        Assert.False(File.Exists(file + ".part"));
    }

    [Fact]
    public void Rad_BiFayleSanjideShode_ChiziAvazNemishavad()
    {
        var (_, bb) = Host("b");
        var bad = new FullBackupInfo(false, "x", "", "", "", "", new Dictionary<string, long>(), 0, null, null);
        Assert.False(FullBackup.Restore(bb, bad).Ok);
    }

    [Fact]
    public void NamePishnahadi_PasvandVaBiNevisehyeNaMojaz()
    {
        var n = FullBackup.SuggestedFileName("پمپ/یعقوبی:مرکزی");
        Assert.EndsWith(FullBackup.Extension, n);
        Assert.DoesNotContain("/", n);
        Assert.DoesNotContain(":", n);
        Assert.True(FullBackup.IsNewer("3.1.200", "3.1.199"));
        Assert.False(FullBackup.IsNewer("3.1.199", "3.1.199"));
        Assert.False(FullBackup.IsNewer("", "3.1.199"));
    }
}
