using System.Text.Json;
using Microsoft.Data.Sqlite;
using PumpYaqobi.App.Services;
using PumpYaqobi.Application.Security;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;
using PumpYaqobi.Services.Data;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ آوردنِ بکاپ فقط با اشتراکِ پولی — و دفترِ کپی‌شده به حسابِ تازه نمی‌رسد (۱۴۰۵/۰۷/۲۰) ══
///
/// «بک‌اپِ برنامه رو روی کامپیوترِ دیگه ثبت می‌کنی، یارو ده‌ها حساب درست می‌کنه
/// و دیگه لازم نداره اشتراک بخره… فقط کسایی که اشتراک دارن بتونن فایل‌های
/// بک‌اپ رو بیارن، حتی آزمایشی‌ها نه.»
/// </summary>
public class RestoreGateTests : IDisposable
{
    private const long Day = 86_400_000L;
    private const long Now = 1_800_000_000_000L;
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "pump-rgate-" + Guid.NewGuid().ToString("N"));

    public RestoreGateTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(_dir, true); } catch { }
    }

    private static LicenseCheck Signed(bool valid, string plan) =>
        new(valid, "", Array.Empty<string>(), Array.Empty<string>(), Now + Day, valid ? Now + Day : Now - Day,
            "x", true, SignatureOk: true, Expired: !valid, Plan: plan);

    // ── تصمیم: خالص ────────────────────────────────────────────────────────

    [Fact]
    public void Pooli_Mitavanad_Azmayeshi_VaBiEshterak_Na()
    {
        Assert.Null(AppLock.RestoreBlocked(true, Signed(true, ""), false));          // استاندارد/وی‌آی‌پی/دائمی
        Assert.Null(AppLock.RestoreBlocked(false, null, offlinePaid: true));           // کدِ بی‌اینترنت (هر سه پلن پولی)
        Assert.Equal(AppLock.RestoreTrial, AppLock.RestoreBlocked(true, Signed(true, "trial"), false));
        Assert.Equal(AppLock.RestoreNoPlan, AppLock.RestoreBlocked(false, null, false));   // بی‌حساب / بی‌اشتراک
        Assert.Equal(AppLock.RestoreNoPlan, AppLock.RestoreBlocked(false, Signed(false, ""), false)); // پولیِ تمام‌شده
        //  ⛔ مجوزِ بی‌امضا هیچ‌وقت «پولی» نیست
        Assert.NotNull(AppLock.RestoreBlocked(true, Signed(true, "") with { SignatureOk = false }, false));
    }

    // ── اعمال: هر سه درِ آوردن ──────────────────────────────────────────────

    private (PumpDbFactory Db, BackupService Backup, PermissionService Perm) Host(string name)
    {
        var d = Path.Combine(_dir, name);
        Directory.CreateDirectory(d);
        var dbf = new PumpDbFactory(Path.Combine(d, "pump.db"));
        dbf.EnsureReady();
        var session = new UserSession();
        session.SignIn(UserRole.Admin, "مدیر");
        var perm = new PermissionService(session);
        return (dbf, new BackupService(dbf, perm), perm);
    }

    [Fact]
    public void BazgardaniyeBackap_BiEshtrakePooli_Rad_BaJomleyeFarsi_VaDaftarDastNamikhorad()
    {
        var (src, srcBackup, _) = Host("src");
        using (var db = src.Create()) { db.Debtors.Add(new Debtor { Name = "بیگانه" }); db.SaveChanges(); }
        var file = Path.Combine(_dir, "src.db");
        srcBackup.WriteSnapshot(file);

        var (dst, backup, perm) = Host("dst");
        perm.RestoreGate = () => AppLock.RestoreTrial;
        var ex = Assert.Throws<PermissionDeniedException>(() => backup.Restore(file));
        Assert.Equal(AppLock.RestoreTrial, ex.Reason);
        Assert.Equal(AppLock.RestoreTrial, ErrorText.Friendly(ex));
        using (var db = dst.Create()) Assert.Empty(db.Debtors.ToList());

        //  پولی ⇒ همان بازگردانیِ همیشگی
        perm.RestoreGate = () => null;
        Assert.True(backup.Restore(file).Ok);
        using (var db = dst.Create()) Assert.Single(db.Debtors.ToList());
    }

    [Fact]
    public void FayleBakhsh_HamRad()
    {
        var (src, _, _) = Host("a");
        long id;
        using (var db = src.Create()) { var d = new Debtor { Name = "بیگانه" }; db.Debtors.Add(d); db.SaveChanges(); id = d.Id; }
        var ex = new SyncStore(src).ExportPortable(new PortablePick("debtor", id));
        var path = Path.Combine(_dir, "part" + PortableFile.Extension);
        PortableFile.Write(path, "آزمون", ex);
        using var doc = JsonDocument.Parse(PortableFile.ReadSnapshot(path)!);

        var (dst, _, _) = Host("b");
        var store = new SyncStore(dst) { RestoreGate = () => AppLock.RestoreNoPlan };
        var e = Assert.Throws<PermissionDeniedException>(() => store.ImportPortable(doc.RootElement));
        Assert.Equal(AppLock.RestoreNoPlan, e.Reason);
        using (var db = dst.Create()) Assert.Empty(db.Debtors.ToList());
    }

    [Fact]
    public void SakhtaneBackap_HamishebBaz()
    {
        var (_, backup, perm) = Host("c");
        perm.RestoreGate = () => AppLock.RestoreNoPlan;
        var file = Path.Combine(_dir, "c.db");
        backup.WriteSnapshot(file);
        Assert.True(File.Exists(file));
    }

    // ── دفترِ کپی‌شده ⇒ حسابِ بی‌اشتراکِ تازه نمی‌گیردش ─────────────────────

    [Fact]
    public void MohreKampyuter_DaftareKopiShode_RaMishenasad()
    {
        var (db, _, _) = Host("stamp");
        Assert.Equal("", LedgerStamp.Read(db));
        Assert.True(LedgerStamp.StampIfEmpty(db, "m-aaa"));
        Assert.False(LedgerStamp.StampIfEmpty(db, "m-bbb"));      // ⛔ مُهر هرگز بازنویسی نمی‌شود
        Assert.Equal("m-aaa", LedgerStamp.Read(db));
        Assert.True(LedgerStamp.Trusted("m-aaa", "m-aaa"));
        Assert.False(LedgerStamp.Trusted("m-aaa", "m-bbb"));
        Assert.True(LedgerStamp.Trusted("", "m-bbb"));             // دفترِ نسخهٔ پیشین ⇒ مالِ همین‌جا
        Assert.True(LedgerStamp.Trusted("m-aaa", ""));             // کامپیوترِ بی‌شناسه ⇒ کسی بیرون نمی‌ماند
    }

    [Theory]
    [InlineData(false, true, false, true)]    // دفترِ همین کامپیوتر ⇒ مثلِ همیشه
    [InlineData(false, false, false, false)]  // ⛔ دفترِ بیگانه + بی‌اشتراکِ پولی ⇒ دفترِ تازه
    [InlineData(false, false, true, true)]    // دفترِ بیگانه + پولی ⇒ می‌گیردش
    [InlineData(true, true, true, false)]     // حساب دفترِ خودش را از قبل دارد ⇒ هیچ‌وقت جابه‌جا نمی‌شود
    public void DaftareRishe_BeHesab_Miresad(bool own, bool trusted, bool paid, bool claim) =>
        Assert.Equal(claim, AccountLedger.MayClaimRoot(own, trusted, paid));

    [Fact]
    public void DaftareBigane_HesabRaBeDaftareKhodashMibarad_VaRisheDastNamikhorad()
    {
        var root = Path.Combine(_dir, "pump.db");
        var own = AccountLedger.PathFor(root, "usr_new", AccountLedger.ForeignRoot);
        Assert.NotEqual(root, own);
        Assert.Contains(AccountLedger.Folder, own);
    }
}
