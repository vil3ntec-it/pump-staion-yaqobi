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
    /// <summary>همهٔ ردیف‌ها را جایگزین می‌کند — با یک خبر، نه هزاران.</summary>
    public void ResetTo(IEnumerable<T> items)
    {
        Items.Clear();
        foreach (var i in items) Items.Add(i);

        OnPropertyChanged(new System.ComponentModel.PropertyChangedEventArgs(nameof(Count)));
        OnPropertyChanged(new System.ComponentModel.PropertyChangedEventArgs("Item[]"));
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }
}
