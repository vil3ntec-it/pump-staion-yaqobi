using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using PumpYaqobi.App.Controls;
using CommunityToolkit.Mvvm.Input;
using PumpYaqobi.App.Services;
using PumpYaqobi.Application.Localization;
using PumpYaqobi.Application.Services;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;

namespace PumpYaqobi.App.ViewModels.Sections;

/// <summary>یک تخلیهٔ ثبت‌شده.</summary>
public sealed class TankerRowViewModel
{
    public TankerRowViewModel(TankerUnload u, int index) { Entity = u; Index = index; }

    public TankerUnload Entity { get; }
    public int Index { get; }

    public string FuelText => Entity.Fuel == FuelType.Diesel ? "🟤 دیزل" : "⛽ پطرول";
    public string DateShamsi => Entity.DateShamsi ?? "";
    public string Driver => Entity.Driver ?? "";
    public string Note => Entity.Note ?? "";
    public string ManifestText => Shamsi.Money(Entity.Manifest);
    public string ActualText => Shamsi.Money(Entity.Actual);

    internal decimal Diff => TankDipService.UnloadDifference(Entity);

    /// <summary>«کم‌آمد ۶۰» یا «✅ کامل» — همان دو حالتِ نسخهٔ وب.</summary>
    public string DiffText => Diff < 0m ? "کم‌آمد " + Shamsi.Money(-Diff) : "✅ کامل";
    public string DiffBrushKey => Diff < 0m ? "Pump.Danger" : "Pump.Ok";
}

/// <summary>
/// ══ تخلیهٔ تانکر ═══════════════════════════════════════════════════════════
/// رونوشتِ ‎confirmTankerLog‎ · ‎deleteTankerLog‎ · ‎_renderTankerPanel‎.
///
/// دفترِ «چقدر در بارنامه نوشته بود و چقدر واقعاً تحویل گرفتیم». به موجودیِ
/// مخزن دست نمی‌زند — موجودی از خودِ خریدها می‌آید — ولی کم‌آمدش در گزارشِ
/// ماهانه جمع می‌شود.
/// </summary>
public sealed partial class TankerSectionViewModel : SectionViewModel
{
    private readonly AppHost _host;

    public TankerSectionViewModel(AppHost host) : base("tanker", "storage", "تخلیهٔ تانکر")
        => _host = host;

    public ObservableCollection<TankerRowViewModel> Rows { get; } = new();

    /// <summary>
    /// ردیفِ «جمله»ی ته جدول — بارنامه، تحویل، و جمعِ کم‌آمد (همان عددی که
    /// «گزارش پایان ماه» هم می‌خواند).
    /// </summary>
    public IReadOnlyList<TotalCell> TotalCells
    {
        get
        {
            var manifest = Rows.Sum(r => r.Entity.Manifest);
            var actual = Rows.Sum(r => r.Entity.Actual);
            var short_ = Rows.Sum(r => r.Diff < 0m ? -r.Diff : 0m);
            return new[]
            {
                new TotalCell("تخلیه‌ها", Shamsi.Money(Rows.Count)),
                new TotalCell("بارنامه", Shamsi.Money(manifest)),
                new TotalCell("تحویل", Shamsi.Money(actual)),
                new TotalCell("کم‌آمد", Shamsi.Money(short_), short_ > 0m ? "Pump.Danger" : "Pump.Ok"),
            };
        }
    }

    [ObservableProperty] private bool _isDiesel;
    [ObservableProperty] private string _manifest = "";
    [ObservableProperty] private string _actual = "";
    [ObservableProperty] private string _driver = "";
    [ObservableProperty] private string _note = "";

    public bool IsEmpty => Rows.Count == 0;
    public string FuelToggleText => IsDiesel ? "🟤 دیزل" : "⛽ پطرول";

    partial void OnIsDieselChanged(bool v) => OnPropertyChanged(nameof(FuelToggleText));

    /// <summary>پیش‌نمایشِ زندهٔ کم‌آمد، پیش از ثبت.</summary>
    public string PreviewText
    {
        get
        {
            var m = Shamsi.Num(Manifest);
            var a = Shamsi.Num(Actual);
            if (m <= 0m || a <= 0m) return "بارنامه و تحویل هر دو لازم است";
            var d = a - m;
            return d < 0m ? "⚠️ کم‌آمدِ تحویل: " + Shamsi.Money(-d) + " لیتر"
                 : d > 0m ? "➕ بیشتر از بارنامه: " + Shamsi.Money(d) + " لیتر"
                 : "✅ تحویل کامل";
        }
    }

    public string PreviewBrushKey =>
        Shamsi.Num(Manifest) <= 0m || Shamsi.Num(Actual) <= 0m ? "Pump.Muted"
        : Shamsi.Num(Actual) - Shamsi.Num(Manifest) < 0m ? "Pump.Danger" : "Pump.Ok";

    partial void OnManifestChanged(string v) => RefreshPreview();
    partial void OnActualChanged(string v) => RefreshPreview();

    private void RefreshPreview()
    {
        OnPropertyChanged(nameof(PreviewText));
        OnPropertyChanged(nameof(PreviewBrushKey));
    }

    protected override Task LoadAsync() => RefreshAsync();

    public override Task OnActivatedAsync() => RefreshAsync();

    private async Task RefreshAsync()
    {
        Rows.Clear();
        var i = 0;
        foreach (var u in await _host.Tools.UnloadsAsync()) Rows.Add(new TankerRowViewModel(u, ++i));
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(TotalCells));
    }

    [RelayCommand]
    private void ToggleFuel() => IsDiesel = !IsDiesel;

    [RelayCommand]
    private async Task AddAsync()
    {
        var u = await _host.Tools.AddUnloadAsync(
            IsDiesel ? FuelType.Diesel : FuelType.Petrol,
            Shamsi.Num(Manifest), Shamsi.Num(Actual), Driver.Trim(), Note.Trim());

        if (u is null)
        {
            _host.Toast("لیتر بارنامه و لیتر تحویل هر دو لازم است", ToastKind.Error);
            return;
        }

        var d = TankDipService.UnloadDifference(u);
        _host.Toast(d < 0m ? "⚠️ " + Shamsi.Money(-d) + " لیتر کم‌آمدِ تحویل"
                           : "✅ ثبت شد — تحویل کامل",
                    d < 0m ? ToastKind.Warn : ToastKind.Ok);

        Manifest = ""; Actual = ""; Note = "";
        await RefreshAsync();
    }

    [RelayCommand]
    private async Task DeleteAsync(TankerRowViewModel? row)
    {
        if (row is null) return;
        if (!await Dialogs.ConfirmAsync("حذفِ تخلیه", "این تخلیه حذف شود؟")) return;
        await _host.Tools.DeleteUnloadAsync(row.Entity.Id);
        await RefreshAsync();
    }
}
