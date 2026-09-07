using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PumpYaqobi.App.Services;
using PumpYaqobi.Application.Localization;
using PumpYaqobi.Application.Services;

namespace PumpYaqobi.App.ViewModels.Sections;

/// <summary>یک قرض‌دارِ بی‌حرکت.</summary>
public sealed class OldLoanRowViewModel
{
    public OldLoanRowViewModel(AgingRow r, int index) { Row = r; Index = index; }

    public AgingRow Row { get; }
    public int Index { get; }

    public string Name => Row.Person.Name ?? "";
    public string Phone => string.IsNullOrWhiteSpace(Row.Person.Phone) ? "—" : Row.Person.Phone!;
    public string UnitText => Row.Figures.IsMoney ? "افغانی" : "لیتر";

    public string AlbaqiText =>
        Shamsi.Money(Math.Round(Row.Figures.Albaqi, 0, MidpointRounding.AwayFromZero))
        + " " + UnitText;

    /// <summary>‎-1‎ یعنی هیچ ردیفش تاریخ ندارد.</summary>
    public string IdleText => Row.DaysIdle < 0 ? "بی‌تاریخ"
                            : Shamsi.Money(Row.DaysIdle) + " روز بی‌حرکت";

    /// <summary>
    /// همان سه رنگِ نسخهٔ وب: از ۶۰ روز قرمز، از ۳۰ روز زرد، وگرنه خاکستری.
    /// </summary>
    public string IdleBrushKey => Row.DaysIdle >= 60 ? "Pump.Danger"
                                : Row.DaysIdle >= 30 ? "Pump.Warn" : "Pump.Muted";
}

/// <summary>
/// ══ قرض‌های کهنه ═══════════════════════════════════════════════════════════
/// رونوشتِ ‎_agingRows‎ · ‎_renderAgingPanel‎.
///
/// فقط کسانی که هنوز الباقیِ مثبت دارند، و بی‌حرکت‌ترین‌ها اول. «واحد تیل» و
/// «واحد پول» دو فهرستِ جدا هستند و هرگز با هم جمع نمی‌شوند — الباقیِ یکی
/// لیتر است و آن‌یکی افغانی.
/// </summary>
public sealed partial class OldLoansSectionViewModel : SectionViewModel
{
    private readonly AppHost _host;

    public OldLoansSectionViewModel(AppHost host) : base("oldloans", "debt", "قرض‌های کهنه")
        => _host = host;

    public ObservableCollection<OldLoanRowViewModel> Rows { get; } = new();

    /// <summary>۰ همه · ۱ فقط واحد تیل · ۲ فقط واحد پول.</summary>
    [ObservableProperty] private int _filterIndex;

    [ObservableProperty] private string _totalText = "—";
    [ObservableProperty] private string _stale30 = "0";
    [ObservableProperty] private string _stale60 = "0";
    [ObservableProperty] private string _countText = "0";

    public bool IsEmpty => Rows.Count == 0;
    public bool IsAll => FilterIndex == 0;
    public bool IsFuel => FilterIndex == 1;
    public bool IsMoney => FilterIndex == 2;

    private AgingFilter Filter => FilterIndex switch
    {
        1 => AgingFilter.Fuel,
        2 => AgingFilter.Money,
        _ => AgingFilter.All,
    };

    partial void OnFilterIndexChanged(int v)
    {
        foreach (var n in new[] { nameof(IsAll), nameof(IsFuel), nameof(IsMoney) })
            OnPropertyChanged(n);
        _ = RefreshAsync();
    }

    [RelayCommand]
    private void SetFilter(string? which) =>
        FilterIndex = which switch { "fuel" => 1, "money" => 2, _ => 0 };

    protected override Task LoadAsync() => RefreshAsync();

    public override Task OnActivatedAsync() => RefreshAsync();

    private async Task RefreshAsync()
    {
        var rows = await _host.Tools.AgingAsync(Filter);

        Rows.Clear();
        var i = 0;
        foreach (var r in rows) Rows.Add(new OldLoanRowViewModel(r, ++i));

        // ⚠️ جمعِ کل فقط وقتی معنی دارد که واحدها یکی باشند. در حالتِ «همه»
        // لیتر و افغانی قاطی می‌شوند، پس عمداً جمع نشان داده نمی‌شود — همان
        // چیزی که PDFهای جدای «تیل» و «پول» در نسخهٔ وب رعایت می‌کردند.
        TotalText = Filter == AgingFilter.All
            ? "—"
            : Shamsi.Money(Math.Round(rows.Sum(r => r.Figures.Albaqi), 0,
                                      MidpointRounding.AwayFromZero))
              + (Filter == AgingFilter.Money ? " افغانی" : " لیتر");

        CountText = Shamsi.Money(rows.Count);
        Stale30 = Shamsi.Money(rows.Count(r => r.DaysIdle >= 30));
        Stale60 = Shamsi.Money(rows.Count(r => r.DaysIdle >= 60));
        OnPropertyChanged(nameof(IsEmpty));
    }
}
