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
    /// هر چهار کلیدی که ردیفِ یک پارچه در ورق می‌تواند داشته باشد (دو تیل × دو شیفت).
    /// ⛔ از ۱۴۰۵/۰۷/۱۶: حذفِ پارچه پایه‌هایش را با همین کلیدها از ورق برمی‌دارد و
    /// بازگردانی از سطلِ زباله با همین‌ها برشان می‌گرداند.
    /// </summary>
    public static string[] ReportKeys(long reportId) => new[]
    {
        SrcKeyOf(FuelType.Petrol, reportId, ShiftKind.Day),
        SrcKeyOf(FuelType.Petrol, reportId, ShiftKind.Night),
        SrcKeyOf(FuelType.Diesel, reportId, ShiftKind.Day),
        SrcKeyOf(FuelType.Diesel, reportId, ShiftKind.Night),
    };

    /// <summary>
    /// کلیدهای همان پارچه و همان شیفت با هر دو تیل — ‎"p-12-day"‎ ⇒ ‎p-12-day، d-12-day‎.
    /// کلیدِ غیرِ پارچه (زنده، دستی) ⇒ خالی.
    /// </summary>
    public static string[] SiblingKeys(string? srcKey)
    {
        var m = System.Text.RegularExpressions.Regex.Match(srcKey ?? "", @"^[pd]-(\d+)-(day|night)$");
        if (!m.Success || !long.TryParse(m.Groups[1].Value, out var id)) return Array.Empty<string>();
        var kind = m.Groups[2].Value == "night" ? ShiftKind.Night : ShiftKind.Day;
        return new[] { SrcKeyOf(FuelType.Petrol, id, kind), SrcKeyOf(FuelType.Diesel, id, kind) };
    }

    // ══════════════════════════════════════════════════════════════════════
    //  ══ ویرایشِ دوطرفه: ورق ⇄ پارچه (۱۴۰۵/۰۷/۱۷) ══════════════════════════
    // ══════════════════════════════════════════════════════════════════════
    //
    //  گزارشِ صاحب ریپو: «قرض رو اشتباه نوشتم و خاستم تغییر بدم؛ از ورق تغییر
    //  می‌خوره اما توی گزارشِ پارچه‌ها و تاریخچه‌ها تغییر نمی‌کنه… و ویرایشِ
    //  تاریخچه هم بزار.»
    //
    //  پیش از این راه یک‌طرفه بود: پارچه ⇐ ورق. پایهٔ ورقی که از پارچه آمده
    //  (‎SrcKey‎ی «p-12-day») حالا شیفتِ همان پارچه را هم به‌روز می‌کند، و
    //  ویرایشِ همان شیفت از تاریخچه پایهٔ ورقش را. تاریخچه‌ها هر بار از خودِ
    //  دفتر ساخته می‌شوند، پس همان لحظه همان عدد را نشان می‌دهند.
    //
    //  ⛔ **هیچ فرمولِ تازه‌ای نیست**: فروش، پول، فایده و «رسیده» از همان
    //  ‎ParchaService.CalcShift‎ی ذخیرهٔ پارچه، با همان فیِ خرید و همان فایدهٔ
    //  فی‌لیترِ ذخیره‌شده در خودِ شیفت. عددِ کهنه همان می‌ماند که آن روز بود.
    //
    //  ⛔ **فقط شش خانه** دو طرف را به هم می‌بندد: نامِ کارمند، شمارهٔ پایه،
    //  شروع، ختم، فی و قرض. تاریخ، تیل، یادداشت و «پول موجودِ» پارچه دست
    //  نمی‌خورند — تیل و تاریخ کلیدِ خودِ پیوندند.

    /// <summary>
    /// ‎"p-12-day"‎ ⇒ پارچهٔ ۱۲، شیفتِ روز. کلیدِ زنده («p-live-day») و ردیفِ دستی ⇒ نه.
    /// </summary>
    public static bool TryParseKey(string? srcKey, out long reportId, out ShiftKind kind)
    {
        reportId = 0; kind = ShiftKind.Day;
        var m = System.Text.RegularExpressions.Regex.Match(srcKey ?? "", @"^[pd]-(\d+)-(day|night)$");
        if (!m.Success || !long.TryParse(m.Groups[1].Value, out reportId) || reportId <= 0) return false;
        kind = m.Groups[2].Value == "night" ? ShiftKind.Night : ShiftKind.Day;
        return true;
    }

    /// <summary>
    /// شش خانهٔ پیوند را روی شیفتِ پارچه می‌نشاند و عددهای حساب‌شده را با همان
    /// ‎CalcShift‎ی ذخیره از نو می‌سازد. برمی‌گرداند که چیزی واقعاً عوض شد یا نه.
    /// ⚠️ نامِ خالی نامِ کارمندِ پارچه را پاک نمی‌کند.
    /// </summary>
    public static bool ApplyToShift(ShiftData s, string? name, int pumpNum, decimal start,
                                    decimal end, decimal price, decimal debt)
    {
        if (s is null) return false;
        var nm = (name ?? "").Trim();
        var newName = nm.Length > 0 ? nm : s.Name;
        if (s.Name == newName && s.PumpNum == pumpNum && s.Start == start && s.End == end
            && s.Price == price && s.Debt == debt)
            return false;

        s.Name = newName;
        s.PumpNum = pumpNum;
        s.Start = start;
        s.End = end;
        s.Price = price;
        s.Debt = debt;

        var n = new ParchaService().CalcShift(start, end, price, debt, s.BuyPerLiter, s.ProfitPer);
        s.ProfitPer = n.ProfitPerBox;
        s.Sale = n.Sale;
        s.Money = n.Money;
        s.Available = n.Available;
        s.Profit = n.Sale * n.ProfitPerBox;
        return true;
    }

    /// <summary>
    /// پایهٔ ورق ⇒ شیفتِ پارچه‌اش، در همان ‎db‎ (ذخیره با صداکننده).
    /// پایهٔ دستی، کلیدِ زنده و پارچهٔ پاک‌شده ⇒ هیچ کاری.
    /// </summary>
    public static async Task<bool> PushPumpToShiftAsync(Persistence.PumpDbContext db, WaraqPump p,
                                                         CancellationToken ct = default)
    {
        if (p is null || !TryParseKey(p.SrcKey, out var id, out var kind)) return false;
        var rep = await db.Reports.Include(r => r.DayShift).Include(r => r.NightShift)
                          .FirstOrDefaultAsync(r => r.Id == id, ct);
        var s = rep is null ? null : kind == ShiftKind.Night ? rep.NightShift : rep.DayShift;
        if (s is null) return false;
        return ApplyToShift(s, p.Worker, p.Num, p.Start, p.End, p.PricePerLiter, p.Debt);
    }

    /// <summary>
    /// شیفتِ پارچه ⇒ پایهٔ ورقش (اگر در ورق هست)، و بعد گاوصندوقِ همان ورق.
    /// ⚠️ ورقِ تازه نمی‌سازد و «پول موجود»ِ ورق را دست نمی‌زند — این ویرایش
    /// است، نه ذخیرهٔ تازهٔ پارچه.
    /// </summary>
    /// <returns>شناسهٔ ورق‌هایی که پایه‌شان عوض شد — صداکننده حساب‌هایشان را تازه می‌کند.</returns>
    public async Task<List<long>> PushShiftToWaraqAsync(Persistence.PumpDbContext db, string srcKey,
                                                        ShiftData s, CancellationToken ct = default)
    {
        var pumps = await db.WaraqPumps.Include(p => p.Shift)
                            .Where(p => p.SrcKey == srcKey).ToListAsync(ct);
        if (pumps.Count == 0) return new List<long>();
        var waraqIds = new List<long>();
        foreach (var p in pumps)
        {
            p.Num = s.PumpNum;
            if (!string.IsNullOrWhiteSpace(s.Name)) p.Worker = s.Name;
            p.Start = s.Start;
            p.End = s.End;
            p.PricePerLiter = s.Price;
            p.Debt = s.Debt;
            if (p.Shift is { } sh) waraqIds.Add(sh.WaraqId);
        }
        await db.SaveChangesAsync(ct);
        await ResyncSalesAsync(waraqIds, ct);
        return waraqIds.Distinct().ToList();
    }

    /// <summary>
    /// ردیفِ «فروش ورق» در گاوصندوق را برای ورق‌های داده‌شده از نو می‌سازد — پس از
    /// آن‌که پایه‌ای بی‌آن‌که از خودِ ورق بگذرد رفت یا برگشت (حذف یا بازگردانیِ پارچه).
    /// ورقِ نبوده یا حذف‌شده نادیده گرفته می‌شود. هیچ‌وقت استثنا بیرون نمی‌دهد.
    /// </summary>
    public async Task ResyncSalesAsync(IEnumerable<long> waraqIds, CancellationToken ct = default)
    {
        var ids = waraqIds.Where(i => i > 0).Distinct().ToList();
        if (ids.Count == 0) return;
        try
        {
            await using var db = _dbf.Create();
            var list = await db.WaraqEntries.AsSplitQuery()
                               .Include(x => x.Shifts).ThenInclude(s => s.Pumps)
                               .Include(x => x.Shifts).ThenInclude(s => s.Transactions)
                               .Where(x => ids.Contains(x.Id))
                               .ToListAsync(ct);
            foreach (var w in list) await SyncSalesToSafeAsync(db, w, ct);
            await db.SaveChangesAsync(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch { /* گاوصندوق با ذخیرهٔ بعدیِ همان ورق درست می‌شود */ }
    }

    /// <summary>
    /// ‎syncShiftToWaraq‎ — پس از ذخیرهٔ پارچه. ورقِ آن تاریخ اگر نباشد ساخته
    /// می‌شود (برخلافِ مسیرِ زنده که ورقِ نساخته را رها می‌کند).
    /// </summary>
    /// <param name="lowBase">
    /// «شروعِ این پایه از پایهٔ قبلی کمتر بود و کاربر هم دکمهٔ ‹دیدم› را نزد» —
    /// فقط روی ردیفِ ورق می‌نشیند تا سرخ دیده شود. هیچ عددی را عوض نمی‌کند.
    /// </param>
    public Task SyncSavedShiftAsync(ShiftKind kind, ShiftData shift, FuelType fuel,
                                    string srcKey, bool lowBase = false,
                                    CancellationToken ct = default) =>
        SyncAsync(kind, shift, fuel, srcKey, createWaraq: true, addAvailable: true,
                  lowBase: lowBase, ct);

    /// <summary>
    /// ‎liveWaraqSync‎ — حین تایپ. اگر ورقی برای آن تاریخ نباشد هیچ نمی‌کند
    /// (‎if (!w) return;‎ در نسخهٔ وب) تا تایپِ نیمه‌کاره ورقِ خالی نسازد.
    /// </summary>
    public Task SyncLiveShiftAsync(ShiftKind kind, ShiftData shift, FuelType fuel,
                                   string srcKey, CancellationToken ct = default) =>
        SyncAsync(kind, shift, fuel, srcKey, createWaraq: false, addAvailable: false,
                  lowBase: null, ct);

    /// <param name="lowBase">
    /// ‎null‎ یعنی «دست نزن» — مسیرِ زنده نشانِ ذخیرهٔ قبلی را پاک نمی‌کند.
    /// </param>
    private async Task SyncAsync(ShiftKind kind, ShiftData shift, FuelType fuel, string srcKey,
                                 bool createWaraq, bool addAvailable, bool? lowBase,
                                 CancellationToken ct)
    {
        if (shift is null) return;
        _perm.Require(Permission.EditData);

        var date = (shift.SavedAt ?? "").Trim();
        if (date.Length == 0) return;
        var key = Shamsi.Key(date);

        await using var db = _dbf.Create();
        var w = await db.WaraqEntries.AsSplitQuery()
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

        // ⛔ همان پارچه و همان شیفت، ولی در ورقِ دیگر (تاریخِ پارچه عوض شد) یا با
        // تیلِ دیگر (تیلِ پارچه عوض شد): پایهٔ کهنه برداشته می‌شود — وگرنه همان
        // شیفت دو بار در دو ورق (یا دو ردیف در یک ورق) شمرده می‌شد.
        var others = new List<long>();
        if (createWaraq)
        {
            var sibs = SiblingKeys(srcKey);
            if (sibs.Length > 0)
            {
                var stale = await db.WaraqPumps.Include(p => p.Shift)
                                    .Where(p => sibs.Contains(p.SrcKey!))
                                    .ToListAsync(ct);
                foreach (var old in stale)
                {
                    if (ReferenceEquals(old, entry)) continue;
                    if (old.Shift is { } os)
                    {
                        os.Pumps.Remove(old);
                        if (os.WaraqId != w.Id) others.Add(os.WaraqId);
                    }
                    db.WaraqPumps.Remove(old);
                }
            }
        }

        entry.Num = shift.PumpNum;
        entry.Worker = shift.Name ?? "";
        entry.Start = shift.Start;
        entry.End = shift.End;
        entry.PricePerLiter = shift.Price;
        entry.Debt = shift.Debt;
        if (lowBase is bool low) entry.LowBase = low;

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
        if (others.Count > 0) await ResyncSalesAsync(others, ct);
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

            var month = Shamsi.MonthKey(w.DateShamsi ?? "");

            if (row is null)
            {
                // ══ نخستین خانهٔ خالیِ گاوصندوق — با دو قیدِ تازه ══════════
                //
                // ⛔ <b>فقط همان ماه</b>: جدولِ گاوصندوق ماه‌به‌ماه دیده
                // می‌شود و این ردیف ماهِ خودش را روی ردیفِ برداشته‌شده
                // می‌نویسد. بی این قید، ردیفِ خالیِ کاربر از ماهِ **دیگری**
                // برداشته می‌شد و همان لحظه از جدولِ آن ماه ناپدید — یک
                // ردیف که کاربر باز کرده بود و دیگر پیدایش نمی‌کرد.
                //
                // ⛔ <b>و با ترتیب</b>: بی ‎OrderBy‎، SQLite هر ردیفی را
                // می‌توانست بدهد. «اولین خانهٔ خالی» یعنی همان که کاربر
                // بالای جدول می‌بیند — ‎DateKey‎ و بعد ‎Id‎، همان ترتیبی که
                // خودِ جدول با آن چیده می‌شود.
                row = await db.SafeEntries
                    .Where(e => e.MonthKey == month
                             && (e.SrcKey == null || e.SrcKey == "")
                             && (e.Title == null || e.Title == "")
                             && e.Amount == 0m
                             && (e.Note == null || e.Note == ""))
                    .OrderBy(e => e.DateKey).ThenBy(e => e.Id)
                    .FirstOrDefaultAsync(ct);
                if (row is null) { row = new SafeEntry(); db.SafeEntries.Add(row); }
                row.SrcKey = srcKey;
                row.Kind = SafeEntryKind.Mandagi;
            }

            row.Title = title;
            row.Amount = sales;
            // ⛔ ارز صریح نوشته می‌شود. فروشِ ورق همیشه افغانی است، ولی ردیفِ
            // خالی‌ای که برداشته می‌شود ممکن است کاربر واحدش را روی دالر
            // گذاشته باشد — و آن‌وقت فروشِ افغانیِ یک شیفت در ستونِ دالرِ
            // گاوصندوق می‌نشست. یک عددِ کاملاً غلط، بی هیچ صدایی.
            row.Currency = Currency.Afn;
            row.DateShamsi = w.DateShamsi ?? "";
            row.DateKey = Shamsi.Key(w.DateShamsi ?? "");
            row.MonthKey = month;
        }
    }

    private static WaraqShift NewShift(ShiftKind kind)
    {
        var s = new WaraqShift { Kind = kind };
        for (var i = 0; i < 14; i++) s.Transactions.Add(new WaraqTransaction { SortIndex = i });
        return s;
    }
}
