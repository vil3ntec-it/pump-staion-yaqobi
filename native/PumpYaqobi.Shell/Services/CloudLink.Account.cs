using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using PumpYaqobi.Application.Localization;
using PumpYaqobi.Domain;

namespace PumpYaqobi.App.Services;

/// <summary>تکه‌ای از <see cref="CloudLink"/> — ورود، ثبت‌نام، چراغِ ابر و رمزِ فراموش‌شده.</summary>
public sealed partial class CloudLink
{
    // ── حساب: ورود با گوگل ─────────────────────────────────────────────

    /// <summary>آیا با حسابِ گوگل وارد شده‌ایم.</summary>
    public bool SignedIn => !string.IsNullOrWhiteSpace(_settings.CloudAccountToken);

    /// <summary>ایمیلِ حساب — برای نشان دادن در صفحهٔ حساب.</summary>
    public string Email => _settings.CloudEmail;

    /// <summary>نامِ حساب — برای نشان دادن در صفحهٔ حساب.</summary>
    public string Name => _settings.CloudName;

    /// <summary>
    /// شناسهٔ کلاینتِ گوگل را از خودِ سرور می‌پرسد.
    ///
    /// ⚠️ از داخلِ کد نمی‌آید تا عوض شدنش نسخهٔ تازهٔ برنامه نخواهد — همان
    /// قاعده‌ای که اپِ کارمندان هم دارد.
    /// </summary>
    public async Task<string> GoogleClientIdAsync(CancellationToken ct = default)
    {
        var (ok, json, _, _) = await GetAsync("/api/config", null, ct);
        if (!ok) return "";
        //  شناسهٔ Desktop مالِ همین برنامه است؛ شناسهٔ Web مالِ سایت و اپِ
        //  کارمندان. اگر سرور هنوز جدا نکرده باشد، به همان یکی برمی‌گردیم.
        var desktop = Str(json, "googleDesktopClientId");
        return desktop.Length > 0 ? desktop : Str(json, "googleClientId");
    }

    /// <summary>
    /// توکنِ هویتِ گوگل را به سرور می‌دهد و نشستِ حساب می‌گیرد.
    /// </summary>
    public async Task<CloudResult> SignInAsync(string idToken, CancellationToken ct = default)
    {
        var (ok, json, why, code) = await PostAsync(
            "/api/auth/google", new { idToken, app = "pump" }, null, ct);
        if (!ok) return CloudResult.No(why, code);

        return await SeatAsync(json);
    }

