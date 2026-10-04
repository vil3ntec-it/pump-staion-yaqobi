using System.Collections;
using System.Collections.Concurrent;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using PumpYaqobi.Domain;

namespace PumpYaqobi.App.Controls;

/// <summary>
/// ══ پیشنهادِ خودکار — همان ‎data-suggest‎ی سایت ═══════════════════════════════
///
/// خواستهٔ صاحب ریپو: «یک بخشِ مهم تو برنامه نیست: پیشنهادیِ خودکارِ تکمیلِ
/// کلمه‌ها… توی سایت بود؛ بعضی بخش‌ها مثلِ پارچه‌ها اسمِ کارمندها پیشنهاد
/// می‌شد یا فاکتور.»
///
/// سایت دو منبع داشت: فهرست‌های نام‌دارِ خودِ برنامه (‎staff-names-dl‎، …) و
/// دستیارِ تایپ که از آن‌چه کاربر قبلاً نوشته یاد می‌گرفت. این‌جا هر دو یک‌جا:
///
///   • کلیدِ نام‌دار (‎Suggest.Key="staff"‎ روی ستون یا کادر) ⇒ ‎Provide(key, …)‎
///   • و **همیشه** مقدارهای همان ستونِ همان جدول (یادگیری از خودِ دفتر)
///
/// قاعده‌های سایت، عیناً: ↑↓ گرفته نمی‌شوند (ناوبریِ جدول کار می‌کند)، ‎Tab‎ روی
/// پیشنهادها می‌چرخد و بعد از آخری لیست بسته می‌شود تا ‎Tab‎ به خانهٔ بعد برود،
/// ‎Enter‎ پیشنهادِ روشن را برمی‌دارد، ‎Esc‎ می‌بندد، کلیک هم برمی‌دارد. مقدارِ
/// کادر فقط با کارِ صریحِ کاربر عوض می‌شود.
///
/// ⚠️ یک پاپ‌آپ برای کلِ برنامه، ساخته‌شده در نخستین نیاز — نه یکی در هر خانه.
/// </summary>
public static class Suggest
{
    /// <summary>کلیدِ فهرستِ نام‌دار روی ستونِ جدول یا کادرِ تایپ.</summary>
    public static readonly AttachedProperty<string?> KeyProperty =
        AvaloniaProperty.RegisterAttached<AvaloniaObject, string?>("Key", typeof(Suggest));

    public static string? GetKey(AvaloniaObject o) => o.GetValue(KeyProperty);
    public static void SetKey(AvaloniaObject o, string? v) => o.SetValue(KeyProperty, v);

    /// <summary>
    /// ══ «/هارون» — فهرستِ پس از خط‌کج (۱۴۰۵/۰۷/۱۷) ═══════════════════════════
    /// ستونی که این را دارد، پس از «/» نامِ حساب را از همین فهرست تکمیل می‌کند
    /// («/ها» ⇒ «/هارون»، با Tab یا Enter). ⛔ همان تکملهٔ درون‌خطی، همان قاعدهٔ
    /// «پذیرفته‌نشده ذخیره نمی‌شود».
    /// </summary>
    public static readonly AttachedProperty<string?> SlashKeyProperty =
        AvaloniaProperty.RegisterAttached<AvaloniaObject, string?>("SlashKey", typeof(Suggest));

    public static string? GetSlashKey(AvaloniaObject o) => o.GetValue(SlashKeyProperty);
    public static void SetSlashKey(AvaloniaObject o, string? v) => o.SetValue(SlashKeyProperty, v);

    /// <summary>
    /// شورا، ث۲ — کلیدِ راهنمای ستون (<c>Services.Hints</c>): نوشتهٔ کم‌رنگِ کادرِ خالی
    /// و یک «💡 می‌دانستید؟» برای نخستین بار. ⛔ هیچ رفتاری عوض نمی‌شود.
    /// </summary>
    public static readonly AttachedProperty<string?> HintProperty =
        AvaloniaProperty.RegisterAttached<AvaloniaObject, string?>("Hint", typeof(Suggest));

    public static string? GetHint(AvaloniaObject o) => o.GetValue(HintProperty);
    public static void SetHint(AvaloniaObject o, string? v) => o.SetValue(HintProperty, v);

    // ══ منبع‌های نام‌دار ═══════════════════════════════════════════════════
    private static readonly ConcurrentDictionary<string, Func<Task<IEnumerable<string>>>> _providers = new();
    private static readonly ConcurrentDictionary<string, (DateTime At, List<string> Items)> _cache = new();
    private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(20);

