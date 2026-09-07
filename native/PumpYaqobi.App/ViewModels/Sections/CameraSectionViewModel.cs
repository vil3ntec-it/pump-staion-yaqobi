using System.Collections.ObjectModel;
using System.Diagnostics;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PumpYaqobi.App.Services;
using PumpYaqobi.Application.Services;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Services.Vision;

namespace PumpYaqobi.App.ViewModels.Sections;

/// <summary>
/// یک کارتِ دوربین. تصویر فقط وقتی گرفته می‌شود که کاربر «پخش» را بزند —
/// ده دوربین که همه با هم پخش شوند، شبکهٔ پمپ را می‌خوابانند.
/// </summary>
public sealed partial class CameraCardViewModel : ObservableObject, IDisposable
{
    private readonly CameraSectionViewModel _owner;
    private readonly CameraFeed _feed = new();

    public CameraCardViewModel(Camera cam, CameraSectionViewModel owner)
    {
        Entity = cam; _owner = owner;
        _feed.FrameArrived += OnFrame;
        _feed.JpegArrived += OnJpeg;
        _feed.Failed += OnFailed;
    }

    public Camera Entity { get; }

    public string Name => Entity.Name ?? "دوربین";
    public string Url => Entity.Url ?? "";

    public CameraKind Kind => CameraService.KindOf(Entity.Url);
    public bool ShowsInApp => CameraService.ShowsInApp(Kind);

    /// <summary>«⛔ RTSP» / «▶️ HLS» / «🖼️ عکسِ زنده» — کاربر باید بداند چه دارد.</summary>
    public string KindText => Kind switch
    {
        CameraKind.None => "بی‌لینک",
        CameraKind.Rtsp => "RTSP",
        CameraKind.Hls => "HLS",
        CameraKind.Video => "ویدیو",
        CameraKind.Image => "عکسِ زنده",
        _ => "صفحهٔ وب",
    };

    [ObservableProperty] private Bitmap? _frame;
    [ObservableProperty] private bool _isLive;
    [ObservableProperty] private string _status = "";

    /// <summary>پیامِ ثابتِ نوعِ لینک، وقتی برنامه خودش نمی‌تواند نشانش دهد.</summary>
    public string Note => CameraService.NoteFor(Kind);

    partial void OnFrameChanged(Bitmap? oldValue, Bitmap? newValue)
    {
        // ⚠️ فریمِ قبلی باید آزاد شود: هر فریم یک بافرِ واقعیِ تصویر است و در
        // یک ساعت پخش، هزاران‌تا می‌شوند. بی این، حافظه بالا می‌رود تا برنامه بایستد.
        if (!ReferenceEquals(oldValue, newValue)) oldValue?.Dispose();
    }

    private void OnFrame(Bitmap bmp) =>
        Dispatcher.UIThread.Post(() =>
        {
            Frame = bmp;
            Status = "";
        });

    private void OnFailed(string msg) => Dispatcher.UIThread.Post(() => Status = msg);

    /// <summary>«▶️/⏸» — پخش فقط با خواستِ کاربر.</summary>
    [RelayCommand]
    private void Toggle()
    {
        if (!ShowsInApp) { _owner.OpenExternally(Url); return; }
        if (IsLive) { Stop(); return; }
        Status = "در حال گرفتنِ تصویر…";
        IsLive = true;
        _feed.Start(Url);
    }

    /// <summary>«⟳» — همان ‎camReload‎: اتصال بسته و از نو باز می‌شود.</summary>
    [RelayCommand]
    private void Reload()
    {
        if (!ShowsInApp) { _owner.OpenExternally(Url); return; }
        _feed.Stop();
        Status = "در حال گرفتنِ تصویر…";
        IsLive = true;
        _feed.Start(Url);
    }

    public void Stop()
    {
        _feed.Stop();
        IsLive = false;
        Scanning = false;
        Frame = null;
        Status = "";
    }

