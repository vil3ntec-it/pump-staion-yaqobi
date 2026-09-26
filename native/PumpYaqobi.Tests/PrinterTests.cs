using System.IO;
using PumpYaqobi.App.Printing;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ «پرینتر وصل است ولی برنامه نشانش نمی‌دهد — مثلِ اکسل درستش کن» (۱۴۰۵/۰۷/۱۴) ══
///
/// ریشه: برنامه هیچ چاپگری فهرست نمی‌کرد و چاپ را با فعلِ ‎print‎ِ PDF به
/// ویندوز می‌داد — که بی آکروبات (با اج) بی‌صدا هیچ کاری نمی‌کرد. این‌جا
/// بخش‌های خالصِ راهِ تازه سنجیده می‌شوند؛ رفتارِ پنجره در ‎printshot‎ است.
/// </summary>
public class PrinterTests
{
    private static PrinterItem P(string n, bool def = false, bool ready = true) =>
        new(n, def, ready ? "آماده" : "آفلاین — روشن و وصل است؟", ready);

    [Fact]
    public void PishFarz_Aval_Ast_Mesle_Excel()
    {
        var list = Printers.Order(new[] { P("Microsoft Print to PDF"), P("HP", ready: false), P("EPSON L382 Series", def: true) });
        Assert.Equal("EPSON L382 Series", list[0].Name);
        Assert.Equal("HP", list[^1].Name);                 // آفلاین آخر
        Assert.Equal("EPSON L382 Series", Printers.Pick(list, null)!.Name);
    }

    [Fact]
    public void Entekhabe_BarePish_YadashMimanad_AgarHanuzHast()
    {
        var list = Printers.Order(new[] { P("EPSON L382 Series", def: true), P("Microsoft Print to PDF") });
        Assert.Equal("Microsoft Print to PDF", Printers.Pick(list, "microsoft print to pdf")!.Name);
        //  چاپگرِ برداشته‌شده ⇒ پیش‌فرضِ ویندوز، نه هیچ
        Assert.Equal("EPSON L382 Series", Printers.Pick(list, "Canon قدیمی")!.Name);
        Assert.Null(Printers.Pick(Array.Empty<PrinterItem>(), "x"));
    }

    [Fact]
    public void Hale_Chapgar_BeZabaneAdam()
    {
        Assert.Equal(("آماده", true), Printers.StatusOf(0, 0));
        Assert.False(Printers.StatusOf(0, 0x400).Ready);          // WORK_OFFLINE
        Assert.False(Printers.StatusOf(0x80, 0).Ready);           // OFFLINE
        Assert.Equal("کاغذ تمام شده", Printers.StatusOf(0x10, 0).Text);
        Assert.Equal("کاغذ گیر کرده", Printers.StatusOf(0x8, 0).Text);
        Assert.True(Printers.StatusOf(0x20000, 0).Ready);         // جوهر کم ⇒ هنوز چاپ می‌کند
    }

    /// <summary>ورق به اندازهٔ واقعیِ خودش — A4 روی A4 همان، روی Letter کوچک و وسط.</summary>
    [Fact]
    public void Varagh_BeAndazeyeVagheii_VaHichVaght_Borideh_Nemishavad()
    {
        //  A4 با ۱۵۰ نقطه: 1240×1754 پیکسل. چاپگرِ ۶۰۰ نقطه، کاغذِ A4 = 4960×7016
        var (x, y, w, h) = Printers.Place(1240, 1754, 150, 600, 600, 4960, 7016, 100, 100);
        Assert.Equal(4960, w);
        Assert.Equal(7016, h);
        Assert.Equal(-100, x);                    // جای‌چاپ‌پذیر از حاشیهٔ سختِ چاپگر شروع می‌شود
        Assert.Equal(-100, y);

        //  همان ورق روی Letter (5100×6600): کوچک می‌شود تا جا شود، و وسط
        (x, y, w, h) = Printers.Place(1240, 1754, 150, 600, 600, 5100, 6600, 0, 0);
        Assert.True(w <= 5100 && h <= 6600);
        Assert.Equal(6600, h);
        Assert.InRange(x, (5100 - w) / 2 - 1, (5100 - w) / 2 + 1);

        //  ورقِ کوچک‌تر از کاغذ هیچ‌وقت بزرگ نمی‌شود
        (_, _, w, _) = Printers.Place(600, 600, 150, 600, 600, 4960, 7016, 0, 0);
        Assert.Equal(2400, w);
    }

    private static string Src(params string[] parts)
    {
        var d = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (d is not null && !Directory.Exists(Path.Combine(d.FullName, "PumpYaqobi.App"))) d = d.Parent;
        return File.ReadAllText(Path.Combine(d!.FullName, Path.Combine(parts)));
    }

    /// <summary>⛔ دکمهٔ چاپ اول به چاپگرِ برگزیده می‌رود؛ فعلِ ‎print‎ فقط وقتی فهرست خالی است.</summary>
    [Fact]
    public void Chap_Mostaghim_BeChapgareBargozide_Mirravad()
    {
        var cs = Src("PumpYaqobi.App", "Views", "DocumentPreviewWindow.axaml.cs");
        var onPrint = cs.Split("private async void OnPrint(")[1].Split("\n    }\n")[0];
        Assert.True(onPrint.IndexOf("Vm.PrintToAsync(printer)", StringComparison.Ordinal)
                    < onPrint.IndexOf("PrintService.Print(path)", StringComparison.Ordinal));
        //  و بی‌صدا نیست: پیامِ «فرستاده شد» نامِ خودِ چاپگر را دارد
        Assert.Contains("فرستاده شد", onPrint);

        var ax = Src("PumpYaqobi.App", "Views", "DocumentPreviewWindow.axaml");
        Assert.Contains("ItemsSource=\"{Binding Printers}\"", ax);
        Assert.Contains("SelectedItem=\"{Binding Printer}\"", ax);
        Assert.Contains("LoadPrintersCommand", ax);

        //  انتخابِ چاپگر مقدارِ راحتی است — در هر دو فهرستِ ‎SaveSoon‎
        var st = Src("PumpYaqobi.App", "Services", "AppSettings.cs");
        Assert.Contains("live.LastPrinter = LastPrinter;", st);
    }
}
