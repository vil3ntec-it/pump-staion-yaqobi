using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ «حساب ساختم؛ نه آزمایشیِ یک‌ماهه آمد، نه روی سرور هست — و چراغ می‌گوید
/// هر دو وصل‌اند. راست است یا دروغ؟» ══════════════════════════════════════
///
/// گزارشِ صاحب ریپو با سه عکس (۱۴۰۵/۰۷/۱۳). با پشتهٔ واقعی (پنل + سرورِ حساب)
/// بازسازی شد: حسابی که ساخته شد و گامِ پمپ را «بعداً» زد، **هیچ پمپی ندارد**.
/// آزمایشیِ ۳۰ روزه داخلِ مجوزی است که فقط با بند شدنِ دستگاه به پمپ صادر
/// می‌شود — پس هرگز نمی‌آمد. و چراغِ سرورِ حساب فقط «جواب داد؟» را می‌سنجید،
/// پس سبز می‌ماند و یک نیمه‌حقیقت را «وصل» می‌گفت.
///
/// سه قاعده، روی خودِ سورس:
///   ۱) چراغِ سرورِ حساب برای دستگاهِ بندنشده **زرد** است، نه سبز.
///   ۲) پروفایل یک کارتِ «این حساب هنوز پمپی ندارد» با دکمهٔ «ساختنِ پمپ» دارد
///      که همان `FinishPumpAsync`ِ صفحهٔ ورود را می‌زند — نه راهِ دوم.
///   ۳) ردیفِ «سرورِ خانگی» از خودِ ناشرِ زنده می‌خواند، نه از تنظیمات.
/// سنجهٔ رفتاری: `dotnet run --project PumpYaqobi.UiTests -- signuptrial`
/// با `PUMP_SIGNUP_SKIP=1` روی پشتهٔ واقعی.
/// </summary>
public class SignedInNoPumpTests
{
    private static readonly string Root =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    private static string Src(string rel) =>
        //  ⚠️ روی ویندوز (CI) فایل‌ها با CRLF بیرون می‌آیند؛ سنجه‌های «تا پایانِ متد»
        //  با `\n` می‌گردند، پس همه‌جا یک‌شکل می‌شوند.
        File.ReadAllText(Path.Combine(Root, rel.Replace('/', Path.DirectorySeparatorChar))).Replace("\r\n", "\n");

    private const string Main = "PumpYaqobi.App/ViewModels/MainViewModel.cs";
    private const string Vm = "PumpYaqobi.App/ViewModels/Sections/AccountSectionViewModel.cs";
    private const string View = "PumpYaqobi.App/Views/Sections/AccountSectionView.axaml";

    /// <summary>
    /// ⛔ «وقتی که حساب هم نداشته باشم اون سرور باید بگه که وصل است و اگه وصل
    /// بود باید سبز بشه» (۱۴۰۵/۰۷/۱۳). رنگِ چراغ حالِ <b>سرور</b> است؛ «ثبت
    /// نشده» فقط یک خطِ آگاهی زیرِ جمله است (و جایش پروفایل).
    /// </summary>
    [Fact]
    public void Cheragh_BaJavabeSarvar_Sabz_Ast_BaHesab_YaBiHesab()
    {
        var s = Src(Main);
        Assert.DoesNotContain("Online when !DeviceBound()", s);
        var i = s.IndexOf("case Services.CloudReach.Online:", StringComparison.Ordinal);
        var arm = s.Substring(i, s.IndexOf("break;", i, StringComparison.Ordinal) - i);
        Assert.Contains("key = \"Pump.Ok\";", arm);
        Assert.DoesNotContain("Pump.Warn", arm);
        Assert.Contains("UnboundWhy(_signedInCache)", arm);
    }

    [Fact]
    public void ChraghYegane_SarvareHesabeSabz_BiKhanegiyeKharab_Sabz_Ast()
    {
        var s = Src(Main);
        Assert.Contains("else if (acct == \"Pump.Ok\" && home != \"Pump.Danger\") { key = \"Pump.Ok\";", s);
        Assert.Contains("ok + warn > 0", s);
    }

