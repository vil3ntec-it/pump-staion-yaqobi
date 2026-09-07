using System.Text.Json;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;

namespace PumpYaqobi.Infrastructure.Migration;

/// <summary>
/// یک روزِ حاضری، هنوز به کارمندِ تازه وصل نشده.
///
/// ⚠️ در نسخهٔ وب کارمند با شناسهٔ رشته‌ای (‎sid‎) صدا زده می‌شد و در نیتیو با
/// کلیدِ عددیِ دیتابیس. کلیدِ عددی پیش از نوشتنِ خودِ کارمند وجود ندارد، پس
/// شناسهٔ کهنه این‌جا نگه داشته می‌شود تا سرویسِ مهاجرت وصلش کند.
/// </summary>
public sealed record LegacyAttendance(string? StaffLegacyId, AttendanceRow Row);

/// <summary>یک پرداختِ معاش، با همان قاعدهٔ بالا.</summary>
public sealed record LegacySalary(string? StaffLegacyId, SalaryPayment Row);

/// <summary>
/// همهٔ آنچه از یک بکاپِ نسخهٔ وب بیرون می‌آید — هر جدول جدا.
/// </summary>
public sealed record LegacyBundle(
    List<Debtor> Debtors,
    List<SafeEntry> Safe,
    List<ExchangeRow> Exchange,
    List<Expense> Expenses,
    List<RetailRow> Retail,
    List<ParchaReport> Reports,
    List<FuelPurchase> Purchases,
    List<TilCompany> Companies,
    List<TankDip> Dips,
    List<TankerUnload> Unloads,
    List<RateHistoryEntry> Rates,
    List<ExtraIncome> ExtraIncomes,
    List<ParchaReceipt> ParchaReceipts,
    List<DebtQuickReceipt> QuickReceipts,
    List<WaraqEntry> Waraq,
    List<Invoice> Invoices,
    List<AmanatAccount> Amanat,
    List<StaffMember> Staff,
    List<LegacyAttendance> Attendance,
    List<LegacySalary> Salaries,
    List<StaffShortSettle> Settles,
    List<Camera> Cameras,
    Dictionary<string, string> Settings,
    /// <summary>شمارشِ ‎_dbRecordCount‎ روی خودِ فایل — مبنای سنجشِ «چیزی گم نشد».</summary>
    int SourceRecords,
    ImportReport Report);

/// <summary>
/// ══ مهاجرتِ جدول‌های عملیاتی (بندِ ۳۱) ═══════════════════════════════════════
/// نیمهٔ دومِ مهاجرت: پارچه، مخزن، شرکت‌ها، ورق، فاکتور، امانت، حاضری و
/// دفترهای کوچک — کنارِ قرض‌داران و پول که پیش‌تر آمده بودند.
///
/// همان سه قاعدهٔ نیمهٔ اول این‌جا هم برقرار است: هیچ چیزی حدس زده نمی‌شود،
/// عددها ‎decimal‎ اند، و رکوردِ خراب فقط خودش رد می‌شود و در ‎Warnings‎
/// می‌نشیند — نه اینکه کلِ مهاجرت را بشکند.
/// </summary>
public sealed class LegacyOperationsImporter
{
    private readonly List<string> _warn = new();

