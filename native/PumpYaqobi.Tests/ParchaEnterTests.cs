using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ۱۴۰۵/۰۷/۱۸: در پارچه Enter هر کارتِ پر (روز، شب یا هر دو) را ذخیره می‌کند و
/// پارچهٔ جدید آماده می‌شود؛ Ctrl+Tab مثلِ Tab تیل را عوض می‌کند.
/// رفتارِ واقعی با کلید را سنجهٔ ‎keys17‎ می‌سنجد.
/// </summary>
public class ParchaEnterTests
{
    private static string Src(string rel) => SrcText.Read(Path.GetFullPath(
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

    /// <summary>
    /// ⛔ (۱۴۰۵/۰۷/۲۴، نسخه 3.1.272) ترتیبِ کادرهای فرمِ پارچه به خواستهٔ صاحب ریپو: نام · شروع · ختم
    /// سرِ جایشان، بعد ۴ قرض · ۵ فی لیتر · ۶ فروش لیتر خودکار · ۷ تیل آزمایشی، بعد بقیه.
    /// ناوبریِ Enter و جهت‌نما با جای دیدنی است، پس همین ترتیبِ فایل ترتیبِ کلید هم هست.
    /// </summary>
    [Fact]
    public void TartibeKadrhayeParcha_KhasteyeSahebRepo()
    {
        var x = Src("Views/Sections/ParchaSectionView.axaml");
        string[] order =
        {
            "Text=\"نام کارمند\"", "Text=\"شروع پایه (لیتر)\"", "Text=\"ختم پایه (لیتر)\"",
            "Text=\"جمله قرض (افغانی)\"", "Text=\"فی لیتر (افغانی)\"", "Text=\"فروش (لیتر) — خودکار\"",
            "Text=\"لیتر آزمایشی", "Text=\"فایده فی لیتر (افغانی)",
        };
        var at = order.Select(o => x.IndexOf(o, StringComparison.Ordinal)).ToList();
        Assert.All(at, i => Assert.True(i > 0));
        for (var k = 1; k < at.Count; k++)
            Assert.True(at[k - 1] < at[k], $"«{order[k - 1]}» باید پیش از «{order[k]}» باشد");
    }
}
