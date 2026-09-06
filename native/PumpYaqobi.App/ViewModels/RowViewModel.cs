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

    /// <summary>
    /// یک خانه عوض شد: مقدار همان لحظه در موجودیت می‌نشیند (تا جمع‌های بالای
    /// صفحه فوری درست شوند) و نوشتن در دیتابیس با کمی تأخیر انجام می‌شود.
    /// </summary>
    protected void Touch()
    {
        if (Loading) return;
        Apply();
        Recalculated?.Invoke();
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
        Apply();
        await SaveAsync();
    }

    /// <summary>مقدارهای جدول را در موجودیت می‌نشاند.</summary>
    protected abstract void Apply();

    protected abstract Task SaveAsync();

    /// <summary>بخش با این خبردار می‌شود که جمع‌ها را دوباره حساب کند.</summary>
    public event Action? Recalculated;
}
