using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PumpYaqobi.App.Services;
using PumpYaqobi.Application.Localization;

namespace PumpYaqobi.App.ViewModels.Sections;

/// <summary>
/// یک پلن در صفحهٔ «اشتراک و پلن‌ها» — همان جدولِ <c>native/docs/PLANS-fa.md</c>.
///
/// ⛔ <b>قیمت را از خودت پر نکن.</b> خواستهٔ صریحِ صاحب ریپو: «جای قیمت‌ها
/// خالی باشد و بعداً خودم می‌گویم چقدر باشد.» پس <see cref="Price"/> برای هر
/// پلنِ پولی «—» است و همان‌طور باید بماند تا خودش عددش را بدهد.
/// </summary>
public sealed record PlanCard(
    string Title,
    string Price,
    string Note,
    IReadOnlyList<string> Yes,
    IReadOnlyList<string> No,
    bool Current);

/// <summary>
/// ══ 💎 اشتراک و پلن‌ها ══════════════════════════════════════════════════════
///
/// خواستهٔ صریحِ صاحب ریپو (۱۴۰۵/۰۶/۲۸): «بخشِ وی‌آی‌پی را هم اعمال کن که من
/// ببینم و تست کنم.»
///
/// پس این صفحه سه کار می‌کند:
///   ۱) می‌گوید همین حالا چه حالی داریم (پلن، روزِ مانده، ارفاق)،
///   ۲) هر سه کارِ ابری را با ✅/🔒 نشان می‌دهد و پشتیبانی را «همیشه باز»،
///   ۳) و یک کلیدِ <b>آزمایش</b> دارد که حالتِ بی‌اشتراک را نشان می‌دهد
///      (<see cref="Entitlements.TestDeny"/>) — فقط می‌بندد، هیچ‌وقت باز
///      نمی‌کند، و فقط تا بسته شدنِ برنامه.
///
/// ⚠️ هیچ تصمیمی این‌جا گرفته نمی‌شود: تنها جای تصمیم
/// <see cref="Entitlements"/> است و این صفحه فقط همان را می‌خواند.
/// </summary>
public sealed partial class VipSectionViewModel : SectionViewModel
{
    private readonly AppHost _host;

    public VipSectionViewModel(AppHost host) : base("vip", "settings", "اشتراک و پلن‌ها")
    {
        _host = host;
        Show();
    }

    [ObservableProperty] private string _planText = "";
    [ObservableProperty] private string _daysText = "";
    [ObservableProperty] private string _stateText = "";
    [ObservableProperty] private string _graceText = "";

    [ObservableProperty] private string _karText = "";
    [ObservableProperty] private string _qrText = "";
    [ObservableProperty] private string _backupText = "";

    /// <summary>پشتیبانی هرگز قفل نمی‌شود — «یکی از واجبات است».</summary>
    public string SupportText => "✅ همیشه باز";

    /// <summary>حالتِ آزمایش روشن است؟ — به <see cref="Entitlements.TestDeny"/> بسته.</summary>
    public bool TestDeny => Entitlements.TestDeny;

    /// <summary>چهار پلن، با قیمتِ خالی.</summary>
    public ObservableCollection<PlanCard> Plans { get; } = new();

    [RelayCommand]
    private void ToggleTest()
    {
        Entitlements.TestDeny = !Entitlements.TestDeny;
        OnPropertyChanged(nameof(TestDeny));
        Show();
        _host.Toast(Entitlements.TestDeny
            ? "🔒 آزمایش: حالا برنامه مثلِ پمپِ بی‌اشتراک رفتار می‌کند"
            : "✅ آزمایش خاموش شد — همان حالِ واقعیِ اشتراک", ToastKind.Info);
    }

