using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using PumpYaqobi.Application.Localization;
using PumpYaqobi.Domain;

namespace PumpYaqobi.App.Services;

/// <summary>تکه‌ای از <see cref="CloudLink"/> — نشستِ حساب، خروج، جدا شدن و نشانیِ خانگی از حساب.</summary>
public sealed partial class CloudLink
{
    // ── نشستِ تازه، و ۴۰۱ ────────────────────────────────────────────────
    //
    //  ⛔ **باگی که برنامه را یک‌ساعته خاموش می‌کرد.** سرور به توکنِ دسترسی
    //  دقیقاً **یک ساعت** عمر می‌دهد (`ACCESS_TOKEN_TTL_MIN = 60`) و
    //  `refreshToken` را برای نود روز می‌دهد. برنامه `refreshToken` را
    //  ذخیره می‌کرد ولی **هیچ‌جا نمی‌خواندش** و هیچ ۴۰۱ی را هم نمی‌فهمید.
    //  یعنی یک ساعت پس از ورود، `/api/pump/me` و کدِ اپِ کارمندان و هر کارِ
    //  حسابیِ دیگر بی‌صدا شکست می‌خوردند و **دیگر هیچ‌وقت درست نمی‌شدند** —
    //  در حالی که صفحهٔ پروفایل همچنان «وارد شده‌اید» می‌گفت.
    //
    //  همان کاری که `kar/cloud.js` از اول می‌کرد (`authed()`): یک بار تازه
    //  کن، یک بار دوباره بزن. ⚠️ **فقط یک بار** — حلقه زدن جز پنهان کردنِ
    //  مشکل کاری نمی‌کند.

    /// <summary>
    /// چند دقیقه پیش از انقضا خودمان تازه می‌کنیم — یک درخواستِ حتماً-شکست
    /// کمتر، و کاربری که وسطِ کار پیامِ خطا نمی‌بیند.
    /// </summary>
    private const long RefreshSkewMs = 60_000;

    /// <summary>
    /// ══ یک تازه‌سازی در کلِ پروسه، نه در هر نمونه ═══════════════════════════
    ///
    /// ⛔ <b>باگِ «گاهی بی‌دلیل از حساب بیرون می‌افتم».</b> این قفل تا
    /// ۱۴۰۵/۰۷/۱۲ مالِ <b>هر نمونه</b> بود — در حالی که حلقهٔ پس‌زمینه، صفحهٔ
    /// پروفایل، همگام‌سازی و گزارشِ خطا هر کدام <see cref="CloudLink"/>ِ خودشان
    /// را با <b>عکسِ جداگانه‌ای</b> از تنظیمات می‌سازند، و سرور توکنِ تازه‌سازی
    /// را <b>می‌چرخاند</b> (توکنِ کهنه پس از سی ثانیه رد می‌شود). پس:
    ///
    ///   ۱) نمونهٔ الف تازه می‌کرد ⇒ توکنِ نو روی دیسک؛
    ///   ۲) نمونهٔ ب با توکنِ <b>کهنهٔ</b> عکسِ خودش تازه می‌کرد ⇒ ۴۰۱؛
    ///   ۳) و ب <see cref="ClearSessionAsync"/> را می‌زد ⇒ توکن‌های <b>سالمِ</b>
    ///      الف هم از دیسک پاک می‌شدند.
    ///
    /// حالا قفل ایستا است، و داخلِ قفل تنظیماتِ <b>روی دیسک</b> دوباره خوانده
    /// می‌شود: اگر کسِ دیگری در همین فاصله چرخانده، همان را برمی‌داریم و
    /// اصلاً تازه نمی‌کنیم. و نشست فقط وقتی پاک می‌شود که توکنِ ردشده
    /// <b>هنوز همانی باشد که روی دیسک است</b>.
    /// </summary>
    private static readonly SemaphoreSlim RefreshGate = new(1, 1);

    /// <summary>توکنِ دسترسی نزدیکِ انقضاست؟</summary>
    private bool AccessNearlyExpired =>
        _settings.CloudAccessExpiresAt > 0
        && AppClock.UnixMs + RefreshSkewMs >= _settings.CloudAccessExpiresAt;

