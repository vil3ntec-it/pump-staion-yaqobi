using System.Text.Json;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;

namespace PumpYaqobi.Infrastructure.Migration;

public sealed record ImportReport(
    int Debtors, int SubAccounts, int DebtRows,
    int SafeEntries, int ExchangeRows, int Expenses, int RetailRows,
    IReadOnlyList<string> Warnings);

/// <summary>
/// ══ ابزارِ مهاجرت (بندِ ۳۷) ════════════════════════════════════════════════
/// فایلِ بکاپِ نسخهٔ HTML چیزی نیست جز ‎JSON.stringify(DB)‎ — همان شیءِ کاملِ
/// برنامه. این کلاس آن را می‌خواند و به موجودیت‌های نسخهٔ Native ترجمه می‌کند.
///
/// اصولی که این‌جا رعایت شده‌اند:
///   • هیچ چیزی حدس زده نمی‌شود: هر فیلدی که در JSON نبود، همان پیش‌فرضی را
///     می‌گیرد که خودِ HTML می‌داد (مثلاً ftype خالی ⇒ پطرول).
///   • عددها با ‎decimal‎ خوانده می‌شوند نه ‎double‎ — پول هرگز شناور نمی‌شود.
///   • دادهٔ خراب کلِ مهاجرت را نمی‌شکند: همان رکورد رد می‌شود و در Warnings
///     می‌آید، دقیقاً مثل کاری که خودِ HTML با کارتِ خراب می‌کرد.
///   • این ابزار فقط برای انتقال است؛ اجرای برنامه هیچ وابستگی‌ای به آن ندارد.
/// </summary>
public sealed class LegacyBackupImporter
{
    private readonly List<string> _warn = new();

    public (List<Debtor> Debtors, List<SafeEntry> Safe, List<ExchangeRow> Exchange,
            List<Expense> Expenses, List<RetailRow> Retail, ImportReport Report) Parse(string json)
    {
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var debtors = new List<Debtor>();
        foreach (var (key, noInv) in new[] { ("debtPersons", false), ("noinvPersons", true) })
            if (root.TryGetProperty(key, out var arr) && arr.ValueKind == JsonValueKind.Array)
                foreach (var p in arr.EnumerateArray())
                    try { debtors.Add(ReadDebtor(p, noInv)); }
                    catch (Exception ex) { _warn.Add($"قرض‌دار رد شد: {ex.Message}"); }

        var safe = ReadList(root, "safeEntries", ReadSafe);
        var exch = ReadList(root, "sarrafiRows", ReadExchange);
        var exp  = ReadList(root, "expenses", ReadExpense);
        var ret  = ReadList(root, "chakanaRows", ReadRetail);

        int subs = debtors.Sum(d => d.SubAccounts.Count);
        int rows = debtors.Sum(d => d.AllAccounts().Sum(a => a.FuelRows.Count + a.MoneyRows.Count));
        var rep = new ImportReport(debtors.Count, subs, rows, safe.Count, exch.Count, exp.Count, ret.Count, _warn);
        return (debtors, safe, exch, exp, ret, rep);
    }

    private List<T> ReadList<T>(JsonElement root, string key, Func<JsonElement, T> read)
    {
        var list = new List<T>();
        if (!root.TryGetProperty(key, out var arr) || arr.ValueKind != JsonValueKind.Array) return list;
        foreach (var e in arr.EnumerateArray())
            try { list.Add(read(e)); }
            catch (Exception ex) { _warn.Add($"{key}: یک رکورد رد شد ({ex.Message})"); }
        return list;
    }

    // ── همان کمکی‌ها، برای نیمهٔ دومِ مهاجرت ─────────────────────────────────
    // ‎LegacyOperationsImporter‎ در همین اسمبلی است و باید همین قاعده‌ها را
    // به کار ببرد، نه رونوشتِ دومی از آن‌ها که روزی از هم دور بیفتند.
    internal static string? Str2(JsonElement e, string k) => Str(e, k);
    internal static decimal Dec2(JsonElement e, string k) => Dec(e, k);
    internal static decimal? DecOrNull2(JsonElement e, string k) => DecOrNull(e, k);
    internal static bool Bool2(JsonElement e, string k) => Bool(e, k);

    // ── خواننده‌های کمکی: هر کدام دقیقاً همان پیش‌فرضِ HTML را می‌دهند ────────
    private static string? Str(JsonElement e, string k)
        => e.TryGetProperty(k, out var v) && v.ValueKind is JsonValueKind.String ? v.GetString() : null;

