using Microsoft.EntityFrameworkCore;
using PumpYaqobi.Application.Localization;
using PumpYaqobi.Application.Security;
using PumpYaqobi.Application.Services;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;
using PumpYaqobi.Persistence;

namespace PumpYaqobi.Services.Data;

/// <summary>گزارشِ یک همگام‌سازی — چند ردیف به حساب رفت، چند تا مصرف شد، چند تا صاحب نداشت.</summary>
public readonly record struct WaraqPostReport(int Posted, int Expenses, int Unmatched);

/// <summary>
/// چیزهایی که باید از دیتابیس هم برداشته یا به آن افزوده شوند — لایهٔ محاسبه
/// خودش به دیتابیس دست نمی‌زند تا بشود بی دیتابیس آزمونش کرد.
/// </summary>
public sealed class WaraqPostOutcome
{
    public int Posted { get; set; }
    public int Expenses { get; set; }
    public int Unmatched { get; set; }
    public List<DebtRow> RemovedRows { get; } = new();
    public List<DebtRow> PlacedRows { get; } = new();
    public List<Expense> RemovedExpenses { get; } = new();
    public List<Expense> AddedExpenses { get; } = new();
    /// <summary>ردیف‌های چکنه‌ای که «/چکنه» ساخت یا برداشت.</summary>
    public List<RetailRow> AddedRetail { get; } = new();
    public List<RetailRow> RemovedRetail { get; } = new();

    public WaraqPostReport Report => new(Posted, Expenses, Unmatched);
}

/// <summary>
/// ══ ورق ⇐ حسابِ قرض‌دار و بخشِ مصارف ═══════════════════════════════════════
///
/// گزارشِ صاحب ریپو: «اون حساب طرف رو که توی ورق زدم با مشخصات نمیاد تو همون
/// اسم حساب… چرا اتومات نمیره تو حسابش؟ … مهم نبود اسمش اول باشه یا آخر، اگه
/// اسمش بزرگ بود یک بخش‌اش هم کافی بود — مثل محمد هارون، همون هارون رو هم
/// می‌زدم دقیق به حسابِ محمد هارون می‌رسید.»
///
/// رونوشتِ ‎syncWaraqTxnsToPersons(w)‎ی نسخهٔ وب (‎index.html‎ خط ۴۵۶۵۸)، با
/// همان قاعده‌ها:
///
///   • هر ردیفِ ورق یک «کلیدِ منبع» دارد: ‎&lt;ورق&gt;|&lt;شیفت&gt;|&lt;شمارهٔ ردیف&gt;‎.
///     همان کلیدِ سایت است، پس ردیف‌هایی که از سایت آمده‌اند هم شناخته و
///     به‌روز می‌شوند، نه دوباره ساخته.
///   • نامِ ردیف با ‎PostingService.FindAccountForText‎ به حساب می‌خورد —
///     تطبیقِ دوطرفهٔ نام: «هارون» به «محمد هارون» می‌رسد، و «محمد هارون
///     یعقوبی» هم به حسابِ «محمد هارون».
///   • «نوع = مصرف» ⇒ ردیف به بخشِ مصارف می‌رود و از حسابِ قرض‌دار برداشته
///     می‌شود؛ «قرض» ⇒ برعکس. عوض کردنِ نوع، ثبتِ قبلی را از بخشِ دیگر
///     برمی‌دارد — پول هرگز دو جا شمرده نمی‌شود.
///   • «واحد = پول» ⇒ دفترِ واحد پول، وگرنه دفترِ واحد تیل.
///   • ردیفِ بی‌نام یا بی‌مبلغ از هر دو جا پاک می‌شود.
///   • حسابِ تازه هرگز ساخته نمی‌شود (خواستهٔ صریحِ صاحب ریپو در سایت هم
///     همین بود) — نامی که حساب ندارد فقط شمرده می‌شود.
///
/// ⚠️ یک چیز بیش از سایت: کلیدهای یتیم جارو می‌شوند. در سایت اگر ردیفِ وسطِ
/// جدول حذف می‌شد، شماره‌ها یکی جلو می‌آمدند و کلیدِ شمارهٔ آخر بی‌صاحب در
/// حسابِ قرض‌دار می‌ماند. این‌جا هر کلیدی از همین ورق و همین شیفت که شماره‌اش
/// از تعدادِ ردیف‌ها بزرگ‌تر باشد برداشته می‌شود.
/// </summary>
public sealed class WaraqPostingService
{
    private readonly PumpDbFactory _dbf;
    private readonly PermissionService _perm;
    private readonly WaraqService _calc;
    private readonly ShiftWaraqSyncService _safe;

    public WaraqPostingService(PumpDbFactory dbf, PermissionService perm, WaraqService calc,
                               ShiftWaraqSyncService safe)
    { _dbf = dbf; _perm = perm; _calc = calc; _safe = safe; }

    /// <summary>شناسهٔ ورق در کلید — ورقی که از سایت آمده با شناسهٔ خودِ سایت.</summary>
    public static string WaraqKey(WaraqEntry w) => SrcKeys.Waraq(w);   // ⛔ شورا ب۲: شناسهٔ سراسری، نه شمارهٔ محلی

    /// <summary>‎w.id + '|' + shift + '|' + i‎ — مو‌به‌مو کلیدِ سایت.</summary>
    public static string SrcKeyOf(WaraqEntry w, ShiftKind kind, int index) =>
        WaraqKey(w) + "|" + (kind == ShiftKind.Night ? "night" : "day") + "|" + index;

