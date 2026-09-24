using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PumpYaqobi.App.Services;
using PumpYaqobi.Services.Data;

namespace PumpYaqobi.App.ViewModels.Sections;

/// <summary>
/// ══ ۴) اپِ گوشی — لینک و کدِ پمپ ════════════════════════════════════════════
///
/// خواستهٔ صریحِ صاحب ریپو (۱۴۰۵/۰۶/۲۸): «لینکِ دانلودِ اپِ اندروید و لینکِ
/// برنامهٔ آیفون را توی یک بخشِ جدید توی تنظیمات بگذار که کپی کنم یا به
/// کارفرما تو واتساپ بفرستم و آن‌ها بتوانند اپ را تماشا کنند و ببینند… و
/// کدِ برنامه که حساب‌های همان پمپ را نشان می‌دهد توی همان بخشِ جدید هم دیده
/// شود و توی پروفایل هم دیده شود.»
///
/// پس این صفحه سه چیز دارد و بس: دو لینک، کدِ پمپ، و یک پیامِ آمادهٔ
/// فرستادن که هر سه را یک‌جا دارد.
///
/// ⛔ <b>فقط <c>PumpYaqobiKar.apk</c></b> — فایلِ نصبِ سایتِ قدیم
/// (<c>android-latest</c>) هیچ‌وقت به کاربر داده نمی‌شود؛ قاعدهٔ ۱۴۰۵/۰۶/۲۴.
///
/// ⛔ هیچ رمزی در این لینک‌ها نیست: نه رمزِ برنامه، نه رمزِ خواندنِ سرور.
/// لینک‌ها عمومی‌اند و تنها چیزی که پمپ را باز می‌کند همان کدِ پمپ است —
/// و بعدش رمزِ خودِ برنامه.
///
/// ⛔ کد این‌جا فقط <b>دیده</b> می‌شود؛ ساختن و عوض کردنش در «پروفایل» است
/// (کد از ابر می‌آید، هیچ کادرِ تایپی ندارد — <c>ProfilePillTests</c>).
/// </summary>
public sealed partial class AppsSectionViewModel : SectionViewModel
{
    private readonly AppHost _host;

    public AppsSectionViewModel(AppHost host) : base("apps", "settings", "اپِ گوشی — لینک و کد")
    {
        _host = host;
        Show();
    }

    /// <summary>لینکِ فایلِ نصبِ اندروید — روی دامنهٔ خودِ پمپ.</summary>
    [ObservableProperty] private string _androidLink = "";

    /// <summary>لینکِ آیفون — همان صفحه در سافاری با «افزودن به صفحهٔ اصلی».</summary>
    [ObservableProperty] private string _iphoneLink = "";

    /// <summary>کدِ پمپ به شکلِ ‎K7PM-3XQ2‎، یا «—» اگر هنوز نیامده.</summary>
    [ObservableProperty] private string _codeText = "—";

    /// <summary>یک جمله دربارهٔ حالِ کد — خالی یعنی همه‌چیز سرِ جایش است.</summary>
    [ObservableProperty] private string _codeHint = "";

    /// <summary>پیامِ آمادهٔ واتساپ: دو لینک + کد، یک‌جا.</summary>
    [ObservableProperty] private string _shareText = "";

    /// <summary>«📋 کپی شد» و مانندِ آن.</summary>
    [ObservableProperty] private string _done = "";

    private string _code = "";
    public bool HasCode => _code.Length > 0;

