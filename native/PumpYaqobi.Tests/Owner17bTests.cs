using PumpYaqobi.Application.Security;
using PumpYaqobi.Application.Services;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;
using PumpYaqobi.Services.Data;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ فهرستِ ۱۴۰۵/۰۷/۱۷ (دوم) — حسابِ فرعی، جستجوی خرید، ثقلت ═══════════════
/// </summary>
public class Owner17bTests : IDisposable
{
    private readonly string _file = Path.Combine(Path.GetTempPath(), $"pump-o17b-{Guid.NewGuid():N}.db");
    public void Dispose() { try { if (File.Exists(_file)) File.Delete(_file); } catch { } }

    private static string Root()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null && !File.Exists(Path.Combine(d.FullName, "PumpYaqobi.sln"))) d = d.Parent;
        return d!.FullName;
    }

    // ── ۵) شمارهٔ تماسِ حسابِ فرعی از اصلی جداست ──────────────────────────

    [Fact]
    public async Task HesabeFarei_ShomareyeKhodash_RaDarad_VaAsliDastNemikhorad()
    {
        var dbf = new PumpDbFactory(_file);
        dbf.EnsureReady();
        var session = new UserSession();
        session.SignIn(UserRole.Admin, "آزمون");
        var perm = new PermissionService(session);
        var svc = new DebtorService(dbf, perm, new TrashService(dbf, perm, session));

        long pid;
        await using (var db = dbf.Create())
        {
            var p = new Debtor { Name = "هارون", LegacyId = "o17", Phone = "0700111222", BuyFeeNote = "56" };
            db.Debtors.Add(p);
            await db.SaveChangesAsync();
            db.DebtAccounts.Add(new DebtAccount { MainOfDebtorId = p.Id, Name = "هارون" });
            await db.SaveChangesAsync();
            pid = p.Id;
        }
        var sub = await svc.AddSubAccountAsync(pid, "دکان");
        Assert.Null(sub.Phone);                     // حسابِ فرعیِ تازه هیچ چیزی از اصلی به ارث نمی‌برد
        Assert.Null(sub.BuyFeeNote);

        sub.Phone = "0799888777";
        sub.BuyFeeNote = "57";
        await svc.UpdateAccountAsync(sub);

        var full = (await svc.LoadFullAsync(pid))!;
        Assert.Equal("0700111222", full.Phone);    // اصلی دست نخورد
        Assert.Equal("56", full.BuyFeeNote);
        var s = full.SubAccounts.Single();
        Assert.Equal("0799888777", s.Phone);
        Assert.Equal("57", s.BuyFeeNote);
        Assert.Null(full.MainAccount.Phone);        // حسابِ اصلی همچنان از خودِ شخص می‌خواند
    }

    [Fact]
    public void KadreShomare_MaleHesabeJeloyeCheshm_Ast()
    {
        var src = File.ReadAllText(Path.Combine(Root(), "PumpYaqobi.App", "ViewModels", "Sections", "PersonViewModel.cs"));
        Assert.Contains("get => Current?.PhoneText", src);
        Assert.Contains("get => Current?.BuyFeeText", src);
        Assert.Contains("(Entity.IsMain ? _person.Entity.Phone : Entity.Phone)", src);
        Assert.Contains("(Entity.IsMain ? _person.Entity.BuyFeeNote : Entity.BuyFeeNote)", src);
        Assert.Contains("partial void OnCurrentChanged(AccountViewModel? value)", src);
        var fac = File.ReadAllText(Path.Combine(Root(), "PumpYaqobi.Services", "Data", "PumpDbFactory.cs"));
        Assert.Contains("(\"DebtAccounts\", \"Phone\", \"TEXT\")", fac);
        Assert.Contains("(\"DebtAccounts\", \"BuyFeeNote\", \"TEXT\")", fac);
    }

    // ── ۶) کشوییِ «کارها» پس از «نه» دوباره کار می‌کند ─────────────────────

    [Fact]
    public void KashoyeKarha_YekTikBad_BeKarhaBarmigardad()
    {
        var src = File.ReadAllText(Path.Combine(Root(), "PumpYaqobi.App", "ViewModels", "Sections", "CompanySectionViewModel.cs"));
        var i = src.IndexOf("partial void OnActionChanged", StringComparison.Ordinal);
        var body = src[i..src.IndexOf("internal void ResetAction()", i, StringComparison.Ordinal)];
        Assert.DoesNotContain("Action = Actions[0];", body);          // نه داخلِ خبرِ خودش
        Assert.Contains("Dispatcher.UIThread.Post(ResetAction)", body);
    }

    // ── ۶ب) جستجوی خرید: تاریخِ دقیق، و نامِ شرکت در هر نتیجه ─────────────

    [Theory]
    [InlineData("1405/5/1", "1405/5/12", false)]   // پیش از این «زیررشته» بود و جور درمی‌آمد
    [InlineData("1405/5/12", "1405/05/12", true)]
    [InlineData("1405/5", "1405/5/12", true)]
    [InlineData("1405/5", "1405/6/5", false)]
    [InlineData("5/12", "1405/5/12", true)]
    [InlineData("1405", "1405/5/12", true)]
    [InlineData("1404", "1405/5/12", false)]
    [InlineData("5", "1405/6/5", true)]
    [InlineData("5", "1405/5/12", false)]
    [InlineData("", "1405/5/12", true)]
    public void TarikheJostoju_KhaneBeKhane(string q, string d, bool ok) =>
        Assert.Equal(ok, CompanyPurchaseService.DateOk(CompanyPurchaseService.DateNorm(q), d));

    [Fact]
    public void HarNatije_NameSherkat_Darad()
    {
        var svc = new CompanyPurchaseService(new CompanyService());
        var co = new TilCompany { Id = 1, Name = "پارس" };
        var buy = new FuelPurchase { Id = 7, LegacyId = "fe7", Seller = "پارس", Fuel = FuelType.Petrol, DateShamsi = "1405/5/12", Ton = 12.3m };
        var lone = new FuelPurchase { Id = 8, LegacyId = "fe8", Seller = "ناشناس", Fuel = FuelType.Petrol, DateShamsi = "1405/5/12", Ton = 12.3m };
        co.Rows.Add(new CompanyRow { CompanyId = 1, Fuel = FuelType.Diesel, DateShamsi = "1405/5/12", Name = "راننده", Ton = 12.3m });
        var hits = svc.Search(new[] { buy, lone }, new[] { co },
            Array.Empty<(CompanyTableArchive, IReadOnlyList<CompanyRow>)>(), 12300m, "", null, null);
        Assert.Equal(3, hits.Count);
        Assert.Equal("پارس", hits.Single(h => h.PurchaseId == 7).CompanyName);
        Assert.Equal("", hits.Single(h => h.PurchaseId == 8).CompanyName);
        Assert.Equal("پارس", hits.Single(h => !h.IsPurchase).CompanyName);   // نامِ ردیف «راننده» است، شرکت «پارس»
    }

    [Fact]
    public void Jostoju_HamisheSafheRaBazMikonad_BaSarbargeRahnama()
    {
        var root = Root();
        var vm = File.ReadAllText(Path.Combine(root, "PumpYaqobi.App", "ViewModels", "Sections", "CompanySectionViewModel.cs"));
        var i = vm.IndexOf("public Task FindInlineAsync", StringComparison.Ordinal);
        var body = vm[i..vm.IndexOf("[ObservableProperty] private string _findText", i, StringComparison.Ordinal)];
        Assert.DoesNotContain("GoToAsync", body);                       // دیگر بی‌خبر به حسابی نمی‌پرد
        Assert.Contains("Overlay = sp;", body);
        var view = File.ReadAllText(Path.Combine(root, "PumpYaqobi.App", "Views", "Sections", "CompanySearchView.axaml"));
        Assert.Contains("{Binding GuideText}", view);
        Assert.Contains("{Binding CompanyText}", view);
        Assert.Contains("{Binding DateText}", view);
        Assert.Contains("{Binding TonText}", view);
    }

    // ── ۷) ثقلت بی نقطه ──────────────────────────────────────────────────

    [Theory]
    [InlineData("0", "0")]
    [InlineData("07", "0.7")]
    [InlineData("0730", "0.730")]
    [InlineData("۰۷۳۰", "0.730")]
    [InlineData("0.73", "0.73")]
    [InlineData("0٫73", "0.73")]
    [InlineData("", "")]
    public void Seghlat_NoghteKhodash(string raw, string want) => Assert.Equal(want, DensityInput.Typed(raw));

    [Theory]
    [InlineData("0730", "0.73")]
    [InlineData("۰۷۳۰", "0.73")]
    [InlineData("730", "0.73")]
    [InlineData("0.835", "0.835")]
    [InlineData("0,730", "0.73")]
    [InlineData("0", "0")]
    [InlineData("", "0")]
    public void Seghlat_Adad(string raw, string want) =>
        Assert.Equal(decimal.Parse(want, System.Globalization.CultureInfo.InvariantCulture), DensityInput.Parse(raw));
}
