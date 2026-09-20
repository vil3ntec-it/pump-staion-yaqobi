using Microsoft.EntityFrameworkCore;
using PumpYaqobi.Domain;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;
using PumpYaqobi.Persistence;
using PumpYaqobi.Services.Data;
using System.Text.Json;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ دفترِ تغییرات — «هر نوشتنی یک op، و هیچ نوشتنی بی op» ════════════════
///
/// بندِ ۲۰٫۱ پرامپت و بندِ ۱ی پرامپتِ ۲۲. این پرونده همان چهار چیزی را
/// قفل می‌کند که اگر بشکنند، همگام‌سازی بی‌صدا دروغ می‌گوید:
///
///   ۱) هر افزودن/ویرایش/حذف op می‌سازد، و ویرایش فقط فیلدِ عوض‌شده را دارد
///   ۲) جدول‌های مالِ همین کامپیوتر هیچ‌وقت op نمی‌سازند
///   ۳) op و خودِ داده در یک تراکنش‌اند
///   ۴) مهاجرت از دیتابیسِ امروز، هیچ ردیفی را نمی‌برد
///
/// ⚠️ روی SQLiteِ <b>واقعیِ روی دیسک</b>، نه درون‌حافظه‌ای — چیزی که باید
/// ثابت شود همان رفتارِ فایلِ واقعی است.
/// </summary>
public class SyncOpLogTests : IDisposable
{
    private readonly string _file = Path.Combine(Path.GetTempPath(), $"pump-oplog-{Guid.NewGuid():N}.db");
    private readonly PumpDbFactory _dbf;

    public SyncOpLogTests()
    {
        _dbf = new PumpDbFactory(_file);
        _dbf.EnsureReady();
    }

    public void Dispose()
    {
        OpLog.Enabled = true;
        foreach (var f in new[] { _file, _file + "-wal", _file + "-shm" })
            try { if (File.Exists(f)) File.Delete(f); } catch { }
        GC.SuppressFinalize(this);
    }

    private static SafeEntry Row(string title = "ردیفِ آزمون") =>
        new() { Title = title, Amount = 100m, DateKey = 14050701, MonthKey = "1405/07" };

    // ── ۱) هر نوشتن یک op ──────────────────────────────────────────────

    [Fact]
    public void Afzoodan_Yek_Op_E_Insert_Misazad()
    {
        using (var db = _dbf.Create())
        {
            db.SafeEntries.Add(Row());
            db.SaveChanges();
        }

        using var read = _dbf.Create();
        var op = Assert.Single(read.SyncOps.Where(x => x.TableName == nameof(SafeEntry)).ToList());
        Assert.Equal("insert", op.OpType);
        Assert.False(op.Synced);
        //  شناسه ULID است و مرتب — سرور با همین «تکراری» را می‌شناسد
        Assert.True(Ulid.IsUlid(op.OpId));
        //  و شناسهٔ ردیف همان چیزی است که روی خودِ ردیف نشسته
        var row = read.SafeEntries.Single();
        Assert.Equal(row.SyncUid, op.RowUid);
        Assert.False(string.IsNullOrEmpty(row.SyncUid));
    }

    /// <summary>
    /// ⛔ <b>مهم‌ترین سنجهٔ این پرونده</b>: «تغییرِ یک حرف در یک اسم = یک op
    /// با یک فیلد». بی این، هر ویرایشِ کوچک کلِ ردیف را می‌فرستاد و قانونِ
    /// طلاییِ بندِ ۲۰٫۰ («فقط تغییر») بی‌معنا می‌شد.
    /// </summary>
    [Fact]
    public void Virayesh_Faghat_Filde_Avaz_Shode_Ra_Mibarad()
    {
        long id;
        using (var db = _dbf.Create())
        {
            var row = Row();
            db.SafeEntries.Add(row);
            db.SaveChanges();
            id = row.Id;
        }

        using (var db = _dbf.Create())
        {
            var row = db.SafeEntries.Single(x => x.Id == id);
            row.Title = "نامِ تازه";
            db.SaveChanges();
        }

        using var read = _dbf.Create();
        var update = read.SyncOps.Where(x => x.OpType == "update").OrderBy(x => x.Id).ToList().Last();
        using var doc = JsonDocument.Parse(update.FieldsJson);
        var keys = doc.RootElement.EnumerateObject().Select(p => p.Name).ToHashSet();

        Assert.Contains("Title", keys);
        //  مبلغ عوض نشده، پس نباید برود
        Assert.DoesNotContain("Amount", keys);
        //  ⚠️ `UpdatedAt` را خودِ `Stamp` پس از تشخیصِ تغییرها می‌زند، پس
        //  ممکن است در فهرست باشد و ممکن است نباشد — هیچ‌کدام مهم نیست و
        //  عمداً سنجیده نمی‌شود: دستگاهِ گیرنده خودش مهرِ زمان می‌زند.
        Assert.Equal("نامِ تازه", doc.RootElement.GetProperty("Title").GetString());
    }

