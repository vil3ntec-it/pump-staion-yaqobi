using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using PumpYaqobi.Application.Localization;
using PumpYaqobi.Domain;

namespace PumpYaqobi.App.Services;

/// <summary>تکه‌ای از <see cref="CloudLink"/> — تازه‌سازیِ مجوز، کدِ آفلاین و سپردنِ نشانیِ خانگی.</summary>
public sealed partial class CloudLink
{
    // ── تازه‌سازی ──────────────────────────────────────────────────────

    /// <summary>
    /// وضعیتِ اشتراک و مجوزِ تازه.
    ///
    /// هر بار که برنامه بالا می‌آید صدا زده می‌شود. نشدنش اشکالی ندارد:
    /// مجوزِ ذخیره‌شده تا ده روز آفلاین کار می‌کند.
    /// </summary>
    public async Task<CloudResult> RefreshAsync(CancellationToken ct = default)
    {
        if (!Activated) return CloudResult.No("این برنامه هنوز فعال نشده است", "not_activated");

        var (ok, me, why, errCode) = await DevGetAsync("/api/pump/device/me", ct);
        if (!ok) return CloudResult.No(why, errCode);

        //  ⚠️ **شناسهٔ پمپ هم مثلِ کلیدِ عمومی قفل است.** پیش از این هر چه
        //  سرور می‌گفت بی سنجش روی تنظیمات می‌نشست؛ یعنی اگر روزی همین
        //  توکن به پمپِ دیگری می‌خورد، برنامه بی‌صدا پمپش را عوض می‌کرد و
        //  از آن به بعد عکسِ حساب‌های این پمپ به پوشهٔ پمپِ دیگری می‌رفت.
        //  `stn`ِ مجوز هم روی همین سنجیده می‌شود (`LicenseGuard`)، پس
        //  عوض شدنش یعنی «یا دستگاه جابه‌جا شده یا کسی وسط نشسته».
        var seen = StationId(me);
        if (seen.Length > 0 && _settings.CloudStationId.Length > 0
            && !string.Equals(seen, _settings.CloudStationId, StringComparison.Ordinal))
            return CloudResult.No(
                "این دستگاه روی پمپِ دیگری ثبت شده است. اگر واقعاً پمپ را عوض کرده‌اید، "
                + "از «پروفایل ← جدا کردنِ این دستگاه» جدا کنید و با حسابِ همان پمپ دوباره وارد شوید.", "station_mismatch");
        if (seen.Length > 0) _settings.CloudStationId = seen;
        if (StationCodeOf(me) is { Length: > 0 } seenCode) _settings.CloudStationCode = seenCode;
        ReadSubscription(me);

        //  مجوزِ تازه — جدا، چون ممکن است اشتراک تمام شده باشد و مجوزی
        //  صادر نشود. آن هم یک جوابِ درست است، نه خطا.
        var (licOk, lic, licWhy, licCode) =
            await DevPostAsync("/api/pump/device/license", new { }, ct);
        var licBefore = _settings.CloudLicense ?? "";
        CloudResult? rejected = null;
        if (licOk)
        {
            var token = Str(lic, "license");
            if (token.Length == 0)
            {
                //  ⚠️ خالی بودن یعنی «اشتراک ندارد» — جوابِ **قطعیِ** سرور، پس
                //  مجوزِ قبلی را پاک می‌کنیم، وگرنه تا ده روز با مجوزِ کهنه
                //  باز می‌ماند. و چون ارفاق فقط از مجوز می‌آید، ارفاق هم همین
                //  لحظه تمام می‌شود.
                _settings.CloudLicense = "";
            }
            else rejected = AdoptLicense(token, Str(lic, "publicKey"));
        }

        _settings.CloudSyncedAt = AppClock.UnixMs;
        //  مُهرِ «دیدیم که باز است» — پایهٔ ارفاق (‎Entitlements.Grace‎). بی این،
        //  یک روزِ بی‌اینترنت می‌توانست کیو‌آر و اپِ کارمندانِ مشتریِ پول‌داده
        //  را خاموش کند.
        Entitlements.Remember(_settings, Subscription, Verify());
        await SaveQuiet();
        if ((_settings.CloudLicense ?? "") != licBefore)
        {
            try { LicenseChanged?.Invoke(); } catch { /* خبر رفاه است، مجوز اصل */ }
        }
        //  ⛔ **گرفتنِ مجوز نشد ⇒ «شد» نگوییم.** تا ۱۴۰۵/۰۷/۱۴ این‌جا `Done`
        //  برمی‌گشت و `KeepLicenseFreshAsync` همان را «رسید» می‌شمرد: یک خطای
        //  لحظه‌ایِ سرور درست سرِ دادنِ VIP یعنی مجوزِ کهنهٔ آزمایشی تا ده دقیقه
        //  و بیشتر سرِ جایش می‌ماند و هیچ‌کس نمی‌فهمید چرا.
        if (!licOk) return CloudResult.No(licWhy, licCode);
        return rejected ?? CloudResult.Done;
    }

