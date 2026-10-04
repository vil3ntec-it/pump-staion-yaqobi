using CommunityToolkit.Mvvm.ComponentModel;

namespace PumpYaqobi.App.Services;

public enum ToastKind { Ok, Error, Warn, Info }

/// <summary>
/// همان «توست»ِ نسخهٔ وب — یک نوارِ کوتاه پایینِ صفحه.
/// بدونِ تارِ پس‌زمینه و بدونِ سایهٔ سنگین (خواستهٔ صریحِ صاحب ریپو).
/// </summary>
public sealed partial class ToastService : ObservableObject
{
    private CancellationTokenSource? _hide;

    [ObservableProperty] private string _text = "";
    [ObservableProperty] private bool _visible;
    [ObservableProperty] private ToastKind _kind = ToastKind.Info;

    public void Show(string text, ToastKind kind = ToastKind.Info, int ms = 2600)
    {
        //  ⛔ همیشه روی نخِ رابط (۱۴۰۵/۰۷/۱۶): ناشر، پشتیبان‌گیر و به‌روزرسانیِ خودکار از
        //  نخِ پس‌زمینه صدا می‌زنند؛ ‎_hide‎ی بی‌قفل آن‌جا توستِ تازه‌تر را زود پنهان می‌کرد.
        if (!UiThread.CheckAccess())
        {
            UiThread.Post(() => Show(text, kind, ms));
            return;
        }
        Text = text; Kind = kind; Visible = true;
        _hide?.Cancel();
        _hide?.Dispose();
        var cts = new CancellationTokenSource();
        _hide = cts;
        _ = HideLaterAsync(ms, cts.Token);
    }

    private async Task HideLaterAsync(int ms, CancellationToken ct)
    {
        try { await Task.Delay(ms, ct); if (!ct.IsCancellationRequested) Visible = false; }
        catch (TaskCanceledException) { }
    }
}
