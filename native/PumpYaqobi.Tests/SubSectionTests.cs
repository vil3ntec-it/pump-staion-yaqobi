using PumpYaqobi.App.Services;
using PumpYaqobi.App.ViewModels;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ هجده بخش، نه بیست‌وپنج ══════════════════════════════════════════════════
///
/// گزارشِ صاحب ریپو: «به جای ۱۸ بخش، تو برنامه ۲۵ بخش است که این خودش یک
/// مشکل بزرگ است… این‌ها باید توی بخش‌های دیگر وقتی روی‌شان می‌زدم می‌رفتند
/// توی همان صفحهٔ مورد نظر، نه این‌که یک بخش برایش درست بشود.»
///
/// هفت بخشِ اضافه در سایت هم دکمهٔ نوار ندارند — هر کدام یک
/// ‎.tool-link-card‎ داخلِ بخشِ دیگری‌اند. این آزمون همان نقشه را قفل می‌کند:
/// نوار هجده‌تاست، و هر یک از آن هفت‌تا زیرِ بخشِ درستِ خودش نشسته.
///
/// ⚠️ اگر روزی یکی‌شان دوباره به نوار برگردد، ‎Ctrl+Shift+عدد‎ هم جابه‌جا
/// می‌شود — و همان آزمونِ ترتیبِ نوار هم قرمز خواهد شد.
/// </summary>
[Collection(AppHostCollection.Name)]
public class SubSectionTests
{
    private static MainViewModel Shell()
    {
        var host = AppHost.Start(
            Path.Combine(Path.GetTempPath(), "pump-sub-" + Guid.NewGuid().ToString("N"), "pump.db"));
        if (host.Auth.NeedsFirstRun()) host.Auth.CreateFirstAdmin("1234");
        host.Auth.SignIn("admin", "1234");
        return new MainViewModel();
    }

    [Fact]
    public void TheNavHasExactlyTheSitesEighteenSections()
        => Assert.Equal(18, Shell().Sections.Count);

    [Theory]
    // زیربخش  →  بخشی که در سایت کارتش آن‌جاست
    [InlineData("invrate", "invoices")]
    [InlineData("chakana", "debtrasid")]
    [InlineData("tanker", "storage")]
    [InlineData("staffshort", "attendance")]
    [InlineData("oldloans", "debt")]
    [InlineData("monthreport", "profit")]
    [InlineData("ratehist", "profit")]
    [InlineData("datamgmt", "settings")]
    public void EachExtraPageSitsUnderTheRightSection(string subId, string parentId)
    {
        var vm = Shell();

        // در نوار نیست
        Assert.DoesNotContain(vm.Sections, s => s.Id == subId);

        // ولی زیرِ بخشِ خودش هست
        var parent = vm.Sections.Single(s => s.Id == parentId);
        var sub = parent.SubSections.Single(s => s.Id == subId);
        Assert.Same(parent, sub.ParentSection);
        Assert.False(string.IsNullOrWhiteSpace(sub.LinkTitle));
    }

    /// <summary>
    /// باز کردنِ زیربخش، محتوای صفحه را عوض می‌کند و نوارِ «برگشت» را می‌آورد؛
    /// بستنش دقیقاً برمی‌گرداند. بی این، کاربر داخلِ زیربخش گیر می‌کرد.
    /// </summary>
    [Fact]
    public async Task OpeningAndClosingASubSectionSwapsTheContent()
    {
        var vm = Shell();
        var debt = vm.Sections.Single(s => s.Id == "debt");
        await vm.GoAsync(debt);

        Assert.False(vm.IsSubOpen);
        Assert.Same(debt, vm.Content);

        var old = debt.SubSections.Single(s => s.Id == "oldloans");
        debt.ShowSubCommand.Execute(old);

        Assert.True(vm.IsSubOpen);
        Assert.Same(old, vm.Content);
        Assert.Same(old, vm.ActiveSection);

        debt.CloseSubCommand.Execute(null);

        Assert.False(vm.IsSubOpen);
        Assert.Same(debt, vm.Content);
    }

    /// <summary>
    /// ‎GoByIdAsync‎ باید زیربخش را هم بشناسد — لینک‌های داشبورد به
    /// «قرض‌های کهنه» می‌روند و در فهرستِ ‎Sections‎ نیستند.
    /// </summary>
    [Fact]
    public async Task GoByIdFindsASubSectionAndOpensItsParentFirst()
    {
        var vm = Shell();
        await vm.GoByIdAsync("oldloans");

        Assert.Equal("debt", vm.Current?.Id);
        Assert.Equal("oldloans", vm.Content?.Id);
    }

    /// <summary>
    /// زدنِ دوبارهٔ دکمهٔ همان بخش یعنی «از اول» — زیربخشِ باز باید بسته شود،
    /// وگرنه کاربر روی «قرض‌داران» می‌زند و باز هم «قرض‌های کهنه» را می‌بیند.
    /// </summary>
    [Fact]
    public async Task PressingTheSameNavButtonClosesAnOpenSubSection()
    {
        var vm = Shell();
        await vm.GoByIdAsync("oldloans");
        Assert.True(vm.IsSubOpen);

        await vm.GoAsync(vm.Current);

        Assert.False(vm.IsSubOpen);
    }
}
