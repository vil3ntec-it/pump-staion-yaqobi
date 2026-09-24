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

    // ══ ترمزِ حدس زدن ═══════════════════════════════════════════════════════
    //
    //  ⛔ تا ۱۴۰۵/۰۷/۱۲ رمزِ بخش را می‌شد بی‌نهایت بار پشتِ سرِ هم امتحان
    //  کرد — و رمزِ چهاررقمی یعنی ده هزار حدس. حالا پس از پنج اشتباه سی
    //  ثانیه صبر، و هر اشتباهِ بعدی دو برابر. ⚠️ فقط در حافظه است (همان
    //  قاعدهٔ «باز ماندن»)، پس بستن و باز کردنِ برنامه هم دوباره پنج حدس
    //  می‌دهد و بیشتر نه؛ و رمزِ درست شمارنده را صفر می‌کند.

    /// <summary>چند اشتباه بی ترمز.</summary>
    public const int FreeTries = 5;

    /// <summary>نخستین ترمز؛ هر اشتباهِ بعدی دو برابر.</summary>
    public static readonly TimeSpan FirstWait = TimeSpan.FromSeconds(30);

    private readonly Dictionary<string, (int Fails, DateTime Until)> _fails = new();

    /// <summary>ساعت — تزریق‌پذیر تا آزمون بی انتظارِ واقعی جلو برود.</summary>
    public Func<DateTime> Clock { get; set; } = () => DateTime.UtcNow;

    public SectionLockService(SettingsService settings) => _settings = settings;

    /// <summary>چند ثانیه تا اجازهٔ امتحانِ بعدی — صفر یعنی همین حالا.</summary>
    public int WaitSeconds(string id)
    {
        lock (_fails)
        {
            if (!_fails.TryGetValue(id, out var f)) return 0;
            var left = f.Until - Clock();
            return left <= TimeSpan.Zero ? 0 : (int)Math.Ceiling(left.TotalSeconds);
        }
    }

    private void Failed(string id)
    {
        lock (_fails)
        {
            var fails = (_fails.TryGetValue(id, out var f) ? f.Fails : 0) + 1;
            var until = DateTime.MinValue;
            if (fails >= FreeTries)
            {
                var times = Math.Min(fails - FreeTries, 10);     // سقف: چند ساعت (۳۰ث × ۲¹⁰)، نه بی‌نهایت
                until = Clock() + TimeSpan.FromTicks(FirstWait.Ticks * (1L << times));
            }
            _fails[id] = (fails, until);
        }
    }

    private void Succeeded(string id)
    {
        lock (_fails) _fails.Remove(id);
    }

    /// <summary>نتیجهٔ سنجشِ رمزِ فعلی.</summary>
    public enum Check { Ok, Wrong, Wait }

    /// <summary>
    /// رمزِ فعلیِ همین بخش — <b>یا رمزِ خودِ برنامه</b> — درست است؟ با ترمز.
    /// بخشِ بی‌رمز همیشه <see cref="Check.Ok"/>.
    ///
    /// ⚠️ رمزِ برنامه هم پذیرفته است: صاحبِ پمپی که رمزِ بخش را فراموش کرده
    /// نباید برای همیشه بیرونِ «مفاد» بماند — و کسی که رمزِ برنامه را دارد
    /// از قبل همه‌چیز را دارد.
    /// </summary>
    public Check Authorize(string id, string current, Func<string, bool>? appPassword = null)
    {
        if (!HasPassword(id)) return Check.Ok;
        if (WaitSeconds(id) > 0) return Check.Wait;
        var ok = PasswordHasher.Verify(current ?? "", Hash(id))
                 || (appPassword is not null && (current ?? "").Length > 0 && appPassword(current!));
        if (ok) { Succeeded(id); return Check.Ok; }
        Failed(id);
        return Check.Wrong;
    }

    /// <summary>
    /// ⛔ <b>عوض کردنِ رمزی که هست، رمزِ فعلی می‌خواهد.</b> تا ۱۴۰۵/۰۷/۱۲ هر
    /// کسی که پای برنامهٔ باز می‌نشست (مثلاً کارمندی که صاحبِ پمپ برایش
    /// بازش گذاشته) می‌توانست رمزِ «مفاد» را عوض کند یا بردارد و بعد خودش
    /// ببیندش. بخشِ بی‌رمز همان‌طور بی رمزِ فعلی رمز می‌گیرد.
    /// </summary>
    public Check ChangePassword(string id, string current, string next, Func<string, bool>? appPassword = null)
    {
        var c = Authorize(id, current, appPassword);
        if (c != Check.Ok) return c;
        SetPassword(id, next);
        return Check.Ok;
    }

    /// <summary>برداشتنِ قفل — با رمزِ فعلیِ بخش یا رمزِ برنامه.</summary>
    public Check RemovePassword(string id, string current, Func<string, bool>? appPassword = null)
    {
        var c = Authorize(id, current, appPassword);
        if (c != Check.Ok) return c;
        ClearPassword(id);
        return Check.Ok;
    }

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

    /// <summary>
    /// درست بود ⇒ تا بسته شدنِ برنامه باز می‌ماند.
    /// ⚠️ با همان ترمزِ حدس زدن: در زمانِ ترمز، حتی رمزِ درست هم سنجیده
    /// نمی‌شود (<see cref="WaitSeconds"/> می‌گوید چقدر مانده).
    /// </summary>
    public bool Unlock(string id, string password)
    {
        var h = Hash(id);
        if (h.Length == 0) return true;
        if (WaitSeconds(id) > 0) return false;
        if (!PasswordHasher.Verify(password, h)) { Failed(id); return false; }
        Succeeded(id);
        _open.Add(id);
        return true;
    }

    /// <summary>بی رمز زدن هم می‌شود بست — برای «همین حالا دوباره قفل کن».</summary>
    public void Relock(string id) => _open.Remove(id);
}