    /// <summary>
    /// همهٔ بکاپ را می‌خواند: هم آنچه ‎LegacyBackupImporter‎ می‌داد و هم بقیه.
    /// </summary>
    public LegacyBundle ParseAll(string json)
    {
        var basePart = new LegacyBackupImporter().Parse(json);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var reports = new List<ParchaReport>();
        // پطرول در ‎reports‎ (دو شیفت در یک رکورد) و دیزل در ‎shifts‎ (هر شیفت
        // یک رکوردِ تخت) — دو شکلِ جدا در نسخهٔ وب، یک جدول در نیتیو.
        Read(root, "reports", e => reports.Add(ReadReport(e)));
        Read(root, "shifts", e => reports.Add(ReadFlatShift(e)));

        var purchases = ReadList(root, "fuelEntries", ReadPurchase);
        var companies = ReadList(root, "tilCompanies", ReadCompany);

        // ⚠️ اصلاح‌های میله‌زنی در نسخهٔ وب جدولِ جدا بودند (‎tankAdjusts‎) و
        // با ‎dipId‎ به میله‌زنی وصل می‌شدند؛ در نیتیو روی خودِ رکورد نشسته‌اند.
        var adjusts = new Dictionary<string, decimal>(StringComparer.Ordinal);
        Read(root, "tankAdjusts", e =>
        {
            var id = LegacyBackupImporter.Str2(e, "dipId");
            if (!string.IsNullOrEmpty(id)) adjusts[id!] = LegacyBackupImporter.Dec2(e, "liters");
        });
        var dips = ReadList(root, "tankDips", e => ReadDip(e, adjusts));

        var bundle = new LegacyBundle(
            basePart.Debtors, basePart.Safe, basePart.Exchange, basePart.Expenses, basePart.Retail,
            reports, purchases, companies, dips,
            ReadList(root, "tankerLogs", ReadUnload),
            ReadList(root, "rateHistory", ReadRate),
            ReadList(root, "extraIncomes", ReadExtraIncome),
            ReadList(root, "parchaReceipts", ReadParchaReceipt),
            ReadList(root, "debtQuickReceipts", ReadQuickReceipt),
            ReadList(root, "waraqEntries", ReadWaraq),
            ReadList(root, "invoices", ReadInvoice),
            ReadList(root, "amanatAccounts", ReadAmanat),
            ReadList(root, "staffMembers", ReadStaff),
            ReadList(root, "attendance", ReadAttendance),
            ReadList(root, "salaryPayments", ReadSalary),
            ReadList(root, "staffShortSettles", ReadSettle),
            ReadList(root, "cameras", ReadCamera),
            ReadSettings(root),
            RecordCount(root),
            new ImportReport(basePart.Report.Debtors, basePart.Report.SubAccounts,
                             basePart.Report.DebtRows, basePart.Report.SafeEntries,
                             basePart.Report.ExchangeRows, basePart.Report.Expenses,
                             basePart.Report.RetailRows,
                             basePart.Report.Warnings.Concat(_warn).ToList()));
        return bundle;
    }

    /// <summary>
    /// ‎_dbRecordCount(db)‎ — همان شمارشی که خودِ نسخهٔ وب برای سنجیدنِ سالم
    /// بودنِ بازیابی به کار می‌برد. مهاجرت هم با همین سنجیده می‌شود: اگر عددِ
    /// وارد‌شده با این یکی نخواند، چیزی در راه گم شده.
    /// </summary>
    public static int RecordCount(JsonElement root)
    {
        var keys = new[]
        {
            "reports", "shifts", "debtPersons", "noinvPersons", "tilCompanies", "sarrafiRows",
            "waraqEntries", "expenses", "safeEntries", "amanatAccounts", "fuelEntries",
            "invoices", "attendance", "staffMembers", "debtRasidRows", "chakanaRows",
        };
        var n = 0;
        foreach (var k in keys)
            if (root.TryGetProperty(k, out var a) && a.ValueKind == JsonValueKind.Array)
                n += a.GetArrayLength();
        return n;
    }

    public static int RecordCount(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return RecordCount(doc.RootElement);
    }

    // ── کمکی‌ها ─────────────────────────────────────────────────────────────
    private void Read(JsonElement root, string key, Action<JsonElement> read)
    {
        if (!root.TryGetProperty(key, out var arr) || arr.ValueKind != JsonValueKind.Array) return;
        foreach (var e in arr.EnumerateArray())
            try { read(e); }
            catch (Exception ex) { _warn.Add($"{key}: یک رکورد رد شد ({ex.Message})"); }
    }

    private List<T> ReadList<T>(JsonElement root, string key, Func<JsonElement, T> read)
    {
        var list = new List<T>();
        Read(root, key, e => list.Add(read(e)));
        return list;
    }

    private static string? S(JsonElement e, string k) => LegacyBackupImporter.Str2(e, k);
    private static decimal D(JsonElement e, string k) => LegacyBackupImporter.Dec2(e, k);
    private static decimal? DN(JsonElement e, string k) => LegacyBackupImporter.DecOrNull2(e, k);
    private static bool B(JsonElement e, string k) => LegacyBackupImporter.Bool2(e, k);
    private static int Key(string? d) => LegacyBackupImporter.DateKeyOf(d);
    private static string? Mon(string? d) => LegacyBackupImporter.MonthKeyOf(d);
    private static FuelType F(JsonElement e, string k) => FuelTypeExtensions.FromLegacy(S(e, k));

