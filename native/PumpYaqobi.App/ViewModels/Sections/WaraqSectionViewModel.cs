using System.Collections.ObjectModel;
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

    partial void OnWorkerChanged(string v) => Touch();
    partial void OnPumpDateChanged(string v) => Touch();
    partial void OnNumChanged(int v) => Touch();
    partial void OnFuelChanged(FuelType v) { Touch(); OnPropertyChanged(nameof(FuelText)); }
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

    protected override void Apply()
    {
        _p.Num = Num; _p.Fuel = Fuel; _p.Start = Start; _p.End = End;
        _p.PricePerLiter = Price; _p.Debt = Debt; _p.Note = Note;
        _p.Worker = Worker; _p.DateShamsi = PumpDate;
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

    partial void OnNameChanged(string v) => Touch();
    partial void OnLitersChanged(decimal v) { _t.AmountAuto ??= true; Touch(); Refresh(); }
    partial void OnIsExpenseChanged(bool v) { Touch(); OnPropertyChanged(nameof(TypeText)); }
    partial void OnFuelChanged(FuelType v) { Touch(); OnPropertyChanged(nameof(FuelText)); }

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

    partial void OnIsMoneyChanged(bool v) { Touch(); OnPropertyChanged(nameof(UnitText)); }

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

    public WaraqPageViewModel(AppHost host, WaraqEntry w, WaraqSectionViewModel section)
    {
        _host = host; _section = section; Entity = w;
        _isNight = w.ActiveShift == ShiftKind.Night;
        Build();
    }

    public WaraqEntry Entity { get; }
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

    partial void OnIsNightChanged(bool v) => Build();

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

        SalesText = Money(d.Sales + n.Sales) + " افغانی";
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

    public WaraqSectionViewModel(AppHost host) : base("waraq", "waraq", "ورق‌های روزانه")
    {
        _host = host;
        _month = Shamsi.ThisMonth();
    }

    public ObservableCollection<WaraqEntry> Sheets { get; } = new();
    /// <summary>کارت‌های همان ورق‌ها، با جمعِ هر دو شیفت.</summary>
    public ObservableCollection<WaraqCardViewModel> Cards { get; } = new();
    public bool IsEmpty => Cards.Count == 0;
    public ObservableCollection<string> Months { get; } = new();

    [ObservableProperty] private string _month;
    [ObservableProperty] private WaraqPageViewModel? _page;

    public bool IsListVisible => Page is null;

    /// <summary>ورقِ باز — تا باز است، میانبرهای ردیف به آن می‌روند نه به فهرست.</summary>
    public override object? ActivePage => Page;

    partial void OnPageChanged(WaraqPageViewModel? v)
    {
        OnPropertyChanged(nameof(IsListVisible));
        // صفحهٔ حساب تمام‌عرض است، مثلِ مودالِ تمام‌صفحهٔ نسخهٔ وب
        IsPageOpen = v is not null;
    }
    partial void OnMonthChanged(string v) => _ = ReloadAsync();

    protected override async Task LoadAsync()
    {
        Months.Clear();
        foreach (var m in await _host.WaraqData.MonthsAsync()) Months.Add(m);
        if (!Months.Contains(Month)) Months.Insert(0, Month);
        await ReloadAsync();
    }

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
        Page = new WaraqPageViewModel(_host, full, this);
    }

    [RelayCommand]
    private async Task NewSheetAsync()
    {
        var station = _host.Settings.GetString(Services.SettingsKeys.StationName);
        var w = await _host.WaraqData.OpenOrCreateAsync(Shamsi.Today(), station);
        var mk = Shamsi.MonthKey(w.DateShamsi);
        if (!Months.Contains(mk)) Months.Insert(0, mk);
        Month = mk;
        await ReloadAsync();
        var full = await _host.WaraqData.LoadAsync(w.Id);
        if (full is not null) Page = new WaraqPageViewModel(_host, full, this);
    }

    [RelayCommand]
    private async Task BackAsync()
    {
        if (Page is not null) await Page.FlushAsync();
        Page = null;
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