    /// <summary>پیشوندِ همهٔ کلیدهای یک شیفت — برای جاروی کلیدهای یتیم.</summary>
    private static string PrefixOf(WaraqEntry w, ShiftKind kind) =>
        WaraqKey(w) + "|" + (kind == ShiftKind.Night ? "night" : "day") + "|";

    /// <summary>
    /// ⚠️ یکی‌یکی: هر ویرایشِ ردیف یک همگام‌سازی راه می‌اندازد و اگر دو تا با
    /// هم بدوند، دومی وضعیتِ نیمه‌کارهٔ اولی را می‌خواند و همان ردیف را دو بار
    /// می‌نویسد.
    /// </summary>
    private static readonly SemaphoreSlim Gate = new(1, 1);

    /// <summary>همگام‌سازیِ یک ورق با حساب‌ها و مصارف.</summary>
    public async Task<WaraqPostReport> SyncAsync(long waraqId, CancellationToken ct = default)
    {
        _perm.Require(Permission.EditData);
        await Gate.WaitAsync(ct);
        try { return await RunAsync(waraqId, ct); }
        finally { Gate.Release(); }
    }

    private async Task<WaraqPostReport> RunAsync(long waraqId, CancellationToken ct)
    {
        await using var db = _dbf.Create();

        var w = await db.WaraqEntries.AsSplitQuery()
            .Include(x => x.Shifts).ThenInclude(s => s.Pumps)
            .Include(x => x.Shifts).ThenInclude(s => s.Transactions)
            .FirstOrDefaultAsync(x => x.Id == waraqId, ct);
        if (w is null) return default;

        var prefix = WaraqKey(w) + "|";

        // ══ فقط آن‌چه لازم است ═══════════════════════════════════════════════
        //
        // ⚠️ این‌جا پیش از این **همهٔ قرض‌داران با همهٔ ردیف‌هایشان** خوانده
        // می‌شد، آن هم با هر ویرایشِ یک خانهٔ ورق. با ده هزار قرض‌دار و یک
        // میلیون ردیف یعنی یک میلیون شیء در حافظه، در هر بار تایپ.
        //
        // برای این کار دو چیز بس است:
        //   ۱) نامِ حساب‌ها (برای تطبیقِ نام) — بی هیچ ردیفی
        //   ۲) ردیف‌هایی که کلیدشان مالِ همین ورق است — یعنی همان‌هایی که این
        //      همگام‌سازی می‌تواند عوض یا پاکشان کند
        //
        var people = await db.Debtors
            .Include(d => d.MainAccount)
            .Include(d => d.SubAccounts)
            .ToListAsync(ct);

        var mine = await db.DebtRows
            .Where(r => r.SrcKey != null && r.SrcKey.StartsWith(prefix))
            .ToListAsync(ct);

        var byAccount = new Dictionary<long, DebtAccount>();
        foreach (var a in people.SelectMany(p => p.AllAccounts()))
            byAccount[a.Id] = a;

        // ⚠️ EF خودش رابطه‌ها را وصل می‌کند وقتی هر دو سر ردیابی شوند؛
        // پس اول می‌پرسیم که ردیف دو بار در دفتر ننشیند.
        void Attach(DebtRow r)
        {
            if (r.FuelAccountId is { } f && byAccount.TryGetValue(f, out var fa))
            { if (!fa.FuelRows.Contains(r)) fa.FuelRows.Add(r); }
            else if (r.MoneyAccountId is { } m && byAccount.TryGetValue(m, out var ma))
            { if (!ma.MoneyRows.Contains(r)) ma.MoneyRows.Add(r); }
        }

        foreach (var r in mine) Attach(r);

        // ══ ردیف‌های خالیِ همان حساب‌هایی که هدفِ این ورق‌اند ════════════════
        //
        // گزارشِ صاحب ریپو: «از ورق که اتومات می‌ره تو حسابِ طرف، می‌ره تهِ
        // جدول، در حالی که کادرِ اولِ جدول خالی است.»
        //
        // حق داشت، و این‌جا نوشته بود که عمدی است: برای پیدا کردنِ ردیفِ
        // خالی باید کلِ دفترِ همهٔ قرض‌داران خوانده می‌شد، و همان برنامه را
        // کند می‌کرد. ولی راهِ سومی هست که نه کند است نه غلط:
        //
        //   ۱) اول فقط **تطبیقِ نام** انجام می‌شود (در حافظه، روی نامِ
        //      حساب‌هایی که از قبل خوانده‌ایم — رایگان).
        //   ۲) بعد ردیف‌های خالیِ **فقط همان چند حساب** از دیتابیس می‌آیند.
        //
        // یعنی به‌جای «همهٔ ردیف‌های همهٔ قرض‌داران»، فقط ردیف‌های خالیِ
        // حساب‌هایی خوانده می‌شود که این ورق واقعاً به آن‌ها می‌نویسد — که
        // در عمل چند ردیف است، نه چند هزار.
        //
        // ⚠️ شرطِ خالی بودن این‌جا به SQL می‌رود، پس «تهیِ ساده» سنجیده
        // می‌شود نه «تهی یا فقط فاصله». ردیفی که فقط فاصله در نامش دارد
        // از قلم می‌افتد و کار مثلِ قبل می‌شود (ته جدول) — نه غلط، فقط
        // کم‌سلیقه؛ قاعدهٔ اصلی ‎PostingService.IsBlankRow‎ است.
        var targets = TargetAccountIds(w, people).ToList();
        if (targets.Count > 0)
        {
            var blanks = await db.DebtRows
                .Where(r => (r.FuelAccountId != null && targets.Contains(r.FuelAccountId.Value))
                         || (r.MoneyAccountId != null && targets.Contains(r.MoneyAccountId.Value)))
                .Where(r => (r.SrcKey == null || r.SrcKey == "")
                         && (r.Name == null || r.Name == "")
                         && (r.Hawala == null || r.Hawala == "")
                         && r.Liters == 0m && r.Bardagi == 0m
                         && r.Rasid == 0m && r.RasidFuel == 0m)
                .OrderBy(r => r.SortIndex).ThenBy(r => r.Id)
                .ToListAsync(ct);

            foreach (var r in blanks) Attach(r);
        }

        // مصرف‌های همین ورق — نه همهٔ مصرف‌های ورقیِ تاریخ. مصرفِ دستیِ کاربر
        // هم هرگز دستکاری نمی‌شود.
        var expenses = await db.Expenses
            .Where(e => e.SrcKey != null && e.SrcKey.StartsWith(prefix))
            .ToListAsync(ct);

        // ══ مصرف‌های خالیِ همان ماه ══════════════════════════════════════════
        //
        // همان خواستهٔ «اول کادرِ خالی، بعد ردیفِ تازه» — این بار برای بخشِ
        // مصارف، که تا امروز <b>همیشه</b> ته جدول می‌رفت.
        //
        // ⛔ <b>فقط همان ماه</b>: دفترِ مصارف ماه‌به‌ماه دیده می‌شود. اگر
        // ردیفِ خالیِ ماهِ دیگری برداشته می‌شد، با نوشتنِ ماهِ ورق روی آن،
        // ردیفِ خالیِ کاربر از ماهِ خودش <b>ناپدید</b> می‌شد.
        var month = Shamsi.MonthKey(w.DateShamsi ?? "");
        if (month.Length > 0)
            expenses.AddRange(await db.Expenses
                .Where(e => e.MonthKey == month
                         && (e.SrcKey == null || e.SrcKey == "")
                         && (e.Title == null || e.Title == "")
                         && e.Amount == 0m
                         && (e.Note == null || e.Note == "")
                         && e.SalaryStaffId == null)
                .OrderBy(e => e.DateKey).ThenBy(e => e.Id)
                .ToListAsync(ct));

        //  «/چکنه» — ردیف‌های چکنهٔ همین ورق، و ردیف‌های خالیِ چکنهٔ همان ماه
        //  (همان قاعدهٔ «اول کادرِ خالی، بعد ردیفِ تازه»)
        var retail = await db.RetailRows
            .Where(r => r.SrcKey != null && r.SrcKey.StartsWith(prefix))
            .ToListAsync(ct);
        if (month.Length > 0)
            retail.AddRange(await db.RetailRows
                .Where(r => r.MonthKey == month
                         && (r.SrcKey == null || r.SrcKey == "")
                         && (r.Name == null || r.Name == "")
                         && r.Liters == 0m && r.Bardagi == 0m && r.Rasid == 0m
                         && (r.Note == null || r.Note == ""))
                .OrderBy(r => r.DateKey).ThenBy(r => r.Id)
                .ToListAsync(ct));

        var outcome = Apply(w, people, expenses, _calc, await ArchivedKeysAsync(db, prefix, ct), retail);

        db.RetailRows.RemoveRange(outcome.RemovedRetail.Where(r => r.Id != 0));
        db.RetailRows.AddRange(outcome.AddedRetail);

        db.Expenses.RemoveRange(outcome.RemovedExpenses.Where(e => e.Id != 0));
        db.Expenses.AddRange(outcome.AddedExpenses);

        // ⚠️ ردیفی که از یک دفتر برداشته شده باید واقعاً حذف شود، نه فقط از
        // فهرست بیفتد: کلیدِ حسابش ‎null‎پذیر است، پس EF به‌جای حذف، آن را
        // «بی‌حساب» می‌کرد و ردیف تا ابد در جدول می‌مانْد. پس هر ردیفی که
        // کلیدش مالِ همین ورق است ولی دیگر در هیچ دفتری نیست، حذف می‌شود.
        var live = people.SelectMany(p => p.AllAccounts())
                         .SelectMany(a => a.FuelRows.Concat(a.MoneyRows))
                         .Where(r => r.Id != 0).Select(r => r.Id).ToHashSet();
        db.DebtRows.RemoveRange(mine.Where(r => !live.Contains(r.Id)));

        await db.SaveChangesAsync(ct);

        // ══ «جمله فروش» ⇐ گاوصندوق ═════════════════════════════════════════
        //
        // گزارشِ صاحب ریپو: «اون جمله پول یا جمله فروش می‌ره به گاوصندوق
        // اتومات یا که نه؟»
        //
        // در سایت ‎saveWaraq()‎ دو کار می‌کرد: ‎syncWaraqTxnsToPersons‎ و
        // ‎syncWaraqSalesToSafe‎ (خط ۴۵۵۹۰). دومی در نیتیو نوشته شده بود ولی
        // فقط از مسیرِ «پارچه ⇐ ورق» صدا زده می‌شد؛ وقتی خودِ کاربر پایه‌های
        // ورق را پر می‌کرد، ردیفِ «ماندگی»ِ گاوصندوق کهنه می‌مانْد. حالا هر
        // همگام‌سازیِ ورق هر دو را با هم می‌کند.
        await _safe.SyncSalesToSafeAsync(db, w, ct);
        await db.SaveChangesAsync(ct);

        return outcome.Report;
    }

