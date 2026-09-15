using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PumpYaqobi.App.Controls;
using PumpYaqobi.App.Services;
using PumpYaqobi.Application.Localization;
using PumpYaqobi.Application.Services;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;
using PumpYaqobi.Services.Data;

namespace PumpYaqobi.App.ViewModels.Sections;

// ══ 🗂️ جدول‌های آرشیوِ یک حسابِ قرض‌دار — صفحهٔ جداگانه ═══════════════════
// رونوشتِ ‎openPersonArchive‎ · ‎_renderHistoryPanel‎ · ‎_histFigures‎ ·
// ‎updateHistoryRow‎ · ‎calcHistoryRow‎ · ‎updateHistoryPercent‎ ·
// ‎histRasidBlur‎ · ‎setHistoryFilter‎ · ‎addHistoryRow‎ · ‎deleteHistoryRow‎.
//
// گزارشِ صاحب ریپو دربارهٔ سایت: «جدول‌های آرشیو اصلاً شبیه اصلی‌ها نیست —
// همهٔ کادرها و سربرگ‌ها را مثل جدولِ اصلی کن» و «جدول‌های آرشیو جمع کل تیل
// برده‌شده ندارد، جمع رسید ندارد». پس هر آرشیو همان سربرگِ حسابِ زنده را
// دارد (فیصدی، مقدار رسید، برد، الباقی — برای هر تیل)، همان فیلترِ
// همه/پطرول/دیزل، ردیف‌های **ویرایش‌شدنی** و جمعِ زیرِ هر ستون.
//
// ⚠️ آرشیو عکس است: ویرایشش فقط خودِ آرشیو را عوض می‌کند (‎RowsJson‎) و به
// جدولِ زنده و الباقیِ حساب دست نمی‌زند.

/// <summary>یک ردیفِ ویرایش‌شدنیِ جدولِ آرشیو — همان ستون‌های ردیفِ زنده.</summary>
public sealed partial class DebtArchiveRowViewModel : RowViewModel
{
    private readonly DebtRow _r;
    private readonly DebtArchiveViewModel _owner;

    public DebtArchiveRowViewModel(DebtRow r, DebtArchiveViewModel owner)
    {
        _r = r; _owner = owner;
        Loading = true;
        _dateShamsi = r.DateShamsi ?? "";
        _name = r.Name ?? "";
        _hawala = r.Hawala ?? "";
        _fuel = r.Fuel;
        _liters = r.Liters;
        _price = r.PricePerLiter ?? 0m;
        _manualBardagi = r.Bardagi;
        _rasid = r.Rasid;
        _rasidFuel = r.RasidFuel;
        Loading = false;
    }

    public DebtRow Entity => _r;

    [ObservableProperty] private string _dateShamsi = "";
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private string _hawala = "";
    [ObservableProperty] private FuelType _fuel;
    [ObservableProperty] private decimal _liters;
    [ObservableProperty] private decimal _price;
    [ObservableProperty] private decimal _manualBardagi;
    [ObservableProperty] private decimal _rasid;
    [ObservableProperty] private decimal _rasidFuel;

    partial void OnDateShamsiChanged(string v) => Touch();
    partial void OnNameChanged(string v) => Touch();
    partial void OnHawalaChanged(string v) => Touch();
    partial void OnFuelChanged(FuelType v)
    {
        Touch(); Refresh();
        foreach (var n in new[] { nameof(FuelText), nameof(FuelChipText), nameof(FuelChipBrushKey), nameof(IsDiesel) })
            OnPropertyChanged(n);
    }
    partial void OnLitersChanged(decimal v) { Touch(); Refresh(); }
    partial void OnPriceChanged(decimal v) { Touch(); Refresh(); }
    partial void OnManualBardagiChanged(decimal v) { Touch(); Refresh(); }
    partial void OnRasidChanged(decimal v) { Touch(); Refresh(); }
    partial void OnRasidFuelChanged(decimal v) { Touch(); Refresh(); }

    private void Refresh()
    {
        foreach (var n in new[] { nameof(LitersText), nameof(PriceText), nameof(BardagiText),
                                  nameof(RasidText), nameof(RasidFuelText), nameof(AlbaqiText) })
            OnPropertyChanged(n);
        _owner.RefreshFigures();
    }

