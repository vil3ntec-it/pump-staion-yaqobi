using Microsoft.EntityFrameworkCore;
using PumpYaqobi.Application.Security;
using PumpYaqobi.Application.Services;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;
using PumpYaqobi.Services.Data;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ جدول‌های آرشیوِ شرکت، «حذف این جدول»، خریدهای مخزن و جستجوی خرید ══════
/// همان قاعده‌های ‎newCompanyTable‎ · ‎deleteCurrentCompanyTable‎ ·
/// ‎_companyPurchases‎ · ‎runCmpSearch‎ی سایت.
/// </summary>
public class CompanyArchiveTests : IDisposable
{
    private readonly string _file = Path.Combine(Path.GetTempPath(), $"pump-carc-{Guid.NewGuid():N}.db");
    public void Dispose() { try { if (File.Exists(_file)) File.Delete(_file); } catch { } }

    private (CompanyDataService Companies, StorageDataService Storage, PumpDbFactory Db) Host()
    {
        var dbf = new PumpDbFactory(_file);
        dbf.EnsureReady();
        var session = new UserSession();
        session.SignIn(UserRole.Admin, "آزمون");
        var perm = new PermissionService(session);
        var trash = new TrashService(dbf, perm, session);
        var companies = new CompanyDataService(dbf, perm, trash);
        var settings = new SettingsService(dbf, perm);
        var storage = new StorageDataService(dbf, perm, trash, new StorageService(), settings, companies);
        return (companies, storage, dbf);
    }

    private static CompanyRow Row(long cid, FuelType fuel, string date, decimal ton, decimal usd, decimal rate, int i, string? src = null) =>
        new() { CompanyId = cid, Fuel = fuel, SortIndex = i, DateShamsi = date, Name = "ردیف " + i, Ton = ton, Usd = usd, Rate = rate, SourcePurchaseId = src };

    [Fact] // فقط جدولِ همان تیل آرشیو و خالی می‌شود؛ دفترِ تیلِ دیگر دست نمی‌خورد
    public async Task NewTable_ArchivesOnlyTheOpenFuel()
    {
        var (co, _, dbf) = Host();
        var c = await co.AddAsync("شرکتِ آزمون");
        await co.SaveRowAsync(Row(c.Id, FuelType.Petrol, "1405/6/1", 10m, 700m, 70m, 0));
        await co.SaveRowAsync(Row(c.Id, FuelType.Petrol, "1405/6/2", 5m, 700m, 70m, 1));
        await co.SaveRowAsync(Row(c.Id, FuelType.Diesel, "1405/6/3", 8m, 650m, 70m, 0));

        var h = await co.ArchiveTableAsync(c.Id, FuelType.Petrol, "1405/6/25");
        Assert.Equal(2, h.RowCount);
        Assert.Equal(FuelType.Petrol, h.Fuel);
        Assert.Equal(2, CompanyDataService.ArchiveRows(h).Count);

        var full = (await co.LoadAsync(c.Id))!;
        Assert.Empty(CompanyService.RowsOf(full, FuelType.Petrol));
        Assert.Single(CompanyService.RowsOf(full, FuelType.Diesel));
        Assert.Single(await co.ListArchivesAsync(c.Id));

        //  ⚠️ شمارندهٔ کارت با `COUNT` می‌آید، نه با خواندنِ `RowsJson`ِ همهٔ
        //  آرشیوها (اسکنِ ۱۴۰۵/۰۶/۳۰) — و پطرول و دیزل را قاطی نمی‌کند.
        var (aP, aD) = await co.CountArchivesAsync(c.Id);
        Assert.Equal(1, aP);
        Assert.Equal(0, aD);
    }

    [Fact] // جدولِ خالی آرشیو نمی‌شود
    public async Task NewTable_RefusesAnEmptyTable()
    {
        var (co, _, _) = Host();
        var c = await co.AddAsync("خالی");
        await Assert.ThrowsAsync<InvalidOperationException>(() => co.ArchiveTableAsync(c.Id, FuelType.Petrol, "1405/6/25"));
    }

