using System.Text.Json.Nodes;
using PumpYaqobi.App.Services;
using PumpYaqobi.App.Update;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ تنظیماتِ «قابلِ بردن» و «نصب از فایل» — ۱۴۰۵/۰۷/۱۵ ══════════════════
/// ⛔ مهم‌ترینِ این‌ها: هیچ رازی (توکن، مجوز، کلید، رمزِ سرور) و هیچ بندی از
/// این کامپیوتر (کدِ پمپ، نشانیِ سرور، حساب) به فایلی که روی فلش می‌گردد
/// نمی‌رود — و هنگامِ آوردن هم هیچ‌کدام خوانده نمی‌شود.
/// </summary>
public class PortableSettingsTests
{
    private static AppSettings Sample() => new()
    {
        ThemeId = "gold", NavOrder = "safe,dashboard,debts",
        CalcWidth = 400, CalcHeight = 580, CalcLarge = true, ParchaChainCheck = false,
        TableBorderColor = "#336699", TableLine = 2, TableHeadLine = 3, TableSumLine = 4,
        NoteFontScale = 1.24, ReportErrorsOff = true,
        SecFontScales = new() { ["safe"] = 1.4, ["waraq"] = 0.84 },
        ColumnWidths = new() { ["debt-7|تاریخ|نام"] = new[] { 90.5, 210, 64 } },
        // رازها و بندها — نباید بروند
        CloudAccountToken = "ACCT-SECRET-1", CloudRefreshToken = "REFRESH-SECRET-2",
        CloudDeviceToken = "DEVICE-SECRET-3", CloudLicense = "LICENSE-SECRET-4",
        CloudPublicKey = "PUBKEY-5", ServerToken = "HOME-WRITE-6", ServerReadKey = "READKEY-7",
        ServerUrl = "http://192.168.1.50:4700", StationCode = "p1b5feb57", CloudStationId = "st-9",
        CloudEmail = "someone@example.com", CloudUserId = "u-10", CloudAccessCode = "12345678",
        LastSection = "safe", WindowWidth = 777, LastPrinter = "EPSON L382",
    };

    [Fact]
    public void HichRaziVaHichBandi_BeFileNemiravad()
    {
        var json = PortableSettings.Capture(Sample());
        foreach (var secret in new[] { "SECRET", "PUBKEY-5", "HOME-WRITE-6", "READKEY-7", "192.168.1.50",
                                       "p1b5feb57", "st-9", "example.com", "u-10", "12345678", "EPSON", "777" })
            Assert.DoesNotContain(secret, json);
        foreach (var name in new[] { "Cloud", "Server", "Station", "Token", "License", "Window", "LastSection", "LastPrinter", "Entitled" })
            Assert.DoesNotContain(name, json);

        //  ⛔ فقط همان نام‌های فهرستِ سفید (به‌علاوهٔ «v»)
        var keys = JsonNode.Parse(json)!.AsObject().Select(k => k.Key).Where(k => k != "v").OrderBy(k => k);
        Assert.Subset(PortableSettings.Fields.ToHashSet(), keys.ToHashSet());
    }

    [Fact]
    public void RaftoBargasht_HamanTanzimat()
    {
        var src = Sample();
        var json = PortableSettings.Capture(src);
        var dst = new AppSettings();
        var done = PortableSettings.ApplyTo(json, dst);

        Assert.Equal("gold", dst.ThemeId);
        Assert.Equal("safe,dashboard,debts", dst.NavOrder);
        Assert.Equal((400d, 580d, true, false), (dst.CalcWidth, dst.CalcHeight, dst.CalcLarge, dst.ParchaChainCheck));
        Assert.Equal(("#336699", 2d, 3d, 4d), (dst.TableBorderColor, dst.TableLine, dst.TableHeadLine, dst.TableSumLine));
        Assert.Equal(1.24, dst.NoteFontScale);
        Assert.True(dst.ReportErrorsOff);
        Assert.Equal(1.4, dst.SecFontScales["safe"]);
        Assert.Equal(new[] { 90.5, 210, 64 }, dst.ColumnWidths["debt-7|تاریخ|نام"]);
        Assert.Contains(nameof(AppSettings.ThemeId), done);

        //  ⛔ هیچ رازی از این راه نمی‌نشیند — حتی اگر کسی در فایل بنویسدش
        var evil = JsonNode.Parse(json)!.AsObject();
        evil["CloudDeviceToken"] = "INJECTED"; evil["ServerUrl"] = "http://evil"; evil["StationCode"] = "other";
        evil["CloudPublicKey"] = "evil-key";
        var target = new AppSettings { CloudDeviceToken = "mine", ServerUrl = "http://home", StationCode = "p1" };
        PortableSettings.ApplyTo(evil.ToJsonString(), target);
        Assert.Equal(("mine", "http://home", "p1", ""), (target.CloudDeviceToken, target.ServerUrl, target.StationCode, target.CloudPublicKey));
    }