    /// <summary>‎parseFloat(x) || 0‎ — رشته یا عدد، هر دو؛ نامعتبر ⇒ صفر.</summary>
    private static decimal Dec(JsonElement e, string k)
    {
        if (!e.TryGetProperty(k, out var v)) return 0m;
        return v.ValueKind switch
        {
            JsonValueKind.Number => v.TryGetDecimal(out var d) ? d : 0m,
            JsonValueKind.String => decimal.TryParse(v.GetString(),
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var s) ? s : 0m,
            _ => 0m
        };
    }

    private static decimal? DecOrNull(JsonElement e, string k)
    {
        if (!e.TryGetProperty(k, out var v)) return null;
        if (v.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) return null;
        if (v.ValueKind == JsonValueKind.String && string.IsNullOrWhiteSpace(v.GetString())) return null;
        return Dec(e, k);
    }

    private static bool Bool(JsonElement e, string k)
        => e.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.True;

    /// <summary>
    /// کلیدِ عددیِ تاریخِ شمسی: سال×۱۰۰۰۰ + ماه×۱۰۰ + روز.
    /// همان ‎_dateSortKey‎ی که HTML داشت — چون مقایسهٔ رشته‌ای روزهای تک‌رقمی را
    /// غلط می‌چیند و ردیف‌ها جابه‌جا می‌شوند.
    /// </summary>
    public static int DateKeyOf(string? shamsi)
    {
        if (string.IsNullOrWhiteSpace(shamsi)) return 0;
        var m = System.Text.RegularExpressions.Regex.Match(ToEnDigits(shamsi), @"(\d+)\D+(\d+)\D+(\d+)");
        if (!m.Success) return 0;
        return int.Parse(m.Groups[1].Value) * 10000 + int.Parse(m.Groups[2].Value) * 100 + int.Parse(m.Groups[3].Value);
    }

    /// <summary>«۱۴۰۵/۶» — کلیدِ ماه برای فیلترِ ماهانه (‎safeMonthKey‎ و مانندِ آن).</summary>
    public static string? MonthKeyOf(string? shamsi)
    {
        var k = DateKeyOf(shamsi);
        return k == 0 ? null : $"{k / 10000:0000}/{(k / 100) % 100:00}";
    }

    private static string ToEnDigits(string s)
    {
        var b = new System.Text.StringBuilder(s.Length);
        foreach (var c in s)
            b.Append(c >= '۰' && c <= '۹' ? (char)('0' + (c - '۰'))
                   : c >= '٠' && c <= '٩' ? (char)('0' + (c - '٠')) : c);
        return b.ToString();
    }

    private Debtor ReadDebtor(JsonElement p, bool noInvoice)
    {
        var d = new Debtor
        {
            LegacyId = Str(p, "id") ?? Guid.NewGuid().ToString("N"),
            Name = Str(p, "name") ?? string.Empty,
            Phone = Str(p, "phone"),
            BuyFeeNote = Str(p, "buyFeeNote"),
            IsNoInvoice = noInvoice,
        };
        // ⚠️ در HTML خودِ شیءِ شخص، «حسابِ اصلی» هم بود — پس همان فیلدها را دارد.
        d.MainAccount = ReadAccount(p, null, null);
        if (p.TryGetProperty("subs", out var subs) && subs.ValueKind == JsonValueKind.Array)
            foreach (var s in subs.EnumerateArray())
            {
                if (s.ValueKind != JsonValueKind.Object) continue;
                // ‎_migrateAcctModes‎ — حسابِ فرعیِ بی‌واحد، واحدِ شخص را
                // می‌گیرد. بی این، حسابِ فرعیِ «واحد پول» پس از مهاجرت «تیل»
                // می‌شد و دفترِ پولش از جلوی چشمِ کاربر غیب می‌شد (خودِ
                // ردیف‌ها سرِ جایشان بودند، ولی دیده نمی‌شدند).
                d.SubAccounts.Add(ReadAccount(s, Str(s, "id") ?? Guid.NewGuid().ToString("N"),
                                              d.MainAccount.Mode));
            }
        return d;
    }

    /// <param name="inheritMode">
    /// واحدی که اگر خودِ حساب واحد نداشته باشد به ارث می‌رسد — برای
    /// حساب‌های فرعیِ دادهٔ قدیمی. برای حسابِ اصلی ‎null‎ است.
    /// </param>
    private DebtAccount ReadAccount(JsonElement a, string? subId, LedgerMode? inheritMode)
    {
        var rawMode = Str(a, "mode");
        var acc = new DebtAccount
        {
            LegacySubId = subId,
            Name = Str(a, "name"),
            Note = Str(a, "note"),
            Mode = string.IsNullOrEmpty(rawMode) && inheritMode is { } m
                ? m
                : LedgerModeExtensions.FromLegacy(rawMode),
            MoneyDeposit = DecOrNull(a, "moneyDeposit"),
            PercentPetrol = DecOrNull(a, "percentP"),
            PercentDiesel = DecOrNull(a, "percentD"),
            PercentLegacy = DecOrNull(a, "percent"),
            RasidFuelPetrol = Dec(a, "rasidFuelP"),
            RasidFuelDiesel = Dec(a, "rasidFuelD"),
            RasidMoneyPetrol = Dec(a, "rasidMoneyP"),
            RasidMoneyDiesel = Dec(a, "rasidMoneyD"),
        };
        ReadRows(a, "rows", acc.FuelRows, false);
        ReadRows(a, "moneyRows", acc.MoneyRows, true);
        return acc;
    }

