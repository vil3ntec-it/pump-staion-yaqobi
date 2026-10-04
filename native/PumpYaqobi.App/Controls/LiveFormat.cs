using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using PumpYaqobi.Application.Services;

namespace PumpYaqobi.App.Controls;

/// <summary>
/// ══ قالبِ زندهٔ کادر — عدد با کاما، تاریخ با «/» (۱۴۰۵/۰۷/۱۹) ═══════════════
///
/// خواستهٔ صاحب ریپو: «5000 ⇐ 5,000 همان لحظهٔ تایپ» و «اسلشِ تاریخ خودکار در جای
/// درست». قاعده‌ها فقط در ‎LiveInput‎اند (خالص و آزمون‌دار)؛ این‌جا فقط به کادر
/// وصل می‌شود: روی کادرِ فرم با ‎c:LiveFormat.Kind="number|date"‎ (یا کلاسِ ‎num‎ /
/// ‎date‎ در ‎Controls.axaml‎)، و در خانهٔ جدول خودِ ‎ExcelGrid‎ می‌گذاردش.
///
/// ⛔ فقط <b>نوشتهٔ کادر</b>: ‎Shamsi.Num‎ کاما را از قبل نادیده می‌گیرد، پس هیچ
/// عددی عوض نمی‌شود. ⛔ تکملهٔ خودکارِ باز (انتخابِ کم‌رنگ) دست نمی‌خورد.
/// ⚠️ ‎TextBox‎ پس از عوض کردنِ متن مکان‌نما را خودش جابه‌جا می‌کند، پس جای
/// مکان‌نما یک تیک بعد هم دوباره گذاشته می‌شود — وگرنه رقمِ بعدی پیش از کاما
/// می‌نشست.
/// </summary>
public static class LiveFormat
{
    public static readonly AttachedProperty<string?> KindProperty =
        AvaloniaProperty.RegisterAttached<TextBox, string?>("Kind", typeof(LiveFormat));

    public static string? GetKind(TextBox t) => t.GetValue(KindProperty);
    public static void SetKind(TextBox t, string? v) => t.SetValue(KindProperty, v);

    private sealed class State { public string Last = ""; public bool Busy; public bool Hooked; public bool User; }
    private static readonly ConditionalWeakTable<TextBox, State> States = new();

    static LiveFormat()
    {
        KindProperty.Changed.AddClassHandler<TextBox>((box, _) => Hook(box));
    }

    /// <summary>برای جدول: همان کادرِ ویرایشِ خانه.</summary>
    public static void Attach(TextBox box, string? kind)
    {
        SetKind(box, kind);
        Hook(box);
        if (States.TryGetValue(box, out var st)) st.Last = box.Text ?? "";
    }

