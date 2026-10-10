using PumpYaqobi.Application.Services;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>══ چند مخزن برای یک تیل — کشیدن از اولی، تمام شد ⇒ دومی (۱۴۰۵/۰۷/۲۲) ══</summary>
public class TankSplitTests
{
    private static readonly TankDef T1 = new(1, 1, 10000m), T2 = new(2, 2, 8000m), T3 = new(3, 3, 5000m);

    [Fact]
    public void AzAvvaliMikeshad_TamamShod_DovvomiFaalMishavad()
    {
        var r = TankSplitService.Split(new[] { T1, T2 },
            new[] { new TankIn(14050701, 1, 6000m), new TankIn(14050701, 2, 4000m) },
            new[] { new TankOut(14050702, 5000m) });
        Assert.Equal(1000m, r[0].Current);
        Assert.True(r[0].Active);
        Assert.Equal(4000m, r[1].Current);

        r = TankSplitService.Split(new[] { T1, T2 },
            new[] { new TankIn(14050701, 1, 6000m), new TankIn(14050701, 2, 4000m) },
            new[] { new TankOut(14050702, 7000m) });
        Assert.Equal(0m, r[0].Current);
        Assert.Equal(3000m, r[1].Current);
        Assert.True(r[1].Active);                        // مخزنِ ۱ تمام شد ⇒ از ۲ می‌کشد
    }

    [Fact]
    public void JamMakhzanHa_HamanMojudiKol()
    {
        var ins = new[] { new TankIn(14050701, 1, 3000m), new TankIn(14050703, 2, 2000m), new TankIn(14050705, null, 1500m) };
        var outs = new[] { new TankOut(14050702, 2500m), new TankOut(14050704, 1200m), new TankOut(14050706, 400m), new TankOut(14050706, -100m) };
        var r = TankSplitService.Split(new[] { T1, T2, T3 }, ins, outs);
        Assert.Equal(3000m + 2000m + 1500m - 2500m - 1200m - 400m + 100m, r.Sum(x => x.Current));
    }

    [Fact]
    public void KharideTaghsimNashode_BeMakhzaneAvval()
    {
        var r = TankSplitService.Split(new[] { T2, T1 }, new[] { new TankIn(14050701, null, 500m) }, Array.Empty<TankOut>());
        Assert.Equal(1, r[0].Num);
        Assert.Equal(500m, r[0].Current);
    }

    [Fact]
    public void ForoshBishAzHame_RuyeAvvaliManfi_VaNamayeshSefr()
    {
        var r = TankSplitService.Split(new[] { T1, T2 }, new[] { new TankIn(14050701, 2, 100m) }, new[] { new TankOut(14050702, 300m) });
        Assert.Equal(-200m, r[0].Current);
        Assert.Equal(0m, r[0].Display);
        Assert.Equal(0m, r[1].Current);
        Assert.Equal(-200m, r.Sum(x => x.Current));      // جمع همان موجودیِ کل
    }

    [Fact]
    public void KharideTaze_BeMakhzaneAvval_DobareAzAvvali()
    {
        //  ۱ تمام شد و ۲ در حالِ کشیدن است؛ خریدِ تازه به ۱ رفت ⇒ دوباره از ۱ کشیده می‌شود (کوچک‌ترین شماره)
        var r = TankSplitService.Split(new[] { T1, T2 },
            new[] { new TankIn(14050701, 1, 1000m), new TankIn(14050701, 2, 1000m), new TankIn(14050703, 1, 2000m) },
            new[] { new TankOut(14050702, 1500m), new TankOut(14050704, 300m) });
        Assert.Equal(1700m, r[0].Current);
        Assert.Equal(500m, r[1].Current);
        Assert.True(r[0].Active);
    }
}
