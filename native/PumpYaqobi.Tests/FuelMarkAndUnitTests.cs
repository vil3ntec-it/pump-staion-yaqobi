using PumpYaqobi.Application.Services;
using PumpYaqobi.Domain.Enums;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ «پ» و «د»ی خالی، و واحدی که خودش پیدا می‌شود ══════════════════════════
///
/// خواستهٔ صریحِ صاحب ریپو (۱۴۰۵/۰۷/۰۶): «از داخلِ بخشِ ورق‌ها از تراکنش‌ها
/// نوعِ تیل رو حذف کن… ولی اگر توی نام پطرول یا دیزل نوشتم توی حسابِ یارو
/// همان انتخاب بشه اتومات… و اگه «پ» خالی نوشتم پطرول ذخیره بشه و اگه «د»
/// خالی نوشتم هم دیزل — هر جای جمله، وسط یا اول یا آخر، فرقی نکنه. و توی
/// نام‌ها و توضیحاتِ همان حساب دیده نشه… و سیستم اتومات تشخیص بده که واحدِ
/// این حساب تیل است یا پول، و اگه هر دو بود این تغییر نخوره.»
/// </summary>
public class FuelMarkAndUnitTests
{
    // ══ ۱) نشانه، هر جای جمله ═══════════════════════════════════════════════

    [Theory]
    [InlineData("د هارون", FuelType.Diesel)]
    [InlineData("هارون د", FuelType.Diesel)]
    [InlineData("محمد هارون د جان", FuelType.Diesel)]
    [InlineData("پ هارون", FuelType.Petrol)]
    [InlineData("هارون پ", FuelType.Petrol)]
    [InlineData("کریم پ جان", FuelType.Petrol)]
    [InlineData("هارون دیزل", FuelType.Diesel)]
    [InlineData("دیزل هارون", FuelType.Diesel)]
    [InlineData("هارون پطرول", FuelType.Petrol)]
    public void ABareMarkAnywhereInTheSentenceIsEnough(string text, FuelType expected)
        => Assert.Equal(expected, PostingService.DetectFuelType(text));

    /// <summary>
    /// ⛔ <b>و فقط وقتی خودش یک واژهٔ کامل است.</b> این مهم‌ترین بندِ این فایل
    /// است: با سنجشِ زیررشته‌ای، «د»ی داخلِ «داوود» و «پ»ی داخلِ «پرویز»
    /// می‌گرفت و نامِ نیمِ قرض‌دارها «دیزل» یا «پطرول» خوانده می‌شد — و بدتر،
    /// یک حرف از نامِ نمایشیِ حسابشان هم پاک می‌شد.
    /// </summary>
    [Theory]
    [InlineData("داوود")]
    [InlineData("پرویز")]
    [InlineData("محمد داوود خان")]
    [InlineData("پروین")]
    [InlineData("احمد")]
    public void ALetterInsideANameIsNotAFuelMark(string name)
    {
        Assert.False(PostingService.MentionsFuel(name));
        Assert.Equal(name, PostingService.StripFuelWords(name));
    }

    /// <summary>واژهٔ کامل جلوتر از نشانهٔ تک‌حرفی است.</summary>
    [Fact]
    public void AFullWordBeatsABareMark()
    {
        Assert.Equal(FuelType.Diesel, PostingService.DetectFuelType("دیزل پ هارون"));
        Assert.Equal(FuelType.Petrol, PostingService.DetectFuelType("پطرول د هارون"));
    }

    // ══ ۲) نشانه در نامِ حساب دیده نمی‌شود ═══════════════════════════════════

    [Theory]
    [InlineData("د هارون", "هارون")]
    [InlineData("هارون د", "هارون")]
    [InlineData("محمد هارون د جان", "محمد هارون جان")]
    [InlineData("پ کریم", "کریم")]
    [InlineData("هارون دیزل", "هارون")]
    [InlineData("پطرول کریم جان", "کریم جان")]
    public void TheMarkNeverReachesTheAccountName(string typed, string shown)
        => Assert.Equal(shown, PostingService.StripFuelWords(typed));

    /// <summary>و ارقامِ فارسی هم همان‌جا نرمال می‌شوند.</summary>
    [Fact]
    public void APersianDigitDoesNotBreakTheMark()
        => Assert.Equal(FuelType.Diesel, PostingService.DetectFuelType("هارون د ۱۲۰"));

    // ══ ۳) واحدِ حساب ═══════════════════════════════════════════════════════

    /// <summary>فقط دفترِ تیل ردیف دارد ⇒ تیل.</summary>
    [Fact]
    public void AnAccountWithOnlyFuelRowsMeansFuel()
        => Assert.Equal(LedgerMode.Fuel,
                        PostingService.UnitForAccount(true, false, LedgerMode.Money));

    /// <summary>فقط دفترِ پول ردیف دارد ⇒ پول.</summary>
    [Fact]
    public void AnAccountWithOnlyMoneyRowsMeansMoney()
        => Assert.Equal(LedgerMode.Money,
                        PostingService.UnitForAccount(false, true, LedgerMode.Fuel));

    /// <summary>
    /// ⛔ <b>هر دو ⇒ دست نزن.</b> همان «اگه هر دو بود… این تغییر نخوره و
    /// میرزا خودش تغییر بده». حدس زدنش یعنی یک قرضِ تیل در دفترِ پول.
    /// </summary>
    [Fact]
    public void AnAccountWithBothLedgersIsLeftAlone()
    {
        Assert.Null(PostingService.UnitForAccount(true, true, LedgerMode.Fuel));
        Assert.Null(PostingService.UnitForAccount(true, true, LedgerMode.Money));
    }

    /// <summary>حسابِ خالی، واحدِ اعلامیِ خودش را می‌دهد.</summary>
    [Theory]
    [InlineData(LedgerMode.Fuel)]
    [InlineData(LedgerMode.Money)]
    public void AnEmptyAccountFallsBackToItsOwnDeclaredUnit(LedgerMode mode)
        => Assert.Equal(mode, PostingService.UnitForAccount(false, false, mode));
}
