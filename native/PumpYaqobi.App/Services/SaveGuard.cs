using System.Collections.Concurrent;

namespace PumpYaqobi.App.Services;

/// <summary>چیزی که نوشتهٔ ذخیره‌نشده دارد و می‌شود همین حالا نشاندش.</summary>
public interface IPendingWrite
{
    /// <summary>نوشتهٔ ذخیره‌نشده دارد؟</summary>
    bool IsDirty { get; }

    /// <summary>همین حالا بنویس. ⛔ هیچ‌وقت استثنا بیرون نمی‌دهد.</summary>
    Task FlushAsync();
}

/// <summary>
/// ══ «هیچ نوشته‌ای گم نشود» ══════════════════════════════════════════════════
///
/// گزارشِ صریحِ صاحب ریپو (۱۴۰۵/۰۷/۱۲): «من چندین ورق رو پر کردم اما با
/// اعتماد تو همه‌شون پاک شدن و هیچ چیزی ثبت نشده بودن… یک ربات الترا
/// اختصاصی درست کن که همه چی که نوشته یا عوض می‌شه در جا ثبت کنه… یارو اگه
/// داشت جمله‌ای می‌نوشت از برنامه در جا بره بیرون همون‌ها باید ثبت شده
/// باشن، یا هم دیدی یک دفعه برق رفت.»
///
/// ⛔ <b>سه سوراخِ واقعی بود، و هر سه بی‌صدا:</b>
///
///   ۱) <b>هیچ‌جا نوشته‌ها پیش از بسته شدنِ برنامه نوشته نمی‌شدند.</b> در کلِ
///      برنامه یک شنوندهٔ <c>Closing</c> یا <c>ShutdownRequested</c> نبود
///      (گشته شد: صفر). پس هر ردیفی که پشتِ تأخیرِ ۳۵۰ میلی‌ثانیه‌ای بود —
///      و هر خانه‌ای که هنوز در حالِ ویرایش بود — با زدنِ ✕ می‌رفت.
///
///   ۲) <b>ذخیرهٔ شکست‌خورده هیچ‌وقت دوباره تلاش نمی‌کرد.</b>
///      <c>RowViewModel</c> ذخیره را با <c>_ = …</c> رها می‌کرد؛ استثنا
///      «مشاهده‌نشده» می‌شد، ردیف تا ابد کثیف می‌ماند و کاربر هیچ نمی‌دید.
///      یک قفلِ لحظه‌ایِ فایل از سوی ضدِ ویروس = یک ردیفِ گم‌شده.
///
///   ۳) <b>و شکست هیچ‌وقت گفته نمی‌شد.</b> همان «کلکِ دروغ»ی که در این ریپو
///      قدغن است: صفحه سالم به نظر می‌رسید و دیسک خالی بود.
///
/// ⛔ <b>این کلاس تنها جای شمردن و نشاندنِ نوشته‌های در صف است.</b> راهِ دومی
/// نسازید — دو فهرستِ در صف یعنی روزی یکی خالی است و آن یکی پر.
///
/// ⚠️ <b>مرجعِ ضعیف</b>: صفحه‌ای که بسته و دور انداخته شد نباید این‌جا زنده
/// بماند. ردیفِ رفته خودش از فهرست می‌افتد.
/// </summary>
public static class SaveGuard
{
    private static readonly object Gate = new();
    private static readonly List<WeakReference<IPendingWrite>> Pending = new();

    /// <summary>
    /// شکستِ نوشتن — پوسته این را به کاربر می‌گوید.
    /// ⛔ بی‌صدا نماند: کاربری که نداند نوشته‌اش ننشسته، فردا دفترِ خراب دارد.
    /// </summary>
    public static event Action<string>? Failed;

    /// <summary>هر بار که نوشتنی واقعاً روی دیسک نشست (سنجه‌ها می‌شمارند).</summary>
    public static event Action? Saved;

    internal static void ReportFailure(string why) => Failed?.Invoke(why);
    internal static void ReportSaved() => Saved?.Invoke();

    /// <summary>
    /// این نوشته را بپا. تکراری ثبت نمی‌شود و ثبتش ارزان است — از
    /// <c>RowViewModel.Touch()</c> صدا زده می‌شود، یعنی با هر خانه‌ای که
    /// کاربر عوض می‌کند.
    /// </summary>
    public static void Track(IPendingWrite w)
    {
        lock (Gate)
        {
            for (var i = Pending.Count - 1; i >= 0; i--)
            {
                if (!Pending[i].TryGetTarget(out var t)) { Pending.RemoveAt(i); continue; }
                if (ReferenceEquals(t, w)) return;
            }
            Pending.Add(new WeakReference<IPendingWrite>(w));
        }
    }

    /// <summary>چند نوشتهٔ ذخیره‌نشده در صف است؟ (سنجه‌ها و نوارِ پایین)</summary>
    public static int DirtyCount
    {
        get
        {
            var n = 0;
            foreach (var w in Snapshot()) if (w.IsDirty) n++;
            return n;
        }
    }

    private static List<IPendingWrite> Snapshot()
    {
        var live = new List<IPendingWrite>();
        lock (Gate)
        {
            for (var i = Pending.Count - 1; i >= 0; i--)
            {
                if (Pending[i].TryGetTarget(out var t)) live.Add(t);
                else Pending.RemoveAt(i);
            }
        }
        return live;
    }

    /// <summary>
    /// ══ همه را همین حالا بنویس ═════════════════════════════════════════════
    /// پیش از بسته شدنِ برنامه، پیش از عوض کردنِ دفتر، و با <c>Ctrl+S</c>.
    ///
    /// ⛔ هیچ‌وقت استثنا بیرون نمی‌دهد و هیچ‌وقت نمی‌ماسد: هر نوشته خودش
    /// <c>FlushAsync</c>ِ بی‌استثنا دارد، و سقفِ وقت جلوی «برنامه بسته
    /// نمی‌شود» را می‌گیرد.
    /// </summary>
    /// <returns>چند نوشته پس از تلاش هنوز ننشسته‌اند.</returns>
    public static async Task<int> FlushAllAsync(TimeSpan? timeout = null)
    {
        var all = Snapshot().Where(w => w.IsDirty).ToList();
        if (all.Count == 0) return 0;

        var work = Task.WhenAll(all.Select(w => w.FlushAsync()));
        var cap = timeout ?? TimeSpan.FromSeconds(8);
        try { await work.WaitAsync(cap); }
        catch { /* سقفِ وقت یا شکست — پایین شمرده می‌شود */ }

        var left = all.Count(w => w.IsDirty);
        if (left > 0)
            ReportFailure(left + " نوشته ذخیره نشد — اینترنت لازم نیست، ولی دیسک باید " +
                          "نوشتنی باشد. برنامه را نبندید و دوباره بزنید.");
        return left;
    }
}
