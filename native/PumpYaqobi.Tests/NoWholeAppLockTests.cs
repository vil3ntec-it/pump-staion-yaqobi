using PumpYaqobi.App.Services;
using PumpYaqobi.Application.Security;
using PumpYaqobi.Domain.Enums;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ اشتراک فقط شش بخش را می‌بندد، نه کلِ برنامه — و به پمپِ خودِ حساب می‌رسد ══
///
/// گزارشِ صاحب ریپو با عکس (۱۴۰۵/۰۷/۱۴): «من بخش‌های محدودی رو گفتم باید با
/// اشتراک کار کنن یا نکنن، ولی تو تمامِ برنامه رو قفل یا غیرِ قابلِ استفاده
/// کردی» — نوارِ «برنامه فقط‌خواندنی است» و «اجازهٔ این کار را ندارید:
/// EditData». و: «چرا اشتراک نمی‌رسه به برنامه؟» — پنل «آزمایشی · ۳۰ روز»،
/// برنامه «بدونِ اشتراکِ فعال».
///
/// رفتارِ دومی با سرورِ واقعی سنجیده می‌شود
/// (‎PUMP_SIGNUP_FOREIGN=1 … signuptrial‎)؛ این‌جا قاعده‌ها روی خودِ سورس.
/// </summary>
public class NoWholeAppLockTests
{
    private static string Src(string rel)
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null && !Directory.Exists(Path.Combine(d.FullName, "PumpYaqobi.App")))
            d = d.Parent;
        Assert.NotNull(d);
        return SrcText.Read(Path.Combine(d!.FullName, rel));
    }

    //  ↩ قاعدهٔ ۱۴۰۵/۰۷/۱۴ («هیچ حالی برنامه را فقط‌خواندنی نمی‌کند») به خواستهٔ
    //  تازهٔ صاحب ریپو (۱۴۰۵/۰۷/۲۰) پس گرفته شد — قاعدهٔ تازه در `AppLockTests`.
    //  آن‌چه از آن روز می‌ماند: فقط <b>نوشتن</b> بسته می‌شود، از <b>یک</b> در.
    [Fact]
    public void QoflFaqatNeveshtanRaMibandad_DidanBaz()
    {
        var old = PermissionService.ReadOnlyHook;
        try
        {
            PermissionService.ReadOnlyHook = () => true;
            var session = new UserSession();
            session.SignIn(UserRole.Admin, "admin");
            var perm = new PermissionService(session);
            Assert.False(perm.Can(Permission.EditData));
            Assert.False(perm.Can(Permission.DeleteData));
            Assert.False(perm.Can(Permission.Import));
            Assert.False(perm.Can(Permission.Restore));
            //  ⛔ دیدن، بکاپ، مفاد و تنظیمات همیشه باز
            Assert.True(perm.Can(Permission.ViewData));
            Assert.True(perm.Can(Permission.Backup));
            Assert.True(perm.Can(Permission.ViewProfit));
            Assert.True(perm.Can(Permission.ManageSettings));
        }
        finally { PermissionService.ReadOnlyHook = old; }
    }

    [Fact]
    public void SoftLock_TasmimRaFaqatAzAppLockMigirad()
    {
        var s = Src("PumpYaqobi.App/Services/SoftLock.cs");
        Assert.Contains("PermissionService.ReadOnlyHook = () => ReadOnly;", s);
        Assert.Contains("AppLock.Decide(", s);
        //  ⛔ خطای خواندنِ تنظیمات یا مجوز ⇒ باز، نه قفل
        Assert.Contains("return LockStatus.Open;", s[s.IndexOf("catch", s.IndexOf("Compute()"))..]);
        //  ⛔ جدول ویرایشگر باز نمی‌کند و کپی/پیست نمی‌نویسد
        var g = Src("PumpYaqobi.App/Controls/ExcelGrid.cs");
        Assert.Contains("if (LockedForWriting()) { e.Cancel = true; return; }", g);
        Assert.Contains("if (LockedForWriting()) return false;", Src("PumpYaqobi.App/Controls/ExcelGrid.Clipboard.cs"));
    }

    [Fact]
    public void TokeneDastgaheDigar_BePompeKhodeHesab_Mirasad()
    {
        var s = Src("PumpYaqobi.Shell/Services/CloudLink.cs");
        var i = s.IndexOf("private async Task<bool> ReseatToAccountPumpAsync", StringComparison.Ordinal);
        Assert.True(i > 0);
        var body = s[i..s.IndexOf("public async Task<CloudResult> BindAsync", i, StringComparison.Ordinal)];
        //  همان BindAsyncِ همیشگی — و نشد ⇒ همه برمی‌گردد
        Assert.Contains("await BindAsync(ct)", body);
        Assert.Contains("= keep;", body);
        //  ⛔ یک بیت از دفتر نه
        Assert.DoesNotContain("Db", body.Replace("DeviceToken", ""));

        var h = s.IndexOf("HomeFromAccountAsync(CancellationToken ct = default, bool forceBind = false)", StringComparison.Ordinal);
        var home = s[h..s.IndexOf("if (!json.TryGetProperty(\"home\"", h, StringComparison.Ordinal)];
        //  ۱) سرور جابه‌جایی را رد کرد ⇒ به پمپِ خودِ حساب
        Assert.Contains("moved.Code == \"station_mismatch\"", home);
        Assert.Contains("ReseatToAccountPumpAsync(leaveOther: true, ct)", home);
        //  ۲) همان پمپ (یا بی شناسه)، سرور می‌گوید فعال، مجوزِ روی دیسک نمی‌خورد ⇒
        //  اول تازه‌سازی؛ هنوز نه و سرورِ خودمان جواب داد ⇒ وصلِ دوباره — هر دلیلی
        //  (توکنِ پمپِ دیگر، کلیدِ دیگر، شناسهٔ دستگاهِ دیگر). ملاک کدِ خطا نیست.
        var g = home.IndexOf("if (Activated && bindDue && acctStation.Length > 0 && Subscription.Active", StringComparison.Ordinal);
        Assert.True(g > 0);
        var a = home.IndexOf("ReseatToAccountPumpAsync(leaveOther: false, ct)", g, StringComparison.Ordinal);
        Assert.True(a > 0);
        var block = home[g..a];
        //  ⚠️ فقط وقتی سرور می‌گوید اشتراک فعال است و مجوز نمی‌گوید — نصبِ سالم هیچ درخواستی نمی‌زند
        Assert.Contains("!Verify().Valid", block);
        Assert.Contains("await RefreshAsync(ct)", block);
        //  ⛔ نرسیدن به سرور هیچ چیزی را کنار نمی‌گذارد
        Assert.Contains("Reach == CloudReach.Online", block);
        Assert.DoesNotContain("fresh.Code == \"station_mismatch\"", block);
        //  وصل شد ولی مجوز نیامد ⇒ ترمزِ ده‌دقیقه‌ای، نه ثبتِ تازه در هر دقیقه
        Assert.Contains("_lastBindFailAt = AppClock.Mono;", home[a..]);
        //  ⛔ حلقهٔ پس‌زمینه همچنان هیچ پمپی نمی‌سازد
        Assert.DoesNotContain("EnsureStationAsync", home);
    }
}
