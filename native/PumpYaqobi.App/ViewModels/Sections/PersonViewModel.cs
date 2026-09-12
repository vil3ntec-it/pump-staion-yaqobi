using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PumpYaqobi.App.Controls;
using PumpYaqobi.App.Printing;
using PumpYaqobi.App.Services;
using PumpYaqobi.Application.Localization;
using PumpYaqobi.Application.Services;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;
using PumpYaqobi.Reporting.Pdf;
using PumpYaqobi.Services.Data;
using PumpYaqobi.Services.Vision;

namespace PumpYaqobi.App.ViewModels.Sections;

/// <summary>یک ردیفِ دفترِ قرض‌دار.</summary>
public sealed partial class DebtRowViewModel : RowViewModel
{
    private readonly DebtRow _r;
    private readonly AccountViewModel _owner;

    public DebtRowViewModel(DebtRow r, AccountViewModel owner)
    {
        _r = r; _owner = owner;
        Loading = true;
        _dateShamsi = r.DateShamsi ?? "";
        _name = r.Name ?? "";
        _hawala = r.Hawala ?? "";
        _fuel = r.Fuel;
        _liters = r.Liters;
        _price = r.PricePerLiter ?? 0m;
        _manualBardagi = r.Bardagi;
        _rasid = r.Rasid;
        _rasidFuel = r.RasidFuel;
        Loading = false;
    }

    public DebtRow Entity => _r;

    [ObservableProperty] private string _dateShamsi = "";
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private string _hawala = "";
    [ObservableProperty] private FuelType _fuel;
    [ObservableProperty] private decimal _liters;
    [ObservableProperty] private decimal _price;
    [ObservableProperty] private decimal _manualBardagi;
    [ObservableProperty] private decimal _rasid;
    [ObservableProperty] private decimal _rasidFuel;

    partial void OnDateShamsiChanged(string v) => Touch();
    partial void OnNameChanged(string v) => Touch();
    partial void OnHawalaChanged(string v) => Touch();
    partial void OnFuelChanged(FuelType v)
    {
        Touch();
        // ‎Refresh‎ هم لازم است: «بردگی» به نوعِ تیل بند است.
        Refresh();
        OnPropertyChanged(nameof(FuelText));
        OnPropertyChanged(nameof(IsPetrol));
        OnPropertyChanged(nameof(IsDiesel));
    }
    partial void OnLitersChanged(decimal v) { Touch(); Refresh(); }
    partial void OnPriceChanged(decimal v) { Touch(); Refresh(); }
    partial void OnManualBardagiChanged(decimal v) { Touch(); Refresh(); }
    partial void OnRasidChanged(decimal v) { Touch(); Refresh(); }
    partial void OnRasidFuelChanged(decimal v) { Touch(); Refresh(); }

    private void Refresh()
    {
        OnPropertyChanged(nameof(LitersText)); OnPropertyChanged(nameof(PriceText));
        OnPropertyChanged(nameof(ManualBardagiText)); OnPropertyChanged(nameof(RasidText));
        OnPropertyChanged(nameof(RasidFuelText)); OnPropertyChanged(nameof(BardagiText));
        OnPropertyChanged(nameof(AlbaqiText));
        // سربرگِ دو تیل و ردیفِ «جمله» هم به همین ردیف بند‌اند
        _owner.RefreshTotals();
    }

    public string LitersText { get => Shamsi.MoneyOrBlank(Liters); set => Liters = Shamsi.Num(value); }
    public string PriceText { get => Shamsi.MoneyOrBlank(Price); set => Price = Shamsi.Num(value); }
    public string ManualBardagiText { get => Shamsi.MoneyOrBlank(ManualBardagi); set => ManualBardagi = Shamsi.Num(value); }
    public string RasidText { get => Shamsi.MoneyOrBlank(Rasid); set => Rasid = Shamsi.Num(value); }
    public string RasidFuelText { get => Shamsi.MoneyOrBlank(RasidFuel); set => RasidFuel = Shamsi.Num(value); }

    /// <summary>بردگیِ پولیِ همین ردیف — از همان سرویسِ آزموده، نه حسابِ دستی.</summary>
    /// <summary>
    /// «مقدار بردگی» — خواندنی **و** نوشتنی، مثلِ سایت.
    ///
    /// خوانده که می‌شود، عددِ محاسبه‌شده است (لیتر × فی). نوشته که می‌شود،
    /// ردیف «پولی» می‌شود و همان عددِ دستی حرفِ آخر را می‌زند — همان قاعده‌ای
    /// که در چکنه هم هست.
    ///
    /// پیش از این یک ستونِ جداگانهٔ «بردگیِ دستی» برای این کار ساخته شده بود
    /// که در سایت اصلاً وجود ندارد، و همان بود که صاحب ریپو گفت «این چیه؟».
    /// </summary>
    public string BardagiText
    {
        get => Shamsi.MoneyOrBlank(_owner.Calc.RowBardagi(_r));
        set { ManualBardagi = Shamsi.Num(value); _r.ByMoney = true; Touch(); Refresh(); }
    }

    /// <summary>
    /// «الباقی» — ‎بردگی − رسید‎، همان فرمولِ ‎NormalizeRow‎. فقط خواندنی.
    /// این ستون در نیتیو اصلاً نبود و صاحب ریپو گفت باید باشد؛ در سایت
    /// آخرین ستونِ عددیِ جدولِ شخص است.
    /// </summary>
    public string AlbaqiText =>
        Shamsi.Money(Math.Round(_owner.Calc.RowBardagi(_r) - Rasid, 0, MidpointRounding.AwayFromZero));

    /// <summary>
    /// ══ نوع تیل — دو کادرِ رادیویی، نه کشویی ═══════════════════════════════
    ///
    /// در سایت ستونِ «نوع تیل» دو کادرِ رادیویی است، پطرول و دیزل زیرِ هم، و
    /// انتخابِ یکی آن‌یکی را خاموش می‌کند. کشویی نه شبیهش بود و نه بی دو بار
    /// کلیک عوض می‌شد.
    ///
    /// هر دو روی همان یک ‎Fuel‎ می‌نشینند، پس ناسازگاری ممکن نیست: با روشن
    /// شدنِ یکی، آن‌یکی خودبه‌خود خاموش می‌شود.
    /// </summary>
    /// <summary>
    /// نامِ گروهِ دو دکمهٔ نوعِ تیلِ همین ردیف.
    ///
    /// ⚠️ باید برای هر ردیف **یکتا** باشد. تا امروز هر دو دکمه ‎GroupName‎ی
    /// ثابت («rowFuel») داشتند، یعنی آوالونیا همهٔ ردیف‌های جدول را یک گروه
    /// می‌دید و زدنِ «دیزل» در یک ردیف، انتخابِ همهٔ ردیف‌های دیگر را برمی‌داشت.
    /// </summary>
    public string FuelGroup => "rowFuel-" + (_r.Id != 0 ? _r.Id : GetHashCode());

    public bool IsPetrol
    {
        get => Fuel == FuelType.Petrol;
        set { if (value) Fuel = FuelType.Petrol; }
    }

    public bool IsDiesel
    {
        get => Fuel == FuelType.Diesel;
        set { if (value) Fuel = FuelType.Diesel; }
    }

    public string FuelText
    {
        get => Fuel.ToPersian();
        set => Fuel = value == "دیزل" ? FuelType.Diesel : FuelType.Petrol;
    }

    protected override void Apply()
    {
        _r.DateShamsi = DateShamsi;
        _r.DateKey = Shamsi.Key(DateShamsi);
        _r.Name = Name;
        _r.Hawala = Hawala;
        _r.Fuel = Fuel;
        _r.Liters = Liters;
        _r.PricePerLiter = Price == 0m ? null : Price;
        _r.Bardagi = ManualBardagi;
        _r.Rasid = Rasid;
        _r.RasidFuel = RasidFuel;
    }

    protected override Task SaveAsync() => _owner.SaveRowAsync(_r);
}

/// <summary>
/// یک ردیفِ **خوانده‌شدنیِ** یک جدولِ آرشیو. عمداً هیچ ‎setter‎ی ندارد: آرشیو
/// عکسِ گذشته است و در سایت هم فقط دیده می‌شود.
/// </summary>
public sealed class ArchiveRowViewModel
{
    public ArchiveRowViewModel(DebtRow r, DebtCalculationService calc)
    {
        DateShamsi = r.DateShamsi ?? "";
        Name = r.Name ?? "";
        Hawala = r.Hawala ?? "";
        FuelText = r.Fuel.ToPersian();
        LitersText = Shamsi.Money(r.Liters);
        PriceText = Shamsi.Money(r.PricePerLiter ?? 0m);
        BardagiText = Shamsi.Money(calc.RowBardagi(r));
        RasidText = Shamsi.Money(r.Rasid);
        RasidFuelText = Shamsi.Money(r.RasidFuel);
    }

