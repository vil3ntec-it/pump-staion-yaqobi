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
}
