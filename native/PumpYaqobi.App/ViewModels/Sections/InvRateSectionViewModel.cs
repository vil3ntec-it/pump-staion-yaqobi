using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using PumpYaqobi.App.Controls;
using CommunityToolkit.Mvvm.Input;
using PumpYaqobi.App.Services;
using PumpYaqobi.Application.Localization;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;

namespace PumpYaqobi.App.ViewModels.Sections;

/// <summary>
/// یک ردیفِ «مقایسهٔ نرخ» — رونوشتِ ‎_plInvView(v)‎ی نسخهٔ وب، خط به خط.
///
/// ⚠️ این‌جا هیچ عددی ساخته نمی‌شود: هر دو نرخ از قبل روی خودِ فاکتور قفل
/// شده‌اند (‎RateOnCreate‎ و ‎RateOnApprove‎) و همان‌ها خوانده می‌شوند. پس عددِ
/// این صفحه با گزارشِ «زیان افزایش قیمت» همیشه یکی است.
/// </summary>
public sealed class InvRateRowViewModel
{
    public InvRateRowViewModel(Invoice v, int index, decimal nowRate)
    {
        Index = index;
        Number = v.InvoiceNumber;
        DateShamsi = v.DateShamsi ?? "";
        Customer = (v.CustomerName ?? "").Trim();
        IsDiesel = v.Fuel == FuelType.Diesel;
        Liters = v.Liters;

        // فاکتورهای کهنه ‎RateOnCreate‎ ندارند — همان فیِ خودشان مبناست
        Rc = v.RateOnCreate ?? v.PricePerLiter;
        Approved = v.Status == InvoiceStatus.Approved;
        Ra = Approved ? v.RateOnApprove ?? 0m : 0m;

        HasDiff = Approved && Rc > 0m && Ra > 0m;
        Dpl = HasDiff ? Ra - Rc : 0m;
        Diff = Dpl * Liters;

        // فاکتورِ در صف: نرخِ اتحادیهٔ همین لحظه — فقط پیش‌نمایش، در هیچ جمعی نیست
        NowRate = Approved ? 0m : nowRate;
        Pdpl = !Approved && Rc > 0m && NowRate > 0m ? NowRate - Rc : 0m;
        PendDiff = Pdpl * Liters;
    }

    public int Index { get; }
    public int Number { get; }
    public string DateShamsi { get; }
    public string Customer { get; }
    public bool IsDiesel { get; }
    public decimal Liters { get; }
    public decimal Rc { get; }
    public decimal Ra { get; }
    public bool Approved { get; }
    public bool HasDiff { get; }
    public decimal Dpl { get; }
    public decimal Diff { get; }
    public decimal NowRate { get; }
    public decimal Pdpl { get; }
    public decimal PendDiff { get; }

    public decimal Loss => Diff > 0m ? Diff : 0m;
    public decimal Gain => Diff < 0m ? -Diff : 0m;

    // ── ستون‌های نمایشی، مو‌به‌مو مثلِ جدولِ سایت ────────────────────────────
    public string IndexText => Shamsi.Money(Index);
    public string NumberText => Shamsi.Money(Number);
    public string DateText => DateShamsi.Length > 0 ? DateShamsi : "—";
    public string CustomerText => Customer.Length > 0 ? Customer : "—";
    public string FuelText => IsDiesel ? "🟤 دیزل" : "⛽ پطرول";
    public string LitersText => Liters != 0m ? Shamsi.Money(Math.Round(Liters, 2), 2) : "—";
    public string RcText => Rc != 0m ? Shamsi.Money(Math.Round(Rc, 2), 2) : "—";

    public string RaText =>
        HasDiff ? Shamsi.Money(Math.Round(Ra, 2), 2)
        : !Approved && NowRate > 0m ? Shamsi.Money(Math.Round(NowRate, 2), 2)
        : "—";

    public string DplText =>
        HasDiff ? Sign(Dpl) + Shamsi.Money(Math.Round(Dpl, 2), 2)
        : !Approved && Pdpl != 0m ? Sign(Pdpl) + Shamsi.Money(Math.Round(Pdpl, 2), 2)
        : "—";

    public string TotalText =>
        HasDiff ? Sign(Diff) + Shamsi.Money(Math.Round(Diff, 0, MidpointRounding.AwayFromZero))
        : !Approved && PendDiff != 0m
            ? Sign(PendDiff) + Shamsi.Money(Math.Round(PendDiff, 0, MidpointRounding.AwayFromZero))
        : "—";

    public string NoteText =>
        HasDiff ? (Diff > 0m ? "🔴 زیان" : Diff < 0m ? "🟢 مفاد" : "⚪ بی‌تفاوت")
        : !Approved ? "🟡 در صف — پیش‌نمایش با نرخِ امروز"
        : "⚪ نرخِ روزِ تایید ثبت نشده";

    /// <summary>سرخ برای زیان، سبز برای مفاد، خاکستری برای پیش‌نمایش.</summary>
    public string DiffBrushKey =>
        !HasDiff ? "Pump.Muted" : Diff > 0m ? "Pump.Danger" : Diff < 0m ? "Pump.Ok" : "Pump.Muted";

    private static string Sign(decimal v) => v > 0m ? "+" : "";
}

