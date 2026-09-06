using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PumpYaqobi.App.Services;
using PumpYaqobi.Application.Localization;
using PumpYaqobi.Application.Services;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;

namespace PumpYaqobi.App.ViewModels.Sections;

/// <summary>یک ردیفِ دفترِ قرض‌دار.</summary>
public sealed partial class DebtRowViewModel : RowViewModel
{
    private readonly DebtRow _r;
    private readonly AccountViewModel _owner;

    public DebtRowViewModel(DebtRow r, AccountViewModel owner)
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
    partial void OnFuelChanged(FuelType v) { Touch(); OnPropertyChanged(nameof(FuelText)); }
    partial void OnLitersChanged(decimal v) { Touch(); Refresh(); }
    partial void OnPriceChanged(decimal v) { Touch(); Refresh(); }
    partial void OnManualBardagiChanged(decimal v) { Touch(); Refresh(); }
    partial void OnRasidChanged(decimal v) { Touch(); Refresh(); }
    partial void OnRasidFuelChanged(decimal v) { Touch(); Refresh(); }

    private void Refresh()
    {
        OnPropertyChanged(nameof(LitersText)); OnPropertyChanged(nameof(PriceText));
        OnPropertyChanged(nameof(ManualBardagiText)); OnPropertyChanged(nameof(RasidText));
        OnPropertyChanged(nameof(RasidFuelText)); OnPropertyChanged(nameof(BardagiText));
    }

    public string LitersText { get => Shamsi.Money(Liters); set => Liters = Shamsi.Num(value); }
    public string PriceText { get => Shamsi.Money(Price); set => Price = Shamsi.Num(value); }
    public string ManualBardagiText { get => Shamsi.Money(ManualBardagi); set => ManualBardagi = Shamsi.Num(value); }
    public string RasidText { get => Shamsi.Money(Rasid); set => Rasid = Shamsi.Num(value); }
    public string RasidFuelText { get => Shamsi.Money(RasidFuel); set => RasidFuel = Shamsi.Num(value); }

    /// <summary>بردگیِ پولیِ همین ردیف — از همان سرویسِ آزموده، نه حسابِ دستی.</summary>
    public string BardagiText => Shamsi.Money(_owner.Calc.RowBardagi(_r));

    public string FuelText
    {
        get => Fuel.ToPersian();
        set => Fuel = value == "دیزل" ? FuelType.Diesel : FuelType.Petrol;
    }

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
    }

    protected override Task SaveAsync() => _owner.SaveRowAsync(_r);
}

/// <summary>
/// یک حسابِ قرض‌دار (اصلی یا فرعی).
/// ⚠️ «واحد پول» و «واحد تیل» دو دفترِ کاملاً جدا هستند. عوض کردنِ واحد،
/// دفترِ دیده‌شده را عوض می‌کند — نه اینکه ردیف‌ها را از یکی به دیگری ببرد.
/// </summary>
public sealed partial class AccountViewModel : ObservableObject, IRowBatchHost
{
    private readonly AppHost _host;
    private readonly PersonViewModel _person;

    public AccountViewModel(AppHost host, DebtAccount a, PersonViewModel person)
    {
        _host = host; _person = person; Entity = a;
        _isMoney = a.Mode.IsMoney();
        _percentPetrol = host.Debt.PercentOf(a, FuelType.Petrol);
        _percentDiesel = host.Debt.PercentOf(a, FuelType.Diesel);
        _rasidFuelPetrol = a.RasidFuelPetrol;
        _rasidFuelDiesel = a.RasidFuelDiesel;
        _rasidMoneyPetrol = a.RasidMoneyPetrol;
        _rasidMoneyDiesel = a.RasidMoneyDiesel;
        BuildRows();
    }

    public DebtAccount Entity { get; }
    public DebtCalculationService Calc => _host.Debt;
    public string Title => Entity.MainOfDebtorId != null ? "حسابِ اصلی" : (Entity.Name ?? "حسابِ فرعی");

    public ObservableCollection<DebtRowViewModel> Rows { get; } = new();

    [ObservableProperty] private bool _isMoney;
    [ObservableProperty] private decimal _percentPetrol;
    [ObservableProperty] private decimal _percentDiesel;
    [ObservableProperty] private decimal _rasidFuelPetrol;
    [ObservableProperty] private decimal _rasidFuelDiesel;
    [ObservableProperty] private decimal _rasidMoneyPetrol;
    [ObservableProperty] private decimal _rasidMoneyDiesel;

    partial void OnIsMoneyChanged(bool v)
    {
        Entity.Mode = v ? LedgerMode.Money : LedgerMode.Fuel;
        BuildRows();
        _ = _host.Debtors.UpdateAccountAsync(Entity);
        _person.Recalc();
    }

