using PumpYaqobi.Reporting.Pdf;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ «دو خط شده تاریخ… یکی کوچیک یکی بزرگ» ═══════════════════════════════════
///
/// گزارشِ صاحب ریپو (۱۴۰۵/۰۷/۱۲) دربارهٔ پرینت‌ها و پی‌دی‌اف‌ها. یک ریشه‌اش
/// ربطی به پهنای ستون نداشت: خانه‌ای مثلِ «۱۲۳٬۴۵۶ افغانی» یک فاصلهٔ معمولی
/// دارد، پس موتورِ متن حق دارد همان‌جا بشکند — و چون بلندیِ ردیفِ جدول از
/// بلندترین خانه‌اش می‌آید، یک شکست **کلِ ردیف** را دو برابر می‌کند.
///
/// ⚠️ این آزمون فقط همان قاعده را می‌سنجد، نه «زیبا شد»: زیباییِ ورق را باید
/// با عکس دید (‎dotnet run --project PumpYaqobi.UiTests -- printshot &lt;پوشه&gt;‎).
/// </summary>
public class PrintTightTests
{
    /// <summary>فاصلهٔ نشکن — همانکه <c>DocStyle.Tight</c> می‌نشاند.</summary>
    private const char Nbsp = '\u00a0';

    [Theory]
    [InlineData("۱۲۳٬۴۵۶ افغانی")]
    [InlineData("$۱۲٫۳۴ دالر")]
    [InlineData("۸۴۰ لیتر")]
    [InlineData("۱۲ روز")]
    [InlineData("۳٫۵ تن")]
    public void Adad_VaVahedash_DoKhat_Nemishavand(string cell)
    {
        var got = DocStyle.Tight(cell);
        Assert.DoesNotContain(" ", got);
        Assert.Contains(Nbsp, got);
        //  ⛔ فقط فاصله عوض می‌شود — نه یک نویسهٔ دیگر، نه یک نویسه کم یا زیاد.
        Assert.Equal(cell.Length, got.Length);
        Assert.Equal(cell, got.Replace(Nbsp, ' '));
    }

    /// <summary>
    /// ⛔ متنِ آزاد باید مثلِ همیشه بپیچد، وگرنه یادداشتِ بلند از کادر می‌زند
    /// بیرون — یعنی همان چیزی که این اصلاح می‌خواست درستش کند، از درِ دیگر.
    /// </summary>
    [Theory]
    [InlineData("یادداشت")]
    [InlineData("💵 بردگی")]
    [InlineData("تاریخ شمسی")]
    [InlineData("قرضِ آقای کریم بابتِ خریدِ دیزلِ ماهِ گذشته")]
    [InlineData("پرداختِ ۲ قسط بابتِ حسابِ شرکتِ نمونه در ماهِ جاری")]
    public void MatneAzad_MesleHamishe_Mipichad(string cell)
    {
        var got = DocStyle.Tight(cell);
        Assert.Equal(cell, got);
        Assert.DoesNotContain(Nbsp, got);
    }

    /// <summary>خانهٔ بی‌فاصله و خانهٔ خالی هیچ کاری لازم ندارند.</summary>
    [Fact]
    public void BiFasele_VaKhali_DastNemikhorand()
    {
        Assert.Equal("۱۴۰۵/۰۶/۲۷", DocStyle.Tight("۱۴۰۵/۰۶/۲۷"));
        Assert.Equal("—", DocStyle.Tight("—"));
        Assert.Equal(string.Empty, DocStyle.Tight(string.Empty));
    }
}
