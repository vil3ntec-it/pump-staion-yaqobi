using CommunityToolkit.Mvvm.ComponentModel;
using PumpYaqobi.App.Services;

namespace PumpYaqobi.App.ViewModels;

/// <summary>
/// ══ شش خانهٔ کد ═════════════════════════════════════════════════════════
///
/// بندِ ۴ی پرامپتِ ۲۲: «شش خانه، پرشِ خودکار، Paste، ارقامِ فارسی/انگلیسی،
/// ارسالِ خودکار بعد از رقمِ ششم، شمارشِ معکوسِ ۶۰ ثانیه.»
///
/// ── چرا منطقش این‌جاست و نه در code-behind ────────────────────────────
/// چون هر پنج رفتار <b>سنجیدنی</b>اند و رفتارِ سنجیده‌نشده همان است که
/// روزی بی‌صدا می‌شکند. صفحه فقط فوکوس را جابه‌جا می‌کند؛ هر تصمیمی —
/// این نویسه رقم است؟ چند رقم شد؟ به کدام خانه برو؟ — این‌جاست.
///
/// ⚠️ <b>ارقامِ فارسی و عربی هم قبول‌اند</b> (<c>LoginRules.Digits</c>).
/// کاربری که صفحه‌کلیدش فارسی است «۱۲۳۴۵۶» می‌زند، و بی این، کدش شش
/// نویسهٔ ناشناخته می‌شد و سرور «کد اشتباه است» می‌گفت — بدترین شکلِ خطا.
///
/// ⚠️ <b>Paste در هر خانه‌ای کارِ درست را می‌کند</b>: شش رقمِ چسبانده‌شده
/// از خانهٔ اول پخش می‌شوند، نه از همان‌جا که چسبیده. کاربری که کد را از
/// ایمیل کپی می‌کند به خانهٔ اول کلیک نمی‌کند.
/// </summary>
public sealed partial class CodeBoxesViewModel : ObservableObject
{
    /// <summary>شمارِ خانه‌ها.</summary>
    public const int Size = 6;

    private readonly char[] _cells = new char[Size];

    /// <summary>پس از رقمِ ششم — «ارسالِ خودکار»ِ بندِ ۴.</summary>
    public event Action? Completed;

    /// <summary>روی کدام خانه باید فوکوس باشد (صفر تا پنج).</summary>
    [ObservableProperty] private int _focus;

    [ObservableProperty] private string _b1 = "";
    [ObservableProperty] private string _b2 = "";
    [ObservableProperty] private string _b3 = "";
    [ObservableProperty] private string _b4 = "";
    [ObservableProperty] private string _b5 = "";
    [ObservableProperty] private string _b6 = "";

    /// <summary>کدِ کامل — شش رقم، یا کمتر اگر هنوز پر نشده.</summary>
    public string Code => new(_cells.Where(c => c != '\0').ToArray());

    /// <summary>همهٔ شش خانه پر است.</summary>
    public bool Full => Code.Length == Size;

    /// <summary>
    /// متنِ تازهٔ یک خانه.
    /// </summary>
    /// <param name="index">شمارهٔ خانه، صفر تا پنج.</param>
    /// <param name="text">هر چه کاربر تایپ یا چسباند.</param>
    /// <returns>خانه‌ای که باید فوکوس بگیرد.</returns>
    public int Put(int index, string? text)
    {
        if (index < 0 || index >= Size) return Focus;
        var digits = LoginRules.Digits(text);

        if (digits.Length == 0)
        {
            //  کادر خالی شد (Backspace) ⇒ همین‌جا پاک و یک خانه عقب
            _cells[index] = '\0';
            Sync();
            return Move(Math.Max(0, index - 1));
        }

        if (digits.Length > 1)
        {
            //  ⚠️ چسباندن: همیشه از خانهٔ اول، نه از جایی که چسبیده
            for (var i = 0; i < Size; i++) _cells[i] = i < digits.Length ? digits[i] : '\0';
            Sync();
            return Move(Math.Min(Size - 1, digits.Length));
        }

        _cells[index] = digits[0];
        Sync();
        return Move(Math.Min(Size - 1, index + 1));
    }

    /// <summary>همه را پاک می‌کند — «کد اشتباه بود، دوباره بزن».</summary>
    public void Clear()
    {
        Array.Clear(_cells);
        Sync();
        Focus = 0;
    }

    /// <summary>کد را دستی می‌نشاند — برای سنجه‌ها.</summary>
    public void Fill(string code)
    {
        var digits = LoginRules.Digits(code);
        for (var i = 0; i < Size; i++) _cells[i] = i < digits.Length ? digits[i] : '\0';
        Sync();
    }

    private int Move(int to)
    {
        Focus = Math.Clamp(to, 0, Size - 1);
        return Focus;
    }

    private void Sync()
    {
        B1 = Cell(0); B2 = Cell(1); B3 = Cell(2);
        B4 = Cell(3); B5 = Cell(4); B6 = Cell(5);
        OnPropertyChanged(nameof(Code));
        OnPropertyChanged(nameof(Full));
        //  ⚠️ **ارسالِ خودکار فقط وقتی هر شش خانه پر باشد** — نه وقتی شش
        //  رقم در فهرست هست. خانهٔ وسطیِ خالی یعنی کاربر هنوز کارش تمام
        //  نشده، و ارسالِ زودهنگام یکی از پنج تلاشِ سرور را می‌سوزاند.
        if (_cells.All(c => c != '\0')) Completed?.Invoke();
    }

    private string Cell(int i) => _cells[i] == '\0' ? "" : _cells[i].ToString();
}