    [Fact] // نقطهٔ شمارشِ خریدها جلو می‌رود: خریدِ قبلی در آرشیو، خریدِ تازه در نمای زنده
    public async Task NewTable_MovesThePurchaseCheckpoint()
    {
        var (co, st, _) = Host();
        var p1 = await st.AddPurchaseAsync(new FuelPurchase { Fuel = FuelType.Petrol, Seller = "پارس", DateShamsi = "1405/6/1", Kg = 10000, Density = 0.73m, PriceTon = 700, UsdRate = 70 });
        var c = CompanyDataService.FindByName(await co.ListAsync(), "پارس")!;
        Assert.Single(CompanyService.RowsOf(c, FuelType.Petrol));            // خرید خودش ردیف شد
        var all = await st.AllPurchasesAsync();
        Assert.Single(CompanyPurchaseService.Live(all, c, FuelType.Petrol));

        var h = await co.ArchiveTableAsync(c.Id, FuelType.Petrol, "1405/6/25");
        var p2 = await st.AddPurchaseAsync(new FuelPurchase { Fuel = FuelType.Petrol, Seller = "پارس", DateShamsi = "1405/6/26", Kg = 5000, Density = 0.73m, PriceTon = 700, UsdRate = 70 });

        c = (await co.LoadAsync(c.Id))!;
        all = await st.AllPurchasesAsync();
        var live = CompanyPurchaseService.Live(all, c, FuelType.Petrol);
        Assert.Equal(new[] { p2.Id }, live.Select(x => x.Id));
        var inArchive = CompanyPurchaseService.Of(all, c.Name, FuelType.Petrol, h.PurchasesAfter, h.PurchasesBefore);
        Assert.Equal(new[] { p1.Id }, inArchive.Select(x => x.Id));
        // خریدِ آرشیوشده به جدولِ نو برنمی‌گردد
        await st.SyncAllPurchasesToCompaniesAsync();
        c = (await co.LoadAsync(c.Id))!;
        Assert.Single(CompanyService.RowsOf(c, FuelType.Petrol));
    }

    [Fact] // آرشیو عکس است — ویرایشِ بعدی به آن نمی‌رسد
    public async Task ArchivesAreSnapshots()
    {
        var (co, _, _) = Host();
        var c = await co.AddAsync("ع");
        var r = Row(c.Id, FuelType.Petrol, "1405/6/1", 10m, 700m, 70m, 0);
        await co.SaveRowAsync(r);
        var h = await co.ArchiveTableAsync(c.Id, FuelType.Petrol, "1405/6/25");
        await co.SaveRowAsync(Row(c.Id, FuelType.Petrol, "1405/6/26", 99m, 1m, 1m, 0));
        Assert.Equal(10m, CompanyDataService.ArchiveRows((await co.ListArchivesAsync(c.Id)).Single()).Single().Ton);
    }

    [Fact] // «حذف این جدول» فقط جدولِ فعلیِ همان تیل؛ آرشیوها دست‌نخورده
    public async Task ClearTable_LeavesArchivesAndTheOtherFuel()
    {
        var (co, _, _) = Host();
        var c = await co.AddAsync("ح");
        await co.SaveRowAsync(Row(c.Id, FuelType.Petrol, "1405/6/1", 10m, 700m, 70m, 0));
        await co.ArchiveTableAsync(c.Id, FuelType.Petrol, "1405/6/2");
        await co.SaveRowAsync(Row(c.Id, FuelType.Petrol, "1405/6/3", 3m, 700m, 70m, 0));
        await co.SaveRowAsync(Row(c.Id, FuelType.Diesel, "1405/6/3", 4m, 700m, 70m, 0));

        Assert.Equal(1, await co.ClearTableAsync(c.Id, FuelType.Petrol));
        var full = (await co.LoadAsync(c.Id))!;
        Assert.Empty(CompanyService.RowsOf(full, FuelType.Petrol));
        Assert.Single(CompanyService.RowsOf(full, FuelType.Diesel));
        Assert.Single(await co.ListArchivesAsync(c.Id));
    }

    [Fact] // حذفِ آرشیو و تغییرِ نام
    public async Task DeleteArchive_AndRename()
    {
        var (co, _, _) = Host();
        var c = await co.AddAsync("قدیم");
        await co.SaveRowAsync(Row(c.Id, FuelType.Diesel, "1405/6/1", 1m, 1m, 1m, 0));
        var h = await co.ArchiveTableAsync(c.Id, FuelType.Diesel, "1405/6/2");
        await co.DeleteArchiveAsync(h.Id);
        Assert.Empty(await co.ListArchivesAsync(c.Id));
        await co.RenameAsync(c.Id, "  جدید ");
        Assert.Equal("جدید", (await co.LoadAsync(c.Id))!.Name);
    }

    // ── جستجوی خرید ──────────────────────────────────────────────────────

    [Theory]
    [InlineData("12300", 12.3, true)]      // کیلو
    [InlineData("12.3", 12.3, true)]       // تن
    [InlineData("12.4", 12.3, false)]
    [InlineData(null, 12.3, true)]         // بی‌مقدار ⇒ همه
    [InlineData("20", 0, false)]           // ردیفِ بی‌مقدار با هیچ عددی جور نیست
    [InlineData(null, 0, true)]
    public void QtyMatchesKiloOrTon(string? q, double ton, bool want)
        => Assert.Equal(want, CompanyPurchaseService.QtyOk(q is null ? null : decimal.Parse(q, System.Globalization.CultureInfo.InvariantCulture), (decimal)ton));

