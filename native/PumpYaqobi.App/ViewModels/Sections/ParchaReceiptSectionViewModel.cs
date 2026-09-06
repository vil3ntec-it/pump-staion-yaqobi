using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PumpYaqobi.App.Services;
using PumpYaqobi.Application.Localization;
using PumpYaqobi.Application.Services;
using PumpYaqobi.Domain.Entities;

namespace PumpYaqobi.App.ViewModels.Sections;

/// <summary>
/// یک ردیفِ صفِ «رسید پارچه‌ها».
///
/// «بردگی» همیشه زنده است: مقدار تیل × فی. اگر فی پاک شود، بردگی هم صفر
/// می‌شود — هیچ عددِ کهنه‌ای نمی‌ماند (ریشهٔ باگِ «فی را پاک کردم ولی بردگی
/// سرِ جایش ماند»).
/// </summary>
public sealed partial class ParchaReceiptRowViewModel : RowViewModel
{
    private readonly ParchaReceipt _e;
    private readonly ParchaReceiptSectionViewModel _owner;

    public ParchaReceiptRowViewModel(ParchaReceipt e, ParchaReceiptSectionViewModel owner)
    {
        _e = e; _owner = owner;
        Loading = true;
        _dateShamsi = e.DateShamsi ?? "";
        _account = e.Account ?? "";
        _name = e.Name ?? "";
        _hawala = e.Hawala ?? "";
        _liters = e.Liters;
        _pricePerLiter = e.PricePerLiter;
        _rasid = e.Rasid;
        Loading = false;
    }

    public ParchaReceipt Entity => _e;

    /// <summary>شمارهٔ ردیف در جدول — از ۱.</summary>
    [ObservableProperty] private int _index;

    [ObservableProperty] private string _dateShamsi = "";
    [ObservableProperty] private string _account = "";
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private string _hawala = "";
    [ObservableProperty] private decimal _liters;
    [ObservableProperty] private decimal _pricePerLiter;
    [ObservableProperty] private decimal _rasid;

    partial void OnDateShamsiChanged(string v) => Touch();
    partial void OnAccountChanged(string v) => Touch();
    partial void OnNameChanged(string v) => Touch();
    partial void OnHawalaChanged(string v) => Touch();
    partial void OnLitersChanged(decimal v) { Touch(); Refresh(); }
    partial void OnPricePerLiterChanged(decimal v) { Touch(); Refresh(); }
    partial void OnRasidChanged(decimal v) { Touch(); Refresh(); }

    private void Refresh()
    {
        foreach (var n in new[] { nameof(LitersText), nameof(PriceText), nameof(RasidText),
                                  nameof(BardagiText), nameof(AlbaqiText), nameof(AlbaqiBrushKey) })
            OnPropertyChanged(n);
    }

    public string LitersText { get => Shamsi.Money(Liters); set => Liters = Shamsi.Num(value); }
    public string PriceText { get => Shamsi.Money(PricePerLiter); set => PricePerLiter = Shamsi.Num(value); }
    public string RasidText { get => Shamsi.Money(Rasid); set => Rasid = Shamsi.Num(value); }

    private decimal Bardagi =>
        Math.Round(PostingService.FuelBardagi(Liters, PricePerLiter), 0, MidpointRounding.AwayFromZero);

    public string BardagiText => Shamsi.Money(Bardagi);
    public string AlbaqiText => Shamsi.Money(Bardagi - Rasid);
    public string AlbaqiBrushKey => Bardagi - Rasid > 0 ? "Pump.Danger" : "Pump.Ok";

    protected override void Apply()
    {
        _e.DateShamsi = DateShamsi; _e.Account = Account; _e.Name = Name;
        _e.Hawala = Hawala; _e.Liters = Liters; _e.PricePerLiter = PricePerLiter; _e.Rasid = Rasid;
    }

    protected override Task SaveAsync() => _owner.SaveRowAsync(_e);
}

