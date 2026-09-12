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
        _month = Shamsi.ThisMonth();
        // کشوی ماه فقط چیزی را که بخش با آن رندر می‌کند عوض می‌کند؛ خودِ منطق
        // دست‌نخورده می‌ماند — همان چیزی که سایت هم صریح نوشته.
        Picker = new YearMonthPicker(k => { if (k.Length > 0 && k != Month) Month = k; });
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
    }

    /// <summary>فیلترِ جست‌وجو — بخش‌هایی که ستونِ نام دارند بازنویسی‌اش می‌کنند.</summary>
    protected virtual void ApplyFilter() { }

    protected override async Task LoadAsync()
    {
        Months.Clear();
        foreach (var m in await Service.MonthsAsync()) Months.Add(m);
        if (!Months.Contains(Month)) Months.Insert(0, Month);
        Picker.Load(Months, Month);
        await ReloadRowsAsync();
    }

    protected async Task ReloadRowsAsync()
    {
        var list = await Service.ListAsync(Month);
        // یک‌جا، نه ردیف‌به‌ردیف — وگرنه جدول به ازای هر ردیف یک‌بار خودش را
        // از نو می‌چیند و بخش هنگامِ باز شدن می‌ایستد.
        Rows.ResetTo(list.Select(e => Track(Wrap(e))));
        ApplyFilter();
        RecalcAll();
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
        if (Shamsi.MonthKey(e.DateShamsi) != Month)
        {
            var mk = Shamsi.MonthKey(e.DateShamsi);
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
        var pick = await Dialogs.PickMonthAsync(Months.ToList());
        if (string.IsNullOrWhiteSpace(pick)) return;

        if (Months.Contains(pick))
        {
            // از قبل هست: فقط برو رویش — نه رکوردِ تکراری.
            Month = pick;
            Picker.Adopt(pick);
            await ReloadRowsAsync();
            return;
        }

        // ردیفِ خالیِ روزِ اولِ همان ماه — همان کاری که ‎addExpenseMonth‎ی سایت
        // می‌کند: ماه با یک ردیفِ خالی «باز» می‌شود.
        var e = NewEntity();
        e.DateShamsi = pick + "/01";
        await Service.AddAsync(e);

        if (!Months.Contains(pick)) Months.Insert(0, pick);
        Month = pick;
        Picker.Adopt(pick);
        await ReloadRowsAsync();
    }

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
