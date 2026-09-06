using PumpYaqobi.Application.Services;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;
using PumpYaqobi.Infrastructure.Migration;
using Xunit;

namespace PumpYaqobi.Tests;

file sealed class NoRates : IUnionRateProvider
{ public decimal UnionRate(FuelType f) => 0m; }

/// <summary>
/// ══ استقلالِ حساب‌ها و زیرحساب‌ها ═══════════════════════════════════════════
///
/// سه قاعده که صاحب ریپو صریحاً خواسته و هر سه آسان می‌شکنند:
///
///   ۱. **واحد مالِ هر حساب است، نه مالِ شخص.** اگر روی یک حساب فرعی «پول»
///      بزنی، حسابِ اصلی و فرعی‌های دیگر نباید پول شوند.
///   ۲. **فیصدیِ پطرول و دیزل هیچ ربطی به هم ندارند.**
///   ۳. **‎moneyRows‎ هرگز واردِ لیتر نمی‌شود** — «از تیل داخل تیل، از پول
///      داخل پول».
/// </summary>
public class DebtAccountParityTests
{
    private static DebtCalculationService Svc() => new(new NoRates());

    // ── ۱) واحد ────────────────────────────────────────────────────────────
    /// <summary>
    /// ‎_acctModeMoney‎ / ‎_setAcctMode‎ — عوض کردنِ واحدِ یک حساب هیچ حسابِ
    /// دیگری از همان شخص را دست نمی‌زند.
    /// </summary>
    [Fact]
    public void ChangingOneAccountsUnitLeavesTheOthersAlone()
    {
        var p = new Debtor { Name = "کریم" };
        p.SubAccounts.Add(new DebtAccount { LegacySubId = "s1", Name = "دکان" });
        p.SubAccounts.Add(new DebtAccount { LegacySubId = "s2", Name = "موتر" });

        p.SubAccounts[0].Mode = LedgerMode.Money;

        Assert.Equal(LedgerMode.Fuel, p.MainAccount.Mode);
        Assert.Equal(LedgerMode.Money, p.SubAccounts[0].Mode);
        Assert.Equal(LedgerMode.Fuel, p.SubAccounts[1].Mode);
    }

    /// <summary>‎_acctRows‎ — دفترِ دیده‌شده از روی واحدِ همان حساب می‌آید.</summary>
    [Fact]
    public void TheVisibleLedgerFollowsTheAccountsOwnUnit()
    {
        var a = new DebtAccount();
        a.FuelRows.Add(new DebtRow { Liters = 10m });
        a.MoneyRows.Add(new DebtRow { Bardagi = 500m, ByMoney = true });

        Assert.Same(a.FuelRows, a.ActiveRows());
        a.Mode = LedgerMode.Money;
        Assert.Same(a.MoneyRows, a.ActiveRows());
    }

    /// <summary>
    /// ‎_migrateAcctModes‎ — حسابِ فرعیِ دادهٔ قدیمی که واحد ندارد، واحدِ شخص
    /// را می‌گیرد.
    ///
    /// ⚠️ بی این، حسابِ فرعیِ «واحد پول» پس از مهاجرت «تیل» می‌شد و دفترِ
    /// پولش از جلوی چشم غیب می‌شد — ردیف‌ها سرِ جایشان بودند ولی دیده
    /// نمی‌شدند، که بدتر از گم شدن است چون کسی متوجه نمی‌شود.
    /// </summary>
    [Fact]
    public void ASubAccountWithoutAUnitInheritsThePersons()
    {
        const string json = """
        { "debtPersons": [ {
            "id": "p1", "name": "کریم", "mode": "money",
            "rows": [], "moneyRows": [],
            "subs": [
              { "id": "s1", "name": "بی‌واحد", "rows": [], "moneyRows": [] },
              { "id": "s2", "name": "تیلی", "mode": "fuel", "rows": [], "moneyRows": [] }
            ] } ] }
        """;

        var (debtors, _, _, _, _, rep) = new LegacyBackupImporter().Parse(json);
        Assert.Empty(rep.Warnings);
        var p = Assert.Single(debtors);

        Assert.Equal(LedgerMode.Money, p.MainAccount.Mode);
        Assert.Equal(LedgerMode.Money, p.SubAccounts[0].Mode);   // ارث بُرد
        Assert.Equal(LedgerMode.Fuel, p.SubAccounts[1].Mode);    // واحدِ خودش را داشت
    }

