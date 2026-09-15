using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PumpYaqobi.Application.Localization;

namespace PumpYaqobi.App.ViewModels;

/// <summary>
/// ══ ماشین‌حسابِ شناور ═══════════════════════════════════════════════════════
///
/// خواستهٔ صریحِ صاحب ریپو: «ماشین‌حساب نیست تو اپ … ماشین‌حساب داینامیک».
/// یعنی جزوِ بخش‌ها نیست؛ هر جای برنامه که باشی با یک دکمه (یا ‎Ctrl+K‎) باز
/// می‌شود، روی همان صفحه می‌نشیند و با ‎Esc‎ بسته می‌شود.
///
/// ⚠️ سه تصمیم که عمدی‌اند:
///
///   ۱) حساب با ‎decimal‎ است نه ‎double‎. در یک برنامهٔ حسابداری ۰٫۱+۰٫۲ باید
///      دقیقاً ۰٫۳ باشد؛ با اعشاریِ شناور نمی‌شود.
///
///   ۲) زنجیرهٔ عمل مثلِ ماشین‌حسابِ واقعی است: ‎۲ + ۳ × ۴‎ یعنی ‎(۲+۳)×۴‎ —
///      هر عملگرِ تازه، عملِ قبلی را می‌بندد. این همان رفتاری است که کاربرِ
///      ماشین‌حسابِ جیبی انتظار دارد، نه ترتیبِ ریاضی.
///
///   ۳) تقسیم بر صفر پیام می‌دهد و حالت را پاک می‌کند؛ استثنا پرت نمی‌کند.
/// </summary>
public sealed partial class CalculatorViewModel : ObservableObject
{
    private decimal _acc;            // عددِ انباشته
    private string? _pending;        // عملگرِ منتظر
    private bool _fresh = true;      // رقمِ بعدی، عددِ تازه شروع کند

    [ObservableProperty] private bool _isOpen;

    // ══ اندازه — مثلِ سایت ════════════════════════════════════════════════
    // ‎#calcPanel‎ ۲۸۶×۴۳۰ است، ‎.large‎ ۴۰۰×۵۸۰، چهار گوشه‌اش کشیدنی، و قلمِ
    // کلیدها با بلندی بزرگ می‌شود (‎cqh‎). گزارشِ صاحب ریپو: «نمی‌شه با همون
    // اندازه که می‌خوایم بزرگ یا کوچیک کنیم و اندازه‌اش ثبت نمی‌شه.» پس اندازه
    // آزاد است، در تنظیمات می‌ماند و قلم دنبالش می‌آید.
    public const double MinW = 230, MinH = 300, MaxW = 900, MaxH = 1100;
    [ObservableProperty] private double _width = 286;
    [ObservableProperty] private double _height = 430;
    [ObservableProperty] private bool _isLarge;

    /// <summary>قلمِ کلیدها و صفحه، به نسبتِ بلندی — همان ‎clamp(…, 5.2cqh, …)‎ی سایت.</summary>
    public double KeyFont => Math.Clamp(Height * 0.045, 13, 30);
    public double DisplayFont => Math.Clamp(Height * 0.075, 20, 46);
    public double KeyHeight => Math.Clamp((Height - 130) / 6.0, 34, 120);

    partial void OnWidthChanged(double v) => SizeChanged?.Invoke();
    partial void OnHeightChanged(double v)
    {
        OnPropertyChanged(nameof(KeyFont)); OnPropertyChanged(nameof(DisplayFont)); OnPropertyChanged(nameof(KeyHeight));
        SizeChanged?.Invoke();
    }

    /// <summary>هر تغییرِ اندازه — ویومدلِ اصلی همین را در تنظیمات می‌نویسد.</summary>
    public event Action? SizeChanged;

    /// <summary>کشیدنِ گوشه: ‎dw‎/‎dh‎ به پیکسل.</summary>
    public void Resize(double dw, double dh)
    {
        Width = Math.Clamp(Width + dw, MinW, MaxW);
        Height = Math.Clamp(Height + dh, MinH, MaxH);
    }

