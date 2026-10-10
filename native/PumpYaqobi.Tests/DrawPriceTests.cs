using PumpYaqobi.Application.Services;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>فیِ خرید = همان خریدی که الان از مخزن کشیده می‌شود (۱۴۰۵/۰۷/۲۲).</summary>
public class DrawPriceTests
{
    private static readonly DrawIn[] Three =
    {
        new(14050701, 1, 1000m, 50m),
        new(14050705, 2, 1000m, 60m),
        new(14050710, 3, 1000m, 70m),
    };

    [Theory]
    [InlineData(0, 50)]        // هنوز از اولی
    [InlineData(999, 50)]
    [InlineData(1000, 60)]     // اولی تمام شد ⇒ دومی
    [InlineData(2500, 70)]
    [InlineData(5000, 70)]     // همه تمام ⇒ تازه‌ترین
    public void Az_HamanKharidiKeKashideMishavad(decimal consumed, decimal expected) =>
        Assert.Equal(expected, DrawPriceService.Current(Three, consumed));

    [Fact]
    public void TartibBaTarikhAst_NaBaJayeFehrest() =>
        Assert.Equal(50m, DrawPriceService.Current(Three.Reverse(), 10m));

    [Fact]
    public void KharideBiGheymat_FiNemishavad()
    {
        var ins = new[] { new DrawIn(1, 1, 1000m, 0m), new DrawIn(2, 2, 1000m, 65m) };
        Assert.Equal(65m, DrawPriceService.Current(ins, 0m));
        Assert.Null(DrawPriceService.Current(new[] { new DrawIn(1, 1, 100m, 0m) }, 0m));
    }
}
