using System.Net;
using System.Text.Json;
using PumpYaqobi.App.Services;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ راهِ رسیدن به سرورِ همین پمپ ════════════════════════════════════════════
///
/// خواستهٔ صریحِ صاحب ریپو: «اگه ادرس نداشت با ادرس براش بساز که کار کنه»، و
/// «از هر پمپ بنزین جداگانه باشه… بعدن چندین پمپ بنزین دیگه رو هم اضافه
/// میکنم».
///
/// این آزمون همان دو چیز را قفل می‌کند — نه بیشتر و نه کمتر:
///
///   ۱) نشانی‌ای که برنامه می‌سازد دقیقاً همانی است که سرور گوش می‌دهد. یک
///      نویسه فرق کند، برنامه بی‌صدا از سرور جدا می‌ماند و کاربر فقط می‌بیند
///      که گوشیِ کارمند دادهٔ دیروز را نشان می‌دهد.
///
///   ۲) هیچ دو پمپی به یک نشانی نمی‌روند.
/// </summary>
public class StationLinkTests
{
    // ── نشانیِ وب‌سوکت ──────────────────────────────────────────────────────

    /// <summary>درِ تازه: پوشهٔ اختصاصیِ همین پمپ.</summary>
    [Fact]
    public void TheStationDoorCarriesTheCodeAndTheToken()
        => Assert.Equal("wss://api.example.com/station?station=pump2&token=s3cret",
                        HomeSync.StationUrl("https://api.example.com", "pump2", "s3cret"));

    /// <summary>
    /// ⚠️ راهِ قدیمی برنداشته شده: سرورِ خانگی‌ای که هنوز به‌روز نشده فقط همین
    /// را بلد است، و روزی که کاربر برنامه را تازه کند ولی سرور را نه، بی این
    /// همان روز از کار می‌افتاد.
    /// </summary>
    [Fact]
    public void TheOldDoorIsStillThereForServersThatHaveNotBeenUpdated()
    {
        Assert.Equal("wss://api.example.com/?token=s3cret",
                     HomeSync.LegacyUrl("https://api.example.com", "s3cret"));
        Assert.Equal("wss://api.example.com", HomeSync.LegacyUrl("https://api.example.com", ""));
    }

    /// <summary>
    /// شبکهٔ خانگی ‎http‎ است و باید ‎ws‎ بماند: ‎wss‎ی اجباری روی یک آی‌پیِ
    /// بی‌گواهی اصلاً وصل نمی‌شود.
    /// </summary>
    [Theory]
    [InlineData("http://192.168.1.9:4700", "ws://192.168.1.9:4700")]
    [InlineData("https://api.example.com", "wss://api.example.com")]
    [InlineData("wss://api.example.com/", "wss://api.example.com")]
    [InlineData("ws://192.168.1.9:4700/", "ws://192.168.1.9:4700")]
    [InlineData("api.example.com", "wss://api.example.com")]
    [InlineData("", "")]
    public void EveryShapeOfAddressBecomesTheRightWebSocket(string given, string expected)
        => Assert.Equal(expected, HomeSync.WsBase(given));

    /// <summary>دو پمپ هرگز به یک پوشه نمی‌روند — همان چیزی که کلِ کار برایش است.</summary>
    [Fact]
    public void TwoStationsNeverShareAnAddress()
    {
        var a = HomeSync.StationUrl("https://api.example.com", "pump1", "k1");
        var b = HomeSync.StationUrl("https://api.example.com", "pump2", "k2");
        Assert.NotEqual(a, b);
        Assert.Contains("station=pump1", a);
        Assert.Contains("station=pump2", b);
    }

    /// <summary>نشانیِ خالی هیچ‌وقت به یک نشانیِ نصفه‌کاره تبدیل نمی‌شود.</summary>
    [Fact]
    public void NoAddressMeansNoUrl()
    {
        Assert.Equal("", HomeSync.StationUrl("", "pump1", "k"));
        Assert.Equal("", HomeSync.LegacyUrl("   ", "k"));
    }

    // ── نشانیِ ‎GET‎ی شورت‌کاتِ آیفون ────────────────────────────────────────

    [Fact]
    public void TheIphoneShortcutGetsAPlainUrl()
        => Assert.Equal("https://api.example.com/api/stations/pump1/live?token=readonly",
                        StationLink.LiveUrl("wss://api.example.com", "pump1", "readonly"));

    /// <summary>
    /// ⚠️ بی رمزِ خواندن، نشانی ساخته نمی‌شود — نه یک لینکِ نصفه که کاربر در
    /// شورت‌کات بگذارد و همیشه خطا بگیرد.
    /// </summary>
    [Fact]
    public void NoReadKeyMeansNoShortcut()
    {
        Assert.Null(StationLink.LiveUrl("https://api.example.com", "pump1", ""));
        Assert.Null(StationLink.LiveUrl("", "pump1", "k"));
        Assert.Null(StationLink.LiveUrl("https://api.example.com", "", "k"));
    }

    [Theory]
    [InlineData("wss://a.example", "https://a.example")]
    [InlineData("ws://192.168.1.9:4700", "http://192.168.1.9:4700")]
    [InlineData("https://a.example/", "https://a.example")]
    [InlineData("a.example", "https://a.example")]
    [InlineData("", "")]
    public void TheHttpAddressIsTheMirrorOfTheWebSocketOne(string given, string expected)
        => Assert.Equal(expected, StationLink.HttpBase(given));

