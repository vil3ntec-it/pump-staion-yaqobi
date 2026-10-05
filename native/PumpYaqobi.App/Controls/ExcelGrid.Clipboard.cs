using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PumpYaqobi.App.ViewModels;
using PumpYaqobi.App.Services;

namespace PumpYaqobi.App.Controls;

//  ⛔ شورا ج۴: بخشی از ‎ExcelGrid‎ — کپی، برش، پیست، انتخابِ همه، برگشت و دوباره. فقط جابه‌جاییِ همان عضوها از ‎ExcelGrid.cs‎، بی تغییرِ یک رفتار.
public partial class ExcelGrid
{
    // ══════════════════════════════════════════════════════════════════════
    //  ══ کپی · برش · پیست · انتخابِ همه · برگشت و دوباره ═══════════════════
    // ══════════════════════════════════════════════════════════════════════
    //
    //  خواستهٔ صاحب ریپو: «Ctrl+C / V / X و Ctrl+Z / Y و Ctrl+A هم باشند…
    //  همه‌شان با دقت و ظرافت کار کنند و هیچ بخشی منطقش خراب یا دست‌کاری
    //  نشود.»
    //
    //  ⛔ و دقیقاً به همین دلیل، **هیچ‌کدامشان درِ تازه‌ای به داده باز نمی‌کند.**
    //  هر نوشتنی از همان یک در رد می‌شود: <see cref="Write"/> رشته را در همان
    //  خاصیتِ ‎…Text‎ی ستون می‌نشاند — مو‌به‌مو همان کاری که تایپِ خودِ کاربر
    //  می‌کند. پس همان ‎Touch()‎، همان ‎Apply()‎، همان ذخیرهٔ تأخیری و همان
    //  جمع‌های بالای صفحه. هیچ محاسبه‌ای دور زده نمی‌شود و هیچ سرویسی از کنار
    //  صدا زده نمی‌شود.
    //
    //  ⛔ **ردیفِ قفل‌شده (‎ILockedRow‎) و ستونِ خواندنی نوشته نمی‌شوند** — همان
    //  قاعده‌ای که ‎BeginningEdit‎ برای تایپ دارد. پیش از این ‎Delete‎ این قید
    //  را نداشت و می‌شد ردیفِ 📦 خریدِ مخزن را خالی کرد؛ حالا هر چهار راه یک
    //  قاعده دارند.
    //
    //  ⚠️ **پیست ردیف نمی‌سازد.** آن‌چه از قابِ جدول بیرون بزند دور ریخته
    //  می‌شود. ساختنِ خودکارِ ردیف یعنی یک ‎Ctrl+V‎ی ناخواسته صدها ردیفِ واقعی
    //  در دفتر بسازد؛ ردیف با ‎Ctrl+عدد‎ یا دکمهٔ «➕ ردیف» ساخته می‌شود، آگاهانه.
    //
    //  ⚠️ **برگشت (‎Ctrl+Z‎) فقط «ویرایشِ خانه» را برمی‌گرداند** — تایپ، خالی
    //  کردن، برش و پیست. ساختن و حذفِ ردیف، حذفِ حساب و هر چیزی که در دیتابیس
    //  ردیف می‌سازد یا می‌برد **در آن نیست**: برگرداندنشان یک «تاریخچهٔ
    //  عکس‌فوریِ کلِ دیتابیس» می‌خواهد که ساخته نشده، و وانمود کردن به داشتنش
    //  از نداشتنش بدتر است. برای حذف، سطلِ زباله سرِ جایش هست.
    //
    //  ⚠️ و تاریخچه مالِ **همین جدول** است، نه سراسری: با عوض شدنِ فهرست (ماهِ
    //  دیگر، حسابِ دیگر) پاک می‌شود، و پیش از هر برگشت سنجیده می‌شود که آن
    //  ردیف هنوز در همین جدول هست (<see cref="Live"/>) — وگرنه نوشتن روی
    //  ردیفی که دیگر نیست، ذخیره‌ای می‌ساخت که هیچ‌کس نخواسته بود.

    /// <summary>یک خانهٔ عوض‌شده: چه بود و چه شد.</summary>
    private readonly record struct Edit(object Item, System.Reflection.PropertyInfo Prop,
                                        string? Before, string? After);


