using Microsoft.Data.Sqlite;
using PumpYaqobi.Services.Data;
using PumpYaqobi.Services.Security;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ «نصبِ تازه باید خالی باشد» — با شمردنِ خودِ ردیف‌ها ══════════════════
///
/// خواستهٔ صریحِ صاحب ریپو (۱۴۰۵/۰۷/۱۱): «برای هر کاربر جدید برنامه خالی از
/// اطلاعات دیده بشه… کسی اگه برنامه رو تازه نصب کنه، برنامه باید بدون
/// اطلاعات باشه.»
///
/// ⚠️ <b>چرا این آزمون با آن‌چه <see cref="NoPasswordTests"/> دارد یکی
/// نیست</b>: آن یکی می‌گوید دفتر <b>کجاست</b> (پوشهٔ کاربر، نه کنارِ
/// فایل‌های نصب). این یکی می‌گوید دفترِ تازه <b>چه چیزی دارد</b> — و جوابش
/// باید «هیچ» باشد.
///
/// ⛔ و عمداً فهرستِ جدول‌ها را <b>دستی</b> نمی‌نویسد: از خودِ
/// <c>sqlite_master</c> می‌خواند. پس جدولِ تازه‌ای که فردا ساخته شود و
/// کسی بی‌خبر در آن ردیفِ «نمونه» بگذارد، همین‌جا قرمز می‌شود — چیزی که
/// یک فهرستِ دستی هیچ‌وقت نمی‌گرفت.
///
/// ⚠️ این کلاس <b>هیچ‌وقت</b> <c>AppSettings.DirOverride</c> یا
/// <c>AppHost.Start</c> را لمس نمی‌کند، پس نشانِ
/// <c>[Collection(AppHostCollection.Name)]</c> لازم ندارد.
/// </summary>
public class FreshInstallEmptyTests : IDisposable
{
    /// <summary>
    /// سه جدولی که ردیف داشتنشان <b>داده</b> نیست، بلکه حالِ خودِ نصب است:
    /// مهرِ اسکیما، دفترِ حالِ همگام‌سازی، و دفترِ کارهای همین دستگاه.
    /// ⛔ هیچ جدولِ دیگری حق ندارد به این فهرست اضافه شود؛ هر کدام که
    /// اضافه شود یعنی نصبِ تازه دیگر خالی نیست.
    /// </summary>
    private static readonly HashSet<string> Housekeeping = new(StringComparer.Ordinal)
    {
        "Settings", "SyncState", "Audit",
    };

    private readonly string _file = Path.Combine(Path.GetTempPath(), $"pump-fresh-{Guid.NewGuid():N}.db");
    private readonly PumpDbFactory _dbf;

    public FreshInstallEmptyTests()
    {
        _dbf = new PumpDbFactory(_file);
        _dbf.EnsureReady();
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try { if (File.Exists(_file)) File.Delete(_file); } catch { }
    }

    /// <summary>
    /// نصبِ تازه: <b>هیچ</b> جدولِ داده‌ای حتی یک ردیف ندارد.
    /// </summary>
    [Fact]
    public void NasbeTaze_HichRadifeDadei_Nadarad()
    {
        var full = new List<string>();

        using var cn = new SqliteConnection($"Data Source={_file}");
        cn.Open();

        var tables = new List<string>();
        using (var list = cn.CreateCommand())
        {
            list.CommandText =
                "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%'";
            using var r = list.ExecuteReader();
            while (r.Read()) tables.Add(r.GetString(0));
        }

        //  اگر این صفر باشد یعنی اسکیما ساخته نشده و آزمون **توخالی** است،
        //  نه سبز. («سنجه‌ای که جدولش خالی است سبز نیست، خراب است.»)
        Assert.True(tables.Count > 20, $"اسکیما ساخته نشد؟ فقط {tables.Count} جدول.");

        foreach (var t in tables)
        {
            if (Housekeeping.Contains(t)) continue;
            using var cmd = cn.CreateCommand();
            cmd.CommandText = $"SELECT COUNT(*) FROM \"{t}\"";
            var n = Convert.ToInt64(cmd.ExecuteScalar());
            if (n > 0) full.Add($"{t}={n}");
        }

        Assert.True(full.Count == 0,
            "نصبِ تازه باید خالی باشد، ولی این جدول‌ها ردیف دارند: " + string.Join(" · ", full));
    }

    /// <summary>
    /// ⛔ و کاربری هم ساخته نشده تا خودِ برنامه بالا نیاید — یعنی هیچ رمزی
    /// هم در کار نیست. (<see cref="AuthService.OpenWithoutPassword"/> تنها
    /// جایی است که کاربرِ مدیر را می‌سازد، و آن هم <b>بی رمز</b>.)
    /// </summary>
    [Fact]
    public void NasbeTaze_NeKarbari_NeRamzi()
    {
        var auth = new AuthService(_dbf, new PumpYaqobi.Application.Security.UserSession());

        Assert.True(auth.NeedsFirstRun());
        Assert.False(auth.HasPassword());

        //  و پس از باز شدنِ بی‌رمز هم باز رمزی نیست — فقط یک کاربرِ بی‌هش
        Assert.True(auth.OpenWithoutPassword());
        Assert.False(auth.HasPassword());
    }
}
