using PumpYaqobi.Application.Security;
using PumpYaqobi.Domain.Enums;
using PumpYaqobi.Services.Data;
using PumpYaqobi.Services.Security;
using Xunit;

namespace PumpYaqobi.Tests;

public class PasswordHasherTests
{
    [Fact]
    public void Hash_IsSaltedSoTwoHashesOfTheSamePasswordDiffer()
    {
        var a = PasswordHasher.Hash("۱۲۳۴");
        var b = PasswordHasher.Hash("۱۲۳۴");
        Assert.NotEqual(a, b);
        Assert.True(PasswordHasher.Verify("۱۲۳۴", a));
        Assert.True(PasswordHasher.Verify("۱۲۳۴", b));
    }

    [Fact]
    public void Verify_RejectsWrongPasswordAndGarbage()
    {
        var h = PasswordHasher.Hash("open sesame");
        Assert.False(PasswordHasher.Verify("Open Sesame", h));
        Assert.False(PasswordHasher.Verify("open sesame", "not-a-hash"));
        Assert.False(PasswordHasher.Verify("open sesame", null));
        Assert.False(PasswordHasher.Verify("open sesame", "pbkdf2$sha256$x$y$z"));
    }

    [Fact]
    public void PlaintextAndOldSha256_AreNotAccepted()
    {
        // نسخهٔ HTML رمز را با SHA-256ِ بی‌نمک نگه می‌داشت؛ آن قالب اینجا معتبر نیست
        Assert.False(PasswordHasher.IsHashed("1234"));
        Assert.False(PasswordHasher.IsHashed("03ac674216f3e15c761ee1a5e255f067953623c8b388b4459e13f978d7c846f4"));
        Assert.True(PasswordHasher.NeedsRehash("1234"));
    }

    [Fact]
    public void NeedsRehash_IsFalseForAFreshHash() =>
        Assert.False(PasswordHasher.NeedsRehash(PasswordHasher.Hash("x")));
}

public class PermissionTests
{
    private static PermissionService For(UserRole role)
    {
        var s = new UserSession();
        s.SignIn(role, "u");
        return new PermissionService(s);
    }

    [Fact]
    public void Viewer_CanOnlyLook()
    {
        var p = For(UserRole.Viewer);
        Assert.True(p.Can(Permission.ViewData));
        Assert.False(p.Can(Permission.EditData));
        Assert.False(p.Can(Permission.DeleteData));
        Assert.False(p.Can(Permission.ViewProfit));
        Assert.False(p.Can(Permission.ManageUsers));
        Assert.Throws<PermissionDeniedException>(() => p.Require(Permission.EditData));
    }

    [Fact]
    public void Staff_CanEditButNotDeleteOrSeeProfit()
    {
        var p = For(UserRole.Staff);
        Assert.True(p.Can(Permission.EditData));
        Assert.False(p.Can(Permission.DeleteData));
        Assert.False(p.Can(Permission.ViewProfit));
        Assert.False(p.Can(Permission.Restore));
    }

    [Fact]
    public void Admin_CanDoEverything()
    {
        var p = For(UserRole.Admin);
        foreach (var perm in Enum.GetValues<Permission>()) Assert.True(p.Can(perm), perm.ToString());
    }

    [Fact]
    public void SignedOutSession_FallsBackToViewer()
    {
        var s = new UserSession();
        s.SignIn(UserRole.Admin, "a");
        s.SignOut();
        Assert.False(new PermissionService(s).Can(Permission.EditData));
    }
}

public class AuthServiceTests : IDisposable
{
    private readonly string _file = Path.Combine(Path.GetTempPath(), $"pump-auth-{Guid.NewGuid():N}.db");
    private readonly PumpDbFactory _dbf;
    private readonly UserSession _session = new();
    private readonly AuthService _auth;

    public AuthServiceTests()
    {
        _dbf = new PumpDbFactory(_file);
        _dbf.EnsureReady();
        _auth = new AuthService(_dbf, _session);
    }

    public void Dispose() { try { if (File.Exists(_file)) File.Delete(_file); } catch { } }

    [Fact]
    public void FirstRun_CreatesAdminAndSignsIn()
    {
        Assert.True(_auth.NeedsFirstRun());
        _auth.CreateFirstAdmin("رمزِ خوب");
        Assert.False(_auth.NeedsFirstRun());

        var r = _auth.SignIn("admin", "رمزِ خوب");
        Assert.Equal(SignInResult.Ok, r.Result);
        Assert.Equal(UserRole.Admin, _session.Role);
        Assert.True(_session.IsSignedIn);
    }

    [Fact]
    public void PasswordIsNeverStoredAsPlaintext()
    {
        _auth.CreateFirstAdmin("۱۲۳۴۵");
        using var db = _dbf.Create();
        var stored = db.Users.Single().PasswordHash!;
        Assert.DoesNotContain("۱۲۳۴۵", stored);
        Assert.StartsWith("pbkdf2$sha256$", stored);
    }

    [Fact]
    public void ThreeWrongTries_LockTheDoor()
    {
        _auth.CreateFirstAdmin("درست");
        Assert.Equal(SignInResult.WrongPassword, _auth.SignIn("admin", "غلط1").Result);
        Assert.Equal(SignInResult.WrongPassword, _auth.SignIn("admin", "غلط2").Result);
        _auth.SignIn("admin", "غلط3");
        var r = _auth.SignIn("admin", "درست");     // حتی رمزِ درست هم در زمانِ قفل رد می‌شود
        Assert.Equal(SignInResult.LockedOut, r.Result);
        Assert.NotNull(r.LockedFor);
    }

    [Fact]
    public void ChangePassword_NeedsTheCurrentOne()
    {
        _auth.CreateFirstAdmin("کهنه");
        Assert.Throws<UnauthorizedAccessException>(() => _auth.ChangePassword("admin", "اشتباه", "نو"));
        _auth.ChangePassword("admin", "کهنه", "نو");
        Assert.Equal(SignInResult.Ok, _auth.SignIn("admin", "نو").Result);
    }
}
