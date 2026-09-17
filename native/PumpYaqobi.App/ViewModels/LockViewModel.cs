using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PumpYaqobi.App.Services;
using PumpYaqobi.Services.Security;

namespace PumpYaqobi.App.ViewModels;

/// <summary>
/// صفحهٔ قفل. دو حالت دارد: نخستین اجرا (ساختنِ رمزِ مدیر) و ورودِ عادی.
/// رمز هرگز در حافظهٔ برنامه نمی‌ماند و هرگز جایی نوشته نمی‌شود.
/// </summary>
public sealed partial class LockViewModel : ObservableObject
{
    private readonly AppHost _host;

    public LockViewModel(AppHost host)
    {
        _host = host;
        IsFirstRun = host.Auth.NeedsFirstRun();
    }

    public event Action? SignedIn;

    [ObservableProperty] private bool _isFirstRun;
    [ObservableProperty] private string _password = "";
    [ObservableProperty] private string _confirm = "";
    [ObservableProperty] private string _error = "";
    [ObservableProperty] private bool _busy;

    public string Title => IsFirstRun ? "نخستین اجرا — رمزِ مدیر را بگذارید" : "پمپ یعقوبی";
    public string ActionText => IsFirstRun ? "ساختنِ رمز و ورود" : "ورود";

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
            if (IsFirstRun)
            {
                if (Password.Length < 4) { Error = "رمز دستِ‌کم چهار نویسه باشد."; return; }
                if (Password != Confirm) { Error = "دو رمز یکی نیستند."; return; }
                //  ⚠️ رمز پیش از رفتن به نخِ دیگر برداشته می‌شود، تا اگر کاربر
                //  وسطِ کار کادر را عوض کرد، همان چیزی سنجیده شود که زد.
                var first = Password;
                await Task.Run(() => _host.Auth.CreateFirstAdmin(first));
                IsFirstRun = false;
            }

            var pass = Password;
            var r = await Task.Run(() => _host.Auth.SignIn("admin", pass));
            switch (r.Result)
            {
                case SignInResult.Ok:
                    Password = ""; Confirm = "";
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
