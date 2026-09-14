using PumpYaqobi.App.Services;
using PumpYaqobi.Application.Localization;
using PumpYaqobi.Application.Services;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;
using Xunit;

namespace PumpYaqobi.Tests;

file sealed class NoRates2 : IUnionRateProvider
{ public decimal UnionRate(FuelType f) => 0m; }

/// <summary>
/// ══ کیو‌آر: هر دو دفتر، تفکیکِ تیل، و آرشیوِ ماه‌ها ══════════════════════════
///
/// گزارشِ صاحب ریپو (۱۴۰۵/۰۶/۲۴): «بعضی جدول‌ها یا کادرهای جدول‌ها نیست؛ یارو
/// نمی‌تواند ببیند که واحدِ پول چقدر قرض‌دار است یا تیل چقدر… شرکت‌ها هم
/// نمی‌دانند دیزل چقدر از من می‌خواهند یا پطرول چقدر… و آرشیوها یادم نرود، و
/// ماه و سال.»
///
/// ریشه‌اش این بود که ‎ForDebtAccount‎ فقط ‎ActiveRows()‎ را می‌برد. هر حساب دو
/// دفترِ جدا دارد (‎FuelRows‎ و ‎MoneyRows‎) و دفترِ غیرفعال اصلاً داخلِ کد
/// نمی‌رفت — مشتری نیمِ حسابش را می‌دید و خبر نداشت نیمهٔ دیگری هست.
/// </summary>
public class AcctBooksTests
{
    private static DebtCalculationService Calc() => new(new NoRates2());
    private static decimal Num(string t) => Shamsi.Num(t);
    private static string Box(AcctBook b, string label) => b.Summary.First(x => x[0] == label)[1];

    private static DebtAccount TwoLedgers()
    {
        var a = new DebtAccount { Mode = LedgerMode.Fuel, PercentPetrol = 10m };
        a.FuelRows.Add(new DebtRow { DateShamsi = "1405/05/03", Fuel = FuelType.Petrol, Liters = 1000m, RasidFuel = 400m });
        a.FuelRows.Add(new DebtRow { DateShamsi = "1405/06/11", Fuel = FuelType.Diesel, Liters = 500m, RasidFuel = 100m });
        a.MoneyRows.Add(new DebtRow { DateShamsi = "1405/06/12", Fuel = FuelType.Petrol, Bardagi = 9000m, Rasid = 2000m });
        return a;
    }

    /// <summary>«واحدِ پول چقدر قرض‌دار است یا تیل چقدر» — هر دو در یک کد.</summary>
    [Fact]
    public void BothLedgersTravelInsideOneCode()
    {
        var snap = AcctSnapshots.ForDebtAccount("هارون", null, TwoLedgers(), Calc());

        Assert.Equal(2, snap.Books.Count);
        // دفترِ فعالِ حساب اول می‌آید — همان که روی کامپیوتر باز بود
        Assert.Equal("واحد تیل", snap.Books[0].Title);
        Assert.Equal("لیتر", snap.Books[0].Unit);
        Assert.Equal("واحد پول", snap.Books[1].Title);
        Assert.Equal("افغانی", snap.Books[1].Unit);

        Assert.Equal(1500m, Num(Box(snap.Books[0], "جمله بردگی")));
        Assert.Equal(9000m, Num(Box(snap.Books[1], "جمله بردگی")));
        Assert.Equal(2000m, Num(Box(snap.Books[1], "جمله رسید")));
    }

    /// <summary>
    /// جای قدیمی دست‌نخورده می‌ماند — کیو‌آرهایی که چاپ شده و دستِ مشتری است
    /// همان شکل را دارند و صفحهٔ ‎view/‎ باید هر دو را باز کند.
    /// </summary>
    [Fact]
    public void TheOldFlatShapeIsStillFilledFromTheActiveLedger()
    {
        var snap = AcctSnapshots.ForDebtAccount("هارون", null, TwoLedgers(), Calc());

        Assert.Equal(snap.Books[0].Head, snap.Head);
        Assert.Equal(snap.Books[0].Rows.Count, snap.Rows.Count);
        Assert.Equal(Box(snap.Books[0], "الباقی"), snap.Summary.First(x => x[0] == "الباقی")[1]);
    }

