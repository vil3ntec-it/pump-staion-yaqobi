using Microsoft.EntityFrameworkCore;
using PumpYaqobi.Application.Security;
using PumpYaqobi.Domain.Enums;
using PumpYaqobi.Services.Data;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// دفترِ دوربین‌ها روی یک دیتابیسِ واقعی — و اینکه کارمند نتواند دستش بزند
/// (در نسخهٔ وب هر سه دکمه ‎requireAdmin()‎ دارند).
/// </summary>
public class CameraDataTests : IDisposable
{
    private readonly string _file =
        Path.Combine(Path.GetTempPath(), $"pump-cam-{Guid.NewGuid():N}.db");

    public void Dispose() { try { if (File.Exists(_file)) File.Delete(_file); } catch { } }

    private (CameraDataService Svc, PumpDbFactory Db) Host(UserRole role = UserRole.Admin)
    {
        var dbf = new PumpDbFactory(_file);
        dbf.EnsureReady();
        var session = new UserSession();
        session.SignIn(role, "آزمون");
        var perm = new PermissionService(session);
        var trash = new TrashService(dbf, perm, session);
        return (new CameraDataService(dbf, perm, trash), dbf);
    }

    [Fact]
    public async Task ACameraWithoutALink_IsNotSaved()
    {
        var (svc, _) = Host();
        Assert.Null(await svc.AddAsync("درِ ورودی", "   "));
        Assert.Null(await svc.AddAsync("درِ ورودی", null));
        Assert.Empty(await svc.ListAsync());
    }

    [Fact]
    public async Task AnEmptyName_BecomesTheDefaultOne()
    {
        var (svc, _) = Host();
        var a = await svc.AddAsync("", "http://192.168.1.9/snap.jpg");
        var b = await svc.AddAsync("  ", "http://192.168.1.8/snap.jpg");
        Assert.Equal("دوربین 1", a!.Name);
        Assert.Equal("دوربین 2", b!.Name);
    }

    [Fact]
    public async Task DeletingACamera_LeavesItInTheTrash()
    {
        var (svc, dbf) = Host();
        var cam = await svc.AddAsync("حیاط", "http://192.168.1.9/snap.jpg");
        await svc.DeleteAsync(cam!.Id);

        Assert.Empty(await svc.ListAsync());
        await using var db = dbf.Create();
        Assert.Equal(1, await db.Trash.CountAsync(t => t.Kind == "camera"));
    }

    [Fact]
    public async Task Staff_CanLookButNotTouch()
    {
        var (admin, _) = Host();
        await admin.AddAsync("حیاط", "http://192.168.1.9/snap.jpg");

        var (staff, _) = Host(UserRole.Staff);
        Assert.Single(await staff.ListAsync());
        await Assert.ThrowsAsync<PermissionDeniedException>(
            () => staff.AddAsync("پنهانی", "http://x/y.jpg"));
        await Assert.ThrowsAsync<PermissionDeniedException>(() => staff.DeleteAsync(1));
    }
}
