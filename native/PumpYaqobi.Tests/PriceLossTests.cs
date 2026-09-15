using PumpYaqobi.Application.Services;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ 📉 زیان ناشی از افزایش قیمت ═══════════════════════════════════════════
/// این قاعده‌ها از خودِ ‎index.html‎ آمده‌اند (‎_plAllocate‎ · ‎_plRowReport‎ ·
/// ‎_plRateOn‎ · ‎_plReportAll‎) و هر آزمون نامِ قاعدهٔ سایت را در توضیحش دارد.
/// سرویس هیچ چیزی نمی‌نویسد؛ همه‌چیز از داده‌ای که به آن داده می‌شود می‌آید.
/// </summary>
public class PriceLossTests
{
    private const string Today = "1405/06/25";

    private static Debtor Person(string name, long id, params DebtRow[] fuelRows)
    {
        var acc = new DebtAccount { Id = id * 10, MainOfDebtorId = id, ReceiptsMigrated = true };
        acc.FuelRows.AddRange(fuelRows);
        return new Debtor { Id = id, Name = name, MainAccount = acc };
    }

    private static DebtRow Take(string date, decimal liters, decimal fee, FuelType fuel = FuelType.Petrol) =>
        new() { DateShamsi = date, Liters = liters, PricePerLiter = fee, Bardagi = liters * fee, Fuel = fuel };

    private static DebtRow Receipt(string date, decimal liters, FuelType fuel = FuelType.Petrol, long? inv = null) =>
        new() { DateShamsi = date, Liters = 0m, RasidFuel = liters, Fuel = fuel, InvoiceId = inv };

    private static RateHistoryEntry Rate(string date, decimal rate, long id, FuelType fuel = FuelType.Petrol) =>
        new() { Id = id, DateShamsi = date, Rate = rate, Fuel = fuel };

    private static PriceLossReport Build(IEnumerable<Debtor> people, IEnumerable<RateHistoryEntry> rates,
                                         IEnumerable<Invoice>? inv = null, decimal nowP = 0m, decimal nowD = 0m) =>
        new PriceLossService().Build(people.ToList(), (inv ?? Array.Empty<Invoice>()).ToList(),
                                     rates.ToList(), nowP, nowD, Today);

    [Fact] // زیان = (نرخِ اتحادیه در روزِ رسید − نرخِ برداشت) × لیتر
    public void LossIsUnionRateOnReceiptDayMinusSaleRate_TimesLiters()
    {
        var p = Person("احمد", 1, Take("1405/06/01", 100m, 60m), Receipt("1405/06/20", 100m));
        var r = Build(new[] { p }, new[] { Rate("1405/06/10", 70m, 1) });

        var x = Assert.Single(r.List);
        var part = Assert.Single(Assert.Single(x.Items).Parts);
        Assert.Equal(100m, part.Qty);
        Assert.Equal(70m, part.Ref);
        Assert.Equal(1000m, part.Loss);            // (70 − 60) × 100
        Assert.Equal(6000m, x.T.Principal);
        Assert.Equal(7000m, x.T.RefValue);
        Assert.Equal(1000m, r.G.Loss);
        Assert.Equal(1, r.G.LossPersons);
    }

    [Fact] // برداشت و رسیدِ همان روز ⇒ زیان صفر، حتی اگر نرخ بالاتر باشد
    public void SameDayReceipt_HasNoLoss()
    {
        var p = Person("ب", 2, Take("1405/06/05", 50m, 60m), Receipt("1405/06/05", 50m));
        var r = Build(new[] { p }, new[] { Rate("1405/06/01", 90m, 1) });

        var part = Assert.Single(Assert.Single(r.List).Items[0].Parts);
        Assert.True(part.SameDay);
        Assert.Equal(0m, part.Loss);
        Assert.Equal(1, r.List[0].T.SameDayN);
    }

    [Fact] // نرخِ روزِ رسیدِ کمتر یا برابر ⇒ زیان صفر (نه عددِ منفی)
    public void LowerOrEqualRate_IsZeroNotNegative()
    {
        var p = Person("پ", 3, Take("1405/06/01", 10m, 60m), Receipt("1405/06/20", 10m));
        var r = Build(new[] { p }, new[] { Rate("1405/06/02", 55m, 1) });
        Assert.Equal(0m, r.G.Loss);
        Assert.Equal(0, r.G.LossPersons);
    }

