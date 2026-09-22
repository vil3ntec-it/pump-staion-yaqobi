using Microsoft.EntityFrameworkCore;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Services.Data;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ هر حساب، دفترِ خودش ═══════════════════════════════════════════════════
///
/// خواستهٔ صریحِ صاحب ریپو (۱۴۰۵/۰۷/۱۱): «حسابِ اول با حسابِ دوم عوض بشه،
/// اطلاعات دست نخوره، توی حساب‌ها بمونن، حساب‌ها عوض می‌شه و اطلاعاتِ همون
/// حساب نشون داده بشه — مثلِ برنامه‌های حرفه‌ای.»
///
/// سه ادعا، و هر سه با <b>رفتار</b> سنجیده می‌شوند روی دفترِ واقعیِ SQLite:
///
///   ۱) حسابِ دوم دفترِ <b>خودش</b> را می‌گیرد و دفترِ اولی را نمی‌بیند؛
///   ۲) برگشتنِ حسابِ اول، دفترِ او را <b>دست‌نخورده</b> برمی‌گرداند؛
///   ۳) هیچ فایلی پاک نمی‌شود — هر دو دفتر روی دیسک می‌مانند.
///
/// ⚠️ این‌جا عمداً <see cref="AppHost"/> ساخته نمی‌شود: تصمیمِ مسیر
/// (<see cref="AccountLedger"/>) و خودِ جابه‌جایی
/// (<see cref="PumpDbFactory.SwitchTo"/>) دو چیزِ خالص‌اند و بی پنجره و بی
/// تنظیماتِ سراسری سنجیده می‌شوند. بندِ پوسته‌ای‌اش در
/// <c>AccountLedgerSourceTests</c> است.
/// </summary>
public class AccountLedgerTests : IDisposable
{
    private readonly string _dir =
        Path.Combine(Path.GetTempPath(), $"pump-ledger-{Guid.NewGuid():N}");

    private string Root => Path.Combine(_dir, AccountLedger.FileName);

    public AccountLedgerTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try { Directory.Delete(_dir, true); } catch { /* ویندوز گاهی دیر رها می‌کند */ }
        GC.SuppressFinalize(this);
    }

    private static void Put(PumpDbFactory dbf, string title)
    {
        using var db = dbf.Create();
        db.SafeEntries.Add(new SafeEntry { Title = title, Amount = 1, MonthKey = "1405-07" });
        db.SaveChanges();
    }

    private static List<string> Titles(PumpDbFactory dbf)
    {
        using var db = dbf.Create();
        return db.SafeEntries.AsNoTracking().Select(x => x.Title ?? "").OrderBy(x => x).ToList();
    }

    // ── ۱) نخستین حساب دفترِ موجود را برمی‌دارد ─────────────────────────────

    [Fact]
    public void Hesabe_Aval_Hamin_Daftar_Ra_Barmidarad()
    {
        //  بی‌حساب ⇒ همان دفترِ ریشه
        Assert.Equal(Root, AccountLedger.PathFor(Root, "", ""));

        //  نخستین حساب ⇒ باز هم همان دفترِ ریشه (دادهٔ بی‌حساب با او می‌آید)
        Assert.True(AccountLedger.ShouldClaimRoot("user-A", ""));
        Assert.Equal(Root, AccountLedger.PathFor(Root, "user-A", "user-A"));

        //  و صاحبِ ریشه دو بار برداشته نمی‌شود
        Assert.False(AccountLedger.ShouldClaimRoot("user-B", "user-A"));
    }

    [Fact]
    public void Hesabe_Dovom_Daftare_Khodash_Ra_Migirad()
    {
        var b = AccountLedger.PathFor(Root, "user-B", "user-A");
        Assert.NotEqual(Root, b);
        Assert.Equal(AccountLedger.FileName, Path.GetFileName(b));
        Assert.Equal(AccountLedger.Folder, Path.GetFileName(Path.GetDirectoryName(Path.GetDirectoryName(b))));
        //  و زیرِ همان پوشهٔ برنامه می‌ماند
        Assert.StartsWith(Path.GetFullPath(_dir), Path.GetFullPath(b), StringComparison.Ordinal);
    }

    // ── ۲) شناسه هیچ‌وقت خودش نامِ پوشه نمی‌شود ─────────────────────────────

    [Fact]
    public void Shenaseye_Kham_Hichvaght_Name_Pushe_Nemishavad()
    {
        //  ⛔ یک `..` در شناسه نباید بتواند بیرونِ پوشهٔ برنامه بنویسد
        var evil = AccountLedger.PathFor(Root, "../../../etc/passwd", "user-A");
        Assert.StartsWith(Path.GetFullPath(_dir), Path.GetFullPath(evil), StringComparison.Ordinal);
        Assert.False(AccountLedger.SafeName("../../../etc/passwd").Contains(".."));

        //  و دو شناسهٔ متفاوت هیچ‌وقت به یک پوشه نمی‌رسند
        Assert.NotEqual(AccountLedger.SafeName("a/b"), AccountLedger.SafeName("a-b"));
        Assert.NotEqual(AccountLedger.SafeName("user-1"), AccountLedger.SafeName("user-2"));

        //  و همان شناسه همیشه همان پوشه — وگرنه دفترِ کاربر گم می‌شود
        Assert.Equal(AccountLedger.SafeName("user-A"), AccountLedger.SafeName("user-A"));
    }

    // ── ۳) رفتارِ واقعی: دو دفتر، دو دادهٔ جدا، هیچ‌کدام پاک نمی‌شود ────────

    [Fact]
    public void Avaz_Kardane_Hesab_Daftare_Har_Kodam_Ra_Negah_Midarad()
    {
        var dbf = new PumpDbFactory(Root);
        dbf.EnsureReady();

        //  حسابِ اول (صاحبِ ریشه) یک ردیف می‌نویسد
        Put(dbf, "مالِ حسابِ اول");
        Assert.Equal(new[] { "مالِ حسابِ اول" }, Titles(dbf));

        //  ⇒ حسابِ دوم: دفترِ خودش، و **خالی**
        var second = AccountLedger.PathFor(Root, "user-B", "user-A");
        Assert.True(dbf.SwitchTo(second));
        Assert.Empty(Titles(dbf));                       // ⛔ دفترِ اولی را نمی‌بیند
        Put(dbf, "مالِ حسابِ دوم");
        Assert.Equal(new[] { "مالِ حسابِ دوم" }, Titles(dbf));

        //  ⇒ برگشت به حسابِ اول: دفترش **دست‌نخورده** برمی‌گردد
        Assert.True(dbf.SwitchTo(Root));
        Assert.Equal(new[] { "مالِ حسابِ اول" }, Titles(dbf));

        //  ⛔ و هیچ فایلی پاک نشده — هر دو دفتر روی دیسک‌اند
        Assert.True(File.Exists(Root));
        Assert.True(File.Exists(second));

        //  و رفتنِ دوباره به همان دفتر یک کارِ بی‌خود نیست، هیچ کاری نیست
        Assert.False(dbf.SwitchTo(Root));
        Assert.Equal(Root, dbf.DbPath);
    }
}
