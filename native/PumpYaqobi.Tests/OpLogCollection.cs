using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ⛔ کلاس‌هایی که کلیدِ <b>سراسریِ</b> ‎OpLog.Enabled‎ را خاموش می‌کنند با هم
/// سریال می‌دوند: xUnit کلاس‌ها را موازی اجرا می‌کند و خاموش شدنِ آن کلید در یک
/// کلاس، opهای ذخیره‌های کلاسِ دیگر را بی‌صدا می‌بلعید — همان مسابقه‌ای که
/// خودِ برنامه با ‎PumpDbContext.SuppressOps‎ (برای هر اتصال جدا) از آن بیرون آمد.
/// </summary>
[CollectionDefinition(Name)]
public sealed class OpLogCollection
{
    public const string Name = "OpLog.Enabled (سراسری)";
}
