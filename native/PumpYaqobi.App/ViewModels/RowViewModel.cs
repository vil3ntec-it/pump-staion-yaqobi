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

    // ══════════════════════════════════════════════════════════════════════
    //  ══ ردیفی که کاربر دست نزده، ذخیره هم نمی‌خواهد ════════════════════════
    // ══════════════════════════════════════════════════════════════════════
    //
    //  گزارشِ صاحب ریپو (۱۴۰۵/۰۶/۲۷): «بیرون شدن از حساب هم کند است.»
    //
    //  سنجشِ تازهٔ ‎enterperf‎ ریشه را نشان داد و عدد هم داد: بستنِ حسابِ یک
    //  شرکت **۲۶۶ دستورِ دیتابیس** می‌زد و ۱٫۴ ثانیه طول می‌کشید — دقیقاً به
    //  شمارِ ردیف‌های جدول. چون ‎FlushAsync‎ **هر** ردیف را ذخیره می‌کرد، حتی
    //  ردیفی که فقط خوانده شده بود.
    //
    //  ⚠️ و زیانش فقط کندی نبود: هر ذخیره ‎PumpDbContext.Version‎ را بالا
    //  می‌برد، و آن شماره ترمزِ نوار، داشبورد و عکسِ ایستگاه است. پس یک
    //  «بستنِ ساده» همهٔ آن‌ها را هم به کارِ دوباره می‌انداخت.
    //
    //  حالا فقط ردیفی ذخیره می‌شود که واقعاً عوض شده باشد. ‎Touch‎ تنها جایی
    //  است که «عوض شد» می‌گوید و خودش هم پشتِ ‎Loading‎ است، پس پر کردنِ اولیهٔ
    //  جدول هیچ ردیفی را کثیف نمی‌کند.

    private bool _dirty;

    /// <summary>این ردیف تغییرِ ذخیره‌نشده دارد؟ (سنجش‌ها می‌خوانند)</summary>
    public bool IsDirty => _dirty;

    /// <summary>
    /// یک خانه عوض شد: مقدار همان لحظه در موجودیت می‌نشیند (تا جمع‌های بالای
    /// صفحه فوری درست شوند) و نوشتن در دیتابیس با کمی تأخیر انجام می‌شود.
    /// </summary>
    protected void Touch()
    {
        if (Loading) return;
        _dirty = true;
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
            if (ct.IsCancellationRequested) return;
            await SaveAsync();
            _dirty = false;
        }
        catch (TaskCanceledException) { /* تایپِ تازه — ذخیرهٔ پیشین لغو شد */ }
    }

    /// <summary>ذخیرهٔ فوری (پیش از بستنِ بخش یا گرفتنِ گزارش).</summary>
    public async Task FlushAsync()
    {
        _debounce?.Cancel();
        if (!_dirty) return;          // ردیفِ دست‌نخورده — چیزی برای نوشتن نیست
        Apply();
        await SaveAsync();
        _dirty = false;
    }

    /// <summary>مقدارهای جدول را در موجودیت می‌نشاند.</summary>
    protected abstract void Apply();

    protected abstract Task SaveAsync();

    /// <summary>بخش با این خبردار می‌شود که جمع‌ها را دوباره حساب کند.</summary>
    public event Action? Recalculated;
}

/// <summary>
/// ردیفی که ویرایش نمی‌شود — مثلِ ردیفِ 📦 حسابِ شرکت که از خریدِ مخزن آمده
/// (‎readonly‎ی سایت). <see cref="Controls.ExcelGrid"/> پیش از باز کردنِ
/// ویرایشگر همین را می‌پرسد. حذفش آزاد است؛ فقط خانه‌هایش تایپ نمی‌شوند.
/// </summary>
public interface ILockedRow
{
    bool IsLocked { get; }
}