    /// <summary>
    /// ══ حساب‌هایی که این ورق در آن‌ها می‌نویسد — <b>پیش از</b> نوشتن ═══════
    ///
    /// همان تطبیقِ نامِ ‎One()‎، ولی بی هیچ نوشتنی: فقط می‌گوید «سر و کارِ این
    /// ورق با کدام حساب‌هاست»، تا ردیف‌های خالیِ همان‌ها از دیتابیس بیاید.
    ///
    /// ⛔ <b>قاعدهٔ تطبیق این‌جا دوباره نوشته نشده</b> — همان سه تابعِ
    /// ‎PostingService‎ است که ‎One()‎ هم می‌زند (‎ExtractHawala‎ ·
    /// ‎StripFuelWords‎ · ‎FindAccountForText‎ · ‎ResolveAccount‎). دو نسخه از
    /// این قاعده یعنی روزی ردیف در حسابی بنشیند که ردیف‌های خالی‌اش خوانده
    /// نشده بود، و همان لحظه دوباره ته جدول می‌رفت.
    ///
    /// ⚠️ این‌جا دربارهٔ مبلغ و لیتر چیزی پرسیده نمی‌شود، پس گاهی حسابی در
    /// فهرست می‌آید که در عملْ ردیفی نمی‌گیرد. زیانش فقط خواندنِ چند ردیفِ
    /// خالیِ بی‌مصرف است؛ نیامدنِ یک حساب اما یعنی همان باگِ «ته جدول».
    /// </summary>
    public static HashSet<long> TargetAccountIds(WaraqEntry w, List<Debtor> people)
    {
        var ids = new HashSet<long>();
        foreach (var kind in new[] { ShiftKind.Day, ShiftKind.Night })
        {
            var sd = w.Shifts.FirstOrDefault(s => s.Kind == kind);
            if (sd is null) continue;

            foreach (var t in sd.Transactions)
            {
                if (t.Type == WaraqTxnType.Expense) continue;
                var name = (t.Name ?? "").Trim();
                if (name.Length == 0) continue;
                if (PostingService.RetailName(name) is not null) continue;   // «/چکنه»

                var m = PostingService.MatchWaraqName(people, t.Name);
                if (m.Found is not { } found) continue;

                var acct = PostingService.ResolveAccount(found.Person, m.AccountText, found.Account);
                if (acct.Id != 0) ids.Add(acct.Id);
            }
        }
        return ids;
    }

