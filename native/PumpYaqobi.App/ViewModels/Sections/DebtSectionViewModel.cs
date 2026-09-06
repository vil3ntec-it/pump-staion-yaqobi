using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PumpYaqobi.App.Services;
using PumpYaqobi.Application.Localization;
using PumpYaqobi.Application.Services;
using PumpYaqobi.Domain.Entities;

namespace PumpYaqobi.App.ViewModels.Sections;

/// <summary>یک کارتِ قرض‌دار در فهرست — با نشانِ حالِ هر سه دفتر.</summary>
public sealed partial class DebtorCardViewModel : ObservableObject
{
    public DebtorCardViewModel(Debtor d, IReadOnlyList<DebtAccount> accounts, DebtCalculationService calc)
    {
        Entity = d;
        Name = d.Name ?? "";
        Phone = d.Phone ?? "";
        AccountCount = accounts.Count;

        var b = calc.Balances(accounts);
        MoneyText = Shamsi.Money(b.Money);
        PetrolText = Shamsi.Money(b.Petrol);
        DieselText = Shamsi.Money(b.Diesel);

        var st = calc.Status(accounts);
        Status = st.Worst;
        StatusText = st.Worst switch
        {
            DebtStatus.Out => "تمام",
            DebtStatus.Low => "کم",
            DebtStatus.Ok => "روبه‌راه",
            _ => "—",
        };
    }

    public Debtor Entity { get; }
    public string Name { get; }
    public string Phone { get; }
    public int AccountCount { get; }
    public string MoneyText { get; }
    public string PetrolText { get; }
    public string DieselText { get; }
    public DebtStatus Status { get; }
    public string StatusText { get; }

    public bool IsOut => Status == DebtStatus.Out;
    public bool IsLow => Status == DebtStatus.Low;
    public bool IsOk => Status == DebtStatus.Ok;
    public bool HasStatus => Status != DebtStatus.None;

    /// <summary>کلیدِ رنگِ نشان — همان سه رنگی که کارت‌های نسخهٔ وب داشتند.</summary>
    public string StatusBrushKey => Status switch
    {
        DebtStatus.Out => "Pump.Danger",
        DebtStatus.Low => "Pump.Warn",
        DebtStatus.Ok => "Pump.Ok",
        _ => "Pump.Muted",
    };
}

/// <summary>
/// ══ بخشِ قرض‌داران ══════════════════════════════════════════════════════════
/// فهرستِ کارت‌ها، و با کلیک روی هر کارت، صفحهٔ حسابِ همان شخص.
///
/// همهٔ عددها از <see cref="DebtCalculationService"/> می‌آیند — همان سرویسی که
/// با ۲۰۰ حالتِ گرفته‌شده از خودِ نسخهٔ وب آزموده شده. هیچ جمعی این‌جا دستی
/// زده نمی‌شود.
/// </summary>
public sealed partial class DebtSectionViewModel : SectionViewModel
{
    private readonly AppHost _host;
    private readonly bool _noInvoice;
    private List<DebtorCardViewModel> _all = new();

    public DebtSectionViewModel(AppHost host, bool noInvoice = false)
        : base(noInvoice ? "noinv" : "debt",
               noInvoice ? "noinv" : "debt",
               noInvoice ? "شرکت‌ها تیل" : "قرض‌داران")
    { _host = host; _noInvoice = noInvoice; }

    public ObservableCollection<DebtorCardViewModel> Cards { get; } = new();

    [ObservableProperty] private string _search = "";
    [ObservableProperty] private PersonViewModel? _person;
    [ObservableProperty] private string _newName = "";
    [ObservableProperty] private string _newPhone = "";

    public bool IsListVisible => Person is null;

    partial void OnPersonChanged(PersonViewModel? v) => OnPropertyChanged(nameof(IsListVisible));

    partial void OnSearchChanged(string v) => ApplyFilter();

    protected override async Task LoadAsync() => await RefreshAsync();

    public async Task RefreshAsync()
    {
        var people = await _host.Debtors.ListAsync(_noInvoice);
        var accounts = await _host.Debtors.AccountsByDebtorAsync(_noInvoice);
        // خوددرمانیِ ردیف‌ها پیش از حسابِ کارت — همان کاری که نسخهٔ وب هنگامِ
        // کشیدنِ جدول می‌کرد. بدونِ آن، «الباقی»ِ ردیف‌های کهنه صفر می‌ماند و
        // عددِ کارت با عددِ داخلِ حساب فرق می‌کند.
        foreach (var list in accounts.Values)
            foreach (var a in list) _host.Debt.NormalizeAccount(a);

        _all = people.Select(d => new DebtorCardViewModel(
            d, accounts.TryGetValue(d.Id, out var a) ? a : new List<DebtAccount>(), _host.Debt)).ToList();
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var s = Search.Trim();
        Cards.Clear();
        foreach (var c in _all)
            if (s.Length == 0 || c.Name.Contains(s, StringComparison.OrdinalIgnoreCase)
                              || c.Phone.Contains(s, StringComparison.OrdinalIgnoreCase))
                Cards.Add(c);
    }

    [RelayCommand]
    private async Task OpenAsync(DebtorCardViewModel? card)
    {
        if (card is null) return;
        var full = await _host.Debtors.LoadFullAsync(card.Entity.Id);
        if (full is null) return;
        Person = new PersonViewModel(_host, full, this);
    }

    [RelayCommand]
    private async Task BackAsync()
    {
        if (Person is not null) await Person.FlushAsync();
        Person = null;
        await RefreshAsync();
    }

    [RelayCommand]
    private async Task AddDebtorAsync()
    {
        var name = NewName.Trim();
        if (name.Length == 0) return;
        await _host.Debtors.AddDebtorAsync(name, NewPhone.Trim(), _noInvoice);
        NewName = ""; NewPhone = "";
        await RefreshAsync();
    }

    [RelayCommand]
    private async Task DeleteDebtorAsync(DebtorCardViewModel? card)
    {
        if (card is null) return;
        await _host.Debtors.DeleteDebtorAsync(card.Entity.Id);
        await RefreshAsync();
    }
}
