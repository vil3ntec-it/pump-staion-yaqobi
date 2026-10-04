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

//  ⛔ شورا ج۴: بخشی از ‎MainViewModel‎ — تنظیماتِ قابلِ بردن، دفترِ دیگر، نوارِ عددها و ساختنِ بخش‌ها. فقط جابه‌جاییِ همان عضوها از ‎MainViewModel.cs‎، بی تغییرِ یک رفتار.
public sealed partial class MainViewModel
{
    // ══ «فایلِ کاملِ برنامه» — تنظیماتِ قابلِ بردن (۱۴۰۵/۰۷/۱۵) ════════════
    //  شرح و فهرستِ سفید بالای ‎Services.PortableSettings‎.

    /// <summary>
    /// تنظیماتِ قابلِ بردنِ همین حالا — نوبتِ در صف (تمی که همین لحظه عوض شد،
    /// ستونی که همین حالا کشیده شد) اول روی دیسک می‌نشیند.
    /// </summary>
    public string CapturePortableSettings()
    {
        AppSettings.FlushNow();
        return PortableSettings.Capture(AppSettings.Load());
    }

    /// <summary>
    /// تنظیماتِ فایل روی دیسک می‌نشیند و <b>همان لحظه</b> روی برنامه: تم، ترتیبِ
    /// نوار، ظاهرِ جدول‌ها، اندازهٔ نوشته‌ها و ماشین‌حساب. پهنای ستون و تنظیمِ
    /// ورق با نخستین باز شدنِ همان جدول/پنجرهٔ چاپ خوانده می‌شوند.
    /// </summary>
    /// <remarks>
    /// ⚠️ نمونهٔ ماندگارِ همین پنجره (<c>_settings</c>) هم به‌روز می‌شود — وگرنه
    /// نخستین <c>SaveSoon</c>ِ بعدی تم و ترتیبِ نوارِ کهنه را برمی‌گرداند.
    /// </remarks>
    public IReadOnlyList<string> ApplyPortableSettings(string? json)
    {
        AppSettings.FlushNow();                 // نوبتِ کهنه بعداً روی تنظیماتِ آورده ننشیند
        var disk = AppSettings.Load();
        var done = PortableSettings.ApplyTo(json, disk);
        if (done.Count == 0) return done;
        disk.Save();
        PortableSettings.ApplyTo(json, _settings);

        var theme = PumpTheme.ById(_settings.ThemeId);
        if (theme.Id != SelectedTheme.Id) SelectedTheme = theme;
        OnPropertyChanged(nameof(NavSections));

        Calculator.Width = Math.Clamp(_settings.CalcWidth, CalculatorViewModel.MinW, CalculatorViewModel.MaxW);
        Calculator.Height = Math.Clamp(_settings.CalcHeight, CalculatorViewModel.MinH, CalculatorViewModel.MaxH);
        Calculator.IsLarge = _settings.CalcLarge;

        try { TableStyle.Apply(disk); } catch { }
        SectionViewModel.ForgetFontScales();
        foreach (var s in Sections.Concat(Sections.SelectMany(x => x.SubSections))) s.ReloadFontScale();
        NoteFontViewModel.Instance.Scale = disk.NoteFontScale;
        return done;
    }

    /// <summary>
    /// ══ خواندنِ دوبارهٔ همهٔ بخش‌ها ═══════════════════════════════════════════
    /// بعد از کاری که کلِ دیتابیس را عوض می‌کند — بازگردانیِ بکاپ، آوردنِ دادهٔ
    /// نسخهٔ وب، یا برگرداندنِ چیزی از سطلِ زباله.
    ///
    /// ⚠️ نمونهٔ بخش‌ها زنده می‌مانند (تا چیدمان و جای اسکرول از دست نرود)، پس
    /// بی این، صفحه‌ها عددِ دیتابیسِ **قبلی** را نشان می‌دهند و کاربر خیال
    /// می‌کند بازگردانی نگرفته است.
    ///
    /// بخشی که همین حالا باز است، آخر و همان‌جا تازه می‌شود.
    /// </summary>
    public async Task ReloadAllAsync()
    {
        //  ⛔ دفتر عوض شد (بازگردانی / فایلِ کامل) ⇒ تنظیمات و نامِ پمپ هم از دفترِ تازه
        try { AppHost.Current.RefreshBrand(); } catch { }
        // زیربخش‌ها هم بخش‌اند و دادهٔ خودشان را دارند — اگر این‌جا از قلم
        // بیفتند، «قرض‌های کهنه» بعد از بازگردانیِ بکاپ عددِ دیتابیسِ قبلی را
        // نشان می‌دهد.
        foreach (var s in Sections.Concat(Sections.SelectMany(x => x.SubSections)))
        {
            // بخشی که هرگز باز نشده، بارِ اولش را همان موقعِ ورودِ کاربر
            // می‌گیرد — این‌جا فقط باید «کهنه» علامت بخورد.
            if (!s.IsLoaded || ReferenceEquals(s, Content)) continue;
            s.IsLoaded = false;
        }

        if (Content is not null)
        {
            try { await Content.ReloadAsync(); } catch { }
            try { await Content.OnActivatedAsync(); } catch { }
        }

        await RefreshBannerAsync();
    }

