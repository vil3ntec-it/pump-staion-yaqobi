using PumpYaqobi.Application.Localization;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;

namespace PumpYaqobi.Application.Services;

/// <summary>حسابِ پیداشده برای یک متن — شخص و همان حسابی که ردیف باید در آن بنشیند.</summary>
public readonly record struct AccountMatch(Debtor Person, DebtAccount Account);

/// <summary>نتیجهٔ ثبتِ یک رسید در حساب.</summary>
public enum PostResult
{
    /// <summary>ثبت شد.</summary>
    Ok,
    /// <summary>همین رسید پیش‌تر از راهِ ورق واردِ حساب شده.</summary>
    Duplicate,
    /// <summary>حسابی با این نام پیدا نشد.</summary>
    NotFound,
}

/// <summary>
/// ══ بردنِ یک ردیف به حسابِ صاحبش ═══════════════════════════════════════════
/// رونوشتِ ‎normFa‎ · ‎findDebtAcctForText‎ · ‎_subForText‎ · ‎placePersonRow‎ ·
/// ‎alreadyReceivedFromWaraq‎ · ‎_detectFuelType‎ · ‎_stripFuelWords‎.
///
/// این لایه بینِ «رسید پارچه‌ها» و «ورق روزانه» مشترک است — هر دو ردیفشان را
/// از همین راه به حسابِ قرض‌دار می‌رسانند، پس یک نسخه بیشتر ندارد.
/// </summary>
public sealed class PostingService
{
    // ── نرمال‌سازیِ متن ───────────────────────────────────────────────────────

    /// <summary>‎normFa‎ — رقمِ لاتین، ی/ک فارسی، بی‌اعراب، فاصله‌های یکی.</summary>
    public static string NormFa(string? s)
    {
        var t = Shamsi.ToEnDigits(s ?? "")
            .Replace('ي', 'ی')   // ي ← ی
            .Replace('ك', 'ک');  // ك ← ک

        var b = new System.Text.StringBuilder(t.Length);
        var lastSpace = false;
        foreach (var c in t)
        {
            // اعرابِ عربی و نیم‌فاصله (‎[‌ً-ْ]‎ در نسخهٔ وب) کنار گذاشته می‌شوند
            if (c == '‌' || (c >= 'ً' && c <= 'ْ')) continue;
            if (char.IsWhiteSpace(c))
            {
                if (!lastSpace) { b.Append(' '); lastSpace = true; }
                continue;
            }
            lastSpace = false;
            b.Append(char.ToLowerInvariant(c));
        }
        return b.ToString().Trim();
    }

    /// <summary>‎_stripFuelWords‎ — نامِ نمایشی نباید «دیزل/پطرول» داشته باشد.</summary>
    public static string StripFuelWords(string? text)
    {
        var t = text ?? "";
        foreach (var w in new[] { "دیزل", "گازوییل", "گازوئیل", "پطرول", "پترول", "بنزین" })
            t = t.Replace(w, " ");
        return System.Text.RegularExpressions.Regex.Replace(t, @"\s{2,}", " ").Trim();
    }

    /// <summary>‎_detectFuelType‎ — پیش‌فرض پطرول است.</summary>
    public static FuelType DetectFuelType(string? text)
    {
        var n = NormFa(text);
        return n.Contains("دیزل") || n.Contains("گازوییل") || n.Contains("گازوئیل")
            ? FuelType.Diesel : FuelType.Petrol;
    }

    // ── پیدا کردنِ حساب ───────────────────────────────────────────────────────

    /// <summary>
    /// ‎findDebtAcctForText‎ — تطبیقِ دوطرفهٔ نام. سه پله امتیاز دارد و
    /// حساب‌های فرعی زودتر بررسی می‌شوند تا در تساوی، فرعیِ دقیق‌تر برنده شود.
    /// </summary>
    public static AccountMatch? FindAccountForText(IEnumerable<Debtor> people,
                                                   string? rawText, string? cleanName)
    {
        var rawSet = NormFa(rawText).Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
        var qTokens = NormFa(string.IsNullOrWhiteSpace(cleanName) ? rawText : cleanName)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(x => x.Length >= 2).ToArray();

        int Score(string? acctName)
        {
            var an = NormFa(acctName);
            if (an.Length == 0) return 0;
            var aTokens = an.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (aTokens.Length == 0) return 0;
            if (qTokens.Length > 0 && string.Join(' ', aTokens) == string.Join(' ', qTokens))
                return 3000 + an.Length;                                       // برابر
            if (aTokens.All(rawSet.Contains)) return 2000 + an.Length;         // متن ⊇ نامِ حساب
            if (qTokens.Length > 0 && qTokens.All(aTokens.Contains))
                return 1000 + an.Length;                                       // نامِ حساب ⊇ تایپ
            return 0;
        }

        AccountMatch? best = null;
        var bestScore = 0;
        foreach (var p in people)
        {
            if (p is null) continue;
            foreach (var s in p.SubAccounts)
            {
                var sc = Score(s?.Name);
                if (sc > bestScore) { bestScore = sc; best = new AccountMatch(p, s!); }
            }
            var pc = Score(p.Name);
            if (pc > bestScore) { bestScore = pc; best = new AccountMatch(p, p.MainAccount); }
        }
        return best;
    }

