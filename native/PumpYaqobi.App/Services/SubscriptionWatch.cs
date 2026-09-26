using PumpYaqobi.Application.Localization;

namespace PumpYaqobi.App.Services;

/// <summary>
/// هم‌خوانیِ «سرور چه می‌گوید» با «این کامپیوتر چه دارد» — یک جوابِ خالص.
/// </summary>
/// <param name="Agree">هر دو یک چیز می‌گویند.</param>
/// <param name="Server">حرفِ سرور، برای نمایش («VIP تا ۱۴۰۶/۰۷/۱۴»).</param>
/// <param name="Local">حالِ مجوزِ امضاشدهٔ همین کامپیوتر، برای نمایش.</param>
/// <param name="Why">چرا یکی نیستند — خالی یعنی یکی‌اند.</param>
public sealed record SubVerdict(bool Agree, string Server, string Local, string Why)
{
    public static readonly SubVerdict Unknown = new(true, "", "", "");
}

/// <summary>
/// ══ 🤖 پیگیرِ اشتراک ═══════════════════════════════════════════════════════
///
/// خواستهٔ صریحِ صاحب ریپو (۱۴۰۵/۰۷/۱۴): «یک ربات بزار داخلِ برنامه که با
/// دقت هر حسابِ کاربر رو چک کنه؛ اشتراکی که سرور براش می‌ده — VIP یا دائمی —
/// رو پی‌گیری کنه و به برنامه برسونه، و دوره‌های آزمایشی رو هم ببینه… به یک
/// حساب اشتراک دادم، برنامه گفت تمدید شد ولی همون مدل قفل بود و ماندهٔ
/// اشتراک رو نمی‌گفت… و اگه آفلاین کار کنه و اشتراک تمدید شد، خودکار همون
/// بخش‌هایی که قفل بودن باز بشن.»
///
/// ── کارش ───────────────────────────────────────────────────────────────────
/// <code>
///   هر دورِ پس‌زمینه (و همین حالا با: پیامِ سرور · برگشتنِ اینترنت · کلیک)
///     حرفِ سرور  (/api/pump/me: فعال؟ آزمایشی یا پولی؟ تا کِی؟)
///     مجوزِ این‌جا (امضاشده، روی دیسک)
///       یکی‌اند ⇒ هیچ کاری، صفر درخواستِ اضافه
///       نیستند ⇒ ۱) مجوزِ تازه  ۲) نشد ⇒ وصلِ دوبارهٔ همین کامپیوتر به پمپِ
///                 حساب  ۳) باز نشد ⇒ **دلیلِ واقعی** در پروفایل، و دورِ بعد
///                 دوباره (۱ · ۲ · ۵ · ۱۰ دقیقه)
/// </code>
///
/// ⛔ <b>تا امروز فقط «باز/بسته» سنجیده می‌شد.</b> «آزمایشی ⇒ VIP» و
/// «VIPِ یک‌ساله ⇒ تمدید» هر دو طرف را «باز» نگه می‌دارند؛ اگر یک بار
/// گرفتنِ مجوز شکست می‌خورد (خطای سرور، قطعیِ لحظه‌ای)، مجوزِ کهنه ده دقیقه
/// و بیشتر همان «آزمایشی» می‌ماند و هیچ‌کس نمی‌فهمید چرا. حالا پلن و تاریخِ
/// پایان هم سنجیده می‌شوند.
///
/// ⛔ <b>هیچ تصمیمِ قفلی این‌جا گرفته نمی‌شود</b> — تنها جای آن
/// <see cref="Entitlements"/> است. این فقط مجوز را می‌رساند و راست می‌گوید.
///
/// ⛔ <b>آفلاین</b>: سرور چیزی نگفته، پس هیچ مقایسه‌ای نیست و هیچ چیزی پاک
/// نمی‌شود. ولی مرزهای خودِ مجوز (پایانِ مجوز، پایانِ اشتراک، پایانِ ارفاق)
/// بی اینترنت هم می‌رسند: <see cref="LocalTick"/> همان لحظه قفل‌ها را از نو
/// می‌خواند — بی هیچ درخواست و بی هیچ دستورِ دیتابیسی.
/// </summary>
public static class SubscriptionWatch
{
    /// <summary>دو تاریخِ پایان بیش از این فرق داشته باشند ⇒ «تمدید نرسیده».</summary>
    public const long EndsSlackMs = 86_400_000;

    /// <summary>فاصلهٔ تلاش‌ها وقتی ناهم‌خوانی ماند: ۰ · ۱ · ۲ · ۵ · ۱۰ دقیقه.</summary>
    public static readonly TimeSpan[] Backoff =
    {
        TimeSpan.Zero, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(2),
        TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(10),
    };

