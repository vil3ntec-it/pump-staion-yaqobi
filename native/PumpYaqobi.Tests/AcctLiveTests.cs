using System.Text.Json;
using System.Text.RegularExpressions;
using PumpYaqobi.App.Services;
using PumpYaqobi.Domain.Entities;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ کیو‌آرِ زنده ═══════════════════════════════════════════════════════════
///
/// خواستهٔ صاحب ریپو: «من دارم زنده تغییرات میارم توی حسابِ طرف، طرف هم داره
/// با کیو‌آر حسابشو چک می‌کنه و می‌خوام درجا برای اون هم بره… که هر دقیقه
/// بتونه چک کنه و بفهمه.»
///
/// چیزهایی که این‌جا قفل می‌شوند:
///   ۱) کیو‌آرِ زنده همان کیو‌آرِ قبلی است به‌علاوهٔ چهار پارامترِ کوتاه؛ دادهٔ
///      داخلِ کد همچنان باز می‌شود و هیچ رمزِ پمپی در آن نیست.
///   ۲) رمزِ حساب یک بار ساخته می‌شود و می‌ماند — کاغذِ چاپ‌شده باید تا ابد
///      کار کند.
///   ۳) فقط حسابی که واقعاً عوض شده فرستاده می‌شود، و فقط به مقصدی که نرفته.
/// </summary>
public class AcctLiveTests
{
    private static AppHost Host()
    {
        var host = new AppHost(
            Path.Combine(Path.GetTempPath(), "pump-acctlive-" + Guid.NewGuid().ToString("N"), "pump.db"));
        if (host.Auth.NeedsFirstRun()) host.Auth.CreateFirstAdmin("1234");
        host.Auth.SignIn("admin", "1234");
        return host;
    }

    private static AcctSnapshot Snap(string name, int rows = 3)
    {
        var s = new AcctSnapshot
        {
            Kind = "قرض‌دار — واحد تیل", Name = name, Unit = "لیتر", Date = "1405/06/25",
            Summary = { new[] { "الباقی", "640" } }, Head = { "تاریخ", "نام", "مقدار" },
        };
        for (var i = 0; i < rows; i++) s.Rows.Add(new[] { "1405/06/0" + (i + 1), "ردیف " + i, "50" });
        return s;
    }

    // ── ۱) نشانی ────────────────────────────────────────────────────────

    [Fact]
    public void TheLiveLinkKeepsTheDataInsideAndAddsOnlyFourShortParams()
    {
        var live = AcctLive.Fragment("pump1", "d7", "abcdef0123456789abcd", 1700000000000);
        Assert.Equal("s=pump1&a=d7&k=abcdef0123456789abcd&t=1700000000000", live);

        var url = AcctView.Url("https://example.invalid/view/", Snap("هارون"), AcctLink.Build(7), live);

        Assert.Contains("#s=pump1&a=d7&k=abcdef0123456789abcd&t=1700000000000&d=", url);
        Assert.DoesNotContain("token", url);
        Assert.DoesNotContain("server", url);

        // دادهٔ داخلِ کد همان است — صفحه بی‌اینترنت هم همین را نشان می‌دهد
        var d = Regex.Match(url, @"(?:^|&)d=([^&]+)").Groups[1].Value;
        var back = AcctView.Decode(d);
        Assert.NotNull(back);
        Assert.Equal("هارون", back!.Name);
        Assert.Equal(3, back.Rows.Count);

        // و نشانهٔ حساب برای اسکنرِ خودِ برنامه سرِ جایش است
        Assert.Equal(7, AcctLink.Parse(Uri.UnescapeDataString(url))!.Value.PersonId);
    }

    [Fact]
    public void WithoutAKeyTheLinkIsExactlyTheOldStaticOne()
    {
        var a = AcctView.Url("https://example.invalid/view/", Snap("هارون"), AcctLink.Build(7));
        var b = AcctView.Url("https://example.invalid/view/", Snap("هارون"), AcctLink.Build(7), "");
        Assert.Equal(a, b);
        Assert.Contains("#d=", a);
        Assert.DoesNotContain("&d=", a);
    }