/// <summary>
/// ══ رسید پارچه‌ها ══════════════════════════════════════════════════════════
/// صفِ رسیدهایی که هنوز واردِ حسابِ کسی نشده‌اند.
///
/// در ستونِ «به حساب» نامِ صاحبِ حساب نوشته می‌شود و با «📥» رسید مستقیم واردِ
/// حسابِ همان شخص می‌شود — نیازی به جست‌وجو یا رفتن داخلِ حساب نیست. اگر همان
/// رسید پیش‌تر از راهِ ورق آمده باشد، دوباره ثبت نمی‌شود.
/// </summary>
public sealed partial class ParchaReceiptSectionViewModel : SectionViewModel
{
    private readonly AppHost _host;

    public ParchaReceiptSectionViewModel(AppHost host)
        : base("rasid", "rasid", "رسید پارچه") => _host = host;

    public ObservableCollection<ParchaReceiptRowViewModel> Rows { get; } = new();

    public bool IsEmpty => Rows.Count == 0;

    protected override Task LoadAsync() => RefreshAsync();

    public async Task RefreshAsync()
    {
        Rows.Clear();
        var i = 0;
        foreach (var e in await _host.ParchaReceipts.ListAsync())
        {
            var vm = new ParchaReceiptRowViewModel(e, this) { Index = ++i };
            Rows.Add(vm);
        }
        OnPropertyChanged(nameof(IsEmpty));
    }

    public Task SaveRowAsync(ParchaReceipt e) => _host.ParchaReceipts.SaveAsync(e);

    [RelayCommand]
    private async Task AddRowAsync()
    {
        await _host.ParchaReceipts.AddAsync();
        await RefreshAsync();
    }

    [RelayCommand]
    private async Task DeleteRowAsync(ParchaReceiptRowViewModel? row)
    {
        if (row is null) return;
        var e = row.Entity;
        var hasData = !string.IsNullOrWhiteSpace(e.Account) || !string.IsNullOrWhiteSpace(e.Name)
                      || e.Liters != 0;
        if (hasData && !await Dialogs.ConfirmAsync("حذفِ رسید", "این رسید حذف شود؟")) return;
        await _host.ParchaReceipts.DeleteAsync(e.Id);
        await RefreshAsync();
    }

    /// <summary>«📥» — همین یک ردیف را واردِ حسابِ صاحبش می‌کند.</summary>
    [RelayCommand]
    private async Task PostRowAsync(ParchaReceiptRowViewModel? row)
    {
        if (row is null) return;
        await row.FlushAsync();
        var (res, person) = await _host.ParchaReceipts.PostAsync(row.Entity.Id);
        switch (res)
        {
            case PostResult.NotFound:
                _host.Toast("⚠️ حساب «" + (row.Account.Length > 0 ? row.Account : row.Name) + "» پیدا نشد",
                            ToastKind.Error);
                return;
            case PostResult.Duplicate:
                _host.Toast("⚠️ یک بار از طرف ورق رسید شده", ToastKind.Warn);
                return;
            default:
                _host.Toast("✅ در حساب " + person + " ثبت شد", ToastKind.Ok);
                await RefreshAsync();
                return;
        }
    }

    /// <summary>«📥 ثبت همه در حساب‌ها».</summary>
    [RelayCommand]
    private async Task PostAllAsync()
    {
        foreach (var r in Rows.ToList()) await r.FlushAsync();
        var rep = await _host.ParchaReceipts.PostAllAsync();
        var msg = "✅ ثبت: " + rep.Ok
                + (rep.Duplicate > 0 ? " — تکراری(ورق): " + rep.Duplicate : "")
                + (rep.NotFound > 0 ? " — حساب نامعلوم: " + rep.NotFound : "");
        _host.Toast(msg, rep.Ok > 0 ? ToastKind.Ok : ToastKind.Warn);
        await RefreshAsync();
    }
}
