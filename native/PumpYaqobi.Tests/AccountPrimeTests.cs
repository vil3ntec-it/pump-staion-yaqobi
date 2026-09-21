using Microsoft.Data.Sqlite;
using PumpYaqobi.App.Services;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Services.Data;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ ⏳ «لودینگ تا اطلاعاتِ حساب بیاید» ═══════════════════════════════════
///
/// خواستهٔ صریحِ صاحب ریپو (۱۴۰۵/۰۷/۱۱): «یارو اینترنت داره و می‌ره تو حساب
/// است و لودینگ روی صفحه نمیاد تا اطلاعاتی که توی حساب و سرور است بیاد روی
/// همون حساب و اطلاعاتِ مورد نظر.»
///
/// ⚠️ این کلاس <c>AppSettings.DirOverride</c> و <c>AppHost.Start</c> را لمس
/// نمی‌کند و هیچ کلیدِ سراسری‌ای را هم عوض نمی‌کند، پس موازی بی‌خطر است.
/// </summary>
public class AccountPrimeTests : IDisposable
{
    private readonly string _file = Path.Combine(Path.GetTempPath(), $"pump-prime-{Guid.NewGuid():N}.db");
    private readonly PumpDbFactory _dbf;

    public AccountPrimeTests()
    {
        _dbf = new PumpDbFactory(_file);
        _dbf.EnsureReady();
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try { if (File.Exists(_file)) File.Delete(_file); } catch { }
    }

    /// <summary>ریشهٔ ‎native/‎ — همان راهی که بقیهٔ آزمون‌های سورس می‌روند.</summary>
    private static readonly string Root =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    private static string Src(string rel) =>
        File.ReadAllText(Path.Combine(Root, rel.Replace('/', Path.DirectorySeparatorChar)));

    // ── ۱) «پرده کِی می‌آید» — یک جای تصمیم، و هر دو شرط لازم ──────────

    /// <summary>
    /// ⛔ <b>هر دو شرط لازم‌اند.</b> <c>PrimedAt == 0</c> تنها مهرِ درست است
    /// (حسابِ خالی با یک گرفتنِ کاملاً موفق هم <c>Cursor</c>ش صفر می‌ماند و
    /// با ملاکِ مکان‌نما پرده هر سی ثانیه برمی‌گشت)، و <c>Cursor == 0</c>
    /// نصب‌های امروزی را — که از قبل همگام‌اند و این ستون را تازه
    /// گرفته‌اند — از یک پردهٔ بی‌دلیل نگه می‌دارد.
    /// </summary>
    [Fact]
    public void Parde_FaghatBaraye_NakhostinGereftane_HarHesab()
    {
        //  نصبِ تازه، حسابِ تازه ⇒ پرده حق دارد
        Assert.True(SyncEngine.PrimeWanted(new SyncStateRow { Cursor = 0, PrimedAt = 0 }));

        //  گرفتیم و تمام شد ⇒ دیگر نه، حتی اگر سرور هیچ چیزی نداشت
        Assert.False(SyncEngine.PrimeWanted(new SyncStateRow { Cursor = 0, PrimedAt = 17 }));

        //  نصبِ امروزی که از قبل همگام است ⇒ پردهٔ بی‌دلیل نمی‌بیند
        Assert.False(SyncEngine.PrimeWanted(new SyncStateRow { Cursor = 4200, PrimedAt = 0 }));
    }

    // ── ۲) حسابِ تازه ⇒ دفترش دوباره باید بیاید ────────────────────────

