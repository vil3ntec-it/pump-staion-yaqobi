using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using PumpYaqobi.App.Controls;
using CommunityToolkit.Mvvm.Input;
using PumpYaqobi.App.Printing;
using PumpYaqobi.App.Services;
using PumpYaqobi.Application.Localization;
using PumpYaqobi.Application.Services;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;
using PumpYaqobi.Reporting.Pdf;
using PumpYaqobi.Services.Data;

namespace PumpYaqobi.App.ViewModels.Sections;

/// <summary>یک «پایه» (نازل) در ورق.</summary>
public sealed partial class WaraqPumpViewModel : RowViewModel
{
    private readonly WaraqPump _p;
    private readonly WaraqPageViewModel _owner;

    public WaraqPumpViewModel(WaraqPump p, WaraqPageViewModel owner)
    {
        _p = p; _owner = owner;
        Loading = true;
        _num = p.Num; _fuel = p.Fuel; _start = p.Start; _end = p.End;
        _price = p.PricePerLiter; _debt = p.Debt; _note = p.Note ?? "";
        _worker = p.Worker ?? ""; _pumpDate = p.DateShamsi ?? "";
        _lowBase = p.LowBase;
        Loading = false;
    }

    public WaraqPump Entity => _p;

    [ObservableProperty] private int _num;
    [ObservableProperty] private FuelType _fuel;
    [ObservableProperty] private decimal _start;
    [ObservableProperty] private decimal _end;
    [ObservableProperty] private decimal _price;
    [ObservableProperty] private decimal _debt;
    [ObservableProperty] private string _note = "";

    /// <summary>ستونِ «نام» — کارمندِ همین پایه، مثلِ جدولِ ورق در نسخهٔ وب.</summary>
    [ObservableProperty] private string _worker = "";

    /// <summary>ستونِ «تاریخ» — خالی یعنی همان تاریخِ ورق.</summary>
    [ObservableProperty] private string _pumpDate = "";

    // ══ نشانِ «شروعِ این پایه از پایهٔ قبلی کمتر بود» ═══════════════════════
    //
    // خواستهٔ صریحِ صاحب ریپو (۱۴۰۵/۰۷/۰۶): «اگر ثبت را زد و توی ورق برود،
    // آن عدد سرخ باشد که آدم بفهمد این کمتر است — و با یک دکمه بشود عادی‌اش
    // کرد.»
    //
    // ⚠️ **چرا خودِ عددِ «شروع» سرخ نمی‌شود**: ستونِ «شروع» یک
    // ‎DataGridTextColumn‎ است و رنگِ هر ردیف را جدا نمی‌گیرد. تبدیلش به
    // ستونِ قالبی یعنی ‎ExcelGrid.Write‎ (کپی/پیست/‎Delete‎) دیگر مسیرش را
    // پیدا نمی‌کند — ‎PathOf‎ فقط روی ستونِ متنی جواب می‌دهد. پس نشان یک
    // ستونِ **کنارِ همان** است: «🔴 کمتر»، درست بغلِ عدد، با راهنما.
    //
    // ⛔ و هیچ محاسبه‌ای از این نمی‌گذرد: نه لیتر، نه فروش، نه جمع.
    [ObservableProperty] private bool _lowBase;

    partial void OnLowBaseChanged(bool v)
    {
        Touch();
        OnPropertyChanged(nameof(LowBaseText));
    }

    public string LowBaseText => LowBase ? "🔴 کمتر" : "";

    /// <summary>دکمهٔ «عادی شد» — فقط نشان را برمی‌دارد، عدد دست نمی‌خورد.</summary>
    [RelayCommand]
    private void ClearLowBase() => LowBase = false;

    partial void OnWorkerChanged(string v) => Touch();
    partial void OnPumpDateChanged(string v) => Touch();
    partial void OnNumChanged(int v) => Touch();
    partial void OnFuelChanged(FuelType v)
    {
        Touch();
        OnPropertyChanged(nameof(FuelText));
        OnPropertyChanged(nameof(FuelChipText));
        OnPropertyChanged(nameof(FuelChipBrushKey));
    }
    partial void OnStartChanged(decimal v) { Touch(); Refresh(); }
    partial void OnEndChanged(decimal v) { Touch(); Refresh(); }
    partial void OnPriceChanged(decimal v) { Touch(); Refresh(); }
    partial void OnDebtChanged(decimal v) { Touch(); Refresh(); }
    partial void OnNoteChanged(string v) => Touch();

    private void Refresh()
    {
        foreach (var n in new[] { nameof(StartText), nameof(EndText), nameof(PriceText),
                                  nameof(DebtText), nameof(LitersText), nameof(SalesText) })
            OnPropertyChanged(n);
    }

    public string StartText { get => Shamsi.MoneyOrBlank(Start); set => Start = Shamsi.Num(value); }
    public string EndText { get => Shamsi.MoneyOrBlank(End); set => End = Shamsi.Num(value); }
    public string PriceText { get => Shamsi.MoneyOrBlank(Price); set => Price = Shamsi.Num(value); }
    public string DebtText { get => Shamsi.MoneyOrBlank(Debt); set => Debt = Shamsi.Num(value); }

    /// <summary>لیترِ منفی وجود ندارد — ‎Math.max(0, end−start)‎.</summary>
    public decimal Liters => Math.Max(0m, End - Start);
    public string LitersText => Shamsi.Money(Liters);
    public string SalesText => Shamsi.Money(Liters * Price);

    /// <summary>گزینه‌های کشویی — رشته، نه ‎ComboBoxItem‎ (باگِ ‎SelectedItem‎).</summary>
    public static string[] FuelOptions { get; } = { "پطرول", "دیزل" };

    public string FuelText
    {
        get => Fuel.ToPersian();
        set => Fuel = value == "دیزل" ? FuelType.Diesel : FuelType.Petrol;
    }

    // ══ کپسول به‌جای کشویی ════════════════════════════════════════════════
    //
    // چرا: هر ‎ComboBox‎ی داخلِ خانه یک قالبِ کامل با ‎Popup‎ و
    // ‎ItemsPresenter‎ی خودش می‌سازد — ده‌ها بصری برای دو گزینه. سنجشِ
    // ‎waraqperf‎ نشان داد ورقِ ۸۰ ردیفی ~۱٬۵۰۰ میلی‌ثانیه باز می‌شود و
    // بیشترش کارِ همین ردیف‌هاست. با دو گزینه، کشویی هم برای کاربر بد است:
    // خودِ صاحب ریپو گفت «فلشش فقط جا می‌گیرد» و «با تب یا اینتر عوض بشه».
    // کپسول هر دو را حل می‌کند: یک ‎Button‎، و ‎ExcelGrid‎ با ‎Enter‎/‎Tab‎
    // فرمانش را می‌زند (کلاسِ ‎celltoggle‎).
    public string FuelChipText => Fuel.ToPersian();

    public string FuelChipBrushKey => Fuel == FuelType.Diesel ? "Pump.Warn" : "Pump.Ok";

    [RelayCommand]
    private void ToggleFuel() =>
        Fuel = Fuel == FuelType.Diesel ? FuelType.Petrol : FuelType.Diesel;

    protected override void Apply()
    {
        _p.Num = Num; _p.Fuel = Fuel; _p.Start = Start; _p.End = End;
        _p.PricePerLiter = Price; _p.Debt = Debt; _p.Note = Note;
        _p.Worker = Worker; _p.DateShamsi = PumpDate; _p.LowBase = LowBase;
    }

    protected override Task SaveAsync() => _owner.SavePumpAsync(_p);
}

/// <summary>یک ردیفِ «قرض/مصرف» در ورق.</summary>
public sealed partial class WaraqTxnViewModel : RowViewModel
{
    private readonly WaraqTransaction _t;
    private readonly WaraqPageViewModel _owner;

