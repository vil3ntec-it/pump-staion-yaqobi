using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Persistence;
using PumpYaqobi.Services.Data;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ بی‌حساب کار کرد، بعد حساب ساخت — دفترش با او می‌آید ══════════════════
///
/// خواستهٔ صریحِ صاحب ریپو (۱۴۰۵/۰۷/۱۲): «کسی بدون حساب اگه حساب‌ها رو تو
/// برنامه می‌زاشت و بعدن بخاد لاگین کنه، اگه حساب جدید بود همون اطلاعات که
/// بودن باقی بمونه و روی حساب همون شخص تو سرور رسیده بشه؟»
///
/// ⚠️ <b>چرا این آزمون با آن دو تای موجود یکی نیست.</b> تا امروز هر نیمهٔ
/// این زنجیره <b>جدا</b> سنجیده می‌شد و هیچ‌کس کلش را با هم نمی‌سنجید:
///
/// <code>
/// AccountLedgerTests    ⇒ مسیرِ دفتر درست انتخاب می‌شود   (بی همگام‌سازی)
/// AccountDataCarryTests ⇒ «بارِ اول» همه را می‌فرستد      (روی یک فایلِ ثابت)
/// </code>
///
/// یعنی اگر روزی مسیرِ دفتر می‌شکست و نخستین حساب به‌جای دفترِ ریشه یک
/// پوشهٔ <b>خالیِ</b> تازه می‌گرفت، هر دو کلاس سبز می‌ماندند و کاربر پس از
/// ورود دفترِ <b>خالی</b> می‌دید — دقیقاً همان چیزی که پرسیده شد. این کلاس
/// همان شکاف را می‌بندد: <b>یک</b> زنجیره، روی SQLiteِ واقعیِ روی دیسک.
/// </summary>
public class AccountFirstLoginCarryTests : IDisposable
{
    private readonly string _dir =
        Path.Combine(Path.GetTempPath(), $"pump-firstlogin-{Guid.NewGuid():N}");

    private string Root => Path.Combine(_dir, AccountLedger.FileName);

    private readonly PumpDbFactory _dbf;

    /// <summary>همتای <c>AppSettings.LedgerAccountId</c> — صاحبِ دفترِ ریشه.</summary>
    private string _owner = "";

    public AccountFirstLoginCarryTests()
    {
        Directory.CreateDirectory(_dir);
        _dbf = new PumpDbFactory(Root);
        _dbf.EnsureReady();
    }

    public void Dispose()
    {
        OpLog.Enabled = true;
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(_dir, true); } catch { /* ویندوز گاهی دیر رها می‌کند */ }
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// همان سه کاری که <see cref="PumpYaqobi.App.Services.AppHost"/> در
    /// <c>UseLedgerOf</c> می‌کند، با <b>همان</b> دو تابعِ تصمیم‌گیرِ واقعی و
    /// به همان ترتیب: صاحبِ ریشه، مسیر، جابه‌جایی.
    ///
    /// ⚠️ تصمیمِ دومی این‌جا ساخته نمی‌شود — هر دو تابع همان‌هایی‌اند که
    /// خودِ <c>UseLedgerOf</c> صدا می‌زند، و ترتیبشان را
    /// <c>AccountLedgerSourceTests</c> روی خودِ سورس قفل کرده.
    /// </summary>
    private void SignIn(string accountId)
    {
        if (AccountLedger.ShouldClaimRoot(accountId, _owner)) _owner = accountId;

        var want = AccountLedger.PathFor(Root, accountId, _owner);
        if (!string.Equals(Path.GetFullPath(want), Path.GetFullPath(_dbf.DbPath),
                           StringComparison.OrdinalIgnoreCase))
            _dbf.SwitchTo(want);
    }

    /// <summary>ردیف‌های امروزی — خودِ <c>SaveChanges</c> برایشان op می‌سازد.</summary>
    private void WriteRows(int n, string tag)
    {
        using var db = _dbf.Create();
        for (var i = 0; i < n; i++)
            db.SafeEntries.Add(new SafeEntry
            { Title = $"{tag} {i}", Amount = 100 + i, DateKey = 14050701, MonthKey = "1405/07" });
        db.SaveChanges();
    }

    /// <summary>
    /// ردیف‌های <b>کهنه</b>: از نسخه‌ای پیش از همگام‌سازی مانده‌اند و هیچ opی
    /// ندارند — همان چیزی که «بارِ اول» برای آن ساخته شد.
    /// </summary>
    private void WriteLegacyRows(int n)
    {
        var was = OpLog.Enabled;
        OpLog.Enabled = false;
        try
        {
            using var db = _dbf.Create();
            for (var i = 0; i < n; i++)
                db.SafeEntries.Add(new SafeEntry
                { Title = $"ردیفِ کهنه {i}", Amount = 500 + i, DateKey = 14050701, MonthKey = "1405/07" });
            db.SaveChanges();
            //  همان کاری که `PatchSyncUid` روی دفترِ مشتری می‌کند
            db.Database.ExecuteSqlRaw(
                "UPDATE \"SafeEntries\" SET \"SyncUid\" = 'old-' || \"Id\" " +
                "WHERE \"SyncUid\" IS NULL OR \"SyncUid\" = '';");
        }
        finally { OpLog.Enabled = was; }
    }

