using PumpYaqobi.App.ViewModels;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ ردیفِ حذف‌شده هرگز دوباره نوشته نمی‌شود (۱۴۰۵/۰۷/۱۲) ════════════════════
///
/// سنجهٔ ‎person‎ گرفتش: رسیدی که همان لحظه حذف شد، ذخیرهٔ تأخیری‌اش هنوز در
/// صف بود و کمی بعد همان ردیف را با ‎IsDeleted = false‎ دوباره روی دیسک
/// نوشت — ردیفِ حذف‌شده برمی‌گشت. این‌جا همان مسابقه بی دیتابیس بازسازی
/// می‌شود.
/// </summary>
public class RowRetireTests
{
    private sealed class Probe : RowViewModel
    {
        public int Saves;
        public TaskCompletionSource? Hold;
        public void Edit() => Touch();
        protected override void Apply() { }
        protected override async Task SaveAsync()
        {
            if (Hold is { } h) await h.Task;
            Interlocked.Increment(ref Saves);
        }
    }

    [Fact]
    public async Task TaghirDarSaf_BaHazf_LaghvMishavad()
    {
        var r = new Probe();
        r.Edit();                                   // یک ذخیرهٔ تأخیری در صف
        await r.RetireAsync();                      // همان لحظه حذف شد
        await Task.Delay(RowViewModel.SaveDelayMs * 4);
        await r.FlushAsync();                       // بستنِ برنامه / Ctrl+S
        Assert.Equal(0, r.Saves);
        Assert.False(r.IsDirty);
    }

    [Fact]
    public async Task NeveshtaneDarRah_PishAzHazf_TamamMishavad()
    {
        var r = new Probe { Hold = new TaskCompletionSource() };
        r.Edit();
        var flush = r.FlushAsync();                 // نوشتن شروع شد و وسطِ کار است
        await Task.Delay(50);
        var retire = r.RetireAsync();
        await Task.Delay(50);
        Assert.False(retire.IsCompleted);           // حذف منتظرِ همان نوشتن می‌ماند
        r.Hold.SetResult();
        await Task.WhenAll(flush, retire);
        Assert.Equal(1, r.Saves);
    }

    [Fact]
    public async Task FlushDovom_MontazereNeveshtaneAvval_MiManad()
    {
        //  ⛔ پیش از این «دومی رد شود» بود: ‎FlushAsync‎ همان لحظه برمی‌گشت و
        //  «جدولِ جدید» پیش از تمام شدنِ نوشتن آرشیو می‌کرد.
        var r = new Probe { Hold = new TaskCompletionSource() };
        r.Edit();
        var first = r.FlushAsync();
        await Task.Delay(50);
        var second = r.FlushAsync();
        await Task.Delay(50);
        Assert.False(second.IsCompleted);
        r.Hold.SetResult();
        await Task.WhenAll(first, second);
        Assert.Equal(1, r.Saves);
    }

    [Fact]
    public void HarMasireHazf_RadifRa_BaznashasteMikonad()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        string Src(string rel) => File.ReadAllText(Path.Combine(root, rel.Replace('/', Path.DirectorySeparatorChar)));
        foreach (var f in new[]
        {
            "PumpYaqobi.App/ViewModels/LedgerSectionViewModel.cs",
            "PumpYaqobi.App/ViewModels/Sections/PersonViewModel.cs",
            "PumpYaqobi.App/ViewModels/Sections/CompanySectionViewModel.cs",
            "PumpYaqobi.App/ViewModels/Sections/AmanatSectionViewModel.cs",
            "PumpYaqobi.App/ViewModels/Sections/WaraqSectionViewModel.cs",
            "PumpYaqobi.App/ViewModels/Sections/ParchaReceiptSectionViewModel.cs",
            "PumpYaqobi.App/ViewModels/Sections/StorageSectionViewModel.cs",
        })
            Assert.Contains("RetireAsync()", Src(f));
    }
}
