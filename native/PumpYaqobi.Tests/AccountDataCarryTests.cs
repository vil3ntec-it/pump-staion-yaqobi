using Microsoft.EntityFrameworkCore;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Persistence;
using PumpYaqobi.Services.Data;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ افتتاحِ حساب هیچ چیزی را از سر نمی‌کند ══════════════════════════════
///
/// خواستهٔ صریحِ صاحب ریپو (۱۴۰۵/۰۷/۰۷): «یارو اگه حساب نداره و اطلاعاتِ
/// خیلی زیاد روی برنامه داشته و می‌خواد حسابِ جدید برای خودش افتتاح کنه،
/// جوری نشه که اطلاعات همه از سر بشن و همون اطلاعاتی که اول توی برنامه
/// بی حساب داشت توی حسابِ جدیدش بیاد. و اگه از قبل حساب داشت قضیه فرق
/// می‌کنه و همون حساب میاد روی این حسابی که بدون حساب داشت. ولی برای
/// افتتاحِ حساب نباید اطلاعات همه حذف یا از سر یا ریست بشن.»
///
/// سه ادعا، و هر سه این‌جا با <b>رفتار</b> سنجیده می‌شوند، نه با خواندنِ کد:
///
///   ۱) دادهٔ بی‌حساب، پس از افتتاحِ حساب، <b>کامل</b> در صفِ رفتن است؛
///   ۲) عوض شدنِ حساب <b>یک بیت</b> از دفتر را لمس نمی‌کند؛
///   ۳) opهای نرفتهٔ حسابِ قبلی به دفترِ حسابِ تازه <b>نمی‌روند</b> —
///      ولی همان ردیف‌ها دوباره، این بار برای حسابِ تازه، فرستاده می‌شوند.
/// </summary>
public class AccountDataCarryTests : IDisposable
{
    private readonly string _file =
        Path.Combine(Path.GetTempPath(), $"pump-carry-{Guid.NewGuid():N}.db");
    private readonly PumpDbFactory _dbf;
    private readonly SyncStore _store;

    public AccountDataCarryTests()
    {
        _dbf = new PumpDbFactory(_file);
        _dbf.EnsureReady();
        _store = new SyncStore(_dbf);
    }

