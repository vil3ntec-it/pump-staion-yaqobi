using Microsoft.EntityFrameworkCore;
using PumpYaqobi.Application.Localization;
using PumpYaqobi.Application.Security;
using PumpYaqobi.Application.Services;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;

namespace PumpYaqobi.Services.Data;

/// <summary>
/// ══ پارچه ← ورق ← گاوصندوق ═════════════════════════════════════════════════
/// رونوشتِ سه تابعِ به‌هم‌بسته در نسخهٔ وب:
///
///   • <c>syncShiftToWaraq(shift, shiftData, fuel, srcKey)</c>
///       ذخیرهٔ هر پارچه یک ردیفِ «پایه» در ورقِ همان تاریخ می‌سازد.
///   • <c>liveWaraqSync(p)</c>
///       همان کار، ولی حین تایپ و با کلیدِ موقتِ «…-live-…».
///   • <c>syncWaraqSalesToSafe(w)</c>
///       فروشِ هر شیفتِ ورق یک ردیفِ «ماندگی» در گاوصندوق می‌شود.
///
/// ⚠️ همهٔ نگه‌داری‌ها به <c>SrcKey</c> بند است. بدونِ آن، پارچهٔ دومِ یک شیفت
/// ردیفِ پارچهٔ اول را بازنویسی می‌کرد و ذخیرهٔ دوباره ردیفِ تکراری می‌ساخت.
/// نسخهٔ وب دقیقاً به همین دلیل این کلید را داشت.
/// </summary>
public sealed class ShiftWaraqSyncService
{
    private readonly PumpDbFactory _dbf;
    private readonly PermissionService _perm;
    private readonly WaraqService _waraq;
    private readonly SettingsService _settings;

    public ShiftWaraqSyncService(PumpDbFactory dbf, PermissionService perm,
                                 WaraqService waraq, SettingsService settings)
    { _dbf = dbf; _perm = perm; _waraq = waraq; _settings = settings; }

    /// <summary>‎'p-&lt;id&gt;-day'‎ · ‎'d-&lt;id&gt;-night'‎ — همان صورتِ نسخهٔ وب.</summary>
    public static string SrcKeyOf(FuelType fuel, long recordId, ShiftKind kind) =>
        (fuel == FuelType.Diesel ? "d-" : "p-") + recordId + "-" + KindWord(kind);

    /// <summary>‎'p-live-day'‎ — ردیفِ موقتِ پیش از ذخیره.</summary>
    public static string LiveKeyOf(FuelType fuel, ShiftKind kind) =>
        (fuel == FuelType.Diesel ? "d" : "p") + "-live-" + KindWord(kind);

    private static string KindWord(ShiftKind k) => k == ShiftKind.Night ? "night" : "day";

    /// <summary>
    /// ‎syncShiftToWaraq‎ — پس از ذخیرهٔ پارچه. ورقِ آن تاریخ اگر نباشد ساخته
    /// می‌شود (برخلافِ مسیرِ زنده که ورقِ نساخته را رها می‌کند).
    /// </summary>
    public Task SyncSavedShiftAsync(ShiftKind kind, ShiftData shift, FuelType fuel,
                                    string srcKey, CancellationToken ct = default) =>
        SyncAsync(kind, shift, fuel, srcKey, createWaraq: true, addAvailable: true, ct);

    /// <summary>
    /// ‎liveWaraqSync‎ — حین تایپ. اگر ورقی برای آن تاریخ نباشد هیچ نمی‌کند
    /// (‎if (!w) return;‎ در نسخهٔ وب) تا تایپِ نیمه‌کاره ورقِ خالی نسازد.
    /// </summary>
    public Task SyncLiveShiftAsync(ShiftKind kind, ShiftData shift, FuelType fuel,
                                   string srcKey, CancellationToken ct = default) =>
        SyncAsync(kind, shift, fuel, srcKey, createWaraq: false, addAvailable: false, ct);