    //  ⛔ پشته دیگر مالِ همین جدول نیست — ‎Services.UndoHub‎ی کلِ برنامه است.
    //  با پشتهٔ جدا، ‎Ctrl+Z‎ فقط وقتی کار می‌کرد که فوکوس همین‌جا بود، و حذفِ
    //  ردیف و حساب در هیچ تاریخچه‌ای نبودند (سنجهٔ ‎undokeys‎ گرفتش).
    private sealed class CellStep : Services.UndoHub.IStep
    {
        private readonly WeakReference<ExcelGrid> _grid;
        private readonly List<Edit> _g;
        public CellStep(ExcelGrid grid, List<Edit> g) { _grid = new(grid); _g = g; }
        public string What => _g.Count == 1 ? "ویرایشِ خانه" : PumpYaqobi.Application.Localization.Shamsi.Money(_g.Count) + " خانه";
        public Task<bool> UndoAsync() => Task.FromResult(_grid.TryGetTarget(out var x) && x.Replay(_g, forward: false));
        public Task<bool> RedoAsync() => Task.FromResult(_grid.TryGetTarget(out var x) && x.Replay(_g, forward: true));
    }

    /// <summary>عکسِ خانه‌ای که همین حالا ویرایشش باز شد (برای ‎Ctrl+Z‎).</summary>
    private (object Item, System.Reflection.PropertyInfo Prop, string? Before)? _snap;

    /// <summary>فهرستِ زندهٔ همین جدول، به ترتیبِ خودش.</summary>
    private List<object> AllRows() =>
        ItemsSource?.Cast<object>().Where(o => o is not null).ToList() ?? new List<object>();

    /// <summary>این ردیف هنوز در همین جدول هست؟</summary>
    private bool Live(object item) =>
        (ItemsSource ?? (_parkedAway ? _parked : null)) is System.Collections.IEnumerable src
        && src.Cast<object>().Any(o => ReferenceEquals(o, item));

    /// <summary>فهرستِ تازه ⇒ تاریخچهٔ کهنه دیگر معنا ندارد.</summary>
    private void ForgetHistory() { _snap = null; }

    /// <summary>
    /// کادرِ انتخاب‌شده: ردیف‌ها (به ترتیبِ خودِ جدول، نه ترتیبِ انتخاب) و
    /// ستون‌های بینِ لنگر و سر. همان مستطیلی که ‎Delete‎ از روزِ اول خالی
    /// می‌کرد — حالا کپی و برش هم از همین می‌خوانند تا هر چهار کلید یک چیز
    /// ببینند.
    /// </summary>
    private (List<object> Rows, List<DataGridColumn> Cols)? Selection()
    {
        var cols = VisibleCols();
        if (cols.Count == 0) return null;

        var cur = CurIndex(cols);
        var lo = _colAnchor < 0 ? cur : Math.Min(_colAnchor, _colHead);
        var hi = _colAnchor < 0 ? cur : Math.Max(_colAnchor, _colHead);
        if (lo < 0 || hi < 0) return null;
        lo = Math.Max(0, lo); hi = Math.Min(cols.Count - 1, hi);

        var rows = SelectedItems.Cast<object>().Where(o => o is not null).ToList();
        if (rows.Count == 0 && SelectedItem is not null) rows.Add(SelectedItem);
        if (rows.Count == 0) return null;

        // ⚠️ ‎SelectedItems‎ ترتیبِ **انتخاب** را می‌دهد، نه ترتیبِ جدول. برای
        // کپی این فرق می‌کند: انتخاب از پایین به بالا، متنِ وارونه می‌داد.
        var all = AllRows();
        if (all.Count > 0)
            rows = rows.OrderBy(o => { var i = all.FindIndex(x => ReferenceEquals(x, o)); return i < 0 ? int.MaxValue : i; })
                       .ToList();

        return (rows, cols.GetRange(lo, hi - lo + 1));
    }

    /// <summary>خواندنِ نوشتهٔ یک خانه — برای کپی. ستونِ خواندنی هم خوانده می‌شود.</summary>
    private static string ReadCell(object item, DataGridColumn col)
    {
        var path = PathOf(col);
        if (string.IsNullOrEmpty(path)) return "";
        var p = item.GetType().GetProperty(path);
        if (p is null || !p.CanRead) return "";
        try { return p.GetValue(item)?.ToString() ?? ""; } catch { return ""; }
    }

