using Avalonia;
using Avalonia.Media;
using PumpYaqobi.App.Services;

namespace PumpYaqobi.App.Themes;

/// <summary>
/// ══ خطِ جدول‌ها — یک جا، برای همهٔ جدول‌ها ══════════════════════════════════
///
/// گزارشِ صاحب ریپو: «خطوطِ جدول (عمودی، افقی، مرزِ خانه، مرزِ سربرگ، مرزِ
/// ردیفِ جمله) واضح و تیره باشند، و **نباید Hard-coded باشند** — در تنظیمات
/// یک بخشِ «ظاهرِ جدول» باشد با رنگِ خط و ضخامتِ خط، و همان روی همهٔ جدول‌های
/// برنامه اعمال شود.»
///
/// پس هیچ‌جای برنامه دیگر رنگ یا ضخامتِ خطِ جدول را خودش نمی‌نویسد؛ همه از
/// همین چهار منبعِ پویا می‌خوانند و این کلاس تنها جایی است که پُرشان می‌کند:
///
///   ‎Pump.Table.Border‎        رنگِ خط (قلم‌مو)
///   ‎Pump.Table.Line‎          ضخامتِ خطِ بینِ خانه‌ها      (عدد)
///   ‎Pump.Table.HeadLine‎      ضخامتِ جداکنندهٔ سربرگ‌ها    (عدد)
///   ‎Pump.Table.HeadUnderline‎ خطِ زیرِ سربرگ               (‎Thickness‎)
///   ‎Pump.Table.SumBorder‎     دورِ ردیفِ «جمله»            (‎Thickness‎)
///
/// ⚠️ چرا با تم هم دوباره صدا زده می‌شود: وقتی کاربر رنگی انتخاب نکرده،
/// رنگِ خط همان ‎Pump.Border‎ی تم است. تم که عوض شود آن رنگ عوض می‌شود، پس
/// این‌جا هم باید از نو نوشته شود — وگرنه جدول‌ها رنگِ تمِ قبلی را نگه
/// می‌داشتند. (‎ThemeManager.Changed‎ در ‎Hook()‎.)
/// </summary>
public static class TableStyle
{
    /// <summary>ضخامت‌های مجاز — همان چهار پله‌ای که در تنظیمات دیده می‌شود.</summary>
    public static readonly double[] Sizes = { 1, 2, 3, 4 };

    private static bool _hooked;

    /// <summary>یک‌بار در راه‌اندازی: با هر تعویضِ تم، خط‌ها از نو نوشته شوند.</summary>
    public static void Hook()
    {
        if (_hooked) return;
        _hooked = true;
        // ⚠️ دیگر روی ‎ThemeManager.Changed‎ نمی‌نشیند: خودِ ‎ThemeManager.Apply‎
        // این چهار منبع را در همان فرهنگِ تم می‌گذارد (‎Fill‎) تا تعویضِ تم
        // **یک** پاسِ بی‌اعتبارسازی باشد، نه هفت تا. با پنج سال داده هر پاس
        // روی جدول‌های زنده دو ثانیه بود.
    }

    /// <summary>
    /// همان چهار منبع، ولی داخلِ فرهنگی که ‎ThemeManager‎ می‌سازد — پیش از
    /// این‌که یک‌جا بنشیند. رنگِ خط از خودِ همان تم می‌آید.
    /// </summary>
    public static void Fill(Avalonia.Controls.ResourceDictionary r, PumpTheme t, AppSettings? s = null)
    {
        s ??= AppSettings.Load();
        var picked = Parse(s.TableBorderColor);
        r["Pump.Table.Border"] = new SolidColorBrush(picked ?? t.Border);
        var line = Clamp(s.TableLine, 1);
        var head = Clamp(s.TableHeadLine, 2);
        var sum = Clamp(s.TableSumLine, 2);
        r["Pump.Table.Line"] = line;
        r["Pump.Table.HeadLine"] = head;
        r["Pump.Table.HeadUnderline"] = new Thickness(0, 0, 0, head);
        r["Pump.Table.SumBorder"] = new Thickness(line, sum, line, line);
    }

    /// <summary>رنگِ متنِ ‎#rrggbb‎ را می‌خواند؛ نشد، ‎null‎.</summary>
    public static Color? Parse(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return null;
        try { return Color.Parse(hex.Trim()); } catch { return null; }
    }

    /// <summary>تنظیماتِ ذخیره‌شده را روی منبع‌های پویا می‌نشاند.</summary>
    public static void Apply(AppSettings? s = null, Avalonia.Application? app = null)
    {
        app ??= Avalonia.Application.Current;
        if (app is null) return;
        s ??= AppSettings.Load();
        // ⚠️ روی فرهنگِ **هر** تمِ نصب‌شده می‌نویسیم، نه ‎app.Resources‎: مقدارِ
        // داخلِ ‎app.Resources‎ روی فرهنگ‌های ادغام‌شده اولویت دارد و اگر آن‌جا
        // می‌نوشتیم، رنگِ خطِ تمِ بعدی هرگز دیده نمی‌شد. و چون هر دو تم از اول
        // نصب‌اند (‎ThemeManager.Installed‎)، ضخامتی که کاربر برمی‌گزیند باید
        // همان لحظه در تمِ دیگر هم بنشیند، وگرنه با تعویضِ تم می‌پرید.
        var any = false;
        foreach (var (t, r) in ThemeManager.Installed) { Fill(r, t, s); any = true; }
        if (any) return;

        // هنوز تمی نصب نشده (آزمون‌های بی‌تم): همان قاعده روی منابعِ خودِ برنامه
        Avalonia.Controls.IResourceDictionary root = app.Resources;
        var picked = Parse(s.TableBorderColor);
        root["Pump.Table.Border"] = picked is { } c
            ? new SolidColorBrush(c)
            : (root.TryGetResource("Pump.Border", app.ActualThemeVariant, out var b) && b is IBrush br
                 ? br : new SolidColorBrush(Colors.Gray));
        var line = Clamp(s.TableLine, 1);
        var head = Clamp(s.TableHeadLine, 2);
        var sum = Clamp(s.TableSumLine, 2);
        root["Pump.Table.Line"] = line;
        root["Pump.Table.HeadLine"] = head;
        root["Pump.Table.HeadUnderline"] = new Thickness(0, 0, 0, head);
        root["Pump.Table.SumBorder"] = new Thickness(line, sum, line, line);
    }

    private static double Clamp(double v, double fallback)
        => v is >= 1 and <= 4 ? Math.Round(v) : fallback;
}