    public string LitersText { get => Shamsi.MoneyOrBlank(Liters); set => Liters = Shamsi.Num(value); }
    public string PriceText { get => Shamsi.MoneyOrBlank(Price); set => Price = Shamsi.Num(value); }
    public string RasidText { get => Shamsi.MoneyOrBlank(Rasid); set => Rasid = Shamsi.Num(value); }
    public string RasidFuelText { get => Shamsi.MoneyOrBlank(RasidFuel); set => RasidFuel = Shamsi.Num(value); }
    /// <summary>بردگی — لیتر × فی، یا عددِ دستیِ ردیفِ پولی (‎_personRowBardagi‎).</summary>
    public string BardagiText
    {
        get => Shamsi.MoneyOrBlank(_owner.Calc.RowBardagi(_r));
        set { ManualBardagi = Shamsi.Num(value); _r.ByMoney = true; Touch(); Refresh(); }
    }
    /// <summary>الباقیِ همین ردیف — بردگی − رسید، مثلِ ستونِ آخرِ جدولِ زنده.</summary>
    public string AlbaqiText =>
        Shamsi.Money(Math.Round(_owner.Calc.RowBardagi(_r) - Rasid, 0, MidpointRounding.AwayFromZero));
    public bool IsDiesel => Fuel == FuelType.Diesel;
    public string FuelText => Fuel.ToPersian();
    public string FuelChipText => Fuel.ToPersian();
    public string FuelChipBrushKey => IsDiesel ? "Pump.Warn" : "Pump.Ok";

    [RelayCommand] private void ToggleFuel() => Fuel = IsDiesel ? FuelType.Petrol : FuelType.Diesel;

    protected override void Apply()
    {
        _r.DateShamsi = DateShamsi;
        _r.DateKey = Shamsi.Key(DateShamsi);
        _r.Name = Name;
        _r.Hawala = Hawala;
        _r.Fuel = Fuel;
        _r.Liters = Liters;
        _r.PricePerLiter = Price == 0m ? null : Price;
        _r.Bardagi = ManualBardagi;
        _r.Rasid = Rasid;
        _r.RasidFuel = RasidFuel;
        _owner.Calc.NormalizeRow(_r);
    }

    protected override Task SaveAsync() => _owner.PersistAsync();
}

/// <summary>یک جدولِ آرشیو با سربرگِ حسابِ زنده، فیلترِ تیل، ردیف‌های ویرایش‌شدنی و جمع.</summary>
public sealed partial class DebtArchiveViewModel : ObservableObject, IRowBatchHost
{
    private readonly AppHost _host;
    private readonly DebtArchivePageViewModel _page;
    private readonly List<DebtRow> _rows;
    private bool _pulling;

    public DebtArchiveViewModel(DebtTableArchive h, AppHost host, DebtArchivePageViewModel page, int no = 1)
    {
        _host = host; _page = page; Entity = h;
        _rows = DebtorService.ArchiveRows(h);
        IsMoney = h.IsMoney;
        Title = "🗂️ " + (h.CreatedShamsi ?? "—") + " — " + Shamsi.Money(_rows.Count) + " ردیف · واحدِ " + (IsMoney ? "پول" : "تیل");
        No = no;
        BarText = BarLabel(h.CreatedShamsi, no);
        _pulling = true;
        PercentPetrolText = Shamsi.MoneyOrBlank(h.PercentPetrol ?? 0m);
        PercentDieselText = Shamsi.MoneyOrBlank(h.PercentDiesel ?? 0m);
        NoteText = h.Note ?? "";
        _pulling = false;
        BuildRows();
    }

    public DebtTableArchive Entity { get; }
    public DebtCalculationService Calc => _host.Debt;

    // ══ نوارِ کشویی — همان ‎personArchiveModal‎ی سایت ══════════════════════
    // خواستهٔ صاحب ریپو با عکس: هر جدول یک نوارِ رنگیِ تمام‌عرض، بسته؛ زدنش
    // مشخصاتِ همان جدول را باز می‌کند. روی نوار فقط چهار چیز است:
    // «۱۴۰۳/۱۲/۱۱ · شنبه · ماه حوت · جدول چهارم». شمارهٔ جدول از ترتیبِ
    // ساخته شدن می‌آید (قدیمی‌ترین = جدول اول) تا با آرشیوِ تازه جابه‌جا نشود.
    public int No { get; }
    public string BarText { get; }
    [ObservableProperty] private bool _isOpen;
    [RelayCommand] private void Toggle() => IsOpen = !IsOpen;

    private static readonly string[] BarKeys = { "Pump.Orange", "Pump.Purple", "Pump.Ok", "Pump.Info" };
    public string BarBrushKey => BarKeys[(No - 1) % BarKeys.Length];