    // ── کارتِ سروری که در شبکه پیدا می‌شود ──────────────────────────────────

    /// <summary>
    /// ⚠️ نشانی از روی <b>همان آی‌پی‌ای که جواب داد</b> ساخته می‌شود، نه از روی
    /// ‎url‎ی داخلِ کارت: سرورِ چندکارته چند نشانی دارد و فقط همانی که بسته از
    /// آن آمد از این‌جا قطعاً در دسترس است.
    /// </summary>
    [Fact]
    public void TheFoundServerIsReachedAtTheAddressItAnsweredFrom()
    {
        var card = ServerFinder.Parse(
            """
            {"reply":"PUMP-SERVER-HERE","id":"abc","name":"سرورِ خانه","port":4700,
             "addresses":["10.5.5.5","192.168.1.20"],"url":"http://10.5.5.5:4700"}
            """,
            IPAddress.Parse("192.168.1.20"));

        Assert.NotNull(card);
        Assert.Equal("http://192.168.1.20:4700", card!.Url);
        Assert.Equal("سرورِ خانه", card.Name);
        Assert.Equal("abc", card.Id);
    }

    /// <summary>هر بسته‌ای که در شبکه می‌گذرد، سرورِ ما نیست.</summary>
    [Theory]
    [InlineData("""{"reply":"SOMETHING-ELSE","port":4700}""")]
    [InlineData("""{"port":4700}""")]
    [InlineData("[]")]
    [InlineData("نه JSON")]
    [InlineData("")]
    public void AnythingElseOnTheNetworkIsIgnored(string payload)
        => Assert.Null(ServerFinder.Parse(payload, IPAddress.Loopback));

    /// <summary>پورتِ نگفته یعنی همان پورتِ همیشگیِ پنل.</summary>
    [Fact]
    public void AServerThatDoesNotSayItsPortGetsTheUsualOne()
    {
        var card = ServerFinder.Parse("""{"reply":"PUMP-SERVER-HERE"}""", IPAddress.Parse("192.168.1.7"));
        Assert.Equal("http://192.168.1.7:4700", card!.Url);
        // نامِ نگفته ⇒ خودِ آی‌پی، تا فهرست هیچ‌وقت ردیفِ بی‌نام نداشته باشد
        Assert.Equal("192.168.1.7", card.Name);
    }

    // ── صندوقِ ورودی: راهِ برگشتِ داده ───────────────────────────────────────

    /// <summary>
    /// گوشیِ کارمند چیزی بالا می‌فرستد و برنامه باید بخواندش — مرتب، و بی
    /// این‌که یک ردیفِ خراب کلِ صندوق را از کار بیندازد.
    /// </summary>
    [Fact]
    public void NotesFromThePhonesAreReadOldestFirst()
    {
        var box = JsonDocument.Parse(
            """
            {
              "b": {"text":"دومی","from":"احمد","kind":"ask","at":200},
              "a": {"text":"اولی","at":100},
              "x": {"from":"بی‌متن","at":300},
              "y": "این اصلاً شیء نیست"
            }
            """).RootElement;

        var notes = StationPublisher.Notes(box);

        Assert.Equal(2, notes.Count);
        Assert.Equal("اولی", notes[0].Text);
        Assert.Equal("note", notes[0].Kind);   // نوعِ نگفته ⇒ یادداشت
        Assert.Equal("دومی", notes[1].Text);
        Assert.Equal("احمد", notes[1].From);
        Assert.Equal("ask", notes[1].Kind);
        Assert.Equal("b", notes[1].Id);        // شناسه لازم است تا بشود پاکش کرد
    }

    /// <summary>صندوقِ خالی یا ناشناس یعنی «چیزی نیست»، نه استثنا.</summary>
    [Fact]
    public void AnEmptyOrStrangeInboxIsJustEmpty()
    {
        Assert.Empty(StationPublisher.Notes(JsonDocument.Parse("{}").RootElement));
        Assert.Empty(StationPublisher.Notes(JsonDocument.Parse("[]").RootElement));
        Assert.Empty(StationPublisher.Notes(JsonDocument.Parse("null").RootElement));
    }

    // ── کیو‌آرِ کارمند ──────────────────────────────────────────────────────

    /// <summary>
    /// ⚠️ مهم‌ترین سنجهٔ این فایل: کیو‌آر روی کاغذ چاپ می‌شود و دستِ چند نفر
    /// می‌گردد. رمزی که در آن می‌رود باید <b>رمزِ فقط‌خواندنی</b> باشد، نه رمزِ
    /// برنامه — وگرنه همان کاغذ اجازهٔ پاک کردنِ دفترِ پمپ را هم دارد.
    /// </summary>
    [Fact]
    public void TheStaffQrNeverCarriesTheWritingPassword()
    {
        const string writeToken = "WRITE-TOKEN";
        const string readKey = "READ-ONLY-KEY";

        var link = KarLink.Build("https://pump.example.com/view/", "wss://api.example.com", readKey, "pump1")!;

        Assert.Contains(readKey, link);
        Assert.DoesNotContain(writeToken, link);
    }
}
