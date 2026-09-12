using CommunityToolkit.Mvvm.Input;
using PumpYaqobi.App.Controls;
using PumpYaqobi.App.Services;
using PumpYaqobi.Application.Localization;
using PumpYaqobi.Application.Services;

namespace PumpYaqobi.App.ViewModels.Sections;

/// <summary>یک خط از «قرض‌های دسته‌جمعی».</summary>
public sealed class DebtSummaryRowViewModel
{
    public DebtSummaryRowViewModel(DebtSummaryRow r, int index, string unit)
    {
        Row = r; Index = index;
        Name = r.Person.Name ?? "";
        BardagiText = Num(r.Figures.Bardagi) + unit;
        RasidText = Num(r.Figures.Rasid) + unit;
        // ⚠️ علامتِ الباقی برعکسِ ستونِ بالاست — عینِ سایت: بدهی با منفی نوشته
        // می‌شود (‎n2fa(rnd(-f.albaqi))‎)، تسویه با صفر یا مثبت.
        AlbaqiText = Num(-r.Figures.Albaqi) + unit;
        AlbaqiBrushKey = r.Owes ? "Pump.Danger" : "Pump.Ok";

        AgeText = r.Owes ? (r.Age.Text.Length > 0 ? r.Age.Text : "بی‌تاریخ") : "✅ تسویه";
        FromText = r.Owes && r.Age.FromShamsi.Length > 0 ? "از " + r.Age.FromShamsi : "";
        // سه رنگِ خودِ سایت: از ۱۸۰ روز قرمز، از ۶۰ روز زرد، وگرنه خاکستری
        AgeBrushKey = !r.Owes ? "Pump.Muted"
                    : r.Age.Days >= 180 ? "Pump.Danger"
                    : r.Age.Days >= 60 ? "Pump.Warn" : "Pump.Muted";
    }

    /// <summary>گِردکردنِ ‎rnd‎ی سایت: تا دو رقمِ اعشار، نه بیشتر.</summary>
    private static string Num(decimal v) => Shamsi.Money(Math.Round(v, 2));

    public DebtSummaryRow Row { get; }
    public int Index { get; }
    public string Name { get; }
    public string BardagiText { get; }
    public string RasidText { get; }
    public string AlbaqiText { get; }
    public string AlbaqiBrushKey { get; }
    public string AgeText { get; }
    public string FromText { get; }
    public string AgeBrushKey { get; }
    public bool HasFrom => FromText.Length > 0;
}

/// <summary>
/// ══ قرض‌های دسته‌جمعی ═══════════════════════════════════════════════════════
/// رونوشتِ ‎renderDebtSummary(kind)‎ی نسخهٔ وب — بخشی که در برنامهٔ نیتیو اصلاً
/// نبود.
///
/// «فقط قرض‌دارانِ واحد تیل — هر کدام یک خط. با کلیک روی هر خط، حساب کامل همان
/// شخص باز می‌شود. ستونِ آخر می‌گوید چند وقت است قرض‌دار است.»
///
/// ⚠️ دو صفحهٔ کاملاً جدا، «تا پول و تیل هرگز در یک جدول قاطی نشوند» — جملهٔ
/// خودِ سایت. این کلاس هر دو را می‌سازد؛ ‎money‎ می‌گوید کدام.
///
/// ⚠️ هیچ چیزی نمی‌نویسد. عددها از همان‌جایی می‌آیند که کارتِ قرض‌دار و
/// «قرض‌های کهنه» می‌آیند.
/// </summary>
public sealed partial class DebtSummarySectionViewModel : SectionViewModel
{
    private readonly AppHost _host;
    private readonly bool _money;
    private readonly Func<long, Task>? _openPerson;

    public DebtSummarySectionViewModel(AppHost host, bool money, Func<long, Task>? openPerson = null)
        : base(money ? "debtsummoney" : "debtsum", "debt",
               money ? "💵 قرض‌های دسته‌جمعی — واحد پول" : "⛽ قرض‌های دسته‌جمعی — واحد تیل")
    {
        _host = host; _money = money; _openPerson = openPerson;
    }

    /// <summary>واحدِ ستون‌ها — لیتر در واحد تیل، و در واحد پول هیچ (افغانی).</summary>
    private string Unit => _money ? "" : " لیتر";

    public string Hint => _money
        ? "فقط قرض‌دارانِ «واحد پول» — هر کدام یک خط. با کلیک روی هر خط، حساب کامل همان شخص باز می‌شود."
        : "فقط قرض‌دارانِ «واحد تیل» — هر کدام یک خط. با کلیک روی هر خط، حساب کامل همان شخص باز می‌شود.";