    private static readonly string[] Ordinals =
        { "اول", "دوم", "سوم", "چهارم", "پنجم", "ششم", "هفتم", "هشتم", "نهم", "دهم",
          "یازدهم", "دوازدهم", "سیزدهم", "چهاردهم", "پانزدهم" };

    public static string BarLabel(string? created, int no)
    {
        var ord = no >= 1 && no <= Ordinals.Length ? Ordinals[no - 1] : Shamsi.Money(no);
        var parts = new List<string>();
        var date = (created ?? "").Trim();
        if (date.Length > 0)
        {
            parts.Add(date);
            if (Shamsi.ToDate(date) is { } d) parts.Add(Shamsi.DayName(d));
            var mk = Shamsi.MonthKey(date);
            if (mk.Length >= 7 && int.TryParse(mk[^2..], out var m) && Shamsi.MonthName(m) is { Length: > 0 } mn)
                parts.Add("ماه " + mn);
        }
        parts.Add("جدول " + ord);
        return string.Join(" · ", parts);
    }
    public string Title { get; }
    public bool IsMoney { get; }
    public string UnitText => IsMoney ? "افغانی" : "لیتر";
    public string HeadRasidLabel => IsMoney ? "مقدار رسید پول" : "مقدار رسید تیل";
    public string HeadAlbaqiLabel => IsMoney ? "الباقی پول" : "الباقی تیل";
    public bool ShowRasidColumn => IsMoney;
    public bool ShowRasidFuelColumn => !IsMoney;

    public BulkRows<DebtArchiveRowViewModel> Rows { get; } = new();
    public int RowCount => Rows.Count;
    public System.Windows.Input.ICommand? RowAddCommand => AddRowCommand;

    // ── فیلتر همه / پطرول / دیزل — ‎setHistoryFilter‎ ─────────────────────
    [ObservableProperty] private string _rowFilter = "all";
    public bool IsAll => RowFilter == "all";
    public bool IsPetrolFilter => RowFilter == "petrol";
    public bool IsDieselFilter => RowFilter == "diesel";
    public bool ShowPetrolCard => RowFilter != "diesel";
    public bool ShowDieselCard => RowFilter != "petrol";
    public bool ShowFuelTypeColumn => RowFilter == "all";
    partial void OnRowFilterChanged(string v)
    {
        foreach (var n in new[] { nameof(IsAll), nameof(IsPetrolFilter), nameof(IsDieselFilter),
                                  nameof(ShowPetrolCard), nameof(ShowDieselCard), nameof(ShowFuelTypeColumn) })
            OnPropertyChanged(n);
        BuildRows();
    }
    [RelayCommand] private void SetFilter(string? f) => RowFilter = f is "petrol" or "diesel" ? f : "all";

    private void BuildRows()
    {
        using (Rows.Batch())
        {
            Rows.Clear();
            foreach (var r in _rows)
                if (RowFilter == "all" || (r.Fuel == FuelType.Diesel ? "diesel" : "petrol") == RowFilter)
                    Rows.Add(new DebtArchiveRowViewModel(r, this));
        }
        OnPropertyChanged(nameof(RowCount));
        RefreshFigures();
    }

    // ── سربرگ — ‎_histFigures‎ ─────────────────────────────────────────────
    [ObservableProperty] private string _percentPetrolText = "";
    [ObservableProperty] private string _percentDieselText = "";
    [ObservableProperty] private string _noteText = "";
    partial void OnPercentPetrolTextChanged(string v) { if (_pulling) return; Entity.PercentPetrol = Shamsi.Num(v); RefreshFigures(); _ = PersistAsync(); }
    partial void OnPercentDieselTextChanged(string v) { if (_pulling) return; Entity.PercentDiesel = Shamsi.Num(v); RefreshFigures(); _ = PersistAsync(); }
    partial void OnNoteTextChanged(string v) { if (_pulling) return; Entity.Note = v; _ = PersistAsync(); }

    private decimal Hdr(FuelType f) => IsMoney
        ? (f == FuelType.Diesel ? Entity.RasidMoneyDiesel : Entity.RasidMoneyPetrol)
        : (f == FuelType.Diesel ? Entity.RasidFuelDiesel : Entity.RasidFuelPetrol);

    private ArchiveFigures Figures() =>
        Calc.ArchiveFigures(_rows, IsMoney, Entity.PercentPetrol ?? 0m, Entity.PercentDiesel ?? 0m,
                            Hdr(FuelType.Petrol), Hdr(FuelType.Diesel));

