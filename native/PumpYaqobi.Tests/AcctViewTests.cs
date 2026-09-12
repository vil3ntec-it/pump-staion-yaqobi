using PumpYaqobi.App.Services;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ کیو‌آری که همهٔ حساب را با خودش می‌برد ══════════════════════════════════
///
/// خواستهٔ صریحِ صاحب ریپو: «طرف که اسکن می‌کند سایت باز بشود و تمامِ اطلاعاتش
/// تو گوشی بیاید؛ یک سایتِ جدید برای دیدن، آن سایتِ قبلی را ول کن.»
///
/// پس داده **داخلِ خودِ کد** است: نه رمزی در نشانی می‌رود، نه گوشی به سرور
/// وصل می‌شود، و کدِ چاپ‌شده همان عددهای همان روز را نگه می‌دارد.
/// </summary>
public class AcctViewTests
{
    private static readonly string Root =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    private static AcctSnapshot Sample(int rows)
    {
        var s = new AcctSnapshot
        {
            Kind = "قرض‌دار — واحد تیل",
            Name = "هارون",
            Unit = "لیتر",
            Date = "1405/06/22",
            Summary = { new[] { "الباقی", "5,100" } },
            Head = { "تاریخ", "نام", "مقدار" },
        };
        for (var i = 0; i < rows; i++)
            s.Rows.Add(new[] { "1405/06/" + (i % 30 + 1).ToString("00"), "ردیفِ " + i, "50" });
        return s;
    }

    /// <summary>آن‌چه فشرده شد، همان چیزی است که باز می‌شود.</summary>
    [Fact]
    public void WhatGoesInComesOut()
    {
        var snap = Sample(12);
        var back = AcctView.Decode(AcctView.Encode(snap));

        Assert.NotNull(back);
        Assert.Equal(snap.Name, back!.Name);
        Assert.Equal(snap.Unit, back.Unit);
        Assert.Equal(snap.Rows.Count, back.Rows.Count);
        Assert.Equal(snap.Rows[0], back.Rows[0]);
        Assert.Equal(snap.Summary[0], back.Summary[0]);
    }

    /// <summary>
    /// نشانی هیچ‌وقت از سقف رد نمی‌شود — وگرنه کیو‌آر آن‌قدر چگال می‌شود که
    /// دوربینِ گوشی از روی کاغذ نمی‌خواندش.
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(40)]
    [InlineData(400)]
    public void TheLinkNeverGrowsPastWhatAPhoneCanRead(int rows)
    {
        var url = AcctView.Url("https://example.invalid/view/", Sample(rows));
        Assert.True(url.Length <= AcctView.MaxUrl, "درازای نشانی: " + url.Length);
        Assert.Contains("#d=", url);
    }

    /// <summary>
    /// حسابِ بزرگ که جا نشود، ردیف‌های **قدیمی** کنار می‌روند و همان‌جا هم
    /// نوشته می‌شود — نه این‌که کد ساخته نشود یا تازه‌ها بیفتند.
    /// </summary>
    [Fact]
    public void ABigAccountKeepsTheNewestRowsAndSaysSo()
    {
        var url = AcctView.Url("https://example.invalid/view/", Sample(400));
        var back = AcctView.Decode(url[(url.IndexOf("#d=", StringComparison.Ordinal) + 3)..]);

        Assert.NotNull(back);
        Assert.True(back!.Rows.Count is > 0 and < 400);
        Assert.Contains("ردیفِ آخر", back.Note);
        // تازه‌ترین ردیف حتماً هست
        Assert.Equal("ردیفِ 399", back.Rows[^1][1]);
    }

    /// <summary>
    /// نشانی نه رمز دارد و نه سرور — همان چیزی که خواسته شد. شناسهٔ حساب فقط
    /// به‌عنوانِ نشانه می‌ماند تا اسکنرِ خودِ برنامه هم همان حساب را باز کند.
    /// </summary>
    [Fact]
    public void TheLinkCarriesNoServerAndNoToken()
    {
        var url = AcctView.Url("https://example.invalid/view/", Sample(3),
                               AcctLink.Build(7));
        Assert.DoesNotContain("token", url);
        Assert.DoesNotContain("server", url);
        Assert.Contains("p=", url);

        // ⚠️ نشانه وسطِ نشانی است (دادهٔ حساب پشتش می‌آید) — خواننده باید
        // همان‌جا بایستد، نه این‌که تا ته بخواند.
        var back = AcctLink.Parse(Uri.UnescapeDataString(url));
        Assert.NotNull(back);
        Assert.Equal(7, back!.Value.PersonId);

        // و نشانیِ پیش‌فرض هم دامنهٔ خودِ پمپ است، نه نامِ مخزن
        Assert.StartsWith("https://", AcctView.DefaultBase);
        Assert.DoesNotContain("github", AcctView.DefaultBase);
    }

    /// <summary>و صفحهٔ تازه واقعاً در مخزن هست و همان تکه را می‌خواند.</summary>
    [Fact]
    public void TheViewerPageExistsAndReadsTheSameFragment()
    {
        var page = Path.GetFullPath(Path.Combine(Root, "..", "view", "index.html"));
        Assert.True(File.Exists(page), "صفحهٔ ‎view/index.html‎ نیست");

        var html = File.ReadAllText(page);
        Assert.Contains("d=([^&]+)", html);          // همان تکهٔ ‎#d=…‎
        Assert.Contains("deflate-raw", html);        // همان فشرده‌سازیِ برنامه
        Assert.DoesNotContain("token", html);        // نه رمزی می‌خواهد
        Assert.DoesNotContain("WebSocket", html);    // نه به سروری وصل می‌شود
    }
}
