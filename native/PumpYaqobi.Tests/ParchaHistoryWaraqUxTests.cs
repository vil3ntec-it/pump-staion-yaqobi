namespace PumpYaqobi.Tests;

/// <summary>
/// ══ سه گزارشِ ۱۴۰۵/۰۷/۰۶ که «خراب درست شده بود» ═══════════════════════════
///
/// «۱) بخشِ پارچه‌ها کادرِ نامِ کارمندان گفتم طولش بزرگ باشه نه عرضش… و این
/// کادر که میاد میگه عدد کمتری وارد کردی نمی‌زاره بره کادرِ پایین‌ش، این
/// مانع نیست، اول هم گفتم… و توی خودِ همون کادر هم عدد سرخ بشه… و توی بخشِ
/// پارچه‌ها دکمه‌های چپ و راست برعکس کار می‌کنن. ۲) تاریخچه‌ها دکمهٔ برگشت به
/// همون بخش که ازش رفتم نداره. ۳) از داخلِ بخشِ ورق‌ها از تراکنش‌ها نوعِ تیل
/// رو حذف کن.»
/// </summary>
public class ParchaHistoryWaraqUxTests
{
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

    private static string Bare(string x) => System.Text.RegularExpressions.Regex.Replace(
        x, "<!--.*?-->", "", System.Text.RegularExpressions.RegexOptions.Singleline);

    private static string Parcha() => Read("PumpYaqobi.App", "Views", "Sections", "ParchaSectionView.axaml");
    private static string ParchaVm() => Read("PumpYaqobi.App", "ViewModels", "Sections", "ParchaSectionViewModel.cs");

    // ══ ۱الف) کادرِ نام: بلندتر، نه پهن‌تر ═══════════════════════════════════

    /// <summary>
    /// «طولش بزرگ باشه نه عرضش» (۱۴۰۵/۰۷/۰۶) — و از ۱۴۰۵/۰۷/۱۰ «همه را با
    /// دقت درست کن و تراز».
    ///
    /// ⚠️ <b>این دو خواسته با هم تنظیم شدند، و یکی‌شان بی‌صدا پس گرفته
    /// نشد.</b> پیش از این «بلند» یعنی ‎MinHeight="48"‎ی دستی روی همین یک
    /// کادر، و پهنایش ۲۱۰ی ثابت بود — یعنی این کادر از <b>همهٔ</b> ردیف‌های
    /// دیگر هم کوتاه‌تر بود هم باریک‌تر، و همان «از همه متفاوت‌تر»ی شد که
    /// گزارشِ بعدی را ساخت.
    ///
    /// حالا:
    ///   • <b>بلند</b> سرِ جایش است، ولی از یک جا می‌آید (‎.wfield‎) و
    ///     <b>همه</b> همان را دارند — و این سنجه می‌خواهد که واقعاً از
    ///     پیش‌فرضِ ‎TextBox‎ بلندتر باشد، نه یک عددِ ثابتِ نوشته‌شده.
    ///   • <b>پهن‌تر نشد</b> به معنای واقعی‌اش: هیچ ‎Width‎ی روی خودش ندارد،
    ///     پس از ستونِ مشترک پهنا می‌گیرد — نه بیشتر از همسایه‌هایش.
    /// ⛔ پهنای <b>ثابت</b> برنگردد؛ همان بود که کارت را ناهموار کرد.
    /// </summary>
    [Fact]
    public void TheStaffNameBoxIsTallerNotWider()
    {
        var x = Bare(Parcha());
        var i = x.IndexOf("Watermark=\"نام کارمند\"", StringComparison.Ordinal);
        Assert.True(i > 0, "کادرِ نامِ کارمند پیدا نشد");

        // ⛔ پهنای ثابتِ آن ستون رفت — همهٔ ردیف‌ها یک قالب دارند
        Assert.DoesNotContain("ColumnDefinitions=\"210", x);
        Assert.Contains("ColumnDefinitions=\"*,8,106\"", x);

        var box = x[Math.Max(0, i - 300)..Math.Min(x.Length, i + 300)];
        // بلندی از کلاسِ مشترک می‌آید، نه از عددی که این‌جا نوشته شود
        Assert.Contains("Classes=\"wfield\"", box);
        Assert.DoesNotContain("MinHeight=\"", box);
        // و هیچ پهنای صریحی روی خودِ کادر نیست
        Assert.DoesNotContain("Width=\"", box[..box.IndexOf("Watermark", StringComparison.Ordinal)]);

        // ⛔ و «بلند» واقعاً بلند است: از پیش‌فرضِ خودِ TextBox بیشتر
        var css = Bare(Read("PumpYaqobi.App", "Themes", "Controls.axaml"));
        var wfield = Num(css, "Selector=\"TextBox.wfield\"");
        var basic = Num(css, "<Style Selector=\"TextBox\">");
        Assert.True(wfield > basic, $"کادرِ فرم ({wfield}) باید از پیش‌فرض ({basic}) بلندتر باشد");
    }