    /// <summary>کدِ پمپ همان شکلی می‌رود که ابر ثبتش کرده — وگرنه صفحه ۴۰۴ می‌گرفت.</summary>
    [Fact]
    public void TheStationCodeIsWrittenTheWayTheCloudKeepsIt()
    {
        Assert.StartsWith("s=pump-yaqobi-2&", AcctLive.Fragment(" Pump_Yaqobi 2 ", "d1", "k", 1));
        Assert.Equal("pump1", AcctLive.CloudCode("pump1"));
    }

    [Fact]
    public void AnIncompleteFragmentIsNoFragmentAtAll()
    {
        Assert.Equal("", AcctLive.Fragment("", "d7", "k", 1));
        Assert.Equal("", AcctLive.Fragment("pump1", "", "k", 1));
        Assert.Equal("", AcctLive.Fragment("pump1", "d7", "", 1));
    }

    [Fact]
    public void TheLiveLinkStillFitsAPhoneCamera()
    {
        var live = AcctLive.Fragment("pump1", "d12345", AcctLive.NewKey(), AcctLive.NowMs());
        var url = AcctView.Url(null, Snap("هارون", 400), AcctLink.Build(12345), live);
        Assert.True(url.Length <= AcctView.MaxUrl, url.Length.ToString());
    }

    // ── ۲) رمز و پاکت ───────────────────────────────────────────────────

    [Fact]
    public void KeysAreTwentyHexCharsAndNeverRepeat()
    {
        var keys = Enumerable.Range(0, 50).Select(_ => AcctLive.NewKey()).ToList();
        Assert.All(keys, k => Assert.Matches("^[0-9a-f]{20}$", k));
        Assert.Equal(50, keys.Distinct().Count());
    }

    [Fact]
    public void TheEnvelopeCarriesTheKeyTheTimeAndTheSameShortJsonThePageReads()
    {
        var env = AcctLive.Envelope("abcdef0123456789abcd", Snap("هارون"), 1234);
        var j = JsonDocument.Parse(JsonSerializer.Serialize(env)).RootElement;
        Assert.Equal(1, j.GetProperty("v").GetInt32());
        Assert.Equal("abcdef0123456789abcd", j.GetProperty("k").GetString());
        Assert.Equal(1234, j.GetProperty("at").GetInt64());
        // همان نام‌های یک‌حرفیِ کیو‌آر — صفحه ‎d.n‎ و ‎d.s‎ را می‌خواند
        Assert.Equal("هارون", j.GetProperty("d").GetProperty("n").GetString());
        Assert.Equal(1, j.GetProperty("d").GetProperty("s").GetArrayLength());
    }

    [Fact]
    public void TheFingerprintIgnoresTheDayButNotTheNumbers()
    {
        var a = Snap("هارون");
        var b = Snap("هارون"); b.Date = "1405/06/26";
        Assert.Equal(AcctLive.HashOf(a), AcctLive.HashOf(b));
        Assert.Equal("1405/06/25", a.Date);           // تاریخ سرِ جایش برمی‌گردد

        b.Summary[0][1] = "641";
        Assert.NotEqual(AcctLive.HashOf(a), AcctLive.HashOf(b));
    }

    // ── ۳) انتشار — فقط تغییر، فقط مقصدِ نرفته ─────────────────────────

