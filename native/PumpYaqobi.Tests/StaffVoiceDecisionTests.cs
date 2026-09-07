using PumpYaqobi.Application.Services;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// تصمیمِ ‎_staffApplyVoice‎ — کِی باز کن، کِی بپرس، کِی بگو نشناختم.
///
/// ⚠️ نکتهٔ ظریفی که نسخهٔ وب دارد و به‌آسانی از دست می‌رود: «رقیب» فقط
/// حسابِ **شخصِ دیگری** است. حسابِ اصلی و فرعیِ یک نفر نامشان یکی است، پس اگر
/// رقیبِ هم شمرده شوند برنامه هیچ‌وقت مطمئن نمی‌شود و همیشه می‌پرسد.
/// </summary>
public class StaffVoiceDecisionTests
{
    private static StaffNameMatch.Candidate C(string key, string person, string label) =>
        new(key, person, label, 0);

    [Fact]
    public void AClearWinner_IsOpenedStraightAway()
    {
        var d = StaffNameMatch.Decide(
            new[] { C("p1", "p1", "احمد شاه"), C("p2", "p2", "نصیر احمد") },
            new[] { "احمد شاه" });

        Assert.Equal(StaffNameMatch.VoiceOutcome.Open, d.Outcome);
        Assert.Equal("p1", d.Best!.Value.Key);
    }

    [Fact]
    public void TwoPeopleTooClose_AreAskedAbout()
    {
        var d = StaffNameMatch.Decide(
            new[] { C("p1", "p1", "احمد ولی"), C("p2", "p2", "احمد شاه") },
            new[] { "احمد" });

        Assert.Equal(StaffNameMatch.VoiceOutcome.Ask, d.Outcome);
        Assert.True(d.Choices.Count >= 2);
    }

    [Fact]
    public void TheSubAccountsOfOnePerson_AreNotRivals()
    {
        var d = StaffNameMatch.Decide(
            new[]
            {
                C("p1", "p1", "احمد شاه"),
                C("p1|s1", "p1", "احمد شاه دیزل"),
                C("p9", "p9", "حاجی عبدالله"),
            },
            new[] { "احمد شاه" });

        Assert.Equal(StaffNameMatch.VoiceOutcome.Open, d.Outcome);
        Assert.Equal("p1", d.Best!.Value.Key);
    }

    [Fact]
    public void SomethingCompletelyDifferent_IsNotFound()
    {
        var d = StaffNameMatch.Decide(
            new[] { C("p1", "p1", "احمد شاه") },
            new[] { "زنجبیل" });

        Assert.Equal(StaffNameMatch.VoiceOutcome.NotFound, d.Outcome);
    }

    [Fact]
    public void NoAccountsAtAll_IsNotFound()
    {
        var d = StaffNameMatch.Decide(Array.Empty<StaffNameMatch.Candidate>(), new[] { "احمد" });
        Assert.Equal(StaffNameMatch.VoiceOutcome.NotFound, d.Outcome);
        Assert.Null(d.Best);
    }

    /// <summary>«كريم» با «ک»ِ عربی هم باید همان «کریم» باشد.</summary>
    [Fact]
    public void ArabicLetters_DoNotHideAName()
    {
        var d = StaffNameMatch.Decide(new[] { C("p3", "p3", "کریم") }, new[] { "كريم" });
        Assert.Equal(StaffNameMatch.VoiceOutcome.Open, d.Outcome);
    }
}