    /// <summary>
    /// نوشتنِ یک خانه — <b>تنها</b> جایی که کپی/برش/پیست/‎Delete‎ به داده دست
    /// می‌زنند. برمی‌گرداند که چیزی عوض شد یا نه، و تغییر را در ‎log‎ می‌نویسد.
    /// </summary>
    private static bool Write(object item, DataGridColumn col, string value, List<Edit> log)
    {
        if (col.IsReadOnly) return false;
        if (item is ViewModels.ILockedRow { IsLocked: true }) return false;
        if (LockedForWriting()) return false;
        var path = PathOf(col);
        if (string.IsNullOrEmpty(path)) return false;
        var p = item.GetType().GetProperty(path);
        if (p is null || !p.CanWrite || p.PropertyType != typeof(string)) return false;

        var before = p.GetValue(item) as string;
        if (string.Equals(before, value, StringComparison.Ordinal)) return false;
        try { p.SetValue(item, value); } catch { return false; }
        log.Add(new Edit(item, p, before, value));
        return true;
    }

    private void PushUndo(List<Edit> group)
    {
        if (group.Count == 0) return;
        Services.UndoHub.Push(new CellStep(this, group));
    }

    // ── عکسِ پیش و پسِ تایپِ خودِ کاربر ────────────────────────────────────
    //
    // ⚠️ عکس در ‎BeginningEdit‎ گرفته می‌شود، نه در ‎CellEditEnding‎: آن‌جا
    // مطمئنیم مقدار هنوز کهنه است. و مقایسه یک تیک **بعد** از پایانِ ویرایش
    // انجام می‌شود، چون نشستنِ مقدار در ردیف کارِ خودِ اتصال است و ممکن است
    // همان لحظه نباشد.
    private void SnapEdit(DataGridColumn? col, DataGridRow? row)
    {
        _snap = null;
        var path = PathOf(col);
        if (string.IsNullOrEmpty(path)) return;
        if (row?.DataContext is not { } item) return;
        var p = item.GetType().GetProperty(path);
        if (p is null || !p.CanWrite || p.PropertyType != typeof(string)) return;
        try { _snap = (item, p, p.GetValue(item) as string); } catch { _snap = null; }
    }

    private void CaptureEdit()
    {
        if (_snap is not { } s) return;
        _snap = null;
        Dispatcher.UIThread.Post(() =>
        {
            if (!Live(s.Item)) return;
            string? after;
            try { after = s.Prop.GetValue(s.Item) as string; } catch { return; }
            if (string.Equals(s.Before, after, StringComparison.Ordinal)) return;
            PushUndo(new List<Edit> { new(s.Item, s.Prop, s.Before, after) });
        }, DispatcherPriority.Background);
    }

    private bool Replay(List<Edit> group, bool forward)
    {
        var any = false;
        // وارونه، تا اگر دو خانه روی هم نوشته شده بودند ترتیب برعکس باز شود
        for (var i = group.Count - 1; i >= 0; i--)
        {
            var ed = group[i];
            if (!Live(ed.Item)) continue;
            try { ed.Prop.SetValue(ed.Item, forward ? ed.After : ed.Before); any = true; } catch { }
        }
        return any;
    }

    // ── کلیپ‌بورد ─────────────────────────────────────────────────────────

    // ⚠️ نامش ‎Clip‎ نیست: ‎Visual.Clip‎ از پیش هست و هم‌نامی آن را پنهان می‌کرد.
    private IClipboard? Board => TopLevel.GetTopLevel(this)?.Clipboard;

    /// <summary>کادرِ انتخابی به‌شکلِ جدولِ Tab-دار — همان چیزی که اکسل می‌فهمد.</summary>
    private string? SelectionText()
    {
        if (Selection() is not { } r) return null;
        var sb = new System.Text.StringBuilder();
        for (var y = 0; y < r.Rows.Count; y++)
        {
            if (y > 0) sb.Append('\n');
            for (var x = 0; x < r.Cols.Count; x++)
            {
                if (x > 0) sb.Append('\t');
                sb.Append(ReadCell(r.Rows[y], r.Cols[x]));
            }
        }
        return sb.ToString();
    }

    private async Task CopySelectionAsync()
    {
        if (SelectionText() is not { } text) return;
        if (Board is not { } clip) return;
        try { await clip.SetTextAsync(text); } catch { }
    }