    [Fact] // نرخِ یک روز = آخرین نرخی که تا آن روز ثبت شده — نه نرخِ بعد از آن، نه نرخِ امروز
    public void RateOnDay_IsTheLastChangeOnOrBeforeThatDay()
    {
        var p = Person("ت", 4, Take("1405/05/01", 10m, 50m), Receipt("1405/05/15", 10m));
        var rates = new[] { Rate("1405/05/10", 65m, 1), Rate("1405/05/20", 80m, 2), Rate("1405/05/12", 66m, 3) };
        var r = Build(new[] { p }, rates, nowP: 100m);

        var part = r.List[0].Items[0].Parts[0];
        Assert.Equal(66m, part.Ref);
        Assert.Equal("1405/05/12", part.RefOn);
        Assert.Equal(160m, part.Loss);
    }

    [Fact] // تا آن تاریخ هیچ نرخی ثبت نشده ⇒ هیچ نرخِ تخمینی؛ در هیچ جمعی نیست
    public void MissingRate_IsNotCountedAndNoEstimateIsMade()
    {
        var p = Person("ث", 5, Take("1405/06/01", 10m, 60m), Receipt("1405/06/20", 10m));
        var r = Build(new[] { p }, new[] { Rate("1405/07/01", 90m, 1) }, nowP: 90m);

        var part = r.List[0].Items[0].Parts[0];
        Assert.True(part.NoRate);
        Assert.Equal(0m, part.Loss);
        Assert.Equal(0m, r.G.Loss);
        Assert.Equal(0m, r.G.Fuel);
        Assert.Equal(1, r.G.NoRateN);
    }

    [Fact] // امروز، نرخِ اتحادیهٔ همین لحظه خودش رکوردِ همین تاریخ است
    public void ReceiptToday_UsesTodaysUnionRateWhenHistoryHasNothing()
    {
        var p = Person("ج", 6, Take("1405/06/01", 10m, 60m), Receipt(Today, 10m));
        var r = Build(new[] { p }, Array.Empty<RateHistoryEntry>(), nowP: 75m);
        Assert.Equal(150m, r.G.Loss);
    }

    [Fact] // قدیمی‌ترین قرض اول تسویه می‌شود و «نصفه» هم پذیرفته می‌شود
    public void Receipts_SettleOldestFirst_AndPartially()
    {
        var p = Person("چ", 7,
            Take("1405/06/03", 40m, 60m),
            Take("1405/06/01", 30m, 60m),
            Receipt("1405/06/20", 50m));
        var r = Build(new[] { p }, new[] { Rate("1405/06/10", 70m, 1) });

        var items = r.List[0].Items;                 // به ترتیبِ تاریخ
        Assert.Equal("1405/06/01", items[0].Date);
        Assert.Equal(30m, items[0].Parts[0].Qty);
        Assert.Equal(0m, items[0].Remain);
        Assert.Equal(20m, items[1].Parts[0].Qty);    // فقط ۲۰ از ۴۰ تسویه شد
        Assert.Equal(20m, items[1].Remain);
        Assert.Equal(20m, r.G.OpenFuel);
        Assert.Equal(500m, r.G.Loss);                // ۵۰ لیتر × ۱۰
    }

    [Fact] // یک برداشت با دو رسیدِ جدا: هر تکه نرخِ روزِ رسیدِ خودش را می‌گیرد
    public void OneWithdrawal_TwoReceipts_EachPartHasItsOwnRate()
    {
        var p = Person("ح", 8, Take("1405/06/01", 100m, 60m),
                                Receipt("1405/06/10", 40m), Receipt("1405/06/20", 60m));
        var rates = new[] { Rate("1405/06/05", 65m, 1), Rate("1405/06/15", 70m, 2) };
        var r = Build(new[] { p }, rates);

        var parts = r.List[0].Items[0].Parts;
        Assert.Equal(2, parts.Count);
        Assert.Equal(200m, parts[0].Loss);           // ۴۰ × ۵
        Assert.Equal(600m, parts[1].Loss);           // ۶۰ × ۱۰
        Assert.Equal(800m, r.G.Loss);
    }

