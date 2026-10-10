using Microsoft.EntityFrameworkCore;
using PumpYaqobi.Application.Security;
using PumpYaqobi.Application.Services;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;
using PumpYaqobi.Services.Data;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ خریدِ مخزن: قیمت و نرخ اختیاری، و ✏️ پس از ثبت — ۱۴۰۵/۰۷/۱۶ ════════════
///
/// «اگه کسی نرخ دالر یا قیمت هر تن رو نزد اجباری نباشه… بعد از ثبت هم بشه
/// ویرایشش کرد… شاید شرکت نگفته باشه». هر بند با دفترِ واقعیِ SQLite.
/// </summary>
public class StorageBuyEditTests : IDisposable
{
    private readonly string _file = Path.Combine(Path.GetTempPath(), $"pump-buyedit-{Guid.NewGuid():N}.db");

    public void Dispose()
    {
        foreach (var f in new[] { _file, _file + "-wal", _file + "-shm" })
            try { if (File.Exists(f)) File.Delete(f); } catch { }
    }

    private (StorageDataService Storage, CompanyDataService Companies, SettingsService Settings, PumpDbFactory Db) Host()
    {
        var dbf = new PumpDbFactory(_file);
        dbf.EnsureReady();
        var session = new UserSession();
        session.SignIn(UserRole.Admin, "آزمون");
        var perm = new PermissionService(session);
        var trash = new TrashService(dbf, perm, session);
        var settings = new SettingsService(dbf, perm);
        var companies = new CompanyDataService(dbf, perm, trash);
        return (new StorageDataService(dbf, perm, trash, new StorageService(), settings, companies), companies, settings, dbf);
    }

    private static FuelPurchase Buy(string seller, decimal price, decimal rate, string date = "1405/07/10") => new()
    {
        Fuel = FuelType.Petrol, DateShamsi = date, Seller = seller,
        Kg = 7300m, Density = 0.73m, PriceTon = price, UsdRate = rate, Note = "",
    };

    private static async Task<List<CompanyRow>> RowsOf(PumpDbFactory dbf, string? id)
    {
        await using var db = dbf.Create();
        return await db.CompanyRows.AsNoTracking().Where(r => r.SourcePurchaseId == id).ToListAsync();
    }

    [Fact]
    public async Task BiGheymatVaNerkh_SabtMishavad_VaFiLitrSefrNemishavad()
    {
        var (storage, _, settings, dbf) = Host();
        await storage.AddPurchaseAsync(Buy("", 1000m, 70m));
        var before = settings.GetDecimal(SettingsService.BuyPerLiterPetrol);
        Assert.True(before > 0m);

        var p = await storage.AddPurchaseAsync(Buy("شرکت الف", 0m, 0m));
        Assert.Equal(10000m, Math.Round(p.Liters, 0));             // لیتر از وزن و ثقلت
        Assert.Equal(before, settings.GetDecimal(SettingsService.BuyPerLiterPetrol));
        var rows = await RowsOf(dbf, p.LegacyId);
        Assert.Single(rows);
        Assert.Equal(7.3m, rows[0].Ton);
    }

    [Fact]
    public async Task Virayesh_GheymatRaMiresanad_BeHesabeSherkat_VaFiLitr()
    {
        var (storage, _, settings, dbf) = Host();
        var p = await storage.AddPurchaseAsync(Buy("شرکت الف", 0m, 0m));

        p.PriceTon = 1000m; p.UsdRate = 70m;
        await storage.UpdatePurchaseAsync(p);

        var rows = await RowsOf(dbf, p.LegacyId);
        Assert.Single(rows);
        Assert.Equal(1000m, rows[0].Usd);
        Assert.Equal(70m, rows[0].Rate);
        Assert.Equal(p.PerLiter, settings.GetDecimal(SettingsService.BuyPerLiterPetrol));
        Assert.True(p.PerLiter > 0m);
    }

    [Fact]
    public async Task VirayesheKharideKohne_FiLitreEmruzRaAvazNemikonad()
    {
        var (storage, _, settings, _) = Host();
        var old = await storage.AddPurchaseAsync(Buy("", 0m, 0m, "1405/06/01"));
        await storage.AddPurchaseAsync(Buy("", 1000m, 70m));
        var now = settings.GetDecimal(SettingsService.BuyPerLiterPetrol);

        old.PriceTon = 2000m; old.UsdRate = 80m;
        await storage.UpdatePurchaseAsync(old);
        Assert.Equal(now, settings.GetDecimal(SettingsService.BuyPerLiterPetrol));
    }

    [Fact]
    public async Task ForushandeyeDirAmade_DarHesabeSherkatMineshinad_MagarDastiJodaShode()
    {
        var (storage, companies, _, dbf) = Host();
        var p = await storage.AddPurchaseAsync(Buy("", 0m, 0m));
        Assert.Empty(await RowsOf(dbf, p.LegacyId));

        p.Seller = "شرکت دیرآمده";
        await storage.UpdatePurchaseAsync(p);
        var rows = await RowsOf(dbf, p.LegacyId);
        Assert.Single(rows);

        // کاربر خودش ردیف را از حسابِ شرکت برداشت ⇒ ویرایشِ بعدی برش نمی‌گرداند
        await companies.DeleteRowAsync(rows[0].Id);
        p.PriceTon = 900m;
        await storage.UpdatePurchaseAsync(p);
        Assert.Empty(await RowsOf(dbf, p.LegacyId));
    }