    /// <summary>
    /// خودِ منطق — بی دیتابیس، تا آزمون بتواند مستقیم صدایش بزند.
    ///
    /// ‎expenses‎ فهرستِ مصرف‌هایی است که کلیدِ منبع دارند؛ همان فهرست جا‌به‌جا
    /// می‌شود و آن‌چه باید در دیتابیس بیفتد در <see cref="WaraqPostOutcome"/>
    /// برمی‌گردد.
    /// </summary>
    public static WaraqPostOutcome Apply(WaraqEntry w, List<Debtor> people,
                                         List<Expense> expenses, WaraqService calc,
                                         IReadOnlyDictionary<string, List<ArchivedSrc>>? archived = null,
                                         List<RetailRow>? retail = null)
    {
        var outcome = new WaraqPostOutcome();
        retail ??= new List<RetailRow>();
        var date = w.DateShamsi ?? "";

        foreach (var kind in new[] { ShiftKind.Day, ShiftKind.Night })
        {
            var sd = w.Shifts.FirstOrDefault(s => s.Kind == kind);
            if (sd is null) continue;

            // مبلغِ خودکار اول با فیِ درست تازه می‌شود — وگرنه مبلغی که در ورق
            // دیده می‌شود با مبلغی که به حساب می‌رود یکی نیست (همان کاری که
            // سایت پیش از هر همگام‌سازی می‌کند).
            calc.NormalizeTxns(sd);

            var txns = sd.Transactions.OrderBy(t => t.SortIndex).ThenBy(t => t.Id).ToList();
            for (var i = 0; i < txns.Count; i++)
                One(w, kind, sd, txns[i], i, people, expenses, calc, date, outcome, archived, retail);

            Sweep(w, kind, txns.Count, people, expenses, outcome, retail);
        }
        return outcome;
    }