    /// <summary>
    /// ══ حساب عوض شد ⇒ دفترِ همان حساب جلوی چشم بیاید ═══════════════════════
    ///
    /// خواستهٔ صریحِ صاحب ریپو (۱۴۰۵/۰۷/۱۱): «حسابِ اول با حسابِ دوم عوض
    /// بشه، اطلاعات دست نخوره، توی حساب‌ها بمونن، حساب‌ها عوض می‌شه و
    /// اطلاعاتِ همون حساب نشون داده بشه — مثلِ برنامه‌های حرفه‌ای.»
    ///
    /// تصمیمِ «کدام فایل» مالِ <see cref="AppHost.UseLedgerOf"/> است؛ این‌جا
    /// فقط پوسته است. سه کار، و ترتیبشان مهم است:
    ///
    /// ۱) <b>صفحه‌های باز بسته می‌شوند</b> — حسابِ قرض‌دار، صفحهٔ شرکت، ورق،
    ///    حسابِ امانت. آن‌ها شیءِ زنده‌اند و ردیف‌های دفترِ <b>قبلی</b> را در
    ///    خود دارند؛ تازه شدنِ فهرست نمی‌بنددشان.
    /// ۲) <b>همه‌چیز از نو خوانده می‌شود</b> — همان
    ///    <see cref="ReloadAllAsync"/>ی بازگردانیِ پشتیبان، که برای همین
    ///    ساخته شده بود.
    /// ۳) <b>قفلِ همان دفترِ تازه پرسیده می‌شود</b> — رمزِ برنامه در خودِ
    ///    دفتر می‌نشیند (<c>AppUser</c> هیچ‌وقت همگام نمی‌شود)، پس هر حساب
    ///    رمزِ خودش را دارد. دفترِ تازه‌ای که رمز ندارد بی رمز باز می‌شود
    ///    (قاعدهٔ ۱۴۰۵/۰۷/۰۷) و دفتری که رمز دارد رمزش را می‌پرسد.
    ///
    /// ⛔ <b>هیچ داده‌ای پاک نمی‌شود</b> — نه این‌جا و نه در
    /// <see cref="AppHost.UseLedgerOf"/>. دفترِ حسابِ قبلی سرِ جایش است و با
    /// برگشتنِ همان حساب، دست‌نخورده برمی‌گردد.
    /// </summary>
    public async Task OnLedgerSwitchedAsync()
    {
        //  دفترِ دیگر ⇒ هشدارهای دیگر؛ فهرستِ دفترِ قبلی نباید یک لحظه هم بماند
        AppHost.Current.LiveAlerts.Reset();
        foreach (var s in AllPages)
        {
            try { s.CloseOpenPage(); } catch { /* بستنِ صفحه رفاه است */ }
            //  ⚠️ **همه** کهنه می‌شوند، حتی بخشِ جلوی چشم: وگرنه بخشی که
            //  از قبل خوانده شده بود، ردیف‌های دفترِ **قبلی** را نگه
            //  می‌داشت و کاربر داده‌ای می‌دید که مالِ حسابِ دیگری است.
            s.IsLoaded = false;
        }

        //  قفلِ همان دفترِ تازه — و همین یک خط هر دو راه را می‌بندد:
        //    • رمز ندارد ⇒ همان لحظه باز می‌شود، `SignedIn` شلیک می‌کند و
        //      خودش بخشِ آغازین را از نو می‌خواند؛
        //    • رمز دارد  ⇒ صفحهٔ قفل، و هیچ ردیفی از دفترِ تازه خوانده
        //      نمی‌شود تا رمزش زده شود.
        //  ⛔ پس این‌جا عمداً هیچ `ReloadAsync`ی نیست: دو جای خواندن یعنی
        //  همان بخش دو بار خوانده می‌شود.
        await LockOrOpenAsync();

        if (Phase == AppPhase.Ready) await RefreshBannerAsync();
    }