    [Theory]
    [InlineData("1405/05/13", "1405/5/13")]
    [InlineData("۱۴۰۵/۵/۱۳", "1405/5/13")]
    [InlineData("1405-5-13", "1405/5/13")]
    [InlineData("", "")]
    public void DatesAreNormalised(string raw, string want) => Assert.Equal(want, CompanyPurchaseService.DateNorm(raw));

    [Fact] // خریدِ مخزن یک‌بار می‌آید (ردیفِ وصل‌شده‌اش تکرار نمی‌شود)، ردیفِ دستی و ردیفِ آرشیو هم پیدا می‌شوند
    public void Search_FindsPurchasesRowsAndArchivesOnce()
    {
        var svc = new CompanyPurchaseService(new CompanyService());
        var co = new TilCompany { Id = 1, Name = "پارس" };
        var buy = new FuelPurchase { Id = 7, LegacyId = "fe7", Seller = "پارس", Fuel = FuelType.Petrol, DateShamsi = "1405/5/12", Ton = 12.3m, TotalUsd = 8000, TotalAfn = 560000 };
        co.Rows.Add(new CompanyRow { CompanyId = 1, Fuel = FuelType.Petrol, DateShamsi = "1405/5/12", Ton = 12.3m, Usd = 650, Rate = 70, SourcePurchaseId = "fe7" });
        co.Rows.Add(new CompanyRow { CompanyId = 1, Fuel = FuelType.Diesel, DateShamsi = "1405/5/12", Name = "دستی", Ton = 12.3m, Usd = 600, Rate = 70 });
        var arc = new CompanyTableArchive { Id = 3, CompanyId = 1, Fuel = FuelType.Petrol, CreatedShamsi = "1405/4/1" };
        var arcRows = new List<CompanyRow> { new() { Fuel = FuelType.Petrol, DateShamsi = "1405/3/9", Ton = 12.3m, Name = "کهنه" } };

        var hits = svc.Search(new[] { buy }, new[] { co }, new[] { (arc, (IReadOnlyList<CompanyRow>)arcRows) }, 12300m, "", null, null);
        Assert.Equal(3, hits.Count);
        Assert.Single(hits, h => h.IsPurchase && h.PurchaseId == 7 && h.CompanyId == 1);
        Assert.Single(hits, h => !h.IsPurchase && h.ArchiveId == 0 && h.Fuel == FuelType.Diesel);
        Assert.Single(hits, h => !h.IsPurchase && h.ArchiveId == 3 && h.RowIndex == 0);

        // فیلترِ تیل و تاریخ
        Assert.Equal(2, svc.Search(new[] { buy }, new[] { co }, new[] { (arc, (IReadOnlyList<CompanyRow>)arcRows) }, 12300m, "", FuelType.Petrol, null).Count);
        Assert.Single(svc.Search(new[] { buy }, new[] { co }, new[] { (arc, (IReadOnlyList<CompanyRow>)arcRows) }, null, "1405/3/9", null, null));
        // بی مقدار و بی تاریخ ⇒ هیچ
        Assert.Empty(svc.Search(new[] { buy }, new[] { co }, Array.Empty<(CompanyTableArchive, IReadOnlyList<CompanyRow>)>(), null, "", null, null));
    }

    [Fact] // ستونِ «کیلو» از حسابِ شرکت رفت — خرید به تن است؛ و کارها/خریدها/جستجو/آرشیو در صفحه هستند
    public void ThePageHasNoKiloColumn_AndHasTheSitesTools()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        var v = File.ReadAllText(Path.Combine(root, "PumpYaqobi.App", "Views", "Sections", "CompanyPageView.axaml"));
        Assert.DoesNotContain("Header=\"کیلو\"", v);
        Assert.Contains("Header=\"خرید (تن)\"", v);
        Assert.Contains("Header=\"📦\"", v);
        Assert.Contains("{Binding Actions}", v);
        Assert.Contains("OpenPurchasesCommand", v);
        Assert.Contains("OpenArchiveCommand", v);
        Assert.Contains("SearchCommand", v);
        Assert.Contains("ClearTableCommand", v);
        Assert.Contains("RowAddCommand", File.ReadAllText(Path.Combine(root, "PumpYaqobi.App", "ViewModels", "Sections", "CompanySectionViewModel.cs")));   // نوارِ ردیف از خودِ SectionPage
        Assert.Contains("PrevCommand", v);
        var s = File.ReadAllText(Path.Combine(root, "PumpYaqobi.App", "Views", "Sections", "CompanySectionView.axaml"));
        foreach (var t in new[] { "CompanyPurchasesView", "CompanyArchiveView", "CompanySearchView" }) Assert.Contains(t, s);
    }
}
