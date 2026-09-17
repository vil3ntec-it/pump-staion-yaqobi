using System.Net;
using System.Net.Http;
using Microsoft.EntityFrameworkCore;
using PumpYaqobi.App.Services;
using PumpYaqobi.Services.Data;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ زیرساختِ اشتراکی و چندمشتری ═══════════════════════════════════════════
///
/// خواستهٔ صاحب ریپو (۱۴۰۵/۰۶/۳۰): «زیرساختِ فنیِ لازم برای برنامهٔ اشتراکی،
/// چندمشتری و قابلِ مدیریت در مقیاسِ بالا را بررسی و در صورتِ مشکل اصلاح کن.»
///
/// این آزمون‌ها همان چهار چیزی را قفل می‌کنند که در بررسی خراب بودند — و
/// شرحِ کاملشان در <c>native/docs/INFRA-fa.md</c> است.
/// </summary>
//  ⚠️ `AppSettings.DirOverride` **استاتیک** است و xUnit کلاس‌ها را موازی
//  می‌دواند؛ بی این نشان، این کلاس و هر کلاسِ دیگری که همان را عوض
//  می‌کند روی هم می‌نویسند و آزمون‌ها **گاهی** سرخ می‌شوند.
[Collection(AppHostCollection.Name)]
public class InfraTests : IDisposable
{
    private readonly string _dir =
        Path.Combine(Path.GetTempPath(), "pump-infra-" + Guid.NewGuid().ToString("N"));

    /// <summary>
    /// ⚠️ پوشهٔ قبلی نگه داشته می‌شود و سرِ پاک‌سازی برمی‌گردد. پیش از این
    /// <c>null</c> می‌شد — یعنی آزمونِ بعدی به <b>تنظیماتِ واقعیِ خودِ
    /// کاربر</b> (<c>%APPDATA%\PumpYaqobi</c>) می‌نوشت.
    /// </summary>
    private readonly string? _was;

    public InfraTests()
    {
        _was = AppSettings.DirOverride;
        Directory.CreateDirectory(_dir);
        AppSettings.DirOverride = _dir;
    }

    public void Dispose()
    {
        AppSettings.DirOverride = _was;
        CloudLink.TestTransport = null;
        try { Directory.Delete(_dir, true); } catch { }
    }