    private async Task CutSelectionAsync()
    {
        if (IsReadOnly) return;
        await CopySelectionAsync();
        ClearSelectedCells();
    }

    private async Task PasteAsync()
    {
        if (IsReadOnly) return;
        if (Board is not { } clip) return;

        string? text = null;
        try { text = await clip.GetTextAsync(); } catch { }
        if (string.IsNullOrEmpty(text)) return;

        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var cells = lines.Select(l => l.Split('\t')).ToList();
        // خطِ خالیِ آخر (هر کپی از اکسل یکی دارد) شمرده نشود
        while (cells.Count > 1 && cells[^1].Length == 1 && cells[^1][0].Length == 0)
            cells.RemoveAt(cells.Count - 1);
        if (cells.Count == 0) return;

        var cols = VisibleCols();
        var all = AllRows();
        if (cols.Count == 0 || all.Count == 0) return;

        var log = new List<Edit>();

        // ⚠️ یک خانه روی یک کادرِ چندتایی ⇒ همان یک مقدار در همهٔ خانه‌ها —
        // همان کاری که اکسل می‌کند و کاربر انتظارش را دارد.
        if (cells.Count == 1 && cells[0].Length == 1 && Selection() is { } r
            && (r.Rows.Count > 1 || r.Cols.Count > 1))
        {
            foreach (var item in r.Rows)
                foreach (var col in r.Cols)
                    Write(item, col, cells[0][0], log);
        }
        else
        {
            var startCol = Math.Max(0, CurIndex(cols));
            var startRow = SelectedItem is { } cur
                ? Math.Max(0, all.FindIndex(x => ReferenceEquals(x, cur)))
                : 0;

            for (var y = 0; y < cells.Count; y++)
            {
                var ri = startRow + y;
                if (ri >= all.Count) break;               // بیرونِ جدول ⇒ دور ریخته می‌شود
                for (var x = 0; x < cells[y].Length; x++)
                {
                    var ci = startCol + x;
                    if (ci >= cols.Count) break;
                    Write(all[ri], cols[ci], cells[y][x], log);
                }
            }
        }

        if (log.Count == 0) return;
        PushUndo(log);
        AppHost.Current.Toasts.Show($"📋 {log.Count} خانه چسبانده شد", ToastKind.Ok);
    }

    /// <summary>‎Ctrl+A‎ — همهٔ ردیف‌ها و همهٔ ستون‌ها.</summary>
    private void SelectAllCells()
    {
        var cols = VisibleCols();
        if (cols.Count == 0) return;
        SelectAll();
        _colAnchor = 0;
        _colHead = cols.Count - 1;
        PaintRange();
    }

    /// <summary>
    /// ویرایشِ بازِ همین جدول را همین حالا می‌نشاند — پیش از ‎Ctrl+S‎، وگرنه
    /// خانه‌ای که کاربر وسطش بود ذخیره نمی‌شد.
    /// </summary>
    public void CommitNow()
    {
        if (_editing) CommitEdit(DataGridEditingUnit.Cell, true);
    }

    /// <summary>
    /// خانهٔ بازِ جدولی که همین حالا فوکوس دارد را می‌نشاند — و بعد ردیفش را
    /// روی دیسک.
    ///
    /// ⚠️ از روی <b>فوکوس</b> پیدا می‌شود، نه از ویومدل: یک صفحه می‌تواند چند
    /// جدول داشته باشد (ورق سه تا دارد) و فقط آن یکی که کاربر داخلش است
    /// ویرایشِ باز دارد.
    ///
    /// ⛔ تنها جای این کار است: <c>Ctrl+S</c> و بسته شدنِ برنامه هر دو از
    /// همین‌جا می‌آیند. دو نسخه یعنی روزی یکی‌شان از قلم می‌افتد — و آن یکی
    /// دقیقاً همان است که کاربر وسطِ تایپ ✕ را می‌زند.
    /// </summary>
    public static void CommitFocused(TopLevel? top)
    {
        if (top?.FocusManager?.GetFocusedElement() is not Visual v) return;
        var grid = v.FindAncestorOfType<ExcelGrid>();
        if (grid is null) return;
        grid.CommitNow();
        grid.FlushDirtyRows();
    }
}
