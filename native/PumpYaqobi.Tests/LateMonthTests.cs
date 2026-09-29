using PumpYaqobi.App.Services;
using PumpYaqobi.App.ViewModels;
using PumpYaqobi.App.ViewModels.Sections;
using PumpYaqobi.Domain.Entities;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// گزارشِ صاحب ریپو (۱۴۰۵/۰۷/۱۶): «داخلِ پارچه‌ها تاریخ‌های گذشته رو زدم و حساب‌های
/// قدیم رو می‌رسوندم… اما بخشِ مصارف، گاوصندوق و بخش‌های دیگه اون ماه رو توی لیستِ
/// ماه‌هاشون نمیاره و باید برم کشویی رو باز کنم و همهٔ ماه‌ها رو انتخاب کنم.»
///
/// ⛔ ریشه: دفترهای ماهانه فهرستِ ماه‌ها را فقط سرِ نخستین بار شدن می‌ساختند و با
/// هر بازگشت فقط ردیف‌ها را دوباره می‌خواندند. پس ماهی که بخشِ **دیگری** (پارچه ⇒
/// ورق ⇒ مصارف/گاوصندوق/صرافی) با تاریخِ گذشته در این دفتر ساخت، هرگز در کشویی نمی‌آمد.
/// </summary>
[Collection(AppHostCollection.Name)]
public class LateMonthTests
{
    private static AppHost Host()
    {
        var host = new AppHost(
            Path.Combine(Path.GetTempPath(), "pump-late-" + Guid.NewGuid().ToString("N"), "pump.db"));
        if (host.Auth.NeedsFirstRun()) host.Auth.CreateFirstAdmin("1234");
        host.Auth.SignIn("admin", "1234");
        return host;
    }

    private const string OldDate = "1404/03/10";
    private const string OldMonth = "1404/03";

    private static bool InPicker(YearMonthPicker p, string key)
    {
        //  ماه در سالِ خودش است — کشوی سال را به همان سال ببر و ببین ماه هست
        var y = p.Years.FirstOrDefault(x => x.Key == YearMonthPicker.YearOf(key));
        if (y is null) return false;
        p.Year = y;
        return p.Months.Any(m => m.Key == key);
    }

    [Fact]
    public async Task Masaref_MaheGozashteAzBakhsheDigar_DarKeshuyiMiayad()
    {
        var host = Host();
        var sec = new ExpenseSectionViewModel(host);
        await sec.EnsureLoadedAsync();
        var shown = sec.Month;
        Assert.DoesNotContain(OldMonth, sec.Months);

        //  بخشِ دیگری (ورق از پارچه) با تاریخِ گذشته ردیف می‌سازد
        await host.ExpenseLedger.AddAsync(new Expense { DateShamsi = OldDate, Title = "مصرفِ دیر", Amount = 500m });
        await sec.OnActivatedAsync();

        Assert.Contains(OldMonth, sec.Months);
        Assert.Equal(shown, sec.Month);          // ⛔ ماهِ جلوی چشمِ کاربر دست نخورد
        Assert.True(InPicker(sec.Picker, OldMonth));
    }

    [Fact]
    public async Task Gavsandogh_Va_Sarrafi_Va_Chakana_Ham()
    {
        var host = Host();
        var safe = new SafeSectionViewModel(host);
        var ex = new ExchangeSectionViewModel(host);
        var ret = new RetailSectionViewModel(host);
        foreach (var s in new SectionViewModel[] { safe, ex, ret }) await s.EnsureLoadedAsync();

        await host.SafeLedger.AddAsync(new SafeEntry { DateShamsi = OldDate });
        await host.ExchangeLedger.AddAsync(new ExchangeRow { DateShamsi = OldDate });
        await host.RetailLedger.AddAsync(new RetailRow { DateShamsi = OldDate });
        foreach (var s in new SectionViewModel[] { safe, ex, ret }) await s.OnActivatedAsync();

        Assert.Contains(OldMonth, safe.Months);
        Assert.Contains(OldMonth, ex.Months);
        Assert.Contains(OldMonth, ret.Months);
    }
}
