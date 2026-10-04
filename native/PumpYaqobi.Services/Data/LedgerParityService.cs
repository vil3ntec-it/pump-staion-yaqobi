using Microsoft.EntityFrameworkCore;
using PumpYaqobi.Application.Localization;
using PumpYaqobi.Application.Services;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;

namespace PumpYaqobi.Services.Data;

/// <summary>یک عددِ مشتقِ ذخیره‌شده که با حقیقت نمی‌خواند.</summary>
public sealed record ParityMismatch(long AccountId, long? RowId, string Field, decimal Stored, decimal Truth);

/// <summary>
/// ══ «سنجهٔ برابری» — عددهای مشتقِ ذخیره‌شده (شورا، الف۳ — ۱۴۰۵/۰۷/۱۹) ═════
///
/// ‎DebtRow.Bardagi/Albaqi‎ و چهار ‎Rasid*‎ِ حساب روی دیسک می‌نشینند و منبعِ حقیقت
/// <b>خودِ ردیف‌ها</b>ست. این سرویس هر دو را با همان دو قاعدهٔ همیشگی می‌سنجد:
/// ‎DebtCalculationService.NormalizeRow‎ (همان که صفحهٔ حساب سرِ باز شدن می‌زند) و
/// جمعِ ‎RasidLedger.FromRowsAsync‎. قاعدهٔ سوم ساخته نشد.
///
/// ⛔ فقط عددِ <b>مشتق</b> نوشته می‌شود — لیتر، فی، رسیدِ هر ردیف و نامِ کسی دست
/// نمی‌خورد. ⛔ چهار ‎Rasid*‎ فقط برای حسابِ مهاجرت‌کرده (‎ReceiptsMigrated‎)؛
/// پیش از آن، آن چهار عدد هنوز منبع‌اند. هر درست‌کردن یک ردیف در دفترِ ممیزی.
/// </summary>
public sealed class LedgerParityService
{
    private readonly PumpDbFactory _dbf;
    private readonly DebtCalculationService _calc;

    /// <summary>فقط آزمون: میانِ خواندن و نوشتن — جای «کاربر همین حالا رسیدی زد».</summary>
    public Func<Task>? BetweenReadAndWrite { get; set; }

    /// <summary>آخرین شکستِ دورِ روزانه (خالی = نه). ⛔ د۳: دیگر بی‌صدا بلعیده نمی‌شود.</summary>
    public string LastError { get; private set; } = "";

    /// <summary>مهرِ «امروز سنجیده شد» در جدولِ تنظیماتِ همان دفتر.</summary>
    public const string DayKey = "parity.day";

    public LedgerParityService(PumpDbFactory dbf, DebtCalculationService calc)
    {
        _dbf = dbf;
        _calc = calc;
    }

