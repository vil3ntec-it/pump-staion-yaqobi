using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ قاعده‌های «هر حساب، دفترِ خودش» — روی خودِ سورس ═══════════════════════
///
/// رفتارِ خودِ جابه‌جایی در <see cref="AccountLedgerTests"/> سنجیده می‌شود.
/// این‌جا آن چیزهایی است که <b>بی پنجره سنجیدنی نیستند</b> ولی شکستنشان
/// همان باگ‌هایی را برمی‌گرداند که این کار برای بستنشان نوشته شد.
/// </summary>
public class AccountLedgerSourceTests
{
    private static readonly string Root =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    private static string Src(string rel) =>
        File.ReadAllText(Path.Combine(Root, rel.Replace('/', Path.DirectorySeparatorChar)));

    private const string Host = "PumpYaqobi.App/Services/AppHost.cs";
    private const string Ledger = "PumpYaqobi.Services/Data/AccountLedger.cs";
    private const string Main = "PumpYaqobi.App/ViewModels/MainViewModel.cs";
    private const string Engine = "PumpYaqobi.App/Services/SyncEngine.cs";

    /// <summary>
    /// ⛔ <b>عوض کردنِ حساب هیچ داده‌ای را پاک نمی‌کند.</b> خواستهٔ صریحِ
    /// صاحب ریپو: «اطلاعات دست نخوره، توی حساب‌ها بمونن.»
    /// </summary>
    [Fact]
    public void Avaz_Kardane_Hesab_Hich_Chizi_Ra_Pak_Nemikonad()
    {
        foreach (var f in new[] { Host, Ledger })
        {
            var src = Src(f);
            Assert.DoesNotContain("File.Delete", src);
            Assert.DoesNotContain("Directory.Delete", src);
            Assert.DoesNotContain("File.Move", src);
            Assert.DoesNotContain("File.Copy", src);
        }

        //  و خودِ تصمیمِ مسیر هیچ فایلی هم نمی‌سازد — فقط یک رشته برمی‌گرداند
        var led = Src(Ledger);
        Assert.DoesNotContain("File.WriteAllText", led);
        Assert.DoesNotContain("Directory.CreateDirectory", led);
    }

    /// <summary>
    /// ⛔ <b>یک جای تصمیم.</b> تنها <c>AppHost.UseLedgerOf</c> دفتر را عوض
    /// می‌کند؛ دو جا یعنی روزی دفتر عوض می‌شود و صفحه عددِ دفترِ قبلی را
    /// نشان می‌دهد.
    /// </summary>
    [Fact]
    public void Faghat_Yek_Ja_Daftar_Ra_Avaz_Mikonad()
    {
        foreach (var f in new[] { Main, Engine,
                                  "PumpYaqobi.App/ViewModels/Sections/AccountSectionViewModel.cs" })
            Assert.DoesNotContain(".SwitchTo(", Src(f));

        var host = Src(Host);
        Assert.Contains("public bool UseLedgerOf(", host);
        Assert.Equal(1, Count(host, "Db.SwitchTo("));
    }

    /// <summary>
    /// ⛔ <b>پوشهٔ حساب‌ها کنارِ دفتری می‌نشیند که برنامه با آن بالا آمد</b>،
    /// نه کنارِ <c>PumpDbFactory.DefaultPath</c>.
    ///
    /// سنجه‌ها و ابزارِ عکس‌گیری دیتابیسِ موقتِ خودشان را می‌دهند؛ با مسیرِ
    /// پیش‌فرض، نخستین «حساب عوض شد» آن‌ها را به پوشهٔ <b>واقعیِ</b> کاربر
    /// می‌برد و روی دفترِ خودِ صاحبِ پمپ می‌نوشتند.
    /// </summary>
    [Fact]
    public void Pusheye_Hesabha_Kenare_Daftare_Hamin_Ejra_Ast()
    {
        var host = Src(Host);
        Assert.Contains("_rootDb = Db.DbPath;", host);
        Assert.Contains("AccountLedger.PathFor(_rootDb,", host);
        Assert.DoesNotContain("AccountLedger.PathFor(PumpDbFactory.DefaultPath", host);
    }

    /// <summary>
    /// ⛔ <b>خروج از حساب دفتر را عوض نمی‌کند.</b> بی این، خروجِ حسابِ دوم
    /// دفترِ حسابِ اول را جلوی چشمش می‌گذاشت.
    /// </summary>
    [Fact]
    public void Khoruj_Az_Hesab_Daftar_Ra_Avaz_Nemikonad()
    {
        Assert.Contains("if (id.Length == 0 && LedgerAccountId.Length > 0) return false;", Src(Host));
    }