/// <summary>
/// ══ 📉 مقایسهٔ نرخ فاکتورها ═════════════════════════════════════════════════
///
/// خواستهٔ صاحب ریپو (همان که در سایت هم نوشته شده): «کسی با نرخ ۶۰ از من
/// فاکتور می‌گیرد و پولش را دیر می‌آورد؛ وقتِ تایید با نرخِ اتحادیهٔ همان روز
/// مقایسه شود که چقدر فرق کرده.»
///
/// در سایت این یک صفحهٔ جداست که از کارتِ سومِ بخشِ فاکتورها باز می‌شود
/// (‎showSection('invrate')‎) — پس این‌جا هم زیربخشِ همان بخش است، نه دکمهٔ نوار.
/// </summary>
public sealed partial class InvRateSectionViewModel : SectionViewModel
{
    private readonly AppHost _host;
    private List<InvRateRowViewModel> _all = new();

    public InvRateSectionViewModel(AppHost host)
        : base("invrate", "invoices", "مقایسهٔ نرخ فاکتورها") => _host = host;

    /// <summary>⚠️ ‎BulkObservableCollection‎: پر شدنِ جدول یک خبر می‌دهد نه ‎n‎ خبر
    /// — وگرنه جدول به ازای هر ردیف یک‌بار از نو چیده می‌شود و بخش می‌ایستد.</summary>
    public BulkObservableCollection<InvRateRowViewModel> Rows { get; } = new();

    /// <summary>۰ همه · ۱ فقط زیان · ۲ فقط مفاد — همان ‎_invRateFilter‎.</summary>
    [ObservableProperty] private int _filterIndex;

    public bool IsAll => FilterIndex == 0;
    public bool IsLoss => FilterIndex == 1;
    public bool IsGain => FilterIndex == 2;

    [ObservableProperty] private string _countText = "0 فاکتور";
    [ObservableProperty] private string _litersText = "0 لیتر";
    [ObservableProperty] private string _lossText = "0";
    [ObservableProperty] private string _gainText = "0";
    [ObservableProperty] private string _netText = "0";
    [ObservableProperty] private string _pendingText = "0 فاکتور";
    [ObservableProperty] private bool _isEmpty = true;

    /// <summary>خالصِ تفاوت — همان عددی که روی کارتِ بخشِ فاکتورها می‌نشیند.</summary>
    public decimal Net { get; private set; }

    public string NetBrushKey => Net > 0m ? "Pump.Danger" : "Pump.Ok";

    /// <summary>ردیفِ «جمله»ی ته جدول — همان ‎_invRateTotals()‎ی سایت.</summary>
    public IReadOnlyList<TotalCell> TotalCells => new[]
    {
        new TotalCell("فاکتورها", CountText),
        new TotalCell("لیتر", LitersText),
        new TotalCell("زیان", LossText, "Pump.Danger"),
        new TotalCell("مفاد", GainText, "Pump.Ok"),
        new TotalCell("خالص", NetText, NetBrushKey),
        new TotalCell("در صف", PendingText, "Pump.Muted"),
    };

    partial void OnNetTextChanged(string v) => OnPropertyChanged(nameof(TotalCells));

    partial void OnFilterIndexChanged(int v)
    {
        foreach (var n in new[] { nameof(IsAll), nameof(IsLoss), nameof(IsGain) })
            OnPropertyChanged(n);
        ApplyFilter();
    }

    [RelayCommand]
    private void SetFilter(string? which) =>
        FilterIndex = which switch { "loss" => 1, "gain" => 2, _ => 0 };

    protected override Task LoadAsync() => RefreshAsync();
    public override Task OnActivatedAsync() => RefreshAsync();

    public async Task RefreshAsync()
    {
        var list = await _host.Invoices.ListAsync();
        var petrol = _host.Settings.UnionRate(FuelType.Petrol);
        var diesel = _host.Settings.UnionRate(FuelType.Diesel);

        _all = list.OrderByDescending(v => v.InvoiceNumber)
                   .Select((v, i) => new InvRateRowViewModel(
                       v, i + 1, v.Fuel == FuelType.Diesel ? diesel : petrol))
                   .ToList();

        // ── جمع‌ها — مو‌به‌مو ‎_invRateTotals()‎ ─────────────────────────────
        decimal loss = 0, gain = 0, liters = 0;
        int n = 0, pend = 0;
        foreach (var r in _all)
        {
            if (!r.Approved) { pend++; continue; }
            if (!r.HasDiff) continue;
            n++; liters += r.Liters;
            loss += r.Loss; gain += r.Gain;
        }
        Net = loss - gain;

        CountText = Shamsi.Money(n) + " فاکتور";
        LitersText = Shamsi.Money(Math.Round(liters, 2), 2) + " لیتر";
        LossText = Shamsi.Money(Math.Round(loss, 0, MidpointRounding.AwayFromZero)) + " افغانی";
        GainText = Shamsi.Money(Math.Round(gain, 0, MidpointRounding.AwayFromZero)) + " افغانی";
        NetText = Shamsi.Money(Math.Round(Net, 0, MidpointRounding.AwayFromZero)) + " افغانی";
        PendingText = Shamsi.Money(pend) + " فاکتور";
        OnPropertyChanged(nameof(NetBrushKey));

        ApplyFilter();
    }

    private void ApplyFilter()
    {
        using (Rows.Batch())
        {
            Rows.Clear();
            foreach (var r in _all)
            {
                var keep = FilterIndex switch
                {
                    1 => r.Approved && r.HasDiff && r.Diff > 0m,
                    2 => r.Approved && r.HasDiff && r.Diff < 0m,
                    _ => true,
                };
                if (keep) Rows.Add(r);
            }
        }
        IsEmpty = Rows.Count == 0;
    }
}
