using System.Linq;
using System.IO;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ «هر بخش رو باز می‌کنم جدول‌ها یک ثانیه بعد میان» ═══════════════════════
///
/// گزارشِ صاحب ریپو (۱۴۰۵/۰۷/۰۵). دو ریشه داشت و هر دو این‌جا قفل می‌شوند —
/// روی خودِ سورس، چون هر دو یک «ترتیبِ کار»اند و با یک ویرایشِ بی‌خبر
/// برمی‌گردند.
///
/// سنجهٔ رفتاری‌اش جداست و عدد می‌دهد:
/// <c>dotnet run --project PumpYaqobi.UiTests -c Release -- sectionopen</c>
/// </summary>
public class SectionOpenSpeedTests
{
    private static string Root()
    {
        var d = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (d is not null && !File.Exists(Path.Combine(d.FullName, "PumpYaqobi.sln")))
            d = d.Parent;
        Assert.NotNull(d);
        return d!.FullName;
    }

    private static string Read(params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { Root() }.Concat(parts).ToArray()));

    /// <summary>
    /// ⛔ <b>«آخرین بخش» هیچ‌وقت روی نخِ رابط روی دیسک نمی‌نشیند.</b>
    ///
    /// <see cref="PumpYaqobi.App.Services.AppSettings.Save"/> عمداً بادوام
    /// است: فایلِ موقت، <c>Flush(true)</c> (یک <c>FlushFileBuffers</c>ِ
    /// واقعی) و <c>File.Replace</c>ِ سه‌فایلی. آن دوام برای توکنِ دستگاه و
    /// کلیدِ عمومی <b>لازم</b> است و برنمی‌گردد.
    ///
    /// ولی <c>GoAsync</c> همان را برای یک مقدارِ راحتی صدا می‌زد، درست
    /// <b>بینِ</b> نشان دادنِ صفحه و خواندنِ داده‌اش — یعنی ردیف‌ها پشتِ یک
    /// <c>fsync</c> معطل می‌ماندند. روی ویندوز، با ضدِ ویروسی که همان لحظه
    /// فایل را باز می‌کند، همان می‌شود «یک ثانیه بعد».
    /// </summary>
    [Fact]
    public void AkharinBakhsh_RoyeNakheRabet_RoyeDisk_Nemineviseh()
    {
        var vm = Read("PumpYaqobi.App", "ViewModels", "MainViewModel.cs");

        //  ⚠️ روی خودِ **کد** می‌گردیم، نه روی توضیحات: نامِ قدیمی در کامنتِ
        //  «این بود و چرا رفت» هست و باید هم باشد.
        var code = string.Join("\n", vm.Split('\n')
            .Where(l => !l.TrimStart().StartsWith("//")));

        Assert.Contains("_settings.LastSection = s.Id;", code);
        Assert.Contains("_settings.SaveSoon();", code);
        //  ⛔ و هیچ `Save()`ِ هم‌زمانی در مسیرِ ناوبری و تعویضِ تم نماند
        Assert.DoesNotContain("_settings.Save();", code);

        //  و خودِ `SaveSoon` واقعاً تأخیری و بیرونِ نخِ رابط است
        var st = Read("PumpYaqobi.App", "Services", "AppSettings.cs");
        Assert.Contains("public void SaveSoon()", st);
        Assert.Contains("System.Threading.Timer", st);
        //  ⛔ و نوشتنِ بادوام دست‌نخورده ماند — این سنجه دربارهٔ **کجا**ست،
        //     نه دربارهٔ «دوام را بردار»
        Assert.Contains("fs.Flush(true);", st);
        Assert.Contains("File.Replace(tmp, File_, Backup_", st);
    }

    /// <summary>
    /// ⛔ <b>فعال‌سازیِ بخش بی ترمزِ <c>Version</c> نمی‌ماند.</b>
    ///
    /// گزارشِ صاحب ریپو (۱۴۰۵/۰۷/۰۵، بارِ دوم): «رسید قرض‌داران · صرافی ·
    /// مصارف · رسید پارچه‌ها · گاوصندوق — جدول‌هاشون دیر باز میشه.»
    ///
    /// ریشه: هجده بخش در <c>OnActivatedAsync</c> خودشان را از نو می‌خواندند
    /// — و درست هم بود، چون دادهٔ یک بخش را بخشِ دیگری عوض می‌کند. ولی
    /// <b>بی هیچ ترمزی</b>: با هر رفت‌وآمد یک پرس‌وجوی SQLite، ساختِ دوبارهٔ
    /// همهٔ ردیف‌ها، یک <c>Reset</c>، و بعد <c>ExcelGrid</c> پهنای ستون‌ها را
    /// دور می‌ریخت و از نو می‌سنجید — حتی وقتی هیچ چیزی عوض نشده بود.
    ///
    /// ⚠️ ترمزش تازه نیست: <c>PumpDbContext.Version</c> همان ترمزی است که
    /// نوار و داشبورد و <c>StationPublisher</c> و بخشِ ورق از ۱۴۰۵/۰۶/۲۶
    /// دارند («⛔ هیچ کارِ دوره‌ای بی ترمزِ Version»).
    ///
    /// ⛔ و عمداً همگانی نیست: بخشی که در فعال‌سازی چیزی <b>بیرونِ</b> دفتر
    /// می‌خواند (فایل‌های پشتیبان، حالِ سرورِ حساب، صندوقِ چت، حالِ
    /// همگام‌سازی) نباید این پرچم را بگیرد — این آزمون همان را هم می‌سنجد.
    /// </summary>
    [Fact]
    public void FaalSaziyeBakhsh_TormozeVersion_Darad()
    {
        var sec = Read("PumpYaqobi.App", "ViewModels", "SectionViewModel.cs");
        Assert.Contains("public virtual bool ActivationOnlyReadsDb => false;", sec);
        Assert.Contains("PumpYaqobi.Persistence.PumpDbContext.Version", sec);
        Assert.Contains("public bool ActivationCanBeSkipped", sec);

        //  و مسیرِ ناوبری واقعاً از آن استفاده می‌کند — هر دو در، بخش و زیربخش
        var vm = Read("PumpYaqobi.App", "ViewModels", "MainViewModel.cs");
        var code = string.Join("\n", vm.Split('\n').Where(l => !l.TrimStart().StartsWith("//")));
        Assert.Equal(2, System.Text.RegularExpressions.Regex.Matches(code, "ActivationCanBeSkipped").Count);
        Assert.Equal(2, System.Text.RegularExpressions.Regex.Matches(code, "MarkActivationSeen").Count);

        //  ⛔ بخش‌هایی که فقط دفتر را می‌خوانند، پرچم را دارند
        foreach (var f in new[] { "ExpenseSectionViewModel", "DebtSectionViewModel",
                                  "DebtSummarySectionViewModel", "OldLoansSectionViewModel",
                                  "ProfitSectionViewModel", "PriceLossSectionViewModel",
                                  "RateHistorySectionViewModel", "StaffShortSectionViewModel",
                                  "InvRateSectionViewModel", "InvoiceSectionViewModel",
                                  "MonthReportSectionViewModel" })
            Assert.Contains("public override bool ActivationOnlyReadsDb => true;",
                            Read("PumpYaqobi.App", "ViewModels", "Sections", f + ".cs"));

        //  ⛔ و بخش‌هایی که بیرونِ دفتر را می‌خوانند، **نباید** داشته باشند
        foreach (var f in new[] { "BackupSectionViewModel", "AccountSectionViewModel",
                                  "ChatSectionViewModel", "SyncSectionViewModel",
                                  "KeysSectionViewModel" })
            Assert.DoesNotContain("ActivationOnlyReadsDb => true",
                                  Read("PumpYaqobi.App", "ViewModels", "Sections", f + ".cs"));
    }

    /// <summary>
    /// ⛔ <b>هیچ شنوندهٔ <c>LayoutUpdated</c>ی بی سنجشِ دیده‌شدن.</b>
    ///
    /// قاعدهٔ ۱۴۰۵/۰۶/۲۶ (نمونه‌بردار نشان داد ۱۳٪ وقتِ نخِ رابط):
    /// <c>LayoutUpdated</c> برای <b>هر</b> چیدمانِ <b>هر جای</b> پنجره شلیک
    /// می‌شود و همهٔ بخش‌ها با هم در درخت می‌مانند. پس شنونده‌ای که این را
    /// نسنجد، با هر فریمِ اسکرولِ <b>بخشِ دیگری</b> هم کار می‌کند.
    ///
    /// ⚠️ سه شنونده این‌جا سنجیده می‌شوند و هر سه در درختِ دائمی‌اند. پنجرهٔ
    /// چاپ (<c>DocumentPreviewWindow</c>) عمداً بیرون است: پنجرهٔ جداست و
    /// فقط تا وقتی باز است زنده می‌ماند.
    /// </summary>
    [Fact]
    public void HichShenavandeyeChideman_BiSanjesheDideShodan_Nist()
    {
        foreach (var (file, parts) in new (string, string[])[]
                 {
                     ("ExcelGrid",      new[] { "PumpYaqobi.App", "Controls", "ExcelGrid.cs" }),
                     ("TotalsBar",      new[] { "PumpYaqobi.App", "Controls", "TotalsBar.cs" }),
                     ("DebtArchiveView", new[] { "PumpYaqobi.App", "Views", "Sections", "DebtArchiveView.axaml.cs" }),
                 })
        {
            var src = Read(parts);
            var at = src.IndexOf("LayoutUpdated +=", System.StringComparison.Ordinal);
            Assert.True(at >= 0, $"{file}: شنوندهٔ چیدمان پیدا نشد.");
            //  نگهبان باید در همان چند خطِ اولِ خودِ شنونده باشد
            var body = src[at..Math.Min(src.Length, at + 400)];
            Assert.True(body.Contains("IsEffectivelyVisible"),
                        $"{file}: شنوندهٔ `LayoutUpdated` بی `IsEffectivelyVisible` است.");
        }
    }

    /// <summary>
    /// ⛔ <b>جدولِ پارک‌شده در همان پاس برمی‌گردد، نه یک نوبتِ ‎Dispatcher‎ بعد.</b>
    ///
    /// ترتیبی که کاربر می‌دید:
    /// <code>
    ///   SyncContent() ⇒ صفحه عوض شد ⇒ NotifyPagesChanged() فقط Post می‌کرد
    ///   چیدمانِ ۱     ⇒ جدول دیده می‌شود ولی ItemsSourceش هنوز پارک است
    ///                 ⇒ یک جدولِ **خالی** به کاربر نشان داده می‌شود
    ///   نوبتِ Loaded  ⇒ تازه حالا فهرست برمی‌گردد و ردیف‌ها ساخته می‌شوند
    /// </code>
    ///
    /// ⚠️ جهتِ <b>پنهان کردن</b> همچنان با <c>Post</c> است و باید بماند:
    /// <c>IsEffectivelyVisible</c> همان لحظه هنوز مقدارِ قبلی را می‌دهد.
    /// فقط جهتِ «برگرد» هم‌زمان شد، که اصلاً به آن نیازی ندارد —
    /// <c>SectionShown()</c> از خودِ ویومدل می‌پرسد و <c>SyncContent</c> آن
    /// را پیش‌تر نوشته است.
    /// </summary>
    [Fact]
    public void JadvaleParkShode_DarHamanPas_Barmigardad()
    {
        var g = Read("PumpYaqobi.App", "Controls", "ExcelGrid.cs");
        var code = string.Join("\n", g.Split('\n')
            .Where(l => !l.TrimStart().StartsWith("//")));

        //  دو جهت، دو رویداد
        Assert.Contains("private static event Action? PagesShown;", code);
        Assert.Contains("private static event Action? PagesChanged;", code);

        //  «برگرد» هم‌زمان، پیش از هر `Post`ی
        var notify = code[code.IndexOf("public static void NotifyPagesChanged()", System.StringComparison.Ordinal)..];
        notify = notify[..notify.IndexOf("private void OnPagesChanged", System.StringComparison.Ordinal)];
        var shownAt = notify.IndexOf("PagesShown?.Invoke();", System.StringComparison.Ordinal);
        var postAt = notify.IndexOf("Dispatcher.UIThread.Post", System.StringComparison.Ordinal);
        Assert.True(shownAt >= 0, "جهتِ «برگرد» باید صدا زده شود.");
        Assert.True(postAt >= 0, "جهتِ «پنهان کن» باید همچنان `Post` بماند.");
        Assert.True(shownAt < postAt, "«برگرد» باید **پیش از** `Post` و بی صبر باشد.");

        //  ⛔ و «برگرد» فقط برمی‌گرداند — اگر پارک هم می‌کرد، جدولی که همین
        //     حالا نشان داده می‌شود با مقدارِ کهنهٔ `IsEffectivelyVisible`
        //     پارک می‌شد.
        var shown = code[code.IndexOf("private void OnPagesShown()", System.StringComparison.Ordinal)..];
        shown = shown[..shown.IndexOf("\n    }", System.StringComparison.Ordinal)];
        Assert.Contains("SectionShown()", shown);
        Assert.Contains("_parkedAway = false;", shown);
        Assert.DoesNotContain("_parkedAway = true;", shown);
        Assert.DoesNotContain("IsEffectivelyVisible", shown);

        //  و هر دو رویداد در هر دو سرِ چرخهٔ عمر بسته و باز می‌شوند
        Assert.Contains("PagesShown += OnPagesShown;", code);
        Assert.Contains("PagesShown -= OnPagesShown;", code);
    }
}
