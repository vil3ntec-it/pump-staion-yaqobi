using System.Text.Json;
using PumpYaqobi.App.Services;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;
using PumpYaqobi.Reporting.Pdf;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ عکس و PDF از خودِ برنامه برای باتِ تلگرام (۱۴۰۵/۰۷/۲۱) ══════════════════
///
/// خواستهٔ صاحب ریپو: «همان عکس‌های برنامه… حسابِ فلانی با رسید، بردگی، الباقی
/// و واحد، و PDF و کیو‌آرش… کیفیت افت نکند… در سرور ذخیره نشود.»
///
///   ۱) درخواست‌ها از پاسخِ ‎/rate‎ درست خوانده می‌شوند و بدشکل‌ها کنار می‌روند.
///   ۲) حسابِ قرض‌دار ⇒ عکسِ PNG + همان PDFِ صفحهٔ حساب + لینکِ کیو‌آرِ همان حساب.
///   ۳) کارت همان عددهای سربرگِ حساب (‎AcctSnapshots‎) را دارد — نه عددِ تازه.
///   ۴) نامِ ناشناس یا چندتایی ⇒ «نشد» با پیامِ روشن، نه استثنا.
///   ۵) نشانهٔ حسابِ کیو‌آر عینِ ‎AcctLink.Build‎ِ لایهٔ رابط است.
/// </summary>
public class BotReportsTests
{
    private static AppHost Host()
    {
        var host = new AppHost(
            Path.Combine(Path.GetTempPath(), "pump-botrep-" + Guid.NewGuid().ToString("N"), "pump.db"));
        if (host.Auth.NeedsFirstRun()) host.Auth.CreateFirstAdmin("1234");
        host.Auth.SignIn("admin", "1234");
        return host;
    }

    private static BotRequest Req(string kind, object arg) =>
        new("rq_test", kind, JsonDocument.Parse(JsonSerializer.Serialize(arg)).RootElement.Clone());

    private static bool IsPng(byte[] b) => b.Length > 8 && b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4e && b[3] == 0x47;
    private static bool IsPdf(byte[] b) => b.Length > 5 && b[0] == (byte)'%' && b[1] == (byte)'P' && b[2] == (byte)'D' && b[3] == (byte)'F';

    [Fact]
    public void RequestsAreReadFromTheRatePollAndJunkIsDropped()
    {
        var json = JsonDocument.Parse("""
            {"ok":true,"cmd":null,"reqs":[
              {"id":"rq_a","kind":"tanks","arg":{}},
              {"id":"rq_b","kind":"debtor","arg":{"id":7}},
              {"id":"","kind":"tanks"},
              {"kind":"tanks"},
              "junk"
            ]}
            """).RootElement;
        var list = CloudLink.ParseBotRequests(json);
        Assert.Equal(2, list.Count);
        Assert.Equal("tanks", list[0].Kind);
        Assert.Equal(7, list[1].Arg.GetProperty("id").GetInt32());
        //  سرورِ کهنه (بی ‎reqs‎) ⇒ هیچ
        Assert.Empty(CloudLink.ParseBotRequests(JsonDocument.Parse("{\"ok\":true,\"cmd\":null}").RootElement));
    }

    [Fact]
    public async Task ADebtorGivesAPngCardTheSamePdfAndTheQrLink()
    {
        var host = Host();
        var person = await host.Debtors.AddDebtorAsync("هارون", null, false);
        var full = (await host.Debtors.LoadFullAsync(person.Id))!;
        var main = full.MainAccount;
        await host.Debtors.SaveRowAsync(new DebtRow
        {
            FuelAccountId = main.Id, DateShamsi = "1405/07/01", Fuel = FuelType.Petrol,
            Liters = 1000m, RasidFuel = 400m,
        });

        var rep = await BotReports.BuildAsync(host, Req("debtor", new { name = "هارون" }), CancellationToken.None);
        Assert.True(rep.Ok, rep.Error);
        Assert.Contains(rep.Files, f => f.Mime == "image/png" && IsPng(f.Data));
        Assert.Contains(rep.Files, f => f.Mime == "application/pdf" && IsPdf(f.Data));
        Assert.True(rep.Extra.TryGetValue("qr", out var qr));
        Assert.Contains("&a=d" + main.Id + "&", qr);
        Assert.Contains("الباقی", rep.Caption);

        //  همان عددِ سربرگِ حساب (‎AcctSnapshots‎): ۱۰۰۰ برد − ۴۰۰ رسید = ۶۰۰
        var again = (await host.Debtors.LoadFullAsync(person.Id))!;
        var snap = AcctSnapshots.ForDebtAccount("هارون", null, again.MainAccount, host.Debt);
        var albaqi = snap.Books[0].Summary.First(s => s[0] == "الباقی")[1];
        Assert.Contains(albaqi, rep.Caption);

        //  با شناسه هم همان
        var byId = await BotReports.BuildAsync(host, Req("debtor", new { id = person.Id }), CancellationToken.None);
        Assert.True(byId.Ok, byId.Error);
    }

    [Fact]
    public async Task UnknownOrAmbiguousNamesFailWithAClearMessage()
    {
        var host = Host();
        await host.Debtors.AddDebtorAsync("احمد کریمی", null, false);
        await host.Debtors.AddDebtorAsync("احمد رحیمی", null, false);

        var none = await BotReports.BuildAsync(host, Req("debtor", new { name = "ناشناس" }), CancellationToken.None);
        Assert.False(none.Ok);
        Assert.Contains("پیدا نشد", none.Error);

        var many = await BotReports.BuildAsync(host, Req("debtor", new { name = "احمد" }), CancellationToken.None);
        Assert.False(many.Ok);
        Assert.Contains("احمد کریمی", many.Error);

        var odd = await BotReports.BuildAsync(host, Req("nope", new { }), CancellationToken.None);
        Assert.False(odd.Ok);
    }

    [Fact]
    public async Task TanksWithoutARendererStillGiveTextAndNeverThrow()
    {
        var host = Host();
        var saved = BotReports.TankRenderer;
        BotReports.TankRenderer = null;
        try
        {
            var rep = await BotReports.BuildAsync(host, Req("tanks", new { }), CancellationToken.None);
            Assert.True(rep.Ok, rep.Error);
            Assert.Contains("پطرول", rep.Caption);
            Assert.Contains("دیزل", rep.Caption);
            Assert.DoesNotContain(rep.Files, f => f.Mime == "image/png");
        }
        finally { BotReports.TankRenderer = saved; }
    }

    [Fact]
    public void TheCardIsARealPng()
    {
        var png = new DebtorCardImage(new DebtorCardInput(
            "پمپ", "هارون", "", "لیتر", "1405/07/21",
            new List<string[]> { new[] { "پطرول", "1,000", "400", "0", "600" } },
            "600", "🟢 روبه‌راه", false, null)).Png();
        Assert.True(IsPng(png));
        Assert.True(png.Length > 2_000);
    }

    [Fact]
    public void TheQrMarkerIsExactlyTheUiOne()
    {
        Assert.Equal(AcctLink.Build(7), BotReports.Marker(7, null));
        Assert.Equal(AcctLink.Build(7, "s123"), BotReports.Marker(7, "s123"));
    }
}
