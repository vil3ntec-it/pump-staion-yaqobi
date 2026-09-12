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

/// <summary>یک ردیفِ صرافی. «شکسته» و «الباقی» محاسبه‌اند، نه ذخیره‌شده.</summary>
public sealed partial class ExchangeRowViewModel : RowViewModel
{
    private readonly ExchangeRow _e;
    private readonly ExchangeSectionViewModel _owner;

    public ExchangeRowViewModel(ExchangeRow e, ExchangeSectionViewModel owner)
    {
        _e = e; _owner = owner;
        Loading = true;
        _dateShamsi = e.DateShamsi ?? "";
        _description = e.Description ?? "";
        _amount = e.Amount;
        _currency = e.Currency;
        _rate = e.Rate;
        _bardagi = e.Bardagi;
        Loading = false;
    }

    public ExchangeRow Entity => _e;

    [ObservableProperty] private string _dateShamsi = "";
    [ObservableProperty] private string _description = "";
    [ObservableProperty] private decimal _amount;
    [ObservableProperty] private ExchangeCurrency _currency;
    [ObservableProperty] private decimal _rate;
    [ObservableProperty] private decimal _bardagi;

    partial void OnDateShamsiChanged(string v) => Touch();
    partial void OnDescriptionChanged(string v) => Touch();
    partial void OnAmountChanged(decimal v) { Touch(); Refresh(); }
    partial void OnCurrencyChanged(ExchangeCurrency v) { Touch(); OnPropertyChanged(nameof(CurrencyText)); }
    partial void OnRateChanged(decimal v) { Touch(); Refresh(); }
    partial void OnBardagiChanged(decimal v) { Touch(); Refresh(); }

    private void Refresh()
    {
        OnPropertyChanged(nameof(AmountText));
        OnPropertyChanged(nameof(RateText));
        OnPropertyChanged(nameof(BardagiText));
        OnPropertyChanged(nameof(UsdText));
        OnPropertyChanged(nameof(RasidText));
    }

    public string AmountText  { get => Shamsi.Money(Amount);  set => Amount = Shamsi.Num(value); }
    public string RateText    { get => Shamsi.Money(Rate);    set => Rate = Shamsi.Num(value); }
    public string BardagiText { get => Shamsi.Money(Bardagi); set => Bardagi = Shamsi.Num(value); }

    /// <summary>دالرِ شکسته — مبلغ ÷ فی.</summary>
    public string UsdText => Shamsi.Money(Math.Round(_owner.Calc.ToUsd(_e), 2));
    /// <summary>
    /// «رسید به صرافی» — همان دالرِ شکسته. در نسخهٔ وب هم همین یک عدد دو بار
    /// در دو ستون می‌آید: یکی «دالر» و یکی «رسید به صرافی». ستونِ «الباقی» در
    /// ردیف‌ها عمداً خالی است و فقط ردیفِ «جمله» عدد دارد.
    /// </summary>
    public string RasidText => UsdText;

    /// <summary>
    /// گزینه‌های کشویی — <b>رشته</b>، نه ‎ComboBoxItem‎.
    ///
    /// ⚠️ باگی که این را لازم کرد: در XAML نوشته شده بود
    ///     ‎&lt;ComboBox SelectedItem="{Binding CurrencyText}"&gt;
    ///        &lt;ComboBoxItem Content="تومان"/&gt; …&lt;/ComboBox&gt;‎
    /// وقتی بچه‌های ComboBox خودشان ‎ComboBoxItem‎ باشند، ‎SelectedItem‎ یک
    /// <b>شیء</b> است نه رشته. پس اتصال به یک ‎string‎ نه سرِ بار مقدار را
    /// می‌نشاند (هیچ گزینه‌ای برابرِ رشته نیست) و نه با انتخابِ کاربر چیزِ
    /// درستی برمی‌گرداند — همان «نه منطقش کار می‌کند، نه سرِ جایش می‌نشیند و
    /// خودبه‌خود عوض می‌شود».
    /// </summary>
    public static string[] CurrencyOptions { get; } = { "افغانی", "تومان", "کلدار" };

    public string CurrencyText
    {
        get => Currency switch
        {
            ExchangeCurrency.Toman => "تومان",
            ExchangeCurrency.Kaldar => "کلدار",
            _ => "افغانی",
        };
        set => Currency = value switch
        {
            "تومان" => ExchangeCurrency.Toman,
            "کلدار" => ExchangeCurrency.Kaldar,
            _ => ExchangeCurrency.Afghani,
        };
    }

    protected override void Apply()
    {
        _e.DateShamsi = DateShamsi;
        _e.Description = Description;
        _e.Amount = Amount;
        _e.Currency = Currency;
        _e.Rate = Rate;
        _e.Bardagi = Bardagi;
    }

    protected override Task SaveAsync() => _owner.SaveEntityAsync(_e);
}