    private static HttpResponseMessage Json(HttpStatusCode code, string body) =>
        new(code) { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") };

    // ── ۱) هویتِ نسخه روی هر درخواست ──────────────────────────────────

    [Fact]
    public void HarDarkhast_ShomareyeNoskhe_RaMibarad()
    {
        var req = new HttpRequestMessage(HttpMethod.Get, "https://example.invalid/x");
        CloudLink.Stamp(req);

        Assert.True(req.Headers.Contains("X-App-Version"));
        Assert.True(req.Headers.Contains("X-App-Platform"));
        var ua = string.Join("", req.Headers.GetValues("User-Agent"));
        Assert.StartsWith("PumpYaqobi/", ua);

        //  ⛔ هیچ چیزِ شناسایی‌کنندهٔ کاربر در هدرها نیست
        var all = string.Join("\n", req.Headers.Select(h => h.Key + ": " + string.Join(",", h.Value)));
        Assert.DoesNotContain(Environment.MachineName, all);
        Assert.DoesNotContain(Environment.UserName, all);
    }

    // ── ۲) شناسهٔ پمپ قفل است: دستگاه بی‌صدا به پمپِ دیگر نمی‌رود ──────

    [Fact]
    public async Task ShenaseyePump_Ghofl_Ast_VaJabejaNemishavad()
    {
        var s = new AppSettings
        {
            CloudDeviceToken = "pd_test",
            CloudStationId = "stn_ALEF",
            CloudPublicKey = "key",
        };
        var link = new CloudLink(s, () => Task.CompletedTask);

        //  سرور (یا یک اشتباه) می‌گوید این دستگاه مالِ پمپِ دیگری است
        CloudLink.TestTransport = (req, _) => Task.FromResult(
            Json(HttpStatusCode.OK, """{"station":{"id":"stn_BE"},"entitlement":null}"""));

        var res = await link.RefreshAsync();

        Assert.False(res.Ok);
        Assert.Equal("station_mismatch", res.Code);
        //  و مهم‌تر: تنظیمات دست‌نخورده ماند
        Assert.Equal("stn_ALEF", s.CloudStationId);
    }

    [Fact]
    public async Task HamanPump_RaKe_GhoflKarde_Miped(/* پمپِ خودش رد نمی‌شود */)
    {
        var s = new AppSettings
        {
            CloudDeviceToken = "pd_test",
            CloudStationId = "stn_ALEF",
            CloudPublicKey = "key",
        };
        var link = new CloudLink(s, () => Task.CompletedTask);
        CloudLink.TestTransport = (req, _) => Task.FromResult(
            Json(HttpStatusCode.OK, """{"station":{"id":"stn_ALEF"},"entitlement":null}"""));

        var res = await link.RefreshAsync();

        Assert.True(res.Ok);
        Assert.Equal("stn_ALEF", s.CloudStationId);
    }

    // ── ۳) جدا کردنِ دستگاه از پمپ — بی لمسِ دفتر ──────────────────────

    [Fact]
    public async Task JodaKardan_HameyeBandhayePump_RaBazMikonad_VaDaftarDastNemikhorad()
    {
        var s = new AppSettings
        {
            CloudDeviceToken = "pd_test",
            CloudStationId = "stn_ALEF",
            CloudLicense = "lic",
            CloudPublicKey = "key",
            CloudAccessCode = "K7PM-3XQ2",
            EntitledUntil = 999,
            EntitledPlan = "VIP",
            ServerUrl = "http://192.168.1.9:4700",
            ServerToken = "write",
            ServerReadKey = "read",
            ServerId = "srv",
            //  این‌ها مالِ حساب و خودِ دستگاه‌اند، نه پمپ
            CloudAccountToken = "acc",
            CloudEmail = "a@b.c",
            CloudDeviceUid = "pc-123",
            StationCode = "pump1",
        };
        var link = new CloudLink(s, () => Task.CompletedTask);

        await link.ForgetStationAsync();

        Assert.Equal("", s.CloudDeviceToken);
        Assert.Equal("", s.CloudStationId);
        Assert.Equal("", s.CloudLicense);
        Assert.Equal("", s.CloudPublicKey);
        Assert.Equal("", s.CloudAccessCode);
        Assert.Equal(0, s.EntitledUntil);
        Assert.Equal("", s.ServerUrl);
        Assert.Equal("", s.ServerToken);
        Assert.Equal("", s.ServerReadKey);

        //  ⚠️ شناسهٔ دستگاه می‌ماند (مجوزِ بعدی به همین بسته می‌شود) و
        //  دفترِ کاربر هم هیچ‌جا لمس نمی‌شود
        Assert.Equal("pc-123", s.CloudDeviceUid);
    }

    [Fact]
    public void JodaKardan_HichDastoorieDatabase_Nemizanad()
    {
        //  ⚠️ **چرا از روی سورس و نه با شمارندهٔ `DbWatch`**: آن شمارنده
        //  مالِ کلِ فرآیند است و آزمون‌های موازیِ دیگر هم بالایش می‌برند —
        //  یک بار همین‌جا سبزِ دروغ و بعد قرمزِ تصادفی داد. شاهدِ درست
        //  خودِ بدنهٔ تابع است: جز `_settings`ِ در حافظه و ذخیرهٔ همان،
        //  هیچ سرویس و هیچ دیتابیسی در کار نیست.
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
            "..", "..", "..", "..", "PumpYaqobi.App", "Services"));
        var src = File.ReadAllText(Path.Combine(root, "CloudLink.cs"));
        var i = src.IndexOf("public async Task ForgetStationAsync()", StringComparison.Ordinal);
        Assert.True(i > 0, "ForgetStationAsync پیدا نشد.");
        var body = src[i..src.IndexOf("\n    }", i, StringComparison.Ordinal)];

