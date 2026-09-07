using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PumpYaqobi.App.Services;
using PumpYaqobi.App.Views;
using PumpYaqobi.Application.Localization;
using PumpYaqobi.Application.Services;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Services.Data;

namespace PumpYaqobi.App.ViewModels.Sections;

/// <summary>یک کارتِ قرض‌دار در فهرست — با نشانِ حالِ هر سه دفتر.</summary>
public sealed partial class DebtorCardViewModel : ObservableObject
{
    public DebtorCardViewModel(Debtor d, IReadOnlyList<DebtAccount> accounts, DebtCalculationService calc)
    {
        Entity = d;
        Name = d.Name ?? "";
        Phone = d.Phone ?? "";
        Accounts = accounts;
        AccountCount = accounts.Count;

        var b = calc.Balances(accounts);
        MoneyText = Shamsi.Money(b.Money);
        PetrolText = Shamsi.Money(b.Petrol);
        DieselText = Shamsi.Money(b.Diesel);

        var st = calc.Status(accounts);
        Status = st.Worst;
        // ⚠️ سه نشانِ جدا، دقیقاً مثلِ کارتِ نسخهٔ وب: «اتمام تیل» از حالِ پطرول
        // و دیزل می‌آید و «اتمام پول» از حالِ دفترِ پول. یکی‌شان می‌تواند قرمز
        // باشد و دیگری نه — با یک نشانِ واحد این تفکیک از دست می‌رفت.
        var fuelSt = st.Petrol >= st.Diesel ? st.Petrol : st.Diesel;
        FuelBadge = BadgeOf(fuelSt, "تیل");
        MoneyBadge = BadgeOf(st.Money, "پول");
        HasFuelBadge = fuelSt is DebtStatus.Out or DebtStatus.Low;
        HasMoneyBadge = st.Money is DebtStatus.Out or DebtStatus.Low;
        FuelBadgeIsOut = fuelSt == DebtStatus.Out;
        MoneyBadgeIsOut = st.Money == DebtStatus.Out;
    }

    private static string BadgeOf(DebtStatus s, string what) => s switch
    {
        DebtStatus.Out => "⛔ اتمام " + what,
        DebtStatus.Low => "⚠️ کمِ " + what,
        _ => "",
    };

    public Debtor Entity { get; }
    public string Name { get; }
    public string Phone { get; }

    /// <summary>حسابِ اصلی و زیرحساب‌ها — کلیدِ صدای هر کدام از همین می‌آید.</summary>
    public IReadOnlyList<DebtAccount> Accounts { get; }

    public int AccountCount { get; }
    public string MoneyText { get; }
    public string PetrolText { get; }
    public string DieselText { get; }
    public DebtStatus Status { get; }
    public string FuelBadge { get; private set; } = "";
    public string MoneyBadge { get; private set; } = "";
    public bool HasFuelBadge { get; private set; }
    public bool HasMoneyBadge { get; private set; }
    public bool FuelBadgeIsOut { get; private set; }
    public bool MoneyBadgeIsOut { get; private set; }

    /// <summary>شمارهٔ کارت در فهرست — همان عددِ پایینِ کارتِ نسخهٔ وب.</summary>
    public int Index { get; set; }

    public bool IsOut => Status == DebtStatus.Out;
    public bool IsLow => Status == DebtStatus.Low;
    public bool IsOk => Status == DebtStatus.Ok;
    public bool HasStatus => Status != DebtStatus.None;

    /// <summary>کلیدِ رنگِ نشان — همان سه رنگی که کارت‌های نسخهٔ وب داشتند.</summary>
    public string StatusBrushKey => Status switch
    {
        DebtStatus.Out => "Pump.Danger",
        DebtStatus.Low => "Pump.Warn",
        DebtStatus.Ok => "Pump.Ok",
        _ => "Pump.Muted",
    };
}

/// <summary>
/// ══ بخشِ قرض‌داران ══════════════════════════════════════════════════════════
/// فهرستِ کارت‌ها، و با کلیک روی هر کارت، صفحهٔ حسابِ همان شخص.
///
/// همهٔ عددها از <see cref="DebtCalculationService"/> می‌آیند — همان سرویسی که
/// با ۲۰۰ حالتِ گرفته‌شده از خودِ نسخهٔ وب آزموده شده. هیچ جمعی این‌جا دستی
/// زده نمی‌شود.
/// </summary>
public sealed partial class DebtSectionViewModel : SectionViewModel, ICardGridHost
{
    private readonly AppHost _host;
    private readonly bool _noInvoice;
    private List<DebtorCardViewModel> _all = new();

    public DebtSectionViewModel(AppHost host, bool noInvoice = false)
        : base(noInvoice ? "noinv" : "debt",
               noInvoice ? "noinv" : "debt",
               noInvoice ? "شرکت‌ها تیل" : "قرض‌داران")
    { _host = host; _noInvoice = noInvoice; }

    public ObservableCollection<DebtorCardViewModel> Cards { get; } = new();

    [ObservableProperty] private string _search = "";

