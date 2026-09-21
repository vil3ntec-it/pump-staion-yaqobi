using PumpYaqobi.App.ViewModels;
using PumpYaqobi.Application.Localization;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ ماه و سال: دقیق کار می‌کنند؟ ═══════════════════════════════════════════
///
/// پرسشِ صاحب ریپو (۱۴۰۵/۰۷/۰۷): «بخش‌هایی که ماه و سال دارند با دقت کار
/// می‌کنند؟ عوض کردنِ تاریخ از کامپیوتر یا جای دیگر به‌هم که نمی‌خورد؟ و
/// اتومات ماه که عوض بشه، ماه هم جدید می‌شود با حساب‌های ماهِ جدید می‌آید یا
/// که نه؟»
///
/// سه جواب، و هر سه این‌جا قفل شده‌اند.
/// </summary>
public class MonthRolloverTests
{
    /// <summary>
    /// ⛔ <b>ماهِ هر ردیف از تاریخِ خودِ همان ردیف درمی‌آید</b>، نه از ساعتِ
    /// کامپیوتر. پس عوض شدنِ تاریخِ سیستم هیچ ردیفی را از ماهش بیرون
    /// نمی‌برد — دفترِ پارسال پارسال می‌ماند.
    /// </summary>
    [Fact]
    public void MaheHarRadif_AzTarikheKhodash_MiAyad()
    {
        Assert.Equal("1404/05", Shamsi.MonthKey("1404/05/12"));
        Assert.Equal("1403/12", Shamsi.MonthKey("1403/12/30"));

        // ارقامِ فارسی هم همان جواب را می‌دهند
        Assert.Equal("1404/05", Shamsi.MonthKey("۱۴۰۴/۰۵/۱۲"));

        // تاریخِ بی‌معنا هیچ ماهی نمی‌سازد (نه «ماهِ امروز»)
        Assert.Equal("", Shamsi.MonthKey(""));
        Assert.Equal("", Shamsi.MonthKey("سلام"));
    }

    /// <summary>
    /// نیمه‌شبِ آخرِ ماه: بخشی که روی ماهِ خودکار بود به ماهِ تازه می‌رود —
    /// و بخشی که کاربر خودش ماهش را برگزیده، دست نمی‌خورد.
    /// </summary>
    [Fact]
    public void NimeShabeAkhareMah_FaghatMaheKhodkar_Jelo_Miravad()
    {
        // همان ماه ⇒ هیچ کاری
        Assert.False(MonthRoll.Decide("1405/07", "1405/07", "1405/07"));

        // ماه عوض شد و کاربر همان ماهِ خودکار را می‌دید ⇒ برو ماهِ تازه
        Assert.True(MonthRoll.Decide("1405/07", "1405/07", "1405/08"));

        // ⛔ کاربر خودش ماهِ دیگری را برگزیده ⇒ زیرِ دستش عوض نمی‌شود
        Assert.False(MonthRoll.Decide("1404/03", "1405/07", "1405/08"));

        // ⛔ «همهٔ ماه‌ها» هم یک انتخابِ کاربر است
        Assert.False(MonthRoll.Decide("1405/*", "1405/07", "1405/08"));
        Assert.False(MonthRoll.Decide("", "1405/07", "1405/08"));

        // سالِ تازه هم همان قاعده
        Assert.True(MonthRoll.Decide("1405/12", "1405/12", "1406/01"));
    }

    /// <summary>سیم‌کشی: تیکِ نیمه‌شب واقعاً به بخش‌ها می‌رسد.</summary>
    [Fact]
    public void TikeNimeShab_BeBakhshha_Miresad()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        string Read(params string[] parts) =>
            File.ReadAllText(Path.Combine(new[] { root }.Concat(parts).ToArray()));

        var mv = Read("PumpYaqobi.App", "ViewModels", "MainViewModel.cs");
        Assert.Contains("foreach (var p in AllPages) p.OnDayChanged();", mv);

        var sec = Read("PumpYaqobi.App", "ViewModels", "SectionViewModel.cs");
        Assert.Contains("public virtual void OnDayChanged() { }", sec);

        var led = Read("PumpYaqobi.App", "ViewModels", "LedgerSectionViewModel.cs");
        Assert.Contains("public override void OnDayChanged()", led);
        Assert.Contains("MonthRoll.Decide(Month, _autoMonth, now)", led);
        //  ⚠️ مهرِ «ماهِ خودکار» در سازنده گذاشته می‌شود، وگرنه دورِ اول همیشه
        //  «عوض شد» دیده می‌شد.
        Assert.Contains("_month = _autoMonth = Shamsi.ThisMonth();", led);
    }
}
