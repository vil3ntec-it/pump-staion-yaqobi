using System.Reflection;
using PumpYaqobi.Reporting.Pdf;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ گزارشِ ۱۴۰۵/۰۷/۱۲ — ورق‌های «ناپدید»، پهنای ستون، و ورقِ چاپی ═════════════
///
/// رفتارِ کامل در دو فرآیندِ جدا سنجیده می‌شود (‎persist‎ و ‎printpages‎ در
/// ‎PumpYaqobi.UiTests‎). این‌جا قاعده‌هایی قفل می‌شوند که اگر کسی فردا
/// «ساده‌شان» کند، همان باگ‌ها بی‌صدا برمی‌گردند.
/// </summary>
public class SheetsWidthsPrintTests
{
    private static readonly string Root =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    private static string Src(string rel) =>
        File.ReadAllText(Path.Combine(Root, rel.Replace('/', Path.DirectorySeparatorChar)));

    // ── ۱) ورق‌ها ناپدید نبودند؛ ماه عوض شده بود ────────────────────────────

    [Fact]
    public void FehresteWaraq_DarMaheKhali_DoroghNemiGooyad()
    {
        var s = Src("PumpYaqobi.App/ViewModels/Sections/WaraqSectionViewModel.cs");
        //  «هیچ ورقی ثبت نشده» فقط وقتی که واقعاً هیچ ماهی ورق ندارد
        Assert.Contains("public string EmptyText => _dataMonths.Count == 0", s);
        //  ماهِ خالیِ جاری ⇒ تازه‌ترین ماهی که ورق دارد
        Assert.Contains("_dataMonths.Contains(now) || _dataMonths.Count == 0 ? now : _dataMonths[0]", s);
        //  ماهی که کاربر خودش برگزیده زیرِ دستش عوض نمی‌شود
        Assert.Contains("_monthAuto = false;                 // کاربر خودش برگزید", s);

        var view = Src("PumpYaqobi.App/Views/Sections/WaraqSectionView.axaml");
        Assert.DoesNotContain("Text=\"هیچ ورقی ثبت نشده", view);
        Assert.Contains("{Binding EmptyText}", view);
    }

    [Fact]
    public void DaftarhayeMahane_NavareMaheDigar_Darand()
    {
        var baseVm = Src("PumpYaqobi.App/ViewModels/SectionViewModel.cs");
        Assert.Contains("private string _monthHint", baseVm);
        Assert.Contains("GoMonthHintAsync", baseVm);

        var ledger = Src("PumpYaqobi.App/ViewModels/LedgerSectionViewModel.cs");
        Assert.Contains("RefreshMonthHint();", ledger);

        //  یک جا، در پوستهٔ پنجره — نه یک متنِ جدا در هر نما
        var win = Src("PumpYaqobi.App/Views/MainWindow.axaml");
        Assert.Contains("Content.HasMonthHint", win);
        Assert.Contains("Content.GoMonthHintCommand", win);
    }

    [Fact]
    public void MaheTaze_MigooyadPakNashode_VaMozahemNist()
    {
        //  «بفهمونه که ماه عوض شده نه حساب‌ها پاک شدن… دیده بشه و مزاحمت ایجاد نکنه»
        foreach (var f in new[] { "PumpYaqobi.App/ViewModels/Sections/WaraqSectionViewModel.cs",
                                  "PumpYaqobi.App/ViewModels/LedgerSectionViewModel.cs" })
            Assert.Contains("هیچ چیزی پاک نشده", Src(f));

        var baseVm = Src("PumpYaqobi.App/ViewModels/SectionViewModel.cs");
        Assert.Contains("MonthHint != _hintDismissed", baseVm);
        Assert.Contains("Content.DismissMonthHintCommand", Src("PumpYaqobi.App/Views/MainWindow.axaml"));

        //  سرِ نیمه‌شبِ آخرِ ماه یک خبرِ گذرا، نه پنجرهٔ پرسش
        var main = Src("PumpYaqobi.App/ViewModels/MainViewModel.cs");
        var i = main.IndexOf("public void DayChanged()", StringComparison.Ordinal);
        var body = main.Substring(i, 1400);
        Assert.Contains("month != _seenMonth", body);
        Assert.Contains("Toasts.Show(", body);
        Assert.DoesNotContain("ConfirmAsync", body);
    }