    /// <summary>شخصِ تیلی هم همین‌طور — واحدِ اشتباه به فرعی‌ها نمی‌رسد.</summary>
    [Fact]
    public void AFuelPersonsSubAccountsStayFuel()
    {
        const string json = """
        { "debtPersons": [ {
            "id": "p1", "name": "احمد",
            "rows": [], "moneyRows": [],
            "subs": [ { "id": "s1", "rows": [], "moneyRows": [] } ] } ] }
        """;
        var (debtors, _, _, _, _, _) = new LegacyBackupImporter().Parse(json);
        Assert.Equal(LedgerMode.Fuel, debtors[0].SubAccounts[0].Mode);
    }

    /// <summary>«سپردهٔ پول» هم مالِ حساب است، نه مالِ شخص.</summary>
    [Fact]
    public void TheMoneyDepositBelongsToTheAccount()
    {
        const string json = """
        { "debtPersons": [ {
            "id": "p1", "name": "کریم", "moneyDeposit": 1200,
            "rows": [], "moneyRows": [],
            "subs": [ { "id": "s1", "moneyDeposit": 300, "rows": [], "moneyRows": [] } ] } ] }
        """;
        var (debtors, _, _, _, _, _) = new LegacyBackupImporter().Parse(json);
        Assert.Equal(1200m, debtors[0].MainAccount.MoneyDeposit);
        Assert.Equal(300m, debtors[0].SubAccounts[0].MoneyDeposit);
    }

    // ── ۲) فیصدی ───────────────────────────────────────────────────────────
    /// <summary>‎_setAcctPct(a, fuel, val)‎ — هر تیل فقط خودش.</summary>
    [Fact]
    public void SettingOneFuelsPercentNeverTouchesTheOther()
    {
        var svc = Svc();
        var a = new DebtAccount();

        svc.SetPercent(a, FuelType.Petrol, 5m);
        Assert.Equal(5m, svc.PercentOf(a, FuelType.Petrol));
        Assert.Equal(0m, svc.PercentOf(a, FuelType.Diesel));

        svc.SetPercent(a, FuelType.Diesel, 8m);
        Assert.Equal(5m, svc.PercentOf(a, FuelType.Petrol));
        Assert.Equal(8m, svc.PercentOf(a, FuelType.Diesel));
    }

    /// <summary>میان‌بُرِ «هر دو» — ‎fuel === 'both'‎.</summary>
    [Fact]
    public void TheBothShortcutWritesBoth()
    {
        var svc = Svc();
        var a = new DebtAccount { PercentPetrol = 5m, PercentDiesel = 8m };
        svc.SetPercent(a, null, 3m);
        Assert.Equal(3m, svc.PercentOf(a, FuelType.Petrol));
        Assert.Equal(3m, svc.PercentOf(a, FuelType.Diesel));
    }

    /// <summary>خالی گذاشتن یعنی «برای این تیل فیصدی نیست» — یعنی صفر.</summary>
    [Fact]
    public void ClearingAPercentReallyMeansZero()
    {
        var svc = Svc();
        var a = new DebtAccount { PercentLegacy = 7m };
        svc.SetPercent(a, FuelType.Petrol, null);

        Assert.Equal(0m, svc.PercentOf(a, FuelType.Petrol));
        // ⚠️ ‎_pctSplit‎: دیزل باید عددِ مشترکِ قدیمی را صریح نگه دارد، وگرنه
        // با خالی کردنِ پطرول، فیصدیِ مشترک دوباره روی دیزل زنده می‌شد.
        Assert.Equal(7m, svc.PercentOf(a, FuelType.Diesel));
    }

    /// <summary>حسابِ قدیمی که فقط ‎percent‎ دارد، همان را برای هر دو می‌خواند.</summary>
    [Fact]
    public void AnOldAccountFallsBackToTheSharedPercent()
    {
        var svc = Svc();
        var a = new DebtAccount { PercentLegacy = 4m };
        Assert.Equal(4m, svc.PercentOf(a, FuelType.Petrol));
        Assert.Equal(4m, svc.PercentOf(a, FuelType.Diesel));
    }

