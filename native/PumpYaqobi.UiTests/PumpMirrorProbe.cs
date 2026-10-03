using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using PumpYaqobi.App.Services;
using PumpYaqobi.App.Update;

namespace PumpYaqobi.UiTests;

/// <summary>
/// ══ «آپدیت از سرورِ خودِ پمپ» — سرتاسری، با سرورِ خانگیِ واقعی (۱۴۰۵/۰۷/۱۹) ══
///
///     node test/pump-mirror-stack.mjs <live.json>          (ریپوی server)
///     dotnet run --project PumpYaqobi.UiTests -c Release -- pumpmirror <نشانیِ پورتِ عمومی> <نسخهٔ مورد انتظار>
///
/// همان <see cref="UpdateService"/>ِ خودِ برنامه — با نشانیِ واقعیِ
/// <c>CloudConfig</c> — فقط مقصدِ شبکه به پورتِ عمومیِ سرورِ محلی برده می‌شود
/// (‎https‎ همان‌جا ‎http‎ است). می‌سنجد: نسخه از سرور آمد · هیچ درخواستی جز
/// سرورِ پمپ نرفت · بسته واقعاً گرفته شد و با چک‌سامِ منتشرشده خورد.
/// ⚠️ در CI نیست: پشتهٔ بیرونی می‌خواهد.
/// </summary>
public static class PumpMirrorProbe
{
    public static int Run(string[] args)
    {
        if (args.Length < 3) { Console.WriteLine("pumpmirror <public-base> <version> [held]"); return 2; }
        var held = args.Length > 3 && args[3] == "held";
        return RunAsync(args[1].TrimEnd('/'), args[2], held).GetAwaiter().GetResult();
    }

    private static async Task<int> RunAsync(string local, string version, bool held)
    {
        int ok = 0, bad = 0;
        void Check(string name, bool pass, string extra = "")
        {
            if (pass) { ok++; Console.WriteLine("  ✅ " + name); }
            else { bad++; Console.WriteLine("  ❌ " + name + (extra.Length > 0 ? " — " + extra : "")); }
        }

        var dir = Path.Combine(Path.GetTempPath(), "pump-mirror-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);
        AppSettings.DirOverride = dir;

        var serverBase = CloudConfig.Url("");
        var asked = new System.Collections.Generic.List<string>();
        using var http = new HttpClient();
        UpdateService.TestTransport = async (req, ct) =>
        {
            var url = req.RequestUri!.ToString();
            asked.Add(url);
            if (!url.StartsWith(serverBase, StringComparison.Ordinal))
                throw new HttpRequestException("سنجه: فقط سرورِ پمپ مجاز است");
            var to = new HttpRequestMessage(req.Method, local + url[serverBase.Length..]);
            foreach (var h in req.Headers) to.Headers.TryAddWithoutValidation(h.Key, h.Value);
            var res = await http.SendAsync(to, HttpCompletionOption.ResponseHeadersRead, ct);
            res.RequestMessage = req;   // «جای نهایی» همان نشانیِ سرورِ پمپ است
            return res;
        };

        var svc = new UpdateService();
        var info = await svc.CheckAsync();
        if (held)
        {
            //  🚦 پخش در پنل خاموش است: نسخه روی سرور هست ولی به برنامه نمی‌رسد
            Check("پخش خاموش ⇒ برنامه نسخهٔ نگه‌داشته را نمی‌بیند", !info.Available && info.LatestVersion != version,
                  info.LatestVersion + " · " + info.StatusText);
            Check("خطا هم نیست (راهِ دیگری باز نمی‌شود)", !info.Failed, info.StatusText);
            Check("هیچ درخواستی جز سرورِ پمپ نرفت", asked.TrueForAll(u => u.StartsWith(serverBase, StringComparison.Ordinal)),
                  string.Join(" | ", asked));
            Console.WriteLine($"\n{ok} موفق، {bad} ناموفق");
            try { Directory.Delete(dir, true); } catch { }
            return bad == 0 ? 0 : 1;
        }
        Check("نسخهٔ تازه از سرورِ پمپ آمد", info.Available && info.LatestVersion == version,
              info.LatestVersion + " · " + info.StatusText);
        Check("بسته از همان سرور", info.DownloadUrl?.StartsWith(serverBase + "/api/pump-updates/files/") == true,
              info.DownloadUrl ?? "");

        var path = await svc.DownloadAsync(info, null);
        Check("بسته گرفته شد و با چک‌سامِ منتشرشده خورد", path is not null && File.Exists(path), svc.LastProblem);

        Check("هیچ درخواستی جز سرورِ پمپ نرفت", asked.TrueForAll(u => u.StartsWith(serverBase, StringComparison.Ordinal)),
              string.Join(" | ", asked));
        Console.WriteLine($"\n{ok} موفق، {bad} ناموفق");
        try { Directory.Delete(dir, true); } catch { }
        return bad == 0 ? 0 : 1;
    }
}