    public string DateShamsi { get; }
    public string Name { get; }
    public string Hawala { get; }
    public string FuelText { get; }
    public string LitersText { get; }
    public string PriceText { get; }
    public string BardagiText { get; }
    public string RasidText { get; }
    public string RasidFuelText { get; }
}

/// <summary>
/// ══ یک جدولِ آرشیو در فهرست ═══════════════════════════════════════════════
///
/// خواستهٔ صاحب ریپو: «کادرِ جدول‌های آرشیو یک کادرِ کشویی است؛ می‌زنم یک کادرِ
/// دیگر باز می‌شود و با زدنِ روی هر کدام کشویی باز می‌شوند — با تاریخ و
/// مشخصات.» پس این‌جا هم دو پله است: فهرستِ کوچکِ عنوان‌ها، و با کلیک روی هر
/// عنوان همان جدول باز می‌شود (آکاردئون).
///
/// ⚠️ عددهای این‌جا در هیچ جمعِ زنده‌ای نمی‌آیند — نه در سربرگِ حساب، نه در
/// «جمله»ی جدولِ زنده. عکسِ گذشته است.
/// </summary>
public sealed partial class ArchiveViewModel : ObservableObject
{
    private readonly DebtTableArchive _h;

    public ArchiveViewModel(DebtTableArchive h, DebtCalculationService calc)
    {
        _h = h;
        var rows = DebtorService.ArchiveRows(h);
        foreach (var r in rows) Rows.Add(new ArchiveRowViewModel(r, calc));

        var t = calc.SplitTotals(rows);
        UnitText = h.IsMoney ? "افغانی" : "لیتر";
        Title = "🗂️ " + (h.CreatedShamsi ?? "—") + " — " + Shamsi.Money(h.RowCount) + " ردیف · واحدِ "
                + (h.IsMoney ? "پول" : "تیل");

        PetrolHeadText = "⛽ پطرول — فیصدی " + Shamsi.Money(h.PercentPetrol ?? 0m)
                       + "٪ · رسید " + Shamsi.Money(h.IsMoney ? h.RasidMoneyPetrol : h.RasidFuelPetrol)
                       + " · برد " + Shamsi.Money(h.IsMoney ? t.Petrol.Bardagi : t.Petrol.Liters)
                       + " · الباقی " + Shamsi.Money(t.Petrol.Albaqi);
        DieselHeadText = "🟤 دیزل — فیصدی " + Shamsi.Money(h.PercentDiesel ?? 0m)
                       + "٪ · رسید " + Shamsi.Money(h.IsMoney ? h.RasidMoneyDiesel : h.RasidFuelDiesel)
                       + " · برد " + Shamsi.Money(h.IsMoney ? t.Diesel.Bardagi : t.Diesel.Liters)
                       + " · الباقی " + Shamsi.Money(t.Diesel.Albaqi);
        NoteText = h.Note ?? "";
        HasNote = NoteText.Length > 0;

        // ⚠️ نامِ ستون‌ها این‌جا با سربرگ‌های جدولِ آرشیو یکی است، نه با
        // جدولِ شخص — سربرگ‌های آن جدول کوتاه‌ترند («مقدار» و «رسیدِ تیل»).
        // اگر نخوانَد، جمع زیرِ ستونِ خودش نمی‌نشیند و ته نوار می‌افتد.
        Totals = new[]
        {
            new TotalCell("مقدار تیل", Shamsi.Money(t.All.Liters), column: "مقدار"),
            new TotalCell("بردگی", Shamsi.Money(t.All.Bardagi)),
            new TotalCell("رسید", Shamsi.Money(t.All.Rasid), "Pump.Ok"),
            new TotalCell("رسید تیل", Shamsi.Money(t.All.RasidFuel), "Pump.Ok", "رسیدِ تیل"),
            new TotalCell("الباقی", Shamsi.Money(t.All.Albaqi),
                          t.All.Albaqi > 0m ? "Pump.Danger" : "Pump.Ok"),
        };
    }

    public DebtTableArchive Entity => _h;
    /// <summary>⚠️ ‎BulkRows‎: پر شدنِ جدول یک خبر می‌دهد نه ‎n‎ خبر
    /// — وگرنه جدول به ازای هر ردیف یک‌بار از نو چیده می‌شود و بخش می‌ایستد.</summary>
    public BulkRows<ArchiveRowViewModel> Rows { get; } = new();
    public IReadOnlyList<TotalCell> Totals { get; }

    public string Title { get; }
    public string UnitText { get; }
    public string PetrolHeadText { get; }
    public string DieselHeadText { get; }
    public string NoteText { get; }
    public bool HasNote { get; }

    /// <summary>کشویی — بسته می‌آید، با کلیک باز می‌شود.</summary>
    [ObservableProperty] private bool _isOpen;

    [RelayCommand]
    private void Toggle() => IsOpen = !IsOpen;

    public string CaretText => IsOpen ? "▴" : "▾";
    partial void OnIsOpenChanged(bool v) => OnPropertyChanged(nameof(CaretText));
}

/// <summary>
/// یک حسابِ قرض‌دار (اصلی یا فرعی).
/// ⚠️ «واحد پول» و «واحد تیل» دو دفترِ کاملاً جدا هستند. عوض کردنِ واحد،
/// دفترِ دیده‌شده را عوض می‌کند — نه اینکه ردیف‌ها را از یکی به دیگری ببرد.
/// </summary>
public sealed partial class AccountViewModel : ObservableObject, IRowBatchHost
{
    private readonly AppHost _host;
    private readonly PersonViewModel _person;

    public AccountViewModel(AppHost host, DebtAccount a, PersonViewModel person)
    {
        _host = host; _person = person; Entity = a;
        _isMoney = a.Mode.IsMoney();
        _percentPetrol = host.Debt.PercentOf(a, FuelType.Petrol);
        _percentDiesel = host.Debt.PercentOf(a, FuelType.Diesel);
        _rasidFuelPetrol = a.RasidFuelPetrol;
        _rasidFuelDiesel = a.RasidFuelDiesel;
        _rasidMoneyPetrol = a.RasidMoneyPetrol;
        _rasidMoneyDiesel = a.RasidMoneyDiesel;
        BuildRows();
    }

    public DebtAccount Entity { get; }
    public DebtCalculationService Calc => _host.Debt;
    public string Title => Entity.MainOfDebtorId != null ? "حسابِ اصلی" : (Entity.Name ?? "حسابِ فرعی");

    /// <summary>
    /// ⚠️ ‎BulkRows‎ است نه ‎ObservableCollection‎ی ساده: پر کردنِ یک حسابِ
    /// صدهزار ردیفی با ‎Add‎ی تک‌تک، صدهزار خبر به جدول می‌داد و برنامه
    /// دقیقه‌ها می‌ایستاد. ‎ResetTo‎ همان کار را با یک خبر می‌کند.
    /// </summary>
    public BulkRows<DebtRowViewModel> Rows { get; } = new();

    [ObservableProperty] private bool _isMoney;
    [ObservableProperty] private decimal _percentPetrol;
    [ObservableProperty] private decimal _percentDiesel;
    [ObservableProperty] private decimal _rasidFuelPetrol;
    [ObservableProperty] private decimal _rasidFuelDiesel;
    [ObservableProperty] private decimal _rasidMoneyPetrol;
    [ObservableProperty] private decimal _rasidMoneyDiesel;

    partial void OnIsMoneyChanged(bool v)
    {
        Entity.Mode = v ? LedgerMode.Money : LedgerMode.Fuel;
        BuildRows();
        _ = _host.Debtors.UpdateAccountAsync(Entity);
        _person.Recalc();
        RefreshTotals();
    }

    /// <summary>⚠️ فیصدیِ پطرول و دیزل دو چیزِ جدا هستند و هرگز یکی نمی‌شوند.</summary>
    /// <summary>وقتی کادرِ «هر دو» خودش این دو را می‌نویسد، دوباره ذخیره نشود.</summary>
    private bool _settingBoth;

    partial void OnPercentPetrolChanged(decimal v)
    {
        if (_settingBoth) return;
        _host.Debt.SetPercent(Entity, FuelType.Petrol, v == 0m ? null : v);
        SaveAccount();
        PercentChanged();
    }

    partial void OnPercentDieselChanged(decimal v)
    {
        if (_settingBoth) return;
        _host.Debt.SetPercent(Entity, FuelType.Diesel, v == 0m ? null : v);
        SaveAccount();
        PercentChanged();
    }