    private static readonly object Gate = new();
    private static int _fails;
    private static DateTime _nextTry = DateTime.MinValue;
    private static long _nextBoundary;
    private static bool _boundaryKnown;

    /// <summary>چند بار پشتِ سرِ هم درست کردن نشد.</summary>
    public static int Fails { get { lock (Gate) return _fails; } }

    /// <summary>آخرین جوابِ پیگیر — پروفایل از همین می‌خواند.</summary>
    public static SubVerdict Last { get; private set; } = SubVerdict.Unknown;

    /// <summary>آخرین بار که حرفِ سرور واقعاً شنیده و سنجیده شد (ساعتِ محلی).</summary>
    public static DateTime LastCheckAt { get; private set; }

    /// <summary>آخرین باری که پیگیر خودش مجوزِ تازه آورد و درست شد.</summary>
    public static DateTime LastFixAt { get; private set; }

    /// <summary>چیزی برای نشان دادن عوض شد.</summary>
    public static event Action? Changed;

    /// <summary>
    /// مرزِ مجوز رد شد (آفلاین هم) — قفل‌ها باید از نو خوانده شوند.
    /// پوسته همان کارِ «مجوزِ تازه رسید» را می‌کند.
    /// </summary>
    public static event Action? BoundaryCrossed;

    // ── جوابِ خالص ────────────────────────────────────────────────────

    /// <summary>
    /// سرور و این کامپیوتر یک چیز می‌گویند؟ خالص — آزمون از همین می‌رود.
    /// </summary>
    public static SubVerdict Compare(PumpSubscription server, LicenseCheck local, long now)
    {
        var s = Describe(server);
        var l = Describe(local, now);

        if (!server.Active && !local.Valid) return new(true, s, l, "");
        if (server.Active && !local.Valid)
            return new(false, s, l, "سرور اشتراک را فعال می‌گوید ولی مجوزِ این کامپیوتر هنوز نرسیده"
                + (local.Reason.Length > 0 ? " (" + local.Reason + ")" : ""));
        if (!server.Active && local.Valid)
            return new(false, s, l, "سرور اشتراک را برداشته ولی مجوزِ کهنه هنوز روی این کامپیوتر است");

        //  هر دو فعال — ولی همان اشتراک؟
        var serverTrial = server.Source == "trial";
        var localTrial = IsTrial(local.PlanTitle);
        if (serverTrial != localTrial)
            return new(false, s, l, serverTrial
                ? "سرور دورهٔ آزمایشی می‌گوید ولی این‌جا اشتراکِ دیگری است"
                : "اشتراکِ تازه روی سرور هست ولی این کامپیوتر هنوز دورهٔ آزمایشی را دارد");

        //  ⚠️ دائمی را سرور با تاریخِ خیلی دور می‌دهد، پس همین سنجش هر دو را
        //  می‌گیرد. تاریخِ نیامده (صفر) چیزی را نمی‌سنجد — حدس نمی‌زنیم.
        if (server.EndsAt > 0 && local.SubscriptionEndsAt > 0
            && Math.Abs(server.EndsAt - local.SubscriptionEndsAt) > EndsSlackMs)
            return new(false, s, l, server.EndsAt > local.SubscriptionEndsAt
                ? "تمدیدِ اشتراک روی سرور هست ولی هنوز به این کامپیوتر نرسیده"
                : "سرور پایانِ اشتراک را زودتر می‌گوید تا مجوزِ این کامپیوتر");

        return new(true, s, l, "");
    }

    public static bool IsTrial(string planTitle) => (planTitle ?? "").Contains("آزمایشی");

    /// <summary>«VIP تا ۱۴۰۶/۰۷/۱۴» — حرفِ سرور.</summary>
    public static string Describe(PumpSubscription s)
    {
        if (!s.Active) return "اشتراکِ فعالی ندارد";
        var kind = s.Source == "trial" ? "آزمایشی" : KindOf(s.PlanTitle);
        return kind + Until(s.EndsAt);
    }

    /// <summary>«VIP تا ۱۴۰۶/۰۷/۱۴» — مجوزِ این کامپیوتر.</summary>
    public static string Describe(LicenseCheck c, long now)
    {
        if (!c.Valid) return c.SignatureOk && c.Expired ? "مجوزِ این‌جا منقضی شده" : "مجوزِ فعالی ندارد";
        return (IsTrial(c.PlanTitle) ? "آزمایشی" : KindOf(c.PlanTitle)) + Until(c.SubscriptionEndsAt);
    }