        foreach (var forbidden in new[] { "Db", "Debtors", "db.", "SaveChanges", "Delete", "Remove" })
            Assert.DoesNotContain(forbidden, body);

        //  و آن‌چه باید باشد: فقط تنظیمات، و ذخیره‌اش
        Assert.Contains("_settings.CloudDeviceToken = \"\"", body);
        Assert.Contains("SaveQuiet", body);
    }

    // ── ۴) حسابِ پمپِ دیگر، نشانیِ پمپِ دیگر را نمی‌نشاند ───────────────

    [Fact]
    public async Task HesabePumpeDigar_NeshaniyeKhodash_RaRoyeInPump_Nemineshanad()
    {
        var s = new AppSettings
        {
            CloudAccountToken = "acc",
            CloudDeviceToken = "pd_test",   // یعنی این نصب فعال شده
            CloudStationId = "stn_ALEF",    // و روی همین پمپ قفل است
            StationCode = "pump1",
        };
        var link = new CloudLink(s, () => Task.CompletedTask);
        //  حسابِ پمپِ دیگر — شناسهٔ ابریِ دیگری می‌دهد
        CloudLink.TestTransport = (req, _) => Task.FromResult(Json(HttpStatusCode.OK,
            """{"station":{"id":"stn_BE"},"home":{"url":"http://10.0.0.5:4700","readKey":"rk","station":"pump-digar"}}"""));

        var (ok, url, readKey, _, why) = await link.HomeFromAccountAsync();

        Assert.False(ok);
        Assert.Equal("", url);
        Assert.Equal("", readKey);
        Assert.Contains("پمپِ دیگری", why);
    }

    [Fact]
    public async Task HesabeHamanPump_Neshani_RaMidahad()
    {
        var s = new AppSettings
        {
            CloudAccountToken = "acc",
            CloudDeviceToken = "pd_test",
            CloudStationId = "stn_ALEF",
            StationCode = "pump1",
        };
        var link = new CloudLink(s, () => Task.CompletedTask);
        //  ⚠️ کدِ ابر («pump-cloud») با کدِ محلی («pump1») یکی نیست و این
        //  اشکالی ندارد — سنجش روی شناسه است.
        CloudLink.TestTransport = (req, _) => Task.FromResult(Json(HttpStatusCode.OK,
            """{"station":{"id":"stn_ALEF"},"home":{"url":"http://10.0.0.5:4700","readKey":"rk","station":"pump-cloud"}}"""));

        var (ok, url, readKey, station, _) = await link.HomeFromAccountAsync();

        Assert.True(ok);
        Assert.Equal("http://10.0.0.5:4700", url);
        Assert.Equal("rk", readKey);
        Assert.Equal("pump-cloud", station);
    }

    // ── ۵) «قفل بود، صبر کن» روی هر اتصال ─────────────────────────────

    [Fact]
    public void HarEtesal_BusyTimeout_Darad()
    {
        var file = Path.Combine(_dir, "busy.db");
        var dbf = new PumpDbFactory(file);
        dbf.EnsureReady();

        using var db = dbf.Create();
        var conn = db.Database.GetDbConnection();
        conn.Open();
        try
        {
            string Ask(string pragma)
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "PRAGMA " + pragma + ";";
                return cmd.ExecuteScalar()?.ToString() ?? "";
            }

            //  پیش از اصلاح این عدد صفر بود — سنجیده شد، حدس نبود
            Assert.True(int.TryParse(Ask("busy_timeout"), out var busy) && busy >= 1000,
                        "انتظارِ قفل باید روی خودِ اتصال باشد، نه فقط حلقهٔ ارائه‌دهنده.");
            Assert.Equal("1", Ask("foreign_keys"));
            Assert.Equal("wal", Ask("journal_mode"));
        }
        finally { conn.Close(); }
    }

    // ── ۶) پشتیبان جریانی می‌رود، نه یک‌جا در حافظه ────────────────────

    [Fact]
    public void Poshtiban_FileRa_YekJa_DarHafeze_Nemikhanad()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
            "..", "..", "..", "..", "PumpYaqobi.App", "Services"));
        var src = File.ReadAllText(Path.Combine(root, "BackupPusher.cs"));
        //  ⚠️ روی خودِ **کد** می‌گردیم، نه توضیحات: نامِ قدیمی در کامنتِ
        //  «پیش از این چه بود» هست و باید هم باشد.
        var code = string.Join("\n", src.Split('\n')
            .Where(l => !l.TrimStart().StartsWith("//")));

        Assert.DoesNotContain("ReadAllBytesAsync", code);
        Assert.Contains("StreamContent", code);
        Assert.Contains("CloudLink.Stamp(req)", code);
    }

    // ── ۷) بازگردانی واقعاً کار می‌کند (نه فقط فایلِ پشتیبان) ───────────
    //
    //  ⚠️ قاعدهٔ ۱۴ی خودِ صاحب ریپو: «فقط داشتنِ فایلِ پشتیبان کافی نیست؛
    //  فرآیندِ Restore هم باید آزمایش شود.» تا امروز هیچ آزمونی دفترِ
    //  عوض‌شده را از یک پشتیبان برنمی‌گرداند. این یکی همان را می‌کند: داده
    //  می‌سازد، پشتیبان می‌گیرد، دفتر را **خراب** می‌کند، برمی‌گرداند و
    //  دوباره می‌خواند.

    [Fact]
    public async Task Bazgardani_DaftarRa_Vaghean_Barmigardanad()
    {
        var file = Path.Combine(_dir, "restore.db");
        var dbf = new PumpDbFactory(file);
        dbf.EnsureReady();

        var session = new PumpYaqobi.Application.Security.UserSession();
        session.SignIn(PumpYaqobi.Domain.Enums.UserRole.Admin, "آزمون");
        var perm = new PumpYaqobi.Application.Security.PermissionService(session);
        var backup = new BackupService(dbf, perm);

        //  یک قرض‌دار که باید بعد از بازگردانی برگردد
        await using (var db = dbf.Create())
        {
            db.Debtors.Add(new PumpYaqobi.Domain.Entities.Debtor
            { Name = "کریمِ پیش از پشتیبان", LegacyId = "p-restore" });
            await db.SaveChangesAsync();
        }

        var snap = Path.Combine(_dir, "snap.db");
        backup.WriteSnapshot(snap);
        Assert.True(File.Exists(snap), "پشتیبان ساخته نشد.");
        Assert.True(BackupService.Inspect(snap) > 0, "پشتیبان خوانده نشد.");

        //  دفتر را عوض می‌کنیم — همان «خرابیِ» بعد از پشتیبان
        await using (var db = dbf.Create())
        {
            var gone = db.Debtors.First(d => d.LegacyId == "p-restore");
            db.Debtors.Remove(gone);
            db.Debtors.Add(new PumpYaqobi.Domain.Entities.Debtor
            { Name = "ردیفِ اشتباهِ بعدی", LegacyId = "p-wrong" });
            await db.SaveChangesAsync();
        }

        var res = backup.Restore(snap);

        Assert.True(res.Ok, res.Message);
        Assert.NotNull(res.SafetyCopy);
        Assert.True(File.Exists(res.SafetyCopy!), "عکسِ ایمنیِ پیش از بازگردانی باید بماند.");

        await using (var db = dbf.Create())
        {
            Assert.Single(db.Debtors.Where(d => d.LegacyId == "p-restore"));
            Assert.Empty(db.Debtors.Where(d => d.LegacyId == "p-wrong"));
        }
    }
}
