using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using PumpYaqobi.App.Controls;
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

    public string LitersText { get => Shamsi.MoneyOrBlank(Liters); set => Liters = Shamsi.Num(value); }
    public string PriceText { get => Shamsi.MoneyOrBlank(PricePerLiter); set => PricePerLiter = Shamsi.Num(value); }
    public string RasidText { get => Shamsi.MoneyOrBlank(Rasid); set => Rasid = Shamsi.Num(value); }

    internal decimal Bardagi =>
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
public sealed partial class ParchaReceiptSectionViewModel : SectionViewModel, IRowBatchHost
{
    private readonly AppHost _host;

    public ParchaReceiptSectionViewModel(AppHost host)
        : base("rasid", "rasid", "رسید پارچه")
    {
        _host = host;
        // «📝 یادداشت این بخش» — همتای ‎.sec-note-box‎ی سایت. کلیدش همان
        // کلیدِ نسخهٔ وب است تا نوت‌های واردشده سرِ جای خودشان بنشینند.
        Notes = new SectionNotesViewModel(Id, host.SectionNotes,
            (m, ok) => host.Toast(m, ok ? ToastKind.Ok : ToastKind.Warn));
    }

    public ObservableCollection<ParchaReceiptRowViewModel> Rows { get; } = new();

    public bool IsEmpty => Rows.Count == 0;

    /// <summary>
    /// ردیفِ «جمله»ی ته جدول — همتای ‎&lt;tfoot class="xls-foot"&gt;‎ی سایت.
    /// روی صفِ رسیدها حساب می‌شود، همان‌طور که دیده می‌شود.
    /// </summary>
    public IReadOnlyList<TotalCell> TotalCells
    {
        get
        {
            var liters = Rows.Sum(r => r.Liters);
            var bardagi = Rows.Sum(r => r.Bardagi);
            var rasid = Rows.Sum(r => r.Rasid);
            var albaqi = bardagi - rasid;
            return new[]
            {
                new TotalCell("مقدار تیل", Shamsi.Money(liters)),
                new TotalCell("بردگی", Shamsi.Money(bardagi)),
                new TotalCell("رسید", Shamsi.Money(rasid), "Pump.Ok"),
                new TotalCell("الباقی", Shamsi.Money(albaqi), albaqi > 0m ? "Pump.Danger" : "Pump.Ok"),
            };
        }
    }

    /// <summary>هر ویرایشِ ردیف، «جمله» را هم تازه می‌کند.</summary>
    public void RefreshTotals() => OnPropertyChanged(nameof(TotalCells));

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
        RefreshTotals();
    }

    public Task SaveRowAsync(ParchaReceipt e)
    {
        RefreshTotals();
        return _host.ParchaReceipts.SaveAsync(e);
    }

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

    public int RowCount => Rows.Count;

    /// <summary>
    /// ‎Ctrl+عدد‎ / ‎Shift+عدد‎ روی جدولِ رسیدها.
    ///
    /// حذفِ گروهی عمداً از راهِ ‎DeleteRowAsync‎ نمی‌رود: آن برای ردیفِ پرشده
    /// پنجرهٔ «مطمئنی؟» باز می‌کند، و ‎n‎ پنجرهٔ پشتِ‌سرِ هم یعنی کارِ خوابیده.
    /// همان کاری که ‎_kbNoConfirm‎ در نسخهٔ وب می‌کرد: در حذفِ گروهی پرسش
    /// نمی‌شود — خودِ عددی که کاربر تایپ کرده تصمیمش است.
    /// </summary>
    public async Task AddRowsAsync(int count)
    {
        for (var i = 0; i < count; i++) await _host.ParchaReceipts.AddAsync();
        await RefreshAsync();
    }

    public async Task DeleteRowsAsync(int count)
    {
        if (count < 1 || Rows.Count < count) return;
        for (var i = 0; i < count; i++)
            await _host.ParchaReceipts.DeleteAsync(Rows[Rows.Count - 1 - i].Entity.Id);
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