    /// <summary>
    /// ورود با حسابِ <b>دیگر</b> روی همین نصب یعنی دفترِ تازه‌ای که هنوز
    /// نیامده — پس مهرِ «گرفتم» باید پاک شود.
    /// ⛔ و <b>یک بیت از دفتر لمس نمی‌شود</b> (قاعدهٔ <c>SyncStore.BindTo</c>).
    /// </summary>
    [Fact]
    public void HesabeTaze_Mohre_Gereftam_RaPakMikonad()
    {
        var store = new SyncStore(_dbf);

        store.BindTo("user-A");
        store.Update(x => { x.PrimedAt = 12345L; x.Cursor = 99L; });
        Assert.False(SyncEngine.PrimeWanted(store.State()));

        var bind = store.BindTo("user-B");

        Assert.True(bind.Rebound);
        Assert.Equal(0L, store.State().PrimedAt);
        Assert.Equal(0L, store.State().Cursor);
        Assert.True(SyncEngine.PrimeWanted(store.State()));
    }

    /// <summary>
    /// ⚠️ ورودِ دوبارهٔ <b>همان</b> حساب هیچ چیزی را از نو نمی‌کند — وگرنه
    /// هر بار باز کردنِ پروفایل یک پردهٔ بی‌دلیل می‌آورد.
    /// </summary>
    [Fact]
    public void HamanHesab_DobareBandShodan_HichHazineyi_Nadarad()
    {
        var store = new SyncStore(_dbf);
        store.BindTo("user-A");
        store.Update(x => { x.PrimedAt = 12345L; x.Cursor = 99L; });

        var again = store.BindTo("user-A");

        Assert.False(again.Rebound);
        Assert.Equal(12345L, store.State().PrimedAt);
        Assert.Equal(99L, store.State().Cursor);
    }

    /// <summary>
    /// ⚠️ <b>ستونِ تازه باید در <c>PatchColumns</c> هم نوشته شود</b> —
    /// فهرستش دستی است و ستونی که آن‌جا نباشد روی دفترِ مشتری ساخته
    /// نمی‌شود و همگام‌سازی همان لحظه با خطای SQLite می‌ایستد.
    /// </summary>
    [Fact]
    public void Sotune_PrimedAt_RooyeDaftareMoshtari_HamSakhteMishavad()
    {
        using var cn = new SqliteConnection($"Data Source={_file}");
        cn.Open();
        using var cmd = cn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM pragma_table_info('SyncState') WHERE name='PrimedAt'";
        Assert.Equal(1L, Convert.ToInt64(cmd.ExecuteScalar()));

        Assert.Contains("(\"SyncState\", \"PrimedAt\"",
            Src("PumpYaqobi.Services/Data/PumpDbFactory.cs"));
    }

    // ── ۳) پرده هیچ‌وقت دیوار نیست ─────────────────────────────────────

    /// <summary>
    /// ⛔ «ادامه در پس‌زمینه» همیشه روی پرده هست، و <b>هر</b> شکستی خودش
    /// پرده را می‌برد. برنامه آفلاین هم باید کار کند (قاعدهٔ ۱۴۰۵/۰۶/۳۰).
    /// </summary>
    [Fact]
    public void Parde_Divar_Nist()
    {
        var engine = Src("PumpYaqobi.App/Services/SyncEngine.cs");
        var view = Src("PumpYaqobi.App/Views/MainWindow.axaml");
        var main = Src("PumpYaqobi.App/ViewModels/MainViewModel.cs");

        //  راهِ بیرون آمدن، در هر سه لایه
        Assert.Contains("public void DismissPrime()", engine);
        Assert.Contains("DismissSyncPrimeCommand", view);
        Assert.Contains("private void DismissSyncPrime()", main);

        //  و هر شکستی پرده را می‌برد — نه فقط یکی
        Assert.Contains("if (priming) EndPrime(false, pull.Why);", engine);
        Assert.Contains("if (priming) EndPrime(false, res.Why);", engine);

        //  ⛔ یک بار در هر اجرا: شبکهٔ لرزان نباید پرده را روشن و خاموش کند
        Assert.Contains("_primeOver = true;", engine);
    }

