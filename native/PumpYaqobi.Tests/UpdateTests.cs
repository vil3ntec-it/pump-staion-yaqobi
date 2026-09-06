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
        Assert.Contains("ConsumeLastFailure", svc);       // و به کاربر گفته می‌شود
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