    /// <summary>
    /// ⛔ <b>صاحبِ دفترِ ریشه با <c>Save()</c>ی بادوام می‌نشیند.</b> گم شدنش
    /// یعنی دفترِ ریشه دوباره «بی‌صاحب» دیده می‌شود و حسابِ بعدی آن را
    /// برمی‌دارد — یعنی کاربر دفترِ حسابِ دیگری را جلوی چشمش می‌بیند.
    /// </summary>
    [Fact]
    public void Sahebe_Daftare_Rishe_Badavam_Mineshinad()
    {
        var host = Src(Host);
        var at = host.IndexOf("file.LedgerAccountId = id;", StringComparison.Ordinal);
        Assert.True(at > 0, "جای نشاندنِ صاحبِ دفترِ ریشه پیدا نشد");
        var after = host[at..Math.Min(host.Length, at + 220)];
        Assert.Contains("file.Save()", after);
        Assert.DoesNotContain("SaveSoon", after);
    }

    /// <summary>
    /// ⛔ <b>حلقهٔ همگام‌سازی پیش از هر خواندنی از دفتر، دفترِ درست را
    /// می‌خواهد.</b> آن حلقه روی نخِ دیگری می‌دود و بی این، یک دور opهای
    /// دفترِ حسابِ <b>قبلی</b> را با توکنِ حسابِ <b>تازه</b> می‌فرستاد —
    /// یعنی دادهٔ یک مشتری در دفترِ ابریِ مشتریِ دیگر.
    /// </summary>
    [Fact]
    public void Halgheye_Hamgamsazi_Aval_Daftare_Dorost_Ra_Mikhahad()
    {
        var src = Src(Engine);
        var use = src.IndexOf("_host.UseLedgerOf(mine)", StringComparison.Ordinal);
        var read = src.IndexOf("var state = _store.State();", StringComparison.Ordinal);
        Assert.True(use > 0, "حلقه اصلاً دفتر را نمی‌سنجد");
        Assert.True(read > 0);
        Assert.True(use < read, "دفتر باید **پیش از** خواندنِ حالِ همگام‌سازی سنجیده شود");

        //  و وسطِ جابه‌جایی هیچ دوری نمی‌دود
        Assert.Contains("public void Hold(bool on)", src);
        Assert.Contains("if (Volatile.Read(ref _held)) return;", src);
        Assert.Contains("sync?.Hold(true);", Src(Host));
    }

    /// <summary>
    /// ⛔ <b>با عوض شدنِ دفتر، هر صفحهٔ باز بسته می‌شود و هر بخش کهنه.</b>
    /// صفحهٔ باز یک شیءِ زنده است که ردیف‌های دفترِ قبلی را در خود دارد و
    /// با تازه شدنِ فهرست بسته نمی‌شود.
    /// </summary>
    [Fact]
    public void Ba_Avaz_Shodane_Daftar_Safheha_Baste_Va_Bakhsha_Kohne_Mishavand()
    {
        var main = Src(Main);
        var at = main.IndexOf("public async Task OnLedgerSwitchedAsync()", StringComparison.Ordinal);
        Assert.True(at > 0, "پوسته به عوض شدنِ دفتر جواب نمی‌دهد");
        var body = main[at..Math.Min(main.Length, at + 1800)];
        Assert.Contains("CloseOpenPage()", body);
        Assert.Contains("s.IsLoaded = false;", body);
        Assert.Contains("LockOrOpenAsync()", body);

        //  و پوسته واقعاً به خبر گوش می‌دهد
        Assert.Contains("AppHost.Current.LedgerSwitched +=", main);

        //  چهار بخشی که صفحهٔ باز دارند، هر چهارتا می‌بندند
        foreach (var f in new[] { "DebtSectionViewModel", "CompanySectionViewModel",
                                  "WaraqSectionViewModel", "AmanatSectionViewModel" })
            Assert.Contains("public override void CloseOpenPage()",
                Src($"PumpYaqobi.App/ViewModels/Sections/{f}.cs"));
    }

    /// <summary>
    /// ⚠️ <b>کارهای یک‌بارمصرفِ پس از ورود دو بار نمی‌دوند.</b> با آمدنِ
    /// دفترِ هر حساب، <c>SignedIn</c> می‌تواند دوباره شلیک شود؛ بی این
    /// نگهبان، سه شنوندهٔ موتورِ همگام‌سازی دو برابر می‌شدند و هر توستِ
    /// اعلان دو بار می‌آمد.
    /// </summary>
    [Fact]
    public void Karhaye_YekBarMasraf_Do_Bar_Nemidavand()
    {
        var main = Src(Main);
        Assert.Contains("if (_afterSignIn) return;", main);
        var guard = main.IndexOf("_afterSignIn = true;", StringComparison.Ordinal);
        var subs = main.IndexOf("sync.PrimeFinished +=", StringComparison.Ordinal);
        Assert.True(guard > 0 && subs > guard, "شنونده‌ها باید **بعد** از نگهبان باشند");
    }

    private static int Count(string src, string needle)
    {
        int n = 0, i = 0;
        while ((i = src.IndexOf(needle, i, StringComparison.Ordinal)) >= 0) { n++; i += needle.Length; }
        return n;
    }
}