    /// <summary>
    /// نشستِ روی دیسک — ⚠️ فقط وقتی چیزی دارد و با عکسِ همین نمونه فرق دارد.
    /// دیسکِ <b>خالی</b> هیچ‌وقت جای عکسِ پرِ این نمونه نمی‌نشیند: نمونه‌ای
    /// که عمداً روی دیسک نمی‌نویسد (سنجه‌ها) نباید با آن خالی شود.
    /// </summary>
    private bool AdoptDiskSession()
    {
        AppSettings disk;
        try { disk = AppSettings.Load(); }
        catch { return false; }

        var r = (disk.CloudRefreshToken ?? "").Trim();
        if (r.Length == 0 || r == (_settings.CloudRefreshToken ?? "").Trim()) return false;

        _settings.CloudRefreshToken = disk.CloudRefreshToken;
        _settings.CloudAccountToken = disk.CloudAccountToken;
        _settings.CloudAccessExpiresAt = disk.CloudAccessExpiresAt;
        return true;
    }

    /// <summary>
    /// نشستِ تازه از روی <c>refreshToken</c>.
    ///
    /// سه پایان دارد و هر سه مهم‌اند:
    ///  • شد ⇒ توکنِ تازه می‌نشیند.
    ///  • سرور گفت این توکن باطل است (۴۰۱/۴۰۳) ⇒ نشست <b>پاک</b> می‌شود، تا
    ///    صفحهٔ پروفایل دیگر دروغ نگوید و کاربر دوباره وارد شود — ⛔ ولی
    ///    فقط اگر همان توکنِ ردشده هنوز روی دیسک است (بالا نوشته چرا).
    ///  • ⚠️ <b>نرسیدیم</b> (بی‌اینترنت، سرورِ خاموش، تایم‌اوت) ⇒ هیچ چیزی
    ///    پاک نمی‌شود. وگرنه یک قطعیِ اینترنت کاربر را از حسابش بیرون
    ///    می‌انداخت — و این برنامه اساساً آفلاین است.
    /// </summary>
    private async Task<bool> RefreshSessionAsync(CancellationToken ct)
    {
        //  توکنی که این نمونه **پیش از** انتظار داشت — اگر تا نوبتش برسد
        //  کسِ دیگری چرخانده باشد، همین فرق نشان می‌دهد.
        var before = (_settings.CloudRefreshToken ?? "").Trim();

        await RefreshGate.WaitAsync(ct);
        try
        {
            //  ⛔ کسِ دیگری در همین فاصله تازه کرد ⇒ همان را برمی‌داریم.
            if (AdoptDiskSession())
            {
                if (!string.IsNullOrWhiteSpace(_settings.CloudAccountToken) && !AccessNearlyExpired
                    && _settings.CloudRefreshToken.Trim() != before)
                    return true;
            }

            var mine = (_settings.CloudRefreshToken ?? "").Trim();
            if (mine.Length == 0)
            {
                await ClearSessionAsync();
                return false;
            }

            var res = await SendFull(Build(HttpMethod.Post, "/api/auth/refresh",
                new { refreshToken = mine, device = new { deviceId = DeviceUid } },
                null), ct);

            if (res.Ok)
            {
                var token = Str(res.Json, "accessToken");
                if (token.Length == 0) return false;
                _settings.CloudAccountToken = token;
                //  ⚠️ سرور توکنِ تازه‌سازی را **می‌چرخاند**؛ اگر داد، همان
                //  می‌نشیند و کهنه دیگر به کار نمی‌آید.
                var again = Str(res.Json, "refreshToken");
                if (again.Length > 0) _settings.CloudRefreshToken = again;
                _settings.CloudAccessExpiresAt = Num(res.Json, "accessExpiresAt");
                await SaveQuiet();
                return true;
            }

            if (res.Status is 401 or 403)
            {
                //  ⛔ شاید توکنِ ما همین حالا به دستِ پروسهٔ دیگری چرخانده
                //  شده: آن‌وقت نشستِ سالمِ روی دیسک را برمی‌داریم، نه این‌که
                //  پاکش کنیم.
                if (AdoptDiskSession()) return !string.IsNullOrWhiteSpace(_settings.CloudAccountToken);
                await ClearSessionAsync();
            }
            return false;
        }
        catch (OperationCanceledException) { return false; }
        finally { RefreshGate.Release(); }
    }

