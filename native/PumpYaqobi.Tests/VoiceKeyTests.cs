using PumpYaqobi.Services.Data;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ کلیدِ صدای هر حساب — بندِ ۲۴ ═════════════════════════════════════════════
/// ‎_vxKeyOf(pid, sid)‎ و ‎_vxLiveKey(key)‎.
///
/// این آزمون کوچک است ولی محافظِ چیزِ بزرگی است: کلیدها با همین شکل در
/// دیتابیسِ کاربر نشسته‌اند. اگر شکلشان عوض شود، همهٔ صداهایی که تا امروز
/// ثبت شده «شناخته‌نشده» می‌شوند و کاربر باید از نو همه را بگوید.
/// </summary>
public class VoiceKeyTests
{
    [Fact]
    public void TheKeyShapeIsExactlyTheOneTheWebVersionWrote()
    {
        Assert.Equal("p7", VoiceKeys.Of(7));
        Assert.Equal("p7|s12", VoiceKeys.Of(7, 12));
    }

    /// <summary>حسابِ اصلی زیرحساب ندارد؛ زیرحساب هر دو را دارد.</summary>
    [Fact]
    public void ParsingGivesBackTheDebtorAndTheSubAccount()
    {
        var main = VoiceKeys.Parse("p7");
        Assert.NotNull(main);
        Assert.Equal(7L, main!.Value.Debtor);
        Assert.Null(main.Value.Account);

        var sub = VoiceKeys.Parse("p7|s12");
        Assert.NotNull(sub);
        Assert.Equal(7L, sub!.Value.Debtor);
        Assert.Equal(12L, sub.Value.Account);
    }

    /// <summary>
    /// کلیدِ خراب باید ‎null‎ بدهد، نه یک حسابِ اشتباه — وگرنه صدا حسابِ
    /// دیگری را باز می‌کند و کاربر روی حسابِ نادرست ردیف می‌نویسد.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("x7")]
    [InlineData("p")]
    [InlineData("pابج")]
    [InlineData("p7|")]
    [InlineData("p7|s")]
    [InlineData("p7|12")]
    [InlineData("p7|sابج")]
    public void ABrokenKeyIsRefused(string key) => Assert.Null(VoiceKeys.Parse(key));

    /// <summary>هر کلیدی که ساخته شود باید همان‌طور هم خوانده شود.</summary>
    [Fact]
    public void EveryKeyRoundTrips()
    {
        foreach (var (debtor, account) in new (long, long?)[]
                 { (1, null), (1, 1), (999999, null), (999999, 424242) })
        {
            var key = account is null ? VoiceKeys.Of(debtor) : VoiceKeys.Of(debtor, account.Value);
            var back = VoiceKeys.Parse(key);
            Assert.NotNull(back);
            Assert.Equal(debtor, back!.Value.Debtor);
            Assert.Equal(account, back.Value.Account);
        }
    }
}
