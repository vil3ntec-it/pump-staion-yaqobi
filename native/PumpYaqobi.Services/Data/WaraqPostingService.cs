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
    public static string WaraqKey(WaraqEntry w) =>
        string.IsNullOrWhiteSpace(w.LegacyId) ? "id" + w.Id : w.LegacyId!;

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

        var w = await db.WaraqEntries
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
        // ⚠️ نتیجه‌اش یک تفاوتِ کوچک با سایت است و عمدی: ردیفِ تازه ته دفتر
        // اضافه می‌شود، به‌جای آن‌که اولین «ردیفِ کاملاً خالی»ِ دفتر را پر کند
        // (سایت آن را می‌کرد). برای پیدا کردنِ آن ردیف باید کلِ دفتر خوانده
        // می‌شد، و همان چیزی است که برنامه را کند می‌کرد.
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

        foreach (var r in mine)
        {
            // ⚠️ EF خودش رابطه‌ها را وصل می‌کند وقتی هر دو سر ردیابی شوند؛
            // پس اول می‌پرسیم که ردیف دو بار در دفتر ننشیند.
            if (r.FuelAccountId is { } f && byAccount.TryGetValue(f, out var fa))
            { if (!fa.FuelRows.Contains(r)) fa.FuelRows.Add(r); }
            else if (r.MoneyAccountId is { } m && byAccount.TryGetValue(m, out var ma))
            { if (!ma.MoneyRows.Contains(r)) ma.MoneyRows.Add(r); }
        }

        // مصرف‌های همین ورق — نه همهٔ مصرف‌های ورقیِ تاریخ. مصرفِ دستیِ کاربر
        // هم هرگز دستکاری نمی‌شود.
        var expenses = await db.Expenses
            .Where(e => e.SrcKey != null && e.SrcKey.StartsWith(prefix))
            .ToListAsync(ct);

        var outcome = Apply(w, people, expenses, _calc);

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
    /// خودِ منطق — بی دیتابیس، تا آزمون بتواند مستقیم صدایش بزند.
    ///
    /// ‎expenses‎ فهرستِ مصرف‌هایی است که کلیدِ منبع دارند؛ همان فهرست جا‌به‌جا
    /// می‌شود و آن‌چه باید در دیتابیس بیفتد در <see cref="WaraqPostOutcome"/>
    /// برمی‌گردد.
    /// </summary>
    public static WaraqPostOutcome Apply(WaraqEntry w, List<Debtor> people,
                                         List<Expense> expenses, WaraqService calc)
    {
        var outcome = new WaraqPostOutcome();
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
                One(w, kind, sd, txns[i], i, people, expenses, calc, date, outcome);

            Sweep(w, kind, txns.Count, people, expenses, outcome);
        }
        return outcome;
    }

    private static void One(WaraqEntry w, ShiftKind kind, WaraqShift sd, WaraqTransaction t,
                            int index, List<Debtor> people, List<Expense> expenses,
                            WaraqService calc, string date, WaraqPostOutcome outcome)
    {
        var srcKey = SrcKeyOf(w, kind, index);
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
            return;
        }

        if (t.Type == WaraqTxnType.Expense)
        {
            DropRows(people, srcKey, outcome);                 // اگر قبلاً قرض بوده
            UpsertExpense(expenses, srcKey, date, name, amount, outcome);
            outcome.Expenses++;
            return;
        }

        DropExpense(expenses, srcKey, outcome);                // اگر قبلاً مصرف بوده

        var hw = PostingService.ExtractHawala(t.Name);
        var display = PostingService.StripFuelWords(
            string.IsNullOrWhiteSpace(hw.Clean) ? name : hw.Clean);
        var fuelType = PostingService.FuelTypeFromText(t.Name, t.Fuel);

        var found = PostingService.FindAccountForText(people, t.Name, display);
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
        if (fuel > 0) priceper = Math.Round(amount / fuel, 2, MidpointRounding.AwayFromZero);
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
        };

        var money = t.Unit == LedgerMode.Money;
        var placed = PostingService.PlaceRow(person, row, t.Name, found.Value.Account, money);

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
                              WaraqPostOutcome outcome)
    {
        var prefix = PrefixOf(w, kind);
        bool Orphan(string? key) =>
            key is not null && key.StartsWith(prefix, StringComparison.Ordinal)
            && int.TryParse(key[prefix.Length..], out var n) && n >= count;

        foreach (var p in people)
            foreach (var a in p.AllAccounts())
                foreach (var list in new[] { a.FuelRows, a.MoneyRows })
                {
                    outcome.RemovedRows.AddRange(list.Where(r => Orphan(r.SrcKey)));
                    list.RemoveAll(r => Orphan(r.SrcKey));
                }

        foreach (var e in expenses.Where(e => Orphan(e.SrcKey)).ToList())
        { outcome.RemovedExpenses.Add(e); expenses.Remove(e); }
    }

    private static void DropRows(IEnumerable<Debtor> people, string srcKey, WaraqPostOutcome outcome)
        => outcome.RemovedRows.AddRange(PostingService.RemoveRowsBySrcKey(people, srcKey));

    private static void DropExpense(List<Expense> expenses, string srcKey, WaraqPostOutcome outcome)
    {
        foreach (var e in expenses.Where(e => e.SrcKey == srcKey).ToList())
        { outcome.RemovedExpenses.Add(e); expenses.Remove(e); }
    }

    private static void UpsertExpense(List<Expense> expenses, string srcKey, string date,
                                      string title, decimal amount, WaraqPostOutcome outcome)
    {
        var ex = expenses.FirstOrDefault(e => e.SrcKey == srcKey);
        if (ex is null)
        {
            ex = new Expense { SrcKey = srcKey };
            expenses.Add(ex);
            outcome.AddedExpenses.Add(ex);
        }
        ex.DateShamsi = date;
        ex.DateKey = Shamsi.Key(date);
        ex.MonthKey = Shamsi.MonthKey(date);
        ex.Title = title;
        ex.Amount = amount;
        ex.Note = "";
    }

    private static decimal Round0(decimal v) => Math.Round(v, 0, MidpointRounding.AwayFromZero);
}