    private static void Hook(TextBox box)
    {
        var st = States.GetValue(box, _ => new State());
        st.Last = box.Text ?? "";
        if (st.Hooked) return;
        st.Hooked = true;
        box.TextChanged += (_, _) => OnText(box, st);
        //  ⛔ فقط نوشتنِ <b>خودِ کاربر</b> قالب می‌خورد (تایپ، پاک‌کن، چسباندن) — نه متنی که
        //  ویومدل یا اتصال در کادر می‌گذارد. وگرنه ویومدلی که «6000» را جای «6,000» می‌نشاند
        //  «پاک کردنِ کاما» دیده می‌شد و رقمِ پیشش می‌رفت («000» — ‎buyform‎ گرفتش)، و متنِ
        //  ویومدل بی‌دلیل با کاما به خودش برمی‌گشت.
        //  ⚠️ نشان تا پایانِ همین کلید معتبر است: کلیدی که متن را عوض نکرد (پاک‌کن در کادرِ
        //  خالی) نباید متنِ ویومدلِ بعدی را «نوشتهٔ کاربر» کند.
        void Mark() { st.User = true; Dispatcher.UIThread.Post(() => st.User = false, DispatcherPriority.Background); }
        box.AddHandler(InputElement.TextInputEvent, (_, _) => Mark(), RoutingStrategies.Tunnel, true);
        box.AddHandler(InputElement.KeyDownEvent, (_, e) =>
        {
            var ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);
            if (e.Key is Key.Back or Key.Delete || ctrl && e.Key is Key.V or Key.X
                || e.KeyModifiers.HasFlag(KeyModifiers.Shift) && e.Key == Key.Insert)
                Mark();
        }, RoutingStrategies.Tunnel, true);
    }

    /// <summary>
    /// متنی که همین حالا گذاشته می‌شود نوشتهٔ کاربر است (حرف‌هایی که ‎ExcelGrid‎ پیش از
    /// آماده شدنِ کادر نگه داشته و خودش در کادر می‌نشاند).
    /// </summary>
    public static void MarkUser(TextBox box)
    {
        if (States.TryGetValue(box, out var st)) st.User = true;
    }

    private static void OnText(TextBox box, State st)
    {
        if (st.Busy) return;
        var kind = GetKind(box);
        var text = box.Text ?? "";
        var prev = st.Last;
        st.Last = text;
        var user = st.User;
        st.User = false;
        //  ⚠️ تکملهٔ کم‌رنگ را خودِ ‎Suggest‎ می‌گذارد (نه کاربر) — ولی «پیشین» باید
        //  همان تکهٔ تایپ‌شده باشد، وگرنه حرفِ بعدی «کوتاه‌تر شدن» دیده می‌شد و
        //  خانهٔ تاریخِ جدول «/» نمی‌گرفت (‎liveformat‎ گرفتش).
        if (!user && box.SelectionStart != box.SelectionEnd
            && Math.Max(box.SelectionStart, box.SelectionEnd) == text.Length)
            st.Last = text[..Math.Min(box.SelectionStart, box.SelectionEnd)];
        if (!user || string.IsNullOrEmpty(kind) || text.Length == 0) return;
        //  تکملهٔ خودکارِ باز (‎Suggest‎) — انتخابش دست نخورد، و «پیشین» فقط همان
        //  تکه‌ای است که کاربر زده (وگرنه تاریخِ پیشنهادی «بلندتر» دیده می‌شد و
        //  حرفِ بعدی هیچ «/»ی نمی‌گرفت)
        var caret = box.CaretIndex;
        if (box.SelectionEnd != box.SelectionStart)
        {
            var a = Math.Min(box.SelectionStart, box.SelectionEnd);
            var z = Math.Max(box.SelectionStart, box.SelectionEnd);
            if (z == text.Length) { st.Last = text[..a]; return; }
            //  ⚠️ انتخابِ <b>کهنه</b>: حرفِ تازه جای تکملهٔ کم‌رنگ نشست و ‎TextBox‎ هنوز
            //  انتخابِ قبلی را دارد (درازتر از متن). پس این تایپِ عادی است — مکان‌نما
            //  سرِ همان انتخاب بود. بی این، حرفِ پنجمِ تاریخ در جدول «/» نمی‌گرفت.
            if (z < text.Length) return;
            caret = a;
        }

        //  ⚠️ ‎TextBox‎ پیش از جابه‌جا کردنِ مکان‌نما این‌جا را صدا می‌زند؛ وقتی متن
        //  یک نویسه بلندتر شد و مکان‌نما هنوز جای قبلی است، جای واقعی یکی جلوتر است.
        if (text.Length == prev.Length + 1 && caret <= text.Length - 1 && FirstDiff(prev, text) == caret)
            caret++;

        var (outText, outCaret) = kind == "date"
            ? LiveInput.Date(text, caret, prev)
            : kind == "number" ? LiveInput.Number(text, caret, prev) : (text, caret);
        if (outText == text) return;

        st.Busy = true;
        try
        {
            box.Text = outText;
            box.CaretIndex = outCaret;
            st.Last = outText;
        }
        finally { st.Busy = false; }
        //  ⚠️ ‎TextBox‎ پس از این بازگشت مکان‌نمای خودش را می‌گذارد — یک تیک بعد درست
        Dispatcher.UIThread.Post(() =>
        {
            if ((box.Text ?? "") == outText && box.SelectionStart == box.SelectionEnd && box.CaretIndex != outCaret)
                box.CaretIndex = outCaret;
        }, DispatcherPriority.Input);
    }

    private static int FirstDiff(string a, string b)
    {
        var n = Math.Min(a.Length, b.Length);
        for (var i = 0; i < n; i++) if (a[i] != b[i]) return i;
        return n;
    }
}