    public WaraqTxnViewModel(WaraqTransaction t, WaraqPageViewModel owner)
    {
        _t = t; _owner = owner;
        Loading = true;
        _name = t.Name ?? ""; _liters = t.Liters; _amount = t.Amount;
        _isExpense = t.Type == WaraqTxnType.Expense; _fuel = t.Fuel;
        _isMoney = t.Unit == LedgerMode.Money;
        Loading = false;
    }

    public WaraqTransaction Entity => _t;

    /// <summary>
    /// ستونِ «#» — شمارهٔ ردیف در کلِ شیفت، نه در جدولی که تویش نشسته.
    /// در سایت هم ‎n2fa(i+1)‎ از ایندکسِ آرایهٔ کلِ تراکنش‌ها می‌آید، پس
    /// جدولِ دوم از همان‌جا که جدولِ اول تمام شده ادامه می‌دهد (۹، ۱۰، …).
    /// </summary>
    [ObservableProperty] private string _index = "";

    [ObservableProperty] private string _name = "";
    [ObservableProperty] private decimal _liters;
    [ObservableProperty] private decimal _amount;
    [ObservableProperty] private bool _isExpense;
    [ObservableProperty] private FuelType _fuel;

    /// <summary>
    /// ══ نام که عوض شد، سوخت و واحد خودشان را پیدا می‌کنند ═══════════════════
    ///
    /// خواستهٔ صریحِ صاحب ریپو (۱۴۰۵/۰۷/۰۶): «ستونِ نوعِ تیل را حذف کن ولی اگر
    /// در نام پطرول/دیزل (یا «پ»/«د»ی خالی) نوشتم، در حسابِ یارو همان انتخاب
    /// بشه… و اسمِ قرض‌دار را که نوشتم، سیستم اتومات تشخیص بده واحدِ این حساب
    /// تیل است یا پول.»
    ///
    /// ⚠️ تصمیم این‌جا گرفته نمی‌شود — هر دو قاعده در
    /// <see cref="PostingService"/> است، همان‌جا که خودِ پست هم از آن
    /// می‌خواند. دو نسخه یعنی روزی ردیف در دفتری بنشیند که صفحه نشانش داده
    /// بود.
    /// </summary>
    partial void OnNameChanged(string v)
    {
        Touch();
        if (!Loading) _owner.AutoFromName(this);
    }
    partial void OnLitersChanged(decimal v) { _t.AmountAuto ??= true; Touch(); Refresh(); }
    partial void OnIsExpenseChanged(bool v)
    {
        Touch();
        OnPropertyChanged(nameof(TypeText));
        OnPropertyChanged(nameof(TypeChipBrushKey));
    }

    partial void OnFuelChanged(FuelType v)
    {
        Touch();
        OnPropertyChanged(nameof(FuelText));
        OnPropertyChanged(nameof(FuelChipText));
        OnPropertyChanged(nameof(FuelChipBrushKey));
    }

    /// <summary>مبلغی که کاربر خودش بنویسد دیگر خودکار نیست و بازحساب نمی‌شود.</summary>
    partial void OnAmountChanged(decimal v)
    {
        if (!Loading) _t.AmountAuto = false;
        Touch(); Refresh();
    }

    private void Refresh()
    {
        OnPropertyChanged(nameof(LitersText));
        OnPropertyChanged(nameof(AmountText));
        OnPropertyChanged(nameof(EffectiveAmountText));
    }

    public string LitersText { get => Shamsi.MoneyOrBlank(Liters); set => Liters = Shamsi.Num(value); }
    public string AmountText { get => Shamsi.MoneyOrBlank(Amount); set => Amount = Shamsi.Num(value); }

    /// <summary>مبلغی که واقعاً در جمع‌ها شمرده می‌شود.</summary>
    public string EffectiveAmountText => Shamsi.Money(_owner.Calc.TxnAmount(_owner.Shift!, _t));

    /// <summary>
    /// پس از تازه‌شدنِ فیِ ورق، مبلغِ مؤثر دوباره خوانده شود — و اگر مبلغ
    /// خودکار است، خانهٔ «مبلغ» هم همان عددِ خودکار را نشان دهد (نه صفر).
    /// </summary>
    public void RefreshEffective()
    {
        if (_t.AmountAuto == true && _amount != _t.Amount)
        {
            _amount = _t.Amount;                 // بی‌آنکه ذخیره‌ای راه بیفتد
            OnPropertyChanged(nameof(Amount));
            OnPropertyChanged(nameof(AmountText));
        }
        OnPropertyChanged(nameof(EffectiveAmountText));
    }

    /// <summary>گزینه‌های کشویی — رشته، نه ‎ComboBoxItem‎ (باگِ ‎SelectedItem‎).</summary>
    public static string[] TypeOptions { get; } = { "قرض", "مصرف" };
    public static string[] FuelOptions { get; } = { "پطرول", "دیزل" };

    public string TypeText
    {
        get => IsExpense ? "مصرف" : "قرض";
        set => IsExpense = value == "مصرف";
    }

    public string FuelText
    {
        get => Fuel.ToPersian();
        set => Fuel = value == "دیزل" ? FuelType.Diesel : FuelType.Petrol;
    }

    /// <summary>
    /// ستونِ «واحد» — ‎t.unit‎ی سایت: این ردیفِ قرض به دفترِ «واحد تیل» برود
    /// یا دفترِ «واحد پول». پیش‌فرض تیل، مثلِ سایت.
    ///
    /// ⚠️ فعلاً فقط ذخیره می‌شود: تراکنش‌های ورق هنوز به حسابِ قرض‌داران پست
    /// نمی‌شوند (‎syncWaraqTxnsToPersons‎ی سایت هنوز همتا ندارد). پس انتخابِ
    /// کاربر می‌ماند و از دست نمی‌رود، ولی تا آن پست ساخته نشود چیزی را
    /// جابه‌جا نمی‌کند.
    /// </summary>
    /// <summary>گزینه‌های کشویی — رشته، نه ‎ComboBoxItem‎ (باگِ ‎SelectedItem‎).</summary>
    public static string[] UnitOptions { get; } = { "تیل", "پول" };

    public string UnitText
    {
        get => IsMoney ? "پول" : "تیل";
        set => IsMoney = value == "پول";
    }

    [ObservableProperty] private bool _isMoney;

    partial void OnIsMoneyChanged(bool v)
    {
        Touch();
        OnPropertyChanged(nameof(UnitText));
        OnPropertyChanged(nameof(UnitChipBrushKey));
    }

    // ══ سه کپسول به‌جای سه کشویی ══════════════════════════════════════════
    //
    // هر ردیفِ تراکنش سه کادرِ کشویی داشت — نوعِ تیل، نوع، واحد — و هر کدام
    // یک قالبِ کاملِ ‎ComboBox‎ با ‎Popup‎ی خودش. سنجشِ ‎waraqperf‎ روی ورقِ
    // ۶۰ ردیفی: باز شدن ~۱٬۵۰۰ میلی‌ثانیه، که بیشترش ساختنِ همین‌هاست.
    //
    // و خواستهٔ خودِ صاحب ریپو هم همین بود: «اون فلش فقط جا گرفته» و «با تب
    // یا اینتر عوض بشه». کپسول یک ‎Button‎ است؛ ‎ExcelGrid‎ با ‎Enter‎/‎Tab‎
    // فرمانش را می‌زند (کلاسِ ‎celltoggle‎). با دو گزینه، کشویی هیچ‌چیزِ
    // بیشتری نمی‌داد.
    //
    // ⚠️ ‎FuelText‎/‎TypeText‎/‎UnitText‎ سرِ جای خود مانده‌اند: هم نوشتنی‌اند
    // (کپسول از همان‌ها می‌خواند) و هم جاهای دیگر — کپی، PDF، آزمون‌ها —
    // رویشان حساب کرده‌اند.