    /// <summary>
    /// ══ مجوزِ تازهٔ سرور — <b>پیش از</b> نشستن سنجیده می‌شود ═════════════════
    ///
    /// ⛔ تا ۱۴۰۵/۰۷/۱۲ <see cref="RefreshAsync"/> هر رشته‌ای را که سرور
    /// می‌داد بی هیچ سنجشی روی مجوزِ سالمِ دیروز می‌نشاند — و اگر کلیدی قفل
    /// نبود، کلیدِ همان پاسخ را هم. یعنی پاسخی جعلی (یا یک باگِ سرور) مجوزِ
    /// واقعیِ مشتری را با زباله عوض می‌کرد.
    ///
    /// حالا: کلید همان قاعدهٔ <see cref="ActivateAsync"/> را دارد (قفل‌شده
    /// عوض نمی‌شود؛ با ریشهٔ اعتمادِ داخلِ برنامه فقط کلیدِ داخلِ فهرست)، و
    /// خودِ مجوز با <see cref="LicenseGuard"/> سنجیده می‌شود. امضا یا هویتش
    /// نخورد ⇒ مجوزِ قبلی <b>دست‌نخورده</b> می‌ماند و دلیلش برمی‌گردد
    /// (<c>key_mismatch</c> · <c>bad_license</c>).
    ///
    /// ⚠️ مجوزِ امضاشده‌ای که همین حالا رسیده، <c>iat</c>ش لنگرِ کفِ ساعت
    /// است (<see cref="LicenseClock.Anchor"/>) — تنها جایی که کفی که به خاطرِ
    /// ساعتِ اشتباهاً جلورفته بالا رفته، می‌تواند پایین بیاید.
    /// </summary>
    /// <returns><c>null</c> یعنی نشست؛ وگرنه چرا نه.</returns>
    private CloudResult? AdoptLicense(string token, string serverKey)
    {
        var pinned = (_settings.CloudPublicKey ?? "").Trim();
        var key = pinned;
        if (CloudConfig.LicenseKeys.Count > 0)
        {
            //  کلیدِ روی دیسک این‌جا هیچ اثری ندارد؛ سنجش با خودِ فهرست است
            if (serverKey.Length > 0 && !CloudConfig.TrustsKey(serverKey)) return KeyMismatch();
            if (serverKey.Length > 0) key = serverKey;
        }
        else if (pinned.Length == 0)
        {
            if (serverKey.Length == 0)
                return CloudResult.No("سرور کلیدِ عمومی نداد؛ مجوزِ تازه سنجیده نشد", "bad_license");
            key = serverKey;
        }
        else if (serverKey.Length > 0 && serverKey != pinned)
        {
            return KeyMismatch();
        }

        //  ⚠️ «حالا» عمداً کفِ ساعت است؛ اگر کف اشتباهاً جلو رفته باشد مجوزِ
        //  تازه «منقضی» دیده می‌شود ولی امضایش سالم است — و همان لنگر کف را
        //  درست می‌کند و بعد دوباره سنجیده می‌شود.
        var check = LicenseGuard.Check(token, key, DeviceUid, _settings.CloudStationId,
                                       LicenseClock.Now(_settings));
        if (!check.SignatureOk)
            return CloudResult.No("مجوزی که سرور داد سنجیده نشد: " + check.Reason, "bad_license");

        LicenseClock.Anchor(_settings, check.IssuedAt, fresh: true);
        _settings.CloudLicense = token;
        if (key != pinned) _settings.CloudPublicKey = key;
        CloudConfig.RecordMachine(_settings);
        return null;
    }