    // ══ ویرایشِ خریدِ پیوندخورده هیچ‌وقت شرکتِ تازه نمی‌سازد (۱۴۰۵/۰۷/۱۸) ══════

    private static async Task<List<TilCompany>> Companies(PumpDbFactory dbf)
    {
        await using var db = dbf.Create();
        return await db.TilCompanies.AsNoTracking().ToListAsync();
    }

    [Fact]
    public async Task VirayesheGheymat_HamanSherkat_BiSherkateTaze()
    {
        var (storage, _, _, dbf) = Host();
        var p = await storage.AddPurchaseAsync(Buy("حمید", 1000m, 70m));
        var before = await Companies(dbf);
        Assert.Single(before);

        // شرکت در بخشِ شرکت‌ها نامش عوض شد ⇒ ویرایشِ قیمت باز همان شرکت
        await using (var db = dbf.Create())
        {
            var c = await db.TilCompanies.FirstAsync();
            c.Name = "شرکت نفتی ستاره";
            await db.SaveChangesAsync();
        }
        p.PriceTon = 1200m;
        await storage.UpdatePurchaseAsync(p);

        var after = await Companies(dbf);
        Assert.Single(after);
        Assert.Equal("شرکت نفتی ستاره", after[0].Name);
        var rows = await RowsOf(dbf, p.LegacyId);
        Assert.Single(rows);
        Assert.Equal(after[0].Id, rows[0].CompanyId);
        Assert.Equal(1200m, rows[0].Usd);
    }

    [Fact]
    public async Task VirayesheNam_HamanSherkatNamashDorostMishavad()
    {
        var (storage, _, _, dbf) = Host();
        var p = await storage.AddPurchaseAsync(Buy("شرکت الف", 1000m, 70m));
        var id = (await Companies(dbf)).Single().Id;

        p.Seller = "شرکت کاملاً دیگر";
        await storage.UpdatePurchaseAsync(p);

        var after = await Companies(dbf);
        Assert.Single(after);
        Assert.Equal(id, after[0].Id);
        Assert.Equal("شرکت کاملاً دیگر", after[0].Name);
        Assert.Equal(id, (await RowsOf(dbf, p.LegacyId)).Single().CompanyId);
    }

    [Fact]
    public async Task VirayesheNam_BeSherkateMojud_JabejaMishavad_BiSherkateTaze()
    {
        var (storage, _, _, dbf) = Host();
        var a = await storage.AddPurchaseAsync(Buy("شرکت الف", 1000m, 70m));
        await storage.AddPurchaseAsync(Buy("شرکت ب", 1000m, 70m));
        var b = (await Companies(dbf)).Single(c => c.Name == "شرکت ب");

        a.Seller = "شرکت ب";
        await storage.UpdatePurchaseAsync(a);

        Assert.Equal(2, (await Companies(dbf)).Count);
        Assert.Equal(b.Id, (await RowsOf(dbf, a.LegacyId)).Single().CompanyId);
    }

    [Fact]
    public async Task VirayesheNam_SherkatiKeKharideDigarDarad_NamashDastNemikhorad()
    {
        var (storage, _, _, dbf) = Host();
        var a = await storage.AddPurchaseAsync(Buy("شرکت الف", 1000m, 70m));
        await storage.AddPurchaseAsync(Buy("شرکت الف", 900m, 70m));
        var id = (await Companies(dbf)).Single().Id;

        a.Seller = "نامِ غلطِ تازه";
        await storage.UpdatePurchaseAsync(a);

        var after = await Companies(dbf);
        Assert.Single(after);
        Assert.Equal("شرکت الف", after[0].Name);
        var row = (await RowsOf(dbf, a.LegacyId)).Single();
        Assert.Equal(id, row.CompanyId);
        Assert.Equal("نامِ غلطِ تازه", row.Name);
    }

    [Fact]
    public void Sors_FaghatVaznVaSaghlatLazemAnd_VaGhalamRuyeKart()
    {
        var root = FindRoot();
        var vm = SrcText.Read(Path.Combine(root, "PumpYaqobi.App/ViewModels/Sections/StorageSectionViewModel.cs"));
        //  «فقط وزن و ثقلت لازم‌اند» ⇒ رفتاری: StorageBuyBehaviourTests (شورا، ت۳)
        Assert.Contains("private void EditPurchase(PurchaseRowViewModel? row)", vm);

        var view = SrcText.Read(Path.Combine(root, "PumpYaqobi.App/Views/Sections/StorageSectionView.axaml"));
        Assert.Contains("EditPurchaseCommand", view);
        Assert.Contains("x:Name=\"BuyCard\" Classes=\"modal\" KeyboardNavigation.TabNavigation=\"Cycle\"", view);

        var nav = SrcText.Read(Path.Combine(root, "PumpYaqobi.App/Services/FieldNavigation.cs"));
        Assert.Contains("ModalOf(from)", nav);
    }

    private static string FindRoot()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null && !File.Exists(Path.Combine(d.FullName, "PumpYaqobi.App", "PumpYaqobi.App.csproj")))
            d = d.Parent;
        return d?.FullName ?? throw new DirectoryNotFoundException("native/");
    }
}
