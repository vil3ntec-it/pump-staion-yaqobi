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
public sealed record FoundServer(string Name, string Url, string Id);

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

    /// <summary>اولین سروری که جواب داد — همان چیزی که ثبتِ خودکار می‌خواهد.</summary>
    public static async Task<FoundServer?> FindFirstAsync(TimeSpan? wait = null, CancellationToken ct = default)
    {
        var all = await FindAsync(wait, ct);
        return all.Count > 0 ? all[0] : null;
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
            return new FoundServer(
                name.Length > 0 ? name : from.ToString(),
                $"http://{from}:{port}",
                S("id"));
        }
        catch { return null; }
    }
}
