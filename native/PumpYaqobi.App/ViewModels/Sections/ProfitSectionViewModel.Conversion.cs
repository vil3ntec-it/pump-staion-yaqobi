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
    private string _petrolSource = "";
    private string _dieselSource = "";

    //  ⚠️ کادرهای قیمت به نامِ تیل‌اند، نه «مبدأ/مقصد» (۱۴۰۵/۰۷/۱۹): عوض کردنِ جهت
    //  قیمتِ دستیِ نوشته‌شده را پاک نمی‌کند و همیشه معلوم است کدام فی مالِ کدام تیل است.
    [ObservableProperty] private string _convQty = "";
    [ObservableProperty] private bool _convPetrolToDiesel = true;
    [ObservableProperty] private string _convPetrolPrice = "";
    [ObservableProperty] private string _convDieselPrice = "";
    [ObservableProperty] private string _convDelivered = "";

    public FuelType ConvFrom => ConvPetrolToDiesel ? FuelType.Petrol : FuelType.Diesel;
    public FuelType ConvTo => FuelConversionService.Other(ConvFrom);
    private static string FuelName(FuelType f) => f == FuelType.Diesel ? "🟤 دیزل" : "⛽ پطرول";
    public string ConvFromName => FuelName(ConvFrom);
    public string ConvToName => FuelName(ConvTo);
    /// <summary>دکمهٔ «نوعِ تیل» — زدنش جهت را برعکس می‌کند (همان دکمهٔ ۳.۱.۲۵۸ که صاحب ریپو خواست بماند).</summary>
    public string ConvDirectionText => FuelName(ConvFrom) + " ⇐ " + FuelName(ConvTo);

    public string ConvQtyLabel => "مقدارِ تیل — " + FuelName(ConvFrom) + " (لیتر)";
    public string ConvDeliveredLabel => FuelName(ConvTo) + "ِ داده‌شده (لیتر)";
    public string ConvPetrolSourceText => SourceText(_petrolSource);
    public string ConvDieselSourceText => SourceText(_dieselSource);
    private string ConvFromPrice => ConvFrom == FuelType.Petrol ? ConvPetrolPrice : ConvDieselPrice;
    private string ConvToPrice => ConvTo == FuelType.Petrol ? ConvPetrolPrice : ConvDieselPrice;
    private string FromSource => ConvFrom == FuelType.Petrol ? _petrolSource : _dieselSource;
    private string ToSource => ConvTo == FuelType.Petrol ? _petrolSource : _dieselSource;

    private static string SourceText(string s) => s.Length == 0 ? "⚠️ خریدی در مخزن نیست — دستی بنویسید" : "منبع: " + s;

    [ObservableProperty] private string _convValueText = "—";
    [ObservableProperty] private string _convAllowedText = "—";
    [ObservableProperty] private string _convDueText = "—";
    [ObservableProperty] private string _convDeliveredHint = "همان مقدارِ حق";
    [ObservableProperty] private string _convResultText = "";
    [ObservableProperty] private bool _convOver;
    [ObservableProperty] private bool _convUnder;
    [ObservableProperty] private bool _convReady;

    partial void OnConvQtyChanged(string value) => ConvRecalc();
    partial void OnConvDeliveredChanged(string value) => ConvRecalc();
    partial void OnConvPetrolPriceChanged(string value)
    {
        if (!_convFilling) { _petrolSource = "دستی"; OnPropertyChanged(nameof(ConvPetrolSourceText)); }
        ConvRecalc();
    }
    partial void OnConvDieselPriceChanged(string value)
    {
        if (!_convFilling) { _dieselSource = "دستی"; OnPropertyChanged(nameof(ConvDieselSourceText)); }
        ConvRecalc();
    }
    partial void OnConvPetrolToDieselChanged(bool value)
    {
        foreach (var n in new[] { nameof(ConvFrom), nameof(ConvTo), nameof(ConvFromName), nameof(ConvToName),
                                  nameof(ConvDirectionText), nameof(ConvQtyLabel),
                                  nameof(ConvDeliveredLabel) })
            OnPropertyChanged(n);
        ConvRecalc();
    }

    [RelayCommand]
    private void ToggleConvDirection() => ConvPetrolToDiesel = !ConvPetrolToDiesel;

    /// <summary>
    /// تازه‌ترین خریدِ همان تیل در مخزن تا امروز (همان قاعدهٔ ‎RealProfitService.PriceAt‎: اگر تا
    /// امروز نبود، نخستین خریدِ پس از آن). ⛔ نرخِ اتحادیه هرگز این‌جا نمی‌آید.
    /// </summary>
    public static BuyPrice? LatestBuy(IEnumerable<BuyPrice> prices, FuelType fuel, int today)
    {
        BuyPrice? before = null, after = null;
        foreach (var p in prices)
        {
            if (p.Fuel != fuel || p.PerLiter <= 0) continue;
            if (p.DateKey <= today)
            {
                if (before is not { } b || (p.DateKey, p.Id).CompareTo((b.DateKey, b.Id)) > 0) before = p;
            }
            else if (after is not { } a || (p.DateKey, p.Id).CompareTo((a.DateKey, a.Id)) < 0) after = p;
        }
        return before ?? after;
    }

    /// <summary>
    /// فیِ هر تیل از خریدهای مخزن (با تاریخِ همان خرید)، وگرنه فیِ خریدِ ذخیره‌شده، وگرنه خالی
    /// تا دستی نوشته شود. ⛔ قیمتی که کاربر دستی نوشته با بازخوانیِ صفحه پاک نمی‌شود.
    /// </summary>
    private void FillConvPrices(bool force)
    {
        var today = Shamsi.Key(Shamsi.Today());
        (string Text, string Source) Auto(FuelType f)
        {
            if (LatestBuy(_prices, f, today) is { } b)
                return (Shamsi.Money(Math.Round(b.PerLiter, 2), 2), "خریدِ مخزن " + Shamsi.FromKey(b.DateKey));
            var fb = _host.Settings.GetDecimal(f == FuelType.Diesel
                ? PumpYaqobi.Services.Data.SettingsService.BuyPerLiterDiesel
                : PumpYaqobi.Services.Data.SettingsService.BuyPerLiterPetrol);
            if (fb > 0) return (Shamsi.Money(Math.Round(fb, 2), 2), "فیِ خریدِ ذخیره‌شده");
            return ("", "");
        }
        _convFilling = true;
        try
        {
            if (force || _petrolSource != "دستی") { var a = Auto(FuelType.Petrol); ConvPetrolPrice = a.Text; _petrolSource = a.Source; }
            if (force || _dieselSource != "دستی") { var b = Auto(FuelType.Diesel); ConvDieselPrice = b.Text; _dieselSource = b.Source; }
        }
        finally { _convFilling = false; }
        OnPropertyChanged(nameof(ConvPetrolSourceText));
        OnPropertyChanged(nameof(ConvDieselSourceText));
        ConvRecalc();
    }

    /// <summary>«↺ از مخزن» — هر دو فی دوباره از خریدهای مخزن.</summary>
    [RelayCommand]
    private void ResetConvPrices() => FillConvPrices(force: true);

    private ConversionResult _conv;

    private void ConvRecalc()
    {
        _conv = FuelConversionService.Compute(Shamsi.Num(ConvQty), Shamsi.Num(ConvFromPrice),
                                              Shamsi.Num(ConvToPrice), Shamsi.Num(ConvDelivered));
        ConvReady = _conv.Ok;
        ConvOver = _conv.Ok && _conv.Over;
        ConvUnder = _conv.Ok && _conv.Diff < 0;
        if (!_conv.Ok)
        {
            ConvValueText = "—";
            ConvAllowedText = "—";
            ConvDueText = "—";
            ConvDeliveredHint = "همان مقدارِ حق";
            ConvResultText = Shamsi.Num(ConvQty) <= 0
                ? "مقدارِ تیل را بنویسید."
                : "فیِ خریدِ " + (Shamsi.Num(ConvFromPrice) <= 0 ? ConvFromName : ConvToName) + " را بنویسید.";
            return;
        }
        var to = ConvToName;
        ConvValueText = Money(_conv.FromValue);
        ConvAllowedText = Lit(_conv.Allowed) + " لیتر " + to;
        ConvDueText = ConvAllowedText + " · " + ConvValueText;
        ConvDeliveredHint = Lit(_conv.Allowed);
        var should = $"باید {Lit(_conv.Allowed)} لیتر {to} داده شود";
        ConvResultText = _conv.Over
            ? $"⛔ ضرر — لیتر اضافه داده شده: {Lit(_conv.ExtraLiters)} لیتر {to} ({Money(_conv.Loss)}) بیشتر از حق به مشتری رفته.\n{should}، نه {Lit(_conv.Delivered)}."
            : _conv.Diff < 0
                ? $"✅ فایده — {Lit(-_conv.Diff)} لیتر کمتر از حق داده شد ({Money(_conv.ProfitLoss)}).\n{should}."
                : $"✅ درست — نه ضرر، نه فایده.\n{should} (به ارزشِ {Money(_conv.FromValue)}).";
    }

    private static string Lit(decimal v) => Shamsi.Money(Math.Round(v, 2), 2);

    /// <summary>«➕ ثبتِ تبدیل» — همهٔ عددها همان لحظه نوشته می‌شوند.</summary>
    [RelayCommand]
    private async Task SaveConversionAsync()
    {
        if (Shamsi.FirstUnreadable(("مقدار", ConvQty), ("فیِ پطرول", ConvPetrolPrice),
                                   ("فیِ دیزل", ConvDieselPrice), ("تحویل‌شده", ConvDelivered)) is { } bad)
        { _host.Toast("«" + bad + "» عدد نیست — ثبت نشد.", ToastKind.Warn); return; }
        ConvRecalc();
        if (!_conv.Ok) { _host.Toast(ConvResultText, ToastKind.Warn); return; }
        if (_conv.Over && !await Dialogs.ConfirmAsync("لیتر اضافه داده شده",
                $"{Lit(_conv.ExtraLiters)} لیتر بیشتر از حق ({Lit(_conv.Allowed)}) داده شده — ضرر {Money(_conv.Loss)}.\nبا همین ثبت شود؟"))
            return;

        var now = AppClock.Now;
        var today = Shamsi.Today();
        await _host.FuelConversionLedger.AddAsync(new FuelConversion
        {
            DateShamsi = today,
            TimeText = now.ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture),
            FromFuel = ConvFrom, ToFuel = ConvTo,
            Qty = Shamsi.Num(ConvQty),
            FromPrice = Shamsi.Num(ConvFromPrice), FromPriceSource = FromSource,
            FromValue = _conv.FromValue,
            ToPrice = Shamsi.Num(ConvToPrice), ToPriceSource = ToSource,
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