    /// <summary>
    /// ⛔ پرده فقط با <b>جوابِ واقعیِ</b> موتور می‌آید — پوسته تصمیمِ دومی
    /// نمی‌سازد. دو جای تصمیم یعنی روزی پرده هست و همگام‌سازی نیست.
    /// </summary>
    [Fact]
    public void Poste_TasmimeDovomi_Nemisazad()
    {
        var main = Src("PumpYaqobi.App/ViewModels/MainViewModel.cs");

        Assert.Contains("var on = sync is { Priming: true };", main);
        Assert.Contains("sync!.PrimeText", main);

        //  ⛔ و پوسته خودش «کِی پرده لازم است» را حساب نمی‌کند
        Assert.DoesNotContain("PrimedAt", main);
        Assert.DoesNotContain("PrimeWanted", main);
    }

    /// <summary>
    /// ⛔ مکثِ پانزده‌ثانیه‌ایِ آغاز باید <b>شکستنی</b> باشد، وگرنه کسی که
    /// همین حالا وارد شده پانزده ثانیه به یک صفحهٔ خالی نگاه می‌کند و
    /// گمان می‌کند دفترش رفته.
    /// </summary>
    [Fact]
    public void Voroode_Tamam_Shod_HamanLahze_Migirad()
    {
        var engine = Src("PumpYaqobi.App/Services/SyncEngine.cs");
        var account = Src("PumpYaqobi.App/ViewModels/Sections/AccountSectionViewModel.cs");

        //  ⛔ نه یک `Task.Delay`ِ شکست‌ناپذیر
        Assert.DoesNotContain("await Task.Delay(FirstDelay, ct)", engine);
        Assert.Contains("await _startNow.WaitAsync(FirstDelay, ct)", engine);

        //  و همان لحظه‌ای که گام به «تمام» می‌رسد صدا زده می‌شود
        Assert.Contains("if (v == 4) AppHost.Current.SyncIfStarted?.PrimeNow();", account);

        //  ⚠️ و مکث را فقط همین یک در رد می‌کند، نه هر ذخیرهٔ دیتابیس:
        //  وگرنه بالا آمدنِ برنامه خودش با همگام‌سازی رقیب می‌شد.
        Assert.Equal(1, Count(engine, "_startNow.Release()"));
    }

    /// <summary>
    /// ⛔ وقتی دفترِ حساب رسید، <b>بخشِ جلوی چشم از نو خوانده می‌شود</b> —
    /// وگرنه کاربر صفحه‌ای را می‌بیند که پیش از رسیدنِ داده خوانده شده بود.
    /// </summary>
    [Fact]
    public void Dadeye_Reside_RooyeSafheye_BazMineshinad()
    {
        var main = Src("PumpYaqobi.App/ViewModels/MainViewModel.cs");

        Assert.Contains("sync.PrimeFinished +=", main);
        Assert.Contains("await cur.ReloadAsync();", main);
    }

    /// <summary>
    /// ⛔ هیچ نام و نشانیِ سروری روی پرده نوشته نمی‌شود — همان قاعدهٔ
    /// همیشگیِ چراغ‌ها.
    /// </summary>
    [Fact]
    public void Parde_HichNameSarvari_Nadarad()
    {
        var view = Src("PumpYaqobi.App/Views/MainWindow.axaml");
        var i = view.IndexOf("IsVisible=\"{Binding IsSyncPriming}\"", StringComparison.Ordinal);
        Assert.True(i > 0, "پردهٔ همگام‌سازی در پنجره نیست.");
        var block = view.Substring(i, Math.Min(2200, view.Length - i));

        foreach (var bad in new[] { "http", ".top", ".com", "vill3n" })
            Assert.DoesNotContain(bad, block);
    }

    private static int Count(string text, string needle)
    {
        var n = 0;
        for (var i = text.IndexOf(needle, StringComparison.Ordinal); i >= 0;
             i = text.IndexOf(needle, i + needle.Length, StringComparison.Ordinal)) n++;
        return n;
    }
}
