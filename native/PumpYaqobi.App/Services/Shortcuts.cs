using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using PumpYaqobi.App.ViewModels;

namespace PumpYaqobi.App.Services;

/// <summary>
/// ══ میانبرهای صفحه‌کلید ═══════════════════════════════════════════════════
/// مو‌به‌مو همان چیزی که کاربر سال‌ها در نسخهٔ وب داشت:
///
///   • ‎Alt+عدد‎         → رفتن به بخشِ شمارهٔ N (به ترتیبِ نوارِ بالا؛ ‎0‎ = دهم)
///   • ‎Ctrl+Shift+عدد‎ → باز کردنِ کارتِ شمارهٔ N در قرض‌داران / شرکت‌ها
///   • ‎Ctrl+عدد‎        → افزودنِ N ردیف به جدولِ جلوی کاربر
///   • ‎Shift+عدد‎       → برداشتنِ N ردیفِ آخرِ همان جدول
///
///   • ‎Ctrl+S‎         → ذخیرهٔ فوریِ همین‌جا
///   • ‎Ctrl+P‎         → پی‌دی‌افِ همان جایی که کاربر داخلش است
///   • ‎Ctrl+K‎         → ماشین‌حسابِ شناور
///   • ‎F1‎             → فهرستِ خودِ همین میانبرها
///
/// ══ چرا ‎Alt‎ برای بخش‌ها و ‎Ctrl+Shift‎ برای کارت‌ها ═══════════════
/// تا ۳.۱.۱۴۷ برعکس بود. خواستهٔ صریحِ صاحب ریپو این دو را جابه‌جا کرد:
/// «‎alt+عدد‎ برود بخش‌هایی که موجود است.» و منطقی هم هست — رفتن به بخش
/// کارِ هر روزی است و باید یک کلید بخواهد؛ باز کردنِ کارتِ شمارهٔ ۱۴ کارِ
/// گاه‌به‌گاه است. ⛔ هیچ‌کدام برداشته نشد، فقط جا عوض کردند.
///
/// ══ آن‌چه این‌جا <b>نیست</b> و عمداً نیست ══════════════════════
/// ‎Ctrl+C‎/‎V‎/‎X‎/‎A‎/‎Z‎/‎Y‎ این‌جا گرفته نمی‌شوند. آن‌ها مالِ <b>خودِ جدول</b>
/// هستند (<see cref="Controls.ExcelGrid"/>) و مالِ <b>کادرِ تایپ</b>، و هر دو
/// خودشان بلدند. گرفتنشان در این شنوندهٔ تونلی یعنی کادرِ تایپ
/// دیگر نمی‌تواند متنِ خودش را کپی کند — همان اشتباهی که یک بار با
/// ‎Shift+عدد‎ شد و نویسه‌ها را خورد.
///
/// ⚠️ و برگشت (‎Ctrl+Z‎) فقط «ویرایشِ خانهٔ جدول» را برمی‌گرداند، نه
/// ساختن و حذفِ ردیف را: آن یکی «تاریخچهٔ عکس‌فوریِ کلِ دیتابیس»
/// می‌خواهد که ساخته نشده. برای حذف، سطلِ زباله سرِ جایش است.
///
/// ══ چرا «بافر» و چرا «هنگامِ رها کردن» ═════════════════════════════════════
/// عدد می‌تواند چندرقمی باشد (‎Ctrl+1‎ سپس ‎2‎ یعنی ۱۲، نه دو بار ۱ و ۲). پس
/// تا وقتی کلیدِ تغییردهنده پایین است فقط رقم‌ها جمع می‌شوند و هیچ کاری
/// انجام نمی‌گیرد؛ کار دقیقاً لحظه‌ای می‌شود که کلید رها شود. برای «کارت»
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

    /// <summary>نامِ متدی که هر صفحهٔ جدول‌دار برای «همین حالا بنویس» دارد.</summary>
    public const string FlushMethodName = "FlushAsync";

    /// <summary>
    /// ‎Ctrl+S‎ — ردیف‌های تغییرکردهٔ همین‌جا را همین حالا می‌نویسد.
    ///
    /// ⚠️ همان الگوی بازتابیِ <see cref="PdfOf"/> و به همان دلیل: چند صفحهٔ
    /// جدول‌دار ‎FlushAsync‎ دارند و هیچ‌کدام واسطِ مشترکی ندارند. افزودنِ واسط
    /// به همه‌شان چند جای دست‌کاری بود، در حالی که کارِ لازم یک چیز است:
    /// «اگر این شیء می‌تواند بنویسد، بنویس».
    ///
    /// ⛔ ‎FlushAsync‎ی <c>private</c> (مثلِ آن یکی در پیام‌رسان که کارش
    /// فرستادنِ پیام است، نه ذخیره) پیدا نمی‌شود — ‎GetMethod‎ی پیش‌فرض فقط
    /// عمومی‌ها را می‌بیند، و این عمدی است.
    /// </summary>
    private static Task? FlushOf(object? target) =>
        target?.GetType()
              .GetMethod(FlushMethodName, Type.EmptyTypes)?
              .Invoke(target, null) as Task;

    private async Task SaveNowAsync()
    {
        var wrote = false;
        foreach (var target in new object?[] { _vm.ActiveSection?.ActivePage, _vm.ActiveSection })
        {
            if (FlushOf(target) is not { } task) continue;
            try { await task; wrote = true; } catch { }
        }
        AppHost.Current.Toasts.Show(
            wrote ? "💾 ذخیره شد" : "💾 چیزی برای ذخیره نبود",
            wrote ? ToastKind.Ok : ToastKind.Info);
    }

    /// <summary>
    /// خانهٔ بازِ جدولی که همین حالا فوکوس دارد را می‌نشاند.
    ///
    /// ⚠️ از روی **فوکوس** پیدا می‌شود، نه از ویومدل: یک صفحه می‌تواند چند
    /// جدول داشته باشد (ورق سه تا دارد) و فقط آن یکی که کاربر داخلش است
    /// ویرایشِ باز دارد.
    /// </summary>
    private static void CommitOpenCell(object? sender)
    {
        if ((sender as TopLevel)?.FocusManager?.GetFocusedElement() is not Visual v) return;
        v.FindAncestorOfType<Controls.ExcelGrid>()?.CommitNow();
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

    /// <summary>
    /// کاربر همین حالا داخلِ یک کادرِ تایپ است؟
    ///
    /// هر کلیدی که **نویسه می‌سازد** باید در این حالت دستِ خودِ کادر بماند؛
    /// میانبری که نویسه را بخورد، از چشمِ کاربر یعنی «برنامه چیزی نمی‌نویسد».
    /// </summary>
    private static bool TypingInBox(object? sender) =>
        (sender as TopLevel)?.FocusManager?.GetFocusedElement() is TextBox;

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (Locked) return;

        var mods = e.KeyModifiers;
        var ctrl = mods.HasFlag(KeyModifiers.Control);
        var shift = mods.HasFlag(KeyModifiers.Shift);
        var alt = mods.HasFlag(KeyModifiers.Alt);

        // ══ Ctrl+K → ماشین‌حسابِ شناور ═════════════════════════════════════
        // «داینامیک» یعنی هر جای برنامه که هستی همان‌جا باز شود؛ پس میانبرش
        // هم مثلِ خودش جهانی است. ‎Esc‎ می‌بنددش.
        if (e.Key == Key.K && ctrl && !alt && !shift)
        {
            _vm.Calculator.ToggleCommand.Execute(null);
            e.Handled = true;
            return;
        }
        if (e.Key == Key.Escape && _vm.Calculator.IsOpen)
        {
            _vm.Calculator.CloseCommand.Execute(null);
            e.Handled = true;
            return;
        }
        // ماشین‌حسابِ باز، صفحه‌کلید هم می‌گیرد — مثلِ ‎_calcKeyHandler‎ی سایت،
        // مگر وقتی کاربر داخلِ کادرِ تایپی است.
        if (_vm.Calculator.IsOpen && !ctrl && !alt && !TypingInBox(sender)
            && CalcKey(e.Key, shift) is { } ck)
        {
            _vm.Calculator.Key(ck);
            e.Handled = true;
            return;
        }

        // ══ Ctrl+P → پی‌دی‌افِ همین‌جا ══════════════════════════════════════
        // همان ترتیبِ اولویتِ ‎_kbPdfAction‎ی نسخهٔ وب: اول صفحهٔ بازِ درونِ بخش
        // (حسابِ شخص، صفحهٔ شرکت…) و اگر نبود، خودِ بخش. جایی که پی‌دی‌اف
        // ندارد اصلاً دست نمی‌خورد و کلید مثلِ همیشه رد می‌شود.
        if (e.Key == Key.P && ctrl && !alt && !shift)
        {
            if (TryPdf()) e.Handled = true;
            return;
        }

        // ══ Ctrl+S → ذخیرهٔ فوریِ همین‌جا ═══════════════════════════════════
        //
        // ⚠️ این برنامه از روزِ اول خودکار ذخیره می‌کند (هر خانه ۳۵۰ میلی‌ثانیه
        // بعد از آخرین تایپ می‌نشیند)، پس ‎Ctrl+S‎ چیزِ تازه‌ای نمی‌سازد —
        // فقط **همین حالا** می‌نویسد و می‌گوید که نوشت. برای کسی که سال‌ها با
        // اکسل کار کرده، نبودنِ این کلید یعنی «یعنی ذخیره نشد؟».
        //
        // ⛔ اول خانهٔ بازِ جدول بسته می‌شود، وگرنه همان چیزی که کاربر همین
        // لحظه تایپ کرده هنوز در کادر است و ذخیره نمی‌شد — یعنی ‎Ctrl+S‎ دقیقاً
        // آن چیزی را جا می‌گذاشت که کاربر برایش زده بود.
        if (e.Key == Key.S && ctrl && !alt && !shift)
        {
            CommitOpenCell(sender);
            _ = SaveNowAsync();
            e.Handled = true;
            return;
        }

        // ══ F1 → فهرستِ خودِ میانبرها ════════════════════════════════════════
        // صاحب ریپو: «برای اف ۱ تا ۱۲ نمی‌دانم چی‌ها بزنم لازم است یا نه.»
        // پس فقط همین یکی ساخته شد و بقیه آزاد ماندند: کلیدی که کاری نکند
        // بهتر از کلیدی است که کارِ حدسی بکند. ⚠️ ‎F2‎ از قبل مالِ خودِ جدول
        // است (باز کردنِ ویرایشِ خانه، مثلِ اکسل) و دست نخورد.
        if (e.Key == Key.F1 && !ctrl && !alt && !shift)
        {
            _ = Views.ShortcutsWindow.ShowAsync();
            e.Handled = true;
            return;
        }

        var d = Digit(e.Key);
        if (d is null) return;
        var digit = d.Value.ToString();

        // ۱) Ctrl+Shift+عدد → باز کردنِ کارت. فقط جمع می‌شود؛ اجرا هنگامِ
        //    رها شدنِ هر دو کلید.
        if (ctrl && shift && !alt)
        {
            e.Handled = true;
            _addBuf = _delBuf = "";
            if (_openBuf.Length < 3) _openBuf += digit;
            return;
        }

        // ۲) Alt+عدد → رفتن به بخش. Alt چیزی داخلِ کادر تایپ نمی‌کند، پس
        //    حتی با فوکوس داخلِ جست‌وجو هم بی‌تداخل است — و همین بود که
        //    آن را نامزدِ خوبی برای «هرروزی‌ترین» میانبر کرد.
        if (alt && !ctrl)
        {
            e.Handled = true;
            if (_sectionBuf.Length < 2) _sectionBuf += digit;
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
        //
        // ⛔ **ولی نه وقتی کاربر داخلِ کادرِ تایپ است.**
        // گزارشِ صاحب ریپو (۱۴۰۵/۰۶/۲۹): «این کادرِ ایمیل ایمیل نیست و هیچی
        // توش نوشته نمی‌شود؛ + @ # ﷼ ( ) ؟ ؛ : , . توی هیچ‌کدام نوشته
        // نمی‌شوند.» ریشه همین‌جا بود: ردیفِ عددها با Shift **نویسه** می‌سازد
        // — انگلیسی `@ # $ % ( )` و فارسی/دری `، ؛ ؟ ﷼ ٪ × ) (` — و این
        // شنونده کلید را `Handled` می‌کرد. روی ویندوز کلیدِ خورده‌شده دیگر
        // `WM_CHAR` نمی‌سازد، پس آن نویسه **اصلاً تایپ نمی‌شد** (و بدتر:
        // همان لحظه چند ردیف هم پاک می‌شد).
        //
        // ⚠️ Ctrl و Alt این مشکل را ندارند (نویسه نمی‌سازند) و سرِ جایشان
        // ماندند؛ AltGr هم از قبل رد می‌شد، چون Ctrl+Alt را با هم دارد.
        // سنجه: `dotnet run --project PumpYaqobi.UiTests -- inputchars`.
        if (shift && !alt && !TypingInBox(sender))
        {
            e.Handled = true;
            if (_delBuf.Length < 3) _delBuf += digit;
        }
    }

    private void OnKeyUp(object? sender, KeyEventArgs e)
    {
        if (Locked) { ClearBuffers(); return; }

        // ── Alt رها شد → بخشِ شمارهٔ واردشده باز شود ──
        if (e.Key is Key.LeftAlt or Key.RightAlt)
        {
            var sec = _sectionBuf;
            _sectionBuf = "";
            if (sec.Length > 0) GotoSection(sec);
            return;
        }

        var isCtrl = e.Key is Key.LeftCtrl or Key.RightCtrl;
        var isShift = e.Key is Key.LeftShift or Key.RightShift;
        if (!isCtrl && !isShift) return;

        // ── کارت: فقط وقتی «هر دو» کلید بالا آمدند، تا عددِ چندرقمی کامل خوانده شود ──
        if (_openBuf.Length > 0)
        {
            var mods = e.KeyModifiers;
            if (mods.HasFlag(KeyModifiers.Control) || mods.HasFlag(KeyModifiers.Shift)) return;
            var card = ParseBuf(ref _openBuf);
            ClearBuffers();
            if (card > 0) _ = OpenCardAsync(card);
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

    private static string? CalcKey(Key k, bool shift) => k switch
    {
        >= Key.D0 and <= Key.D9 when !shift => ((int)k - (int)Key.D0).ToString(),
        >= Key.NumPad0 and <= Key.NumPad9 => ((int)k - (int)Key.NumPad0).ToString(),
        Key.Add or Key.OemPlus => "+",
        Key.Subtract or Key.OemMinus => "-",
        Key.Multiply => "×",
        Key.Divide or Key.OemQuestion => "÷",
        Key.Decimal or Key.OemPeriod => ".",
        Key.Enter or Key.Return => "=",
        Key.Back => "←",
        Key.Delete => "C",
        Key.D8 when shift => "×",
        Key.D5 when shift => "%",
        _ => null,
    };
}