    // ══ پویشِ زندهٔ کیو‌آر ═══════════════════════════════════════════════════
    // همتای ‎camScanStart‎/‎camScanStop‎: کیو‌آر را جلوی همین دوربین بگیرید و
    // لینکِ داخلش در کادرِ «دوربینِ تازه» می‌نشیند.
    //
    // ⚠️ نسخهٔ وب برای این کار دوربینِ **خودِ دستگاه** را باز می‌کرد
    // (‎getUserMedia‎). در ویندوز باز کردنِ وب‌کم کتابخانهٔ گرفتنِ تصویر
    // می‌خواهد که هنوز در برنامه نیست؛ ولی همین دوربینِ شبکه‌ای که تصویرش
    // روی صفحه است، همان کار را می‌کند و هیچ وابستگیِ تازه‌ای نمی‌خواهد.

    /// <summary>در حالِ پویشِ کیو‌آر روی تصویرِ زنده.</summary>
    [ObservableProperty] private bool _scanning;

    /// <summary>
    /// یک فریم در هر لحظه خوانده می‌شود.
    ///
    /// ⚠️ بی این، هر فریمِ MJPEG (سی‌تا در ثانیه) یک رمزگشاییِ کامل راه
    /// می‌انداخت و پردازنده را می‌خورد — و صف هم عقب می‌ماند.
    /// </summary>
    private int _decoding;

    /// <summary>«🔍 پویشِ کیو‌آر» / «⏹ پایانِ پویش».</summary>
    public string ScanText => Scanning ? "⏹ پایانِ پویش" : "🔍 پویشِ کیو‌آر";

    partial void OnScanningChanged(bool value) => OnPropertyChanged(nameof(ScanText));

    /// <summary>پویش فقط روی تصویری که خودِ برنامه نشان می‌دهد معنا دارد.</summary>
    public bool CanScan => ShowsInApp;

    [RelayCommand]
    private void ToggleScan()
    {
        if (!ShowsInApp) return;
        if (Scanning) { Scanning = false; Status = ""; return; }

        if (!IsLive) Toggle();                 // پویش بی تصویر معنا ندارد
        Scanning = true;
        Status = "🔍 کیو‌آر را جلوی دوربین بگیرید…";
    }

    private void OnJpeg(byte[] bytes)
    {
        if (!Scanning) return;

        // اگر رمزگشاییِ فریمِ قبلی هنوز تمام نشده، این فریم رد می‌شود.
        if (Interlocked.Exchange(ref _decoding, 1) == 1) return;

        _ = Task.Run(() =>
        {
            string? text = null;
            try { text = QrReader.DecodeImageBytes(bytes); }
            catch { }
            finally { Interlocked.Exchange(ref _decoding, 0); }

            if (string.IsNullOrWhiteSpace(text)) return;
            Dispatcher.UIThread.Post(() =>
            {
                if (!Scanning) return;         // کاربر همان لحظه پویش را بست
                Scanning = false;
                Status = "";
                _owner.QrFound(text!);
            });
        });
    }

    public void Dispose()
    {
        _feed.FrameArrived -= OnFrame;
        _feed.JpegArrived -= OnJpeg;
        _feed.Failed -= OnFailed;
        _feed.Dispose();
        Frame = null;
    }
}

/// <summary>
/// ══ دوربین‌های مداربسته ═════════════════════════════════════════════════════
/// رونوشتِ ‎renderCameras‎ · ‎camConfirmAdd‎ · ‎camDelete‎ · ‎camReload‎ ·
/// ‎camAddByQRImage‎ (بندِ ۱۵).
///
/// دو راهِ افزودن، هر دو مثلِ نسخهٔ وب: نوشتنِ لینک، یا خواندنِ کیو‌آر از عکس.
/// خواندنِ کیو‌آر بومی است و اینترنت نمی‌خواهد.
///
/// ⚠️ تصویرِ HLS و RTSP را خودِ برنامه پخش نمی‌کند و ادعایش را هم نمی‌کند:
/// دکمهٔ «باز کردن در پخش‌کنندهٔ ویندوز» لینک را به خودِ سیستم می‌دهد. تصویرِ
/// نیامدهٔ بی‌توضیح بدتر از یک دکمهٔ صادق است.
/// </summary>
public sealed partial class CameraSectionViewModel : SectionViewModel
{
    private readonly AppHost _host;

