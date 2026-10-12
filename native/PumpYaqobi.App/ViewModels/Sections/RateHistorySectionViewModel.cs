using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using PumpYaqobi.App.Printing;
using PumpYaqobi.App.Services;
using PumpYaqobi.Reporting.Pdf;
using PumpYaqobi.Application.Localization;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;

namespace PumpYaqobi.App.ViewModels.Sections;

/// <summary>یک ثبتِ نرخ.</summary>
public sealed class RateHistoryRowViewModel
{
    public RateHistoryRowViewModel(RateHistoryEntry e, int index) { Entity = e; Index = index; }

    public RateHistoryEntry Entity { get; }
    public int Index { get; }

    public string FuelText => Entity.Fuel == FuelType.Diesel ? "🟤 دیزل" : "⛽ پطرول";
    public string RateText => Shamsi.Money(Entity.Rate) + " فی لیتر";
    public string DateShamsi => Entity.DateShamsi ?? "";
}

/// <summary>
/// ══ تاریخچهٔ نرخِ اتحادیه ═══════════════════════════════════════════════════
/// رونوشتِ ‎_renderRateHistPanel‎ — فقط دفترچهٔ «کِی چند بود».
///
/// خودش چیزی نمی‌نویسد: با هر ذخیرهٔ تنظیمات، اگر نرخ واقعاً عوض شده باشد
/// یک ردیف این‌جا می‌نشیند. روی هیچ محاسبه‌ای اثر ندارد.
/// </summary>
public sealed partial class RateHistorySectionViewModel : SectionViewModel
{
    private readonly AppHost _host;

    public RateHistorySectionViewModel(AppHost host)
        : base("ratehist", "history", "تاریخچهٔ نرخ") => _host = host;

    /// <summary>⚠️ ‎BulkRows‎: پر شدنِ جدول یک خبر می‌دهد نه ‎n‎ خبر
    /// — وگرنه جدول به ازای هر ردیف یک‌بار از نو چیده می‌شود و بخش می‌ایستد.</summary>
    public BulkRows<RateHistoryRowViewModel> Rows { get; } = new();

    public bool IsEmpty => Rows.Count == 0;

    protected override Task LoadAsync() => RefreshAsync();

    /// <summary>
    /// ⛔ فعال‌سازیِ این بخش فقط دفترِ دیتابیس را دوباره می‌خواند، پس با
    /// <c>PumpDbContext.Version</c>ِ دست‌نخورده اصلاً صدا زده نمی‌شود —
    /// ریشهٔ «هر بخش رو باز می‌کنم جدول‌ها یک ثانیه بعد میان» (۱۴۰۵/۰۷/۰۵).
    /// شرحش بالای <see cref="SectionViewModel.ActivationOnlyReadsDb"/>.
    /// </summary>
    public override bool ActivationOnlyReadsDb => true;

    public override Task OnActivatedAsync() => RefreshAsync();
    public override bool ActivationRepeatsLoad => true;

    private async Task RefreshAsync()
    {
        using (Rows.Batch())
        {
            Rows.Clear();
            var i = 0;
            foreach (var e in await _host.Tools.RateHistoryAsync())
            Rows.Add(new RateHistoryRowViewModel(e, ++i));
        }
        OnPropertyChanged(nameof(IsEmpty));
    }

    /// <summary>🖨 PDF — همان سه ستونِ جدول (۱۴۰۵/۰۷/۲۰). ورق‌ها بیرونِ نخِ رابط ساخته می‌شوند.</summary>
    [RelayCommand]
    private Task PdfAsync()
    {
        var rows = Rows.ToList();
        return Documents.ShowAsync(() => new HistoryReport(PdfInput(rows, DocDates.Line())), "تاریخچهٔ نرخ اتحادیه");
    }

    public static HistoryReportInput PdfInput(IReadOnlyList<RateHistoryRowViewModel> rows, string dates) =>
        new("نرخ اتحادیه", "همهٔ ثبت‌ها",
            new[] { new HistoryReportColumn("نوع تیل"), new HistoryReportColumn("نرخ", Brush: "Pump.Ok"),
                    new HistoryReportColumn("تاریخ") },
            rows.Select(r => new HistoryReportRow(new[] { r.FuelText, r.RateText, r.DateShamsi })).ToList(),
            dates);
}