    /// <summary>نخستین ‎MinHeight‎ِ پس از یک سلکتور — برای سنجشِ «بلندتر».</summary>
    private static double Num(string css, string selector)
    {
        var i = css.IndexOf(selector, StringComparison.Ordinal);
        Assert.True(i > 0, "سلکتور پیدا نشد: " + selector);
        var j = css.IndexOf("MinHeight\" Value=\"", i, StringComparison.Ordinal);
        Assert.True(j > 0, "MinHeight پیدا نشد برای " + selector);
        j += "MinHeight\" Value=\"".Length;
        return double.Parse(css[j..css.IndexOf('"', j)],
                            System.Globalization.CultureInfo.InvariantCulture);
    }

    // ══ ۱ب) هشدارِ پایه مانع نیست و چیزی را جابه‌جا نمی‌کند ═════════════════

    /// <summary>
    /// ⛔ ریشهٔ «نمی‌گذارد بروم کادرِ پایین» یک <b>جابه‌جاییِ چیدمان</b> بود:
    /// کادرِ هشدار با دکمهٔ «دیدم» داخلِ همان ستون باز می‌شد و «ختم پایه» را
    /// ~۷۰ پیکسل پایین می‌پراند. بعد یک ردیفِ همیشه‌حاضرِ ۲۲ پیکسلی شد —
    /// جابه‌جا نمی‌کرد ولی همیشه یک فاصلهٔ خالی زیرِ کادر می‌گذاشت
    /// (عکسِ صاحب ریپو، ۱۴۰۵/۰۷/۱۲). حالا ⚠️ و «✔ دیدم» <b>داخلِ خودِ کادرِ
    /// شروع</b>‌اند (‎TextBox.InnerRightContent‎)، پس نه چیزی تکان می‌خورد و نه
    /// جایی گرفته می‌شود.
    /// </summary>
    [Fact]
    public void TheLowBaseWarningNeverMovesTheNextBox()
    {
        var x = Bare(Parcha());
        // ⛔ نه ردیفِ جدا، نه کادرِ بازشو
        Assert.DoesNotContain("Grid Height=\"22\"", x);
        Assert.DoesNotContain("Border Classes=\"panel\" IsVisible=\"{Binding LowBase}\"", x);
        // ✅ داخلِ خودِ کادرِ شروع — همان کادری که ‎StartBrushKey‎ سرخش می‌کند
        var box = x.IndexOf("Text=\"{Binding Start}\"", StringComparison.Ordinal);
        Assert.True(box > 0, "کادرِ شروعِ پایه پیدا نشد");
        var inner = x.IndexOf("<TextBox.InnerRightContent>", box, StringComparison.Ordinal);
        var close = x.IndexOf("</TextBox>", box, StringComparison.Ordinal);
        Assert.True(inner > box && inner < close, "هشدار باید داخلِ خودِ کادرِ شروع باشد");
        var ack = x.IndexOf("AckLowBaseCommand", box, StringComparison.Ordinal);
        Assert.True(ack > inner && ack < close, "«✔ دیدم» باید داخلِ خودِ کادرِ شروع باشد");
    }

