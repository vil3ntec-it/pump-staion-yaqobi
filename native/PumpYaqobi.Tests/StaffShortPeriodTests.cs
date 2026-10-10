using PumpYaqobi.Application.Services;
using PumpYaqobi.Domain.Entities;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ کمبودیِ کارمندان: هر دوره جدا، سهمِ هر ورق، رسیدِ همان دوره (۱۴۰۵/۰۷/۱۸) ══
/// </summary>
public class StaffShortPeriodTests
{
    private static WaraqEntry Waraq(long id, string date, string worker, decimal declared)
    {
        var w = new WaraqEntry { Id = id, DateShamsi = date, DateKey = PumpYaqobi.Domain.DateKeys.Key(date) };
        var sd = new WaraqShift { Kind = ShiftKind.Day, WorkerName = worker };
        sd.Pumps.Add(new WaraqPump { Debt = declared });
        w.Shifts.Add(sd);
        return w;
    }

    private static readonly StaffShortService Svc = new(new WaraqService());

    private static List<WaraqEntry> Data() => new()
    {
        Waraq(1, "1405/06/10", "کریم", 500m),
        Waraq(2, "1405/07/02", "کریم", 300m),
        Waraq(3, "1405/07/05", "رحیم", 200m),
    };

    [Fact]
    public void HarMah_FaghatVaraghayeHamanMah()
    {
        var mizan = Svc.RowsFor(Data(), Array.Empty<StaffShortSettle>(), ProfitPeriod.FromKey("1405/07"));
        Assert.Equal(300m, mizan.Single(r => r.Name == "کریم").RemainShort);
        Assert.Equal(200m, mizan.Single(r => r.Name == "رحیم").RemainShort);

        var sonbola = Svc.RowsFor(Data(), Array.Empty<StaffShortSettle>(), ProfitPeriod.FromKey("1405/06"));
        Assert.Equal(500m, Assert.Single(sonbola).RemainShort);

        var all = Svc.RowsFor(Data(), Array.Empty<StaffShortSettle>(), ProfitPeriod.All);
        Assert.Equal(800m, all.Single(r => r.Name == "کریم").RemainShort);
        // «همه» مو‌به‌مو همان جدولِ پیشین
        Assert.Equal(Svc.Rows(Data(), Array.Empty<StaffShortSettle>()), all);
    }

    [Fact]
    public void RasideMah_FaghatHamanMahRaKamMikonad()
    {
        var key = PostingService.NormFa("کریم");
        var settles = new List<StaffShortSettle>
        {
            new() { NameKey = key, Name = "کریم", Kind = StaffSettleKind.Short, Amount = 100m,
                    DateShamsi = "1405/07/10", DateKey = 14050710, ForMonth = "1405/06" },
        };
        var sonbola = Svc.RowsFor(Data(), settles, ProfitPeriod.FromKey("1405/06"));
        Assert.Equal(400m, Assert.Single(sonbola).RemainShort);          // رسید برای سنبله
        var mizan = Svc.RowsFor(Data(), settles, ProfitPeriod.FromKey("1405/07"));
        Assert.Equal(300m, mizan.Single(r => r.Name == "کریم").RemainShort);  // میزان دست نخورد
        var year = Svc.RowsFor(Data(), settles, ProfitPeriod.FromKey("1405/*"));
        Assert.Equal(700m, year.Single(r => r.Name == "کریم").RemainShort);
        var all = Svc.RowsFor(Data(), settles, ProfitPeriod.All);
        Assert.Equal(700m, all.Single(r => r.Name == "کریم").RemainShort);
    }

    [Fact]
    public void RasideKohneyeBiMah_BaTarikheKhodash()
    {
        var key = PostingService.NormFa("کریم");
        var old = new StaffShortSettle { NameKey = key, Kind = StaffSettleKind.Short, Amount = 50m, DateKey = 14050620 };
        Assert.True(StaffShortService.SettleIn(old, ProfitPeriod.FromKey("1405/06")));
        Assert.False(StaffShortService.SettleIn(old, ProfitPeriod.FromKey("1405/07")));
        var yearOnly = new StaffShortSettle { ForMonth = "1405", DateKey = 14050720 };
        Assert.True(StaffShortService.SettleIn(yearOnly, ProfitPeriod.FromKey("1405/*")));
        Assert.False(StaffShortService.SettleIn(yearOnly, ProfitPeriod.FromKey("1405/07")));
    }

    [Fact]
    public void SahmeHarVaragh_BaTarikhVaDalil()
    {
        var lines = Svc.Lines(Data());
        Assert.Equal(3, lines.Count);
        var l = lines.First();
        Assert.Equal("1405/06/10", l.DateShamsi);
        Assert.Equal(500m, l.Declared);
        Assert.Equal(500m, l.Shortage);
        Assert.Equal(1, lines.Count(x => x.WaraqId == 2 && x.Shortage == 300m));
    }
}
