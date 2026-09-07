using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using PumpYaqobi.App.ViewModels;

namespace PumpYaqobi.App.Services;

/// <summary>
/// ══ میانبرهای صفحه‌کلید ═══════════════════════════════════════════════════
/// مو‌به‌مو همان چیزی که کاربر سال‌ها در نسخهٔ وب داشت:
///
///   • ‎Ctrl+Shift+عدد‎ → رفتن به بخشِ شمارهٔ N (به ترتیبِ نوارِ بالا؛ ‎0‎ = دهم)
///   • ‎Alt+عدد‎         → باز کردنِ کارتِ شمارهٔ N در قرض‌داران / شرکت‌ها
///   • ‎Ctrl+عدد‎        → افزودنِ N ردیف به جدولِ جلوی کاربر
///   • ‎Shift+عدد‎       → برداشتنِ N ردیفِ آخرِ همان جدول
///
///   • ‎Ctrl+P‎         → پی‌دی‌افِ همان جایی که کاربر داخلش است
///
/// ‎Ctrl+Z‎/‎Ctrl+X‎ (برگشت و جلو رفتنِ سراسری) هنوز نیست: آن به یک «تاریخچهٔ
/// عکس‌فوریِ کلِ دیتابیس» نیاز دارد که در نیتیو ساخته نشده. سطلِ زباله
/// «برگرداندنِ حذف‌شده» را می‌دهد، ولی برگرداندنِ یک ویرایش را نه.
///
/// ══ چرا «بافر» و چرا «هنگامِ رها کردن» ═════════════════════════════════════
/// عدد می‌تواند چندرقمی باشد (‎Ctrl+1‎ سپس ‎2‎ یعنی ۱۲، نه دو بار ۱ و ۲). پس
/// تا وقتی کلیدِ تغییردهنده پایین است فقط رقم‌ها جمع می‌شوند و هیچ کاری
/// انجام نمی‌گیرد؛ کار دقیقاً لحظه‌ای می‌شود که کلید رها شود. برای «بخش»
/// باید <b>هر دو</b> کلید (Ctrl و Shift) رها شوند، وگرنه با رها شدنِ زودترِ
/// یکی، عددِ ناقص می‌پرید.
///
/// اگر پنجره وسطِ کار فوکس را از دست بدهد (Alt+Tab و…) بافرها پاک می‌شوند تا
/// عملِ ناخواسته انجام نشود.
/// </summary>
public sealed class ShortcutService
{
    private readonly Window _window;
    private readonly MainViewModel _vm;

    private string _sectionBuf = "";
    private string _addBuf = "";
    private string _delBuf = "";
    private string _openBuf = "";

    public ShortcutService(Window window, MainViewModel vm)
    {
        _window = window;
        _vm = vm;

        // Tunnel: پیش از آن‌که کادرِ متنیِ زیرِ فوکوس کلید را بخورد. بدونِ آن،
        // ‎Ctrl+۵‎ داخلِ یک کادرِ عدد، «۵» تایپ می‌کرد و میانبر هرگز نمی‌رسید.
        _window.AddHandler(InputElement.KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
        _window.AddHandler(InputElement.KeyUpEvent, OnKeyUp, RoutingStrategies.Tunnel);
        _window.LostFocus += (_, _) => ClearBuffers();
        _window.Deactivated += (_, _) => ClearBuffers();
    }

    private void ClearBuffers() { _sectionBuf = _addBuf = _delBuf = _openBuf = ""; }

    /// <summary>نامِ فرمانی که هر بخشِ پی‌دی‌اف‌دار می‌سازد (‎[RelayCommand] PdfAsync‎).</summary>
    public const string PdfCommandName = "PdfCommand";

    /// <summary>
    /// ‎_kbPdfAction()‎ — فرمانِ پی‌دی‌افِ جایی که کاربر داخلش است.
    ///
    /// ⚠️ چرا بازتاب (reflection) و نه یک واسط: چهارده بخش این فرمان را دارند و
    /// سه‌تایشان فهرستِ پایه‌شان در خطِ بعدی نوشته شده. افزودنِ واسط به هر
    /// چهارده کلاس چهارده جای دست‌کاری بود، در حالی که کارِ لازم یک چیز است:
    /// «اگر این شیء فرمانِ پی‌دی‌اف دارد، اجرایش کن». ‎PdfCommandParityTests‎
    /// جلوی خاموش شکستنش را می‌گیرد — اگر روزی نامِ ‎PdfAsync‎ عوض شود، قرمز
    /// می‌شود.
    /// </summary>
    private static System.Windows.Input.ICommand? PdfOf(object? target) =>
        target?.GetType()
              .GetProperty(PdfCommandName)?
              .GetValue(target) as System.Windows.Input.ICommand;

    private bool TryPdf()
    {
        // اول صفحهٔ بازِ درونِ بخش، بعد خودِ بخش — همان اولویتِ نسخهٔ وب.
        // «بخش» یعنی آن‌چه واقعاً جلوی چشم است: اگر زیربخشی باز باشد
        // (قرض‌های کهنه، تخلیهٔ تانکر، گزارش ماهانه…) ‎Ctrl+P‎ باید ورقِ همان
        // را بدهد، نه ورقِ بخشِ پشتِ آن.
        foreach (var target in new object?[] { _vm.ActiveSection?.ActivePage, _vm.ActiveSection })
        {
            var cmd = PdfOf(target);
            if (cmd is null || !cmd.CanExecute(null)) continue;
            cmd.Execute(null);
            return true;
        }
        return false;
    }

    /// <summary>روی صفحهٔ قفل هیچ میانبری کار نمی‌کند.</summary>
    private bool Locked => _vm.IsLocked;

    /// <summary>
    /// رقمِ این کلید. با Shift مقدارِ نویسه عوض می‌شود (‎!‎ ‎@‎ …) پس مبنا
    /// خودِ کلید است، نه نویسه — همان دلیلی که در نسخهٔ وب ‎e.code‎ مبنا بود.
    /// ردیفِ بالای صفحه‌کلید و ماشین‌حساب هر دو پذیرفته‌اند.
    /// </summary>
    private static int? Digit(Key k) => k switch
    {
        >= Key.D0 and <= Key.D9 => k - Key.D0,
        >= Key.NumPad0 and <= Key.NumPad9 => k - Key.NumPad0,
        _ => null,
    };

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (Locked) return;

        var mods = e.KeyModifiers;
        var ctrl = mods.HasFlag(KeyModifiers.Control);
        var shift = mods.HasFlag(KeyModifiers.Shift);
        var alt = mods.HasFlag(KeyModifiers.Alt);

        // ══ Ctrl+P → پی‌دی‌افِ همین‌جا ══════════════════════════════════════
        // همان ترتیبِ اولویتِ ‎_kbPdfAction‎ی نسخهٔ وب: اول صفحهٔ بازِ درونِ بخش
        // (حسابِ شخص، صفحهٔ شرکت…) و اگر نبود، خودِ بخش. جایی که پی‌دی‌اف
        // ندارد اصلاً دست نمی‌خورد و کلید مثلِ همیشه رد می‌شود.
        if (e.Key == Key.P && ctrl && !alt && !shift)
        {
            if (TryPdf()) e.Handled = true;
            return;
        }

        var d = Digit(e.Key);
        if (d is null) return;
        var digit = d.Value.ToString();

        // ۱) Ctrl+Shift+عدد → بخش. فقط جمع می‌شود؛ اجرا هنگامِ رها شدنِ هر دو.
        if (ctrl && shift && !alt)
        {
            e.Handled = true;
            _addBuf = _delBuf = "";
            if (_sectionBuf.Length < 2) _sectionBuf += digit;
            return;
        }

        // ۲) Alt+عدد → باز کردنِ کارت. Alt چیزی داخلِ کادر تایپ نمی‌کند، پس
        //    حتی با فوکوس داخلِ جست‌وجو هم بی‌تداخل است.
        if (alt && !ctrl)
        {
            e.Handled = true;
            if (_openBuf.Length < 3) _openBuf += digit;
            return;
        }

        // ۳) Ctrl+عدد → افزودنِ ردیف
        if (ctrl && !alt)
        {
            e.Handled = true;
            if (_addBuf.Length < 3) _addBuf += digit;
            return;
        }

        // ۴) Shift+عدد → حذفِ ردیف
        if (shift && !alt)
        {
            e.Handled = true;
            if (_delBuf.Length < 3) _delBuf += digit;
        }
    }