    /// <summary>⚠️ فیصدیِ پطرول و دیزل دو چیزِ جدا هستند و هرگز یکی نمی‌شوند.</summary>
    partial void OnPercentPetrolChanged(decimal v)
    {
        _host.Debt.SetPercent(Entity, FuelType.Petrol, v == 0m ? null : v);
        SaveAccount();
        OnPropertyChanged(nameof(PercentPetrolText));
    }

    partial void OnPercentDieselChanged(decimal v)
    {
        _host.Debt.SetPercent(Entity, FuelType.Diesel, v == 0m ? null : v);
        SaveAccount();
        OnPropertyChanged(nameof(PercentDieselText));
    }

    partial void OnRasidFuelPetrolChanged(decimal v) { Entity.RasidFuelPetrol = v; SaveAccount(); OnPropertyChanged(nameof(RasidFuelPetrolText)); }
    partial void OnRasidFuelDieselChanged(decimal v) { Entity.RasidFuelDiesel = v; SaveAccount(); OnPropertyChanged(nameof(RasidFuelDieselText)); }
    partial void OnRasidMoneyPetrolChanged(decimal v) { Entity.RasidMoneyPetrol = v; SaveAccount(); OnPropertyChanged(nameof(RasidMoneyPetrolText)); }
    partial void OnRasidMoneyDieselChanged(decimal v) { Entity.RasidMoneyDiesel = v; SaveAccount(); OnPropertyChanged(nameof(RasidMoneyDieselText)); }

    // نوشته‌های ورودی: عددِ خام بی «۰٫۰»، و پذیرشِ رقمِ فارسی و کاما
    public string PercentPetrolText { get => Shamsi.Money(PercentPetrol); set => PercentPetrol = Shamsi.Num(value); }
    public string PercentDieselText { get => Shamsi.Money(PercentDiesel); set => PercentDiesel = Shamsi.Num(value); }
    public string RasidFuelPetrolText { get => Shamsi.Money(RasidFuelPetrol); set => RasidFuelPetrol = Shamsi.Num(value); }
    public string RasidFuelDieselText { get => Shamsi.Money(RasidFuelDiesel); set => RasidFuelDiesel = Shamsi.Num(value); }
    public string RasidMoneyPetrolText { get => Shamsi.Money(RasidMoneyPetrol); set => RasidMoneyPetrol = Shamsi.Num(value); }
    public string RasidMoneyDieselText { get => Shamsi.Money(RasidMoneyDiesel); set => RasidMoneyDiesel = Shamsi.Num(value); }

    private void SaveAccount()
    {
        _ = _host.Debtors.UpdateAccountAsync(Entity);
        _person.Recalc();
    }

    /// <summary>دفترِ دیده‌شده = دفترِ واحدِ همین حساب.</summary>
    private void BuildRows()
    {
        // خوددرمانیِ دادهٔ کهنه پیش از کشیدنِ جدول — همان کاری که renderPersonRows
        // می‌کرد. اگر چیزی عوض شد، همان‌جا ذخیره می‌شود تا دوباره لازم نشود.
        if (_host.Debt.NormalizeAccount(Entity)) _ = PersistHealedAsync();

        Rows.Clear();
        foreach (var r in Entity.ActiveRows().OrderBy(r => r.SortIndex).ThenBy(r => r.Id))
        {
            var vm = new DebtRowViewModel(r, this);
            vm.Recalculated += _person.Recalc;
            Rows.Add(vm);
        }
    }

    private async Task PersistHealedAsync()
    {
        foreach (var r in Entity.FuelRows.Concat(Entity.MoneyRows))
            await _host.Debtors.SaveRowAsync(r);
    }

    public async Task SaveRowAsync(DebtRow r)
    {
        _host.Debt.NormalizeRow(r);          // بردگی و الباقی، دقیقاً مثلِ نسخهٔ وب
        await _host.Debtors.SaveRowAsync(r);
        _person.Recalc();
    }

    [RelayCommand]
    private async Task AddRowAsync()
    {
        var r = new DebtRow
        {
            DateShamsi = Shamsi.Today(),
            DateKey = Shamsi.Key(Shamsi.Today()),
            SortIndex = Entity.ActiveRows().Count,
            ByMoney = IsMoney,
        };
        if (IsMoney) { r.MoneyAccountId = Entity.Id; Entity.MoneyRows.Add(r); }
        else { r.FuelAccountId = Entity.Id; Entity.FuelRows.Add(r); }
        await _host.Debtors.SaveRowAsync(r);
        var vm = new DebtRowViewModel(r, this);
        vm.Recalculated += _person.Recalc;
        Rows.Add(vm);
        _person.Recalc();
    }

    [RelayCommand]
    private async Task DeleteRowAsync(DebtRowViewModel? row)
    {
        if (row is null) return;
        await _host.Debtors.DeleteRowAsync(row.Entity.Id);
        Entity.FuelRows.Remove(row.Entity);
        Entity.MoneyRows.Remove(row.Entity);
        Rows.Remove(row);
        _person.Recalc();
    }

