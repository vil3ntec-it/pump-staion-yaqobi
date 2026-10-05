using Avalonia.Controls;
using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PumpYaqobi.App.Services;
using PumpYaqobi.App.Themes;
using PumpYaqobi.App.ViewModels.Sections;
using PumpYaqobi.Application.Localization;
using PumpYaqobi.Application.Services;
using PumpYaqobi.Domain.Enums;
using PumpYaqobi.Domain;

namespace PumpYaqobi.App.ViewModels;

//  ⛔ شورا ج۴: بخشی از ‎MainViewModel‎ — چراغ‌های سربرگ: سرورِ خانگی، سرورِ حساب، همگام‌سازی و پردهٔ آوردنِ حساب. فقط جابه‌جاییِ همان عضوها از ‎MainViewModel.cs‎، بی تغییرِ یک رفتار.
public sealed partial class MainViewModel
{
    /// <summary>
    /// ══ چراغِ سرور در سربرگ ══════════════════════════════════════════════════
    /// سبز = به سرورِ خانگی وصل‌ایم؛ سرخ = سرور تنظیم شده ولی جواب نمی‌دهد؛
    /// خاکستری = سروری تنظیم نشده. با تیکِ ساعتِ پنجره تازه می‌شود
    /// (<see cref="TickServerDot"/>) — فقط خواندنِ دو ویژگی، هیچ درخواستی.
    /// </summary>
    [ObservableProperty] private string _serverDotBrushKey = "Pump.Muted";
    [ObservableProperty] private string _serverDotReason = "سرور تنظیم نشده";

    public void TickServerDot()
    {
        var sync = AppHost.Current.PublisherIfStarted;
        string key, why;
        //  ⛔ پلنی که خدماتِ سرور ندارد اصلاً وصل نمی‌شود (۱۴۰۵/۰۷/۲۰) — خرابی نیست،
        //  خاکستری است و چراغِ یکی‌شده با سرورِ حسابِ سالم سبز می‌ماند.
        DeviceBound();
        if (_planOfflineCache)
        {
            key = "Pump.Muted";
            why = "سرورِ خانگی در پلنِ شما نیست — دفتر فقط روی همین کامپیوتر است";
        }
        else if (sync is null || !sync.Configured)
        {
            key = "Pump.Muted";
            //  ⚠️ «از پروفایل وارد شوید» غلط بود: یافتنِ سرورِ خانگی هیچ حسابی
            //  نمی‌خواهد، و آن جمله کاربر را دنبالِ ورود می‌فرستاد
            why = "هنوز به سرورِ خانگی وصل نشده — خودش در همین شبکه دنبالش می‌گردد؛ برای همین حالا، روی چراغ بزنید";
        }
        else if (sync.Connected)
        {
            key = "Pump.Ok";
            why = sync.Mode == Services.HomeSyncMode.Station
                ? "به سرورِ خانگی وصل است"
                : "به سرورِ خانگی وصل است (درِ قدیمی)";
        }
        else
        {
            key = "Pump.Danger";
            //  ⚠️ «آخرین وصل» را می‌گوید تا کاربر بداند همین حالا قطع شده یا
            //  از اول وصل نشده — و این‌که هر پنج ثانیه خودش دوباره می‌گردد.
            var last = sync.LastLinkedAt is { } t ? $" · آخرین وصل: {t:HH:mm}" : "";
            why = "سرورِ خانگی جواب نمی‌دهد — خاموش است یا شبکه قطع است" + last
                + " · هر پنج ثانیه خودش دوباره می‌گردد؛ برای بررسیِ همین حالا کلیک کنید";
        }
        if (key != ServerDotBrushKey) ServerDotBrushKey = key;
        if (why != ServerDotReason) ServerDotReason = why;
    }