    /// <summary>ثبتِ یک فهرستِ نام‌دار (مثلاً «staff» = نامِ کارمندان).</summary>
    public static void Provide(string key, Func<Task<IEnumerable<string>>> source) => _providers[key] = source;

    /// <summary>
    /// ══ گرم کردنِ فهرست‌های نام‌دار، سرِ بالا آمدنِ برنامه ════════════════════
    ///
    /// گزارشِ صاحب ریپو: «اسمِ کارمندان نمی‌آید توی کادرش… در حالی که من توی
    /// حاضری و معاشات اسمِ کارمندها را هم نوشتم.» و «وقتی اسمِ طرف را می‌خواهم
    /// بزنم پیشنهاد نمی‌شود.»
    ///
    /// ⛔ ریشه اینجاست، نه در فهرست: <see cref="Of"/> عمداً منتظرِ دیتابیس
    /// نمی‌ماند — کَشِ خالی را همان لحظه پس می‌دهد و در پس‌زمینه پُرش می‌کند.
    /// پس <b>نخستین</b> باری که کاربر در یک کادر تایپ می‌کرد، فهرست هنوز خالی
    /// بود و هیچ تکمله‌ای نمی‌آمد. دومین بار کار می‌کرد — و همین «گاهی می‌آید
    /// گاهی نه»ی گیج‌کننده بود.
    ///
    /// حالا همان لحظهٔ ثبتِ منبع یک بار خوانده می‌شود، پس تا کاربر به کادر
    /// برسد فهرست آماده است.
    /// </summary>
    /// <summary>کَشِ یک کلید را دور بریز تا نخستین خواندنِ بعدی از نو بپرسد (سنجه‌ها).</summary>
    public static void Forget(string key) => _cache.TryRemove(key, out _);

    public static void Warm(params string[] keys)
    {
        foreach (var k in keys) Of(k);
    }

    /// <summary>
    /// فهرستِ یک کلید — از کَش، همان لحظه؛ تازه‌سازی در پس‌زمینه. مسیرِ تایپ
    /// نباید منتظرِ دیتابیس بماند.
    /// </summary>
    public static IReadOnlyList<string> Of(string? key)
    {
        if (string.IsNullOrEmpty(key) || !_providers.TryGetValue(key, out var src)) return Array.Empty<string>();
        var hit = _cache.TryGetValue(key, out var c);
        if (!hit || AppClock.Mono - c.At > Ttl)
        {
            _cache[key] = (AppClock.Mono, hit ? c.Items : new List<string>());
            _ = Task.Run(async () =>
            {
                try
                {
                    var items = (await src()).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().ToList();
                    _cache[key] = (AppClock.Mono, items);
                }
                catch { /* پیشنهاد رفاه است، نه داده */ }
            });
        }
        return hit ? c.Items : Array.Empty<string>();
    }

    // ══ کادرِ تایپِ مستقل (بیرونِ جدول) ═══════════════════════════════════
    static Suggest()
    {
        KeyProperty.Changed.AddClassHandler<TextBox>((tb, _) =>
        {
            tb.GotFocus -= OnBoxFocus; tb.GotFocus += OnBoxFocus;
        });
    }

    private static void OnBoxFocus(object? sender, GotFocusEventArgs e)
    {
        if (sender is TextBox tb) Attach(tb, Of(GetKey(tb)), Array.Empty<string>());
    }