    /// <summary>
    /// نشستی که سرور داد را می‌نشاند.
    ///
    /// ⚠️ **سرور `accessToken` می‌دهد، نه `token`** — و برنامه تا امروز فقط
    /// دنبالِ `token` بود، پس هر ورودِ **موفقی** را «سرور نشست نداد»
    /// می‌خواند. با آزمونِ واقعیِ سرور دیده شد
    /// (`shop/server/test/pump-account.test.js`). هر دو نام خوانده می‌شوند
    /// تا نسخهٔ قدیمیِ سرور هم کار کند.
    /// </summary>
    /// <summary>
    /// ══ «این کامپیوتر حالا مالِ حسابِ دیگری است» ═════════════════════════
    ///
    /// ⛔ <b>نشتی که این می‌بندد</b> (بندِ ۱۲ی خواستهٔ صاحب ریپو: «اگر یک
    /// حساب از دستگاه دیگری وارد شود… اطلاعاتِ حسابِ قبلی نباید باقی
    /// بماند، با حسابِ جدید مخلوط نشود، و توکنِ حسابِ قبلی اشتباهاً برای
    /// حسابِ جدید استفاده نشود»): تا امروز ورودِ یک حسابِ <b>دیگر</b> روی
    /// همین نصب فقط چهار فیلدِ حساب را عوض می‌کرد و بندهای پمپِ قبلی
    /// دست‌نخورده می‌ماندند — توکنِ <b>دستگاه</b>، شناسهٔ پمپ، مجوزِ
    /// امضاشده، کلیدِ عمومی، کدِ اپِ کارمندان، نشانی و رمزِ سرورِ خانگی و
    /// مُهرِ ارفاق. نتیجه‌اش سه چیز بود:
    ///   • عکسِ حساب‌ها با توکنِ دستگاهِ پمپِ قبلی به پوشهٔ ابریِ <b>او</b>
    ///     می‌رفت،
    ///   • کدِ اپِ کارمندان و اشتراکِ پمپِ قبلی روی پروفایلِ حسابِ تازه
    ///     دیده می‌شد (و ارفاقش برای این یکی خرج می‌شد)،
    ///   • و <see cref="HomeFromAccountAsync"/> با «این حساب مالِ پمپِ
    ///     دیگری است — دستگاه را جدا کنید» جلوی کاربر دیوار می‌کشید،
    ///     در حالی که <b>هیچ راهی برای جدا کردن نبود</b>:
    ///     <see cref="ForgetStationAsync"/> نوشته شده بود و هیچ‌جا صدا
    ///     زده نمی‌شد. یعنی جابه‌جاییِ پمپ عملاً ناممکن بود.
    ///
    /// ⚠️ <b>فقط با شناسهٔ حساب تصمیم می‌گیریم</b>، و فقط وقتی شناسهٔ کهنه
    /// را <b>داریم</b> و با تازه <b>یکی نیست</b>. پس این سه حالت هیچ چیزی
    /// را باز نمی‌کنند: ورودِ دوبارهٔ همان حساب، نخستین ورودِ یک نصبِ تازه،
    /// و نصبی که شناسه‌اش را ندارد (نسخهٔ پیش از امروز) و ایمیلش هم همان
    /// است. «قفلِ ناخواسته بدتر از بازِ ناخواسته است» این‌جا هم برقرار
    /// است: مشتریِ امروزی نباید با یک به‌روزرسانی از پمپش جدا شود.
    ///
    /// ⚠️ و برای نصبِ کهنه‌ای که شناسه ندارد، <b>ایمیل</b> ملاک است —
    /// همان چیزی که داریم. (ایمیلِ عوض‌شدهٔ همان حساب یک بار بندها را
    /// بی‌دلیل باز می‌کند و کاربر دوباره کدِ شش‌رقمی می‌زند؛ بدترین
    /// حالتش همین است، و دفتر دست نمی‌خورد.)
    ///
    /// ⛔ <b>یک بیت از دفتر لمس نمی‌شود</b> — همان قاعدهٔ
    /// <see cref="ForgetStationAsync"/>.
    /// </summary>
    private async Task ReleaseIfOtherAccountAsync(JsonElement json)
    {
        //  ⚠️ هر ورود از صفر: وگرنه هشدارِ یک جابه‌جاییِ قدیمی سرِ ورودهای
        //  بعدیِ همان حساب هم نشان داده می‌شد.
        AccountSwitched = false;

        if (!json.TryGetProperty("user", out var u) || u.ValueKind != JsonValueKind.Object) return;

        var freshId = Ident(u, "id");
        var freshMail = Str(u, "email").Trim();
        var oldId = (_settings.CloudUserId ?? "").Trim();
        var oldMail = (_settings.CloudEmail ?? "").Trim();

        bool other;
        if (oldId.Length > 0 && freshId.Length > 0)
            other = !string.Equals(oldId, freshId, StringComparison.Ordinal);
        else if (oldMail.Length > 0 && freshMail.Length > 0)
            other = !string.Equals(oldMail, freshMail, StringComparison.OrdinalIgnoreCase);
        else
            other = false;

        if (!other) return;

        //  بندی هست که باز شود؟ نصبی که هیچ‌وقت فعال نشده چیزی ندارد.
        //
        //  ⛔ **و کلیدِ عمومی هم یک بند است.** این خط تا ۱۴۰۵/۰۷/۱۱ سه
        //  موردِ اول را داشت و کلید را نه — و همان یک قلم، دیوارِ
        //  نامرئیِ «هر بار می‌روم پروفایل، دوباره اسمِ پمپ را می‌خواهد»
        //  بود:
        //
        //    ۱) نصبی که یک بار با حسابِ الف کلید را قفل کرده ولی هنوز
        //       توکنِ دستگاه نگرفته بود (bind نیمه‌کاره) ⇒ `bound` دروغ
        //       می‌شد؛
        //    ۲) پس ورود با حسابِ ب `ForgetStationAsync` را **صدا نمی‌زد**
        //       و کلیدِ حسابِ الف سرِ جا می‌ماند؛
        //    ۳) از آن به بعد هر `BindAsync` با `key_mismatch` رد می‌شد —
        //       بی هیچ پیامی، چون `HomeFromAccountAsync` نتیجه‌اش را
        //       می‌بلعد؛
        //    ۴) پس توکنِ دستگاه هیچ‌وقت نمی‌آمد، «فعال نشده» می‌ماند،
        //       مجوز و دورهٔ آزمایشیِ ۳۰ روزه هم هیچ‌وقت صادر نمی‌شد.
        //
        //  یعنی یک قفلِ ضدِ کرک، به جانِ خودِ مشتری افتاده بود.
        var bound = (_settings.CloudDeviceToken ?? "").Length > 0
                 || (_settings.CloudStationId ?? "").Length > 0
                 || (_settings.CloudAccessCode ?? "").Length > 0
                 || (_settings.CloudPublicKey ?? "").Length > 0;

        AccountSwitched = bound;
        if (bound) await ForgetStationAsync();
    }

