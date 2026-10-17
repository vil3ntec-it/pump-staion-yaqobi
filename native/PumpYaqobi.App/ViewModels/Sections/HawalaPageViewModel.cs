using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PumpYaqobi.App.Controls;
using PumpYaqobi.App.Services;
using PumpYaqobi.Application.Localization;
using PumpYaqobi.Application.Services;
using PumpYaqobi.Domain.Entities;

namespace PumpYaqobi.App.ViewModels.Sections;

// ══ ⛽ تیل چکنهٔ حواله — صفحهٔ جداگانه روی حسابِ قرض‌دار (۱۴۰۵/۰۷/۲۵) ═══════════════
// خواستهٔ صاحب ریپو: «در حسابِ هر قرض‌دار، بغلِ کشوییِ فیصدی، بخشی به اسمِ تیل چکنهٔ
// حواله باشد؛ بزنم صفحهٔ جدید بیاید، حواله را ثبت کنم، و هر چه تیل از آن حواله رفت کم
// شود… ۷۰ درصد که رفت و وقتی تمام شد، نقطهٔ سرخ.» و «در ورق‌ها هم که آن حواله زده
// می‌شود از همان‌جا کم شود… برای هر قرض‌دار مالِ خودش.»
// ⚠️ برداشت‌ها نوشته نمی‌شوند، شمرده می‌شوند: ردیف‌های همین حساب با همین شمارهٔ حواله
// (دستی یا از ورق). شرح: ‎HawalaCalc‎.

public sealed partial class HawalaItemViewModel : ObservableObject
{
    private readonly HawalaPageViewModel _page;

    public HawalaItemViewModel(HawalaQuota q, IReadOnlyList<DebtRow> rows, HawalaPageViewModel page,
                               DebtStatusInfo? account = null)
    {
        Entity = q; _page = page;
        Takings = HawalaCalc.Takings(q.Number, rows)
            .Select(t => new HawalaTakeViewModel(t)).ToList();
        _status = HawalaCalc.Of(q, rows, account);
        _litersEdit = Shamsi.Money(q.Liters);
    }

    public HawalaQuota Entity { get; }
    public IReadOnlyList<HawalaTakeViewModel> Takings { get; }
    private HawalaCalc.Status _status;

    public string Title => "حوالهٔ " + Entity.Number + (string.IsNullOrWhiteSpace(Entity.Note) ? "" : " — " + Entity.Note);
    public string DateText => "ثبت: " + (Entity.DateShamsi ?? "—");
    public string TotalText => Shamsi.Money(_status.Total) + " لیتر";
    public string UsedText => Shamsi.Money(_status.Used) + " لیتر";
    public string LeftText => IsBurned
        ? "0 لیتر (" + Shamsi.Money(_status.Left) + " لیتر سوخت)"
        : Shamsi.Money(Math.Max(0m, _status.Left)) + " لیتر"
          + (_status.Left < 0m ? " (" + Shamsi.Money(-_status.Left) + " بیشتر رفت)" : "");
    public bool IsBurned => _status.State == HawalaCalc.State.Burned;
    public string ShareText => Shamsi.Money(Math.Round(_status.Share * 100m, 0)) + "٪ رفت";
    public double SharePercent => IsBurned ? 100d : (double)Math.Clamp(_status.Share * 100m, 0m, 100m);
    public bool Alert => _status.Alert;
    public bool IsDone => _status.State is HawalaCalc.State.Done or HawalaCalc.State.Burned;
    public string StateText => _status.State switch
    {
        HawalaCalc.State.Done => "🔴 تمام شد",
        HawalaCalc.State.Burned => "🔴 تمام شد — قرض‌دار دیگر تیل ندارد، مانده سوخت",
        HawalaCalc.State.Warn => "🔴 ۷۰٪ رفت — کم مانده",
        _ => "🟢 باز",
    };
    public string StateBrushKey => _status.Alert ? "Pump.Danger" : "Pump.Ok";
    public string TakeCountText => Takings.Count == 0 ? "هنوز برداشتی نشده" : Shamsi.Money(Takings.Count) + " برداشت";
    public bool HasTakings => Takings.Count > 0;

    [ObservableProperty] private bool _isOpen;
    [ObservableProperty] private string _litersEdit;

    [RelayCommand] private void Toggle() => IsOpen = !IsOpen;
    [RelayCommand] private Task DeleteAsync() => _page.DeleteAsync(this);
    [RelayCommand] private Task SaveLitersAsync() => _page.SaveLitersAsync(this, LitersEdit);
}

