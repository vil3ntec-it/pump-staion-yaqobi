namespace PumpYaqobi.Services.Data;

/// <summary>
/// ══ جای همهٔ اطلاعاتِ برنامه — داخلِ پوشهٔ خودِ برنامه (۱۴۰۵/۰۷/۱۵) ═══════
///
/// خواستهٔ صریحِ صاحب ریپو: «کسایی که بدون اینترنت کار می‌کنن… هرگز توی
/// درایو سی نره هیچ اطلاعاتی؛ همه باید توی همون فولدرِ اپ بیان و یک پوشه
/// توی فولدرِ برنامه برای همین‌ها باشه. الان اگه ویندوز رو عوض کرد هرچی تو
/// درایو سی بود همه می‌رن.»
///
/// <code>
/// &lt;پوشهٔ برنامه&gt;\data\   pump.db · settings.json · backups\ · accounts\ · chat.db …
/// </code>
///
/// ⛔ <b>هیچ چیزی پاک نمی‌شود</b>: جای کهنه (‎%AppData%\PumpYaqobi‎) فقط
/// <b>کپی</b> می‌شود و سرِ جایش دست‌نخورده می‌ماند، با یک یادداشت که
/// اطلاعات کجا رفت. کپی اول در پوشهٔ موقت، بعد سنجشِ اندازهٔ تک‌تکِ فایل‌ها،
/// و تنها بعد جابه‌جا — پس کپیِ نیمه هرگز «اطلاعاتِ برنامه» خوانده نمی‌شود.
///
/// ⚠️ <b>فقط خودِ برنامهٔ نصب‌شده روی ویندوز</b> (<c>PumpYaqobi.exe</c>):
/// آزمون‌ها و سنجه‌ها با <c>testhost</c>/<c>PumpYaqobi.UiTests</c> بالا
/// می‌آیند و جای خودشان را دارند؛ لینوکس هم همان جای همیشگی.
///
/// ⚠️ <b>پوشهٔ برنامه نانوشتنی</b> (مثلاً Program Files بی اجازه) ⇒ همان جای
/// کهنه، نه شکستن. نصاب پوشهٔ <c>data</c> را با اجازهٔ نوشتن می‌سازد تا این
/// حالت پیش نیاید.
///
/// ⚠️ <b>نصب در پوشهٔ دیگر</b>: جای آخر در
/// <c>HKCU\Software\PumpYaqobi\DataDir</c> نوشته می‌شود، پس نصبِ تازه در
/// پوشهٔ دیگر اطلاعاتِ پوشهٔ قبلی را کپی می‌کند، نه این‌که خالی بالا بیاید.
/// </summary>
public static class DataHome
{
    public const string SubDir = "data";
    public const string MovedNote = "اطلاعات-این-پوشه-منتقل-شد.txt";

    private static string? _root;
    private static readonly object Gate = new();

    /// <summary>جای کهنه — ‎%AppData%\PumpYaqobi‎.</summary>
    public static string Legacy => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PumpYaqobi");

    /// <summary>جای اطلاعات برای همین اجرا — یک بار حساب و نگه داشته می‌شود.</summary>
    public static string Root
    {
        get
        {
            if (_root is not null) return _root;
            lock (Gate) return _root ??= Resolve();
        }
    }