    /// <summary>
    /// نوشتهٔ روی کپسولِ نوعِ تیل.
    ///
    /// ⚠️ یک بار نبودش و کپسول **خالی** دیده می‌شد: نما ‎FuelChipText‎ را
    /// می‌خواست و این کلاس فقط ‎FuelText‎ داشت. اتصالِ نبوده در آوالونیا
    /// بی‌صدا خالی می‌ماند، پس نه خطایی می‌آمد نه چیزی — فقط ستونِ «نوع تیل»
    /// در ورق سفید بود. گزارشِ صاحب ریپو: «نوع تیل توی بخش ورق‌ها دیده
    /// نمی‌شه.»
    /// </summary>
    public string FuelChipText => Fuel.ToPersian();

    public string FuelChipBrushKey => Fuel == FuelType.Diesel ? "Pump.Warn" : "Pump.Ok";

    /// <summary>سرخِ قرض یا بنفشِ مصرف — همان رنگ‌هایی که در جمع‌ها هم هست.</summary>
    public string TypeChipBrushKey => IsExpense ? "Pump.Purple" : "Pump.Danger";

    public string UnitChipBrushKey => IsMoney ? "Pump.Accent" : "Pump.Ok";

    [RelayCommand]
    private void ToggleFuel() =>
        Fuel = Fuel == FuelType.Diesel ? FuelType.Petrol : FuelType.Diesel;

    [RelayCommand]
    private void ToggleType() => IsExpense = !IsExpense;

    [RelayCommand]
    private void ToggleUnit() => IsMoney = !IsMoney;

    protected override void Apply()
    {
        _t.Name = Name; _t.Liters = Liters; _t.Amount = Amount;
        _t.Type = IsExpense ? WaraqTxnType.Expense : WaraqTxnType.Debt;
        _t.Fuel = Fuel;
        _t.Unit = IsMoney ? LedgerMode.Money : LedgerMode.Fuel;
    }

    protected override Task SaveAsync() => _owner.SaveTxnAsync(_t);
}

/// <summary>صفحهٔ یک ورق — شیفتِ روز و شب، پایه‌ها و ردیف‌های قرض/مصرف.</summary>
public sealed partial class WaraqPageViewModel : ObservableObject, IRowBatchHost
{
    private readonly AppHost _host;
    private readonly WaraqSectionViewModel _section;

    /// <summary>نوارِ «➕ ردیف / ➕➕ چندتایی»ِ پایینِ ورق — همتای ‎addWaraqRowsBulk‎ی سایت.</summary>
    public System.Windows.Input.ICommand? RowAddCommand => AddTxnCommand;

    public WaraqPageViewModel(AppHost host, WaraqEntry w, WaraqSectionViewModel section)
    {
        _host = host; _section = section; Entity = w;
        _isNight = w.ActiveShift == ShiftKind.Night;
        Build();
    }

    /// <summary>
    /// ══ همین صفحه، ورقِ دیگر ═══════════════════════════════════════════════
    ///
    /// خواستهٔ صاحب ریپو: «بازگشت به صفحهٔ اصلی نباید تأخیر داشته باشد» و
    /// «ورق زود باز شود».
    ///
    /// ⚠️ و سنجش گفت ریشه کجاست: پیش از این هر بار باز کردنِ ورق یک
    /// ‎WaraqPageViewModel‎ی **تازه** می‌ساخت و بستنش ‎Page‎ را ‎null‎ می‌کرد.
    /// چون نمای صفحه ‎DataContext="{Binding Page}"‎ است، هر بار کلِ درختِ
    /// بصری — دو جدولِ تراکنش با قالب و سبک و ستون‌هایشان — از نو ساخته
    /// می‌شد. اندازه‌گیری (‎waraqperf‎): یک ‎UpdateLayout()‎ی ۱٬۱۸۹ میلی‌ثانیه،
    /// که ۹۲۲ تایش مالِ همین دو جدول بود؛ با پنهان کردنشان ۲۶۷ می‌شد.
    ///
    /// حالا ویومدل یکی است و فقط **بار می‌شود**: جدول‌ها سرِ جایشان می‌مانند و
    /// تنها ردیف‌هایشان عوض می‌شود.
    /// </summary>
    public bool IsDay => !IsNight;

    /// <summary>روز/شب با دو دکمهٔ رادیویی — نه کلیدِ لغزان.</summary>
    [RelayCommand]
    private void SetNight(string? which) => IsNight = which == "night";

    public void Load(WaraqEntry w)
    {
        Entity = w;
        _isNight = w.ActiveShift == ShiftKind.Night;
        OnPropertyChanged(nameof(IsNight));
        OnPropertyChanged(nameof(IsDay));
        OnPropertyChanged(nameof(Title));
        Build();
    }

    public WaraqEntry Entity { get; private set; }
    public WaraqService Calc => _host.Waraq;
    public string Title => "ورقِ " + (Entity.DateShamsi ?? "");

    public ObservableCollection<WaraqPumpViewModel> Pumps { get; } = new();
    public ObservableCollection<WaraqTxnViewModel> Txns { get; } = new();

    // ══ دو جدولِ هم‌شکل، کنارِ هم ═══════════════════════════════════════════
    //
    // خواستهٔ صاحب ریپو: «توی ورق‌ها دو کادر دارد که شبیه هم است و آن بابتِ
    // این است که بیشتر جا بشود.»
    //
    // در سایت هم همین است — ‎renderWaraqTransactions‎:
    //
    //     const mid = Math.ceil(txns.length / 2);
    //     … if (i < mid) leftBody.appendChild(tr); else rightBody.appendChild(tr);
    //
    // یعنی نیمهٔ اول در جدولِ اول و نیمهٔ دوم در جدولِ دوم؛ با ۱۵ ردیف
    // می‌شود ۸ و ۷. چون کلِ پنجره ‎RightToLeft‎ است، جدولِ اول سمتِ راست
    // دیده می‌شود — درست مثلِ عکسی که صاحب ریپو فرستاد (۱ تا ۸ راست،
    // ۹ تا ۱۵ چپ).
    //
    // ⚠️ ‎Txns‎ همچنان یگانه‌سرچشمهٔ حقیقت است؛ این دو فقط نما هستند و هیچ
    // ردیفی را دو بار نگه نمی‌دارند.
    public ObservableCollection<WaraqTxnViewModel> TxnsFirst { get; } = new();
    public ObservableCollection<WaraqTxnViewModel> TxnsSecond { get; } = new();

    /// <summary>‎mid = ceil(n/2)‎ — مو‌به‌مو همان تقسیمِ سایت.</summary>
    private void SplitTxns()
    {
        TxnsFirst.Clear();
        TxnsSecond.Clear();
        var mid = (int)Math.Ceiling(Txns.Count / 2.0);
        for (var i = 0; i < Txns.Count; i++)
        {
            Txns[i].Index = Shamsi.Money(i + 1);
            (i < mid ? TxnsFirst : TxnsSecond).Add(Txns[i]);
        }
    }

    [ObservableProperty] private bool _isNight;
    [ObservableProperty] private string _workerName = "";
    [ObservableProperty] private decimal _fabricDebt;
    [ObservableProperty] private string _petrolLiters = "";
    [ObservableProperty] private string _dieselLiters = "";
    [ObservableProperty] private string _sales = "";
    [ObservableProperty] private string _debt = "";
    [ObservableProperty] private string _expenses = "";
    [ObservableProperty] private string _shortage = "";
    [ObservableProperty] private string _shortageLabel = "";

    /// <summary>جمعِ لیترِ همین شیفت — خانهٔ ‎#wq-total-liters‎ی سایت.</summary>
    [ObservableProperty] private string _pumpLiters = "";

    /// <summary>جمعِ ستونِ «جمله قرض»ِ پایه‌ها — خانهٔ ‎#wq-total-debt‎ی سایت.</summary>
    [ObservableProperty] private string _pumpDebt = "";

