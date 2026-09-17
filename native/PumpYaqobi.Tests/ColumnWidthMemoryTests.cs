using System.Text.RegularExpressions;
using PumpYaqobi.App.Services;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ پهنای ستون یک بار تنظیم شود، همه‌جا و همیشه بماند ════════════════════════
///
/// گزارشِ صاحب ریپو: «بخشِ ورق‌ها — تأکید می‌کنم فقط بخشِ ورق‌ها — وقتی جدولِ
/// یکی را دارم تنظیم می‌کنم، تمامِ ورق و تمامِ جدول‌های همهٔ ورق‌ها برابر
/// بشوند… من الان یک ورق را درست کردم و فردا یکی دیگر را درست می‌کنم؛ نمی‌شود
/// که هر روز من اندازه‌ها را درست کنم.»
///
/// پس دو چیز باید درست باشد و هر دو این‌جا قفل می‌شوند:
///
///   ۱) پهنا جایی بیرون از برنامه نوشته شود تا «فردا» هم باشد.
///   ۲) دو جدولِ تراکنشِ ورق **یک کلید** بگیرند، وگرنه چپ و راست هم‌اندازه
///      نمی‌مانند.
/// </summary>
//  ⚠️ `AppSettings.DirOverride` **استاتیک** است و xUnit کلاس‌ها را موازی
//  می‌دواند؛ بی این نشان، این کلاس و هر کلاسِ دیگری که همان را عوض
//  می‌کند روی هم می‌نویسند و آزمون‌ها **گاهی** سرخ می‌شوند.
[Collection(AppHostCollection.Name)]
public class ColumnWidthMemoryTests
{
    private static readonly string Root =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    private static string Read(params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { Root }.Concat(parts).ToArray()));

    /// <summary>پهنا نوشته می‌شود و همان برمی‌گردد — با بقیهٔ تنظیمات دست‌نخورده.</summary>
    [Fact]
    public void WidthsSurviveClosingTheApp()
    {
        var dir = Path.Combine(Path.GetTempPath(), "pump-cols-" + Guid.NewGuid().ToString("N"));
        var old = AppSettings.DirOverride;
        try
        {
            AppSettings.DirOverride = dir;

            // تمی که کاربر انتخاب کرده باید بعدِ نوشتنِ پهنا هم سرِ جایش باشد
            var a = AppSettings.Load();
            a.ThemeId = "marble";
            a.LastSection = "waraq";
            a.Save();

            Assert.Null(AppSettings.LoadColumnWidths("waraq.txns"));

            AppSettings.SaveColumnWidths("waraq.txns", new[] { 260d, 90d, 110d, 130d, 80d, 70d });

            var back = AppSettings.LoadColumnWidths("waraq.txns");
            Assert.NotNull(back);
            Assert.Equal(new[] { 260d, 90d, 110d, 130d, 80d, 70d }, back);

            Assert.Equal("waraq", AppSettings.Load().LastSection);
        }
        finally
        {
            AppSettings.DirOverride = old;
            try { Directory.Delete(dir, true); } catch { }
        }
    }

    /// <summary>جدولی بی کلید چیزی نمی‌نویسد — پیش‌فرضِ همهٔ جدول‌های دیگر.</summary>
    [Fact]
    public void AGridWithNoKeyRemembersNothing()
    {
        var dir = Path.Combine(Path.GetTempPath(), "pump-cols-" + Guid.NewGuid().ToString("N"));
        var old = AppSettings.DirOverride;
        try
        {
            AppSettings.DirOverride = dir;
            AppSettings.SaveColumnWidths("", new[] { 100d });
            Assert.Empty(AppSettings.Load().ColumnWidths);
        }
        finally
        {
            AppSettings.DirOverride = old;
            try { Directory.Delete(dir, true); } catch { }
        }
    }

    /// <summary>
    /// ⚠️ دو جدولِ تراکنشِ ورق باید **یک** کلید داشته باشند. اگر کسی روزی
    /// دو کلیدِ جدا بگذارد، چپ و راست از هم می‌افتند — دقیقاً همان چیزی که
    /// گزارش شده بود.
    /// </summary>
    [Fact]
    public void BothTransactionGridsShareOneKey()
    {
        var x = Read("PumpYaqobi.App", "Views", "Sections", "WaraqPageView.axaml");
        var keys = Regex.Matches(x, @"WidthKey=""([^""]+)""")
                        .Select(m => m.Groups[1].Value).ToList();

        Assert.Equal(3, keys.Count);                       // پایه‌ها + دو جدولِ تراکنش
        Assert.Equal(2, keys.Count(k => k == "waraq.txns"));
        Assert.Contains("waraq.pumps", keys);
    }

    /// <summary>پهنای ذخیره‌شده باید بر پهنای طبیعیِ محتوا مقدم باشد.</summary>
    [Fact]
    public void TheSavedWidthWinsOverTodaysContent()
    {
        var g = Read("PumpYaqobi.App", "Controls", "ExcelGrid.cs");
        Assert.Contains("LoadColumnWidths", g);
        Assert.Contains("SaveColumnWidths", g);
        Assert.Contains("saved is not null", g);
    }
}
