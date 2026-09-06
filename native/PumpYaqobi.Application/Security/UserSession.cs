using PumpYaqobi.Domain.Enums;

namespace PumpYaqobi.Application.Security;

/// <summary>نقشِ کاربرِ همین لحظه. سرویس‌ها فقط از این می‌پرسند.</summary>
public interface IUserSession
{
    UserRole Role { get; }
    string? UserName { get; }
    bool IsSignedIn { get; }
}

public sealed class UserSession : IUserSession
{
    public UserRole Role { get; private set; } = UserRole.Viewer;
    public string? UserName { get; private set; }
    public bool IsSignedIn { get; private set; }

    public event Action? Changed;

    public void SignIn(UserRole role, string? userName)
    {
        Role = role; UserName = userName; IsSignedIn = true;
        Changed?.Invoke();
    }

    public void SignOut()
    {
        Role = UserRole.Viewer; UserName = null; IsSignedIn = false;
        Changed?.Invoke();
    }
}
