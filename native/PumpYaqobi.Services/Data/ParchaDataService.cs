using Microsoft.EntityFrameworkCore;
using PumpYaqobi.Application.Localization;
using PumpYaqobi.Application.Security;
using PumpYaqobi.Application.Services;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;

namespace PumpYaqobi.Services.Data;

/// <summary>
/// ورودیِ ‎saveShift‎ — دقیقاً همان کادرهایی که آن تابع از صفحه می‌خواند.
/// </summary>
public sealed record ShiftSaveRequest(
    FuelType Fuel, ShiftKind Kind, string DateShamsi,
    string Name, int PumpNum, decimal Start, decimal End, decimal Price,
    decimal Debt, decimal BoxProfitPer, decimal AvailMan, string Note,
    decimal BuyPerLiter, bool ForceNew);

/// <summary>نتیجهٔ ذخیره — پیامِ خطا همان پیامِ نسخهٔ وب است.</summary>
public sealed record ShiftSaveResult(
    bool Ok, string? Error, ParchaReport? Report, ShiftData? Shift, string? SrcKey);

/// <summary>
/// ══ پارچه‌ها ═══════════════════════════════════════════════════════════════
/// در نسخهٔ وب پارچهٔ پطرول در <c>DB.reports</c> و پارچهٔ دیزل در
/// <c>DB.shifts</c> بود — دو ساختارِ یکسان با دو نام. اینجا یک جدول است و
/// <see cref="ParchaReport.Fuel"/> جدایشان می‌کند؛ هیچ رفتاری عوض نشده.
/// </summary>
public sealed class ParchaDataService
{
    private readonly PumpDbFactory _dbf;
    private readonly PermissionService _perm;
    private readonly TrashService _trash;
    private readonly ParchaService _calc;
    private readonly ShiftWaraqSyncService? _waraqSync;
    private readonly Func<string> _today;

    /// <param name="today">
    /// «امروز» از بیرون داده می‌شود تا آزمونِ برابری بتواند همان روزی را
    /// بازپخش کند که دادهٔ طلایی در آن گرفته شده. در برنامهٔ واقعی همان
    /// <see cref="Shamsi.Today"/> است.
    /// </param>
    public ParchaDataService(PumpDbFactory dbf, PermissionService perm, TrashService trash,
                             ParchaService calc, ShiftWaraqSyncService? waraqSync = null,
                             Func<string>? today = null)
    { _dbf = dbf; _perm = perm; _trash = trash; _calc = calc; _waraqSync = waraqSync;
      _today = today ?? Shamsi.Today; }

    /// <summary>
    /// ‎getCurrentReport(fuel)‎ — **آخرین** پارچهٔ همان سوخت، نه پارچهٔ امروز.
    /// </summary>
    public async Task<ParchaReport?> CurrentAsync(FuelType fuel, CancellationToken ct = default)
    {
        await using var db = _dbf.Create();
        var list = await db.Reports.AsNoTracking()
                           .Include(r => r.DayShift).Include(r => r.NightShift)
                           .Where(r => r.Fuel == fuel)
                           .OrderBy(r => r.Id).ToListAsync(ct);
        return list.LastOrDefault();
    }

    /// <summary>
    /// ══ ‎saveShift(type, fuel)‎ — کلِ جریان، یک‌جا ═══════════════════════════
    ///
    ///  ۱. بررسی: نام لازم است؛ ختمِ پایه نباید کمتر از شروع باشد.
    ///  ۲. ‎shDate‎ = تاریخِ دستیِ کادرِ پارچه، وگرنه امروز.
    ///  ۳. پارچهٔ جاری = آخرین پارچهٔ همان سوخت (‎ensureReport‎ اگر نبود).
    ///  ۴. پارچهٔ **تازه** فقط در دو حالت باز می‌شود:
    ///       • پرچمِ «پارچهٔ جدید» بالا باشد (‎_forceNewParcha/_forceNewDiesel‎)
    ///       • تاریخِ پارچهٔ جاری با ‎shDate‎ فرق کند
    ///     در غیرِ این دو، همان پارچه به‌روز می‌شود — تا «ذخیره»ی دوباره
    ///     (ویرایش) ردیفِ دوتایی نسازد.
    ///  ۵. عددها با ‎CalcShift‎ حساب و روی خودِ رکورد نوشته می‌شوند، تا گزارشِ
    ///     کهنه همان عددِ آن روز را نشان دهد حتی اگر فیِ خرید بعداً عوض شود.
    ///  ۶. ‎syncShiftToWaraq‎ با ‎srcKey‎ی که به همین رکورد اشاره می‌کند.
    ///
    /// ⚠️ پرچمِ «پارچهٔ جدید» ورودیِ همین تابع است و برای هر (سوخت، شیفت) جدا
    /// نگه داشته می‌شود؛ پس «پارچهٔ جدیدِ روز» هرگز پارچهٔ شب یا سوختِ دیگر را
    /// دست نمی‌زند.
    /// </summary>
    public async Task<ShiftSaveResult> SaveShiftFlowAsync(ShiftSaveRequest req,
                                                          CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(req.Name))
            return new ShiftSaveResult(false, "نام کارمند را وارد کنید", null, null, null);
        if (req.End < req.Start)
            return new ShiftSaveResult(false, "ختم پایه نمی‌تواند کمتر از شروع باشد", null, null, null);

