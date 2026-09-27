using System.Collections;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Metadata;
using PumpYaqobi.Domain;

namespace PumpYaqobi.App.Controls;

/// <summary>
/// ══ شبکهٔ کارت با مجازی‌سازیِ واقعی — کارت‌های قرض‌داران (۱۴۰۵/۰۷/۱۵) ═══════
///
/// خواستهٔ صاحب ریپو: «اسکرولِ کارت‌های قرض‌داران رو هم درست کن.» سنجهٔ
/// <c>scrollperf cards</c> پیش از این (۵۷۰ کارت، ‎ItemsRepeater + UniformGridLayout‎):
/// <list type="bullet">
/// <item>بلندیِ صفحه با <b>هر</b> گام یک ردیف بالا و پایین می‌پرید
///   (۲۸٬۰۸۸ ⇄ ۲۸٬۳۳۶) ⇒ کلِ صفحه از نو چیده می‌شد، و ته صفحه «پرپر» می‌زد.</item>
/// <item>۷۵ کارت زنده می‌ماند (کَشِ دو قابِ بالا و پایین) ⇒ هر گام ~۵۰ms حتی بی
///   کارتِ تازه (صفحه‌های دیگر ~۱۲ms).</item>
/// <item>هر ردیفِ تازه شش کارت از صفر می‌ساخت ⇒ گام‌های ۱۵۰ تا ۲۸۰ms.</item>
/// </list>
///
/// این‌جا هر سه با ساختار بسته می‌شوند، نه با ترفند:
/// <list type="number">
/// <item><b>بلندیِ دقیق و ثابت</b>: همهٔ کارت‌ها یک اندازه‌اند، پس بلندی =
///   ردیف‌ها × (قد + فاصله) — از روی شمار، نه تخمین. تا شمار و پهنا عوض نشود،
///   بلندی یک پیکسل هم تکان نمی‌خورد.</item>
/// <item><b>فقط کارت‌های جلوی چشم</b> به‌علاوهٔ یک ردیف بالا و پایین زنده‌اند
///   (<see cref="BufferRows"/>).</item>
/// <item><b>بازیافت</b>: کارتی که از قاب بیرون رفت پنهان و در انبار می‌ماند و
///   برای کارتِ تازه فقط <c>DataContext</c>ش عوض می‌شود — هیچ کارتی از صفر ساخته
///   نمی‌شود مگر انبار خالی باشد.</item>
/// </list>
///
/// ⚠️ قابِ دید از <see cref="Layoutable.EffectiveViewportChanged"/> می‌آید (همان
/// اسکرولِ خودِ صفحه) و تنها وقتی چیدمان را باطل می‌کند که <b>بازهٔ ردیف‌ها</b>
/// عوض شده باشد — نه با هر پیکسلِ اسکرول.
/// </summary>
public sealed class CardGrid : Control
{
    public static readonly StyledProperty<IEnumerable?> ItemsSourceProperty =
        AvaloniaProperty.Register<CardGrid, IEnumerable?>(nameof(ItemsSource));

    public static readonly StyledProperty<IDataTemplate?> ItemTemplateProperty =
        AvaloniaProperty.Register<CardGrid, IDataTemplate?>(nameof(ItemTemplate));

    public static readonly StyledProperty<double> MinItemWidthProperty =
        AvaloniaProperty.Register<CardGrid, double>(nameof(MinItemWidth), 210);

    public static readonly StyledProperty<double> ItemHeightProperty =
        AvaloniaProperty.Register<CardGrid, double>(nameof(ItemHeight), 238);

    public static readonly StyledProperty<int> MaxColumnsProperty =
        AvaloniaProperty.Register<CardGrid, int>(nameof(MaxColumns), 8);

    public static readonly StyledProperty<double> SpacingProperty =
        AvaloniaProperty.Register<CardGrid, double>(nameof(Spacing), 10);

    public IEnumerable? ItemsSource { get => GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }

    [InheritDataTypeFromItems(nameof(ItemsSource))]
    public IDataTemplate? ItemTemplate { get => GetValue(ItemTemplateProperty); set => SetValue(ItemTemplateProperty, value); }

    public double MinItemWidth { get => GetValue(MinItemWidthProperty); set => SetValue(MinItemWidthProperty, value); }
    public double ItemHeight { get => GetValue(ItemHeightProperty); set => SetValue(ItemHeightProperty, value); }
    public int MaxColumns { get => GetValue(MaxColumnsProperty); set => SetValue(MaxColumnsProperty, value); }
    public double Spacing { get => GetValue(SpacingProperty); set => SetValue(SpacingProperty, value); }