    // ══ ● چراغِ دوم: ابر ═══════════════════════════════════════════════════
    //
    //  گزارشِ صاحب ریپو (۱۴۰۵/۰۷/۰۱): «ببین برنامه‌ها چرا به سرور وصل
    //  نمی‌شوند… و هیچ کلکِ دروغی نباشد که بگوید وصل است.»
    //
    //  ⚠️ **دو سرور داریم و یکی نیستند.** چراغِ اول مالِ سرورِ **خانگی** است
    //  (دفتر و دادهٔ زنده، در شبکهٔ خودِ پمپ). این یکی مالِ **ابر** است
    //  (حساب و اشتراک). تا امروز فقط اولی چراغ داشت، پس کاربر یک سبز
    //  می‌دید و گمان می‌کرد همه‌چیز وصل است — در حالی که ممکن بود برنامه
    //  هیچ‌وقت به ابر نرسیده باشد.
    //
    //  ⛔ **سبز فقط با جوابِ واقعی.** این‌جا هیچ تصمیمی گرفته نمی‌شود؛ فقط
    //  `CloudLink.Reach` خوانده می‌شود که خودش از `SendFull` پر می‌شود.
    //  «هنوز نپرسیده‌ایم» خاکستری است، نه سبز.
    //  ⛔ و هیچ نام و نشانیِ سروری نوشته نمی‌شود — همان قاعدهٔ چراغِ اول.

    [ObservableProperty] private string _cloudDotBrushKey = "Pump.Muted";
    [ObservableProperty] private string _cloudDotReason = "هنوز با سرورِ حساب تماس نگرفته‌ایم";

    //  ⚠️ «این کامپیوتر به پمپی بند است؟» — از تنظیماتِ روی دیسک، ولی **هر ده
    //  ثانیه یک بار**: این تیک هر ثانیه می‌دود و خواندنِ هر ثانیهٔ فایلِ
    //  تنظیمات (با رازهای رمزشده) همان کارِ دوره‌ایِ بی‌ترمزی است که قدغن است.
    private bool _boundCache;
    private bool _signedInCache;
    private bool _planOfflineCache;
    private DateTime _boundAt = DateTime.MinValue;
    private bool DeviceBound()
    {
        if (AppClock.Mono - _boundAt < TimeSpan.FromSeconds(10)) return _boundCache;
        _boundAt = AppClock.Mono;
        try
        {
            var f = Services.AppSettings.Load();
            _boundCache = !string.IsNullOrWhiteSpace(f.CloudDeviceToken);
            _signedInCache = !string.IsNullOrWhiteSpace(f.CloudAccountToken);
            _planOfflineCache = Services.Entitlements.PlanDenies(Services.Entitlements.Online);
        }
        catch { /* همان مقدارِ قبلی */ }
        return _boundCache;
    }

    /// <summary>
    /// ⛔ <b>چرا این کامپیوتر ثبت نیست — و کلیک چه می‌کند.</b> تنها جای این جمله.
    ///
    /// گزارشِ صاحب ریپو (۱۴۰۵/۰۷/۱۳، عکسِ چراغ): «ببین الان این وصل نمی‌شه».
    /// سنجهٔ <c>linkstates</c> روی پشتهٔ واقعی نشان داد که یک جمله برای سه
    /// حالِ جدا گفته می‌شد — «در پروفایل پمپ را بسازید» — در حالی که:
    /// نصبِ بی‌حساب اصلاً پمپی نمی‌تواند بسازد (اول باید وارد شود)، و حسابی
    /// که پمپ دارد پمپِ تازه نمی‌خواهد، ثبتِ همین کامپیوتر را می‌خواهد. هر
    /// حال حالا جملهٔ خودش را دارد، و کلیکِ چراغ همان کار را **انجام می‌دهد**.
    /// </summary>
    public static string UnboundWhy(bool signedIn) =>
        !signedIn
            ? "هنوز وارد حساب نشده‌اید — برای ورود روی چراغ بزنید"
            : Services.CloudLink.AccountHasStation == false
                ? "پمپِ حسابتان در حالِ آماده شدن است — پروفایل را باز کنید"
                : Services.CloudLink.LastBindWhy is { Length: > 0 } why
                    ? "وصل کردنِ این کامپیوتر به پمپ هنوز نشد: " + why + " — خودش دوباره امتحان می‌کند"
                    : "این کامپیوتر در حالِ وصل شدن به پمپِ حسابتان است";