    private async Task SyncAsync(ShiftKind kind, ShiftData shift, FuelType fuel, string srcKey,
                                 bool createWaraq, bool addAvailable, CancellationToken ct)
    {
        if (shift is null) return;
        _perm.Require(Permission.EditData);

        var date = (shift.SavedAt ?? "").Trim();
        if (date.Length == 0) return;
        var key = Shamsi.Key(date);

        await using var db = _dbf.Create();
        var w = await db.WaraqEntries
                        .Include(x => x.Shifts).ThenInclude(s => s.Pumps)
                        .Include(x => x.Shifts).ThenInclude(s => s.Transactions)
                        .FirstOrDefaultAsync(x => x.DateKey == key, ct);

        if (w is null)
        {
            if (!createWaraq) return;
            w = new WaraqEntry
            {
                DateShamsi = date,
                DateKey = key,
                Station = _settings.GetString(SettingsService.StationName),
            };
            w.Shifts.Add(NewShift(ShiftKind.Day));
            w.Shifts.Add(NewShift(ShiftKind.Night));
            db.WaraqEntries.Add(w);
        }

        var sd = w.Shifts.FirstOrDefault(s => s.Kind == kind);
        if (sd is null) { sd = NewShift(kind); w.Shifts.Add(sd); }

        // نامِ کارمندِ ورق فقط وقتی پر می‌شود که خالی باشد — نامِ نوشته‌شده
        // به دستِ کاربر با هر ذخیرهٔ پارچه بازنویسی نمی‌شود.
        if (!string.IsNullOrWhiteSpace(shift.Name) && string.IsNullOrWhiteSpace(sd.WorkerName))
            sd.WorkerName = shift.Name;

        // ردیفِ موقتِ همین شیفت/سوخت برداشته شود تا با ردیفِ ذخیره‌شده دوتا نشود
        var liveKey = LiveKeyOf(fuel, kind);
        if (srcKey != liveKey)
            foreach (var dead in sd.Pumps.Where(e => e.SrcKey == liveKey).ToList())
            { sd.Pumps.Remove(dead); db.WaraqPumps.Remove(dead); }

        // پایه‌های خالیِ پیش‌فرض (بی‌منبع و بی‌داده) پاک شوند تا ردیفِ اول خالی نماند
        foreach (var dead in sd.Pumps.Where(IsBlank).ToList())
        { sd.Pumps.Remove(dead); db.WaraqPumps.Remove(dead); }

        var entry = sd.Pumps.FirstOrDefault(e => e.SrcKey == srcKey);
        if (entry is null)
        {
            entry = new WaraqPump
            {
                Shift = sd,
                SortIndex = sd.Pumps.Count,
                Fuel = fuel,
                SrcKey = srcKey,
            };
            sd.Pumps.Add(entry);
            db.WaraqPumps.Add(entry);
        }

        entry.Num = shift.PumpNum;
        entry.Worker = shift.Name ?? "";
        entry.Start = shift.Start;
        entry.End = shift.End;
        entry.PricePerLiter = shift.Price;
        entry.Debt = shift.Debt;

        if (shift.Price > 0m)
        {
            if (fuel == FuelType.Diesel) sd.PricePerLiterDiesel = shift.Price;
            else sd.PricePerLiter = shift.Price;
        }

        // «پول موجودیِ پارچه» فقط اگر دستی نوشته شده باشد
        if (shift.AvailMan > 0m) sd.FabricAvailable = shift.AvailMan;

        // ذخیره جمع می‌زند، مسیرِ زنده جای‌گزین می‌کند — همان تفاوتِ نسخهٔ وب
        if (shift.Available > 0m)
            sd.AvailableFromShift = addAvailable
                ? sd.AvailableFromShift + shift.Available
                : shift.Available;

        // فیِ تازه رسید → مبلغِ خودکارِ ردیف‌های قرض/مصرفِ همین ورق بازحساب شود
        _waraq.NormalizeTxns(sd);

        await db.SaveChangesAsync(ct);
        await SyncSalesToSafeAsync(db, w, ct);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>پایهٔ «خالیِ پیش‌فرض» — نه منبعی دارد نه داده‌ای.</summary>
    private static bool IsBlank(WaraqPump e) =>
        string.IsNullOrEmpty(e.SrcKey)
        && string.IsNullOrWhiteSpace(e.Worker)
        && e.Start == 0m && e.End == 0m && e.PricePerLiter == 0m
        && string.IsNullOrWhiteSpace(e.Note);

    /// <summary>
    /// ‎syncWaraqSalesToSafe(w)‎ — فروشِ هر شیفت یک ردیفِ «ماندگی» در گاوصندوق.
    /// فروشِ صفر یعنی ردیف باید برداشته شود، نه ردیفِ صفر بماند.
    /// </summary>
    public async Task SyncSalesToSafeAsync(Persistence.PumpDbContext db, WaraqEntry w,
                                           CancellationToken ct = default)
    {
        if (w is null) return;
        foreach (var kind in new[] { ShiftKind.Day, ShiftKind.Night })
        {
            var sd = w.Shifts.FirstOrDefault(s => s.Kind == kind);
            if (sd is null) continue;

            var sales = Math.Round(_waraq.ShiftTotals(sd).Sales, 0, MidpointRounding.AwayFromZero);
            var srcKey = "wq-sales-" + w.Id + "-" + KindWord(kind);
            var row = await db.SafeEntries.FirstOrDefaultAsync(e => e.SrcKey == srcKey, ct);

            if (sales <= 0m)
            {
                if (row is not null) db.SafeEntries.Remove(row);
                continue;
            }

            var title = "📝 فروش ورق " + (w.DateShamsi ?? "") + " — "
                      + (kind == ShiftKind.Night ? "شب" : "روز")
                      + (string.IsNullOrWhiteSpace(w.Station) ? "" : " (" + w.Station + ")");

            if (row is null)
            {
                // مثلِ نسخهٔ وب: اول در اولین خانهٔ خالیِ گاوصندوق می‌نشیند
                row = await db.SafeEntries.FirstOrDefaultAsync(
                    e => e.SrcKey == null && (e.Title == null || e.Title == "")
                         && e.Amount == 0m && (e.Note == null || e.Note == ""), ct);
                if (row is null) { row = new SafeEntry(); db.SafeEntries.Add(row); }
                row.SrcKey = srcKey;
                row.Kind = SafeEntryKind.Mandagi;
            }

            row.Title = title;
            row.Amount = sales;
            row.DateShamsi = w.DateShamsi ?? "";
            row.DateKey = Shamsi.Key(w.DateShamsi ?? "");
            row.MonthKey = Shamsi.MonthKey(w.DateShamsi ?? "");
        }
    }

    private static WaraqShift NewShift(ShiftKind kind)
    {
        var s = new WaraqShift { Kind = kind };
        for (var i = 0; i < 14; i++) s.Transactions.Add(new WaraqTransaction { SortIndex = i });
        return s;
    }
}
