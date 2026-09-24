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
    // ══════════════════════════════════════════════════════════════════════
    //  ══ نوشتنِ تکی — سربرگِ ورق، حسابِ قرض‌دار، رسیدِ سربرگ ════════════════
    // ══════════════════════════════════════════════════════════════════════
    //
    //  ⛔ نگهبانِ بالا فقط **ردیف‌های جدول** را می‌پوشاند. هفت نوشتنِ دیگر با
    //  ‎_ = …Async()‎ رها می‌شدند: سربرگِ ورق (نامِ کارمند، قرضِ پارچه)،
    //  حسابِ قرض‌دار (فیصدی، واحد، رسیدِ سربرگ)، حسابِ امانت، شماره و
    //  یادداشتِ قرض‌دار، و یادداشتِ بخش. هیچ‌کدام نه منتظر می‌ماندند، نه
    //  خطایشان دیده می‌شد، و بستنِ برنامه هم منتظرشان نمی‌ماند — یعنی همان
    //  «چندین ورق را پر کردم و هیچ چیزی ثبت نشده بود».
    //
    //  ⚠️ **بی تلاشِ دوباره**، عمداً: افزودنِ رسید تکرارپذیر نیست و تلاشِ
    //  دوباره یعنی رسیدِ دوتایی در حسابِ مشتری. کارِ این‌جا فقط دو چیز است —
    //  بسته شدنِ برنامه منتظرش بماند، و شکستش دیده شود. همان متد، همان
    //  آرگومان، همان لحظه: هیچ منطقی عوض نمی‌شود.

    private static readonly HashSet<Task> InFlight = new();

    /// <summary>یک نوشتنِ در جریان را زیرِ نظر می‌گیرد.</summary>
    public static void Watch(Task write, string what)
    {
        if (write.IsCompletedSuccessfully) return;
        lock (Gate) InFlight.Add(write);
        _ = write.ContinueWith(t =>
        {
            lock (Gate) InFlight.Remove(t);
            if (t.IsFaulted)
                ReportFailure(what + " ذخیره نشد ("
                              + (t.Exception?.InnerException?.GetType().Name ?? "خطا") + ")");
        }, TaskScheduler.Default);
    }

    private static Task[] Running()
    {
        lock (Gate) return InFlight.Where(t => !t.IsCompleted).ToArray();
    }

    public static int DirtyCount
    {
        get
        {
            var n = Running().Length;
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
        var running = Running();
        if (all.Count == 0 && running.Length == 0) return 0;

        //  ⛔ **یکی‌یکی، نه ‎Task.WhenAll‎.** هر ذخیره اتصالِ SQLiteِ خودش را
        //  باز می‌کند (‎LedgerService‎: ‎await using var db = _dbf.Create()‎)،
        //  پس صدها نوشتنِ هم‌زمان یعنی صدها تراکنشِ رقیب روی یک فایل. با
        //  ‎busy_timeout‎ خطا نمی‌دهند ولی پشتِ هم صف می‌کشند — همان کار، با
        //  هزاران اتصالِ اضافه.
        //
        //  ⚠️ و ترتیبِ سریالی است که سقفِ وقت را واقعی می‌کند: وقت که تمام
        //  شد، آن‌چه نوشته شده واقعاً نوشته شده و بقیه در صف می‌مانند.
        var cap = timeout ?? TimeSpan.FromSeconds(8);
        var clock = System.Diagnostics.Stopwatch.StartNew();
        foreach (var w in all)
        {
            var mandeh = cap - clock.Elapsed;
            if (mandeh <= TimeSpan.Zero) break;
            try { await w.FlushAsync().WaitAsync(mandeh); }
            catch { /* شکست یا سقفِ وقت — پایین شمرده می‌شود */ }
        }

        //  ⚠️ این‌جا ‎WhenAll‎ بی‌خطر است: کارِ تازه‌ای **شروع** نمی‌کند، فقط
        //  منتظرِ نوشتن‌هایی می‌ماند که از قبل در جریان‌اند.
        if (running.Length > 0)
        {
            var mandeh2 = cap - clock.Elapsed;
            if (mandeh2 > TimeSpan.Zero)
                try { await Task.WhenAll(running).WaitAsync(mandeh2); } catch { }
        }

        var left = all.Count(w => w.IsDirty) + running.Count(t => !t.IsCompleted);
        if (left > 0)
            ReportFailure(left + " نوشته ذخیره نشد — اینترنت لازم نیست، ولی دیسک باید " +
                          "نوشتنی باشد. برنامه را نبندید و دوباره بزنید.");
        return left;
    }
}