    /// <summary>کارهای یک‌بارمصرفِ پس از نخستین ورود انجام شده‌اند؟</summary>
    private bool _afterSignIn;

    private string? _healthChecked;
    private async Task ReportLedgerHealthAsync()
    {
        try
        {
            var host = AppHost.Current;
            var path = host.DbPath;
            if (_healthChecked == path) return;
            _healthChecked = path;
            var bad = await Task.Run(() => host.Health.ScanAsync());
            if (bad.Count > 0)
                host.Toast($"🩺 {bad.Count} عددِ ناخوانا در دفتر هست — «تنظیمات ← بک‌اپ ← سلامتِ دفتر» جایشان را می‌گوید.",
                           ToastKind.Warn);
        }
        catch { }
    }

    private bool _bannerBusy, _bannerAgain;

    /// <summary>
    /// ══ نوارِ بالا جلوی باز شدنِ بخش را نگیرد ═══════════════════════════════
    ///
    /// چهار عددِ نوار به هیچ بخشی ربط ندارند؛ ولی چون جابه‌جایی منتظرشان
    /// می‌ماند، هزینه‌شان به **هر** باز کردنِ بخش اضافه می‌شد — در سنجش حدودِ
    /// ۱۶۰ ms روی هر جابه‌جایی، یعنی همان مکثی که خواستهٔ صاحب ریپو نبود.
    ///
    /// حالا بخش فوراً باز می‌شود و نوار یک لحظه بعد خودش تازه می‌شود.
    ///
    /// ⚠️ اگر کاربر تند تند بخش عوض کند، تازه‌سازی‌ها روی هم نمی‌ریزند: تا یکی
    /// در جریان است بقیه فقط «یک‌بارِ دیگر» را علامت می‌زنند و در پایان همان
    /// یک‌بار اجرا می‌شود — پس عددِ آخر همیشه عددِ درست است.
    /// </summary>
    public void QueueBannerRefresh()
    {
        if (_bannerBusy) { _bannerAgain = true; return; }
        _bannerBusy = true;
        _ = RunAsync();

        async Task RunAsync()
        {
            try
            {
                do
                {
                    _bannerAgain = false;
                    // ⚠️ بی این ‎try‎، یک خطای گذرا در خواندن، استثنای
                    // «مشاهده‌نشده» می‌شد و برنامه را می‌بست.
                    try { await RefreshBannerAsync(); } catch { }
                } while (_bannerAgain);
            }
            finally { _bannerBusy = false; }
        }
    }

    /// <summary>
    /// چهار عددِ نوارِ بالا — همان ‎updateBanner‎ِ نسخهٔ وب. فقط خواندنی است و
    /// هر بار که کاربر بخشی را باز می‌کند تازه می‌شود.
    /// </summary>
    /// <summary>نسخهٔ داده‌ای که نوار با آن حساب شده — تا با هر جابه‌جایی از نو نخواند.</summary>
    private long _bannerVersion = -1;

