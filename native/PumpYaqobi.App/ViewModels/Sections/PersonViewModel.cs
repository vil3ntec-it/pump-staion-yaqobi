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
        OnPropertyChanged(nameof(FuelChipText));
        OnPropertyChanged(nameof(FuelChipBrushKey));
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

    /// <summary>
    /// ══ نشانِ نوعِ تیل — یک کپسول، بی دایرهٔ رادیو ═══════════════════════════
    ///
    /// گزارشِ صاحب ریپو با عکسِ سایت: «اون دکمه‌های رادیو دیده نشن ولی باشن …
    /// بین پطرول و دیزل یکی فقط انتخاب بشه مثلِ رادیو، و شکلش هم شبیهِ این
    /// عکسِ دوم باشه.»
    ///
    /// در سایت هر ردیف **یک** کپسولِ کوچک دارد — «پطرول» یا «دیزل» — نه دو
    /// دایره روی هم. رفتار همان رادیو است (همیشه دقیقاً یکی)، فقط دایره‌اش
    /// دیده نمی‌شود: زدنِ کپسول می‌بَردش به آن یکی.
    ///
    /// با این، ستون از دو ردیفِ رادیو به یک کپسول رسید و ردیفِ جدول هم دیگر
    /// لازم نیست بلند بماند.
    /// </summary>
    public string FuelChipText => Fuel.ToPersian();

    /// <summary>رنگِ همان کپسول — سبزِ پطرول یا کهربایِ دیزل.</summary>
    public string FuelChipBrushKey => IsDiesel ? "Pump.Warn" : "Pump.Ok";

    /// <summary>پطرول ⇄ دیزل — همان کاری که زدنِ دکمهٔ رادیوی دیگر می‌کرد.</summary>
    [RelayCommand]
    private void ToggleFuel() =>
        Fuel = IsDiesel ? FuelType.Petrol : FuelType.Diesel;

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
        Adopt(a);
    }

    /// <summary>
    /// ══ همین ویومدل، حسابِ دیگر ═══════════════════════════════════════════
    /// چرایی‌اش بالای <see cref="DebtSectionViewModel.PersonOpen"/>: صفحه دور
    /// انداخته نمی‌شود، پس ‎Rows‎ همان شیء می‌ماند و ‎DataGrid‎ ردیف‌هایش را
    /// بازیافت می‌کند به‌جای ساختنِ دوبارهٔ هر خانه.
    ///
    /// ⚠️ ‎ArchiveCount‎ هم صفر می‌شود، وگرنه دکمهٔ آرشیوِ حسابِ قبلی روی
    /// حسابِ تازه می‌ماند تا شمارشِ تازه برسد.
    /// </summary>
    internal void Load(DebtAccount a)
    {
        Entity = a;
        OnPropertyChanged(nameof(WidthScope));
        ArchiveCount = 0;
        _rowFilter = "all";      // فیلترِ حسابِ قبلی روی حسابِ تازه نماند
        Adopt(a);
        OnPropertyChanged(nameof(Title));
        RefreshAll();
    }

    private void Adopt(DebtAccount a)
    {
        _pullingSums = true;
        _isMoney = a.Mode.IsMoney();
        _percentPetrol = _host.Debt.PercentOf(a, FuelType.Petrol);
        _percentDiesel = _host.Debt.PercentOf(a, FuelType.Diesel);
        _rasidFuelPetrol = a.RasidFuelPetrol;
        _rasidFuelDiesel = a.RasidFuelDiesel;
        _rasidMoneyPetrol = a.RasidMoneyPetrol;
        _rasidMoneyDiesel = a.RasidMoneyDiesel;
        _pullingSums = false;
        BuildRows();
    }

    /// <summary>پس از نشستنِ حسابِ تازه، هر کادرِ سربرگ دوباره خوانده شود.</summary>
    private void RefreshAll()
    {
        foreach (var n in new[]
        {
            nameof(IsMoney), nameof(PercentPetrol), nameof(PercentDiesel),
            nameof(RasidFuelPetrol), nameof(RasidFuelDiesel),
            nameof(RasidMoneyPetrol), nameof(RasidMoneyDiesel),
            nameof(PercentPetrolText), nameof(PercentDieselText), nameof(PercentBothText),
            nameof(RasidFuelPetrolText), nameof(RasidFuelDieselText),
            nameof(RasidMoneyPetrolText), nameof(RasidMoneyDieselText),
            nameof(RowFilter), nameof(IsFilterAll), nameof(IsFilterPetrol), nameof(IsFilterDiesel),
            nameof(ShowPetrolCard), nameof(ShowDieselCard), nameof(ShowFuelTypeColumn),
        })
            OnPropertyChanged(n);
        RefreshTotals();
    }

    public DebtAccount Entity { get; private set; }

    /// <summary>دامنهٔ پهنای ستون‌ها — هر حساب پهنای خودش (‎ExcelGrid.WidthScope‎).</summary>
    public string WidthScope => "debt-" + Entity.Id;

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
        SaveGuard.Watch(_host.Debtors.UpdateAccountAsync(Entity), "حسابِ قرض‌دار");
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

    // ⚠️ و بعد صاحب ریپو با عکسِ خودِ سایت گفت: «نوشته‌های اضافه نباشه داخلش …
    // کوچیک و با مفهوم و بدون گرفتنِ جای زیاد». حق داشت و عکس هم همین را نشان
    // می‌داد: سایت **امروز** «فیصدی / رسید قبلی / برد / الباقی» می‌نویسد، نه
    // آن نام‌های بلندِ داخلِ شناسه‌ها. «پول» و «تیل» هم لازم نیست — خودِ دکمهٔ
    // «واحدِ پول ⇄ واحدِ تیل» بالای صفحه می‌گوید در کدام دفتریم.
    public string HeadRasidLabel => "رسید قبلی";
    public string HeadAlbaqiLabel => "الباقی";
    public string HeadPetrolPercentLabel => "فیصدی";
    public string HeadDieselPercentLabel => "فیصدی";

    /// <summary>عددِ فیصدی با نشانِ درصد — در سایت مقدار است، نه برچسب.</summary>
    public string HeadPetrolPercentValue => HeadPetrolPercentText + "٪";
    public string HeadDieselPercentValue => HeadDieselPercentText + "٪";

    /// <summary>نوشتهٔ دکمهٔ تعویضِ دفتر — همتای ‎#pm-mode-btn‎ی سایت.</summary>
    public string ModeToggleText => IsMoney ? "🔁 تیل" : "🔁 پول";

    /// <summary>
    /// ══ «واحدِ پول» و «واحدِ تیل» — دو دکمه، نه یکی ═════════════════════════
    ///
    /// گزارشِ صاحب ریپو با عکسِ سایت: «بالا رو ببین، واحد تیل و پول هم خیلی
    /// خوب در اومدن — می‌خام این جزئیات رو دقیق درست کنی.»
    ///
    /// در برنامه یک دکمهٔ «🔁 پول» بود که نوشته‌اش عوض می‌شد، یعنی همیشه نامِ
    /// دفترِ **دیگر** را نشان می‌داد. آدم نمی‌توانست بگوید الان کجاست. سایت دو
    /// دکمهٔ کنارِ هم دارد و آن که فعال است پررنگ می‌ماند — همان رادیو.
    /// </summary>
    public bool IsFuelUnit => !IsMoney;

    [RelayCommand]
    private void SetUnit(string? which)
    {
        var money = which == "money";
        if (IsMoney != money) IsMoney = money;
    }

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
    /// <summary>همان «رسید قبلی»ِ کادرِ صفحه — برای سندِ چاپی.</summary>
    internal decimal HeadRasidOf(FuelType fuel) => HeadRasid(fuel);

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
        if (v == 0m) { RefreshTotals(); RepaintHeadEdits(); return; }
        SaveGuard.Watch(AddHeadReceiptAsync(fuel, v), "رسیدِ سربرگ");
    }

    /// <summary>
    /// ⚠️ کادرِ رسیدِ سربرگ با ‎LostFocus‎ می‌نویسد، و اتصالِ دوطرفه در همان لحظه
    /// که دارد مقدار را به منبع می‌دهد، خبرِ برگشتی را نادیده می‌گیرد (پاسدارِ
    /// بازگشتی). پس اگر کاربر کادر را باز کند و چیزی ننویسد، کادر خالی می‌ماند تا
    /// دوباره وارد حساب شود — گزارشِ صاحب ریپو. خبر یک فریم بعد دوباره داده
    /// می‌شود تا کادر جمعِ واقعی را نشان بدهد.
    /// </summary>
    private void RepaintHeadEdits() =>
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            OnPropertyChanged(nameof(HeadPetrolRasidEdit));
            OnPropertyChanged(nameof(HeadDieselRasidEdit));
        }, Avalonia.Threading.DispatcherPriority.Background);

    /// <summary>
    /// ردیفی که هیچ چیزی در آن نوشته نشده — جای طبیعیِ رسیدِ تازه.
    ///
    /// ⛔ <b>قاعده‌اش یکی است و در ‎PostingService‎ نوشته شده.</b> این‌جا
    /// نسخهٔ دومی داشت و با نسخهٔ سمتِ ثبت (ورق، پارچه، رسیدِ سریع) مو‌به‌مو
    /// یکی نبود؛ یعنی رسیدِ سربرگ یک ردیف را «خالی» می‌دید و همگام‌سازیِ ورق
    /// همان را «پُر». دو حقیقت برای یک جدول، و عددی که جابه‌جا می‌شد.
    /// </summary>
    private static bool IsBlank(DebtRow r) => PostingService.IsBlankRow(r);

    /// <summary>
    /// رسیدِ سربرگ **از اول** در جدول می‌نشیند، نه تهِ آن. خواستهٔ صاحب ریپو:
    /// «میره آخرِ جدول رسید زده می‌شه؛ می‌خوام به ترتیب از اول بره، اگه پر بود
    /// بره دومی، اگه جدول نبود خودش بسازه.» پس اولین ردیفِ خالیِ جدول (از بالا)
    /// پر می‌شود؛ اگر هیچ ردیفِ خالی‌ای نبود، ردیفِ تازه ساخته می‌شود.
    /// </summary>
    private async Task AddHeadReceiptAsync(FuelType fuel, decimal value)
    {
        var unit = IsMoney ? LedgerMode.Money : LedgerMode.Fuel;
        var row = _host.Debt.AddReceiptRow(Entity, unit, fuel, value, Shamsi.Today());

        var blank = Rows.FirstOrDefault(r => IsBlank(r.Entity));
        if (blank is not null)
        {
            // همان رسید، ولی روی ردیفِ خالیِ موجود؛ ردیفِ تازه دور ریخته می‌شود
            (IsMoney ? Entity.MoneyRows : Entity.FuelRows).Remove(row);
            blank.Entity.ByMoney = row.ByMoney;
            blank.Fuel = fuel;
            blank.DateShamsi = row.DateShamsi ?? "";
            if (IsMoney) blank.Rasid = row.Rasid; else blank.RasidFuel = row.RasidFuel;
            blank.Entity.Rasid = row.Rasid; blank.Entity.RasidFuel = row.RasidFuel;
            blank.Entity.DateShamsi = row.DateShamsi; blank.Entity.Fuel = fuel;
            _host.Debt.NormalizeRow(blank.Entity);
            await _host.Debtors.SaveRowAsync(blank.Entity);
            await SyncReceiptsAsync();
            _person.Recalc();
            RefreshTotals();
            RepaintHeadEdits();
            return;
        }

        await _host.Debtors.SaveRowAsync(row);
        await SyncReceiptsAsync();

        var vm = new DebtRowViewModel(row, this);
        vm.Recalculated += _person.Recalc;
        Rows.Add(vm);

        _person.Recalc();
        RefreshTotals();
        RepaintHeadEdits();
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


    [ObservableProperty] private int _archiveCount;

    public bool HasArchives => ArchiveCount > 0;
    /// <summary>
    /// «🗂️ آرشیو ۳» — کوتاه و با شمارِ جدول‌ها.
    /// ⚠️ «— جدول» از میانش برداشته شد (۱۴۰۵/۰۷/۰۷): نوارِ حساب یک
    /// <c>WrapPanel</c> است و هر واژهٔ اضافه یک دکمه را به خطِ بعد می‌انداخت.
    /// </summary>
    public string ArchiveToggleText =>
        "🗂️ آرشیو " + Shamsi.Money(ArchiveCount);
    partial void OnArchiveCountChanged(int v)
    {
        OnPropertyChanged(nameof(HasArchives));
        OnPropertyChanged(nameof(ArchiveToggleText));
    }

    /// <summary>شمارِ آرشیوها را می‌خواند، بی ساختنِ خودِ جدول‌ها.</summary>
    // ══ 🧾 فاکتورهای همین مشتری — هم در صف، هم تاییدشده ═══════════════════
    //
    // گزارشِ صاحب ریپو (۱۴۰۵/۰۶/۲۷): «فاکتور به اسمِ قرض‌داری که هست نمی‌رود
    // داخلِ حسابِ همان قرض‌دار و تو صفش نیست.» فاکتورِ تاییدشده از قبل ردیفِ
    // رسیدِ خودش را در جدول می‌گرفت (بندِ ۱۲ در ‎verify‎)، ولی فاکتورِ **در
    // صف** هیچ‌جای این صفحه دیده نمی‌شد — و همان «تو صفش نیست»ِ گزارش بود.
    //
    // ⚠️ فقط دیدنی است: هیچ عددی از این‌جا وارد بدهی، الباقی یا رسید نمی‌شود.
    // تاییدِ فاکتور همان‌جای همیشگی‌اش است (بخشِ فاکتورها).

    public ObservableCollection<AcctInvoiceViewModel> Invoices { get; } = new();

    [ObservableProperty] private string _invoicesText = "";

    public bool HasInvoices => Invoices.Count > 0;

    [ObservableProperty] private bool _isInvoicesOpen;

    /// <summary>
    /// «🧾 فاکتورها ۲ 🟡۱ ▼» — کوتاه، تا کنارِ «🗂️ آرشیو» در همان خط جا شود.
    ///
    /// گزارشِ صاحب ریپو (۱۴۰۵/۰۷/۰۷): «اون کادر کشوییِ فاکتور رو کوچیک کن و
    /// بزار بغلِ اون کادرهای دیگه… الان اومده پایینِ اون کادرها و جا اشغال
    /// کرده… اسمشو هم بزار فاکتورها ۱ یا هر چند تا که تو صف بودن.»
    ///
    /// ⛔ نوشتهٔ بلندِ قبلی («🧾 فاکتورها: ۲ · 🟡 ۱ در صف» / «· همه تایید شده»)
    /// دکمه را از نوار بیرون می‌انداخت. حالا شمارِ در صف فقط یک نشانِ کوچک
    /// است و وقتی صفر باشد اصلاً نوشته نمی‌شود.
    /// ⚠️ هیچ عددی عوض نشد — همان دو عدد، فقط کوتاه‌تر نوشته می‌شوند.
    /// </summary>
    public string InvoicesToggleText =>
        InvoicesText.Length == 0 ? "" : InvoicesText + (IsInvoicesOpen ? " ▲" : " ▼");

    partial void OnIsInvoicesOpenChanged(bool v) => OnPropertyChanged(nameof(InvoicesToggleText));

    [RelayCommand]
    private void ToggleInvoices() => IsInvoicesOpen = !IsInvoicesOpen;

    public async Task LoadInvoicesAsync()
    {
        try
        {
            var name = Entity.MainOfDebtorId != null ? _person.Name : (Entity.Name ?? _person.Name);
            var list = await _host.Invoices.ForAccountAsync(Entity.Id, name);
            Invoices.Clear();
            foreach (var v in list) Invoices.Add(new AcctInvoiceViewModel(v));
            var pend = list.Count(v => v.Status != InvoiceStatus.Approved);
            InvoicesText = list.Count == 0 ? ""
                : "🧾 فاکتورها " + Shamsi.Money(list.Count)
                  + (pend > 0 ? " 🟡" + Shamsi.Money(pend) : "");
        }
        catch { /* فاکتور رفاه است، نه حساب */ }
        OnPropertyChanged(nameof(HasInvoices));
        OnPropertyChanged(nameof(InvoicesToggleText));
    }

    public async Task LoadArchiveCountAsync()
    {
        //  ⚠️ شمارنده با `COUNT` می‌آید، نه با خواندنِ همهٔ آرشیوها: `RowsJson`ِ
        //  هر جدولِ آرشیو صدها ردیفِ سریال‌شده است و این‌جا فقط یک عدد
        //  می‌خواهیم (اسکنِ کاملِ ۱۴۰۵/۰۶/۳۰).
        try { ArchiveCount = await _host.Debtors.CountArchivesAsync(Entity.Id); }
        catch { /* شمارنده نباید صفحه را بشکند */ }
    }

    /// <summary>‎openPersonArchive‎ — صفحهٔ جداگانهٔ جدول‌های آرشیوِ همین حساب (مثلِ شرکت‌ها).</summary>
    [RelayCommand]
    private Task ToggleArchivesAsync() => CrashGuard.RunAsync("جدول‌های آرشیو", async () =>
    {
        await FlushAsync();
        await _person.OpenArchivesAsync(this);
    });

    private Task ReloadArchivesAsync() => LoadArchiveCountAsync();

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
        //  ⛔ و بعد هیچ ردیفِ این جدول دیگر حقِ نوشتن ندارد — وگرنه نوشتنی که
        //  بعد از آرشیو برسد، ردیف را به جدولِ تازه برمی‌گرداند.
        foreach (var r in Rows.ToList()) await r.RetireAsync();
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
        _person.Recalc();
        _host.Toast("✅ جدول جدید ساخته شد — جدولِ قبلی در آرشیو نشست", ToastKind.Ok);
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
            nameof(HeadPetrolPercentValue), nameof(HeadDieselPercentValue),
            nameof(ModeToggleText), nameof(IsFuelUnit),
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
        SaveGuard.Watch(_host.Debtors.UpdateAccountAsync(Entity), "حسابِ قرض‌دار");
        _person.Recalc();
    }

    /// <summary>دفترِ دیده‌شده = دفترِ واحدِ همین حساب.</summary>
    private void BuildRows()
    {
        // ⚠️ اول مهاجرت، بعد هر چیزِ دیگر: حساب‌های قدیمی رسیدشان را در چهار
        // عددِ خودِ حساب (و نسخهٔ پیشین، در دفترِ جدا) دارند. یک‌بار به ردیفِ
        // واقعی تبدیل می‌شوند تا از این پس فقط یک انبار بماند و هیچ عددی گم
        // نشود. بارِ دوم چیزی نمی‌سازد.
        // ⚠️ «بارِ دوم چیزی نمی‌سازد» تا امروز دروغ بود: نشانِ ‎ReceiptsMigrated‎
        // و خالی شدنِ ‎RasidLog‎ فقط در حافظه می‌نشستند و هیچ‌وقت ذخیره
        // نمی‌شدند، پس هر بار که همین حساب باز می‌شد همان رسیدها **دوباره**
        // ردیف می‌شدند. ‎enterperf‎ همین را لو داد (شش ‎INSERT‎ و بیست‌وچهار
        // ‎UPDATE‎ با هر باز کردن، و شمارِ ردیف‌ها ۱۰۰ ⇒ ۱۰۶ ⇒ ۱۱۲).
        // حالا نشان هم با همان ردیف‌ها در یک ‎SaveChanges‎ می‌نشیند.
        var firstTime = !Entity.ReceiptsMigrated;
        var moved = _host.Debt.MigrateReceiptsToRows(Entity, Shamsi.Today());
        if (firstTime) _ = PersistMigratedAsync(moved);

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
        await _host.Debtors.CommitReceiptMigrationAsync(Entity, rows);
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
        await row.RetireAsync();

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
            _ = vm.LoadInvoicesAsync();
        }
        _current = Accounts.FirstOrDefault();
        Recalc();
    }

    /// <summary>
    /// ══ همین صفحه، شخصِ دیگر ══════════════════════════════════════════════
    /// چرایی‌اش بالای <see cref="DebtSectionViewModel.PersonOpen"/>. ویومدلِ
    /// هر حساب هم **بازیافت** می‌شود، نه ساختِ تازه — وگرنه ‎Current.Rows‎ یک
    /// شیءِ نو می‌شد و ‎DataGrid‎ باز هم همه‌چیز را از صفر می‌ساخت.
    /// </summary>
    internal void Load(Debtor d)
    {
        Entity = d;

        var want = d.AllAccounts().ToList();
        for (var i = 0; i < want.Count; i++)
        {
            if (i < Accounts.Count) Accounts[i].Load(want[i]);
            else Accounts.Add(new AccountViewModel(_host, want[i], this));
        }
        while (Accounts.Count > want.Count) Accounts.RemoveAt(Accounts.Count - 1);

        Current = Accounts.FirstOrDefault();
        foreach (var a in Accounts) { _ = a.LoadArchiveCountAsync(); _ = a.LoadInvoicesAsync(); }

        foreach (var n in new[] { nameof(Name), nameof(Phone), nameof(PhoneText), nameof(BuyFeeText) })
            OnPropertyChanged(n);
        Recalc();
    }

    public Debtor Entity { get; private set; }
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
            SaveGuard.Watch(_host.Debtors.UpdateDebtorAsync(Entity), "مشخصاتِ قرض‌دار");
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
            SaveGuard.Watch(_host.Debtors.UpdateDebtorAsync(Entity), "مشخصاتِ قرض‌دار");
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
            //  ⛔ همان «رسید قبلی»ِ کادرِ صفحه، نه فیلدِ کهنهٔ سربرگ — شرحش بالای
            //  ‎DebtorStatementReport‎.
            RasidPetrol: acct.HeadRasidOf(FuelType.Petrol),
            RasidDiesel: acct.HeadRasidOf(FuelType.Diesel),
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

    /// <summary>صفحهٔ جدول‌های آرشیوِ یک حساب — روی همین شخص باز می‌شود.</summary>
    public Task OpenArchivesAsync(AccountViewModel acct) => _section.OpenArchivesAsync(this, acct);

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
        if (!Entitlements.Gate(_host, Entitlements.QrLive)) return;

        // حسابِ فرعی همیشه ‎LegacySubId‎ دارد (هم آن‌هایی که از نسخهٔ وب آمده‌اند،
        // هم آن‌هایی که ‎AddSubAccountAsync‎ می‌سازد)، پس ‎IsMain‎ همان محکِ درست است.
        var isSub = !acct.Entity.IsMain;
        var sub = isSub ? acct.Entity.LegacySubId : null;

        // ══ کیو‌آر: تمامِ حساب داخلِ خودِ کد ══════════════════════════════
        //
        // خواستهٔ صریحِ صاحب ریپو: «طرف که اسکن می‌کند سایت باز بشود و تمامِ
        // اطلاعاتش تو گوشی بیاید؛ یک سایتِ جدید برای دیدن، آن سایتِ قبلی را
        // ول کن.»
        //
        // پس دیگر نه ‎server‎ی در نشانی است و نه ‎token‎ی: عکسِ حساب فشرده
        // می‌شود و **داخلِ خودِ کیو‌آر** می‌رود، و صفحهٔ ‎view/‎ همان را نشان
        // می‌دهد. یعنی گوشیِ مشتری نه رمز می‌خواهد، نه به سرورِ خانگی نیاز
        // دارد، و کدِ چاپ‌شده همان عددهای همان روز را نگه می‌دارد.
        var snap = AcctSnapshots.ForDebtAccount(
            Name, isSub ? acct.Title : null, acct.Entity,
            acct.Rows.Select(r => r.Entity).ToList(), _host.Debt);

        // کیو‌آرِ زنده: رمزِ همین حساب (یک‌بار ساخته و ذخیره می‌شود) تا صفحهٔ
        // مشتری هر دقیقه از ابر بپرسد «تازه‌تر شد؟». خالی ⇒ همان کدِ ایستا.
        var live = await AcctLive.EnsureAsync(_host, acct.Entity);

        var link = AcctView.Url(_host.Settings.GetString(SettingsKeys.ViewerUrl), snap,
                                AcctLink.Build(Entity.Id, sub), live);

        var png = await Task.Run(() => QrWriter.EncodePng(link));
        var title = isSub ? "📲 📄 " + acct.Title : "📲 " + Name;
        var hint = "این کد را به مشتری بدهید؛ با اسکنش همین حساب — با همهٔ "
                 + "ردیف‌هایش — روی گوشیِ خودش باز می‌شود. رمز نمی‌خواهد"
                 + AcctLive.Hint(_host, live);

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

