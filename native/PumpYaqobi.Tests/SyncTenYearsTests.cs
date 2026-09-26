using PumpYaqobi.App.Services;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Persistence;
using PumpYaqobi.Services.Data;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ همگام‌سازی با ده سال داده — سنجهٔ `tensync` (۱۴۰۵/۰۷/۱۴) ══════════════
///
/// دو کامپیوتر روی سرورِ حسابِ واقعی، دفترِ بزرگ. چهار ریشه که این‌جا قفل‌اند
/// (پنجمی — کلیدِ خارجی — در <see cref="SyncParentLinkTests"/>، و ششمی —
/// فیلدِ خالی — در <see cref="SyncStoreTests"/>):
///   ۱) دسته با بایت هم بسته می‌شود — وگرنه سرور کلِ دسته را رد و صف را برای
///      همیشه نگه می‌داشت
///   ۲) شناسهٔ همگام‌سازی مالِ دفتر است — نصبِ دوباره روی همان کامپیوتر
///      دفترِ خالی نمی‌گیرد
///   ۳) صفِ پر همان لحظه دورِ بعد را می‌زند
///   ۴) پردهٔ «آوردنِ اطلاعات» تا آخرین صفحه می‌ماند
/// </summary>
public class SyncTenYearsTests : IDisposable
{
    private readonly string _file = Path.Combine(Path.GetTempPath(), $"pump-ten-{Guid.NewGuid():N}.db");

    public void Dispose()
    {
        foreach (var f in new[] { _file, _file + "-wal", _file + "-shm" })
            try { if (File.Exists(f)) File.Delete(f); } catch { }
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void Daste_BaByte_Ham_Baste_Mishavad()
    {
        var f = new PumpDbFactory(_file); f.EnsureReady();
        var big = "{\"RowsJson\":\"" + new string('x', 5_000) + "\"}";
        using (var db = f.Create())
        {
            for (var i = 0; i < 150; i++)
                db.SyncOps.Add(new SyncOp { OpId = "op" + i.ToString("D4"), TableName = nameof(SafeEntry),
                    RowUid = "u" + i, OpType = "insert", FieldsJson = big });
            db.SaveChanges();
        }

        var take = new SyncStore(f).Take();
        Assert.InRange(take.Count, 1, 149);
        Assert.True(take.Sum(o => System.Text.Encoding.UTF8.GetByteCount(o.FieldsJson)) <= SyncStore.MaxBatchBytes);
        //  به ترتیب — هیچ opی جا نمی‌ماند
        Assert.Equal("op0000", take[0].OpId);
    }

    [Fact]
    public void OpeBozorgtarAzSaghf_Tanha_Miravad_Na_SafRaNegahDarad()
    {
        var f = new PumpDbFactory(_file); f.EnsureReady();
        using (var db = f.Create())
        {
            db.SyncOps.Add(new SyncOp { OpId = "op-big", TableName = nameof(SafeEntry), RowUid = "u1", OpType = "insert",
                FieldsJson = "{\"RowsJson\":\"" + new string('x', SyncStore.MaxBatchBytes + 10) + "\"}" });
            db.SyncOps.Add(new SyncOp { OpId = "op-small", TableName = nameof(SafeEntry), RowUid = "u2", OpType = "insert",
                FieldsJson = "{\"Title\":\"x\"}" });
            db.SaveChanges();
        }
        var take = new SyncStore(f).Take();
        Assert.Equal("op-big", Assert.Single(take).OpId);
    }

    [Fact]
    public void ShenaseyeHamgamSazi_MaleDaftar_Ast()
    {
        //  همان کامپیوتر (همان DeviceUid)، دو دفتر ⇒ دو شناسه
        var a = CloudLink.SyncDeviceFor("", "01J9ZK3X4Q7RS8TVWXYZ", "pc-1");
        var b = CloudLink.SyncDeviceFor("", "01J9ZK3X4Q7RS8ABCDEF", "pc-1");
        Assert.NotEqual(a, b);
        Assert.StartsWith("pc-1-", a);
        //  ⛔ شناسه‌ای که یک بار فرستاده دیگر عوض نمی‌شود
        Assert.Equal("pc-1", CloudLink.SyncDeviceFor("pc-1", "01J9ZK3X4Q7RS8TVWXYZ", "pc-1"));
        //  ریشهٔ کوتاه/خالی ⇒ همان رفتارِ پیشین
        Assert.Equal("pc-1", CloudLink.SyncDeviceFor("", "", "pc-1"));
    }

    /// <summary>
    /// ⛔ پیوندِ پدر <b>یک پرس‌وجو برای هر جدول در هر دسته</b> است، نه دو تا برای
    /// هر کلیدِ هر op. نمونه‌بردار روی دفترِ ده‌ساله: ۹۱٪ وقتِ هر دورِ فرستادن
    /// همان هشتصد جست‌وجوی تکی بود (۳.۱.۱۸۸).
    /// </summary>
    [Fact]
    public void PeyvandePedar_YekPorsojoo_BarayeHarJadval_Ast()
    {
        var src = File.ReadAllText(Path.Combine(Root(), "PumpYaqobi.Services", "Data", "SyncStore.cs"));
        var a = src.IndexOf("private static void AttachParents(", StringComparison.Ordinal);
        Assert.True(a > 0);
        var b = src.IndexOf("    /// <summary>", a, StringComparison.Ordinal);
        var body = src[a..b];
        Assert.Contains("LEFT JOIN", body);
        Assert.Contains("IN ({marks})", body);   // WHERE x.\"SyncUid\" IN ($u0, $u1, …)
        Assert.DoesNotContain("ScalarLong(", body);
        Assert.DoesNotContain("Scalar(db", body);
    }

    [Fact]
    public void Motor_Hamin_Ghavaed_Ra_Darad()
    {
        var src = File.ReadAllText(Path.Combine(Root(), "PumpYaqobi.App", "Services", "SyncEngine.cs"));
        Assert.Contains("cloud.SyncDeviceOverride = CloudLink.SyncDeviceFor(state.DeviceId, state.UidSeed, cloud.DeviceUid);", src);
        Assert.Contains("x.DeviceId = cloud.SyncDevice;", src);
        Assert.Contains("if (Queued > 0) Nudge();", src);
        //  پرده تا آخرین صفحه
        Assert.Contains("var priming = !_primeEnded && (_primeRun || PrimeWanted(state));", src);
    }

    private static string Root()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null && !Directory.Exists(Path.Combine(d.FullName, "PumpYaqobi.App")))
            d = d.Parent;
        Assert.NotNull(d);
        return d!.FullName;
    }
}