    public async Task RefreshBannerAsync()
    {
        //  خاموش ⇒ خوانده نمی‌شود؛ ‎_bannerVersion‎ دست نمی‌خورد تا روشن شدن
        //  همان لحظه از نو بخواند (‎BannerPref‎)
        if (!Services.BannerPref.Show) return;
        var host = AppHost.Current;
        var calc = new DashboardService();

        // ⚠️ سنجشِ «پنج سال داده»: این تابع با هر بار عوض کردنِ بخش همهٔ
        // ردیف‌های شرکت‌ها و همهٔ گزارش‌های پارچه را می‌خواند — یک ثانیه روی
        // **هر** جابه‌جایی، حتی به بخشی خالی مثلِ دوربین‌ها. تا چیزی ذخیره
        // نشده، عددها همان‌اند.
        // ⚠️ روز هم بخشی از کلید است: «مفادِ امروز» و «مصارفِ امروز» با نیمه‌شب
        // عوض می‌شوند بی آن‌که چیزی ذخیره شده باشد (چک‌لیستِ تحویل، بندِ ۶۲).
        var version = PumpYaqobi.Services.Data.DataVersion.Current * 100000L + AppClock.Now.DayOfYear;
        if (version == _bannerVersion) return;
        _bannerVersion = version;

        // ۱) الباقیِ شرکت‌های تیل
        var companies = await host.Companies.ListAsync();
        var compAlbaqi = companies.Sum(c => host.Company.Summarize(c, c.Rows).AlbaqiAfn);

        // ۲) قرضِ کلِ قرض‌داران
        //
        // ⚠️ این‌جا پیش از این **همهٔ ردیف‌های همهٔ حساب‌ها** خوانده می‌شد — و
        // چون نوارِ بالا با هر بار عوض کردنِ بخش تازه می‌شود، همان هزینه به
        // ازای هر جابه‌جایی تکرار می‌گشت. سنجشِ کارایی همین را نشان داد:
        // باز کردنِ هر بخش، حتی سبک‌ترینشان، شش ثانیهٔ ثابت.
        //
        // ‎CardAccountsAsync‎ همان جمع‌ها را از خودِ دیتابیس می‌گیرد (با همان
        // خوددرمانیِ ردیف، داخلِ کوئری) و هیچ ردیفی نمی‌خواند.
        var accounts = await host.Debtors.CardAccountsAsync();
        decimal debt = 0;
        foreach (var list in accounts.Values) debt += host.Debt.SumTotals(list).All.Albaqi;

        // ۳) مفادِ امروز — جمعِ فایدهٔ هر دو شیفتِ پارچه‌های همین تاریخ
        var today = Shamsi.Today();
        var reports = (await host.StorageData.ReportsAsync(FuelType.Petrol))
            .Concat(await host.StorageData.ReportsAsync(FuelType.Diesel))
            .Where(r => r.DateShamsi == today);
        var profit = reports.Sum(r => (r.DayShift?.Profit ?? 0) + (r.NightShift?.Profit ?? 0));

        // ۴) مصارفِ امروز — فقط ماهِ جاری، نه همهٔ مصارفِ تاریخ. «امروز» همیشه
        //    داخلِ همین ماه است، پس عدد همان است و خواندن هزار برابر کمتر.
        var expToday = calc.ExpQuick(await host.ExpenseLedger.ListAsync(Shamsi.ThisMonth())).Day;

        string M(decimal v) => Shamsi.Money(Math.Round(v, 0, MidpointRounding.AwayFromZero)) + " افغانی";
        Banner[0].Value = M(compAlbaqi);
        Banner[1].Value = M(debt);
        //  ⛔ «مفاد و ضرر هم یک نوع اس تو صفحهٔ اصلی است و دیده نمیشه» —
        //  جملهٔ خودِ صاحب ریپو دربارهٔ پلنِ استاندارد. عدد **حساب می‌شود**
        //  (چون بقیهٔ برنامه به آن نیاز دارد) ولی روی نوار «•••» می‌نشیند.
        _bannerProfit = Entitlements.Allows(Entitlements.Profit) ? M(profit) : "•••";
        //  ⛔ رمزِ «مفاد/ضرر» این‌جا هم (۱۴۰۵/۰۷/۱۶) — ‎ProfitVeil‎ تنها جای تصمیم
        Banner[2].Value = ProfitVeil.Show(_bannerProfit);
        Banner[3].Value = M(expToday);
    }

