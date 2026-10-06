using System.Diagnostics;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using PumpYaqobi.App.Services;
using PumpYaqobi.Domain.Entities;

namespace PumpYaqobi.UiTests;

/// <summary>
/// ══ «هر تغییر، درجا روی سرور و کامپیوترِ دیگر» — با سرورِ حسابِ واقعی ═════════
///
/// خواستهٔ صاحب ریپو (۱۴۰۵/۰۷/۲۲): «حسابی اضافه کردم یا حذف، اصلاً درست نشد…
/// تمامِ اطلاعات در لحظه باید به سرور بروند و بیایند.» دو فرآیند، دو کامپیوترِ
/// واقعی، یک حساب، **حلقهٔ واقعیِ پس‌زمینه** (نه دور به دورِ دستی):
///
///   a   کامپیوترِ الف: حسابِ تازه ⇒ افزودنِ قرض‌دار، ردیف، رسید، ویرایشِ نام،
///       حذفِ ردیف، حذفِ قرض‌دار — و پس از هر کار منتظرِ دیدنِ «ب».
///   b   کامپیوترِ ب: همان حساب، دفترِ خالی ⇒ هر ۱۰۰ms دفترِ خودش را می‌خواند و
///       لحظهٔ دیدنِ هر تغییر را می‌نویسد.
///
///     node test/signup-stack.mjs live.json                     (ریپوی server)
///     dotnet run … -- livesync live.json a &lt;پوشهٔ مشترک&gt;   &amp;
///     dotnet run … -- livesync live.json b &lt;پوشهٔ مشترک&gt;
///
/// ⚠️ در CI نیست: پشتهٔ بیرونی می‌خواهد.
/// </summary>
internal static class LiveSyncProbe
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(60) };
    private static string _pub = "", _mailCodes = "";
    private const string Pass = "Pump!1405live";

    /// <summary>هر گام: نام، و این‌که «ب» از روی دفترِ خودش کِی آن را دیده است.</summary>
    private static readonly string[] Steps =
        { "add-debtor", "add-row", "rasid", "rename", "add-sub", "del-row", "del-sub", "del-debtor" };

    public static int Run(string[] args)
    {
        if (args.Length < 4 || !File.Exists(args[1])) { Console.WriteLine("livesync <live.json> a|b <dir>"); return 2; }
        var live = JsonDocument.Parse(File.ReadAllText(args[1])).RootElement;
        var pub = new Uri(live.GetProperty("public").GetString()!);
        _pub = pub.ToString().TrimEnd('/');
        _mailCodes = live.GetProperty("mailCodes").GetString()!;
        var mode = args[2];
        var shared = args[3];
        Directory.CreateDirectory(shared);

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

        //  ⚠️ سوکتِ زنده هم به همان پشته — همان راهی که تونل می‌رود (‎LIVE_WS=0‎ ⇒ بی سوکت)
        if (Environment.GetEnvironmentVariable("LIVE_WS") != "0")
            CloudConfig.TestWsBase = (pub.Scheme == "https" ? "wss://" : "ws://") + pub.Authority;

        var dir = Path.Combine(Path.GetTempPath(), "pump-livesync-" + mode + "-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);
        var db = Path.Combine(dir, "pump.db");
        var emailFile = Path.Combine(shared, "email.txt");

        string email;
        if (mode == "bk")
        {
            var swSeed = Stopwatch.StartNew();
            YearsAudit.Seed(db);
            Console.WriteLine($"[bk] دفتر: {new FileInfo(db).Length / 1_048_576.0:0.0} MB · {swSeed.ElapsedMilliseconds:N0}ms");
            email = "bk-" + Guid.NewGuid().ToString("N")[..8] + "@example.com";
            Register(email);
        }
        else if (mode == "a")
        {
            email = "live-" + Guid.NewGuid().ToString("N")[..8] + "@example.com";
            Register(email);
            File.WriteAllText(emailFile, email);
        }
        else
        {
            for (var i = 0; i < 600 && !File.Exists(emailFile); i++) Thread.Sleep(200);
            email = File.ReadAllText(emailFile).Trim();
            new PumpYaqobi.Services.Data.PumpDbFactory(db).EnsureReady();
            using var c = new SqliteConnection("Data Source=" + db);
            c.Open();
            using var q = c.CreateCommand();
            q.CommandText = "INSERT OR REPLACE INTO sqlite_sequence(name, seq) " +
                            "SELECT name, 7000 FROM sqlite_master WHERE type='table' AND sql LIKE '%AUTOINCREMENT%'";
            q.ExecuteNonQuery();
        }

        AppHost.Start(db);
        var host = AppHost.Current;
        if (host.Auth.NeedsFirstRun()) host.Auth.CreateFirstAdmin("1234");
        host.Auth.SignIn("admin", "1234");

        var login = Post("/api/auth/login", new { email, password = Pass, app = "pump",
            device = new { uid = "live-" + mode, name = mode, platform = "windows" } }, null);
        var file = AppSettings.Load();
        file.CloudAccountToken = login.GetProperty("accessToken").GetString()!;
        file.CloudRefreshToken = login.GetProperty("refreshToken").GetString()!;
        file.CloudUserId = login.GetProperty("user").GetProperty("id").ToString();
        file.CloudEmail = email;
        //  ⚠️ دو «کامپیوتر» از یک پوشه می‌دوند — شناسهٔ دستگاه از مسیرِ نصب است، پس جدا
        file.CloudDeviceUid = "pc-live-" + mode + "-" + Guid.NewGuid().ToString("N")[..6];
        file.Save();
        var keep = typeof(StationPublisher).GetMethod("CloudKeepAsync", BindingFlags.NonPublic | BindingFlags.Static)!;
        ((Task)keep.Invoke(null, new object[] { CancellationToken.None, true })!).GetAwaiter().GetResult();
        file = AppSettings.Load();
        Console.WriteLine($"[{mode}] دستگاه بند شد؟ {file.CloudDeviceToken.Length > 0} · {CloudLink.LastBindWhy}");
        if (file.CloudDeviceToken.Length == 0) return 1;
        {
            var probe = new CloudLink(file, () => Task.CompletedTask);
            var pr = probe.SyncPullAsync(0).GetAwaiter().GetResult();
            Console.WriteLine($"[{mode}] pull مستقیم: ok={pr.Ok} code={pr.Code} why={pr.Why} · توکن {file.CloudDeviceToken[..Math.Min(3, file.CloudDeviceToken.Length)]}…");
        }

        if (mode == "bk") return RunBackupTiming(host, dir);

        //  ⛔ حلقهٔ واقعیِ برنامه — همان ‎sync.Start()‎ِ ‎MainViewModel‎
        SyncEngine.Disabled = false;
        var eng = host.Sync;
        eng.Start();
        eng.PrimeNow();
        var clock = Stopwatch.StartNew();
        while (clock.Elapsed < TimeSpan.FromSeconds(90))
        {
            var st = new PumpYaqobi.Services.Data.SyncStore(host.Db).State();
            if (st.SeededAt > 0 && st.PrimedAt > 0 && eng.Queued == 0) break;
            Thread.Sleep(200);
        }
        Console.WriteLine($"[{mode}] آماده در {clock.ElapsedMilliseconds:N0}ms · {eng.Light} {eng.Reason}");
        File.WriteAllText(Path.Combine(shared, mode + ".ready"), "1");

        return mode == "a" ? RunA(host, eng, shared) : RunB(host, shared);
    }

    // ── کامپیوترِ الف ──────────────────────────────────────────────────

    private static int RunA(AppHost host, SyncEngine eng, string shared)
    {
        for (var i = 0; i < 600 && !File.Exists(Path.Combine(shared, "b.ready")); i++) Thread.Sleep(200);
        if (Environment.GetEnvironmentVariable("LS_KILL_TOKEN") == "1")
        {
            //  ⛔ توکنِ دستگاه همین حالا می‌میرد (همان ‎401 invalid_token‎ِ سرور) — برنامه
            //  باید خودش از نو بند شود و هیچ تغییری گم نشود
            var f = AppSettings.Load();
            f.CloudDeviceToken = "pd_" + Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
            f.Save();
            Console.WriteLine("[a] توکنِ دستگاه عمداً مرد");
        }
        var bad = 0;
        Debtor? d = null;
        DebtAccount? sub = null;
        long rowId = 0;
        var results = new List<string>();
        foreach (var step in Steps)
        {
            var at = DateTime.UtcNow;
            switch (step)
            {
                case "add-debtor":
                    d = host.Debtors.AddDebtorAsync("قرض‌دارِ زنده", null, false).GetAwaiter().GetResult();
                    break;
                case "add-row":
                    var row = new DebtRow { FuelAccountId = d!.MainAccount.Id, DateShamsi = "1405/07/22", Name = "ردیفِ زنده", Liters = 10, PricePerLiter = 70, Bardagi = 700 };
                    host.Debtors.SaveRowAsync(row).GetAwaiter().GetResult();
                    rowId = row.Id;
                    break;
                case "rasid":
                    var r2 = new DebtRow { FuelAccountId = d!.MainAccount.Id, DateShamsi = "1405/07/22", Name = "رسیدِ زنده", Rasid = 300 };
                    host.Debtors.SaveRowAsync(r2).GetAwaiter().GetResult();
                    break;
                case "rename":
                    d!.Name = "قرض‌دارِ زندهٔ تازه‌نام";
                    host.Debtors.UpdateDebtorAsync(d).GetAwaiter().GetResult();
                    break;
                case "add-sub":
                    sub = host.Debtors.AddSubAccountAsync(d!.Id, "فرعیِ زنده").GetAwaiter().GetResult();
                    break;
                case "del-row":
                    host.Debtors.DeleteRowAsync(rowId).GetAwaiter().GetResult();
                    break;
                case "del-sub":
                    host.Debtors.DeleteAccountAsync(sub!.Id).GetAwaiter().GetResult();
                    break;
                case "del-debtor":
                    host.Debtors.DeleteDebtorAsync(d!.Id).GetAwaiter().GetResult();
                    break;
            }
            File.WriteAllText(Path.Combine(shared, "step.txt"), step + "|" + at.Ticks);
            //  الف: کِی صفش خالی شد (یعنی سرور پذیرفت)
            var sw = Stopwatch.StartNew();
            while (sw.Elapsed < TimeSpan.FromSeconds(30) && (eng.Queued > 0 || new PumpYaqobi.Services.Data.SyncStore(host.Db).Pending() > 0)) Thread.Sleep(50);
            var pushed = sw.ElapsedMilliseconds;
            //  ب: کِی دید
            var seenFile = Path.Combine(shared, "seen-" + step + ".txt");
            var sw2 = Stopwatch.StartNew();
            while (sw2.Elapsed < TimeSpan.FromSeconds(75) && !File.Exists(seenFile)) Thread.Sleep(50);
            var seen = File.Exists(seenFile) ? (new DateTime(long.Parse(File.ReadAllText(seenFile))) - at).TotalMilliseconds : -1;
            var ok = pushed < 30_000 && seen >= 0 && seen < 5_000;
            if (!ok) bad++;
            var line = $"  {(ok ? "✔" : "✖")} {step,-11} به سرور رسید: {pushed,6:N0}ms · کامپیوترِ ب دید: {(seen < 0 ? "هرگز (۷۵ ثانیه)" : seen.ToString("N0") + "ms")}";
            Console.WriteLine(line);
            results.Add(line);
        }
        File.WriteAllText(Path.Combine(shared, "done.txt"), "1");
        File.WriteAllLines(Path.Combine(shared, "result.txt"), results);
        Console.WriteLine(bad == 0 ? "✅ هر تغییر زیرِ پنج ثانیه روی کامپیوترِ دیگر" : $"❌ {bad} گام کند یا نرسیده");
        return bad == 0 ? 0 : 1;
    }

    // ── بکاپ: هر کارِ دکمه، با ساعت ──────────────────────────────────

    private static int RunBackupTiming(AppHost host, string dir)
    {
        var sw = Stopwatch.StartNew();
        var snap = host.Backup.SnapshotToday();
        Console.WriteLine($"[bk] 📸 عکسِ امروز: {sw.ElapsedMilliseconds:N0}ms ({snap is not null})");
        sw.Restart();
        var full = Path.Combine(dir, "full" + PumpYaqobi.Services.Data.FullBackup.Extension);
        var info = PumpYaqobi.Services.Data.FullBackup.Write(host.Backup, full, "3.1.999", "{}", "پمپ");
        Console.WriteLine($"[bk] 💾 فایلِ کامل: {sw.ElapsedMilliseconds:N0}ms · {new FileInfo(full).Length / 1_048_576.0:0.0} MB");
        sw.Restart();
        var sealedOk = BackupKeys.SealIfPossibleAsync(full).GetAwaiter().GetResult();
        Console.WriteLine($"[bk] 🔒 مُهر: {sw.ElapsedMilliseconds:N0}ms ({sealedOk})");
        sw.Restart();
        var ok = host.BackupToServer.RunOnceAsync(manual: true).GetAwaiter().GetResult();
        Console.WriteLine($"[bk] 📤 فرستادن به سرور: {sw.ElapsedMilliseconds:N0}ms · ok={ok} · خانگی={host.BackupToServer.LastHomeOk} «{host.BackupToServer.LastHomeWhy}» · حساب={host.BackupToServer.LastCloudOk} «{host.BackupToServer.LastCloudWhy}»");
        //  و برگشت: همان بکاپ از سرور ⇒ باز ⇒ دفترِ همین برنامه با همان رکوردها
        {
            var f = AppSettings.Load();
            var cloud = new CloudLink(f, () => Task.CompletedTask);
            var (lok, items, _, why) = cloud.BackupListAsync().GetAwaiter().GetResult();
            var top = items.FirstOrDefault();
            Console.WriteLine($"[bk] ☁️ روی سرور: {items.Count} بکاپ · تازه‌ترین {(top is null ? 0 : top.Bytes / 1_048_576.0):0.0} MB «{top?.Name}» {why}");
            if (top is not null)
            {
                var got = Path.Combine(dir, "back.bin");
                var dl = cloud.BackupDownloadAsync(top.Id, got).GetAwaiter().GetResult();
                var seal = BackupKeys.OpenForRestoreAsync(got, dir).GetAwaiter().GetResult();
                var plainPath = seal.Ok ? seal.Path : got;
                var plain = PumpYaqobi.Services.Data.BackupService.ExpandIfGzip(plainPath, dir);
                var rec = plain is null ? -1 : PumpYaqobi.Services.Data.BackupService.Inspect(plain);
                Console.WriteLine($"[bk] ⤵️ برگشت: دانلود={dl.Ok} مُهر={seal.Ok} فشرده‌باز={plain is not null} رکورد={rec:N0} (دفترِ زنده {PumpYaqobi.Services.Data.BackupService.Inspect(host.Db.DbPath):N0})");
            }
        }
        sw.Restart();
        var p = host.WriteSyncBackup("manual");
        Console.WriteLine($"[bk] 🔐 پشتیبانِ رمزشده: {sw.ElapsedMilliseconds:N0}ms ({p is not null})");
        return 0;
    }

    // ── کامپیوترِ ب ────────────────────────────────────────────────────

    private static int RunB(AppHost host, string shared)
    {
        var stepFile = Path.Combine(shared, "step.txt");
        var done = new HashSet<string>();
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < TimeSpan.FromMinutes(12) && !File.Exists(Path.Combine(shared, "done.txt")))
        {
            if (File.Exists(stepFile))
            {
                var step = File.ReadAllText(stepFile).Split('|')[0];
                if (!done.Contains(step) && Seen(host.Db.DbPath, step))
                {
                    done.Add(step);
                    File.WriteAllText(Path.Combine(shared, "seen-" + step + ".txt"), DateTime.UtcNow.Ticks.ToString());
                    Console.WriteLine($"[b] دید: {step}");
                }
            }
            Thread.Sleep(100);
        }
        return 0;
    }

    private static bool Seen(string db, string step)
    {
        using var c = new SqliteConnection("Data Source=" + db + ";Mode=ReadOnly;Pooling=False");
        c.Open();
        long N(string sql) { using var q = c.CreateCommand(); q.CommandText = sql; return Convert.ToInt64(q.ExecuteScalar()); }
        return step switch
        {
            "add-debtor" => N("SELECT COUNT(*) FROM Debtors WHERE DeletedAt IS NULL AND Name='قرض‌دارِ زنده'") > 0,
            "add-row" => N("SELECT COUNT(*) FROM DebtRows WHERE DeletedAt IS NULL AND Name='ردیفِ زنده'") > 0,
            "rasid" => N("SELECT COUNT(*) FROM DebtRows WHERE DeletedAt IS NULL AND Name='رسیدِ زنده'") > 0,
            "rename" => N("SELECT COUNT(*) FROM Debtors WHERE DeletedAt IS NULL AND Name='قرض‌دارِ زندهٔ تازه‌نام'") > 0,
            "add-sub" => N("SELECT COUNT(*) FROM DebtAccounts WHERE DeletedAt IS NULL AND Name='فرعیِ زنده'") > 0,
            "del-row" => N("SELECT COUNT(*) FROM DebtRows WHERE DeletedAt IS NULL AND Name='ردیفِ زنده'") == 0,
            "del-sub" => N("SELECT COUNT(*) FROM DebtAccounts WHERE DeletedAt IS NULL AND Name='فرعیِ زنده'") == 0,
            "del-debtor" => N("SELECT COUNT(*) FROM Debtors WHERE DeletedAt IS NULL AND Name LIKE 'قرض‌دارِ زنده%'") == 0,
            _ => false,
        };
    }

    private static void Register(string email)
    {
        var sentAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        Post("/api/auth/register/start", new { name = "صاحبِ زنده", email, password = Pass, passwordConfirm = Pass, app = "pump" }, null);
        var code = CodeFor(email, sentAt);
        var v = Post("/api/auth/register/verify", new { email, code, app = "pump" }, null);
        var ticket = v.GetProperty("ticket").GetString()!;
        var ver = v.TryGetProperty("terms", out var t) && t.TryGetProperty("version", out var tv) ? tv.GetString() : "";
        var done = Post("/api/auth/register/complete", new
        {
            ticket, name = "صاحبِ زنده", password = Pass,
            terms = new { accepted = true, version = ver },
            device = new { uid = "live-reg", name = "reg", platform = "windows" }, app = "pump",
        }, null);
        Post("/api/pump", new { name = "پمپِ زنده" }, done.GetProperty("accessToken").GetString());
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