    // ══════════════════════════════════════════════════════════════════════
    //  ══ تکمیلِ داخلِ خودِ کادر — نه پاپ‌آپ ═══════════════════════════════════
    // ══════════════════════════════════════════════════════════════════════
    //
    //  گزارشِ صاحب ریپو با عکس (۱۴۰۵/۰۶/۲۷): «این تکمیلِ خودکار جوری است که
    //  پیشنهاد پاپ‌آپ می‌آید — نه. با همان «س» که دادم، داخلِ خودِ کادر
    //  تکمیلش را نشان بده که با زدنِ Tab یا Enter آن جمله تکمیل شود.»
    //
    //  پس فهرستِ شناور رفت و جایش همان چیزی آمد که مرورگر و اکسل می‌کنند:
    //
    //      کاربر «س» می‌زند  ⇒  کادر: «س|لام من هارون هستم»
    //                            (بخشِ تکمیل، انتخاب‌شده و برجسته)
    //      Tab یا Enter      ⇒  جمله می‌ماند و انتخاب برداشته می‌شود
    //      تایپِ ادامه       ⇒  حرفِ تازه جای بخشِ انتخاب‌شده می‌نشیند
    //      Backspace / Esc   ⇒  تکمیل می‌رود و همان چیزی می‌ماند که تایپ شده
    //
    //  ⚠️ **مهم‌ترین قاعده: تکمیلِ پذیرفته‌نشده هرگز در داده نمی‌نشیند.**
    //  متنِ داخلِ کادر برای دیدن است؛ اگر کاربر بی‌آن‌که بپذیرد جایی دیگر
    //  کلیک کند، پیش از هر ذخیره‌ای کادر به همان چیزی برمی‌گردد که خودش
    //  تایپ کرده بود (‎OnBoxLost‎ ⇒ ‎Revert‎). وگرنه یک «س» می‌توانست در
    //  دفتر «سلام من هارون هستم» ذخیره شود — و این دیگر رفاه نیست، خرابیِ
    //  داده است. سنجهٔ ۶ در ‎verify‎ همین را قفل کرده.
    //
    //  ⚠️ فقط از **سرِ** واژه تکمیل می‌شود (‎StartsWith‎)، نه هر جای متن:
    //  «تکمیل» یعنی ادامهٔ چیزی که تایپ شده. جست‌وجوی «شامل» جایش در فهرست
    //  بود، نه این‌جا.

    private static TextBox? _box;
    private static List<string> _all = new();
    /// <summary>نام‌های حساب برای پسِ «/» — خالی یعنی این کادر «/» ندارد.</summary>
    private static List<string> _slash = new();

    /// <summary>آن‌چه کاربر واقعاً تایپ کرده — تکیه‌گاهِ برگشت.</summary>
    private static string _typed = "";

    /// <summary>تکملهٔ نشان‌داده‌شده (کلِ جمله)، یا خالی.</summary>
    private static string _ghost = "";

    /// <summary>خودمان داریم متنِ کادر را عوض می‌کنیم.</summary>
    private static bool _busy;

    /// <summary>کلیدِ قبلی پاک‌کن بود ⇒ تکمیلِ تازه پیشنهاد نکن.</summary>
    private static bool _erasing;

    /// <summary>برای سنجش: تکمله‌ای نشان داده می‌شود؟ (۰ یا ۱)</summary>
    public static int Showing => _ghost.Length > 0 ? 1 : 0;

    /// <summary>برای سنجش: کلِ جملهٔ پیشنهادی، یا خالی.</summary>
    public static string Ghost => _ghost;

    /// <summary>برای سنجش: همان چیزی که کاربر تایپ کرده.</summary>
    public static string TypedText => _typed;

    /// <summary>سازگاری با سنجش‌های قدیمی: ۰ یعنی تکمله هست، ‎-1‎ یعنی نیست.</summary>
    public static int Active => _ghost.Length > 0 ? 0 : -1;

    /// <summary>به یک کادرِ تایپ وصل می‌شود: نام‌دار + آموخته‌ها.</summary>
    public static void Attach(TextBox box, IReadOnlyList<string> named, IReadOnlyList<string> learned,
                              IReadOnlyList<string>? slash = null)
    {
        Detach();
        _all = named.Concat(learned).Where(s => !string.IsNullOrWhiteSpace(s))
                    .Select(s => s.Trim()).Distinct()
                    // کوتاه‌ترین اول: تکمله‌ای که زودتر تمام شود کم‌آزارتر است
                    .OrderBy(s => s.Length).ThenBy(s => s, StringComparer.OrdinalIgnoreCase)
                    .ToList();
        _slash = (slash ?? Array.Empty<string>()).Where(s => !string.IsNullOrWhiteSpace(s))
                    .Select(s => s.Trim()).Distinct()
                    .OrderBy(s => s.Length).ThenBy(s => s, StringComparer.OrdinalIgnoreCase)
                    .ToList();
        if (_all.Count == 0 && _slash.Count == 0) return;

        _box = box;
        _typed = box.Text ?? "";
        box.PropertyChanged += OnBoxText;
        box.LostFocus += OnBoxLost;
        box.AddHandler(InputElement.KeyDownEvent, OnBoxKey, RoutingStrategies.Tunnel);
        box.DetachedFromVisualTree += OnBoxGone;
    }

    private static void Detach()
    {
        Ghostly(false);
        if (_box is { } b)
        {
            b.PropertyChanged -= OnBoxText;
            b.LostFocus -= OnBoxLost;
            b.RemoveHandler(InputElement.KeyDownEvent, OnBoxKey);
            b.DetachedFromVisualTree -= OnBoxGone;
        }
        _box = null; _ghost = ""; _typed = ""; _erasing = false;
    }