    /// <summary>آیا اطلاعات داخلِ پوشهٔ برنامه است؟ (برای نوشتهٔ صفحهٔ بکاپ)</summary>
    public static bool InsideApp =>
        string.Equals(Path.GetFullPath(Root).TrimEnd('\\', '/'),
                      Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, SubDir)).TrimEnd('\\', '/'),
                      StringComparison.OrdinalIgnoreCase);

    /// <summary>پیامِ جابه‌جاییِ همین اجرا (برای یک توست) — خالی یعنی جابه‌جایی نبود.</summary>
    public static string Moved { get; private set; } = "";

    /// <summary>پیامِ جابه‌جایی را یک بار بده و فراموش کن.</summary>
    public static string TakeMoved() { var m = Moved; Moved = ""; return m; }

    private static string Resolve()
    {
        try
        {
            if (!OperatingSystem.IsWindows() || !IsRealApp()) return Legacy;
            //  ⚠️ دو دوبار-کلیکِ پشتِ سرِ هم سرِ نخستین اجرا نباید دو کپیِ هم‌زمان بسازند
            using var m = new System.Threading.Mutex(false, "Local\\PumpYaqobi-DataHome");
            try { m.WaitOne(TimeSpan.FromSeconds(60)); } catch (System.Threading.AbandonedMutexException) { }
            Outcome r;
            try { r = Settle(Path.Combine(AppContext.BaseDirectory, SubDir), ReadRemembered(), Legacy); }
            finally { try { m.ReleaseMutex(); } catch { } }
            Moved = r.Moved;
            if (r.Root != Legacy) Remember(r.Root);
            return r.Root;
        }
        catch
        {
            return Legacy;   // هر خطایی ⇒ جای همیشگی، نه برنامه‌ای که بالا نیاید
        }
    }

    private static bool IsRealApp()
    {
        var exe = Environment.ProcessPath;
        return exe is not null &&
               string.Equals(Path.GetFileName(exe), "PumpYaqobi.exe", StringComparison.OrdinalIgnoreCase);
    }

    public sealed record Outcome(string Root, string Moved);

    /// <summary>
    /// تصمیم و کپی — بی وابستگی به ویندوز، پس آزمون‌پذیر.
    /// <paramref name="target"/> = ‎&lt;برنامه&gt;\data‎؛ منبع‌ها به ترتیب: جای
    /// آخرِ ثبت‌شده (نصبِ قبلی در پوشهٔ دیگر)، بعد جای کهنه.
    /// </summary>
    public static Outcome Settle(string target, string? remembered, string legacy)
    {
        if (!CanWrite(target)) return new(legacy, "");
        //  ⛔ شورا، ب۴: جابه‌جاییِ نیمه‌کاره (برق وسطِ کار رفت) «اطلاعات دارد» نیست —
        //  آن‌چه نشست کنار می‌رود (پاک نه) و از منبع دوباره کپی می‌شود.
        if (File.Exists(Path.Combine(target, Incomplete))) SetPartialAside(target);
        if (HasData(target)) return new(target, "");

        foreach (var src in new[] { remembered, legacy })
        {
            if (string.IsNullOrWhiteSpace(src) || Same(src, target) || !HasData(src)) continue;
            if (!CopyTree(src, target))
                return new(src, "");   // کپی نشد ⇒ همان جای قبلی، هیچ چیزی گم نمی‌شود
            try
            {
                File.WriteAllText(Path.Combine(src, MovedNote),
                    "اطلاعاتِ برنامهٔ پمپ بنزین به این پوشه کپی شد و از این به بعد آن‌جاست:\r\n"
                    + target + "\r\n\r\nاین پوشه فقط نسخهٔ کهنه است و برنامه دیگر آن را نمی‌خواند.\r\n");
            }
            catch { }
            return new(target, "اطلاعات به پوشهٔ برنامه آمد: " + target);
        }
        return new(target, "");
    }

    /// <summary>دفتر یا تنظیمات دارد؟</summary>
    public static bool HasData(string dir) =>
        File.Exists(Path.Combine(dir, "pump.db")) || File.Exists(Path.Combine(dir, "settings.json"));

    private static bool Same(string a, string b) =>
        string.Equals(Path.GetFullPath(a).TrimEnd('\\', '/'), Path.GetFullPath(b).TrimEnd('\\', '/'),
                      StringComparison.OrdinalIgnoreCase);

    private static bool CanWrite(string dir)
    {
        try
        {
            Directory.CreateDirectory(dir);
            var probe = Path.Combine(dir, ".write-" + Guid.NewGuid().ToString("N")[..8]);
            File.WriteAllText(probe, "");
            File.Delete(probe);
            return true;
        }
        catch { return false; }
    }

    /// <summary>
    /// کپیِ کامل در یک پوشهٔ موقتِ کنارِ مقصد، سنجشِ اندازهٔ هر فایل، و تنها
    /// بعد جابه‌جا. ⛔ دفتر و تنظیمات <b>آخر</b> جابه‌جا می‌شوند — پس اگر وسطِ
    /// کار برق رفت، مقصد هنوز «بی اطلاعات» است و بارِ بعد از نو کپی می‌شود.
    /// </summary>
    /// <summary>نشانِ «جابه‌جایی هنوز تمام نشده» — پیش از نخستین جابه‌جایی نوشته و پس از آخرین برداشته می‌شود.</summary>
    public const string Incomplete = ".copy-incomplete";

    /// <summary>فقط برای آزمون: پس از هر گامِ جابه‌جایی صدا زده می‌شود (رفتنِ برق را می‌سازد).</summary>
    public static Action<string>? StepHook;

    /// <summary>استثنایی که آزمون برای «برق رفت» می‌اندازد — پاک‌سازیِ پایانی را هم نمی‌کند.</summary>
    public sealed class PowerCut : Exception { }

    /// <summary>آن‌چه از جابه‌جاییِ نیمه‌کاره نشست، با نامِ تاریخ‌دار کنار می‌رود — هرگز پاک نمی‌شود.</summary>
    private static void SetPartialAside(string target)
    {
        var aside = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(target).TrimEnd('\\', '/'))!,
                                 ".data-partial-" + PumpYaqobi.Domain.AppClock.UtcNow.ToString("yyyyMMddHHmmss") + "-" + Guid.NewGuid().ToString("N")[..6]);
        Directory.CreateDirectory(aside);
        foreach (var f in Directory.GetFiles(target))
            if (!string.Equals(Path.GetFileName(f), Incomplete, StringComparison.Ordinal))
                File.Move(f, Path.Combine(aside, Path.GetFileName(f)));
        foreach (var d in Directory.GetDirectories(target))
            Directory.Move(d, Path.Combine(aside, Path.GetFileName(d)));
        File.Delete(Path.Combine(target, Incomplete));
    }

    public static bool CopyTree(string src, string target)
    {
        var cut = false;
        var parent = Path.GetDirectoryName(Path.GetFullPath(target).TrimEnd('\\', '/'))!;
        var stage = Path.Combine(parent, ".data-incoming-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            var srcFull = Path.GetFullPath(src);
            foreach (var f in Directory.EnumerateFiles(srcFull, "*", SearchOption.AllDirectories))
            {
                var rel = Path.GetRelativePath(srcFull, f);
                var to = Path.Combine(stage, rel);
                Directory.CreateDirectory(Path.GetDirectoryName(to)!);
                File.Copy(f, to, overwrite: false);
                if (new FileInfo(to).Length != new FileInfo(f).Length) return false;
            }

            Directory.CreateDirectory(target);
            File.WriteAllText(Path.Combine(target, Incomplete), src);
            StepHook?.Invoke("marker");
            //  اول همه جز دفتر و تنظیمات
            foreach (var d in Directory.GetDirectories(stage))
            {
                var to = Path.Combine(target, Path.GetFileName(d));
                if (!Directory.Exists(to)) Directory.Move(d, to);
                StepHook?.Invoke("dir:" + Path.GetFileName(d));
            }
            var last = new[] { "pump.db", "settings.json" };
            foreach (var f in Directory.GetFiles(stage))
            {
                if (last.Contains(Path.GetFileName(f), StringComparer.OrdinalIgnoreCase)) continue;
                var to = Path.Combine(target, Path.GetFileName(f));
                if (!File.Exists(to)) File.Move(f, to);
                StepHook?.Invoke("file:" + Path.GetFileName(f));
            }
            foreach (var name in last)
            {
                var f = Path.Combine(stage, name);
                if (File.Exists(f)) File.Move(f, Path.Combine(target, name));
                StepHook?.Invoke("last:" + name);
            }
            File.Delete(Path.Combine(target, Incomplete));
            return true;
        }
        catch (PowerCut) { cut = true; throw; }
        catch { return false; }
        finally
        {
            if (!cut) try { if (Directory.Exists(stage)) Directory.Delete(stage, true); } catch { }
        }
    }

    // ── جای آخر، برای نصب در پوشهٔ دیگر ────────────────────────────────────
    private const string RegKey = @"Software\PumpYaqobi";

    private static string? ReadRemembered()
    {
        if (!OperatingSystem.IsWindows()) return null;
        try
        {
            using var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RegKey);
            return k?.GetValue("DataDir") as string;
        }
        catch { return null; }
    }

    private static void Remember(string root)
    {
        if (!OperatingSystem.IsWindows()) return;
        try
        {
            using var k = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(RegKey);
            k.SetValue("DataDir", root);
        }
        catch { }
    }
}
