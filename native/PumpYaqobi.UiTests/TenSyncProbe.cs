using System.Diagnostics;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using PumpYaqobi.App.Services;

namespace PumpYaqobi.UiTests;

/// <summary>
/// ══ ده سال داده روی سرورِ حسابِ واقعی — «هم روی کامپیوتر هم روی سرور» ═══════
///
/// خواستهٔ صاحب ریپو (۱۴۰۵/۰۷/۱۴): «با ده سال اطلاعات تست کن… هم روی کامپیوتر
/// هم روی سرور». دو فرآیندِ جدا، همان دو کامپیوترِ واقعی:
///
///   up    کامپیوترِ الف: دفترِ N ساله ⇒ ورود به حسابِ تازه ⇒ «بارِ اول»ِ
///         همگام‌سازی همه را به سرورِ حسابِ واقعی می‌فرستد. زمان و شمارِ دسته.
///   down  کامپیوترِ ب: دفترِ خالی ⇒ ورود به **همان** حساب ⇒ همه را می‌گیرد.
///         شمارِ ردیفِ هر جدول باید مو‌به‌مو همان کامپیوترِ الف باشد.
///
///     node test/signup-stack.mjs live.json                  (ریپوی server)
///     PUMP_YEARS=10 dotnet run … -- tensync live.json up   out.json
///     dotnet run … -- tensync live.json down out.json
///
/// ⚠️ در CI نیست: پشتهٔ بیرونی می‌خواهد.
/// </summary>
internal static class TenSyncProbe
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(120) };
    private static string _pub = "", _mailCodes = "";
    private const string Pass = "Pump!1405ten";

    /// <summary>جدول‌هایی که عمداً همگام نمی‌شوند (`OpLog.Local`) و جدول‌های خودِ همگام‌سازی.</summary>
    private static readonly HashSet<string> Local = new(StringComparer.OrdinalIgnoreCase)
    {
        "AppUsers", "Settings", "TrashItems", "AuditEntries", "Audit", "SyncOps", "SyncState",
        "__EFMigrationsHistory", "sqlite_sequence", "sqlite_stat1",
    };

    public static int Run(string[] args)
    {
        if (args.Length < 4 || !File.Exists(args[1])) { Console.WriteLine("tensync <live.json> up|down <out.json>"); return 2; }
        var live = JsonDocument.Parse(File.ReadAllText(args[1])).RootElement;
        var pub = new Uri(live.GetProperty("public").GetString()!);
        _pub = pub.ToString().TrimEnd('/');
        _mailCodes = live.GetProperty("mailCodes").GetString()!;
        var mode = args[2];
        var outFile = args[3];

        CloudLink.TestTransport = async (req, ct) =>
        {
            var to = new UriBuilder(req.RequestUri!) { Scheme = pub.Scheme, Host = pub.Host, Port = pub.Port }.Uri;
            var fwd = new HttpRequestMessage(req.Method, to);
            foreach (var h in req.Headers) fwd.Headers.TryAddWithoutValidation(h.Key, h.Value);
            if (req.Content is not null)
            {
                var bytes = await req.Content.ReadAsByteArrayAsync(ct);
                fwd.Content = new ByteArrayContent(bytes);
                foreach (var h in req.Content.Headers) fwd.Content.Headers.TryAddWithoutValidation(h.Key, h.Value);
            }
            return await Http.SendAsync(fwd, ct);
        };

        var dir = Path.Combine(Path.GetTempPath(), "pump-tensync-" + mode + "-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);
        var db = Path.Combine(dir, "pump.db");

        string email;
        if (mode == "up")
        {
            var sw = Stopwatch.StartNew();
            YearsAudit.Seed(db);
            Console.WriteLine($"دفتر ساخته شد: {new FileInfo(db).Length / 1_048_576.0:0.0} MB · {sw.ElapsedMilliseconds:N0}ms");
            email = "ten-" + Guid.NewGuid().ToString("N")[..8] + "@example.com";
            Register(email);
        }
        else
        {
            email = JsonDocument.Parse(File.ReadAllText(outFile)).RootElement.GetProperty("email").GetString()!;
            PumpYaqobi.Services.Data.PumpDbFactory f = new(db);
            f.EnsureReady();
        }

        AppHost.Start(db);
        var host = AppHost.Current;
        if (host.Auth.NeedsFirstRun()) host.Auth.CreateFirstAdmin("1234");
        host.Auth.SignIn("admin", "1234");

        //  ورود با ایمیل و رمز — همان مسیرِ صفحهٔ ورود
        var login = Post("/api/auth/login", new { email, password = Pass, app = "pump",
            device = new { uid = "ten-" + mode, name = mode, platform = "windows" } }, null);
        var file = AppSettings.Load();
        file.CloudAccountToken = login.GetProperty("accessToken").GetString()!;
        file.CloudRefreshToken = login.GetProperty("refreshToken").GetString()!;
        file.CloudUserId = login.GetProperty("user").GetProperty("id").ToString();
        file.CloudEmail = email;
        file.Save();

        //  ثبتِ همین کامپیوتر (همان دورِ پس‌زمینه)
        var keep = typeof(StationPublisher).GetMethod("CloudKeepAsync", BindingFlags.NonPublic | BindingFlags.Static)!;
        ((Task)keep.Invoke(null, new object[] { CancellationToken.None, true })!).GetAwaiter().GetResult();
        file = AppSettings.Load();
        Console.WriteLine($"دستگاه بند شد؟ {file.CloudDeviceToken.Length > 0} · پمپ {file.CloudStationId} · {CloudLink.LastBindWhy}");
        if (file.CloudDeviceToken.Length == 0) return 1;

        //  همگام‌سازی — فقط خودِ موتور، دور به دور
        SyncEngine.Disabled = false;
        var eng = host.Sync;
        var step = typeof(SyncEngine).GetMethod("StepAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var clock = Stopwatch.StartNew();
        var rounds = 0; var lastPrint = 0L; var idle = 0;
        while (clock.Elapsed < TimeSpan.FromMinutes(60))
        {
            rounds++;
            ((Task)step.Invoke(eng, new object[] { true, CancellationToken.None })!).GetAwaiter().GetResult();
            var st = new PumpYaqobi.Services.Data.SyncStore(host.Db).State();
            if (clock.ElapsedMilliseconds - lastPrint > 10_000)
            {
                lastPrint = clock.ElapsedMilliseconds;
                Console.WriteLine($"   {clock.Elapsed.TotalSeconds,6:0}s · دور {rounds} · صف {eng.Queued:N0} · گرفته {eng.PrimeGot:N0} · {eng.Light} {eng.LastError}");
            }
            var done = st.SeededAt > 0 && eng.Queued == 0 && eng.LastError.Length == 0;
            if (mode == "down") done = done && st.PrimedAt > 0 || (st.SeededAt > 0 && eng.Queued == 0 && eng.PrimeGot > 0 && !eng.Priming && idle > 2);
            if (done) { if (++idle > 3) break; } else idle = 0;
        }
        Console.WriteLine($"همگام‌سازی: {clock.Elapsed.TotalSeconds:0.0}s · {rounds} دور · صف {eng.Queued} · خطا «{eng.LastError}» · گرفته {eng.PrimeGot:N0}");

        var counts = Count(db);
        var total = counts.Values.Sum();
        Console.WriteLine($"ردیف‌های همگام‌شدنی: {total:N0} در {counts.Count} جدول");

        if (mode == "up")
        {
            File.WriteAllText(outFile, JsonSerializer.Serialize(new { email, counts, seconds = clock.Elapsed.TotalSeconds }));
            return eng.Queued == 0 && eng.LastError.Length == 0 ? 0 : 1;
        }

        var want = JsonDocument.Parse(File.ReadAllText(outFile)).RootElement.GetProperty("counts");
        var bad = 0;
        foreach (var p in want.EnumerateObject())
        {
            var got = counts.GetValueOrDefault(p.Name);
            var ok = got == p.Value.GetInt64();
            if (!ok) bad++;
            if (!ok || p.Value.GetInt64() > 0)
                Console.WriteLine($"  {(ok ? "✔" : "✖")} {p.Name,-26}{p.Value.GetInt64(),10:N0} ⇒ {got,10:N0}");
        }
        Console.WriteLine(bad == 0 ? "✅ کامپیوترِ دوم همهٔ دفتر را گرفت — مو‌به‌مو" : $"❌ {bad} جدول نابرابر");
        return bad == 0 ? 0 : 1;
    }

    /// <summary>ردیف‌های زندهٔ هر جدولِ همگام‌شدنی.</summary>
    private static Dictionary<string, long> Count(string db)
    {
        var outD = new Dictionary<string, long>();
        using var c = new SqliteConnection("Data Source=" + db + ";Mode=ReadOnly");
        c.Open();
        var tables = new List<string>();
        using (var q = c.CreateCommand())
        {
            q.CommandText = "SELECT name FROM sqlite_master WHERE type='table'";
            using var r = q.ExecuteReader();
            while (r.Read()) tables.Add(r.GetString(0));
        }
        foreach (var t in tables.Where(t => !Local.Contains(t)))
        {
            bool hasDel;
            using (var q = c.CreateCommand())
            {
                q.CommandText = $"SELECT COUNT(*) FROM pragma_table_info('{t}') WHERE name='IsDeleted'";
                hasDel = Convert.ToInt64(q.ExecuteScalar()) > 0;
            }
            using var q2 = c.CreateCommand();
            q2.CommandText = $"SELECT COUNT(*) FROM \"{t}\"" + (hasDel ? " WHERE IsDeleted = 0" : "");
            outD[t] = Convert.ToInt64(q2.ExecuteScalar());
        }
        return outD;
    }

    private static void Register(string email)
    {
        var sentAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        Post("/api/auth/register/start", new { name = "صاحبِ ده‌ساله", email, password = Pass, passwordConfirm = Pass, app = "pump" }, null);
        var code = CodeFor(email, sentAt);
        var v = Post("/api/auth/register/verify", new { email, code, app = "pump" }, null);
        var ticket = v.GetProperty("ticket").GetString()!;
        var ver = v.TryGetProperty("terms", out var t) && t.TryGetProperty("version", out var tv) ? tv.GetString() : "";
        var done = Post("/api/auth/register/complete", new
        {
            ticket, name = "صاحبِ ده‌ساله", password = Pass,
            terms = new { accepted = true, version = ver },
            device = new { uid = "ten-reg", name = "reg", platform = "windows" }, app = "pump",
        }, null);
        Post("/api/pump", new { name = "پمپِ ده‌ساله" }, done.GetProperty("accessToken").GetString());
        Console.WriteLine("حساب ساخته شد: " + email);
    }

    private static JsonElement Post(string path, object body, string? bearer)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, _pub + path)
        { Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json") };
        req.Headers.TryAddWithoutValidation("X-App-Id", CloudConfig.ApplicationId);
        if (bearer is not null) req.Headers.TryAddWithoutValidation("Authorization", "Bearer " + bearer);
        var res = Task.Run(() => Http.SendAsync(req)).GetAwaiter().GetResult();
        var text = Task.Run(() => res.Content.ReadAsStringAsync()).GetAwaiter().GetResult();
        if (!res.IsSuccessStatusCode) Console.WriteLine($"     ⓘ {path} ⇒ {(int)res.StatusCode} {text[..Math.Min(200, text.Length)]}");
        try { return JsonDocument.Parse(text).RootElement.Clone(); } catch { return default; }
    }

    private static string CodeFor(string email, long sentAt)
    {
        for (var i = 0; i < 120; i++)
        {
            if (File.Exists(_mailCodes))
                foreach (var line in File.ReadAllLines(_mailCodes).Reverse())
                {
                    try
                    {
                        var j = JsonDocument.Parse(line).RootElement;
                        if (j.GetProperty("at").GetInt64() + 2000 < sentAt) continue;
                        if (!j.GetProperty("to").GetString()!.Contains(email, StringComparison.OrdinalIgnoreCase)) continue;
                        var c = j.GetProperty("code").GetString() ?? "";
                        if (c.Length == 6) return c;
                    }
                    catch { }
                }
            Thread.Sleep(250);
        }
        return "";
    }
}