    // ── پارچه ───────────────────────────────────────────────────────────────
    private static ShiftData? ReadShiftData(JsonElement parent, string key)
    {
        if (!parent.TryGetProperty(key, out var e) || e.ValueKind != JsonValueKind.Object) return null;
        return new ShiftData
        {
            Name = S(e, "name"), PumpNum = (int)D(e, "pumpNum"),
            Start = D(e, "start"), End = D(e, "end"), Price = D(e, "price"),
            ProfitPer = D(e, "profitPer"), BuyPerLiter = D(e, "buyPerLiter"),
            Sale = D(e, "sale"), Money = D(e, "money"), Debt = D(e, "debt"),
            Available = D(e, "available"), AvailMan = D(e, "availMan"),
            Profit = D(e, "profit"), Note = S(e, "note"), SavedAt = S(e, "savedAt"),
        };
    }

    private static ParchaReport ReadReport(JsonElement e) => new()
    {
        LegacyId = S(e, "id") ?? IdOfNumber(e),
        ReportNum = (int)D(e, "reportNum"),
        DateShamsi = S(e, "date"), DateKey = Key(S(e, "date")),
        DateMiladi = S(e, "dateMi"), DateQamari = S(e, "dateQa"),
        Fuel = F(e, "fuel"),
        DayShift = ReadShiftData(e, "day"),
        NightShift = ReadShiftData(e, "night"),
    };

    /// <summary>
    /// ‎DB.shifts‎ — رکوردِ تختِ دیزل: خودش هم «پارچه» است هم «شیفت».
    /// شیفتِ شب در همان جای شب می‌نشیند تا شمارشِ گزارشِ ماه یکی بماند.
    /// </summary>
    private static ParchaReport ReadFlatShift(JsonElement e)
    {
        var sd = new ShiftData
        {
            Name = S(e, "name"), PumpNum = (int)D(e, "pumpNum"),
            Start = D(e, "start"), End = D(e, "end"), Price = D(e, "price"),
            ProfitPer = D(e, "profitPer"), BuyPerLiter = D(e, "buyPerLiter"),
            Sale = D(e, "sale"), Money = D(e, "money"), Debt = D(e, "debt"),
            Available = D(e, "available"), Profit = D(e, "profit"),
            Note = S(e, "note"), SavedAt = S(e, "date"),
        };
        var night = S(e, "type") == "night";
        return new ParchaReport
        {
            LegacyId = S(e, "id") ?? IdOfNumber(e),
            DateShamsi = S(e, "date"), DateKey = Key(S(e, "date")),
            DateMiladi = S(e, "dateMi"), DateQamari = S(e, "dateQa"),
            Fuel = F(e, "fuel"),
            DayShift = night ? null : sd,
            NightShift = night ? sd : null,
        };
    }

    /// <summary>شناسه در نسخهٔ وب گاهی عدد است (‎Date.now()‎) و گاهی رشته.</summary>
    private static string? IdOfNumber(JsonElement e) =>
        e.TryGetProperty("id", out var v) && v.ValueKind == JsonValueKind.Number
            ? v.ToString() : null;

    // ── مخزن و شرکت ─────────────────────────────────────────────────────────
    private static FuelPurchase ReadPurchase(JsonElement e) => new()
    {
        LegacyId = S(e, "id") ?? IdOfNumber(e),
        Fuel = F(e, "fuelType"),
        DateShamsi = S(e, "date"), DateKey = Key(S(e, "date")),
        Seller = S(e, "seller"), Kg = D(e, "kg"), Density = D(e, "density"),
        PriceTon = D(e, "priceTon"), UsdRate = D(e, "usdRate"),
        Ton = D(e, "ton"), Liters = D(e, "liters"),
        TotalUsd = D(e, "totalUSD"), TotalAfn = D(e, "totalAFN"),
        PerLiter = D(e, "perLiter"), Note = S(e, "note"),
    };

