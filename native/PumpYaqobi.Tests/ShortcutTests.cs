using PumpYaqobi.App.Services;
using PumpYaqobi.App.ViewModels;
using PumpYaqobi.App.ViewModels.Sections;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ میانبرهای صفحه‌کلید ═══════════════════════════════════════════════════
/// این آزمون‌ها خودِ «کارِ» میانبرها را می‌سنجند — نه رویداد‌های Avalonia
/// (که پنجرهٔ واقعی می‌خواهند)، بلکه قراردادی که میانبر روی آن سوار است:
/// ‎IRowBatchHost‎ و ‎ICardGridHost‎ و ‎RowHost‎.
///
/// چرا مهم است: قاعدهٔ «ردیفِ کافی نبود → هیچ کاری نکن» ظاهراً جزئی است ولی
/// در نسخهٔ وب عمداً نوشته شده بود؛ اگر روزی کسی آن را به «هرچه هست حذف کن»
/// عوض کند، کاربر با یک ‎Shift+9‎ اشتباهی کلِ جدولش را از دست می‌دهد.
/// </summary>
public class ShortcutTests
{
    /// <summary>جدولِ ساختگی — همان قراردادی که میانبر می‌شناسد.</summary>
    private sealed class FakeTable : IRowBatchHost
    {
        public List<int> Rows { get; } = new();
        public int RowCount => Rows.Count;

        public Task AddRowsAsync(int count)
        {
            for (var i = 0; i < count; i++) Rows.Add(Rows.Count + 1);
            return Task.CompletedTask;
        }

        public Task DeleteRowsAsync(int count)
        {
            if (count < 1 || Rows.Count < count) return Task.CompletedTask;
            Rows.RemoveRange(Rows.Count - count, count);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task AddRows_adds_exactly_that_many()
    {
        var t = new FakeTable();
        await t.AddRowsAsync(12);           // Ctrl نگه‌داشته، «۱» و «۲» → ۱۲
        Assert.Equal(12, t.RowCount);
    }

    [Fact]
    public async Task DeleteRows_removes_from_the_end()
    {
        var t = new FakeTable();
        await t.AddRowsAsync(10);
        await t.DeleteRowsAsync(3);
        Assert.Equal(7, t.RowCount);
        Assert.Equal(new[] { 1, 2, 3, 4, 5, 6, 7 }, t.Rows);
    }

    [Fact]
    public async Task DeleteRows_does_nothing_when_not_enough_rows()
    {
        var t = new FakeTable();
        await t.AddRowsAsync(4);
        await t.DeleteRowsAsync(9);         // ۹ > ۴ → هیچ، نه حذفِ ناقص
        Assert.Equal(4, t.RowCount);
    }

    [Fact]
    public async Task DeleteRows_ignores_zero_and_negative()
    {
        var t = new FakeTable();
        await t.AddRowsAsync(3);
        await t.DeleteRowsAsync(0);
        await t.DeleteRowsAsync(-2);
        Assert.Equal(3, t.RowCount);
    }

    // ── شمارهٔ بخش: «۰» یعنی دهمین، نه صفرم ────────────────────────────────
    [Theory]
    [InlineData("1", 1)]
    [InlineData("9", 9)]
    [InlineData("0", 10)]
    [InlineData("12", 12)]
    [InlineData("18", 18)]
    public void Section_number_is_parsed_like_the_web(string buf, int expected)
    {
        Assert.Equal(expected, SectionNumber(buf));
    }

    /// <summary>همان قاعدهٔ <c>_kbGotoSection</c>ِ نسخهٔ وب.</summary>
    private static int SectionNumber(string buf) =>
        buf == "0" ? 10 : (int.TryParse(buf, out var v) ? v : 0);

    // ══════════════════════════════════════════════════════════════════════
    //  آزمونِ سیم‌کشیِ واقعی — نه ماکت: خودِ بخش‌های برنامه روی یک دیتابیسِ
    //  موقت. اگر روزی کسی قرارداد را از یک بخش بردارد، همین‌جا قرمز می‌شود.
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// میزبانِ آزمون، با ورودِ مدیر. بدونِ ورود، لایهٔ اجازه‌ها جلوی هر
    /// نوشتنی را می‌گیرد — که خودش درست است، ولی این‌جا چیزی که سنجیده
    /// می‌شود میانبرهاست، نه اجازه‌ها.
    /// </summary>
    private static AppHost Host()
    {
        var host = AppHost.Start(
            Path.Combine(Path.GetTempPath(), "pump-kb-" + Guid.NewGuid().ToString("N"), "pump.db"));
        if (host.Auth.NeedsFirstRun()) host.Auth.CreateFirstAdmin("1234");
        host.Auth.SignIn("admin", "1234");
        return host;
    }

    [Fact]
    public async Task Expense_section_really_adds_and_deletes_rows()
    {
        var host = Host();
        var sec = new ExpenseSectionViewModel(host);
        await sec.EnsureLoadedAsync();

        var host2 = (IRowBatchHost)sec;
        var start = host2.RowCount;

        await host2.AddRowsAsync(5);
        Assert.Equal(start + 5, host2.RowCount);

        await host2.DeleteRowsAsync(2);
        Assert.Equal(start + 3, host2.RowCount);

        // بیش از آن‌چه هست → هیچ
        var before = host2.RowCount;
        await host2.DeleteRowsAsync(before + 1);
        Assert.Equal(before, host2.RowCount);
    }

    [Fact]
    public void Every_row_table_section_signs_the_contract()
    {
        var host = Host();
        // همان بخش‌هایی که در نسخهٔ وب داخلِ نقشهٔ ‎_SECT‎ بودند
        Assert.IsAssignableFrom<IRowBatchHost>(new ExpenseSectionViewModel(host));
        Assert.IsAssignableFrom<IRowBatchHost>(new ExchangeSectionViewModel(host));
        Assert.IsAssignableFrom<IRowBatchHost>(new SafeSectionViewModel(host));
        Assert.IsAssignableFrom<IRowBatchHost>(new ParchaReceiptSectionViewModel(host));
        Assert.IsAssignableFrom<IRowBatchHost>(new RetailSectionViewModel(host));
    }

    [Fact]
    public void Card_grids_can_be_opened_by_number()
    {
        var host = Host();
        Assert.IsAssignableFrom<ICardGridHost>(new DebtSectionViewModel(host));
        Assert.IsAssignableFrom<ICardGridHost>(new CompanySectionViewModel(host));
    }

    [Fact]
    public async Task RowHost_prefers_the_open_account_over_the_list_behind_it()
    {
        var host = Host();
        var debtors = new DebtSectionViewModel(host);
        await debtors.EnsureLoadedAsync();

        // فهرستِ کارت‌ها باز است → خودِ بخش جدولِ ردیفی نیست
        Assert.Null(debtors.ActivePage);

        await host.Debtors.AddDebtorAsync("آزمون", "", false);
        await debtors.RefreshAsync();
        await debtors.OpenByNumberAsync(1);          // همان ‎Alt+1‎

        // حالا حسابِ شخص باز است و میانبرها باید به آن بروند
        Assert.NotNull(debtors.ActivePage);
        Assert.IsAssignableFrom<IRowBatchHost>(debtors.ActivePage!);

        var table = (IRowBatchHost)debtors.ActivePage!;
        var start = table.RowCount;
        await table.AddRowsAsync(3);
        Assert.Equal(start + 3, table.RowCount);
    }
}
