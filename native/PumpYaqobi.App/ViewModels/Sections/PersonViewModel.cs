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
        // سربرگِ دو تیل و ردیفِ «جمله» هم به همین ردیف بند‌اند
        _owner.RefreshTotals();
    }

    public string LitersText { get => Shamsi.Money(Liters); set => Liters = Shamsi.Num(value); }
    public string PriceText { get => Shamsi.Money(Price); set => Price = Shamsi.Num(value); }
    public string ManualBardagiText { get => Shamsi.Money(ManualBardagi); set => ManualBardagi = Shamsi.Num(value); }
    public string RasidText { get => Shamsi.Money(Rasid); set => Rasid = Shamsi.Num(value); }
    public string RasidFuelText { get => Shamsi.Money(RasidFuel); set => RasidFuel = Shamsi.Num(value); }

    /// <summary>بردگیِ پولیِ همین ردیف — از همان سرویسِ آزموده، نه حسابِ دستی.</summary>
    public string BardagiText => Shamsi.Money(_owner.Calc.RowBardagi(_r));

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
    public string FuelGroup => "rowFuel-" + _r.Id;

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

        Totals = new[]
        {
            new TotalCell("مقدار تیل", Shamsi.Money(t.All.Liters)),
            new TotalCell("بردگی", Shamsi.Money(t.All.Bardagi)),
            new TotalCell("رسید", Shamsi.Money(t.All.Rasid), "Pump.Ok"),
            new TotalCell("رسید تیل", Shamsi.Money(t.All.RasidFuel), "Pump.Ok"),
            new TotalCell("الباقی", Shamsi.Money(t.All.Albaqi),
                          t.All.Albaqi > 0m ? "Pump.Danger" : "Pump.Ok"),
        };
    }

    public DebtTableArchive Entity => _h;
    public ObservableCollection<ArchiveRowViewModel> Rows { get; } = new();
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

    public ObservableCollection<DebtRowViewModel> Rows { get; } = new();

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

    partial void OnRasidFuelPetrolChanged(decimal v) { Entity.RasidFuelPetrol = v; SaveAccount(); OnPropertyChanged(nameof(RasidFuelPetrolText)); RefreshTotals(); }
    partial void OnRasidFuelDieselChanged(decimal v) { Entity.RasidFuelDiesel = v; SaveAccount(); OnPropertyChanged(nameof(RasidFuelDieselText)); RefreshTotals(); }
    partial void OnRasidMoneyPetrolChanged(decimal v) { Entity.RasidMoneyPetrol = v; SaveAccount(); OnPropertyChanged(nameof(RasidMoneyPetrolText)); RefreshTotals(); }
    partial void OnRasidMoneyDieselChanged(decimal v) { Entity.RasidMoneyDiesel = v; SaveAccount(); OnPropertyChanged(nameof(RasidMoneyDieselText)); RefreshTotals(); }

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

    private SplitTotals Totals => Calc.SplitTotals(Rows.Select(r => r.Entity).ToList());

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
            nameof(ShowPetrolCard), nameof(ShowDieselCard),
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
    public string HeadPetrolRasidText =>
        Shamsi.Money(IsMoney ? RasidMoneyPetrol : RasidFuelPetrol);
    public string HeadPetrolBordText =>
        Shamsi.Money(IsMoney ? Totals.Petrol.Bardagi : Totals.Petrol.Liters);
    public string HeadPetrolAlbaqiText => Shamsi.Money(Totals.Petrol.Albaqi);
    public string HeadPetrolAlbaqiBrushKey =>
        Totals.Petrol.Albaqi > 0m ? "Pump.Danger" : "Pump.Ok";

    /// <summary>
    /// همان «مقدار رسید»ِ سربرگ، ولی نوشتنی — چون در سایت رسید را همان‌جا در
    /// سربرگ می‌نویسند، نه در یک کادرِ دیگر پایین‌تر.
    ///
    /// ⚠️ کدام فیلد را می‌نویسد به دفترِ باز بستگی دارد: دفترِ پول رسیدِ پول
    /// را عوض می‌کند و دفترِ تیل رسیدِ تیل را. این دو هرگز یکی نمی‌شوند.
    /// </summary>
    public string HeadPetrolRasidEdit
    {
        get => HeadPetrolRasidText;
        set { if (IsMoney) RasidMoneyPetrolText = value; else RasidFuelPetrolText = value; RefreshTotals(); }
    }

    // ── 🟤 حساب دیزل ──────────────────────────────────────────────────────
    public string HeadDieselPercentText => PercentDiesel == 0m ? "0" : Shamsi.Money(PercentDiesel);
    public string HeadDieselRasidText =>
        Shamsi.Money(IsMoney ? RasidMoneyDiesel : RasidFuelDiesel);
    public string HeadDieselBordText =>
        Shamsi.Money(IsMoney ? Totals.Diesel.Bardagi : Totals.Diesel.Liters);
    public string HeadDieselRasidEdit
    {
        get => HeadDieselRasidText;
        set { if (IsMoney) RasidMoneyDieselText = value; else RasidFuelDieselText = value; RefreshTotals(); }
    }

    public string HeadDieselAlbaqiText => Shamsi.Money(Totals.Diesel.Albaqi);
    public string HeadDieselAlbaqiBrushKey =>
        Totals.Diesel.Albaqi > 0m ? "Pump.Danger" : "Pump.Ok";

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
                new TotalCell(IsMoney ? "مقدار (افغانی)" : "مقدار تیل", Shamsi.Money(t.All.Liters)),
                new TotalCell("بردگی", Shamsi.Money(t.All.Bardagi)),
                new TotalCell("رسید", Shamsi.Money(t.All.Rasid), "Pump.Ok"),
                new TotalCell("رسید تیل", Shamsi.Money(t.All.RasidFuel), "Pump.Ok"),
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
        if (Rows.Count == 0) { _host.Toast("جدول همین حالا خالی است", ToastKind.Warn); return; }
        if (!await Dialogs.ConfirmAsync("جدول جدید",
                "جدولِ فعلی آرشیو می‌شود و جدولِ خالیِ تازه‌ای باز می‌شود. ادامه؟")) return;

        await FlushAsync();                       // هرچه نیم‌تایپ مانده، اول ذخیره شود
        await _host.Debtors.ArchiveTableAsync(Entity.Id, Shamsi.Today());

        // عکسِ حافظه هم باید با پایگاه یکی شود، وگرنه جدولِ پاک‌شده روی صفحه می‌ماند
        Entity.ActiveRows().Clear();
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
            nameof(SumLitersText), nameof(SumBardagiText), nameof(SumRasidText),
            nameof(SumRasidFuelText), nameof(SumAlbaqiText), nameof(TotalCells),
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
    public string RasidFuelPetrolText { get => Shamsi.Money(RasidFuelPetrol); set => RasidFuelPetrol = Shamsi.Num(value); }
    public string RasidFuelDieselText { get => Shamsi.Money(RasidFuelDiesel); set => RasidFuelDiesel = Shamsi.Num(value); }
    public string RasidMoneyPetrolText { get => Shamsi.Money(RasidMoneyPetrol); set => RasidMoneyPetrol = Shamsi.Num(value); }
    public string RasidMoneyDieselText { get => Shamsi.Money(RasidMoneyDiesel); set => RasidMoneyDiesel = Shamsi.Num(value); }

    private void SaveAccount()
    {
        _ = _host.Debtors.UpdateAccountAsync(Entity);
        _person.Recalc();
    }

    /// <summary>دفترِ دیده‌شده = دفترِ واحدِ همین حساب.</summary>
    private void BuildRows()
    {
        // خوددرمانیِ دادهٔ کهنه پیش از کشیدنِ جدول — همان کاری که renderPersonRows
        // می‌کرد. اگر چیزی عوض شد، همان‌جا ذخیره می‌شود تا دوباره لازم نشود.
        if (_host.Debt.NormalizeAccount(Entity)) _ = PersistHealedAsync();

        Rows.Clear();
        // ⚠️ فیلتر فقط روی «دیده شدن» است. ردیف‌های تیلِ دیگر سرِ جایشان‌اند و
        // در دیتابیس دست نمی‌خورند؛ فقط این‌بار ساخته نمی‌شوند.
        var want = RowFilter switch
        {
            "petrol" => (FuelType?)FuelType.Petrol,
            "diesel" => FuelType.Diesel,
            _ => null,
        };

        foreach (var r in Entity.ActiveRows()
                                .Where(r => want is null || r.Fuel == want)
                                .OrderBy(r => r.SortIndex).ThenBy(r => r.Id))
        {
            var vm = new DebtRowViewModel(r, this);
            vm.Recalculated += _person.Recalc;
            Rows.Add(vm);
        }

        // جدول عوض شد ⇒ سربرگ و ردیفِ «جمله» هم باید از نو خوانده شوند
        RefreshTotals();
    }

    private async Task PersistHealedAsync()
    {
        foreach (var r in Entity.FuelRows.Concat(Entity.MoneyRows))
            await _host.Debtors.SaveRowAsync(r);
    }

    public async Task SaveRowAsync(DebtRow r)
    {
        _host.Debt.NormalizeRow(r);          // بردگی و الباقی، دقیقاً مثلِ نسخهٔ وب
        await _host.Debtors.SaveRowAsync(r);
        _person.Recalc();
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
        _person.Recalc();
        RefreshTotals();
    }

    [RelayCommand]
    private async Task DeleteRowAsync(DebtRowViewModel? row)
    {
        if (row is null) return;
        await _host.Debtors.DeleteRowAsync(row.Entity.Id);
        Entity.FuelRows.Remove(row.Entity);
        Entity.MoneyRows.Remove(row.Entity);
        Rows.Remove(row);
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
        var link = AcctLink.Build(Entity.Id, sub);

        var png = await Task.Run(() => QrWriter.EncodePng(link));
        var title = isSub ? "📲 📄 " + acct.Title : "📲 " + Name;
        var hint = isSub
            ? "با اسکن این کد، حساب «" + acct.Title + "» باز می‌شود"
            : "با اسکن این کد، حساب همین شخص باز می‌شود";

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
