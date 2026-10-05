using Microsoft.EntityFrameworkCore;
using PumpYaqobi.Application.Localization;
using PumpYaqobi.Application.Security;
using PumpYaqobi.Application.Services;
using PumpYaqobi.Domain.Entities;

namespace PumpYaqobi.Services.Data;

/// <summary>
/// ══ ورقِ روزانه ════════════════════════════════════════════════════════════
/// یک ورق برای هر روز. ورقِ تکراریِ همان تاریخ ساخته نمی‌شود — نسخهٔ وب هم
/// همین را می‌کرد («ورقِ فلان تاریخ از قبل وجود داشت، همان باز شد»).
/// </summary>
public sealed class WaraqDataService
{
    private readonly PumpDbFactory _dbf;
    private readonly PermissionService _perm;
    private readonly TrashService _trash;

    public WaraqDataService(PumpDbFactory dbf, PermissionService perm, TrashService trash)
    { _dbf = dbf; _perm = perm; _trash = trash; }

    private static IQueryable<WaraqEntry> Full(Persistence.PumpDbContext db) =>
        // ⚠️ ‎AsSplitQuery‎: بی آن، هر پمپ در هر تراکنشِ همان شیفت ضرب می‌شود.
        db.WaraqEntries.AsSplitQuery()
          .Include(w => w.Shifts).ThenInclude(s => s.Pumps)
          .Include(w => w.Shifts).ThenInclude(s => s.Transactions);

    public async Task<List<WaraqEntry>> ListAsync(string? monthKey, CancellationToken ct = default)
    {
        _perm.Require(Permission.ViewData);
        await using var db = _dbf.Create();
        var q = Full(db).AsNoTracking();
        if (!string.IsNullOrWhiteSpace(monthKey))
        {
            var from = Shamsi.Key(monthKey + "/01");
            q = q.Where(w => w.DateKey >= from && w.DateKey <= from + 99);
        }
        return await q.OrderByDescending(w => w.DateKey).ToListAsync(ct);
    }

    /// <summary>
    /// ══ فروشِ ورق‌ها برای «مفاد و ضرر» (۱۴۰۵/۰۷/۲۰) ════════════════════════
    /// صاحب ریپو: «نه از پارچه حساب بشه؛ همون قد لیتری که فروخته شده، پولِ
    /// همون لیتر بیاد توی مفاد و ضرر — دیزل و پطرول هر دو جمع بشن — و یک بخش
    /// باشه که این‌ها از کجا اومدن.» پس هر شیفتِ هر ورق یک سطر: لیتر و پولِ
    /// پطرول و دیزل، از همان قرائتِ پایه‌ها (‎(ختم−شروع) × فی‎ — همان
    /// ‎WaraqService.ShiftTotals‎؛ پارچه‌ها هم از راهِ پایهٔ ‎SrcKey‎دار در ورق‌اند).
    /// ⛔ فقط چهار ستون خوانده می‌شود، نه ورقِ کامل. ‎null‎ یعنی همهٔ زمان‌ها.
    /// </summary>
    public async Task<List<WaraqSaleLine>> SalesLinesAsync((int Lo, int Hi)? keys, CancellationToken ct = default)
    {
        _perm.Require(Permission.ViewData);
        await using var db = _dbf.Create();
        var q = db.WaraqPumps.AsNoTracking().Where(p => p.Shift != null && p.Shift.Waraq != null);
        if (keys is { } k) q = q.Where(p => p.Shift!.Waraq!.DateKey >= k.Lo && p.Shift!.Waraq!.DateKey <= k.Hi);
        var rows = await q.Select(p => new
        {
            p.Shift!.WaraqId, p.Shift!.Waraq!.DateShamsi, p.Shift!.Waraq!.DateKey, p.Shift!.Kind,
            p.Fuel, p.Start, p.End, p.PricePerLiter,
        }).ToListAsync(ct);
        return rows.GroupBy(r => (r.WaraqId, r.Kind))
                   .Select(g =>
                   {
                       decimal pl = 0, pm = 0, dl = 0, dm = 0;
                       foreach (var r in g)
                       {
                           var l = Math.Max(0m, r.End - r.Start);
                           if (r.Fuel == Domain.Enums.FuelType.Diesel) { dl += l; dm += l * r.PricePerLiter; }
                           else { pl += l; pm += l * r.PricePerLiter; }
                       }
                       var f = g.First();
                       return new WaraqSaleLine(f.DateShamsi ?? "", f.DateKey, f.Kind, pl, pm, dl, dm);
                   })
                   .Where(x => x.PetrolLiters != 0 || x.DieselLiters != 0)
                   .OrderBy(x => x.DateKey).ThenBy(x => x.Kind)
                   .ToList();
    }

