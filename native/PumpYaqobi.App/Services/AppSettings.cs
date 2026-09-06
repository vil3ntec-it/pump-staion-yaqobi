using System.Text.Json;

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