    /// <summary>عددِ خامِ کمبودی/اضافی — فقط برای رنگِ کادرِ ششم.</summary>
    private decimal _shortageAmount;

    public WaraqShift? Shift =>
        Entity.Shifts.FirstOrDefault(s => s.Kind == (IsNight ? ShiftKind.Night : ShiftKind.Day));

    public string FabricDebtText
    {
        get => Shamsi.Money(FabricDebt);
        set => FabricDebt = Shamsi.Num(value);
    }

    partial void OnIsNightChanged(bool v) { OnPropertyChanged(nameof(IsDay)); Build(); }

    /// <summary>
    /// ⚠️ هنگامِ پر کردنِ اولیهٔ کادرها هیچ چیزی ذخیره نمی‌شود.
    /// بدونِ این، نشستنِ «کارمندِ شیفت» یک ذخیره راه می‌انداخت که «قرضِ پارچه»
    /// را با مقدارِ هنوز-پرنشده (صفر) روی دیتابیس می‌نوشت و عددِ واقعی پاک می‌شد.
    /// </summary>
    private bool _filling;

    partial void OnWorkerNameChanged(string v) => SaveShift();

    partial void OnFabricDebtChanged(decimal v)
    {
        OnPropertyChanged(nameof(FabricDebtText));
        SaveShift();
    }

    private void SaveShift()
    {
        if (_filling) return;
        var sd = Shift;
        if (sd is null) return;
        sd.WorkerName = WorkerName;
        sd.FabricDebt = FabricDebt;
        _ = _host.WaraqData.SaveShiftAsync(sd);
        Recalc();
    }


    private void Build()
    {
        var sd = Shift;
        Pumps.Clear(); Txns.Clear();
        if (sd is null) return;

        _filling = true;
        WorkerName = sd.WorkerName ?? "";
        FabricDebt = sd.FabricDebt;
        _filling = false;

        foreach (var p in sd.Pumps.OrderBy(p => p.SortIndex).ThenBy(p => p.Id))
        {
            var vm = new WaraqPumpViewModel(p, this);
            vm.Recalculated += Recalc;
            Pumps.Add(vm);
        }
        foreach (var t in sd.Transactions.OrderBy(t => t.SortIndex).ThenBy(t => t.Id))
        {
            var vm = new WaraqTxnViewModel(t, this);
            vm.Recalculated += Recalc;
            Txns.Add(vm);
        }
        SplitTxns();
        Recalc();
    }

    public void Recalc()
    {
        var sd = Shift;
        if (sd is null) return;
        var t = Calc.ShiftTotals(sd);
        PetrolLiters = Shamsi.Money(t.PetrolLiters);
        DieselLiters = Shamsi.Money(t.DieselLiters);
        Sales = Shamsi.Money(Math.Round(t.Sales, 0, MidpointRounding.AwayFromZero));
        Debt = Shamsi.Money(Math.Round(t.Debt, 0, MidpointRounding.AwayFromZero));
        Expenses = Shamsi.Money(Math.Round(t.Expenses, 0, MidpointRounding.AwayFromZero));
        PumpLiters = Shamsi.Money(t.PetrolLiters + t.DieselLiters) + " لیتر";
        PumpDebt = Shamsi.Money(Math.Round(t.DeclaredDebt, 0, MidpointRounding.AwayFromZero));

        var sh = Calc.Shortage(t);
        if (sh.Shortage > 0)
        { ShortageLabel = "کمبودی"; _shortageAmount = Math.Round(sh.Shortage); Shortage = Shamsi.Money(_shortageAmount); }
        else if (sh.Excess > 0)
        { ShortageLabel = "اضافی"; _shortageAmount = Math.Round(sh.Excess); Shortage = Shamsi.Money(_shortageAmount); }
        else
        { ShortageLabel = "کمبودی"; _shortageAmount = 0m; Shortage = "0"; }

        foreach (var x in Txns) x.RefreshEffective();
        OnPropertyChanged(nameof(TotalCells));
        RefreshSummary();
    }

    /// <summary>شش کادرِ «خلاصه شیفت» از عددهای بالا ساخته می‌شوند، پس با هر
    /// حساب دوباره باید خوانده شوند.</summary>
    private void RefreshSummary()
    {
        OnPropertyChanged(nameof(SumPetrol));
        OnPropertyChanged(nameof(SumDiesel));
        OnPropertyChanged(nameof(SumExpenses));
        OnPropertyChanged(nameof(SumDebt));
        OnPropertyChanged(nameof(SumSales));
        OnPropertyChanged(nameof(SumShortage));
        OnPropertyChanged(nameof(ShortageBoxLabel));
        OnPropertyChanged(nameof(ShortageBrushKey));
        OnPropertyChanged(nameof(SummaryTitle));
    }

    // ══ خلاصهٔ شیفت — زیرِ جدولِ تراکنش‌ها ═══════════════════════════════════
    //
    // گزارشِ صاحب ریپو: «اون شش تا پایینِ این جدولِ تراکنش‌ها استن.» حق داشت؛
    // در سایت هم همان‌جاست: ‎index.html‎ خط ۱۹۹۷۴ — عنوانِ
    // ‎#wq-sum-shift-title‎ و شش ‎.stat-box‎ **بعد از** دو جدولِ تراکنش می‌آیند،
    // نه بالای صفحه. واحدها هم از خط ۴۵۵۴۴ تا ۴۵۵۶۵ برداشته شده‌اند: «لیتر»
    // برای تیل و «افغانی» برای پول.
    public string SummaryTitle => "📊 خلاصه شیفت " + (IsNight ? "شب" : "روز");

    public string SumPetrol => PetrolLiters + " لیتر";
    public string SumDiesel => DieselLiters + " لیتر";
    public string SumExpenses => Expenses + " افغانی";
    public string SumDebt => Debt + " افغانی";
    public string SumSales => Sales + " افغانی";
    public string SumShortage => Shortage + " افغانی";

    /// <summary>برچسبِ کادرِ ششم — «⚠️ کمبودی» یا «✅ اضافی»، مثلِ خودِ سایت.</summary>
    public string ShortageBoxLabel => ShortageLabel == "اضافی" ? "✅ اضافی" : "⚠️ کمبودی";

    /// <summary>رنگِ عددِ همان کادر: سرخِ کمبودی، سبزِ اضافی، خاکستریِ صفر.</summary>
    public string ShortageBrushKey =>
        _shortageAmount <= 0m ? "Pump.Muted"
        : ShortageLabel == "اضافی" ? "Pump.Ok" : "Pump.Danger";

    /// <summary>
    /// ردیفِ «جمله این شیفت»ِ ته جدولِ قرائت پمپ‌ها — همتای ‎&lt;tfoot&gt;‎ی سایت
    /// (‎index.html‎ خط ۱۹۹۲۹): مقدارِ لیتر، مبلغِ فروش و «جمله قرض»ِ پایه‌ها،
    /// هر کدام زیرِ ستونِ خودش.
    ///
    /// ⚠️ پیش از این، شش عددِ «خلاصه شیفت» این‌جا نشسته بودند؛ ولی آن‌ها جمعِ
    /// این جدول نیستند و در سایت هم کادرهای جداگانه‌ای زیرِ جدولِ تراکنش‌ها
    /// هستند — حالا همان‌جا‌اند.
    ///
    /// ⚠️ فقط شیفتی که باز است؛ روز و شب هرگز با هم جمع نمی‌شوند.
    /// </summary>
    public IReadOnlyList<TotalCell> TotalCells => new[]
    {
        new TotalCell("لیتر", PumpLiters),
        new TotalCell("فروش", SumSales, "Pump.Ok"),
        new TotalCell("قرضِ پایه", PumpDebt, "Pump.Danger"),
    };