    [Fact] // رسیدِ داخلِ خودِ ردیف با تاریخِ همان ردیف حساب می‌شود ⇒ رسیدِ همان روز
    public void ReceiptWrittenInsideTheRow_CountsOnTheRowsOwnDay()
    {
        var row = Take("1405/06/01", 10m, 60m);
        row.RasidFuel = 10m;
        var r = Build(new[] { Person("خ", 9, row) }, new[] { Rate("1405/05/01", 90m, 1) });
        var part = Assert.Single(r.List[0].Items[0].Parts);
        Assert.True(part.SameDay);
        Assert.False(part.Hdr);
        Assert.Equal(0m, r.G.Loss);
    }

    [Fact] // دفترِ پول: رسید افغانی است و با فیِ همان ردیف به لیتر برمی‌گردد
    public void MoneyLedger_ConvertsAfghaniReceiptToLitersWithTheRowsFee()
    {
        var acc = new DebtAccount { Id = 100, MainOfDebtorId = 10, ReceiptsMigrated = true, Mode = LedgerMode.Money };
        acc.MoneyRows.Add(new DebtRow { DateShamsi = "1405/06/01", Liters = 10m, Bardagi = 600m, ByMoney = true, Fuel = FuelType.Petrol });
        acc.MoneyRows.Add(new DebtRow { DateShamsi = "1405/06/20", Liters = 0m, Rasid = 300m, Fuel = FuelType.Petrol });
        var p = new Debtor { Id = 10, Name = "د", MainAccount = acc };
        var r = Build(new[] { p }, new[] { Rate("1405/06/10", 70m, 1) });

        var part = Assert.Single(r.List[0].Items[0].Parts);
        Assert.Equal(5m, part.Qty);                  // ۳۰۰ ÷ ۶۰
        Assert.Equal(50m, part.Loss);                // ۵ × ۱۰
        Assert.Equal(5m, r.List[0].T.OpenFuel);
    }

    [Fact] // پطرول و دیزل دو استخرِ جدا هستند — رسیدِ دیزل روی قرضِ پطرول نمی‌نشیند
    public void PetrolAndDieselReceipts_NeverMix()
    {
        var p = Person("ذ", 11, Take("1405/06/01", 10m, 60m, FuelType.Petrol),
                                Receipt("1405/06/20", 10m, FuelType.Diesel));
        var r = Build(new[] { p }, new[] { Rate("1405/06/10", 70m, 1), Rate("1405/06/10", 70m, 2, FuelType.Diesel) });
        Assert.Equal(10m, r.G.OpenFuel);
        Assert.Equal(0m, r.G.Loss);
    }

    [Fact] // حسابِ قدیمیِ مهاجرت‌نکرده: عددِ سربرگ منهای سهمِ فاکتورها — و بی‌تاریخ، پس بی‌نرخ
    public void UnmigratedAccount_UsesHeaderNumberMinusInvoiceShare_Dateless()
    {
        var acc = new DebtAccount { Id = 120, MainOfDebtorId = 12, ReceiptsMigrated = false, RasidFuelPetrol = 30m };
        acc.FuelRows.Add(Take("1405/06/01", 30m, 60m));
        acc.FuelRows.Add(Receipt("1405/06/15", 10m, inv: 5));
        var inv = new Invoice { Id = 5, InvoiceNumber = 5, Status = InvoiceStatus.Approved, Liters = 10m,
                                PricePerLiter = 60m, RateOnCreate = 60m, RateOnApprove = 60m, DebtAccountId = 120 };
        var p = new Debtor { Id = 12, Name = "ر", MainAccount = acc };
        var r = Build(new[] { p }, new[] { Rate("1405/06/10", 70m, 1) }, new[] { inv });

        var parts = r.List[0].Items[0].Parts;
        Assert.Equal(2, parts.Count);
        Assert.Equal(10m, parts.Single(x => x.InvNo == 5).Qty);     // رسیدِ فاکتور، با تاریخِ خودش
        var head = parts.Single(x => x.InvNo == 0);
        Assert.Equal(20m, head.Qty);                                 // ۳۰ − ۱۰
        Assert.True(head.NoRate);                                    // تاریخ ندارد
        Assert.Equal(100m, r.G.Loss);                                // فقط تکهٔ فاکتوری: ۱۰ × ۱۰
    }

    [Fact] // حسابِ مهاجرت‌کرده عددِ سربرگش را دوباره نمی‌شمارد
    public void MigratedAccount_IgnoresHeaderNumber()
    {
        var acc = new DebtAccount { Id = 130, MainOfDebtorId = 13, ReceiptsMigrated = true, RasidFuelPetrol = 999m };
        acc.FuelRows.Add(Take("1405/06/01", 30m, 60m));
        var r = Build(new[] { new Debtor { Id = 13, Name = "ز", MainAccount = acc } }, new[] { Rate("1405/06/10", 70m, 1) });
        Assert.Equal(30m, r.G.OpenFuel);
        Assert.Empty(r.List[0].Items[0].Parts);
    }