    /// <summary>
    /// نشست را پاک می‌کند — ولی <b>نام و ایمیل می‌مانند</b>، تا صفحهٔ ورود
    /// کادرها را پر کند و کاربر فقط رمز بزند.
    /// ⛔ و دفترِ روی کامپیوتر و اشتراکِ دستگاه دست نمی‌خورند.
    /// </summary>
    private async Task ClearSessionAsync()
    {
        _settings.CloudAccountToken = "";
        _settings.CloudRefreshToken = "";
        _settings.CloudAccessExpiresAt = 0;
        AccountHasStation = null;
        await SaveQuiet();
    }

    /// <summary>
    /// درخواستی با توکنِ حساب — با یک تازه‌سازی و یک تلاشِ دوباره.
    ///
    /// ⚠️ هر تلاش <b>پیامِ تازه‌ای</b> می‌سازد: یک
    /// <see cref="HttpRequestMessage"/> فقط یک بار فرستادنی است.
    /// </summary>
    private async Task<CloudReply> AccountAsync(HttpMethod method, string path, object? body,
                                                CancellationToken ct)
    {
        if (!SignedIn) return CloudReply.Fail("اول وارد حساب شوید", "signed_out");

        //  زودتر از انقضا، بی این‌که منتظرِ یک ۴۰۱ِ حتمی بمانیم
        if (AccessNearlyExpired) await RefreshSessionAsync(ct);
        if (!SignedIn) return CloudReply.Fail("نشست منقضی شده — دوباره وارد شوید", "signed_out");

        var first = await SendFull(Build(method, path, body, _settings.CloudAccountToken), ct);
        if (first.Status != 401) return first;

        if (!await RefreshSessionAsync(ct)) return first;
        return await SendFull(Build(method, path, body, _settings.CloudAccountToken), ct);
    }

    /// <summary>
    /// خروج از حساب — دفترِ روی کامپیوتر دست نمی‌خورد.
    ///
    /// ⛔ **پیش از این فقط محلی بود.** توکنِ دسترسی و توکنِ تازه‌سازی روی
    /// سرور زنده می‌ماندند (تازه‌سازی تا نود روز)، پس هر کسی که یک بار
    /// آن رشته را برداشته بود، «خروج»ِ کاربر جلویش را نمی‌گرفت. حالا اول
    /// از سرور باطل می‌شوند (<c>POST /api/auth/logout</c>).
    ///
    /// ⚠️ ولی نشدنِ آن هیچ‌وقت جلوی خروجِ محلی را نمی‌گیرد: کسی که
    /// اینترنت ندارد هم باید بتواند از حسابش بیرون بیاید.
    /// </summary>
    public async Task SignOutAsync(CancellationToken ct = default)
    {
        //  ⛔ پیش از باطل کردن، نشستِ **روی دیسک** برداشته می‌شود: عکسِ این
        //  نمونه ممکن است توکنی داشته باشد که پروسهٔ دیگری همین حالا چرخانده
        //  — آن‌وقت «خروج» توکنِ مرده را باطل می‌کرد و توکنِ زنده روی سرور
        //  نود روز می‌ماند. زیرِ همان قفلِ تازه‌سازی، تا وسطِ یک چرخش نیفتد.
        await RefreshGate.WaitAsync(ct);
        try
        {
            AdoptDiskSession();
            if (SignedIn)
            {
                try
                {
                    await SendFull(Build(HttpMethod.Post, "/api/auth/logout",
                        new { refreshToken = _settings.CloudRefreshToken }, _settings.CloudAccountToken), ct);
                }
                catch { /* خروجِ محلی گروگانِ سرور نیست */ }
            }
        }
        finally { RefreshGate.Release(); }

        _settings.CloudAccountToken = "";
        _settings.CloudRefreshToken = "";
        _settings.CloudAccessExpiresAt = 0;
        AccountHasStation = null;
        _settings.CloudEmail = "";
        _settings.CloudName = "";
        await SaveQuiet();
    }