    /// <summary>
    /// ══ ترتیبِ نوار — مو‌به‌مو همان هجده دکمهٔ ‎&lt;div class="nav"&gt;‎ ═══════════
    ///
    /// ⚠️ این ترتیب فقط ظاهری نیست: میانبرِ ‎Ctrl+Shift+عدد‎ بخش را **با شمارهٔ
    /// جایش در همین فهرست** باز می‌کند. پیش از این ترتیب از ردیفِ دهم به بعد
    /// با سایت فرق داشت، پس ‎Ctrl+Shift+۱۰‎ که در سایت «گاوصندوق» بود این‌جا
    /// «رسید قرض‌داران» را باز می‌کرد — و همین‌طور تا هجده. کاربری که سال‌ها با
    /// این میانبرها کار کرده، هر بار بخشِ اشتباه را می‌گرفت.
    ///
    ///   ۱ داشبورد · ۲ پارچه‌ها · ۳ ورق‌های روزانه · ۴ قرض‌داران ·
    ///   ۵ ثبت فاکتورها · ۶ رسید قرض‌داران / چکنه · ۷ صرافی · ۸ مصارف ·
    ///   ۹ رسید پارچه · ۱۰ گاوصندوق · ۱۱ تیل امانت · ۱۲ شرکت‌ها تیل ·
    ///   ۱۳ مخزن · ۱۴ دوربین‌ها · ۱۵ حاضری و معاش · ۱۶ مفاد/ضرر ·
    ///   ۱۷ تنظیمات · ۱۸ تاریخچه‌ها
    ///
    /// ⚠️ <b>هجده‌تا و بس.</b> تا دیروز هفت بخشِ دیگر هم ته این فهرست بودند و
    /// نوار بیست‌وپنج دکمه‌ای شده بود. آن هفت‌تا در سایت هم دکمهٔ نوار ندارند:
    /// هر کدام کارتی داخلِ بخشِ دیگری‌اند. حالا این‌جا هم همان‌اند و در
    /// <see cref="AttachSubSections"/> به بخشِ خودشان بسته می‌شوند. اگر
    /// دوباره یکی‌شان را به این فهرست اضافه کنید، هم نوار شلوغ می‌شود هم
    /// آزمونِ ترتیبِ نوار قرمز.
    /// </summary>
    private IEnumerable<SectionViewModel> BuildSections(AppHost host) => new SectionViewModel[]
    {
        new DashboardSectionViewModel(host, this),   //  ۱
        new ParchaSectionViewModel(host),            //  ۲
        new WaraqSectionViewModel(host),             //  ۳
        new DebtSectionViewModel(host),              //  ۴
        new InvoiceSectionViewModel(host),           //  ۵
        new DebtReceiptSectionViewModel(host),       //  ۶
        new ExchangeSectionViewModel(host),          //  ۷
        new ExpenseSectionViewModel(host),           //  ۸
        new ParchaReceiptSectionViewModel(host),     //  ۹
        new SafeSectionViewModel(host),              // ۱۰
        new AmanatSectionViewModel(host),            // ۱۱
        new CompanySectionViewModel(host),           // ۱۲
        new StorageSectionViewModel(host),           // ۱۳
        new CameraSectionViewModel(host),            // ۱۴
        new AttendanceSectionViewModel(host),        // ۱۵
        new ProfitSectionViewModel(host),            // ۱۶
        new SettingsSectionViewModel(host),          // ۱۷
        new HistorySectionViewModel(host),           // ۱۸
        // ⚠️ بعد از هجدهمی، نه وسط: هجده بخشِ اولِ نوار باید به همان ترتیبِ
        // سایت بمانند، وگرنه ‎Ctrl+Shift+عدد‎ بخشِ دیگری را باز می‌کند.
        // (‎NavOrderTests‎ همین را گرفت، و درست هم گرفت.)
        new ChatSectionViewModel(host),              // ۱۹ — پیام‌رسان، مالِ خودِ نیتیو
        //  ۲۰ — حساب و اشتراک. باید بعد از هجدهمی بماند، وگرنه
        //  ‎Ctrl+Shift+عدد‎ جابه‌جا می‌شود (‎NavOrderTests‎).
        new AccountSectionViewModel(host),           // ۲۰
    };

