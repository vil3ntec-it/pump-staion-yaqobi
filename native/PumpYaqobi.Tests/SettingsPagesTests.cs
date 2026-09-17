using PumpYaqobi.App.Services;
using PumpYaqobi.App.ViewModels;
using PumpYaqobi.App.ViewModels.Sections;
using PumpYaqobi.Services.Security;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ تنظیمات: سه صفحه، و رمزِ بخش‌ها ═════════════════════════════════════════
///
/// خواستهٔ صریحِ صاحب ریپو (۱۴۰۵/۰۶/۲۸):
///   «بخشِ تنظیمات این مدل باشه، سه بخش داشته باشه؛ هر کدوم که بزنی صفحهٔ
///    جداگانه باز بشه: ۱) رمزها و کد ۲) بک‌اپ و به‌روزرسانی‌ها ۳) سطلِ زباله…
///    و تنظیماتِ الان همه‌شونو حذف کن.»
///
/// این آزمون همان سه در، ترتیبشان، و قفلِ «مفاد/ضرر» را قفل می‌کند.
/// </summary>
[Collection(AppHostCollection.Name)]
public class SettingsPagesTests
{
    /// <summary>
    /// ⚠️ ‎AppHost.Start‎ برای کلِ اجرا یکی است (‎Current ??=‎)، پس این
    /// آزمون‌ها روی **یک** میزبان و یک دیتابیس می‌دوند. هر آزمون خودش حال را
    /// پاک می‌کند و همان‌طور که یافت، تحویل می‌دهد — وگرنه یکی، بعدیِ خودش را
    /// می‌شکند (یک بار همین شد: قفلِ ماندهٔ «مفاد» و رمزِ عوض‌شدهٔ مدیر).
    /// </summary>
    private static AppHost Host()
    {
        var host = AppHost.Start(
            Path.Combine(Path.GetTempPath(), "pump-set-" + Guid.NewGuid().ToString("N"), "pump.db"));
        if (host.Auth.NeedsFirstRun()) host.Auth.CreateFirstAdmin("1234");
        host.Auth.SignIn("admin", "1234");
        foreach (var id in SectionLockService.Ids) host.Locks.ClearPassword(id);
        return host;
    }

    [Fact]
    public void SettingsHasExactlyTheFourPagesInOrder()
    {
        Host();
        var vm = new MainViewModel();
        var settings = vm.Sections.Single(s => s.Id == "settings");

        //  درِ چهارم (۱۴۰۵/۰۶/۲۸): «لینکِ اپِ اندروید و آیفون را توی یک بخشِ
        //  جدید توی تنظیمات بزار» — و همان‌جا کدِ پمپ هم دیده می‌شود.
        Assert.Equal(new[] { "keys", "backups", "trash", "apps" },
                     settings.SubSections.Select(s => s.Id).ToArray());

        // صفحهٔ تنظیمات خودش کارت‌های بزرگ دارد، پس ردیفِ خودکارِ لینک‌ها خاموش است
        Assert.False(settings.ShowSubLinkCards);
    }

    /// <summary>رمزِ خودِ برنامه از همین صفحه عوض می‌شود — و همان لحظه اثر دارد.</summary>
    [Fact]
    public void TheAppPasswordChangesFromTheKeysPage()
    {
        var host = Host();
        var vm = new KeysSectionViewModel(host)
        {
            Current = "1234",
            Next = "5678",
            Confirm = "5678",
        };
        vm.ChangeAppPasswordCommand.Execute(null);

        Assert.Equal("", vm.AppError);

        // ⚠️ با ‎SignIn‎ نمی‌سنجیم: ورودِ غلط شمارندهٔ قفلِ فزاینده را بالا
        // می‌برد و آزمونِ بعدی را «LockedOut» می‌کند. خودِ «عوض کردنِ رمز»
        // هم رمزِ فعلی را می‌سنجد، پس همان گواهی است — و رمز را سرِ جایش
        // برمی‌گرداند.
        host.Auth.ChangePassword("admin", "5678", "1234");
    }

    [Fact]
    public void AWrongCurrentPasswordChangesNothing()
    {
        var host = Host();
        var vm = new KeysSectionViewModel(host)
        {
            Current = "بی‌ربط",
            Next = "5678",
            Confirm = "5678",
        };
        vm.ChangeAppPasswordCommand.Execute(null);

        Assert.NotEqual("", vm.AppError);

        // رمز عوض نشده: همان «۱۲۳۴» هنوز رمزِ فعلی است
        host.Auth.ChangePassword("admin", "1234", "1234");
    }

