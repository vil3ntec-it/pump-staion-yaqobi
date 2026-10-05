using Microsoft.Data.Sqlite;
using PumpYaqobi.App.Services;
using PumpYaqobi.Application.Security;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;
using PumpYaqobi.Services.Data;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ «❌ به هیچ سروری نرسید — عکسِ پشتیبان گرفته نشد» (۱۴۰۵/۰۷/۲۱) ══════════
/// ریشه: ‎BackupService.Inspect‎ عکسِ امروز را با اتصالِ <b>استخری</b> می‌خواند و
/// اتصال پس از ‎Dispose‎ در استخر باز می‌ماند. روی ویندوز ‎File.Move(…, overwrite)‎
/// روی فایلِ باز «sharing violation» می‌دهد، پس عکسِ بعدیِ همان روز ساخته نمی‌شد —
/// و ‎SnapshotToday‎ خطا را بی‌صدا می‌خورد. روی لینوکس جابه‌جایی روی فایلِ باز
/// بی‌صدا می‌گذرد، پس این سنجه دستهٔ بازِ فایل را مستقیم از ‎/proc/self/fd‎
/// می‌شمارد (و روی ویندوز با بازکردنِ انحصاری).
/// ⚠️ ‎MoveForTests‎ ایستاست؛ این کلاس با کلاس‌های دیگرِ بکاپ هم‌زمان نمی‌دود.
/// </summary>
[Collection("SnapshotMove")]
public class SnapshotHandleTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "pump-snaph-" + Guid.NewGuid().ToString("N"));
    public SnapshotHandleTests() => Directory.CreateDirectory(_dir);
    public void Dispose()
    {
        BackupService.MoveForTests = null;
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(_dir, true); } catch { }
    }

    private (PumpDbFactory Db, BackupService Backup) Host()
    {
        var dbf = new PumpDbFactory(Path.Combine(_dir, "pump.db"));
        dbf.EnsureReady();
        var session = new UserSession();
        session.SignIn(UserRole.Admin, "آزمون");
        using (var db = dbf.Create())
        {
            var d = new Debtor { LegacyId = "d1", Name = "مشتری", MainAccount = new DebtAccount { Name = "مشتری" } };
            d.MainAccount.FuelRows.Add(new DebtRow { DateShamsi = "1405/07/01", Liters = 10 });
            db.Debtors.Add(d);
            db.SaveChanges();
        }
        return (dbf, new BackupService(dbf, new PermissionService(session)));
    }

    /// <summary>چند دستهٔ باز از همین پروسه به این فایل اشاره می‌کند.</summary>
    private static int OpenHandles(string path)
    {
        var full = Path.GetFullPath(path);
        if (OperatingSystem.IsLinux())
        {
            var n = 0;
            foreach (var fd in Directory.EnumerateFileSystemEntries("/proc/self/fd"))
            {
                try
                {
                    var target = new FileInfo(fd).LinkTarget;
                    if (string.Equals(target, full, StringComparison.Ordinal)) n++;
                }
                catch { /* همان لحظه بسته شد */ }
            }
            return n;
        }
        try { using var fs = new FileStream(full, FileMode.Open, FileAccess.ReadWrite, FileShare.None); return 0; }
        catch (IOException) { return 1; }
    }

    [Fact]
    public void Inspect_DasteyeBazRaNegahNemidarad()
    {
        var (_, backup) = Host();
        var snap = backup.SnapshotToday();
        Assert.NotNull(snap);
        Assert.Equal(0, OpenHandles(snap!));
        Assert.True(BackupService.Inspect(snap!) > 0);
        Assert.Equal(0, OpenHandles(snap!));   // ⛔ پیش از اصلاح: ۱ — اتصالِ استخری باز ماند
    }

    [Fact]
    public void AzmouneBazyabi_VaShomareshVaSalamat_DasteyeBazNemigozarand()
    {
        var (db, backup) = Host();
        var snap = backup.SnapshotToday()!;
        Assert.Null(RestoreDrill.Verify(snap, db.DbPath, out _));
        Assert.True(FullBackup.IntegrityOk(snap));
        Assert.NotNull(FullBackup.CountRows(snap));
        Assert.True(BackupService.Inspect(snap) > 0);
        Assert.Equal(0, OpenHandles(snap));
    }

    [Fact]
    public void PasAzInspect_AksDovomeHamanRuz_SakhteMishavad()
    {
        var (_, backup) = Host();
        var first = backup.SnapshotToday();
        Assert.NotNull(first);
        Assert.True(BackupService.Inspect(first!) > 0);   // همان کاری که ‎BackupPusher‎ می‌کند
        Assert.Equal(0, OpenHandles(first!));
        var second = backup.SnapshotToday();
        Assert.NotNull(second);
        Assert.Equal("", backup.LastSnapshotError);
        Assert.True(BackupService.Inspect(second!) > 0);
        Assert.NotNull(backup.SnapshotToday());
    }

    [Fact]
    public void JabejaiyeGhofl_BaNameTaze_NaShekast()
    {
        //  ویندوز: فایلِ امروز را ضدِ ویروس یا برنامهٔ دیگری باز نگه داشته ⇒ جابه‌جایی همیشه شکست
        var (_, backup) = Host();
        var first = backup.SnapshotToday()!;
        var tries = 0;
        BackupService.MoveForTests = (src, dst) =>
        {
            if (string.Equals(dst, first, StringComparison.Ordinal))
            {
                tries++;
                throw new IOException("The process cannot access the file because it is being used by another process.");
            }
            File.Move(src, dst, overwrite: true);
        };
        var second = backup.SnapshotToday();
        Assert.NotNull(second);
        Assert.NotEqual(first, second);
        Assert.True(tries >= 2, "دوباره امتحان نشد: " + tries);
        Assert.True(File.Exists(second));
        Assert.True(BackupService.Inspect(second!) > 0);
        Assert.Equal("", backup.LastSnapshotError);
        Assert.Empty(Directory.EnumerateFiles(backup.SnapshotDir, "*.part"));
        //  فهرست هر دو را با روزِ درست نشان می‌دهد، تازه‌تر اول
        var list = backup.List();
        Assert.Equal(2, list.Count);
        Assert.All(list, f => Assert.Matches(@"^\d{4}/\d{2}/\d{2}$", f.Day));
    }

    [Fact]
    public void AksNashod_DalilashGofteMishavad_NaSokout()
    {
        var (db, backup) = Host();
        //  جای پوشهٔ بکاپ یک فایل است ⇒ ساختنِ پوشه شکست می‌خورد
        File.WriteAllText(Path.Combine(Path.GetDirectoryName(db.DbPath)!, "backups"), "x");
        Assert.Null(backup.SnapshotToday());
        Assert.False(string.IsNullOrWhiteSpace(backup.LastSnapshotError));
    }

    [Fact]
    public void Manba_HichSqliteConnectionBiPoolingFalseBarayeBackup()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../"));
        var hits = new List<string>();
        foreach (var proj in new[] { "PumpYaqobi.Services", "PumpYaqobi.Shell", "PumpYaqobi.App" })
            foreach (var f in Directory.EnumerateFiles(Path.Combine(root, proj), "*.cs", SearchOption.AllDirectories))
            {
                var norm = f.Replace('\\', '/');
                if (norm.Contains("/obj/") || norm.Contains("/bin/")) continue;
                var text = File.ReadAllText(f);
                var n = System.Text.RegularExpressions.Regex.Matches(text, @"new\s+SqliteConnection\s*\(").Count;
                for (var i = 0; i < n; i++) hits.Add(Path.GetFileName(f));
            }
        //  ⛔ هر خواندنِ فایلِ بکاپ از ‎BackupService.OpenReadOnly‎ (یک جا، ‎Pooling = false‎).
        //  ‎ChatStore‎ دفترِ بکاپ نیست و خودش ‎Pooling = false‎ دارد.
        Assert.Equal(new[] { "BackupService.cs", "ChatStore.cs" }, hits.OrderBy(x => x, StringComparer.Ordinal).ToArray());
        var svc = File.ReadAllText(Path.Combine(root, "PumpYaqobi.Services/Data/BackupService.cs"));
        var i0 = svc.IndexOf("public static SqliteConnection OpenReadOnly", StringComparison.Ordinal);
        Assert.True(i0 > 0, "OpenReadOnly نیست");
        var body = svc[i0..svc.IndexOf("con.Open();", i0, StringComparison.Ordinal)];
        Assert.Contains("Pooling = false", body);
        Assert.Contains("new SqliteConnection(", body);
        var full = File.ReadAllText(Path.Combine(root, "PumpYaqobi.Services/Data/FullBackup.cs"));
        Assert.Contains("BackupService.OpenReadOnly(", full);
        //  و ‎SnapshotToday‎ دیگر بی‌صدا نمی‌خورد
        var st = svc[svc.IndexOf("public string? SnapshotToday()", StringComparison.Ordinal)..];
        st = st[..st.IndexOf("public void Prune()", StringComparison.Ordinal)];
        Assert.DoesNotContain("catch { return null; }", st);
        Assert.Contains("LastSnapshotError", st);
    }
}