    [Fact]
    public void MeghdareKharab_NadideGerefteMishavad_NaShekast()
    {
        var s = new AppSettings { ThemeId = "blue", TableBorderColor = "", NavOrder = "" };
        var bad = """
        {"v":1,"ThemeId":123,"NavOrder":"safe<script>","TableBorderColor":"red;drop",
         "CalcWidth":1e9,"TableLine":-5,"NoteFontScale":99,
         "SecFontScales":{"../x":2,"safe":"big","waraq":50},
         "ColumnWidths":{"k":[1,-3],"ok":[10,20]},"PrintSetup":{"Paper":42}}
        """;
        PortableSettings.ApplyTo(bad, s);
        Assert.Equal("blue", s.ThemeId);                 // نوعِ غلط
        Assert.Equal("", s.NavOrder);                    // نویسهٔ غلط
        Assert.Equal("", s.TableBorderColor);            // رنگِ ناخوانا
        Assert.Equal(900, s.CalcWidth);                  // بریده شد
        Assert.Equal(1, s.TableLine);
        Assert.Equal(2.5, s.NoteFontScale);
        Assert.False(s.SecFontScales.ContainsKey("../x"));
        Assert.False(s.SecFontScales.ContainsKey("safe"));
        Assert.Equal(2.2, s.SecFontScales["waraq"]);
        Assert.False(s.ColumnWidths.ContainsKey("k"));
        Assert.True(s.ColumnWidths.ContainsKey("ok"));

        var before = PortableSettings.Capture(s);
        Assert.Empty(PortableSettings.ApplyTo("not json {", s));
        Assert.Empty(PortableSettings.ApplyTo(null, s));
        Assert.Empty(PortableSettings.ApplyTo("[1,2]", s));
        Assert.Equal(before, PortableSettings.Capture(s));
    }

    // ══ «نصب از فایل» — کهنه‌تر رد، تازه‌تر پذیرفته ═══════════════════════

    [Theory]
    [InlineData("3.1.201.0", OfflineInstaller.Verdict.Newer, true)]
    [InlineData("3.2.0.0", OfflineInstaller.Verdict.Newer, true)]
    [InlineData("3.1.200.0", OfflineInstaller.Verdict.Same, true)]
    [InlineData("3.1.199.0", OfflineInstaller.Verdict.Older, false)]
    [InlineData("2.9.466", OfflineInstaller.Verdict.Older, false)]
    [InlineData("", OfflineInstaller.Verdict.Unreadable, false)]
    [InlineData("abc", OfflineInstaller.Verdict.Unreadable, false)]
    public void NasbAzFile_KohneRad_TazePazirofte(string ver, OfflineInstaller.Verdict want, bool run)
    {
        var d = OfflineInstaller.Decide(OfflineInstaller.ProductName, ver, "3.1.200");
        Assert.Equal(want, d.Kind);
        Assert.Equal(run, d.CanRun);
    }

    [Fact]
    public void NasbAzFile_FaghatFayleKhodeBarname()
    {
        Assert.Equal(OfflineInstaller.Verdict.NotOurs, OfflineInstaller.Decide("Some Other App", "9.9.9.9", "3.1.200").Kind);
        Assert.Equal(OfflineInstaller.Verdict.NotOurs, OfflineInstaller.Decide(null, "9.9.9.9", "3.1.200").Kind);
        Assert.False(OfflineInstaller.Inspect("/no/such/file.exe", "3.1.200").CanRun);
        Assert.Equal("3.1.200", OfflineInstaller.Normalize("3.1.200.0"));
    }
}
