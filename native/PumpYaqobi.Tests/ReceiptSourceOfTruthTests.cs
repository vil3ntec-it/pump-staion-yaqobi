using PumpYaqobi.Application.Services;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ رسید = یک رکوردِ واقعی ══════════════════════════════════════════════════
///
/// خواستهٔ صریحِ صاحب ریپو: «برای هر قسمت یک مقدارِ جداگانه نساز. رسید باید یک
/// دادهٔ واقعی باشد؛ سربرگ، جدول و جمله همان یکی را نشان بدهند.»
///
/// ریشهٔ ناهم‌گامی دو انبارِ جدا بود — چهار عددِ ‎Rasid…‎ی حساب برای سربرگ، و
/// ستونِ رسیدِ ردیف‌ها برای جدول. این آزمون‌ها همان یکی‌بودن را قفل می‌کنند.
/// </summary>
public class ReceiptSourceOfTruthTests
{
    private static DebtCalculationService Calc() => new(new Rates());

    private sealed class Rates : IUnionRateProvider
    {
        public decimal UnionRate(FuelType fuel) => 60m;
    }

    private static DebtAccount Fresh() =>
        new() { Mode = LedgerMode.Fuel, ReceiptsMigrated = true };

    // ══ TEST 1..6 — همان سناریوی دستور ══════════════════════════════════════

    /// <summary>رسیدِ سربرگ یک ردیفِ واقعی می‌سازد و جمع همان است.</summary>
    [Fact]
    public void AReceiptFromTheHeaderBecomesARealRow()
    {
        var c = Calc();
        var a = Fresh();

        c.AddReceiptRow(a, LedgerMode.Fuel, FuelType.Petrol, 10000m);

        Assert.Single(a.FuelRows);
        Assert.Equal(10000m, a.FuelRows[0].RasidFuel);
        Assert.Equal(10000m, c.ReceiptTotal(a, LedgerMode.Fuel, FuelType.Petrol));
        Assert.Equal(10000m, a.RasidFuelPetrol);          // کشِ حساب هم همان
    }

    /// <summary>رسیدِ دوم جای اولی را نمی‌گیرد — جمع، نه آخرین.</summary>
    [Fact]
    public void TheHeaderIsTheSumNotTheLatest()
    {
        var c = Calc();
        var a = Fresh();

        c.AddReceiptRow(a, LedgerMode.Fuel, FuelType.Petrol, 10000m);
        c.AddReceiptRow(a, LedgerMode.Fuel, FuelType.Petrol, 30000m);

        Assert.Equal(2, a.FuelRows.Count);
        Assert.Equal(40000m, c.ReceiptTotal(a, LedgerMode.Fuel, FuelType.Petrol));
    }

    /// <summary>حذفِ ردیف از جدول، سربرگ را هم کم می‌کند.</summary>
    [Fact]
    public void DeletingTheRowLowersTheHeader()
    {
        var c = Calc();
        var a = Fresh();
        var first = c.AddReceiptRow(a, LedgerMode.Fuel, FuelType.Petrol, 10000m);
        c.AddReceiptRow(a, LedgerMode.Fuel, FuelType.Petrol, 30000m);

        a.FuelRows.Remove(first);
        c.SyncReceiptTotals(a);

        Assert.Equal(30000m, c.ReceiptTotal(a, LedgerMode.Fuel, FuelType.Petrol));
        Assert.Equal(30000m, a.RasidFuelPetrol);
    }

    /// <summary>رسیدی که مستقیم در جدول نوشته شود هم در سربرگ می‌آید.</summary>
    [Fact]
    public void AReceiptTypedIntoTheTableReachesTheHeader()
    {
        var c = Calc();
        var a = Fresh();
        c.AddReceiptRow(a, LedgerMode.Fuel, FuelType.Petrol, 30000m);

        // همان کاری که کاربر در خانهٔ «رسید تیل» می‌کند
        var typed = new DebtRow { FuelAccountId = a.Id, Fuel = FuelType.Petrol, RasidFuel = 20000m };
        a.FuelRows.Add(typed);
        c.SyncReceiptTotals(a);

        Assert.Equal(50000m, c.ReceiptTotal(a, LedgerMode.Fuel, FuelType.Petrol));
    }

