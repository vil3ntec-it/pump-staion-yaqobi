using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PumpYaqobi.App.Services;
using PumpYaqobi.Services.Security;

namespace PumpYaqobi.App.ViewModels.Sections;

/// <summary>
/// یک قفلِ بخش روی صفحهٔ «رمزها و کد» — «مفاد / ضرر» و «زیان ناشی از افزایش
/// قیمت»، هر کدام یک ردیف.
/// </summary>
public sealed partial class SectionLockViewModel : ObservableObject
{
    private readonly SectionLockService _locks;
    private readonly AppHost _host;

    public SectionLockViewModel(AppHost host, string id)
    {
        _host = host; _locks = host.Locks; Id = id;
        Title = SectionLockService.TitleOf(id);
    }

    public string Id { get; }
    public string Title { get; }

    /// <summary>رمزِ تازه — هیچ‌وقت خوانده نمی‌شود، فقط نوشته.</summary>
    [ObservableProperty] private string _password = "";
    [ObservableProperty] private string _confirm = "";
    [ObservableProperty] private string _error = "";

    public bool HasPassword => _locks.HasPassword(Id);

    public string StateText => HasPassword
        ? "🔒 قفل است — بارِ اولِ هر اجرا رمز می‌پرسد"
        : "🔓 قفل ندارد — هر کسی که وارد برنامه شود می‌بیندش";

    public string ActionText => HasPassword ? "عوض کردنِ رمز" : "گذاشتنِ رمز";

    [RelayCommand]
    private void Save()
    {
        Error = "";
        var p = Password.Trim();
        if (p.Length < 4) { Error = "رمز دستِ‌کم چهار نویسه باشد."; return; }
        if (p != Confirm.Trim()) { Error = "دو رمز یکی نیستند."; return; }

        try { _locks.SetPassword(Id, p); }
        catch (Exception ex) { Error = ex.Message; return; }

        Password = ""; Confirm = "";
        Refresh();
        _host.Toast("🔒 رمزِ «" + Title + "» گذاشته شد", ToastKind.Ok);
    }

    [RelayCommand]
    private async Task RemoveAsync()
    {
        if (!HasPassword) return;
        if (!await Dialogs.ConfirmAsync("برداشتنِ قفل",
                "قفلِ «" + Title + "» برداشته شود؟ از آن پس هر کسی که وارد برنامه شود می‌بیندش."))
            return;

        _locks.ClearPassword(Id);
        Refresh();
        _host.Toast("🔓 قفلِ «" + Title + "» برداشته شد", ToastKind.Warn);
    }

    /// <summary>«همین حالا دوباره قفل کن» — بی بسته شدنِ برنامه.</summary>
    [RelayCommand]
    private void Relock()
    {
        _locks.Relock(Id);
        _host.Toast("🔒 «" + Title + "» دوباره قفل شد", ToastKind.Info);
    }

    public void Refresh()
    {
        OnPropertyChanged(nameof(HasPassword));
        OnPropertyChanged(nameof(StateText));
        OnPropertyChanged(nameof(ActionText));
    }
}

/// <summary>
/// ══ ۱) رمزها و کد ══════════════════════════════════════════════════════════
/// صفحهٔ اولِ تنظیمات. خواستهٔ صریحِ صاحب ریپو (۱۴۰۵/۰۶/۲۸):
/// «تو این بخش بشه رمزِ برنامه رو عوض کرد، رمز برای مفاد و ضرر تعیین کرد.»
///
/// ⚠️ رمزِ برنامه همان رمزِ اپِ کارمندان هم هست: اپ همان هشِ مدیر را می‌گیرد
/// و خودش مقایسه می‌کند (<see cref="PumpYaqobi.Services.Security.AuthService.AdminPasswordHash"/>).
/// پس عوض کردنِ رمز این‌جا، رمزِ گوشی‌ها را هم عوض می‌کند — و همین‌جا نوشته
/// شده تا کاربر غافلگیر نشود.
/// </summary>
public sealed partial class KeysSectionViewModel : SectionViewModel
{
    private readonly AppHost _host;

    public KeysSectionViewModel(AppHost host) : base("keys", "settings", "رمزها و کد")
    {
        _host = host;
        Locks = new ObservableCollection<SectionLockViewModel>(
            SectionLockService.Ids.Select(id => new SectionLockViewModel(host, id)));
    }

    // ── رمزِ خودِ برنامه ──────────────────────────────────────────────────────
    [ObservableProperty] private string _current = "";
    [ObservableProperty] private string _next = "";
    [ObservableProperty] private string _confirm = "";
    [ObservableProperty] private string _appError = "";
    [ObservableProperty] private string _appDone = "";

    /// <summary>قفلِ بخش‌ها — فقط «مفاد/ضرر» و «زیان افزایش قیمت».</summary>
    public ObservableCollection<SectionLockViewModel> Locks { get; }

    /// <summary>کدِ اپِ کارمندان — همان کدی که در «پروفایل» هم هست، فقط برای دیدن.</summary>
    public string AccessCode => AppSettings.Load().CloudAccessCode is { Length: > 0 } c ? c : "—";

    [RelayCommand]
    private void ChangeAppPassword()
    {
        AppError = ""; AppDone = "";
        if (Current.Length == 0) { AppError = "رمزِ فعلی را بنویسید."; return; }
        if (Next.Trim().Length < 4) { AppError = "رمزِ تازه دستِ‌کم چهار نویسه باشد."; return; }
        if (Next.Trim() != Confirm.Trim()) { AppError = "دو رمزِ تازه یکی نیستند."; return; }

        try { _host.Auth.ChangePassword("admin", Current, Next.Trim()); }
        catch (UnauthorizedAccessException) { AppError = "رمزِ فعلی درست نیست."; return; }
        catch (Exception ex) { AppError = ex.Message; return; }

        Current = ""; Next = ""; Confirm = "";
        AppDone = "✅ رمزِ برنامه عوض شد — رمزِ اپِ کارمندان هم همین شد.";
        _host.Toast(AppDone, ToastKind.Ok);
    }

    public override Task OnActivatedAsync()
    {
        foreach (var l in Locks) l.Refresh();
        OnPropertyChanged(nameof(AccessCode));
        return Task.CompletedTask;
    }
}