    /// <summary>دفترِ خالی تبِ خالی نمی‌سازد.</summary>
    [Fact]
    public void AnEmptySecondLedgerIsLeftOut()
    {
        var a = new DebtAccount { Mode = LedgerMode.Fuel };
        a.FuelRows.Add(new DebtRow { DateShamsi = "1405/06/01", Liters = 10m });

        var snap = AcctSnapshots.ForDebtAccount("هارون", null, a, Calc());
        Assert.Single(snap.Books);
    }

    /// <summary>«دیزل چقدر یا پطرول چقدر» — هر تیل ردیفِ خودش را دارد.</summary>
    [Fact]
    public void EachFuelGetsItsOwnRow()
    {
        var book = AcctSnapshots.ForDebtAccount("هارون", null, TwoLedgers(), Calc()).Books[0];

        Assert.Equal(2, book.Fuels.Count);
        Assert.Equal(5, book.FuelHead.Count);
        Assert.StartsWith("پطرول", book.Fuels[0][0]);
        Assert.StartsWith("دیزل", book.Fuels[1][0]);

        // پطرول: بردگی ۱۰۰۰، رسید ۴۰۰، فیصدی ۴۰، الباقی ۶۴۰
        Assert.Equal(1000m, Num(book.Fuels[0][1]));
        Assert.Equal(400m, Num(book.Fuels[0][2]));
        Assert.Equal(40m, Num(book.Fuels[0][3]));
        Assert.Equal(640m, Num(book.Fuels[0][4]));
        // دیزل فیصدی ندارد: 500 + 0 − 100
        Assert.Equal(400m, Num(book.Fuels[1][4]));
    }

    /// <summary>«در هر ماه چقدر برد یا تحویل داشت» — آرشیوِ ماه‌به‌ماه.</summary>
    [Fact]
    public void EveryMonthGetsItsOwnArchiveRow()
    {
        var book = AcctSnapshots.ForDebtAccount("هارون", null, TwoLedgers(), Calc()).Books[0];

        Assert.Equal(2, book.Archive.Count);
        Assert.Equal("1405/05", book.Archive[0][0]);
        Assert.Equal(1000m, Num(book.Archive[0][1]));
        Assert.Equal(400m, Num(book.Archive[0][2]));
        Assert.Equal("1405/06", book.Archive[1][0]);
        Assert.Equal(500m, Num(book.Archive[1][1]));
    }

    /// <summary>
    /// ⚠️ مهم‌ترینِ این آزمون‌ها: کیو‌آرِ بزرگ ردیف کم می‌کند، ولی آرشیو
    /// <b>هیچ‌وقت</b> کم نمی‌شود — وگرنه ماهی که ردیفش نرفته بود اصلاً از
    /// چشمِ مشتری می‌افتاد و نمی‌فهمید که هست.
    /// </summary>
    [Fact]
    public void TrimmingRowsNeverTrimsTheArchive()
    {
        var a = new DebtAccount { Mode = LedgerMode.Fuel };
        for (var i = 1; i <= 240; i++)
            a.FuelRows.Add(new DebtRow
            {
                DateShamsi = "1404/" + (i % 12 + 1).ToString("00") + "/0" + (i % 9 + 1),
                Name = "ردیفِ شمارهٔ " + i,
                Fuel = i % 2 == 0 ? FuelType.Diesel : FuelType.Petrol,
                Liters = 100m + i, RasidFuel = 50m,
            });

        var snap = AcctSnapshots.ForDebtAccount("هارون", null, a, Calc());
        var months = snap.Books[0].Archive.Count;
        var url = AcctView.Url("https://example.invalid/view/", snap);

        Assert.True(url.Length <= AcctView.MaxUrl, "نشانی از سقف گذشت: " + url.Length);

        var back = AcctView.Decode(url[(url.IndexOf("#d=", StringComparison.Ordinal) + 3)..]);
        Assert.NotNull(back);
        Assert.Single(back!.Books);
        Assert.True(back.Books[0].Rows.Count < 240, "ردیفی کم نشد، پس این آزمون چیزی را نمی‌سنجد");
        Assert.Equal(months, back.Books[0].Archive.Count);
        // ⚠️ ‎StringComparison.Ordinal‎ لازم است: ‎Assert.Contains‎ی رشته‌ای
        // پیش‌فرض با فرهنگِ جاری می‌سنجد و ICU کسرهٔ «آرشیوِ» را جور دیگری
        // می‌بیند — آزمون بی این، بی‌دلیل قرمز می‌شد.
        Assert.Contains("آرشیو", back.Books[0].Note, StringComparison.Ordinal);

        // و عکسِ خودِ صداکننده ناقص تحویل داده نشده
        Assert.Equal(240, snap.Books[0].Rows.Count);
        Assert.Equal(240, snap.Rows.Count);
    }

