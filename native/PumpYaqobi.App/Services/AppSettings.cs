using System.Text.Json;
using PumpYaqobi.Reporting.Pdf;

namespace PumpYaqobi.App.Services;

/// <summary>
/// تنظیماتِ سبکِ خودِ برنامه (تم، آخرین بخش، اندازهٔ پنجره).
/// کنارِ دیتابیس در پوشهٔ کاربر می‌نشیند تا بکاپِ دیتابیس آن را با خود نبرد.
/// </summary>
public sealed class AppSettings
{
    public string ThemeId { get; set; } = "dark-amber";
    public string LastSection { get; set; } = "dashboard";
    public double WindowWidth { get; set; } = 1440;
    public double WindowHeight { get; set; } = 900;
    public bool WindowMaximized { get; set; } = true;

    /// <summary>
    /// «تنظیمِ ورق»ِ چاپ — همتای ‎pumpPrintStudio_v4‎ در ‎localStorage‎ی نسخهٔ وب.
    /// کنارِ خودِ برنامه می‌نشیند، نه در دیتابیس: مالِ همین دستگاه است و
    /// بکاپِ حساب‌ها نباید آن را با خود ببرد.
    /// </summary>
    public PageSetup? PrintSetup { get; set; }

    /// <summary>
    /// ══ ظاهرِ جدول‌ها ═══════════════════════════════════════════════════════
    /// خواستهٔ صریحِ صاحب ریپو: «رنگ و ضخامتِ خطِ جدول نباید سفت و سخت داخلِ کد
    /// نوشته شده باشد؛ در تنظیمات باشد و روی **همهٔ** جدول‌های برنامه بنشیند.»
    ///
    /// پس این چهار عدد تنها جای تعریفِ خطِ جدول‌اند و از این‌جا به شکلِ منبعِ
    /// پویا (<c>Pump.Table.…</c>) پخش می‌شوند — ‎Themes/TableStyle.cs‎.
    /// کنارِ خودِ برنامه می‌نشینند نه در دیتابیس: مالِ همین دستگاه‌اند.
    /// </summary>
    /// <remarks>خالی یعنی «رنگِ خودِ تم» — همان چیزی که تا امروز بوده.</remarks>
    public string TableBorderColor { get; set; } = "";

    /// <summary>ضخامتِ خطِ بینِ خانه‌ها (۱ تا ۴ پیکسل).</summary>
    public double TableLine { get; set; } = 1;

    /// <summary>ضخامتِ خطِ زیرِ سربرگِ جدول.</summary>
    public double TableHeadLine { get; set; } = 2;

    /// <summary>ضخامتِ خطِ بالای ردیفِ «جمله».</summary>
    public double TableSumLine { get; set; } = 2;

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    /// <summary>
    /// جای فایلِ تنظیمات. ابزارِ عکس‌گیری و آزمون‌ها آن را به یک پوشهٔ موقت
    /// می‌برند تا هرگز تنظیماتِ واقعیِ کاربر را نخوانند و ننویسند.
    /// </summary>
    public static string? DirOverride { get; set; }

    public static string Dir => DirOverride ?? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PumpYaqobi");

    private static string File_ => Path.Combine(Dir, "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(File_))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(File_)) ?? new AppSettings();
        }
        catch { /* تنظیماتِ خراب هرگز نباید جلوی باز شدنِ برنامه را بگیرد */ }
        return new AppSettings();
    }

    /// <summary>تنظیمِ ورقِ ذخیره‌شده — نبود، همان پیش‌فرضِ همیشگی.</summary>
    public static PageSetup LoadPrintSetup() => Load().PrintSetup ?? PageSetup.Default;

    /// <summary>
    /// فقط تنظیمِ ورق را عوض می‌کند و بقیهٔ فایل را دست‌نخورده نگه می‌دارد —
    /// وگرنه تم و «آخرین بخش» پاک می‌شدند.
    /// </summary>
    public static void SavePrintSetup(PageSetup setup)
    {
        var a = Load();
        a.PrintSetup = setup;
        a.Save();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Dir);
            File.WriteAllText(File_, JsonSerializer.Serialize(this, Json));
        }
        catch { }
    }
}
