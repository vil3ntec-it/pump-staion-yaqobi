using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using PumpYaqobi.App.Controls;
using CommunityToolkit.Mvvm.Input;
using PumpYaqobi.App.Printing;
using PumpYaqobi.App.Services;
using PumpYaqobi.Application.Localization;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;
using PumpYaqobi.Reporting.Pdf;
using PumpYaqobi.Services.Data;

namespace PumpYaqobi.App.ViewModels.Sections;

/// <summary>یک رسیدِ ثبت‌شده در فهرستِ ماه.</summary>
public sealed class DebtReceiptRowViewModel
{
    public DebtReceiptRowViewModel(DebtQuickReceipt e, int index)
    {
        Entity = e;
        Index = index;
        DateShamsi = e.DateShamsi ?? "";
        Account = e.Account ?? "";
        Note = e.Note ?? "";
        AmountText = Shamsi.Money(e.Amount);
        UnitText = DebtQuickReceiptService.IsRetail(e)
            ? "🛒 چکنه · 💵 پول"
            : DebtReceiptSectionViewModel.UnitLabel(e.Unit, e.Fuel);
        UnitBrushKey = e.Unit == LedgerMode.Fuel
            ? (e.Fuel == FuelType.Diesel ? "Pump.Warn" : "Pump.Ok")
            : "Pump.Info";
    }

    public DebtQuickReceipt Entity { get; }
    public int Index { get; }
    public string DateShamsi { get; }
    public string Account { get; }
    public string Note { get; }
    public string AmountText { get; }

    /// <summary>«💵 پول» یا «⛽ تیل — دیزل» — خواستهٔ صریحِ صاحب ریپو.</summary>
    public string UnitText { get; }
    public string UnitBrushKey { get; }
}

/// <summary>
/// ══ بخشِ «رسید قرض‌داران» ═══════════════════════════════════════════════════
/// پرداختِ مستقیم به حسابِ یک قرض‌دار.
///
/// ⚠️ **هیچ دکمهٔ «ثبت» ندارد و نباید داشته باشد.** در نسخهٔ وب به‌محضِ کامل
/// شدنِ نام و مبلغ، ردیف خودش در حسابِ طرف می‌نشیند. این‌جا هم همان: با
/// بیرون رفتنِ فوکوس از کادرِ مبلغ (یا زدنِ Enter) ثبت می‌شود، کادرها خالی
/// می‌شوند و فوکوس به «نام» برمی‌گردد تا رسیدِ بعدی نوشته شود.
///
/// ══ واحدِ رسید ═════════════════════════════════════════════════════════════
/// گزارشِ صاحب ریپو: «مشکلِ اصلی واحد ندارد که رسید پول است یا تیلِ دیزل است
/// یا پطرول.» حق داشت: هر رسیدی مبلغِ افغانی شمرده می‌شد، پس تیلی که مشتری
/// پس داده بود در دفتر به شکلِ **پول** می‌نشست.
///
/// ⛔ پیش‌فرض همان «پول» است و مسیرِ پول **مو‌به‌مو** همان چیزی که بود —
/// رسیدهای ثبت‌شدهٔ مشتری‌های امروزی یک میلی‌متر هم جابه‌جا نمی‌شوند
/// (ستونِ دیتابیس هم پیش‌فرضِ «پول» دارد).
/// </summary>
public sealed partial class DebtReceiptSectionViewModel : SectionViewModel
{
    private readonly AppHost _host;

    public DebtReceiptSectionViewModel(AppHost host) : base("debtrasid", "debt", "رسید قرض‌داران / چکنه")
    {
        // «📝 یادداشت این بخش» — همتای ‎.sec-note-box‎ی سایت. کلیدش همان
        // کلیدِ نسخهٔ وب است تا نوت‌های واردشده سرِ جای خودشان بنشینند.
        Notes = new SectionNotesViewModel(Id, host.SectionNotes,
            (m, ok) => host.Toast(m, ok ? ToastKind.Ok : ToastKind.Warn));
        _host = host;
        _dateShamsi = Shamsi.Today();
        // درِ «🕘 تاریخچه»ی همین بخش — شرحش بالای ‎SectionViewModel.HistoryKind‎
        HistoryKind = "rasid";
        Picker = new YearMonthPicker(k => { if (k.Length > 0 && k != Month) Month = k; });
    }

