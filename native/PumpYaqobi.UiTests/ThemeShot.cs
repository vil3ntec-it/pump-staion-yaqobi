using SkiaSharp;

namespace PumpYaqobi.UiTests;

/// <summary>
/// چهار عکسِ یک تم کنارِ هم، در یک تصویر — تا صاحب ریپو تم‌ها را با هم مقایسه
/// کند و یکی را برگزیند. هر عکس نصف می‌شود و در یک شبکهٔ ۲×۲ می‌نشیند.
/// </summary>
internal static class ThemeShot
{
    public static void Compose(IReadOnlyList<string> files, string outPath)
    {
        var bitmaps = files.Where(File.Exists).Select(SKBitmap.Decode).Where(b => b is not null).ToList();
        if (bitmaps.Count == 0) return;
        var cw = bitmaps[0].Width / 2;
        var ch = bitmaps[0].Height / 2;
        var cols = 2;
        var rows = (bitmaps.Count + cols - 1) / cols;
        var gap = 8;
        using var surface = SKSurface.Create(new SKImageInfo(cols * cw + (cols + 1) * gap, rows * ch + (rows + 1) * gap));
        var canvas = surface.Canvas;
        canvas.Clear(new SKColor(0x40, 0x40, 0x40));
        using var paint = new SKPaint { FilterQuality = SKFilterQuality.High, IsAntialias = true };
        for (var i = 0; i < bitmaps.Count; i++)
        {
            var x = gap + (i % cols) * (cw + gap);
            var y = gap + (i / cols) * (ch + gap);
            canvas.DrawBitmap(bitmaps[i], new SKRect(x, y, x + cw, y + ch), paint);
            bitmaps[i].Dispose();
        }
        using var img = surface.Snapshot();
        using var data = img.Encode(SKEncodedImageFormat.Png, 90);
        using var fs = File.OpenWrite(outPath);
        data.SaveTo(fs);
        Console.WriteLine("  ✔ " + Path.GetFileName(outPath));
    }
}
