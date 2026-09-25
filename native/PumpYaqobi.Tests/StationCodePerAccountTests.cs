using PumpYaqobi.App.Services;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ «pump1 را حذف کن؛ برای هر حساب یک ایدی باشد» (۱۴۰۵/۰۷/۱۳) ══════════════
///
/// خواستهٔ صریحِ صاحب ریپو: «این pump1 را حذف کن و جای آن برای هر حساب کاربری
/// یک ایدی باشد تا قاطی نشود، و برای هر حساب، حسابِ خودشان بیاید.»
///
/// کدِ پمپ حالا همان کدی است که سرورِ حساب برای پمپِ همان حساب ساخته
/// (‎AppSettings.CloudStationCode‎). همین کد هم پوشهٔ سرورِ خانگی است، هم کدی
/// که اپِ کارمندان می‌گیرد، هم ‎s‎ی کیو‌آرِ زنده. این آزمون‌ها خالص‌اند (هیچ
/// فایلی نمی‌نویسند) مگر آن‌هایی که صریح خودِ سورس را می‌خوانند.
/// </summary>
public class StationCodePerAccountTests
{
    private static AppSettings S(string cloudCode = "", string cloudId = "", string saved = "",
                                 string token = "", string device = "pc-0123456789abcdef01234567") =>
        new()
        {
            CloudStationCode = cloudCode,
            CloudStationId = cloudId,
            StationCode = saved,
            ServerToken = token,
            CloudDeviceUid = device,
        };