    /// <summary>
    /// آخرین ورود، حسابِ دیگری بود و بندهای پمپِ قبلی باز شدند.
    ///
    /// ⚠️ برای همین است که هست: قفلی که بی‌صدا باشد در چشمِ کاربر باگ
    /// است. صفحهٔ ورود همین را به او می‌گوید تا بداند چرا باید کدِ
    /// شش‌رقمیِ پمپش را دوباره بزند.
    /// </summary>
    public bool AccountSwitched { get; private set; }

    /// <summary>
    /// چرا آخرین بند شدنِ دستگاه نشد — خالی یعنی مشکلی نبود.
    ///
    /// ⛔ <b>تا ۱۴۰۵/۰۷/۱۱ این دلیل هیچ‌جا نمی‌رفت.</b> حلقهٔ
    /// شصت‌ثانیه‌ای هر دور <c>BindAsync</c> را می‌زد و نتیجه‌اش را دور
    /// می‌ریخت، پس یک شکستِ دائمی (مثلِ <c>key_mismatch</c>) برای همیشه
    /// نامرئی بود و کاربر فقط «سرورِ حساب: فعال نشده» را می‌دید.
    ///
    /// ⚠️ ایستا است چون هر درخواست یک <c>CloudLink</c>ِ تازه می‌سازد —
    /// همان الگوی <see cref="Reach"/>.
    /// </summary>
    public static string LastBindWhy { get; private set; } = "";

    /// <summary>
    /// «حسابِ واردشده روی سرور پمپ دارد؟» — از آخرین جوابِ واقعیِ
    /// <c>/api/pump/me</c>؛ <c>null</c> یعنی هنوز نپرسیده‌ایم.
    ///
    /// ⛔ <b>چراغ و پروفایل از همین می‌خوانند، نه از <c>PumpStepDone</c>ِ روی
    /// دیسک</b> (۱۴۰۵/۰۷/۱۳، سنجهٔ <c>linkstates</c>): آن مُهر می‌گوید «روزی
    /// پمپ داشت»، نه «همین حالا دارد» — و پمپی که از پنل حذف شده بود با مُهرِ
    /// کهنه هیچ‌وقت کارتِ «ساختنِ پمپ» را نشان نمی‌داد. ایستا است، همان
    /// الگوی <see cref="LastBindWhy"/>.
    /// </summary>
    public static bool? AccountHasStation { get; set; }

    /// <summary>پمپِ حساب، همان‌طور که آخرین ‎/api/pump/me‎ی همین نمونه گفت.</summary>
    private string _acctStationSeen = "";

    /// <summary>آخرین ثبتِ ناموفقِ خودکار — ترمزِ حلقه (بالای `bindDue` نوشته چرا).</summary>
    private static DateTime _lastBindFailAt = DateTime.MinValue;

    /// <summary>پس از شکستِ ثبت، حلقهٔ پس‌زمینه تا این مدت دوباره نمی‌زند.</summary>
    public static readonly TimeSpan BindRetryAfterFail = TimeSpan.FromMinutes(10);

    private async Task<CloudResult> SeatAsync(JsonElement json)
    {
        var token = Str(json, "accessToken");
        if (token.Length == 0) token = Str(json, "token");
        if (token.Length == 0) return CloudResult.No("سرور نشست نداد");

        //  ⚠️ **پیش از نشستنِ توکنِ تازه** — وگرنه یک لحظه توکنِ حسابِ تازه
        //  کنارِ توکنِ دستگاهِ حسابِ قبلی می‌نشیند و هر انتشاری که در همان
        //  لحظه بدود به پوشهٔ پمپِ قبلی می‌رود.
        await ReleaseIfOtherAccountAsync(json);

        _settings.CloudAccountToken = token;
        var refresh = Str(json, "refreshToken");
        if (refresh.Length > 0) _settings.CloudRefreshToken = refresh;
        //  ⚠️ عمرِ توکن را از خودِ سرور برمی‌داریم (یک ساعت)، تا کمی پیش از
        //  انقضا خودمان تازه کنیم و کاربر هیچ‌وقت به دیوارِ ۴۰۱ نخورد.
        _settings.CloudAccessExpiresAt = Num(json, "accessExpiresAt");
        if (json.TryGetProperty("user", out var u) && u.ValueKind == JsonValueKind.Object)
        {
            var em = Str(u, "email"); if (em.Length > 0) _settings.CloudEmail = em;
            var nm = Str(u, "name");  if (nm.Length > 0) _settings.CloudName = nm;
            //  ⚠️ شناسه **جانشین** می‌شود، نه «اگر بود»: حسابِ تازه باید
            //  شناسهٔ خودش را بنشاند، وگرنه دفعهٔ بعد شناسهٔ حسابِ قبلی
            //  ملاکِ مقایسه می‌ماند.
            var id = Ident(u, "id"); if (id.Length > 0) _settings.CloudUserId = id;
        }
        await SaveQuiet();
        return CloudResult.Done;
    }