        _perm.Require(Permission.EditData);

        var shDate = (req.DateShamsi ?? "").Trim();
        if (shDate.Length == 0) shDate = _today();

        var rep = req.Fuel == FuelType.Diesel
            ? await DieselTargetAsync(req, shDate, ct)
            : await PetrolTargetAsync(req, shDate, ct);

        var n = _calc.CalcShift(req.Start, req.End, req.Price, req.Debt,
                                req.BuyPerLiter, req.BoxProfitPer);

        var existing = req.Kind == ShiftKind.Day ? rep.DayShift : rep.NightShift;
        var shift = existing ?? new ShiftData();
        shift.Name = req.Name.Trim();
        shift.PumpNum = req.PumpNum;
        shift.Start = req.Start;
        shift.End = req.End;
        shift.Price = req.Price;
        // ‎saveShift‎ عددِ **کادر** را می‌خواند، یعنی گردشدهٔ toFixed(1)
        shift.ProfitPer = n.ProfitPerBox;
        shift.BuyPerLiter = n.BuyPerLiter;
        shift.Sale = n.Sale;
        shift.Money = n.Money;
        shift.Debt = req.Debt;
        shift.Available = n.Available;
        shift.AvailMan = req.AvailMan;
        shift.Profit = n.Sale * n.ProfitPerBox;
        shift.Note = (req.Note ?? "").Trim();
        shift.SavedAt = shDate;

        rep.DateShamsi = shDate;
        await SaveShiftAsync(rep, req.Kind, shift, ct);
        if (req.Kind == ShiftKind.Day) rep.DayShift = shift; else rep.NightShift = shift;

        var srcKey = ShiftWaraqSyncService.SrcKeyOf(req.Fuel, rep.Id, req.Kind);
        if (_waraqSync is not null)
            await _waraqSync.SyncSavedShiftAsync(req.Kind, shift, req.Fuel, srcKey, ct);

