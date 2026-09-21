using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PumpYaqobi.App.Services;
using PumpYaqobi.Services.Security;

namespace PumpYaqobi.App.ViewModels;

/// <summary>
/// ══ صفحهٔ قفل — فقط وقتی رمزی گذاشته شده باشد ═════════════════════════════
///
/// خواستهٔ صریحِ صاحب ریپو (۱۴۰۵/۰۷/۰۷): «برنامه بدون رمز باشه، چون کسایی که
/// تازه به برنامه می‌رسن نباید رمز داشته باشه و خود طرف برای خودش رمز خودشو
/// می‌زنه.»
///
/// ⛔ پس «نخستین اجرا ⇒ رمز بساز» برداشته شد. نصبِ تازه هیچ صفحهٔ قفلی
/// نمی‌بیند (<see cref="AuthService.OpenWithoutPassword"/>) و این صفحه فقط
/// وقتی می‌آید که خودِ کاربر در «تنظیمات ← رمزها و کد» رمزی گذاشته باشد.
///
/// رمز هرگز در حافظهٔ برنامه نمی‌ماند و هرگز جایی نوشته نمی‌شود.
/// </summary>
public sealed partial class LockViewModel : ObservableObject
{
    private readonly AppHost _host;

    public LockViewModel(AppHost host) => _host = host;

    public event Action? SignedIn;

    [ObservableProperty] private string _password = "";
    [ObservableProperty] private string _error = "";
    [ObservableProperty] private bool _busy;

    public string Title => "پمپ یعقوبی";
    public string ActionText => "ورود";

    /// <summary>
    /// رمزی گذاشته شده؟ پوسته با همین تصمیم می‌گیرد که صفحهٔ قفل را نشان
    /// بدهد یا همان لحظه وارد شود.
    /// </summary>
    public bool HasPassword() => _host.Auth.HasPassword();

    /// <summary>
    /// نصبِ بی‌رمز: وارد می‌شود و <see cref="SignedIn"/> را شلیک می‌کند.
    /// ⚠️ روی نخِ دیگر می‌رود چون یک نوشتنِ دیتابیس دارد، و ادامه‌اش به
    /// همان نخِ رابط برمی‌گردد — همان قاعدهٔ <see cref="SubmitAsync"/>.
    /// </summary>
    public async Task<bool> OpenIfNoPasswordAsync()
    {
        if (_host.Auth.HasPassword()) return false;
        var ok = await Task.Run(() => _host.Auth.OpenWithoutPassword());
        if (ok) SignedIn?.Invoke();
        return ok;
    }

    /// <summary>
    /// ══ ورود — روی نخِ دیگر، نه روی نخِ رابط ═══════════════════════════════
    ///
    /// ⛔ <b>باگی که هر بار باز شدنِ برنامه را می‌گرفت.</b> این متد
    /// <c>void</c>ِ هم‌زمان بود و همه‌چیز روی <b>نخِ رابط</b> می‌دوید:
    /// <c>PasswordHasher.Verify</c> با ۲۱۰٬۰۰۰ دورِ PBKDF2 (سنجیده شد:
    /// <b>۱۴۰ میلی‌ثانیه</b>، و نخستین اجرا با ساختنِ رمز <b>۲۸۳</b>)،
    /// به‌علاوهٔ پرس‌وجو و نوشتنِ دیتابیس. یعنی پنجره در هر تلاشِ ورود همان‌قدر
    /// <b>می‌خشکید</b> و هیچ فریمی کشیده نمی‌شد.
    ///
    /// و بدتر: <c>Busy</c> در همان بلوکِ هم‌زمان روشن و خاموش می‌شد، پس
    /// <b>هیچ‌وقت حتی یک فریم دیده نمی‌شد</b> — یعنی
    /// <c>IsEnabled="{Binding !Busy}"</c>ی دکمه عملاً مرده بود.
    ///
    /// ⚠️ <c>SignedIn</c> باید روی نخِ رابط شلیک شود (پوسته را عوض می‌کند) و
    /// می‌شود: ادامهٔ پس از <c>await</c> به همان نخِ رابط برمی‌گردد.
    /// ⚠️ <c>AuthService</c> برای هر فراخوان ‎DbContext‎ِ خودش را می‌سازد، پس
    /// روی نخِ دیگر بی‌خطر است. و هیچ‌کس به <c>UserSession.Changed</c> گوش
    /// نمی‌دهد، پس شلیکش از نخِ دیگر چیزی را نمی‌شکند.
    /// ⚠️ <c>[RelayCommand]</c> روی متدِ ‎async‎ یک ‎AsyncRelayCommand‎ می‌سازد
    /// که پیش‌فرضش اجرای هم‌زمان را نمی‌پذیرد — پس دو بار زدنِ «ورود» هم
    /// خودبه‌خود یکی حساب می‌شود.
    /// </summary>
    [RelayCommand]
    private async Task SubmitAsync()
    {
        Error = "";
        if (string.IsNullOrWhiteSpace(Password)) { Error = "رمز را بنویسید."; return; }

        Busy = true;
        try
        {
            //  ⚠️ رمز پیش از رفتن به نخِ دیگر برداشته می‌شود، تا اگر کاربر
            //  وسطِ کار کادر را عوض کرد، همان چیزی سنجیده شود که زد.
            var pass = Password;
            var r = await Task.Run(() => _host.Auth.SignIn("admin", pass));
            switch (r.Result)
            {
                case SignInResult.Ok:
                    Password = "";
                    SignedIn?.Invoke();
                    break;
                case SignInResult.LockedOut:
                    Error = $"به‌خاطرِ تلاش‌های نادرست، ورود تا {(int)Math.Ceiling(r.LockedFor!.Value.TotalSeconds)} ثانیه بسته است.";
                    break;
                default:
                    Error = "رمز درست نیست.";
                    break;
            }
        }
        finally { Busy = false; }
    }
}