    // ── حساب با ایمیل و رمز ─────────────────────────────────────────────
    //
    //  خواستهٔ صریحِ صاحب ریپو (۱۴۰۵/۰۶/۲۸): «نه می‌گوید حساب داری یا نه، نه
    //  ثبت یا ساختنِ حساب دارد و نه رمز دارد و می‌خواهد… اول اسم، ایمیل،
    //  رمز، تکرارِ رمز؛ بعد برود بخشِ بعدی: کدِ شش‌رقمی و تاییدش از سرور و
    //  اسمِ پمپ و لوکیشنِ پمپ. همین.»
    //
    //  ⚠️ **رمز هیچ‌جا روی این کامپیوتر ذخیره نمی‌شود** — نه خام، نه هش.
    //  فقط همان یک بار به ابر می‌رود و آن‌جا با توکنِ نشست جواب می‌آید،
    //  دقیقاً مثلِ راهِ گوگل. چیزی که ذخیره می‌شود همان توکن است.
    //
    //  ⚠️ پاسخِ سرور **همان شکلِ راهِ گوگل** است (`{token, refreshToken,
    //  user{email,name}}`) تا هیچ جای دیگرِ برنامه فرق نفهمد.

    // ── ثبت‌نام سه‌پله‌ای: نام و ایمیل و رمز ⇒ کدِ ایمیل ⇒ حساب ─────────
    //
    //  گزارشِ صاحب ریپو (۱۴۰۵/۰۶/۲۹): «می‌خواهم با ایمیل خودم حسابی بسازم،
    //  نمی‌شود.»
    //
    //  ⛔ **درِ یک‌مرحله‌ایِ `/api/auth/register` عمداً بسته است** و
    //  `verification_required` می‌دهد: حساب بی تأییدِ ایمیل ساخته نمی‌شود.
    //  همین را با آزمونِ واقعیِ سرور دیدیم، نه با حدس
    //  (`shop/server/test/pump-account.test.js`). پس راهِ درست سه‌پله است:
    //
    //      ۱) /register/start     نام · ایمیل · رمز · تکرارِ رمز ⇒ کد به ایمیل
    //      ۲) /register/verify    کدِ شش‌رقمیِ ایمیل ⇒ «بلیتِ ثبت‌نام»
    //      ۳) /register/complete  بلیت + پذیرشِ شرایط ⇒ حساب و نشست
    //
    //  ⚠️ **بلیت و رمز هیچ‌وقت روی دیسک نمی‌نشینند** — بلیت در حافظهٔ همین
    //  شیء است و رمز را ویومدل در همان صفحه نگه می‌دارد و بعد پاکش می‌کند.

    // ── ترمزِ «ارسالِ بی‌نهایت» ──────────────────────────────────────────
    //
    //  بندِ آخرِ خواستهٔ صاحب ریپو دربارهٔ ورود: «درخواست‌های ورود قابلِ
    //  سوءاستفاده و ارسالِ بی‌نهایت نباشند.»
    //
    //  ⚠️ سقفِ **واقعی** روی خودِ سرور است و هست (سنجیده شد، نه حدس:
    //  `shop/server/src/routes/auth.js` — مسیرهای کدِ یک‌بارمصرف
    //  `otpLimit` دارند، پنج تا در هر پانزده دقیقه، و ورود و ثبت‌نام
    //  `authLimit` ده تا، به‌علاوهٔ قفلِ هشت‌تلاشیِ خودِ حساب). این ترمزِ
    //  محلی **جایش را نمی‌گیرد** — کارِ دیگری می‌کند:
    //
    //    ۱) دو مسیرِ این‌جا به هر زدنِ دکمه یک **ایمیل** می‌فرستند؛
    //    ۲) و کاربری که پنج بار «دوباره بفرست» را بزند، سقفِ سرور را خرج
    //       می‌کند و **پانزده دقیقه** از ثبت‌نامِ خودش بیرون می‌افتد.
    //
    //  پس شمردنِ محلی هم به سود کاربر است هم به سودِ سرور.
    //
    //  ⚠️ ترمز روی **همین شیء** است، نه static: سنجه‌ها هر کدام لینکِ خودشان
    //  را می‌سازند و ترمزِ یکی سنجهٔ دیگری را قرمز نمی‌کند. در خودِ برنامه
    //  صفحهٔ ورود یک لینک دارد و همان است که دکمه‌اش زده می‌شود.
    //  ⚠️ و فقط با **موفقیت** مهر می‌خورد: درخواستی که به سرور نرسید (مودم
    //  خاموش) هیچ ایمیلی نفرستاده، پس نباید کاربر را یک دقیقه معطل کند.

