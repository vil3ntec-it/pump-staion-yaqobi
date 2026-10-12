using PumpYaqobi.App.ViewModels.Sections;
using PumpYaqobi.Application.Services;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ «تیل امانت» مو‌به‌مو مثلِ سایت (نسخه 3.1.272) ══════════════════════════════
/// خواستهٔ صاحب ریپو: «هر چیزی که در سایت در این بخش بود، در برنامه هم همان‌طور
/// باشد.» این آزمون‌ها فرمول‌هایی را که تازه از ‎archive/old-site/index.html‎
/// رونویسی شدند (‎amSuggest‎ · ‎amFeeAdvice‎ · ‎_amWhatIfPaint‎ · ‎_amPaint‎ ·
/// ‎row.myPct‎ · ‎_amSortedRows‎ · ‎_amAccMatch‎ · ‎_amFmt‎) با عددهای دستی قفل می‌کنند،
/// و چیدمانِ صفحه را از روی سورس.
/// </summary>
public class AmanatWebParityTests
{
    private static readonly AmanatService Calc = new();
    private static readonly AmanatSettings S = AmanatSettings.Default;

    // ── فیصدیِ ردیف (‎row.myPct‎) ─────────────────────────────────────────────

    [Fact]
    public void FeesadiyeRadif_BarFeesadiyeHesab_Mi_Chirbad()
    {
        var acc = new AmanatAccount { Fuel = FuelType.Petrol, MyPct = 3m };
        var row = new AmanatRow { Liters = 1000m, Days = 30m, Temp = 20m, MyPct = 5m };
        var c = Calc.RowCalc(row, acc, S, 0m);
        Assert.Equal(5m, c.MyPct);
        Assert.True(c.RowOwnPct);
        Assert.Equal(50m, c.TargetL);
        Assert.Equal(1000m - 50m, c.Rest);

        row.MyPct = null;   // خالی ⇐ ارث از حساب (با «↑»)
        c = Calc.RowCalc(row, acc, S, 0m);
        Assert.Equal(3m, c.MyPct);
        Assert.False(c.RowOwnPct);
        Assert.Equal(30m, c.TargetL);
    }

    [Fact]
    public void JameHesab_SahmRaAzFeesadiyeHarRadifMigirad()
    {
        //  ‎t.share‎ جمعِ ردیف‌به‌ردیف است؛ ‎t.targetL‎ از فیصدیِ خودِ حساب
        var acc = new AmanatAccount { Fuel = FuelType.Petrol, MyPct = 2m };
        acc.Rows.Add(new AmanatRow { Liters = 1000m, Days = 0m, MyPct = 4m });
        acc.Rows.Add(new AmanatRow { Liters = 1000m, Days = 0m });
        var t = Calc.AccountCalc(acc, S, _ => 0m);
        Assert.Equal(60m, t.Share);
        Assert.Equal(40m, t.TargetL);
    }

    [Fact]
    public void RoozeKhali_Khodkar_RoozeSefr_Dasti()
    {
        var acc = new AmanatAccount { Fuel = FuelType.Petrol };
        var c = Calc.RowCalc(new AmanatRow { Liters = 1000m }, acc, S, 12m);
        Assert.False(c.Manual);
        Assert.Equal(12m, c.Days);
        Assert.Equal(12m, c.AutoDays);
        c = Calc.RowCalc(new AmanatRow { Liters = 1000m, Days = 0m }, acc, S, 12m);
        Assert.True(c.Manual);
        Assert.Equal(0m, c.Days);
        Assert.Equal(0m, c.Loss);
    }

    [Fact]
    public void RadifeBaste_FeesadiyeHesabRooyashGhofl_Mishavad()
    {
        var acc = new AmanatAccount { MyPct = 3m };
        var closed = new AmanatRow { State = AmanatRowState.Closed };
        var own = new AmanatRow { State = AmanatRowState.Closed, MyPct = 7m };
        var open = new AmanatRow { State = AmanatRowState.Open };
        acc.Rows.AddRange(new[] { closed, own, open });
        var changed = AmanatService.LockClosedRowPcts(acc);
        Assert.Same(closed, Assert.Single(changed));
        Assert.Equal(3m, closed.MyPct);
        Assert.Equal(7m, own.MyPct);
        Assert.Null(open.MyPct);
        Assert.Empty(AmanatService.LockClosedRowPcts(new AmanatAccount()));
    }

