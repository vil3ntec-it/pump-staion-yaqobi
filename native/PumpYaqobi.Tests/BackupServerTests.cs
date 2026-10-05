using System.Net;
using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using PumpYaqobi.App.Services;
using PumpYaqobi.App.ViewModels.Sections;
using PumpYaqobi.Application.Security;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;
using PumpYaqobi.Services.Data;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ «بک‌اپ به سرور نمی‌رود… روی کامپیوترِ دیگر هیچ اطلاعاتی نداد» (۱۴۰۵/۰۷/۲۰) ══
/// سه ریشه: آپلود با مهلتِ بیست‌ثانیه‌ای، دفترِ خالیِ کامپیوترِ تازه که جای بکاپ‌های
/// واقعی را می‌گرفت، و نبودنِ هیچ راهی برای پس گرفتنِ بکاپ از سرور.
/// </summary>
[Collection(AppHostCollection.Name)]
public class BackupServerTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "pump-bsrv-" + Guid.NewGuid().ToString("N"));
    public BackupServerTests() => Directory.CreateDirectory(_dir);
    public void Dispose()
    {
        CloudLink.TestTransport = null;
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(_dir, true); } catch { }
    }

    [Fact]
    public void DaftareKhali_Ferestade_Nemishavad()
    {
        Assert.NotNull(BackupPusher.SkipReason(-1));
        Assert.Contains("خالی", BackupPusher.SkipReason(0));
        Assert.Null(BackupPusher.SkipReason(1));
    }

    [Fact]
    public void FaghatSarvareKhanegi_Zard_AstVaDalileSarvareHesabRaMigooyad()
    {
        var (text, brush) = BackupSectionViewModel.ServerResult(true, home: true, cloud: false, why: "",
                                                                cloudWhy: "سرور دیر جواب داد");
        Assert.Equal("Pump.Warn", brush);
        Assert.Contains("سرور دیر جواب داد", text);
        Assert.Equal("Pump.Ok", BackupSectionViewModel.ServerResult(true, true, true, "").Brush);
    }

    [Fact]
    public void Ferestadan_BaMohlateBoland_NaBistSaniye()
    {
        var src = SrcText.Read(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
            "../../../../PumpYaqobi.Shell/Services/CloudLink.Support.cs")));
        var up = src[src.IndexOf("BackupUploadAsync(", StringComparison.Ordinal)..];
        up = up[..up.IndexOf("BackupDownloadAsync", StringComparison.Ordinal)];
        Assert.Contains("SendOn(BigHttp, req, ct)", up);
        Assert.DoesNotContain("await Send(req, ct)", up);
        Assert.True(CloudLink.BigHttp.Timeout >= TimeSpan.FromMinutes(10));
    }

    private static CloudLink Linked() =>
        new(new AppSettings { CloudDeviceToken = "pd_test_device_token_x" }, () => Task.CompletedTask);

    private static HttpResponseMessage FileReply(byte[] data, string sha)
    {
        var r = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(data) };
        r.Headers.TryAddWithoutValidation("X-Backup-Sha256", sha);
        return r;
    }

    [Fact]
    public async Task GereftanAzSarvar_BaHash_VaFayleDastKhordeRadMishavad()
    {
        var data = RandomNumberGenerator.GetBytes(300_000);
        var sha = Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();
        string? asked = null;
        CloudLink.TestTransport = (req, _) =>
        {
            asked = req.RequestUri!.AbsolutePath;
            return Task.FromResult(FileReply(data, sha));
        };
        var target = Path.Combine(_dir, "got.db");
        var ok = await Linked().BackupDownloadAsync("bak_123", target);
        Assert.True(ok.Ok, ok.Why);
        Assert.Equal("/api/pump/device/backups/bak_123", asked);
        Assert.Equal(data, await File.ReadAllBytesAsync(target));

        //  هشِ ناجور ⇒ هیچ فایلی نمی‌نشیند
        CloudLink.TestTransport = (_, _) => Task.FromResult(FileReply(data, new string('0', 64)));
        var bad = Path.Combine(_dir, "bad.db");
        var no = await Linked().BackupDownloadAsync("bak_123", bad);
        Assert.False(no.Ok);
        Assert.False(File.Exists(bad));
        Assert.False(File.Exists(bad + ".part"));
    }

    // ── بکاپِ خودِ همین حساب از سرور: از درِ اشتراک رد نمی‌شود ───────────────

    [Fact]
    public void BackapeKhodeHesab_AzSarvar_BaTrial_HamBarmigardad_VaDarBaghiAsh_Baste()
    {
        var src = new PumpDbFactory(Path.Combine(_dir, "src.db"));
        src.EnsureReady();
        using (var db = src.Create()) { db.Debtors.Add(new Debtor { Name = "خودم" }); db.SaveChanges(); }
        var session = new UserSession();
        session.SignIn(UserRole.Admin, "مدیر");
        var file = Path.Combine(_dir, "snap.db");
        new BackupService(src, new PermissionService(session)).WriteSnapshot(file);

        var dst = new PumpDbFactory(Path.Combine(_dir, "dst.db"));
        dst.EnsureReady();
        var perm = new PermissionService(session) { RestoreGate = () => AppLock.RestoreTrial };
        var backup = new BackupService(dst, perm);

        Assert.Throws<PermissionDeniedException>(() => backup.Restore(file));
        using (PermissionService.TrustOwnCloudBackup())
            Assert.True(backup.Restore(file).Ok);
        using (var db = dst.Create()) Assert.Single(db.Debtors.ToList());
        //  بیرونِ همان در، دوباره بسته
        Assert.Throws<PermissionDeniedException>(() => backup.Restore(file));
    }
}
