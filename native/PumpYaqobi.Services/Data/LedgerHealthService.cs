using System.Globalization;
using Microsoft.EntityFrameworkCore;

namespace PumpYaqobi.Services.Data;

/// <summary>یک خانهٔ عددیِ ناخوانا روی دیسک.</summary>
public sealed record BadNumberCell(string Table, string Column, long Id, string Raw);

/// <summary>
/// ══ «🩺 سلامتِ دفتر» — عددهای ناخوانای روی دیسک (شورا، بندِ ۱ — ۱۴۰۵/۰۷/۱۹) ══
///
/// مبلغ‌ها در SQLite متن ذخیره می‌شوند (EF ‎decimal‎ را ‎TEXT‎ می‌نویسد). متنی که
/// عدد نیست — از نسخه‌های پیش از این بند، از آوردنِ دادهٔ نسخهٔ وب، یا از یک op
/// همگام‌سازیِ خراب — هنگامِ خواندن یا خطا می‌دهد یا (در جمع‌های متنی) ۰ شمرده
/// می‌شود. این‌جا فقط <b>گزارش</b> می‌شود؛ هیچ چیزی نوشته یا «درست» نمی‌شود —
/// حدس زدنِ عددِ درست کارِ آدم است، نه برنامه.
///
/// ⚠️ ستون‌ها از <b>خودِ مدلِ EF</b> می‌آیند (هر خاصیتِ ‎decimal‎/‎decimal?‎)، نه از
/// فهرستِ دستی — ستونِ پولیِ تازه خودبه‌خود سنجیده می‌شود.
/// </summary>
public sealed class LedgerHealthService
{
    private readonly PumpDbFactory _dbf;
    public LedgerHealthService(PumpDbFactory dbf) => _dbf = dbf;

    /// <summary>ستون‌های عددیِ مدل: (جدول، ستون).</summary>
    public IReadOnlyList<(string Table, string Column)> NumberColumns()
    {
        using var db = _dbf.Create();
        var list = new List<(string, string)>();
        foreach (var et in db.Model.GetEntityTypes())
        {
            var table = et.GetTableName();
            if (table is null || et.FindPrimaryKey()?.Properties is not [{ Name: "Id" }]) continue;
            foreach (var p in et.GetProperties())
            {
                var t = Nullable.GetUnderlyingType(p.ClrType) ?? p.ClrType;
                if (t == typeof(decimal)) list.Add((table, p.GetColumnName()));
            }
        }
        return list;
    }

    /// <summary>هر خانهٔ عددیِ ناخوانا در کلِ دفتر — فقط خواندن.</summary>
    public async Task<List<BadNumberCell>> ScanAsync(CancellationToken ct = default)
    {
        var bad = new List<BadNumberCell>();
        using var db = _dbf.Create();
        var conn = db.Database.GetDbConnection();
        await db.Database.OpenConnectionAsync(ct);
        try
        {
            foreach (var (table, col) in NumberColumns())
            {
                using var cmd = conn.CreateCommand();
                //  فقط خانه‌های متنی: عدد/تهی که SQLite خودش عدد دیده، خوانا است
                cmd.CommandText = $"SELECT \"Id\", \"{col}\" FROM \"{table}\" WHERE typeof(\"{col}\") = 'text'";
                using var r = await cmd.ExecuteReaderAsync(ct);
                while (await r.ReadAsync(ct))
                {
                    var raw = r.IsDBNull(1) ? "" : r.GetString(1);
                    if (!decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out _))
                        bad.Add(new BadNumberCell(table, col, r.GetInt64(0), raw));
                }
            }
        }
        finally { await db.Database.CloseConnectionAsync(); }
        return bad;
    }
}
