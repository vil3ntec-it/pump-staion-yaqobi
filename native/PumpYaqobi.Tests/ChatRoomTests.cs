using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using PumpYaqobi.App.Services;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ پیام‌رسانِ تمام‌صفحه — ماندگاریِ ۱۵ + ۱۵ روزه، گروهِ کارکنان، پشتیبانی ══
///
/// خواستهٔ صاحب ریپو (۱۴۰۵/۰۷/۱۳): «همه پیام‌ها بعدِ ۱۵ روز برن سطل آشغالی و
/// ۱۵ روز هم تو آشغالی بمونه و بعدش حذفِ کامل… روی سرور فضایی رو نگیره، توی
/// برنامه باشن… گروپ چت برای کارمندان و مدیر و میرزا، نه کاربران.»
/// </summary>
public sealed class ChatRoomTests : IDisposable
{
    private const long Day = 86_400_000L;
    private const long T0 = 1_760_000_000_000L;
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "pump-chat-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        StationChat.TestTransport = null;
        try { Directory.Delete(_dir, true); } catch { }
    }

    private static ChatRow Row(string key, long at, string? media = null, string thread = "group") =>
        new(key, thread, 1, "کریم", "سلام", media is null ? "text" : "image", media, false, at);

    // ══ قاعدهٔ ماندگاری ═════════════════════════════════════════════════════

    [Fact]
    public void Decide_PanzdahRoozDarGoftogoo_PanzdahRoozDarSatl_BadHazf()
    {
        Assert.Equal(ChatFate.Keep, ChatStore.Decide(T0, 0, T0 + 14 * Day));
        Assert.Equal(ChatFate.Trash, ChatStore.Decide(T0, 0, T0 + 15 * Day));
        Assert.Equal(ChatFate.Keep, ChatStore.Decide(T0, T0 + 15 * Day, T0 + 29 * Day));
        Assert.Equal(ChatFate.Purge, ChatStore.Decide(T0, T0 + 15 * Day, T0 + 30 * Day));
        Assert.Equal(15, ChatStore.ActiveDays);
        Assert.Equal(15, ChatStore.TrashDays);
        Assert.Equal(15, ChatStore.DaysLeft(T0, 0, T0));
        Assert.Equal(1, ChatStore.DaysLeft(T0, 0, T0 + 14 * Day + 1));
        Assert.Equal(0, ChatStore.DaysLeft(T0, 0, T0 + 16 * Day));
    }

    [Fact]
    public void Sweep_PirRaBeSatl_PireSatlRaHazf_VaResaneyeBiSahebRaPak()
    {
        var s = new ChatStore(_dir);
        s.Upsert(Row("old", T0, "m-old"));
        s.Upsert(Row("new", T0 + 10 * Day));
        Assert.NotNull(s.SaveMedia("m-old", "image/png", new byte[] { 1, 2, 3 }));

        var (trashed, purged) = s.Sweep(T0 + 16 * Day);
        Assert.Equal((1, 0), (trashed, purged));
        Assert.Equal(new[] { "new" }, s.Rows("group").Select(r => r.Key));
        Assert.Equal(new[] { "old" }, s.Rows("group", trash: true).Select(r => r.Key));
        Assert.Equal(1, s.TrashCount("group"));
        Assert.NotNull(s.MediaPath("m-old"));          // در سطل هنوز هست

        // پانزده روزِ سطل تمام شد ⇒ حذفِ کامل، و فایلِ رسانه هم از دیسک
        (trashed, purged) = s.Sweep(T0 + 16 * Day + 15 * Day);
        Assert.Equal(1, purged);
        Assert.DoesNotContain(s.Rows("group", trash: true), r => r.Key == "old");
        Assert.Null(s.MediaPath("m-old"));
        // «new» خودش در همان جارو به سطل رفت، نه حذف
        Assert.Equal(1, trashed);
        Assert.Equal(new[] { "new" }, s.Rows("group", trash: true).Select(r => r.Key));
    }

    [Fact]
    public void Restore_AzHaminLahze_PanzdahRoozeDigar()
    {
        var s = new ChatStore(_dir);
        s.Upsert(Row("a", T0));
        s.Sweep(T0 + 15 * Day);
        Assert.Single(s.Rows("group", trash: true));

        var back = T0 + 20 * Day;
        Assert.True(s.Restore("a", back));
        Assert.Single(s.Rows("group"));
        // جاروی فردا دوباره به سطل نمی‌بردش
        s.Sweep(back + Day);
        Assert.Single(s.Rows("group"));
        Assert.False(s.Restore("a", back));            // دیگر در سطل نیست
    }

    [Fact]
    public void Upsert_PayameSatlRaAzSatlBiroonNemiavarad()
    {
        var s = new ChatStore(_dir);
        s.Upsert(Row("a", T0));
        s.Sweep(T0 + 15 * Day);
        // صندوقی که از اول پرسیده شد همان پیام را دوباره آورد
        s.Upsert(Row("a", T0) with { Text = "سلام دوباره" });
        Assert.Empty(s.Rows("group"));
        Assert.Equal("سلام دوباره", Assert.Single(s.Rows("group", trash: true)).Text);
    }

    [Fact]
    public void Rekey_PayameDarRah_BaShomareyeVaghei_YekiMimanad()
    {
        var s = new ChatStore(_dir);
        s.Upsert(Row("grp:cid:x1", T0) with { Pending = true, Mine = true });
        s.Rekey("grp:cid:x1", Row("grp:7", T0) with { Seq = 7, Mine = true });
        var r = Assert.Single(s.Rows("group"));
        Assert.Equal("grp:7", r.Key);
        Assert.False(r.Pending);
    }

    [Fact]
    public void Resane_FaghatShenaseyeAmn_VaFaghatDarPusheyeKhodash()
    {
        var s = new ChatStore(_dir);
        Assert.Null(s.SaveMedia("../../evil", "image/png", new byte[] { 1 }));
        Assert.Null(s.SaveMedia("ok", "image/png", Array.Empty<byte>()));
        var p = s.SaveMedia("ok-1", "video/mp4", new byte[] { 9 });
        Assert.NotNull(p);
        Assert.StartsWith(s.MediaDir, p!);
        Assert.EndsWith(".mp4", p);
        Assert.False(ChatStore.SafeId("a/b"));
        Assert.False(ChatStore.SafeId(new string('a', 81)));
    }

    [Fact]
    public void Meta_MimanadBaBazShodaneDobare()
    {
        new ChatStore(_dir).SetMeta("group.since", "42");
        Assert.Equal(42, new ChatStore(_dir).MetaLong("group.since"));
    }

    // ══ پشتیبانی — شکلِ پاسخِ سرورِ حساب ═════════════════════════════════════

    [Fact]
    public void ParseSupport_MatneBody_RaMikhanad_NaText()
    {
        using var d = JsonDocument.Parse("""
            {"id":"s1","threadId":"t","sender":"admin","senderName":"پشتیبانی","body":"سلام، بفرمایید",
             "kind":"text","readAt":null,"createdAt":1760000000000}
            """);
        var m = CloudChatMessage.ParseSupport(d.RootElement);
        Assert.NotNull(m);
        Assert.Equal("سلام، بفرمایید", m!.Text);
        Assert.Equal("a", m.From);
        Assert.Equal(1760000000000, m.At);

        using var mine = JsonDocument.Parse("""{"id":"s2","sender":"user","body":"مشکل","createdAt":1}""");
        Assert.Equal("o", CloudChatMessage.ParseSupport(mine.RootElement)!.From);

        using var empty = JsonDocument.Parse("""{"id":"s3","sender":"admin","createdAt":1}""");
        Assert.Null(CloudChatMessage.ParseSupport(empty.RootElement));
    }

    // ══ گروهِ کارکنان — پوشهٔ همین پمپ روی سرورِ خانگی ═══════════════════════

    private static AppSettings Station(string lan = "http://192.168.1.5:4700") => new()
    {
        StationCode = "p1b5feb57", ServerToken = "tok-123", ServerUrl = lan,
    };

    private static HttpResponseMessage Json(HttpStatusCode code, string body) =>
        new(code) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    [Fact]
    public void Gorooh_BePusheyeHaminPomp_BasteAst_NaMozooeSarasari()
    {
        var chat = new StationChat(() => Station());
        var doors = chat.Doors("/api/stations/p1b5feb57/chat");
        Assert.Equal("http://192.168.1.5:4700/api/stations/p1b5feb57/chat", doors[0]);
        Assert.Equal(CloudConfig.Url("/api/stations/p1b5feb57/chat"), doors[1]);
        Assert.False(new StationChat(() => new AppSettings()).Ready);

        var src = File.ReadAllText(Path.Combine(SrcRoot(), "PumpYaqobi.App", "Services", "StationChat.cs"));
        Assert.Contains("\"/api/stations/\" + code + \"/chat\"", src);
        Assert.DoesNotContain("/api/notify", src);
        Assert.DoesNotContain("topic", src);
    }

    [Fact]
    public async Task Gorooh_Mikhanad_VaMifrestad_BaRamzeHaminPomp()
    {
        var seen = new List<HttpRequestMessage>();
        StationChat.TestTransport = async (req, ct) =>
        {
            seen.Add(req);
            if (req.Method == HttpMethod.Get)
                return Json(HttpStatusCode.OK, """
                    {"ok":true,"relayDays":15,"last":2,"messages":[
                      {"seq":2,"cid":"c2","from":"کریم","role":"staff","text":"دوم","at":2},
                      {"seq":1,"cid":"c1","from":"میرزا","role":"mirza","text":"اول","at":1},
                      {"seq":3,"cid":"c3","from":"x","role":"staff","text":"","at":3}]}
                    """);
            var body = await req.Content!.ReadAsStringAsync(ct);
            Assert.Contains("\"cid\":\"abc\"", body);
            return Json(HttpStatusCode.Created, """
                {"ok":true,"message":{"seq":4,"cid":"abc","from":"مدیر","role":"admin","text":"سلام","at":4}}
                """);
        };
        var chat = new StationChat(() => Station());

        var (ok, list, _, last) = await chat.FetchAsync(0);
        Assert.Equal(2, last);
        Assert.True(ok);
        Assert.Equal(new long[] { 1, 2 }, list.Select(m => m.Seq));   // خالی رد شد، ترتیب درست
        Assert.Equal("میرزا", list[0].RoleText);
        Assert.Contains("since=0", seen[0].RequestUri!.Query);
        Assert.Equal("Bearer tok-123", seen[0].Headers.Authorization!.ToString());

        var sent = await chat.PostAsync("abc", "مدیر", "admin", "سلام");
        Assert.True(sent.Ok);
        Assert.Equal(4, sent.Message!.Seq);
    }

    [Fact]
    public async Task Gorooh_404eServer_MiguyadServerKohneAst_VaDareDovomRaNemizanad()
    {
        var hits = 0;
        StationChat.TestTransport = (req, ct) =>
        {
            hits++;
            return Task.FromResult(Json(HttpStatusCode.NotFound, """{"error":"not_found"}"""));
        };
        var (ok, _, why, _) = await new StationChat(() => Station()).FetchAsync(0);
        Assert.False(ok);
        Assert.Contains("کهنه", why);
        Assert.Equal(1, hits);
    }

    [Fact]
    public async Task Gorooh_DareAvvalNaresid_DareDovom()
    {
        var hosts = new List<string>();
        StationChat.TestTransport = (req, ct) =>
        {
            hosts.Add(req.RequestUri!.Host);
            if (hosts.Count == 1) throw new HttpRequestException("down");
            return Task.FromResult(Json(HttpStatusCode.OK, """{"ok":true,"messages":[]}"""));
        };
        var (ok, _, _, _) = await new StationChat(() => Station()).FetchAsync(5);
        Assert.True(ok);
        Assert.Equal(2, hosts.Count);
        Assert.Equal("192.168.1.5", hosts[0]);
    }

    // ══ صفحه — تمام‌صفحه، سه ستون ════════════════════════════════════════════

    private static string SrcRoot()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null && !Directory.Exists(Path.Combine(d.FullName, "PumpYaqobi.App"))) d = d.Parent;
        return d!.FullName;
    }

    [Fact]
    public void Safhe_TamamSafhe_SeSotoone_BiSectionPage()
    {
        var root = SrcRoot();
        var xaml = File.ReadAllText(Path.Combine(root, "PumpYaqobi.App", "Views", "Sections", "ChatSectionView.axaml"));
        var vm = File.ReadAllText(Path.Combine(root, "PumpYaqobi.App", "ViewModels", "Sections", "ChatSectionViewModel.cs"));

        Assert.Contains("ColumnDefinitions=\"310,*,320\"", xaml);
        Assert.DoesNotContain("<c:SectionPage", xaml);
        Assert.Contains("BackCommand", xaml);
        Assert.Contains("ToggleTrashCommand", xaml);
        Assert.Contains("RestoreCommand", xaml);
        Assert.Contains("OpenAccountCommand", xaml);
        Assert.Contains("InfoBooks", xaml);
        Assert.Contains("SharedImages", xaml);
        Assert.Contains("IsPageOpen = true;", vm);
        // ⛔ پیام‌رسانِ کهنهٔ سرورِ خانگی (موضوعِ سراسریِ staff) دیگر در این صفحه نیست
        Assert.DoesNotContain("HomeServer", vm);
        Assert.DoesNotContain("/api/notify", vm);
        Assert.DoesNotContain("topic", vm);
    }

    [Fact]
    public void Safhe_BiShenavandeyeChidman_VaSayehBiMahv()
    {
        var root = SrcRoot();
        var cs = File.ReadAllText(Path.Combine(root, "PumpYaqobi.App", "Views", "Sections", "ChatSectionView.axaml.cs"));
        var xaml = File.ReadAllText(Path.Combine(root, "PumpYaqobi.App", "Views", "Sections", "ChatSectionView.axaml"));
        Assert.DoesNotContain("LayoutUpdated", cs);
        Assert.Contains("ClientSizeProperty", cs);
        // سه ستونِ صفحه‌قد «panel»اند (سایهٔ بی‌محو)، نه «card»ِ محو
        Assert.DoesNotContain("Classes=\"card\"", xaml);
    }
}
