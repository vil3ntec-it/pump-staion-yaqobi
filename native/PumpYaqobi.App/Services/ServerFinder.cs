using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace PumpYaqobi.App.Services;

/// <summary>یک سرورِ خانگی که در شبکه پیدا شد.</summary>
/// <param name="Name">نامی که خودِ سرور گفت.</param>
/// <param name="Url">نشانیِ آمادهٔ استفاده، مثل ‎http://192.168.1.20:4700‎.</param>
/// <param name="Id">شناسهٔ ثابتِ همان سرور — با نصبِ دوباره عوض نمی‌شود.</param>
/// <param name="LanUrl">
/// نشانی‌ای که <b>گوشی و کامپیوترِ دیگر</b> با آن می‌رسند. وقتی جواب از
/// ‎127.0.0.1‎ آمده (سرور روی همین کامپیوتر است)، ‎Url‎ برای دیگران بی‌معناست و
/// این از کارتِ خودِ سرور (‎url‎) می‌آید؛ وگرنه همان ‎Url‎ است.
/// </param>
public sealed record FoundServer(string Name, string Url, string Id, string LanUrl = "");

/// <summary>
/// ══ «سرور کجاست؟» — بی این‌که کاربر چیزی تایپ کند ═══════════════════════════
///
/// آی‌پیِ سرورِ خانگی با هر بار روشن شدنِ مودم عوض می‌شود. تا امروز صاحبِ پمپ
/// باید آن عدد را پیدا می‌کرد و دستی در تنظیمات می‌نوشت — و هر بار که عوض
/// می‌شد، برنامه بی‌صدا از سرور جدا می‌ماند و کسی نمی‌فهمید چرا گوشیِ کارمند
/// دادهٔ دیروز را نشان می‌دهد.
///
/// حالا برنامه یک بستهٔ کوچک در شبکه پخش می‌کند و سرور خودش جواب می‌دهد:
/// «من این‌جام، نشانی‌ام این است». همان چیزی که ‎homelab-panel/server‎ در
/// ‎src/discovery.js‎ گوش می‌دهد.
///
/// ⚠️ هیچ رمزی در این گفت‌وگو نیست — نه پرسیده می‌شود نه جواب داده. رمز از
/// راهِ <see cref="StationLink"/> و مسیرِ ثبت گرفته می‌شود.
///
/// ⚠️ هیچ استثنایی بیرون نمی‌دهد. نبودنِ شبکه، بسته بودنِ پورت و فایروال
/// همه یعنی «پیدا نشد»، نه «خطا».
/// </summary>
public static class ServerFinder
{
    /// <summary>همان پورتی که سرور رویش گوش می‌دهد (‎HLP_DISCOVERY_PORT‎).</summary>
    public const int Port = 4702;

    private const string Probe = "PUMP-SERVER-DISCOVER?";
    private const string Reply = "PUMP-SERVER-HERE";

    /// <summary>پیش‌فرضِ صبر — سرورِ خانگی معمولاً زیرِ ۵۰ میلی‌ثانیه جواب می‌دهد.</summary>
    public static readonly TimeSpan DefaultWait = TimeSpan.FromSeconds(2);