    /// <summary>فاصلهٔ لازم میان دو ایمیلِ کد — به ثانیه.</summary>
    public const int ResendWaitSeconds = 60;

    private readonly Dictionary<string, long> _lastMail = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>هنوز زود است؟ پیامِ آدمیزاد برمی‌گردد، وگرنه <c>null</c>.</summary>
    private string? TooSoon(string route, string email)
    {
        var key = route + "|" + (email ?? "").Trim();
        if (!_lastMail.TryGetValue(key, out var at)) return null;
        var left = ResendWaitSeconds - (int)((AppClock.MonoSource() - at) / 1000);
        return left > 0
            ? $"کد همین حالا فرستاده شد — {left} ثانیه صبر کنید و صندوقِ ایمیلتان را ببینید."
            : null;
    }

    private void MailSent(string route, string email) =>
        _lastMail[route + "|" + (email ?? "").Trim()] = AppClock.MonoSource();

    /// <summary>بلیتِ ثبت‌نام — فقط در حافظه، تا پلهٔ سوم.</summary>
    private string _registerTicket = "";

    /// <summary>نسخهٔ شرایطی که همراهِ بلیت آمد — همان را برمی‌گردانیم.</summary>
    private string _termsVersion = "";

    /// <summary>پلهٔ دوم را رفته‌ایم و بلیت داریم؟</summary>
    public bool HasRegisterTicket => _registerTicket.Length > 0;

    /// <summary>پلهٔ یک — کد به ایمیل فرستاده می‌شود. حسابی ساخته نمی‌شود.</summary>
    public async Task<CloudResult> RegisterStartAsync(string name, string email, string password,
                                                      CancellationToken ct = default)
    {
        if (TooSoon("register/start", email) is { } wait) return CloudResult.No(wait, "too_soon");

        var res = await SendFull(Build(HttpMethod.Post, "/api/auth/register/start", new
        {
            name = (name ?? "").Trim(),
            email = (email ?? "").Trim(),
            password,
            passwordConfirm = password,
            app = "pump",
        }, null), ct);
        if (res.Ok) { MailSent("register/start", email); return CloudResult.Done; }
        //  ⚠️ ۴۰۴ اینجا «سرور جواب نداد» نیست — پایینِ همین فایل، `NoRouteAsync`.
        if (res.Status == 404) return await NoRouteAsync(ct);
        return CloudResult.No(res.Why, res.Code);
    }

    /// <summary>پلهٔ دو — کدِ ایمیل. جوابش بلیتِ بیست‌دقیقه‌ای است.</summary>
    public async Task<CloudResult> RegisterVerifyAsync(string email, string emailCode,
                                                       CancellationToken ct = default)
    {
        var clean = new string((emailCode ?? "").Where(char.IsDigit).ToArray());
        if (clean.Length != 6) return CloudResult.No("کدِ تأییدِ ایمیل باید شش رقم باشد");

        var res = await SendFull(Build(HttpMethod.Post, "/api/auth/register/verify",
            new { email = (email ?? "").Trim(), code = clean, app = "pump" }, null), ct);
        if (!res.Ok)
        {
            if (res.Status == 404) return await NoRouteAsync(ct);
            return CloudResult.No(res.Why, res.Code);
        }
        var json = res.Json;

        _registerTicket = Str(json, "ticket");
        if (_registerTicket.Length == 0) return CloudResult.No("سرور بلیتِ ثبت‌نام نداد");
        if (json.TryGetProperty("terms", out var t) && t.ValueKind == JsonValueKind.Object)
            _termsVersion = Str(t, "version");
        return CloudResult.Done;
    }

    /// <summary>
    /// پلهٔ سه — حساب ساخته می‌شود و نشست می‌آید.
    ///
    /// ⚠️ پذیرشِ شرایط **اجباریِ خودِ سرور** است (`terms_required`)، پس
    /// برنامه باید واقعاً از کاربر پرسیده باشد.
    /// </summary>
    public async Task<CloudResult> RegisterCompleteAsync(string name, string password,
                                                         bool termsAccepted,
                                                         CancellationToken ct = default)
    {
        if (_registerTicket.Length == 0) return CloudResult.No("اول کدِ تأییدِ ایمیل را بزنید");
        if (!termsAccepted) return CloudResult.No("برای ساختنِ حساب باید شرایط را بپذیرید", "terms_required");

        var res = await SendFull(Build(HttpMethod.Post, "/api/auth/register/complete", new
        {
            ticket = _registerTicket,
            name = (name ?? "").Trim(),
            password,
            terms = new { accepted = true, version = _termsVersion },
            device = new { uid = DeviceUid, name = Environment.MachineName, platform = "windows" },
            app = "pump",
        }, null), ct);
        if (!res.Ok)
        {
            if (res.Status == 404) return await NoRouteAsync(ct);
            return CloudResult.No(res.Why, res.Code);
        }

        _registerTicket = "";      // بلیت خرج شد
        return await SeatAsync(res.Json);
    }