    private static void One(WaraqEntry w, ShiftKind kind, WaraqShift sd, WaraqTransaction t,
                            int index, List<Debtor> people, List<Expense> expenses,
                            WaraqService calc, string date, WaraqPostOutcome outcome,
                            IReadOnlyDictionary<string, List<ArchivedSrc>>? archived = null,
                            List<RetailRow>? retail = null)
    {
        retail ??= new List<RetailRow>();
        var srcKey = SrcKeyOf(w, kind, index);

        // ⛔ ردیفی که با «جدول جدید» به آرشیوِ حساب رفته دوباره ساخته نمی‌شود
        // (۱۴۰۵/۰۷/۱۶): آرشیو ردیف را از جدولِ زنده برمی‌دارد، پس هر ویرایشِ بعدیِ
        // همان ورقِ قدیمی آن قرض را **دوباره** در جدولِ نو می‌نشاند — یک قرض، دو
        // بار شمرده. ردیفِ زنده با همان کلید (بازگردانده از سطل) مثلِ همیشه.
        //
        //  ⛔ (۱۴۰۵/۰۷/۲۲، ممیزی گرفتش) کلید فقط **جای** ردیف است. ردیفی بالاتر که حذف شود،
        //  ردیفِ شخصِ دیگری به همان جا و همان کلیدِ آرشیوشده می‌رسید و بی‌صدا از حسابش
        //  پاک می‌شد (قرضِ ۵۰۰۰ِ رحیم در ورق و گاوصندوق بود و در حسابش صفر). پس کلیدِ آرشیو
        //  فقط وقتی جلو را می‌گیرد که همین ردیف **همان** ثبت باشد — با شناسهٔ خودِ تراکنش
        //  (‎DebtRow.SrcTxn‎)، و برای آرشیوِ کهنهٔ بی‌شناسه همان حساب و همان مبلغ (‎IsArchived‎).
        if (archived is not null && t.Type != WaraqTxnType.Expense && IsArchived(t, srcKey, sd, calc, people, archived))
        {
            //  ⛔ شورا، الف۵ (آزمونِ تصادفی گرفتش): مصرف یا چکنه‌ای که همین کلید را از
            //  ردیفِ پیشینِ همین جا دارد (ردیفی که حذف شد و بقیه یک خانه بالا آمدند)
            //  مالِ این ردیفِ قرض نیست و باید برود — وگرنه یک مصرفِ حذف‌شده برای
            //  همیشه در مصارف می‌ماند و دو بار شمرده می‌شد. همان برای ردیفِ حسابی که
            //  مالِ تراکنشِ دیگری بود (‎SrcTxn‎ِ دیگر).
            DropExpense(expenses, srcKey, outcome);
            DropRetail(retail, srcKey, outcome);
            if (!string.IsNullOrEmpty(t.SyncUid)) DropRows(people, srcKey, outcome);
            return;
        }
        var name = (t.Name ?? "").Trim();
        var amount = Round0(calc.TxnAmount(sd, t));
        var liters = t.Liters;

        // پُرکردنِ دوطرفهٔ تیل ↔ پول با نرخِ همین ورق، مثلِ سایت:
        //   • مبلغ خالی و مقدار تیل پر ⇒ مبلغ = مقدار × نرخ
        //   • مقدار تیل خالی و مبلغ پر ⇒ مقدار = مبلغ ÷ نرخ (به‌جز واحد پول)
        var rate = calc.RepPrice(sd, t.Fuel);
        if (amount <= 0 && liters > 0 && rate > 0) amount = Round0(liters * rate);
        if (liters <= 0 && amount > 0 && rate > 0 && t.Unit != LedgerMode.Money) liters = amount / rate;

        if (name.Length == 0 || amount == 0)
        {
            DropExpense(expenses, srcKey, outcome);
            DropRows(people, srcKey, outcome);
            DropRetail(retail, srcKey, outcome);
            return;
        }

        if (t.Type == WaraqTxnType.Expense)
        {
            DropRows(people, srcKey, outcome);                 // اگر قبلاً قرض بوده
            DropRetail(retail, srcKey, outcome);
            //  ⛔ «/هارون» فقط راهنمای حساب است — در عنوانِ مصرف هم دیده نمی‌شود
            UpsertExpense(expenses, srcKey, date, PostingService.CleanWaraqName(people, name),
                          amount, outcome);
            outcome.Expenses++;
            return;
        }

        DropExpense(expenses, srcKey, outcome);                // اگر قبلاً مصرف بوده

        //  ══ «/چکنه» ⇒ دفترِ چکنه (۱۴۰۵/۰۷/۱۸) — نه حسابِ قرض‌دار ══════════
        if (PostingService.RetailName(name) is { } retailName)
        {
            DropRows(people, srcKey, outcome);
            UpsertRetail(retail, srcKey, date, retailName,
                         PostingService.FuelTypeFromText(t.Name, t.Fuel),
                         liters, amount, t.Unit == LedgerMode.Money, outcome);
            outcome.Posted++;
            return;
        }
        DropRetail(retail, srcKey, outcome);                   // اگر قبلاً چکنه بوده

        //  ⛔ «/هارون»: حساب از پسِ خط‌کج، نامِ ردیف بی نامِ حساب (۱۴۰۵/۰۷/۱۷) —
        //  تنها جای این تصمیم ‎PostingService.MatchWaraqName‎ است.
        var match = PostingService.MatchWaraqName(people, t.Name);
        var hw = match.Hawala;
        var display = match.Display;
        var fuelType = PostingService.FuelTypeFromText(t.Name, t.Fuel);

        var found = match.Found;
        if (found is null)
        {
            // حسابِ تازه ساخته نمی‌شود — فقط ثبتِ قبلیِ همین ردیف برداشته می‌شود.
            DropRows(people, srcKey, outcome);
            outcome.Unmatched++;
            return;
        }

        var person = found.Value.Person;
        decimal fuel = liters, priceper;
        var byMoney = false;
        //  ⛔ شش رقمِ اعشار، نه دو (۱۴۰۵/۰۷/۱۶): حساب بردگی را «لیتر × فی» حساب می‌کند،
        //  پس فیِ دورقمی مبلغِ دستیِ ورق را عوض می‌کرد (۱٬۰۰۰ لیتر × ۶۰٫۰۱ = ۶۰٬۰۱۰
        //  به‌جای ۶۰٬۰۰۵). با شش رقم خطا زیرِ نیم افغانی می‌ماند و گردِ صفر آن را می‌برد.
        if (fuel > 0) priceper = Math.Round(amount / fuel, 6, MidpointRounding.AwayFromZero);
        else { fuel = 0; priceper = 0; byMoney = true; }
        if (t.Unit == LedgerMode.Money) byMoney = true;

        // اگر نام عوض شده و حالا به شخصِ دیگری می‌خورد، ثبتِ قبلی از آن‌جا برود
        DropRows(people.Where(p => !ReferenceEquals(p, person)), srcKey, outcome);

        var row = new DebtRow
        {
            DateShamsi = date,
            DateKey = Shamsi.Key(date),
            Name = display,
            Hawala = hw.Num,
            Fuel = fuelType,
            Liters = fuel,
            PricePerLiter = priceper,
            Bardagi = amount,
            Rasid = 0,
            Albaqi = amount,
            ByMoney = byMoney,
            Src = "waraq",
            SrcKey = srcKey,
            SrcTxn = string.IsNullOrEmpty(t.SyncUid) ? null : t.SyncUid,
        };

        var money = t.Unit == LedgerMode.Money;
        var placed = PostingService.PlaceRow(person, row, match.AccountText, found.Value.Account, money);

        // ردیفِ تازه باید کلیدِ دفترش را داشته باشد تا بداند کجا بنشیند
        var acct = found.Value.Account;
        if (placed.Id == 0)
        {
            if (money && placed.MoneyAccountId is null)
                placed.MoneyAccountId = acct.Id != 0 ? acct.Id : person.MainAccount.Id;
            else if (!money && placed.FuelAccountId is null)
                placed.FuelAccountId = acct.Id != 0 ? acct.Id : person.MainAccount.Id;
        }

        outcome.PlacedRows.Add(placed);
        outcome.Posted++;
    }