    /// <summary>شمارهٔ ستونِ تاریخ و تیل درست باشد — صفحه با همین فیلتر می‌کند.</summary>
    [Fact]
    public void TheDateAndFuelColumnsArePointedAtCorrectly()
    {
        var book = AcctSnapshots.ForDebtAccount("هارون", null, TwoLedgers(), Calc()).Books[0];

        Assert.Equal("تاریخ", book.Head[book.DateCol]);
        Assert.Equal("تیل", book.Head[book.FuelCol]);
        Assert.Equal("1405/05/03", book.Rows[0][book.DateCol]);
        Assert.Equal("پطرول", book.Rows[0][book.FuelCol]);
    }

    // ══════════════════════════════════════════════════════════════════════
    //  شرکتِ تیل
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>«شرکت‌ها نمی‌دانند دیزل چقدر از من می‌خواهند یا پطرول چقدر».</summary>
    [Fact]
    public void ACompanySeesPetrolAndDieselApart()
    {
        var c = new TilCompany { Name = "شرکتِ نمونه" };
        c.Rows.Add(new CompanyRow { DateShamsi = "1405/05/02", Fuel = FuelType.Petrol, Ton = 2m, Usd = 100m, Rate = 70m, Poul = 10_000m });
        c.Rows.Add(new CompanyRow { DateShamsi = "1405/06/09", Fuel = FuelType.Diesel, Ton = 1m, Usd = 200m, Rate = 70m });

        var snap = AcctSnapshots.ForCompany(c, new CompanyService());
        var book = Assert.Single(snap.Books);

        Assert.Equal(2, book.Fuels.Count);
        // پطرول: ۲ تن × ۱۰۰$ × ۷۰ = ۱۴٬۰۰۰ افغانی، ۱۰٬۰۰۰ پرداخت، ۴٬۰۰۰ الباقی
        Assert.Equal("پطرول", book.Fuels[0][0]);
        Assert.Equal(2m, Num(book.Fuels[0][1]));
        Assert.Equal(14_000m, Num(book.Fuels[0][2]));
        Assert.Equal(10_000m, Num(book.Fuels[0][3]));
        Assert.Equal(4_000m, Num(book.Fuels[0][4]));
        // دیزل: ۱ × ۲۰۰ × ۷۰ = ۱۴٬۰۰۰، هیچ پرداختی
        Assert.Equal(14_000m, Num(book.Fuels[1][4]));

        // و جمعِ دو تیل همان کلِ شرکت است
        Assert.Equal(28_000m, Num(Box(book, "کلِ افغانی")));
        Assert.Equal(18_000m, Num(Box(book, "الباقی")));

        Assert.Equal("تاریخ", book.Head[book.DateCol]);
        Assert.Equal("تیل", book.Head[book.FuelCol]);
        Assert.Equal(2, book.Archive.Count);
    }
}