/// <summary>
/// ══ بخشِ صرافی ═════════════════════════════════════════════════════════════
/// همهٔ عددهای این صفحه دالرند: مبلغ به ارزِ خودش وارد می‌شود و با «فی»
/// شکسته می‌شود. فیِ صفر ⇒ صفر (نه بی‌نهایت) — مثلِ خودِ نسخهٔ وب.
/// </summary>
public sealed partial class ExchangeSectionViewModel
    : LedgerSectionViewModel<ExchangeRowViewModel, ExchangeRow>
{
    private readonly AppHost _host;

    public ExchangeSectionViewModel(AppHost host)
        : base("sarrafi", "sarrafi", "صرافی", host.ExchangeLedger)
    {
        _host = host;
        // «📝 یادداشت این بخش» — همتای ‎.sec-note-box‎ی سایت. کلیدش همان
        // کلیدِ نسخهٔ وب است تا نوت‌های واردشده سرِ جای خودشان بنشینند.
        Notes = new SectionNotesViewModel(Id, host.SectionNotes,
            (m, ok) => host.Toast(m, ok ? ToastKind.Ok : ToastKind.Warn));
        Calc = host.Exchange;
    }

    internal ExchangeService Calc { get; }

    /// <summary>
    /// ‎updateSarrafiRow‎ — پس از هر ویرایش، همان سطر با حسابِ شرکت هم‌گام
    /// می‌شود. در نسخهٔ وب این کار برای ‎desc‎ و ‎amount‎ و ‎rate‎ و ‎bardagi‎
    /// انجام می‌شود؛ چون این‌جا ذخیره ردیف‌به‌ردیف است، همان یک مسیر کافی است.
    /// </summary>
    public override async Task SaveEntityAsync(ExchangeRow e)
    {
        await base.SaveEntityAsync(e);
        await _host.ExchangeSync.SyncAsync(e);
        if (e.LegacyId is { Length: > 0 }) await Service.UpdateAsync(e);   // شناسهٔ تازه بماند
    }

    /// <summary>
    /// حذفِ یک سطر — ردیفِ خودکارش در حسابِ شرکت هم می‌رود.
    /// ⚠️ وگرنه بردگی در حسابِ شرکت می‌مانَد بی آنکه سطری پشتش باشد.
    /// </summary>
    protected override async Task BeforeDeleteAsync(ExchangeRow e) =>
        await _host.ExchangeSync.UnlinkAsync(e.LegacyId);

    [ObservableProperty] private ExchangeSummary _summary;

    public string TotalUsd => Shamsi.Money(Math.Round(Summary.TotalUsd, 2));
    public string TotalBardagi => Shamsi.Money(Math.Round(Summary.TotalBardagi, 2));
    public string TotalBardagiUsd => Shamsi.Money(Math.Round(Summary.TotalBardagiUsd, 2));
    public string Baqi => Shamsi.Money(Math.Round(Summary.Baqi, 2));

    /// <summary>
    /// جملهٔ زیرِ کادرها. مثبت یعنی طلبِ پمپ از صرافی (سبز) و منفی یعنی بدهیِ
    /// پمپ به صرافی (سرخ) — مو‌به‌مو همان دو جملهٔ نسخهٔ وب.
    /// </summary>
    public string BaqiSentence
    {
        get
        {
            var net = Math.Round(Summary.Baqi, 0, MidpointRounding.AwayFromZero);
            return net >= 0
                ? "🟢 الباقی صرافی نزد پمپ: " + Shamsi.Money(net) + " $"
                : "🔴 الباقی پمپ نزد صرافی: " + Shamsi.Money(Math.Abs(net)) + " $";
        }
    }

    public string BaqiSentenceNote =>
        Summary.Baqi >= 0 ? "این مقدار طلبِ شما از صرافی است" : "این مقدار را به صرافی بدهکارید";

    public string BaqiSentenceBrushKey => Summary.Baqi >= 0 ? "Pump.Ok" : "Pump.Danger";

    partial void OnSummaryChanged(ExchangeSummary v)
    {
        OnPropertyChanged(nameof(TotalUsd)); OnPropertyChanged(nameof(TotalBardagi));
        OnPropertyChanged(nameof(TotalBardagiUsd)); OnPropertyChanged(nameof(Baqi));
        OnPropertyChanged(nameof(BaqiSentence)); OnPropertyChanged(nameof(BaqiSentenceNote));
        OnPropertyChanged(nameof(BaqiSentenceBrushKey));
    }

    /// <summary>ردیفِ «جمله» — همه‌چیزِ این صفحه دالر است.</summary>
    protected override IReadOnlyList<TotalCell> BuildTotals() => new[]
    {
        new TotalCell("دالر", TotalUsd),
        new TotalCell("رسید به صرافی", TotalBardagi, "Pump.Ok"),
        new TotalCell("بردگی پمپ ($)", TotalBardagiUsd, "Pump.Warn"),
        new TotalCell("الباقی ($)", Baqi, Summary.Baqi >= 0m ? "Pump.Ok" : "Pump.Danger"),
    };

    protected override ExchangeRowViewModel Wrap(ExchangeRow e) => new(e, this);
    protected override long EntityIdOf(ExchangeRowViewModel r) => r.Entity.Id;
    protected override ExchangeRow EntityOf(ExchangeRowViewModel r) => r.Entity;

    protected override void Recalc() => Summary = Calc.Summarize(Rows.Select(r => r.Entity));

    /// <summary>‎printSarrafi()‎ — ورقِ صرافیِ همین ماه.</summary>
    [RelayCommand]
    private Task PdfAsync()
    {
        var rows = Rows.Select(r => r.Entity).ToList();
        var input = new ExchangeReportInput(Shamsi.MonthLabel(Month), rows, DocDates.Line());
        return Documents.ShowAsync(() => new ExchangeReport(input, Calc),
                                   "صرافی " + Shamsi.MonthLabel(Month));
    }
}
