namespace PumpYaqobi.Services.Data;

/// <summary>
/// ══ کارهای سنگینِ پس از ورود، پشتِ سرِ هم — نه همه با هم (۱۴۰۵/۰۷/۲۲) ══
/// بندِ ۲ِ کندی‌ها: درست کردنِ فروشِ ورق‌های قدیم، سلامتِ دفتر، سنجهٔ برابری و عکسِ روزانه همه
/// همان لحظهٔ ورود راه می‌افتادند — درست وقتی کاربر منتظرِ صفحهٔ اول است. حالا هر کدام چند ثانیه
/// پس از ورود و با فاصله از هم. ⛔ خودِ کارها دست نخوردند؛ فقط زمانِ شروعشان.
/// <para>سنجه‌ها <see cref="Factor"/> را صفر می‌گذارند تا همان رفتارِ پیشین (بی مکث) را ببینند.</para>
/// </summary>
public static class StartDelay
{
    /// <summary>ضریبِ همهٔ مکث‌ها — ۱ در برنامه، ۰ در سنجه‌ها.</summary>
    public static double Factor { get; set; } = 1;

    public static TimeSpan Seconds(double s) => TimeSpan.FromSeconds(Math.Max(0, s * Factor));

    /// <summary>پس از مکث، کار را روی نخِ دیگر می‌دواند (مکثِ صفر ⇒ همان لحظه).</summary>
    public static Task<T> After<T>(TimeSpan wait, Func<Task<T>> work) =>
        Task.Run(async () =>
        {
            if (wait > TimeSpan.Zero) await Task.Delay(wait);
            return await work();
        });
}