    /// <summary>
    /// ‎a.percent‎ برای فایل‌های کپی و نسخه‌های قدیمی هم‌گام می‌ماند:
    /// «پطرول اگر صفر نبود، وگرنه دیزل».
    /// </summary>
    [Fact]
    public void TheLegacyPercentStaysInSync()
    {
        var svc = Svc();
        var a = new DebtAccount();

        svc.SetPercent(a, FuelType.Diesel, 9m);
        Assert.Equal(9m, a.PercentLegacy);          // پطرول صفر است → دیزل

        svc.SetPercent(a, FuelType.Petrol, 2m);
        Assert.Equal(2m, a.PercentLegacy);          // حالا پطرول برنده
    }

    // ── ۳) پول و تیل قاطی نشوند ────────────────────────────────────────────
    /// <summary>
    /// ‎_noFuel(r)‎ — لیترِ ردیف‌های دفترِ **پول** فقط مدرکِ همان ردیف است و
    /// در هیچ جمعِ لیتری نمی‌نشیند.
    ///
    /// ⚠️ باگی که این قاعده را ساخت: روی کارتِ قرض‌دار «الباقی ۳۲۲٫۲۲ لیتر»
    /// دیده می‌شد — ۱۰۰ لیترِ دفترِ تیل به‌علاوهٔ ۲۲۲٫۲۲ لیترِ ردیفی که در
    /// دفترِ پول نشسته بود. مبلغ‌ها اما باید کامل بمانند، وگرنه بدهی پنهان
    /// می‌شود.
    /// </summary>
    [Fact]
    public void MoneyRowLitersNeverReachTheFuelTotals()
    {
        var a = new DebtAccount();
        a.FuelRows.Add(new DebtRow { Fuel = FuelType.Petrol, Liters = 100m, Bardagi = 6000m, Albaqi = 6000m });
        a.MoneyRows.Add(new DebtRow
        {
            Fuel = FuelType.Petrol, ByMoney = true,
            Liters = 222.22m, RasidFuel = 5m,      // ← این دو نباید شمرده شوند
            Bardagi = 3000m, Rasid = 1000m, Albaqi = 2000m,
        });

        var t = Svc().AccountTotals(a);

        Assert.Equal(100m, t.Petrol.Liters);        // نه ۳۲۲٫۲۲
        Assert.Equal(0m, t.Petrol.RasidFuel);
        // ولی مبلغ‌ها کامل‌اند
        Assert.Equal(9000m, t.Petrol.Bardagi);
        Assert.Equal(1000m, t.Petrol.Rasid);
        Assert.Equal(8000m, t.Petrol.Albaqi);
    }

    /// <summary>همان قاعده در سطحِ شخص، با حسابِ اصلی و فرعی با هم.</summary>
    [Fact]
    public void TheRuleHoldsAcrossTheMainAndSubAccounts()
    {
        var p = new Debtor { Name = "کریم" };
        p.MainAccount.FuelRows.Add(new DebtRow { Fuel = FuelType.Diesel, Liters = 40m });
        p.MainAccount.MoneyRows.Add(new DebtRow { Fuel = FuelType.Diesel, Liters = 999m, ByMoney = true });

        var sub = new DebtAccount { LegacySubId = "s1", Mode = LedgerMode.Money };
        sub.FuelRows.Add(new DebtRow { Fuel = FuelType.Diesel, Liters = 10m });
        sub.MoneyRows.Add(new DebtRow { Fuel = FuelType.Diesel, Liters = 888m, ByMoney = true });
        p.SubAccounts.Add(sub);

        var t = Svc().SumTotals(p.AllAccounts());
        Assert.Equal(50m, t.Diesel.Liters);         // ۴۰ + ۱۰، و بس
    }

    /// <summary>
    /// و برعکسش: بدهیِ دفترِ پول در جمع‌های پولی هست، پس چیزی پنهان نمی‌شود.
    /// </summary>
    [Fact]
    public void MoneyLedgerDebtIsStillCounted()
    {
        var a = new DebtAccount { Mode = LedgerMode.Money };
        a.MoneyRows.Add(new DebtRow { Fuel = FuelType.Petrol, ByMoney = true, Bardagi = 5000m, Albaqi = 5000m });

        var b = Svc().Balances(new[] { a });
        Assert.Equal(5000m, b.Money);
        Assert.Equal(0m, b.Fuel);
    }
}