    // ── پیشنهاد، ماشین‌حساب و برآوردِ دستی ──────────────────────────────────

    [Fact]
    public void AmSuggest_SarbeSar_Va_Pishnahad()
    {
        //  ۱۰۰۰ لیتر، ۳۰ روز، ۲۰ درجه ⇐ بخار = ۱۰۰۰ × ۰٫۰۲٪ = ۰٫۲ لیتر = ۰٫۰۲٪
        var sg = Calc.Suggest(1000m, 30m, 20m, FuelType.Petrol, null, S, null);
        Assert.Equal(0.2m, sg.Loss);
        Assert.Equal(0.02m, sg.EvenPct);
        Assert.Equal(2m, sg.Want);              // «سودِ هدف»ِ پیش‌فرض
        Assert.Equal(2.1m, sg.Pct);             // ‎ceil(20.2)/10‎
        sg = Calc.Suggest(1000m, 30m, 20m, FuelType.Petrol, null, S, 3m);
        Assert.Equal(3.1m, sg.Pct);
        Assert.Equal(30.8m, sg.NetAt(3.1m));
        //  اندازه‌گیری٪ روی سربه‌سر سوار می‌شود
        sg = Calc.Suggest(1000m, 30m, 20m, FuelType.Petrol, null, S with { HandlingPct = 0.5m }, 0m);
        Assert.Equal(0.52m, sg.EvenPct);
        Assert.Equal(0.6m, sg.Pct);
    }

    [Fact]
    public void AmFeeAdvice_HashiyeEtminan()
    {
        var a = Calc.FeeAdvice(1000m, 30m, 20m, FuelType.Petrol, null, S);
        Assert.Equal(0.02m, a.NeedPct);
        Assert.Equal(0.025m, a.SafePct);     // ‎need × (1 + 25/100)‎
        Assert.Null(a.Diff);
        a = Calc.FeeAdvice(1000m, 30m, 20m, FuelType.Petrol, 1m, S);
        Assert.Equal(10m, a.MyLiters);
        Assert.Equal(9.8m, a.Diff);
        Assert.Equal(45, AmanatService.NearestGuideDay(40m));
        Assert.Equal(30, AmanatService.NearestGuideDay(37.5m));   // برابر ⇐ نخستین
        Assert.Equal(new[] { 7, 15, 30, 45, 60, 90, 120, 180 }, AmanatService.GuideDays);
    }

    [Fact]
    public void BarawordeDasti_HamanAdadhayeSite()
    {
        //  ۱۰۰٬۰۰۰ لیتر، ۳۰ روز، ۳۰ درجه (ضریب ۲)، فیصدیِ من ۳
        var w = Calc.WhatIf(100000m, 30m, 30m, FuelType.Petrol, 3m, null, S);
        Assert.Equal(40m, w.Loss);
        Assert.Equal(0.04m, w.LossPct);
        Assert.Equal(3000m, w.Share);
        Assert.Equal(2960m, w.Net);
        Assert.Equal(97000m, w.Rest);
        Assert.Equal(3.04m, w.NeedPct);
        Assert.Equal(3.1m, w.AskPct);
        Assert.Equal(3100m, w.AskL);
        Assert.Equal(3060m, w.NetIfAsk);
        //  «اگر رویِ‌هم این‌قدر بماند» — روزِ کل، نه «+چند روز»
        Assert.Equal(new[] { 30m, 60m, 90m, 120m, 210m }, w.Future.Select(f => f.Days));
        Assert.True(w.Future[0].IsNow);
        Assert.Equal(80m, w.Future[1].Loss);
        Assert.Equal(3.1m, w.Future[1].SuggestPct);
        Assert.Equal(2920m, w.Future[1].Net);

        //  بی «فیصدیِ من»: سهمی نیست، الباقی همهٔ تیل است، پیشنهاد از «سودِ هدف»
        w = Calc.WhatIf(100000m, 30m, 30m, FuelType.Petrol, null, null, S);
        Assert.Null(w.Share);
        Assert.Equal(100000m, w.Rest);
        Assert.Equal(2.1m, w.Suggest.Pct);
        Assert.Null(w.Future[0].Net);
    }

