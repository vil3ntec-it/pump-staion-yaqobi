using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PumpYaqobi.App.Services;
using PumpYaqobi.Application.Localization;
using PumpYaqobi.Application.Services;
using PumpYaqobi.Domain.Entities;

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
    partial void OnIsBardagiChanged(bool v) => Touch();
    partial void OnTitleChanged(string v) => Touch();
    partial void OnAmountChanged(decimal v) { Touch(); OnPropertyChanged(nameof(AmountText)); }
    partial void OnIsUsdChanged(bool v) => Touch();
    partial void OnNoteChanged(string v) => Touch();

    /// <summary>مبلغ برای نمایش و تایپ: با جداکنندهٔ هزارگان دیده می‌شود و
    /// هنگامِ تایپ، هم رقمِ فارسی می‌پذیرد هم لاتین هم کاما.</summary>
    public string AmountText
    {
        get => Shamsi.Money(Amount);
        set => Amount = Shamsi.Num(value);
    }

    /// <summary>«بردگی» یا «ماندگی» — برای کشویِ ستونِ نوع.</summary>
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

    protected override async Task SaveAsync()
    {
        _e.DateShamsi = DateShamsi;
        _e.Kind = IsBardagi ? SafeEntryKind.Bardagi : SafeEntryKind.Mandagi;
        _e.Title = Title;
        _e.Amount = Amount;
        _e.Currency = IsUsd ? Currency.Usd : Currency.Afn;
        _e.Note = Note;
        await _owner.SaveRowAsync(this);
    }
}

/// <summary>
/// ══ بخشِ گاوصندوق ══════════════════════════════════════════════════════════
/// همان صفحهٔ <c>renderSafe</c>ِ نسخهٔ وب: فهرستِ بردگی/ماندگیِ یک ماه و سه
/// کادرِ جمع. دو ارز هرگز با هم جمع نمی‌شوند — قاعدهٔ ثابتِ برنامه.
/// </summary>
public sealed partial class SafeSectionViewModel : SectionViewModel
{
    private readonly AppHost _host;

    public SafeSectionViewModel(AppHost host) : base("safe", "safe", "گاوصندوق")
    {
        _host = host;
        _month = Shamsi.ThisMonth();
    }

    public ObservableCollection<SafeRowViewModel> Rows { get; } = new();
    public ObservableCollection<string> Months { get; } = new();

    [ObservableProperty] private string _month;
    [ObservableProperty] private SafeSummary _summary;

    public string BardagiAfn => Shamsi.Money(Summary.Bardagi.Afn);
    public string BardagiUsd => Shamsi.Money(Summary.Bardagi.Usd);
    public string MandagiAfn => Shamsi.Money(Summary.Mandagi.Afn);
    public string MandagiUsd => Shamsi.Money(Summary.Mandagi.Usd);
    public string NetAfn => Shamsi.Money(Summary.Net.Afn);
    public string NetUsd => Shamsi.Money(Summary.Net.Usd);

    partial void OnMonthChanged(string value) => _ = ReloadRowsAsync();

    partial void OnSummaryChanged(SafeSummary value)
    {
        OnPropertyChanged(nameof(BardagiAfn)); OnPropertyChanged(nameof(BardagiUsd));
        OnPropertyChanged(nameof(MandagiAfn)); OnPropertyChanged(nameof(MandagiUsd));
        OnPropertyChanged(nameof(NetAfn)); OnPropertyChanged(nameof(NetUsd));
    }

    protected override async Task LoadAsync()
    {
        Months.Clear();
        foreach (var m in await _host.SafeData.MonthsAsync()) Months.Add(m);
        if (!Months.Contains(Month)) Months.Insert(0, Month);
        await ReloadRowsAsync();
    }

    private async Task ReloadRowsAsync()
    {
        var list = await _host.SafeData.ListAsync(Month);
        Rows.Clear();
        foreach (var e in list) Rows.Add(new SafeRowViewModel(e, this));
        Recalc();
    }

    /// <summary>جمع‌ها از همان سرویسِ آزمودهٔ لایهٔ Application می‌آیند.</summary>
    public void Recalc() => Summary = _host.Safe.Summarize(Rows.Select(r => r.Entity));

    internal async Task SaveRowAsync(SafeRowViewModel row)
    {
        if (row.Entity.Id == 0) await _host.SafeData.AddAsync(row.Entity);
        else await _host.SafeData.UpdateAsync(row.Entity);
        Recalc();
    }

    [RelayCommand]
    private async Task AddRowAsync()
    {
        var e = new SafeEntry { DateShamsi = Shamsi.Today(), Kind = SafeEntryKind.Mandagi };
        await _host.SafeData.AddAsync(e);
        if (Shamsi.MonthKey(e.DateShamsi) != Month) Month = Shamsi.MonthKey(e.DateShamsi);
        else Rows.Add(new SafeRowViewModel(e, this));
        Recalc();
    }

    [RelayCommand]
    private async Task DeleteRowAsync(SafeRowViewModel? row)
    {
        if (row is null) return;
        await _host.SafeData.DeleteAsync(row.Entity.Id);
        Rows.Remove(row);
        Recalc();
    }
}
