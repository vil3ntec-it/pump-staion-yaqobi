using PumpYaqobi.App.Controls;
using PumpYaqobi.Application.Services;
using PumpYaqobi.Domain.Entities;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ «/هارون» در نامِ ورق — قاعده و تکمیل (۱۴۰۵/۰۷/۱۷) ═════════════════════
/// </summary>
public class SlashNameTests
{
    private static Debtor Person(string name, params string[] subs)
    {
        var p = new Debtor { Name = name };
        p.MainAccount.Name = name;
        foreach (var s in subs) p.SubAccounts.Add(new DebtAccount { Name = s });
        return p;
    }

    private static readonly List<Debtor> People = new()
    {
        Person("هارون", "دکان"), Person("ابراهیم"), Person("کریم احمدی"),
    };

    [Theory]
    [InlineData("ابراهیم بردگی /هارون", "هارون", "ابراهیم بردگی")]
    [InlineData("/هارون ابراهیم", "هارون", "ابراهیم")]
    [InlineData("ابراهیم /کریم احمدی", "کریم احمدی", "ابراهیم")]
    public void Slash_Hesab_VaNam(string text, string person, string display)
    {
        var m = PostingService.MatchWaraqName(People, text);
        Assert.True(m.Slashed);
        Assert.Equal(person, m.Found!.Value.Person.Name);
        Assert.Same(m.Found.Value.Person.MainAccount, m.Found.Value.Account);
        Assert.Equal(display, m.Display);
    }

    [Fact]
    public void Slash_Farei()
    {
        var m = PostingService.MatchWaraqName(People, "/هارون دکان احمد");
        Assert.Equal("دکان", m.Found!.Value.Account.Name);
        Assert.Equal("احمد", m.Display);
        var m2 = PostingService.MatchWaraqName(People, "علی /دکان");
        Assert.Equal("دکان", m2.Found!.Value.Account.Name);
        Assert.Equal("علی", m2.Display);
    }

    [Fact]
    public void Slash_Tanha_NameHesab_Mimanad()
    {
        var m = PostingService.MatchWaraqName(People, "/هارون");
        Assert.Equal("هارون", m.Found!.Value.Person.Name);
        Assert.Equal("هارون", m.Display);
    }

    [Fact]
    public void Slash_HesabiNist_HichHesabi()
    {
        //  ⛔ «ابراهیم» پیش از خط‌کج هست، ولی حساب از پسِ خط‌کج خواسته شده
        var m = PostingService.MatchWaraqName(People, "ابراهیم /ناشناس");
        Assert.Null(m.Found);
    }

    [Theory]
    [InlineData("هارون 1405/07/10")]
    [InlineData("حواله 12/3 هارون")]
    [InlineData("هارون")]
    public void BiSlash_HamanRaftareGhabli(string text)
    {
        var m = PostingService.MatchWaraqName(People, text);
        Assert.False(m.Slashed);
        Assert.Equal("هارون", m.Found!.Value.Person.Name);
    }

    [Fact]
    public void Slash_HavaleVaSookht_AzNamBarmidarad()
    {
        var m = PostingService.MatchWaraqName(People, "ابراهیم حواله 742 دیزل /هارون");
        Assert.Equal("742", m.Hawala.Num);
        Assert.Equal("ابراهیم", m.Display);
    }

    [Fact]
    public void Takmil_PasAzSlash()
    {
        var names = new[] { "هارون", "هارون دکان", "ابراهیم" };
        Assert.Equal("ابراهیم /هارون", Suggest.SlashHit("ابراهیم /ها", names));
        Assert.Equal("/ابراهیم", Suggest.SlashHit("/اب", names));
        Assert.Null(Suggest.SlashHit("ابراهیم /", names));
        Assert.Null(Suggest.SlashHit("1405/0", names));
        Assert.Null(Suggest.SlashHit("ابراهیم", names));
        Assert.Null(Suggest.SlashHit("/هارون", new[] { "هارون" }));   // کامل است
    }
}
