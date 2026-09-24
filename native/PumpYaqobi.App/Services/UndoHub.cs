using PumpYaqobi.Persistence;
using PumpYaqobi.Services.Data;

namespace PumpYaqobi.App.Services;

/// <summary>
/// ══ ‎Ctrl+Z‎ و ‎Ctrl+Y‎ برای کلِ برنامه — یک پشته، نه یکی برای هر جدول ═══════════
///
/// گزارشِ صاحب ریپو (۱۴۰۵/۰۷/۱۲): «کنترول زد اصلن کار نمیکنه — هر چیزی از
/// کادر حذف بشه یا حسابی رو حذف بشه و غیره — به عقب نمیره و کنترول وای هم
/// جلو نمیره.»
///
/// سه ریشه، که سنجهٔ ‎undokeys‎ با کلیدِ واقعی نشانشان داد:
///   ۱) برگشت مالِ <b>هر جدول</b> بود و فقط وقتی کار می‌کرد که فوکوس همان‌جا
///      باشد. یک کلیک روی دکمه یا جای خالی، و ‎Ctrl+Z‎ به هیچ‌کس نمی‌رسید.
///   ۲) حذفِ ردیف و حذفِ حساب اصلاً در هیچ تاریخچه‌ای نبودند.
///   ۳) کلیکِ دوم روی همان خانه ویرایش را باز می‌کرد و از آن لحظه هر کلیدی
///      مالِ کادرِ تایپ بود (آن یکی در ‎ExcelGrid‎ بسته شد).
///
/// حالا دو جنس قدم در <b>یک</b> پشته می‌نشینند، به ترتیبِ واقعیِ کارِ کاربر:
///   • ویرایشِ خانه (تایپ، خالی کردن، برش، پیست) — از ‎ExcelGrid‎، در حافظه.
///   • حذف (ردیف، حساب، ورق…) — از خودِ <b>سطلِ زباله</b>: هر حذفی در همان
///     ذخیره یک قلمِ سطل می‌گذارد (‎PumpDbContext.TrashAdded‎)، پس برگشتِ
///     حذف همان «بازگردانی از سطل» است که از روزِ اول آزموده شده، و «دوباره»
///     همان رکوردها را از همان راهِ حذف دوباره می‌برد.
///
/// ⛔ <b>هیچ منطقِ حسابی این‌جا نیست</b>: قدمِ خانه همان خاصیتِ ‎…Text‎ را
/// می‌نویسد که تایپِ کاربر می‌نوشت، و قدمِ حذف همان ‎TrashService‎ را صدا
/// می‌زند. محاسبه‌ها همان‌جایی می‌دوند که همیشه می‌دویدند.
/// </summary>
public static class UndoHub
{
    /// <summary>یک قدم: برگرداندن و دوباره انجام دادن. ‎false‎ یعنی «دیگر نمی‌شود».</summary>
    public interface IStep
    {
        string What { get; }
        Task<bool> UndoAsync();
        Task<bool> RedoAsync();
    }

    public const int Depth = 120;

    private static readonly object Gate = new();
    private static readonly List<IStep> Undo = new();
    private static readonly List<IStep> Redo = new();
    private static bool _replaying;
    private static DeleteStep? _group;
    private static bool _wired;

    /// <summary>پس از برگرداندن یا دوباره انجام دادنِ یک حذف، صفحهٔ جلوی چشم تازه شود.</summary>
    public static Func<Task>? AfterDataChange;

    public static int UndoCount { get { lock (Gate) return Undo.Count; } }
    public static int RedoCount { get { lock (Gate) return Redo.Count; } }

    /// <summary>یک بار، سرِ بالا آمدنِ برنامه.</summary>
    public static void Wire()
    {
        if (_wired) return;
        _wired = true;
        PumpDbContext.TrashAdded += OnTrashAdded;
    }

    /// <summary>دفترِ دیگری باز شد (حسابِ دیگر) ⇒ تاریخچهٔ دفترِ قبلی معنا ندارد.</summary>
    public static void Clear() { lock (Gate) { Undo.Clear(); Redo.Clear(); _group = null; } }

    public static void Push(IStep s)
    {
        lock (Gate)
        {
            if (_replaying) return;
            Undo.Add(s);
            if (Undo.Count > Depth) Undo.RemoveAt(0);
            Redo.Clear();          // شاخهٔ تازه ⇒ «دوباره»ی کهنه بی‌معنا شد
        }
    }

