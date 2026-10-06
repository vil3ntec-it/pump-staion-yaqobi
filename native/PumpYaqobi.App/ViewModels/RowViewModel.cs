using PumpYaqobi.Application.Localization;
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

    /// <summary>
    /// ⛔ شمارهٔ «کدام دفتر» (۱۴۰۵/۰۷/۱۶). عوض شدنِ حساب (دفترِ دیگر) یا بازگردانیِ بکاپ
    /// یکی بالایش می‌برد؛ ردیفی که با شمارهٔ کهنه ساخته شده دیگر **هیچ‌جا** نمی‌نویسد.
    /// پیش از این ردیفِ در صفِ حسابِ الف، پس از ورودِ حسابِ ب در دفترِ ب نوشته می‌شد —
    /// روی ردیفی با همان شماره (شماره‌ها در هر دفتر از ۱ شروع می‌شوند) و با شناسهٔ
    /// همگام‌سازیِ الف، یعنی دادهٔ یک حساب در دفترِ حسابِ دیگر.
    /// </summary>
    //  ⛔ شورا ج۵: شمارنده در پوسته (‎LedgerGen‎) — همان یک شمارنده، چون میزبان هم می‌زندش
    public static int LedgerGeneration => Services.LedgerGen.Current;
    public static void NewLedger() => Services.LedgerGen.Next();
    private readonly int _gen = Services.LedgerGen.Current;

    // ══════════════════════════════════════════════════════════════════════
    //  ══ خانه‌ای که در حالِ نوشتن است، زیرِ دستِ کاربر عوض نمی‌شود (۱۴۰۵/۰۷/۱۹) ══
    // ══════════════════════════════════════════════════════════════════════
    //
    //  گزارشِ صاحب ریپو: «چندین جمله یا عدد رو که می‌زنم… خودبه‌خود پاک می‌شه یا
    //  نصفه.» سنجهٔ ‎waraqtype‎ گرفتش: کادرِ عدد با هر حرف به ردیف می‌رسد،
    //  ردیف عدد را قالب می‌زند («1250» ⇒ «1,250») و همان را <b>به خودِ کادرِ در
    //  حالِ نوشتن</b> پس می‌فرستاد — کاما وسطِ تایپ می‌نشست و مکان‌نما جا
    //  می‌ماند، پس حرفِ بعدی یا پاک‌کن جای دیگری می‌خورد.
    //
    //  ⛔ تا خانه باز است، خبرِ «همین ستون عوض شد» به جدول نمی‌رود؛ با بسته شدنِ
    //  خانه یک بار می‌رود و نوشتهٔ قالب‌خورده می‌نشیند. مقدار همان لحظه در ردیف
    //  است — ذخیره، جمع‌ها و حساب‌ها هیچ تأخیری ندارند. ستون‌های دیگرِ همان ردیف
    //  هم مثلِ همیشه خبر می‌گیرند. ‎ExcelGrid‎ تنها جایی است که این را می‌گذارد.
    private string? _editingProp;

    /// <summary>خانهٔ این ستون باز شد (‎ExcelGrid‎).</summary>
    public void BeginCellEdit(string? property) { _editingProp = property; _editRaw = null; }

    /// <summary>خانه بسته شد — نوشتهٔ قالب‌خورده یک بار به جدول برود.</summary>
    public void EndCellEdit()
    {
        var p = _editingProp;
        _editingProp = null;
        _editRaw = null;
        if (p is not null) OnPropertyChanged(p);
    }

    /// <summary>آخرین نوشتهٔ خامِ کاربر در خانهٔ باز — همان را پس می‌دهیم، نه قالب‌خورده‌اش.</summary>
    private string? _editRaw;

    /// <summary>
    /// گیرندهٔ ستونِ نوشتنی: تا خانه‌اش باز است همان چیزی که کاربر نوشته، وگرنه قالب‌خورده.
    /// ⚠️ اتصالِ آوالونیا پس از نوشتن در ردیف خودِ خاصیت را دوباره می‌خواند و به کادر
    /// پس می‌دهد — پس جلوگیری از خبرِ «عوض شد» به‌تنهایی کافی نبود (‎waraqtype‎ گرفتش).
    /// </summary>
    protected string Shown(string property, string formatted)
    {
        if (!_numberCols.Contains(property)) NoteNumberColumn(property);
        return _editingProp == property && _editRaw is not null ? _editRaw : formatted;
    }

    //  ══ کدام ستون‌ها عدد‌ند؟ (۱۴۰۵/۰۷/۱۹ — قالبِ زندهٔ «5,000») ══════════════
    //  هر ستونی که از ‎Shown‎ می‌گذرد عددِ قالب‌خورده است (‎Shamsi.Money*‎)؛ پس
    //  ‎ExcelGrid‎ همان را می‌پرسد و کادرِ آن خانه کاما را همان لحظهٔ تایپ می‌گیرد.
    //  ⛔ «ثقلت» نه — قاعدهٔ خودش را دارد («0730» ⇐ «0.730»، ‎DensityInput‎).
    //  ⚡ نسخه‌برداری هنگامِ نوشتن: خواندن (هر گیرنده) بی‌قفل است.
    private static volatile HashSet<string> _numberCols = new(StringComparer.Ordinal);
    private static readonly object _numberLock = new();

    private static void NoteNumberColumn(string property)
    {
        lock (_numberLock)
        {
            if (_numberCols.Contains(property)) return;
            _numberCols = new HashSet<string>(_numberCols, StringComparer.Ordinal) { property };
        }
    }

    /// <summary>ستونِ عددیِ جدول (همان که از ‎Shown‎ می‌گذرد)، جز «ثقلت».</summary>
    public static bool IsNumberColumn(string? path) =>
        path is { Length: > 0 } && path != "DensityText" && _numberCols.Contains(path);

    /// <summary>
    /// نویسندهٔ ستونِ نوشتنی — نوشتهٔ خام را برای همان خانهٔ باز نگه می‌دارد.
    /// ⛔ (شورا، بندِ ۱ — ۱۴۰۵/۰۷/۱۹) برمی‌گرداند که نوشته <b>خوانا</b> هست یا نه.
    /// ناخوانا («12a»، «abc») ⇐ نویسنده مقدار را <b>عوض نمی‌کند</b>: پیش از این
    /// ‎Shamsi.Num‎ آن را بی‌صدا ۰ می‌کرد و همان صفر ذخیره و در جمع‌ها شمرده می‌شد.
    /// خانه سرخ می‌ماند (‎ExcelGrid‎، کلاسِ ‎badnum‎) تا درست یا با Esc رها شود.
    /// </summary>
    protected bool Typed(string property, string? raw)
    {
        if (_editingProp == property) _editRaw = raw ?? "";
        if (Shamsi.IsReadable(raw)) return true;
        BadInput?.Invoke(property, raw ?? "");
        return false;
    }

    /// <summary>نوشتهٔ ناخوانایی رد شد (ستون، نوشته) — برای سنجه‌ها و پیام.</summary>
    public static event Action<string, string>? BadInput;

    protected override void OnPropertyChanged(System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (_editingProp is not null && e.PropertyName == _editingProp) return;
        base.OnPropertyChanged(e);
    }

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

    //  ══ نوشتنی که وسطِ ذخیره رسید، گم نمی‌شود (۱۴۰۵/۰۷/۲۲) ══════════════════
    //
    //  گزارشِ صاحب ریپو: «توی ورق نام و مقدار و مبلغ را نوشتم؛ وقتی بیرون شدم
    //  عددها نصفه یا پاک شده بودند و به حساب‌ها اشتباه رفته بودند.»
    //
    //  ریشه: ‎WriteAsync‎ پس از ذخیره ‎_dirty = false‎ می‌کرد — بی آن‌که بپرسد
    //  «در این فاصله چیزِ تازه‌ای تایپ شد؟». تا ۳.۱.۲۴۵ ذخیرهٔ ‎SQLite‎ عملاً روی
    //  همان نخِ رابط تمام می‌شد و هیچ کلیدی وسطش نمی‌رسید؛ از ۳.۱.۲۴۶ ذخیرهٔ ورق
    //  روی نخِ دیگر است، پس کلیدهای وسطِ ذخیره ‎Touch‎ می‌زدند، نوشتنِ بعدی پشتِ
    //  دروازه منتظر می‌ماند، و وقتی نوبتش می‌رسید ردیف «پاک» بود و برمی‌گشت.
    //  یعنی روی دیسک «125» به‌جای «12500»، و ثبت به حساب‌ها و بازخوانیِ صفحه هم
    //  همان نیمه را می‌دیدند.
    //
    //  ⛔ هر ‎Touch‎ این شماره را بالا می‌برد؛ ردیف فقط وقتی «پاک» می‌شود که شماره
    //  از پیش از ‎Apply‎ تا پس از ذخیره عوض نشده باشد — وگرنه همان‌جا دوباره
    //  نوشته می‌شود. این قاعده برای هر جدولی است، نه فقط ورق.
    private int _editVersion;

    /// <summary>این ردیف تغییرِ ذخیره‌نشده دارد؟ (سنجش‌ها می‌خوانند)</summary>
    public bool IsDirty => _dirty;

    /// <summary>
    /// یک خانه عوض شد: مقدار همان لحظه در موجودیت می‌نشیند (تا جمع‌های بالای
    /// صفحه فوری درست شوند) و نوشتن در دیتابیس با کمی تأخیر انجام می‌شود.
    /// </summary>
    protected void Touch()
    {
        if (Loading || _retired) return;
        _dirty = true;
        Interlocked.Increment(ref _editVersion);
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

    //  ⛔ دو نوشتنِ هم‌زمانِ یک ردیف یعنی دو تراکنشِ رقیب روی یک سطر — ولی
    //  «دومی رد شود» هم غلط بود: ‎FlushAsync‎ی که وسطِ یک نوشتنِ تأخیری
    //  می‌رسید **همان لحظه برمی‌گشت**، پیش از آن‌که آن نوشتن تمام شود. پس
    //  «جدولِ جدید» (فلاش ⇒ آرشیو ⇒ پاک کردنِ ردیف‌ها) ردیف‌ها را پاک می‌کرد
    //  و نوشتنِ در راه یکی را دوباره روی دیسک برمی‌گرداند — ردیفِ آرشیوشده در
    //  جدولِ تازه (سنجهٔ ‎person‎ با مکثِ ۱۵۰ میلی‌ثانیه گرفتش). حالا دومی
    //  **منتظر می‌ماند** و بعد می‌سنجد که هنوز چیزی برای نوشتن هست یا نه.
    private SemaphoreSlim? _gate;

    private SemaphoreSlim Gate() =>
        _gate ?? Interlocked.CompareExchange(ref _gate, new SemaphoreSlim(1, 1), null) ?? _gate!;

    private async Task WriteAsync()
    {
        var gate = Gate();
        await gate.WaitAsync();
        try
        {
            for (var i = 0; i < Backoff.Length; i++)
            {
                if (!_dirty || _retired) return;
                if (_gen != LedgerGeneration)
                {
                    //  دفترِ این ردیف دیگر دفترِ جلوی چشم نیست — نوشتن یعنی خرابیِ دفترِ دیگر
                    _dirty = false;
                    SaveGuard.ReportFailure("یک ردیف پیش از عوض شدنِ دفتر ذخیره نشد");
                    return;
                }
                if (Backoff[i] > 0)
                    try { await Task.Delay(Backoff[i]); } catch { }
                try
                {
                    int v;
                    var rounds = 0;
                    do
                    {
                        v = Volatile.Read(ref _editVersion);
                        Apply();
                        await SaveAsync();
                        if (_retired || _gen != LedgerGeneration) break;
                    }
                    //  نوشتهٔ تازه‌ای وسطِ ذخیره رسید ⇒ همان را هم بنویس (سقف فقط برای ایمنی)
                    while (v != Volatile.Read(ref _editVersion) && ++rounds < 50);
                    if (v == Volatile.Read(ref _editVersion)) _dirty = false;
                    else _ = DelayedSaveAsync(CancellationToken.None);
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
        finally { gate.Release(); }
    }

    /// <summary>
    /// ذخیرهٔ فوری (پیش از بستنِ بخش، بسته شدنِ برنامه، یا گرفتنِ گزارش).
    /// ⛔ هیچ‌وقت استثنا بیرون نمی‌دهد — وگرنه یک ردیفِ خراب جلوی نوشتنِ
    /// بقیه را می‌گرفت و بسته شدنِ برنامه هم می‌ماسید.
    /// </summary>
    public virtual async Task FlushAsync()
    {
        _debounce?.Cancel();
        if (!_dirty) return;          // ردیفِ دست‌نخورده — چیزی برای نوشتن نیست
        await WriteAsync();
    }

    // ══════════════════════════════════════════════════════════════════════
    //  ══ ردیفِ حذف‌شده هرگز دوباره نوشته نمی‌شود ═══════════════════════════
    // ══════════════════════════════════════════════════════════════════════
    //
    //  ⛔ سنجهٔ ‎person‎ (با مکثِ ۱۵۰ میلی‌ثانیه) گرفتش: رسیدی از سربرگ آمد و
    //  همان لحظه حذف شد، ولی ذخیرهٔ تأخیریِ خودِ ردیف هنوز در صف بود — و
    //  کمی بعد همان ردیف را با ‎IsDeleted = false‎ دوباره روی دیسک نوشت. ردیفِ
    //  حذف‌شده برمی‌گشت؛ این بار با «جدولِ جدید»، و در کارِ واقعی با بستنِ
    //  برنامه (‎SaveGuard.FlushAllAsync‎ همهٔ ردیف‌های کثیف را می‌نویسد).
    //
    //  پس هر مسیرِ حذفِ ردیف **پیش از** پاک کردن این را می‌زند: صفِ تأخیری
    //  لغو می‌شود، ردیف دیگر کثیف نیست، و نوشتنِ در راه (اگر بود) تمام
    //  می‌شود پیش از آن‌که حذف به دیسک برسد.
    private volatile bool _retired;

    public async Task RetireAsync()
    {
        _wasDirty = _dirty;
        _retired = true;
        _debounce?.Cancel();
        _dirty = false;
        var gate = Gate();
        await gate.WaitAsync();
        gate.Release();
    }

    private bool _wasDirty;

    /// <summary>
    /// ⛔ حذف نشد (دیتابیسِ قفل، بی‌اجازه، دیسکِ پر) ⇒ ردیف دوباره «زنده» (۱۴۰۵/۰۷/۱۶).
    /// پیش از این ردیفِ بازنشسته روی صفحه می‌ماند و هر ویرایشِ بعدی‌اش دیده می‌شد ولی
    /// هیچ‌وقت ذخیره نمی‌شد — برای «جدولِ جدید» یعنی کلِ جدول.
    /// </summary>
    public void Unretire()
    {
        if (!_retired) return;
        _retired = false;
        if (!_wasDirty) return;
        _dirty = true;
        SaveGuard.Track(this);
        _ = WriteAsync();
    }

    /// <summary>بازنشسته کن، پاک کن، و اگر پاک نشد برگردان — تنها راهِ حذفِ یک ردیف.</summary>
    public async Task RetireWhileAsync(Func<Task> delete)
    {
        await RetireAsync();
        try { await delete(); }
        catch { Unretire(); throw; }
    }

    /// <summary>همان، برای چند ردیف با یک کار (آرشیوِ کلِ جدول).</summary>
    public static async Task RetireAllWhileAsync(IEnumerable<RowViewModel> rows, Func<Task> work)
    {
        var list = rows.ToList();
        foreach (var r in list) await r.RetireAsync();
        try { await work(); }
        catch { foreach (var r in list) r.Unretire(); throw; }
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
