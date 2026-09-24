using Avalonia.Media;
using PumpYaqobi.App.Themes;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ «همه نوشته‌ها قابل خوندن باشن» — با عدد، نه با چشم ══════════════════════
///
/// خواستهٔ صریحِ صاحب ریپو (۱۴۰۵/۰۷/۱۲): «دارک مود رو از اول درست کن، ریمیک
/// با تفاوتِ ۱۰۰… یک تم جدید بده که همه نوشته‌ها قابل خوندن باشن و بهتر.»
///
/// ⛔ و ریشهٔ ماندگارش این بود که **هیچ سنجه‌ای خوانایی را نمی‌سنجید**.
/// ‎LookAudit‎ فقط سطح‌ها را می‌دید (پلهٔ کارت از بوم، دیده شدنِ خطِ لبه)، پس
/// یک پالت می‌توانست کارت‌های کاملاً واضح داشته باشد و نوشتهٔ کم‌رنگش محو
/// باشد و همه‌جا سبز بماند. اندازه‌گیری نشان داد هر دو تم زیرِ آستانه جفت
/// داشتند: تیره ۵٫۱۵× و **روشن ۴٫۰۸×**.
///
/// ⚠️ این آزمون عمداً در ‎dotnet test‎ است، نه فقط در سنجهٔ ‎look‎: پالت یک
/// مقدارِ خالص است و برای سنجیدنش نه پنجره لازم است نه آوالونیا، پس باید در
/// همان مجموعه‌ای بدود که با هر PR می‌دود.
/// </summary>
public class ThemeReadabilityTests
{
    /// <summary>WCAG 2.1 AA برای نوشتهٔ معمولی.</summary>
    private const double MinText = 4.5;

    /// <summary>WCAG 2.1 AA برای نوشتهٔ درشت و پررنگ (دکمهٔ اصلی و نوارِ سرِ جدول).</summary>
    private const double MinBig = 3.0;

    /// <summary>کم‌ترین دیده شدنِ خطِ لبه روی سطحی که دورش کشیده می‌شود.</summary>
    private const double MinRim = 1.18;

    private static double Lin(byte v)
    {
        var c = v / 255.0;
        return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
    }

    private static double Luma(Color c) => 0.2126 * Lin(c.R) + 0.7152 * Lin(c.G) + 0.0722 * Lin(c.B);

    private static double Ratio(Color a, Color b)
    {
        double x = Luma(a), y = Luma(b);
        return (Math.Max(x, y) + 0.05) / (Math.Min(x, y) + 0.05);
    }

    /// <summary>
    /// هر نوشته روی هر سطحی که واقعاً زیرش می‌نشیند.
    /// ⛔ جفتی را از این فهرست برندارید تا سبز شود — پالت را درست کنید.
    /// </summary>
    [Fact]
    public void HameyeNeveshtehha_RoyeHameyeSathha_KhandaniAnd()
    {
        var bad = new List<string>();

        foreach (var t in PumpTheme.All)
        {
            //  هر دو تم ‎Input‎ را صریح می‌گذارند. اگر روزی یکی نگذارد،
            //  ‎ThemeManager‎ خودش مخلوطش می‌کند و این آزمون دیگر همان رنگی
            //  را نمی‌سنجد که کاربر می‌بیند — پس همین‌جا قفل می‌شود.
            Assert.True(t.Input is not null, $"{t.Title}: رنگِ کادرِ تایپ صریح نیست");

            var surfaces = new (string Name, Color C)[]
            {
                ("بوم", t.Dark), ("بدنهٔ بخش", t.SectionBg), ("پنل", t.Panel),
                ("کارت", t.Card), ("کادرِ تایپ", t.Input!.Value),
                ("نوارِ سرِ جدول", t.HeadBand),
            };

            var inks = new (string Name, Color C)[]
            {
                ("نوشته", t.Text), ("کم‌رنگ", t.Muted), ("برچسب", t.Label),
            };

            foreach (var ink in inks)
                foreach (var bg in surfaces)
                {
                    var r = Ratio(ink.C, bg.C);
                    if (r < MinText)
                        bad.Add($"{t.Title}: «{ink.Name}» روی «{bg.Name}» {r:0.00}× (کمینه {MinText:0.00}×)");
                }

            //  سه جفتِ درشت — نوشتهٔ دکمهٔ اصلی، نوارِ سرِ جدول و عنوانِ سربرگ.
            //  ⛔ به این فهرست چیزی اضافه نکنید تا از آستانهٔ کامل فرار کند.
            foreach (var (name, fg, bg) in new (string, Color, Color)[]
                     {
                         ("نوشتهٔ دکمهٔ اصلی", t.OnAccent, t.Accent),
                         ("نوشتهٔ نوارِ سرِ جدول", t.OnHeadBand, t.HeadBand),
                         ("عنوانِ سربرگ", t.HeaderTitle, t.HeaderBg[1]),
                     })
            {
                var r = Ratio(fg, bg);
                if (r < MinBig)
                    bad.Add($"{t.Title}: «{name}» {r:0.00}× (کمینه {MinBig:0.00}×)");
            }
        }

        Assert.True(bad.Count == 0, string.Join("\n", bad));
    }