    [Fact]
    public void HazfeWaraq_BiPorsesh_Nist()
    {
        var s = Src("PumpYaqobi.App/ViewModels/Sections/WaraqSectionViewModel.cs");
        var i = s.IndexOf("private async Task DeleteSheetAsync", StringComparison.Ordinal);
        Assert.True(i > 0);
        var body = s.Substring(i, s.IndexOf("DeleteAsync(w.Id)", i, StringComparison.Ordinal) - i);
        Assert.Contains("Dialogs.ConfirmAsync(\"حذفِ ورق\"", body);
    }

    // ── ۲) «درجا ثبت شود» ───────────────────────────────────────────────────

    [Fact]
    public void MakseZakhire_KootahAst()
    {
        //  سنجهٔ ‎persist crash‎ با همین عدد سبز است؛ بالا بردنش یعنی حرف‌هایی
        //  که با رفتنِ برق گم می‌شوند.
        Assert.True(PumpYaqobi.App.ViewModels.RowViewModel.SaveDelayMs <= 150);
        var s = Src("PumpYaqobi.App/ViewModels/RowViewModel.cs");
        Assert.Contains("await Task.Delay(SaveDelayMs, ct);", s);
    }

    // ── ۳) پهنای ستون: فقط خواستهٔ کاربر، و هر حساب برای خودش ────────────────

    [Fact]
    public void PahnayeKhodkar_BeJayeKarbar_ZakhireNemishavad()
    {
        var s = Src("PumpYaqobi.App/Controls/ExcelGrid.cs");
        var i = s.IndexOf("private void RememberWidths()", StringComparison.Ordinal);
        var body = s.Substring(i, 2500);
        //  پیش از نخستین چیدنِ خودِ جدول، هیچ چیزی ذخیره نمی‌شود
        Assert.Contains("if (!_spread) return;", body);
        //  همه ستاره‌ای ⇒ کاربر چیزی نکشیده
        Assert.Contains("if (!cols.Any(c => c.Width.UnitType == DataGridLengthUnitType.Pixel)) return;", body);
        //  خطِ پایهٔ ستاره‌ای پیش از کشیدن گرفته می‌شود، نه پس از آن
        Assert.Contains("_starBase = cols.Select(c => c.ActualWidth).ToArray();", s);
    }

    [Fact]
    public void PahnaBeHesab_BasteAst()
    {
        var grid = Src("PumpYaqobi.App/Controls/ExcelGrid.cs");
        Assert.Contains("WidthScopeProperty", grid);
        Assert.Contains("\"@\" + scope", grid);

        Assert.Contains("WidthScope=\"{Binding Current.WidthScope}\"",
                        Src("PumpYaqobi.App/Views/Sections/PersonView.axaml"));
        Assert.Contains("WidthScope=\"{Binding WidthScope}\"",
                        Src("PumpYaqobi.App/Views/Sections/CompanyPageView.axaml"));
        Assert.Contains("public string WidthScope => \"debt-\" + Entity.Id;",
                        Src("PumpYaqobi.App/ViewModels/Sections/PersonViewModel.cs"));
        Assert.Contains("public string WidthScope => \"co-\" + Entity.Id;",
                        Src("PumpYaqobi.App/ViewModels/Sections/CompanySectionViewModel.cs"));
    }

    [Fact]
    public void NavareJomle_BaJayeSarsotoonHa_Midanad()
    {
        //  امضای نوار پهنا و جای سرستون‌ها را هم دارد — بی آن، پس از نشستنِ
        //  پهنای ذخیره‌شده جمله‌ها ~۶۰۰ پیکسل دور از ستونشان می‌ماندند.
        var s = Src("PumpYaqobi.App/Controls/TotalsBar.cs");
        Assert.Contains("h = unchecked(h * 31 + (long)Math.Round(hd.Bounds.Width));", s);
    }

    // ── ۴) ورقِ چاپی ───────────────────────────────────────────────────────