    /// <summary>و خودِ عدد داخلِ کادرش سرخ می‌شود — «توی خود همون کادر».</summary>
    [Fact]
    public void TheStartNumberItselfTurnsRed()
    {
        Assert.Contains("StartBrushKey", Bare(Parcha()));
        Assert.Contains("public string StartBrushKey => LowBase ? \"Pump.Danger\" : \"Pump.Text\";",
                        ParchaVm());
    }

    /// <summary>⛔ و هیچ‌وقت مانعِ ذخیره نبوده و نباید بشود.</summary>
    [Fact]
    public void TheWarningNeverBlocksTheSave()
    {
        var vm = ParchaVm();
        var i = vm.IndexOf("SaveShiftFlowAsync", StringComparison.Ordinal);
        Assert.True(i > 0);
        // پیش از فرستادنِ درخواست هیچ ‎return‎ی به‌خاطرِ هشدار نیست
        var before = vm[Math.Max(0, i - 1200)..i];
        Assert.DoesNotContain("LowBaseUnacked) return", before);
    }

    // ══ ۱ج) زنجیرهٔ پایه — اختیاری، و به ازای هر پایه ═══════════════════════

    /// <summary>
    /// «ختمِ یک عدد با شروع متفاوت باشه… بگه این مقدار بیشتر زده شده و بررسی
    /// باید بشه… و من چندین پایه دارم و می‌خوام با پایه‌ها در ارتباط باشن.»
    /// </summary>
    [Fact]
    public void TheChainCheckIsPerBaseAndOptional()
    {
        var vm = ParchaVm();
        // هر دو حال: کمتر و بیشتر
        Assert.Contains("کمتر است", vm);
        Assert.Contains("بیشتر زده شده", vm);
        // ⛔ «بیشتر» فقط با شمارهٔ پایهٔ نوشته‌شده سنجیده می‌شود
        Assert.Contains("ChainCheck && num > 0", vm);
        // و کلیدش اختیاری است و می‌نشیند
        Assert.Contains("ParchaChainCheck", vm);
        Assert.Contains("IsChecked=\"{Binding ChainCheck}\"", Bare(Parcha()));

        var st = Read("PumpYaqobi.App", "Services", "AppSettings.cs");
        Assert.Contains("public bool ParchaChainCheck { get; set; } = true;", st);
        // ⛔ فهرستِ «مقدارهای راحتی» باید با آن یکی بماند
        Assert.Contains("live.ParchaChainCheck = ParchaChainCheck;", st);
    }

    // ══ ۱د) دکمه‌های انتقالِ پایه: بی فلش ═══════════════════════════════════

    /// <summary>
    /// ⛔ <c>←</c> و <c>→</c> هر دو <c>Bidi_Mirrored</c> هستند و در متنِ
    /// راست‌به‌چپ خودِ شکل‌دهنده آینه‌شان می‌کند — پس «درست» بودنشان به رفتارِ
    /// رندرِ متن بند بود. جایشان واژه نشست. فلش را برنگردانید.
    /// </summary>
    [Fact]
    public void TheBaseTransferButtonsCarryNoArrows()
    {
        var vm = ParchaVm();
        var i = vm.IndexOf("public string PullText", StringComparison.Ordinal);
        var j = vm.IndexOf("public string PushText", StringComparison.Ordinal);
        Assert.True(i > 0 && j > i);
        var both = vm[i..(j + 120)];
        Assert.DoesNotContain("←", both);
        Assert.DoesNotContain("→", both);
        Assert.Contains("گرفتن از شب", both);
        Assert.Contains("فرستادن به شب", both);
    }

    // ══ ۲) تاریخچه‌ها: برگشت به همان بخش ════════════════════════════════════

