using System.Net;
using System.Net.Http;
using System.Text;
using PumpYaqobi.App.Update;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ ۳۲بیتی و ۶۴بیتی ════════════════════════════════════════════════════════
/// خواستهٔ صاحب ریپو (۱۴۰۵/۰۷/۱۲): «برنامه رو نمیشه هر سه مدل شد؟ ۳۲ بیت،
/// ۶۴ بیت و ۸۶ بیت، چون کامپیوتر خیلی نسخه قدیمی است.»
///
/// ⚠️ «۸۶ بیت» وجود ندارد — <c>x86</c> همان ۳۲بیتی است. پس دو مدل منتشر
/// می‌شود، و خطرِ واقعیِ دو مدل این است که نصبِ یکی بستهٔ آن یکی را بگیرد:
/// کاربر «به‌روزرسانی» می‌زند و برنامه‌ای می‌گیرد که ویندوزش اجرا نمی‌کند.
/// این فایل همان را می‌بندد — با **رفتار**، نه با خواندنِ رشته.
///
/// ⚠️ <c>[Collection]</c> لازم است: <see cref="AppArch.Override"/> و
/// <c>AppBase.LocalIdOverride</c> و <c>UpdateService.TestTransport</c> هر سه
/// **استاتیک**‌اند و <c>UpdateBehaviourTests</c> هم همان‌ها را عوض می‌کند.
/// </summary>
[Collection(AppHostCollection.Name)]
public class ThirtyTwoBitTests : IDisposable
{
    public ThirtyTwoBitTests() => AppBase.LocalIdOverride = "aaaa1111";

    public void Dispose()
    {
        UpdateService.TestTransport = null;
        AppBase.LocalIdOverride = null;
        AppArch.Override = null;
    }

    private static readonly string Native =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    private static readonly string Repo = Path.GetFullPath(Path.Combine(Native, ".."));

    private static string Workflow() =>
        File.ReadAllText(Path.Combine(Repo, ".github", "workflows", "build-native.yml"));

    private static string Iss() =>
        File.ReadAllText(Path.Combine(Native, "installer", "PumpYaqobi.iss"));

