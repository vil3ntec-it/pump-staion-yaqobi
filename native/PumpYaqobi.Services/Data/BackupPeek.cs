namespace PumpYaqobi.Services.Data;

/// <summary>شمارِ یک جدول در بکاپ — با نامِ فارسیِ همان بخش.</summary>
public sealed record BackupPeekTable(string Table, string Label, long Count);

/// <summary>آن‌چه داخلِ یک بکاپ است — فقط خوانده، هیچ‌چیز نوشته نمی‌شود.</summary>
public sealed record BackupPeek(
    bool Ok, string Why, bool Integrity, IReadOnlyList<BackupPeekTable> Tables, long Total,
    string LatestWaraq, string LatestDebtRow, string LatestSafe)
{
    public long CountOf(string table) => Tables.FirstOrDefault(t => t.Table == table)?.Count ?? 0;
}

/// <summary>
/// ══ «مشاهدهٔ بکاپ» — دیدنِ محتوای بکاپ بی بازگردانی (۱۴۰۵/۰۷/۱۸) ══════════════
///
/// خواستهٔ صاحب ریپو: «امکانِ مشاهدهٔ بکاپ برای دیدنِ محتوای بکاپ بدون تغییر در
/// اطلاعاتِ فعلی». ⛔ فقط از <see cref="BackupService.OpenReadOnly"/> (‎Mode=ReadOnly‎،
/// ‎Pooling=false‎) — هیچ اتصالی به دفترِ زنده، هیچ نوشتنی روی خودِ بکاپ. همان
/// خواندن برای «چه چیزی جایگزین می‌شود»ِ بازیابی هم به کار می‌رود (فعلی ⇐ بکاپ).
/// </summary>
public static class BackupPeeker
{
    /// <summary>جدول‌های داده با نامِ فارسی، به ترتیبِ دیدن. بقیه زیرِ «سایر».</summary>
    public static readonly (string Table, string Label)[] Known =
    {
        ("Debtors", "قرض‌داران"),
        ("DebtAccounts", "حساب‌های قرض‌داران"),
        ("DebtRows", "ردیف‌های حساب‌ها"),
        ("WaraqEntries", "ورق‌های روزانه"),
        ("WaraqTransactions", "تراکنش‌های ورق"),
        ("Reports", "پارچه‌ها"),
        ("SafeEntries", "گاوصندوق"),
        ("Expenses", "مصارف"),
        ("ExchangeRows", "صرافی"),
        ("RetailRows", "چکنه"),
        ("TilCompanies", "شرکت‌های تیل"),
        ("CompanyRows", "ردیف‌های شرکت‌ها"),
        ("FuelPurchases", "خریدهای مخزن"),
        ("Invoices", "فاکتورها"),
        ("StaffMembers", "کارمندان"),
        ("Attendance", "حاضری"),
        ("SalaryPayments", "معاش‌ها"),
        ("StaffShortSettles", "رسیدهای کمبودی"),
        ("FuelConversions", "تبدیلِ تیل"),
        ("AmanatRows", "امانت"),
    };

    public static BackupPeek Read(string plainDbPath)
    {
        if (!File.Exists(plainDbPath))
            return Fail("فایل پیدا نشد: " + Path.GetFileName(plainDbPath));
        if (BackupService.Inspect(plainDbPath) < 0)
            return Fail("این فایل بکاپِ این برنامه نیست (یا خراب است)");
        var counts = FullBackup.CountRows(plainDbPath);
        if (counts is null) return Fail("فایل خوانده نشد");

        var tables = new List<BackupPeekTable>();
        foreach (var (t, label) in Known)
            if (counts.TryGetValue(t, out var n)) tables.Add(new BackupPeekTable(t, label, n));
        var known = Known.Select(k => k.Table).ToHashSet(StringComparer.Ordinal);
        var other = counts.Where(kv => !known.Contains(kv.Key) && !FullBackup.Housekeeping.Contains(kv.Key)
                                       && kv.Key is not ("Audit" or "Trash" or "SyncConflicts" or "Users"))
                          .Sum(kv => kv.Value);
        if (other > 0) tables.Add(new BackupPeekTable("", "سایر", other));

        return new BackupPeek(true, "", FullBackup.IntegrityOk(plainDbPath), tables, tables.Sum(t => t.Count),
            Latest(plainDbPath, "WaraqEntries"), Latest(plainDbPath, "DebtRows"), Latest(plainDbPath, "SafeEntries"));
    }

    private static BackupPeek Fail(string why) =>
        new(false, why, false, Array.Empty<BackupPeekTable>(), 0, "", "", "");

    /// <summary>تازه‌ترین تاریخِ یک جدول — از کلیدِ عددیِ تاریخ (متنِ کاربر قالبِ ثابت ندارد).</summary>
    private static string Latest(string path, string table)
    {
        try
        {
            using var con = BackupService.OpenReadOnly(path);
            using var cmd = con.CreateCommand();
            cmd.CommandText = $"SELECT DateShamsi FROM \"{table}\" WHERE DateKey > 0 ORDER BY DateKey DESC LIMIT 1;";
            return cmd.ExecuteScalar() as string ?? "";
        }
        catch { return ""; }
    }

    /// <summary>
    /// «چه چیزی جایگزین می‌شود» — هر جدول: فعلی ⇐ بکاپ، فقط آن‌هایی که فرق دارند.
    /// </summary>
    public static IReadOnlyList<string> Diff(BackupPeek live, BackupPeek backup)
    {
        var lines = new List<string>();
        foreach (var (t, label) in Known.Append(("", "سایر")))
        {
            var a = live.Tables.FirstOrDefault(x => x.Table == t)?.Count ?? 0;
            var b = backup.Tables.FirstOrDefault(x => x.Table == t)?.Count ?? 0;
            if (a != b) lines.Add($"{label}: {a:N0} ⇐ {b:N0}");
        }
        return lines;
    }
}