    /// <summary>ویرایشِ همان خانه هم بی‌درنگ در جمع دیده می‌شود.</summary>
    [Fact]
    public void EditingTheCellUpdatesTheTotal()
    {
        var c = Calc();
        var a = Fresh();
        var r = c.AddReceiptRow(a, LedgerMode.Fuel, FuelType.Petrol, 30000m);
        c.AddReceiptRow(a, LedgerMode.Fuel, FuelType.Petrol, 20000m);

        r.RasidFuel = 50000m;
        c.SyncReceiptTotals(a);

        Assert.Equal(70000m, c.ReceiptTotal(a, LedgerMode.Fuel, FuelType.Petrol));
    }

    /// <summary>TEST 7 — ردیفِ خالی جمع را خراب نمی‌کند.</summary>
    [Fact]
    public void ABlankRowKeepsTheTotal()
    {
        var c = Calc();
        var a = Fresh();
        c.AddReceiptRow(a, LedgerMode.Fuel, FuelType.Petrol, 70000m);

        a.FuelRows.Add(new DebtRow { FuelAccountId = a.Id, Fuel = FuelType.Petrol });
        c.SyncReceiptTotals(a);

        Assert.Equal(70000m, c.ReceiptTotal(a, LedgerMode.Fuel, FuelType.Petrol));
    }

    /// <summary>TEST 8 — با رفتنِ همهٔ رسیدها، جمع به صفر برمی‌گردد.</summary>
    [Fact]
    public void RemovingEveryReceiptReturnsToZero()
    {
        var c = Calc();
        var a = Fresh();
        c.AddReceiptRow(a, LedgerMode.Fuel, FuelType.Petrol, 10000m);
        c.AddReceiptRow(a, LedgerMode.Fuel, FuelType.Petrol, 30000m);

        a.FuelRows.Clear();
        c.SyncReceiptTotals(a);

        Assert.Equal(0m, c.ReceiptTotal(a, LedgerMode.Fuel, FuelType.Petrol));
        Assert.Equal(0m, a.RasidFuelPetrol);
    }

    // ══ TEST 9..11 — پطرول و دیزل ═══════════════════════════════════════════

    /// <summary>هر رسید مالِ تیلِ خودش است و روی آن‌یکی اثر ندارد.</summary>
    [Fact]
    public void PetrolAndDieselNeverMix()
    {
        var c = Calc();
        var a = Fresh();

        c.AddReceiptRow(a, LedgerMode.Fuel, FuelType.Petrol, 10000m);
        c.AddReceiptRow(a, LedgerMode.Fuel, FuelType.Diesel, 5000m);

        Assert.Equal(10000m, c.ReceiptTotal(a, LedgerMode.Fuel, FuelType.Petrol));
        Assert.Equal(5000m, c.ReceiptTotal(a, LedgerMode.Fuel, FuelType.Diesel));

        var d = a.FuelRows.First(r => r.Fuel == FuelType.Diesel);
        a.FuelRows.Remove(d);
        c.SyncReceiptTotals(a);

        Assert.Equal(10000m, a.RasidFuelPetrol);   // پطرول دست‌نخورده
        Assert.Equal(0m, a.RasidFuelDiesel);
    }

    /// <summary>دفترِ تیل و دفترِ پول هم دو ستونِ جدا هستند.</summary>
    [Fact]
    public void TheFuelAndMoneyLedgersUseTheirOwnColumn()
    {
        var c = Calc();
        var a = Fresh();

        c.AddReceiptRow(a, LedgerMode.Fuel, FuelType.Petrol, 100m);
        c.AddReceiptRow(a, LedgerMode.Money, FuelType.Petrol, 200m);

        Assert.Equal(100m, a.FuelRows[0].RasidFuel);   // دفترِ تیل ⇒ «رسید تیل»
        Assert.Equal(0m, a.FuelRows[0].Rasid);
        Assert.Equal(200m, a.MoneyRows[0].Rasid);      // دفترِ پول ⇒ «رسید»
        Assert.Equal(0m, a.MoneyRows[0].RasidFuel);

        Assert.Equal(100m, a.RasidFuelPetrol);
        Assert.Equal(200m, a.RasidMoneyPetrol);
    }

    // ══ TEST 24 — دو راهِ ورود، یک نتیجه ════════════════════════════════════

