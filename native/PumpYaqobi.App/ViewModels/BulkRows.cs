using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace PumpYaqobi.App.ViewModels;

/// <summary>
/// ══ فهرستی که یک‌جا عوض می‌شود ══════════════════════════════════════════════
///
/// خواستهٔ صاحب ریپو: «حتی اگر یک حساب صدهزار ردیف داشت، نباید کند شود.»
///
/// <see cref="ObservableCollection{T}"/> برای هر ‎Add‎ یک خبر می‌دهد، و جدولِ
/// وصل‌شده به همان خبر هر بار خودش را از نو می‌سنجد. پس پر کردنِ یک حسابِ
/// صدهزار ردیفی صدهزار خبر می‌شود — و همان‌جاست که برنامه دقیقه‌ها می‌ایستد،
/// نه در خواندنِ دیتابیس.
///
/// <see cref="ResetTo"/> همهٔ ردیف‌ها را می‌گذارد و **یک** خبر می‌دهد.
///
/// ⚠️ همان ‎ObservableCollection‎ می‌مانَد، پس هر جای دیگری که ‎Add‎/‎Remove‎
/// می‌کند دست‌نخورده کار می‌کند و هیچ ‎Binding‎ی عوض نمی‌شود.
/// </summary>
public sealed class BulkRows<T> : ObservableCollection<T>
{
    private bool _quiet;

    /// <summary>همهٔ ردیف‌ها را جایگزین می‌کند — با یک خبر، نه هزاران.</summary>
    public void ResetTo(IEnumerable<T> items)
    {
        _quiet = true;
        try
        {
            Items.Clear();
            foreach (var i in items) Items.Add(i);
        }
        finally { _quiet = false; }

        Announce();
    }

    /// <summary>
    /// همان ‎Clear()‎ و ‎Add()‎ی همیشگی را بنویس، ولی داخلِ این محدوده — تا
    /// خبرها تا آخرِ کار نگه داشته شوند و یکجا، یک‌بار، بیرون بروند:
    ///
    ///     using (Rows.Batch()) { Rows.Clear(); foreach (…) Rows.Add(…); }
    ///
    /// ‎ResetTo‎ برای جایی است که فهرستِ آماده داری؛ این یکی برای جاهایی که
    /// پر کردن شمارنده و شرط دارد و بازنویسی‌اش ریسک است.
    /// </summary>
    public IDisposable Batch() => new Scope(this);

    private void Announce()
    {
        OnPropertyChanged(new System.ComponentModel.PropertyChangedEventArgs(nameof(Count)));
        OnPropertyChanged(new System.ComponentModel.PropertyChangedEventArgs("Item[]"));
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }

    protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
    {
        if (_quiet) return;
        base.OnCollectionChanged(e);
    }

    protected override void OnPropertyChanged(System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (_quiet) return;
        base.OnPropertyChanged(e);
    }

    private sealed class Scope : IDisposable
    {
        private readonly BulkRows<T> _c;
        private readonly bool _was;

        public Scope(BulkRows<T> c) { _c = c; _was = c._quiet; c._quiet = true; }

        public void Dispose()
        {
            if (_was) return;              // محدودهٔ تودرتو: فقط بیرونی‌ترین خبر می‌دهد
            _c._quiet = false;
            _c.Announce();
        }
    }
}