    private static string Root()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null && !File.Exists(Path.Combine(d.FullName, "PumpYaqobi.sln")))
            d = d.Parent;
        Assert.NotNull(d);
        return d!.FullName;
    }

    private static string Read(params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { Root() }.Concat(parts).ToArray()));

    /// <summary>کد را بی توضیح‌ها برمی‌گرداند — نامِ ‎pump1‎ در توضیح‌ها هست و باید باشد.</summary>
    private static string Code(string src) =>
        System.Text.RegularExpressions.Regex.Replace(
            System.Text.RegularExpressions.Regex.Replace(src, @"/\*.*?\*/", "",
                System.Text.RegularExpressions.RegexOptions.Singleline),
            @"//.*$", "", System.Text.RegularExpressions.RegexOptions.Multiline);

    [Fact]
    public void HichNasbi_DigarPump1_Nemigirad()
    {
        //  نصبِ تازه، بی حساب و بی هیچ کدی
        Assert.NotEqual("pump1", StationLink.CodeFor(S()));
        Assert.StartsWith("d-", StationLink.CodeFor(S()));
        //  و پیش‌فرضِ خودِ تنظیمات هم دیگر «pump1» نیست
        Assert.Equal("", new AppSettings().StationCode);
    }

    [Fact]
    public void HarHesab_KodeKhodash_Ra_Darad_Va_DoHesab_GhatiNemishavand()
    {
        var a = StationLink.CodeFor(S(cloudCode: "p1a2b3c4d"));
        var b = StationLink.CodeFor(S(cloudCode: "p9z8y7x6w"));
        Assert.Equal("p1a2b3c4d", a);
        Assert.Equal("p9z8y7x6w", b);
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void DoKampiyuterBiHesab_DoKodeJoda_Darand()
    {
        var a = StationLink.CodeFor(S(device: "pc-aaaaaaaaaaaaaaaaaaaaaaaa"));
        var b = StationLink.CodeFor(S(device: "pc-bbbbbbbbbbbbbbbbbbbbbbbb"));
        Assert.NotEqual(a, b);
        Assert.Matches("^d-[a-z0-9]{1,12}$", a);
    }

    [Fact]
    public void KodeSarvareHesab_JolotarAzShenasehAst()
    {
        //  کدِ خودِ سرورِ حساب همان است که اپِ کارمندان و کیو‌آر می‌شناسند
        Assert.Equal("p1a2b3c4d", StationLink.CodeFor(S(cloudCode: "p1a2b3c4d", cloudId: "stn_zzz")));
        //  تا کد نیامده، شناسه — هنوز مالِ همین یک حساب
        Assert.Equal("stn_zzz", StationLink.CodeFor(S(cloudId: "stn_zzz")));
    }

    [Fact]
    public void Pump1eKohne_BaHesab_JabeJa_Mishavad()
    {
        var old = S(cloudCode: "p1a2b3c4d", saved: "pump1", token: "t-old");
        Assert.Equal("p1a2b3c4d", StationLink.CodeFor(old));
        Assert.True(StationLink.NeedsMove(old));
    }

    [Fact]
    public void Pump1eKohne_BiHesab_Ham_JabeJa_Mishavad()
    {
        var old = S(saved: "pump1", token: "t-old");
        Assert.StartsWith("d-", StationLink.CodeFor(old));
        Assert.True(StationLink.NeedsMove(old));
    }

    [Fact]
    public void KodeHesabeDigar_JabeJa_Mishavad_VaKodeKhodash_Na()
    {
        //  پوشهٔ حسابِ قبلی روی همین کامپیوتر ⇒ پوشهٔ خودِ همین حساب
        Assert.True(StationLink.NeedsMove(S(cloudCode: "pnew", saved: "pold", token: "t")));
        //  پوشهٔ خودش، یا جایگزینی که با کدِ خودش شروع می‌شود ⇒ دست نمی‌خورد
        Assert.False(StationLink.NeedsMove(S(cloudCode: "pnew", saved: "pnew", token: "t")));
        Assert.False(StationLink.NeedsMove(S(cloudCode: "pnew", saved: "pnew-89abcdef", token: "t")));
        Assert.Equal("pnew-89abcdef", StationLink.CodeFor(S(cloudCode: "pnew", saved: "pnew-89abcdef")));
        //  بی رمز، چیزی برای جابه‌جایی نیست — ثبتِ بعدی خودش با کدِ درست می‌رود
        Assert.False(StationLink.NeedsMove(S(cloudCode: "pnew", saved: "pold")));
    }

    [Fact]
    public void HesabiKeKodeshKhodPump1Ast_KodashRaNegahMidarad()
    {
        //  پمپِ نخستین که سرورِ حساب واقعاً «pump1» برایش ثبت کرده: این کدِ
        //  همان یک حساب است، نه پیش‌فرضِ مشترک
        var owner = S(cloudCode: "pump1", saved: "pump1", token: "t");
        Assert.Equal("pump1", StationLink.CodeFor(owner));
        Assert.False(StationLink.NeedsMove(owner));
    }

    [Fact]
    public void KodeDastiyeKohne_BiHesab_DastNemikhorad()
    {
        //  کدی که کاربر روزی خودش نوشته بود (نه «pump1»ی مشترک) و هنوز حسابی نیست
        var mine = S(saved: "yaqobi", token: "t");
        Assert.Equal("yaqobi", StationLink.CodeFor(mine));
        Assert.False(StationLink.NeedsMove(mine));
    }

    [Fact]
    public void Jaygozinha_BiHesab_BaKodeHaminKampiyuter_Shoru_Mishavand()
    {
        var f = S();
        var alts = StationLink.Alternatives(f, StationLink.CodeFor(f)).ToList();
        Assert.All(alts, x => Assert.DoesNotContain("pump1", x));
        Assert.All(alts, x => Assert.StartsWith("d-", x));
    }

    [Fact]
    public void DarKodeBarname_HichPump1ePishfarzi_Nist()
    {
        var files = Directory.GetFiles(Path.Combine(Root(), "PumpYaqobi.App"), "*.cs", SearchOption.AllDirectories);
        foreach (var f in files)
        {
            var code = Code(File.ReadAllText(f));
            if (!code.Contains("\"pump1\"")) continue;
            //  ⛔ تنها جای مجاز: نامِ کدِ کهنه برای **شناختنِ** نصب‌هایی که باید جابه‌جا شوند
            Assert.EndsWith("StationLink.cs", f);
            Assert.Single(System.Text.RegularExpressions.Regex.Matches(code, "\"pump1\""));
            Assert.Contains("internal const string LegacySharedCode = \"pump1\";", code);
        }
        Assert.DoesNotContain("DefaultStationCode", Code(Read("PumpYaqobi.App", "Services", "HomeLink.cs")));
        Assert.DoesNotContain("DefaultStationCode", Code(Read("PumpYaqobi.App", "Services", "StationPublisher.cs")));
    }

    [Fact]
    public void KodeHesab_AzSarvareHesab_Mineshinad_VaBaJodaShodan_Mirvad()
    {
        var src = Code(Read("PumpYaqobi.App", "Services", "CloudLink.cs"));
        //  هر سه جایی که شناسهٔ پمپ می‌نشیند، کدش هم می‌نشیند
        Assert.Contains("_settings.CloudStationCode = StationCodeOf(json);", src);
        Assert.Contains("_settings.CloudStationCode = bound;", src);
        Assert.Contains("_settings.CloudStationCode = seenCode;", src);
        //  و نصبی که از قبل بند شده، از `/api/pump/me` می‌گیردش
        Assert.Contains("_settings.CloudStationCode = acctCode;", src);
        //  جدا شدن از پمپ یعنی کدِ آن پمپ هم می‌رود
        var forget = src[src.IndexOf("public async Task ForgetStationAsync()", StringComparison.Ordinal)..];
        Assert.Contains("_settings.CloudStationCode = \"\";", forget[..forget.IndexOf("await SaveQuiet();", StringComparison.Ordinal)]);
    }

    [Fact]
    public void KiuAreZende_BaKodeHamanHesab_Ast()
    {
        var home = Code(Read("PumpYaqobi.App", "Services", "HomeLink.cs"));
        Assert.Contains("public static string CloudCode(AppHost host)", home);
        var acct = Code(Read("PumpYaqobi.App", "Services", "AcctLive.cs"));
        Assert.Contains("return Fragment(HomeLink.CloudCode(host), IdOf(acct)", acct);
        Assert.Contains("return Fragment(HomeLink.CloudCode(host), IdOf(company)", acct);
    }

    [Fact]
    public void JabeJayi_PoosheyeTaze_Migirad_VaHameChiz_AzNo_Miravad()
    {
        var pub = Code(Read("PumpYaqobi.App", "Services", "StationPublisher.cs"));
        Assert.Contains("if (StationLink.NeedsMove(AppSettings.Load())", pub);
        var at = pub.IndexOf("if (StationLink.NeedsMove(AppSettings.Load())", StringComparison.Ordinal);
        var body = pub[at..(at + 900)];
        Assert.Contains("await _sync.DropAsync();", body);
        Assert.Contains("_lastHash = \"\";", body);
        Assert.Contains("_accts.ForgetHome();", body);
        //  ⚠️ تا ثبتِ تازه ننشسته، اتصالِ فعلی دست نمی‌خورد
        Assert.True(body.IndexOf("moved.Ok", StringComparison.Ordinal) < body.IndexOf("DropAsync", StringComparison.Ordinal));
    }
}