    /// <summary>
    /// کادرِ «هر دو» — میان‌بُرِ ‎_setAcctPct(a, 'both', val)‎.
    ///
    /// نوشتن در آن، همان لحظه هر دو فیصدی را می‌نویسد. و عمداً فقط وقتی عدد
    /// نشان می‌دهد که فیصدیِ هر دو تیل یکی باشد — وگرنه خالی می‌ماند، چون
    /// «هر دو» عددی ندارد که نشان بدهد.
    /// </summary>
    public string PercentBothText
    {
        get => PercentPetrolText == PercentDieselText ? PercentPetrolText : "";
        set
        {
            var v = Shamsi.Num(value);
            _host.Debt.SetPercent(Entity, null, v == 0m ? null : v);
            _settingBoth = true;
            PercentPetrol = v;
            PercentDiesel = v;
            _settingBoth = false;
            SaveAccount();
            PercentChanged();
        }
    }

    private void PercentChanged()
    {
        OnPropertyChanged(nameof(PercentPetrolText));
        OnPropertyChanged(nameof(PercentDieselText));
        OnPropertyChanged(nameof(PercentBothText));
        RefreshTotals();
    }

    // ⚠️ این چهار عدد دیگر «منبع» نیستند — جمعِ ستونِ رسیدِ ردیف‌هایند. وقتی
    // ‎SyncReceiptsAsync‎ آن‌ها را از حساب برمی‌دارد نباید دوباره روی حساب
    // بنشینند و ذخیره شوند.
    partial void OnRasidFuelPetrolChanged(decimal v) { if (_pullingSums) return; Entity.RasidFuelPetrol = v; SaveAccount(); OnPropertyChanged(nameof(RasidFuelPetrolText)); RefreshTotals(); }
    partial void OnRasidFuelDieselChanged(decimal v) { if (_pullingSums) return; Entity.RasidFuelDiesel = v; SaveAccount(); OnPropertyChanged(nameof(RasidFuelDieselText)); RefreshTotals(); }
    partial void OnRasidMoneyPetrolChanged(decimal v) { if (_pullingSums) return; Entity.RasidMoneyPetrol = v; SaveAccount(); OnPropertyChanged(nameof(RasidMoneyPetrolText)); RefreshTotals(); }
    partial void OnRasidMoneyDieselChanged(decimal v) { if (_pullingSums) return; Entity.RasidMoneyDiesel = v; SaveAccount(); OnPropertyChanged(nameof(RasidMoneyDieselText)); RefreshTotals(); }

    // نوشته‌های ورودی: عددِ خام بی «۰٫۰»، و پذیرشِ رقمِ فارسی و کاما

    /// <summary>
    /// ‎_acctPctRaw‎ — کادرِ فیصدی وقتی فیصدی‌ای نیست باید **خالی** باشد، نه
    /// «۰». صفر و خالی این‌جا یک معنا دارند («این تیل فیصدی ندارد») و نشان
    /// دادنِ «۰» کاربر را به این گمان می‌انداخت که عددی ثبت شده.
    /// </summary>
    public string PercentPetrolText
    {
        get => PercentPetrol == 0m ? "" : Shamsi.Money(PercentPetrol);
        set => PercentPetrol = Shamsi.Num(value);
    }

    public string PercentDieselText
    {
        get => PercentDiesel == 0m ? "" : Shamsi.Money(PercentDiesel);
        set => PercentDiesel = Shamsi.Num(value);
    }

    // ══ سربرگِ دو حساب + ردیفِ «جمله» ══════════════════════════════════════
    //
    // گزارشِ صاحب ریپو، دو تا:
    //   «آخر هر جدول جمله ندارد — در سایت بگرد و همان مدل این‌جا هم پیاده شود.»
    //   «سربرگ آن مدلی است، رنگ‌به‌رنگ، هم دیزل و هم پطرول را نشان می‌دهد و
    //    رسید هم در همان سربرگ‌ها نوشته می‌شود.»
    //
    // در سایت سربرگِ حسابِ شخص دو کادر است — «⛽ حساب پطرول» و «🟤 حساب دیزل» —
    // و هر کدام چهار عدد دارد: فیصدیِ ما، مقدار رسید، برد، و الباقی. ته جدول
    // هم ‎<tfoot class="xls-foot">‎ با یک ردیفِ «جمله» است.
    //
    // هر دو از همان ‎SplitTotals‎ی می‌آیند که ورقِ PDF هم از آن می‌خواند، پس
    // عددِ صفحه و عددِ ورق هرگز از هم جدا نمی‌شوند.

    /// <summary>
    /// ⚠️ تنها منبعِ همهٔ عددهای این صفحه — سربرگ، جمله و کارتِ حساب همه از
    /// همین یکی می‌خوانند. رسید هم دیگر جدا نیست: ستونِ رسیدِ همین ردیف‌هاست.
    /// </summary>
    private SplitTotals Totals =>
        Calc.SplitTotals(Rows.Select(r => r.Entity).ToList());

    /// <summary>واحدِ همین حساب — «لیتر» یا «افغانی».</summary>
    public string UnitText => IsMoney ? "افغانی" : "لیتر";

    // ── برچسب‌های سربرگ — مو‌به‌مو مثلِ سایت ────────────────────────────────
    //
    // در ‎index.html‎ این سه برچسب ثابت نیستند؛ ‎setL()‎ آن‌ها را با دفترِ باز
    // عوض می‌کند (خطِ ۳۶۲۳۵ به بعد):
    //     pm-p-rasid-l → «مقدار رسید پول» یا «مقدار رسید تیل»
    //     pm-p-rem-l   → «الباقی پول»    یا «الباقی تیل»
    //     pm-p-comm-l  → «فیصدی ما (X٪)» — خودِ فیصدی داخلِ برچسب می‌نشیند
    // پیش از این نیتیو هر سه را ثابت نوشته بود، پس در دفترِ پول هم «تیل»
    // می‌گفت و فیصدی داخلِ برچسب نبود.

    public string HeadRasidLabel => IsMoney ? "مقدار رسید پول" : "مقدار رسید تیل";
    public string HeadAlbaqiLabel => IsMoney ? "الباقی پول" : "الباقی تیل";
    public string HeadPetrolPercentLabel => "فیصدی ما (" + HeadPetrolPercentText + "٪)";
    public string HeadDieselPercentLabel => "فیصدی ما (" + HeadDieselPercentText + "٪)";

    /// <summary>نوشتهٔ دکمهٔ تعویضِ دفتر — همتای ‎#pm-mode-btn‎ی سایت.</summary>
    public string ModeToggleText => IsMoney ? "🔁 تیل" : "🔁 پول";

    /// <summary>
    /// کادرهای فیصدی باز است یا نه.
    ///
    /// خواستهٔ صاحب ریپو: «چرا فیصدی پطرول و دیزل و هر دو این سه کادر جدا
    /// باشن و دیده بشن … بغل اون حساب جدید باید کادر اش باشه و هر وقت زدم
    /// اینا بیان». پس همیشه پیدا نیستند؛ با دکمهٔ کنارِ «حساب جدید» باز و
    /// بسته می‌شوند.
    /// </summary>
    [ObservableProperty] private bool _isPercentOpen;

    public string PercentToggleText => IsPercentOpen ? "٪ فیصدی ▲" : "٪ فیصدی ▼";

    partial void OnIsPercentOpenChanged(bool v) => OnPropertyChanged(nameof(PercentToggleText));

    [RelayCommand]
    private void TogglePercent() => IsPercentOpen = !IsPercentOpen;

    /// <summary>دفترِ پول ⇄ دفترِ تیل — همتای ‎togglePersonMode()‎ی سایت.</summary>
    [RelayCommand]
    private void ToggleMode() => IsMoney = !IsMoney;

    // ══ «📋 همه / ⛽ حساب جداگانه پطرول / 🟤 حساب جداگانه دیزل» ═════════════
    //
    // همتای ‎setPersonFilter('all'|'petrol'|'diesel')‎ی سایت — سه دکمهٔ
    // ‎.pm-filter-btn‎ی وسطِ نوارِ حساب. در برنامهٔ نیتیو اصلاً نبودند، یعنی
    // راهی نبود که فقط ردیف‌های یک تیل را ببینی.
    //
    // ⚠️ فیلتر فقط چیزی است که **دیده** می‌شود؛ هیچ ردیفی پاک نمی‌شود و هیچ
    // عددی در دیتابیس عوض نمی‌شود. سربرگ هم مثلِ سایت فقط کارتِ همان تیل را
    // نشان می‌دهد.

    [ObservableProperty] private string _rowFilter = "all";

    partial void OnRowFilterChanged(string v)
    {
        BuildRows();
        foreach (var n in new[]
        {
            nameof(IsFilterAll), nameof(IsFilterPetrol), nameof(IsFilterDiesel),
            nameof(ShowPetrolCard), nameof(ShowDieselCard), nameof(ShowFuelTypeColumn),
        })
            OnPropertyChanged(n);
    }

    public bool IsFilterAll => RowFilter == "all";
    public bool IsFilterPetrol => RowFilter == "petrol";
    public bool IsFilterDiesel => RowFilter == "diesel";

