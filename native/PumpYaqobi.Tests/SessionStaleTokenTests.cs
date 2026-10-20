using System.Net;
using PumpYaqobi.App.Services;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ «لاگین می‌کنم درست است؛ می‌روم بیرون و برمی‌گردم، می‌گوید دوباره با همان ایمیل وارد شو» (۱۴۰۵/۰۷/۲۸) ══
///
/// سرورِ حساب توکنِ تازه‌سازی را <b>می‌چرخاند</b> و توکنِ چرخیده‌ای که پس از سی ثانیه دوباره
/// بیاید «دزدی» است: کلِ نشست بسته می‌شود (<c>shop/server/src/lib/tokens.js</c>، ‎refresh_reused‎).
///
/// ریشه در برنامه: نمونه‌ای که توکنِ تازه را از دیسک <b>برمی‌داشت</b> (‎AdoptDiskSession‎) پایهٔ
/// ادغامش را جلو نمی‌برد؛ پس ذخیرهٔ بعدیِ همان نمونه، توکنِ برداشته را «تغییرِ خودم» می‌شمرد و
/// روی توکنِ تازه‌ترِ دیسک می‌نوشت. بازِ بعدیِ برنامه همان توکنِ چرخیده را می‌فرستاد ⇒ نشست بسته ⇒
/// صفحهٔ ورود با همان ایمیل.
/// </summary>
[Collection(AppHostCollection.Name)]
public class SessionStaleTokenTests : IDisposable
{
    private readonly string _dir;
    private readonly string? _was;

    public SessionStaleTokenTests()
    {
        _was = AppSettings.DirOverride;
        _dir = Path.Combine(Path.GetTempPath(), "pump-stale-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_dir);
        AppSettings.DirOverride = _dir;
    }

    public void Dispose()
    {
        CloudLink.TestTransport = null;
        CloudLink.ResetReach();
        AppSettings.DirOverride = _was;
        try { Directory.Delete(_dir, true); } catch { }
        GC.SuppressFinalize(this);
    }

    private static long Now => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    [Fact]
    public async Task AdoptedToken_NeverOverwritesNewerDiskToken()
    {
        //  سرورِ چرخاننده: هر توکنِ تازه‌سازی فقط یک بار پذیرفته است
        var live = "ref-1";
        var n = 1;
        CloudLink.TestTransport = (req, _) =>
        {
            var body = req.Content?.ReadAsStringAsync().Result ?? "";
            if (req.RequestUri!.AbsolutePath == "/api/auth/refresh" && body.Contains("\"" + live + "\""))
            {
                n++;
                live = "ref-" + n;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent($$"""{"accessToken":"acc-{{n}}","accessExpiresAt":{{Now + 3_600_000}},"refreshToken":"{{live}}"}""",
                                                System.Text.Encoding.UTF8, "application/json"),
                });
            }
            if (req.RequestUri!.AbsolutePath == "/api/pump/me")
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""{"home":{"url":"http://192.168.1.50:4701","readKey":"rk","station":"s"}}""",
                                                System.Text.Encoding.UTF8, "application/json"),
                });
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = new StringContent("""{"error":{"code":"refresh_reused","message":"این نشست جای دیگری هم به کار رفته و بسته شد؛ دوباره وارد شوید"}}""",
                                            System.Text.Encoding.UTF8, "application/json"),
            });
        };

        //  ورود: ref-1 روی دیسک، توکنِ دسترسی تمام‌شده
        var seed = AppSettings.Load();
        seed.CloudAccountToken = "acc-1"; seed.CloudRefreshToken = "ref-1"; seed.CloudAccessExpiresAt = Now - 1;
        seed.CloudEmail = "haroon@gmail.com";
        seed.Save();

        //  نمونهٔ ماندگار (پروفایل) و نمونهٔ کوتاه‌عمر (همگام‌سازی) — هر دو همان دیسک
        var fa = AppSettings.Load();
        var a = new CloudLink(fa, () => { fa.Save(); return Task.CompletedTask; });
        var fs = AppSettings.Load();
        var s = new CloudLink(fs, () => { fs.Save(); return Task.CompletedTask; });

        //  ۱) همگام‌سازی تازه می‌کند: ref-1 ⇒ ref-2 روی دیسک
        Assert.True((await s.HomeFromAccountAsync()).Item1);
        Assert.Equal("ref-2", AppSettings.Load().CloudRefreshToken);

        //  ۲) پروفایل ref-2 را از دیسک برمی‌دارد (نه این‌که خودش بچرخاند)
        Assert.True((await a.HomeFromAccountAsync()).Item1);
        Assert.Equal("ref-2", fa.CloudRefreshToken);

        //  ۳) یک ساعت بعد، نمونهٔ کوتاه‌عمرِ دیگری می‌چرخاند: ref-2 ⇒ ref-3
        var f3 = AppSettings.Load();
        f3.CloudAccessExpiresAt = Now - 1;
        f3.Save();
        f3 = AppSettings.Load();
        var t = new CloudLink(f3, () => { f3.Save(); return Task.CompletedTask; });
        Assert.True((await t.HomeFromAccountAsync()).Item1);
        Assert.Equal("ref-3", AppSettings.Load().CloudRefreshToken);

        //  ۴) ⛔ پروفایل به هر دلیلی ذخیره می‌کند (بستنِ برنامه) — نباید ref-2ِ چرخیده را برگرداند
        fa.ThemeId = "dark";
        fa.Save();
        var disk = AppSettings.Load();
        Assert.Equal("ref-3", disk.CloudRefreshToken);

        //  ۵) بازِ دوباره: هنوز وارد است و تازه‌سازی کار می‌کند
        disk.CloudAccessExpiresAt = Now - 1;
        disk.Save();
        var fr = AppSettings.Load();
        var r = new CloudLink(fr, () => { fr.Save(); return Task.CompletedTask; });
        Assert.True((await r.HomeFromAccountAsync()).Item1);
        Assert.True(r.SignedIn);
    }
}