    private void ReadRows(JsonElement a, string key, List<DebtRow> into, bool money)
    {
        if (!a.TryGetProperty(key, out var arr) || arr.ValueKind != JsonValueKind.Array) return;
        int i = 0;
        foreach (var r in arr.EnumerateArray())
        {
            if (r.ValueKind != JsonValueKind.Object) continue;
            var date = Str(r, "date");
            into.Add(new DebtRow
            {
                SortIndex = i++,
                DateShamsi = date,
                DateKey = DateKeyOf(date),
                Name = Str(r, "name"),
                Hawala = Str(r, "hawala"),
                Fuel = FuelTypeExtensions.FromLegacy(Str(r, "ftype")),
                Liters = Dec(r, "fuel"),
                PricePerLiter = DecOrNull(r, "priceper"),
                Bardagi = Dec(r, "bardagi"),
                Rasid = Dec(r, "rasid"),
                RasidFuel = Dec(r, "rasidFuel"),
                Albaqi = Dec(r, "albaqi"),
                ByMoney = money || Bool(r, "byMoney"),
                // ⚠️ «از کجا آمده» باید بیاید: ردیفی که سایت از ورق ساخته بود
                // کلیدِ منبعش را دارد، و همگام‌سازیِ ورق در نیتیو با همان کلید
                // پیدایش می‌کند. بی این دو، همان ردیف بارِ دوم ساخته می‌شد و
                // قرضِ طرف دو برابر دیده می‌شد.
                Src = Str(r, "src"),
                SrcKey = Str(r, "srcKey"),
            });
        }
    }

    private static SafeEntry ReadSafe(JsonElement e)
    {
        var date = Str(e, "date");
        return new SafeEntry
        {
            DateShamsi = date, DateKey = DateKeyOf(date), MonthKey = MonthKeyOf(date),
            Kind = Str(e, "type") == "mandagi" ? SafeEntryKind.Mandagi : SafeEntryKind.Bardagi,
            Title = Str(e, "title"),
            Amount = Dec(e, "amount"),
            Currency = Str(e, "currency") == "usd" ? Currency.Usd : Currency.Afn,
            Note = Str(e, "note"),
        };
    }

    private static ExchangeRow ReadExchange(JsonElement e)
    {
        var date = Str(e, "date");
        return new ExchangeRow
        {
            DateShamsi = date, DateKey = DateKeyOf(date), MonthKey = MonthKeyOf(date),
            Description = Str(e, "desc"),
            Amount = Dec(e, "amount"),
            Currency = Str(e, "currency") switch
            {
                "kaldar" => ExchangeCurrency.Kaldar,
                "afghani" => ExchangeCurrency.Afghani,
                _ => ExchangeCurrency.Toman
            },
            Rate = Dec(e, "rate"),
            Bardagi = Dec(e, "bardagi"),
        };
    }

    private static Expense ReadExpense(JsonElement e)
    {
        var date = Str(e, "date");
        return new Expense
        {
            DateShamsi = date, DateKey = DateKeyOf(date), MonthKey = MonthKeyOf(date),
            Title = Str(e, "title"), Amount = Dec(e, "amount"), Note = Str(e, "note"),
            // مصرفی که از ورق آمده کلیدِ منبع دارد — همان کلید در نیتیو هم
            // شناخته می‌شود تا مصرف دوباره ساخته نشود.
            SrcKey = Str(e, "srcKey"),
        };
    }

    private static RetailRow ReadRetail(JsonElement e)
    {
        var date = Str(e, "date");
        return new RetailRow
        {
            DateShamsi = date, DateKey = DateKeyOf(date), MonthKey = MonthKeyOf(date),
            Name = Str(e, "name"),
            Fuel = FuelTypeExtensions.FromLegacy(Str(e, "ftype")),
            Liters = Dec(e, "fuel"),
            PricePerLiter = Dec(e, "priceper"),
            Rasid = Dec(e, "rasid"),
        };
    }
}
