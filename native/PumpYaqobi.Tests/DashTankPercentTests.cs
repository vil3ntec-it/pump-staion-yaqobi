using PumpYaqobi.Application.Services;
using PumpYaqobi.Domain.Enums;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ درصدِ مخزن در داشبورد = درصدِ بخشِ مخزن (۱۴۰۵/۰۷/۱۹) ══
/// گزارشِ صاحب ریپو: «داشبورد مخزنِ پطرول را اشتباه نشان می‌دهد؛ درصدش با درصدِ اصلیِ مخزن خیلی
/// تفاوت دارد.» ظرفیتِ نانوشته در مخزن ۱۰٬۰۰۰ بود و داشبورد آن را از موجودی می‌ساخت.
/// </summary>
public class DashTankPercentTests
{
    [Fact]
    public void ZarfiyatNaneveshte_HamanDahHezarMakhzan()
    {
        var t = DashboardService.Tank(FuelType.Petrol, 3000m, null, 1000m);
        Assert.Equal(10000m, t.Capacity);          // نه ۴٬۰۰۰ِ ساختگی
        Assert.Equal(30m, t.Current / t.Capacity * 100m);
        Assert.Equal(DashboardService.DefaultTankCapacity, DashboardService.TankCapacity(0m));
    }

    [Fact]
    public void ZarfiyatNeveshte_HamanZarfiyat()
    {
        var t = DashboardService.Tank(FuelType.Diesel, 4500m, 6000m, 1000m);
        Assert.Equal(6000m, t.Capacity);
        Assert.Equal(75m, t.Current / t.Capacity * 100m);
    }
}