    /// <summary>⚠️ ‎BulkRows‎: پر شدنِ جدول یک خبر می‌دهد نه ‎n‎ خبر.</summary>
    public BulkRows<DebtSummaryRowViewModel> Rows { get; } = new();

    public bool IsEmpty => Rows.Count == 0;

    public string EmptyText => _money
        ? "هیچ قرض‌دارِ «واحد پول» ثبت نشده"
        : "هیچ قرض‌دارِ «واحد تیل» ثبت نشده";

    private string _totBardagi = "0", _totRasid = "0", _totAlbaqi = "0";

    /// <summary>ردیفِ «جمله کل» — روی **همهٔ** قرض‌داران، نه فقط دیده‌شده‌ها.</summary>
    public IReadOnlyList<TotalCell> TotalCells => new[]
    {
        new TotalCell("قرض‌دارها", Shamsi.Money(Rows.Count)),
        new TotalCell("جمله بردگی", _totBardagi, "Pump.Accent", "جمله بردگی"),
        new TotalCell("جمله رسید", _totRasid, "Pump.Ok", "جمله رسید"),
        new TotalCell("الباقی", _totAlbaqi, "Pump.Danger", "الباقی"),
    };

    protected override Task LoadAsync() => RefreshAsync();

    public override Task OnActivatedAsync() => RefreshAsync();

    private async Task RefreshAsync()
    {
        var rows = await _host.Tools.DebtSummaryAsync(_money);

        using (Rows.Batch())
        {
            Rows.Clear();
            var i = 0;
            foreach (var r in rows) Rows.Add(new DebtSummaryRowViewModel(r, ++i, Unit));
        }

        _totBardagi = Shamsi.Money(Math.Round(rows.Sum(r => r.Figures.Bardagi), 2)) + Unit;
        _totRasid = Shamsi.Money(Math.Round(rows.Sum(r => r.Figures.Rasid), 2)) + Unit;
        _totAlbaqi = Shamsi.Money(Math.Round(-rows.Sum(r => r.Figures.Albaqi), 2)) + Unit;

        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(TotalCells));
    }

    /// <summary>«با کلیک روی هر خط، حساب کامل همان شخص باز می‌شود.»</summary>
    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task OpenAsync(DebtSummaryRowViewModel? row)
    {
        if (row is null || _openPerson is null) return;
        await _openPerson(row.Row.Person.Id);
    }
}

/// <summary>
/// ══ مدتِ عضویت ═════════════════════════════════════════════════════════════
/// رونوشتِ ‎openMembershipList()‎ — صفحه‌ای که دکمهٔ «⏳ مدت عضویت همه» باز
/// می‌کند. ⚠️ آن دکمه تا امروز **هیچ فرمانی نداشت**.
/// </summary>
public sealed partial class MembershipSectionViewModel : SectionViewModel
{
    private readonly AppHost _host;

    public MembershipSectionViewModel(AppHost host)
        : base("membership", "debt", "⏳ مدت عضویت قرض‌داران") => _host = host;

    public BulkRows<MembershipRowViewModel> Rows { get; } = new();

    public bool IsEmpty => Rows.Count == 0;

    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty] private string _summaryText = "";

    protected override Task LoadAsync() => RefreshAsync();

    public override Task OnActivatedAsync() => RefreshAsync();

    private async Task RefreshAsync()
    {
        var rows = await _host.Tools.MembershipAsync();
        using (Rows.Batch())
        {
            Rows.Clear();
            var i = 0;
            foreach (var r in rows) Rows.Add(new MembershipRowViewModel(r, ++i));
        }
        SummaryText = "مجموعاً " + Shamsi.Money(rows.Count) + " قرض‌دار — از قدیمی‌ترین مشتری به جدیدترین";
        OnPropertyChanged(nameof(IsEmpty));
    }
}

/// <summary>یک خط از «مدت عضویت».</summary>
public sealed class MembershipRowViewModel
{
    public MembershipRowViewModel(MembershipRow r, int index)
    {
        Index = index;
        Name = r.Person.Name ?? "—";
        FromText = r.FromShamsi.Length > 0 ? r.FromShamsi : "—";
        AgeText = r.Text.Length > 0 ? r.Text : "—";
    }

    public int Index { get; }
    public string Name { get; }
    public string FromText { get; }
    public string AgeText { get; }
}