    /// <summary>متنِ شرایط و ضوابط — همان چیزی که کاربر می‌پذیرد.</summary>
    public async Task<(bool Ok, string Text, string Why)> TermsAsync(CancellationToken ct = default)
    {
        var (ok, json, why, _) = await GetAsync("/api/auth/terms", null, ct);
        if (!ok) return (false, "", why);

        var sb = new System.Text.StringBuilder();
        var title = Str(json, "title");
        if (title.Length > 0) sb.AppendLine(title).AppendLine();
        if (json.TryGetProperty("sections", out var arr) && arr.ValueKind == JsonValueKind.Array)
            foreach (var sec in arr.EnumerateArray())
            {
                var st = Str(sec, "title");
                var sb2 = Str(sec, "body");
                if (st.Length > 0) sb.AppendLine("• " + st);
                if (sb2.Length > 0) sb.AppendLine(sb2).AppendLine();
            }
        var text = sb.ToString().Trim();
        return (text.Length > 0, text, text.Length > 0 ? "" : "متنِ شرایط نیامد");
    }

    /// <summary>ورود به حسابی که از قبل ساخته شده.</summary>
    public Task<CloudResult> SignInWithPasswordAsync(string email, string password,
                                                     CancellationToken ct = default) =>
        AuthAsync("/api/auth/login",
                  new { email = (email ?? "").Trim(), password, app = "pump" },
                  ct);

    private async Task<CloudResult> AuthAsync(string path, object body, CancellationToken ct)
    {
        var res = await SendFull(Build(HttpMethod.Post, path, body, null), ct);
        if (!res.Ok)
        {
            //  ⚠️ صادق باش: اگر سرورِ ابر این راه را نداشت، «رمز غلط» نگو.
            //  ⚠️ از روی **کدِ خودِ HTTP** تصمیم می‌گیریم، نه از روی گشتنِ
            //  رشتهٔ «404» در متنِ فارسیِ خطا: سرورِ به‌روز برای مسیرِ نبوده
            //  پیامِ خودش را می‌دهد («این مسیر وجود ندارد») و آن گشتن دیگر
            //  نمی‌گرفت.
            if (res.Status == 404) return await NoRouteAsync(ct);
            return CloudResult.No(res.Why, res.Code);
        }

        return await SeatAsync(res.Json);
    }

    // ── ۴۰۴ی که «سرور جواب نداد» نیست ────────────────────────────────────
    //
    //  ⛔ **پیامی که کاربر را گمراه می‌کرد** (با عکسِ صاحب ریپو، ۱۴۰۵/۰۷/۰۱):
    //  زیرِ دکمهٔ «ساختنِ حساب و ادامه» نوشته می‌شد «سرور جواب نداد (404)» —
    //  و کاربر درست می‌گفت که این پیام هیچ کاری دستش نمی‌دهد. دو حالِ
    //  کاملاً جدا زیرِ آن یک جمله قایم شده بود، با دو کارِ جدا:
    //
    //      سرورِ حساب بالا است ولی این مسیر را ندارد ⇒ سرورِ حساب را به‌روز کن
    //      خودِ سرورِ حساب جواب نمی‌دهد               ⇒ سرویس/دامنه/پروکسی
    //
    //  ⚠️ **و این از روی خودِ ۴۰۴ دیدنی نیست** — سنجیده شد، حدس نیست:
    //  سرورِ نودِ ما برای مسیرِ نبوده همیشه **JSONِ فارسی** می‌دهد (زیرِ
    //  `/api/auth` می‌شود ۴۰۱ «احراز هویت لازم است»، جای دیگر ۴۰۴ «این مسیر
    //  وجود ندارد») — هر دو نسخهٔ کهنه (۲.۰.۰) و تازه محلی سنجیده شدند و هر
    //  دو همین را دادند. پس ۴۰۴ِ **بی‌بدنه** یعنی درخواست به خودِ سرور
    //  نرسیده و چیزی جلوی آن نشسته. تنها راهِ فهمیدنش پرسیدن از
    //  `/api/health` است، که باز است و توکن نمی‌خواهد.
    //
    //  ⚠️ «برنامه را به‌روز کنید» پیامِ غلطی بود و برداشته شد: برنامه تازه
    //  است و سرور کهنه، پس کاربر را دنبالِ نخودِ سیاه می‌فرستاد.
    //  ⛔ و **نامِ میزبان در پیام نمی‌آید** — همان قاعدهٔ همیشه؛ فقط نسخه.