    [Fact]
    public void SanadeHesab_HamanAdadeSafhe_Ast()
    {
        var r = Src("PumpYaqobi.Reporting/Pdf/DebtorStatementReport.cs");
        //  ستونِ «الباقی»ِ ردیف‌ها دیگر خالی نیست
        Assert.DoesNotContain("Td(\"\");", r);
        Assert.Contains("bardagi - r.Rasid", r);
        //  علامتِ الباقی همان صفحه است، نه قرینه
        Assert.DoesNotContain("var show = -rem;", r);
        Assert.Contains("var show = Math.Round(bord + comm - rasid", r);

        //  و رسید همان «رسید قبلی»ِ کادرِ صفحه است
        var vm = Src("PumpYaqobi.App/ViewModels/Sections/PersonViewModel.cs");
        Assert.Contains("RasidPetrol: acct.HeadRasidOf(FuelType.Petrol)", vm);
    }

    [Fact]
    public void HichRangeKhamiDarGozareshha_Nist()
    {
        //  هر رنگی در ‎*Report.cs‎ از ‎Paint/Ink/Edge‎ رد می‌شود، وگرنه
        //  سیاه‌وسفید و خاکستری روی آن اثر ندارند.
        var dir = Path.Combine(Root, "PumpYaqobi.Reporting", "Pdf");
        var bad = new List<string>();
        var rx = new System.Text.RegularExpressions.Regex(@"\.(FontColor|Background|BorderColor)\((?!DocStyle\.(Paint|Ink|Edge|InkOn)\()");
        foreach (var f in Directory.GetFiles(dir, "*Report.cs"))
        {
            var lines = File.ReadAllLines(f);
            for (var i = 0; i < lines.Length; i++)
                if (rx.IsMatch(lines[i])) bad.Add(Path.GetFileName(f) + ":" + (i + 1));
        }
        Assert.True(bad.Count == 0, "رنگِ خام: " + string.Join("، ", bad));
    }

    private static void With(PrintColor mode, Action act)
    {
        var f = typeof(DocStyle).GetField("_current", BindingFlags.NonPublic | BindingFlags.Static)!;
        var was = f.GetValue(null);
        f.SetValue(null, PageSetup.Default with { Color = mode });
        try { act(); } finally { f.SetValue(null, was); }
    }

    [Fact]
    public void SiyahVaSefid_NeveshteVaKhat_HargezGomNemishavand()
    {
        With(PrintColor.BlackWhite, () =>
        {
            //  خطِ خاکستریِ روشنِ جدول ⇒ سیاه، نه سفید
            Assert.Equal("#000000", DocStyle.Edge(DocStyle.CellLine));
            //  نوشتهٔ کم‌رنگ (برچسب) ⇒ سیاه، نه سفید روی سفید
            Assert.Equal("#000000", DocStyle.Ink("#a0aec0"));
            Assert.Equal("#000000", DocStyle.Ink(DocStyle.Sub));
            //  نوشتهٔ سرستون روی نوارِ تیره ⇒ سفید
            Assert.Equal("#ffffff", DocStyle.InkOn(DocStyle.HeadFg, DocStyle.HeadBg));
        });
        With(PrintColor.Color, () =>
        {
            //  رنگی دست‌نخورده است
            Assert.Equal(DocStyle.CellLine, DocStyle.Edge(DocStyle.CellLine));
            Assert.Equal("#a0aec0", DocStyle.Ink("#a0aec0"));
        });
    }

    [Fact]
    public void TarikhVaAdad_DoKhat_Nemishavand()
    {
        var s = Src("PumpYaqobi.Reporting/Pdf/DocStyle.cs");
        Assert.Contains("Compact(text) ? cell.MaxHeight(CellSize * OneLine).ScaleToFit() : cell", s);
    }

    [Fact]
    public void Petrol_BaPe_Neveshte_Mishavad()
    {
        foreach (var f in new[] { "PumpYaqobi.Reporting/Pdf/WaraqReport.cs",
                                  "PumpYaqobi.App/Views/Sections/WaraqPageView.axaml" })
            Assert.DoesNotContain("بطرول", Src(f));
    }
}
