using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using PumpYaqobi.App.Controls;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// نقشهٔ مخزن (‎TankGauge‎) — به‌جای نوارِ عمودیِ پرشدگی (۱۴۰۵/۰۷/۱۳).
///
/// خواستهٔ صاحب ریپو با تصویر: مخزنِ افقی با تیل روی آب، پروب، دو شناور، پمپِ
/// غوطه‌ور و درصدِ پرشدگی؛ دو جملهٔ حساب‌شده زیرِ دو کادرِ تایپ؛ و دکمه‌های
/// مخزن در سربرگِ «خلاصه پول‌ها». سه چیز این‌جا قفل می‌شود:
///   ۱) حسابِ سطح‌ها خالص و درست است (تنها عددِ حقیقیِ نقشه).
///   ۲) هر تکهٔ تصویرِ مرجع در کنترل هست و نوارِ قدیمی نیست.
///   ۳) چیدمانِ تازه همان است که با کادرِ زرد و آبی خواسته شد.
/// </summary>
public class TankGaugeTests
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

    private static string NoComments(string xml) => Regex.Replace(xml, "<!--.*?-->", "", RegexOptions.Singleline);

    private static string Xaml() => NoComments(Read("PumpYaqobi.App", "Views", "Sections", "StorageSectionView.axaml"));
    private static string Gauge() => Read("PumpYaqobi.App", "Controls", "TankGauge.cs");

    // ══ ۱) سطح‌ها ═══════════════════════════════════════════════════════════

    [Fact]
    public void Sath_Ha_RoyeMehvareZarfiat_Mineshinand()
    {
        //  سقفِ داخلی ۵۱، کف ۲۱۱ — همان بومِ طراحی
        var full = TankGauge.LevelsOf(51, 211, 100, 12, 10);
        Assert.Equal(211 - 0.12 * 160, full.WaterTop, 6);
        Assert.Equal(51, full.OilTop, 6);                         // ۱۰۰٪ ⇒ تیل تا سقف

        var empty = TankGauge.LevelsOf(51, 211, 0, 12, 10);
        Assert.Equal(empty.WaterTop, empty.OilTop, 6);            // ۰٪ ⇒ هیچ تیلی روی آب نیست

        var half = TankGauge.LevelsOf(51, 211, 50, 12, 10);
        Assert.Equal((half.WaterTop + 51) / 2, half.OilTop, 6);   // ۵۰٪ ⇒ وسطِ آب و سقف

        //  آستانه روی همان محور: بینِ سطحِ آب و سقف، هرگز داخلِ نوارِ آب
        Assert.Equal(full.WaterTop - 0.10 * (full.WaterTop - 51), full.ThresholdY, 6);
        Assert.True(full.ThresholdY <= full.WaterTop && full.ThresholdY >= 51);

        //  آستانهٔ ۱۰٪ و تیلِ ۱۰٪ یک خط‌اند — خطِ هشدار دقیقاً روی سطحِ تیل
        var ten = TankGauge.LevelsOf(51, 211, 10, 12, 10);
        Assert.Equal(ten.OilTop, ten.ThresholdY, 6);
    }

    [Fact]
    public void AdadeKharab_Ya_BiroonAzBaze_NaghsheRa_Nemishekanad()
    {
        var over = TankGauge.LevelsOf(51, 211, 150, 12, 0);
        Assert.Equal(51, over.OilTop, 6);                          // بالای صد ⇒ صد
        var under = TankGauge.LevelsOf(51, 211, -5, 12, 0);
        Assert.Equal(under.WaterTop, under.OilTop, 6);            // زیرِ صفر ⇒ صفر
        var nan = TankGauge.LevelsOf(51, 211, double.NaN, double.PositiveInfinity, double.NaN);
        Assert.Equal(211, nan.WaterTop, 6);                        // آبِ ناجور ⇒ بی آب
        Assert.Equal(nan.WaterTop, nan.OilTop, 6);
        //  نوارِ آب سقف دارد تا جای تیل را نگیرد
        var flood = TankGauge.LevelsOf(51, 211, 100, 90, 0);
        Assert.Equal(211 - 0.40 * 160, flood.WaterTop, 6);
    }

    [Fact]
    public void BiAndazeyeDastgah_HichAbi_Nist()
    {
        //  خواستهٔ صاحب ریپو (۱۴۰۵/۰۷/۱۳): «نمی‌دونم اصلاً توی مخزن آبی هست یا
        //  نه» ⇒ تا دستگاهِ فیزیکی عدد نداده، آب نیست و تیل از کف شمرده می‌شود.
        var none = TankGauge.LevelsOf(51, 211, 50, null, 10);
        Assert.Equal(211, none.WaterTop, 6);
        Assert.Equal(131, none.OilTop, 6);                          // ۵۰٪ ⇒ وسطِ کف و سقف
        Assert.False(TankGauge.HasWaterReading(null));
        Assert.False(TankGauge.HasWaterReading(double.NaN));
        Assert.True(TankGauge.HasWaterReading(0));                  // «صفر آب» اندازه است، «نمی‌دانیم» نه
        Assert.Null(new TankGauge().WaterPercent);                  // پیش‌فرض: اندازه‌ای نیست

        //  و هر سه تکهٔ آب پشتِ همان یک شرط‌اند
        var src = Gauge();
        Assert.Contains("if (HasWater && lv.WaterTop < Inner.Bottom - 0.5)", src);
        Assert.Contains("if (HasWater)\n            Centered(ctx, WaterFloatLabel", src.Replace("\r\n", "\n"));
        Assert.Matches(@"if \(HasWater\)\s*\{\s*ctx\.DrawEllipse\(ball, ballEdge, new Point\(FloatX, waterBall\)", src);
        //  ⛔ هیچ عددِ حدسی به آب داده نمی‌شود — نه پیش‌فرض، نه در صفحه
        Assert.DoesNotContain("nameof(WaterPercent), 12", src);
        Assert.DoesNotContain("WaterPercent=", Xaml());
    }

    // ══ ۲) خودِ نقشه ═══════════════════════════════════════════════════════

    [Fact]
    public void HameyeTekkehayeTasvireMarja_DarNaghshe_Hastand()
    {
        var src = Gauge();
        foreach (var part in new[] { "پروب", "شناورِ تیل", "پمپِ غوطه‌ور", "شناورِ آب", "میزان پرشدگی" })
            Assert.Contains("\"" + part + "\"", src);
        //  تیل نارنجی روی آبِ آبی — دو رنگِ خودِ نقشه، نه رنگِ تم
        Assert.Contains("DrawFluids", src);
        Assert.Contains("#f28c1e", src);
        Assert.Contains("#1f6fd6", src);
        //  پمپِ غوطه‌ور سرخ، با میلهٔ سیاه
        Assert.Contains("#e11d48", src);
        Assert.Contains("#1f2937", src);
        //  خطِ آستانه نقطه‌چین است
        Assert.Contains("DrawThreshold", src);
        Assert.Contains("new DashStyle(", src);
        //  ⛔ چپ‌به‌راست — وگرنه در پنجرهٔ راست‌به‌چپ کلِ نقشه آینه می‌شد
        Assert.Contains("FlowDirection = FlowDirection.LeftToRight", src);
        //  و آب صریح «فقط از دستگاه» خوانده شده، نه نقشه و نه حدس
        Assert.Contains("آب تا دستگاهِ واقعی اندازه نداده دیده نمی‌شود", src);
    }

    [Fact]
    public void NoshteyeVasat_RoyeTil_Sefid_Va_RoyeBadaneyeKhali_RangeTam()
    {
        //  با مخزنِ کم، سفید روی سفید می‌شد — پس رنگ از روی سطحِ تیل انتخاب می‌شود
        var src = Gauge();
        Assert.Contains("lv.OilTop <= pctTop ? Brushes.White : text", src);
        Assert.Contains("lv.OilTop <= capTop ? Brushes.White : text", src);
    }

    // ══ ۳) چیدمان ═══════════════════════════════════════════════════════════

    [Fact]
    public void NavareAmoodi_Raft_Va_NaghsheJayash_Neshast()
    {
        var xaml = Xaml();
        Assert.DoesNotContain("PercentToHeightConverter", xaml);
        Assert.DoesNotContain("{Binding FillText}\" HorizontalAlignment=\"Center\"", xaml);
        var tag = Regex.Match(xaml, "<c:TankGauge[^>]*/>", RegexOptions.Singleline).Value;
        Assert.NotEqual("", tag);
        foreach (var bind in new[]
                 {
                     "FillPercent=\"{Binding FillPercent}\"", "FillText=\"{Binding FillText}\"",
                     "ThresholdPercent=\"{Binding ThresholdPercent}\"", "CaptionText=\"{Binding ThresholdText}\"",
                 })
            Assert.Contains(bind, tag);
        //  بدنه و نوشته از تم؛ هیچ رنگِ خامی روی خودِ تگ نیست
        foreach (var brush in new[] { "BodyBrush", "BodyDeepBrush", "RimBrush", "LabelBrush", "TextBrush", "ThresholdBrush" })
            Assert.Matches(brush + "=\"\\{DynamicResource Pump\\.[A-Za-z]+\\}\"", tag);
        Assert.DoesNotContain("#", tag);

        //  ویومدل آستانه را روی همان محورِ ظرفیت می‌دهد و درصد یک رقمِ اعشار دارد
        var vm = Read("PumpYaqobi.App", "ViewModels", "Sections", "StorageSectionViewModel.cs");
        Assert.Contains("ThresholdPercent = cap > 0m ? (double)Math.Min(100m, Math.Max(0m, threshold / cap * 100m)) : 0;", vm);
        Assert.Contains("FillText = FillPercent.ToString(\"0.#\"", vm);
    }

    [Fact]
    public void DoJomle_ZireDoKadreTyp_Neshastand_DoRadif_DoSotoon()
    {
        var xaml = Xaml();
        var grid = Regex.Match(xaml, "<UniformGrid Rows=\"2\" Columns=\"2\" Classes=\"stats\"[^>]*>(.*?)</UniformGrid>",
                               RegexOptions.Singleline);
        Assert.True(grid.Success, "شبکهٔ ۲×۲ی زیرِ عددِ مخزن نیست");
        var g = grid.Groups[1].Value;
        //  ترتیبِ چیدن: ردیفِ اول دو کادرِ تایپ، ردیفِ دوم دو جمله
        var order = new[] { "ظرفیت مخزن (لیتر)", "آستانهٔ هشدار (لیتر)", "جمله ورودی", "جمله فروش" }
                    .Select(t => g.IndexOf("Text=\"" + t + "\"", StringComparison.Ordinal)).ToArray();
        Assert.All(order, i => Assert.True(i >= 0));
        Assert.True(order.SequenceEqual(order.OrderBy(i => i)), "ترتیبِ چهار کادر عوض شده");
        //  همان دو کادرِ خواندنی و دو کادرِ تایپ — نه بیشتر، نه کمتر
        Assert.Equal(2, Regex.Matches(g, "<Border Classes=\"calc tankbox\"").Count);
        Assert.Equal(2, Regex.Matches(g, "<TextBox Classes=\"tankbox\"").Count);
        //  ⛔ هیچ ردیفِ چهارتایی‌ای در کارتِ مخزن برنگردد (سه کادرِ «خلاصه پول‌ها»
        //  ردیفِ خودشان را دارند و مالِ این بند نیستند)
        var tankCard = xaml.Substring(0, xaml.IndexOf("{Binding MoneyTitle}", StringComparison.Ordinal));
        Assert.DoesNotContain("<UniformGrid Rows=\"1\"", tankCard);
    }

    [Fact]
    public void DokmehayeMakhzan_DarSarbarge_KholaseyePoolha_Hastand()
    {
        var xaml = Xaml();
        //  سربرگِ «خلاصه پول‌ها» همان بلوکی است که ‎MoneyTitle‎ را دارد
        var head = Regex.Matches(xaml, "<Border Classes=\"card-head\">(.*?)</Border>", RegexOptions.Singleline)
                        .Select(m => m.Groups[1].Value)
                        .FirstOrDefault(b => b.Contains("{Binding MoneyTitle}"));
        Assert.NotNull(head);
        foreach (var cmd in new[] { "OpenDipCommand", "CloseDipCommand", "PdfCommand", "OpenBuyCommand" })
            Assert.Contains(cmd, head);
        //  دکمه‌ها در ستونِ آغازِ شبکه (سمتِ راست در راست‌به‌چپ)، و عنوان وسطِ کلِ سربرگ
        Assert.Contains("x:Name=\"TankActions\" Grid.Column=\"0\"", head);
        Assert.Contains("Width=\"{Binding #TankActions.Bounds.Width}\"", head);
        var title = Regex.Match(head!, "<TextBlock[^>]*\\{Binding MoneyTitle\\}[^>]*>").Value;
        Assert.Contains("TextAlignment=\"Center\"", title);
        Assert.Contains("HorizontalAlignment=\"Stretch\"", title);

        //  و در کارتِ مخزن دیگر هیچ دکمه‌ای نیست — از نقشه تا آغازِ ردیفِ دکمه‌های سربرگِ پول‌ها
        var tank = xaml.Substring(xaml.IndexOf("<c:TankGauge", StringComparison.Ordinal));
        tank = tank.Substring(0, tank.IndexOf("x:Name=\"TankActions\"", StringComparison.Ordinal));
        Assert.DoesNotContain("OpenBuyCommand", tank);
        Assert.DoesNotContain("<Button", tank);
    }
}