    private void OnKeyUp(object? sender, KeyEventArgs e)
    {
        if (Locked) { ClearBuffers(); return; }

        // ── Alt رها شد → حسابِ شمارهٔ واردشده باز شود ──
        if (e.Key is Key.LeftAlt or Key.RightAlt)
        {
            var n = ParseBuf(ref _openBuf);
            if (n > 0) _ = OpenCardAsync(n);
            return;
        }

        var isCtrl = e.Key is Key.LeftCtrl or Key.RightCtrl;
        var isShift = e.Key is Key.LeftShift or Key.RightShift;
        if (!isCtrl && !isShift) return;

        // ── بخش: فقط وقتی «هر دو» کلید بالا آمدند، تا عددِ چندرقمی کامل خوانده شود ──
        if (_sectionBuf.Length > 0)
        {
            var mods = e.KeyModifiers;
            if (mods.HasFlag(KeyModifiers.Control) || mods.HasFlag(KeyModifiers.Shift)) return;
            var s = _sectionBuf;
            ClearBuffers();
            GotoSection(s);
            return;
        }

        if (isCtrl)
        {
            var n = ParseBuf(ref _addBuf);
            if (n > 0) _ = AddRowsAsync(n);
        }
        else
        {
            var n = ParseBuf(ref _delBuf);
            if (n > 0) _ = DeleteRowsAsync(n);
        }
    }

    private static int ParseBuf(ref string buf)
    {
        var s = buf;
        buf = "";
        return int.TryParse(s, out var n) ? n : 0;
    }

    // ══════════════════════════ خودِ کارها ══════════════════════════

    /// <summary>‎0‎ یعنی دهمین بخش — همان قاعدهٔ نسخهٔ وب.</summary>
    private void GotoSection(string buf)
    {
        var n = buf == "0" ? 10 : (int.TryParse(buf, out var v) ? v : 0);
        if (n < 1 || n > _vm.Sections.Count) return;
        _ = _vm.GoAsync(_vm.Sections[n - 1]);
    }

    private async Task OpenCardAsync(int n)
    {
        // همان قاعده: زیربخشِ باز مقدم است بر بخشِ پشتِ آن.
        if (_vm.ActiveSection is ICardGridHost grid) await grid.OpenByNumberAsync(n);
    }

    private async Task AddRowsAsync(int n)
    {
        if (_vm.RowHost is not { } host) return;
        await host.AddRowsAsync(n);
        AppHost.Current.Toasts.Show($"✅ {n} ردیف افزوده شد", ToastKind.Ok);
    }

    private async Task DeleteRowsAsync(int n)
    {
        if (_vm.RowHost is not { } host) return;
        var before = host.RowCount;
        await host.DeleteRowsAsync(n);
        // ردیفِ کافی نبود → خودِ میزبان هیچ نکرده؛ توستِ دروغ هم نباید بدهیم
        if (host.RowCount == before) return;
        AppHost.Current.Toasts.Show($"🗑️ {n} ردیف حذف شد", ToastKind.Error);
    }
}
