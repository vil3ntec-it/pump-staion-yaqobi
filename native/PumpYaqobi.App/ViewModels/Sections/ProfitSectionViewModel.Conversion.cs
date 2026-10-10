using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PumpYaqobi.App.Printing;
using PumpYaqobi.App.Services;
using PumpYaqobi.Application.Localization;
using PumpYaqobi.Application.Services;
using PumpYaqobi.Domain;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;
using PumpYaqobi.Reporting.Pdf;

namespace PumpYaqobi.App.ViewModels.Sections;

/// <summary>
/// ══ 🔁 تبدیلِ تیل — پطرول ⇄ دیزل (۱۴۰۵/۰۷/۱۸) ════════════════════════════════
/// خواستهٔ صاحب ریپو: مقدارِ تیل، جهتِ تبدیل، قیمتِ خریدِ هر لیترِ مقصد (خودکار از
/// خریدهای مخزن و دستی‌پذیر)، مقدارِ نهایی و مبلغ، و «لیتر اضافه داده شده» با کادرِ سرخ.
/// محاسبه فقط در ‎FuelConversionService‎ است؛ این‌جا فقط کادرها، ثبت و تاریخچه.
/// ⛔ ثبت دائمی است: این صفحه هیچ دکمهٔ حذف یا ویرایشی برای تاریخچه ندارد.
/// </summary>
public sealed partial class ProfitSectionViewModel
{
    private List<BuyPrice> _prices = new();
    private bool _convFilling;
    private string _fromSource = "";
    private string _toSource = "";

    [ObservableProperty] private string _convQty = "";
    [ObservableProperty] private bool _convPetrolToDiesel = true;
    [ObservableProperty] private string _convFromPrice = "";
    [ObservableProperty] private string _convToPrice = "";
    [ObservableProperty] private string _convDelivered = "";

    public FuelType ConvFrom => ConvPetrolToDiesel ? FuelType.Petrol : FuelType.Diesel;
    public FuelType ConvTo => FuelConversionService.Other(ConvFrom);
    private static string FuelName(FuelType f) => f == FuelType.Diesel ? "🟤 دیزل" : "⛽ پطرول";
    public string ConvFromName => FuelName(ConvFrom);
    public string ConvToName => FuelName(ConvTo);
    public string ConvDirectionText => FuelName(ConvFrom) + " ⇐ " + FuelName(ConvTo);
    public string ConvQtyLabel => "مقدارِ " + FuelName(ConvFrom) + " (لیتر)";
    public string ConvFromPriceLabel => "قیمتِ خریدِ هر لیترِ " + FuelName(ConvFrom);
    public string ConvToPriceLabel => "قیمتِ خریدِ هر لیترِ " + FuelName(ConvTo);
    public string ConvDeliveredLabel => FuelName(ConvTo) + "ِ تحویل‌شده (لیتر)";
    public string ConvFromSourceText => SourceText(_fromSource);
    public string ConvToSourceText => SourceText(_toSource);

    private static string SourceText(string s) => s.Length == 0 ? "قیمتی ثبت نشده — دستی بنویسید" : "منبع: " + s;

    [ObservableProperty] private string _convValueText = "—";
    [ObservableProperty] private string _convAllowedText = "—";
    [ObservableProperty] private string _convResultText = "";
    [ObservableProperty] private bool _convOver;
    [ObservableProperty] private bool _convReady;

    partial void OnConvQtyChanged(string value) => ConvRecalc();
    partial void OnConvDeliveredChanged(string value) => ConvRecalc();
    partial void OnConvFromPriceChanged(string value)
    {
        if (!_convFilling) { _fromSource = "دستی"; OnPropertyChanged(nameof(ConvFromSourceText)); }
        ConvRecalc();
    }
    partial void OnConvToPriceChanged(string value)
    {
        if (!_convFilling) { _toSource = "دستی"; OnPropertyChanged(nameof(ConvToSourceText)); }
        ConvRecalc();
    }
    partial void OnConvPetrolToDieselChanged(bool value)
    {
        foreach (var n in new[] { nameof(ConvFrom), nameof(ConvTo), nameof(ConvFromName), nameof(ConvToName),
                                  nameof(ConvDirectionText), nameof(ConvQtyLabel), nameof(ConvFromPriceLabel),
                                  nameof(ConvToPriceLabel), nameof(ConvDeliveredLabel) })
            OnPropertyChanged(n);
        FillConvPrices(force: true);
    }

