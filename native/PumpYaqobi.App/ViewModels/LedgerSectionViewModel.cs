using PumpYaqobi.App.Services;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using PumpYaqobi.App.Controls;
using CommunityToolkit.Mvvm.Input;
using PumpYaqobi.Application.Localization;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Services.Data;

namespace PumpYaqobi.App.ViewModels;

/// <summary>
/// ══ بخش‌های «دفتری» ════════════════════════════════════════════════════════
/// هر بخشی که یک فهرستِ ردیفِ تاریخ‌دار است و با ماه فیلتر می‌شود:
/// گاوصندوق، صرافی، مصارف، چکنه، درآمدِ اضافی، میله‌زنی، تخلیهٔ تانکر…
///
/// یک‌جا نوشته می‌شود تا همهٔ این بخش‌ها مو‌به‌مو یک رفتار داشته باشند و
/// اصلاحِ یک نکته در همه‌شان با هم اعمال شود.
/// </summary>
public abstract partial class LedgerSectionViewModel<TRow, TEntity> : SectionViewModel, IRowBatchHost
    where TRow : RowViewModel
    where TEntity : EntityBase, ILedgerRow, new()
{
    protected LedgerSectionViewModel(string id, string iconKey, string title, LedgerService<TEntity> svc)
        : base(id, iconKey, title)
    {
        Service = svc;
        _month = _autoMonth = Shamsi.ThisMonth();
        // کشوی ماه فقط چیزی را که بخش با آن رندر می‌کند عوض می‌کند؛ خودِ منطق
        // دست‌نخورده می‌ماند — همان چیزی که سایت هم صریح نوشته.
        // ⚠️ «همهٔ ماه‌ها» هم یک گزینه است، مثلِ سایت — پس کلیدِ خالی/«1405/*»
        // هم باید بنشیند، نه این‌که نادیده گرفته شود.
        Picker = new YearMonthPicker(k => { if (k != Month) Month = k; }, "همهٔ ماه‌ها");
    }

    protected LedgerService<TEntity> Service { get; }

    /// <summary>
    /// ⚠️ ‎BulkRows‎ است، نه ‎ObservableCollection‎ی ساده: پر
    /// کردنِ ردیف‌ها یک خبر می‌دهد نه ‎n‎ خبر. همان چیزی که «یک ثانیه گیر
    /// کردنِ» بخش‌های پرجدول را می‌ساخت.
    /// </summary>
    public BulkRows<TRow> Rows { get; } = new();
    public ObservableCollection<string> Months { get; } = new();

    /// <summary>
    /// کشویِ «سال» + «ماه» — همتای ‎_ymSelectsHtml‎ی سایت.
    /// گزارشِ صاحب ریپو: «کادرِ کشویی سال هم نیست.» هیچ بخشی نداشت.
    /// ⚠️ ‎Months‎ی رشته‌ای هم مانده چون چند آزمون و چند بخشِ دیگر به آن بندند.
    /// </summary>
    public YearMonthPicker Picker { get; }

    [ObservableProperty] private string _month;
    [ObservableProperty] private string _search = "";

    partial void OnMonthChanged(string value) => _ = ReloadRowsAsync();

    // ══ نیمه‌شبِ آخرِ ماه: خودِ بخش به ماهِ تازه می‌رود ═══════════════════════
    //
    // پرسشِ صاحب ریپو (۱۴۰۵/۰۷/۰۷): «اتومات ماه که عوض بشه، ماه هم جدید
    // می‌شود با حساب‌های ماهِ جدید می‌آید یا نه؟»
    //
    // ⛔ تا امروز **نه**: ‎_month‎ یک بار در سازنده از ‎Shamsi.ThisMonth()‎
    // پر می‌شد (یعنی لحظهٔ باز شدنِ برنامه) و ‎DayChanged‎ فقط تاریخِ سربرگ و
    // نوار را از نو می‌ساخت. پس برنامه‌ای که شبِ آخرِ ماه باز مانده بود، فردا
    // هنوز جدولِ ماهِ گذشته را نشان می‌داد — و ردیفِ تازه (که تاریخش امروز
    // است) در آن نما نمی‌گنجید.
    //
    // ⚠️ **و ماهِ انتخابیِ خودِ کاربر دست نمی‌خورد.** ‎_autoMonth‎ همان ماهی
    // است که *برنامه* خودش گذاشته؛ اگر کاربر ماهِ دیگری (یا «همهٔ ماه‌ها») را
    // برگزیده باشد، ‎Month != _autoMonth‎ است و این‌جا هیچ کاری نمی‌شود. یک
    // جدولِ سه‌سال‌پیش که وسطِ کار زیرِ دستِ کاربر بپرد، باگ است نه راحتی.

    /// <summary>ماهی که خودِ برنامه گذاشته — نه چیزی که کاربر برگزیده.</summary>
    private string _autoMonth;

    public override void OnDayChanged()
    {
        var now = Shamsi.ThisMonth();
        var roll = MonthRoll.Decide(Month, _autoMonth, now);
        _autoMonth = now;
        if (!roll) return;

        if (!Months.Contains(now)) Months.Insert(0, now);
        Picker.Adopt(now);
        Month = now;                                // خودش ‎ReloadRowsAsync‎ را می‌زند
    }
    partial void OnSearchChanged(string value) => ApplyFilter();

    /// <summary>ردیفِ دیتابیس ← ردیفِ جدول.</summary>
    protected abstract TRow Wrap(TEntity e);

    /// <summary>ردیفِ خالیِ تازه‌ای که دکمهٔ «ردیفِ تازه» می‌سازد.</summary>
    protected virtual TEntity NewEntity() => new() { DateShamsi = Shamsi.Today() };

    /// <summary>جمع‌های بالای صفحه — هر بخش خودش می‌داند.</summary>
    protected virtual void Recalc() { }

    /// <summary>
    /// ══ ردیفِ «جمله»ی ته جدول ═══════════════════════════════════════════════
    /// همتای ‎&lt;tfoot class="xls-foot"&gt;‎ی سایت. هر بخش خانه‌های خودش را
    /// می‌دهد؛ بخشی که جمع معنا ندارد (فهرست‌های فقط‌خواندنی) چیزی نمی‌دهد و
    /// نوار اصلاً دیده نمی‌شود.
    /// </summary>
    protected virtual IReadOnlyList<TotalCell> BuildTotals() => Array.Empty<TotalCell>();

    public IReadOnlyList<TotalCell> TotalCells => BuildTotals();
    public bool HasTotals => TotalCells.Count > 0;

    /// <summary>جمع‌های بالای صفحه + ردیفِ «جمله»، با هم و همیشه هم‌زمان.</summary>
    private void RecalcAll()
    {
        Recalc();
        OnPropertyChanged(nameof(TotalCells));
        OnPropertyChanged(nameof(HasTotals));
        RefreshMonthHint();          // ردیفِ تازه در ماهِ خالی ⇒ نوار برود
    }

    /// <summary>فیلترِ جست‌وجو — بخش‌هایی که ستونِ نام دارند بازنویسی‌اش می‌کنند.</summary>
    protected virtual void ApplyFilter() { }

    protected override async Task LoadAsync()
    {
        Months.Clear();
        _dataMonths = await Service.MonthsAsync();
        foreach (var m in _dataMonths) Months.Add(m);
        if (!Months.Contains(Month)) Months.Insert(0, Month);
        Picker.Load(Months, Month);
        await ReloadRowsAsync();
    }

    /// <summary>
    /// کلیدی که به دیتابیس می‌رود: «1405/07» یک ماه، «1405/*» همهٔ ماه‌های آن
    /// سال، و خالی یعنی همهٔ سال‌ها — همان سه حالتِ کشویِ سایت.
    /// </summary>
    protected string MonthFilter => YearMonthPicker.IsAll(Month)
        ? YearMonthPicker.AllOfYear(Month) is { Length: > 0 } y ? y + "/" : ""
        : Month;

    protected async Task ReloadRowsAsync()
    {
        var list = await Service.ListAsync(MonthFilter);
        // یک‌جا، نه ردیف‌به‌ردیف — وگرنه جدول به ازای هر ردیف یک‌بار خودش را
        // از نو می‌چیند و بخش هنگامِ باز شدن می‌ایستد.
        Rows.ResetTo(list.Select(e => Track(Wrap(e))));
        ApplyFilter();
        RecalcAll();
    }

    // ══ ماهِ تازه خالی است؛ ماهِ پیش سرِ جایش است ══════════════════════════
    //
    // ⛔ این دفترها ماه‌به‌ماه‌اند و ماهِ تازه عمداً خالی شروع می‌شود — ولی
    // سرِ آغازِ میزان کاربر دید جدول خالی است و گمان کرد «هر چه نوشته بودم پاک
    // شد». پس ماهِ خالی **می‌گوید** که ردیف‌های ماهِ دیگر هست و یک دکمه به
    // آن‌جا دارد. شرح در ‎SectionViewModel.MonthHint‎.
    // ⚠️ ماه خودکار جابه‌جا نمی‌شود: ردیفِ تازه جایش در ماهِ جاری است.

    /// <summary>ماه‌هایی که واقعاً ردیف دارند — تازه‌ترین اول.</summary>
    private List<string> _dataMonths = new();
    private string _hintTarget = "";

    private void RefreshMonthHint()
    {
        _hintTarget = "";
        if (Rows.Count == 0 && !YearMonthPicker.IsAll(Month)
            && _dataMonths.FirstOrDefault(m => m != Month) is { } other)
        {
            MonthHint = (Month == Shamsi.ThisMonth()
                            ? "📅 ماهِ «" + Shamsi.MonthLabel(Month) + "» تازه شروع شده و هنوز ردیفی ندارد"
                            : "📅 «" + Shamsi.MonthLabel(Month) + "» هنوز ردیفی ندارد")
                      + " — هیچ چیزی پاک نشده؛ ردیف‌های «" + Shamsi.MonthLabel(other) + "» سرِ جایشان‌اند";
            MonthHintAction = "نمایشِ «" + Shamsi.MonthLabel(other) + "»";
            _hintTarget = other;
        }
        else { MonthHint = ""; MonthHintAction = ""; }
    }

    protected override Task OnGoMonthHintAsync()
    {
        if (_hintTarget.Length == 0) return Task.CompletedTask;
        var t = _hintTarget;
        if (!Months.Contains(t)) Months.Insert(0, t);
        Picker.Adopt(t);
        Month = t;
        return Task.CompletedTask;
    }

    /// <summary>ذخیرهٔ یک ردیف — تنها همان ردیف، نه کلِ جدول.</summary>
    public virtual async Task SaveEntityAsync(TEntity e)
    {
        if (e.Id == 0) await Service.AddAsync(e);
        else await Service.UpdateAsync(e);
        RecalcAll();
    }

    /// <summary>
    /// پیش از برداشتنِ یک ردیف — برای دفترهایی که ردیفِ خودکاری جای دیگری
    /// ساخته‌اند و باید همان‌جا هم برداشته شود (مثلِ صرافی ← حسابِ شرکت).
    /// </summary>
    protected virtual Task BeforeDeleteAsync(TEntity e) => Task.CompletedTask;

    [RelayCommand]
    protected async Task AddRowAsync()
    {
        var e = NewEntity();
        await Service.AddAsync(e);
        var mk = Shamsi.MonthKey(e.DateShamsi);
        // ردیفِ تازه در نمایی که جلوی چشم است می‌گنجد؟ («همهٔ ماه‌های ۱۴۰۵»
        // هم یک نماست، پس ردیفِ همان سال نباید نما را عوض کند.)
        var year = YearMonthPicker.AllOfYear(Month);
        var fits = YearMonthPicker.IsAll(Month)
            ? year.Length == 0 || mk.StartsWith(year + "/", StringComparison.Ordinal)
            : mk == Month;
        if (!fits)
        {
            if (!Months.Contains(mk)) Months.Insert(0, mk);
            Month = mk;                       // خودش ReloadRowsAsync را صدا می‌زند
            Picker.Adopt(mk);
        }
        else
        {
            Rows.Add(Track(Wrap(e)));
            RecalcAll();
        }
    }

    /// <summary>
    /// ══ «📅 ماه جدید» — ‎addExpenseMonth()‎ی سایت ═══════════════════════════
    ///
    /// گزارشِ صاحب ریپو: «کادرها ماهِ بعد کار نمی‌کند.» علتش این بود که فهرستِ
    /// ماه‌ها فقط ماه‌هایی را دارد که <b>داده</b> دارند؛ ماهِ بعد تا وقتی
    /// ردیفی نداشته باشد اصلاً در کشویی نیست. در سایت دکمهٔ «🗓️ ماه جدید»
    /// همین کار را می‌کند و در نیتیو دکمه‌اش بود ولی <b>هیچ فرمانی نداشت</b>.
    ///
    /// منطقش مو‌به‌مو همان سایت است: تازه‌ترین ماه را بردار، یکی جلو ببر (با
    /// چرخشِ سال در ماهِ ۱۲)، و یک ردیفِ خالی به تاریخِ روزِ اولِ همان ماه بساز.
    /// </summary>
    /// <summary>
    /// ══ «📅 ماه جدید» ═══════════════════════════════════════════════════════
    ///
    /// خواستهٔ صریحِ صاحب ریپو: «کلیک → دیالوگِ انتخابِ سال و ماه → تأیید →
    /// ماه در Data Layer ایجاد شود → همان ماه فعال شود → بدون Restart.»
    ///
    /// پیش از این دو مشکل داشت: اولش هیچ فرمانی نداشت، بعد هم که وصل شد
    /// بی‌سروصدا ماهِ بعدی را می‌ساخت و کاربر هیچ انتخابی نداشت.
    ///
    /// ⚠️ ماهِ تکراری ساخته نمی‌شود — خودِ دیالوگ ماه‌های موجود را می‌داند و
    /// دکمه‌اش را خاموش می‌کند. این‌جا هم بارِ دوم بررسی می‌شود، چون بینِ باز
    /// شدنِ دیالوگ و تأیید ممکن است چیزی عوض شده باشد.
    /// </summary>
    [RelayCommand]
    protected async Task NewMonthAsync()
    {
        // ══ «📅 ماه جدید» — مو‌به‌مو مثلِ ‎addExpenseMonth()‎ی سایت ═══════════
        //
        // ⚠️ این‌جا یک‌بار دیالوگِ «سال و ماه را انتخاب کن» گذاشته شد. سایت
        // چنین چیزی ندارد و صاحب ریپو با عکس همین را نشان داد: دکمه بی هیچ
        // پرسشی **ماهِ بعدیِ آخرین ماه** را باز می‌کند و پیام می‌دهد
        // «✅ جدول ماه ‎<نام ماه>‎ باز شد».
        //
        // «باز کردن» یعنی یک ردیفِ خالیِ روزِ اولِ همان ماه — در سایت هم
        // همین است (‎DB.expenses.push({date: nextDate, ...})‎)، چون ماه تا
        // ردیفی نداشته باشد در کشویی پیدا نمی‌شود.
        var latest = Months.Where(m => m.Any(char.IsDigit))
                           .OrderByDescending(m => m, StringComparer.Ordinal)
                           .FirstOrDefault();

        string next;
        if (latest is { Length: > 0 })
        {
            var p = Shamsi.ToEnDigits(latest).Split('/');
            var yr = int.TryParse(p.ElementAtOrDefault(0), out var y) ? y : 1403;
            var mo = int.TryParse(p.ElementAtOrDefault(1), out var m) ? m : 1;
            mo++;
            if (mo > 12) { mo = 1; yr++; }
            next = $"{yr:0000}/{mo:00}";
        }
        else next = Shamsi.ThisMonth();

        if (!Months.Contains(next))
        {
            var e = NewEntity();
            e.DateShamsi = next + "/01";
            await Service.AddAsync(e);
            Months.Insert(0, next);
        }

        Month = next;
        Picker.Adopt(next);
        await ReloadRowsAsync();
        Toast?.Invoke("✅ جدول ماه " + Shamsi.MonthLabel(next) + " باز شد");
    }

    /// <summary>پیامِ کوتاهِ گوشهٔ صفحه — بخشی که میزبان دارد وصلش می‌کند.</summary>
    public Action<string>? Toast { get; set; }


    [RelayCommand]
    protected async Task DeleteRowAsync(TRow? row)
    {
        if (row is null) return;
        await BeforeDeleteAsync(EntityOf(row));
        await Service.DeleteAsync(EntityIdOf(row));
        Rows.Remove(row);
        RecalcAll();
    }

    public int RowCount => Rows.Count;

    /// <summary>نوارِ «➕ ردیف / ➕➕ چندتایی»ِ پایینِ همین دفتر.</summary>
    public override System.Windows.Input.ICommand? RowAddCommand => AddRowCommand;

    /// <summary>‎Ctrl+عدد‎ — همان ‎AddRowAsync‎، فقط ‎n‎ بار.</summary>
    public async Task AddRowsAsync(int count)
    {
        for (var i = 0; i < count; i++) await AddRowAsync();
    }

    /// <summary>
    /// ‎Shift+عدد‎ — ‎n‎ ردیفِ آخرِ <b>همان چیزی که روی صفحه دیده می‌شود</b>.
    /// ردیفِ کافی نبود، هیچ (نه خطا، نه حذفِ ناقص).
    /// </summary>
    public async Task DeleteRowsAsync(int count)
    {
        if (count < 1 || Rows.Count < count) return;
        for (var i = 0; i < count; i++) await DeleteRowAsync(Rows[^1]);
    }

    protected abstract long EntityIdOf(TRow row);

    /// <summary>خودِ موجودیتِ پشتِ یک ردیف — برای <see cref="BeforeDeleteAsync"/>.</summary>
    protected abstract TEntity EntityOf(TRow row);

    /// <summary>هر ردیف که خانه‌ای‌اش عوض شود، جمع‌های بالای صفحه فوری تازه می‌شوند.</summary>
    private TRow Track(TRow row)
    {
        row.Recalculated += RecalcAll;
        return row;
    }
}

/// <summary>
/// ══ «ماه عوض شد — بروم ماهِ تازه؟» ════════════════════════════════════════
///
/// یک تصمیمِ <b>خالص</b>، جدا از ویومدل، تا آزمون بتواند هر چهار حالت را بی
/// ساعتِ ساختگی بسنجد — همان الگوی <c>guard.decide()</c>ِ ریپوی سرور.
/// </summary>
public static class MonthRoll
{
    /// <param name="shown">ماهی که همین حالا جلوی چشم است.</param>
    /// <param name="auto">آخرین ماهی که خودِ <b>برنامه</b> گذاشته.</param>
    /// <param name="now">ماهِ امروز.</param>
    /// <returns>
    /// ‎true‎ فقط وقتی ماه واقعاً عوض شده <b>و</b> کاربر همان ماهِ خودکار را
    /// می‌دیده. ⛔ ماهِ انتخابیِ کاربر («۱۴۰۴/۰۳» یا «همهٔ ماه‌ها») هیچ‌وقت
    /// زیرِ دستش عوض نمی‌شود.
    /// </returns>
    public static bool Decide(string shown, string auto, string now) =>
        now != auto && shown == auto;
}