    /// <summary>
    /// کلیدهای یتیمِ همین ورق و شیفت — شماره‌هایی بزرگ‌تر از تعدادِ ردیف‌ها.
    /// وقتی ردیفی از ورق حذف شود، شماره‌ها یکی جلو می‌آیند و کلیدِ آخر
    /// بی‌صاحب می‌مانَد؛ بی این جارو، آن ردیف تا ابد در حسابِ قرض‌دار می‌ماند.
    /// </summary>
    private static void Sweep(WaraqEntry w, ShiftKind kind, int count,
                              List<Debtor> people, List<Expense> expenses,
                              WaraqPostOutcome outcome, List<RetailRow>? retail = null)
    {
        var prefix = PrefixOf(w, kind);
        bool Orphan(string? key) =>
            key is not null && key.StartsWith(prefix, StringComparison.Ordinal)
            && int.TryParse(key[prefix.Length..], out var n) && n >= count;

        foreach (var p in people)
            foreach (var a in p.AllAccounts())
                foreach (var list in new[] { a.FuelRows, a.MoneyRows })
                {
                    foreach (var r in list.Where(r => Orphan(r.SrcKey)).ToList())
                        Retire(list, r, outcome);
                }

        foreach (var e in expenses.Where(e => Orphan(e.SrcKey)).ToList())
        { outcome.RemovedExpenses.Add(e); expenses.Remove(e); }

        if (retail is not null)
            foreach (var r in retail.Where(r => Orphan(r.SrcKey)).ToList())
                RetireRetail(retail, r, outcome);
    }

    private static void DropRows(IEnumerable<Debtor> people, string srcKey, WaraqPostOutcome outcome)
    {
        if (string.IsNullOrEmpty(srcKey)) return;
        foreach (var p in people)
        {
            if (p is null) continue;
            foreach (var a in p.AllAccounts())
                foreach (var list in new[] { a.FuelRows, a.MoneyRows })
                    foreach (var r in list.Where(r => r.SrcKey == srcKey).ToList())
                        Retire(list, r, outcome);
        }
    }

    /// <summary>
    /// ردیفی که دیگر مالِ این ورق نیست. ⛔ اگر کاربر رویش <b>رسید</b> نوشته
    /// (۱۴۰۵/۰۷/۱۶)، رسید نمی‌رود: ردیف فقط از ورق جدا می‌شود و یک ردیفِ
    /// رسیدِ تنها می‌ماند. پیش از این حذفِ یک تراکنشِ وسطِ ورق شماره‌ها را جلو
    /// می‌آورد، ردیفِ آخر «یتیم» می‌شد و با همان رسیدی که مشتری داده بود پاک
    /// می‌شد — پولِ گرفته‌شده از حساب ناپدید.
    /// </summary>
    private static void Retire(ICollection<DebtRow> list, DebtRow r, WaraqPostOutcome outcome)
    {
        if (r.Rasid != 0m || r.RasidFuel != 0m)
        {
            r.SrcKey = null;
            r.SrcTxn = null;
            r.Src = null;
            r.Liters = 0m;
            r.PricePerLiter = 0m;
            r.Bardagi = 0m;
            r.ByMoney = true;
            r.Albaqi = Round0(-r.Rasid);
            return;
        }
        outcome.RemovedRows.Add(r);
        list.Remove(r);
    }

    private static bool HasLiveRow(IEnumerable<Debtor> people, string srcKey) =>
        people.Where(p => p is not null).SelectMany(p => p.AllAccounts())
              .Any(a => a.FuelRows.Any(r => r.SrcKey == srcKey) || a.MoneyRows.Any(r => r.SrcKey == srcKey));