    /// <summary>
    /// قفلِ بخش: رمز می‌گیرد، رمزِ غلط را رد می‌کند، و رمزِ درست تا بسته شدنِ
    /// برنامه بازش می‌کند.
    /// </summary>
    [Fact]
    public void TheProfitSectionCanTakeItsOwnPassword()
    {
        var host = Host();
        var locks = host.Locks;

        Assert.False(locks.HasPassword(SectionLockService.Profit));
        Assert.False(locks.NeedsUnlock(SectionLockService.Profit));

        locks.SetPassword(SectionLockService.Profit, "9999");
        Assert.True(locks.HasPassword(SectionLockService.Profit));
        Assert.True(locks.NeedsUnlock(SectionLockService.Profit));

        Assert.False(locks.Unlock(SectionLockService.Profit, "0000"));
        Assert.True(locks.NeedsUnlock(SectionLockService.Profit));

        Assert.True(locks.Unlock(SectionLockService.Profit, "9999"));
        Assert.False(locks.NeedsUnlock(SectionLockService.Profit));

        // «همین حالا دوباره قفل کن» بی برداشتنِ رمز
        locks.Relock(SectionLockService.Profit);
        Assert.True(locks.NeedsUnlock(SectionLockService.Profit));

        locks.ClearPassword(SectionLockService.Profit);
        Assert.False(locks.HasPassword(SectionLockService.Profit));
        Assert.False(locks.NeedsUnlock(SectionLockService.Profit));
    }

    /// <summary>⚠️ رمزِ خام هیچ‌وقت ذخیره نمی‌شود — فقط هشِ PBKDF2.</summary>
    [Fact]
    public void ThePasswordIsNeverStoredInTheClear()
    {
        var host = Host();
        host.Locks.SetPassword(SectionLockService.PriceLoss, "راز");

        var stored = host.Settings.GetString("lock." + SectionLockService.PriceLoss);
        Assert.StartsWith("pbkdf2$sha256$", stored);
        Assert.DoesNotContain("راز", stored);
    }

    /// <summary>قفل فقط روی همان دو بخش می‌نشیند، نه روی هر بخشی.</summary>
    [Fact]
    public void OnlyProfitAndPriceLossAreLockable()
    {
        var host = Host();
        Assert.True(SectionLockService.Lockable("profit"));
        Assert.True(SectionLockService.Lockable("priceloss"));
        Assert.False(SectionLockService.Lockable("debt"));
        Assert.Throws<InvalidOperationException>(() => host.Locks.SetPassword("debt", "1234"));
    }

    /// <summary>
    /// بخشِ قفل‌دار بی رمز باز نمی‌شود — و چون کاربر انصراف می‌دهد، صفحهٔ
    /// پیشین سرِ جایش می‌ماند.
    /// </summary>
    [Fact]
    public async Task ALockedSectionDoesNotOpenWithoutThePassword()
    {
        var host = Host();

        //  ⛔ **پیش از ساختنِ ویومدل، نه بعدش.** ویومدل نسخهٔ خودش از تنظیمات
        //  را نگه می‌دارد و با ذخیرهٔ «آخرین بخش» همان را روی دیسک می‌نویسد،
        //  پس هر چیزی که بعد از ساختنش در فایل بنویسیم همان لحظه پاک
        //  می‌شود. (همین آزمون گرفتش.)
        //
        //  و اشتراک باز گذاشته می‌شود چون «مفاد» از ۱۴۰۵/۰۶/۳۰ قفلِ **پلن**
        //  هم دارد و این آزمون دربارهٔ قفلِ **رمز** است. ارفاق کافی است —
        //  همان راهی که برنامهٔ بی‌اینترنت هم از آن باز می‌ماند.
        var file = AppSettings.Load();
        file.CloudDeviceToken = "pd-test";
        file.EntitledUntil = DateTimeOffset.UtcNow.AddDays(30).ToUnixTimeMilliseconds();
        file.Save();

        var vm = new MainViewModel();

        //  ⚠️ صفحهٔ «پیشین» عمداً داشبورد نیست: داشبورد هم قفلِ پلن دارد.
        //  «قرض‌داران» در هیچ پلنی قفل نمی‌شود.
        var dash = vm.Sections.Single(s => s.Id == "debt");
        var profit = vm.Sections.Single(s => s.Id == "profit");

        await vm.GoAsync(dash);
        host.Locks.SetPassword(SectionLockService.Profit, "9999");

        try
        {
            Dialogs.PromptHook = (_, _) => null;             // «انصراف»
            await vm.GoAsync(profit);
            Assert.Same(dash, vm.Current);

            Dialogs.PromptHook = (_, _) => "غلط";
            await vm.GoAsync(profit);
            Assert.Same(dash, vm.Current);

            Dialogs.PromptHook = (_, _) => "9999";
            await vm.GoAsync(profit);
            Assert.Same(profit, vm.Current);
        }
        finally
        {
            Dialogs.PromptHook = null;
            host.Locks.ClearPassword(SectionLockService.Profit);
        }
    }
}