    /// <summary>بزرگ ⇄ کوچک — همان ‎toggleCalcSize‎ی سایت.</summary>
    [RelayCommand]
    private void ToggleSize()
    {
        IsLarge = !IsLarge;
        Width = IsLarge ? 400 : 286;
        Height = IsLarge ? 580 : 430;
    }

    /// <summary>چیزی که روی صفحه است — همیشه رشته، تا «۰٫» هم بشود نوشت.</summary>
    [ObservableProperty] private string _display = "0";

    /// <summary>نوارِ بالا: «۱۲۰ +» تا معلوم باشد وسطِ چه عملی هستیم.</summary>
    [ObservableProperty] private string _trail = "";

    [RelayCommand] private void Toggle() => IsOpen = !IsOpen;
    [RelayCommand] private void Close() => IsOpen = false;

    /// <summary>رقم یا نقطه.</summary>
    [RelayCommand]
    public void Key(string? k)
    {
        if (string.IsNullOrEmpty(k)) return;
        switch (k)
        {
            case "C": Clear(); return;
            case "←": Back(); return;
            case "±": Negate(); return;
            case "%": Percent(); return;
            case "=": Equals(); return;
            case "+" or "-" or "×" or "÷": Operator(k); return;
        }

        if (k == ".")
        {
            if (_fresh) { Display = "0."; _fresh = false; return; }
            if (!Display.Contains('.')) Display += ".";
            return;
        }

        if (k.Length != 1 || !char.IsAsciiDigit(k[0])) return;
        if (_fresh || Display == "0") { Display = k; _fresh = false; }
        else if (Display.Length < 18) Display += k;
    }

    private decimal Current => decimal.TryParse(Display, out var v) ? v : 0m;

    private void Clear()
    {
        _acc = 0m; _pending = null; _fresh = true;
        Display = "0"; Trail = "";
    }

    private void Back()
    {
        if (_fresh) return;
        Display = Display.Length <= 1 ? "0" : Display[..^1];
        if (Display is "" or "-") Display = "0";
    }

    private void Negate()
    {
        if (Display.StartsWith('-')) Display = Display[1..];
        else if (Display != "0") Display = "-" + Display;
    }

    /// <summary>درصد: وسطِ یک عمل، درصدی از عددِ انباشته؛ وگرنه تقسیم بر صد.</summary>
    private void Percent()
    {
        Display = Fmt(_pending is null ? Current / 100m : _acc * Current / 100m);
        _fresh = true;
    }

    private void Operator(string op)
    {
        if (_pending is not null && !_fresh) Fold();
        else _acc = Current;

        _pending = op;
        _fresh = true;
        Trail = Fmt(_acc) + " " + op;
    }

    private void Equals()
    {
        if (_pending is null) { Trail = ""; return; }
        Fold();
        _pending = null;
        _fresh = true;
        Trail = "";
    }

    /// <summary>عملِ منتظر را ببند. تقسیم بر صفر فقط پیام است، نه استثنا.</summary>
    private void Fold()
    {
        var b = Current;
        switch (_pending)
        {
            case "+": _acc += b; break;
            case "-": _acc -= b; break;
            case "×": _acc *= b; break;
            case "÷":
                if (b == 0m) { Clear(); Display = "تقسیم بر صفر"; return; }
                _acc /= b;
                break;
        }
        Display = Fmt(_acc);
    }

    /// <summary>
    /// نوشتنِ عدد — جداکنندهٔ هزارگان دارد، ولی همان چیزی می‌ماند که
    /// ‎decimal.TryParse‎ بتواند دوباره بخواند.
    /// </summary>
    private static string Fmt(decimal v)
    {
        var s = Shamsi.Money(Math.Round(v, 6));
        return s.Length == 0 ? "0" : s;
    }

    /// <summary>نتیجه، آمادهٔ چسباندن در خانهٔ جدول — بی جداکننده.</summary>
    public string Plain => Current.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