    /// <summary>کشوی سال و ماه (۱۴۰۵/۰۷/۱۴). ⛔ ماه همان <see cref="Month"/> است؛ این فقط نما است.</summary>
    public YearMonthPicker Picker { get; }

    /// <summary>⚠️ ‎BulkRows‎: پر شدنِ جدول یک خبر می‌دهد نه ‎n‎ خبر
    /// — وگرنه جدول به ازای هر ردیف یک‌بار از نو چیده می‌شود و بخش می‌ایستد.</summary>
    public BulkRows<DebtReceiptRowViewModel> Rows { get; } = new();
    public ObservableCollection<string> Months { get; } = new();

    // ── کادرهای ورودِ سریع ─────────────────────────────────────────────────
    [ObservableProperty] private string _typedName = "";
    [ObservableProperty] private string _amountText = "";
    [ObservableProperty] private string _note = "";
    [ObservableProperty] private string _dateShamsi;

    /// <summary>واحدِ رسیدِ بعدی — پول یا تیل. پیش‌فرض «پول».</summary>
    [ObservableProperty] private LedgerMode _unit = LedgerMode.Money;
    /// <summary>وقتی واحد «تیل» است، کدام تیل.</summary>
    [ObservableProperty] private FuelType _fuel = FuelType.Petrol;

    // ══ حسابِ کجا: قرض‌دار یا چکنه (۱۴۰۵/۰۷/۱۴) ═══════════════════════════
    //
    //  خواستهٔ صاحب ریپو: «یک کادرِ کشوییِ دیگه هم بغلِ اون دوتا بزار به
    //  اسمِ چکنه یا قرض‌دار؛ اگه چکنه بود یارو رو با همون اسم درجا اتومات
    //  پیدا کنه و رسید بره توی کادرِ اون و الباقی معلوم بشه؛ اگه قرض‌داران
    //  بودن هم برای قرض‌داران این‌طوری بشه.»
    //
    //  ⛔ رسیدِ چکنه فقط پول است (دفترِ چکنه ستونِ رسیدِ تیل ندارد)، پس با
    //  «چکنه» کشوییِ واحد روی «پول» می‌ایستد و بسته می‌شود — و می‌گوید چرا.

    /// <summary>گزینه‌های کشوییِ حساب — ترتیبشان با <see cref="TargetIndex"/> قفل است.</summary>
    public string[] TargetOptions { get; } = { "👤 قرض‌دار", "🛒 چکنه" };

    [ObservableProperty] private bool _isRetail;

    public int TargetIndex
    {
        get => IsRetail ? 1 : 0;
        set { if (value >= 0) IsRetail = value == 1; }
    }

    partial void OnIsRetailChanged(bool v)
    {
        if (v) Unit = LedgerMode.Money;
        OnPropertyChanged(nameof(TargetIndex));
        OnPropertyChanged(nameof(UnitOpen));
        OnPropertyChanged(nameof(NameLabel));
        OnPropertyChanged(nameof(NameSuggestKey));
        OnPropertyChanged(nameof(UnitHint));
        RefreshUnit();
    }

    /// <summary>کشوییِ واحد باز است؟ با «چکنه» نه — فقط پول.</summary>
    public bool UnitOpen => !IsRetail;
    public string UnitHint => IsRetail ? "چکنه فقط رسیدِ پول دارد" : "پول یا تیل";
    public string NameLabel => IsRetail ? "نامِ حسابِ چکنه" : "نام قرض‌دار";
    /// <summary>تکمیلِ خودکارِ کادرِ نام از نام‌های همان دفتر.</summary>
    public string NameSuggestKey => IsRetail ? "chakana" : "debtor";

    /// <summary>
    /// «الباقی معلوم بشه» — پس از هر رسید: حساب و الباقیِ همین حالای او.
    /// ⛔ هیچ عددی این‌جا ساخته نمی‌شود؛ از همان محاسبه‌های کارتِ قرض‌دار و جملهٔ چکنه.
    /// </summary>
    [ObservableProperty] private string _lastResult = "";

    partial void OnUnitChanged(LedgerMode v) => RefreshUnit();
    partial void OnFuelChanged(FuelType v) => RefreshUnit();