/// <summary>
/// یک فاکتورِ همین حساب، فقط برای دیدن — شماره، تاریخ، تیل، مقدار و حال.
/// هیچ فرمانی ندارد و در هیچ جمعی نیست.
/// </summary>
public sealed class AcctInvoiceViewModel
{
    public AcctInvoiceViewModel(Invoice v)
    {
        Number = v.InvoiceNumber;
        DateShamsi = v.DateShamsi ?? "";
        Approved = v.Status == InvoiceStatus.Approved;
        Fuel = v.Fuel;
        Liters = v.Liters;
        Amount = v.Amount;
    }

    public int Number { get; }
    public string DateShamsi { get; }
    public bool Approved { get; }
    public FuelType Fuel { get; }
    public decimal Liters { get; }
    public decimal Amount { get; }

    public string NumberText => "شمارهٔ " + Shamsi.Money(Number);
    public string DateText => DateShamsi.Length > 0 ? DateShamsi : "—";
    public string FuelText => Fuel == FuelType.Diesel ? "🟤 دیزل" : "⛽ پطرول";
    public string ValueText =>
        Liters > 0m ? Shamsi.Money(Math.Round(Liters, 2), 2) + " لیتر"
        : Amount > 0m ? Shamsi.Money(Amount) + " افغانی" : "—";
    public string StatusText => Approved ? "🟢 تایید شده" : "🟡 در صف";
    public string StatusBrushKey => Approved ? "Pump.Ok" : "Pump.Warn";
}
