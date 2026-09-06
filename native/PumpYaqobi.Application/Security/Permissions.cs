using PumpYaqobi.Domain.Enums;

namespace PumpYaqobi.Application.Security;

/// <summary>
/// کارهایی که می‌شود اجازه‌شان را داشت یا نداشت. هر سرویسی که چیزی را
/// تغییر می‌دهد، پیش از تغییر یکی از این‌ها را می‌خواهد.
/// </summary>
public enum Permission
{
    ViewData = 1,
    EditData,
    DeleteData,
    /// <summary>خالی کردنِ سطلِ زباله / حذفِ همیشگی.</summary>
    PurgeData,
    ManageUsers,
    ManageSettings,
    /// <summary>دیدنِ «مفاد / ضرر» — در نسخهٔ HTML رمزِ جداگانه داشت.</summary>
    ViewProfit,
    Backup,
    Restore,
    Import,
}

/// <summary>وقتی کاری بی‌اجازه انجام شود، این پرتاب می‌شود — نه یک پیامِ ساکت.</summary>
public sealed class PermissionDeniedException : Exception
{
    public PermissionDeniedException(Permission p)
        : base("اجازهٔ این کار را ندارید: " + p) { Permission = p; }

    public Permission Permission { get; }
}

/// <summary>
/// ══ اجازه‌ها ═══════════════════════════════════════════════════════════════
/// بندِ ۴۱: «Permissionها باید در Service Layer enforce شوند، نه فقط UI.
/// مخفی کردن Button به‌تنهایی Security نیست.»
///
/// پس هیچ سرویسی به نقشِ کاربر نگاه نمی‌کند؛ همه از همین‌جا اجازه می‌گیرند و
/// اگر نداشته باشند، کارشان با استثنا می‌ایستد — چه دکمه‌ای در کار باشد چه نه.
/// </summary>
public sealed class PermissionService
{
    private static readonly Dictionary<UserRole, HashSet<Permission>> Map = new()
    {
        [UserRole.Admin] = new HashSet<Permission>(Enum.GetValues<Permission>()),

        // بیننده: فقط می‌بیند. حتی «مفاد/ضرر» را هم نه.
        [UserRole.Viewer] = new HashSet<Permission> { Permission.ViewData },

        // کارمند: ثبتِ روزانه بله؛ حذف، کاربر، تنظیمات، مفاد و بکاپ نه.
        [UserRole.Staff] = new HashSet<Permission> { Permission.ViewData, Permission.EditData },
    };

    private readonly IUserSession _session;

    public PermissionService(IUserSession session) => _session = session;

    public bool Can(Permission p) =>
        Map.TryGetValue(_session.Role, out var set) && set.Contains(p);

    /// <summary>اجازه را می‌خواهد؛ اگر نباشد کار همان‌جا می‌ایستد.</summary>
    public void Require(Permission p)
    {
        if (!Can(p)) throw new PermissionDeniedException(p);
    }
}