    public void TickCloudDot()
    {
        string key, why;
        switch (Services.CloudLink.Reach)
        {
            case Services.CloudReach.Online:
                //  ⛔ **جواب داد ⇒ سبز — با حساب یا بی حساب** (۱۴۰۵/۰۷/۱۳، صاحب
                //  ریپو: «وقتی که حساب هم نداشته باشم اون سرور باید بگه که وصل
                //  است و اگه وصل بود باید سبز بشه»). این چراغ حالِ **سرور** را
                //  می‌گوید؛ «این کامپیوتر ثبت است؟» مالِ پروفایل است و این‌جا
                //  فقط یک خطِ آگاهی زیرِ همان جمله است، نه رنگ.
                key = "Pump.Ok";
                why = "به سرورِ حساب وصل است"
                    + (Services.CloudLink.CloudOkAt is { } at ? $" · آخرین جواب: {at:HH:mm}" : "")
                    + (DeviceBound() ? "" : " · " + UnboundWhy(_signedInCache));
                break;

            case Services.CloudReach.Offline:
                key = "Pump.Danger";
                var seen = Services.CloudLink.CloudOkAt is { } ok ? $" · آخرین جوابِ درست: {ok:HH:mm}" : "";
                var reason = Services.CloudLink.CloudWhy.Length > 0
                    ? " — " + Services.CloudLink.CloudWhy : "";
                why = "به سرورِ حساب نمی‌رسیم" + reason + seen
                    + " · هر دقیقه خودش دوباره می‌گردد؛ برای بررسیِ همین حالا کلیک کنید";
                break;

            default:
                key = "Pump.Muted";
                why = "هنوز با سرورِ حساب تماس نگرفته‌ایم — برای بررسیِ همین حالا کلیک کنید";
                break;
        }
        if (key != CloudDotBrushKey) CloudDotBrushKey = key;
        if (why != CloudDotReason) CloudDotReason = why;
    }

    // ══ ● و در سربرگ **یک** چراغ دیده می‌شود، نه دو ═══════════════════════
    //
    //  گزارشِ صاحب ریپو با عکس (۱۴۰۵/۰۷/۱۰): «چرا دو نوع سرور رو برای من
    //  نشون میده؟ یکی باشه اصلی که واقعاً نشون بده که وصل است یا که نه؛
    //  الان دوتا استن، یکی میگه وصل یکی میگه قط.»
    //
    //  ⚠️ **و او حق دارد.** یادداشتِ ۱۴۰۵/۰۷/۰۱ نوشته بود «دو چراغ یعنی دو
    //  سرورِ جدا و این عمدی است» — آن وقتی درست بود که مسئله «یک سبز دیده
    //  می‌شد و کاربر گمان می‌کرد هر دو وصل‌اند» بود. ولی دو چراغِ بی‌برچسب
    //  کنارِ هم همان سردرگمی را از درِ دیگر می‌سازد: کاربر نمی‌داند کدام
    //  کدام است و کدام را باور کند.
    //
    //  ⛔ **راهِ درست یکی کردنِ دو حقیقت نبود، یکی کردنِ دو چراغ بود.**
    //  این‌جا هیچ تصمیمِ تازه‌ای گرفته نمی‌شود: خروجیِ همان دو تیکِ بالا
    //  خوانده می‌شود و بس. دو منبعِ حقیقت سرِ جایشان‌اند، فقط یک چراغ
    //  نشانشان می‌دهد.
    //
    //  ⛔ **و سبز دروغ نمی‌گوید.** «یکی وصل، یکی نه» نه سبز است نه سرخ —
    //  زرد است، و ‎ToolTip‎ می‌گوید کدام کدام. سبز کردنش همان «کلکِ دروغ»ی
    //  است که در این ریپو قدغن است، و سرخ کردنش می‌گوید هیچ چیزی کار
    //  نمی‌کند در حالی که نیمی از برنامه سرِ جایش است.
    //
    //  ⛔ و همچنان **هیچ نام و نشانیِ سروری** نوشته نمی‌شود: متن از همان دو
    //  ‎…DotReason‎ می‌آید که خودشان این قاعده را دارند.