    /// <summary>
    /// هر سروری که در شبکه جواب داد. فهرستِ خالی یعنی «نبود» — خطا نیست.
    /// </summary>
    public static async Task<IReadOnlyList<FoundServer>> FindAsync(
        TimeSpan? wait = null, CancellationToken ct = default)
    {
        var found = new List<FoundServer>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        UdpClient? udp = null;
        try
        {
            udp = new UdpClient(AddressFamily.InterNetwork);
            udp.EnableBroadcast = true;
            udp.Client.Bind(new IPEndPoint(IPAddress.Any, 0));
        }
        catch
        {
            udp?.Dispose();
            return found;
        }

        using (udp)
        {
            var probe = Encoding.UTF8.GetBytes(Probe);
            foreach (var target in Targets())
            {
                try { await udp.SendAsync(probe, probe.Length, target); }
                catch { /* این کارت شبکه نشد — بقیه را امتحان کن */ }
            }

            var deadline = DateTime.UtcNow + (wait ?? DefaultWait);
            while (!ct.IsCancellationRequested)
            {
                var left = deadline - DateTime.UtcNow;
                if (left <= TimeSpan.Zero) break;

                using var window = CancellationTokenSource.CreateLinkedTokenSource(ct);
                window.CancelAfter(left);

                UdpReceiveResult got;
                try { got = await udp.ReceiveAsync(window.Token); }
                catch { break; } // وقت تمام شد یا سوکت بسته شد

                var card = Parse(Encoding.UTF8.GetString(got.Buffer), got.RemoteEndPoint.Address);
                if (card is null) continue;
                if (seen.Add(card.Url)) found.Add(card);
            }
        }

        return found;
    }

    /// <summary>اولین سروری که جواب داد — فقط برای سنجه‌ها؛ ثبت از <see cref="FindReachableAsync"/> می‌رود.</summary>
    public static async Task<FoundServer?> FindFirstAsync(TimeSpan? wait = null, CancellationToken ct = default)
    {
        var all = await FindAsync(wait, ct);
        return all.Count > 0 ? all[0] : null;
    }

    /// <summary>
    /// ══ «جواب داد» با «می‌رسیم» یکی نیست (۱۴۰۵/۰۷/۱۳) ═════════════════════
    ///
    /// کشفِ خودکار روی UDP است و روی همهٔ کارت‌ها جواب می‌دهد، ولی خودِ پنل
    /// ممکن است فقط روی ‎127.0.0.1‎ گوش بدهد (مرکز فرمانِ ویندوز تا ۱.۵۰.۱۱) یا
    /// دیوارِ آتشِ ویندوز پورتش را از شبکه ببندد. تا امروز اولین جواب برداشته
    /// می‌شد — که روی خودِ همان کامپیوتر گاهی نشانیِ کارتِ شبکه بود و «اتصال رد
    /// شد» می‌گرفت، و چراغ برای همیشه سرخ می‌ماند.
    ///
    /// حالا هر نشانی با ‎GET /health‎ سنجیده می‌شود و اولینِ رسیدنی برنده است؛
    /// نشانیِ شبکه پیش از ‎127.0.0.1‎ (چون همان است که به گوشی‌ها هم می‌رسد).
    /// هیچ‌کدام نرسید ⇒ همان اولین جواب، مثلِ قبل.
    /// </summary>
    public static async Task<FoundServer?> FindReachableAsync(TimeSpan? wait = null, CancellationToken ct = default)
    {
        var all = await FindAsync(wait, ct);
        if (all.Count == 0) return null;
        foreach (var s in Order(all))
            if (await AnswersAsync(s.Url, ct)) return s;
        return all[0];
    }

    /// <summary>نشانیِ شبکه اول، ‎127.0.0.1‎ آخر — خالص، برای آزمون.</summary>
    public static IEnumerable<FoundServer> Order(IEnumerable<FoundServer> found) =>
        found.OrderBy(f => IsLoopbackUrl(f.Url) ? 1 : 0);

