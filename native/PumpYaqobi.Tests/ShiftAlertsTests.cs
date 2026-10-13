using PumpYaqobi.App.Services;
using PumpYaqobi.Application.Localization;
using PumpYaqobi.Application.Services;
using PumpYaqobi.Domain.Entities;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ 💰 کسریِ صندوق و ⏰ شیفتِ بسته‌نشده — از ورق (۱۴۰۵/۰۷/۲۱) ═════════════════════
///
/// خواستهٔ صاحب ریپو: «کسری صندوق و شیفت بسته‌نشده رو هم درست کن.»
///
///   ۱) کسری همان ‎WaraqService.Shortage‎ِ ورق است (قرضِ نوشته‌شده در پایه منهای قرض و مصرفِ ثبت‌شده).
///   ۲) پایه‌ای که شروع دارد و ختمش خالی است ⇒ شیفتِ بسته‌نشده — نه کسری.
///   ۳) شیفتِ در حالِ کار هشدار نمی‌دهد: شبِ امروز هرگز، شبِ دیروز تا ظهر، روزِ امروز تا تحویل.
///   ۴) ورقِ درست ⇒ هیچ هشدار؛ کلید شناسهٔ ورق و شیفت را دارد و سرور ‎shift‎ می‌خواندش.
/// </summary>
public class ShiftAlertsTests
{
    private static readonly WaraqService Calc = new();
    private static readonly DateTime Now = new(2026, 10, 12, 15, 0, 0);

    private static WaraqEntry Day(int daysAgo, long id, ShiftKind active = ShiftKind.Day)
    {
        var d = Now.Date.AddDays(-daysAgo);
        return new WaraqEntry { Id = id, DateShamsi = Shamsi.Of(d), DateKey = Shamsi.Key(d), ActiveShift = active };
    }

    private static WaraqShift Shift(ShiftKind kind, string who, decimal end, decimal declaredDebt = 0m, decimal recordedDebt = 0m)
    {
        var s = new WaraqShift { Kind = kind, WorkerName = who };
        s.Pumps.Add(new WaraqPump { Num = 1, Start = 1000m, End = end, PricePerLiter = 50m, Debt = declaredDebt });
        if (recordedDebt > 0m)
            s.Transactions.Add(new WaraqTransaction { Name = "کریم", Amount = recordedDebt, Type = WaraqTxnType.Debt, AmountAuto = false });
        return s;
    }

    [Fact]
    public void KasriAzHamanKambudiyeWaraqAst()
    {
        var w = Day(1, 7);
        w.Shifts.Add(Shift(ShiftKind.Day, "احمد", 1500m, declaredDebt: 2500m));
        var list = AlertWatch.ShiftAlerts(new[] { w }, Now, Calc);
        var a = Assert.Single(list);
        Assert.Equal("cash-7-day", a.Key);
        Assert.Contains("کسریِ صندوق", a.Text);
        Assert.Contains("احمد", a.Text);
        Assert.Contains(Shamsi.Money(2500m) + " افغانی", a.Text);
        Assert.Equal("waraq", a.Section);
        Assert.True(a.IsShift);
        Assert.Equal("shift", CloudEvents.KindOf(a.Key, a.State));
        //  عدد همان کمبودیِ ورق
        Assert.Equal(2500m, Calc.Shortage(Calc.ShiftTotals(w.Shifts[0])).Shortage);
    }

    [Fact]
    public void WaraqeDorost_HichHoshdar()
    {
        var w = Day(1, 8);
        w.Shifts.Add(Shift(ShiftKind.Day, "احمد", 1500m, declaredDebt: 2500m, recordedDebt: 2500m));
        w.Shifts.Add(Shift(ShiftKind.Night, "محمود", 1800m));
        Assert.Empty(AlertWatch.ShiftAlerts(new[] { w }, Now, Calc));
    }

    [Fact]
    public void KhatmeKhali_ShifteBastaNashode_NaKasri()
    {
        var w = Day(2, 9);
        w.Shifts.Add(Shift(ShiftKind.Day, "محمود", 0m, declaredDebt: 2500m));
        var a = Assert.Single(AlertWatch.ShiftAlerts(new[] { w }, Now, Calc));
        Assert.Equal("shift-9-day", a.Key);
        Assert.Contains("بسته نشده", a.Text);
        Assert.Contains("ختمِ 1 پایه", a.Text);
        Assert.Equal("shift", CloudEvents.KindOf(a.Key, a.State));
    }

    [Fact]
    public void ShifteDarHaleKar_HoshdarNemidahad()
    {
        //  شبِ امروز، و روزِ امروز پیش از تحویل ⇒ هیچ
        var today = Day(0, 10);
        today.Shifts.Add(Shift(ShiftKind.Day, "احمد", 0m));
        today.Shifts.Add(Shift(ShiftKind.Night, "محمود", 0m));
        Assert.Empty(AlertWatch.ShiftAlerts(new[] { today }, Now, Calc));

        //  روزِ امروز پس از تحویل به شب ⇒ هشدار
        var handed = Day(0, 11, ShiftKind.Night);
        handed.Shifts.Add(Shift(ShiftKind.Day, "احمد", 0m));
        Assert.Equal("shift-11-day", Assert.Single(AlertWatch.ShiftAlerts(new[] { handed }, Now, Calc)).Key);

        //  شبِ دیروز: پیش از ظهر فرصت دارد، پس از ظهر هشدار
        var night = Day(1, 12);
        night.Shifts.Add(Shift(ShiftKind.Night, "محمود", 0m));
        Assert.Empty(AlertWatch.ShiftAlerts(new[] { night }, Now.Date.AddHours(9), Calc));
        Assert.Equal("shift-12-night", Assert.Single(AlertWatch.ShiftAlerts(new[] { night }, Now, Calc)).Key);
    }

    [Fact]
    public void ShifteBiPaya_Va_KasriyeNimAfghani_Hichi()
    {
        var w = Day(1, 13);
        w.Shifts.Add(new WaraqShift { Kind = ShiftKind.Day, WorkerName = "خالی" });
        w.Shifts.Add(Shift(ShiftKind.Night, "احمد", 1500m, declaredDebt: 0.4m));
        Assert.Empty(AlertWatch.ShiftAlerts(new[] { w }, Now, Calc));
    }
}
