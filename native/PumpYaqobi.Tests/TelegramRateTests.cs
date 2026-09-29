using System.Net;
using System.Text.Json;
using PumpYaqobi.App.Services;
using PumpYaqobi.Domain.Enums;
using PumpYaqobi.Services.Data;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// صاحب ریپو (۱۴۰۵/۰۷/۱۶): «نرخِ اتحادیه رو توی تلگرام بنویسم — پطرول ۴۵
/// دیزل ۹۹ — و اتومات توی برنامهٔ نیتیوِ کامپیوتر لایف آپدیت کنه… روی
/// حساب‌های دیگهٔ کاربران تأثیری نذاره.»
/// </summary>
[Collection(AppHostCollection.Name)]
public class TelegramRateTests : IDisposable
{
    public void Dispose()
    {
        CloudLink.TestTransport = null;
        GC.SuppressFinalize(this);
    }

    private static JsonElement J(string s) => JsonDocument.Parse(s).RootElement;

    [Fact]
    public void Farman_Khande_Mishavad_VaNerkheNamomken_Nemineshinad()
    {
        var c = CloudLink.ParseRateCommand(J("""{"ok":true,"cmd":{"id":"rate_1","petrol":79,"diesel":80.5,"by":"میرزا"}}"""));
        Assert.NotNull(c);
        Assert.Equal(79m, c!.Petrol);
        Assert.Equal(80.5m, c.Diesel);
        Assert.Equal("پطرول 79 · دیزل 80.5", CloudLink.RateLine(c));

        var onlyP = CloudLink.ParseRateCommand(J("""{"cmd":{"id":"r2","petrol":79,"diesel":null}}"""));
        Assert.Null(onlyP!.Diesel);

        Assert.Null(CloudLink.ParseRateCommand(J("""{"ok":true,"cmd":null}""")));
        Assert.Null(CloudLink.ParseRateCommand(J("""{"cmd":{"id":"r3","petrol":4,"diesel":900}}""")));
        Assert.Null(CloudLink.ParseRateCommand(J("""{"cmd":{"petrol":79}}""")));
    }

    [Fact]
    public async Task Farman_Az_DareDastgah_Mi_Ayad_Va_Neshast_Gofte_Mishavad()
    {
        var hits = new List<(string Method, string Path, string Body, string Auth)>();
        CloudLink.TestTransport = async (req, _) =>
        {
            var body = req.Content is null ? "" : await req.Content.ReadAsStringAsync();
            hits.Add((req.Method.Method, req.RequestUri!.AbsolutePath, body, req.Headers.Authorization?.ToString() ?? ""));
            var json = req.RequestUri!.AbsolutePath == "/api/pump/device/rate"
                ? """{"ok":true,"cmd":{"id":"rate_9","petrol":79,"diesel":null,"by":"میرزا"}}"""
                : """{"ok":true,"status":"applied"}""";
            return new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json") };
        };
        var s = new AppSettings { CloudDeviceToken = "pd_test_device" };
        var cloud = new CloudLink(s, () => Task.CompletedTask);

        var cmd = await cloud.RateCommandAsync();
        Assert.NotNull(cmd);
        Assert.Equal("rate_9", cmd!.Id);
        Assert.Equal(("GET", "/api/pump/device/rate"), (hits[0].Method, hits[0].Path));
        //  ⛔ فقط با توکنِ دستگاهِ همین پمپ — سرور پمپ را از همان پیدا می‌کند
        Assert.Contains("pd_test_device", hits[0].Auth);

        Assert.True((await cloud.RateAckAsync(cmd.Id, true)).Ok);
        Assert.Equal(("POST", "/api/pump/device/rate/rate_9/ack"), (hits[1].Method, hits[1].Path));
        Assert.Contains("\"applied\":true", hits[1].Body);
    }

    [Fact]
    public async Task BiDastgah_HichDarkhasti_Nemiravad()
    {
        var n = 0;
        CloudLink.TestTransport = (_, _) => { n++; return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)); };
        var cloud = new CloudLink(new AppSettings(), () => Task.CompletedTask);
        Assert.Null(await cloud.RateCommandAsync());
        Assert.Equal(0, n);
    }

    [Fact]
    public async Task Neshandan_HamanDoTanzim_VaTarikhche_TileNagofteDastNemikhorad()
    {
        var host = AppHost.Start(
            Path.Combine(Path.GetTempPath(), "pump-tgrate-" + Guid.NewGuid().ToString("N"), "pump.db"));
        if (host.Auth.NeedsFirstRun()) host.Auth.CreateFirstAdmin("1234");
        host.Auth.SignIn("admin", "1234");
        host.Settings.Set(SettingsService.UnionRateDiesel, 88m);

        string? heard = null;
        void On(string k, string? _) { if (k == SettingsService.UnionRatePetrol) heard = k; }
        SettingsService.Written += On;
        try
        {
            await UnionRateApply.ApplyAsync(host, 79m, null);
        }
        finally { SettingsService.Written -= On; }

        Assert.Equal(79m, host.Settings.GetDecimal(SettingsService.UnionRatePetrol));
        Assert.Equal(88m, host.Settings.GetDecimal(SettingsService.UnionRateDiesel));
        Assert.NotNull(heard);   // ⇐ صفحهٔ باز همان لحظه تازه می‌شود
        var hist = await host.Tools.RateHistoryAsync();
        Assert.Contains(hist, r => r.Fuel == FuelType.Petrol && r.Rate == 79m);
        Assert.DoesNotContain(hist, r => r.Fuel == FuelType.Diesel && r.Rate == 0m);
    }

    [Fact]
    public void Barname_HarDaghighe_Mipursad_VaSafhe_ZendeTazeMishavad()
    {
        string R(params string[] p) => File.ReadAllText(Path.Combine(new[] { Root() }.Concat(p).ToArray()));
        var pub = R("PumpYaqobi.App", "Services", "StationPublisher.cs");
        Assert.Contains("await RateTickAsync(ct);", pub);
        Assert.True(pub.IndexOf("await CloudKeepAsync(ct);", StringComparison.Ordinal)
                    < pub.IndexOf("await RateTickAsync(ct);", StringComparison.Ordinal));
        Assert.Contains("UnionRateApply.ApplyAsync(_host, cmd.Petrol, cmd.Diesel)", pub);
        Assert.Contains("RateAckAsync(cmd.Id, applied, note, ct)", pub);
        var profit = R("PumpYaqobi.App", "ViewModels", "Sections", "ProfitSectionViewModel.cs");
        Assert.Contains("SettingsService.Written +=", profit);
        Assert.Contains("RefillUnion", profit);
    }

    private static string Root()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null && !File.Exists(Path.Combine(d.FullName, "PumpYaqobi.sln"))) d = d.Parent;
        return d?.FullName ?? throw new InvalidOperationException("ریشه پیدا نشد");
    }
}
