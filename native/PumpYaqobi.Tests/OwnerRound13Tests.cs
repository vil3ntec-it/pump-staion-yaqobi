using PumpYaqobi.App.ViewModels.Sections;
using PumpYaqobi.Application.Services;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ فهرستِ نُه‌تاییِ صاحب ریپو (۱۴۰۵/۰۷/۱۳) ════════════════════════════════
///
/// هر بند یک قاعده؛ رفتارِ پنجره را سنجه‌های ‎PumpYaqobi.UiTests‎ می‌سنجند.
/// </summary>
public class OwnerRound13Tests
{
    private static string Root()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null && !File.Exists(Path.Combine(d.FullName, "PumpYaqobi.sln")))
            d = d.Parent;
        Assert.NotNull(d);
        return d!.FullName;
    }

    private static string Read(params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { Root() }.Concat(parts).ToArray()));

    private static string Bare(string x) => System.Text.RegularExpressions.Regex.Replace(
        x, "<!--.*?-->", "", System.Text.RegularExpressions.RegexOptions.Singleline);

    // ══ ۱) مخزن: وزن به کیلو ═════════════════════════════════════════════

    [Fact]
    public void Makhzan_Vazn_BeKilo_Ast_Na_HezarBarabar()
    {
        //  عددهای خودِ صاحب ریپو: ۱۰۰۰ ⇒ ۱ تن، ۱۲۰۰ دالر، ۷۹٬۲۰۰ افغانی
        var kg = StorageSectionViewModel.KgOf("1000");
        Assert.Equal(1000m, kg);
        var n = new StorageService().Compute(kg, 0.7435m, 1200m, 66m);
        Assert.Equal(1m, n.Ton);
        Assert.Equal(1200m, n.TotalUsd);
        Assert.Equal(79_200m, n.TotalAfn);
        Assert.Equal(1345m, Math.Round(n.Liters, 0));
        //  رقمِ فارسی هم
        Assert.Equal(1000m, StorageSectionViewModel.KgOf("۱۰۰۰"));
    }

    [Fact]
    public void Makhzan_Kadr_Kilo_Ast_Va_Kharide_Nashodani_Hoshdar_Darad()
    {
        var v = Bare(Read("PumpYaqobi.App", "Views", "Sections", "StorageSectionView.axaml"));
        Assert.Contains("Text=\"وزن (کیلو)\"", v);
        Assert.Contains("{Binding BuyKg}", v);
        Assert.DoesNotContain("{Binding BuyTon}", v);
        Assert.Contains("{Binding Implausible}", v);
        var vm = Read("PumpYaqobi.App", "ViewModels", "Sections", "StorageSectionViewModel.cs");
        //  ⛔ ضربِ × ۱۰۰۰ برنگردد
        Assert.DoesNotContain("Shamsi.Num(ton) * 1000m", vm);
        //  ⛔ «۱٬۰۰۰٫۰۰۰» — تن بی صفرهای بی‌مصرف
        Assert.DoesNotContain("Math.Round(n.Ton, 3), 3)", vm);
    }

    // ══ ۲) چراغِ همگام‌سازیِ پایین ═══════════════════════════════════════

    [Fact]
    public void CheraghHamgamSazi_DarNavarePayin_Nist()
    {
        var w = Bare(Read("PumpYaqobi.App", "Views", "MainWindow.axaml"));
        Assert.DoesNotContain("{Binding SyncDotText}", w);
        Assert.DoesNotContain("SyncNowCommand", w);
        var i = w.IndexOf("Name=\"StatusBar\"", StringComparison.Ordinal);
        Assert.True(i > 0);
        Assert.Contains("<Binding Path=\"HasNotice\" />", w.Substring(i, 900));
        //  حالش همچنان در «تنظیمات ← همگام‌سازی» دیده می‌شود
        Assert.True(File.Exists(Path.Combine(Root(), "PumpYaqobi.App", "Views", "Sections", "SyncSectionView.axaml")));
    }

    // ══ ۳) فاکتورها: برگشت، و کلیک روی کارت ═════════════════════════════

    [Fact]
    public void Faktor_Bargasht_Darad_Va_Kart_Klik_Mishavad()
    {
        var v = Bare(Read("PumpYaqobi.App", "Views", "Sections", "InvoiceSectionView.axaml"));
        //  فهرست و خودِ فاکتور هر دو «‹ برگشت» دارند
        Assert.True(System.Text.RegularExpressions.Regex.Matches(v, "BackCommand").Count >= 2);
        Assert.Contains("PointerReleased=\"OnCardReleased\"", v);
        var cs = Read("PumpYaqobi.App", "Views", "Sections", "InvoiceSectionView.axaml.cs");
        //  ⛔ کلیک روی دکمه‌های خودِ کارت فاکتور را باز نمی‌کند
        Assert.Contains("FindAncestorOfType<Button>(includeSelf: true)", cs);
        var vm = Read("PumpYaqobi.App", "ViewModels", "Sections", "InvoiceSectionViewModel.cs");
        Assert.Contains("_cameFrom = IsList ? Pane : InvoicePane.Form;", vm);
        Assert.Contains("if (IsDetail) { Detail = null; Pane = _cameFrom; return; }", vm);
    }

    // ══ ۴) پارچه‌ها: گزارش‌ها در صفحهٔ جدا ═══════════════════════════════

    [Fact]
    public void Parcha_Gozareshha_SafheyeJoda_Mahbemah_Va_FaghatKhandani()
    {
        var v = Bare(Read("PumpYaqobi.App", "Views", "Sections", "ParchaSectionView.axaml"));
        Assert.Contains("OpenReportsCommand", v);
        Assert.Contains("ReportsBackCommand", v);
        Assert.Contains("x:DataType=\"vm:ReportYearGroup\"", v);
        Assert.Contains("x:DataType=\"vm:ReportMonthGroup\"", v);
        Assert.Contains("ShowReportCommand", v);
        //  ⛔ فهرستِ کارت‌ها دیگر زیرِ پارچه‌ها نیست: تنها ‎ItemsRepeater‎ِ گزارش‌ها داخلِ ماه است
        Assert.DoesNotContain("ItemsSource=\"{Binding Reports}\"", v);
        var vm = Read("PumpYaqobi.App", "ViewModels", "Sections", "ParchaSectionViewModel.cs");
        //  ⛔ زیرِ پارچه‌ها فقط یک شمارش، نه همهٔ پارچه‌ها
        var i = vm.IndexOf("private async Task ReloadLogAsync()", StringComparison.Ordinal);
        var body = vm.Substring(i, 600);
        Assert.True(body.IndexOf("CountAsync", StringComparison.Ordinal) < body.IndexOf("if (!ReportsOpen) return;", StringComparison.Ordinal));
        Assert.True(body.IndexOf("if (!ReportsOpen) return;", StringComparison.Ordinal) < body.IndexOf("ListAsync", StringComparison.Ordinal));
        //  ⛔ حذفِ گزارش می‌پرسد
        Assert.Contains("Dialogs.ConfirmAsync(\"حذف گزارش\"", vm);
        //  ⛔ صفحهٔ گزارشِ یک پارچه هیچ کادرِ تایپی ندارد
        var j = v.IndexOf("IsVisible=\"{Binding ShowReportDetail}\"", StringComparison.Ordinal);
        Assert.True(j > 0);
        Assert.DoesNotContain("<TextBox", v.Substring(j));
    }

    // ══ ۶) سربرگ: نامِ ماه، و تنظیمِ تاریخ و ساعت ══════════════════════════

    [Fact]
    public void Sarbarg_NameMah_Darad_Va_Saat_BeTanzimeWindows_Miravad()
    {
        //  ۲۴ سپتامبر ۲۰۲۶ = پنج‌شنبه ۲ میزان ۱۴۰۵
        var t = PumpYaqobi.App.ViewModels.MainViewModel.HeaderDate(new DateTime(2026, 9, 24, 10, 0, 0));
        Assert.Equal("پنج‌شنبه، 2 میزان 1405", t);
        var w = Bare(Read("PumpYaqobi.App", "Views", "MainWindow.axaml"));
        Assert.Contains("OpenClockSettingsCommand", w);
        var src = Read("PumpYaqobi.App", "Services", "SystemClockSettings.cs");
        Assert.Contains("\"ms-settings:dateandtime\"", src);
        //  ⛔ برنامه ساعتِ دومی نمی‌سازد: هیچ «تاریخِ دستی» در تنظیمات نیست
        var settings = Read("PumpYaqobi.App", "Services", "AppSettings.cs");
        Assert.DoesNotContain("DateOverride", settings);
        Assert.DoesNotContain("ClockOffset", settings);
    }

    // ══ ۷) جدول‌ها بی جای خالی · تاریخچهٔ پارچه با صافیِ تیل و پایه ════════

    [Fact]
    public void Jadval_JayeKhali_Nadarad_Va_TarikhcheyeParcha_Safi_Darad()
    {
        var g = Read("PumpYaqobi.App", "Controls", "ExcelGrid.cs");
        Assert.Contains("private bool FillGapStep()", g);
        Assert.Contains("if (!KeepInsideStep() && !FillGapStep()) RememberWidths();", g);
        //  ⚠️ پُرکردن «خواستهٔ کاربر» روی دیسک نمی‌شود
        Assert.Contains("if (auto) _autoWidths = w;", g);
        var h = Bare(Read("PumpYaqobi.App", "Views", "Sections", "HistorySectionView.axaml"));
        Assert.Contains("PickFuelCommand", h);
        Assert.Contains("x:DataType=\"vm:PumpChip\"", h);
        Assert.Contains("IsVisible=\"{Binding HasShiftFilters}\"", h);
        var svc = Read("PumpYaqobi.Services", "Data", "HistoryService.cs");
        Assert.Contains("}, rep.Fuel, shift.PumpNum));", svc);
    }

    // ══ ۸) ورق: پیام روی کلِ خانه، دو خطِ روشن، کارتِ بلندتر ═══════════════

    [Fact]
    public void Varaq_Payam_RoyeKoleKhane_Va_KarteBoland()
    {
        var c = Read("PumpYaqobi.App", "Controls", "IssueTextColumn.cs");
        Assert.Contains("cell.Bind(ToolTip.TipProperty", c);
        Assert.DoesNotContain("host.Bind(ToolTip.TipProperty", c);
        var vm = Read("PumpYaqobi.App", "ViewModels", "Sections", "WaraqSectionViewModel.cs");
        Assert.Contains("\"⚠️ شروع \" + Shamsi.Money(-d) + \" لیتر کمتر است\\n\" + nums", vm);
        var v = Bare(Read("PumpYaqobi.App", "Views", "Sections", "WaraqSectionView.axaml"));
        var m = System.Text.RegularExpressions.Regex.Match(v, "MinItemHeight=\"(\\d+)\"");
        Assert.True(m.Success && int.Parse(m.Groups[1].Value) >= 225);
    }

    // ══ ۹) سرورِ خانگی: «pump1»ی مشترک دیگر گیر نمی‌اندازد ═══════════════

    private static PumpYaqobi.App.Services.AppSettings Settings(string station, string device) =>
        new() { CloudStationId = station, CloudDeviceUid = device };

    [Fact]
    public void KodePomp_AzShenaseyeHesab_YektaVaAmn_Ast()
    {
        var code = PumpYaqobi.App.Services.StationLink.UniqueCode(Settings("stn_A63AE00D/..\\x y", "pc-1"));
        Assert.Equal("stn_a63ae00d----x-y", code);
        Assert.Matches("^[a-z0-9_-]*$", code);
        Assert.True(PumpYaqobi.App.Services.StationLink.UniqueCode(Settings(new string('a', 90), "pc-1")).Length <= 48);
        //  حسابِ بی پمپ ⇒ کدِ حسابی نیست (کدِ همین کامپیوتر جایش می‌نشیند — ‎StationCodePerAccountTests‎)
        Assert.Equal("", PumpYaqobi.App.Services.StationLink.UniqueCode(Settings("", "pc-1")));
    }

    [Fact]
    public void JaygozinHa_HargezHamanKodeRadShode_Nistand()
    {
        var f = Settings("stn_abc", "pc-0123456789abcdef");
        var alts = PumpYaqobi.App.Services.StationLink.Alternatives(f, "pump1").ToList();
        Assert.Equal("stn_abc", alts[0]);                    // اول: کدِ یکتای همین حساب
        Assert.Equal("stn_abc-89abcdef", alts[1]);           // بعد: + شناسهٔ همین کامپیوتر
        Assert.Equal(3, alts.Count);                          // و آخر: یکی تصادفی
        Assert.All(alts, a => Assert.True(a.Length <= 48 && System.Text.RegularExpressions.Regex.IsMatch(a, "^[a-z0-9_-]+$")));

        //  کدِ حساب همان است که رد شد ⇒ دیگر پیشنهاد نمی‌شود
        var again = PumpYaqobi.App.Services.StationLink.Alternatives(f, "stn_abc").ToList();
        Assert.DoesNotContain("stn_abc", again);
        Assert.Equal("stn_abc-89abcdef", again[0]);
    }

    [Fact]
    public void BarkhordeKod_FaghatBiRamzVaBiPin_JaygozinMigirad()
    {
        var src = Read("PumpYaqobi.App", "Services", "StationLink.cs");
        //  ⛔ نصبی که رمزِ پوشهٔ **همین حساب** را دارد هیچ‌وقت کدش عوض نمی‌شود؛
        //  فقط پوشه‌ای که مالِ این حساب نیست جابه‌جا می‌شود، و رمزش همراه نمی‌رود
        Assert.Contains("var moving = token.Length > 0 && !Same(saved, code);", src);
        Assert.Contains("if (moving) { token = \"\"; readKey = \"\"; }", src);
        Assert.Contains("result.Error == \"already_taken\" && token.Length == 0 && pin.Trim().Length == 0", src);
        //  ⛔ خطای دیگری که برخوردِ کد نیست، حلقه را می‌بندد (بی تلاشِ کور)
        Assert.Contains("if (again.Error != \"already_taken\") break;", src);
    }
}