    /// <summary>
    /// چند حذفِ پشتِ سرِ هم یک قدم‌اند (‎Shift+5‎ پنج ذخیرهٔ جدا می‌زند ولی
    /// کاربر یک کار کرده) — تا پایانِ این دامنه هر قلمِ سطل در همان قدم می‌نشیند.
    /// </summary>
    public static IDisposable Group(string what)
    {
        lock (Gate) _group = new DeleteStep(what);
        return new EndGroup();
    }

    private sealed class EndGroup : IDisposable
    {
        public void Dispose()
        {
            DeleteStep? g;
            lock (Gate) { g = _group; _group = null; }
            if (g is { Count: > 0 }) Push(g);
        }
    }

    private static void OnTrashAdded(IReadOnlyList<(long Id, string? Kind)> added)
    {
        var ids = added.Where(a => TrashService.CanRestore(a.Kind)).Select(a => a.Id).ToList();
        if (ids.Count == 0) return;
        lock (Gate)
        {
            if (_replaying) return;
            if (_group is not null) { _group.Add(ids); return; }
        }
        var step = new DeleteStep(added.Count == 1 ? Label(added[0].Kind) : "حذف");
        step.Add(ids);
        Push(step);
    }

    private static string Label(string? kind) => TrashService.KindLabel(kind);

    /// <summary>‎Ctrl+Z‎ — آخرین کار را برمی‌گرداند. خروجی: جملهٔ توست، یا ‎null‎ اگر چیزی نبود.</summary>
    public static async Task<string?> UndoAsync()
    {
        while (true)
        {
            IStep s;
            lock (Gate)
            {
                if (Undo.Count == 0) return null;
                s = Undo[^1];
                Undo.RemoveAt(Undo.Count - 1);
                _replaying = true;
            }
            bool ok;
            try { ok = await s.UndoAsync(); }
            catch { ok = false; }
            finally { lock (Gate) _replaying = false; }
            if (!ok) continue;                       // دیگر نیست ⇒ قدمِ قبلی
            lock (Gate) Redo.Add(s);
            await Refresh(s);
            return "↩️ برگشت: " + s.What;
        }
    }

    /// <summary>‎Ctrl+Y‎ — همان را دوباره انجام می‌دهد.</summary>
    public static async Task<string?> RedoAsync()
    {
        while (true)
        {
            IStep s;
            lock (Gate)
            {
                if (Redo.Count == 0) return null;
                s = Redo[^1];
                Redo.RemoveAt(Redo.Count - 1);
                _replaying = true;
            }
            bool ok;
            try { ok = await s.RedoAsync(); }
            catch { ok = false; }
            finally { lock (Gate) _replaying = false; }
            if (!ok) continue;
            lock (Gate) Undo.Add(s);
            await Refresh(s);
            return "↪️ دوباره: " + s.What;
        }
    }

    private static async Task Refresh(IStep s)
    {
        if (s is not DeleteStep || AfterDataChange is not { } f) return;
        try { await f(); } catch { /* تازه کردنِ صفحه رفاه است؛ داده روی دیسک نشسته */ }
    }

    /// <summary>
    /// قدمِ حذف: قلم‌های سطلی که همان کار ساخت. برگشت = بازگردانی از سطل؛
    /// دوباره = حذفِ <b>همان</b> رکوردها.
    /// </summary>
    private sealed class DeleteStep : IStep
    {
        private List<long> _trash = new();
        private readonly List<TrashService.Revived> _revived = new();
        public DeleteStep(string what) { What = what; }
        public string What { get; }
        public int Count => _trash.Count;
        public void Add(IEnumerable<long> ids) => _trash.AddRange(ids);

        public async Task<bool> UndoAsync()
        {
            var trash = AppHost.Current.Trash;
            _revived.Clear();
            // وارونه: آخرین حذف اول برمی‌گردد
            for (var i = _trash.Count - 1; i >= 0; i--)
            {
                var (err, trace) = await trash.RestoreTracedAsync(_trash[i]);
                if (err is null && trace is not null) _revived.Add(trace);
            }
            _trash = new List<long>();
            return _revived.Count > 0;
        }

        public async Task<bool> RedoAsync()
        {
            var trash = AppHost.Current.Trash;
            var again = new List<long>();
            for (var i = _revived.Count - 1; i >= 0; i--)
                if (await trash.DeleteAgainAsync(_revived[i]) is { } id) again.Add(id);
            _revived.Clear();
            _trash = again;
            return again.Count > 0;
        }
    }
}