    public void Dispose()
    {
        OpLog.Enabled = true;
        foreach (var f in new[] { _file, _file + "-wal", _file + "-shm" })
            try { if (File.Exists(f)) File.Delete(f); } catch { }
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// دفترِ یک کاربرِ <b>بی‌حساب</b> که ماه‌ها کار کرده: ردیف‌هایی که
    /// هیچ opی ندارند — دقیقاً همان چیزی که «بارِ اول» برای آن ساخته شد.
    /// </summary>
    private void SeedOldLedger(int rows)
    {
        var was = OpLog.Enabled;
        OpLog.Enabled = false;
        try
        {
            using var db = _dbf.Create();
            for (var i = 0; i < rows; i++)
                db.SafeEntries.Add(new SafeEntry
                { Title = "ردیفِ بی‌حساب " + i, Amount = 100 + i, DateKey = 14050701, MonthKey = "1405/07" });
            db.SaveChanges();
            //  همان کاری که `PatchSyncUid` روی دفترِ مشتری می‌کند
            db.Database.ExecuteSqlRaw(
                "UPDATE \"SafeEntries\" SET \"SyncUid\" = 'old-' || \"Id\" " +
                "WHERE \"SyncUid\" IS NULL OR \"SyncUid\" = '';");
        }
        finally { OpLog.Enabled = was; }
    }

    /// <summary>«بارِ اول» را تا ته می‌دواند — همان کاری که حلقه تکه‌تکه می‌کند.</summary>
    private void RunSeed()
    {
        for (var guard = 0; guard < 500; guard++)
            if (_store.SeedStep().Done) return;
        throw new Xunit.Sdk.XunitException("«بارِ اول» تمام نشد");
    }

    private int DataRows()
    {
        using var db = _dbf.Create();
        return db.SafeEntries.Count();
    }

    // ══════════════════════════════════════════════════════════════════

    /// <summary>
    /// ⛔ <b>دادهٔ بی‌حساب، با افتتاحِ حساب، کامل به همان حساب می‌رود.</b>
    /// بی این، «همگام‌سازی» یعنی فقط چیزهایی که از فردا عوض شوند و کاربر
    /// حسابِ تازه‌اش را خالی می‌دید.
    /// </summary>
    [Fact]
    public void BiHesab_Kar_Karde_Bood_Hameyash_BeHesabeTaze_Miravad()
    {
        SeedOldLedger(40);
        Assert.Equal(0, _store.Pending());          // هنوز حسابی نیست

        //  افتتاحِ حساب: اولین بند شدن
        Assert.False(_store.BindTo("user-A").Rebound);   // «نمی‌دانستیم» ⇒ فقط ثبت
        RunSeed();

        Assert.Equal(40, _store.Pending());
        Assert.Equal(40, DataRows());               // و یک ردیف هم کم نشده
    }

    /// <summary>
    /// ⛔ <b>افتتاحِ حساب یک بیت از دفتر را لمس نمی‌کند</b> — نه ردیفی کم
    /// می‌شود، نه مقداری عوض. «برای افتتاحِ حساب نباید اطلاعات حذف یا از
    /// سر یا ریست بشن.»
    /// </summary>
    [Fact]
    public void Eftetahe_Hesab_Yek_Bit_Az_Daftar_Ra_Lams_Nemikonad()
    {
        SeedOldLedger(25);
        List<(long Id, string Title, decimal Amount)> Snapshot()
        {
            using var db = _dbf.Create();
            return db.SafeEntries.OrderBy(x => x.Id)
                     .Select(x => new ValueTuple<long, string, decimal>(x.Id, x.Title, x.Amount))
                     .ToList();
        }

        var before = Snapshot();

        _store.BindTo("user-A");
        RunSeed();
        _store.BindTo("user-B");        // و حتی عوض کردنِ حساب هم
        RunSeed();

        Assert.Equal(before, Snapshot());
    }

    /// <summary>
    /// ⛔ <b>حسابِ دیگر ⇒ دفتر از نو بسته می‌شود.</b> بی این،
    /// <c>SeededAt</c> بزرگ‌تر از صفر می‌ماند و دادهٔ همین کامپیوتر
    /// <b>هیچ‌وقت</b> به حسابِ تازه نمی‌رسید.
    /// </summary>
    [Fact]
    public void Hesabe_Digar_Daftar_Ra_AzNo_Mibandad()
    {
        SeedOldLedger(12);
        _store.BindTo("user-A");
        RunSeed();

        //  همه رفتند و هرس شدند — یعنی صف خالی است
        var batch = _store.Take();
        _store.MarkResults(batch.ToDictionary(x => x.OpId, _ => "applied"));
        Assert.Equal(0, _store.Pending());
        _store.Update(x => x.Cursor = 987);

        //  حالا حسابِ دیگری وارد می‌شود
        var bind = _store.BindTo("user-B");
        Assert.True(bind.Rebound);
        Assert.Equal(12, bind.Dropped);             // opهای حسابِ قبلی برداشته شدند

        var st = _store.State();
        Assert.Equal("user-B", st.AccountId);
        Assert.Equal(0L, st.SeededAt);
        Assert.Equal(0L, st.Cursor);                 // کلِ دفترِ حسابِ تازه از اول

        //  ⛔ و همان دوازده ردیف، این بار برای حسابِ تازه، دوباره می‌روند
        RunSeed();
        Assert.Equal(12, _store.Pending());
        Assert.Equal(12, DataRows());
    }

    /// <summary>
    /// ⛔ <b>opهای نرفتهٔ حسابِ قبلی در دفترِ حسابِ تازه نمی‌نشینند</b> —
    /// ولی دادهٔ پشتشان گم هم نمی‌شود: همان ردیف‌ها از نو فرستاده می‌شوند.
    /// </summary>
    [Fact]
    public void Ophaye_Naraftehye_Hesabe_Ghabli_Be_Hesabe_Taze_Nemiravand()
    {
        SeedOldLedger(5);
        _store.BindTo("user-A");
        RunSeed();
        var oldIds = _store.Take().Select(x => x.OpId).ToHashSet();
        Assert.Equal(5, oldIds.Count);

        _store.BindTo("user-B");
        RunSeed();

        var fresh = _store.Take().Select(x => x.OpId).ToHashSet();
        Assert.Equal(5, fresh.Count);
        Assert.Empty(fresh.Intersect(oldIds));      // هیچ‌کدام از آن‌ها نیست
    }

    /// <summary>
    /// ⚠️ <b>ورودِ دوبارهٔ همان حساب، و نصبِ بی‌شناسه، هیچ هزینه‌ای
    /// ندارند.</b> «قفلِ ناخواسته بدتر از بازِ ناخواسته است» این‌جا هم
    /// برقرار است: مشتریِ امروزی نباید با یک به‌روزرسانی کلِ دفترش را
    /// دوباره بفرستد.
    /// </summary>
    [Fact]
    public void Hamin_Hesab_Va_Shenaseye_Khali_Chizi_Ra_AzNo_Nemikonand()
    {
        SeedOldLedger(3);
        _store.BindTo("user-A");
        RunSeed();
        _store.Update(x => x.Cursor = 55);

        Assert.False(_store.BindTo("user-A").Rebound);      // همان حساب
        Assert.False(_store.BindTo("").Rebound);            // نصبِ بی‌شناسه
        Assert.False(_store.BindTo(null).Rebound);

        var st = _store.State();
        Assert.Equal(55L, st.Cursor);
        Assert.True(st.SeededAt > 0);
        Assert.Equal("user-A", st.AccountId);
    }

    /// <summary>
    /// ⛔ حلقهٔ همگام‌سازی واقعاً از همین در رد می‌شود — وگرنه همهٔ این
    /// سنجه‌ها سبز می‌مانند و در برنامه هیچ اتفاقی نمی‌افتد.
    /// </summary>
    [Fact]
    public void Halgheye_Hamgamsazi_Vagheaan_BindTo_Ra_Mizanad()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        var src = File.ReadAllText(Path.Combine(root, "PumpYaqobi.App", "Services", "SyncEngine.cs"));
        Assert.Contains("_store.BindTo(mine)", src);
        Assert.Contains("file.CloudUserId", src);      // شناسهٔ خودِ حساب، نه چیزِ دیگری

        //  و پیش از «بارِ اول»، وگرنه یک دور با دفترِ حسابِ قبلی می‌دود
        Assert.True(src.IndexOf("_store.BindTo(", StringComparison.Ordinal)
                  < src.IndexOf("_store.SeedStep()", StringComparison.Ordinal));

        //  ⛔ و خودِ بند شدن هیچ جدولِ داده‌ای را دست نمی‌زند
        var store = File.ReadAllText(Path.Combine(root, "PumpYaqobi.Services", "Data", "SyncStore.cs"));
        var i = store.IndexOf("public BindReport BindTo(", StringComparison.Ordinal);
        var body = store[i..store.IndexOf("\n    // ── صفِ فرستادنی", i, StringComparison.Ordinal)];
        Assert.Contains("db.SyncOps.ExecuteDelete()", body);
        foreach (var t in new[] { "SafeEntries", "DebtRows", "DebtAccounts", "Waraqs", "Expenses" })
            Assert.DoesNotContain(t, body);
        //  و ریشهٔ شناسه‌ها عوض نمی‌شود
        Assert.DoesNotContain("UidSeed =", body);

        //  ⛔ ستونِ تازه روی **دفترِ مشتری** هم بنشیند: فهرستِ `PatchColumns`
        //  دستی است و ستونی که آن‌جا نوشته نشود، روی نصبِ امروزی هیچ‌وقت
        //  ساخته نمی‌شود — و همگام‌سازی همان لحظه با خطای SQLite می‌ایستد.
        var fac = File.ReadAllText(Path.Combine(root, "PumpYaqobi.Services", "Data", "PumpDbFactory.cs"));
        Assert.Contains("(\"SyncState\", \"AccountId\"", fac);
    }
}