    private void RefreshUnit()
    {
        OnPropertyChanged(nameof(UnitChipText));
        OnPropertyChanged(nameof(UnitChipBrushKey));
        OnPropertyChanged(nameof(AmountLabel));
        OnPropertyChanged(nameof(AmountWatermark));
        OnPropertyChanged(nameof(UnitIndex));
        OnPropertyChanged(nameof(FuelIndex));
        OnPropertyChanged(nameof(IsFuel));
        OnPropertyChanged(nameof(FuelHint));
    }

    // ══ دو کشویی، نه یک دکمهٔ چرخشی ═══════════════════════════════════════
    //
    //  گزارشِ صاحب ریپو با عکس (۱۴۰۵/۰۷/۱۰): «چرا اون واحدِ رسید دکمه‌ای
    //  است؟ اون و اپشن‌هاش رو کشویی کن که هر کدوم رو خواستم انتخاب کنم:
    //  واحدِ تیل، واحدِ پول، و پطرول یا دیزل. الان شاید واحد تیل باشه و
    //  رسیدش دیزل… الان که کاری هم نمیشه کرد.»
    //
    //  ⛔ **و حق داشت، این فقط سلیقه نبود.** دکمهٔ چرخشی سه حال را پشتِ سرِ
    //  هم می‌چرخاند (پول ⇄ تیلِ پطرول ⇄ تیلِ دیزل)، پس رسیدنِ به «تیلِ
    //  دیزل» از «پول» دو کلیک لازم داشت و **هیچ راهی نبود که بشود دید چه
    //  گزینه‌هایی هست** — کاربر باید حدس می‌زد. با کشویی، هر دو تصمیم
    //  مستقل و دیدنی‌اند.
    //
    //  ⛔ **و دو تصمیم‌اند، نه یکی.** «واحد» (پول/تیل) و «کدام تیل» دو
    //  چیزند و در دفتر هم دو ستونِ جدا (`Unit` و `Fuel`)؛ یکی کردنشان در
    //  یک فهرستِ سه‌تایی همان چیزی بود که این گزارش را ساخت.
    //
    //  ⚠️ و هیچ منطقی عوض نشد: `Unit` و `Fuel` همان دو خاصیتِ قبلی‌اند و
    //  `AddAsync` همان‌ها را می‌گیرد. این‌جا فقط دو نما به همان دو نشسته.

    /// <summary>گزینه‌های کشوییِ واحد — ترتیبشان با <see cref="UnitIndex"/> قفل است.</summary>
    public string[] UnitOptions { get; } = { "💵 پول", "⛽ تیل" };

    /// <summary>گزینه‌های کشوییِ نوعِ تیل — ترتیبشان با <see cref="FuelIndex"/> قفل است.</summary>
    public string[] FuelOptions { get; } = { "پطرول", "دیزل" };

    public int UnitIndex
    {
        get => Unit == LedgerMode.Fuel ? 1 : 0;
        set { if (value >= 0) Unit = value == 1 ? LedgerMode.Fuel : LedgerMode.Money; }
    }

    public int FuelIndex
    {
        get => Fuel == FuelType.Diesel ? 1 : 0;
        set { if (value >= 0) Fuel = value == 1 ? FuelType.Diesel : FuelType.Petrol; }
    }

    /// <summary>
    /// ⛔ «رسیدِ پول هیچ‌کدوم لازم نیست، نه پطرول نه دیزل» — خواستهٔ صریحِ
    /// صاحب ریپو. پس کشوییِ تیل با واحدِ «پول» **بسته** می‌شود.
    ///
    /// ⚠️ بسته می‌شود، پنهان نه: پنهان شدنش ردیف را جابه‌جا می‌کند و کادرِ
    /// بعدی از زیرِ دستِ کاربر فرار می‌کند — همان درسی که کادرِ هشدارِ
    /// پارچه داد.
    /// </summary>
    public bool IsFuel => Unit == LedgerMode.Fuel && !IsRetail;

    /// <summary>و چرا بسته است، نوشته می‌شود — قفلِ بی‌توضیح باگ است.</summary>
    public string FuelHint => IsRetail ? "برای چکنه لازم نیست"
        : IsFuel ? "پطرول یا دیزل" : "برای رسیدِ پول لازم نیست";

