using PumpYaqobi.App.ViewModels;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ فهرستِ ۱۴۰۵/۰۷/۱۵ی صاحب ریپو ═══════════════════════════════════════════
///
/// «در دارک مود نوشته‌ها می‌روند سمتِ چپ» · تمِ رادیویی · تاریخ و ساعتِ خودِ
/// برنامه · ترتیبِ بخش‌ها · سربرگِ جمع‌وجور. رفتارِ پنجره را ‎layoutcycle‎ و
/// ‎round15‎ و ‎waraqscroll‎ در ‎PumpYaqobi.UiTests‎ می‌سنجند.
/// </summary>
public class OwnerRound15Tests
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

    // ══ ۱) نوشتهٔ چپ‌چین پس از تعویضِ تم ══════════════════════════════════

    [Fact]
    public void Tem_QalameRangi_RaJaNemizanad_FaqatRangash_Avaz_Mishavad()
    {
        var src = Read("PumpYaqobi.App", "Themes", "ThemeManager.cs");
        //  ⛔ هر کلیدِ تک‌رنگ یک قلمِ مشترک برای هر دو تم — عوض شدنِ قلم ‎Foreground‎ی
        //  همهٔ نوشته‌ها را عوض و آن‌ها را «نااندازه» می‌کرد (ریشهٔ نوشتهٔ چپ‌چین).
        Assert.Contains("r[\"Pump.\" + key] = Shared(\"Pump.\" + key, c);", src);
        Assert.DoesNotContain("r[\"Pump.\" + key] = new SolidColorBrush(c)", src);
        //  و ‎Apply‎ رنگِ همان قلم‌ها را عوض می‌کند، پیش از خبرِ مبدل‌ها
        var apply = src[src.IndexOf("public static void Apply", StringComparison.Ordinal)..];
        var paint = apply.IndexOf("Paint(variant);", StringComparison.Ordinal);
        var refresh = apply.IndexOf("ResourceKeyToBrushConverter.Refresh();", StringComparison.Ordinal);
        Assert.True(paint > 0 && refresh > paint, "‎Paint‎ باید پیش از ‎Refresh‎ی مبدل‌ها باشد");
    }

    [Fact]
    public void NeveshteyeKohne_BaDideShodan_DobareKeshide_Mishavad()
    {
        var src = Read("PumpYaqobi.App", "Controls", "StaleTextGuard.cs");
        Assert.Contains("IsVisibleProperty.Changed.AddClassHandler<Visual>", src);
        Assert.Contains("!tb.IsMeasureValid", src);
        Assert.Contains("tb.InvalidateVisual()", src);
        //  ⛔ شاخهٔ پنهان گشته نمی‌شود و هیچ شنوندهٔ ‎LayoutUpdated‎ی نیست (قاعدهٔ سرعت)
        Assert.Contains("if (c.IsVisible) Redraw(c);", src);
        Assert.DoesNotContain("LayoutUpdated +=", src);
        Assert.Contains("Controls.StaleTextGuard.Install();", Read("PumpYaqobi.App", "App.axaml.cs"));
    }

    // ══ ۲) سربرگ ════════════════════════════════════════════════════════

    /// <summary>
    /// ══ تم: یک کلیدِ کپسولی برای هر دو (۱۴۰۵/۰۷/۱۵) ══
    /// «کشویی است، که خیلی جا نگیرد» و بعد «هر دو توی یک کادر باشند»: نه
    /// کشویی و نه دو دکمه — یک کلید که روی «روشن» نارنجی با خورشید است و روی
    /// «تیره» بنفش با ماه. ⛔ هیچ قلمِ جدا-برای-هر-تمی روی نوشته و هیچ انتقالِ
    /// رنگی (همان دو ریشهٔ «نوشته‌ها در دارک مود چپ‌چین می‌شوند» و عکسِ
    /// نیمه‌کارهٔ ‎themeflip‎).
    /// </summary>
    [Fact]
    public void Tem_YekKelideKapsuli_BaraHarDo()
    {
        var w = Bare(Read("PumpYaqobi.App", "Views", "MainWindow.axaml"));
        Assert.DoesNotContain("ItemsSource=\"{Binding Themes}\"", w);
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(w, "Classes=\"themeswitch\""));
        Assert.Contains("IsChecked=\"{Binding DarkSwitch}\"", w);
        Assert.DoesNotContain("GroupName=\"pump-theme\"", w);          // دو دکمه دیگر نیست
        Assert.Contains("Classes=\"dayside\"", w);
        Assert.Contains("Classes=\"nightside\"", w);

        var styles = w[w.IndexOf("<Style Selector=\"ToggleButton.themeswitch\">", StringComparison.Ordinal)..];
        styles = styles[..styles.IndexOf("Border#WarmLogo", StringComparison.Ordinal)];
        Assert.DoesNotContain("Transition", styles);
        foreach (var line in styles.Split('\n').Where(l => l.Contains("Property=\"Foreground\"")))
            Assert.Contains("#FFFFFF", line);

        var vm = Read("PumpYaqobi.App", "ViewModels", "MainViewModel.cs");
        //  همان ‎SelectedTheme‎ — یک حقیقت، و «نادرست» یعنی برگشت به روشن
        Assert.Contains("set { if (value != SelectedTheme.IsDark) SelectedTheme = value ? PumpTheme.Gold : PumpTheme.Blue; }", vm);
        Assert.Contains("OnPropertyChanged(nameof(DarkSwitch));", vm);
    }

    [Fact]
    public void Tarikh_DarJazireyeRastBeChap_Va_NameMah()
    {
        var t = MainViewModel.HeaderDate(new DateTime(2026, 8, 23, 9, 0, 0));   // ۱ سنبله ۱۴۰۵
        Assert.StartsWith("\u2067", t);
        Assert.EndsWith("\u2069", t);
        //  ⛔ شکلِ خواستهٔ صاحب ریپو: «یکشنبه سنبله 1405.6.5» — بی صفرِ پیشرو
        Assert.Equal("یک‌شنبه سنبله 1405/6/1", t.Trim('\u2067', '\u2069'));
        Assert.Equal("یک‌شنبه میزان 1405/7/5",
                     MainViewModel.HeaderDate(new DateTime(2026, 9, 27, 9, 0, 0)).Trim('\u2067', '\u2069'));
        //  داشبورد هم همان شکل را دارد، نه ‎1405/07/05‎
        var dash = Read("PumpYaqobi.App", "ViewModels", "Sections", "DashboardSectionViewModel.cs");
        Assert.Contains("DateLine = \"· \" + MainViewModel.HeaderDate(d);", dash);
    }

    [Fact]
    public void Saat_PanjereyeKhodeBarname_RoozhayeMah_AzTaqvim()
    {
        var vm = new ClockViewModel(new DateTime(2026, 8, 23, 9, 5, 0));   // ۱ سنبله ۱۴۰۵
        Assert.Equal(1405, vm.Year);
        Assert.Equal(5, vm.MonthIndex);
        Assert.Equal(1, vm.Day);
        Assert.Equal(31, vm.Days.Count);
        vm.Day = 31;
        vm.MonthIndex = 6;                     // میزان ۳۰ روز ⇒ روزِ ۳۱ می‌شود ۳۰
        Assert.Equal(30, vm.Days.Count);
        Assert.Equal(30, vm.Day);
        Assert.Contains("میزان", vm.PickedText);
        //  ⛔ ساعتِ دوازده‌ساعته با AM/PM (خواستهٔ ۱۴۰۵/۰۷/۱۵)
        Assert.Contains("09:05 AM", vm.PickedText);
        Assert.Equal("12 AM", vm.Hours[0]);
        Assert.Equal("01 PM", vm.Hours[13]);
        Assert.Equal("12 PM", vm.Hours[12]);
        Assert.Equal("\u200E09:05:07 PM", PumpYaqobi.App.Localization.Clock.Of(new DateTime(2026, 1, 1, 21, 5, 7), seconds: true));
        Assert.DoesNotContain("HH:mm", Read("PumpYaqobi.App", "Localization", "Clock.cs"));
        //  همان لحظه به میلادی: ۳۰ میزان ۱۴۰۵ = ۲۲ اکتبر ۲۰۲۶
        Assert.Equal(new DateTime(2026, 10, 22, 9, 5, 0), vm.Picked);
    }

    [Fact]
    public void Saat_HamanSaateWindows_RaAvazMikonad_BiSaateDovom()
    {
        PumpYaqobi.App.Services.ClockService.TestRun = null;
        var psi = PumpYaqobi.App.Services.ClockService.Elevated("powershell.exe", "x");
        Assert.Equal("runas", psi.Verb);
        Assert.True(psi.UseShellExecute);
        var src = Read("PumpYaqobi.App", "Services", "ClockService.cs");
        Assert.Contains("Set-Date -Date '", src);
        Assert.Contains("CultureInfo.InvariantCulture", src);
        Assert.Contains("NativeErrorCode == 1223", src);   // «نه»ی کاربر ⇒ لغو، نه خطا
        Assert.DoesNotContain("ms-settings", src);
    }

    // ══ ۳) ترتیبِ بخش‌ها ═══════════════════════════════════════════════════

    private static readonly string[] D = { "dashboard", "shifts", "waraq", "debt", "sarrafi", "safe" };

    [Fact]
    public void TartibeNavar_PishFarz_Va_Jabejayi()
    {
        Assert.Equal(D, NavOrder.Arrange(D, ""));
        var first = NavOrder.Move(D, "sarrafi", NavOrder.Where.First);
        Assert.Equal("sarrafi", first[0]);
        var last = NavOrder.Move(D, "sarrafi", NavOrder.Where.Last);
        Assert.Equal("sarrafi", last[^1]);
        Assert.Equal(new[] { "dashboard", "shifts", "waraq", "sarrafi", "debt", "safe" },
                     NavOrder.Move(D, "sarrafi", NavOrder.Where.Earlier));
        Assert.Equal(new[] { "dashboard", "shifts", "waraq", "debt", "safe", "sarrafi" },
                     NavOrder.Move(D, "sarrafi", NavOrder.Where.Later));
        //  لبه‌ها چیزی را نمی‌شکنند
        Assert.Equal(D, NavOrder.Move(D, "dashboard", NavOrder.Where.Earlier));
        Assert.Equal(D, NavOrder.Move(D, "safe", NavOrder.Where.Later));
        Assert.Equal(D, NavOrder.Move(D, "nope", NavOrder.Where.First));
    }

    [Fact]
    public void TartibeNavar_Zakhire_BakhsheTaze_Va_BakhsheRafte()
    {
        //  ترتیبِ پیش‌فرض ذخیره نمی‌شود — تا بخشِ تازهٔ فردا جای درستش را بگیرد
        Assert.Equal("", NavOrder.Save(D, D));
        var moved = NavOrder.Move(D, "sarrafi", NavOrder.Where.First);
        var saved = NavOrder.Save(D, moved);
        Assert.Equal(moved, NavOrder.Arrange(D, saved));
        //  بخشِ تازه (نسخهٔ بعد) کنارِ همسایهٔ پیش‌فرضش می‌نشیند، گم نمی‌شود
        var withNew = new[] { "dashboard", "shifts", "waraq", "newone", "debt", "sarrafi", "safe" };
        var arranged = NavOrder.Arrange(withNew, saved);
        Assert.Equal(withNew.Length, arranged.Count);
        Assert.Equal("sarrafi", arranged[0]);
        Assert.Equal(arranged.IndexOf("waraq") + 1, arranged.IndexOf("newone"));
        //  بخشی که دیگر نیست نادیده گرفته می‌شود؛ تکراری هم
        Assert.Equal(D, NavOrder.Arrange(D, "gone,dashboard,dashboard"));
    }

    [Fact]
    public void AltAdad_TartibeNavar_RaMiravad_Va_TartibZakhireMishavad()
    {
        var sc = Read("PumpYaqobi.App", "Services", "Shortcuts.cs");
        var go = sc[sc.IndexOf("private void GotoSection", StringComparison.Ordinal)..];
        go = go[..go.IndexOf("private async Task OpenCardAsync", StringComparison.Ordinal)];
        Assert.Contains("_vm.NavSections", go);
        Assert.DoesNotContain("_vm.Sections[", go);
        //  مقدارِ راحتی ⇒ ‎SaveSoon‎ و فهرستِ ‎SaveComfortOnly‎
        var st = Read("PumpYaqobi.App", "Services", "AppSettings.cs");
        Assert.Contains("live.NavOrder = NavOrder;", st);
        var w = Bare(Read("PumpYaqobi.App", "Views", "MainWindow.axaml"));
        Assert.Contains("Click=\"OnNavMove\"", w);
    }

    // ══ ۴) سربرگِ هر بخش: توضیح یک ردیفِ تمام‌پهناست ═══════════════════════

    [Fact]
    public void TozihZireOnvan_RadifeTamamPahna_NaSotooneBarik()
    {
        var c = Read("PumpYaqobi.App", "Themes", "Controls.axaml");
        var i = c.IndexOf("Text=\"{TemplateBinding SubHeader}\"", StringComparison.Ordinal);
        Assert.True(i > 0);
        var tag = c[c.LastIndexOf("<TextBlock", i, StringComparison.Ordinal)..c.IndexOf("/>", i, StringComparison.Ordinal)];
        Assert.Contains("Grid.ColumnSpan=\"3\"", tag);
        Assert.Contains("Grid.Row=\"1\"", tag);
        Assert.DoesNotContain("MaxWidth=\"250\"", tag);
    }
}