    /// <summary>حذف فیلد ندارد — همان «چند صد بایت»ِ بندِ ۲۰٫۰.</summary>
    [Fact]
    public void Hazf_Hich_Fildi_Nadarad_Va_Zire_Yek_Kilobyte_Ast()
    {
        using (var db = _dbf.Create())
        {
            db.SafeEntries.Add(Row());
            db.SaveChanges();
        }

        using (var db = _dbf.Create())
        {
            db.SafeEntries.Remove(db.SafeEntries.Single());
            db.SaveChanges();
        }

        using var read = _dbf.Create();
        var del = read.SyncOps.Where(x => x.OpType == "delete").ToList();
        var op = Assert.Single(del);
        Assert.Equal("{}", op.FieldsJson);
        //  خودِ ردیف نرم حذف شده، نه واقعاً
        Assert.Empty(read.SafeEntries.ToList());
        Assert.NotEmpty(read.SafeEntries.IgnoreQueryFilters().ToList());
        //  بندِ ۲۰٫۱۰: «پاک کردنِ یک اسم = درخواستی کمتر از یک کیلوبایت»
        var wire = op.TableName.Length + op.RowUid.Length + op.OpId.Length + op.FieldsJson.Length + 64;
        Assert.True(wire < 1024, $"بدنهٔ حذف {wire} بایت شد");
    }

    // ── ۲) جدول‌های محلی ───────────────────────────────────────────────

    /// <summary>
    /// ⛔ رمزِ محلی، تنظیمات، سطلِ زباله و دفترِ کارها هیچ‌وقت به سرور
    /// نمی‌روند — همان چهارتایی که فهرستِ سفیدِ سرور هم ندارد. فرستادنشان
    /// یعنی رمزِ یک پمپ روی کامپیوترِ دیگری بنشیند.
    /// </summary>
    [Fact]
    public void Jadvalhaye_Mahalli_Hich_Opi_Nemisazand()
    {
        using (var db = _dbf.Create())
        {
            db.Settings.Add(new Setting { Key = "k", Value = "v" });
            db.Users.Add(new AppUser { UserName = "u", PasswordHash = "h", Role = UserRole.Admin });
            db.SaveChanges();
        }

        using var read = _dbf.Create();
        Assert.Empty(read.SyncOps.Where(x => x.TableName == nameof(Setting)).ToList());
        Assert.Empty(read.SyncOps.Where(x => x.TableName == nameof(AppUser)).ToList());
    }

    // ── ۳) op و داده در یک تراکنش ─────────────────────────────────────

    /// <summary>
    /// ⛔ هیچ‌وقت داده بی op و هیچ op بی داده نمی‌ماند: هر دو در یک
    /// <c>SaveChanges</c> می‌نشینند. سنجه‌اش شمارِ برابرِ ردیف و op است.
    /// </summary>
    [Fact]
    public void Op_Va_Dade_Ba_Ham_Minshinand()
    {
        using (var db = _dbf.Create())
        {
            for (var i = 0; i < 25; i++) db.SafeEntries.Add(Row("ردیف " + i));
            db.SaveChanges();
        }

        using var read = _dbf.Create();
        Assert.Equal(25, read.SafeEntries.Count());
        Assert.Equal(25, read.SyncOps.Count(x => x.TableName == nameof(SafeEntry) && x.OpType == "insert"));
    }

    /// <summary>خاموش بودنِ دفتر یعنی هیچ opی — برای مهاجرت و بازگردانی.</summary>
    [Fact]
    public void Daftar_Khamush_Hich_Opi_Nemisazad()
    {
        OpLog.Enabled = false;
        try
        {
            using var db = _dbf.Create();
            db.SafeEntries.Add(Row());
            db.SaveChanges();
        }
        finally { OpLog.Enabled = true; }

        using var read = _dbf.Create();
        Assert.Empty(read.SyncOps.ToList());
        //  ولی خودِ ردیف نشسته
        Assert.Single(read.SafeEntries.ToList());
    }

    // ── ۴) مهاجرت ─────────────────────────────────────────────────────

