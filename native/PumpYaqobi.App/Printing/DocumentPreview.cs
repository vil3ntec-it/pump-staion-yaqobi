using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PumpYaqobi.Reporting.Pdf;
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
///
/// ⚠️ سند با هر بار عوض شدنِ «تنظیمِ ورق» **از نو ساخته می‌شود**، نه اینکه
/// همان تصویرها کش شوند: اندازهٔ کاغذ و حاشیه‌ها چیدمانِ جدول را عوض می‌کنند
/// و بزرگ‌نماییِ ساختگی، ورقی نشان می‌داد که با چاپ فرق داشت.
/// </summary>
public sealed partial class DocumentPreviewViewModel : ObservableObject
{
    private readonly Func<PageSetup, IDocument> _build;
    private IDocument _doc;
    private readonly List<Bitmap> _pages = new();

    public DocumentPreviewViewModel(Func<PageSetup, IDocument> build, string title,
                                    PageSetup? setup = null)
    {
        _build = build;
        Title = title;
        Setup = setup ?? PageSetup.Default;
        _doc = _build(Setup);
        Render();
    }

    public string Title { get; }

    /// <summary>تنظیمِ ورقِ جاری — پنجرهٔ «تنظیمات» همین را عوض می‌کند.</summary>
    public PageSetup Setup { get; private set; }

    public IReadOnlyList<Bitmap> Pages => _pages;

    [ObservableProperty] private int _pageIndex;
    [ObservableProperty] private Bitmap? _currentPage;
    [ObservableProperty] private string _pageLabel = "";
    [ObservableProperty] private string _status = "";

    public int PageCount => _pages.Count;

    /// <summary>
    /// سند را با تنظیمِ تازه از نو می‌سازد. روی نخِ پس‌زمینه صدا زده می‌شود —
    /// یک گزارشِ چندصد ردیفی چند صدم ثانیه نیست.
    /// </summary>
    public void Rebuild(PageSetup setup)
    {
        Setup = setup;
        _doc = _build(setup);
        Render();
    }

    private void Render()
    {
        foreach (var b in _pages) b.Dispose();
        _pages.Clear();

        foreach (var bytes in _doc.GenerateImages(new ImageGenerationSettings
        {
            ImageFormat = ImageFormat.Png,
            RasterDpi = Math.Clamp(Setup.Dpi, 72, 400),   // خوانا روی صفحه، سبک برای حافظه
        }))
        {
            using var ms = new MemoryStream(bytes);
            _pages.Add(new Bitmap(ms));
        }
        PageIndex = 0;
        Show();
        OnPropertyChanged(nameof(PageCount));
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