    [ObservableProperty] private string _linkDotBrushKey = "Pump.Muted";
    [ObservableProperty] private string _linkDotReason = "هنوز سروری تنظیم نشده";

    public void TickLinkDot()
    {
        TickServerDot();
        TickCloudDot();

        var home = ServerDotBrushKey;
        var acct = CloudDotBrushKey;
        var ok = (home == "Pump.Ok" ? 1 : 0) + (acct == "Pump.Ok" ? 1 : 0);
        var bad = (home == "Pump.Danger" ? 1 : 0) + (acct == "Pump.Danger" ? 1 : 0);
        var warn = acct == "Pump.Warn" ? 1 : 0;

        string key, head;
        if (ok == 2)            { key = "Pump.Ok";     head = "✅ هر دو سرور وصل‌اند"; }
        //  ⛔ سرورِ حساب جواب می‌دهد و سرورِ خانگی **خراب** نیست (فقط هنوز
        //  پیدا/تنظیم نشده) ⇒ سبز (۱۴۰۵/۰۷/۱۳، صاحب ریپو: «اگه وصل بود باید
        //  سبز بشه»). خانگیِ پیداشده‌ای که جواب نمی‌دهد همچنان زرد است.
        else if (acct == "Pump.Ok" && home != "Pump.Danger") { key = "Pump.Ok"; head = "✅ به سرور وصل است"; }
        else if (bad > 0 && ok + warn > 0) { key = "Pump.Warn"; head = "⚠️ یکی وصل است و یکی نه"; }
        //  ⛔ «سرورِ حساب جواب می‌دهد ولی پمپی نیست» خاکستریِ «سروری تنظیم نشده»
        //  نیست — همان جمله‌ای است که کاربر باید ببیند (۱۴۰۵/۰۷/۱۳).
        else if (warn > 0)      { key = "Pump.Warn";   head = _signedInCache ? "⚠️ این کامپیوتر هنوز به هیچ پمپی ثبت نشده" : "⚠️ هنوز وارد حساب نشده‌اید"; }
        //  ⛔ «یکی وصل، دیگری هنوز تنظیم نشده» سبز نیست (۱۴۰۵/۰۷/۱۳): سنجهٔ
        //  ‎livestack‎ چراغ را سبز دید در حالی که سرورِ خانگی اصلاً وصل نشده بود —
        //  همان «کلکِ دروغ». زرد است و دلیلش در همان کادر.
        else if (ok == 1)       { key = "Pump.Warn";   head = "⚠️ یکی وصل است، دیگری هنوز وصل نشده"; }
        else if (bad > 0)       { key = "Pump.Danger"; head = "❌ به سرور وصل نیستیم"; }
        else                    { key = "Pump.Muted";  head = "هنوز سروری تنظیم نشده"; }

        //  ⚠️ جملهٔ هر سرور دوباره نوشته نمی‌شود — همان‌هایی که بالا ساخته
        //  شدند این‌جا کنارِ هم می‌نشینند. دو جای نوشتن یعنی روزی چراغ یک
        //  چیز می‌گوید و ‎ToolTip‎ چیزِ دیگر.
        var why = head + "\n• " + ServerDotReason + "\n• " + CloudDotReason;

        if (key != LinkDotBrushKey) LinkDotBrushKey = key;
        if (why != LinkDotReason) LinkDotReason = why;
    }

    /// <summary>
    /// کلیکِ همان یک چراغ — هر دو سرور را همین حالا می‌پرسد.
    ///
    /// ⚠️ منطقِ پرسیدن دوباره نوشته نشد: همان دو تابعِ موجود پشتِ سرِ هم
    /// صدا زده می‌شوند، پس پیام و رفتارِ هر کدام همان است که بود.
    /// </summary>
    [RelayCommand]
    private async Task CheckLinksAsync()
    {
        await CheckServerAsync();
        await CheckCloudAsync();
        TickLinkDot();
    }