    /// <summary>نشانیِ ‎127.x‎ / ‎localhost‎ / ‎::1‎؟</summary>
    public static bool IsLoopbackUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var u)) return false;
        if (u.IsLoopback) return true;
        return IPAddress.TryParse(u.Host.Trim('[', ']'), out var ip) && IPAddress.IsLoopback(ip);
    }

    private static readonly System.Net.Http.HttpClient Probe2 = new() { Timeout = TimeSpan.FromMilliseconds(1500) };

    /// <summary>سنجه‌ها مسیرِ ‎/health‎ را عوض می‌کنند؛ برنامه هیچ‌وقت.</summary>
    internal static Func<string, CancellationToken, Task<bool>>? TestAnswers;

    private static async Task<bool> AnswersAsync(string url, CancellationToken ct)
    {
        if (TestAnswers is not null) return await TestAnswers(url, ct);
        try
        {
            using var res = await Probe2.GetAsync(url.TrimEnd('/') + "/health", ct);
            return (int)res.StatusCode < 500;
        }
        catch { return false; }
    }

    /// <summary>
    /// کجاها بپرسیم.
    ///
    /// ⚠️ فقط ‎255.255.255.255‎ کافی نیست: بعضی کارت‌های شبکه و بعضی
    /// روترها بستهٔ «پخشِ همگانی» را جابه‌جا نمی‌کنند ولی «پخشِ همان
    /// زیرشبکه» (‎192.168.1.255‎) را می‌کنند. هر دو را می‌فرستیم، به‌اضافهٔ
    /// خودِ همین کامپیوتر برای وقتی که سرور روی همین دستگاه است.
    /// </summary>
    private static IEnumerable<IPEndPoint> Targets()
    {
        yield return new IPEndPoint(IPAddress.Loopback, Port);
        yield return new IPEndPoint(IPAddress.Broadcast, Port);

        NetworkInterface[] nics;
        try { nics = NetworkInterface.GetAllNetworkInterfaces(); }
        catch { yield break; }

        foreach (var nic in nics)
        {
            if (nic.OperationalStatus != OperationalStatus.Up) continue;
            if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;

            IPInterfaceProperties props;
            try { props = nic.GetIPProperties(); }
            catch { continue; }

            foreach (var info in props.UnicastAddresses)
            {
                if (info.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                var broadcast = BroadcastOf(info.Address, info.IPv4Mask);
                if (broadcast is not null) yield return new IPEndPoint(broadcast, Port);
            }
        }
    }

    /// <summary>نشانیِ پخشِ همان زیرشبکه — ‎null‎ یعنی نشد حساب کرد.</summary>
    private static IPAddress? BroadcastOf(IPAddress address, IPAddress? mask)
    {
        if (mask is null) return null;
        try
        {
            var a = address.GetAddressBytes();
            var m = mask.GetAddressBytes();
            if (a.Length != 4 || m.Length != 4) return null;
            var b = new byte[4];
            for (var i = 0; i < 4; i++) b[i] = (byte)(a[i] | (byte)~m[i]);
            return new IPAddress(b);
        }
        catch { return null; }
    }

    /// <summary>
    /// خواندنِ کارتِ سرور. شکلِ ناشناس ⇒ ‎null‎، نه استثنا.
    ///
    /// ⚠️ نشانی از روی <b>همان آی‌پی‌ای که جواب داد</b> ساخته می‌شود، نه از
    /// روی ‎url‎ی داخلِ کارت: سرورِ چندکارته چند نشانی دارد و فقط همانی که
    /// بسته از آن آمد، از این‌جا قطعاً در دسترس است.
    /// </summary>
    public static FoundServer? Parse(string json, IPAddress from)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var r = doc.RootElement;
            if (r.ValueKind != JsonValueKind.Object) return null;

            string S(string name) =>
                r.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";

            if (S("reply") != Reply) return null;

            var port = r.TryGetProperty("port", out var p) && p.TryGetInt32(out var n) && n > 0 ? n : 4700;
            var name = S("name");
            var url = $"http://{(from.AddressFamily == AddressFamily.InterNetworkV6 ? "[" + from + "]" : from.ToString())}:{port}";
            //  ⛔ جواب از ‎127.0.0.1‎ ⇒ برای گوشی‌ها نشانیِ کارتِ شبکهٔ خودِ سرور
            var card = S("url");
            var lan = IPAddress.IsLoopback(from) && card.Length > 0 && !IsLoopbackUrl(card) ? card : url;
            return new FoundServer(
                name.Length > 0 ? name : from.ToString(),
                url,
                S("id"),
                lan);
        }
        catch { return null; }
    }
}
