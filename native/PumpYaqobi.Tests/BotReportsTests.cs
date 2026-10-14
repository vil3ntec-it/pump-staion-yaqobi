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

    /// <summary>
    /// ⛔ با عکسِ صاحب ریپو (۱۴۰۵/۰۷/۲۲): «حسابِ ح هاشم هیچ چیز ندارد… واحدِ اشتباه… و حسابِ فرعیِ
    /// هارون را هم با آن می‌دهی.» حسابی که حالتش «پول» است ولی تیل برده ⇒ کارتِ لیتری با عددِ
    /// خودش؛ و فقط حسابِ اصلی — نه کارت و PDFِ حسابِ فرعی.
    /// </summary>
    [Fact]
    public async Task MoneyModeAccountWithFuelRowsShowsLitresAndOnlyTheMainAccount()
    {
        var host = Host();
        var person = await host.Debtors.AddDebtorAsync("ح هاشم", null, false);
        var full = (await host.Debtors.LoadFullAsync(person.Id))!;
        await host.Debtors.SaveRowAsync(new DebtRow
        {
            FuelAccountId = full.MainAccount.Id, DateShamsi = "1405/07/20", Fuel = FuelType.Petrol, Liters = 38m,
        });
        full = (await host.Debtors.LoadFullAsync(person.Id))!;
        full.MainAccount.Mode = LedgerMode.Money;
        await host.Debtors.UpdateAccountAsync(full.MainAccount);
        await host.Debtors.AddSubAccountAsync(person.Id, "هارون");

        var rep = await BotReports.BuildAsync(host, Req("debtor", new { id = person.Id }), CancellationToken.None);
        Assert.True(rep.Ok, rep.Error);
        Assert.Contains("38 لیتر", rep.Caption);
        Assert.DoesNotContain("افغانی", rep.Caption);
        Assert.DoesNotContain("هارون", rep.Caption);
        Assert.Single(rep.Files, f => f.Mime == "image/png");
        Assert.Single(rep.Files, f => f.Mime == "application/pdf");

        //  ۱۴۰۵/۰۷/۲۲: «حساب‌های فرعی را هم اگر خواستم بده» ⇒ دکمه‌اش (‎extra.subs‎) و کارت و PDFِ خودش
        Assert.True(rep.Extra.TryGetValue("subs", out var subs));
        var sub = (await host.Debtors.LoadFullAsync(person.Id))!.SubAccounts.Single();
        Assert.Equal(sub.Id + "\tهارون", subs);
        var subRep = await BotReports.BuildAsync(host, Req("debtor", new { id = person.Id, sub = sub.Id }), CancellationToken.None);
        Assert.True(subRep.Ok, subRep.Error);
        Assert.Contains("حسابِ فرعی: هارون", subRep.Caption);
        Assert.False(subRep.Extra.ContainsKey("subs"));
        Assert.Single(subRep.Files, f => f.Mime == "image/png");
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

    /// <summary>
    /// ۱۴۰۵/۰۷/۲۲: «برای شرکت‌ها هم عکس و PDF و لینک را بساز.» ⇒ کارتِ شرکت (عکس)، PDFِ همان
    /// ‎CompanyReport‎ و لینکِ همان کیو‌آرِ «📲»ِ شرکت؛ و گزارشِ مخزن‌ها شناسهٔ شرکت را برای دکمه می‌دهد.
    /// </summary>
    [Fact]
    public async Task ACompanyGivesACardThePdfAndTheLinkAndTanksListItForAButton()
    {
        var host = Host();
        var c = await host.Companies.AddAsync("شرکتِ نور");
        await host.Companies.SaveRowAsync(new CompanyRow
        {
            CompanyId = c.Id, Fuel = FuelType.Petrol, DateShamsi = "1405/07/01",
            Ton = 5m, Usd = 700m, Rate = 70m,
        });

        var rep = await BotReports.BuildAsync(host, Req("company", new { id = c.Id }), CancellationToken.None);
        Assert.True(rep.Ok, rep.Error);
        Assert.Single(rep.Files, f => f.Mime == "image/png" && IsPng(f.Data));
        Assert.Single(rep.Files, f => f.Mime == "application/pdf" && IsPdf(f.Data));
        Assert.True(rep.Extra.TryGetValue("qr", out var qr));
        Assert.StartsWith("http", qr);
        Assert.Contains("شرکتِ نور", rep.Caption);
        Assert.Contains("الباقی", rep.Caption);

        var byName = await BotReports.BuildAsync(host, Req("company", new { name = "نور" }), CancellationToken.None);
        Assert.True(byName.Ok, byName.Error);

        var tanks = await BotReports.BuildAsync(host, Req("tanks", new { }), CancellationToken.None);
        Assert.True(tanks.Ok, tanks.Error);
        Assert.True(tanks.Extra.TryGetValue("companies", out var list));
        Assert.Equal(c.Id + "\tشرکتِ نور", list);
    }

    [Fact]
    public void TheCompanyQrMarkerIsExactlyTheUiOne()
        => Assert.Equal(AcctLink.Build(9, null, "company"), BotReports.Marker(9, null, "company"));

    /// <summary>
    /// ۱۴۰۵/۰۷/۲۲ (با عکس): «دقیق نمی‌گوید چند لیتر تیل دیگر دارم و تا هشدار چقدر مانده.»
    /// ⇒ هر مخزن لیترِ مانده و فاصله تا حدِ هشدار را دارد — در عکس و در متن.
    /// </summary>
    [Fact]
    public async Task EachTankSaysLitresLeftAndHowFarToTheAlarm()
    {
        var host = Host();
        var saved = BotReports.TankRenderer;
        IReadOnlyList<TankShot>? got = null;
        BotReports.TankRenderer = (shots, _) => { got = shots; return Task.FromResult<byte[]?>(null); };
        try
        {
            var rep = await BotReports.BuildAsync(host, Req("tanks", new { }), CancellationToken.None);
            Assert.True(rep.Ok, rep.Error);
            Assert.NotNull(got);
            Assert.All(got!, s =>
            {
                Assert.EndsWith("لیتر مانده", s.Liters);
                Assert.True(s.Note.Contains("حدِ هشدار"), s.Note);
            });
            Assert.Contains("لیتر مانده", rep.Caption);
            Assert.Contains("حدِ هشدار", rep.Caption);
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

    // ── لینکِ کوتاهِ کیو‌آر روی vill3n.top (۱۴۰۵/۰۷/۲۱) ─────────────────────

    [Fact]
    public void TheShortLinkIsShortOnVill3nAndStillOpensTheAccountInTheAppScanner()
    {
        var live = AcctLive.Fragment("pump-yaqobi", "d7", "abcdef0123456789abcd", 1700000000000);
        var url = AcctView.ShortUrl(live, AcctLink.Build(12));
        Assert.Equal("https://vill3n.top/a/pump-yaqobi/d7/abcdef0123456789abcd?p=%23roview-debt-12-pdf", url);
        Assert.True(url.Length < 100, url.Length.ToString());
        //  اسکنرِ خودِ برنامه همان شخص را پیدا می‌کند
        Assert.Equal(12, AcctLink.Parse(Uri.UnescapeDataString(url))!.Value.PersonId);
        //  تکهٔ ناقص ⇒ هیچ (صدا‌زننده لینکِ بلند را می‌سازد)
        Assert.Equal("", AcctView.ShortUrl("s=pump1&a=d7"));
        Assert.Equal("", AcctView.ShortUrl(""));
    }

    [Fact]
    public async Task WithoutTheCloudTheQrStaysTheOldSelfContainedLink()
    {
        var host = Host();
        var person = await host.Debtors.AddDebtorAsync("نادر", null, false);
        var main = (await host.Debtors.LoadFullAsync(person.Id))!.MainAccount;
        var live = await AcctLive.EnsureAsync(host, main);
        //  رمزِ تازه ⇒ حساب‌ها در دورِ بعد منتشر شوند (یک بار)
        Assert.True(AcctLive.TakeFresh());
        var snap = AcctSnapshots.ForDebtAccount("نادر", null, main, host.Debt);
        //  این کامپیوترِ آزمون به ابر وصل نیست ⇒ همان لینکِ بلندِ دارای داده
        var link = AcctLive.LinkFor(host, snap, AcctLink.Build(person.Id), live);
        Assert.Contains("&d=", link);
    }
}