    /// <summary>
    /// ⛔ «این ثبتِ همین کامپیوتر و ساختِ حساب چیه… همین که یارو حسابِ کاربری
    /// برای خودش درست کرد تموم حساب درست شده» (۱۴۰۵/۰۷/۱۳): نه گامِ «نامِ
    /// پمپ»، نه کارتِ «ساختنِ پمپ»، نه دکمهٔ «ثبتِ همین کامپیوتر».
    /// </summary>
    [Fact]
    public void Profile_HichKarteSakhtanYaSabt_Nadarad_HameKhodkar()
    {
        var vm = Src(Vm);
        Assert.DoesNotContain("CreatePumpHereAsync", vm);
        Assert.DoesNotContain("BindHereAsync", vm);
        var v = Src(View);
        Assert.DoesNotContain("CreatePumpHereCommand", v);
        Assert.DoesNotContain("BindHereCommand", v);
        Assert.DoesNotContain("{Binding NeedsPump}", v);
        Assert.DoesNotContain("{Binding NeedsBind}", v);
        Assert.Contains("{Binding LinkingNow}", v);
        //  ⛔ واردشده همیشه «تمام» است — گامِ ۳ (نامِ پمپ) خودکار رد می‌شود
        Assert.Contains("LoginStep = SignedIn || f.LoginSkipped ? 4 : 1;", vm);
        //  و خودِ کار: همان راهِ همیشگی (FinishPumpAsync / CloudKeepNowAsync)، نه راهِ دوم
        var i = vm.IndexOf("private async Task EnsureReadyAsync(", StringComparison.Ordinal);
        var body = vm[i..vm.IndexOf("\n    }\n", i, StringComparison.Ordinal)];
        Assert.Contains("await FinishPumpAsync();", body);
        Assert.Contains("StationPublisher.CloudKeepNowAsync()", body);
        Assert.DoesNotContain("EnsureStationAsync", body);
        //  و باز کردنِ پروفایل منتظرِ اینترنت نمی‌ماند
        Assert.Contains("if (SignedIn && !Busy) _ = EnsureReadySafeAsync();", vm);
    }

    /// <summary>
    /// ⛔ «ببین الان این وصل نمی‌شه» (۱۴۰۵/۰۷/۱۳، عکسِ چراغ): یک جمله برای سه
    /// حال گفته می‌شد و کلیکِ چراغ فقط «بالاست؟» را می‌پرسید. سنجهٔ رفتاری:
    /// `linkstates` روی پشتهٔ واقعی.
    /// </summary>
    [Fact]
    public void HarHal_JomleyeKhodash_Va_KelikeCheragh_SabtMikonad()
    {
        var s = Src(Main);
        Assert.Contains("UnboundWhy(_signedInCache)", s);
        var i = s.IndexOf("private static string UnboundWhy", StringComparison.Ordinal);
        var body = s[i..s.IndexOf(";\n", i, StringComparison.Ordinal)];
        Assert.Contains("هنوز وارد حساب نشده‌اید", body);
        Assert.Contains("AccountHasStation == false", body);
        Assert.Contains("LastBindWhy", body);
        //  ⛔ جملهٔ کهنه که بی‌حساب را به «ساختنِ پمپ» می‌فرستاد، برنگشت
        Assert.DoesNotContain("در «پروفایل» پمپ را بسازید", s);
        Assert.DoesNotContain("نامِ پمپ را بنویسید", s);

        //  ⛔ کلیکِ چراغ همان دورِ کامل را می‌زند و اگر کارِ کاربر است، پروفایل را باز می‌کند
        var c = s.IndexOf("private async Task CheckCloudAsync()", StringComparison.Ordinal);
        var click = s[c..s.IndexOf("\n    }\n", c, StringComparison.Ordinal)];
        Assert.Contains("StationPublisher.CloudKeepNowAsync()", click);
        Assert.Contains("await GoAsync(Account);", click);
        //  ⛔ و هیچ پمپی بی‌خبر نمی‌سازد — «هر حساب یک پمپ»
        Assert.DoesNotContain("EnsureStationAsync", click);
        var pub = Src("PumpYaqobi.App/Services/StationPublisher.cs");
        Assert.DoesNotContain("EnsureStationAsync", pub);
    }

    /// <summary>
    /// ⛔ ورود با حسابی که پمپ دارد، دوباره «نامِ پمپ» نمی‌خواهد — و هر چهار
    /// راهِ ورود از همان یک تصمیم می‌گذرند. سنجهٔ رفتاری: بندهای ز، ح و ط در `linkstates`.
    /// </summary>
    [Fact]
    public void VoroodBaHesabePompDar_NameePompNemikhahad()
    {
        var vm = Src(Vm);
        Assert.Equal(4, vm.Split("await NextStepAfterSignInAsync();").Length - 1);
        var i = vm.IndexOf("private async Task NextStepAfterSignInAsync()", StringComparison.Ordinal);
        var body = vm[i..vm.IndexOf("\n    }\n", i, StringComparison.Ordinal)];
        Assert.Contains("await EnsureReadyAsync(force: true);", body);
        Assert.Contains("LoginStep = 4;", body);
        Assert.DoesNotContain("LoginStep = 3", body);
        //  ⛔ هیچ پمپی بی‌خبر ساخته نمی‌شود — فقط از راهِ کارِ خودِ کاربر
        Assert.DoesNotContain("EnsureStationAsync", body);
        var e = vm.IndexOf("private async Task EnsureReadyAsync(", StringComparison.Ordinal);
        var eb = vm[e..vm.IndexOf("\n    }\n", e, StringComparison.Ordinal)];
        Assert.Contains("HasStationAsync()", eb);
    }

