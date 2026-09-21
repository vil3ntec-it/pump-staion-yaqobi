using System.Text.RegularExpressions;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// به‌روزرسانی — و مهم‌تر از خودِ کار، این قاعده که نشانیِ منبع هرگز به چشمِ
/// کاربر نیاید (خواستهٔ صریحِ صاحب ریپو: «توش نوشته نباشه از مخزن فلان فلان»).
/// </summary>
public class UpdateTests
{
    /// <summary>ریشهٔ پروژه — از پوشهٔ خروجیِ آزمون بالا می‌رویم تا native پیدا شود.</summary>
    private static string Root()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null && !File.Exists(Path.Combine(d.FullName, "PumpYaqobi.sln")))
            d = d.Parent;
        Assert.NotNull(d);
        return d!.FullName;
    }

    [Theory]
    [InlineData("2.9.10", "2.9.9", 1)]     // ⚠️ مقایسهٔ رشته‌ای این را برعکس می‌داد
    [InlineData("2.9.9", "2.9.10", -1)]
    [InlineData("3.0.0", "2.9.999", 1)]
    [InlineData("1.2.3", "1.2.3", 0)]
    public void VersionsCompareAsNumbers_NotAsText(string a, string b, int expected)
        => Assert.Equal(expected, Math.Sign(PumpYaqobi.App.Update.UpdateService.Compare(a, b)));

    [Fact]
    public void NoUserFacingFileMentionsTheRepository()
    {
        // نامِ مخزن/کاربری فقط اجازه دارد در همان یک فایلِ سرویسِ به‌روزرسانی باشد
        var allowed = Path.Combine("PumpYaqobi.App", "Update", "UpdateService.cs");
        // ⚠️ «github.com/avaloniaui» فضای‌نامِ خودِ Avalonia است و ربطی به مخزن
        // ندارد؛ پس دنبالِ نامِ صاحب و نامِ مخزن و نشانیِ API می‌گردیم، نه هر «github».
        var needles = new[] { "vil3ntec", "pump-staion-yaqobi", "api.github.com", "github.com/repos" };

        var offenders = new List<string>();
        foreach (var file in Directory.EnumerateFiles(Root(), "*.*", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(Root(), file);
            if (rel.Contains("bin") || rel.Contains("obj")) continue;
            var ext = Path.GetExtension(file).ToLowerInvariant();
            if (ext is not (".axaml" or ".cs" or ".xaml")) continue;
            if (rel == allowed) continue;
            if (rel.EndsWith("UpdateTests.cs")) continue;   // خودِ همین آزمون

            var text = File.ReadAllText(file);
            foreach (var n in needles)
                if (text.Contains(n, StringComparison.OrdinalIgnoreCase))
                { offenders.Add($"{rel} → «{n}»"); break; }
        }

        Assert.True(offenders.Count == 0,
            "نشانیِ منبع نباید بیرون از سرویسِ به‌روزرسانی باشد:\n" + string.Join("\n", offenders));
    }

    [Fact]
    public void TheUpdateServiceItselfKeepsTheAddressPrivate()
    {
        var path = Path.Combine(Root(), "PumpYaqobi.App", "Update", "UpdateService.cs");
        var text = File.ReadAllText(path);

        // نشانی باید ثابتِ private باشد، نه چیزی که به بیرون داده شود
        Assert.Contains("private const string FeedUrl", text);
        Assert.DoesNotContain("public const string FeedUrl", text);

        // و هیچ رشتهٔ دیگری در همین فایل نباید نشانی داشته باشد
        foreach (Match m in Regex.Matches(text, "\"([^\"]{0,200})\""))
        {
            var s = m.Groups[1].Value;
            if (s.Contains("api.github")) continue;   // خودِ همان ثابت
            Assert.DoesNotContain("vil3ntec", s, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("pump-staion", s, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// «هر بار اپ رو از سر دانلود نکنم» — کاربر باید ببیند چه چیزی گرفته
    /// می‌شود. اگر این نوشته نباشد، بستهٔ کوچک هست ولی کسی خبر ندارد.
    /// </summary>
    [Theory]
    [InlineData(true, 2_200_000, "به‌روزرسانیِ کوچک — 2.1 مگابایت")]
    [InlineData(false, 59_000_000, "بستهٔ کامل — 56.3 مگابایت")]
    public void TheUserIsToldWhetherItIsTheSmallPackage(bool small, long size, string expected)
    {
        var info = new PumpYaqobi.App.Update.UpdateInfo(
            true, "3.1.1", "3.1.2", "https://example.invalid/x.zip", size, null, small);
        Assert.Equal(expected, info.PackageText);
    }

    /// <summary>وقتی نسخهٔ تازه‌ای نیست، نوشتهٔ بسته هم نباید چیزی بگوید.</summary>
    [Fact]
    public void NoUpdateMeansNoPackageText()
    {
        var info = new PumpYaqobi.App.Update.UpdateInfo(false, "3.1.1", "3.1.1", null, 0, null);
        Assert.Equal("", info.PackageText);
    }

    /// <summary>
    /// کاربر می‌تواند برنامه را هرجا نصب کند. پس باید بشود فهمید آن پوشه
    /// اجازهٔ نوشتن می‌دهد یا نه — وگرنه به‌روزرسانی بی‌صدا شکست می‌خورد و
    /// برنامه با نسخهٔ کهنه باز می‌شود، بدونِ آنکه کسی بفهمد.
    /// </summary>
    [Fact]
    public void AWritableFolderIsRecognisedAsWritable()
    {
        var dir = Path.Combine(Path.GetTempPath(), "pump-w-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);
        try { Assert.True(PumpYaqobi.App.Update.UpdateService.IsWritable(dir)); }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void AMissingFolderIsNotWritable()
        => Assert.False(PumpYaqobi.App.Update.UpdateService.IsWritable(
               Path.Combine(Path.GetTempPath(), "pump-does-not-exist-" + Guid.NewGuid().ToString("N"))));

    /// <summary>
    /// اگر پوشهٔ نصب اجازهٔ مدیر بخواهد، دستورِ جای‌گزینی باید با اجازه اجرا
    /// شود و نتیجه‌اش نوشته شود. این‌جا خودِ کد خوانده می‌شود چون اجرایش
    /// ویندوز می‌خواهد — ولی نبودِ این دو خط یعنی همان شکستِ بی‌صدا.
    /// </summary>
    [Fact]
    public void TheUpdaterAsksForPermissionInsteadOfFailingSilently()
    {
        var svc = File.ReadAllText(Path.Combine(Root(), "PumpYaqobi.App", "Update", "UpdateService.cs"));
        Assert.Contains("\"runas\"", svc);                 // اجازهٔ مدیر
        Assert.Contains("IsWritable(appDir)", svc);       // فقط وقتی لازم است
        Assert.Contains("if %RC% GEQ 8", svc);            // نتیجهٔ robocopy سنجیده می‌شود
        Assert.Contains("ConsumeLastResult", svc);        // و به کاربر گفته می‌شود
    }

    /// <summary>
    /// ══ نصاب هم باید اجازهٔ مدیر بگیرد ═══════════════════════════════════════
    /// مسیرِ زیپ از اول این را رعایت می‌کرد و مسیرِ نصاب نه. نتیجه‌اش این بود
    /// که به‌روزرسانی در پوشه‌ای مثلِ ‎Program Files‎ **همیشه** شکست می‌خورد،
    /// چون نصابِ ‎/SILENT‎ خودش نمی‌تواند پنجرهٔ اجازه را بالا بیاورد.
    /// </summary>
    [Fact]
    public void TheSetupPathAlsoElevatesWhenTheFolderNeedsIt()
    {
        var svc = File.ReadAllText(Path.Combine(Root(), "PumpYaqobi.App", "Update", "UpdateService.cs"));
        var i = svc.IndexOf("/SILENT", StringComparison.Ordinal);
        Assert.True(i > 0, "اجرای نصاب پیدا نشد");
        // در همان بلوکِ نصاب، شرطِ «پوشه اجازه می‌خواهد» باید باشد
        var block = svc[i..Math.Min(svc.Length, i + 1200)];
        Assert.Contains("InstallDirWritable", block);
        Assert.Contains("runas", block);
    }

    /// <summary>
    /// ══ «واقعاً به‌روز شد؟» با نسخه سنجیده شود، نه با کدِ اسکریپت ═══════════
    ///
    /// کدِ خروجی را فقط مسیرِ زیپ می‌نوشت. اگر به‌روزرسانی از راهِ نصاب
    /// می‌رفت و شکست می‌خورد، هیچ فایلی نوشته نمی‌شد و برنامه با نسخهٔ کهنه و
    /// **بی هیچ پیامی** باز می‌شد — یعنی همان چیزی که قرار بود جلویش گرفته شود.
    ///
    /// مقایسهٔ نسخه هر دو مسیر را می‌پوشاند.
    /// </summary>
    [Fact]
    public void TheResultIsJudgedByVersion_NotByTheScriptExitCode()
    {
        var svc = File.ReadAllText(Path.Combine(Root(), "PumpYaqobi.App", "Update", "UpdateService.cs"));
        Assert.Contains("MarkPending", svc);
        Assert.Contains("Compare(AppVersion.Current, target)", svc);

        // و نسخهٔ هدف باید واقعاً از رابط کاربری پاس داده شود
        // کارتِ به‌روزرسانی از ۱۴۰۵/۰۶/۲۸ در صفحهٔ «بک‌اپ و به‌روزرسانی‌ها» است.
        var vm = File.ReadAllText(Path.Combine(Root(), "PumpYaqobi.App", "ViewModels", "Sections",
                                               "BackupSectionViewModel.cs"));
        Assert.Contains("Launch(_downloaded, _info?.LatestVersion)", vm);
    }

    /// <summary>
    /// هر دو حالت به کاربر گفته می‌شود — گرفتن و نگرفتن. تاییدِ «به‌روز شد»
    /// همان‌قدر لازم است که هشدارِ «نشد»: بی آن، کاربر راهی ندارد بفهمد.
    /// </summary>
    [Fact]
    public void BothOutcomesReachTheUser()
    {
        var view = File.ReadAllText(Path.Combine(Root(), "PumpYaqobi.App", "Views", "Sections",
                                                 "BackupSectionView.axaml"));
        Assert.Contains("LastSuccess", view);
        Assert.Contains("LastFailure", view);
    }

    // ══ «نشد» با «به‌روز است» یکی نیست ══════════════════════════════════════
    // گزارشِ صاحب ریپو (۱۴۰۵/۰۷/۰۶): «از داخل برنامه چرا اپدیت نمیشه؟» —
    // برنامه روی ۳.۱.۱۴۴ بود و نسخهٔ تازه منتشر شده بود. ریشه: هر شکستی
    // (قطعیِ اینترنت، بسته بودنِ مسیر، سقفِ نرخ) به «برنامه به‌روز است»
    // ترجمه می‌شد، پس کاربر هیچ‌وقت نفهمید بررسی اصلاً انجام نشده.

    [Fact]
    public void AFailedCheckNeverClaimsTheAppIsUpToDate()
    {
        var broken = new PumpYaqobi.App.Update.UpdateInfo(
            false, "3.1.144", "3.1.144", null, 0, null, false, "به سرورِ به‌روزرسانی نرسیدیم");

        Assert.True(broken.Failed);
        Assert.DoesNotContain("به‌روز است", broken.StatusText);
        Assert.Contains("نرسیدیم", broken.StatusText);
        Assert.Equal("Pump.Danger", broken.StatusBrushKey);
    }

    [Fact]
    public void AGenuineUpToDateAnswerStillSaysSo()
    {
        var ok = new PumpYaqobi.App.Update.UpdateInfo(false, "3.1.146", "3.1.146", null, 0, null);
        Assert.False(ok.Failed);
        Assert.Contains("به‌روز است", ok.StatusText);
        Assert.Equal("Pump.Muted", ok.StatusBrushKey);
    }

    [Fact]
    public void AnAvailableUpdateNamesTheVersion()
    {
        var up = new PumpYaqobi.App.Update.UpdateInfo(
            true, "3.1.144", "3.1.146", "https://example.invalid/x.zip", 10, null, true);
        Assert.False(up.Failed);
        Assert.Contains("3.1.146", up.StatusText);
    }

    /// <summary>
    /// و همان جمله باید به صفحه برسد — ⛔ ویومدل نباید جملهٔ خودش را بسازد،
    /// وگرنه همان سه حال دوباره جایی با هم قاطی می‌شوند.
    /// </summary>
    [Fact]
    public void TheScreenShowsTheHonestSentence()
    {
        var vm = File.ReadAllText(Path.Combine(Root(), "PumpYaqobi.App", "ViewModels", "Sections",
                                               "BackupSectionViewModel.cs"));
        Assert.Contains("_info.StatusText", vm);
        Assert.Contains("_info.StatusBrushKey", vm);
        Assert.DoesNotContain("\"برنامه به‌روز است\"", vm);

        var view = File.ReadAllText(Path.Combine(Root(), "PumpYaqobi.App", "Views", "Sections",
                                                 "BackupSectionView.axaml"));
        Assert.Contains("UpdateStatusBrushKey", view);
    }

    /// <summary>
    /// ══ درِ دوم ═════════════════════════════════════════════════════════════
    /// ریپوی خواهر (اپِ دکان) از اول دو در داشت و دلیلش را هم نوشته بود:
    /// فهرستِ انتشار برای درخواستِ بی‌توکن سقفِ ساعتی دارد و «به‌روزرسانی
    /// بی‌صدا شکست می‌خورد». برنامهٔ پمپ فقط یک در داشت.
    /// </summary>
    [Fact]
    public void TheCheckHasASecondDoorThatNeedsNoApi()
    {
        var svc = File.ReadAllText(Path.Combine(Root(), "PumpYaqobi.App", "Update", "UpdateService.cs"));
        Assert.Contains("FromApiAsync", svc);
        Assert.Contains("FromFileAsync", svc);
        Assert.Contains("RollingTag", svc);
        Assert.Contains("version.txt", svc);
        Assert.Contains("base.txt", svc);

        // و نشانیِ درِ دوم از خودِ همان یک ثابت ساخته می‌شود، نه از رشتهٔ دوم
        Assert.Contains("new Uri(FeedUrl)", svc);

        // ⛔ خطای خامِ استثنا به کاربر نمی‌رسد (ممکن است نامِ میزبان داشته باشد)
        Assert.DoesNotContain("e.Message", svc);
    }

    /// <summary>
    /// اگر برچسبِ «تازه‌ترین انتشار» شماره نداشته باشد (انتشارِ اپِ گوشی، یا
    /// یک برچسبِ چرخشی)، آن جواب به کارِ برنامهٔ کامپیوتر نمی‌آید و باید درِ
    /// دوم زده شود — نه اینکه «به‌روز است» گفته شود. همان تله‌ای که ریپوی
    /// سرور یک بار خورد.
    /// </summary>
    [Fact]
    public void ATaglessReleaseFallsThroughToTheSecondDoor()
    {
        var svc = File.ReadAllText(Path.Combine(Root(), "PumpYaqobi.App", "Update", "UpdateService.cs"));
        var i = svc.IndexOf("if (latest.Length == 0) return (null,", StringComparison.Ordinal);
        Assert.True(i > 0, "برچسبِ بی‌شماره باید به درِ دوم برود");
    }

    /// <summary>
    /// و فایلِ نیمه‌کاره باید در **هر دو** در رد شود. درِ دوم اندازه را از
    /// قبل نمی‌داند، پس نگهبان باید از خودِ پاسخ بخواند — وگرنه آن مسیر یک
    /// دانلودِ بریده را روی برنامه می‌نشاند.
    /// </summary>
    [Fact]
    public void AHalfDownloadIsRejectedOnBothDoors()
    {
        var svc = File.ReadAllText(Path.Combine(Root(), "PumpYaqobi.App", "Update", "UpdateService.cs"));
        Assert.Contains("if (expected <= 0) expected = res.Content.Headers.ContentLength", svc);
        Assert.Contains("if (expected > 0 && new FileInfo(partial).Length != expected)", svc);
    }

    /// <summary>
    /// نامِ بستهٔ کوچک باید شناسهٔ پایه را بدهد و نامِ بستهٔ کامل هیچ.
    ///
    /// اگر این را اشتباه بخوانیم، بدترین حالت پیش می‌آید: فایل‌های تازهٔ
    /// برنامه روی پایه‌ای می‌نشینند که با آن ساخته نشده‌اند و برنامه بالا
    /// نمی‌آید. برای همین قاعده‌اش صریح است: نامی که با «PumpYaqobi-app-»
    /// شروع نشود، اصلاً بستهٔ کوچک نیست.
    /// </summary>
    [Theory]
    [InlineData("PumpYaqobi-app-1a2b3c4d.zip", "1a2b3c4d")]
    [InlineData("PumpYaqobi-app-ff00ff00.zip", "ff00ff00")]
    [InlineData("PumpYaqobi-Windows.zip", null)]
    [InlineData("PumpYaqobi-Setup.exe", null)]
    [InlineData("version.txt", null)]
    [InlineData("PumpYaqobi-app-.zip", null)]
    public void OnlyTheSmallPackageCarriesABaseId(string name, string? expected)
        => Assert.Equal(expected, PumpYaqobi.App.Update.AppBase.IdInAssetName(name));
}