    private static string KindOf(string plan) => (plan ?? "").Trim() switch
    {
        "" or "vip" => "VIP",
        "std" or "standard" => "استاندارد",
        "perm" or "permanent" => "دائمی",
        var t => t,
    };

    private static string Until(long endsAt)
    {
        if (endsAt <= 0) return "";
        var left = endsAt - DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if (left > 3650L * 86_400_000) return " (دائمی)";
        var d = DateTimeOffset.FromUnixTimeMilliseconds(endsAt).LocalDateTime;
        return " تا " + Shamsi.Of(d);
    }

    // ── یک دور ────────────────────────────────────────────────────────

    /// <summary>وقتِ تلاشِ دوباره رسیده؟ (پس از ناهم‌خوانی)</summary>
    public static bool FixDue(bool force)
    {
        lock (Gate) return force || DateTime.UtcNow >= _nextTry;
    }

    /// <summary>حرفِ سرور شنیده و سنجیده شد — ثبت، و اگر چیزی عوض شد خبر.</summary>
    public static void Report(SubVerdict v, bool triedFix, string fixWhy = "")
    {
        lock (Gate)
        {
            LastCheckAt = DateTime.Now;
            if (v.Agree)
            {
                if (triedFix || _fails > 0) LastFixAt = DateTime.Now;
                _fails = 0;
                _nextTry = DateTime.MinValue;
            }
            else if (triedFix)
            {
                _fails++;
                _nextTry = DateTime.UtcNow + Backoff[Math.Min(_fails, Backoff.Length - 1)];
            }
            var why = v.Agree || fixWhy.Length == 0 || v.Why.Contains(fixWhy) ? v.Why : v.Why + " — " + fixWhy;
            Last = v with { Why = why };
        }
        _boundaryKnown = false;     // مجوز شاید عوض شد
        try { Changed?.Invoke(); } catch { /* نمایش رفاه است */ }
    }

    /// <summary>یک جمله برای پروفایل.</summary>
    public static string Line()
    {
        var v = Last;
        if (LastCheckAt == default) return "🤖 پیگیرِ اشتراک هنوز با سرورِ حساب حرف نزده — با وصل شدنِ اینترنت خودش می‌پرسد.";
        var at = $"آخرین بررسی {LastCheckAt:HH:mm}";
        if (v.Agree)
            return $"🤖 پیگیرِ اشتراک: سرور و این کامپیوتر یکی‌اند — {v.Server}. {at}"
                + (LastFixAt != default && (DateTime.Now - LastFixAt).TotalHours < 24 ? $" · خودش رساند {LastFixAt:HH:mm}" : "");
        return $"🤖 پیگیرِ اشتراک: {v.Why}.\nسرور: {v.Server} · این کامپیوتر: {v.Local}\n{at} — خودش دوباره امتحان می‌کند.";
    }

    // ── آفلاین: مرزهای خودِ مجوز ─────────────────────────────────────

    /// <summary>
    /// هر تیکِ پنج‌ثانیه‌ایِ حلقه. تا مرزِ بعدی نرسیده <b>هیچ کاری</b>
    /// نمی‌کند (نه خواندنِ فایل، نه امضا). مرز = نزدیک‌ترینِ پایانِ مجوز،
    /// پایانِ اشتراک و پایانِ ارفاق. رسید ⇒ <see cref="BoundaryCrossed"/>.
    /// </summary>
    public static void LocalTick(Func<long> now, Func<(long Exp, long SubEnds, long GraceEnds)> read)
    {
        var t = now();
        if (_boundaryKnown && t < _nextBoundary) return;
        var crossed = _boundaryKnown;
        var (exp, subEnds, grace) = read();
        var next = long.MaxValue;
        foreach (var b in new[] { exp, subEnds, grace })
            if (b > t && b < next) next = b;
        _nextBoundary = next;
        _boundaryKnown = true;
        if (crossed)
            try { BoundaryCrossed?.Invoke(); } catch { /* نمایش رفاه است */ }
    }

    /// <summary>مرز را از نو بخوان (مجوزِ تازه رسید).</summary>
    public static void Forget() => _boundaryKnown = false;

    /// <summary>⚠️ فقط برای آزمون.</summary>
    public static void Reset()
    {
        lock (Gate)
        {
            _fails = 0; _nextTry = DateTime.MinValue; _boundaryKnown = false; _nextBoundary = 0;
            Last = SubVerdict.Unknown; LastCheckAt = default; LastFixAt = default;
        }
    }
}
