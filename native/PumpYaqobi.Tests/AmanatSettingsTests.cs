using PumpYaqobi.App.ViewModels.Sections;
using PumpYaqobi.Application.Services;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ ⚙️ تنظیمات مدیرِ تیل امانت ═══════════════════════════════════════════
/// سایت (‎#am-settings‎) نُه ضریب را می‌دهد که همان لحظه ذخیره می‌شوند؛
/// برنامه تا امروز فقط می‌خواندشان. این آزمون‌ها همان رفتار را قفل می‌کنند.
/// </summary>
public class AmanatSettingsTests
{
    private static (AmanatSettingsEditorViewModel Vm, List<AmanatSettings> Saved, List<int> Refreshed) Make()
    {
        var saved = new List<AmanatSettings>();
        var refreshed = new List<int>();
        var vm = new AmanatSettingsEditorViewModel(new AmanatService(), s => saved.Add(s),
                                                   () => { refreshed.Add(1); return Task.CompletedTask; });
        return (vm, saved, refreshed);
    }

    [Fact] // ‎fillAmanatSettings‎ — پر کردنِ خانه‌ها چیزی ذخیره نمی‌کند
    public void Fill_DoesNotSave()
    {
        var (vm, saved, refreshed) = Make();
        vm.Fill(new AmanatSettings(BasePct: 0.05m, TankFactor: 0.5m));
        Assert.Empty(saved);
        Assert.Empty(refreshed);
        Assert.Equal("0.05", vm.BasePct);
        Assert.Equal("0.5", vm.TankFactor);
    }

    [Fact] // ‎setAmanatSetting‎ — هر تغییرِ خانه همان لحظه ذخیره و حساب می‌شود
    public void ChangingABox_SavesAndRefreshes()
    {
        var (vm, saved, refreshed) = Make();
        vm.FDiesel = "0.5";
        var s = Assert.Single(saved);
        Assert.Equal(0.5m, s.FDiesel);
        Assert.Equal(AmanatSettings.Default.FPetrol, s.FPetrol);   // بقیه دست‌نخورده
        Assert.Single(refreshed);
        Assert.Equal(s, vm.Current);
    }

    [Fact] // ‎_amNum‎ — رقمِ فارسی پذیرفته می‌شود؛ خراب یا خالی ⇒ پیش‌فرضِ همان ضریب
    public void PersianDigitsAreAccepted_AndGarbageFallsBackToTheDefault()
    {
        Assert.Equal(12.5m, AmanatSettingsEditorViewModel.Num("۱۲٫۵", 0m));
        Assert.Equal(0.02m, AmanatSettingsEditorViewModel.Num("abc", 0.02m));
        Assert.Equal(7m, AmanatSettingsEditorViewModel.Num("", 7m));

        var (vm, saved, _) = Make();
        vm.RefTemp = "۲۵";
        Assert.Equal(25m, saved.Last().RefTemp);
        vm.RefTemp = "؟";
        Assert.Equal(AmanatSettings.Default.RefTemp, saved.Last().RefTemp);
    }

    [Fact] // همان مقدار دوباره ⇒ ذخیرهٔ دوباره نه
    public void SameValueAgain_DoesNotSaveTwice()
    {
        var (vm, saved, _) = Make();
        vm.SafetyPct = "30";
        vm.SafetyPct = "30.0";
        Assert.Single(saved);
    }

    [Fact] // ‎resetAmanatSettings‎ — «برگشت به ضریب‌های منبع» پیش‌فرض را صریح ذخیره می‌کند
    public async Task Reset_SavesTheDefaultsExplicitly()
    {
        var (vm, saved, refreshed) = Make();
        vm.TDouble = "3";
        await vm.ResetCommand.ExecuteAsync(null);
        Assert.Equal(AmanatSettings.Default, saved.Last());
        Assert.Equal(AmanatSettings.Default, vm.Current);
        Assert.Equal("10", vm.TDouble);
        Assert.Equal(2, refreshed.Count);
    }

    [Fact] // جدولِ «تبخیر در هر گرما» — ده دما، از خودِ ‎AmanatService‎، و ردیفِ دمای پیش‌فرض پررنگ
    public void TemperatureTable_ComesFromTheServiceAndMarksTheDefaultTemp()
    {
        var (vm, _, _) = Make();
        Assert.Equal(10, vm.TempRows.Count);
        var near = Assert.Single(vm.TempRows, r => r.IsNear);
        Assert.Equal("25°C", near.TempText);
        // در دمای مرجع ضریب ۱× است و تبخیرِ ماهانه = پایه × ضریبِ پطرول × ضریبِ مخزن
        var atRef = vm.TempRows.Single(r => r.TempText == "20°C");
        Assert.Equal("1×", atRef.FactorText);
        Assert.Equal("0.02٪", atRef.MonthText);
        Assert.Contains("ضریب گرما = ۲ ^", vm.FormulaText);
    }

    [Fact] // دکمه‌اش در خودِ بخش است، نه در تنظیماتِ برنامه
    public void TheSectionHasTheAdminSettingsButton()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        var view = File.ReadAllText(Path.Combine(root, "PumpYaqobi.App", "Views", "Sections", "AmanatSectionView.axaml"));
        Assert.Contains("⚙️ تنظیمات مدیر", view);
        Assert.Contains("ToggleSettingsCommand", view);
        Assert.Contains("برگشت به ضریب‌های منبع", view);
    }
}