    public int RowCount => Rows.Count;

    /// <summary>‎Ctrl+عدد‎ / ‎Shift+عدد‎ — افزودن و برداشتنِ گروهیِ ردیف.
    /// حذف فقط وقتی ردیفِ کافی باشد؛ وگرنه هیچ.</summary>
    public async Task AddRowsAsync(int count)
    {
        for (var i = 0; i < count; i++) await AddRowAsync();
    }

    public async Task DeleteRowsAsync(int count)
    {
        if (count < 1 || Rows.Count < count) return;
        for (var i = 0; i < count; i++) await DeleteRowAsync(Rows[^1]);
    }

    public async Task FlushAsync()
    {
        foreach (var r in Rows.ToList()) await r.FlushAsync();
    }
}

/// <summary>
/// ══ صفحهٔ حسابِ یک قرض‌دار ══════════════════════════════════════════════════
/// حسابِ اصلی و حساب‌های فرعی، هر کدام دفترِ خودش. عددهای بالای صفحه
/// جمعِ همهٔ حساب‌هاست و از همان سرویسِ آزموده می‌آید.
/// </summary>
public sealed partial class PersonViewModel : ObservableObject, IRowBatchHost
{
    private readonly AppHost _host;
    private readonly DebtSectionViewModel _section;

    public PersonViewModel(AppHost host, Debtor d, DebtSectionViewModel section)
    {
        _host = host; _section = section; Entity = d;
        foreach (var a in d.AllAccounts()) Accounts.Add(new AccountViewModel(host, a, this));
        _current = Accounts.FirstOrDefault();
        Recalc();
    }

    public Debtor Entity { get; }
    public string Name => Entity.Name ?? "";
    public string Phone => Entity.Phone ?? "";

    public ObservableCollection<AccountViewModel> Accounts { get; } = new();

    [ObservableProperty] private AccountViewModel? _current;
    [ObservableProperty] private string _moneyText = "";
    [ObservableProperty] private string _petrolText = "";
    [ObservableProperty] private string _dieselText = "";
    [ObservableProperty] private string _statusText = "";

    /// <summary>جمع‌ها و حال — همیشه از روی همهٔ حساب‌ها، نه فقط حسابِ باز.</summary>
    public void Recalc()
    {
        var accounts = Accounts.Select(a => a.Entity).ToList();
        var b = _host.Debt.Balances(accounts);
        MoneyText = Shamsi.Money(b.Money);
        PetrolText = Shamsi.Money(b.Petrol);
        DieselText = Shamsi.Money(b.Diesel);
        StatusText = _host.Debt.Status(accounts).Worst switch
        {
            DebtStatus.Out => "تمام شده",
            DebtStatus.Low => "رو به تمام",
            DebtStatus.Ok => "روبه‌راه",
            _ => "—",
        };
    }

    /// <summary>
    /// میانبرهای ردیف به «حسابِ باز» می‌روند — حسابِ اصلی یا هر حسابِ فرعی که
    /// همین حالا جلوی چشمِ کاربر است. دو دفترِ «تیل» و «پول» جدا می‌مانند،
    /// چون هر حساب ردیف‌های خودش را دارد.
    /// </summary>
    public int RowCount => Current?.RowCount ?? 0;
    public Task AddRowsAsync(int count) => Current?.AddRowsAsync(count) ?? Task.CompletedTask;
    public Task DeleteRowsAsync(int count) => Current?.DeleteRowsAsync(count) ?? Task.CompletedTask;

    /// <summary>برگشت به فهرست — از راهِ خودِ بخش، تا ذخیرهٔ نیمه‌کاره جا نماند.</summary>
    [RelayCommand]
    private Task BackAsync() => _section.BackCommand.ExecuteAsync(null);

    [RelayCommand]
    private async Task AddSubAccountAsync()
    {
        var a = await _host.Debtors.AddSubAccountAsync(Entity.Id, null);
        Entity.SubAccounts.Add(a);
        var vm = new AccountViewModel(_host, a, this);
        Accounts.Add(vm);
        Current = vm;                 // فوراً دیده می‌شود — همان باگی که در نسخهٔ وب بود
        Recalc();
    }

    [RelayCommand]
    private async Task DeleteSubAccountAsync(AccountViewModel? a)
    {
        if (a is null || a.Entity.MainOfDebtorId != null) return;
        await _host.Debtors.DeleteAccountAsync(a.Entity.Id);
        Entity.SubAccounts.Remove(a.Entity);
        Accounts.Remove(a);
        Current = Accounts.FirstOrDefault();
        Recalc();
    }

    public async Task FlushAsync()
    {
        foreach (var a in Accounts) await a.FlushAsync();
    }
}