    /// <summary>در فیلترِ دیزل، کارتِ پطرول دیده نمی‌شود — مثلِ سایت.</summary>
    public bool ShowPetrolCard => RowFilter != "diesel";
    public bool ShowDieselCard => RowFilter != "petrol";

    [RelayCommand]
    private void SetFilter(string? f) => RowFilter = f is "petrol" or "diesel" ? f : "all";

    // ── ⛽ حساب پطرول ──────────────────────────────────────────────────────
    public string HeadPetrolPercentText => PercentPetrol == 0m ? "0" : Shamsi.Money(PercentPetrol);
    /// <summary>
    /// ══ «مقدار رسید»ِ سربرگ — سربرگ + جدول ═════════════════════════════════
    /// گزارشِ صاحب ریپو: «تو سربرگ‌ها مقدار را رسید می‌زنم، اتومات نمی‌آید تو
    /// جدول و بالعکسش.»
    ///
    /// نیمهٔ دومش باگ بود: این‌جا فقط عددِ **دستیِ سربرگ** خوانده می‌شد، پس
    /// رسیدی که کاربر داخلِ ردیف‌های جدول می‌نوشت هیچ‌وقت در سربرگ دیده
    /// نمی‌شد. سایت جمعِ هر دو را نشان می‌دهد و خودش هم نوشته چرا:
    ///
    ///     «عددِ دیده‌شده جمعِ رسیدِ سربرگ + رسیدهای داخلِ جدول است… data-hf
    ///      برای این است که حینِ تایپ در خودِ جدول، این کادر هم در جا به‌روز
    ///      شود — پیش از این تا رندرِ بعدی عقب می‌ماند و کاربر می‌دید رسیدِ
    ///      جدول در سربرگ نمی‌آید.»
    ///
    /// ⚠️ نوشتن همچنان فقط سهمِ **سربرگ** را عوض می‌کند (‎HeadPetrolRasidEdit‎)،
    /// وگرنه رسیدهای جدول دوباره‌شماری می‌شدند.
    /// </summary>
    /// <summary>
    /// «مقدار رسید»ِ سربرگِ پطرول — **جمعِ همهٔ رسیدهای همان دفتر و همان تیل**،
    /// خوانده‌شده از خودِ ردیف‌های جدول. هیچ عددِ مستقلی این‌جا نگه داشته
    /// نمی‌شود.
    /// </summary>
    public string HeadPetrolRasidText => Shamsi.Money(HeadRasid(FuelType.Petrol));

    public string HeadPetrolBordText =>
        Shamsi.Money(IsMoney ? Totals.Petrol.Bardagi : Totals.Petrol.Liters);
    public string HeadPetrolAlbaqiText => Shamsi.Money(Remainder(FuelType.Petrol));
    public string HeadPetrolAlbaqiBrushKey =>
        Remainder(FuelType.Petrol) > 0m ? "Pump.Danger" : "Pump.Ok";

    /// <summary>
    /// همان عدد، ولی نوشتنی — چون در سایت رسید را همان‌جا در سربرگ می‌نویسند.
    /// نوشتن یعنی «یک رسیدِ تازه»، نه بازنویسیِ جمع.
    /// </summary>
    public string HeadPetrolRasidEdit
    {
        get => Shamsi.MoneyOrBlank(HeadRasid(FuelType.Petrol));
        set => AddHeadReceipt(FuelType.Petrol, value);
    }

    // ── 🟤 حساب دیزل ──────────────────────────────────────────────────────
    public string HeadDieselPercentText => PercentDiesel == 0m ? "0" : Shamsi.Money(PercentDiesel);
    public string HeadDieselRasidText => Shamsi.Money(HeadRasid(FuelType.Diesel));
    public string HeadDieselBordText =>
        Shamsi.Money(IsMoney ? Totals.Diesel.Bardagi : Totals.Diesel.Liters);
    public string HeadDieselRasidEdit
    {
        get => Shamsi.MoneyOrBlank(HeadRasid(FuelType.Diesel));
        set => AddHeadReceipt(FuelType.Diesel, value);
    }

    // ══════════════════════════════════════════════════════════════════════
    //  سربرگ ⇄ جدول ⇄ جمله — یک منبع، سه نما
    // ══════════════════════════════════════════════════════════════════════
    //
    //  خواستهٔ صریحِ صاحب ریپو: «سربرگ نباید مالکِ مستقلِ مقدارِ رسید باشد؛
    //  سربرگ باید نمایشگرِ مقدارِ واقعیِ موجود در دفتر باشد.»
    //
    //  پس هیچ‌کدام از این سه، عددِ خودش را نگه نمی‌دارد:
    //
    //      ردیف‌های جدول  ──▶  Totals  ──┬──▶  سربرگ
    //                                    ├──▶  جملهٔ ته جدول
    //                                    └──▶  کارتِ حساب
    //
    //  «مقدار رسید»ِ سربرگ = جمعِ ستونِ رسیدِ همان دفتر و همان تیل. پس:
    //    • رسیدی که در سربرگ نوشته شود، ردیفِ واقعیِ خودش را در جدول می‌سازد.
    //    • رسیدی که در جدول نوشته شود، همان لحظه در سربرگ دیده می‌شود.
    //    • ردیفی که حذف شود، از سربرگ و جمله هم کم می‌شود.
    //  هیچ‌کدام از این‌ها «هم‌گام‌سازی» لازم ندارد؛ یک عدد است که سه جا نشان
    //  داده می‌شود.

    // ══════════════════════════════════════════════════════════════════════
    //  ستون‌های زندهٔ جدول — هر بار فقط آن‌هایی که معنی دارند
    // ══════════════════════════════════════════════════════════════════════
    //
    //  گزارشِ صاحب ریپو: «دو تا رسید تو یکی» — جدول هم ستونِ «رسید» داشت و هم
    //  «رسید تیل»، و کاربر نمی‌دانست کدام مالِ کدام است.
    //
    //  سایت این را ندارد و صریح هم نوشته (خطِ ۳۴۳۲۹ی ‎index.html‎):
    //     «ستونِ واحدِ مقابل از جدول برداشته شود — واحد تیل ⇐ ستونِ «رسید»
    //      (پولی) حذف؛ واحد پول ⇐ ستونِ «رسید تیل» حذف.»
    //  و همان‌جا برای «نوع تیل» هم (خطِ ۳۴۲۹۸): در حسابِ جداگانهٔ پطرول یا
    //  دیزل، وقتی همهٔ ردیف‌ها یک تیل‌اند، آن ستون بی‌معناست و برداشته می‌شود.
    //
    //  ⚠️ فقط نمایش است — دادهٔ هیچ ردیفی دست نمی‌خورد. ردیفی که در دفترِ
    //  تیل رسیدِ پولی هم داشته باشد، عددش سرِ جایش می‌ماند و در جمع‌ها هم
    //  شمرده می‌شود؛ فقط آن ستون دیده نمی‌شود.

    /// <summary>ستونِ «رسید» (پولی) — فقط در دفترِ پول.</summary>
    public bool ShowRasidColumn => IsMoney;

    /// <summary>ستونِ «رسید تیل» — فقط در دفترِ تیل.</summary>
    public bool ShowRasidFuelColumn => !IsMoney;

    /// <summary>ستونِ «نوع تیل» — وقتی فیلترِ یک‌تیله روشن است، بی‌معناست.</summary>
    public bool ShowFuelTypeColumn => RowFilter == "all";

    /// <summary>جمعِ رسیدهای همین تیل، در دفترِ باز.</summary>
    private decimal HeadRasid(FuelType fuel)
    {
        var t = fuel == FuelType.Diesel ? Totals.Diesel : Totals.Petrol;
        return IsMoney ? t.Rasid : t.RasidFuel;
    }

    /// <summary>
    /// «الباقی»ِ سربرگ — فرمولِ خودِ سایت (‎_updatePersonTotals‎، خطِ ۳۶۲۲۰ی
    /// ‎index.html‎):
    ///
    ///     فیصدی  = رسید × ٪
    ///     الباقی = برد + فیصدی − رسید
    ///
    /// ⚠️ پیش از این نیتیو به‌جای این، جمعِ سادهٔ ستونِ «الباقی»ِ ردیف‌ها را
    /// نشان می‌داد — یعنی نه فیصدی در آن بود و نه رسیدِ سربرگ. عددِ سربرگ با
    /// عددِ سایت نمی‌خواند و همین یکی از گلایه‌ها بود.
    ///
    /// ⚠️ رسید فقط **یک‌بار** کم می‌شود. در سایت دو بار کم می‌شد چون دو انبارِ
    /// جدا بود (رسیدِ سربرگ و رسیدِ جدول)؛ حالا یک انبار است.
    /// </summary>
    private decimal Remainder(FuelType fuel)
    {
        var t = fuel == FuelType.Diesel ? Totals.Diesel : Totals.Petrol;
        var pct = fuel == FuelType.Diesel ? PercentDiesel : PercentPetrol;

        var bord = IsMoney ? t.Bardagi : t.Liters;
        var rasid = HeadRasid(fuel);
        var comm = rasid * pct / 100m;
        return DebtCalculationService.Round0(bord + comm - rasid);
    }

