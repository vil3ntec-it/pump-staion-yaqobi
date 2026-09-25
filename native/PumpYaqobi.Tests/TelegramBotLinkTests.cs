using System.Net;
using System.Net.Http;
using PumpYaqobi.App.Services;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ باتِ تلگرام — این برنامه فقط نشانی‌اش را نشان می‌دهد ══════════════════
///
/// خواستهٔ صاحب ریپو (۱۴۰۵/۰۷/۱۳): «بات تلگرام رو روی سرور بساز و برای پمپ
/// باشد و دیگه جای نشتی نکنه… دکمه برای اندروید و آیفون… و گروه تلگرام.»
///
/// <para>
/// خودِ بات روی <b>سرورِ حساب</b> است (ریپوی <c>shop</c>، <c>lib/telegram.js</c>
/// و آزمونِ ۲۵بندیِ <c>pump-telegram.test.js</c>). سهمِ این برنامه دو چیز
/// است و این کلاس هر دو را قفل می‌کند:
/// </para>
/// <list type="number">
///   <item>نشانیِ بات را از درِ عمومیِ سرور می‌پرسد و <b>فقط</b>
///     <c>https://t.me/…</c> را می‌پذیرد.</item>
///   <item>⛔ <b>هیچ رمزِ باتی داخلِ برنامه نیست</b> — اگر بود، هر کسی از
///     فایلِ برنامه بیرونش می‌کشید و از طرفِ پمپ پیام می‌داد.</item>
/// </list>
///
/// ⚠️ <see cref="CloudLink.TestTransport"/> ایستا است، پس سریال می‌دود.
/// </summary>
[Collection(AppHostCollection.Name)]
public class TelegramBotLinkTests : IDisposable
{
    public TelegramBotLinkTests() => CloudLink.ResetReach();

    public void Dispose()
    {
        CloudLink.TestTransport = null;
        CloudLink.ResetReach();
        GC.SuppressFinalize(this);
    }

    private static void Serve(HttpStatusCode code, string body, List<string>? paths = null) =>
        CloudLink.TestTransport = (req, _) =>
        {
            paths?.Add(req.RequestUri?.AbsolutePath ?? "");
            return Task.FromResult(new HttpResponseMessage(code)
            {
                Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json"),
            });
        };

    [Fact]
    public async Task BatRoshan_NeshaniRa_Midahad_Az_DareOmoomi()
    {
        var paths = new List<string>();
        Serve(HttpStatusCode.OK,
            """{"enabled":true,"username":"PumpBot","url":"https://t.me/PumpBot"}""", paths);

        Assert.Equal("https://t.me/PumpBot", await CloudLink.TelegramBotUrlAsync());
        Assert.Equal(new[] { "/api/pump/public/telegram" }, paths);
    }

    [Theory]
    [InlineData("""{"enabled":false,"username":"","url":""}""")]
    [InlineData("""{"enabled":true,"username":"X","url":"https://evil.example/X"}""")]
    [InlineData("""{"enabled":"true","url":"https://t.me/X"}""")]
    [InlineData("""<html>not found</html>""")]
    public async Task HarChizeNajoor_Nist_Ast(string body)
    {
        Serve(HttpStatusCode.OK, body);
        Assert.Equal("", await CloudLink.TelegramBotUrlAsync());
    }

    [Fact]
    public async Task SarvarNarasid_Nist_Ast_Va_Estesna_Nemidahad()
    {
        CloudLink.TestTransport = (_, _) => throw new HttpRequestException("no route to host");
        Assert.Equal("", await CloudLink.TelegramBotUrlAsync());
        Serve(HttpStatusCode.NotFound, """{"error":{"code":"not_found","message":"x"}}""");
        Assert.Equal("", await CloudLink.TelegramBotUrlAsync());
    }

    private static readonly string App =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..",
                                      "PumpYaqobi.App"));

    /// <summary>
    /// ⛔ هیچ رمزِ بات و هیچ فراخوانِ مستقیمِ API تلگرام در کلِ برنامه نیست.
    /// رمزِ بات شکلِ <c>123456:ABC…</c> دارد و API با <c>api.telegram.org/bot</c>
    /// شروع می‌شود؛ هر دو فقط جایشان روی سرور است.
    /// </summary>
    [Fact]
    public void DarBarname_HichRamzeBat_Va_HichApiTelegram_Nist()
    {
        foreach (var file in Directory.EnumerateFiles(App, "*.*", SearchOption.AllDirectories)
                     .Where(f => f.EndsWith(".cs") || f.EndsWith(".axaml") || f.EndsWith(".json"))
                     .Where(f => !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar)
                              && !f.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar)))
        {
            var s = File.ReadAllText(file);
            Assert.DoesNotContain("api.telegram.org", s);
            Assert.False(System.Text.RegularExpressions.Regex.IsMatch(s, @"\b\d{6,}:[A-Za-z0-9_-]{30,}\b"),
                Path.GetFileName(file) + " چیزی شبیهِ رمزِ باتِ تلگرام دارد");
        }
    }

    [Fact]
    public void SafheyeApp_LinkeBat_Ra_Neshan_Midahad()
    {
        var view = File.ReadAllText(Path.Combine(App, "Views", "Sections", "AppsSectionView.axaml"));
        Assert.Contains("{Binding BotLink}", view);
        Assert.Contains("CopyBotCommand", view);
        Assert.Contains("{Binding BotHint}", view);
        var vm = File.ReadAllText(Path.Combine(App, "ViewModels", "Sections", "AppsSectionViewModel.cs"));
        Assert.Contains("CloudLink.TelegramBotUrlAsync()", vm);
    }
}