    /// <summary>
    /// کلیدهای همین ورق که در یک جدولِ آرشیوِ قرض‌دار نشسته‌اند. ⚡ فقط
    /// آرشیوهایی خوانده می‌شوند که متنشان پیشوندِ همین ورق را دارد (یک ‎instr‎ی
    /// خودِ SQLite)، نه همهٔ آرشیوها.
    /// </summary>
    /// <summary>ثبتی که با «جدول جدید» به آرشیوِ یک حساب رفته: کدام حساب، چه مبلغی، کدام تراکنش.</summary>
    public readonly record struct ArchivedSrc(long AccountId, decimal Bardagi, string? Txn = null);

    /// <summary>
    /// این تراکنشِ ورق پیش از این با «جدول جدید» به آرشیوِ یک حساب رفته؟
    ///
    /// ⛔ (۱۴۰۵/۰۷/۲۲) کلیدِ منبع فقط <b>جای</b> ردیف است. پس با شناسهٔ خودِ تراکنش
    /// (‎SrcTxn‎ = ‎SyncUid‎) پرسیده می‌شود، در <b>هر</b> کلیدِ این ورق: تراکنشی که پس از
    /// حذفِ ردیفی بالاتر یک خانه بالا آمده هم همان ثبتِ آرشیوشده است و دوباره نمی‌رود،
    /// و ردیفِ دیگری (حتی همان شخص و همان مبلغ) که به کلیدِ آرشیوشده رسیده، ثبتِ تازه
    /// است و می‌رود. ردیفِ زندهٔ همین تراکنش (بازگشته از سطل) مثلِ همیشه به‌روز می‌شود.
    /// آرشیوِ کهنهٔ بی‌شناسه همان قاعدهٔ «همان کلید، همان حساب، همان مبلغ».
    /// </summary>
    private static bool IsArchived(WaraqTransaction t, string srcKey, WaraqShift sd, WaraqService calc,
                                   List<Debtor> people, IReadOnlyDictionary<string, List<ArchivedSrc>> archived)
    {
        var uid = t.SyncUid;
        if (!string.IsNullOrEmpty(uid))
        {
            if (PostingService.RetailName((t.Name ?? "").Trim()) is not null) return false;
            if (archived.Values.Any(l => l.Any(a => string.Equals(a.Txn, uid, StringComparison.Ordinal))))
                return !people.Where(p => p is not null).SelectMany(p => p.AllAccounts())
                              .Any(a => a.FuelRows.Concat(a.MoneyRows)
                                         .Any(r => r.SrcKey == srcKey && string.Equals(r.SrcTxn, uid, StringComparison.Ordinal)));
        }
        return archived.TryGetValue(srcKey, out var arch) && !HasLiveRow(people, srcKey)
               && arch.Any(a => (string.IsNullOrEmpty(a.Txn) || string.IsNullOrEmpty(uid))
                                && SameAsArchived(t, sd, calc, people, a));
    }

    /// <summary>این ردیفِ ورق همان ثبتی است که با این کلید به آرشیو رفت؟</summary>
    private static bool SameAsArchived(WaraqTransaction t, WaraqShift sd, WaraqService calc,
                                       List<Debtor> people, ArchivedSrc arch)
    {
        //  آرشیوِ کهنه (پیش از ‎SrcTxn‎) — همان حساب و همان مبلغ
        var amount = Round0(calc.TxnAmount(sd, t));
        var rate = calc.RepPrice(sd, t.Fuel);
        if (amount <= 0 && t.Liters > 0 && rate > 0) amount = Round0(t.Liters * rate);
        if (amount != Round0(arch.Bardagi)) return false;
        if (PostingService.RetailName((t.Name ?? "").Trim()) is not null) return arch.AccountId == 0;
        var m = PostingService.MatchWaraqName(people, t.Name);
        if (m.Found is not { } f) return false;
        var acct = PostingService.ResolveAccount(f.Person, m.AccountText, f.Account);
        return arch.AccountId == 0 || acct.Id == arch.AccountId;
    }

