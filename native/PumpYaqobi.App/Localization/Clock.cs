using PumpYaqobi.Application.Localization;

namespace PumpYaqobi.App.Localization;

/// <summary>
/// ساعتِ سربرگ. فقط ساعت — تاریخ خطِ بالای همین بلوک است (مثلِ نسخهٔ وب که
/// ‎#headerDate‎ و خطِ دومش جدا هستند). پیش از این تاریخ در هر دو خط تکرار
/// می‌شد.
/// </summary>
public static class Clock
{
    public static string Now() => $"{DateTime.Now:HH:mm:ss}";
}
