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
        File.ReadAllText(Path.Combine(Root, rel.Replace('/', Path.DirectorySeparatorChar)));

    private const string Main = "PumpYaqobi.App/ViewModels/MainViewModel.cs";
    private const string Vm = "PumpYaqobi.App/ViewModels/Sections/AccountSectionViewModel.cs";
    private const string View = "PumpYaqobi.App/Views/Sections/AccountSectionView.axaml";

    [Fact]
    public void Cheragh_Baraye_DastgaheBandNashode_Sabz_Nist()
    {
        var s = Src(Main);
        Assert.Contains("Online when !DeviceBound()", s);
        var i = s.IndexOf("Online when !DeviceBound()", StringComparison.Ordinal);
        var arm = s.Substring(i, s.IndexOf("break;", i, StringComparison.Ordinal) - i);
        Assert.Contains("Pump.Warn", arm);
        Assert.DoesNotContain("Pump.Ok", arm);
        // ⚠️ این شاخه باید پیش از «Online ⇒ سبز» باشد، وگرنه هرگز نمی‌رسد.
        var ok = s.IndexOf("case Services.CloudReach.Online:", StringComparison.Ordinal);
        Assert.True(ok > i, "شاخهٔ «بند نشده» باید پیش از سبزِ Online بیاید");
    }

    [Fact]
    public void ChraghYegane_ZardRa_Hesab_Mikonad()
    {
        var s = Src(Main);
        Assert.Contains("ok + warn > 0", s);
        Assert.Contains("warn > 0", s);
    }

    [Fact]
    public void Profile_KarteSakhtanePomp_Darad_Va_HamanRahRaMizanad()
    {
        var vm = Src(Vm);
        Assert.Contains("NeedsPump = SignedIn && string.IsNullOrWhiteSpace(f.CloudDeviceToken) && !f.PumpStepDone", vm);
        var i = vm.IndexOf("CreatePumpHereAsync", StringComparison.Ordinal);
        Assert.True(i > 0);
        var body = vm.Substring(i, Math.Min(500, vm.Length - i));
        Assert.Contains("await FinishPumpAsync();", body);
        Assert.DoesNotContain("EnsureStationAsync", body);

        var v = Src(View);
        Assert.Contains("{Binding NeedsPump}", v);
        Assert.Contains("CreatePumpHereCommand", v);
    }

    [Fact]
    public void RadifeSarvareKhanegi_Az_NasherZende_Mikhanad()
    {
        var vm = Src(Vm);
        Assert.Contains("PublisherIfStarted", vm);
        Assert.Contains("Connected: true", vm);
    }
}