    /// <summary>برچسبِ واحد — یک جا، پس ردیفِ جدول و کپسولِ فرم هیچ‌وقت دو چیز نمی‌گویند.</summary>
    public static string UnitLabel(LedgerMode unit, FuelType fuel) =>
        unit == LedgerMode.Fuel ? "⛽ تیل — " + fuel.ToPersian() : "💵 پول";

    public string UnitChipText => UnitLabel(Unit, Fuel);

    public string UnitChipBrushKey => Unit == LedgerMode.Fuel
        ? (Fuel == FuelType.Diesel ? "Pump.Warn" : "Pump.Ok")
        : "Pump.Info";

    /// <summary>⚠️ برچسبِ کادرِ مبلغ با واحد عوض می‌شود — «۲۰۰» لیتر است یا افغانی؟</summary>
    public string AmountLabel => Unit == LedgerMode.Fuel ? "مقدار رسید (لیتر)" : "مبلغ رسید (افغانی)";
    public string AmountWatermark => Unit == LedgerMode.Fuel ? "لیتر…" : "افغانی…";


    [ObservableProperty] private string _month = "";
    [ObservableProperty] private string _totalText = "0";

    /// <summary>ردیفِ «جمله»ی ته جدول — جمعِ رسیدهای همین ماه.</summary>
    public IReadOnlyList<TotalCell> TotalCells => new[]
    {
        new TotalCell("شمارِ رسیدها", Shamsi.Money(Rows.Count), column: TotalCell.NoColumn),
        new TotalCell("مبلغِ رسید", TotalText, "Pump.Ok", "مبلغ رسید"),
        new TotalCell("رسیدِ تیل", FuelTotalText, "Pump.Warn", "واحد"),
    };

    /// <summary>جمعِ لیترِ رسیدهای تیلِ همین ماه — جدا از پول، چون دو چیزند.</summary>
    [ObservableProperty] private string _fuelTotalText = "0";

    partial void OnTotalTextChanged(string v) => OnPropertyChanged(nameof(TotalCells));
    partial void OnFuelTotalTextChanged(string v) => OnPropertyChanged(nameof(TotalCells));

    /// <summary>فوکوس باید به کادرِ «نام» برگردد — صفحه به آن گوش می‌دهد.</summary>
    public event Action? FocusNameRequested;

    /// <summary>
    /// ⚠️ کادرِ کشوییِ ماه می‌تواند ‎null‎ پس بدهد — وقتی فهرستِ ماه‌ها خالی
    /// باشد، آوالونیا ‎SelectedItem‎ را پاک می‌کند و همان ‎null‎ روی این
    /// خاصیت می‌نشیند. بعدش هر ‎Month.Length‎ی در این کلاس می‌ترکد. یک بار
    /// همین‌طور شد. پس ‎null‎ همان‌جا به «هیچ ماه» ترجمه می‌شود.
    /// </summary>
    partial void OnMonthChanged(string? v)
    {
        if (v is null) { Month = ""; return; }
        _ = Services.CrashGuard.RunAsync("خواندنِ رسیدها", ReloadAsync);
    }

    protected override async Task LoadAsync()
    {
        var months = await _host.DebtReceipts.MonthsAsync();
        Months.Clear();
        foreach (var m in months) Months.Add(m);
        if (Month.Length == 0 || !Months.Contains(Month))
            _month = Months.FirstOrDefault() ?? Shamsi.MonthKey(Shamsi.Today());
        OnPropertyChanged(nameof(Month));
        Picker.Load(Months, Month);
        await ReloadAsync();
    }

    private async Task ReloadAsync()
    {
        var list = await _host.DebtReceipts.ListAsync(Month.Length == 0 ? null : Month);
        using (Rows.Batch())
        {
            Rows.Clear();
            var i = 1;
            foreach (var r in list) Rows.Add(new DebtReceiptRowViewModel(r, i++));
        }
        TotalText = Shamsi.Money(list.Where(r => r.Unit != LedgerMode.Fuel).Sum(r => r.Amount));
        FuelTotalText = Shamsi.Money(list.Where(r => r.Unit == LedgerMode.Fuel).Sum(r => r.Amount));
    }