    // ── ترتیب، جست‌وجو، خلاصه و هشدارها ─────────────────────────────────────

    [Fact]
    public void Radifha_BeTartibeTarikh_BiTarikhAkhar()
    {
        var a = new AmanatRow { DateKey = 14050310 };
        var none = new AmanatRow { DateKey = 0 };
        var b = new AmanatRow { DateKey = 14050101 };
        var b2 = new AmanatRow { DateKey = 14050101 };
        var sorted = AmanatService.SortedRows(new[] { a, none, b, b2 });
        Assert.Equal(new[] { b, b2, a, none }, sorted);
    }

    [Fact]
    public void JostoJoo_NameRadifVaBeHesabeh_Ham()
    {
        var acc = new AmanatAccount { Name = "شرکت", Fuel = FuelType.Diesel, Note = "" };
        acc.Rows.Add(new AmanatRow { Name = "احمد", ToAccount = "علي" });   // «ي»ِ عربی
        Assert.True(AmanatService.Matches(acc, "all", "علی"));
        Assert.True(AmanatService.Matches(acc, "diesel", "احمد"));
        Assert.False(AmanatService.Matches(acc, "petrol", ""));
        Assert.False(AmanatService.Matches(acc, "all", "محمود"));
        Assert.Equal("شرکت (دیزل)", AmanatService.AccLabel(acc));
        Assert.Equal("حسابِ بی‌نام (پطرول)", AmanatService.AccLabel(new AmanatAccount()));
    }

    [Fact]
    public void KhulasaVaHoshdarha()
    {
        var p = new AmanatAccount { Name = "الف", Fuel = FuelType.Petrol, MyPct = 2m };
        p.Rows.Add(new AmanatRow { SortIndex = 0, Liters = 1000m, Taken = 1500m, Days = 30m, Temp = 20m, DateKey = 14050101 });
        p.Rows.Add(new AmanatRow { SortIndex = 1, Liters = 1000m, Days = 3000m, Temp = 20m });   // بی تاریخ ولی روزِ دستی
        var d = new AmanatAccount { Name = "ب", Fuel = FuelType.Diesel };
        d.Rows.Add(new AmanatRow { Liters = 500m, Temp = 99m, DateKey = 0 });
        var o = Calc.Overview(new[] { p, d }, S, (_, _) => 0m, _ => true);

        Assert.Equal(1, o.Petrol.Count);
        Assert.Equal(2000m, o.Petrol.Liters);
        Assert.Equal(1500m, o.Petrol.Taken);
        Assert.Equal(40m, o.Petrol.Share);
        Assert.Equal(1, o.Diesel.Count);
        Assert.Equal(500m, o.Diesel.Liters);

        Assert.Contains("الف (پطرول) — ردیفِ 1: برده شده از رسید تیل بیشتر است.", o.Errors);
        Assert.Contains(o.Warnings, w => w.StartsWith("الف (پطرول) — ردیفِ 2: تبخیرِ 2٪", StringComparison.Ordinal));
        Assert.Contains("ب (دیزل) — ردیفِ 1: تاریخ ثبت نشده، پس مدت زمان خودکار شمرده نمی‌شود.", o.Warnings);
        Assert.Contains("ب (دیزل) — ردیفِ 1: درجه گرمای واردشده غیرعادی است.", o.Warnings);

        //  هشدار فقط برای حساب‌هایی که با فیلتر جورند؛ خلاصه برای همه
        var only = Calc.Overview(new[] { p, d }, S, (_, _) => 0m, a => a.Fuel == FuelType.Diesel);
        Assert.Empty(only.Errors);
        Assert.Equal(2000m, only.Petrol.Liters);

        var lines = AmanatSectionViewModel.SumLines(o.Petrol);
        Assert.Equal(new[] { "رسید کل", "برده شده", "بخارِ تخمینی", "سهمِ شما (فیصدی)", "باقی تیلِ مشتری",
                             "درصد بخار", "یعنی روی هر فیصدی" }, lines.Select(l => l.Label));
        Assert.Equal("2,000.00 لیتر", lines[0].Value);
    }