public sealed class HawalaTakeViewModel
{
    public HawalaTakeViewModel(HawalaCalc.Take t) { Liters = t.Liters; Date = t.Date; Name = t.Name; }
    public decimal Liters { get; }
    public string Date { get; }
    public string Name { get; }
    public string LitersText => Shamsi.Money(Liters) + " لیتر";
}

public sealed partial class HawalaPageViewModel : ObservableObject
{
    private readonly AppHost _host;
    private readonly DebtSectionViewModel _section;

    public HawalaPageViewModel(AppHost host, PersonViewModel person, AccountViewModel acct, DebtSectionViewModel section)
    {
        _host = host; _section = section; Account = acct;
        Title = "⛽ تیل چکنهٔ حواله — " + person.Name
              + (acct.Entity.MainOfDebtorId is null ? " · 📄 " + acct.Title : "");
    }

    public AccountViewModel Account { get; }
    public string Title { get; }
    public ObservableCollection<HawalaItemViewModel> Items { get; } = new();
    public bool IsEmpty => Items.Count == 0;

    [ObservableProperty] private string _newNumber = "";
    [ObservableProperty] private string _newLiters = "";
    [ObservableProperty] private string _newNote = "";

    public string SummaryText
    {
        get
        {
            if (Items.Count == 0) return "";
            var open = Items.Count(i => !i.IsDone);
            var alerts = Items.Count(i => i.Alert);
            return Shamsi.Money(Items.Count) + " حواله · " + Shamsi.Money(open) + " باز"
                   + (alerts > 0 ? " · 🔴 " + Shamsi.Money(alerts) + " کم‌مانده یا تمام" : "");
        }
    }

    public async Task LoadAsync()
    {
        var qs = await _host.Hawalas.ListAsync(Account.Entity.Id);
        var rows = qs.Count == 0 ? new List<DebtRow>() : await _host.Hawalas.AccountRowsAsync(Account.Entity.Id);
        var open = Items.Where(i => i.IsOpen).Select(i => i.Entity.Id).ToHashSet();
        //  ⛔ حسابِ خودِ قرض‌دار سقفِ حواله است — شرح بالای ‎HawalaCalc‎
        var acct = _host.Debt.Status(new[] { Account.Entity });
        Items.Clear();
        //  کم‌مانده‌ها و تمام‌شده‌ها اول — همان‌هایی که نقطهٔ سرخ دارند
        foreach (var q in qs)
            Items.Add(new HawalaItemViewModel(q, rows, this, acct) { IsOpen = open.Contains(q.Id) });
        var sorted = Items.OrderByDescending(i => i.Alert && !i.IsDone).ThenBy(i => i.IsDone).ThenBy(i => i.Entity.SortIndex).ToList();
        Items.Clear();
        foreach (var i in sorted) Items.Add(i);
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(SummaryText));
        await Account.LoadHawalaAsync();
    }

    [RelayCommand]
    private Task AddAsync() => CrashGuard.RunAsync("ثبتِ حواله", async () =>
    {
        var liters = Shamsi.Num(NewLiters);
        var err = await _host.Hawalas.AddAsync(Account.Entity.Id, NewNumber, liters, NewNote);
        if (err is not null) { _host.Toast(err, ToastKind.Warn); return; }
        _host.Toast("حوالهٔ " + HawalaCalc.Norm(NewNumber) + " ثبت شد", ToastKind.Ok);
        NewNumber = ""; NewLiters = ""; NewNote = "";
        await LoadAsync();
    });

    public Task SaveLitersAsync(HawalaItemViewModel it, string text) => CrashGuard.RunAsync("مقدارِ حواله", async () =>
    {
        var v = Shamsi.Num(text);
        if (v <= 0m) { _host.Toast("مقدارِ تیلِ حواله باید بیشتر از صفر باشد", ToastKind.Warn); return; }
        if (v == it.Entity.Liters) return;
        await _host.Hawalas.UpdateAsync(it.Entity.Id, v, it.Entity.Note);
        await LoadAsync();
    });

    public Task DeleteAsync(HawalaItemViewModel it) => CrashGuard.RunAsync("حذفِ حواله", async () =>
    {
        if (!await Dialogs.ConfirmAsync("حذفِ حواله",
                "«" + it.Title + "» از فهرستِ حواله‌ها برداشته شود؟ ردیف‌های حساب دست نمی‌خورند.")) return;
        await _host.Hawalas.DeleteAsync(it.Entity.Id);
        await LoadAsync();
    });

    [RelayCommand] private Task CloseAsync() => _section.CloseOverlayAsync();
}