    /// <summary>‎_subForText‎ — اگر نامِ یک حسابِ فرعی داخلِ متن باشد، همان.</summary>
    public static DebtAccount? SubForText(Debtor person, string? text)
    {
        if (person.SubAccounts.Count == 0 || string.IsNullOrWhiteSpace(text)) return null;
        var hay = " " + NormFa(text) + " ";
        DebtAccount? best = null;
        var bestLen = 0;
        foreach (var s in person.SubAccounts)
        {
            var nm = NormFa(s?.Name);
            if (nm.Length > bestLen && hay.Contains(nm) && nm.Length > 0) { best = s; bestLen = nm.Length; }
        }
        return best;
    }

    // ── ثبت ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// ‎alreadyReceivedFromWaraq‎ — همین رسید پیش‌تر از ورق واردِ حساب شده؟
    /// حوالهٔ خالی یعنی «حواله را نادیده بگیر».
    /// </summary>
    /// <summary>نتیجهٔ ‎extractHawala‎ — شمارهٔ حواله و متنِ بی‌حواله.</summary>
    public readonly record struct HawalaText(string Num, string Clean);

    /// <summary>«بدون حواله» — همان ‎NO_HAWALA‎ی سایت.</summary>
    public const string NoHawala = "بدون حواله";

    /// <summary>
    /// ‎extractHawala(text)‎ — شمارهٔ حواله را از دلِ جمله بیرون می‌کشد و
    /// خودش را از متن برمی‌دارد، تا در جدولِ قرض‌دار «فلانی حواله ۱۲» نوشته
    /// نشود؛ نام در ستونِ نام بنشیند و شماره در ستونِ حواله.
    ///
    /// دو الگو، مو‌به‌مو مثلِ سایت:
    ///   • «حواله [نمبر|شماره] [:|#] ۱۲» ⇒ شماره
    ///   • «بدون حواله» / «بی حواله» / «حواله ندارد|نیست|نداره» ⇒ ‎NoHawala‎
    /// </summary>
    public static HawalaText ExtractHawala(string? text)
    {
        var t = Localization.Shamsi.ToEnDigits(text ?? "");

        var m = System.Text.RegularExpressions.Regex.Match(
            t, @"حواله\s*(?:نمبر|شماره)?\s*[:#]?\s*(\d+)");
        if (m.Success)
            return new HawalaText(m.Groups[1].Value, Tidy(t.Replace(m.Value, "")));

        var nm = System.Text.RegularExpressions.Regex.Match(
            t, @"(?:بدون|بی[ \u200c]?)\s*(?:نمبر|شماره)?\s*حواله|حواله\s*(?:ای)?\s*(?:ندار[دم]|نیست|نداره)");
        if (nm.Success)
            return new HawalaText(NoHawala, Tidy(t.Replace(nm.Value, "")));

        return new HawalaText("", t.Trim());
    }

    private static string Tidy(string s) =>
        System.Text.RegularExpressions.Regex.Replace(s, @"\s{2,}", " ").Trim();

    public static bool AlreadyReceivedFromWaraq(Debtor person, decimal liters,
                                                decimal bardagi, string? hawala)
    {
        var b = Math.Round(bardagi, 0, MidpointRounding.AwayFromZero);
        var h = (hawala ?? "").Trim();
        return person.AllAccounts()
            .SelectMany(a => a.FuelRows.Concat(a.MoneyRows))
            .Any(r => r.Src == "waraq"
                   && r.Liters == liters
                   && Math.Round(r.Bardagi, 0, MidpointRounding.AwayFromZero) == b
                   && (h.Length == 0 || (r.Hawala ?? "").Trim() == h));
    }

