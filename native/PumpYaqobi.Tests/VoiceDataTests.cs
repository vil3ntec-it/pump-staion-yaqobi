using PumpYaqobi.Application.Security;
using PumpYaqobi.Application.Services;
using PumpYaqobi.Domain.Enums;
using PumpYaqobi.Services.Data;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// انبارِ صداها روی یک دیتابیسِ واقعی: ثبت، سقفِ سه‌تایی، فراموشی، و تصمیمِ
/// «مطمئنم / بپرس».
/// </summary>
public class VoiceDataTests : IDisposable
{
    private readonly string _file =
        Path.Combine(Path.GetTempPath(), $"pump-vx-{Guid.NewGuid():N}.db");

    public void Dispose() { try { if (File.Exists(_file)) File.Delete(_file); } catch { } }

    private VoiceDataService Host(UserRole role = UserRole.Admin)
    {
        var dbf = new PumpDbFactory(_file);
        dbf.EnsureReady();
        var session = new UserSession();
        session.SignIn(role, "آزمون");
        return new VoiceDataService(dbf, new PermissionService(session));
    }

    /// <summary>ویژگیِ ساختگی ولی معتبر — محتوایش مهم نیست، ساختارش مهم است.</summary>
    private static VoiceFeatures Feat(int frames, int seed)
    {
        var dim = VoiceEngine.Cepstra * 2;
        var d = new sbyte[frames * dim];
        for (var i = 0; i < d.Length; i++) d[i] = (sbyte)(((i * 37 + seed * 11) % 255) - 127);
        return new VoiceFeatures(frames, dim, d);
    }

    [Fact]
    public async Task Enrolling_KeepsOnlyTheThreeNewest()
    {
        var svc = Host();
        for (var i = 0; i < 6; i++)
            Assert.True(await svc.EnrollAsync("p7", "کریم", Feat(20, i)));

        Assert.Equal(VoiceEngine.MaxPerName, await svc.CountAsync("p7"));
        Assert.Equal(1, await svc.AccountCountAsync());
    }

    [Fact]
    public async Task NothingIsStored_WithoutAKeyOrFeatures()
    {
        var svc = Host();
        Assert.False(await svc.EnrollAsync("", "کریم", Feat(20, 1)));
        Assert.False(await svc.EnrollAsync("p7", "کریم", null));
        Assert.Equal(0, await svc.AccountCountAsync());
    }

    /// <summary>صدای ثبت‌شده باید خودش را با فاصلهٔ صفر پیدا کند.</summary>
    [Fact]
    public async Task TheEnrolledVoice_IsItsOwnNearestMatch()
    {
        var svc = Host();
        var mine = Feat(24, 3);
        await svc.EnrollAsync("p7", "کریم", mine);
        await svc.EnrollAsync("p9", "نصیر", Feat(24, 99));

        var res = await svc.MatchAsync(mine);
        Assert.Equal(2, res.Count);
        Assert.Equal("p7", res[0].Key);
        Assert.Equal(0, res[0].Distance, 9);
        Assert.True(res[1].Distance > res[0].Distance);
    }

    [Fact]
    public async Task Forgetting_RemovesEveryVoiceOfThatAccount()
    {
        var svc = Host();
        await svc.EnrollAsync("p7", "کریم", Feat(20, 1));
        await svc.EnrollAsync("p7", "کریم", Feat(20, 2));

        Assert.True(await svc.ForgetAsync("p7"));
        Assert.Equal(0, await svc.CountAsync("p7"));
        Assert.False(await svc.ForgetAsync("p7"));
    }

    /// <summary>
    /// ‎_VX_ACCEPT‎ و ‎_VX_MARGIN‎ — نزدیک‌ترین بودن کافی نیست: باید هم به‌قدرِ
    /// کافی نزدیک باشد، هم از نفرِ دوم به‌قدرِ کافی جلوتر. وگرنه پرسیده می‌شود.
    /// </summary>
    [Fact]
    public void Sure_NeedsBothTheThresholdAndTheGap()
    {
        Assert.False(VoiceDataService.IsSure(Array.Empty<VoiceMatch>()));

        // تنها نامزد و نزدیک → مطمئن
        Assert.True(VoiceDataService.IsSure(new[] { new VoiceMatch("p1", "الف", 1.0) }));

        // نزدیک ولی دومی هم چسبیده → بپرس
        Assert.False(VoiceDataService.IsSure(new[]
        {
            new VoiceMatch("p1", "الف", 1.0),
            new VoiceMatch("p2", "ب", 1.05),
        }));

        // نزدیک و دومی به‌قدرِ کافی دور → مطمئن
        Assert.True(VoiceDataService.IsSure(new[]
        {
            new VoiceMatch("p1", "الف", 1.0),
            new VoiceMatch("p2", "ب", 1.2),
        }));

        // از حدِ پذیرش دورتر → هرچقدر هم فاصلهٔ دومی زیاد باشد، مطمئن نیست
        Assert.False(VoiceDataService.IsSure(new[]
        {
            new VoiceMatch("p1", "الف", 2.5),
            new VoiceMatch("p2", "ب", 9.0),
        }));
    }

    [Fact]
    public async Task AViewer_CannotTeachTheApp()
    {
        var svc = Host(UserRole.Viewer);
        await Assert.ThrowsAsync<PermissionDeniedException>(
            () => svc.EnrollAsync("p7", "کریم", Feat(20, 1)));
    }
}
