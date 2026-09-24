using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PumpYaqobi.App.Services;
using PumpYaqobi.Application.Security;
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

    /// <summary>
    /// رمزِ <b>فعلیِ</b> همین بخش (یا رمزِ برنامه) — فقط وقتی بخش رمز دارد.
    /// ⛔ بی آن، هر کسی که پای برنامهٔ باز می‌نشست رمزِ «مفاد» را عوض یا
    /// برمی‌داشت و خودش می‌دیدش.
    /// </summary>
    [ObservableProperty] private string _current = "";

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

        try
        {
            if (HasPassword)
            {
                if (Current.Length == 0) { Error = "رمزِ فعلیِ این بخش (یا رمزِ برنامه) را بنویسید."; return; }
                if (!Allowed(_locks.ChangePassword(Id, Current, p, AppPassword))) return;
            }
            else _locks.SetPassword(Id, p);
        }
        catch (Exception ex) { Error = ErrorText.Friendly(ex); return; }

        Current = ""; Password = ""; Confirm = "";
        Refresh();
        _host.Toast("🔒 رمزِ «" + Title + "» گذاشته شد", ToastKind.Ok);
    }

    [RelayCommand]
    private async Task RemoveAsync()
    {
        Error = "";
        if (!HasPassword) return;
        if (Current.Length == 0) { Error = "برای برداشتنِ قفل، رمزِ فعلیِ این بخش (یا رمزِ برنامه) را بنویسید."; return; }
        if (!await Dialogs.ConfirmAsync("برداشتنِ قفل",
                "قفلِ «" + Title + "» برداشته شود؟ از آن پس هر کسی که وارد برنامه شود می‌بیندش."))
            return;

        if (!Allowed(_locks.RemovePassword(Id, Current, AppPassword))) return;
        Current = "";
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

    /// <summary>رمزِ خودِ برنامه هم کلیدِ همین قفل است — شرحش در <c>SectionLockService.Authorize</c>.</summary>
    private bool AppPassword(string typed)
    {
        var h = _host.Auth.AdminPasswordHash();
        return !string.IsNullOrEmpty(h) && PasswordHasher.Verify(typed, h);
    }

    /// <summary>نتیجهٔ سنجش ⇒ جملهٔ کاربر. راست یعنی «ادامه بده».</summary>
    private bool Allowed(SectionLockService.Check c)
    {
        switch (c)
        {
            case SectionLockService.Check.Ok: return true;
            case SectionLockService.Check.Wait:
                Error = $"چند بار رمزِ نادرست زده شد — {_locks.WaitSeconds(Id)} ثانیهٔ دیگر دوباره امتحان کنید.";
                return false;
            default:
                Current = "";
                Error = "رمزِ فعلی درست نیست.";
                return false;
        }
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

    // ══ «برنامه بی رمز باز می‌شود، مگر خودِ کاربر رمزی بگذارد» ═══════════════
    //
    // خواستهٔ صریحِ صاحب ریپو (۱۴۰۵/۰۷/۰۷): «برنامه بدون رمز باشه، چون کسایی
    // که تازه به برنامه می‌رسن نباید رمز داشته باشه و خود طرف برای خودش رمز
    // خودشو می‌زنه.»
    //
    // ⛔ پس این صفحه تنها جایی است که رمزِ برنامه **ساخته** می‌شود. صفحهٔ قفل
    // دیگر رمز نمی‌سازد (‎LockViewModel‎)، و تا این‌جا رمزی گذاشته نشود هیچ
    // صفحهٔ قفلی دیده نمی‌شود.

    /// <summary>رمزی گذاشته شده؟ کلِ شکلِ این کارت از همین می‌آید.</summary>
    public bool HasAppPassword => _host.Auth.HasPassword();

    public string AppLockStateText => HasAppPassword
        ? "🔒 برنامه با رمز باز می‌شود"
        : "🔓 برنامه بی رمز باز می‌شود — هر کسی که پای این کامپیوتر بنشیند دفتر را می‌بیند";

    public string AppActionText => HasAppPassword ? "عوض کردنِ رمزِ برنامه" : "گذاشتنِ رمز";

    /// <summary>
    /// ⚠️ بی رمز، اپِ کارمندان هم باز نمی‌شود: آن‌چه منتشر می‌شود هشِ **خالی**
    /// است و گوشی خودش می‌گوید صاحبِ پمپ هنوز رمزی نگذاشته. کیو‌آر و کدِ پمپ
    /// روی کاغذ می‌گردند؛ دفترِ پمپ نباید بی رمز از شبکه خوانده شود.
    /// </summary>
    public string AppLockHint => HasAppPassword
        ? "همین رمز، رمزِ اپِ کارمندان روی گوشی هم هست. با عوض کردنش، گوشی‌ها هم از همان لحظه رمزِ تازه می‌خواهند."
        : "تا رمزی نگذارید، اپِ کارمندان روی گوشی هم باز نمی‌شود. رمزی که این‌جا بگذارید همان رمزِ اپِ گوشی است.";

    [RelayCommand]
    private void ChangeAppPassword()
    {
        AppError = ""; AppDone = "";
        var had = HasAppPassword;

        //  ⚠️ بی رمزِ فعلی، «رمزِ فعلی را بنویسید» فقط کاربر را گیج می‌کرد —
        //  رمزی نیست که بنویسد.
        if (had && Current.Length == 0) { AppError = "رمزِ فعلی را بنویسید."; return; }
        if (Next.Trim().Length < 4) { AppError = "رمز دستِ‌کم چهار نویسه باشد."; return; }
        if (Next.Trim() != Confirm.Trim()) { AppError = "دو رمز یکی نیستند."; return; }

        try
        {
            if (had) _host.Auth.ChangePassword("admin", Current, Next.Trim());
            else _host.Auth.SetFirstPassword(Next.Trim());
        }
        catch (UnauthorizedAccessException) { AppError = "رمزِ فعلی درست نیست."; return; }
        catch (Exception ex) { AppError = ErrorText.Friendly(ex); return; }

        Current = ""; Next = ""; Confirm = "";
        AppDone = had
            ? "✅ رمزِ برنامه عوض شد — رمزِ اپِ کارمندان هم همین شد."
            : "✅ رمز گذاشته شد — از این پس برنامه با همین رمز باز می‌شود، و اپِ کارمندان هم.";
        RefreshAppLock();
        _host.Toast(AppDone, ToastKind.Ok);
    }

    /// <summary>
    /// رمز را برمی‌دارد و برنامه دوباره بی‌رمز باز می‌شود — با رمزِ فعلی.
    /// ⛔ پیامدش گفته می‌شود، نه «مطمئنید؟»: قاعدهٔ همیشگیِ پنجره‌های تأیید.
    /// </summary>
    [RelayCommand]
    private async Task RemoveAppPasswordAsync()
    {
        AppError = ""; AppDone = "";
        if (!HasAppPassword) return;
        if (Current.Length == 0) { AppError = "برای برداشتنِ رمز، رمزِ فعلی را بنویسید."; return; }

        if (!await Dialogs.ConfirmAsync("برداشتنِ رمزِ برنامه",
                "رمز برداشته شود؟ از آن پس برنامه بی هیچ رمزی باز می‌شود و اپِ کارمندان روی گوشی هم دیگر باز نمی‌شود. "
                + "هیچ داده‌ای پاک نمی‌شود."))
            return;

        try { _host.Auth.ClearPassword(Current); }
        catch (UnauthorizedAccessException) { AppError = "رمزِ فعلی درست نیست."; return; }
        catch (Exception ex) { AppError = ErrorText.Friendly(ex); return; }

        Current = ""; Next = ""; Confirm = "";
        AppDone = "🔓 رمز برداشته شد — برنامه بی رمز باز می‌شود.";
        RefreshAppLock();
        _host.Toast(AppDone, ToastKind.Warn);
    }

    private void RefreshAppLock()
    {
        OnPropertyChanged(nameof(HasAppPassword));
        OnPropertyChanged(nameof(AppLockStateText));
        OnPropertyChanged(nameof(AppActionText));
        OnPropertyChanged(nameof(AppLockHint));
    }

    public override Task OnActivatedAsync()
    {
        foreach (var l in Locks) l.Refresh();
        RefreshAppLock();
        OnPropertyChanged(nameof(AccessCode));
        return Task.CompletedTask;
    }
}