    /// <summary>
    /// رسیدی که از سربرگ ساخته می‌شود و رسیدی که در جدول تایپ می‌شود باید
    /// **یک‌جور** رکورد باشند — خواستهٔ صریحِ صاحب ریپو.
    /// </summary>
    [Fact]
    public void BothWaysProduceTheSameRecord()
    {
        var c = Calc();

        var fromHeader = Fresh();
        c.AddReceiptRow(fromHeader, LedgerMode.Fuel, FuelType.Petrol, 25000m);

        var fromTable = Fresh();
        fromTable.FuelRows.Add(new DebtRow
        {
            FuelAccountId = fromTable.Id, Fuel = FuelType.Petrol, RasidFuel = 25000m,
        });
        c.SyncReceiptTotals(fromTable);

        Assert.Equal(c.ReceiptTotal(fromTable, LedgerMode.Fuel, FuelType.Petrol),
                     c.ReceiptTotal(fromHeader, LedgerMode.Fuel, FuelType.Petrol));
        Assert.Equal(fromTable.RasidFuelPetrol, fromHeader.RasidFuelPetrol);
        Assert.Equal(fromTable.FuelRows[0].RasidFuel, fromHeader.FuelRows[0].RasidFuel);
        Assert.Equal(fromTable.FuelRows[0].Fuel, fromHeader.FuelRows[0].Fuel);
    }

    // ══ مهاجرت ══════════════════════════════════════════════════════════════

    /// <summary>حسابِ قدیمی — چهار عددش یک‌بار ردیفِ واقعی می‌شوند.</summary>
    [Fact]
    public void OldAccountsGetTheirReceiptsAsRows()
    {
        var c = Calc();
        var a = new DebtAccount { RasidFuelPetrol = 900m, RasidMoneyDiesel = 50m };

        var made = c.MigrateReceiptsToRows(a);

        Assert.Equal(2, made.Count);
        Assert.Equal(900m, c.ReceiptTotal(a, LedgerMode.Fuel, FuelType.Petrol));
        Assert.Equal(50m, c.ReceiptTotal(a, LedgerMode.Money, FuelType.Diesel));
        Assert.Equal(900m, a.RasidFuelPetrol);      // عددها همان ماندند
        Assert.Equal(50m, a.RasidMoneyDiesel);
    }

    /// <summary>بارِ دوم چیزی نمی‌سازد — وگرنه رسیدها دو برابر می‌شدند.</summary>
    [Fact]
    public void MigrationRunsExactlyOnce()
    {
        var c = Calc();
        var a = new DebtAccount { RasidFuelPetrol = 900m };

        Assert.Single(c.MigrateReceiptsToRows(a));
        Assert.Empty(c.MigrateReceiptsToRows(a));
        Assert.Equal(900m, c.ReceiptTotal(a, LedgerMode.Fuel, FuelType.Petrol));
        Assert.True(a.ReceiptsMigrated);
    }

    /// <summary>حسابی که رسیدش همین حالا ردیف دارد، ردیفِ تکراری نمی‌گیرد.</summary>
    [Fact]
    public void MigrationDoesNotDoubleWhatTheRowsAlreadyExplain()
    {
        var c = Calc();
        var a = new DebtAccount { RasidFuelPetrol = 900m };
        a.FuelRows.Add(new DebtRow { Fuel = FuelType.Petrol, RasidFuel = 900m });

        Assert.Empty(c.MigrateReceiptsToRows(a));
        Assert.Equal(900m, c.ReceiptTotal(a, LedgerMode.Fuel, FuelType.Petrol));
    }

    /// <summary>دفترِ نسخهٔ پیشین هم به ردیفِ واقعی تبدیل می‌شود.</summary>
    [Fact]
    public void ThePreviousSeparateLedgerIsMigratedToo()
    {
        var c = Calc();
        var a = new DebtAccount();
        a.RasidLog.Add(new RasidEntry { Unit = LedgerMode.Fuel, Fuel = FuelType.Petrol, Value = 700m });
        a.RasidFuelPetrol = 700m;

        var made = c.MigrateReceiptsToRows(a);

        Assert.Single(made);
        Assert.Empty(a.RasidLog);
        Assert.Equal(700m, c.ReceiptTotal(a, LedgerMode.Fuel, FuelType.Petrol));
    }
}