    [RelayCommand]
    private void ToggleConvDirection() => ConvPetrolToDiesel = !ConvPetrolToDiesel;

    /// <summary>
    /// قیمتِ خرید از مخزن: تازه‌ترین خریدِ همان تیل تا امروز (‎RealProfitService.PriceAt‎)، وگرنه
    /// فیِ خریدِ ذخیره‌شده. ⛔ قیمتی که کاربر دستی نوشته با بازخوانیِ صفحه پاک نمی‌شود —
    /// فقط با عوض کردنِ جهت.
    /// </summary>
    private void FillConvPrices(bool force)
    {
        var today = Shamsi.Key(Shamsi.Today());
        (string Text, string Source) Auto(FuelType f)
        {
            var fb = _host.Settings.GetDecimal(f == FuelType.Diesel
                ? PumpYaqobi.Services.Data.SettingsService.BuyPerLiterDiesel
                : PumpYaqobi.Services.Data.SettingsService.BuyPerLiterPetrol);
            var fromStore = RealProfitService.PriceAt(_prices, f, today);
            if (fromStore is { } v) return (Shamsi.Money(Math.Round(v, 2), 2), "خریدِ مخزن");
            if (fb > 0) return (Shamsi.Money(Math.Round(fb, 2), 2), "فیِ خریدِ ذخیره‌شده");
            return ("", "");
        }
        _convFilling = true;
        try
        {
            if (force || _fromSource != "دستی") { var a = Auto(ConvFrom); ConvFromPrice = a.Text; _fromSource = a.Source; }
            if (force || _toSource != "دستی") { var b = Auto(ConvTo); ConvToPrice = b.Text; _toSource = b.Source; }
        }
        finally { _convFilling = false; }
        OnPropertyChanged(nameof(ConvFromSourceText));
        OnPropertyChanged(nameof(ConvToSourceText));
        ConvRecalc();
    }

    private ConversionResult _conv;

    private void ConvRecalc()
    {
        _conv = FuelConversionService.Compute(Shamsi.Num(ConvQty), Shamsi.Num(ConvFromPrice),
                                              Shamsi.Num(ConvToPrice), Shamsi.Num(ConvDelivered));
        ConvReady = _conv.Ok;
        ConvOver = _conv.Ok && _conv.Over;
        if (!_conv.Ok)
        {
            ConvValueText = "—";
            ConvAllowedText = "—";
            ConvResultText = "مقدار و دو قیمتِ خرید را بنویسید.";
            return;
        }
        ConvValueText = Money(_conv.FromValue);
        ConvAllowedText = Lit(_conv.Allowed) + " لیتر " + FuelName(ConvTo);
        ConvResultText = _conv.Over
            ? $"⛔ لیتر اضافه داده شده: {Lit(_conv.ExtraLiters)} لیتر — ضررِ احتمالی {Money(_conv.Loss)}"
            : _conv.Diff < 0
                ? $"✅ {Lit(-_conv.Diff)} لیتر کمتر از مجاز — سود {Money(_conv.ProfitLoss)}"
                : "✅ درست — مقدارِ تحویل همان مقدارِ مجاز است";
    }

    private static string Lit(decimal v) => Shamsi.Money(Math.Round(v, 2), 2);

