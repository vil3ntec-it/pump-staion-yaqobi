using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ۱۴۰۵/۰۷/۱۸: در پارچه Enter هر کارتِ پر (روز، شب یا هر دو) را ذخیره می‌کند و
/// پارچهٔ جدید آماده می‌شود؛ Ctrl+Tab مثلِ Tab تیل را عوض می‌کند.
/// رفتارِ واقعی با کلید را سنجهٔ ‎keys17‎ می‌سنجد.
/// </summary>
public class ParchaEnterTests
{
    private static string Src(string rel) => File.ReadAllText(Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "../../../../PumpYaqobi.App", rel)));

    [Fact]
    public void Enter_HarKarteParRaZakhireMikonad_VaParcheyeJadid()
    {
        var view = Src("Views/Sections/ParchaSectionView.axaml.cs");
        var vm = Src("ViewModels/Sections/ParchaSectionViewModel.cs");
        Assert.Contains("vm.SaveFilledAndNewAsync()", view);
        Assert.Contains("Where(f => f.HasUnsavedInput)", vm);
        //  روز و شبِ یک Enter در همان پارچه
        Assert.Contains("SetForceNew(fuel, f.IsDay ? ShiftKind.Night : ShiftKind.Day, false);", vm);
        //  نشد ⇒ هیچ کارتی خالی نمی‌شود
        Assert.Contains("if (!await SaveShiftAsync(f)) return saved;", vm);
    }

    [Fact]
    public void CtrlTab_TilRaAvazMikonad()
    {
        var view = Src("Views/Sections/ParchaSectionView.axaml.cs");
        var i = view.IndexOf("e.Key == Key.Tab && e.KeyModifiers.HasFlag(KeyModifiers.Control)", StringComparison.Ordinal);
        Assert.True(i > 0);
        var body = view[i..view.IndexOf("return;", i, StringComparison.Ordinal)];
        Assert.Contains("ToggleFuelCommand", body);
        Assert.DoesNotContain("NightCard", view);
    }
    /// <summary>۱۴۰۵/۰۷/۱۸: شروعِ شب با ختمِ «تایپ‌شده»ی روزِ همان پایه سنجیده می‌شود. رفتار: ‎undokeys‎.</summary>
    [Fact]
    public void ShoruyeShab_BaKhatmeNeveshteShodeyeRuz_Sanjide_Mishavad()
    {
        var vm = Src("ViewModels/Sections/ParchaSectionViewModel.cs");
        Assert.Contains("if (DayEndFor(form) is decimal typed) { Apply(form, start, typed, num); return; }", vm);
        Assert.Contains("Apply(form, form.StartValue, DayEndFor(form) ?? v ?? 0m, key.Num);", vm);
        Assert.Contains("if (!form.IsNight) return null;", vm);
        //  ختم یا شمارهٔ پایهٔ روز عوض شد ⇒ شب از نو
        Assert.Contains("if (!_loading) _owner.RecheckSibling(this);", vm);
        Assert.Contains("_owner.RecheckSibling(this);", vm[vm.IndexOf("partial void OnPumpNumChanged", StringComparison.Ordinal)..]);
    }
}
