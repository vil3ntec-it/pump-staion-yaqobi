using PumpYaqobi.App.Services;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ 🤖 پیگیرِ اشتراک ═══════════════════════════════════════════════════════
///
/// گزارشِ صاحب ریپو (۱۴۰۵/۰۷/۱۴): «به یک حساب اشتراک دادم، توی برنامه گفت
/// تمدید شد یک سال VIP، ولی برنامه باز هم همون مدل قفل بود و اشتراکِ مونده رو
/// نمی‌گفت.» با پشتهٔ واقعی بازسازی شد (‎oldacct … subwatch real‎): یک سکسکهٔ
/// سرور سرِ گرفتنِ مجوز ⇒ روی کدِ پیشین، ۲۲۵ ثانیه بعد هنوز «آزمایشی · ۳۰
/// روز»؛ با پیگیر ۱۲۲ ثانیه و VIP · ۳۶۵ روز.
///
/// رفتارِ کامل با سرورِ واقعی سنجیده می‌شود؛ این‌جا جوابِ خالص و قاعده‌ها.
/// </summary>
[Collection(AppHostCollection.Name)]
public class SubscriptionWatchTests : IDisposable
{
    private static readonly long Now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    private const long Day = 86_400_000;

    public SubscriptionWatchTests() => SubscriptionWatch.Reset();
    public void Dispose() { SubscriptionWatch.Reset(); GC.SuppressFinalize(this); }

    private static PumpSubscription Server(bool active, string source, string plan, long endsAt) =>
        new(active, source, plan, 0, endsAt, Array.Empty<string>());

    private static LicenseCheck Local(bool valid, string plan, long subEnds, bool expired = false) =>
        new(valid, valid ? "" : "مجوز منقضی شده", Array.Empty<string>(), Array.Empty<string>(),
            subEnds, Now + 10 * Day, plan, HasFeatureList: true, SignatureOk: true, Expired: expired);

    // ── جوابِ خالص ────────────────────────────────────────────────────

    [Fact]
    public void AzmayeshiInja_VipRooyeServer_YekiNistand()
    {
        //  ⛔ همان گزارش: سرور VIPِ یک‌ساله، این کامپیوتر هنوز آزمایشی —
        //  هر دو «باز»اند، پس سنجشِ کهنهٔ «باز/بسته» هرگز نمی‌دیدش.
        var v = SubscriptionWatch.Compare(
            Server(true, "subscription", "vip", Now + 365 * Day),
            Local(true, "دوره‌ی آزمایشی", Now + 30 * Day), Now);
        Assert.False(v.Agree);
        Assert.Contains("آزمایشی", v.Why);
        Assert.StartsWith("VIP", v.Server);
        Assert.StartsWith("آزمایشی", v.Local);
    }

    [Fact]
    public void Tamdid_Narasideh_YekiNistand()
    {
        var v = SubscriptionWatch.Compare(
            Server(true, "subscription", "vip", Now + 730 * Day),
            Local(true, "vip", Now + 365 * Day), Now);
        Assert.False(v.Agree);
        Assert.Contains("روی سرور هست ولی هنوز به این کامپیوتر نرسیده", v.Why);
    }

    [Fact]
    public void HamanEshterak_YekiAst_VaYekRoozFarghRaNemishomarad()
    {
        Assert.True(SubscriptionWatch.Compare(
            Server(true, "subscription", "vip", Now + 365 * Day),
            Local(true, "vip", Now + 365 * Day - 3_600_000), Now).Agree);
        //  دائمی: هر دو خیلی دور
        Assert.True(SubscriptionWatch.Compare(
            Server(true, "subscription", "perm", Now + 18_000 * Day),
            Local(true, "perm", Now + 18_000 * Day), Now).Agree);
        //  آزمایشی هر دو طرف
        Assert.True(SubscriptionWatch.Compare(
            Server(true, "trial", "دورهٔ آزمایشی", Now + 30 * Day),
            Local(true, "دوره‌ی آزمایشی", Now + 30 * Day), Now).Agree);
    }

    [Fact]
    public void Bardashtan_Va_Dadan_HarDoDideMishavand()
    {
        var removed = SubscriptionWatch.Compare(
            Server(false, "none", "", 0), Local(true, "vip", Now + 365 * Day), Now);
        Assert.False(removed.Agree);
        Assert.Contains("برداشته", removed.Why);

        var given = SubscriptionWatch.Compare(
            Server(true, "subscription", "vip", Now + 365 * Day), Local(false, "", 0, expired: true), Now);
        Assert.False(given.Agree);
        Assert.Contains("نرسیده", given.Why);

        Assert.True(SubscriptionWatch.Compare(
            Server(false, "none", "", 0), Local(false, "", 0), Now).Agree);
    }