    /// <summary>«➕ ثبتِ تبدیل» — همهٔ عددها همان لحظه نوشته می‌شوند.</summary>
    [RelayCommand]
    private async Task SaveConversionAsync()
    {
        if (Shamsi.FirstUnreadable(("مقدار", ConvQty), ("قیمتِ مبدأ", ConvFromPrice),
                                   ("قیمتِ مقصد", ConvToPrice), ("تحویل‌شده", ConvDelivered)) is { } bad)
        { _host.Toast("«" + bad + "» عدد نیست — ثبت نشد.", ToastKind.Warn); return; }
        ConvRecalc();
        if (!_conv.Ok) { _host.Toast("مقدار و هر دو قیمتِ خرید را بنویسید.", ToastKind.Warn); return; }
        if (_conv.Over && !await Dialogs.ConfirmAsync("لیتر اضافه داده شده",
                $"{Lit(_conv.ExtraLiters)} لیتر بیشتر از مجاز ({Lit(_conv.Allowed)}) داده شده — ضررِ احتمالی {Money(_conv.Loss)}.\nبا همین ثبت شود؟"))
            return;

        var now = AppClock.Now;
        var today = Shamsi.Today();
        await _host.FuelConversionLedger.AddAsync(new FuelConversion
        {
            DateShamsi = today,
            TimeText = now.ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture),
            FromFuel = ConvFrom, ToFuel = ConvTo,
            Qty = Shamsi.Num(ConvQty),
            FromPrice = Shamsi.Num(ConvFromPrice), FromPriceSource = _fromSource,
            FromValue = _conv.FromValue,
            ToPrice = Shamsi.Num(ConvToPrice), ToPriceSource = _toSource,
            AllowedLiters = _conv.Allowed, DeliveredLiters = _conv.Delivered,
            DiffLiters = _conv.Diff, ProfitLoss = _conv.ProfitLoss, Status = _conv.Status,
        });
        ConvQty = ""; ConvDelivered = "";
        _host.Toast("✅ تبدیل ثبت شد", ToastKind.Ok);
        await LoadConversionsAsync();
    }

    // ── تاریخچه ─────────────────────────────────────────────────────────────

    public YearMonthPicker ConvPicker { get; private set; } = null!;
    private bool _convPickerLoading;
    public IReadOnlyList<string> ConvFuelFilters { get; } = new[] { "همه", "⛽ پطرول ⇐ 🟤 دیزل", "🟤 دیزل ⇐ ⛽ پطرول" };
    [ObservableProperty] private string _convFuelFilter = "همه";
    [ObservableProperty] private string _convSearch = "";
    public BulkRows<ConversionRow> ConvRows { get; } = new();
    [ObservableProperty] private string _convSummary = "";
    private List<FuelConversion> _convAll = new();

    partial void OnConvFuelFilterChanged(string value) => ShowConversions();
    partial void OnConvSearchChanged(string value) => ShowConversions();

    private void InitConversion()
    {
        ConvPicker = new YearMonthPicker(key =>
        {
            if (_convPickerLoading) return;
            _ = CrashGuard.RunAsync("تاریخچهٔ تبدیلِ تیل", LoadConversionRowsAsync);
        }, "همهٔ ماه‌ها");
    }

    /// <summary>ماه‌ها از تاریخِ واقعیِ ثبت (‎MonthKey‎ی ردیف‌ها)، بعد ردیف‌های همان دوره.</summary>
    private async Task LoadConversionsAsync()
    {
        var months = await _host.FuelConversionLedger.MonthsAsync();
        _convPickerLoading = true;
        try { ConvPicker.Load(months, ConvPicker.SelectedKey); }
        finally { _convPickerLoading = false; }
        await LoadConversionRowsAsync();
    }

    private async Task LoadConversionRowsAsync()
    {
        var period = ProfitPeriod.FromKey(ConvPicker.SelectedKey);
        _convAll = await _host.FuelConversionLedger.ListAsync(period.MonthFilter);
        ShowConversions();
    }

    /// <summary>صافیِ تیل و جست‌وجو فقط نمایش را می‌برد؛ هیچ سابقه‌ای عوض یا حذف نمی‌شود.</summary>
    internal List<FuelConversion> FilteredConversions()
    {
        IEnumerable<FuelConversion> q = _convAll;
        if (ConvFuelFilter == ConvFuelFilters[1]) q = q.Where(r => r.FromFuel == FuelType.Petrol);
        else if (ConvFuelFilter == ConvFuelFilters[2]) q = q.Where(r => r.FromFuel == FuelType.Diesel);
        var s = Shamsi.ToEnDigits(ConvSearch ?? "").Trim();
        if (s.Length > 0)
            q = q.Where(r => (r.DateShamsi ?? "").Contains(s, StringComparison.Ordinal)
                          || (r.Status ?? "").Contains(s, StringComparison.Ordinal)
                          || (r.Note ?? "").Contains(s, StringComparison.Ordinal));
        return q.OrderByDescending(r => r.DateKey).ThenByDescending(r => r.Id).ToList();
    }

    private void ShowConversions()
    {
        var rows = FilteredConversions();
        ConvRows.ResetTo(rows.Select(r => new ConversionRow(r)));
        var loss = rows.Where(r => r.ProfitLoss < 0).Sum(r => -r.ProfitLoss);
        var gain = rows.Where(r => r.ProfitLoss > 0).Sum(r => r.ProfitLoss);
        ConvSummary = rows.Count == 0 ? "در این دوره هیچ تبدیلی ثبت نشده."
            : $"{rows.Count} تبدیل · ارزشِ کل {Money(rows.Sum(r => r.FromValue))} · ضرر {Money(loss)} · سود {Money(gain)}";
    }

    /// <summary>📄 PDF و 🖨 چاپ — همان پیش‌نمایشِ همیشگی (چاپگر و ذخیره در آن‌جاست).</summary>
    [RelayCommand]
    private Task PdfConversionsAsync()
    {
        var rows = FilteredConversions().OrderBy(r => r.DateKey).ThenBy(r => r.Id).ToList();
        var filter = PeriodLabel(ProfitPeriod.FromKey(ConvPicker.SelectedKey))
                     + (ConvFuelFilter == "همه" ? "" : " · " + ConvFuelFilter)
                     + (string.IsNullOrWhiteSpace(ConvSearch) ? "" : " · جست‌وجو: " + ConvSearch.Trim());
        var input = new FuelConversionReportInput(rows, filter, DocDates.Line());
        return Documents.ShowAsync(() => new FuelConversionReport(input), "تاریخچهٔ تبدیلِ تیل");
    }
}