    public string HeadPetrolPercentValue => Shamsi.Money(Entity.PercentPetrol ?? 0m) + "٪";
    public string HeadDieselPercentValue => Shamsi.Money(Entity.PercentDiesel ?? 0m) + "٪";
    public string HeadPetrolCommText => Shamsi.Money(Figures().Petrol.Comm);
    public string HeadDieselCommText => Shamsi.Money(Figures().Diesel.Comm);
    public string HeadPetrolBordText => Shamsi.Money(Figures().Petrol.Bord);
    public string HeadDieselBordText => Shamsi.Money(Figures().Diesel.Bord);
    public string HeadPetrolAlbaqiText => Shamsi.Money(Figures().Petrol.Rem);
    public string HeadDieselAlbaqiText => Shamsi.Money(Figures().Diesel.Rem);
    public string HeadPetrolAlbaqiBrushKey => Figures().Petrol.Rem > 0m ? "Pump.Danger" : "Pump.Ok";
    public string HeadDieselAlbaqiBrushKey => Figures().Diesel.Rem > 0m ? "Pump.Danger" : "Pump.Ok";

    /// <summary>
    /// کادرِ «مقدار رسید» — عددِ دیده‌شده رسیدِ سربرگ + رسیدهای جدول است؛ نوشتن
    /// فقط سهمِ سربرگ را عوض می‌کند تا دوباره‌شماری نشود (‎histRasidBlur‎).
    /// </summary>
    public string HeadPetrolRasidEdit
    {
        get => Shamsi.MoneyOrBlank(Figures().Petrol.Rasid);
        set => SetHeadRasid(FuelType.Petrol, value);
    }
    public string HeadDieselRasidEdit
    {
        get => Shamsi.MoneyOrBlank(Figures().Diesel.Rasid);
        set => SetHeadRasid(FuelType.Diesel, value);
    }
    private void SetHeadRasid(FuelType f, string text)
    {
        var total = Shamsi.Num(text);
        var rowPart = f == FuelType.Diesel ? Figures().Diesel.RowRasid : Figures().Petrol.RowRasid;
        var hdr = Math.Max(0m, total - rowPart);
        if (IsMoney) { if (f == FuelType.Diesel) Entity.RasidMoneyDiesel = hdr; else Entity.RasidMoneyPetrol = hdr; }
        else { if (f == FuelType.Diesel) Entity.RasidFuelDiesel = hdr; else Entity.RasidFuelPetrol = hdr; }
        RefreshFigures();
        _ = PersistAsync();
    }

    public string TotalBordText => Shamsi.Money(Figures().Petrol.Bord + Figures().Diesel.Bord) + " " + UnitText;
    public string TotalRemText => Shamsi.Money(Figures().Petrol.Rem + Figures().Diesel.Rem) + " " + UnitText;

    /// <summary>جمعِ زیرِ هر ستون — روی همان ردیف‌های دیده‌شده (فیلترِ تیل رعایت می‌شود).</summary>
    public IReadOnlyList<TotalCell> TotalCells
    {
        get
        {
            var shown = Rows.Select(r => r.Entity).ToList();
            var t = Calc.SplitTotals(shown).All;
            var bard = shown.Sum(r => Calc.RowBardagi(r));
            var alb = bard - t.Rasid;
            // ⚠️ هر جمع زیرِ ستونِ خودش؛ ستونی که در این دفتر پنهان است، جمعش هم نمی‌آید
            var cells = new List<TotalCell>
            {
                new("مقدار تیل", Shamsi.Money(t.Liters)),
                new("مقدار بردگی", Shamsi.Money(DebtCalculationService.Round0(bard))),
            };
            if (IsMoney) cells.Add(new("رسید", Shamsi.Money(t.Rasid), "Pump.Ok"));
            else cells.Add(new("رسید تیل", Shamsi.Money(t.RasidFuel), "Pump.Ok"));
            cells.Add(new("الباقی", Shamsi.Money(DebtCalculationService.Round0(alb)), alb > 0m ? "Pump.Danger" : "Pump.Ok"));
            return cells;
        }
    }

