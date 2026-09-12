using CommunityToolkit.Mvvm.ComponentModel;
using PumpYaqobi.App.Controls;
using CommunityToolkit.Mvvm.Input;
using PumpYaqobi.App.Printing;
using PumpYaqobi.App.Services;
using PumpYaqobi.Application.Localization;
using PumpYaqobi.Application.Services;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Reporting.Pdf;

namespace PumpYaqobi.App.ViewModels.Sections;

/// <summary>یک ردیفِ گاوصندوق روی جدول.</summary>
public sealed partial class SafeRowViewModel : RowViewModel
{
    private readonly SafeEntry _e;
    private readonly SafeSectionViewModel _owner;

    public SafeRowViewModel(SafeEntry e, SafeSectionViewModel owner)
    {
        _e = e; _owner = owner;
        Loading = true;
        _dateShamsi = e.DateShamsi ?? "";
        _isBardagi = e.Kind == SafeEntryKind.Bardagi;
        _title = e.Title ?? "";
        _amount = e.Amount;
        _isUsd = e.Currency == Currency.Usd;
        _note = e.Note ?? "";
        Loading = false;
    }

    public SafeEntry Entity => _e;

    [ObservableProperty] private string _dateShamsi = "";
    [ObservableProperty] private bool _isBardagi;
    [ObservableProperty] private string _title = "";
    [ObservableProperty] private decimal _amount;
    [ObservableProperty] private bool _isUsd;
    [ObservableProperty] private string _note = "";

    partial void OnDateShamsiChanged(string v) => Touch();
    partial void OnIsBardagiChanged(bool v) { Touch(); OnPropertyChanged(nameof(KindText)); }
    partial void OnTitleChanged(string v) => Touch();
    partial void OnAmountChanged(decimal v) { Touch(); OnPropertyChanged(nameof(AmountText)); }
    partial void OnIsUsdChanged(bool v) { Touch(); OnPropertyChanged(nameof(CurrencyText)); }
    partial void OnNoteChanged(string v) => Touch();

    /// <summary>مبلغ برای نمایش و تایپ: با جداکنندهٔ هزارگان دیده می‌شود و
    /// هنگامِ تایپ، هم رقمِ فارسی می‌پذیرد هم لاتین هم کاما.</summary>
    public string AmountText
    {
        get => Shamsi.Money(Amount);
        set => Amount = Shamsi.Num(value);
    }

    /// <summary>گزینه‌های کشویی — رشته، نه ‎ComboBoxItem‎ (باگِ ‎SelectedItem‎).</summary>
    public static string[] KindOptions { get; } = { "بردگی", "ماندگی" };
    public static string[] CurrencyOptions { get; } = { "افغانی", "دالر" };

    public string KindText
    {
        get => IsBardagi ? "بردگی" : "ماندگی";
        set => IsBardagi = value == "بردگی";
    }

    public string CurrencyText
    {
        get => IsUsd ? "دالر" : "افغانی";
        set => IsUsd = value == "دالر";
    }

    protected override void Apply()
    {
        _e.DateShamsi = DateShamsi;
        _e.Kind = IsBardagi ? SafeEntryKind.Bardagi : SafeEntryKind.Mandagi;
        _e.Title = Title;
        _e.Amount = Amount;
        _e.Currency = IsUsd ? Currency.Usd : Currency.Afn;
        _e.Note = Note;
    }

    protected override Task SaveAsync() => _owner.SaveEntityAsync(_e);
}

/// <summary>
/// ══ بخشِ گاوصندوق ══════════════════════════════════════════════════════════
/// همان صفحهٔ <c>renderSafe</c>ِ نسخهٔ وب: بردگی/ماندگیِ یک ماه و سه کادرِ جمع.
/// دو ارز هرگز با هم جمع نمی‌شوند — قاعدهٔ ثابتِ برنامه.
/// </summary>
public sealed partial class SafeSectionViewModel : LedgerSectionViewModel<SafeRowViewModel, SafeEntry>
{
    private readonly SafeService _calc;