    /// <summary>ردیف‌های زندهٔ بیرون از قاب، بالا و پایین.</summary>
    public const int BufferRows = 1;

    private IList _items = Array.Empty<object>();
    private INotifyCollectionChanged? _watched;

    /// <summary>کارت‌های زنده، به ترتیبِ شمارهٔ قلم.</summary>
    private readonly Dictionary<int, Control> _live = new();
    /// <summary>کارت‌های پنهانِ آمادهٔ بازیافت.</summary>
    private readonly Stack<Control> _pool = new();

    private Rect _viewport;
    private bool _hasViewport;
    private (int First, int Last) _range = (0, -1);
    private int _cols = 1;
    private double _itemW;

    /// <summary>برای سنجه‌ها: کارتِ زنده و کلِ کارت‌های ساخته‌شده.</summary>
    public int LiveCount => _live.Count;
    public int BuiltCount { get; private set; }

    /// <summary>همهٔ کارت‌های زنده، برای سنجه‌ها و رفتنِ صفحه‌کلید.</summary>
    public IEnumerable<Control> LiveCards => _live.OrderBy(kv => kv.Key).Select(kv => kv.Value);

    public CardGrid()
    {
        EffectiveViewportChanged += (_, e) =>
        {
            //  ⚠️ این رویداد با هر پاسِ چیدمان هم شلیک می‌شود، حتی بی هیچ جابه‌جایی
            //  (همین که کارتِ پیش‌ساخته به درخت می‌رود). «اسکرول» فقط وقتی است که قاب
            //  واقعاً جابه‌جا شده — وگرنه پیش‌ساختن هرگز آرام نمی‌گرفت.
            if (_hasViewport && e.EffectiveViewport != _viewport) _lastScroll = AppClock.Mono;
            _viewport = e.EffectiveViewport;
            _hasViewport = true;
            //  ⛔ فقط اگر بازهٔ ردیف‌ها عوض شد — نه با هر پیکسلِ اسکرول
            if (RangeFor(_viewport, _items.Count) != _range) InvalidateMeasure();
            //  نخستین قاب (یا قابِ بزرگ‌تر) ⇒ انبار را پر کن، پیش از نخستین چرخ
            else QueueWarm();
        };
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ItemsSourceProperty) Rebind();
        else if (change.Property == ItemTemplateProperty) { ClearAll(); InvalidateMeasure(); }
        else if (change.Property == MinItemWidthProperty || change.Property == ItemHeightProperty
              || change.Property == MaxColumnsProperty || change.Property == SpacingProperty)
            InvalidateMeasure();
    }

    private void Rebind()
    {
        if (_watched is not null) _watched.CollectionChanged -= OnCollectionChanged;
        _watched = ItemsSource as INotifyCollectionChanged;
        if (_watched is not null) _watched.CollectionChanged += OnCollectionChanged;
        _items = ItemsSource as IList ?? ItemsSource?.Cast<object>().ToList() ?? (IList)Array.Empty<object>();
        ReleaseAll();
        InvalidateMeasure();
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (ItemsSource is not IList) _items = ItemsSource?.Cast<object>().ToList() ?? (IList)Array.Empty<object>();
        //  شمارهٔ قلم‌ها جابه‌جا می‌شود ⇒ همه به انبار، و دوباره از روی شماره‌ها
        ReleaseAll();
        InvalidateMeasure();
    }

    /// <summary>ستون‌ها و پهنای کارت برای یک پهنا — همان قاعدهٔ ‎UniformGridLayout‎ی پیشین.</summary>
    public static (int Cols, double ItemWidth) Columns(double width, double minItemWidth, double spacing, int maxColumns)
    {
        if (double.IsInfinity(width) || width <= 0) width = minItemWidth;
        var cols = (int)Math.Floor((width + spacing) / (minItemWidth + spacing));
        cols = Math.Clamp(cols, 1, Math.Max(1, maxColumns));
        var w = Math.Max(0, (width - spacing * (cols - 1)) / cols);
        return (cols, w);
    }

    /// <summary>بلندیِ دقیقِ کلِ شبکه — از روی شمار، نه تخمین.</summary>
    public static double TotalHeight(int count, int cols, double itemHeight, double spacing)
    {
        if (count <= 0) return 0;
        var rows = (count + cols - 1) / cols;
        return rows * itemHeight + (rows - 1) * spacing;
    }

    private (int First, int Last) RangeFor(Rect vp, int count)
    {
        if (count == 0) return (0, -1);
        var rows = (count + _cols - 1) / _cols;
        var pitch = ItemHeight + Spacing;
        if (!_hasViewport)
        {
            //  هنوز قابی نیامده: فقط یک قابِ پنجره‌ای (نه همه)
            var guess = (int)Math.Ceiling(1200 / pitch) + BufferRows;
            return (0, Math.Min(count - 1, Math.Min(rows, guess) * _cols - 1));
        }
        if (vp.Height <= 0 || vp.Bottom < 0 || vp.Y > TotalHeight(count, _cols, ItemHeight, Spacing))
            return (0, -1);   // دیده نمی‌شود ⇒ هیچ کارتی زنده نمی‌ماند
        var firstRow = Math.Max(0, (int)Math.Floor(vp.Y / pitch) - BufferRows);
        var lastRow = Math.Min(rows - 1, (int)Math.Floor(vp.Bottom / pitch) + BufferRows);
        var first = firstRow * _cols;
        //  دست‌کم به اندازهٔ کارت‌هایی که در بی‌کاری از پیش زنده شده‌اند (‎_ahead‎)
        var last = Math.Max((lastRow + 1) * _cols - 1, first + _ahead - 1);
        return (first, Math.Min(count - 1, last));
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        (_cols, _itemW) = Columns(availableSize.Width, MinItemWidth, Spacing, MaxColumns);
        var count = _items.Count;
        var range = RangeFor(_viewport, count);

        //  آن‌هایی که بیرونِ بازه‌اند ⇒ انبار
        foreach (var i in _live.Keys.Where(i => i < range.First || i > range.Last).ToList())
        {
            var c = _live[i];
            _live.Remove(i);
            c.IsVisible = false;
            _pool.Push(c);
        }

        for (var i = range.First; i <= range.Last; i++)
        {
            if (!_live.TryGetValue(i, out var c))
            {
                c = Rent(_items[i]);
                if (c is null) continue;
                _live[i] = c;
            }
            else if (!ReferenceEquals(c.DataContext, _items[i])) c.DataContext = _items[i];
            c.Measure(new Size(_itemW, ItemHeight));
        }
        _range = range;
        QueueWarm();

        var w = double.IsInfinity(availableSize.Width) ? _cols * _itemW + (_cols - 1) * Spacing : availableSize.Width;
        return new Size(w, TotalHeight(count, _cols, ItemHeight, Spacing));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var pitch = ItemHeight + Spacing;
        foreach (var (i, c) in _live)
        {
            var row = i / _cols;
            var col = i % _cols;
            //  ⚠️ در راست‌به‌چپ خودِ آوالونیا چیدمان را آینه می‌کند؛ پس همان x ِ چپ‌به‌راست
            var x = col * (_itemW + Spacing);
            c.Arrange(new Rect(x, row * pitch, _itemW, ItemHeight));
        }
        return finalSize;
    }

    // ══ پیش‌زنده کردن در بی‌کاری ═════════════════════════════════════════
    //  سنجه (‎scrollperf cards‎): بارِ اولِ اسکرول چند گامِ ۱۵۰–۲۲۰ms داشت. نه از
    //  اسکرول، از **نخستین** ساختن/پیوند/چیدنِ کارت‌هایی که تازه به قاب می‌رسند —
    //  بارِ دوم به بعد همان کارت‌ها بازیافت می‌شوند و گام زیرِ ۳۵ms است. پیش‌ساختنِ
    //  کارتِ پنهان در انبار بس نبود (هزینهٔ پیوند و نخستین چیدن سرِ جایش ماند)؛
    //  پس تا صفحه جلوی چشم است و کسی نمی‌چرخاند، کارت‌های پایینِ قاب **یکی‌یکی**
    //  کامل زنده می‌شوند — ساخته، پیوند خورده، چیده — تا بازهٔ زنده دست‌کم به
    //  اندازهٔ یک پنجرهٔ کامل به‌علاوهٔ کناره‌ها باشد (‎_ahead‎ = کمینهٔ بازه).
    //  پس وقتی سربرگِ بخش بالا می‌رود و قاب بلندتر می‌شود، کارتِ تازه‌ای لازم
    //  نیست؛ و در اسکرولِ بعدی ردیفِ بالا فقط بازیافت می‌شود.
    //  ⛔ فقط وقتی دیده می‌شود — بخشِ پنهان هیچ کارتی نمی‌سازد (قاعدهٔ ‎idle‎)،
    //  و تا کاربر می‌چرخاند هیچ کاری نمی‌کند (‎WarmCalm‎).
    private bool _warmQueued;
    private int _ahead;

    /// <summary>سقفِ کارتِ زنده: بلندیِ پنجره + دو ردیفِ کنار + یک ردیف.</summary>
    private int WarmTarget()
    {
        if (!_hasViewport || _viewport.Height <= 0 || _items.Count == 0) return 0;
        var pitch = ItemHeight + Spacing;
        //  ⚠️ قابِ کارت‌ها با اسکرول بلندتر می‌شود (سربرگِ بخش بالا می‌رود)؛ پس از
        //  بلندیِ خودِ پنجره، نه از قابِ همین لحظه — وگرنه نخستین گام‌ها باز می‌سازند.
        var h = Math.Max(_viewport.Height, TopLevel.GetTopLevel(this)?.ClientSize.Height ?? 0);
        var rows = (int)Math.Ceiling(h / pitch) + 2 * BufferRows + 1;
        return Math.Min(_items.Count, rows * _cols);
    }

    /// <summary>آخرین جابه‌جاییِ قاب — تا وقتی کاربر می‌چرخاند، هیچ کارتی پیش‌ساخته نمی‌شود.</summary>
    private DateTime _lastScroll;

    /// <summary>فاصلهٔ دو کارتِ پیش‌زنده، و آرامشِ لازم پس از آخرین اسکرول.</summary>
    public static readonly TimeSpan WarmGap = TimeSpan.FromMilliseconds(40);
    public static readonly TimeSpan WarmCalm = TimeSpan.FromMilliseconds(300);

    private bool NeedsWarm() =>
        IsEffectivelyVisible && ItemTemplate is not null && _range.Last >= _range.First
        && _range.Last < _items.Count - 1 && _range.Last - _range.First + 1 < WarmTarget();

    private void QueueWarm()
    {
        if (_warmQueued || !NeedsWarm()) return;
        _warmQueued = true;
        //  ⚠️ زمان‌سنج، نه ‎Post‎: کارِ پس‌زمینه‌ای که پشتِ سرِ هم پست شود، در همان
        //  فاصلهٔ دو فریمِ اسکرول هم می‌دود؛ زمان‌سنج فقط در بی‌کاریِ واقعی.
        Avalonia.Threading.DispatcherTimer.RunOnce(WarmOne, WarmGap, Avalonia.Threading.DispatcherPriority.Background);
    }

    private void WarmOne()
    {
        _warmQueued = false;
        if (!NeedsWarm()) return;
        //  کاربر همین حالا می‌چرخاند ⇒ بعداً
        if (AppClock.Mono - _lastScroll < WarmCalm) { QueueWarm(); return; }
        _ahead = _range.Last - _range.First + 2;
        InvalidateMeasure();   // اندازه‌گیریِ بعدی همین یک کارت را زنده می‌کند و دوباره صف می‌گیرد
    }

    /// <summary>برای سنجه‌ها: کارتِ آماده در انبار.</summary>
    public int PooledCount => _pool.Count;

    private Control? Rent(object? item)
    {
        Control? c = null;
        while (_pool.Count > 0 && c is null) c = _pool.Pop();
        if (c is null)
        {
            c = ItemTemplate?.Build(item);
            if (c is null) return null;
            BuiltCount++;
            //  ⛔ داده **پیش از** نشستن در درخت (۱۴۰۵/۰۷/۱۶ — «توی حسابِ قرض‌دار
            //  نمی‌ره»). وارونه‌اش یعنی کارت یک لحظه ‎DataContext‎ِ خودِ بخش را به
            //  ارث می‌برد؛ ‎CommandParameter="{Binding}"‎ همان بخش می‌شد،
            //  ‎RelayCommand<DebtorCardViewModel>.CanExecute‎ با نوعِ غلط استثنا
            //  می‌داد و اتصالِ ‎Command‎ برای همیشه **خالی** می‌ماند — هیچ کارتی
            //  باز نمی‌شد (سنجهٔ ‎round16‎ با کلیکِ واقعیِ ماوس، و لاگِ اتصال).
            c.DataContext = item;
            LogicalChildren.Add(c);
            VisualChildren.Add(c);
        }
        c.DataContext = item;
        c.IsVisible = true;
        return c;
    }

    private void ReleaseAll()
    {
        _ahead = 0;
        foreach (var c in _live.Values) { c.IsVisible = false; _pool.Push(c); }
        _live.Clear();
        _range = (0, -1);
    }

    private void ClearAll()
    {
        _live.Clear();
        _pool.Clear();
        LogicalChildren.Clear();
        VisualChildren.Clear();
        _range = (0, -1);
    }
}
