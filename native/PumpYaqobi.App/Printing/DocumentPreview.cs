using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace PumpYaqobi.App.Printing;

/// <summary>
/// ══ پیش‌نمایشِ سند ══════════════════════════════════════════════════════════
/// همان صفحهٔ چاپِ نسخهٔ وب: نوارِ سبزِ بالا با «بستن»، «تنظیمات» و «چاپ»،
/// و ورق‌های سفیدِ سند زیرِ آن با شمارندهٔ «۱ از ۸».
///
/// ⚠️ برخلافِ نسخهٔ وب، هیچ مرورگری این‌جا نیست: صفحه‌ها همان تصویرِ واقعیِ
/// PDF هستند که موتورِ سند می‌سازد. آنچه می‌بینید دقیقاً همان چیزی است که
/// چاپ می‌شود — نه یک نمایشِ تقریبی.
/// </summary>
public sealed partial class DocumentPreviewViewModel : ObservableObject
{
    private readonly IDocument _doc;
    private readonly List<Bitmap> _pages = new();

    public DocumentPreviewViewModel(IDocument doc, string title)
    {
        _doc = doc;
        Title = title;
        Render();
    }

    public string Title { get; }

    public IReadOnlyList<Bitmap> Pages => _pages;

    [ObservableProperty] private int _pageIndex;
    [ObservableProperty] private Bitmap? _currentPage;
    [ObservableProperty] private string _pageLabel = "";
    [ObservableProperty] private string _status = "";

    public int PageCount => _pages.Count;

    private void Render()
    {
        foreach (var bytes in _doc.GenerateImages(new ImageGenerationSettings
        {
            ImageFormat = ImageFormat.Png,
            RasterDpi = 144,          // خوانا روی صفحه، سبک برای حافظه
        }))
        {
            using var ms = new MemoryStream(bytes);
            _pages.Add(new Bitmap(ms));
        }
        PageIndex = 0;
        Show();
    }

    private void Show()
    {
        CurrentPage = _pages.Count == 0 ? null : _pages[Math.Clamp(PageIndex, 0, _pages.Count - 1)];
        PageLabel = _pages.Count == 0 ? "—" : $"{PageIndex + 1} از {_pages.Count}";
    }

    partial void OnPageIndexChanged(int value) => Show();

    [RelayCommand]
    private void Next() { if (PageIndex + 1 < _pages.Count) PageIndex++; }

    [RelayCommand]
    private void Prev() { if (PageIndex > 0) PageIndex--; }

    /// <summary>ذخیرهٔ فایلِ PDF کنارِ کاربر — بی هیچ وابستگی به چاپگر.</summary>
    public string SaveTo(string folder)
    {
        Directory.CreateDirectory(folder);
        var safe = string.Join("_", Title.Split(Path.GetInvalidFileNameChars()));
        var path = Path.Combine(folder, safe + ".pdf");
        _doc.GeneratePdf(path);
        return path;
    }

    public byte[] ToPdfBytes()
    {
        using var ms = new MemoryStream();
        _doc.GeneratePdf(ms);
        return ms.ToArray();
    }
}