    /// <summary>
    /// ‎printWaraq()‎ — ورقِ **همان شیفتی که باز است** (روز یا شب)، نه هر دو.
    /// در نسخهٔ وب هم دقیقاً همین است: ‎sd = isNight ? w.night : w.day‎.
    /// </summary>
    [RelayCommand]
    private Task PdfAsync()
    {
        var sd = Shift;
        if (sd is null) return Task.CompletedTask;

        var input = new WaraqReportInput(
            Entity.Station ?? "", Entity.DateShamsi ?? "",
            IsNight ? ShiftKind.Night : ShiftKind.Day, sd, DocDates.Line());

        return Documents.ShowAsync(() => new WaraqReport(input, Calc),
                                   (IsNight ? "ورق شب " : "ورق روز ") + (Entity.DateShamsi ?? ""));
    }

    public async Task SavePumpAsync(WaraqPump p)
    {
        await _host.WaraqData.SavePumpAsync(p);
        Recalc();
        // فیِ پایه که عوض شود، مبلغِ خودکارِ ردیف‌ها هم عوض می‌شود — پس حساب‌ها
        // باید همان لحظه تازه شوند، نه بعداً.
        await PostAsync();
    }

    public async Task SaveTxnAsync(WaraqTransaction t)
    {
        await _host.WaraqData.SaveTxnAsync(t);
        Recalc();
        await PostAsync();
    }

    /// <summary>
    /// ══ ردیف‌های ورق ⇐ حسابِ قرض‌دار / مصارف ═══════════════════════════════
    ///
    /// گزارشِ صاحب ریپو: «اون حسابِ طرف رو که توی ورق زدم… چرا اتومات نمی‌ره
    /// تو حساب‌اش؟» سایت این را در ‎syncWaraqTxnsToPersons‎ می‌کرد — هم موقعِ
    /// «ذخیره ورق» و هم همان لحظه‌ای که «واحد/نوع/نوع تیل» عوض می‌شد.
    ///
    /// این‌جا هر تغییرِ ردیف همین کار را می‌کند، پس نامی که نوشته می‌شود بی
    /// هیچ دکمه‌ای به حسابِ صاحبش می‌رسد.
    ///
    /// ⚠️ خطا هرگز به بیرون درز نمی‌کند: ورق باید ذخیره‌شدنی بماند حتی اگر
    /// همگام‌سازی به هر دلیلی نگیرد.
    /// </summary>
    private async Task PostAsync()
    {
        try { LastPost = await _host.WaraqPosting.SyncAsync(Entity.Id); }
        catch { /* ورق ذخیره شده؛ همگام‌سازی دفعهٔ بعد دوباره تلاش می‌کند */ }
    }

    /// <summary>آخرین گزارشِ همگام‌سازی — برای آزمون و برای نوارِ وضعیت.</summary>
    public WaraqPostReport LastPost { get; private set; }

    [RelayCommand]
    private async Task AddPumpAsync()
    {
        var sd = Shift;
        if (sd is null) return;
        var p = new WaraqPump
        {
            ShiftId = sd.Id,
            SortIndex = sd.Pumps.Count,
            Num = sd.Pumps.Count + 1,
            PricePerLiter = sd.PricePerLiter,
        };
        await _host.WaraqData.SavePumpAsync(p);
        sd.Pumps.Add(p);
        var vm = new WaraqPumpViewModel(p, this);
        vm.Recalculated += Recalc;
        Pumps.Add(vm);
        Recalc();
    }

    [RelayCommand]
    private async Task DeletePumpAsync(WaraqPumpViewModel? row)
    {
        if (row is null) return;
        await _host.WaraqData.DeletePumpAsync(row.Entity.Id);
        Shift?.Pumps.Remove(row.Entity);
        Pumps.Remove(row);
        Recalc();
    }

    [RelayCommand]
    private async Task AddTxnAsync()
    {
        var sd = Shift;
        if (sd is null) return;
        var t = new WaraqTransaction { ShiftId = sd.Id, SortIndex = sd.Transactions.Count };
        await _host.WaraqData.SaveTxnAsync(t);
        sd.Transactions.Add(t);
        var vm = new WaraqTxnViewModel(t, this);
        vm.Recalculated += Recalc;
        Txns.Add(vm);
        SplitTxns();
        Recalc();
    }

    [RelayCommand]
    private async Task DeleteTxnAsync(WaraqTxnViewModel? row)
    {
        if (row is null) return;
        var sd = Shift;
        if (sd is null) return;
        await _host.WaraqData.DeleteTxnAsync(row.Entity.Id);
        sd.Transactions.Remove(row.Entity);
        Txns.Remove(row);
        SplitTxns();
        Recalc();
        // ردیف که رفت، ثبتش در حسابِ قرض‌دار یا مصارف هم باید برود
        await PostAsync();
    }

    public int RowCount => Txns.Count;

    /// <summary>‎Ctrl+عدد‎ / ‎Shift+عدد‎ روی جدولِ تراکنش‌های ورقِ باز.
    /// مثلِ نسخهٔ وب دستِ‌کم یک ردیف می‌ماند و ردیفِ کافی نبود، هیچ.</summary>
    public async Task AddRowsAsync(int count)
    {
        for (var i = 0; i < count; i++) await AddTxnAsync();
    }

    public async Task DeleteRowsAsync(int count)
    {
        if (count < 1 || Txns.Count - count < 1) return;
        for (var i = 0; i < count; i++) await DeleteTxnAsync(Txns[^1]);
    }

    [RelayCommand]
    private Task BackAsync() => _section.BackCommand.ExecuteAsync(null);

    /// <summary>پلِ «نام ⇒ سوخت و واحد» — تصمیمش در بخش است، نه این‌جا.</summary>
    internal void AutoFromName(WaraqTxnViewModel row) => _section.AutoFromName(row);

    public async Task FlushAsync()
    {
        foreach (var p in Pumps.ToList()) await p.FlushAsync();
        foreach (var t in Txns.ToList()) await t.FlushAsync();
        await PostAsync();
    }
}

/// <summary>
/// ══ بخشِ ورق‌های روزانه ═════════════════════════════════════════════════════
/// فهرستِ ورق‌ها و صفحهٔ هر ورق. یک ورق برای هر روز — ورقِ تکراری ساخته نمی‌شود.
/// </summary>
/// <summary>
/// کارتِ یک ورق در فهرست — مو‌به‌مو همان کارتی که <c>renderWaraqList</c>
/// می‌سازد: «☀️🌙 تاریخ»، نامِ جایگاه و کارمندان، مبلغِ فروشِ هر دو شیفت،
/// و خطِ «قرض / مصرف».
/// </summary>
public sealed class WaraqCardViewModel
{
    public WaraqCardViewModel(WaraqEntry w, WaraqService calc)
    {
        Entity = w;
        Title = "☀️🌙 " + (string.IsNullOrWhiteSpace(w.DateShamsi) ? "—" : w.DateShamsi);

        var day = w.Shifts.FirstOrDefault(s => s.Kind == ShiftKind.Day);
        var night = w.Shifts.FirstOrDefault(s => s.Kind == ShiftKind.Night);
        var d = day is null ? default : calc.ShiftTotals(day);
        var n = night is null ? default : calc.ShiftTotals(night);

        var workers = new[] { day?.WorkerName, night?.WorkerName }
            .Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
        SubText = (w.Station ?? "") + (workers.Length > 0 ? " — " + string.Join(" / ", workers) : "");

        // عددِ ساده — «فروشِ ورق · افغانی» زیرش در کارت می‌آید
        SalesText = Money(d.Sales + n.Sales);
        DebtText = "قرض: " + Money(d.Debt + n.Debt);
        ExpenseText = "مصرف: " + Money(d.Expenses + n.Expenses);
    }

    private static string Money(decimal v) =>
        Shamsi.Money(Math.Round(v, 0, MidpointRounding.AwayFromZero));