    private static void OnBoxGone(object? s, VisualTreeAttachmentEventArgs e) => Detach();

    /// <summary>
    /// فوکوس رفت: تکملهٔ پذیرفته‌نشده **همین‌جا** برداشته می‌شود — پیش از
    /// آن‌که جدول مقدارِ کادر را در ردیف بنشاند.
    /// </summary>
    private static void OnBoxLost(object? s, RoutedEventArgs e)
    {
        Revert();
        Detach();
    }

    /// <summary>
    /// پیش از این‌که مقدارِ کادر جایی ذخیره شود: تکملهٔ پذیرفته‌نشده برداشته
    /// شود. ‎ExcelGrid‎ این را در ‎CellEditEnding‎ می‌زند — یعنی درست پیش از
    /// آن‌که جدول متنِ کادر را در ردیف بنشاند. فوکوس‌رفتن هم همین را می‌کند،
    /// ولی تنها تکیه بر آن کافی نیست: ‎CommitEdit‎ی برنامه‌ای فوکوس را نمی‌برد.
    /// </summary>
    public static void Settle() => Revert();

    private static void OnBoxText(object? s, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property != TextBox.TextProperty || _busy || _box is null) return;
        _ghost = "";
        Ghostly(false);
        _typed = _box.Text ?? "";
        if (_erasing) { _erasing = false; return; }