    private int DataRows()
    {
        using var db = _dbf.Create();
        return db.SafeEntries.AsNoTracking().Count();
    }

    /// <summary>«بارِ اول» را تا ته می‌دواند — همان کاری که حلقه تکه‌تکه می‌کند.</summary>
    private static void RunSeed(SyncStore store)
    {
        for (var guard = 0; guard < 500; guard++)
            if (store.SeedStep().Done) return;
        throw new Xunit.Sdk.XunitException("«بارِ اول» تمام نشد");
    }

    // ══════════════════════════════════════════════════════════════════

    /// <summary>
    /// ⛔ <b>نخستین ورود، دفترِ بی‌حساب را با خودش می‌برد.</b> نه فایلی عوض
    /// می‌شود، نه ردیفی کم — و هر ردیف، کهنه و تازه، در صفِ رفتن به همان
    /// حساب می‌نشیند.
    /// </summary>
    [Fact]
    public void BiHesab_Neveshte_Bood_Ba_Nokhostin_Vorood_Hamash_Miravad()
    {
        //  ── بی‌حساب: ماه‌ها کار، دو جورِ ردیف ──────────────────────────
        WriteRows(40, "ردیفِ بی‌حساب");
        WriteLegacyRows(15);
        Assert.Equal(55, DataRows());

        //  ── نخستین ورود ───────────────────────────────────────────────
        SignIn("user-A");

        //  ⛔ جانِ این آزمون: **همان فایل** باز مانده است. اگر این بشکند،
        //  کاربر پس از ورود دفترِ خالی می‌بیند.
        Assert.Equal(Path.GetFullPath(Root), Path.GetFullPath(_dbf.DbPath));
        Assert.Equal("user-A", _owner);
        Assert.Equal(55, DataRows());
        Assert.False(Directory.Exists(Path.Combine(_dir, AccountLedger.Folder)));

        //  ── و همه‌اش به همان حساب می‌رود ───────────────────────────────
        var store = new SyncStore(_dbf);
        //  «نمی‌دانستیم مالِ کیست» ⇒ فقط ثبت، هیچ چیزی از سر نمی‌شود
        Assert.False(store.BindTo("user-A").Rebound);
        Assert.Equal("user-A", store.State().AccountId);

        RunSeed(store);
        Assert.Equal(55, store.Pending());     // ۴۰ تازه + ۱۵ کهنه، هیچ‌کدام جا نماند
        Assert.Equal(55, DataRows());          // و یک ردیف هم کم نشد
    }

    /// <summary>
    /// ⛔ <b>حسابِ دوم دفترِ نفرِ اول را نه می‌بیند و نه می‌برد</b> — و
    /// برگشتنِ حسابِ اول، دفتر و صفِ نرفته‌اش را دست‌نخورده برمی‌گرداند.
    ///
    /// ⚠️ بی این بند، «بارِ اول» می‌توانست با هر رفت‌وبرگشت از نو بدود و
    /// همان ردیف‌ها بارِ دوم بروند.
    /// </summary>
    [Fact]
    public void Hesabe_Dovom_Daftare_Nafare_Aval_Ra_Nemibarad()
    {
        WriteRows(12, "ردیفِ بی‌حساب");
        SignIn("user-A");

        var first = new SyncStore(_dbf);
        first.BindTo("user-A");
        RunSeed(first);
        Assert.Equal(12, first.Pending());

        //  ── حسابِ دوم روی همین کامپیوتر ────────────────────────────────
        SignIn("user-B");
        Assert.NotEqual(Path.GetFullPath(Root), Path.GetFullPath(_dbf.DbPath));
        Assert.Equal(0, DataRows());                    // ⛔ دفترِ اولی را نمی‌بیند
        Assert.Equal(0, new SyncStore(_dbf).Pending());

        //  ⛔ و دفترِ نفرِ اول سرِ جایش روی دیسک است
        Assert.True(File.Exists(Root));

        //  ── و برگشتِ حسابِ اول ─────────────────────────────────────────
        SignIn("user-A");
        Assert.Equal(Path.GetFullPath(Root), Path.GetFullPath(_dbf.DbPath));
        Assert.Equal(12, DataRows());

        var back = new SyncStore(_dbf);
        Assert.Equal("user-A", back.State().AccountId);
        Assert.True(back.State().SeededAt > 0);          // «بارِ اول» دوباره نمی‌دود
        Assert.Equal(12, back.Pending());                // همان صف، نه دو برابر
    }
}