    /// <summary>
    /// خطِ جدول و لبهٔ کارت باید از سطحِ زیرشان جدا باشند.
    ///
    /// ⚠️ همین بند بود که زردِ کم‌نورِ تمِ تیره را گرفت: ‎#4a4322‎ روی کارتِ
    /// ‎#212127‎ فقط ۱٫۶۱× بود — خطی که «هست» ولی دیده نمی‌شود.
    /// </summary>
    [Fact]
    public void KhateLabe_AzSathe_Zirash_JodaDideMishavad()
    {
        foreach (var t in PumpTheme.All)
            foreach (var (name, surface) in new[]
                     {
                         ("کارت", t.Card), ("پنل", t.Panel), ("بدنهٔ بخش", t.SectionBg),
                     })
            {
                var r = Ratio(t.Border, surface);
                Assert.True(r >= MinRim,
                    $"{t.Title}: خطِ لبه روی «{name}» {r:0.00}× (کمینه {MinRim:0.00}×)");
            }
    }

    /// <summary>
    /// ⛔ تمِ سوم اضافه نمی‌شود و «آبی = روشن، طلایی = تیره» قرارداد است.
    /// ‎TableStyle.Apply‎ روی **هر دو** فرهنگ می‌نویسد و دو تم با یک گونه
    /// یعنی یکی‌شان رنگِ دیگری را می‌پوشاند.
    /// </summary>
    [Fact]
    public void DoTam_DoGoone_NaBishtar()
    {
        Assert.Equal(2, PumpTheme.All.Length);
        Assert.Single(PumpTheme.All, t => !t.IsDark);
        Assert.Single(PumpTheme.All, t => t.IsDark);
        Assert.False(PumpTheme.Blue.IsDark);
        Assert.True(PumpTheme.Gold.IsDark);
    }

    /// <summary>
    /// ریمیکِ دارک مود پس نرود: سه رنگی که «نوشته‌ها خوانده نمی‌شوند» را
    /// می‌ساختند دیگر برنمی‌گردند — کم‌رنگِ خاکیِ زیتونی، و خط و نوارِ سرِ
    /// جدولِ زردِ کم‌نور.
    /// </summary>
    [Fact]
    public void DarkMode_RangeaayeMohv_Barnemigardand()
    {
        var g = PumpTheme.Gold;
        Assert.NotEqual(PumpTheme.C("#a9a79a"), g.Muted);      // کم‌رنگِ خاکی
        Assert.NotEqual(PumpTheme.C("#4a4322"), g.Border);     // خطِ زردِ محو
        Assert.NotEqual(PumpTheme.C("#3a3418"), g.HeadBand);   // نوارِ زیتونی

        //  ⚠️ ولی هویتِ «مشکی و زرد» پس گرفته نشد — خواستهٔ خودش در
        //  ۱۴۰۵/۰۶/۲۵ بود و هیچ‌وقت لغو نشد.
        Assert.Equal(PumpTheme.C("#ffd700"), g.Accent);
        Assert.True(Luma(g.Dark) < 0.02, "بومِ دارک مود باید مشکی بماند");
    }
}