/// <summary>یک سطرِ تاریخچهٔ تبدیل — همهٔ جزئیات، فقط خواندنی.</summary>
public sealed class ConversionRow
{
    public ConversionRow(FuelConversion r)
    {
        string F(FuelType f) => f == FuelType.Diesel ? "🟤 دیزل" : "⛽ پطرول";
        string L(decimal v) => Shamsi.Money(Math.Round(v, 2), 2);
        string M(decimal v) => Shamsi.Money(Math.Round(v, 0, MidpointRounding.AwayFromZero)) + " افغانی";
        Title = $"{r.DateShamsi} {r.TimeText} — {F(r.FromFuel)} ⇐ {F(r.ToFuel)}";
        Source = $"{L(r.Qty)} لیتر × {L(r.FromPrice)} ({r.FromPriceSource}) = {M(r.FromValue)}";
        Target = $"÷ {L(r.ToPrice)} ({r.ToPriceSource}) = مجاز {L(r.AllowedLiters)} لیتر · تحویل {L(r.DeliveredLiters)} لیتر";
        Result = r.DiffLiters > 0 ? $"⛔ {r.Status}: +{L(r.DiffLiters)} لیتر · ضرر {M(-r.ProfitLoss)}"
               : r.DiffLiters < 0 ? $"✅ {r.Status}: {L(-r.DiffLiters)} لیتر · سود {M(r.ProfitLoss)}"
               : "✅ " + (r.Status ?? "درست");
        IsLoss = r.DiffLiters > 0;
    }
    public string Title { get; }
    public string Source { get; }
    public string Target { get; }
    public string Result { get; }
    public bool IsLoss { get; }
}