        // ⚠️ یک تیک بعد، نه همین حالا. ‎TextBox‎ اول متن را عوض می‌کند و بعد
        // مکان‌نما را جابه‌جا؛ اگر همین‌جا بپرسیم «مکان‌نما تهِ متن است؟» جوابِ
        // حرفِ اولِ هر خانه «نه» است و تکمیل هیچ‌وقت شروع نمی‌شود. (همین باگ
        // را ‎verify‎ گرفت: کادر «ح» بود و تکمله صفر.)
        if (_queued) return;
        _queued = true;
        Dispatcher.UIThread.Post(() => { _queued = false; Propose(); }, DispatcherPriority.Input);
    }

    private static bool _queued;

    /// <summary>ادامهٔ آن‌چه تایپ شده را داخلِ کادر می‌گذارد و انتخابش می‌کند.</summary>
    private static void Propose()
    {
        if (_box is not { } box) return;
        var typed = box.Text ?? "";
        if (typed.Length == 0) return;
        // فقط وقتی مکان‌نما تهِ متن است؛ ویرایشِ وسطِ جمله تکمیل نمی‌خواهد
        if (box.CaretIndex < typed.Length) return;

        var hit = SlashHit(typed)
                  ?? _all.FirstOrDefault(v => v.Length > typed.Length
                                           && v.StartsWith(typed, StringComparison.CurrentCultureIgnoreCase));
        if (hit is null) return;

        _busy = true;
        try
        {
            box.Text = hit;
            // ⚠️ ترتیب مهم است: ‎CaretIndex‎ انتخاب را جمع می‌کند، پس اول
            // مکان‌نما و بعد انتخاب. وگرنه تکمله دیده می‌شود ولی برجسته نیست
            // و کاربر نمی‌فهمد کدام تکه پیشنهاد است (‎verify‎ همین را گرفت).
            box.CaretIndex = hit.Length;
            box.SelectionStart = typed.Length;
            box.SelectionEnd = hit.Length;
        }
        finally { _busy = false; }
        _typed = typed;
        _ghost = hit;
        Ghostly(true);
    }

    /// <summary>
    /// «ابراهیم /ها» ⇒ «ابراهیم /هارون». فقط تکهٔ پسِ آخرین «/»، و فقط وقتی
    /// دستِ‌کم یک حرف پس از آن تایپ شده. ⚠️ رقم پس از «/» (تاریخ، «12/3») نه.
    /// </summary>
    public static string? SlashHit(string typed) => SlashHit(typed, _slash);

    public static string? SlashHit(string typed, IReadOnlyList<string> names)
    {
        if (names.Count == 0) return null;
        var i = typed.LastIndexOf('/');
        if (i < 0) return null;
        var seg = typed[(i + 1)..].TrimStart();
        if (seg.Length == 0 || !char.IsLetter(seg[0])) return null;
        var name = names.FirstOrDefault(v => v.Length > seg.Length
                                          && v.StartsWith(seg, StringComparison.CurrentCultureIgnoreCase));
        return name is null ? null : typed + name[seg.Length..];
    }

    /// <summary>
    /// تکمله کم‌رنگ دیده شود، نه هم‌رنگِ نوشتهٔ کاربر (سبکِ ‎TextBox.ghost‎).
    /// ⚠️ هر جا تکمله برمی‌خیزد (پذیرش، برگشت، تایپِ تازه، رفتنِ فوکوس)
    /// کلاس هم برمی‌خیزد — وگرنه انتخابِ واقعیِ بعدیِ کاربر هم نامرئی می‌شد.
    /// </summary>
    private static void Ghostly(bool on)
    {
        if (_box is not { } b) return;
        if (on) { if (!b.Classes.Contains("ghost")) b.Classes.Add("ghost"); }
        else b.Classes.Remove("ghost");
    }

    /// <summary>Tab/Enter: جمله می‌ماند و انتخاب برداشته می‌شود.</summary>
    private static bool Accept()
    {
        if (_ghost.Length == 0 || _box is not { } box) return false;
        _busy = true;
        try
        {
            var end = (box.Text ?? "").Length;
            box.SelectionStart = box.SelectionEnd = end;
            box.CaretIndex = end;
        }
        finally { _busy = false; }
        _typed = box.Text ?? "";
        _ghost = "";
        Ghostly(false);
        return true;
    }

    /// <summary>تکمله را برمی‌دارد و همان چیزی می‌ماند که تایپ شده بود.</summary>
    private static bool Revert()
    {
        if (_ghost.Length == 0 || _box is not { } box) return false;
        _busy = true;
        try
        {
            box.Text = _typed;
            box.SelectionStart = box.SelectionEnd = _typed.Length;
            box.CaretIndex = _typed.Length;
        }
        finally { _busy = false; }
        _ghost = "";
        Ghostly(false);
        return true;
    }

    private static void OnBoxKey(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            // پاک‌کن‌ها: اول تکمله می‌رود (مثلِ مرورگر)، بارِ بعد خودِ حرف
            case Key.Back:
            case Key.Delete:
                _erasing = true;
                if (Revert()) e.Handled = true;
                return;

            case Key.Tab when _ghost.Length > 0:
            case Key.Enter when _ghost.Length > 0:
                Accept();
                e.Handled = true;      // همین یک بار؛ Tab/Enterِ بعدی کارِ خودش را می‌کند
                return;

            case Key.Escape when _ghost.Length > 0:
                Revert();
                e.Handled = true;
                return;

            // ⛔ فلش و End تکمله را **برمی‌دارند**، نمی‌پذیرند (۱۴۰۵/۰۷/۱۸).
            // گزارشِ صاحب ریپو: «وقتی می‌نویسم جملات… بیخود اصلاح می‌شن؛ فقط
            // اون پلیس‌هولدر باشه تا پیشنهاد بده.» در جدول، فلش یعنی «خانهٔ
            // بعد»؛ پذیرفتنِ تکمله همین‌جا یعنی «علی» + ← ⇒ «علی احمدی» در
            // دفتر، بی آن‌که کاربر خواسته باشد. پذیرفتن فقط با Tab یا Enter.
            // جلوی خودِ کلید را نمی‌گیریم: جابه‌جایی همان است که بود.
            case Key.Right:
            case Key.Left:
            case Key.End:
            case Key.Home:
            case Key.Up:
            case Key.Down:
                Revert();
                return;

            default:
                _erasing = false;
                return;
        }
    }

    // ══ کمکی برای جدول: مقدارهای همان ستون ═══════════════════════════════
    /// <summary>مقدارهای متنیِ همان ستون در همهٔ ردیف‌های همین جدول — یادگیری از خودِ دفتر.</summary>
    public static IReadOnlyList<string> ColumnValues(IEnumerable? items, DataGridColumn column)
    {
        if (items is null || column is not DataGridBoundColumn bc || bc.Binding is not Binding b || string.IsNullOrEmpty(b.Path))
            return Array.Empty<string>();
        var path = b.Path;
        System.Reflection.PropertyInfo? prop = null;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var list = new List<string>();
        foreach (var it in items)
        {
            if (it is null) continue;
            prop ??= it.GetType().GetProperty(path);
            if (prop is null || prop.PropertyType != typeof(string)) return Array.Empty<string>();
            if (prop.GetValue(it) is string s && s.Trim() is { Length: > 1 } t && seen.Add(t)) list.Add(t);
            if (list.Count >= 400) break;
        }
        return list;
    }
}