    /// <summary>فیصدیِ ما — همان ‎comm‎ی سایت.</summary>
    public string HeadPetrolCommText =>
        Shamsi.Money(DebtCalculationService.Round0(HeadRasid(FuelType.Petrol) * PercentPetrol / 100m));
    public string HeadDieselCommText =>
        Shamsi.Money(DebtCalculationService.Round0(HeadRasid(FuelType.Diesel) * PercentDiesel / 100m));

    /// <summary>
    /// رسیدِ تازه از سربرگ — یک **ردیفِ واقعی** در همین جدول می‌سازد.
    ///
    /// ⚠️ عمداً هیچ فرقی با ردیفی که کاربر خودش در جدول می‌سازد ندارد: خواستهٔ
    /// صریحِ صاحب ریپو بود که «رسیدی که از سربرگ ساخته می‌شود نباید ساختارِ
    /// متفاوتی از رسیدی که از جدول ساخته می‌شود داشته باشد».
    /// </summary>
    private void AddHeadReceipt(FuelType fuel, string text)
    {
        var v = Shamsi.Num(text);
        if (v == 0m) { RefreshTotals(); return; }
        _ = AddHeadReceiptAsync(fuel, v);
    }

    private async Task AddHeadReceiptAsync(FuelType fuel, decimal value)
    {
        var unit = IsMoney ? LedgerMode.Money : LedgerMode.Fuel;
        var row = _host.Debt.AddReceiptRow(Entity, unit, fuel, value, Shamsi.Today());

        await _host.Debtors.SaveRowAsync(row);
        await SyncReceiptsAsync();

        var vm = new DebtRowViewModel(row, this);
        vm.Recalculated += _person.Recalc;
        Rows.Add(vm);

        _person.Recalc();
        RefreshTotals();
    }

    /// <summary>
    /// چهار عددِ ‎Rasid…‎ی حساب را با ردیف‌ها یکی می‌کند و ذخیره‌شان می‌کند.
    ///
    /// آن‌ها دیگر منبع نیستند، «کش»اند: کارتِ حساب، PDF، آرشیو و هشدارها از
    /// همان‌ها می‌خوانند، پس بعد از هر افزودن/ویرایش/حذف باید تازه شوند.
    /// </summary>
    internal async Task SyncReceiptsAsync()
    {
        if (!_host.Debt.SyncReceiptTotals(Entity)) return;
        _pullingSums = true;
        RasidFuelPetrol = Entity.RasidFuelPetrol;
        RasidFuelDiesel = Entity.RasidFuelDiesel;
        RasidMoneyPetrol = Entity.RasidMoneyPetrol;
        RasidMoneyDiesel = Entity.RasidMoneyDiesel;
        _pullingSums = false;
        await _host.Debtors.UpdateAccountAsync(Entity);
    }

    /// <summary>وقتی خودِ جمع‌ها عددها را می‌نویسند، دوباره روی حساب ننشیند.</summary>
    private bool _pullingSums;

    public string HeadDieselAlbaqiText => Shamsi.Money(Remainder(FuelType.Diesel));
    public string HeadDieselAlbaqiBrushKey =>
        Remainder(FuelType.Diesel) > 0m ? "Pump.Danger" : "Pump.Ok";

    // ── ردیفِ «جمله»، ته جدول ─────────────────────────────────────────────
    //
    // خانه‌به‌خانهٔ ‎<tfoot class="xls-foot">‎ی سایت: «مقدار تیل، فی لیتر (—)،
    // مقدار بردگی، رسید، رسید تیل، الباقی». «فی لیتر» در سایت هم جمع ندارد
    // (میانگینِ فی معنایی نمی‌دهد) و این‌جا هم نیامده.
    public IReadOnlyList<TotalCell> TotalCells
    {
        get
        {
            var t = Totals;
            return new[]
            {
                new TotalCell(IsMoney ? "مقدار (افغانی)" : "مقدار تیل", Shamsi.Money(t.All.Liters),
                              column: "مقدار تیل"),
                new TotalCell("بردگی", Shamsi.Money(t.All.Bardagi), column: "مقدار بردگی"),
                // ⚠️ همان ستونی که در جدول دیده می‌شود، نه هر دو — وگرنه
                // «جمله» دوباره دو تا رسید نشان می‌داد.
                IsMoney
                    ? new TotalCell("رسید", Shamsi.Money(t.All.Rasid), "Pump.Ok")
                    : new TotalCell("رسید تیل", Shamsi.Money(t.All.RasidFuel), "Pump.Ok"),
                new TotalCell("الباقی", Shamsi.Money(t.All.Albaqi),
                              t.All.Albaqi > 0m ? "Pump.Danger" : "Pump.Ok"),
            };
        }
    }

    public string SumLitersText => Shamsi.Money(Totals.All.Liters);
    public string SumBardagiText => Shamsi.Money(Totals.All.Bardagi);
    public string SumRasidText => Shamsi.Money(Totals.All.Rasid);
    public string SumRasidFuelText => Shamsi.Money(Totals.All.RasidFuel);
    public string SumAlbaqiText => Shamsi.Money(Totals.All.Albaqi);

    // ══ جدول‌های آرشیو و «جدول جدید» ═══════════════════════════════════════
    //
    // دو خواستهٔ صاحب ریپو که در نیتیو اصلاً نبودند:
    //   «کادرِ جدول‌های آرشیو یک کادرِ کشویی است… با زدنِ روی هر کدام کشویی باز
    //    می‌شوند، با تاریخ و مشخصات.»
    //   «جدول جدید در هر حسابِ قرض‌دار وجود ندارد.»
    //
    // آرشیوها تنبل بار می‌شوند — تا کاربر کادر را باز نکرده، هیچ پرس‌وجویی
    // نمی‌رود. با ده‌ها حساب، این تفاوتِ باز شدنِ آنی و کند است.

    public ObservableCollection<ArchiveViewModel> Archives { get; } = new();

    [ObservableProperty] private bool _isArchiveOpen;
    [ObservableProperty] private int _archiveCount;
    private bool _archivesLoaded;

    public bool HasArchives => ArchiveCount > 0;
    public string ArchiveToggleText =>
        "🗂️ جدول‌های آرشیو این حساب — " + Shamsi.Money(ArchiveCount) + " جدول " + (IsArchiveOpen ? "▴" : "▾");

    partial void OnIsArchiveOpenChanged(bool v) => OnPropertyChanged(nameof(ArchiveToggleText));
    partial void OnArchiveCountChanged(int v)
    {
        OnPropertyChanged(nameof(HasArchives));
        OnPropertyChanged(nameof(ArchiveToggleText));
    }

    /// <summary>شمارِ آرشیوها را می‌خواند، بی ساختنِ خودِ جدول‌ها.</summary>
    public async Task LoadArchiveCountAsync()
    {
        try { ArchiveCount = (await _host.Debtors.ListArchivesAsync(Entity.Id)).Count; }
        catch { /* شمارنده نباید صفحه را بشکند */ }
    }

    [RelayCommand]
    private Task ToggleArchivesAsync() => CrashGuard.RunAsync("جدول‌های آرشیو", async () =>
    {
        if (!IsArchiveOpen && !_archivesLoaded) await ReloadArchivesAsync();
        IsArchiveOpen = !IsArchiveOpen;
    });

    private async Task ReloadArchivesAsync()
    {
        Archives.Clear();
        foreach (var h in await _host.Debtors.ListArchivesAsync(Entity.Id))
            Archives.Add(new ArchiveViewModel(h, Calc));
        ArchiveCount = Archives.Count;
        _archivesLoaded = true;
    }

    /// <summary>
    /// «🆕 جدول جدید» — ‎newPersonTable()‎.
    ///
    /// جدولِ زنده عکس می‌شود، به آرشیو می‌رود و خالی می‌ماند. فقط دفترِ واحدِ
    /// فعال؛ دفترِ آن‌یکی واحد دست‌نخورده می‌ماند.
    /// </summary>
    [RelayCommand]
    private Task NewTableAsync() => CrashGuard.RunAsync("جدول جدید", async () =>
    {
        if (RowCount == 0) { _host.Toast("جدول همین حالا خالی است", ToastKind.Warn); return; }
        if (!await Dialogs.ConfirmAsync("جدول جدید",
                "جدولِ فعلی آرشیو می‌شود و جدولِ خالیِ تازه‌ای باز می‌شود. ادامه؟")) return;

        await FlushAsync();                       // هرچه نیم‌تایپ مانده، اول ذخیره شود
        await _host.Debtors.ArchiveTableAsync(Entity.Id, Shamsi.Today());

        // عکسِ حافظه هم باید با پایگاه یکی شود، وگرنه جدولِ پاک‌شده روی صفحه می‌ماند
        Entity.ActiveRows().Clear();
        // رسید ستونِ خودِ همین ردیف‌هاست، پس با رفتنِ جدول خودش رفت. فقط کشِ
        // چهار عددِ حساب باید صفر شود (عکسشان در آرشیو ماند).
        if (IsMoney) { RasidMoneyPetrol = 0m; RasidMoneyDiesel = 0m; }
        else { RasidFuelPetrol = 0m; RasidFuelDiesel = 0m; }
        Entity.Note = null;
        BuildRows();

        await ReloadArchivesAsync();
        IsArchiveOpen = true;
        _person.Recalc();
        _host.Toast("✅ جدول جدید ساخته شد — جدولِ قبلی در آرشیو نشست", ToastKind.Ok);
    });