    private void Show()
    {
        var file = AppSettings.Load();
        var viewer = _host.Settings.GetString(SettingsService.ViewerUrl);

        AndroidLink = KarLink.ApkUrl(viewer);
        IphoneLink = KarLink.IphoneUrl(viewer);

        //  ⛔ **پلنِ استاندارد کدِ پمپ نمی‌گیرد.** خواستهٔ صریحِ صاحب ریپو:
        //  «اپِ کارمندان و آیفون براش فعال نشه، یعنی کدی که برای این‌ها
        //  استفاده می‌شد برای این نوعِ خرید داده نشه.»
        //
        //  ⚠️ کد **پاک نمی‌شود**، فقط نشان داده نمی‌شود: با خریدِ وی‌آی‌پی
        //  همان کدِ قبلی برمی‌گردد و گوشی‌ها لازم نیست دوباره چیزی بزنند.
        var planHasKar = Entitlements.Allows(Entitlements.Kar);

        _code = planHasKar ? (file.CloudAccessCode ?? "") : "";
        CodeText = HasCode ? CloudLink.FormatAccessCode(_code) : "—";
        CodeHint = !planHasKar
            ? "اپِ کارمندان و آیفون در پلنِ شما نیست. با وی‌آی‌پی یا دائمی باز می‌شود."
            : HasCode
            ? ""
            : string.IsNullOrWhiteSpace(file.CloudDeviceToken)
                ? "کدِ پمپ بعد از فعال شدنِ اشتراک از سرور می‌آید — «پروفایل»."
                : "هنوز از سرور گرفته نشده — در «پروفایل» دکمهٔ «گرفتنِ کد» را بزنید.";

        //  و پیامِ آماده هم کدی را که نداریم جا نمی‌گذارد
        ShareText = planHasKar
            ? KarLink.ShareText(_code, _host.Settings.GetString(SettingsService.StationName), viewer)
            : "";
        OnPropertyChanged(nameof(HasCode));
    }

    [RelayCommand]
    private Task CopyAndroidAsync() => CopyAsync(AndroidLink, "لینکِ اندروید");

    [RelayCommand]
    private Task CopyIphoneAsync() => CopyAsync(IphoneLink, "لینکِ آیفون");

    [RelayCommand]
    private Task CopyCodeAsync() => HasCode ? CopyAsync(CodeText, "کدِ پمپ") : Task.CompletedTask;

    /// <summary>همان چیزی که در واتساپ چسبانده می‌شود — هر سه با هم.</summary>
    [RelayCommand]
    private Task CopyShareAsync() => CopyAsync(ShareText, "پیامِ آمادهٔ فرستادن");

    private Task CopyAsync(string text, string what) =>
        CrashGuard.RunAsync("کپی", async () =>
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            Done = await Dialogs.CopyAsync(text) ? "📋 " + what + " کپی شد." : "";
        });

    /// <summary>لینک را در مرورگرِ خودِ سیستم باز می‌کند — برای «خودم ببینم».</summary>
    [RelayCommand]
    private void Open(string? url)
    {
        //  ⛔ فقط نشانیِ وب (`SafeOpen`) — هیچ مسیرِ فایلی به ویندوز سپرده نمی‌شود.
        SafeOpen.Url(url);
    }

    /// <summary>
    /// کیو‌آرِ «با کدِ پمپ» — کارمند اسکن می‌کند و اپ خودش کد را می‌زند.
    /// همان چیزی که در پروفایل هم هست، پس همان قفلِ اشتراک را دارد.
    /// </summary>
    [RelayCommand]
    private Task ShowQrAsync() => CrashGuard.RunAsync("کیو‌آرِ کدِ پمپ", async () =>
    {
        if (!HasCode) return;
        if (!Entitlements.Gate(_host, Entitlements.Kar)) return;
        var link = KarLink.ForCode(_code, _host.Settings.GetString(SettingsService.ViewerUrl));
        var png = await Task.Run(() => PumpYaqobi.Services.Vision.QrWriter.EncodePng(link));
        await Dialogs.ShowQrAsync("📲 کدِ پمپ — " + CodeText, link, png,
            "کارمند این را اسکن کند یا همین کد را در اپ بزند. هیچ رمزِ سروری در این کد نیست.");
    });

    public override Task OnActivatedAsync()
    {
        Done = "";
        Show();
        return Task.CompletedTask;
    }
}