    private static TilCompany ReadCompany(JsonElement e)
    {
        var c = new TilCompany
        {
            LegacyId = S(e, "id") ?? IdOfNumber(e),
            Name = S(e, "name"), Note = S(e, "note"),
        };
        // دو دفترِ جدا در نسخهٔ وب (‎rows‎ و ‎dieselRows‎)، یک جدول با ستونِ سوخت
        // در نیتیو — همان جداییِ معنایی، بی دو جدولِ موازی.
        AddRows(c, e, "rows", FuelType.Petrol);
        AddRows(c, e, "dieselRows", FuelType.Diesel);
        return c;
    }

    private static void AddRows(TilCompany c, JsonElement e, string key, FuelType fuel)
    {
        if (!e.TryGetProperty(key, out var arr) || arr.ValueKind != JsonValueKind.Array) return;
        var i = 0;
        foreach (var r in arr.EnumerateArray())
            c.Rows.Add(new CompanyRow
            {
                Fuel = fuel, SortIndex = i++,
                DateShamsi = S(r, "date"), DateKey = Key(S(r, "date")),
                Name = S(r, "name"), Kg = D(r, "kg"), Ton = D(r, "ton"),
                Usd = D(r, "usd"), Rate = D(r, "rate"), Poul = D(r, "poul"),
                PayRate = DN(r, "payRate"),
                SourcePurchaseId = S(r, "srcPurchaseId") ?? NumStr(r, "srcPurchaseId"),
                SourceReceiptId = S(r, "srcReceiptId"),
                SourceExchangeId = S(r, "srcSarrafiId"),
                Note = S(r, "note"),
            });
    }

    private static string? NumStr(JsonElement e, string k) =>
        e.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.Number ? v.ToString() : null;

    private static TankDip ReadDip(JsonElement e, IReadOnlyDictionary<string, decimal> adjusts)
    {
        var id = S(e, "id") ?? "";
        var adjId = S(e, "adjId");
        var adjust = adjId is not null && adjusts.TryGetValue(id, out var a) ? a : 0m;
        return new TankDip
        {
            Fuel = F(e, "fuel"),
            DateShamsi = S(e, "date"), DateKey = Key(S(e, "date")), MonthKey = Mon(S(e, "date")),
            Measured = D(e, "actual"), Expected = D(e, "book"),
            BookAdjust = adjust, Note = S(e, "note"),
        };
    }

    private static TankerUnload ReadUnload(JsonElement e) => new()
    {
        Fuel = F(e, "fuel"),
        DateShamsi = S(e, "date"), DateKey = Key(S(e, "date")), MonthKey = Mon(S(e, "date")),
        Manifest = D(e, "manifest"), Actual = D(e, "actual"), Note = S(e, "note"),
    };

    private static RateHistoryEntry ReadRate(JsonElement e) => new()
    {
        Fuel = F(e, "fuel"), Rate = D(e, "rate"),
        DateShamsi = S(e, "date"), DateKey = Key(S(e, "date")), MonthKey = Mon(S(e, "date")),
    };

    // ── دفترهای کوچک ────────────────────────────────────────────────────────
    private static ExtraIncome ReadExtraIncome(JsonElement e) => new()
    {
        DateShamsi = S(e, "date"), DateKey = Key(S(e, "date")), MonthKey = Mon(S(e, "date")),
        Qty = D(e, "qty"), Buy = D(e, "buy"), Market = D(e, "market"),
        Seller = S(e, "seller"), Amount = D(e, "amount"), Note = S(e, "note"),
    };

    private static ParchaReceipt ReadParchaReceipt(JsonElement e) => new()
    {
        LegacyId = S(e, "id"),
        DateShamsi = S(e, "date"), DateKey = Key(S(e, "date")),
        Account = S(e, "account"), Name = S(e, "name"), Hawala = S(e, "hawala"),
        Liters = D(e, "fuel"), PricePerLiter = D(e, "priceper"),
        Rasid = D(e, "rasid"), Posted = B(e, "posted"),
    };

    private static DebtQuickReceipt ReadQuickReceipt(JsonElement e) => new()
    {
        LegacyId = S(e, "id") ?? "",
        DateShamsi = S(e, "date"), DateKey = Key(S(e, "date")), MonthKey = Mon(S(e, "date")),
        Account = S(e, "account"), Note = S(e, "note"), Amount = D(e, "amount"),
    };

    private static Camera ReadCamera(JsonElement e) => new()
    {
        LegacyId = S(e, "id"), Name = S(e, "name"), Url = S(e, "url"), Note = S(e, "note"),
    };