    public SafeSectionViewModel(AppHost host)
        : base("safe", "safe", "گاوصندوق", host.SafeLedger)
    {
        _calc = host.Safe;
        // «📝 یادداشت این بخش» — همتای ‎.sec-note-box‎ی سایت. کلیدش همان
        // کلیدِ نسخهٔ وب است تا نوت‌های واردشده سرِ جای خودشان بنشینند.
        Notes = new SectionNotesViewModel(Id, host.SectionNotes,
            (m, ok) => host.Toast(m, ok ? ToastKind.Ok : ToastKind.Warn));
    }

    [ObservableProperty] private SafeSummary _summary;

    public string BardagiAfn => Shamsi.Money(Summary.Bardagi.Afn);
    public string BardagiUsd => Shamsi.Money(Summary.Bardagi.Usd);
    public string MandagiAfn => Shamsi.Money(Summary.Mandagi.Afn);
    public string MandagiUsd => Shamsi.Money(Summary.Mandagi.Usd);
    public string NetAfn => Shamsi.Money(Summary.Net.Afn);
    public string NetUsd => Shamsi.Money(Summary.Net.Usd);

    partial void OnSummaryChanged(SafeSummary value)
    {
        OnPropertyChanged(nameof(BardagiAfn)); OnPropertyChanged(nameof(BardagiUsd));
        OnPropertyChanged(nameof(MandagiAfn)); OnPropertyChanged(nameof(MandagiUsd));
        OnPropertyChanged(nameof(NetAfn)); OnPropertyChanged(nameof(NetUsd));
    }

    protected override SafeRowViewModel Wrap(SafeEntry e) => new(e, this);
    protected override long EntityIdOf(SafeRowViewModel r) => r.Entity.Id;
    protected override SafeEntry EntityOf(SafeRowViewModel r) => r.Entity;
    protected override SafeEntry NewEntity() =>
        new() { DateShamsi = Shamsi.Today(), Kind = SafeEntryKind.Mandagi };

    /// <summary>جمع‌ها از همان سرویسِ آزمودهٔ لایهٔ Application می‌آیند.</summary>
    protected override void Recalc() => Summary = _calc.Summarize(Rows.Select(r => r.Entity));

    /// <summary>
    /// ردیفِ «جمله» — ⚠️ افغانی و دالر در دو خانهٔ جدا می‌مانند و هرگز با هم
    /// جمع نمی‌شوند (همان قاعدهٔ سرویس: نرخ وارد حساب نمی‌شود).
    /// </summary>
    protected override IReadOnlyList<TotalCell> BuildTotals() => new[]
    {
        new TotalCell("بردگی (افغانی)", BardagiAfn, "Pump.Warn"),
        new TotalCell("بردگی ($)", BardagiUsd, "Pump.Warn"),
        new TotalCell("ماندگی (افغانی)", MandagiAfn, "Pump.Ok"),
        new TotalCell("ماندگی ($)", MandagiUsd, "Pump.Ok"),
        new TotalCell("خالص (افغانی)", NetAfn, Summary.Net.Afn < 0m ? "Pump.Danger" : "Pump.Ok"),
        new TotalCell("خالص ($)", NetUsd, Summary.Net.Usd < 0m ? "Pump.Danger" : "Pump.Ok"),
    };

    /// <summary>‎printSafe(monthKey)‎ — ورقِ همین ماه، همان‌طور که روی صفحه است.</summary>
    [RelayCommand]
    private Task PdfAsync()
    {
        // ⚠️ عکسِ ردیف‌ها همین‌جا — روی نخِ رابط — گرفته می‌شود، نه داخلِ
        // سازندهٔ سند: ‎Rows‎ یک ‎ObservableCollection‎ است و خواندنش از نخِ
        // پس‌زمینه، وقتی کاربر هم‌زمان ردیفی می‌افزاید، می‌ترکد.
        var rows = Rows.Select(r => r.Entity).ToList();
        var input = new SafeReportInput(Shamsi.MonthLabel(Month), rows, DocDates.Line());
        return Documents.ShowAsync(() => new SafeReport(input, _calc),
                                   "گاوصندوق " + Shamsi.MonthLabel(Month));
    }
}