    /// <summary>
    /// همهٔ حساب‌ها را می‌سنجد؛ با <paramref name="fix"/> ناجورها را از روی ردیف‌ها
    /// درست می‌کند. خروجی: همهٔ ناجورهایی که دیده شد (پیش از درست کردن).
    /// </summary>
    public async Task<List<ParityMismatch>> CheckAsync(bool fix, CancellationToken ct = default)
    {
        var found = new List<ParityMismatch>();
        List<long> ids;
        await using (var db = _dbf.Create())
            ids = await db.DebtAccounts.AsNoTracking().Select(a => a.Id).ToListAsync(ct);

        foreach (var chunk in ids.Chunk(200))
        {
            //  ⚡ همیشه بی ردیابی: این کار روزی یک بار درست پس از ورود همهٔ ردیف‌ها را
            //  می‌خواند. با ردیابی (برای درست کردن) هر ردیف یک عکسِ «مقدارِ اصلی» هم
            //  می‌ساخت و زبالهٔ همان، مکثِ GCِ نخِ رابط را وسطِ نوشتنِ کاربر می‌انداخت
            //  (‎waraqtype‎ روی ویندوز ۴۴۷ms). فقط ردیفِ ناجور پایین‌تر نوشته می‌شود.
            List<DebtAccount> accounts; List<DebtRow> rows;
            await using (var rdb = _dbf.Create())
            {
                accounts = await rdb.DebtAccounts.AsNoTracking().Where(a => chunk.Contains(a.Id)).ToListAsync(ct);
                rows = await rdb.DebtRows.AsNoTracking()
                    .Where(r => (r.FuelAccountId != null && chunk.Contains(r.FuelAccountId.Value))
                             || (r.MoneyAccountId != null && chunk.Contains(r.MoneyAccountId.Value)))
                    .ToListAsync(ct);
            }
            var fixRows = new Dictionary<long, (DebtRow Row, HashSet<string> Fields)>();
            var fixAccts = new Dictionary<long, (DebtAccount Acct, HashSet<string> Fields)>();

            var byFuel = rows.Where(r => r.FuelAccountId != null).ToLookup(r => r.FuelAccountId!.Value);
            var byMoney = rows.Where(r => r.MoneyAccountId != null).ToLookup(r => r.MoneyAccountId!.Value);
            var fixedAccounts = new Dictionary<long, int>();

            foreach (var a in accounts)
            {
                var before = found.Count;
                foreach (var r in byFuel[a.Id].Concat(byMoney[a.Id]))
                {
                    var (b0, al0, m0) = (r.Bardagi, r.Albaqi, r.ByMoney);
                    if (!_calc.NormalizeRow(r)) continue;
                    if (b0 != r.Bardagi) found.Add(new(a.Id, r.Id, "Bardagi", b0, r.Bardagi));
                    if (al0 != r.Albaqi) found.Add(new(a.Id, r.Id, "Albaqi", al0, r.Albaqi));
                    if (m0 != r.ByMoney) found.Add(new(a.Id, r.Id, "ByMoney", m0 ? 1 : 0, r.ByMoney ? 1 : 0));
                    if (!fix) { (r.Bardagi, r.Albaqi, r.ByMoney) = (b0, al0, m0); continue; }
                    var changed = new HashSet<string>();
                    if (b0 != r.Bardagi) changed.Add(nameof(DebtRow.Bardagi));
                    if (al0 != r.Albaqi) changed.Add(nameof(DebtRow.Albaqi));
                    if (m0 != r.ByMoney) changed.Add(nameof(DebtRow.ByMoney));
                    if (changed.Count > 0) fixRows[r.Id] = (r, changed);
                }
                if (a.ReceiptsMigrated)
                {
                    decimal S(IEnumerable<DebtRow> rs, FuelType f, bool money) =>
                        rs.Where(r => r.Fuel == f).Sum(r => money ? r.Rasid : r.RasidFuel);
                    var truth = new (string F, decimal Stored, decimal Truth, Action<decimal> Set)[]
                    {
                        ("RasidFuelPetrol", a.RasidFuelPetrol, S(byFuel[a.Id], FuelType.Petrol, false), v => a.RasidFuelPetrol = v),
                        ("RasidFuelDiesel", a.RasidFuelDiesel, S(byFuel[a.Id], FuelType.Diesel, false), v => a.RasidFuelDiesel = v),
                        ("RasidMoneyPetrol", a.RasidMoneyPetrol, S(byMoney[a.Id], FuelType.Petrol, true), v => a.RasidMoneyPetrol = v),
                        ("RasidMoneyDiesel", a.RasidMoneyDiesel, S(byMoney[a.Id], FuelType.Diesel, true), v => a.RasidMoneyDiesel = v),
                    };
                    foreach (var t in truth)
                    {
                        if (t.Stored == t.Truth) continue;
                        found.Add(new(a.Id, null, t.F, t.Stored, t.Truth));
                        if (!fix) continue;
                        t.Set(t.Truth);
                        if (!fixAccts.TryGetValue(a.Id, out var fa)) fixAccts[a.Id] = fa = (a, new HashSet<string>());
                        fa.Fields.Add(t.F);
                    }
                }
                if (found.Count > before) fixedAccounts[a.Id] = found.Count - before;
            }

            if (fix && fixedAccounts.Count > 0)
            {
                if (BetweenReadAndWrite is { } hook) await hook();
                //  ⛔ شورا د۳ — حساب روی خواندنِ بی‌ردیابیِ بالا بود؛ کاربر می‌تواند همین
                //  میان یک رسید را عوض کرده باشد. پس درست کردن <b>درونِ یک تراکنش، از روی
                //  ردیف‌های همین لحظه</b> از نو حساب می‌شود و فقط آن‌چه هنوز ناجور است
                //  نوشته می‌شود — عددِ کهنه هرگز روی دیسک نمی‌نشیند. اگر کسی همان لحظه
                //  می‌نوشت (SQLite «مشغول»)، هیچ چیزی نوشته نمی‌شود و فردا دوباره.
                await using var db = _dbf.Create();
                await using var tx = await db.Database.BeginTransactionAsync(ct);
                var accIds = fixedAccounts.Keys.ToList();
                var freshRows = await db.DebtRows
                    .Where(r => (r.FuelAccountId != null && accIds.Contains(r.FuelAccountId.Value))
                             || (r.MoneyAccountId != null && accIds.Contains(r.MoneyAccountId.Value)))
                    .ToListAsync(ct);
                foreach (var r in freshRows)
                {
                    if (!fixRows.ContainsKey(r.Id)) continue;
                    _calc.NormalizeRow(r);           //  فقط عددِ مشتق؛ ردیابی فقط همان را می‌نویسد
                }
                var freshAccts = await db.DebtAccounts.Where(a => accIds.Contains(a.Id) && a.ReceiptsMigrated).ToListAsync(ct);
                var fFuel = freshRows.Where(r => r.FuelAccountId != null).ToLookup(r => r.FuelAccountId!.Value);
                var fMoney = freshRows.Where(r => r.MoneyAccountId != null).ToLookup(r => r.MoneyAccountId!.Value);
                foreach (var a in freshAccts)
                {
                    if (!fixAccts.TryGetValue(a.Id, out var want)) continue;
                    decimal S(IEnumerable<DebtRow> rs, FuelType f, bool money) =>
                        rs.Where(r => r.Fuel == f).Sum(r => money ? r.Rasid : r.RasidFuel);
                    if (want.Fields.Contains("RasidFuelPetrol")) a.RasidFuelPetrol = S(fFuel[a.Id], FuelType.Petrol, false);
                    if (want.Fields.Contains("RasidFuelDiesel")) a.RasidFuelDiesel = S(fFuel[a.Id], FuelType.Diesel, false);
                    if (want.Fields.Contains("RasidMoneyPetrol")) a.RasidMoneyPetrol = S(fMoney[a.Id], FuelType.Petrol, true);
                    if (want.Fields.Contains("RasidMoneyDiesel")) a.RasidMoneyDiesel = S(fMoney[a.Id], FuelType.Diesel, true);
                }
                foreach (var (id, n) in fixedAccounts)
                    db.Audit.Add(new AuditEntry
                    {
                        Actor = "system", Action = "parity.fix", Target = "DebtAccount:" + id,
                        DateShamsi = Shamsi.Today(),
                        Detail = $"{n} عددِ مشتقِ ذخیره‌شده از روی ردیف‌ها درست شد",
                    });
                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
            }
        }
        return found;
    }