    /// <summary>در حالِ شنیدن — دکمهٔ 🎤 قرمز می‌شود.</summary>
    [ObservableProperty] private bool _listening;
    [ObservableProperty] private string _voiceStatus = "";

    /// <summary>
    /// صدایی که تازه شنیده شده ولی هنوز به حسابی نچسبیده.
    ///
    /// ⚠️ همان ‎_vxPendingTeach‎ی نسخهٔ وب و قلبِ «یادگیریِ بی‌زحمت»: اگر موتور
    /// نشناخت و خودِ کاربر روی نامی زد، همین صدا برای همان حساب ثبت می‌شود.
    /// پس برنامه بدونِ هیچ «حالتِ آموزش» یاد می‌گیرد.
    /// </summary>
    private VoiceFeatures? _pendingTeach;
    [ObservableProperty] private PersonViewModel? _person;
    [ObservableProperty] private string _newName = "";
    [ObservableProperty] private string _newPhone = "";

    public bool IsListVisible => Person is null;

    /// <summary>حسابِ شخص — تا باز است، میانبرهای ردیف به آن می‌روند نه به فهرست.</summary>
    public override object? ActivePage => Person;

    partial void OnPersonChanged(PersonViewModel? v)
    {
        OnPropertyChanged(nameof(IsListVisible));
        // صفحهٔ حساب تمام‌عرض است، مثلِ مودالِ تمام‌صفحهٔ نسخهٔ وب
        IsPageOpen = v is not null;
    }

    partial void OnSearchChanged(string v) => ApplyFilter();

    protected override async Task LoadAsync() => await RefreshAsync();

    public async Task RefreshAsync()
    {
        var people = await _host.Debtors.ListAsync(_noInvoice);
        var accounts = await _host.Debtors.AccountsByDebtorAsync(_noInvoice);
        // خوددرمانیِ ردیف‌ها پیش از حسابِ کارت — همان کاری که نسخهٔ وب هنگامِ
        // کشیدنِ جدول می‌کرد. بدونِ آن، «الباقی»ِ ردیف‌های کهنه صفر می‌ماند و
        // عددِ کارت با عددِ داخلِ حساب فرق می‌کند.
        foreach (var list in accounts.Values)
            foreach (var a in list) _host.Debt.NormalizeAccount(a);

        _all = people.Select(d => new DebtorCardViewModel(
            d, accounts.TryGetValue(d.Id, out var a) ? a : new List<DebtAccount>(), _host.Debt)).ToList();
        for (var i = 0; i < _all.Count; i++) _all[i].Index = i + 1;
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var s = Search.Trim();
        Cards.Clear();
        foreach (var c in _all)
            if (s.Length == 0 || c.Name.Contains(s, StringComparison.OrdinalIgnoreCase)
                              || c.Phone.Contains(s, StringComparison.OrdinalIgnoreCase))
                Cards.Add(c);
    }


    /// <summary>
    /// ‎Alt+عدد‎ — کارتِ شمارهٔ ‎n‎ همان عددی است که زیرِ کارت نوشته شده، و
    /// چون از روی فهرستِ <b>نمایش‌داده‌شده</b> شمرده می‌شود، با جست‌وجو هم
    /// خودکار جابه‌جا می‌گردد.
    /// </summary>
    public Task OpenByNumberAsync(int number)
    {
        if (number < 1 || number > Cards.Count) return Task.CompletedTask;
        return OpenAsync(Cards[number - 1]);
    }

    [RelayCommand]
    private async Task OpenAsync(DebtorCardViewModel? card)
    {
        if (card is null) return;
        var full = await _host.Debtors.LoadFullAsync(card.Entity.Id);
        if (full is null) return;
        Person = new PersonViewModel(_host, full, this);

        // ══ یادگیری، خاموش و بی‌سروصدا ══════════════════════════════════════
        // خواستهٔ صریحِ صاحب ریپو: «آموزش صدا را از همه جا حذف کن — آموزش چیه،
        // باید خودش بفهمه.» پس صفحهٔ آموزش و دکمه‌اش رفتند و هیچ توستی هم
        // نمی‌گوید «یادم دادی».
        //
        // ولی خودِ یادگیری ماند و عمداً: موتورِ صدا اثرِ صداست، نه تشخیصِ گفتار.
        // اگر هیچ صدایی ثبت نشود، دکمهٔ مایکروفون برای همیشه هیچ‌کس را
        // نمی‌شناسد. حالا همان کارِ همیشگیِ کاربر — گفتنِ نام و بعد باز کردنِ
        // حساب — خودش برنامه را یاد می‌دهد، بی آن‌که کاربر چیزی ببیند یا
        // کاری کند.
        var teach = _pendingTeach;
        _pendingTeach = null;
        if (teach is not null)
            await _host.Voice.EnrollAsync(AccountKey(card.Entity.Id), card.Name, teach);
    }

    /// <summary>کلیدِ صدا برای حسابِ اصلیِ یک شخص — همان ‎p&lt;id&gt;‎ی نسخهٔ وب.</summary>
    private static string AccountKey(long debtorId) => VoiceKeys.Of(debtorId);