    public void RefreshFigures()
    {
        foreach (var n in new[]
        {
            nameof(HeadPetrolPercentValue), nameof(HeadDieselPercentValue),
            nameof(HeadPetrolCommText), nameof(HeadDieselCommText),
            nameof(HeadPetrolBordText), nameof(HeadDieselBordText),
            nameof(HeadPetrolAlbaqiText), nameof(HeadDieselAlbaqiText),
            nameof(HeadPetrolAlbaqiBrushKey), nameof(HeadDieselAlbaqiBrushKey),
            nameof(HeadPetrolRasidEdit), nameof(HeadDieselRasidEdit),
            nameof(TotalBordText), nameof(TotalRemText), nameof(TotalCells),
        })
            OnPropertyChanged(n);
    }

    // ── ردیف‌ها ───────────────────────────────────────────────────────────
    public async Task AddRowsAsync(int count) { for (var i = 0; i < count; i++) await AddRowAsync(); }
    public async Task DeleteRowsAsync(int count)
    {
        if (count < 1 || Rows.Count < count) return;
        for (var i = 0; i < count; i++) await DeleteRowAsync(Rows[^1]);
    }

    [RelayCommand]
    private async Task AddRowAsync()
    {
        var r = new DebtRow
        {
            DateShamsi = Shamsi.Today(), DateKey = Shamsi.Key(Shamsi.Today()),
            SortIndex = _rows.Count, ByMoney = IsMoney,
            Fuel = RowFilter == "diesel" ? FuelType.Diesel : FuelType.Petrol,
        };
        _rows.Add(r);
        Rows.Add(new DebtArchiveRowViewModel(r, this));
        OnPropertyChanged(nameof(RowCount));
        RefreshFigures();
        await PersistAsync();
    }

    [RelayCommand]
    private async Task DeleteRowAsync(DebtArchiveRowViewModel? row)
    {
        if (row is null) return;
        _rows.Remove(row.Entity);
        Rows.Remove(row);
        OnPropertyChanged(nameof(RowCount));
        RefreshFigures();
        await PersistAsync();
    }

    [RelayCommand] private Task Delete() => _page.DeleteAsync(this);

    /// <summary>هر تغییری همان لحظه در خودِ آرشیو می‌نشیند (‎RowsJson‎ و سربرگ).</summary>
    public Task PersistAsync() => _host.Debtors.UpdateArchiveAsync(Entity, _rows);

    public async Task FlushAsync()
    {
        foreach (var r in Rows.ToList()) await r.FlushAsync();
    }
}

/// <summary>«🗂️ جدول‌های آرشیو — نامِ شخص» — ‎personArchiveModal‎، تازه‌ترین اول.</summary>
public sealed partial class DebtArchivePageViewModel : ObservableObject
{
    private readonly AppHost _host;
    private readonly DebtSectionViewModel _section;

    public DebtArchivePageViewModel(AppHost host, PersonViewModel person, AccountViewModel acct,
                                    IReadOnlyList<DebtTableArchive> arcs, DebtSectionViewModel section)
    {
        _host = host; _section = section;
        Account = acct;
        Title = "🗂️ جدول‌های آرشیو — " + person.Name
              + (acct.Entity.MainOfDebtorId is null ? " · 📄 " + acct.Title : "");
        // شمارهٔ جدول از ترتیبِ ساخته شدن (قدیمی‌ترین = اول)؛ نمایش تازه‌به‌کهنه مثلِ سایت
        var ordered = arcs.OrderBy(a => a.Id).ToList();
        foreach (var h in ordered.AsEnumerable().Reverse())
            Archives.Add(new DebtArchiveViewModel(h, host, this, ordered.IndexOf(h) + 1));
    }

    public AccountViewModel Account { get; }
    public string Title { get; }
    public ObservableCollection<DebtArchiveViewModel> Archives { get; } = new();
    public bool IsEmpty => Archives.Count == 0;

    [RelayCommand] private Task CloseAsync() => _section.CloseOverlayAsync();

    public async Task FlushAsync()
    {
        foreach (var a in Archives) await a.FlushAsync();
    }

    public Task DeleteAsync(DebtArchiveViewModel a) => CrashGuard.RunAsync("حذفِ آرشیو", async () =>
    {
        if (!await Dialogs.ConfirmAsync("حذفِ جدولِ آرشیو",
                "«" + a.Title + "» پاک شود؟ (به سطلِ زباله می‌رود)")) return;
        await _host.Debtors.DeleteArchiveAsync(a.Entity.Id);
        Archives.Remove(a);
        OnPropertyChanged(nameof(IsEmpty));
        await Account.LoadArchiveCountAsync();
        if (Archives.Count == 0) await _section.CloseOverlayAsync();
    });
}