    /// <summary>کارِ در جریان — سنجه‌ها منتظرش می‌مانند.</summary>
    /// <summary>
    /// ══ شورا، ج۶ — برابریِ کلیدهای تاریخ ═══════════════════════════════════════
    ///
    /// هر جدولی که <c>DateShamsi</c> و <c>DateKey</c> دارد: کلید (و ماه، اگر هست)
    /// باید همان باشد که از متنِ خودِ ردیف درمی‌آید (<see cref="PumpYaqobi.Domain.DateKeys"/>
    /// — همان قاعدهٔ ذخیره). ردیف‌های کهنه‌ای که راهی کلیدشان را ننوشته بود
    /// (<c>DateKey = 0</c>) یا ماهِ کهنه دارند، این‌جا درست می‌شوند.
    ///
    /// ⚠️ متنِ خالی یا ناخوانا دست نمی‌خورد، و <b>متنِ کاربر هرگز نوشته نمی‌شود</b> —
    /// فقط دو ستونِ مشتق. هر جدولِ درست‌شده یک ردیفِ ممیزی.
    /// </summary>
    public async Task<int> CheckDatesAsync(bool fix, CancellationToken ct = default)
    {
        var found = 0;
        await using var db = _dbf.Create();
        var tables = db.Model.GetEntityTypes()
            .Where(t => t.FindProperty("DateShamsi") is not null
                        && t.FindProperty("DateKey") is { } k && k.ClrType == typeof(int)
                        && t.GetTableName() is not null)
            .Select(t => (Table: t.GetTableName()!, HasMonth: t.FindProperty("MonthKey") is not null))
            .Distinct().ToList();

        var conn = db.Database.GetDbConnection();
        var opened = conn.State != System.Data.ConnectionState.Open;
        if (opened) await conn.OpenAsync(ct);
        try
        {
            foreach (var (table, hasMonth) in tables)
            {
                var bad = new List<(long Id, int Key, string Month)>();
                await using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = $"SELECT \"Id\", \"DateShamsi\", \"DateKey\"{(hasMonth ? ", \"MonthKey\"" : "")} FROM \"{table}\"";
                    await using var rd = await cmd.ExecuteReaderAsync(ct);
                    while (await rd.ReadAsync(ct))
                    {
                        var k = PumpYaqobi.Domain.DateKeys.Key(rd.IsDBNull(1) ? null : rd.GetString(1));
                        if (k == 0) continue;
                        var m = PumpYaqobi.Domain.DateKeys.Month(k);
                        var storedK = rd.IsDBNull(2) ? 0 : rd.GetInt32(2);
                        var storedM = hasMonth && !rd.IsDBNull(3) ? rd.GetString(3) : "";
                        if (storedK != k || (hasMonth && storedM != m)) bad.Add((rd.GetInt64(0), k, m));
                    }
                }
                found += bad.Count;
                if (!fix || bad.Count == 0) continue;