    private static async Task<Dictionary<string, List<ArchivedSrc>>> ArchivedKeysAsync(PumpDbContext db,
                                                                 string prefix, CancellationToken ct)
    {
        //  یک کلید ممکن است در چند آرشیو باشد (همان جا، ثبت‌های پیاپی) — همه نگه داشته می‌شوند
        var keys = new Dictionary<string, List<ArchivedSrc>>(StringComparer.Ordinal);
        //  نامِ کلید در ‎RowsJson‎ همان است که ‎JsonSerializer‎ نوشته — ممکن است
        //  نویسه‌ای را با ‎\uXXXX‎ نوشته باشد، پس هر دو شکل پرسیده می‌شود.
        var escaped = System.Text.Json.JsonSerializer.Serialize(prefix).Trim('"');
        var hits = await db.DebtTableArchives
            .Where(x => x.RowsJson != null && (x.RowsJson.Contains(prefix) || x.RowsJson.Contains(escaped)))
            .Select(x => new { x.AccountId, x.RowsJson })
            .ToListAsync(ct);
        foreach (var hit in hits)
        {
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(hit.RowsJson!);
                if (doc.RootElement.ValueKind != System.Text.Json.JsonValueKind.Array) continue;
                foreach (var el in doc.RootElement.EnumerateArray())
                    if (el.ValueKind == System.Text.Json.JsonValueKind.Object
                        && el.TryGetProperty("SrcKey", out var k)
                        && k.ValueKind == System.Text.Json.JsonValueKind.String
                        && k.GetString() is { } key && key.StartsWith(prefix, StringComparison.Ordinal))
                        (keys.TryGetValue(key, out var l) ? l : keys[key] = new()).Add(new ArchivedSrc(hit.AccountId,
                            el.TryGetProperty("Bardagi", out var b) && b.ValueKind == System.Text.Json.JsonValueKind.Number
                                ? b.GetDecimal() : 0m,
                            el.TryGetProperty("SrcTxn", out var tx) && tx.ValueKind == System.Text.Json.JsonValueKind.String
                                ? tx.GetString() : null));
            }
            catch (System.Text.Json.JsonException) { /* آرشیوِ خراب — نادیده */ }
        }
        return keys;
    }

    private static void DropExpense(List<Expense> expenses, string srcKey, WaraqPostOutcome outcome)
    {
        foreach (var e in expenses.Where(e => e.SrcKey == srcKey).ToList())
        { outcome.RemovedExpenses.Add(e); expenses.Remove(e); }
    }

    /// <summary>
    /// مصرفی که هیچ چیزی در آن نوشته نشده — جای طبیعیِ مصرفِ خودکارِ تازه.
    ///
    /// ⛔ ‎SalaryStaffId‎ هم شمرده می‌شود: مصرفی که «پرداختِ معاش» ساخته،
    /// حتی با مبلغِ صفر، <b>خالی نیست</b> و پر کردنش یعنی گم شدنِ مهرِ معاشِ
    /// آن کارمند در آن ماه.
    /// </summary>
    internal static bool IsBlankExpense(Expense e) =>
        string.IsNullOrEmpty(e.SrcKey) && string.IsNullOrWhiteSpace(e.Title)
        && e.Amount == 0m && string.IsNullOrWhiteSpace(e.Note)
        && e.SalaryStaffId is null;

    private static void UpsertExpense(List<Expense> expenses, string srcKey, string date,
                                      string title, decimal amount, WaraqPostOutcome outcome)
    {
        var ex = expenses.FirstOrDefault(e => e.SrcKey == srcKey);
        if (ex is null)
        {
            // نخستین مصرفِ خالیِ همان ماه، از بالا — وگرنه ردیفِ تازه ته جدول
            ex = expenses.Where(IsBlankExpense)
                         .OrderBy(e => e.DateKey).ThenBy(e => e.Id).FirstOrDefault();
            if (ex is null)
            {
                ex = new Expense();
                expenses.Add(ex);
                outcome.AddedExpenses.Add(ex);      // فقط ردیفِ واقعاً تازه
            }
            ex.SrcKey = srcKey;
        }
        ex.DateShamsi = date;
        ex.DateKey = Shamsi.Key(date);
        ex.MonthKey = Shamsi.MonthKey(date);
        ex.Title = title;
        ex.Amount = amount;
        ex.Note = "";
    }

    private static void DropRetail(List<RetailRow> retail, string srcKey, WaraqPostOutcome outcome)
    {
        foreach (var r in retail.Where(r => r.SrcKey == srcKey).ToList())
            RetireRetail(retail, r, outcome);
    }

    /// <summary>
    /// ردیفِ چکنه‌ای که دیگر مالِ این ورق نیست. ⛔ اگر کاربر رویش رسید نوشته،
    /// رسید نمی‌رود — همان قاعدهٔ ‎Retire‎ِ ردیفِ قرض‌دار.
    /// </summary>
    private static void RetireRetail(List<RetailRow> retail, RetailRow r, WaraqPostOutcome outcome)
    {
        if (r.Rasid != 0m)
        {
            r.SrcKey = null;
            r.Liters = 0m;
            r.PricePerLiter = 0m;
            r.Bardagi = 0m;
            r.ByMoney = true;
            return;
        }
        outcome.RemovedRetail.Add(r);
        retail.Remove(r);
    }

    internal static bool IsBlankRetail(RetailRow r) =>
        string.IsNullOrEmpty(r.SrcKey) && string.IsNullOrWhiteSpace(r.Name)
        && r.Liters == 0m && r.Bardagi == 0m && r.Rasid == 0m
        && string.IsNullOrWhiteSpace(r.Note);

    private static void UpsertRetail(List<RetailRow> retail, string srcKey, string date, string name,
                                     FuelType fuel, decimal liters, decimal amount, bool money,
                                     WaraqPostOutcome outcome)
    {
        var r = retail.FirstOrDefault(x => x.SrcKey == srcKey);
        if (r is null)
        {
            r = retail.Where(IsBlankRetail).OrderBy(x => x.DateKey).ThenBy(x => x.Id).FirstOrDefault();
            if (r is null)
            {
                r = new RetailRow();
                retail.Add(r);
                outcome.AddedRetail.Add(r);
            }
            r.SrcKey = srcKey;
        }
        r.DateShamsi = date;
        r.DateKey = Shamsi.Key(date);
        r.MonthKey = Shamsi.MonthKey(date);
        r.Name = name;
        r.Fuel = fuel;
        r.Note = "ورق";
        //  همان قاعدهٔ ردیفِ قرض‌دار: واحدِ پول یا بی‌لیتر ⇒ بردگی همان مبلغ؛
        //  وگرنه لیتر × فیِ شش‌رقمی، تا مبلغِ ورق عوض نشود.
        if (money || liters <= 0m)
        {
            r.ByMoney = true;
            r.Liters = money ? 0m : liters;
            r.PricePerLiter = 0m;
            r.Bardagi = amount;
        }
        else
        {
            r.ByMoney = false;
            r.Liters = liters;
            r.PricePerLiter = Math.Round(amount / liters, 6, MidpointRounding.AwayFromZero);
            r.Bardagi = amount;
        }
    }

    private static decimal Round0(decimal v) => Math.Round(v, 0, MidpointRounding.AwayFromZero);
}