    [RelayCommand]
    private Task DeleteArchiveAsync(ArchiveViewModel? h) => CrashGuard.RunAsync("حذفِ آرشیو", async () =>
    {
        if (h is null) return;
        if (!await Dialogs.ConfirmAsync("حذفِ جدولِ آرشیو",
                "«" + h.Title + "» پاک شود؟ (به سطلِ زباله می‌رود)")) return;
        await _host.Debtors.DeleteArchiveAsync(h.Entity.Id);
        Archives.Remove(h);
        ArchiveCount = Archives.Count;
    });

    /// <summary>
    /// هر عددِ سربرگ و هر عددِ «جمله» را از نو می‌خواند. با هر تغییری که روی
    /// ردیف‌ها یا رسیدها اثر دارد صدا زده می‌شود — وگرنه جمع‌ها روی عکسِ
    /// لحظهٔ باز شدنِ حساب می‌مانند.
    /// </summary>
    public void RefreshTotals()
    {
        foreach (var n in new[]
        {
            nameof(UnitText),
            nameof(HeadPetrolPercentText), nameof(HeadPetrolRasidText), nameof(HeadPetrolRasidEdit),
            nameof(HeadPetrolBordText), nameof(HeadPetrolAlbaqiText), nameof(HeadPetrolAlbaqiBrushKey),
            nameof(HeadDieselPercentText), nameof(HeadDieselRasidText), nameof(HeadDieselRasidEdit),
            nameof(HeadDieselBordText), nameof(HeadDieselAlbaqiText), nameof(HeadDieselAlbaqiBrushKey),
            nameof(HeadPetrolCommText), nameof(HeadDieselCommText),
            nameof(ShowRasidColumn), nameof(ShowRasidFuelColumn), nameof(ShowFuelTypeColumn),
            nameof(SumLitersText), nameof(SumBardagiText), nameof(SumRasidText),
            nameof(SumRasidFuelText), nameof(SumAlbaqiText), nameof(TotalCells),
            nameof(HeadPetrolRasidEdit), nameof(HeadDieselRasidEdit),
            // برچسب‌های سربرگ هم با دفتر و با فیصدی عوض می‌شوند — اگر این‌جا
            // نباشند، در دفترِ پول همچنان «تیل» می‌نویسند.
            nameof(HeadRasidLabel), nameof(HeadAlbaqiLabel),
            nameof(HeadPetrolPercentLabel), nameof(HeadDieselPercentLabel),
            nameof(ModeToggleText),
        })
            OnPropertyChanged(n);
    }

    /// <summary>
    /// یادداشتِ همین حساب — همتای ‎#pm-note-field‎ی سایت.
    ///
    /// ‎DebtAccount.Note‎ از اول در دیتابیس بود و آرشیوها هم نشانش می‌دادند،
    /// ولی هیچ‌جای برنامهٔ نیتیو نمی‌شد نوشتش. گزارشِ صاحب ریپو: «تو سایت بخش
    /// نوت هم داشت، آن چه شد؟»
    /// </summary>
    public string AccountNote
    {
        get => Entity.Note ?? "";
        set
        {
            Entity.Note = string.IsNullOrWhiteSpace(value) ? null : value;
            SaveAccount();
            OnPropertyChanged(nameof(AccountNote));
        }
    }

    /// <summary>سپردهٔ پولِ همین حساب — خالی یعنی چیزی نوشته نشده.</summary>
    public string MoneyDepositText
    {
        get => Entity.MoneyDeposit is { } v && v != 0m ? Shamsi.Money(v) : "";
        set
        {
            var v = Shamsi.Num(value);
            Entity.MoneyDeposit = v == 0m ? null : v;
            SaveAccount();
            OnPropertyChanged(nameof(MoneyDepositText));
        }
    }
    public string RasidFuelPetrolText { get => Shamsi.MoneyOrBlank(RasidFuelPetrol); set => RasidFuelPetrol = Shamsi.Num(value); }
    public string RasidFuelDieselText { get => Shamsi.MoneyOrBlank(RasidFuelDiesel); set => RasidFuelDiesel = Shamsi.Num(value); }
    public string RasidMoneyPetrolText { get => Shamsi.MoneyOrBlank(RasidMoneyPetrol); set => RasidMoneyPetrol = Shamsi.Num(value); }
    public string RasidMoneyDieselText { get => Shamsi.MoneyOrBlank(RasidMoneyDiesel); set => RasidMoneyDiesel = Shamsi.Num(value); }

    private void SaveAccount()
    {
        _ = _host.Debtors.UpdateAccountAsync(Entity);
        _person.Recalc();
    }

    /// <summary>دفترِ دیده‌شده = دفترِ واحدِ همین حساب.</summary>
    private void BuildRows()
    {
        // ⚠️ اول مهاجرت، بعد هر چیزِ دیگر: حساب‌های قدیمی رسیدشان را در چهار
        // عددِ خودِ حساب (و نسخهٔ پیشین، در دفترِ جدا) دارند. یک‌بار به ردیفِ
        // واقعی تبدیل می‌شوند تا از این پس فقط یک انبار بماند و هیچ عددی گم
        // نشود. بارِ دوم چیزی نمی‌سازد.
        var moved = _host.Debt.MigrateReceiptsToRows(Entity, Shamsi.Today());
        if (moved.Count > 0) _ = PersistMigratedAsync(moved);

        // خوددرمانیِ دادهٔ کهنه پیش از کشیدنِ جدول — همان کاری که renderPersonRows
        // می‌کرد. اگر چیزی عوض شد، همان‌جا ذخیره می‌شود تا دوباره لازم نشود.
        // ⚠️ فقط ردیف‌هایی که واقعاً عوض شدند ذخیره می‌شوند. پیش از این اگر یک
        // ردیف درمان می‌شد، **همهٔ** ردیف‌های حساب یکی‌یکی ذخیره می‌شدند — در
        // حسابی با صدهزار ردیف یعنی صدهزار نوشتن در دیتابیس.
        var healed = Entity.FuelRows.Concat(Entity.MoneyRows)
                           .Where(r => _host.Debt.NormalizeRow(r)).ToList();
        if (healed.Count > 0) _ = PersistHealedAsync(healed);

        // ⚠️ فیلتر فقط روی «دیده شدن» است. ردیف‌های تیلِ دیگر سرِ جایشان‌اند و
        // در دیتابیس دست نمی‌خورند؛ فقط این‌بار ساخته نمی‌شوند.
        var want = RowFilter switch
        {
            "petrol" => (FuelType?)FuelType.Petrol,
            "diesel" => FuelType.Diesel,
            _ => null,
        };

        // ⚠️ اول ساخته می‌شوند، بعد **یک‌جا** جای‌گزین: با ‎Rows.Add‎ی تک‌تک،
        // حسابی با صدهزار ردیف صدهزار بار جدول را از نو می‌سنجید.
        var built = new List<DebtRowViewModel>();
        foreach (var r in Entity.ActiveRows()
                                .Where(r => want is null || r.Fuel == want)
                                .OrderBy(r => r.SortIndex).ThenBy(r => r.Id))
        {
            var vm = new DebtRowViewModel(r, this);
            vm.Recalculated += _person.Recalc;
            built.Add(vm);
        }
        Rows.ResetTo(built);

        // جدول عوض شد ⇒ سربرگ و ردیفِ «جمله» هم باید از نو خوانده شوند
        RefreshTotals();
    }


    /// <summary>ردیف‌هایی که از رسیدهای قدیمی ساخته شدند، ذخیره شوند.</summary>
    private async Task PersistMigratedAsync(List<DebtRow> rows)
    {
        foreach (var r in rows) await _host.Debtors.SaveRowAsync(r);
        await SyncReceiptsAsync();
    }

    private async Task PersistHealedAsync(List<DebtRow> healed)
    {
        foreach (var r in healed) await _host.Debtors.SaveRowAsync(r);
    }

