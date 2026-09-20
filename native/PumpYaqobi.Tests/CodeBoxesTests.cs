using PumpYaqobi.App.Services;
using PumpYaqobi.App.ViewModels;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ شش خانهٔ کد ═════════════════════════════════════════════════════════
///
/// بندِ ۴ی پرامپتِ ۲۲: «شش خانه، پرشِ خودکار، Paste، ارقامِ فارسی/انگلیسی،
/// ارسالِ خودکار بعد از رقمِ ششم.»
///
/// ⚠️ همهٔ تصمیم‌ها در ویومدل است و نه در صفحه — چون رفتارِ سنجیده‌نشده
/// همان است که روزی بی‌صدا می‌شکند. صفحه فقط فوکوس را جابه‌جا می‌کند.
/// </summary>
public class CodeBoxesTests
{
    private static CodeBoxesViewModel New() => new();

    [Fact]
    public void Har_Raqam_Focus_Ra_Yek_Khane_Jelo_Mibarad()
    {
        var vm = New();
        Assert.Equal(1, vm.Put(0, "1"));
        Assert.Equal(2, vm.Put(1, "2"));
        Assert.Equal("12", vm.Code);
        Assert.Equal("1", vm.B1);
        Assert.Equal("2", vm.B2);
        Assert.False(vm.Full);
    }

    /// <summary>
    /// ⛔ <b>ارقامِ فارسی و عربی هم قبول‌اند.</b> کاربری که صفحه‌کلیدش فارسی
    /// است «۱۲۳۴۵۶» می‌زند؛ بی این، کدش شش نویسهٔ ناشناخته می‌شد و سرور
    /// «کد اشتباه است» می‌گفت — بدترین شکلِ خطا، چون کاربر مطمئن است درست
    /// زده.
    /// </summary>
    [Theory]
    [InlineData("۱۲۳۴۵۶")]
    [InlineData("١٢٣٤٥٦")]
    [InlineData("123456")]
    public void Raqamhaye_Farsi_Va_Arabi_Ham_Ghabool_And(string typed)
    {
        var vm = New();
        vm.Put(0, typed);
        Assert.Equal("123456", vm.Code);
        Assert.True(vm.Full);
    }

    /// <summary>
    /// ⚠️ چسباندن همیشه از خانهٔ <b>اول</b> پخش می‌شود، نه از جایی که
    /// چسبیده — کاربری که کد را از ایمیل کپی می‌کند به خانهٔ اول کلیک
    /// نمی‌کند.
    /// </summary>
    [Fact]
    public void Paste_Az_Har_Khane_Ei_Az_Aval_Pakhsh_Mishavad()
    {
        var vm = New();
        vm.Put(3, "  98-76-54  ");
        Assert.Equal("987654", vm.Code);
        Assert.Equal("9", vm.B1);
        Assert.Equal("4", vm.B6);
    }

    /// <summary>ارسالِ خودکار پس از رقمِ ششم — و فقط یک بار برای هر پر شدن.</summary>
    [Fact]
    public void Baad_Az_Raqame_Sheshom_Khodash_Khabar_Midahad()
    {
        var vm = New();
        var fired = 0;
        vm.Completed += () => fired++;

        for (var i = 0; i < 5; i++) vm.Put(i, (i + 1).ToString());
        Assert.Equal(0, fired);      // هنوز پنج تا

        vm.Put(5, "6");
        Assert.Equal(1, fired);
        Assert.Equal("123456", vm.Code);
    }

    /// <summary>
    /// ⚠️ خانهٔ وسطیِ خالی یعنی کاربر هنوز کارش تمام نشده — ارسالِ زودهنگام
    /// یکی از پنج تلاشِ سرور را می‌سوزاند.
    /// </summary>
    [Fact]
    public void Khaneye_Khali_Dar_Miane_Ersal_Nemikonad()
    {
        var vm = New();
        var fired = 0;
        vm.Completed += () => fired++;

        vm.Put(0, "1"); vm.Put(1, "2"); vm.Put(3, "4"); vm.Put(4, "5"); vm.Put(5, "6");
        Assert.Equal(0, fired);
        Assert.False(vm.Full);
    }

    /// <summary>خالی کردنِ یک خانه یعنی پاک کردن و یک خانه عقب.</summary>
    [Fact]
    public void Backspace_Pak_Mikonad_Va_Aghab_Miravad()
    {
        var vm = New();
        vm.Put(0, "1"); vm.Put(1, "2");
        Assert.Equal(0, vm.Put(1, ""));
        Assert.Equal("1", vm.Code);
        Assert.Equal("", vm.B2);
    }

    [Fact]
    public void Clear_Hame_Ra_Mibarad()
    {
        var vm = New();
        vm.Fill("123456");
        Assert.True(vm.Full);
        vm.Clear();
        Assert.Equal("", vm.Code);
        Assert.Equal(0, vm.Focus);
    }

    /// <summary>نویسهٔ غیرِ رقم اصلاً نمی‌نشیند.</summary>
    [Fact]
    public void Neveshteye_Gheyre_Raqam_Nemineshinad()
    {
        Assert.Equal("", LoginRules.Digits("abc-خ ط"));
        Assert.Equal("1405", LoginRules.Digits("۱۴۰۵"));
    }
}