    [Fact]
    public async Task OnlyChangedAccountsGoAndOnlyToTheDestinationThatMissedThem()
    {
        var pub = new AcctLivePublisher();
        var home = new List<string>();
        var cloud = new List<string>();
        var cloudUp = true;

        Task<bool> Home(string p, object v, CancellationToken _) { home.Add(p); return Task.FromResult(true); }
        Task<bool> Cloud(string p, object v, CancellationToken _) { if (cloudUp) cloud.Add(p); return Task.FromResult(cloudUp); }

        var items = new List<AcctLive.Item>
        {
            new("d1", "k1", Snap("الف")),
            new("c2", "k2", Snap("ب")),
        };

        Assert.Equal(2, await pub.PublishAsync(items, Home, Cloud));
        Assert.Equal(new[] { "acct/d1", "acct/c2" }, home);
        Assert.Equal(new[] { "acct-d1", "acct-c2" }, cloud);
        Assert.Equal(0, pub.Pending);

        // هیچ چیزی عوض نشده ⇒ هیچ چیزی نمی‌رود
        Assert.Equal(0, await pub.PublishAsync(items, Home, Cloud));
        Assert.Equal(2, home.Count);

        // فقط «ب» عوض شد ⇒ فقط «ب» می‌رود
        items[1] = new("c2", "k2", Snap("ب", 5));
        Assert.Equal(1, await pub.PublishAsync(items, Home, Cloud));
        Assert.Equal("acct/c2", home[^1]);
        Assert.Equal("acct-c2", cloud[^1]);

        // ابر قطع: خانگی می‌رود، ابر طلبکار می‌ماند
        cloudUp = false;
        items[0] = new("d1", "k1", Snap("الف", 7));
        Assert.Equal(1, await pub.PublishAsync(items, Home, Cloud));
        Assert.Equal(1, pub.Pending);
        Assert.Equal("acct/d1", home[^1]);
        Assert.Equal(2, cloud.Count(x => x == "acct-c2") + cloud.Count(x => x == "acct-d1") - 1);

        // ابر برگشت: بی هیچ تغییری، فقط ابر و فقط «الف»
        cloudUp = true;
        var homeBefore = home.Count;
        Assert.Equal(1, await pub.PublishAsync(items, Home, Cloud));
        Assert.Equal(homeBefore, home.Count);
        Assert.Equal("acct-d1", cloud[^1]);
        Assert.Equal(0, pub.Pending);

        // حسابی که کیو‌آرش رفت، فراموش می‌شود — و اگر برگشت دوباره می‌رود
        Assert.Equal(0, await pub.PublishAsync(items.Take(1).ToList(), Home, Cloud));
        Assert.Equal(1, await pub.PublishAsync(items, Home, Cloud));
        Assert.Equal("acct/c2", home[^1]);
    }

    [Fact]
    public async Task NoHomeDoorMeansCloudOnlyAndTheOtherWayRound()
    {
        var pub = new AcctLivePublisher();
        var cloud = 0;
        var items = new List<AcctLive.Item> { new("d1", "k1", Snap("الف")) };
        Assert.Equal(1, await pub.PublishAsync(items, null, (_, _, _) => { cloud++; return Task.FromResult(true); }));
        Assert.Equal(1, cloud);
        Assert.Equal(0, pub.Pending);

        var home = 0;
        var pub2 = new AcctLivePublisher();
        Assert.Equal(1, await pub2.PublishAsync(items, (_, _, _) => { home++; return Task.FromResult(true); }, null));
        Assert.Equal(1, home);
    }

    // ── ۴) با دیتابیسِ واقعی ────────────────────────────────────────────

    [Fact]
    public async Task TheKeyIsMadeOnceStoredAndTheAccountThenShowsUpForPublishing()
    {
        var host = Host();

        // تا کیو‌آری ساخته نشده، هیچ حسابی منتشر نمی‌شود
        Assert.Empty(await AcctLive.CollectAsync(host));

        var person = await host.Debtors.AddDebtorAsync("محمد هارون", null, false);
        var full = await host.Debtors.LoadFullAsync(person.Id);
        var main = full!.MainAccount;

        var live1 = await AcctLive.EnsureAsync(host, main);
        Assert.StartsWith("s=pump1&a=d" + main.Id + "&k=", live1);
        var key = main.QrKey!;
        Assert.Matches("^[0-9a-f]{20}$", key);

        // بارِ دوم — و حتی از روی حسابِ دوباره‌خوانده‌شده — همان رمز
        var again = (await host.Debtors.LoadFullAsync(person.Id))!.MainAccount;
        Assert.Equal(key, again.QrKey);
        Assert.Contains("&k=" + key + "&", await AcctLive.EnsureAsync(host, again));

        var items = await AcctLive.CollectAsync(host);
        var it = Assert.Single(items);
        Assert.Equal("d" + main.Id, it.Id);
        Assert.Equal(key, it.Key);
        Assert.Equal("محمد هارون", it.Snap.Name);

        // شرکت هم همان راه
        var co = await host.Companies.AddAsync("شرکتِ الف");
        var coFull = await host.Companies.LoadAsync(co.Id);
        var live2 = await AcctLive.EnsureAsync(host, coFull!);
        Assert.StartsWith("s=pump1&a=c" + co.Id + "&k=", live2);
        Assert.Equal(coFull!.QrKey, (await host.Companies.LoadAsync(co.Id))!.QrKey);

        items = await AcctLive.CollectAsync(host);
        Assert.Equal(2, items.Count);
        Assert.Contains(items, x => x.Id == "c" + co.Id && x.Snap.Name == "شرکتِ الف");
    }
}
