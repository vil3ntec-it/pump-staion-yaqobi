namespace PumpYaqobi.App.Services;

/// <summary>یک گامِ چک‌لیستِ «شروعِ سریع».</summary>
/// <param name="Id">شناسهٔ ثابت — سنجه‌ها و «نشانم بده» با همین می‌روند.</param>
/// <param name="Section">بخشی که «نشانم بده» باز می‌کند.</param>
/// <param name="Spot">کادری که برجسته می‌شود (<see cref="PumpYaqobi.App.Controls.Spot"/>).</param>
public sealed record QuickStep(string Id, string Title, string Hint, bool Done, string Section, string Spot)
{
    public string Mark => Done ? "✅" : "⬜";
}

/// <summary>
/// ══ شروعِ سریع (شورا، ث۱) ═══════════════════════════════════════════════════
/// پنج گام، به ترتیبی که پمپ‌دارِ تازه باید برود: حساب ⇐ نامِ پمپ ⇐ ظرفیتِ
/// مخزن ⇐ نخستین پارچه ⇐ نخستین ورق.
///
/// ⛔ هر «شد» از **حقیقتِ همان لحظه** است، نه از «دکمه را زد»: حساب از توکنِ
/// روی دیسک، نام از تنظیمات، مخزن از ظرفیتِ ذخیره‌شده، پارچه و ورق از وجودِ
/// دستِ‌کم یک ردیف. پس کسی که از راهِ دیگری همان کار را کرده، تیکش را می‌گیرد.
///
/// ⛔ فقط می‌گوید و راه نشان می‌دهد — هیچ چیزی نمی‌نویسد، هیچ بخشی را نمی‌بندد.
/// </summary>
public static class QuickStart
{
    public const string Account = "account", PumpName = "pumpname", Capacity = "capacity",
                        Parcha = "parcha", Waraq = "waraq";

    /// <summary>قاعده، خالص.</summary>
    public static IReadOnlyList<QuickStep> Steps(bool account, bool pumpName, bool capacity, bool parcha, bool waraq) =>
        new[]
        {
            new QuickStep(Account, "ساختنِ حساب", "با ایمیل — اشتراک و پشتیبان روی همین حساب می‌نشینند",
                account, "account", "login-email"),
            new QuickStep(PumpName, "نامِ پمپ", "همان نامی که سرِ همهٔ برگه‌ها و PDFها می‌آید",
                pumpName, "account", "pump-name"),
            new QuickStep(Capacity, "ظرفیتِ مخزن", "تا هشدارِ «کم است» و درصدِ پرشدگی درست باشد",
                capacity, "storage", "tank-capacity"),
            new QuickStep(Parcha, "نخستین پارچه", "کارتِ روز: کارمند، پایه، شروع و ختم — Enter ذخیره می‌کند",
                parcha, "shifts", "parcha-day"),
            //  ⚠️ خودِ ورق را پارچه می‌سازد (‎ShiftWaraqSync‎)، پس «ورق هست» از گامِ ۴
            //  جدا نبود؛ کارِ واقعیِ ورق نوشتنِ ردیفِ قرض یا مصرف است.
            new QuickStep(Waraq, "نخستین ردیفِ ورق", "«اضافه کردن» با امروز ⇒ ورقِ همان روز؛ یک ردیفِ قرض یا مصرف بنویسید",
                waraq, "waraq", "waraq-new"),
        };

    /// <summary>همه شد؟</summary>
    public static bool AllDone(IEnumerable<QuickStep> steps) => steps.All(s => s.Done);

    /// <summary>نامِ پمپ «نوشته شده» یعنی چیزی جز نامِ پیش‌فرض.</summary>
    public static bool HasPumpName(string? stationName) =>
        PumpYaqobi.Application.Localization.PumpBrand.Of(stationName)
        != PumpYaqobi.Application.Localization.PumpBrand.Default;
}
