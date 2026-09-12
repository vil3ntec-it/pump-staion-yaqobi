using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace PumpYaqobi.App.ViewModels;

/// <summary>
/// ══ پر کردنِ جدول با «یک» خبر، نه صدها خبر ═════════════════════════════════
///
/// گزارشِ صاحب ریپو: «بخش‌هایی که جدول‌های زیادی دارند خیلی دیر باز می‌شوند؛
/// یک ثانیه یا بیشتر می‌ایستد و گیر می‌کند، بعد می‌رود.»
///
/// ریشه‌اش این بود که هر بخش جدولش را این‌طور پر می‌کرد:
///
///     Rows.Clear();
///     foreach (var e in list) Rows.Add(Wrap(e));
///
/// هر ‎Add‎ یک ‎CollectionChanged‎ می‌دهد، و ‎DataGrid‎ به هر کدام جدا واکنش
/// نشان می‌دهد: اندازه‌گیریِ دوباره، چیدمانِ دوباره، محاسبهٔ دوبارهٔ پهنای
/// ستون‌ها. با سیصد ردیف یعنی سیصد بارِ همهٔ این‌ها، همه روی نخِ رابط — و
/// همان «یک ثانیه ایستادن» است.
///
/// این کلاس ‎ResetTo‎ می‌دهد: فهرست یک‌جا عوض می‌شود و <b>یک</b> خبرِ
/// ‎Reset‎ بیرون می‌رود. جدول یک‌بار خودش را می‌چیند، نه ‎n‎ بار.
///
/// ⚠️ رفتارِ بیرونی هیچ فرقی نمی‌کند: همان ‎ObservableCollection‎ است و هر
/// جای دیگری که ‎Add‎/‎Remove‎ می‌کند مثلِ قبل کار می‌کند.
/// </summary>
public sealed class BulkObservableCollection<T> : ObservableCollection<T>
{
    private bool _quiet;

    public BulkObservableCollection() { }
    public BulkObservableCollection(IEnumerable<T> items) : base(items) { }

    /// <summary>همهٔ محتوا را با <paramref name="items"/> عوض کن — یک خبر، نه ‎n‎ خبر.</summary>
    public void ResetTo(IEnumerable<T> items)
    {
        _quiet = true;
        try
        {
            Items.Clear();
            foreach (var i in items) Items.Add(i);
        }
        finally { _quiet = false; }

        OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
        OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }

    /// <summary>
    /// همان ‎Clear()‎ و ‎Add()‎ی همیشگی را بنویس، ولی داخلِ این محدوده — تا
    /// خبرها تا آخرِ کار نگه داشته شوند و یکجا، یک‌بار، بیرون بروند:
    ///
    ///     using (Rows.Batch()) { Rows.Clear(); foreach (…) Rows.Add(…); }
    ///
    /// برای جاهایی که پر کردن شمارنده و شرط دارد و بازنویسی‌اش ریسک است.
    /// </summary>
    public IDisposable Batch() => new Scope(this);

    private sealed class Scope : IDisposable
    {
        private readonly BulkObservableCollection<T> _c;
        private readonly bool _was;

        public Scope(BulkObservableCollection<T> c) { _c = c; _was = c._quiet; c._quiet = true; }

        public void Dispose()
        {
            if (_was) return;                 // محدودهٔ تودرتو: فقط بیرونی‌ترین خبر می‌دهد
            _c._quiet = false;
            _c.OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
            _c.OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
            _c.OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }
    }

    protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
    {
        if (_quiet) return;
        base.OnCollectionChanged(e);
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        if (_quiet) return;
        base.OnPropertyChanged(e);
    }
}