    /// <summary>
    /// ══ «این دستگاه دیگر مالِ این پمپ نیست» ═════════════════════════════
    ///
    /// ⛔ **نشتی که این می‌بندد**: خروج از حساب تا امروز فقط چهار فیلدِ
    /// حساب را پاک می‌کرد. توکنِ **دستگاه**، شناسهٔ پمپ، مجوز، کلیدِ عمومی،
    /// کدِ اپِ کارمندان، نشانی و رمزِ سرورِ خانگی و مُهرِ ارفاق همه سرِ جا
    /// می‌ماندند. یعنی اگر همین کامپیوتر به حسابِ پمپِ دیگری می‌رفت:
    ///   • عکسِ حساب‌ها به پوشهٔ ابریِ **پمپِ قبلی** می‌رفت (توکنِ دستگاهِ او)،
    ///   • کدِ اپِ کارمندان و اشتراکِ پمپِ قبلی روی صفحهٔ پروفایل دیده می‌شد،
    ///   • و ارفاقِ اشتراکِ او برای این یکی خرج می‌شد.
    ///
    /// ⚠️ **دفترِ روی کامپیوتر دست نمی‌خورد.** این تابع یک بیت از دیتابیس
    /// را هم لمس نمی‌کند — دفترِ صاحبِ پمپ مالِ خودش است و گروگان گرفتنش
    /// (یا پاک کردنش) هیچ‌وقت کارِ ما نیست. فقط بندهای «این نصب به کدام پمپ
    /// وصل است» باز می‌شوند.
    ///
    /// ⚠️ کلیدِ عمومی هم پاک می‌شود، و این عمدی است: قفلِ TOFU برای «همین
    /// دستگاه، همین سرور» است و با عوض شدنِ پمپ باید از نو قفل شود. نشانیِ
    /// ابر همچنان در کد قفل است، پس این هیچ دری را باز نمی‌کند.
    /// </summary>
    public async Task ForgetStationAsync()
    {
        AccountHasStation = null;
        _settings.CloudDeviceToken = "";
        _settings.CloudStationId = "";
        _settings.CloudStationCode = "";
        _settings.CloudLicense = "";
        _settings.CloudPublicKey = "";
        _settings.CloudAccessCode = "";
        //  ⛔ حسابِ تازه یعنی گامِ پمپ از نو — وگرنه پمپِ حسابِ قبلی
        //  «تمام‌شده» حساب می‌شد و کاربر هیچ‌وقت پمپِ خودش را نمی‌ساخت.
        _settings.PumpStepDone = false;
        _settings.CloudSyncedAt = 0;
        _settings.EntitledUntil = 0;
        _settings.EntitledPlan = "";
        //  نشانی و رمزهای سرورِ خانگی هم مالِ همان پمپ بودند
        _settings.ServerUrl = "";
        _settings.ServerLanUrl = "";
        _settings.ServerToken = "";
        _settings.ServerReadKey = "";
        _settings.ServerId = "";
        Subscription = PumpSubscription.None;
        await SaveQuiet();
    }

