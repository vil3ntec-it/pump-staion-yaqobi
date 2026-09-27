using PumpYaqobi.App.Services;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ «تیل امانت» در حالِ ساخت و «دوربین‌ها» پنهان (۱۴۰۵/۰۷/۱۵) ═══════════
/// شرح در ‎SectionGate‎. ⚠️ این کلاس ‎TestOpenAll‎ را دست نمی‌زند (سراسری است و
/// کلاس‌ها موازی می‌دوند)؛ فقط تابع‌های خالص و خودِ سورس را می‌سنجد.
/// </summary>
public class SectionGateTests
{
    private static string Src(string rel) =>
        File.ReadAllText(Path.Combine(RepoRoot(), "native", "PumpYaqobi.App", rel));

    private static string RepoRoot()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null && !File.Exists(Path.Combine(d.FullName, "CLAUDE.md"))) d = d.Parent;
        return d!.FullName;
    }

    [Fact]
    public void AmanatDarHaleSakht_VaDoorbinhaPenhan()
    {
        Assert.Contains("amanat", SectionGate.Building);
        Assert.Contains("cameras", SectionGate.Hidden);
        Assert.DoesNotContain("amanat", SectionGate.Hidden);   // در نوار هست؛ فقط باز نمی‌شود
    }

    [Fact]
    public void SeZadanePoshteSarHam_RamzMikhahad_VaFaseleyeZiad_AzNo()
    {
        var t = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        SectionGate.ResetTaps();
        Assert.Equal(1, SectionGate.Tap(t));
        Assert.Equal(2, SectionGate.Tap(t.AddSeconds(1)));
        Assert.Equal(3, SectionGate.Tap(t.AddSeconds(2)));
        Assert.Equal(3, SectionGate.TapsForPin);
        // فاصلهٔ بلند ⇒ شمارش از نو
        Assert.Equal(1, SectionGate.Tap(t.AddSeconds(30)));
        SectionGate.ResetTaps();
    }

    [Fact]
    public void RamzeTose_6008_Ast_BaRaghameFarsiHam_VaRamzeKhamDarKodNist()
    {
        var now = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        SectionGate.ResetForTests();
        Assert.False(SectionGate.TryDevUnlock("1234", now));
        Assert.True(SectionGate.TryDevUnlock("6008", now));
        SectionGate.ResetForTests();
        Assert.True(SectionGate.TryDevUnlock("۶۰۰۸", now));
        SectionGate.ResetForTests();

        var src = Src("Services/SectionGate.cs");
        Assert.DoesNotContain("\"6008\"", src);
        Assert.Contains("pbkdf2$sha256$210000$", src);
    }

    [Fact]
    public void PanjRamzeGhalat_TormozDarad()
    {
        var now = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        SectionGate.ResetForTests();
        for (var i = 0; i < 5; i++) Assert.False(SectionGate.TryDevUnlock("0000", now));
        Assert.True(SectionGate.WaitSeconds(now) > 0);
        Assert.False(SectionGate.TryDevUnlock("6008", now));          // حتی درست، تا ترمز تمام شود
        Assert.True(SectionGate.TryDevUnlock("6008", now.AddMinutes(2)));
        SectionGate.ResetForTests();
    }

    [Fact]
    public void Navbar_VaGoAsync_VaSafheyeAghaz_AzDarGozar_Migozarand()
    {
        var vm = Src("ViewModels/MainViewModel.cs");
        Assert.Contains("!SectionGate.IsHidden(s.Id)", vm);                              // نوار
        Assert.Contains("if (SectionGate.IsHidden(s.Id)) return;", vm);                   // GoAsync
        Assert.Contains("SectionGate.IsBuilding(s.Id) && !await BuildingTapAsync(s)", vm);
        Assert.Contains("SectionGate.IsHidden(x.Id) || SectionGate.IsBuilding(x.Id)", vm); // صفحهٔ آغاز
        // ⛔ خودِ بخش‌ها از برنامه برداشته نشده‌اند
        Assert.Contains("new AmanatSectionViewModel(", vm);
        Assert.Contains("new CameraSectionViewModel(", vm);
    }
}