    /// <summary>
    /// ⛔ پس از شکستِ ثبت، حلقه هر دقیقه دوباره نمی‌زند (سقفِ ده‌تاییِ `device/bind`
    /// در ربع ساعت)؛ کلیکِ کاربر همیشه همین حالا.
    /// </summary>
    [Fact]
    public void SabteNashode_HalgheHarDagighe_NemiZanad()
    {
        var c = Src("PumpYaqobi.App/Services/CloudLink.cs");
        Assert.Contains("var bindDue = forceBind || DateTime.UtcNow - _lastBindFailAt >= BindRetryAfterFail;", c);
        Assert.Contains("if (!Activated && acctStation.Length > 0 && bindDue)", c);
        var pub = Src("PumpYaqobi.App/Services/StationPublisher.cs");
        Assert.Contains("await CloudKeepAsync(ct, forceBind: true);", pub);
    }

    /// <summary>
    /// ⛔ حسابِ حذف‌شده: نشست همین حالا مُرد ⇒ خودِ دستگاه پرسیده می‌شود، تا
    /// توکنِ پمپِ حذف‌شده چراغ را سبز نگه ندارد.
    /// </summary>
    [Fact]
    public void NeshasteMorde_Dastgah_Ra_HaminHala_MiPorsad()
    {
        var pub = Src("PumpYaqobi.App/Services/StationPublisher.cs");
        var i = pub.IndexOf("await cloud.HomeFromAccountAsync(ct, forceBind);", StringComparison.Ordinal);
        var after = pub[i..(i + 1400)];
        Assert.Contains("if (!cloud.SignedIn && cloud.Activated)", after);
        Assert.Contains("await cloud.RefreshAsync(ct);", after);
    }

    [Fact]
    public void RadifeSarvareKhanegi_Az_NasherZende_Mikhanad()
    {
        var vm = Src(Vm);
        Assert.Contains("PublisherIfStarted", vm);
        Assert.Contains("Connected: true", vm);
    }

    /// <summary>
    /// ⛔ «حسابی که قبلاً آزمایشی نداشت باز هم نگرفت» (۱۴۰۵/۰۷/۱۳، پس از ۳.۱.۱۷۹).
    /// حسابِ واردشده‌ای که با نسخهٔ پیشین ساخته شده و پمپ ندارد، بی باز کردنِ
    /// پروفایل هم آماده می‌شود: یک بار در هر اجرا، پس از مکثِ ناشر، **همان**
    /// گامِ پروفایل. و حلقهٔ پس‌زمینه همچنان هیچ پمپی نمی‌سازد.
    /// سنجهٔ رفتاری: `oldacct` روی پشتهٔ واقعی.
    /// </summary>
    [Fact]
    public void HesabeGhadimi_BiBazKardaneProfile_Amade_Mishavad()
    {
        var main = Src(Main);
        var hook = main.IndexOf("_ = Account.EnsureReadyOnOpenAsync()", StringComparison.Ordinal);
        Assert.True(hook > 0, "گامِ «حساب آماده» پس از باز شدنِ برنامه صدا زده نمی‌شود");
        //  یک بار در هر اجرا — زیرِ نگهبانِ `_afterSignIn`، نه پیش از آن
        Assert.True(hook > main.IndexOf("_afterSignIn = true;", StringComparison.Ordinal));
        Assert.Contains("await Task.Delay(Services.StationPublisher.FirstDelay)", main);
        var vm = Src(Vm);
        Assert.Contains("public Task EnsureReadyOnOpenAsync() => EnsureReadySafeAsync();", vm);
        //  ⛔ نصبِ وصل‌شده هیچ درخواستی نمی‌زند
        Assert.Contains("if (!string.IsNullOrWhiteSpace(AppSettings.Load().CloudDeviceToken)) return;", vm);
        Assert.DoesNotContain("EnsureStationAsync", Src("PumpYaqobi.App/Services/StationPublisher.cs"));
    }
}
