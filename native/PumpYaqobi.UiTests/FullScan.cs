using System.Diagnostics;
using System.Text;

namespace PumpYaqobi.UiTests;

/// <summary>
/// ══ «بعد از هر آپدیت، همهٔ برنامه از اول تا آخر اسکن شود» ═══════════════════
///
/// خواستهٔ صریحِ صاحب ریپو (۱۴۰۵/۰۶/۳۰): «بعد هر اپدیت همه برنامه از اول تا
/// اخر اسکن بشه که کند که نشده و لگ و باگی نگرفته باشه.»
///
///     dotnet run --project PumpYaqobi.UiTests -c Release -- fullscan
///     dotnet run --project PumpYaqobi.UiTests -c Release -- fullscan quick
///
/// یک دستور، یک جدولِ آخر: هر سنجه سبز یا سرخ، با وقتش. کدِ بازگشت صفر است
/// فقط اگر همه سبز باشند — پس در CI هم همین یک خط بس است.
///
/// ⚠️ **هر سنجه در یک فرآیندِ جدا** اجرا می‌شود و این عمدی است، نه تنبلی:
/// آوالونیا در هر فرآیند یک بار راه‌اندازی می‌شود (`SetupWithoutStarting`) و
/// چند سنجه در یک فرآیند یعنی «Avalonia is already initialized» یا بدتر،
/// عددهایی که از حافظه و کَشِ سنجهٔ قبلی آلوده‌اند. جدا بودن، وقتِ هر سنجه را
/// هم راست می‌کند.
///
/// ⚠️ سنجه‌هایی که اینترنت یا سرورِ واقعی می‌خواهند این‌جا نیستند
/// (`cloudlogin` پشتِ `PUMP_VERIFY_CLOUD`، و `themeflip` که روی رانرِ گیت‌هاب
/// سنجهٔ پیکسلی‌اش قرمز می‌شود — همان کارِ نیمه‌تمامِ CLAUDE.md).
/// </summary>
internal static class FullScan
{
    /// <summary>ترتیب از «باز شدنِ برنامه» تا «رفتارِ کاربر» — همان از اول تا آخر.</summary>
    private static readonly (string Mode, string Title, bool Quick)[] Probes =
    {
        ("startup",    "باز شدنِ برنامه و بی‌کاریِ پس از آن", true),
        ("idle",       "بخشِ پنهان: صفرِ مطلق",               true),
        ("warm",       "پردهٔ لودینگ و صفحهٔ رمز",             true),
        ("look",       "سه پلهٔ رنگ (بوم ← بدنه ← کادر)",      true),
        ("ledgerperf", "دفترها (گاوصندوق، صرافی، مصارف)",      true),
        ("enterperf",  "داخل و بیرونِ حساب",                   true),
        ("scrollperf", "گامِ چرخِ ماوس",                        true),
        ("scrollend",  "ته اسکرول می‌ایستد",                   true),
        ("bigtable",   "جدولِ بزرگ: ردیفِ زنده و درستی",        false),
        ("gridperf",   "یک میلیون ردیف",                       false),
        ("cardperf",   "کارت‌های قرض‌داران",                   true),
        ("waraqperf",  "ورق و پارچه",                          true),
        ("dashperf",   "داشبورد",                              true),
        ("printperf",  "پنجرهٔ چاپ",                            true),
        ("years",      "پنج سال داده — همهٔ بخش‌ها",            false),
        ("verify",     "رفتارِ کاربر (شانزده بند)",             true),
        ("inputchars", "میانبر نویسه را نخورد",                 true),
        ("loginart",   "نقشهٔ صفحهٔ ورود",                      true),
        ("serverdot",  "چراغِ سرور با سرورِ محلی",              true),
    };

    public static int Run(bool quick)
    {
        var exe = Environment.ProcessPath;
        if (string.IsNullOrEmpty(exe))
        {
            Console.WriteLine("فرآیندِ خودم را پیدا نکردم — `fullscan` فقط از فایلِ ساخته‌شده اجرا می‌شود.");
            return 1;
        }

        var list = Probes.Where(p => !quick || p.Quick).ToArray();
        Console.WriteLine($"══ اسکنِ کاملِ برنامه — {list.Length} سنجه{(quick ? " (تند)" : "")} ══");
        Console.WriteLine();

        var rows = new List<(string Mode, string Title, bool Ok, long Ms, string Note)>();
        foreach (var (mode, title, _) in list)
        {
            Console.Write($"… {mode,-11} {title}");
            var sw = Stopwatch.StartNew();
            var (code, tail) = RunChild(exe!, mode);
            sw.Stop();
            var ok = code == 0;
            rows.Add((mode, title, ok, sw.ElapsedMilliseconds, tail));
            Console.WriteLine($"   {(ok ? "✔" : "✖")}  {sw.ElapsedMilliseconds:n0}ms");
        }

        Console.WriteLine();
        Console.WriteLine("══ جمع‌بندی ═══════════════════════════════════════════════════");
        foreach (var r in rows)
            Console.WriteLine($"{(r.Ok ? "✔" : "✖")} {r.Mode,-11} {r.Ms,9:n0}ms  {r.Title}");

        var bad = rows.Where(r => !r.Ok).ToArray();
        Console.WriteLine();
        if (bad.Length == 0)
        {
            Console.WriteLine($"همهٔ {rows.Count} سنجه سبز — نه کندی، نه لگ، نه باگِ رفتاری.");
            return 0;
        }

        Console.WriteLine($"⛔ {bad.Length} سنجه قرمز است:");
        foreach (var r in bad)
        {
            Console.WriteLine($"── {r.Mode} ──");
            Console.WriteLine(r.Note);
        }
        Console.WriteLine("⚠️ سنجه را ضعیف نکنید تا سبز شود — ریشه را درست کنید.");
        return 1;
    }

    /// <summary>خروجیِ فرزند را کامل نگه می‌دارد ولی فقط تهِ آن را گزارش می‌کند.</summary>
    private static (int Code, string Tail) RunChild(string exe, string mode)
    {
        var psi = new ProcessStartInfo(exe, mode)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        var lines = new List<string>();
        using var p = Process.Start(psi);
        if (p is null) return (1, "فرآیند اجرا نشد.");
        p.OutputDataReceived += (_, e) => { if (e.Data is not null) lock (lines) lines.Add(e.Data); };
        p.ErrorDataReceived += (_, e) => { if (e.Data is not null) lock (lines) lines.Add(e.Data); };
        p.BeginOutputReadLine();
        p.BeginErrorReadLine();
        // سقفِ وقت: هیچ سنجه‌ای بیش از ده دقیقه طول نمی‌کشد؛ بیشتر یعنی گیر کرده.
        if (!p.WaitForExit(TimeSpan.FromMinutes(10)))
        {
            try { p.Kill(entireProcessTree: true); } catch { }
            return (1, "بیش از ده دقیقه طول کشید — گیر کرده.");
        }
        p.WaitForExit();

        var sb = new StringBuilder();
        lock (lines)
            foreach (var l in lines.Where(Interesting).TakeLast(20))
                sb.AppendLine(l);
        if (sb.Length == 0)
            lock (lines)
                foreach (var l in lines.TakeLast(12)) sb.AppendLine(l);
        return (p.ExitCode, sb.ToString().TrimEnd());
    }

    /// <summary>خطی که خودِ سنجه «ایراد» می‌داند — نه همهٔ عددها.</summary>
    private static bool Interesting(string line) =>
        line.Contains('✖') || line.Contains("⛔") || line.Contains("ایراد")
        || line.Contains("شکست") || line.Contains("Unhandled") || line.Contains("Exception");
}
