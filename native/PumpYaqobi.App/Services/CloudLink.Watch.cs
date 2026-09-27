using PumpYaqobi.Domain;
namespace PumpYaqobi.App.Services;

public sealed partial class CloudLink
{
    /// <summary>
    /// ══ 🤖 پیگیرِ اشتراک — یک دور ═════════════════════════════════════════
    ///
    /// پس از <see cref="HomeFromAccountAsync"/> و <see cref="KeepLicenseFreshAsync"/>
    /// صدا می‌خورد، پس حرفِ همین لحظهٔ سرور را دارد. شرحِ کامل بالای
    /// <see cref="SubscriptionWatch"/>.
    ///
    /// ⚠️ ارزان است: وقتی سرور و این کامپیوتر یکی‌اند <b>هیچ درخواستی</b>
    /// نمی‌زند. و هیچ‌وقت استثنا بیرون نمی‌دهد.
    /// ⛔ هیچ پمپی نمی‌سازد و یک بیت از دفتر را لمس نمی‌کند.
    /// </summary>
    public async Task WatchSubscriptionAsync(bool force = false, CancellationToken ct = default)
    {
        //  ⚠️ حرفِ سرور را نشنیده‌ایم (آفلاین، یا بی حساب) ⇒ هیچ حکمی نیست.
        //  «اشتراک: نیست»ِ پیش‌فرض با مجوزِ سالم همیشه ناجور است.
        if (!_subscriptionKnown || !Activated) return;

        var v = Now();
        if (v.Agree) { SubscriptionWatch.Report(v, triedFix: false); return; }
        if (!SubscriptionWatch.FixDue(force)) { SubscriptionWatch.Report(v, triedFix: false); return; }

        //  ۱) مجوزِ تازه — همان درِ همیشگی
        var fixWhy = "";
        var refreshed = false;
        try
        {
            var r = await RefreshAsync(ct);
            refreshed = r.Ok;
            if (!r.Ok) fixWhy = r.Why;
        }
        catch (OperationCanceledException) { throw; }
        catch { fixWhy = "به سرورِ حساب نرسیدیم"; }
        v = Now();

        //  ۲) هنوز نه ⇒ این کامپیوتر را از نو به پمپِ همان حساب وصل کن —
        //  فقط اگر سرورِ خودمان همین حالا جواب داد و سرور واقعاً «فعال» می‌گوید
        //  (برداشتنِ اشتراک هیچ‌وقت با وصلِ دوباره «درست» نمی‌شود)، با همان
        //  ترمزِ ده‌دقیقه‌ایِ ثبت (سقفِ ده ‹bind› در ربع ساعت روی سرور).
        //  ⚠️ فقط وقتی این کامپیوتر روی **همان** پمپِ حساب است (یا شناسه‌ای
        //  ندارد): جابه‌جاییِ پمپ کارِ `HomeFromAccountAsync` است، با سنجشِ
        //  سرور (`adopt`)، نه کارِ این‌جا.
        var here = (_settings.CloudStationId ?? "").Trim();
        //  ⚠️ و **نه برای یک سکسکهٔ گذرا**: گرفتنِ مجوز که خودش شکست خورد (۵۰۰،
        //  قطعیِ لحظه‌ای) اول فقط دوباره امتحان می‌شود؛ وصلِ دوباره توکنِ
        //  دستگاه را عوض می‌کند و جای آن دو بار شکست یا مجوزی است که **رسید**
        //  و باز هم نمی‌خورد.
        if (!v.Agree && (refreshed || SubscriptionWatch.Fails >= 2)
            && Subscription.Active && SignedIn && Reach == CloudReach.Online
            && _acctStationSeen.Length > 0 && (here.Length == 0 || here == _acctStationSeen)
            && (force || AppClock.Mono - _lastBindFailAt >= BindRetryAfterFail))
        {
            try
            {
                if (await ReseatToAccountPumpAsync(leaveOther: false, ct)) v = Now();
                else fixWhy = LastBindWhy;
            }
            catch (OperationCanceledException) { throw; }
            catch { }
            if (!v.Agree) _lastBindFailAt = AppClock.Mono;
        }

        SubscriptionWatch.Report(v, triedFix: true, fixWhy);
        if (v.Agree)
        {
            try { LicenseChanged?.Invoke(); } catch { /* نمایش رفاه است */ }
        }

        SubVerdict Now()
        {
            var now = LicenseClock.Now(_settings);
            return SubscriptionWatch.Compare(Subscription, LicenseGuard.CheckStored(_settings, now), now);
        }
    }
}
