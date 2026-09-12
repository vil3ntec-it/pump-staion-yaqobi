using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace PumpYaqobi.App.Controls;

/// <summary>
/// ══ نشانگرِ «خانهٔ اکسل» — همتای ‎cursor:cell‎ی سایت ═════════════════════════
///
/// گزارشِ صاحب ریپو: «علامتِ مثبت هم شبیهِ اکسل نیست، و روی جدول‌ها که بیایم
/// آن موس بشود شبیهِ اکسل.»
///
/// سایت روی خانه‌های جدول ‎cursor:cell‎ می‌گذارد (خطِ ۱۰۴۸ی ‎index.html‎):
///
///     .tbl td,.tbl th,.xls-tbl td,.xls-tbl th,.ro-tbl td,.ro-tbl th{cursor:cell}
///
/// و ‎cell‎ در مرورگر همان **به‌علاوهٔ کلفتِ سفید با دورِ سیاه** است — همانی که
/// اکسل روی خانه‌ها نشان می‌دهد.
///
/// پیش از این این‌جا ‎StandardCursorType.Cross‎ گذاشته شده بود، به این گمان که
/// «نزدیک‌ترین چیز» است. ولی ‎Cross‎ یک ضربدرِ **نازکِ نخی** است (نشانگرِ دقتِ
/// نقاشی)، نه به‌علاوهٔ کلفتِ اکسل؛ از نزدیک هیچ شباهتی ندارند. آوالونیا
/// نشانگرِ آماده‌ای برای ‎cell‎ ندارد، پس همان شکل این‌جا کشیده و به نشانگرِ
/// سفارشی تبدیل می‌شود.
///
/// ⚠️ کشیدنِ نشانگر به موتورِ رسم نیاز دارد، پس تنبل است و در بدترین حالت به
/// همان ‎Cross‎ی قبلی برمی‌گردد — هیچ‌وقت برنامه را نمی‌شکند.
/// </summary>
public static class ExcelCursor
{
    private static Cursor? _cell;

    /// <summary>نشانگرِ خانه — یک‌بار ساخته می‌شود و بعد همان برگردانده می‌شود.</summary>
    public static Cursor Cell => _cell ??= Build();

    private static Cursor Build()
    {
        try { return new Cursor(Draw(), new PixelPoint(Size / 2, Size / 2)); }
        catch { return new Cursor(StandardCursorType.Cross); }   // بدترین حالت: همان قبلی
    }

    // اندازه‌ها از شکلِ خودِ نشانگرِ اکسل: به‌علاوه‌ای که بازوهایش کلفت‌اند و
    // وسطش تو‌خالی نیست. ‎Arm‎ کلفتیِ بازو و ‎Pad‎ حاشیه‌ای است که دورِ سیاه
    // بیرونِ بوم نیفتد.
    private const int Size = 24;
    private const double Arm = 8;
    private const double Pad = 2;

    private static RenderTargetBitmap Draw()
    {
        var bmp = new RenderTargetBitmap(new PixelSize(Size, Size), new Vector(96, 96));
        using (var ctx = bmp.CreateDrawingContext())
        {
            var g = new StreamGeometry();
            using (var p = g.Open())
            {
                double c = Size / 2.0, h = Arm / 2.0, lo = Pad, hi = Size - Pad;
                // دوازده گوشهٔ یک «به‌علاوه»، ساعتگرد
                p.BeginFigure(new Point(c - h, lo), true);
                p.LineTo(new Point(c + h, lo));
                p.LineTo(new Point(c + h, c - h));
                p.LineTo(new Point(hi, c - h));
                p.LineTo(new Point(hi, c + h));
                p.LineTo(new Point(c + h, c + h));
                p.LineTo(new Point(c + h, hi));
                p.LineTo(new Point(c - h, hi));
                p.LineTo(new Point(c - h, c + h));
                p.LineTo(new Point(lo, c + h));
                p.LineTo(new Point(lo, c - h));
                p.LineTo(new Point(c - h, c - h));
                p.EndFigure(true);
            }

            // سفیدِ تو‌پر با دورِ سیاه — روی هر پس‌زمینه‌ای، روشن یا تیره، دیده می‌شود
            ctx.DrawGeometry(Brushes.White, new Pen(Brushes.Black, 1.7), g);
        }
        return bmp;
    }
}
