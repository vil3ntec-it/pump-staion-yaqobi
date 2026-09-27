using PumpYaqobi.Services.Data;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ جای اطلاعات: داخلِ پوشهٔ برنامه، و هیچ چیزی پاک نمی‌شود (۱۴۰۵/۰۷/۱۵) ══
///
/// خواستهٔ صاحب ریپو: «هرگز توی درایو سی نره هیچ اطلاعاتی؛ همه باید توی همون
/// فولدرِ اپ بیان… اگه ویندوز رو عوض کرد هرچی که تو درایو سی بود همه می‌رن.»
/// این‌جا خودِ کپی با فایل‌های واقعی روی دیسک سنجیده می‌شود.
/// </summary>
public class DataHomeTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "pyq-datahome-" + Guid.NewGuid().ToString("N")[..8]);

    public void Dispose() { try { Directory.Delete(_root, true); } catch { } }

    private string Dir(string name) { var d = Path.Combine(_root, name); Directory.CreateDirectory(d); return d; }

    private static void Put(string dir, string rel, string text)
    {
        var p = Path.Combine(dir, rel);
        Directory.CreateDirectory(Path.GetDirectoryName(p)!);
        File.WriteAllText(p, text);
    }

    [Fact]
    public void JayeKohne_KopiMishavad_Va_DastNakhorde_Mimanad()
    {
        var legacy = Dir("appdata");
        Put(legacy, "pump.db", "ledger");
        Put(legacy, "settings.json", "{}");
        Put(legacy, Path.Combine("backups", "pump-1405-07-05.db"), "b1");
        Put(legacy, Path.Combine("accounts", "usr_1", "pump.db"), "acct");
        var target = Path.Combine(_root, "app", DataHome.SubDir);

        var r = DataHome.Settle(target, null, legacy);

        Assert.Equal(target, r.Root);
        Assert.NotEqual("", r.Moved);
        Assert.Equal("ledger", File.ReadAllText(Path.Combine(target, "pump.db")));
        Assert.Equal("b1", File.ReadAllText(Path.Combine(target, "backups", "pump-1405-07-05.db")));
        Assert.Equal("acct", File.ReadAllText(Path.Combine(target, "accounts", "usr_1", "pump.db")));
        //  ⛔ جای کهنه پاک نمی‌شود — فقط یک یادداشت می‌گیرد
        Assert.Equal("ledger", File.ReadAllText(Path.Combine(legacy, "pump.db")));
        Assert.True(File.Exists(Path.Combine(legacy, "backups", "pump-1405-07-05.db")));
        Assert.True(File.Exists(Path.Combine(legacy, DataHome.MovedNote)));
        //  و هیچ پوشهٔ موقتی نمی‌ماند
        Assert.Empty(Directory.GetDirectories(Path.Combine(_root, "app"), ".data-incoming-*"));
    }

    [Fact]
    public void PusheyeBarname_AzGhabl_Daftar_Darad_HichKopiNist()
    {
        var legacy = Dir("appdata");
        Put(legacy, "pump.db", "old");
        var target = Dir(Path.Combine("app", DataHome.SubDir));
        Put(target, "pump.db", "current");

        var r = DataHome.Settle(target, null, legacy);

        Assert.Equal(target, r.Root);
        Assert.Equal("", r.Moved);
        Assert.Equal("current", File.ReadAllText(Path.Combine(target, "pump.db")));
        Assert.False(File.Exists(Path.Combine(legacy, DataHome.MovedNote)));
    }

    /// <summary>نصب در پوشهٔ دیگر: جای آخرِ ثبت‌شده پیش از جای کهنه.</summary>
    [Fact]
    public void JayeAkhar_PishAzJayeKohne()
    {
        var legacy = Dir("appdata");
        Put(legacy, "pump.db", "very-old");
        var previous = Dir(Path.Combine("old-install", DataHome.SubDir));
        Put(previous, "pump.db", "previous-install");
        var target = Path.Combine(_root, "new-install", DataHome.SubDir);

        var r = DataHome.Settle(target, previous, legacy);

        Assert.Equal(target, r.Root);
        Assert.Equal("previous-install", File.ReadAllText(Path.Combine(target, "pump.db")));
        Assert.Equal("previous-install", File.ReadAllText(Path.Combine(previous, "pump.db")));
    }

    [Fact]
    public void HichDaftari_Nist_PusheyeBarname()
    {
        var target = Path.Combine(_root, "app", DataHome.SubDir);
        var r = DataHome.Settle(target, null, Path.Combine(_root, "nothing"));
        Assert.Equal(target, r.Root);
        Assert.True(Directory.Exists(target));
        Assert.Empty(Directory.GetFiles(target));   // کاوشِ نوشتنی بودن ردی نگذاشت
    }

    /// <summary>پوشهٔ برنامه نانوشتنی ⇒ همان جای کهنه، نه برنامه‌ای که بالا نیاید.</summary>
    [Fact]
    public void PusheyeNaneveshtani_JayeKohne()
    {
        var legacy = Dir("appdata");
        Put(legacy, "pump.db", "ledger");
        var blocker = Path.Combine(_root, "a-file");
        File.WriteAllText(blocker, "x");                       // «پوشهٔ برنامه» یک فایل است ⇒ ساختنی نیست
        var r = DataHome.Settle(Path.Combine(blocker, DataHome.SubDir), null, legacy);
        Assert.Equal(legacy, r.Root);
        Assert.False(File.Exists(Path.Combine(legacy, DataHome.MovedNote)));
    }

    /// <summary>
    /// ⛔ فقط خودِ برنامهٔ نصب‌شده جابه‌جا می‌شود — آزمون‌ها و سنجه‌ها هرگز کنارِ
    /// فایل‌های خودشان دفتر نمی‌سازند.
    /// </summary>
    [Fact]
    public void FaghatBarnameyeVaghei_RuyeWindows()
    {
        var src = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..",
                                                "PumpYaqobi.Services", "Data", "DataHome.cs"));
        Assert.Contains("!OperatingSystem.IsWindows() || !IsRealApp()", src);
        Assert.Contains("\"PumpYaqobi.exe\"", src);
        //  ⛔ هیچ پاک کردنی در جای کهنه
        var settle = src[src.IndexOf("public static Outcome Settle", StringComparison.Ordinal)..
                         src.IndexOf("public static bool HasData", StringComparison.Ordinal)];
        Assert.DoesNotContain("Delete", settle);
        Assert.DoesNotContain("Move(src", settle);
    }
}