    [Fact] // فاکتورِ تاییدشده: (نرخِ تایید − نرخِ ثبت) × لیتر؛ در صف فقط پیش‌نمایش
    public void InvoiceRateLoss_IsApproveMinusCreate_PendingIsPreviewOnly()
    {
        var p = new Debtor { Id = 14, Name = "س", MainAccount = new DebtAccount { Id = 140, MainOfDebtorId = 14, ReceiptsMigrated = true } };
        var ok = new Invoice { Id = 1, InvoiceNumber = 1, Status = InvoiceStatus.Approved, Liters = 100m,
                               PricePerLiter = 60m, RateOnCreate = 60m, RateOnApprove = 70m, DebtAccountId = 140 };
        var pend = new Invoice { Id = 2, InvoiceNumber = 2, Status = InvoiceStatus.Pending, Liters = 50m,
                                 PricePerLiter = 60m, RateOnCreate = 60m, CustomerName = "س" };
        var r = Build(new[] { p }, Array.Empty<RateHistoryEntry>(), new[] { ok, pend }, nowP: 65m);

        var x = Assert.Single(r.List);
        Assert.Equal(2, x.Inv.Count);                // یکی با شناسهٔ حساب، یکی فقط با نام — هر دو مالِ همین شخص
        Assert.Equal(1000m, x.Iv.Loss);
        Assert.Equal(1, x.Iv.N);
        Assert.Equal(1, x.Iv.PendN);
        Assert.Equal(250m, x.Iv.PendLoss);           // ۵۰ × ۵ — در هیچ جمعی نیست
        Assert.Equal(1000m, r.G.InvLoss);
        Assert.Equal(250m, r.G.PendLoss);
        Assert.Equal(1, r.G.LossPersons);
    }

    [Fact] // پایین آمدنِ نرخ تا روزِ تایید «مفاد» است، نه زیانِ منفی
    public void InvoiceRateDrop_IsGainNotNegativeLoss()
    {
        var inv = new Invoice { Id = 1, InvoiceNumber = 1, Status = InvoiceStatus.Approved, Liters = 10m,
                                PricePerLiter = 70m, RateOnCreate = 70m, RateOnApprove = 60m, CustomerName = "ش" };
        var r = Build(Array.Empty<Debtor>(), Array.Empty<RateHistoryEntry>(), new[] { inv });
        Assert.Equal(0m, r.G.InvLoss);
        Assert.Equal(100m, r.G.InvGain);
        Assert.Equal(0, r.G.LossPersons);
    }

    [Fact] // نامی که فقط فاکتور دارد و هنوز حسابِ قرض‌داری ندارد، با کلیدِ «name:» می‌آید
    public void InvoiceOnlyName_AppearsWithoutAnAccount()
    {
        var inv = new Invoice { Id = 1, InvoiceNumber = 7, Status = InvoiceStatus.Pending, Liters = 10m,
                                PricePerLiter = 60m, CustomerName = "  مشتریِ  تازه " };
        var r = Build(Array.Empty<Debtor>(), Array.Empty<RateHistoryEntry>(), new[] { inv });
        var x = Assert.Single(r.List);
        Assert.Null(x.Id);
        Assert.StartsWith("name:", x.Key);
        Assert.Equal("  مشتریِ  تازه ", x.Name);
        Assert.Empty(x.Items);
    }

    [Fact] // فاکتورِ پولی و فاکتورِ بی‌لیتر در گزارش نیستند
    public void MoneyOrZeroLiterInvoices_AreSkipped()
    {
        var a = new Invoice { Id = 1, InvoiceNumber = 1, ByMoney = true, Liters = 10m, PricePerLiter = 60m, CustomerName = "ص" };
        var b = new Invoice { Id = 2, InvoiceNumber = 2, Liters = 0m, PricePerLiter = 60m, CustomerName = "ض" };
        var r = Build(Array.Empty<Debtor>(), Array.Empty<RateHistoryEntry>(), new[] { a, b });
        Assert.Empty(r.List);
    }