    /// <summary>شورا ث۱/ج۵: «نخستین ردیفِ نام‌دارِ ورق نوشته شده؟» — فقط یک EXISTS.</summary>
    public async Task<bool> AnyNamedTxnAsync(CancellationToken ct = default)
    {
        await using var db = _dbf.Create();
        return await db.WaraqTransactions.AnyAsync(t => t.Name != null && t.Name != "", ct);
    }

    /// <summary>
    /// تاریخِ همهٔ ورق‌ها — فقط همان یک ستون. ⛔ برای «کدام روز ورق دارد»
    /// همهٔ ورق‌ها با شیفت‌ها، پایه‌ها و تراکنش‌هایشان خوانده نشود (ده سال یعنی
    /// ده‌ها هزار ردیف روی نخِ رابط، فقط برای یک فهرستِ تاریخ).
    /// </summary>
    public async Task<List<string>> DatesAsync(CancellationToken ct = default)
    {
        _perm.Require(Permission.ViewData);
        await using var db = _dbf.Create();
        return await db.WaraqEntries.AsNoTracking().Select(w => w.DateShamsi ?? "").ToListAsync(ct);
    }

    public async Task<List<string>> MonthsAsync(CancellationToken ct = default)
    {
        _perm.Require(Permission.ViewData);
        await using var db = _dbf.Create();
        var keys = await db.WaraqEntries.AsNoTracking().Where(w => w.DateKey > 0)
                           .Select(w => w.DateKey).Distinct().ToListAsync(ct);
        return keys.Select(k => $"{k / 10000:0000}/{k / 100 % 100:00}")
                   .Distinct().OrderByDescending(x => x).ToList();
    }

    public async Task<WaraqEntry?> LoadAsync(long id, CancellationToken ct = default)
    {
        _perm.Require(Permission.ViewData);
        await using var db = _dbf.Create();
        return await Full(db).AsNoTracking().FirstOrDefaultAsync(w => w.Id == id, ct);
    }

    /// <summary>ورقِ همان تاریخ اگر باشد همان برمی‌گردد، وگرنه تازه ساخته می‌شود.</summary>
    public async Task<WaraqEntry> OpenOrCreateAsync(string dateShamsi, string? station,
                                                    CancellationToken ct = default)
    {
        _perm.Require(Permission.EditData);
        var key = Shamsi.Key(dateShamsi);
        await using var db = _dbf.Create();
        var exist = await Full(db).FirstOrDefaultAsync(w => w.DateKey == key, ct);
        if (exist is not null) return exist;

        var w = new WaraqEntry { DateShamsi = dateShamsi, DateKey = key, Station = station };
        w.Shifts.Add(NewShift(ShiftKind.Day));
        w.Shifts.Add(NewShift(ShiftKind.Night));
        db.WaraqEntries.Add(w);
        await db.SaveChangesAsync(ct);
        return w;
    }

    /// <summary>
    /// شیفتِ خالی با چهارده ردیفِ آماده — همان ‎initWaraqTransactions()‎ که
    /// چهارده ردیفِ خالی می‌ساخت تا کارمند فقط پر کند.
    /// </summary>
    private static WaraqShift NewShift(ShiftKind kind)
    {
        var s = new WaraqShift { Kind = kind };
        for (var i = 0; i < 14; i++)
            s.Transactions.Add(new WaraqTransaction { SortIndex = i });
        return s;
    }

