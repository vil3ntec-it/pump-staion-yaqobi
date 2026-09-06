namespace PumpYaqobi.Domain.Enums;

/// <summary>
/// واحدِ یک حساب: «تیل» یا «پول».
///
/// ⚠️ اینها دو دفترِ کاملاً جدا هستند (قاعدهٔ صریحِ CLAUDE.md پروژه):
/// هر حساب ردیف‌های خودش را دارد و جمع‌های یکی نباید در الباقیِ دیگری بیاید.
/// در HTML این با رشتهٔ 'money' / 'fuel' روی خودِ شیء نگه داشته می‌شد.
/// </summary>
public enum LedgerMode
{
    Fuel = 1,
    Money = 2
}

public static class LedgerModeExtensions
{
    /// <summary>‎_acctModeMoney‎ در HTML: هر چیزی جز 'money' یعنی تیل.</summary>
    public static LedgerMode FromLegacy(string? raw) =>
        raw == "money" ? LedgerMode.Money : LedgerMode.Fuel;

    public static string ToLegacy(this LedgerMode m) => m == LedgerMode.Money ? "money" : "fuel";
    public static bool IsMoney(this LedgerMode m) => m == LedgerMode.Money;
}