    // ══ شورا، ث۴ — «یک چراغ که آدم بفهمد» ═════════════════════════════════
    //  کلیکِ چراغ یک کادرِ کوچکِ سه‌ردیفی هم باز می‌کند: سرورِ حساب، سرورِ
    //  خانگی، همگام‌سازی — هر کدام جملهٔ آدمیزادِ خودش (همان ‎…DotReason‎ها،
    //  ⛔ بی هیچ نام و نشانیِ سروری) و یک دکمهٔ کار. ⛔ هیچ تصمیمِ تازه‌ای:
    //  هر دکمه همان تابعِ موجود را می‌زند.

    /// <summary>ردیفِ «سرورِ حساب» ⇒ «همین حالا بپرس».</summary>
    [RelayCommand]
    private async Task AskAccountNowAsync() { await CheckCloudAsync(); TickLinkDot(); }

    /// <summary>ردیفِ «سرورِ خانگی» ⇒ «بگرد».</summary>
    [RelayCommand]
    private async Task FindHomeNowAsync() { await CheckServerAsync(); TickLinkDot(); }

    // ══ ● چراغِ سوم: همگام‌سازی — در نوارِ **پایینِ** پنجره ═══════════════
    //
    //  بندِ ۱۳ی پرامپتِ ۲۲: «نمایشِ وضعیتِ Sync در نوارِ وضعیتِ پایینِ پنجره
    //  (سبز/زرد/خاکستری/قرمز).»
    //
    //  ⛔ **هیچ نام و نشانیِ سروری نوشته نمی‌شود** — همان قاعدهٔ دو چراغِ
    //  سربرگ. دلیل فقط در ‎ToolTip‎ است.
    //  ⚠️ و هیچ تصمیمی این‌جا گرفته نمی‌شود: فقط ‎SyncEngine‎ خوانده می‌شود،
    //  که خودش از جوابِ واقعیِ سرور پر می‌شود.

    [ObservableProperty] private string _syncDotBrushKey = "Pump.Muted";
    [ObservableProperty] private string _syncDotReason = "همگام‌سازی هنوز شروع نشده";
    [ObservableProperty] private string _syncDotText = "همگام‌سازی";

    public void TickSyncDot()
    {
        var sync = AppHost.Current.SyncIfStarted;
        if (sync is null)
        {
            SyncDotBrushKey = "Pump.Muted";
            SyncDotReason = "همگام‌سازی هنوز شروع نشده";
            SyncDotText = "همگام‌سازی";
            return;
        }

        var key = sync.Light switch
        {
            SyncLight.Synced => "Pump.Ok",
            SyncLight.Queued => "Pump.Warn",
            SyncLight.Failed => "Pump.Danger",
            _ => "Pump.Muted",
        };
        var text = sync.Light switch
        {
            SyncLight.Synced => "همگام",
            SyncLight.Queued => sync.Queued > 0 ? $"{sync.Queued} در صف" : "در صف",
            SyncLight.Failed => "همگام نشد",
            _ => "همگام‌سازی",
        };

        if (key != SyncDotBrushKey) SyncDotBrushKey = key;
        if (text != SyncDotText) SyncDotText = text;
        if (sync.Reason != SyncDotReason) SyncDotReason = sync.Reason;
    }

    // ══ ⏳ پردهٔ «آوردنِ اطلاعاتِ حساب» ═══════════════════════════════════
    //
    //  خواستهٔ صریحِ صاحب ریپو (۱۴۰۵/۰۷/۱۱): «یارو اینترنت داره و می‌ره تو
    //  حساب است و لودینگ روی صفحه نمیاد تا اطلاعاتی که توی حساب و سرور
    //  است بیاد روی همون حساب.»
    //
    //  ⚠️ **هیچ تصمیمی این‌جا گرفته نمی‌شود** — همان قاعدهٔ چراغِ همگام‌سازی.
    //  «پرده باید باشد یا نه» فقط در خودِ موتورِ همگام‌سازی تصمیم
    //  گرفته می‌شود و این‌جا فقط **خوانده** می‌شود. دو جای تصمیم یعنی
    //  روزی پرده هست و همگام‌سازی نیست.

