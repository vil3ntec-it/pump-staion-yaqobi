using PumpYaqobi.App.ViewModels.Sections;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ⚠️ «اگر قیمت بالا برود» در تیل امانت (۱۴۰۵/۰۷/۲۰) — با همان مثالِ صاحب ریپو:
/// ۵۰٬۰۰۰ لیترِ امانتی به ۶۵ فروخته شد و حالا قیمت ۸۵ است.
/// </summary>
public class AmanatPriceRiskTests
{
    [Fact]
    public void MesaleSahebRepo_YekMilionZarar()
    {
        var r = AmanatPriceRisk.Of(50_000m, 65m, 85m);
        Assert.Equal(3_250_000m, r.Got);        // پولی که گرفت
        Assert.Equal(4_250_000m, r.NeedNow);    // پولِ لازم برای پس دادن
        Assert.Equal(1_000_000m, r.Loss);       // از جیبِ خودش
        Assert.Equal(50_000m, r.PerAfghani);    // هر یک افغانی = ۵۰٬۰۰۰ ضررِ بیشتر
    }

    [Fact]
    public void QeymatPayinBiyayad_Sood_Ast()
    {
        var r = AmanatPriceRisk.Of(1_000m, 85m, 80m);
        Assert.Equal(-5_000m, r.Loss);
    }

    [Fact]
    public void QeymatHaman_NaZararNaSood() =>
        Assert.Equal(0m, AmanatPriceRisk.Of(1_000m, 70m, 70m).Loss);
}