    /// <summary>
    /// هر ده دقیقه یک بار، مجوز از نو گرفته می‌شود — حتی اگر هیچ چیزی
    /// عوض نشده باشد. سقفِ «دیر فهمیدن» است، نه راهِ اصلی.
    /// </summary>
    public static readonly TimeSpan LicenseTick = TimeSpan.FromMinutes(10);

    /// <summary>
    /// ══ اشتراکی که مدیر همین حالا داد، باید همین حالا برسد ═══════════════
    ///
    /// خواستهٔ صریحِ صاحب ریپو (۱۴۰۵/۰۷/۰۸): «اشتراک که می‌دم … برنامه اونو
    /// دریافت کنه و قفل‌ها باز بشه و درست کار کنه… و بتونم اشتراکشو بردارم
    /// یا روش اضافه کنم.»
    ///
    /// ⛔ <b>و تا امروز نمی‌رسید.</b> حلقهٔ شصت‌ثانیه‌ایِ
    /// <c>StationPublisher.CloudKeepAsync</c> فقط
    /// <see cref="HomeFromAccountAsync"/> را می‌زد، و آن برای دستگاهی که
    /// <b>از قبل بند شده</b> هیچ مجوزی نمی‌گیرد —
    /// <see cref="RefreshAsync"/> تنها جای گرفتنِ مجوز است و **تنها
    /// صداکننده‌اش صفحهٔ پروفایل بود**. یعنی:
    ///
    /// <list type="bullet">
    ///   <item>اشتراک دادم ⇒ <c>/api/pump/me</c> می‌گفت «فعال» و
    ///     <see cref="Entitlements.Remember"/> مُهرِ ارفاق را جلو می‌برد،
    ///     ولی <b>فهرستِ قابلیت‌ها نمی‌آمد</b> — و فهرستِ نیامده یعنی
    ///     «پلنِ کامل» (<see cref="LicenseCheck.HasFeatureList"/>). پس
    ///     مشتریِ <b>استاندارد</b> هر شش قفل را باز می‌دید. مستقیم روی
    ///     پول، و از همان دری که ۱۴۰۵/۰۶/۳۱ یک بار بسته شده بود.</item>
    ///   <item>اشتراک را برداشتم ⇒ مجوزِ کهنه روی دیسک می‌ماند و تا
    ///     انقضای خودش باز بود، و بعدش هم چهارده روز ارفاق.</item>
    /// </list>
    ///
    /// ── تصمیم، و چرا ارزان است ──────────────────────────────────────────
    ///
    /// <c>/api/pump/me</c> در همان دورِ شصت‌ثانیه‌ای می‌گوید اشتراک فعال است
    /// یا نه. اگر آن حرف با <b>مجوزِ روی دیسک</b> جور نبود، همان لحظه مجوز
    /// از نو گرفته می‌شود. پس:
    ///
    /// <code>
    ///   مدیر اشتراک داد   ⇒ me: فعال · مجوز: بسته  ⇒ ناجور ⇒ همین حالا
    ///   مدیر اشتراک گرفت  ⇒ me: خاموش · مجوز: باز  ⇒ ناجور ⇒ همین حالا
    ///   هیچ چیزی عوض نشد  ⇒ جور  ⇒ هیچ درخواستی، تا تیکِ ده‌دقیقه‌ای
    /// </code>
    ///
    /// ⚠️ <b>و ده‌دقیقه‌ای لازم است، نه تجمل</b>: عوض شدنِ <b>پلن</b>
    /// (استاندارد ⇒ وی‌آی‌پی) هر دو طرف را «فعال» نگه می‌دارد، پس از راهِ
    /// ناجوری دیده نمی‌شود. آن سقف تنها چیزی است که فهرستِ تازهٔ
    /// قابلیت‌ها را می‌آورد.
    ///
    /// ⚠️ <b>هیچ‌وقت استثنا بیرون نمی‌دهد و هیچ‌وقت چیزی را نمی‌بندد</b>:
    /// بی‌اینترنت یعنی همان مجوزِ دیروز، و ارفاقِ چهارده‌روزه سرِ جایش.
    /// </summary>
    public async Task KeepLicenseFreshAsync(CancellationToken ct = default)
    {
        if (!Activated) return;

        var now = AppClock.UnixMs;
        var since = now - _settings.CloudSyncedAt;
        //  ⛔ `since < 0` یعنی مُهرِ روی دیسک از آینده است (ساعتِ ویندوز روزی جلو
        //  بود) — «هنوز نرسیده» نیست، «همین حالا» است؛ وگرنه تازه‌سازیِ مجوز
        //  تا رسیدنِ همان آینده خاموش می‌ماند (۱۴۰۵/۰۷/۱۵).
        var due = _settings.CloudSyncedAt <= 0 || since < 0 || since >= (long)LicenseTick.TotalMilliseconds;

        //  ⚠️ «مجوز چه می‌گوید» را از خودِ `LicenseGuard` می‌پرسیم، نه از
        //  مُهرِ ارفاق: ارفاق عمداً دیر می‌بندد و این‌جا باید حقیقتِ همین
        //  لحظه را بدانیم، وگرنه برداشتنِ اشتراک دو هفته دیده نمی‌شد.
        //  ⚠️ فقط اگر این نمونه واقعاً حرفِ سرور را شنیده باشد — وگرنه
        //  «اشتراک: نیست»ِ پیش‌فرض همیشه با مجوزِ سالم ناجور است.
        var mismatch = _subscriptionKnown && Verify().Valid != Subscription.Active;

        //  ⛔ **«سرور چیزِ تازه‌ای گفت» ⇒ یک بار مجوز.** «باز/بسته» تنها
        //  ناجوری نیست: دورهٔ آزمایشی ⇒ وی‌آی‌پی، تمدید، و استاندارد ⇒
        //  وی‌آی‌پی هر دو طرف را «باز» نگه می‌دارند، پس تا ۱۴۰۵/۰۷/۱۳ اشتراکی
        //  که مدیر به حسابِ **آزمایشی** می‌داد تا ده دقیقه به برنامه
        //  نمی‌رسید (سنجهٔ `livestack` با سرورِ واقعی گرفتش: نود ثانیه، هنوز
        //  «دورهٔ آزمایشی · ۲۹ روز»). حالا هر بار که حالِ خوانده‌شده از
        //  `/api/pump/me` با آخرین حالی که مجوزش را گرفته‌ایم فرق کند، همان
        //  دور مجوز می‌آید.
        //  ⚠️ کلید «روزِ مانده» ندارد (هر روز عوض می‌شود) و فقط **یک بار** به
        //  ازای هر تغییر می‌زند، پس ترمزِ «هیچ چیزی عوض نشد ⇒ صفر درخواست»
        //  سرِ جایش است.
        //  ⚠️ «آخرین حال» به **همان مجوزِ روی دیسک** بسته است، نه به حافظهٔ
        //  خام: مجوزی که این نمونه نگرفته (برنامهٔ تازه‌بازشده، پروفایلی که
        //  خودش تازه کرد) نخستین دیدنش «خطِ پایه» است، نه «چیزِ تازه» — وگرنه
        //  هر بالا آمدنِ برنامه یک درخواستِ بی‌دلیل می‌زد.
        var key = SubKey(Subscription);
        var lic = _settings.CloudLicense ?? "";
        var news = false;
        if (_subscriptionKnown)
        {
            if (_lastSub.License != lic) _lastSub = (lic, key);
            else news = _lastSub.Key != key;
        }

        if (!due && !mismatch && !news) return;
        try
        {
            var r = await RefreshAsync(ct);
            //  ⚠️ کلیدِ **پیش از** تازه‌سازی ثبت می‌شود: `RefreshAsync` حال را
            //  از `device/me` می‌خواند که شکلِ دیگری دارد، و ثبتِ آن یعنی
            //  دورِ بعد `/api/pump/me` «تازه» دیده می‌شد — یک درخواست هر دقیقه.
            if (r.Ok) _lastSub = (_settings.CloudLicense ?? "", key);
        }
        catch (OperationCanceledException) { throw; }
        catch { /* بی‌اینترنت خطا نیست — مجوزِ دیروز سرِ جایش است */ }
    }