    private static HttpResponseMessage Json(string body) =>
        new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static HttpResponseMessage Text(string body) =>
        new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "text/plain") };

    /// <summary>
    /// انتشاری که **هر دو** نصاب را دارد و بستهٔ کوچکش به پایهٔ این نصب
    /// نمی‌خورد — پس برنامه ناچار است یکی از دو نصاب را برگزیند.
    /// </summary>
    private const string BothSetups = """
        {
          "tag_name": "v99.9.9",
          "body": "یادداشت",
          "assets": [
            { "name": "PumpYaqobi-app-ffff9999.zip", "size": 6000000,
              "browser_download_url": "https://x/small-x64.zip" },
            { "name": "PumpYaqobi-Setup.exe", "size": 160000000,
              "browser_download_url": "https://x/setup.exe" },
            { "name": "PumpYaqobi-Windows.zip", "size": 127000000,
              "browser_download_url": "https://x/noinstall-x64.zip" },
            { "name": "PumpYaqobi-Windows-x86.zip", "size": 117000000,
              "browser_download_url": "https://x/noinstall-x86.zip" }
          ]
        }
        """;

    // ══ ۱) درِ اول: هر دو نصب همان یک نصاب را می‌گیرند، نه زیپِ معماریِ دیگر ══
    //  از ۱۴۰۵/۰۷/۱۴ نصاب یکی است و هر دو بار داخلش؛ برنامه با ‎/ARCH‎ بارِ
    //  خودش را می‌نشاند. زیپ‌های بی‌نصاب همچنان مالِ معماریِ خودشان‌اند.

    [Fact]
    public async Task NasbeSiVaDo_Hamaan_NasabeYegane_Ra_Barmidarad()
    {
        AppArch.Override = "x86";
        UpdateService.TestTransport = (_, _) => Task.FromResult(Json(BothSetups));

        var info = await new UpdateService().CheckAsync();

        Assert.True(info.Available);
        Assert.False(info.IsSmallPackage);
        Assert.Equal("https://x/setup.exe", info.DownloadUrl);
    }

    [Fact]
    public async Task NasbeShastVaChahar_Hargez_Zipe_SiVaDo_Ra_Nemigirad()
    {
        AppArch.Override = "x64";
        UpdateService.TestTransport = (_, _) => Task.FromResult(Json(BothSetups));

        var info = await new UpdateService().CheckAsync();

        Assert.True(info.Available);
        Assert.Equal("https://x/setup.exe", info.DownloadUrl);
        //  ⛔ و این مهم‌ترین ادعای این فایل است: بستهٔ ۳۲بیتی روی نصبِ
        //  ۶۴بیتی برنامه‌ای می‌دهد که کار می‌کند ولی کندتر است — و ساکت،
        //  پس هیچ‌کس نمی‌فهمد چرا.
        Assert.DoesNotContain("x86", info.DownloadUrl!);
    }

    /// <summary>⛔ نصابِ بی‌صدا همیشه با ‎/ARCH‎ی خودش صدا زده می‌شود.</summary>
    [Fact]
    public void Nasabe_BiSeda_Ba_ArchE_Khodash_Seda_Zadeh_Mishavad()
    {
        AppArch.Override = "x86";
        Assert.Equal("/ARCH=x86", AppArch.ArchArg);
        AppArch.Override = "x64";
        Assert.Equal("/ARCH=x64", AppArch.ArchArg);
        var up = File.ReadAllText(Path.Combine(Native, "PumpYaqobi.App", "Update", "UpdateService.cs"));
        var launch = up.Split("public static bool Launch(")[1].Split("private static bool LaunchZip(")[0];
        Assert.Contains("AppArch.ArchArg", launch);
    }

    // ══ ۲) درِ دوم: هر معماری فایلِ پایهٔ خودش ═══════════════════════════════

    [Fact]
    public async Task DarreDovom_SiVaDo_Az_BaseX86_Mikhanad()
    {
        AppArch.Override = "x86";
        var asked = new List<string>();

        UpdateService.TestTransport = (req, _) =>
        {
            var u = req.RequestUri!.ToString();
            asked.Add(u);
            if (u.EndsWith("/releases/latest")) throw new HttpRequestException("درِ اول بسته");
            if (u.EndsWith("/version.txt")) return Task.FromResult(Text("99.9.9"));
            if (u.EndsWith("/base-x86.txt")) return Task.FromResult(Text("aaaa1111"));
            //  پایهٔ ۶۴بیتی هم هست و **عمداً همان چیزی است که این نصب دارد** —
            //  اگر برنامه اشتباهی این را بخواند، سنجه سبزِ دروغ می‌دهد.
            if (u.EndsWith("/base.txt")) return Task.FromResult(Text("aaaa1111"));
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        };

        var info = await new UpdateService().CheckAsync();

        Assert.True(info.Available);
        Assert.True(info.IsSmallPackage);
        //  ⚠️ با گزاره، نه با رشته: ‎Assert.Contains(string, List<string>)‎
        //  اورلودِ **مجموعه** است و برابریِ عنصر می‌خواهد، نه زیررشته —
        //  و `asked` نشانیِ کامل دارد. همین یک اورلود سنجه را سرخ کرد
        //  در حالی که کد درست بود.
        Assert.Contains(asked, u => u.EndsWith("/base-x86.txt", StringComparison.Ordinal));
        Assert.DoesNotContain(asked, u => u.EndsWith("/base.txt"));
    }

    [Fact]
    public async Task DarreDovom_SarvareKohne_BastehyeKuchake_Digar_Ra_Nemigirad()
    {
        AppArch.Override = "x86";

        //  سرورِ کهنه فقط `base.txt`ِ ۶۴بیتی دارد. نصبِ ۳۲بیتی نباید از آن
        //  بستهٔ کوچک بسازد — باید برود سراغِ نصابِ کاملِ ۳۲بیتیِ خودش.
        UpdateService.TestTransport = (req, _) =>
        {
            var u = req.RequestUri!.ToString();
            if (u.EndsWith("/releases/latest")) throw new HttpRequestException("درِ اول بسته");
            if (u.EndsWith("/version.txt")) return Task.FromResult(Text("99.9.9"));
            if (u.EndsWith("/base.txt")) return Task.FromResult(Text("aaaa1111"));
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        };

        var info = await new UpdateService().CheckAsync();

        Assert.True(info.Available);
        Assert.False(info.IsSmallPackage);
        //  یک نصاب برای هر دو معماری (۱۴۰۵/۰۷/۱۴) — بارِ ۳۲ داخلِ همین است
        Assert.EndsWith("PumpYaqobi-Setup.exe", info.DownloadUrl);
    }

    // ══ ۳) نام‌ها ════════════════════════════════════════════════════════════

    [Fact]
    public void NameFayleShastVaChahar_Yek_Harf_Ham_Avaz_Nashod()
    {
        AppArch.Override = "x64";
        //  ⛔ هر نصبی که همین حالا دستِ مشتری است کدِ قدیمی دارد و فقط این
        //  دو نام را می‌شناسد. عوض کردنشان یعنی همهٔ آن نصب‌ها یک‌شبه از
        //  به‌روزرسانی می‌افتند.
        Assert.Equal("PumpYaqobi-Setup.exe", AppArch.SetupName);
        Assert.Equal("base.txt", AppArch.BaseFileName);

        //  ⛔ نصاب یکی است (۱۴۰۵/۰۷/۱۴): ۳۲بیتی هم همان فایل را می‌گیرد و با
        //  ‎/ARCH=x86‎ بارِ خودش را می‌نشاند؛ فقط فایلِ پایه‌اش جداست.
        AppArch.Override = "x86";
        Assert.Equal("PumpYaqobi-Setup.exe", AppArch.SetupName);
        Assert.Equal("/ARCH=x86", AppArch.ArchArg);
        Assert.Equal("base-x86.txt", AppArch.BaseFileName);
    }

    [Fact]
    public void Owns_Name_BiPasvand_Ra_ShastVaChahar_Mishomarad()
    {
        AppArch.Override = "x64";
        Assert.True(AppArch.Owns("PumpYaqobi-Setup.exe"));
        Assert.True(AppArch.Owns("PumpYaqobi-Windows.zip"));
        Assert.False(AppArch.Owns("PumpYaqobi-Windows-x86.zip"));

        //  نصابِ یگانه مالِ هر دو است؛ زیپ‌ها همچنان مالِ معماریِ خودشان
        AppArch.Override = "x86";
        Assert.True(AppArch.Owns("PumpYaqobi-Setup.exe"));
        Assert.True(AppArch.Owns("PumpYaqobi-Windows-x86.zip"));
        Assert.False(AppArch.Owns("PumpYaqobi-Windows.zip"));
    }

    // ══ ۴) سمتِ ساخت ════════════════════════════════════════════════════════

    [Fact]
    public void Workflow_Har_Do_Memari_Ra_Misazad_Va_Montasher_Mikonad()
    {
        var w = Workflow();

        //  هر دو RID ساخته می‌شوند
        Assert.Contains("win-x64", w);
        Assert.Contains("win-x86", w);
        Assert.Contains("dotnet publish PumpYaqobi.App", w);

        //  و یک نصاب با هر دو بار منتشر می‌شود، و فایلِ پایهٔ هر معماری جدا
        Assert.Contains("rel/PumpYaqobi-Setup.exe", w);
        Assert.DoesNotContain("rel/PumpYaqobi-Setup-x86.exe", w);
        Assert.Contains("/DSourceDir64=..\\publish\\win-x64", w);
        Assert.Contains("/DSourceDir86=..\\publish\\win-x86", w);
        Assert.Contains("rel/base.txt", w);
        Assert.Contains("rel/base-x86.txt", w);

        //  ⛔ و دو بستهٔ کوچکِ جدا: شناسهٔ پایهٔ دو معماری هیچ‌وقت یکی
        //  نمی‌شود، ولی «شدنی نیست» با «سنجیده شد» یکی نیست — خودِ ورک‌فلو
        //  هم اگر یکی درآمد می‌شکند.
        Assert.Contains("steps.pack.outputs.baseId86", w);
        Assert.Contains("شناسهٔ پایهٔ ۳۲ و ۶۴بیتی یکی درآمد", w);
    }

    [Fact]
    public void Nasab_Yeki_Ast_Ba_AppIdE_Hamishegi()
    {
        var s = Iss();

        //  ⛔ AppIdِ همیشگی یک حرف هم عوض نشد — عوض شدنش یعنی هر نصبِ
        //  امروزیِ مشتری برای ویندوز «غریبه» می‌شود. و از ۱۴۰۵/۰۷/۱۴ هر دو
        //  معماری همین یک AppId و همین یک پوشه را دارند: یکی جای دیگری.
        Assert.Contains("AppId={#AppGuid}", s);
        Assert.Contains("#define AppGuid \"{{8E86F349-343C-4FFB-983E-BBDDC5390081}\"", s);
        Assert.DoesNotContain("PumpYaqobi-32", s);
        Assert.DoesNotContain("#ifndef Arch", s);

        //  ⛔ روی ویندوزِ ۳۲بیتی هر چه باشد ۳۲بیتی می‌نشیند — ۶۴ آن‌جا اجرا نمی‌شود
        Assert.Contains("if not IsWin64 then Result := 'x86';", s);
        Assert.Contains("if not IsWin64 then\n    ArchPage.CheckListBox.ItemEnabled[0] := False;", s.Replace("\r\n", "\n"));
    }
}