    // ── کارمندان ────────────────────────────────────────────────────────────
    private static StaffMember ReadStaff(JsonElement e) => new()
    {
        LegacyId = S(e, "id"), Name = S(e, "name"),
        ShiftIn = S(e, "shiftIn"), ShiftOut = S(e, "shiftOut"), Salary = D(e, "salary"),
    };

    private static LegacyAttendance ReadAttendance(JsonElement e) => new(
        S(e, "sid"),
        new AttendanceRow
        {
            DateShamsi = S(e, "date"), DateKey = Key(S(e, "date")),
            In = S(e, "in"), Out = S(e, "out"), Note = S(e, "note"),
        });

    private static LegacySalary ReadSalary(JsonElement e) => new(
        S(e, "sid"),
        new SalaryPayment
        {
            MonthKey = S(e, "month"), Amount = D(e, "amount"),
            DateShamsi = S(e, "date"), Note = S(e, "note"),
        });

    private static StaffShortSettle ReadSettle(JsonElement e) => new()
    {
        LegacyId = S(e, "id"), NameKey = S(e, "key"), Name = S(e, "name"),
        // ثبتِ بی‌نوع «رسیدِ کمبودی» است — همان ‎(s.type || 'short')‎ .
        Kind = S(e, "type") == "excess" ? StaffSettleKind.Excess : StaffSettleKind.Short,
        Amount = D(e, "amount"),
        DateShamsi = S(e, "date"), DateKey = Key(S(e, "date")), MonthKey = Mon(S(e, "date")),
    };

    // ── امانت ───────────────────────────────────────────────────────────────
    private static AmanatAccount ReadAmanat(JsonElement e)
    {
        var a = new AmanatAccount
        {
            LegacyId = S(e, "id"), Name = S(e, "name"), Fuel = F(e, "fuel"),
            MyPct = DN(e, "myPct"), Rate = DN(e, "rate"), Note = S(e, "note"),
        };
        if (e.TryGetProperty("rows", out var arr) && arr.ValueKind == JsonValueKind.Array)
        {
            var i = 0;
            foreach (var r in arr.EnumerateArray())
                a.Rows.Add(new AmanatRow
                {
                    SortIndex = i++,
                    DateShamsi = S(r, "date"), DateKey = Key(S(r, "date")),
                    Name = S(r, "name"), ToAccount = S(r, "toAcct"),
                    Liters = DN(r, "liters"), Taken = DN(r, "taken"),
                    Days = DN(r, "days"), Temp = DN(r, "temp"),
                    State = S(r, "state") == "closed" ? AmanatRowState.Closed : AmanatRowState.Open,
                    CloseDate = S(r, "closeDate"), Lid = S(r, "lid"),
                    BasePct = DN(r, "basePct"), Actual = DN(r, "actual"), Note = S(r, "note"),
                });
        }
        return a;
    }

    // ── ورق ─────────────────────────────────────────────────────────────────
    private static WaraqEntry ReadWaraq(JsonElement e)
    {
        var w = new WaraqEntry
        {
            LegacyId = S(e, "id"),
            DateShamsi = S(e, "date"), DateKey = Key(S(e, "date")),
            Station = S(e, "station"),
            ActiveShift = S(e, "active") == "night" ? ShiftKind.Night : ShiftKind.Day,
        };
        AddShift(w, e, "day", ShiftKind.Day);
        AddShift(w, e, "night", ShiftKind.Night);
        return w;
    }