    public WaraqEntry Entity { get; }
    public string Title { get; }
    public string SubText { get; }
    public string SalesText { get; }
    public string DebtText { get; }
    public string ExpenseText { get; }
}

public sealed partial class WaraqSectionViewModel : SectionViewModel
{
    private readonly AppHost _host;

    // ══ «این نام مالِ کدام حساب است؟» — یک کَش، با ترمزِ Version ══════════════
    //
    // ⛔ این با **هر حرفِ تایپِ کاربر** صدا زده می‌شود، پس یک پرس‌وجو به ازای
    // هر کلید همان کندی‌ای است که قاعدهٔ سرعتِ این ریپو قدغنش کرده. تا شمارهٔ
    // دفتر عوض نشود، **صفر** دستورِ دیتابیس.
    private List<AccountUnitRow>? _units;
    private Dictionary<long, AccountUnitRow>? _unitById;
    private List<Debtor>? _unitPeople;
    private long _unitsVersion = -1;

    /// <summary>
    /// نام که عوض شد: سوخت از خودِ متن، و واحد از حسابی که نامش خورده.
    ///
    /// ⚠️ منتظرش نمی‌مانیم و هیچ استثنایی بیرون نمی‌دهد: این یک **راحتی**
    /// است، و خودِ پست (<see cref="WaraqPostingService"/>) دوباره و مستقل
    /// همین دو قاعده را می‌زند. پس نشدنش هیچ عددی را غلط نمی‌کند.
    /// </summary>
    internal void AutoFromName(WaraqTxnViewModel row) => _ = AutoFromNameAsync(row);

    private async Task AutoFromNameAsync(WaraqTxnViewModel row)
    {
        try
        {
            var text = row.Name ?? "";
            if (text.Trim().Length == 0) return;

            // ۱) سوخت: «پطرول» · «دیزل» · «پ» · «د» — هر جای جمله
            if (PostingService.MentionsFuel(text))
                row.Fuel = PostingService.DetectFuelType(text);

            // ۲) واحد: تیل یا پول، از روی حسابی که نامش خورده
            await EnsureUnitsAsync();
            if (_unitPeople is null || _unitById is null) return;
            if ((row.Name ?? "") != text) return;      // کاربر ادامه داد — نتیجه کهنه است

            var hw = PostingService.ExtractHawala(text);
            var display = PostingService.StripFuelWords(
                string.IsNullOrWhiteSpace(hw.Clean) ? text : hw.Clean);

            // ⛔ همان یک تطبیق‌کنندهٔ همیشگی — حساب‌های فرعی را هم می‌بیند.
            var m = PostingService.FindAccountForText(_unitPeople, text, display);
            if (m is null) return;
            if (!_unitById.TryGetValue(m.Value.Account.Id, out var info)) return;

            var want = PostingService.UnitForAccount(info.HasFuelRows, info.HasMoneyRows, info.Mode);
            if (want is not null) row.IsMoney = want.Value == LedgerMode.Money;
        }
        catch { /* راحتی است، نه اصل */ }
    }

    private async Task EnsureUnitsAsync()
    {
        var v = PumpYaqobi.Persistence.PumpDbContext.Version;
        if (_units is not null && _unitsVersion == v) return;

        var rows = await _host.Debtors.AccountUnitsAsync();

        // گرافِ سبکِ «شخص ⇒ حساب‌ها» تا همان ‎FindAccountForText‎ی همیشگی
        // بتواند رویش کار کند. ⛔ هیچ ردیفی در این گراف نیست.
        var people = new Dictionary<long, Debtor>();
        foreach (var r in rows)
        {
            if (!people.TryGetValue(r.PersonId, out var p))
            {
                p = new Debtor { Id = r.PersonId, Name = r.PersonName };
                people[r.PersonId] = p;
            }
            var a = new DebtAccount { Id = r.AccountId, Name = r.AccountName, Mode = r.Mode };
            if (r.IsMain) { a.MainOfDebtorId = r.PersonId; p.MainAccount = a; }
            // ⚠️ ‎LegacySubId‎ لازم است: ‎DebtAccount.IsMain‎ از همان می‌خواند.
            else { a.DebtorId = r.PersonId; a.LegacySubId = "s" + r.AccountId; p.SubAccounts.Add(a); }
        }

        _units = rows;
        _unitById = rows.ToDictionary(x => x.AccountId);
        _unitPeople = people.Values.ToList();
        _unitsVersion = v;
    }

    public WaraqSectionViewModel(AppHost host) : base("waraq", "waraq", "ورق‌های روزانه")
    {
        // ↓ درِ «🕘 تاریخچه»ی همین بخش — شرحش بالای ‎SectionViewModel.HistoryKind‎
        HistoryKind = "waraq";

        _host = host;
        _month = Shamsi.ThisMonth();
    }

    public ObservableCollection<WaraqEntry> Sheets { get; } = new();
    /// <summary>کارت‌های همان ورق‌ها، با جمعِ هر دو شیفت.</summary>
    public ObservableCollection<WaraqCardViewModel> Cards { get; } = new();
    public bool IsEmpty => Cards.Count == 0;
    public ObservableCollection<string> Months { get; } = new();

    [ObservableProperty] private string _month;

    // ══════════════════════════════════════════════════════════════════════
    //  ══ «بخشِ ورق‌ها ماه و سال ندارد» ══════════════════════════════════════
    // ══════════════════════════════════════════════════════════════════════
    //
    //  گزارشِ صاحب ریپو (۱۴۰۵/۰۷/۰۶). کشویی بود، ولی چیزی که نشان می‌داد
    //  کلیدِ خامِ «1405/06» بود — نه نامِ ماه و نه سالِ جدا. با پنج سال داده
    //  آن کشویی شصت ردیفِ عددی می‌شود و پیدا کردنِ «سنبلهٔ پارسال» در آن
    //  یعنی شمردن.
    //
    //  حالا دو کشویی: **سال**، و **ماهِ همان سال** با نامِ فارسی‌اش.
    //
    //  ⛔ ‎Month‎ و ‎Months‎ دست نخوردند و همچنان کلیدِ خام‌اند: هر جای دیگری
    //  که به آن‌ها بند است (خواندنِ فهرست، گرم کردن، سنجه‌ها) باید همان
    //  بماند. این دو فقط **نمایش**‌اند و سرِ آخر همان ‎Month‎ را می‌نویسند.

    /// <summary>یک گزینهٔ کشوییِ ماه — «سنبله 1405»، با کلیدِ خامش زیرش.</summary>
    public sealed record MonthOption(string Key, string Label)
    {
        public override string ToString() => Label;
    }

    /// <summary>سال‌هایی که ورق دارند — تازه‌ترین اول.</summary>
    public ObservableCollection<string> Years { get; } = new();

    /// <summary>ماه‌های همان سالِ برگزیده.</summary>
    public ObservableCollection<MonthOption> MonthOptions { get; } = new();

    [ObservableProperty] private string _year = "";
    [ObservableProperty] private MonthOption? _selectedMonth;

    /// <summary>جلوگیری از حلقه وقتی خودِ کد کشویی‌ها را می‌نشاند.</summary>
    private bool _pickerWriting;

    partial void OnYearChanged(string? v)
    {
        if (_pickerWriting || string.IsNullOrEmpty(v)) return;
        BuildMonthOptions(v!, preferred: null);
    }

    partial void OnSelectedMonthChanged(MonthOption? v)
    {
        if (_pickerWriting || v is null) return;
        Month = v.Key;                      // ⇒ ‎OnMonthChanged‎ ⇒ ‎ReloadAsync‎
    }