    // ══ چراغِ ابر — راست می‌گوید یا هیچ نمی‌گوید ═════════════════════════
    //
    //  ⛔ **هیچ‌کدامِ این‌ها حدس نیستند.** `Reach` تنها از `SendFull` پر
    //  می‌شود، یعنی از خودِ جوابِ سرور؛ و «سرورِ ما جواب داد» یعنی یا ۲xx
    //  بود یا خطایی به شکلِ خودمان (`error.code`). یک ۴۰۴ِ بی‌بدنه از
    //  پروکسی **وصل حساب نمی‌شود** — همان چیزی که ۱۴۰۵/۰۷/۰۱ گرفتیم.
    //
    //  ⚠️ چرا ایستا: `CloudLink` در شش جای برنامه با یک `new` ساخته می‌شود
    //  (پروفایل، چت، انتشار، پشتیبان…). حالِ «به ابر می‌رسیم؟» مالِ خودِ
    //  برنامه است، نه مالِ یک نمونه.

    /// <summary>آخرین باری که واقعاً جواب گرفتیم چه بود.</summary>
    public static CloudReach Reach { get; private set; } = CloudReach.Unknown;

    /// <summary>آخرین باری که سرورِ ما واقعاً جواب داد (به وقتِ محلی).</summary>
    public static DateTime? CloudOkAt { get; private set; }

    /// <summary>اگر نرسیدیم، چرا — بی نام و نشانیِ میزبان.</summary>
    public static string CloudWhy { get; private set; } = "";

    private static void NoteOnline()
    {
        Reach = CloudReach.Online;
        CloudOkAt = AppClock.Now;
        CloudWhy = "";
    }

    private static void NoteOffline(string why)
    {
        Reach = CloudReach.Offline;
        CloudWhy = why;
    }

    /// <summary>
    /// کدهایی که درگاهِ سرورِ خانگی به جای سرورِ حساب می‌دهد: سرورِ حساب
    /// خاموش است یا درگاه به آن نمی‌رسد. جوابِ خودِ درگاه است، نه سرورِ حساب.
    /// </summary>
    public static bool IsDownCode(string code) =>
        code is "account_server_down" or "account_server_unreachable";

    /// <summary>فقط برای سنجه‌ها — برنامه هیچ‌وقت حال را دستی نمی‌سازد.</summary>
    public static void ResetReach()
    {
        Reach = CloudReach.Unknown;
        CloudOkAt = null;
        CloudWhy = "";
        //  ترمزِ «پس از شکستِ ثبت» هم استاتیک است: آزمونی که ثبتِ ناموفق
        //  می‌سازد نباید ده دقیقه ثبتِ آزمونِ بعدی را ببندد.
        _lastBindFailAt = DateTime.MinValue;
        LastBindWhy = "";
        _codeCheckedAt = DateTime.MinValue;
    }

    /// <summary>
    /// حالِ سرورِ حساب: بالا است؟ چه نسخه‌ای؟ (بی توکن، پیش از ورود هم.)
    /// </summary>
    public static async Task<(bool Up, string Version)> CloudHealthAsync(CancellationToken ct = default)
    {
        var r = await SendFull(Build(HttpMethod.Get, "/api/health", null, null), ct);
        return r.Ok ? (true, Str(r.Json, "version")) : (false, "");
    }

    /// <summary>
    /// باتِ تلگرامِ پمپ روی سرورِ حساب: هست؟ نشانی‌اش چیست؟ (بی توکن.)
    /// </summary>
    /// <remarks>
    /// ⛔ فقط نشانیِ عمومیِ بات (<c>https://t.me/…</c>) — رمزِ بات فقط روی
    /// سرور است و هیچ‌وقت به این برنامه نمی‌رسد؛ اگر داخلِ برنامه بود، هر
    /// کسی از فایلِ برنامه بیرونش می‌کشید و از طرفِ پمپ پیام می‌داد.
    /// ⚠️ هیچ‌وقت استثنا بیرون نمی‌دهد و هر چیزِ ناجور «نیست» است.
    /// </remarks>
    public static async Task<string> TelegramBotUrlAsync(CancellationToken ct = default)
    {
        try
        {
            var r = await SendFull(Build(HttpMethod.Get, "/api/pump/public/telegram", null, null), ct);
            if (!r.Ok || r.Json.ValueKind != JsonValueKind.Object) return "";
            if (!r.Json.TryGetProperty("enabled", out var on) || on.ValueKind != JsonValueKind.True) return "";
            var url = Str(r.Json, "url");
            return url.StartsWith("https://t.me/", StringComparison.Ordinal) ? url : "";
        }
        catch (OperationCanceledException) { throw; }
        catch { return ""; }
    }

