using System.Text.Json;
using PumpYaqobi.Application.Services;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// بردنِ یک ردیف به حسابِ صاحبش — تطبیقِ نام، نرمال‌سازیِ متن و تشخیصِ سوخت.
///
/// «رسید پارچه‌ها» و «ورق روزانه» هر دو از همین راه رد می‌شوند، پس یک اشتباهِ
/// کوچک این‌جا یعنی پولِ یک نفر در حسابِ نفرِ دیگری. ۳۰۰ تطبیق و ۲۰۰ متن که
/// خودِ نسخهٔ وب در یک کرومیومِ واقعی پاسخشان را داده.
/// </summary>
public class PostingParityTests
{
    private sealed record P(string name, List<string> subs);
    private sealed record MatchCase(string raw, string typed, string? person, string? acct);
    private sealed record TextCase(string t, string norm, string strip, string fuel);
    private sealed record Golden(List<P> persons, List<MatchCase> cases, List<TextCase> texts);

    private static Golden Load()
    {
        var p = Path.Combine(AppContext.BaseDirectory, "golden-posting.json");
        if (!File.Exists(p)) p = "golden-posting.json";
        return JsonSerializer.Deserialize<Golden>(File.ReadAllText(p))!;
    }

    private static List<Debtor> People(Golden g) => g.persons.Select(p => new Debtor
    {
        Name = p.name,
        MainAccount = new DebtAccount { Name = p.name },
        SubAccounts = p.subs.Select(s => new DebtAccount { Name = s, LegacySubId = "s" }).ToList(),
    }).ToList();

    [Fact]
    public void Name_matching_picks_the_same_account_as_the_web()
    {
        var g = Load();
        var people = People(g);
        foreach (var c in g.cases)
        {
            var m = PostingService.FindAccountForText(people, c.raw, c.typed);
            if (c.person is null) { Assert.Null(m); continue; }
            Assert.NotNull(m);
            Assert.Equal(c.person, m!.Value.Person.Name);
            Assert.Equal(c.acct, m.Value.Account.Name ?? m.Value.Person.Name);
        }
    }

    [Fact]
    public void Text_normalisation_matches_the_web()
    {
        var g = Load();
        foreach (var c in g.texts)
        {
            Assert.Equal(c.norm, PostingService.NormFa(c.t));
            Assert.Equal(c.strip, PostingService.StripFuelWords(c.t));
            Assert.Equal(c.fuel == "diesel" ? FuelType.Diesel : FuelType.Petrol,
                         PostingService.DetectFuelType(c.t));
        }
    }

    /// <summary>
    /// ردیفِ هم‌منبع باید به‌روز شود، نه دوباره ساخته — و رسیدِ دستیِ کاربر
    /// روی آن نباید پاک شود.
    /// </summary>
    [Fact]
    public void Re_posting_the_same_source_updates_the_row_and_keeps_the_manual_receipt()
    {
        var person = new Debtor { Name = "احمد", MainAccount = new DebtAccount { Name = "احمد" } };

        PostingService.PlaceRow(person, new DebtRow
        {
            Name = "بردگی", Liters = 100, Bardagi = 6200, Albaqi = 6200,
            Src = "parcha", SrcKey = "parcha|1",
        }, "", null);

        // کاربر دستی رسید نوشت
        person.MainAccount.FuelRows[0].Rasid = 2000;

        PostingService.PlaceRow(person, new DebtRow
        {
            Name = "بردگی", Liters = 120, Bardagi = 7440, Albaqi = 7440,
            Src = "parcha", SrcKey = "parcha|1",
        }, "", null);

        Assert.Single(person.MainAccount.FuelRows);
        var row = person.MainAccount.FuelRows[0];
        Assert.Equal(7440m, row.Bardagi);
        Assert.Equal(2000m, row.Rasid);          // پاک نشد
        Assert.Equal(5440m, row.Albaqi);         // بردگیِ تازه منهای همان رسید
    }

    /// <summary>ردیفِ کاملاً خالیِ داخلِ دفتر پر می‌شود، نه اینکه ردیفِ تازه بیفتد ته جدول.</summary>
    [Fact]
    public void An_empty_row_is_filled_instead_of_appending()
    {
        var person = new Debtor { Name = "ولی", MainAccount = new DebtAccount { Name = "ولی" } };
        person.MainAccount.FuelRows.Add(new DebtRow());

        PostingService.PlaceRow(person, new DebtRow { Name = "خرید", Liters = 50, Bardagi = 3100 }, "", null);

        Assert.Single(person.MainAccount.FuelRows);
        Assert.Equal("خرید", person.MainAccount.FuelRows[0].Name);
    }

    /// <summary>رسیدی که پیش‌تر از ورق آمده، دوباره ثبت نمی‌شود.</summary>
    [Fact]
    public void A_receipt_already_taken_from_the_waraq_is_seen_as_duplicate()
    {
        var person = new Debtor { Name = "نور", MainAccount = new DebtAccount { Name = "نور" } };
        person.MainAccount.FuelRows.Add(new DebtRow
        { Src = "waraq", Liters = 80, Bardagi = 4960, Hawala = "12" });

        Assert.True(PostingService.AlreadyReceivedFromWaraq(person, 80m, 4960m, "12"));
        Assert.True(PostingService.AlreadyReceivedFromWaraq(person, 80m, 4960m, ""));   // حوالهٔ خالی = نادیده
        Assert.False(PostingService.AlreadyReceivedFromWaraq(person, 80m, 4960m, "99"));
        Assert.False(PostingService.AlreadyReceivedFromWaraq(person, 81m, 4960m, "12"));
    }
}