                await using var tx = await conn.BeginTransactionAsync(ct);
                foreach (var (id, k, m) in bad)
                {
                    await using var up = conn.CreateCommand();
                    up.Transaction = tx;
                    up.CommandText = $"UPDATE \"{table}\" SET \"DateKey\" = $k{(hasMonth ? ", \"MonthKey\" = $m" : "")} WHERE \"Id\" = $id";
                    var pk = up.CreateParameter(); pk.ParameterName = "$k"; pk.Value = k; up.Parameters.Add(pk);
                    var pi = up.CreateParameter(); pi.ParameterName = "$id"; pi.Value = id; up.Parameters.Add(pi);
                    if (hasMonth) { var pm = up.CreateParameter(); pm.ParameterName = "$m"; pm.Value = m; up.Parameters.Add(pm); }
                    await up.ExecuteNonQueryAsync(ct);
                }
                await tx.CommitAsync(ct);
                db.Audit.Add(new AuditEntry
                {
                    Actor = "system", Action = "parity.dates", Target = table,
                    DateShamsi = Shamsi.Today(),
                    Detail = $"{bad.Count} کلیدِ تاریخ از روی متنِ همان ردیف درست شد",
                });
            }
        }
        finally { if (opened) await conn.CloseAsync(); }
        if (fix && found > 0) await db.SaveChangesAsync(ct);
        return found;
    }

    public Task<int>? DailyTask { get; private set; }

    /// <summary>یک بار در روز برای هر دفتر، روی نخِ دیگر؛ دو صدا زدنِ هم‌زمان یکی می‌شوند.</summary>
    public Task<int> StartDaily()
    {
        lock (this)
        {
            if (DailyTask is { IsCompleted: false } running) return running;
            return DailyTask = Task.Run(() => DailyOnceAsync());
        }
    }

    public async Task<int> DailyOnceAsync(CancellationToken ct = default)
    {
        try
        {
            var today = Shamsi.Today();
            await using (var db = _dbf.Create())
                if (await db.Settings.AnyAsync(x => x.Key == DayKey && x.Value == today, ct)) return 0;
            var fixedCount = (await CheckAsync(fix: true, ct)).Count
                           + await CheckDatesAsync(fix: true, ct);      //  شورا ج۶
            await using (var db = _dbf.Create())
            {
                var s = await db.Settings.FirstOrDefaultAsync(x => x.Key == DayKey, ct);
                if (s is null) db.Settings.Add(new Setting { Key = DayKey, Value = today });
                else s.Value = today;
                await db.SaveChangesAsync(ct);
            }
            return fixedCount;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            //  ⛔ شورا د۳ — فردا یا بارِ بعد دوباره، ولی <b>بی‌صدا نه</b>: مهرِ روز زده نشده،
            //  دلیل در ‎LastError‎ و یک ردیفِ ممیزی. پیامِ خام (شاید مسیرِ فایل) فقط نوعش.
            LastError = ex.GetType().Name;
            try
            {
                await using var db = _dbf.Create();
                db.Audit.Add(new AuditEntry
                {
                    Actor = "system", Action = "parity.failed", Target = "ledger",
                    DateShamsi = Shamsi.Today(), Detail = "سنجهٔ برابری نشد: " + ex.GetType().Name,
                });
                await db.SaveChangesAsync(CancellationToken.None);
            }
            catch { /* دفترِ قفل‌شده — همان ‎LastError‎ کافی است */ }
            return -1;
        }
    }
}
