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

    [RelayCommand]
    private void Submit()
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
                _host.Auth.CreateFirstAdmin(Password);
                IsFirstRun = false;
            }

            var r = _host.Auth.SignIn("admin", Password);
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