        return new ShiftSaveResult(true, null, rep, shift, srcKey);
    }

    /// <summary>
    /// مسیرِ پطرول — گزارش‌محور (‎DB.reports‎):
    /// <code>
    /// let rep = ensureReport('petrol');            // اگر هیچ نبود، با تاریخِ *امروز*
    /// if (_forceNewParcha[type] || (rep.date &amp;&amp; rep.date !== shDate)) { …پارچهٔ تازه… }
    /// </code>
    ///
    /// ⚠️ ریزه‌کاری‌ای که به چشم نمی‌آید ولی در عدد پیداست: ‎ensureReport‎
    /// پارچه را با تاریخِ **امروز** می‌سازد، نه با تاریخی که کاربر نوشته. پس
    /// اگر اولین ذخیره با تاریخی جز امروز باشد، همان خطِ بعدی هم می‌گیرد و
    /// پارچهٔ **دوم** ساخته می‌شود. دادهٔ گرفته‌شده از خودِ نسخهٔ وب همین را
    /// نشان می‌دهد: پس از اولین ذخیره، شمارِ پارچه‌ها ۲ است نه ۱.
    /// </summary>
    private async Task<ParchaReport> PetrolTargetAsync(ShiftSaveRequest req, string shDate,
                                                       CancellationToken ct)
    {
        var rep = await CurrentAsync(req.Fuel, ct)
                  ?? await AddAsync(req.Fuel, _today(), ct);

        var repDate = (rep.DateShamsi ?? "").Trim();
        if (req.ForceNew || (repDate.Length > 0 && repDate != shDate))
            rep = await AddAsync(req.Fuel, shDate, ct);
        return rep;
    }

    /// <summary>
    /// مسیرِ دیزل — ردیف‌محور (‎DB.shifts‎):
    /// <code>
    /// const sameDay = DB.shifts.filter(s =&gt; s.fuel==='diesel' &amp;&amp; s.type===type &amp;&amp; s.date===shDate);
    /// let rec = (!_forceNewDiesel[type] &amp;&amp; sameDay.length) ? sameDay[sameDay.length-1] : null;
    /// </code>
    ///
    /// یعنی پارچهٔ دیزل با **همان تاریخ و همان شیفت** به‌روز می‌شود، هر جای
    /// فهرست که باشد — نه فقط اگر آخرین پارچه باشد. با پرچمِ «پارچهٔ جدید»
    /// ردیفِ تازه ثبت می‌شود، پس چند پارچهٔ دیزل در یک روز شدنی است.
    ///
    /// این‌جا هر پارچهٔ دیزل یک <see cref="ParchaReport"/> با یک خانهٔ پر است،
    /// پس «ردیفِ همان روز و همان شیفت» یعنی پارچه‌ای که خانهٔ همان شیفتش پر
    /// باشد.
    /// </summary>
    private async Task<ParchaReport> DieselTargetAsync(ShiftSaveRequest req, string shDate,
                                                       CancellationToken ct)
    {
        if (!req.ForceNew)
        {
            await using var db = _dbf.Create();
            var key = Shamsi.Key(shDate);
            var sameDay = await db.Reports.AsNoTracking()
                                  .Include(r => r.DayShift).Include(r => r.NightShift)
                                  .Where(r => r.Fuel == FuelType.Diesel && r.DateKey == key)
                                  .OrderBy(r => r.Id).ToListAsync(ct);
            var hit = sameDay.LastOrDefault(r =>
                (req.Kind == ShiftKind.Day ? r.DayShift : r.NightShift) is not null);
            if (hit is not null) return hit;
        }
        return await AddAsync(FuelType.Diesel, shDate, ct);
    }

    public async Task<List<ParchaReport>> ListAsync(FuelType fuel, string? monthKey,
                                                    CancellationToken ct = default)
    {
        _perm.Require(Permission.ViewData);
        await using var db = _dbf.Create();
        var q = db.Reports.AsNoTracking()
            .Include(r => r.DayShift).Include(r => r.NightShift)
            .Where(r => r.Fuel == fuel);
        if (!string.IsNullOrWhiteSpace(monthKey))
        {
            var from = Shamsi.Key(monthKey + "/01");
            var to = from + 99;
            q = q.Where(r => r.DateKey >= from && r.DateKey <= to);
        }
        return await q.OrderBy(r => r.DateKey).ThenBy(r => r.Id).ToListAsync(ct);
    }

    public async Task<List<string>> MonthsAsync(FuelType fuel, CancellationToken ct = default)
    {
        _perm.Require(Permission.ViewData);
        await using var db = _dbf.Create();
        var keys = await db.Reports.AsNoTracking().Where(r => r.Fuel == fuel && r.DateKey > 0)
                           .Select(r => r.DateKey).Distinct().ToListAsync(ct);
        return keys.Select(k => $"{k / 10000:0000}/{k / 100 % 100:00}")
                   .Distinct().OrderByDescending(x => x).ToList();
    }

    /// <summary>شمارهٔ پارچهٔ بعدی برای همین سوخت — مثلِ ‎fuelReports.length + 1‎.</summary>
    public async Task<int> NextReportNumberAsync(FuelType fuel, CancellationToken ct = default)
    {
        await using var db = _dbf.Create();
        return await db.Reports.CountAsync(r => r.Fuel == fuel, ct) + 1;
    }

    public async Task<ParchaReport> AddAsync(FuelType fuel, string dateShamsi, CancellationToken ct = default)
    {
        _perm.Require(Permission.EditData);
        await using var db = _dbf.Create();
        var r = new ParchaReport
        {
            Fuel = fuel,
            DateShamsi = dateShamsi,
            DateKey = Shamsi.Key(dateShamsi),
            ReportNum = await db.Reports.CountAsync(x => x.Fuel == fuel, ct) + 1,
        };
        db.Reports.Add(r);
        await db.SaveChangesAsync(ct);
        return r;
    }

    /// <summary>
    /// ذخیرهٔ یک شیفت. عددهای حساب‌شده پیش از نوشتن روی خودِ شیفت می‌نشینند،
    /// همان‌طور که ‎saveShift‎ می‌کرد — تا گزارشِ کهنه همان عددِ آن روز را نشان دهد.
    /// </summary>
    public async Task SaveShiftAsync(ParchaReport report, ShiftKind kind, ShiftData shift,
                                     CancellationToken ct = default)
    {
        _perm.Require(Permission.EditData);
        _calc.Apply(shift);

        await using var db = _dbf.Create();
        if (shift.Id == 0) db.ShiftDataSet.Add(shift);
        else { db.ShiftDataSet.Attach(shift); db.Entry(shift).State = EntityState.Modified; }
        await db.SaveChangesAsync(ct);

        var rep = await db.Reports.FirstOrDefaultAsync(x => x.Id == report.Id, ct);
        if (rep is null) return;
        if (kind == ShiftKind.Day) rep.DayShiftId = shift.Id; else rep.NightShiftId = shift.Id;
        rep.DateShamsi = report.DateShamsi;
        rep.DateKey = Shamsi.Key(report.DateShamsi);
        await db.SaveChangesAsync(ct);
    }

    public async Task SaveReportAsync(ParchaReport r, CancellationToken ct = default)
    {
        _perm.Require(Permission.EditData);
        await using var db = _dbf.Create();
        var rep = await db.Reports.FirstOrDefaultAsync(x => x.Id == r.Id, ct);
        if (rep is null) return;
        rep.DateShamsi = r.DateShamsi;
        rep.DateKey = Shamsi.Key(r.DateShamsi);
        rep.ReportNum = r.ReportNum;
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(long id, CancellationToken ct = default)
    {
        _perm.Require(Permission.DeleteData);
        await using var db = _dbf.Create();
        var r = await db.Reports.Include(x => x.DayShift).Include(x => x.NightShift)
                        .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (r is null) return;
        await _trash.RememberAsync(db, "parcha", $"پارچهٔ {r.ReportNum} — {r.DateShamsi}", r, ct);
        db.Reports.Remove(r);
        await db.SaveChangesAsync(ct);
    }
}
