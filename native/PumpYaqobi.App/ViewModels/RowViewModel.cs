using CommunityToolkit.Mvvm.ComponentModel;

namespace PumpYaqobi.App.ViewModels;

/// <summary>
/// پایهٔ ردیف‌های جدول. هر خانه که کاربر عوض کند، فقط همان ردیف ذخیره
/// می‌شود — نه کلِ جدول (بندِ ۲۸: «ویرایش یک Cell نباید کل جدول را Reload کند»).
///
/// ذخیره با کمی تأخیر انجام می‌شود تا تایپِ پیاپی ده‌ها نوشتن در دیتابیس نسازد.
/// </summary>
public abstract partial class RowViewModel : ObservableObject
{
    private CancellationTokenSource? _debounce;

    /// <summary>وقتی true باشد، تغییرِ خانه‌ها ذخیره نمی‌شود (هنگامِ پر کردنِ اولیه).</summary>
    protected bool Loading { get; set; }

    protected void Touch()
    {
        if (Loading) return;
        _debounce?.Cancel();
        var cts = new CancellationTokenSource();
        _debounce = cts;
        _ = DelayedSaveAsync(cts.Token);
    }

    private async Task DelayedSaveAsync(CancellationToken ct)
    {
        try
        {
            await Task.Delay(350, ct);
            if (!ct.IsCancellationRequested) await SaveAsync();
        }
        catch (TaskCanceledException) { /* تایپِ تازه — ذخیرهٔ پیشین لغو شد */ }
    }

    /// <summary>ذخیرهٔ فوری (پیش از بستنِ بخش یا گرفتنِ گزارش).</summary>
    public async Task FlushAsync()
    {
        _debounce?.Cancel();
        await SaveAsync();
    }

    protected abstract Task SaveAsync();
}