    /// <summary>آخرین حالِ اشتراک که مجوزش گرفته شد — کلیدِ «چیزِ تازه‌ای شد؟».</summary>
    private static (string License, string Key) _lastSub = ("\u0000", "");

    private static string SubKey(PumpSubscription s) =>
        $"{s.Active}|{s.Source}|{s.PlanTitle}|{s.EndsAt / 3_600_000}|{string.Join(",", s.Features)}";

    /// <summary>
    /// مجوزِ روی دیسک عوض شد — تنها خبرِ آن. پوستهٔ برنامه با همین سربرگ و
    /// پروفایل را از نو می‌خواند (<c>MainViewModel</c>).
    ///
    /// ⛔ بی این، مجوزی که حلقهٔ پس‌زمینه گرفته بود فقط با **باز کردنِ
    /// دوبارهٔ پروفایل** دیده می‌شد: دکمهٔ سربرگ همچنان «VIP · ۲۹ روز» ِ
    /// دیروز را می‌گفت و صاحبِ پمپ گمان می‌کرد اشتراک نرسید.
    /// </summary>
    public static event Action? LicenseChanged;

    private static DateTime _offlineNextTry = DateTime.MinValue;

    /// <summary>
    /// ══ کدِ اشتراکِ آفلاین ⇒ سرورِ حساب ═════════════════════════════════════
    ///
    /// «اگه یارو اینترنت پیدا کرد… سرور همون کد رو ببینه و بگه آره این حساب
    /// اشتراک داره.» کامپیوتری که کدِ آفلاین دارد و حالا به پمپی بند است، کد
    /// و کدِ کامپیوترِ خودش را یک بار به <c>/api/pump/device/offline-code</c>
    /// می‌برد؛ سرور امضا و کامپیوتر را می‌سنجد و اشتراک روی همان پمپ
    /// می‌نشیند (کوتاه‌تر کردنِ اشتراکِ بلندترِ موجود هرگز). بعد همان لحظه
    /// مجوزِ تازه گرفته می‌شود.
    ///
    /// ⚡ یک بار برای هر (سریال، پمپ) — نشانش <c>OfflineCodeRedeemed</c> است؛
    /// پس هر دور هیچ درخواستی نمی‌زند. شکست ⇒ ده دقیقه بعد.
    /// ⛔ کدِ باطل‌شده روی سرور (۴۱۰) از همین کامپیوتر هم برداشته می‌شود.
    /// ⛔ هیچ‌وقت استثنا بیرون نمی‌دهد و نرسیدن هیچ چیزی را پاک نمی‌کند.
    /// </summary>
    public async Task<CloudResult> RedeemOfflineAsync(CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_settings.OfflineCode) || !Activated) return CloudResult.Done;
            var chk = OfflineKey.Stored(_settings, LicenseClock.Now(_settings));
            if (!chk.Genuine) return CloudResult.Done;
            var mark = chk.Serial + "@" + _settings.CloudStationId;
            if (_settings.OfflineCodeRedeemed.StartsWith(mark, StringComparison.Ordinal)) return CloudResult.Done;
            if (AppClock.Mono < _offlineNextTry) return CloudResult.Done;

            var (ok, _, why, code) = await DevPostAsync("/api/pump/device/offline-code",
                new { code = chk.Canonical, computer = OfflineKey.ComputerCode() }, ct);
            if (!ok)
            {
                if (code == "code_revoked")
                {
                    _settings.OfflineCode = "";
                    _settings.OfflineCodeRedeemed = "";
                    await _save();
                    NotifyLicenseChanged();
                    return CloudResult.No(why, code);
                }
                //  کدی که پمپِ دیگری پیش از این برداشته، یا برای کامپیوترِ دیگر
                //  است: دوباره زدنش هر دقیقه جوابِ دیگری نمی‌گیرد.
                if (code is "code_used_elsewhere" or "computer_mismatch" or "bad_offline_code" or "account_mismatch")
                {
                    _settings.OfflineCodeRedeemed = mark + "!" + code;
                    await _save();
                }
                else _offlineNextTry = AppClock.Mono.AddMinutes(10);
                return CloudResult.No(why, code);
            }
            _settings.OfflineCodeRedeemed = mark;
            await _save();
            await RefreshAsync(ct);
            return CloudResult.Done;
        }
        catch (Exception ex)
        {
            _offlineNextTry = AppClock.Mono.AddMinutes(10);
            return CloudResult.No(ErrorText.Friendly(ex), "error");
        }
    }

    /// <summary>
    /// همان خبر، از بیرونِ این کلاس — کدِ اشتراکِ آفلاین (‎OfflineKey.Apply‎)
    /// هم قفل‌ها را جابه‌جا می‌کند و پوسته باید همان کارِ «مجوزِ تازه» را بکند.
    /// </summary>
    public static void NotifyLicenseChanged()
    {
        try { LicenseChanged?.Invoke(); } catch { /* خبر رفاه است */ }
    }

    /// <summary>
    /// سپردنِ نشانی و رمزِ فقط‌خواندنیِ سرورِ خانگی به ابر.
    ///
    /// همان چیزی که اپِ کارمند را از پرسیدنِ آدرس بی‌نیاز می‌کند: آی‌پیِ
    /// خانگی با هر بار روشن شدنِ مودم عوض می‌شود، پس این باید پرتکرار و
    /// ارزان باشد.
    /// </summary>
    public async Task<CloudResult> PublishHomeAsync(string homeUrl, string readKey,
        CancellationToken ct = default, string station = "")
    {
        if (!Activated) return CloudResult.No("فعال نشده", "not_activated");
        if (string.IsNullOrWhiteSpace(homeUrl)) return CloudResult.No("نشانیِ خانگی خالی است");

        //  ⛔ کدِ پوشهٔ **واقعیِ** همین پمپ روی سرورِ خانگی هم می‌رود (۱۴۰۵/۰۷/۱۳):
        //  اپِ کارمندان با همین پوشه را می‌پرسد. تا امروز سرورِ حساب کورکورانه
        //  کدِ خودش را به گوشی می‌داد، و پوشهٔ برنامه («pump1» یا جایگزینِ
        //  حساب) چیزِ دیگری بود — یعنی درِ شبکهٔ پمپ هیچ‌وقت درست باز نمی‌شد.
        //  سرورِ حسابِ کهنه این فیلد را نادیده می‌گیرد.
        var (ok, _, why, code) = await DevPostAsync("/api/pump/device/home",
            new { homeUrl, readKey = readKey ?? "", station = (station ?? "").Trim() }, ct);
        return ok ? CloudResult.Done : CloudResult.No(why, code);
    }

    /// <summary>
    /// نوشتنِ یک فایل در پوشهٔ ابریِ همین پمپ — ‎PUT /api/pump/device/files/&lt;نام&gt;‎.
    /// برای کیو‌آرِ زنده (‎AcctLive‎). سرور خودش اشتراک را می‌سنجد؛ اگر تمام
    /// شده باشد ‎subscription_required‎ برمی‌گردد و این‌جا فقط ‎false‎ می‌شود.
    /// </summary>
    public async Task<CloudResult> PutFileAsync(string name, object data, CancellationToken ct = default)
    {
        if (!Activated) return CloudResult.No("فعال نشده", "not_activated");
        if (string.IsNullOrWhiteSpace(name)) return CloudResult.No("نامِ فایل خالی است");

        var (ok, _, why, code) = await DevPutAsync("/api/pump/device/files/" + Uri.EscapeDataString(name.Trim()),
            new { data }, ct);
        return ok ? CloudResult.Done : CloudResult.No(why, code);
    }
}