    /// <summary>
    /// ۴۰۴ی که از یک مسیرِ حساب آمد ⇒ همان جمله‌ای که کاربر با آن می‌داند
    /// چه کار کند.
    /// </summary>
    private static async Task<CloudResult> NoRouteAsync(CancellationToken ct)
    {
        var (up, ver) = await CloudHealthAsync(ct);
        if (!up)
            return CloudResult.No(
                "به سرورِ حساب نرسیدیم — سرور بالا نیست یا درخواست به آن نمی‌رسد.",
                "no_server");

        var v = ver.Length > 0 ? $" (نسخهٔ {ver})" : "";
        return CloudResult.No(
            $"سرورِ حساب{v} این مسیر را ندارد — سرورِ حساب را به‌روز کنید.",
            "no_route");
    }

    // ── رمزِ فراموش‌شده ─────────────────────────────────────────────────
    //
    //  ⚠️ این راه **ساخته نشد، پیدا شد**: سرور از قبل هر دو مسیر را دارد
    //  (`shop/server/src/routes/auth.js` و `test/password-reset.test.js`) و
    //  فقط برنامهٔ نیتیو هیچ‌وقت صدایشان نزده بود. پس کسی که رمزش را گم
    //  می‌کرد هیچ راهی جز ساختنِ حسابِ تازه نداشت.
    //
    //      POST /api/auth/password/forgot  { email }         ⇒ کد به ایمیل
    //      POST /api/auth/password/reset   { email, code, password }
    //                                      ⇒ رمزِ تازه + نشست، یک‌جا
    //
    //  ⚠️ **جوابِ «این ایمیل هست» و «نیست» عمداً یکی است** (خودِ سرور
    //  این‌طور نوشته). پس این‌جا هم نباید چیزی به آن اضافه کرد: پیامی مثلِ
    //  «چنین حسابی نیست» فهرستِ ایمیل‌های مشتری‌ها را لو می‌دهد.

    /// <summary>پلهٔ یک — کدِ یک‌بارمصرف به همان ایمیل می‌رود.</summary>
    public async Task<CloudResult> ForgotPasswordAsync(string email, CancellationToken ct = default)
    {
        var clean = (email ?? "").Trim();
        if (clean.Length == 0) return CloudResult.No("ایمیل را بنویسید");
        if (TooSoon("password/forgot", clean) is { } wait) return CloudResult.No(wait, "too_soon");

        var res = await SendFull(Build(HttpMethod.Post, "/api/auth/password/forgot",
            new { email = clean, app = "pump" }, null), ct);
        if (res.Ok) { MailSent("password/forgot", clean); return CloudResult.Done; }
        if (res.Status == 404) return await NoRouteAsync(ct);
        return CloudResult.No(res.Why, res.Code);
    }

    /// <summary>
    /// پلهٔ دو — کدِ ایمیل و رمزِ تازه. سرور خودش همان‌جا وارد هم می‌کند، پس
    /// کاربر رمزِ تازه‌اش را دوباره تایپ نمی‌کند.
    ///
    /// ⚠️ سرور پس از عوض شدنِ رمز **همهٔ نشست‌های باز را می‌بندد**
    /// (<c>revokeAllForSubject</c>) — یعنی اگر رمز را گم کرده بودید چون
    /// گوشی‌تان دستِ دیگری افتاده، آن نشست هم همان لحظه می‌میرد.
    /// </summary>
    public async Task<CloudResult> ResetPasswordAsync(string email, string emailCode, string newPassword,
                                                      CancellationToken ct = default)
    {
        var clean = new string((emailCode ?? "").Where(char.IsDigit).ToArray());
        if (clean.Length != 6) return CloudResult.No("کدِ تأییدِ ایمیل باید شش رقم باشد");

        var res = await SendFull(Build(HttpMethod.Post, "/api/auth/password/reset", new
        {
            email = (email ?? "").Trim(),
            code = clean,
            password = newPassword,
            device = new { uid = DeviceUid, name = Environment.MachineName, platform = "windows" },
            app = "pump",
        }, null), ct);
        if (!res.Ok)
        {
            if (res.Status == 404) return await NoRouteAsync(ct);
            return CloudResult.No(res.Why, res.Code);
        }

        return await SeatAsync(res.Json);
    }
}