    public async Task SaveRowAsync(DebtRow r)
    {
        _host.Debt.NormalizeRow(r);          // بردگی و الباقی، دقیقاً مثلِ نسخهٔ وب
        await _host.Debtors.SaveRowAsync(r);
        // ⚠️ ویرایشِ ستونِ رسیدِ یک ردیف، «مقدار رسید»ِ سربرگ و کارتِ حساب را
        // هم عوض می‌کند — چون همه یک عددند. پس کشِ حساب همین‌جا تازه می‌شود.
        await SyncReceiptsAsync();
        _person.Recalc();
        RefreshTotals();
    }

    [RelayCommand]
    private async Task AddRowAsync()
    {
        var r = new DebtRow
        {
            DateShamsi = Shamsi.Today(),
            DateKey = Shamsi.Key(Shamsi.Today()),
            SortIndex = Entity.ActiveRows().Count,
            ByMoney = IsMoney,
        };
        if (IsMoney) { r.MoneyAccountId = Entity.Id; Entity.MoneyRows.Add(r); }
        else { r.FuelAccountId = Entity.Id; Entity.FuelRows.Add(r); }
        await _host.Debtors.SaveRowAsync(r);
        var vm = new DebtRowViewModel(r, this);
        vm.Recalculated += _person.Recalc;
        Rows.Add(vm);
        // ردیفِ خالی هیچ رسیدی ندارد، پس جمع عوض نمی‌شود — ولی همان را هم
        // صریح حساب می‌کنیم تا هیچ‌وقت عددِ کهنه‌ای جا نماند.
        await SyncReceiptsAsync();
        _person.Recalc();
        RefreshTotals();
    }

    [RelayCommand]
    private async Task DeleteRowAsync(DebtRowViewModel? row)
    {
        if (row is null) return;

        // ⚠️ یک مسیر، برای هر ردیفی — چه رسید باشد چه نباشد. رسید رکوردِ
        // جداگانه‌ای ندارد که راهِ حذفِ جداگانه بخواهد؛ حذفِ ردیف خودش
        // رسیدش را هم می‌برد و جمع‌ها از نو حساب می‌شوند.
        await _host.Debtors.DeleteRowAsync(row.Entity.Id);
        Entity.FuelRows.Remove(row.Entity);
        Entity.MoneyRows.Remove(row.Entity);
        Rows.Remove(row);
        await SyncReceiptsAsync();
        _person.Recalc();
        RefreshTotals();
    }

    public int RowCount => Rows.Count;

    /// <summary>‎Ctrl+عدد‎ / ‎Shift+عدد‎ — افزودن و برداشتنِ گروهیِ ردیف.
    /// حذف فقط وقتی ردیفِ کافی باشد؛ وگرنه هیچ.</summary>
    public async Task AddRowsAsync(int count)
    {
        for (var i = 0; i < count; i++) await AddRowAsync();
    }

    public async Task DeleteRowsAsync(int count)
    {
        if (count < 1 || Rows.Count < count) return;
        for (var i = 0; i < count; i++) await DeleteRowAsync(Rows[^1]);
    }

    public async Task FlushAsync()
    {
        foreach (var r in Rows.ToList()) await r.FlushAsync();
    }
}

/// <summary>
/// ══ صفحهٔ حسابِ یک قرض‌دار ══════════════════════════════════════════════════
/// حسابِ اصلی و حساب‌های فرعی، هر کدام دفترِ خودش. عددهای بالای صفحه
/// جمعِ همهٔ حساب‌هاست و از همان سرویسِ آزموده می‌آید.
/// </summary>
public sealed partial class PersonViewModel : ObservableObject, IRowBatchHost
{
    private readonly AppHost _host;
    private readonly DebtSectionViewModel _section;

    public PersonViewModel(AppHost host, Debtor d, DebtSectionViewModel section)
    {
        _host = host; _section = section; Entity = d;
        foreach (var a in d.AllAccounts())
        {
            var vm = new AccountViewModel(host, a, this);
            Accounts.Add(vm);
            // فقط شمارنده — خودِ جدول‌های آرشیو تا باز نشوند ساخته نمی‌شوند
            _ = vm.LoadArchiveCountAsync();
        }
        _current = Accounts.FirstOrDefault();
        Recalc();
    }

    public Debtor Entity { get; }
    public string Name => Entity.Name ?? "";
    public string Phone => Entity.Phone ?? "";

    // ══ «📞 شماره تماس» و «📝 فیِ خرید» — دو کادرِ نوارِ حساب ═══════════════
    //
    // هر دو در سایت داخلِ ‎.pm-acct-bar‎اند (‎#pm-phone‎ و ‎#pm-buyfee‎) و هر دو
    // در برنامهٔ نیتیو فقط خوانده می‌شدند: شماره زیرِ نامِ شخص چاپ می‌شد و
    // «فیِ خرید» با آن‌که ‎Debtor.BuyFeeNote‎ از اول در دیتابیس بود، هیچ‌جا
    // دیده و نوشته نمی‌شد.
    //
    // ⚠️ «فیِ خرید» فقط یادداشت است — در هیچ محاسبه‌ای نیست. همان جمله‌ای که
    // خودِ سایت روی ‎title‎ی این کادر نوشته.

    public string PhoneText
    {
        get => Entity.Phone ?? "";
        set
        {
            var v = (value ?? "").Trim();
            Entity.Phone = v.Length == 0 ? null : v;
            _ = _host.Debtors.UpdateDebtorAsync(Entity);
            OnPropertyChanged(nameof(PhoneText));
            OnPropertyChanged(nameof(Phone));
        }
    }

    public string BuyFeeText
    {
        get => Entity.BuyFeeNote ?? "";
        set
        {
            var v = (value ?? "").Trim();
            Entity.BuyFeeNote = v.Length == 0 ? null : v;
            _ = _host.Debtors.UpdateDebtorAsync(Entity);
            OnPropertyChanged(nameof(BuyFeeText));
        }
    }

    public ObservableCollection<AccountViewModel> Accounts { get; } = new();

    [ObservableProperty] private AccountViewModel? _current;
    [ObservableProperty] private string _moneyText = "";
    [ObservableProperty] private string _petrolText = "";
    [ObservableProperty] private string _dieselText = "";
    [ObservableProperty] private string _statusText = "";

    /// <summary>جمع‌ها و حال — همیشه از روی همهٔ حساب‌ها، نه فقط حسابِ باز.</summary>
    public void Recalc()
    {
        var accounts = Accounts.Select(a => a.Entity).ToList();
        var b = _host.Debt.Balances(accounts);
        MoneyText = Shamsi.Money(b.Money);
        PetrolText = Shamsi.Money(b.Petrol);
        DieselText = Shamsi.Money(b.Diesel);
        StatusText = _host.Debt.Status(accounts).Worst switch
        {
            DebtStatus.Out => "تمام شده",
            DebtStatus.Low => "رو به تمام",
            DebtStatus.Ok => "روبه‌راه",
            _ => "—",
        };
    }

    /// <summary>
    /// میانبرهای ردیف به «حسابِ باز» می‌روند — حسابِ اصلی یا هر حسابِ فرعی که
    /// همین حالا جلوی چشمِ کاربر است. دو دفترِ «تیل» و «پول» جدا می‌مانند،
    /// چون هر حساب ردیف‌های خودش را دارد.
    /// </summary>
    public int RowCount => Current?.RowCount ?? 0;
    public Task AddRowsAsync(int count) => Current?.AddRowsAsync(count) ?? Task.CompletedTask;
    public Task DeleteRowsAsync(int count) => Current?.DeleteRowsAsync(count) ?? Task.CompletedTask;

    /// <summary>
    /// ‎pdfPerson()‎ — ورقِ **همان حسابی که باز است**، نه همهٔ حساب‌ها.
    ///
    /// ⚠️ نامِ حسابِ اصلی داخلِ ورقِ حسابِ فرعی نمی‌آید — خواستهٔ صریحِ صاحب
    /// ریپو: این‌ها حساب‌های جدا هستند و هر کدام سربرگِ نامِ خودش را دارد.
    ///
    /// ⚠️ «برد» در دفترِ تیل جمعِ **لیتر** است و در دفترِ پول جمعِ **بردگی** —
    /// دو دفترِ کاملاً جدا، همان‌طور که در نسخهٔ وب بود.
    /// </summary>
    [RelayCommand]
    private Task PdfAsync()
    {
        var acct = Current;
        if (acct is null) return Task.CompletedTask;

        var calc = _host.Debt;
        var rows = acct.Rows.Select(r => r.Entity).ToList();
        var t = calc.SplitTotals(rows);
        var money = acct.IsMoney;
        var isSub = acct.Entity.MainOfDebtorId is null;

        var input = new DebtorStatementInput(
            PersonName: Name,
            AccountTitle: isSub ? "📄 " + acct.Title : "حساب " + Name,
            IsMoneyLedger: money,
            Filter: null,
            Rows: rows,
            PercentPetrol: acct.PercentPetrol, PercentDiesel: acct.PercentDiesel,
            RasidPetrol: money ? acct.RasidMoneyPetrol : acct.RasidFuelPetrol,
            RasidDiesel: money ? acct.RasidMoneyDiesel : acct.RasidFuelDiesel,
            BordPetrol: money ? t.Petrol.Bardagi : t.Petrol.Liters,
            BordDiesel: money ? t.Diesel.Bardagi : t.Diesel.Liters,
            RasidRowsPetrol: t.Petrol.Rasid, RasidRowsDiesel: t.Diesel.Rasid,
            Dates: DocDates.Line());

        return Documents.ShowAsync(() => new DebtorStatementReport(input, calc),
                                   input.AccountTitle);
    }

