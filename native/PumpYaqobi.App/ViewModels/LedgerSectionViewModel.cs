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
    }

    protected LedgerService<TEntity> Service { get; }

    public ObservableCollection<TRow> Rows { get; } = new();
    public ObservableCollection<string> Months { get; } = new();

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
        await ReloadRowsAsync();
    }

    protected async Task ReloadRowsAsync()
    {
        var list = await Service.ListAsync(Month);
        Rows.Clear();
        foreach (var e in list) Rows.Add(Track(Wrap(e)));
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
        }
        else
        {
            Rows.Add(Track(Wrap(e)));
            RecalcAll();
        }
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