    /// <summary>
    /// ══ مهاجرت از دیتابیسِ امروزِ مشتری ═══════════════════════════════
    ///
    /// یک دیتابیسِ «قدیمی» ساخته می‌شود (بی ستونِ <c>SyncUid</c> و بی
    /// جدول‌های همگام‌سازی)، پر می‌شود، و بعد <see cref="PumpDbFactory.EnsureReady"/>
    /// رویش می‌دود.
    ///
    /// ⛔ هیچ ردیفی نباید گم شود، و هر ردیف باید شناسهٔ خودش را بگیرد —
    /// شناسه‌ای که با شناسهٔ ردیفِ دیگر یکی نباشد.
    /// </summary>
    [Fact]
    public void Mohajerat_Az_Daftare_Ghadimi_Hich_Radifi_Ra_Nemibarad()
    {
        var file = Path.Combine(Path.GetTempPath(), $"pump-mig-{Guid.NewGuid():N}.db");
        try
        {
            //  ۱) دیتابیسِ «قدیمی»: ستونِ SyncUid و جدول‌های Sync برداشته می‌شوند
            var dbf = new PumpDbFactory(file);
            dbf.EnsureReady();
            using (var db = dbf.Create())
            {
                for (var i = 0; i < 40; i++)
                    db.SafeEntries.Add(new SafeEntry
                    { Title = "کهنه " + i, Amount = i, DateKey = 14050700 + i, MonthKey = "1405/07" });
                db.SaveChanges();

                db.Database.ExecuteSqlRaw("DROP TABLE IF EXISTS \"SyncOps\";");
                db.Database.ExecuteSqlRaw("DROP TABLE IF EXISTS \"SyncState\";");
                //  SQLite ستون را حذف می‌کند (۳٫۳۵ به بعد)؛ نشد، خالی‌اش می‌کنیم
                try { db.Database.ExecuteSqlRaw("ALTER TABLE \"SafeEntries\" DROP COLUMN \"SyncUid\";"); }
                catch { db.Database.ExecuteSqlRaw("UPDATE \"SafeEntries\" SET \"SyncUid\" = NULL;"); }
                //  مهر را هم پاک می‌کنیم تا وصله‌ها واقعاً بدوند
                db.Database.ExecuteSqlRaw("DELETE FROM \"Settings\" WHERE \"Key\" = 'schema.stamp';");
            }

            //  ۲) نسخهٔ امروز بالا می‌آید
            var again = new PumpDbFactory(file);
            again.EnsureReady();

            //  ۳) هیچ ردیفی نرفته، و هر کدام شناسهٔ یکتای خودش را دارد
            using var read = again.Create();
            var rows = read.SafeEntries.ToList();
            Assert.Equal(40, rows.Count);
            Assert.All(rows, r => Assert.False(string.IsNullOrEmpty(r.SyncUid)));
            Assert.Equal(40, rows.Select(r => r.SyncUid).Distinct().Count());
            //  و مبالغ دست‌نخورده‌اند
            Assert.Equal(39m, rows.Single(r => r.Title == "کهنه 39").Amount);
        }
        finally
        {
            foreach (var f in new[] { file, file + "-wal", file + "-shm" })
                try { if (File.Exists(f)) File.Delete(f); } catch { }
        }
    }

    /// <summary>
    /// ⛔ ریشهٔ شناسه‌ها <b>یک بار</b> ساخته می‌شود و عوض نمی‌شود: عوض شدنش
    /// یعنی همان ردیف‌ها بارِ دوم با شناسهٔ دیگری به سرور می‌روند، یعنی
    /// کلِ دفتر دو برابر.
    /// </summary>
    [Fact]
    public void Risheye_Shenase_Yek_Bar_Sakhte_Mishavad()
    {
        string first;
        using (var db = _dbf.Create()) first = db.SyncState.Single(x => x.Id == 1).UidSeed;
        Assert.False(string.IsNullOrEmpty(first));

        new PumpDbFactory(_file).EnsureReady();

        using var read = _dbf.Create();
        Assert.Equal(first, read.SyncState.Single(x => x.Id == 1).UidSeed);
    }

    // ── ULID ──────────────────────────────────────────────────────────

    /// <summary>
    /// ⚠️ در یک میلی‌ثانیه هم تکراری نمی‌سازد. بی این، صد ردیفِ یک ذخیرهٔ
    /// دسته‌ای می‌توانست دو شناسهٔ برابر بسازد و سرور ردیفِ دوم را
    /// «تکراری» می‌شمرد — یعنی دادهٔ گم‌شده.
    /// </summary>
    [Fact]
    public void Ulid_Dar_Yek_Milisanie_Ham_Tekrari_Nemisazad()
    {
        var ids = new List<string>();
        for (var i = 0; i < 5_000; i++) ids.Add(Ulid.New(1_758_300_000_123));
        Assert.Equal(ids.Count, ids.Distinct().Count());
        Assert.All(ids, id => Assert.True(Ulid.IsUlid(id)));
        //  و مرتب است: همان زمان ⇒ همان ده نویسهٔ اول، و بقیه بالارونده
        Assert.Equal(ids.OrderBy(x => x, StringComparer.Ordinal).ToList(), ids);
    }

    [Fact]
    public void Ulid_Ba_Gozashte_Zaman_Bozorgtar_Mishavad()
    {
        var early = Ulid.New(1_000_000_000_000);
        var late = Ulid.New(2_000_000_000_000);
        Assert.True(string.CompareOrdinal(early, late) < 0);
    }
}