    [Fact]
    public void TarikheNayamade_HadsZadeNemishavad()
    {
        //  سرور تاریخ نداد ⇒ فقط نوع سنجیده می‌شود، نه پایان
        Assert.True(SubscriptionWatch.Compare(
            Server(true, "subscription", "vip", 0), Local(true, "vip", Now + 365 * Day), Now).Agree);
    }

    [Fact]
    public void ServerMiguyadChera_AzmayeshiNist()
    {
        //  گزارشِ صاحب ریپو: «حسابِ تازه ۳۰ روز آزمایشی نمی‌گیرد.» با سرورِ واقعی:
        //  روزِ آزمایشی در پنل صفر ⇒ حسابِ تازه هیچ دوره‌ای ندارد و برنامه فقط
        //  «مجوزی ذخیره نشده است» می‌گفت. حالا دلیلِ خودِ سرور گفته می‌شود.
        static System.Text.Json.JsonElement E(string j) => System.Text.Json.JsonDocument.Parse(j).RootElement;

        var off = CloudLink.TrialNoteOf("free", E("""{"trial":{"enabled":false,"active":false}}"""));
        Assert.Contains("خاموش", off);
        Assert.Contains("دورهٔ آزمایشیِ حسابِ تازه", off);

        Assert.Contains("اشتراکش تمام شده", CloudLink.TrialNoteOf("free",
            E("""{"trial":{"enabled":true,"active":false,"consumed":true}}""")));
        Assert.Contains("تمام شده", CloudLink.TrialNoteOf("free",
            E("""{"trial":{"enabled":true,"active":false,"endsAt":1700000000000}}""")));

        //  آزمایشی یا اشتراکِ فعال ⇒ هیچ یادداشتی
        Assert.Equal("", CloudLink.TrialNoteOf("trial", E("""{"trial":{"enabled":true,"active":true}}""")));
        Assert.Equal("", CloudLink.TrialNoteOf("subscription", E("""{"trial":{"enabled":false}}""")));
        Assert.Equal("", CloudLink.TrialNoteOf("free", E("""{}""")));

        //  و در خطِ پیگیر هم دیده می‌شود
        var s = PumpSubscription.None with { TrialNote = off };
        Assert.Contains("خاموش", SubscriptionWatch.Describe(s));
    }

    // ── دور و ترمز ────────────────────────────────────────────────────

    [Fact]
    public void Nashod_BaTarmzeFazayandeh_DobarehMizanad()
    {
        var bad = new SubVerdict(false, "VIP", "آزمایشی", "اشتراکِ تازه نرسیده");
        Assert.True(SubscriptionWatch.FixDue(false));
        SubscriptionWatch.Report(bad, triedFix: true, "خطای داخلی سرور");
        Assert.Equal(1, SubscriptionWatch.Fails);
        Assert.False(SubscriptionWatch.FixDue(false));       // یک دقیقه صبر
        Assert.True(SubscriptionWatch.FixDue(true));         // کلیکِ کاربر همین حالا
        Assert.Contains("خطای داخلی سرور", SubscriptionWatch.Line());
        Assert.Contains("خودش دوباره", SubscriptionWatch.Line());

        SubscriptionWatch.Report(new SubVerdict(true, "VIP", "VIP", ""), triedFix: true);
        Assert.Equal(0, SubscriptionWatch.Fails);
        Assert.True(SubscriptionWatch.FixDue(false));
        Assert.Contains("یکی‌اند", SubscriptionWatch.Line());
        Assert.Contains("خودش رساند", SubscriptionWatch.Line());
    }