    [Fact]
    public void AmFmt_HamanToLocaleString()
    {
        Assert.Equal("0.025", AmanatService.Fmt(0.0251m, 3));
        Assert.Equal("0.02", AmanatService.Fmt(0.020m, 3));
        Assert.Equal("1,235", AmanatService.Fmt(1234.5m, 0));
        Assert.Equal("2.00", AmanatService.Fmt2(2m));
        Assert.Equal("1,234.57", AmanatService.Fmt2(1234.565m));
        Assert.Equal("0", AmanatService.Fmt(-0.0001m, 2));
        Assert.Equal("0.00", AmanatService.Fmt2(-0.001m));
        Assert.Equal(3.6m, AmanatService.CeilTenth(3.55m));
        Assert.Equal(3m, AmanatService.CeilTenth(3m));
    }

    [Fact]
    public void Kart_HamanMatneSite()
    {
        var acc = new AmanatAccount { Name = "  ", Fuel = FuelType.Petrol, MyPct = 3m };
        var t = Calc.AccountCalc(acc, S, _ => 0m);
        var card = new AmanatCardViewModel(acc, t, 1);
        Assert.Equal("بی‌نام", card.Name);
        Assert.Equal("➕ هنوز تیلی ثبت نشده", card.State);
        Assert.Equal("0.00", card.RestText);

        acc.Rows.Add(new AmanatRow { Liters = 100000m, Days = 30m, Temp = 30m });
        t = Calc.AccountCalc(acc, S, _ => 0m);
        card = new AmanatCardViewModel(acc, t, 2);
        Assert.Equal("🔸 40.00 لیتر از سهمتان می‌رود", card.State);
        Assert.Equal("فیصدیِ شما 3٪ → بگیرید 3.1٪", card.PctText);
        Assert.Equal("⛽ 100,000", card.LitersText);
        Assert.Equal("97,000.00", card.RestText);
    }

    [Fact]
    public void JadvaleGarma_BarayeSookhteHesab()
    {
        var rows = AmanatSettingsEditorViewModel.TempTable(Calc, S, FuelType.Diesel);
        Assert.Equal(10, rows.Count);
        //  دیزل: ۰٫۰۲ × ۰٫۰۲ = ۰٫۰۰۰۴٪ در ماه ⇐ سه رقم ⇐ «0»
        Assert.Equal("0٪", rows.Single(r => r.TempText == "20°C").MonthText);
        Assert.Contains("دیزل", AmanatSettingsEditorViewModel.TempTableNote(S, FuelType.Diesel));
        Assert.Equal(10, AmanatSettingsEditorViewModel.Sources.Count);   // ‎AM_SOURCES‎ — هر ده منبع
    }

    // ── چیدمانِ صفحه — از روی سورس ───────────────────────────────────────────

    private static string Root()
    {
        var root = AppContext.BaseDirectory;
        while (root is not null && !File.Exists(Path.Combine(root, "PumpYaqobi.sln"))) root = Path.GetDirectoryName(root);
        return root!;
    }

    private static string View() =>
        System.Text.RegularExpressions.Regex.Replace(
            SrcText.Read(Path.Combine(Root(), "PumpYaqobi.App", "Views", "Sections", "AmanatSectionView.axaml")),
            "<!--.*?-->", "", System.Text.RegularExpressions.RegexOptions.Singleline);