    /// <summary>
    /// بازکردنِ حسابی که صدا آن را شناخت — ‎_vxOpenKey‎.
    ///
    /// ⚠️ اگر کلید به یک **زیرحساب** اشاره کند، همان زیرحساب انتخاب می‌شود نه
    /// حسابِ اصلی: صدای «موترِ دومِ کریم» باید دفترِ همان موتر را باز کند،
    /// وگرنه کاربر باز هم باید دستی بگردد.
    /// </summary>
    private async Task<bool> OpenByVoiceKeyAsync(string key)
    {
        var parsed = VoiceKeys.Parse(key);
        if (parsed is null) return false;

        var card = _all.FirstOrDefault(c => c.Entity.Id == parsed.Value.Debtor);
        if (card is null) return false;

        await OpenAsync(card);

        if (parsed.Value.Account is { } acctId && Person is not null)
        {
            var acct = Person.Accounts.FirstOrDefault(a => a.Entity.Id == acctId);
            if (acct is not null) Person.Current = acct;
        }
        return true;
    }

    /// <summary>
    /// ‎_vxOfflineSearch‎ — بگو، بشنو، حساب باز شود.
    ///
    /// هیچ سروری در کار نیست: صدا روی همین دستگاه به ویژگی تبدیل می‌شود و با
    /// صداهای ثبت‌شده مقایسه می‌گردد. پس بی‌اینترنت هم کار می‌کند.
    /// </summary>
    [RelayCommand]
    private async Task VoiceSearchAsync()
    {
        if (Listening) return;
        Listening = true;
        VoiceStatus = "🎤 نام را بگویید…";
        try
        {
            var pcm = await new MicRecorder().RecordAsync();
            if (pcm is null)
            {
                _host.Toast("🎤 مایکروفون باز نشد — دسترسیِ ویندوز را بررسی کنید", ToastKind.Error);
                return;
            }

            var feat = VoiceEngine.Features(pcm);
            if (feat is null)
            {
                _host.Toast("🎤 چیزی شنیده نشد — نزدیک‌تر و بلندتر بگویید", ToastKind.Error);
                return;
            }

            _pendingTeach = feat;

            // حساب‌های پاک‌شده کنار می‌روند (‎_vxLiveKey‎): کلیدِ صدا می‌ماند ولی
            // حسابش دیگر نیست، و بازکردنِ حسابِ نبوده هیچ معنایی ندارد.
            // کلیدهای زندهٔ هر شخص: حسابِ اصلی و همهٔ زیرحساب‌هایش. صدایی که
            // به حسابِ پاک‌شده اشاره کند کنار می‌رود (‎_vxLiveKey‎).
            var alive = new HashSet<string>();
            foreach (var c in _all)
            {
                alive.Add(VoiceKeys.Of(c.Entity.Id));
                foreach (var a in c.Accounts)
                    if (a.MainOfDebtorId is null) alive.Add(VoiceKeys.Of(c.Entity.Id, a.Id));
            }
            var matches = (await _host.Voice.MatchAsync(feat))
                          .Where(m => alive.Contains(m.Key)).ToList();

            if (matches.Count == 0)
            {
                _host.Toast("🎤 نشناختم — نام را بنویسید", ToastKind.Warn);
                return;
            }

            if (!VoiceDataService.IsSure(matches))
            {
                // مطمئن نیست: به‌جای حدس زدن، فهرست را روی نامزدها می‌بندد تا
                // کاربر با یک زدن هم کارش راه بیفتد هم به برنامه یاد بدهد.
                var names = matches.Take(3).Select(m => m.Name).Where(x => x.Length > 0).ToList();
                Search = names.Count == 1 ? names[0] : "";
                _host.Toast("🎤 مطمئن نیستم — " + (names.Count > 0
                    ? "کدام‌شان بود؟ " + string.Join(" · ", names)
                    : "نام را بنویسید"), ToastKind.Warn);
                return;
            }

            var best = matches[0];
            _pendingTeach = null;                    // شناخت؛ چیزی برای یاد دادن نمانده
            if (!await OpenByVoiceKeyAsync(best.Key)) return;
            _host.Toast("🎤 " + best.Name, ToastKind.Ok);
        }
        finally
        {
            Listening = false;
            VoiceStatus = "";
        }
    }

    [RelayCommand]
    private async Task BackAsync()
    {
        if (Person is not null) await Person.FlushAsync();
        Person = null;
        await RefreshAsync();
    }

    [RelayCommand]
    private async Task AddDebtorAsync()
    {
        var name = NewName.Trim();
        if (name.Length == 0) return;
        await _host.Debtors.AddDebtorAsync(name, NewPhone.Trim(), _noInvoice);
        NewName = ""; NewPhone = "";
        await RefreshAsync();
    }

    [RelayCommand]
    private async Task DeleteDebtorAsync(DebtorCardViewModel? card)
    {
        if (card is null) return;
        await _host.Debtors.DeleteDebtorAsync(card.Entity.Id);
        await RefreshAsync();
    }
}