    /// <summary>
    /// ══ هفت زیربخش، هر کدام زیرِ بخشِ خودش ══════════════════════════════════
    ///
    /// جای هر کدام از خودِ سایت آمده — همان‌جایی که ‎.tool-link-card‎ش نشسته:
    ///
    ///   مقایسهٔ نرخ      → ثبت فاکتورها            (‎sec-invoices‎)
    ///   چکنه            → رسید قرض‌داران / چکنه   (‎sec-debtrasid‎)
    ///   تخلیهٔ تانکر     → مخزن                    (‎sec-storage‎)
    ///   کمبودی کارمندان → حاضری و معاش            (‎sec-attendance‎)
    ///   قرض‌های کهنه     → قرض‌داران                (‎sec-debt‎)
    ///   گزارش ماهانه    → مفاد / ضرر              (‎sec-profit‎)
    ///   تاریخچهٔ نرخ     → مفاد / ضرر              (‎sec-profit‎)
    ///   مدیریت داده‌ها   → تنظیمات                 (‎sec-settings‎)
    ///
    /// ⚠️ نمونه‌ها این‌جا ساخته می‌شوند، نه در ‎BuildSections‎ — وگرنه آزمونِ
    /// ترتیبِ نوار (که تنِ ‎BuildSections‎ را می‌خواند) آن‌ها را هم دکمهٔ نوار
    /// می‌شمارد.
    /// </summary>
    private void AttachSubSections(AppHost host)
    {
        SectionViewModel? By(string id) => Sections.FirstOrDefault(s => s.Id == id);

        By("invoices")?.AddSub(new InvRateSectionViewModel(host),      "📉 مقایسهٔ نرخ فاکتورها");
        By("debtrasid")?.AddSub(new RetailSectionViewModel(host),      "حساب‌های چکنه");
        By("storage")?.AddSub(new TankerSectionViewModel(host),        "🚚 تخلیهٔ تانکر");
        By("attendance")?.AddSub(new StaffShortSectionViewModel(host), "👷 کمبودی کارمندان");
        By("debt")?.AddSub(new OldLoansSectionViewModel(host),         "⏰ قرض‌های کهنه");
        // «⏳ مدت عضویت همه»ی سایت — تا امروز دکمه‌اش هیچ کاری نمی‌کرد
        By("debt")?.AddSub(new MembershipSectionViewModel(host),       "⏳ مدت عضویت قرض‌داران");
        // «قرض‌های دسته‌جمعی» — دو صفحهٔ کاملاً جدا، مثلِ سایت. کلیک روی هر خط
        // حسابِ همان شخص را در بخشِ قرض‌داران باز می‌کند.
        if (By("debt") is DebtSectionViewModel debt)
        {
            // ⛔ ‎ContinueWith‎ی بی زمان‌بند روی نخِ پس‌زمینه می‌دوید و ‎OpenPersonAsync‎
            // کالکشن‌های رابط را از همان نخ عوض می‌کرد ⇒ «Call from invalid thread».
            async Task Open(long id) { await GoAsync(debt); await debt.OpenPersonAsync(id); }
            By("debtrasid")?.AddSub(new DebtSummarySectionViewModel(host, false, Open),
                                    "دسته‌جمعی — تیل");
            By("debtrasid")?.AddSub(new DebtSummarySectionViewModel(host, true, Open),
                                    "دسته‌جمعی — پول");
            // «📉 زیان ناشی از افزایش قیمت» — کارتِ بخشِ قرض‌دارانِ سایت
            // (‎sec-priceloss‎ و ‎sec-plperson‎). تا امروز در برنامه نبود.
            debt.AddSub(new PriceLossSectionViewModel(host, Open), "📉 زیان ناشی از افزایش قیمت");
        }
        By("profit")?.AddSub(new MonthReportSectionViewModel(host),    "📅 گزارش پایان ماه");
        By("profit")?.AddSub(new RateHistorySectionViewModel(host),    "📈 تاریخچهٔ نرخ اتحادیه");
        // ══ تنظیمات: سه صفحه، و بس ═══════════════════════════════════════
        // خواستهٔ صریحِ صاحب ریپو (۱۴۰۵/۰۶/۲۸). ترتیبشان همان ترتیبی است که
        // گفت و کارت‌های صفحهٔ تنظیمات هم همین است.
        By("settings")?.AddSub(new KeysSectionViewModel(host),         "🔑 رمزها و کد");
        By("settings")?.AddSub(new BackupSectionViewModel(host, this), "💾 بک‌اپ و به‌روزرسانی‌ها");
        By("settings")?.AddSub(new TrashSectionViewModel(host, this),  "🗑️ سطل زباله");
        //  خواستهٔ صاحب ریپو (۱۴۰۵/۰۶/۲۸): «لینکِ دانلودِ اپِ اندروید و لینکِ
        //  برنامهٔ آیفون را توی یک بخشِ جدید توی تنظیمات بگذار… و کدِ پمپ هم
        //  همان‌جا دیده شود.»
        By("settings")?.AddSub(new AppsSectionViewModel(host),         "📲 اپِ گوشی — لینک و کد");
        //  بندِ ۱۱ی پرامپتِ ۲۲: «وضعیتِ Sync با جزئیات… و دکمهٔ الان همگام کن.»
        //  ⛔ کادرِ نشانیِ سرور آن‌جا **نیست** — نشانی قفل است.
        By("settings")?.AddSub(new SyncSectionViewModel(host),         "🔄 همگام‌سازی");
        //  «بخشِ وی‌آی‌پی را هم اعمال کن که من ببینم و تست کنم» — زیرِ خودِ
        //  پروفایل، چون همان‌جا حالِ اشتراک دیده می‌شود.
        By("account")?.AddSub(new VipSectionViewModel(host),           "💎 اشتراک و پلن‌ها");
    }
}