    /// <summary>سال‌ها و ماه‌ها را از روی ‎Months‎ی خام می‌سازد.</summary>
    private void BuildPickers()
    {
        _pickerWriting = true;
        try
        {
            Years.Clear();
            foreach (var y in Months.Select(YearOf).Where(y => y.Length > 0)
                                    .Distinct().OrderByDescending(y => y))
                Years.Add(y);

            var cur = YearOf(Month);
            if (cur.Length == 0 || !Years.Contains(cur)) cur = Years.FirstOrDefault() ?? "";
            _year = cur;
            OnPropertyChanged(nameof(Year));
        }
        finally { _pickerWriting = false; }

        BuildMonthOptions(Year, preferred: Month);
    }

    private void BuildMonthOptions(string year, string? preferred)
    {
        _pickerWriting = true;
        MonthOption? pick = null;
        try
        {
            MonthOptions.Clear();
            foreach (var k in Months.Where(m => YearOf(m) == year).OrderByDescending(m => m))
            {
                var o = new MonthOption(k, Shamsi.MonthLabel(k));
                MonthOptions.Add(o);
                if (preferred is not null && k == preferred) pick = o;
            }
            pick ??= MonthOptions.FirstOrDefault();
            _selectedMonth = pick;
            OnPropertyChanged(nameof(SelectedMonth));
        }
        finally { _pickerWriting = false; }

        // سالِ دیگری برگزیده شد ⇒ ماهِ همان سال باید واقعاً بار شود
        if (pick is not null && pick.Key != Month) Month = pick.Key;
    }

    /// <summary>«1405/06» ⇒ «1405». کلیدِ خراب ⇒ رشتهٔ خالی.</summary>
    private static string YearOf(string? key)
    {
        var k = Shamsi.ToEnDigits(key);
        var i = k.IndexOf('/');
        return i == 4 ? k[..4] : "";
    }

    /// <summary>
    /// صفحهٔ ورق — **یکی**، و پس از نخستین باز شدن دیگر دور انداخته نمی‌شود.
    /// چرایی‌اش در ‎WaraqPageViewModel.Load‎ نوشته شده: با ‎null‎ شدنِ این،
    /// کلِ درختِ بصریِ صفحه (دو جدولِ تراکنش) هر بار از نو ساخته می‌شد.
    /// </summary>
    [ObservableProperty] private WaraqPageViewModel? _page;

    /// <summary>ورقی باز است؟ جانشینِ «‎Page is null‎»ی قدیم.</summary>
    [ObservableProperty] private bool _sheetOpen;

    public bool IsListVisible => !SheetOpen;

    /// <summary>ورقِ باز — تا باز است، میانبرهای ردیف به آن می‌روند نه به فهرست.</summary>
    public override object? ActivePage => SheetOpen ? Page : null;

    partial void OnSheetOpenChanged(bool v)
    {
        OnPropertyChanged(nameof(IsListVisible));
        // صفحهٔ حساب تمام‌عرض است، مثلِ مودالِ تمام‌صفحهٔ نسخهٔ وب
        IsPageOpen = v;
    }

    /// <summary>همان ورق در همان صفحه — یا نخستین‌بار، ساختنش.</summary>
    private void Show(WaraqEntry full)
    {
        if (Page is null) Page = new WaraqPageViewModel(_host, full, this);
        else Page.Load(full);
        SheetOpen = true;
    }
    partial void OnMonthChanged(string v) => _ = ReloadAsync();

    protected override async Task LoadAsync()
    {
        Months.Clear();
        foreach (var m in await _host.WaraqData.MonthsAsync()) Months.Add(m);
        if (!Months.Contains(Month)) Months.Insert(0, Month);
        BuildPickers();
        _seenVersion = PumpYaqobi.Persistence.PumpDbContext.Version;
        await ReloadAsync();
    }

    /// <summary>
    /// ══ «شیفت را ثبت کردم، ولی در ورق‌ها ورقی ساخته نشد» ═══════════════════
    ///
    /// گزارشِ صاحب ریپو (۱۴۰۵/۰۶/۲۷). ورق **ساخته می‌شد** — سنجشِ ۳ب در
    /// ‎parcha‎ نشان داد که ‎ShiftWaraqSync‎ کارش را درست می‌کند — ولی این
    /// بخش فهرستش را فقط **یک بار** می‌خواند (‎EnsureLoadedAsync‎ با
    /// ‎IsLoaded‎). پس هر کس یک بار به ورق‌ها سر زده بود، تا بسته شدنِ برنامه
    /// همان فهرستِ کهنه را می‌دید و ورقِ تازه غیب بود.
    ///
    /// ⚠️ و بی‌قید تازه نمی‌شود: ‎PumpDbContext.Version‎ ترمزِ همیشگیِ این
    /// برنامه است (قاعدهٔ «کارِ دوره‌ای بی ترمزِ Version ممنوع»). داده عوض
    /// نشده ⇒ هیچ پرس‌وجویی. ماه‌ها هم از نو خوانده می‌شوند، وگرنه ورقی که
    /// در ماهِ تازه‌ای نشسته حتی در کشویی هم پیدا نمی‌شد.
    /// </summary>
    public override Task OnActivatedAsync()
    {
        if (_seenVersion == PumpYaqobi.Persistence.PumpDbContext.Version) return Task.CompletedTask;
        return LoadAsync();
    }

    private long _seenVersion = -1;

    public async Task ReloadAsync()
    {
        Sheets.Clear();
        Cards.Clear();
        foreach (var w in await _host.WaraqData.ListAsync(Month))
        {
            Sheets.Add(w);
            Cards.Add(new WaraqCardViewModel(w, _host.Waraq));
        }
        OnPropertyChanged(nameof(IsEmpty));
    }

    /// <summary>
    /// ══ صفحهٔ ورق هم پشتِ پردهٔ لودینگ ساخته شود ═══════════════════════════
    ///
    /// ⚠️ گرم کردنِ خودِ بخش کافی نبود و سنجش همین را گفت: فهرستِ ورق‌ها گرم
    /// می‌شد ولی صفحهٔ یک ورق تا نخستین کلیک اصلاً ساخته نمی‌شد — ۲٬۱۹۳
    /// میلی‌ثانیه برای بارِ اول، در برابرِ ۱۱۵ برای دفعه‌های بعد.
    ///
    /// با ورقِ **واقعیِ** فهرست گرم می‌شود، نه یک ورقِ ساختگی: جدول‌ها باید
    /// با ردیفِ واقعی چیده شوند تا قالبِ ردیف و خانه هم پیاده شود.
    /// </summary>
    /// <summary>
    /// چند ردیفِ ساختگی برای گرم کردن — به اندازهٔ یک ورقِ معمولی، نه بیشتر.
    /// </summary>
    private const int WarmRows = 60;
    private const int WarmPumps = 6;

    /// <summary>
    /// ══ صفحهٔ ورق، پشتِ پرده و با دادهٔ ساختگی ═══════════════════════════
    ///
    /// چراییِ کامل در <see cref="SectionViewModel.WarmInner"/>. دو نکته که با
    /// عدد به آن‌ها رسیدیم و اگر ننویسم دوباره تکرار می‌شود:
    ///
    ///   ۱) ورقِ **خالی** هیچ فرقی نکرد (۲٬۱۰۶ ⇐ ۲٬۱۰۶ میلی‌ثانیه): آن‌چه
    ///      گران است ساختنِ **ردیف و خانه** است، نه خواندنِ XAMLِ صفحه.
    ///   ۲) و یک نمونهٔ **جدا** هم کافی نبود (۱٬۷۱۰): صفحه‌ای که کاربر بعداً
    ///      می‌بیند همانی است که داخلِ نمای بخش نشسته، و تا ‎SheetOpen‎ روشن
    ///      نشود آوالونیا اصلاً اندازه‌اش نمی‌گیرد.
    ///
    /// پس همان صفحهٔ واقعی باز می‌شود — ولی روی یک ورقِ ساختگیِ درجا، پشتِ
    /// پردهٔ لودینگ، و بی آن‌که ‎Current‎ یا مسیرِ کاربر تکان بخورد. هیچ
    /// خواندنی از دیتابیس در کار نیست، پس پیش از رمز هم هیچ دادهٔ
    /// محافظت‌شده‌ای ساخته نمی‌شود.
    ///
    /// ⚠️ و ‎Page‎ همان‌جا می‌ماند: باز کردنِ ورقِ واقعی بعداً ‎Page.Load(full)‎
    /// می‌زند و همین درختِ گرم را دوباره به کار می‌گیرد.
    /// </summary>
    public override void WarmInner(Action layout)
    {
        var blank = new WaraqEntry { DateShamsi = "" };
        var shift = new WaraqShift { Kind = ShiftKind.Day };
        blank.Shifts.Add(shift);

        for (var i = 1; i <= WarmPumps; i++)
            shift.Pumps.Add(new WaraqPump { Num = i });

        for (var i = 1; i <= WarmRows; i++)
            shift.Transactions.Add(new WaraqTransaction { SortIndex = i, Name = "" });

        Page = new WaraqPageViewModel(_host, blank, this);
        SheetOpen = true;
        layout();
        SheetOpen = false;
    }