    /// <summary>
    /// ‎placePersonRow‎ — ردیف را در دفترِ درست می‌نشاند.
    ///
    /// سه قاعده که هر سه از باگ‌های واقعی آمده‌اند:
    ///   ۱) اگر همان ‎SrcKey‎ در دفترِ دیگری از همین شخص بود، از آن‌جا برداشته
    ///      می‌شود — وگرنه با عوض کردنِ «واحد» ردیف دو تا می‌شد.
    ///   ۲) رسیدِ دستیِ کاربر روی ردیفِ هم‌منبع حفظ می‌شود، نه پاک.
    ///   ۳) اگر ردیفِ کاملاً خالی در دفتر باشد، همان پر می‌شود.
    /// </summary>
    /// <summary>
    /// ‎removePersonRowBySrcKey(srcKey)‎ — ردیفِ ساخته‌شده از یک منبع را از هر
    /// دو دفترِ همهٔ حساب‌های شخص برمی‌دارد.
    ///
    /// برای «برگرداندنِ» یک رسید لازم است: خودِ رسید که پاک می‌شود، اثرش هم
    /// باید از حساب برود — وگرنه پول در حساب می‌مانَد و در هیچ فهرستی دیده
    /// نمی‌شود.
    /// </summary>
    /// <returns>ردیف‌هایی که برداشته شدند — تا فراخوان بتواند از دیتابیس هم پاکشان کند.</returns>
    public static List<DebtRow> RemoveRowsBySrcKey(IEnumerable<Debtor> people, string? srcKey)
    {
        var gone = new List<DebtRow>();
        if (string.IsNullOrEmpty(srcKey)) return gone;

        foreach (var p in people)
        {
            if (p is null) continue;
            foreach (var a in p.AllAccounts())
                foreach (var list in new[] { a.FuelRows, a.MoneyRows })
                {
                    gone.AddRange(list.Where(r => r.SrcKey == srcKey));
                    list.RemoveAll(r => r.SrcKey == srcKey);
                }
        }
        return gone;
    }

    public static DebtRow PlaceRow(Debtor person, DebtRow data, string? descText,
                                   DebtAccount? targetAccount, bool intoMoneyLedger = false)
    {
        var acct = targetAccount is not null && !ReferenceEquals(targetAccount, person.MainAccount)
            ? targetAccount
            : SubForText(person, descText) ?? person.MainAccount;

        var arr = intoMoneyLedger ? acct.MoneyRows : acct.FuelRows;

        if (!string.IsNullOrEmpty(data.SrcKey))
        {
            decimal movedRasid = 0, movedRasidFuel = 0;
            foreach (var a in person.AllAccounts())
            {
                foreach (var list in new[] { a.FuelRows, a.MoneyRows })
                {
                    if (ReferenceEquals(list, arr)) continue;
                    foreach (var r in list.Where(r => r.SrcKey == data.SrcKey))
                    {
                        if (movedRasid == 0) movedRasid = r.Rasid;
                        if (movedRasidFuel == 0) movedRasidFuel = r.RasidFuel;
                    }
                    list.RemoveAll(r => r.SrcKey == data.SrcKey);
                }
            }

            var ex = arr.FirstOrDefault(r => r.SrcKey == data.SrcKey);
            if (ex is not null)
            {
                var keepRasid = ex.Rasid != 0 ? ex.Rasid : movedRasid;
                var keepRasidFuel = ex.RasidFuel != 0 ? ex.RasidFuel : movedRasidFuel;
                CopyInto(data, ex);
                ex.Rasid = keepRasid;
                if (keepRasidFuel != 0) ex.RasidFuel = keepRasidFuel;
                ex.Albaqi = ex.Bardagi - keepRasid;
                return ex;
            }

            if (movedRasid != 0 || movedRasidFuel != 0)
            {
                data.Rasid = movedRasid;
                if (movedRasidFuel != 0) data.RasidFuel = movedRasidFuel;
                data.Albaqi = data.Bardagi - movedRasid;
            }
        }

        var empty = arr.FirstOrDefault(r =>
            string.IsNullOrWhiteSpace(r.Name) && string.IsNullOrWhiteSpace(r.Hawala)
            && r.Liters == 0 && r.Bardagi == 0 && r.Rasid == 0);
        if (empty is not null) { CopyInto(data, empty); return empty; }

        data.SortIndex = arr.Count;
        arr.Add(data);
        return data;
    }

    /// <summary>مقدارهای ردیفِ تازه روی ردیفِ موجود — کلیدها دست نمی‌خورند.</summary>
    private static void CopyInto(DebtRow from, DebtRow to)
    {
        to.DateShamsi = from.DateShamsi; to.DateKey = from.DateKey;
        to.Name = from.Name; to.Hawala = from.Hawala; to.Fuel = from.Fuel;
        to.Liters = from.Liters; to.PricePerLiter = from.PricePerLiter;
        to.Bardagi = from.Bardagi; to.Rasid = from.Rasid; to.RasidFuel = from.RasidFuel;
        to.Albaqi = from.Albaqi; to.ByMoney = from.ByMoney;
        to.Src = from.Src; to.SrcKey = from.SrcKey;
    }

    /// <summary>‎_fuelBardagi‎ — بردگیِ حالتِ تیل: مقدار تیل × فی، همیشه زنده.</summary>
    public static decimal FuelBardagi(decimal liters, decimal pricePerLiter) => liters * pricePerLiter;
}