    /// <summary>
    /// ‎quickAddDebtRasid‎ — همان لحظه‌ای که نام و مبلغ هر دو پر باشند.
    /// از کادرِ مبلغ (خروجِ فوکوس یا Enter) و از کادرِ نام صدا زده می‌شود.
    /// </summary>
    [RelayCommand]
    public async Task SubmitAsync()
    {
        var amount = Shamsi.Num(AmountText);

        if (IsRetail)
        {
            var (r, name, albaqi) = await _host.DebtReceipts.AddRetailAsync(
                TypedName, amount, _host.Retail, DateShamsi, Note);
            if (r == QuickReceiptResult.Incomplete) return;
            if (r == QuickReceiptResult.NotFound)
            {
                _host.Toast("⚠️ حسابِ چکنهٔ «" + TypedName.Trim() +
                            "» پیدا نشد — اول در «حساب‌های چکنه» ردیفی به همین نام بنویسید", ToastKind.Error);
                return;
            }
            LastResult = "🛒 " + name + " — رسیدِ " + Shamsi.Money(amount) + " افغانی ثبت شد · الباقی: "
                         + Shamsi.Money(albaqi) + " افغانی";
            _host.Toast("✅ رسیدِ چکنه در حسابِ " + name + " ثبت شد · الباقی " + Shamsi.Money(albaqi), ToastKind.Ok);
            TypedName = ""; AmountText = ""; Note = "";
            await LoadAsync();
            FocusNameRequested?.Invoke();
            return;
        }

        var (res, person) = await _host.DebtReceipts.AddAsync(TypedName, amount, DateShamsi, Note, Unit, Fuel);

        switch (res)
        {
            case QuickReceiptResult.Incomplete:
                return;                                     // هنوز تمام نشده — بی‌صدا
            case QuickReceiptResult.NotFound:
                _host.Toast("⚠️ حساب «" + TypedName.Trim() +
                            "» پیدا نشد — اول از بخش قرض‌داران اضافه کنید", ToastKind.Error);
                return;
        }

        LastResult = "👤 " + person + " — " + UnitChipText + " " + Shamsi.Money(amount) + " ثبت شد · "
                     + BalanceWords(_host.DebtReceipts.LastPerson);
        _host.Toast("✅ " + UnitChipText + " در حساب " + person + " ثبت شد", ToastKind.Ok);
        TypedName = ""; AmountText = ""; Note = "";
        await LoadAsync();
        FocusNameRequested?.Invoke();
    }

    /// <summary>الباقیِ یک قرض‌دار به زبانِ آدم — همان ‎Balances‎ی کارتِ قرض‌دار.</summary>
    private string BalanceWords(Debtor? person)
    {
        if (person is null) return "";
        var b = _host.Debt.Balances(person.AllAccounts());
        var parts = new List<string> { "الباقیِ پول: " + Shamsi.Money(b.Money) + " افغانی" };
        if (b.Petrol != 0m) parts.Add("پطرول: " + Shamsi.Money(b.Petrol) + " لیتر");
        if (b.Diesel != 0m) parts.Add("دیزل: " + Shamsi.Money(b.Diesel) + " لیتر");
        return string.Join(" · ", parts);
    }

    /// <summary>‎pdfDebtRasid(monthKey)‎ — ورقِ رسیدهای همین ماه.</summary>
    [RelayCommand]
    private Task PdfAsync()
    {
        var rows = Rows.Select(r => r.Entity).ToList();
        var input = new DebtReceiptReportInput(Shamsi.MonthLabel(Month), rows, DocDates.Line());
        return Documents.ShowAsync(() => new DebtReceiptReport(input),
                                   "رسید قرض‌داران " + Shamsi.MonthLabel(Month));
    }

    /// <summary>‎undoDebtRasid‎ — رسید و اثرش روی حساب، هر دو با هم.</summary>
    [RelayCommand]
    private async Task UndoAsync(DebtReceiptRowViewModel? row)
    {
        if (row is null) return;
        if (!await Dialogs.ConfirmAsync("برگرداندنِ رسید", "این رسید از حساب برداشته شود؟")) return;
        await _host.DebtReceipts.UndoAsync(row.Entity.Id);
        _host.Toast("↩️ رسید برداشته شد", ToastKind.Warn);
        await LoadAsync();
    }
}
