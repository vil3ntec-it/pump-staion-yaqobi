using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PumpYaqobi.App.Services;
using PumpYaqobi.Application.Localization;
using PumpYaqobi.Services.Data;

namespace PumpYaqobi.App.ViewModels.Sections;

/// <summary>یک کارت در صفحهٔ «تاریخچه‌ها» — یک بخش و شمارِ ردیف‌هایش.</summary>
public sealed partial class HistoryCardViewModel : ObservableObject
{
    private readonly HistorySectionViewModel _owner;

    public HistoryCardViewModel(HistoryKind k, HistorySectionViewModel owner)
    { Entity = k; _owner = owner; }

    public HistoryKind Entity { get; }

    public string Label => Entity.Label;

    /// <summary>«۱۲۴ ردیف · تازه‌ترین: ۱۴۰۵/۰۶/۱۲» یا «هنوز چیزی ثبت نشده».</summary>
    public string SubText => Entity.Count == 0
        ? "هنوز چیزی ثبت نشده"
        : Shamsi.Money(Entity.Count) + " ردیف"
          + (string.IsNullOrWhiteSpace(Entity.LatestDate) ? "" : " · تازه‌ترین: " + Entity.LatestDate);

    [RelayCommand]
    private Task Open() => _owner.OpenAsync(Entity.Key);
}

/// <summary>یک ردیفِ صفحهٔ تاریخچهٔ یک بخش.</summary>
public sealed class HistoryRowViewModel
{
    public HistoryRowViewModel(HistoryRow r, int index) { Entity = r; Index = index; }

    public HistoryRow Entity { get; }
    public int Index { get; }

    public string DateText => string.IsNullOrWhiteSpace(Entity.DateShamsi) ? "بی‌تاریخ" : Entity.DateShamsi;
    public string Title => Entity.Title;
    public string Detail => Entity.Detail;
    public string AmountText => Entity.AmountText;

    /// <summary>رنگِ مبلغ — سبزِ «آمد»، نارنجیِ «رفت»، یا رنگِ معمولی.</summary>
    public string AmountBrushKey => Entity.Tone switch
    {
        "in" => "Pump.Ok",
        "out" => "Pump.Accent",
        _ => "Pump.Text",
    };
}

/// <summary>
/// ══ تاریخچه‌ها ═════════════════════════════════════════════════════════════
/// رونوشتِ ‎#sec-history‎ و ‎#sec-historyview‎ — تنها بخشی از نسخهٔ وب که در
/// نیتیو جای خالی مانده بود.
///
/// دو صفحه: فهرستِ کارت‌ها، و تاریخچهٔ یک بخشِ مشخص با کشویِ ماه. **هیچ چیزی
/// این‌جا عوض یا پاک نمی‌شود** — فقط برای دیدن است، همان‌طور که در نسخهٔ وب
/// زیرِ عنوان نوشته شده بود.
/// </summary>
public sealed partial class HistorySectionViewModel : SectionViewModel
{
    /// <summary>گزینهٔ اولِ کشویِ ماه.</summary>
    public const string AllMonths = "همهٔ ماه‌ها";

    private readonly AppHost _host;
    private List<HistoryRow> _feed = new();

    public HistorySectionViewModel(AppHost host)
        : base("history", "history", "تاریخچه‌ها") => _host = host;

    public ObservableCollection<HistoryCardViewModel> Cards { get; } = new();
    public ObservableCollection<HistoryRowViewModel> Rows { get; } = new();
    public ObservableCollection<string> Months { get; } = new();

    /// <summary>فهرستِ کارت‌ها باز است یا صفحهٔ یک بخش.</summary>
    [ObservableProperty] private bool _isListVisible = true;

    [ObservableProperty] private string _openKind = "";
    [ObservableProperty] private string _pageTitle = "";
    [ObservableProperty] private string _summary = "";
    [ObservableProperty] private string _month = AllMonths;

    public bool IsEmpty => Rows.Count == 0;

    protected override Task LoadAsync() => RefreshCardsAsync();

    public override async Task OnActivatedAsync()
    {
        // تاریخچه خلاصهٔ دفترهای دیگر است، پس هر بار که کاربر وارد می‌شود
        // باید تازه شود — وگرنه عددها روی لحظهٔ ورودِ اول می‌مانند.
        if (IsListVisible) await RefreshCardsAsync();
        else await OpenAsync(OpenKind);
    }

    private async Task RefreshCardsAsync()
    {
        Cards.Clear();
        foreach (var k in await _host.History.CardsAsync())
            Cards.Add(new HistoryCardViewModel(k, this));
    }

    /// <summary>باز کردنِ تاریخچهٔ یک بخش — همان ‎openSectionHistory‎.</summary>
    public async Task OpenAsync(string kind)
    {
        OpenKind = kind;
        PageTitle = "🕘 تاریخچهٔ " + HistoryService.LabelOf(kind);
        IsListVisible = false;
        IsPageOpen = true;

        _feed = await _host.History.FeedAsync(kind);

        // کشویِ ماه از خودِ ردیف‌ها ساخته می‌شود، نه از تقویم: ماهی که ثبتی
        // ندارد در فهرست نمی‌آید تا کاربر دنبالِ صفحهٔ خالی نگردد.
        Months.Clear();
        Months.Add(AllMonths);
        foreach (var m in _feed.Select(r => r.MonthKey).Where(m => m.Length > 0)
                               .Distinct().OrderByDescending(m => m))
            Months.Add(m);

        Month = AllMonths;      // خودش ‎Apply‎ را صدا می‌زند
        Apply();
    }

    /// <summary>برگشت به کارت‌ها.</summary>
    [RelayCommand]
    private async Task BackAsync()
    {
        IsListVisible = true;
        IsPageOpen = false;
        OpenKind = "";
        Rows.Clear();
        _feed = new List<HistoryRow>();
        await RefreshCardsAsync();
    }

    partial void OnMonthChanged(string value) => Apply();

    private void Apply()
    {
        var picked = string.IsNullOrEmpty(Month) || Month == AllMonths
            ? _feed
            : _feed.Where(r => r.MonthKey == Month).ToList();

        Rows.Clear();
        var i = 0;
        foreach (var r in picked) Rows.Add(new HistoryRowViewModel(r, ++i));

        Summary = picked.Count == 0
            ? (Month == AllMonths ? "هنوز چیزی در این بخش ثبت نشده" : "در این ماه چیزی ثبت نشده")
            : "مجموعاً " + Shamsi.Money(picked.Count) + " ردیف"
              + (Month == AllMonths ? "" : " در " + Shamsi.MonthLabel(Month))
              + " — از تازه به کهنه";

        OnPropertyChanged(nameof(IsEmpty));
    }
}