    [Theory]
    // صفحهٔ اصلی: چهار دکمهٔ سربرگ + جست‌وجو + چیپِ سوخت
    [InlineData("🕘 تاریخچه")]
    [InlineData("📊 خلاصه و ابزارها")]
    [InlineData("📄 PDF")]
    [InlineData("➕ حساب جدید")]
    [InlineData("🔍 جست‌وجوی نام حساب…")]
    [InlineData("{Binding FuelChipText}")]
    // خلاصه و ابزارها
    [InlineData("📊 خلاصه و ابزارهای تیل امانت")]
    [InlineData("{Binding PetrolSum}")]
    [InlineData("{Binding DieselSum}")]
    [InlineData("{Binding Errors}")]
    [InlineData("{Binding Warnings}")]
    [InlineData("vm:AmanatSectionViewModel.Disclaimer")]
    [InlineData("🧮 ماشین‌حسابِ فیصدی — برای تیلی که هنوز ثبت نشده")]
    [InlineData("📋 جدولِ راهنما — بخار با مدت کم و زیاد می‌شود")]
    [InlineData("⚙️ تنظیمات مدیر")]
    // صفحهٔ حساب
    [InlineData("نام حساب / صاحب تیل")]
    [InlineData("{Binding FuelIndex}")]
    [InlineData("٪ فیصدی‌ها")]
    [InlineData("سهمِ من (لیتر)")]
    [InlineData("🧾 برآوردِ دستی — قبول کنم؟ چند فیصد بگیرم؟")]
    [InlineData("🎯 سودِ هدف")]
    [InlineData("💰 نرخ هر لیتر")]
    [InlineData("🧮 تحلیل — چند فیصد بگیرم که ضرر نکنم؟")]
    [InlineData("📏 اندازه‌گیری")]
    [InlineData("🌡️ در هر گرما چقدر تبخیر می‌شود؟")]
    [InlineData("🗑️ بله، این حساب پاک شود")]
    // سربرگِ یک‌خطیِ جدول (۱۴۰۵/۰۷/۲۰) — همان شانزده ستون، نامِ کوتاهِ ‎AmanatColumns.Short‎
    [InlineData("Header=\"تاریخ\"")]
    [InlineData("Header=\"نام\"")]
    [InlineData("Header=\"به حسابِ\"")]
    [InlineData("Header=\"رسید (لیتر)\"")]
    [InlineData("Header=\"برده شده (لیتر)\"")]
    [InlineData("Header=\"روز\"")]
    [InlineData("Header=\"گرما °C\"")]
    [InlineData("Header=\"بخار (لیتر)\"")]
    [InlineData("Header=\"فیصدیِ من\"")]
    [InlineData("Header=\"فیصدیِ بخار\"")]
    [InlineData("Header=\"فیصدیِ لازم\"")]
    [InlineData("Header=\"سهمیهٔ من (لیتر)\"")]
    [InlineData("Header=\"به من می‌رسد (لیتر)\"")]
    [InlineData("Header=\"الباقیِ طرف (لیتر)\"")]
    [InlineData("Header=\"موجودیِ واقعی\"")]
    [InlineData("Header=\"اختلاف\"")]
    [InlineData("ChipTextPath=\"ChipText\"")]
    [InlineData("HintPath=\"DaysHint\"")]
    [InlineData("HintPath=\"TempHint\"")]
    [InlineData("HintPath=\"MyPctHint\"")]
    public void SafheHamanChizhayeSiteRaDarad(string want) => Assert.Contains(want, View());

    [Fact]
    public void ChizhayiKeSiteBardasht_DarBarnameHamNist()
    {
        var v = View();
        //  ستونِ باز/بسته، و خانهٔ «فیصدیِ من»ِ حساب در سربرگ — سایت هر دو را برداشت
        Assert.DoesNotContain("Binding IsClosed", v);
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(v, "MyPctText"));   // فقط ستونِ جدول
        //  «حذفِ حساب» کنارِ دکمه‌های هر روزه نیست — فقط در کشوی ته صفحه
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(v, "DeleteAccountCommand"));
        //  جمع‌های سربرگِ جدول با نامِ همان ستون‌ها
        foreach (var h in new[] { AmanatAccountViewModel.HLiters, AmanatAccountViewModel.HTaken, AmanatAccountViewModel.HLoss,
                                  AmanatAccountViewModel.HLossPct, AmanatAccountViewModel.HNeed, AmanatAccountViewModel.HShare,
                                  AmanatAccountViewModel.HNet, AmanatAccountViewModel.HRest,
                                  AmanatAccountViewModel.HActual, AmanatAccountViewModel.HDiff })
            Assert.Contains("Header=\"" + h.Replace("\n", "&#10;") + "\"", v);
    }
}
