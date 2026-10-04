using Microsoft.Data.Sqlite;
using PumpYaqobi.Application.Localization;
using PumpYaqobi.Services.Data;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ شورا، بندِ ۱ — «عددِ ناخوانا بی‌صدا صفر نشود» (۱۴۰۵/۰۷/۱۹) ═════════════
///
/// رفتارِ خانهٔ جدول (سرخ، ذخیره‌نشده، Esc ⇐ عددِ قبلی) با پنجرهٔ واقعی در
/// ‎UiTests -- badnum‎ است؛ این‌جا قاعدهٔ خالص و «سلامتِ دفتر» روی SQLiteِ واقعی.
/// ⚠️ این کلاس ‎AppSettings‎ و ‎AppHost‎ را لمس نمی‌کند.
/// </summary>
public class BadNumberTests : IDisposable
{
    private readonly string _file = Path.Combine(Path.GetTempPath(), $"pump-badnum-{Guid.NewGuid():N}.db");

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try { if (File.Exists(_file)) File.Delete(_file); } catch { }
    }

    [Theory]
    [InlineData("12a")]
    [InlineData("abc")]
    [InlineData("1.2.3")]
    [InlineData("--5")]
    [InlineData("۵۰۰ تومان")]
    [InlineData("1-2")]
    [InlineData("12..5")]
    [InlineData("1٫2٫3")]
    [InlineData("12 34x")]
    [InlineData("$")]
    [InlineData("--")]
    public void Nakhana_Kharab_Ast_Va_TryNum_Na_Migooyad(string s)
    {
        Assert.False(Shamsi.IsReadable(s));
        Assert.False(Shamsi.TryNum(s, out _));
        //  ⚠️ ‎Num‎ همان رفتارِ پیشین را دارد (۰) — فقط دیگر هیچ نوشتهٔ کاربری بی پرسش به آن نمی‌رسد
        Assert.Equal(0m, Shamsi.Num(s));
    }

    [Theory]
    [InlineData("", 0)]
    [InlineData("   ", 0)]
    [InlineData("5,000", 5000)]
    [InlineData("۱۲٬۵۰۰", 12500)]
    [InlineData("‏7000", 7000)]
    [InlineData("12٫5", 12.5)]
    [InlineData("-25", -25)]
    [InlineData("۱۲۳", 123)]
    [InlineData("١٢٣", 123)]
    [InlineData(" 42 ", 42)]
    [InlineData("0", 0)]
    [InlineData("1,234,567", 1234567)]
    [InlineData("۰٫۵", 0.5)]
    [InlineData("‎-7", -7)]
    [InlineData("3.25", 3.25)]
    //  ⚠️ نمادِ علمی همیشه خوانده می‌شد (‎NumberStyles.Any‎) — همان می‌ماند، نه ناخوانا
    [InlineData("۱۲e۳", 12000)]
    public void Khana_Hamaan_Adad_Ast(string s, double want)
    {
        Assert.True(Shamsi.TryNum(s, out var d));
        Assert.Equal((decimal)want, d);
        Assert.Equal((decimal)want, Shamsi.Num(s));
    }

    [Fact]
    public void AvvalinKadreNakhana_BaBarchasbash()
    {
        Assert.Null(Shamsi.FirstUnreadable(("الف", "1,000"), ("ب", "")));
        Assert.Equal("ب", Shamsi.FirstUnreadable(("الف", "1,000"), ("ب", "12a"), ("پ", "x")));
    }

    /// <summary>
    /// «سلامتِ دفتر» هر خانهٔ متنیِ ناخوانا را می‌یابد — و ستون‌ها را از <b>مدلِ EF</b>
    /// می‌گیرد، پس مبلغِ گاوصندوق و ردیفِ قرض‌دار و پارچه همه هستند.
    /// </summary>
    [Fact]
    public async Task SalamatiyeDaftar_NakhanaRa_Peyda_Mikonad()
    {
        var dbf = new PumpDbFactory(_file);
        dbf.EnsureReady();
        var health = new LedgerHealthService(dbf);

        var cols = health.NumberColumns();
        Assert.Contains(cols, c => c.Table == "Expenses" && c.Column == "Amount");
        Assert.Contains(cols, c => c.Table == "DebtRows");
        Assert.True(cols.Count > 30, "ستون‌های عددی از مدل آمدند: " + cols.Count);

        Assert.Empty(await health.ScanAsync());

        using (var db = dbf.Create())
        {
            db.Add(new PumpYaqobi.Domain.Entities.Expense { DateShamsi = "1405/07/19", Title = "خوب", Amount = 1500m });
            db.Add(new PumpYaqobi.Domain.Entities.Expense { DateShamsi = "1405/07/19", Title = "خراب", Amount = 7m });
            db.SaveChanges();
        }
        using (var c = new SqliteConnection("Data Source=" + _file))
        {
            c.Open();
            using var cmd = c.CreateCommand();
            //  همان چیزی که یک op یا آوردنِ کهنه می‌توانست روی دیسک بگذارد
            cmd.CommandText = "UPDATE Expenses SET Amount = '12a' WHERE Title = 'خراب'";
            Assert.Equal(1, cmd.ExecuteNonQuery());
        }
        var bad = await health.ScanAsync();
        var one = Assert.Single(bad);
        Assert.Equal(("Expenses", "Amount", "12a"), (one.Table, one.Column, one.Raw));
    }
}