    [Fact]
    public void Afline_MarzeMojavez_KhodashMirasad_BiKhandaneBihoodeh()
    {
        //  ⛔ آفلاین هم: رسیدنِ پایانِ مجوز باید همان لحظه قفل‌ها را از نو
        //  بخواند — ولی تا نرسیده، هیچ خواندنی (نه فایل، نه امضا).
        var t = 1_000L;
        var reads = 0;
        var crossed = 0;
        void OnCross() => crossed++;
        SubscriptionWatch.BoundaryCrossed += OnCross;
        try
        {
            (long, long, long) Read() { reads++; return (5_000, 9_000, 0); }
            SubscriptionWatch.LocalTick(() => t, Read);
            Assert.Equal(1, reads);
            Assert.Equal(0, crossed);                        // بارِ اول فقط مرز را می‌خواند

            t = 4_999;
            for (var i = 0; i < 50; i++) SubscriptionWatch.LocalTick(() => t, Read);
            Assert.Equal(1, reads);                          // تا مرز: صفر خواندن

            t = 5_000;
            SubscriptionWatch.LocalTick(() => t, Read);
            Assert.Equal(1, crossed);                        // مرزِ اول رد شد
            Assert.Equal(2, reads);

            t = 6_000;
            SubscriptionWatch.LocalTick(() => t, Read);
            Assert.Equal(1, crossed);                        // تا مرزِ بعدی، نه

            t = 9_001;
            SubscriptionWatch.LocalTick(() => t, Read);
            Assert.Equal(2, crossed);
        }
        finally { SubscriptionWatch.BoundaryCrossed -= OnCross; }
    }

    // ── قاعده‌ها روی خودِ سورس ───────────────────────────────────────

    private static string Src(string rel)
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null && !Directory.Exists(Path.Combine(d.FullName, "PumpYaqobi.App")))
            d = d.Parent;
        Assert.NotNull(d);
        return File.ReadAllText(Path.Combine(d!.FullName, rel));
    }

    [Fact]
    public void GereftaneMojavezNashod_ShodGoftehNemishavad()
    {
        //  ⛔ ریشهٔ همان گزارش: `RefreshAsync` شکستِ گرفتنِ مجوز را «شد» می‌گفت
        var s = Src("PumpYaqobi.Shell/Services/CloudLink.cs");
        var i = s.IndexOf("public async Task<CloudResult> RefreshAsync", StringComparison.Ordinal);
        var body = s[i..s.IndexOf("private CloudResult? AdoptLicense", i, StringComparison.Ordinal)];
        Assert.Contains("if (!licOk) return CloudResult.No(licWhy, licCode);", body);
    }

    [Fact]
    public void Peygir_DarHalgheh_Va_BaPayameServer_Va_BargashtaneInternet()
    {
        var pub = Src("PumpYaqobi.Shell/Services/StationPublisher.cs");
        var keep = pub.IndexOf("await cloud.KeepLicenseFreshAsync(ct);", StringComparison.Ordinal);
        var watch = pub.IndexOf("await cloud.WatchSubscriptionAsync(forceBind, ct);", StringComparison.Ordinal);
        Assert.True(keep > 0 && watch > keep, "پیگیر پس از گرفتنِ حالِ سرور");
        Assert.Contains("SubscriptionWatch.LocalTick(", pub);
        Assert.Contains("NetworkChange.NetworkAvailabilityChanged", pub);

        var main = Src("PumpYaqobi.App/ViewModels/MainViewModel.cs");
        var notice = main[main.IndexOf("private void ShowNotice(CloudNotice n)", StringComparison.Ordinal)..];
        Assert.Contains("StationPublisher.CloudSoon();", notice[..400]);
        Assert.Contains("SubscriptionWatch.BoundaryCrossed +=", main);
    }

    [Fact]
    public void Peygir_HichPompiNemisazad_Va_BeDaftarDastNemizanad()
    {
        var w = Src("PumpYaqobi.Shell/Services/CloudLink.Watch.cs");
        Assert.DoesNotContain("EnsureStationAsync", w);
        Assert.DoesNotContain("Db.", w);
        //  ⚠️ سکسکهٔ گذرا اول فقط دوباره امتحان می‌شود، نه وصلِ دوباره
        Assert.Contains("SubscriptionWatch.Fails >= 2", w);
        Assert.Contains("Reach == CloudReach.Online", w);
        //  جابه‌جاییِ پمپ کارِ این‌جا نیست
        Assert.Contains("here == _acctStationSeen", w);
    }

    [Fact]
    public void DalileNarasidan_DarProfile_DidehMishavad()
    {
        //  ⛔ `SubStatus` ساخته می‌شد و در هیچ صفحه‌ای نبود
        var v = Src("PumpYaqobi.App/Views/Sections/AccountSectionView.axaml");
        Assert.Contains("{Binding SubStatus}", v);
        Assert.Contains("{Binding WatchLine}", v);
    }
}