    public async Task SavePumpAsync(WaraqPump p, CancellationToken ct = default)
    {
        _perm.Require(Permission.EditData);
        await using var db = _dbf.Create();
        if (p.Id == 0) db.WaraqPumps.Add(p);
        else { db.WaraqPumps.Attach(p); db.Entry(p).State = EntityState.Modified; }
        //  ⛔ پایه‌ای که از پارچه آمده، همان شیفتِ پارچه را هم به‌روز می‌کند —
        //  در همان ذخیره، تا گزارشِ پارچه و تاریخچه هرگز عددِ دیگری نگویند (۱۴۰۵/۰۷/۱۷).
        await ShiftWaraqSyncService.PushPumpToShiftAsync(db, p, ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task SaveTxnAsync(WaraqTransaction t, CancellationToken ct = default)
    {
        _perm.Require(Permission.EditData);
        await using var db = _dbf.Create();
        if (t.Id == 0) db.WaraqTransactions.Add(t);
        else { db.WaraqTransactions.Attach(t); db.Entry(t).State = EntityState.Modified; }
        await db.SaveChangesAsync(ct);
    }

    public async Task SaveShiftAsync(WaraqShift s, CancellationToken ct = default)
    {
        _perm.Require(Permission.EditData);
        await using var db = _dbf.Create();
        var row = await db.WaraqShifts.FirstOrDefaultAsync(x => x.Id == s.Id, ct);
        if (row is null) return;
        row.WorkerName = s.WorkerName;
        row.FabricDebt = s.FabricDebt;
        row.FabricAvailable = s.FabricAvailable;
        row.AvailableFromShift = s.AvailableFromShift;
        row.PricePerLiter = s.PricePerLiter;
        row.PricePerLiterDiesel = s.PricePerLiterDiesel;
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// برداشتنِ یک تراکنشِ ورق. تا امروز فقط «افزودن» بود چون دکمهٔ حذفی روی
    /// جدولِ تراکنش‌ها نبود؛ میانبرِ ‎Shift+عدد‎ همان کاری را می‌کند که
    /// <c>removeWaraqRow</c> در نسخهٔ وب می‌کرد، پس این‌جا هم لازم شد.
    /// </summary>
    public async Task DeleteTxnAsync(long id, CancellationToken ct = default)
    {
        _perm.Require(Permission.DeleteData);
        await using var db = _dbf.Create();
        var t = await db.WaraqTransactions.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (t is null) return;
        db.WaraqTransactions.Remove(t);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeletePumpAsync(long id, CancellationToken ct = default)
    {
        _perm.Require(Permission.DeleteData);
        await using var db = _dbf.Create();
        var p = await db.WaraqPumps.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (p is null) return;
        db.WaraqPumps.Remove(p);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// ورق که حذف شود، هر چیزی که خودش ساخته بود هم می‌رود:
    ///   • ردیف‌های «ماندگی»ِ فروش در گاوصندوق (‎removeWaraqSalesFromSafe‎ی سایت)
    ///   • ردیف‌های قرض که از تراکنش‌های همین ورق در حساب‌ها نشسته‌اند
    ///   • مصرف‌هایی که از همین ورق آمده‌اند
    ///
    /// ⚠️ بی این، پاک کردنِ یک ورق قرضِ طرف را در حسابش جا می‌گذاشت — قرضی که
    /// دیگر هیچ ورقی پشتش نبود و هیچ‌جا هم نمی‌شد پیدایش کرد.
    /// </summary>
    public async Task DeleteAsync(long id, CancellationToken ct = default)
    {
        _perm.Require(Permission.DeleteData);
        await using var db = _dbf.Create();
        var w = await Full(db).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (w is null) return;
        await _trash.RememberAsync(db, "waraq", "ورقِ " + w.DateShamsi, w, ct);

        var salesKeys = new[] { SrcKeys.WaraqSales(w, ShiftKind.Day), SrcKeys.WaraqSales(w, ShiftKind.Night) };
        db.SafeEntries.RemoveRange(
            await db.SafeEntries.Where(e => e.SrcKey != null && salesKeys.Contains(e.SrcKey))
                                .ToListAsync(ct));

        var prefix = WaraqPostingService.WaraqKey(w) + "|";
        db.DebtRows.RemoveRange(
            await db.DebtRows.Where(r => r.SrcKey != null && r.SrcKey.StartsWith(prefix))
                             .ToListAsync(ct));
        db.Expenses.RemoveRange(
            await db.Expenses.Where(e => e.SrcKey != null && e.SrcKey.StartsWith(prefix))
                             .ToListAsync(ct));

        db.WaraqEntries.Remove(w);
        await db.SaveChangesAsync(ct);
    }
}