    private void Show()
    {
        var file = AppSettings.Load();
        var st = Entitlements.State(file);

        static string Mark(bool ok) => ok ? "✅ باز" : "🔒 با اشتراک";
        KarText = Mark(st.Allows(Entitlements.Kar));
        QrText = Mark(st.Allows(Entitlements.QrLive));
        BackupText = Mark(st.Allows(Entitlements.CloudBackup));

        var open = st.Allows(Entitlements.Kar) || st.Allows(Entitlements.QrLive)
                   || st.Allows(Entitlements.CloudBackup);

        PlanText = Entitlements.TestDeny
            ? "آزمایشِ حالتِ بی‌اشتراک"
            : st.NotActivated ? "فعال نشده"
            : string.IsNullOrWhiteSpace(st.PlanTitle) ? (st.Open ? "اشتراکِ فعال" : "بدونِ اشتراکِ فعال")
            : st.PlanTitle;

        var days = st.EntitledUntil > st.NowMs
            ? (int)Math.Ceiling((st.EntitledUntil - st.NowMs) / 86_400_000d)
            : 0;
        DaysText = st.Open && days > 0 ? Shamsi.Money(days) + " روز" : "—";

        StateText = Entitlements.TestDeny
            ? "⚠️ فقط آزمایش است — قفل‌ها را همان‌طور که مشتریِ بی‌اشتراک می‌بیند نشان می‌دهد."
            : st.NotActivated ? "کدِ شش‌رقمی را در «پروفایل» بزنید."
            : open ? "کارهای ابری باز است." : "کارهای ابری بسته است؛ دفتر و پشتیبانی باز.";

        GraceText = !Entitlements.TestDeny && st.InGrace
            ? "⏳ اشتراک تمام شده — " + Shamsi.Money(st.GraceDaysLeft)
              + " روز ارفاق. در این مدت همان پلنِ شما کار می‌کند."
            : "";

        BuildPlans(st, open);
    }

    private void BuildPlans(EntitlementState st, bool open)
    {
        //  «پلنِ فعلی» را از روی چیزی می‌گوییم که واقعاً باز است، نه از روی
        //  نامِ پلن: نامِ پلن را سرور می‌نویسد و می‌تواند هر چیزی باشد.
        var basic = !Entitlements.TestDeny && st.Open && !open;
        var vip = !Entitlements.TestDeny && open && !st.NotActivated;

        var ledger = "دفترِ کامل — قرض‌داران، ورق، پارچه، مخزن، گاوصندوق، صرافی، "
                     + "مصارف، فاکتور، مفاد، امانت، شرکت‌ها، حاضری و چاپ";
        var local = "سطلِ زباله و بک‌اپِ محلی";
        var support = "چتِ پشتیبانی";
        var kar = "اپِ کارمندان روی گوشی + رباتِ جست‌وجو";
        var qr = "کیو‌آرِ حسابِ مشتری با به‌روزرسانیِ زنده";
        var cloud = "بک‌اپِ خودکارِ هر شش ساعت روی سرور";

        Plans.Clear();
        Plans.Add(new PlanCard("آزمایشی", "رایگان · ۳۰ روز",
            "برای دیدنِ همه‌چیز، پیش از خرید.",
            new[] { ledger, local, support, kar, qr, cloud },
            Array.Empty<string>(), false));

        Plans.Add(new PlanCard("پایه", "—",
            "همهٔ دفتر، بی کارهای ابری.",
            new[] { ledger, local, support },
            new[] { kar, qr, cloud }, basic));

        Plans.Add(new PlanCard("VIP", "—",
            "دفتر به‌علاوهٔ هر سه کارِ ابری.",
            new[] { ledger, local, support, kar, qr, cloud, "چند پمپ در یک حساب" },
            Array.Empty<string>(), vip));

        Plans.Add(new PlanCard("مالکیت (یک‌بار)", "—",
            "یک‌بار پرداخت؛ برنامه مالِ خودِ خریدار می‌شود. آپدیت و بک‌اپِ ابری سالِ اول.",
            new[] { ledger, local, support, kar, qr, cloud, "چند پمپ در یک حساب" },
            Array.Empty<string>(), false));
    }

    public override Task OnActivatedAsync()
    {
        Show();
        return Task.CompletedTask;
    }
}