    /// <summary>
    /// «دکمهٔ برگشت به همون بخش که ازش رفتم نداره و می‌ره توی بخشِ تاریخچه‌ها.»
    /// ⚠️ و روی <b>هر دو</b> صفحه — چون «برگشت به تاریخچه‌ها» آدم را در فهرستِ
    /// کارت‌ها می‌گذارد و راهِ برگشت باید آن‌جا هم باشد.
    /// </summary>
    [Fact]
    public void TheHistoryPageCanGoBackToTheSectionItCameFrom()
    {
        var view = Bare(Read("PumpYaqobi.App", "Views", "Sections", "HistorySectionView.axaml"));
        Assert.Equal(2, System.Text.RegularExpressions.Regex
            .Matches(view, "BackToOriginCommand").Count);

        var vm = Read("PumpYaqobi.App", "ViewModels", "Sections", "HistorySectionViewModel.cs");
        Assert.Contains("public void SetOrigin(string id, string title)", vm);
        Assert.Contains("_host.GoSection", vm);

        // مبدأ **پیش از** رفتن ثبت می‌شود، و رفتنِ مستقیم پاکش می‌کند
        var main = Read("PumpYaqobi.App", "ViewModels", "MainViewModel.cs");
        Assert.Contains("h.SetOrigin(Current.Id, Current.Title)", main);
        Assert.Contains("hv.SetOrigin(\"\", \"\")", main);

        // ⚠️ و از همان ‎GoAsync‎ می‌رود، پس قفلِ پلن و رمزِ بخش سرِ جایشان‌اند
        var i = main.IndexOf("private async Task GoSectionAsync", StringComparison.Ordinal);
        Assert.True(i > 0);
        Assert.Contains("GoAsync(s)", main[i..Math.Min(main.Length, i + 400)]);
    }

    // ══ ۳) ورق‌ها: ستونِ «نوع تیل» رفت ══════════════════════════════════════

    /// <summary>
    /// «از داخلِ بخشِ ورق‌ها از تراکنش‌ها نوعِ تیل رو حذف کن.»
    ///
    /// ⚠️ فقط <b>ستونش</b>، نه خودِ ‎Fuel‎ی ردیف: نام که «دیزل» یا «د» داشته
    /// باشد همان لحظه رویش می‌نشیند، و جدولِ <b>پایه‌ها</b>ی همان صفحه هم از
    /// همان کپسول می‌خواند.
    /// </summary>
    [Fact]
    public void TheTransactionsGridHasNoFuelTypeColumnAnyMore()
    {
        var x = Bare(Read("PumpYaqobi.App", "Views", "Sections", "WaraqPageView.axaml"));
        Assert.DoesNotContain("Header=\"نوع تیل\"", x);
        // ولی ستونِ «واحد» — که تصمیمِ دفتر است — سرِ جایش است، در هر دو جدول
        Assert.Equal(2, System.Text.RegularExpressions.Regex
            .Matches(x, "Header=\"واحد\"").Count);
    }

    /// <summary>
    /// و نام که عوض شد، سوخت و واحد خودشان را پیدا می‌کنند — با همان
    /// تطبیق‌کنندهٔ پست، نه یک قاعدهٔ دوم.
    /// </summary>
    [Fact]
    public void TypingANamePicksTheFuelAndTheUnitByItself()
    {
        var vm = Read("PumpYaqobi.App", "ViewModels", "Sections", "WaraqSectionViewModel.cs");
        Assert.Contains("_owner.AutoFromName(this)", vm);
        Assert.Contains("PostingService.MentionsFuel(text)", vm);
        Assert.Contains("PostingService.DetectFuelType(text)", vm);
        Assert.Contains("PostingService.FindAccountForText(_unitPeople", vm);
        Assert.Contains("PostingService.UnitForAccount(", vm);

        // ⛔ و با ترمزِ ‎Version‎ — وگرنه یک پرس‌وجو به ازای هر کلید
        Assert.Contains("PumpDbContext.Version", vm);
        Assert.Contains("_unitsVersion == v", vm);

        // ⛔ و هیچ ردیفی برای این کار خوانده نمی‌شود
        var svc = Read("PumpYaqobi.Services", "Data", "DebtorService.cs");
        var i = svc.IndexOf("AccountUnitsAsync", StringComparison.Ordinal);
        Assert.True(i > 0);
        var body = svc[i..Math.Min(svc.Length, i + 2600)];
        Assert.Contains("Distinct()", body);
        Assert.DoesNotContain("LoadFullAsync", body);
    }
}