    public CameraSectionViewModel(AppHost host) : base("cameras", "cameras", "دوربین‌ها")
        => _host = host;

    public ObservableCollection<CameraCardViewModel> Cameras { get; } = new();

    [ObservableProperty] private string _newName = "";
    [ObservableProperty] private string _newUrl = "";

    public bool IsEmpty => Cameras.Count == 0;

    protected override Task LoadAsync() => RefreshAsync();

    private async Task RefreshAsync()
    {
        foreach (var c in Cameras) c.Dispose();
        Cameras.Clear();
        foreach (var cam in await _host.Cameras.ListAsync())
            Cameras.Add(new CameraCardViewModel(cam, this));
        OnPropertyChanged(nameof(IsEmpty));
    }

    /// <summary>
    /// بیرون رفتن از بخش، همهٔ پخش‌ها را می‌بندد.
    ///
    /// ⚠️ همان ‎stopAllLocalCams‎ی نسخهٔ وب: بی این، ده اتصالِ باز به دوربین‌ها
    /// تا بسته شدنِ برنامه زنده می‌مانند و شبکهٔ پمپ را می‌گیرند.
    /// </summary>
    public override void OnDeactivated() => StopAll();

    public void StopAll()
    {
        foreach (var c in Cameras) c.Stop();
    }

    [RelayCommand]
    private async Task AddAsync()
    {
        var cam = await _host.Cameras.AddAsync(NewName, NewUrl);
        if (cam is null)
        {
            _host.Toast("لینک دوربین را بنویسید یا با کیو‌آر بگیرید", ToastKind.Error);
            return;
        }
        NewName = ""; NewUrl = "";
        await RefreshAsync();
        _host.Toast("✅ دوربین ذخیره شد", ToastKind.Ok);
    }

    /// <summary>
    /// ‎camAddByQRImage‎ — عکسِ کیو‌آر را می‌خواند و لینکش را در کادر می‌گذارد.
    /// خودش ثبت نمی‌کند: کاربر باید نام بدهد و «ذخیره» بزند، همان‌طور که در
    /// نسخهٔ وب بود.
    /// </summary>
    [RelayCommand]
    private async Task AddByQrAsync()
    {
        var path = await Dialogs.PickImageAsync();
        if (path is null) return;

        var text = await Task.Run(() => QrReader.DecodeFile(path));
        if (string.IsNullOrWhiteSpace(text))
        {
            _host.Toast("کیو‌آر در این عکس پیدا نشد — عکسِ واضح‌تر بگیرید", ToastKind.Error);
            return;
        }

        QrFound(text!);
    }

    /// <summary>
    /// ‎_camQRFound‎ — لینکِ خوانده‌شده در کادر می‌نشیند و نامش را کاربر
    /// می‌نویسد. خودش ثبت نمی‌کند، همان‌طور که در نسخهٔ وب هم نمی‌کرد.
    /// </summary>
    public void QrFound(string text)
    {
        var t = (text ?? "").Trim();
        if (t.Length == 0) return;
        NewName = "";
        NewUrl = t;
        _host.Toast("✅ کیو‌آر خوانده شد — نامِ دوربین را بنویسید و ذخیره کنید", ToastKind.Ok);
    }

    [RelayCommand]
    private async Task DeleteAsync(CameraCardViewModel? card)
    {
        if (card is null) return;
        if (!await Dialogs.ConfirmAsync("حذفِ دوربین", "این دوربین حذف شود؟")) return;
        card.Stop();
        await _host.Cameras.DeleteAsync(card.Entity.Id);
        await RefreshAsync();
    }

    /// <summary>لینک را به خودِ ویندوز می‌دهد — پخش‌کننده یا مرورگرِ پیش‌فرض.</summary>
    public void OpenExternally(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return;
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch
        {
            _host.Toast("باز کردنِ این لینک در ویندوز ممکن نشد", ToastKind.Error);
        }
    }
}