    private static void AddShift(WaraqEntry w, JsonElement e, string key, ShiftKind kind)
    {
        if (!e.TryGetProperty(key, out var s) || s.ValueKind != JsonValueKind.Object) return;
        var sh = new WaraqShift
        {
            Kind = kind, WorkerName = S(s, "workerName"),
            FabricDebt = D(s, "fabricDebt"),
            FabricAvailable = D(s, "fabricAvailable"),
            AvailableFromShift = D(s, "availableFromShift"),
            PricePerLiter = D(s, "pricePerLiter"),
            PricePerLiterDiesel = D(s, "pricePerLiterDiesel"),
        };

        if (s.TryGetProperty("pumps", out var pumps) && pumps.ValueKind == JsonValueKind.Array)
        {
            var i = 0;
            foreach (var p in pumps.EnumerateArray())
                sh.Pumps.Add(new WaraqPump
                {
                    SortIndex = i++, Num = (int)D(p, "num"), Fuel = F(p, "fuel"),
                    Worker = S(p, "worker"), DateShamsi = S(p, "date"), Note = S(p, "note"),
                    Start = D(p, "start"), End = D(p, "end"),
                    PricePerLiter = D(p, "pricePerLiter"), Debt = D(p, "debt"),
                    SrcKey = S(p, "srcKey"),
                });
        }

        if (s.TryGetProperty("transactions", out var txns) && txns.ValueKind == JsonValueKind.Array)
        {
            var i = 0;
            foreach (var t in txns.EnumerateArray())
                sh.Transactions.Add(new WaraqTransaction
                {
                    SortIndex = i++, Name = S(t, "name"),
                    Liters = D(t, "liters"), Amount = D(t, "amount"),
                    Type = S(t, "type") == "expense" ? WaraqTxnType.Expense : WaraqTxnType.Debt,
                    Fuel = F(t, "fuel"),
                    // سه‌حالته می‌ماند: نبودنِ کلید یعنی «دادهٔ کهنه»، نه false.
                    AmountAuto = t.TryGetProperty("amountAuto", out var aa)
                                 && aa.ValueKind is JsonValueKind.True or JsonValueKind.False
                        ? aa.ValueKind == JsonValueKind.True
                        : null,
                });
        }

        w.Shifts.Add(sh);
    }

    // ── فاکتور ──────────────────────────────────────────────────────────────
    private static Invoice ReadInvoice(JsonElement e) => new()
    {
        LegacyId = S(e, "id"),
        InvoiceNumber = (int)D(e, "invoice_number"),
        Status = S(e, "status") == "approved" ? InvoiceStatus.Approved : InvoiceStatus.Pending,
        Fuel = F(e, "fuel_type"),
        DateShamsi = S(e, "date_sh"), DateKey = Key(S(e, "date_sh")),
        CustomerName = S(e, "customer_name"), DebtAlias = S(e, "debt_alias"),
        VehicleType = S(e, "vehicle_type"), Phone = S(e, "phone"),
        PricePerLiter = D(e, "price_per_liter"), Liters = D(e, "liters"),
        Amount = D(e, "amount"), ByMoney = B(e, "by_money"),
        RateOnCreate = DN(e, "rate_on_create"), RateOnApprove = DN(e, "rate_on_approve"),
        CreatedAtUtc = Utc(S(e, "created_at")),
        Note = S(e, "note"),
    };

    private static DateTime Utc(string? iso) =>
        DateTime.TryParse(iso, System.Globalization.CultureInfo.InvariantCulture,
                          System.Globalization.DateTimeStyles.AdjustToUniversal
                          | System.Globalization.DateTimeStyles.AssumeUniversal, out var d)
            ? d : DateTime.UtcNow;

    // ── تنظیم‌ها ────────────────────────────────────────────────────────────
    /// <summary>
    /// عددها و نام‌هایی که در نسخهٔ وب مستقیم روی خودِ ‎DB‎ می‌نشستند.
    ///
    /// ⚠️ اینها در شمارشِ رکورد نمی‌آیند ولی نبودنشان یعنی نرخِ اتحادیه صفر و
    /// فیِ خریدِ صفر — و از آن‌جا مفادِ غلط. پس همراهِ داده می‌آیند.
    /// </summary>
    private static Dictionary<string, string> ReadSettings(JsonElement root)
    {
        var keys = new[]
        {
            "stationName", "stationAddress", "stationPhone",
            "unionRatePetrol", "unionRateDiesel",
            "buyPerLiter_petrol", "buyPerLiter_diesel",
            "tankCapacity_petrol", "tankCapacity_diesel",
            "lowStockThreshold", "lowStockPhone", "syncCode",
        };
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var k in keys)
        {
            if (!root.TryGetProperty(k, out var v)) continue;
            var s = v.ValueKind switch
            {
                JsonValueKind.String => v.GetString(),
                JsonValueKind.Number => v.ToString(),
                _ => null,
            };
            if (!string.IsNullOrWhiteSpace(s)) map[k] = s!;
        }
        return map;
    }
}
