using CommunityToolkit.Mvvm.ComponentModel;
using PumpYaqobi.App.Services;

namespace PumpYaqobi.App.ViewModels;

/// <summary>
/// پایهٔ ردیف‌های جدول. هر خانه که کاربر عوض کند، فقط همان ردیف ذخیره
/// می‌شود — نه کلِ جدول (بندِ ۲۸: «ویرایش یک Cell نباید کل جدول را Reload کند»).
///
/// ذخیره با کمی تأخیر انجام می‌شود تا تایپِ پیاپی ده‌ها نوشتن در دیتابیس نسازد.
/// </summary>
public abstract partial class RowViewModel : ObservableObject, IPendingWrite
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
        //  ⛔ از همین لحظه این ردیف «در صف»ِ نگهبان است: بسته شدنِ برنامه،
        //  عوض شدنِ دفتر و ‎Ctrl+S‎ همه از همان یک فهرست می‌نویسند.
        SaveGuard.Track(this);
        Apply();
        Recalculated?.Invoke();
        _debounce?.Cancel();
        var cts = new CancellationTokenSource();
        _debounce = cts;
        _ = DelayedSaveAsync(cts.Token);
    }

    /// <summary>
    /// ══ «هر چیزی که می‌نویسم باید درجا ثبت بشه» ═══════════════════════════
    ///
    /// خواستهٔ صاحب ریپو (۱۴۰۵/۰۷/۱۲)، حتی «اگر برق یک‌باره برود». سنجهٔ
    /// ‎persist crash‎ نشان داد متنِ در حالِ تایپ **همان لحظه** به ردیف
    /// می‌رسد (اتصالِ جدول ‎PropertyChanged‎ است) و تنها فاصله همین مکث
    /// بود: ۳۵۰ میلی‌ثانیه پس از آخرین کلید. برق که در همان پنجره برود، آن
    /// چند حرف رفته بود.
    ///
    /// ⚠️ صفر نیست، عمداً: صفر یعنی یک تراکنشِ دیتابیس برای هر کلید. ۱۵۰
    /// کمتر از فاصلهٔ دو کلیدِ تایپِ معمولی است، پس عملاً هر کلمه همان‌جا
    /// می‌نشیند. سنجه‌های سرعت (‎waraqperf‎، ‎ledgerperf‎، ‎scrollperf‎) با
    /// همین عدد دویده‌اند.
    /// </summary>
    public const int SaveDelayMs = 150;

    private async Task DelayedSaveAsync(CancellationToken ct)
    {
        try
        {
            await Task.Delay(SaveDelayMs, ct);
            if (ct.IsCancellationRequested) return;
        }
        catch (TaskCanceledException) { return; }   // تایپِ تازه — این یکی لغو شد
        await WriteAsync();
    }

    // ══════════════════════════════════════════════════════════════════════
    //  ══ نوشتنی که شکست بخورد، دوباره تلاش می‌کند و ساکت هم نمی‌ماند ════════
    // ══════════════════════════════════════════════════════════════════════
    //
    //  ⛔ پیش از این، ذخیره با ‎_ = …‎ رها می‌شد و هر استثنایی «مشاهده‌نشده»
    //  بود: ردیف تا ابد کثیف می‌ماند، هیچ تلاشِ دوباره‌ای نبود، و کاربر هیچ
    //  نمی‌دید. یک قفلِ لحظه‌ایِ فایل از سوی ضدِ ویروس = یک ردیفِ گم‌شده، و
    //  صفحه‌ای که سالم به نظر می‌رسید. همان «کلکِ دروغ»ی که این‌جا قدغن است.
    //
    //  ⚠️ سه تلاش با فاصلهٔ فزاینده، و بعد **گفتن**. ردیف کثیف می‌ماند تا
    //  بسته شدنِ برنامه یا ‎Ctrl+S‎ دوباره امتحانش کند — پاکش نمی‌کنیم.
    private static readonly int[] Backoff = { 0, 400, 1_500 };

    private int _writing;

    private async Task WriteAsync()
    {
        //  دو نوشتنِ هم‌زمانِ یک ردیف یعنی دو تراکنشِ رقیب روی یک سطر.
        if (Interlocked.Exchange(ref _writing, 1) == 1) return;
        try
        {
            for (var i = 0; i < Backoff.Length; i++)
            {
                if (!_dirty) return;
                if (Backoff[i] > 0)
                    try { await Task.Delay(Backoff[i]); } catch { }
                try
                {
                    Apply();
                    await SaveAsync();
                    _dirty = false;
                    SaveGuard.ReportSaved();
                    return;
                }
                catch (Exception e)
                {
                    if (i == Backoff.Length - 1)
                        SaveGuard.ReportFailure("یک ردیف ذخیره نشد (" + e.GetType().Name + ")");
                }
            }
        }
        finally { Interlocked.Exchange(ref _writing, 0); }
    }

    /// <summary>
    /// ذخیرهٔ فوری (پیش از بستنِ بخش، بسته شدنِ برنامه، یا گرفتنِ گزارش).
    /// ⛔ هیچ‌وقت استثنا بیرون نمی‌دهد — وگرنه یک ردیفِ خراب جلوی نوشتنِ
    /// بقیه را می‌گرفت و بسته شدنِ برنامه هم می‌ماسید.
    /// </summary>
    public async Task FlushAsync()
    {
        _debounce?.Cancel();
        if (!_dirty) return;          // ردیفِ دست‌نخورده — چیزی برای نوشتن نیست
        await WriteAsync();
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