    /// <summary>برگشت به فهرست — از راهِ خودِ بخش، تا ذخیرهٔ نیمه‌کاره جا نماند.</summary>
    [RelayCommand]
    private Task BackAsync() => _section.BackCommand.ExecuteAsync(null);

    // ── «→ قبلی» و «بعدی ←» — ‎navigatePerson(±1)‎ی سایت ────────────────────
    // در سایت این دو دکمه اولِ سربرگِ مودالِ شخص‌اند و در نیتیو اصلاً نبودند،
    // یعنی برای رفتن به قرض‌دارِ بعدی باید هر بار به فهرست برمی‌گشتی.

    [RelayCommand]
    private Task PrevPerson() => _section.NavigatePersonAsync(-1);

    [RelayCommand]
    private Task NextPerson() => _section.NavigatePersonAsync(+1);

    /// <summary>
    /// «📲 کیو‌آر» — ‎showPersonQRFromModal()‎ی سایت.
    ///
    /// کیو‌آرِ <b>همان حسابی که باز است</b>: حسابِ اصلی یا هر حسابِ فرعی. متنِ
    /// داخلش دقیقاً همان نشانی‌ای است که سایت می‌سازد (‎_acctHash‎)، پس کاغذهای
    /// چاپ‌شدهٔ قبلی هم با همین برنامه خوانده می‌شوند.
    /// </summary>
    [RelayCommand]
    private Task ShowQr() => CrashGuard.RunAsync("کیو‌آر", async () =>
    {
        var acct = Current;
        if (acct is null) return;

        // حسابِ فرعی همیشه ‎LegacySubId‎ دارد (هم آن‌هایی که از نسخهٔ وب آمده‌اند،
        // هم آن‌هایی که ‎AddSubAccountAsync‎ می‌سازد)، پس ‎IsMain‎ همان محکِ درست است.
        var isSub = !acct.Entity.IsMain;
        var sub = isSub ? acct.Entity.LegacySubId : null;

        // ⚠️ این کیو‌آر برای **خودِ قرض‌دار** است: می‌فرستیدش، مشتری با گوشیِ
        // خودش اسکن می‌کند و حسابش را زنده می‌بیند — دادهٔ آن صفحه از همین
        // سرورِ خانگی می‌آید. پس نشانی باید کامل باشد، نه فقط تکهٔ ‎#roview…‎؛
        // با تکهٔ تنها، گوشیِ مشتری چیزی برای باز کردن ندارد.
        // ⚠️ نشانیِ **صفحه**، نه سرورِ هم‌گام‌سازی. در نسخهٔ وب هم این دو جدا
        // هستند: SELF_HOST_URL سرورِ دادهٔ وب‌سوکت است، ولی کیو‌آر از نشانیِ
        // خودِ صفحه ساخته می‌شود (window.location.href). گوشیِ مشتری صفحه را
        // باز می‌کند و آن صفحه است که داده را از سرور می‌گیرد.
        //
        // ⚠️ و ‎server‎/‎token‎ هم داخلِ نشانی می‌روند: گوشیِ مشتری این صفحه را
        // تا امروز باز نکرده، پس نمی‌داند به کدام سرور وصل شود و صفحه‌ای خالی
        // می‌بیند. خودِ سایت هم در ‎copyShareLink‎ همین کار را می‌کند.
        //
        // ⚠️ خواستهٔ صریحِ صاحب ریپو: «بدونِ نت هم که شده باید برای هر حساب
        // کیو‌آر ساخته بشه.» پس نبودِ نشانی جلوی ساختِ کد را نمی‌گیرد: همان
        // تکهٔ ‎#roview…‎ کد می‌شود — خواننده‌ی خودِ برنامه (‎AcctLink.Parse‎)
        // آن را می‌فهمد و حساب را باز می‌کند. فقط گفته می‌شود که برای گوشیِ
        // مشتری نشانیِ صفحه لازم است.
        var full = AcctLink.FullUrl(
            _host.Settings.GetString(SettingsKeys.ViewerUrl), Entity.Id, sub, "debt",
            _host.Settings.GetString(SettingsKeys.ServerUrl),
            _host.Settings.GetString(SettingsKeys.SyncCode));
        var link = full ?? AcctLink.Build(Entity.Id, sub);

        var png = await Task.Run(() => QrWriter.EncodePng(link));
        var title = isSub ? "📲 📄 " + acct.Title : "📲 " + Name;
        var hint = full is not null
            ? (isSub
                ? "این کد را به مشتری بدهید؛ با اسکنش حساب «" + acct.Title + "» را می‌بیند"
                : "این کد را به مشتری بدهید؛ با اسکنش حسابِ خودش را می‌بیند")
              + " — زنده، از همین سرور."
            : "این کد بی‌اینترنت ساخته شد و با خودِ همین برنامه خوانده می‌شود. "
              + "برای این‌که با گوشیِ مشتری هم باز شود، در «تنظیمات › نشانیِ صفحهٔ حساب» "
              + "نشانیِ صفحه را بنویسید (و نشانیِ سرور را، تا گوشی بداند به کجا وصل شود).";

        await Dialogs.ShowQrAsync(title, link, png, hint);
    });

    /// <summary>
    /// «✏️ تغییر اسم» — ‎renameCurrentPerson()‎ی سایت.
    ///
    /// در سایت داخلِ کشوییِ «⋯ بیشتر» است و در نیتیو هیچ‌جا نبود؛ برای عوض
    /// کردنِ نامِ یک قرض‌دار هیچ راهی وجود نداشت.
    /// </summary>
    [RelayCommand]
    private Task RenamePerson() => CrashGuard.RunAsync("تغییر اسم", async () =>
    {
        var name = (await Dialogs.PromptAsync("تغییر اسم", "نام تازه:", Name) ?? "").Trim();
        if (name.Length == 0 || name == Name) return;

        Entity.Name = name;
        await _host.Debtors.UpdateDebtorAsync(Entity);
        OnPropertyChanged(nameof(Name));
        await _section.ReloadAsync();
        _host.Toast("✅ نام به «" + name + "» عوض شد", ToastKind.Ok);
    });

    /// <summary>
    /// ══ «➕ حساب جدید» — ‎newPersonSub()‎ ═══════════════════════════════════
    ///
    /// گزارشِ صاحب ریپو: «حساب فرعی یک کادر کشویی است که از آن‌جا انتخاب بشود
    /// و آن حساب کاملاً جدا است و باید یک اسمِ جدا هم داشته باشد.»
    ///
    /// در سایت هم اول ‎prompt('نام حساب جدید:')‎ می‌آید و بی‌نام ساخته نمی‌شود.
    /// پیش از این نیتیو با نامِ ‎null‎ می‌ساخت و همهٔ حساب‌های فرعی «حسابِ
    /// فرعی» نام می‌گرفتند — در کشویی از هم شناخته نمی‌شدند.
    /// </summary>
    [RelayCommand]
    private Task AddSubAccountAsync() => CrashGuard.RunAsync("حساب جدید", async () =>
    {
        var name = (await Dialogs.PromptAsync("حساب جدید", "نام حساب جدید:") ?? "").Trim();
        if (name.Length == 0) return;

        var a = await _host.Debtors.AddSubAccountAsync(Entity.Id, name);
        Entity.SubAccounts.Add(a);
        var vm = new AccountViewModel(_host, a, this);
        Accounts.Add(vm);
        Current = vm;                 // فوراً دیده می‌شود — همان باگی که در نسخهٔ وب بود
        Recalc();
        _host.Toast("✅ حساب «" + name + "» ساخته شد", ToastKind.Ok);
    });

    [RelayCommand]
    private async Task DeleteSubAccountAsync(AccountViewModel? a)
    {
        if (a is null || a.Entity.MainOfDebtorId != null) return;
        await _host.Debtors.DeleteAccountAsync(a.Entity.Id);
        Entity.SubAccounts.Remove(a.Entity);
        Accounts.Remove(a);
        Current = Accounts.FirstOrDefault();
        Recalc();
    }

    public async Task FlushAsync()
    {
        foreach (var a in Accounts) await a.FlushAsync();
    }
}
