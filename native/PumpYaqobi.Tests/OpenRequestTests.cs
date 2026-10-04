using PumpYaqobi.App.Services;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ دوبار-کلیک روی ‎.pumpyaqobi‎ و ‎.pumpkey‎ (۱۴۰۵/۰۷/۱۵) ═══════════════════
/// «چرا فایلِ برنامه رو که گرفتم و می‌خوام باز کنم، برنامهٔ من پیشنهاد نمی‌شه؟»
/// </summary>
public class OpenRequestTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "pyq-open-" + Guid.NewGuid().ToString("N")[..8]);

    public OpenRequestTests() => Directory.CreateDirectory(_dir);
    public void Dispose() { try { Directory.Delete(_dir, true); } catch { } }

    private string Make(string name) { var p = Path.Combine(_dir, name); File.WriteAllText(p, "x"); return p; }

    [Fact]
    public void FaghatFileyeKhodeman_Ke_Hast()
    {
        var full = Make("محمد-هارون-1405-07-05.pumpyaqobi");
        var key = Make("code.PUMPKEY");
        var other = Make("note.txt");

        Assert.Equal(full, OpenRequest.FromArgs(new[] { full }));
        Assert.Equal(key, OpenRequest.FromArgs(new[] { "/something", key }));
        Assert.Null(OpenRequest.FromArgs(new[] { other }));
        Assert.Null(OpenRequest.FromArgs(new[] { Path.Combine(_dir, "gone.pumpyaqobi") }));
        Assert.Null(OpenRequest.FromArgs(null));
        Assert.Null(OpenRequest.FromArgs(Array.Empty<string>()));
    }

    [Fact]
    public void Nobat_BeTartib()
    {
        while (OpenRequest.Take() is not null) { }
        OpenRequest.Add("a");
        OpenRequest.Add("b");
        Assert.Equal("a", OpenRequest.Take());
        Assert.Equal("b", OpenRequest.Take());
        Assert.Null(OpenRequest.Take());
    }

    /// <summary>
    /// ⛔ همان درِ دکمه‌ها — هیچ راهِ کوتاه‌تری: فایلِ کامل از
    /// ‎ImportFullFromAsync‎ (سنجش ⇒ خلاصه ⇒ پرسش) و کد از ‎ApplyKeyFileAsync‎.
    /// </summary>
    [Fact]
    public void AzHamanDareDokmeha()
    {
        string Src(params string[] p) => SrcText.Read(Path.Combine(
            new[] { AppContext.BaseDirectory, "..", "..", "..", "..", "PumpYaqobi.App" }.Concat(p).ToArray()));
        var main = Src("ViewModels", "MainViewModel.cs");
        Assert.Contains("await b.ImportFullFromAsync(path)", main);
        Assert.Contains("await v.ApplyKeyFileAsync(path)", main);
        Assert.Contains("if (Phase != AppPhase.Ready || _opening) return;", main);

        var backup = Src("ViewModels", "Sections", "BackupSectionViewModel.cs");
        var i = backup.IndexOf("public async Task ImportFullFromAsync(string path)", StringComparison.Ordinal);
        Assert.True(i > 0);
        var body = backup[i..backup.IndexOf("public static string FullSummary", i, StringComparison.Ordinal)];
        Assert.Contains("FullBackup.Read(path", body);
        Assert.Contains("Dialogs.ConfirmAsync(", body);

        var program = Src("Program.cs");
        Assert.Contains("OpenRequest.Hand(opened)", program);
        Assert.True(program.IndexOf("OpenRequest.Hand(opened)", StringComparison.Ordinal)
                    < program.IndexOf("SingleInstance.WakeOther()", StringComparison.Ordinal),
                    "اول فایل، بعد بیدار کردن");
    }
}