    public override async Task WarmInnerAsync(Func<Task> layout)
    {
        var first = Sheets.FirstOrDefault();
        if (first is null) return;

        var full = await _host.WaraqData.LoadAsync(first.Id);
        if (full is null) return;

        Show(full);
        await layout();
        SheetOpen = false;                 // ‎Page‎ می‌ماند؛ فقط از دید می‌رود
    }

    [RelayCommand]
    private Task OpenCardAsync(WaraqCardViewModel? c) => OpenAsync(c?.Entity);

    [RelayCommand]
    private Task DeleteCardAsync(WaraqCardViewModel? c) => DeleteSheetAsync(c?.Entity);

    /// <summary>
    /// ‎printWaraqById(id)‎ — ورقِ یک کارت، بی باز کردنِ صفحه‌اش.
    ///
    /// ⚠️ کارتِ فهرست فقط جمع‌ها را دارد؛ پایه‌ها و ردیف‌های قرض/مصرف با یک
    /// خواندنِ کامل می‌آیند، وگرنه ورق خالی چاپ می‌شود.
    /// </summary>
    [RelayCommand]
    private async Task PdfCardAsync(WaraqCardViewModel? c)
    {
        if (c is null) return;
        var full = await _host.WaraqData.LoadAsync(c.Entity.Id);
        if (full is null) return;

        var kind = full.ActiveShift;
        var sd = full.Shifts.FirstOrDefault(s => s.Kind == kind);
        if (sd is null) return;

        var input = new WaraqReportInput(full.Station ?? "", full.DateShamsi ?? "",
                                         kind, sd, DocDates.Line());
        await Documents.ShowAsync(() => new WaraqReport(input, _host.Waraq),
                                  (kind == ShiftKind.Night ? "ورق شب " : "ورق روز ")
                                  + (full.DateShamsi ?? ""));
    }

    [RelayCommand]
    private async Task OpenAsync(WaraqEntry? w)
    {
        if (w is null) return;
        var full = await _host.WaraqData.LoadAsync(w.Id);
        if (full is null) return;
        Show(full);
    }

    [RelayCommand]
    private async Task NewSheetAsync()
    {
        // ══ ورق با تاریخِ دلخواه — همان کادرِ سایت ═══════════════════════════
        //
        // تا امروز این دکمه همیشه ورقِ **امروز** را باز می‌کرد و راهی برای
        // «ورقِ فردا» نبود. خواستهٔ صریحِ صاحب ریپو: «تا ورق ساخته بشه و
        // بتونم روز رو خودم انتخاب کنم».
        //
        // ⚠️ ورقِ تکراری ساخته نمی‌شود: ‎OpenOrCreateAsync‎ با **کلیدِ تاریخ**
        // می‌گردد، و همان کلید است که همگام‌سازیِ پارچه هم با آن ورق را پیدا
        // می‌کند (‎ShiftWaraqSyncService‎). پس ورقی که برای فردا ساخته شود،
        // فردا که پارچه پر شود در همان می‌نشیند — نه در یک ورقِ تازه.
        // ⚠️ همهٔ ورق‌ها، نه فقط ماهِ جلوی چشم — وگرنه تاریخی از ماهِ دیگر
        // «تازه» معرفی می‌شد در حالی که ورقش هست.
        var keys = (await _host.WaraqData.ListAsync(null))
                   .Select(x => Shamsi.Key(x.DateShamsi)).Where(k => k > 0).ToHashSet();
        var pick = await Dialogs.PickWaraqDateAsync(keys);
        if (string.IsNullOrWhiteSpace(pick)) return;

        var known = keys.Contains(Shamsi.Key(pick));
        var station = _host.Settings.GetString(Services.SettingsKeys.StationName);
        var w = await _host.WaraqData.OpenOrCreateAsync(pick, station);
        _host.Toast(known ? "📝 ورقِ " + pick + " از قبل وجود داشت — همان ورق باز شد"
                          : "📝 ورقِ " + pick + " ساخته شد",
                    known ? ToastKind.Warn : ToastKind.Ok);
        var mk = Shamsi.MonthKey(w.DateShamsi);
        if (!Months.Contains(mk)) Months.Insert(0, mk);
        Month = mk;
        await ReloadAsync();
        var full = await _host.WaraqData.LoadAsync(w.Id);
        if (full is not null) Show(full);
    }

    [RelayCommand]
    private async Task BackAsync()
    {
        // ══ اول فهرست بیاید، بعد کارِ دیتابیس ══════════════════════════════
        //
        // خواستهٔ صاحب ریپو: «بازگشت به صفحهٔ اصلی نباید تأخیر داشته باشد.»
        //
        // ⚠️ و سنجش گفت تأخیر از کجاست: ‎FlushAsync‎ی ۶۰ ردیف و بعد
        // همگام‌سازیِ ورق با حسابِ قرض‌داران (‎WaraqPosting.SyncAsync‎) روی هم
        // ~۷۰۰ میلی‌ثانیه بود و **پیش از** عوض شدنِ صفحه انجام می‌شد. با
        // خاموش کردنِ همگام‌سازی، بازگشت ۳۰۰ می‌شد.
        //
        // هیچ‌کدام‌شان لازم نیست جلوی چشمِ کاربر را بگیرند: هر ردیف همان
        // لحظهٔ ویرایش ذخیره شده و همگام‌سازی هم با هر تغییرِ بعدی دوباره
        // اجرا می‌شود. پس اول پرده عوض می‌شود و کارِ دیتابیس پشتِ سرش
        // می‌آید — ولی همچنان ‎await‎ می‌شود تا آزمون‌ها و بستنِ برنامه
        // بدانند کِی تمام شده.
        //
        // ⚠️ ‎Page‎ عمداً ‎null‎ نمی‌شود — با ‎null‎ شدنش درختِ بصریِ صفحه دور
        // انداخته می‌شد و باز کردنِ بعدی باید همه را از نو می‌ساخت.
        SheetOpen = false;

        // ⚠️ ‎Task.Yield()‎ این‌جا کافی نیست: ادامهٔ کار با اولویتِ ‎Normal‎
        // برمی‌گردد و چیدمان اولویتِ پایین‌تری دارد، پس باز هم کارِ دیتابیس
        // **پیش از** عوض شدنِ پرده انجام می‌شد. ‎Background‎ زیرِ چیدمان است.
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
        if (Page is not null) await Page.FlushAsync();
        await ReloadAsync();
    }

    [RelayCommand]
    private async Task DeleteSheetAsync(WaraqEntry? w)
    {
        if (w is null) return;
        await _host.WaraqData.DeleteAsync(w.Id);
        await ReloadAsync();
    }
}