    /// <summary>
    /// نشانیِ سرورِ خانگی و رمزِ خواندن را از حساب می‌گیرد.
    ///
    /// ⚠️ همین است که کادرِ «نشانیِ سرور» و «رمزِ سرور» را از تنظیمات
    /// برداشت: کاربر هیچ‌کدام را تایپ نمی‌کند، از حسابش می‌آید.
    /// </summary>
    public async Task<(bool Ok, string Url, string ReadKey, string Station, string Why)>
        HomeFromAccountAsync(CancellationToken ct = default, bool forceBind = false)
    {
        if (!SignedIn) return (false, "", "", "", "اول وارد حساب شوید");

        //  ⚠️ از راهِ `AccountAsync` می‌رود، نه `GetAsync`ِ خام: توکنِ دسترسی
        //  یک ساعت عمر دارد و این همان جایی بود که بی‌صدا می‌مرد.
        var res = await AccountAsync(HttpMethod.Get, "/api/pump/me", null, ct);
        if (!res.Ok) return (false, "", "", "", res.Why);
        var json = res.Json;

        //  ⛔ **اشتراک به حساب بسته است، نه به یک کد.**
        //
        //  خواستهٔ صریحِ صاحب ریپو (۱۴۰۵/۰۶/۳۰): «من یادم نمیاد که برای
        //  اشتراک کدی گفته باشم — اون رو من به حسابِ یارو از سرور می‌دم
        //  اینترنتی و تو برنامه تو حسابِ همون ثبت می‌شن.»
        //
        //  سرور همین حالا هم `entitlement`ِ همان پمپ را در همین پاسخ
        //  می‌فرستد (`routes/pump.js`) — ولی تا امروز برنامه فقط `home` را
        //  برمی‌داشت و **بقیه‌اش را دور می‌ریخت**. یعنی صاحبِ پمپی که
        //  اشتراکش را روی سرور گرفته بود، در برنامه «بی‌اشتراک» می‌ماند تا
        //  وقتی کدی بزند. حالا همین‌جا نشانده می‌شود.
        //
        //  ⚠️ فقط **پر** می‌کند: پاسخی که اشتراک ندارد چیزی را خاموش
        //  نمی‌کند، چون ارفاق و مجوزِ امضاشده راهِ خودشان را دارند.
        ReadSubscription(json);
        if (Subscription.Active) Entitlements.Remember(_settings, Subscription, Verify());

        //  ⛔ **پمپِ این حساب با پمپی که این دستگاه رویش قفل شده یکی نیست.**
        //  بی این سنجش، ورود با حسابِ پمپِ دیگر نشانی و رمزِ **آن** پمپ را
        //  روی تنظیماتِ این یکی می‌نشاند: از آن لحظه دفترِ این پمپ به سرورِ
        //  خانگیِ پمپِ دیگری می‌رفت. راهِ درستِ جابه‌جایی
        //  `ForgetStationAsync` است.
        //
        //  ⚠️ **سنجش روی شناسهٔ ابری است، نه کدِ پمپ.** یک بار با کدِ پمپ
        //  نوشتمش و سنجهٔ `cloudlogin` گرفتش: کدِ ابر لازم نیست با
        //  `AppSettings.StationCode`ی محلی یکی باشد (ابر می‌تواند
        //  هنجارش کند یا خودش بسازدش)، پس آن مقایسه نصبِ سالم را هم رد
        //  می‌کرد. شناسه همان چیزی است که مجوز (`stn`) هم رویش قفل است.
        var acctStation = StationId(json);
        AccountHasStation = acctStation.Length > 0;
        _acctStationSeen = acctStation;
        if (acctStation.Length == 0) LastBindWhy = "";
        var locked = (_settings.CloudStationId ?? "").Trim();
        var bindDue = forceBind || AppClock.Mono - _lastBindFailAt >= BindRetryAfterFail;

        /*
         *  ⛔ **این کامپیوتر روی پمپی است که مالِ این حساب نیست** — یا حساب
         *  اصلاً پمپی ندارد. (۱۴۰۵/۰۷/۱۴، بازسازی‌شده با پشتهٔ واقعی و خودِ
         *  برنامه: نصبی که روزی با کدِ شش‌رقمی فعال شده بود روی پمپِ بی‌صاحبِ
         *  آن کد ماند؛ صاحبش وارد حسابش شد و مدیر به پمپِ حساب VIP داد — و
         *  برنامه برای همیشه روی پمپِ کد ماند، بی هیچ پیامی. «نه آزمایشی،
         *  نه اشتراکی که دادم».)
         *
         *  تا دیروز همین‌جا فقط «مالِ پمپِ دیگری است» برمی‌گشت و هیچ‌جا
         *  دیده نمی‌شد. حالا **سرور** تصمیم می‌گیرد (`BindAsync(adopt)`):
         *  پمپِ بی‌صاحب مالِ حساب می‌شود یا دستگاه با روزهای اشتراکش به پمپِ
         *  حساب می‌رود؛ پمپی که صاحبِ دیگری دارد دست نمی‌خورد و دلیلش
         *  همان‌جا (`LastBindWhy`) گفته می‌شود.
         */
        //  ⛔ نصبی که **هنوز فعال نشده** ولی شناسهٔ پمپِ دیگری رویش مانده، مثلِ
        //  همیشه دست نمی‌خورد: توکنی ندارد که مدرکِ آن پمپ باشد، پس سرور
        //  چیزی برای سنجیدن ندارد و بستنش به پمپِ حساب یعنی نشاندنِ نشانی و
        //  رمزِ پمپِ دیگر روی این دفتر.
        if (!Activated && locked.Length > 0 && acctStation.Length > 0
            && !string.Equals(acctStation, locked, StringComparison.Ordinal))
            return (false, "", "", "", "این حساب مالِ پمپِ دیگری است. برای جابه‌جایی، "
                + "این دستگاه را از پمپِ فعلی جدا کنید.");

        //  ⛔ **شناسهٔ پمپِ روی دیسک خراب است، ولی مجوزِ امضاشده خودش می‌گوید
        //  این کامپیوتر روی همان پمپِ حساب است** (۱۴۰۵/۰۷/۱۴، سنجهٔ
        //  `signuptrial` با ‎PUMP_SIGNUP_CORRUPT=1‎). بی این، راهِ «روی پمپِ
        //  دیگری است» (`adopt`) می‌رفت و دستگاهِ سالم را از پمپِ خودش جدا
        //  می‌کرد. امضا مدرک است، نه شناسهٔ روی دیسک.
        if (Activated && locked.Length > 0 && acctStation.Length > 0
            && !string.Equals(acctStation, locked, StringComparison.Ordinal)
            && LicenseGuard.Check(_settings.CloudLicense, _settings.CloudPublicKey, DeviceUid, acctStation,
                                  LicenseClock.Now(_settings)).SignatureOk)
        {
            _settings.CloudStationId = acctStation;
            locked = acctStation;
            await SaveQuiet();
        }

        //  ⛔ **همین پمپ، ولی مجوزِ روی دیسک مالِ این کامپیوتر نیست** —
        //  همان عکسِ صاحب ریپو (۱۴۰۵/۰۷/۱۴): پنل «آزمایشی · ۳۰ روز»، برنامه
        //  «بدونِ اشتراکِ فعال» و **کلِ برنامه فقط‌خواندنی**.
        await ReseatIfForeignLicenseAsync(acctStation, locked, bindDue, ct);

        var elsewhere = Activated && locked.Length > 0
            && !string.Equals(acctStation, locked, StringComparison.Ordinal);
        if (elsewhere)
        {
            if (!bindDue)
                return (false, "", "", "", LastBindWhy.Length > 0 ? LastBindWhy
                    : "این کامپیوتر روی پمپِ دیگری است");
            CloudResult moved;
            try { moved = await BindAsync(ct, adopt: true); }
            catch (Exception ex) { moved = CloudResult.No(ex.GetType().Name); }
            //  ⛔ **سرور جابه‌جایی را رد کرد ⇒ این کامپیوتر به پمپِ خودِ حساب
            //  وصل می‌شود** (۱۴۰۵/۰۷/۱۴، سنجهٔ `signuptrial` با
            //  ‎PUMP_SIGNUP_FOREIGN=1‎ روی سرورِ واقعی). پمپِ قبلی صاحبِ دیگری
            //  دارد (`station_mismatch`) یا سرورِ حساب کهنه است و جابه‌جایی را
            //  بلد نیست — و تا دیروز این‌جا برای همیشه می‌ایستاد: پنل «آزمایشی ·
            //  ۳۰ روز»، برنامه بی مجوز. کسی که با حسابِ خودش وارد شده، پمپِ
            //  خودش را می‌خواهد؛ پمپِ دیگر روی سرور دست نمی‌خورد و دفتر هم نه.
            //  ⚠️ سرورِ کهنه فقط وقتی کنار گذاشته می‌شود که پمپِ حساب خودش
            //  اشتراک دارد — وگرنه روزهای پمپِ کدی بی‌صدا از دست می‌رفت.
            if (!moved.Ok && (moved.Code == "station_mismatch"
                              || (moved.Code == "adopt_unsupported" && Subscription.Active)))
            {
                if (await ReseatToAccountPumpAsync(leaveOther: true, ct))
                    moved = CloudResult.Done;
            }
            LastBindWhy = moved.Ok ? ""
                : (moved.Why ?? "").Contains("پمپِ دیگری") ? moved.Why!
                : "این کامپیوتر روی پمپِ دیگری است — " + (moved.Why ?? "");
            _lastBindFailAt = moved.Ok ? DateTime.MinValue : AppClock.Mono;
            if (!moved.Ok) return (false, "", "", "", LastBindWhy);
            //  حالِ تازهٔ حساب — پمپ حالا همان پمپِ این کامپیوتر است
            res = await AccountAsync(HttpMethod.Get, "/api/pump/me", null, ct);
            if (!res.Ok) return (false, "", "", "", res.Why);
            json = res.Json;
            ReadSubscription(json);
            acctStation = StationId(json);
            AccountHasStation = acctStation.Length > 0;
        _acctStationSeen = acctStation;
        }

        /*
         *  ⛔ **شناسهٔ پمپِ روی دیسک همان پمپِ حساب است، ولی توکنِ دستگاه مالِ
         *  پمپِ دیگری است** — همان عکسِ دومِ صاحب ریپو (۱۴۰۵/۰۷/۱۴): پنل
         *  «آزمایشی · ۳۰ روز»، برنامه «بدونِ اشتراکِ فعال» و «پروفایل». سنجهٔ
         *  `signuptrial` با ‎PUMP_SIGNUP_FOREIGN=1‎ دقیقاً همین صفحه را ساخت:
         *  `RefreshAsync` هر بار ‹station_mismatch› می‌گرفت و پیش از گرفتنِ
         *  مجوز برمی‌گشت، و چون دستگاه «فعال» بود هیچ‌وقت دوباره بند نمی‌شد.
         *
         *  ⚠️ فقط وقتی سرور می‌گوید پمپِ حساب اشتراک یا آزمایشیِ **فعال** دارد
         *  و مجوزِ روی دیسک آن را نمی‌گوید — پس نصبِ سالم هیچ درخواستِ
         *  تازه‌ای نمی‌زند. اول همان تازه‌سازیِ همیشگی؛ فقط اگر سرور گفت
         *  «این دستگاه روی پمپِ دیگری است»، وصلِ دوباره به پمپِ حساب.
         */
        //
        //  ⛔ **و هر دلیلِ دیگری که مجوزِ همین پمپ روی دیسک ننشیند** (۱۴۰۵/۰۷/۱۴،
        //  پس از ۳.۱.۱۹۳ — صاحب ریپو: «آپدیت کردم، هنوز اشتراک نیومده»). سنجهٔ
        //  `signuptrial` با ‎PUMP_SIGNUP_FOREIGN=1‎ دو حالِ دیگر را یافت که همان
        //  صفحه را می‌سازند و تازه‌سازی هرگز درستشان نمی‌کرد: کلیدِ امضای سرور با
        //  کلیدِ قفل‌شدهٔ روی دیسک یکی نیست (`key_mismatch` — سرورِ حسابی که از نو
        //  نصب شده)، و مجوز برای شناسهٔ دستگاهِ دیگری صادر می‌شود (`bad_license`).
        //  پس ملاک دیگر **کدِ خطا نیست**: اگر سرورِ خودمان همین حالا جواب داد و
        //  هنوز مجوزِ معتبری روی دیسک نیست ⇒ وصلِ دوباره با توکنِ حساب. نرسیدن به
        //  سرور (`Reach != Online`) هیچ چیزی را کنار نمی‌گذارد.
        //  ⚠️ پذیرفتنِ کلیدِ تازه این‌جا همان استثنای «دستگاهِ خالی»ِ `BindAsync`
        //  است و از همان نشانیِ قفل‌شده و با توکنِ **حساب** می‌آید — نه راهِ تازه.
        var here = (_settings.CloudStationId ?? "").Trim();
        if (Activated && bindDue && acctStation.Length > 0 && Subscription.Active
            && (here.Length == 0 || string.Equals(acctStation, here, StringComparison.Ordinal))
            && !Verify().Valid)
        {
            try { await RefreshAsync(ct); }
            catch (OperationCanceledException) { throw; }
            catch { /* نرسیدن ⇒ پایین‌تر `Reach` می‌گوید */ }
            if (Activated && !Verify().Valid && Reach == CloudReach.Online
                && await ReseatToAccountPumpAsync(leaveOther: false, ct) && !Verify().Valid)
            {
                //  وصل شد ولی سرور مجوزی نداد ⇒ ده دقیقه صبر، نه هر دقیقه یک ثبتِ تازه
                LastBindWhy = "سرورِ حساب اشتراکِ این پمپ را فعال می‌گوید ولی برای این کامپیوتر مجوز نداد";
                _lastBindFailAt = AppClock.Mono;
            }
        }

        //  ⛔ کدِ پمپِ همین حساب همین‌جا می‌نشیند — نصبی که از قبل بند شده
        //  هیچ‌وقت دوباره ‎bind‎ نمی‌زند، پس بی این خط کدِ حسابش را هیچ‌وقت
        //  نمی‌گرفت و روی ‎pump1‎ی کهنه می‌ماند.
        if (StationCodeOf(json) is { Length: > 0 } acctCode
            && !string.Equals(acctCode, _settings.CloudStationCode, StringComparison.Ordinal))
        {
            _settings.CloudStationCode = acctCode;
            await SaveQuiet();
        }

        /*
         *  ⛔ نصبی که فقط وارد حساب شده، خودش بند می‌شود — بی هیچ کدی.
         *
         *  تا دیروز تنها راهِ گرفتنِ توکنِ دستگاه و مجوز، کدِ شش‌رقمی بود.
         *  یعنی صاحبِ پمپی که اشتراکش را مدیر روی **حسابش** گذاشته بود،
         *  باز هم بی کد نمی‌توانست برنامه را راه بیندازد — و بی مجوز،
         *  فهرستِ قابلیت‌های پلن هم به برنامه نمی‌رسید.
         *
         *  ⚠️ **پس از** سنجشِ «این حساب مالِ پمپِ دیگری است» می‌آید، وگرنه
         *  ورود با حسابِ پمپِ دیگر همین دستگاه را به آن پمپ می‌بست.
         *  ⚠️ و نشدنش این مسیر را نمی‌شکند: نشانیِ خانگی همان است که بود.
         */
        //  ⛔ **پس از یک شکست، حلقه هر دقیقه دوباره نمی‌زند** (۱۴۰۵/۰۷/۱۳،
        //  سنجهٔ `linkstates` روی سرورِ واقعی): `device/bind` سقفِ ده بار در
        //  ربع ساعت برای هر آی‌پی دارد. کامپیوتری که از پمپ جدا شده
        //  (`device_revoked`) هر دقیقه همان ۴۰۳ را می‌گرفت و در ده دقیقه سقف
        //  را پر می‌کرد — از آن به بعد حتی کلیکِ خودِ کاربر پس از برگرداندنش
        //  «تلاشِ زیاد» می‌دید، و کامپیوترِ دیگرِ همان پمپ پشتِ همان اینترنت
        //  هم. پس پس از شکست فقط هر ده دقیقه، و کلیکِ کاربر (`forceBind`)
        //  همیشه همین حالا.
        if (!Activated && acctStation.Length > 0 && bindDue)
        {
            //  ⛔ **نتیجه‌اش دیگر بلعیده نمی‌شود.** تا دیروز این خط هم
            //  استثنا را می‌خورد و هم مقدارِ بازگشتی را دور می‌ریخت، پس
            //  یک `key_mismatch`ِ دائمی هیچ‌جا دیده نمی‌شد و کاربر فقط
            //  «فعال نشده» را می‌دید بی این‌که بداند چرا.
            //  ⚠️ همچنان چیزی را نمی‌شکند: نشانیِ خانگی مهم‌تر است و
            //  مسیر ادامه می‌یابد؛ فقط دلیل ثبت می‌شود.
            try
            {
                var bind = await BindAsync(ct);
                LastBindWhy = bind.Ok ? "" : (bind.Why ?? "");
                _lastBindFailAt = bind.Ok ? DateTime.MinValue : AppClock.Mono;
            }
            catch (Exception ex) { LastBindWhy = ex.GetType().Name; _lastBindFailAt = AppClock.Mono; }
        }

        /*
         *  ⚠️ سنجشِ «پمپی هست؟» **پس از** بند شدن آمد، نه پیش از آن.
         *
         *  پمپی که هنوز سرورِ خانگی‌اش را معرفی نکرده `home` ندارد، و با
         *  ترتیبِ قبلی همان‌جا برمی‌گشتیم — پس نصبِ تازه هیچ‌وقت بند
         *  نمی‌شد و مجوزش را نمی‌گرفت. نشانیِ خانگی رفاه است، اشتراک اصل.
         */
        if (!json.TryGetProperty("home", out var home) || home.ValueKind != JsonValueKind.Object)
            return (false, "", "", "", "هنوز سرورِ خانگیِ این پمپ معرفی نشده است");

        return (true, Str(home, "url"), Str(home, "readKey"), Str(home, "station"), "");
    }
}