    /// <summary>پردهٔ «اطلاعاتِ حسابتان دارد می‌آید» روی صفحه است؟</summary>
    [ObservableProperty] private bool _isSyncPriming;

    /// <summary>همان لحظه چه می‌گذرد — جملهٔ آمادهٔ خودِ موتور.</summary>
    [ObservableProperty] private string _syncPrimeText = "";

    /// <summary>
    /// «ادامه در پس‌زمینه» — پرده می‌رود و همگام‌سازی سرِ جایش می‌ماند.
    ///
    /// ⛔ این دکمه <b>همیشه</b> روی پرده هست. صفحهٔ ورود نباید دیوار شود و
    /// این پرده هم نباید — کسی که اینترنتش کند است باید بتواند دفترِ خودش
    /// را ببیند (قاعدهٔ ۱۴۰۵/۰۶/۳۰).
    /// </summary>
    [RelayCommand]
    private void DismissSyncPrime()
    {
        AppHost.Current.SyncIfStarted?.DismissPrime();
        IsSyncPriming = false;
    }

    private void TickSyncPrime()
    {
        var sync = AppHost.Current.SyncIfStarted;
        var on = sync is { Priming: true };
        if (IsSyncPriming != on) IsSyncPriming = on;
        var text = on ? sync!.PrimeText : "";
        if (SyncPrimeText != text) SyncPrimeText = text;
    }

    /// <summary>
    /// دفترِ حساب رسید (یا نرسید) — پرده رفت.
    ///
    /// ⛔ <b>بخشِ جلوی چشم از نو خوانده می‌شود.</b> ردیف‌های رسیده با SQLِ
    /// خام می‌نشینند و <c>PumpDbContext.Bump()</c> می‌خورند، پس <b>هر بخشِ
    /// دیگری</b> سرِ نخستین دیدارش خودش تازه می‌شود (ترمزِ <c>Version</c>).
    /// ولی بخشی که همین حالا باز است تا کاربر جایی نرود دوباره خوانده
    /// نمی‌شود — یعنی صفحه‌ای که همین الان دیده می‌شود از دادهٔ تازه عقب
    /// می‌ماند.
    /// </summary>
    private async Task OnPrimeFinishedAsync(bool ok, string why, int got)
    {
        TickSyncPrime();
        TickSyncDot();

        if (ok && got > 0)
        {
            try { if (Current is { } cur) await cur.ReloadAsync(); }
            catch { /* تازه کردنِ صفحه رفاه است، خودِ داده روی دیسک نشسته */ }
            AppHost.Current.Toast(
                $"✅ اطلاعاتِ حسابتان آمد — {Shamsi.Money(got)} تغییر", ToastKind.Ok);
        }
        else if (!ok)
        {
            //  ⚠️ پرده رفت ولی همگام‌سازی نرفته: حلقه خودش دوباره می‌کوشد و
            //  چراغِ نوارِ پایین دلیلش را نگه می‌دارد.
            AppHost.Current.Toast(
                "⏳ اطلاعاتِ حساب هنوز نیامد — در پس‌زمینه دوباره تلاش می‌شود"
                + (why.Length > 0 ? " · " + why : ""), ToastKind.Warn);
        }
    }

    /// <summary>کلیکِ چراغِ همگام‌سازی — «الان همگام کن».</summary>
    [RelayCommand]
    private async Task SyncNowAsync()
    {
        AppHost.Current.Toast("در حالِ همگام‌سازی…", ToastKind.Info);
        var ok = await AppHost.Current.Sync.SyncNowAsync();
        TickSyncDot();
        AppHost.Current.Toast(
            ok ? "✅ همگام شد" : "⏳ " + AppHost.Current.Sync.Reason,
            ok ? ToastKind.Ok : ToastKind.Warn);
    }
}
