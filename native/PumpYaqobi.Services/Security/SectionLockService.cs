using PumpYaqobi.Application.Security;
using PumpYaqobi.Services.Data;

namespace PumpYaqobi.Services.Security;

/// <summary>
/// ══ رمزِ بخش‌های حساس — «مفاد» و «ضرر» ══════════════════════════════════════
///
/// خواستهٔ صریحِ صاحب ریپو (۱۴۰۵/۰۶/۲۸): «تو این بخش بشه رمزِ برنامه رو عوض
/// کرد، رمز برای مفاد و ضرر تعیین کرد.» نسخهٔ HTML هم همین را داشت — بخشِ
/// «مفاد/ضرر» رمزِ جدا داشت (<see cref="Permission.ViewProfit"/>).
///
/// ⚠️ **رمز هیچ‌وقت خام ذخیره نمی‌شود.** فقط همان رشتهٔ ‎pbkdf2$sha256$…‎ی
/// <see cref="PasswordHasher"/> در جدولِ ‎Settings‎ می‌نشیند — همان چیزی که
/// رمزِ خودِ برنامه هم با آن نگه داشته می‌شود. پس دیدنِ دیتابیس هم رمز را
/// لو نمی‌دهد.
///
/// ⚠️ **باز ماندن فقط تا بسته شدنِ برنامه است** و در حافظه می‌ماند، نه روی
/// دیسک: یک‌بار رمز زدن برای همان اجرا بس است، ولی اجرای بعدی دوباره
/// می‌پرسد. اگر روی دیسک می‌نشست، قفل با یک بار باز کردن برای همیشه
/// می‌رفت.
/// </summary>
public sealed class SectionLockService
{
    /// <summary>بخشِ «مفاد / ضرر / اتحادیه».</summary>
    public const string Profit = "profit";

    /// <summary>زیربخشِ «زیان ناشی از افزایش قیمت».</summary>
    public const string PriceLoss = "priceloss";

    private const string Prefix = "lock.";

    /// <summary>فقط این دو بخش قفل می‌پذیرند — قفلِ دلبخواه روی هر بخشی نه.</summary>
    public static readonly string[] Ids = { Profit, PriceLoss };

    public static string TitleOf(string id) => id switch
    {
        Profit => "مفاد / ضرر / اتحادیه",
        PriceLoss => "زیان ناشی از افزایش قیمت",
        _ => id,
    };

    private readonly SettingsService _settings;
    private readonly HashSet<string> _open = new();

    public SectionLockService(SettingsService settings) => _settings = settings;

    public static bool Lockable(string? id) => id is not null && Array.IndexOf(Ids, id) >= 0;

    private string Hash(string id) => _settings.GetString(Prefix + id);

    /// <summary>این بخش رمز دارد؟</summary>
    public bool HasPassword(string id) => Lockable(id) && Hash(id).Length > 0;

    /// <summary>همین حالا باید رمز پرسیده شود؟ (رمز دارد و هنوز باز نشده.)</summary>
    public bool NeedsUnlock(string id) => HasPassword(id) && !_open.Contains(id);

    /// <summary>رمزِ تازه. رمزِ خالی یعنی «قفل بردار».</summary>
    public void SetPassword(string id, string password)
    {
        if (!Lockable(id)) throw new InvalidOperationException("این بخش قفل نمی‌پذیرد: " + id);
        _settings.Set(Prefix + id, PasswordHasher.Hash(password));
        _open.Remove(id);
    }

    public void ClearPassword(string id)
    {
        if (!Lockable(id)) return;
        _settings.Set(Prefix + id, "");
        _open.Remove(id);
    }

    /// <summary>درست بود ⇒ تا بسته شدنِ برنامه باز می‌ماند.</summary>
    public bool Unlock(string id, string password)
    {
        var h = Hash(id);
        if (h.Length == 0) return true;
        if (!PasswordHasher.Verify(password, h)) return false;
        _open.Add(id);
        return true;
    }

    /// <summary>بی رمز زدن هم می‌شود بست — برای «همین حالا دوباره قفل کن».</summary>
    public void Relock(string id) => _open.Remove(id);
}