    [Fact] // مرتب‌سازی: بیشترین زیان اول
    public void List_IsSortedByTotalLossDescending()
    {
        var small = Person("کم", 20, Take("1405/06/01", 10m, 60m), Receipt("1405/06/20", 10m));
        var big = Person("زیاد", 21, Take("1405/06/01", 100m, 60m), Receipt("1405/06/20", 100m));
        var r = Build(new[] { small, big }, new[] { Rate("1405/06/10", 70m, 1) });
        Assert.Equal("زیاد", r.List[0].Name);
        Assert.Equal("کم", r.List[1].Name);
    }

    [Fact] // فیلترِ «پرداخت‌نشده / پرداخت‌شده» — همان ‎openFuel > 0‎ و ‎paidN > 0‎ی سایت
    public void OpenAndPaidFlags_FollowTheSitesRule()
    {
        var open = Person("باز", 30, Take("1405/06/01", 10m, 60m));
        var paid = Person("بسته", 31, Take("1405/06/01", 10m, 60m), Receipt("1405/06/20", 10m));
        var r = Build(new[] { open, paid }, new[] { Rate("1405/06/10", 70m, 1) });
        var o = r.List.Single(x => x.Name == "باز");
        var c = r.List.Single(x => x.Name == "بسته");
        Assert.True(o.HasOpenFuel); Assert.False(o.HasPaid);
        Assert.False(c.HasOpenFuel); Assert.True(c.HasPaid);
    }

    [Fact] // نرخِ برداشت: بردگی ÷ لیتر، و اگر بردگی نبود فیِ دستی؛ بی هر دو ⇒ «بی‌نرخ»
    public void SaleRate_IsBardagiOverLiters_ThenFee_ElseNoSale()
    {
        Assert.Equal(65m, PriceLossService.SaleRate(new DebtRow { Liters = 10m, Bardagi = 650m, PricePerLiter = 60m }));
        Assert.Equal(60m, PriceLossService.SaleRate(new DebtRow { Liters = 10m, PricePerLiter = 60m }));
        Assert.Equal(0m, PriceLossService.SaleRate(new DebtRow { Liters = 10m }));

        var p = Person("ط", 40, new DebtRow { DateShamsi = "1405/06/01", Liters = 10m, Fuel = FuelType.Petrol },
                       Receipt("1405/06/20", 10m));
        var r = Build(new[] { p }, new[] { Rate("1405/06/10", 70m, 1) });
        var part = Assert.Single(r.List[0].Items[0].Parts);
        Assert.True(part.NoSale);
        Assert.Equal(0m, r.G.Loss);
        Assert.Equal(1, r.G.NoRateN);
    }

    [Fact] // حسابِ فرعی هم شمرده می‌شود و نامش کنارِ ردیف می‌نشیند
    public void SubAccounts_AreIncludedWithTheirLabel()
    {
        var p = Person("ظ", 50);
        var sub = new DebtAccount { Id = 501, DebtorId = 50, LegacySubId = "s1", Name = "موترِ دوم", ReceiptsMigrated = true };
        sub.FuelRows.Add(Take("1405/06/01", 10m, 60m));
        sub.FuelRows.Add(Receipt("1405/06/20", 10m));
        p.SubAccounts.Add(sub);
        var r = Build(new[] { p }, new[] { Rate("1405/06/10", 70m, 1) });
        var it = Assert.Single(r.List[0].Items);
        Assert.Equal("موترِ دوم", it.Acct);
        Assert.Equal(100m, r.G.Loss);
    }

    [Fact] // این سرویس هیچ چیزی را عوض نمی‌کند — ردیف‌ها و حساب بعد از گزارش همان‌اند
    public void Build_DoesNotMutateAnything()
    {
        var row = Take("1405/06/01", 10m, 60m);
        var rc = Receipt("1405/06/20", 10m);
        var p = Person("ع", 60, row, rc);
        var before = System.Text.Json.JsonSerializer.Serialize(new { row.Liters, row.RasidFuel, row.Rasid, row.Bardagi, Rc = rc.RasidFuel, Head = p.MainAccount.RasidFuelPetrol });
        Build(new[] { p }, new[] { Rate("1405/06/10", 70m, 1) });
        var after = System.Text.Json.JsonSerializer.Serialize(new { row.Liters, row.RasidFuel, row.Rasid, row.Bardagi, Rc = rc.RasidFuel, Head = p.MainAccount.RasidFuelPetrol });
        Assert.Equal(before, after);
    }
}
