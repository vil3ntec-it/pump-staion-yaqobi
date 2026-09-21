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

    // ══ نامِ سوخت در دلِ جمله ═══════════════════════════════════════════════
    //
    // خواستهٔ صریحِ صاحب ریپو (۱۴۰۵/۰۷/۰۶): «ستونِ نوعِ تیل از تراکنش‌ها حذف
    // بشه، ولی اگر توی نام پطرول یا دیزل نوشتم توی حسابِ یارو همان انتخاب
    // بشه… و اگر «پ» خالی نوشتم پطرول ذخیره بشه و اگر «د» خالی نوشتم دیزل —
    // هر جای جمله، وسط یا اول یا آخر، فرقی نکنه. و توی نام‌ها و توضیحاتِ
    // همان حساب دیده نشه.»

    /// <summary>واژه‌های کاملِ سوخت — همان‌های نسخهٔ وب.</summary>
    private static readonly string[] DieselWords = { "دیزل", "گازوییل", "گازوئیل" };
    private static readonly string[] PetrolWords = { "پطرول", "پترول", "بنزین" };

    /// <summary>
    /// نشانهٔ <b>تک‌حرفی</b> — و فقط وقتی خودش یک واژهٔ کامل باشد.
    ///
    /// ⛔ زیررشته‌ای سنجیده نمی‌شود و نباید بشود: «د» داخلِ «داوود» و «پ»
    /// داخلِ «پرویز» است. یعنی با سنجشِ زیررشته‌ای، نامِ نیمِ قرض‌دارها
    /// «دیزل» خوانده می‌شد و از نامِ نمایشیِ حسابشان هم یک حرف پاک می‌شد.
    /// </summary>
    private static bool IsDieselMark(string token) => token == "د";
    private static bool IsPetrolMark(string token) => token == "پ";

    private static string[] Tokens(string? text) =>
        NormFa(text).Split(' ', StringSplitOptions.RemoveEmptyEntries);

    /// <summary>
    /// ‎_stripFuelWords‎ — نامِ نمایشی نباید «دیزل/پطرول» داشته باشد، و از
    /// امروز نشانهٔ تک‌حرفی («پ» / «د») را هم ندارد.
    /// </summary>
    public static string StripFuelWords(string? text)
    {
        var t = text ?? "";
        foreach (var w in DieselWords.Concat(PetrolWords)) t = t.Replace(w, " ");

        // و نشانهٔ تک‌حرفی، واژه‌به‌واژه — نه با ‎Replace‎، وگرنه یک حرف از
        // دلِ نامِ آدم‌ها برداشته می‌شد.
        var kept = t.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
                    .Where(w => { var n = NormFa(w); return !IsDieselMark(n) && !IsPetrolMark(n); });

        return System.Text.RegularExpressions.Regex
            .Replace(string.Join(' ', kept), @"\s{2,}", " ").Trim();
    }

    /// <summary>
    /// ‎_detectFuelType‎ — پیش‌فرض پطرول است.
    ///
    /// ⚠️ ترتیب مهم است: <b>واژهٔ کامل</b> جلوتر از نشانهٔ تک‌حرفی است، پس
    /// «دیزل پ» دیزل می‌ماند. و دیزل جلوتر از پطرول سنجیده می‌شود — همان
    /// ترتیبِ نسخهٔ وب، دست‌نخورده.
    /// </summary>
    public static FuelType DetectFuelType(string? text)
    {
        var n = NormFa(text);
        foreach (var w in DieselWords) if (n.Contains(w)) return FuelType.Diesel;
        foreach (var w in PetrolWords) if (n.Contains(w)) return FuelType.Petrol;

        foreach (var tok in Tokens(text))
        {
            if (IsDieselMark(tok)) return FuelType.Diesel;
            if (IsPetrolMark(tok)) return FuelType.Petrol;
        }
        return FuelType.Petrol;
    }

    /// <summary>متن اصلاً نامِ سوختی در خود دارد؟ (واژهٔ کامل یا نشانهٔ تک‌حرفی)</summary>
    public static bool MentionsFuel(string? text)
    {
        var n = NormFa(text);
        foreach (var w in DieselWords.Concat(PetrolWords)) if (n.Contains(w)) return true;
        foreach (var tok in Tokens(text)) if (IsDieselMark(tok) || IsPetrolMark(tok)) return true;
        return false;
    }

    // ══ واحدِ حساب: تیل یا پول ═══════════════════════════════════════════════

    /// <summary>
    /// خواستهٔ صریحِ صاحب ریپو (۱۴۰۵/۰۷/۰۶): «اسمِ قرض‌دار رو توی نامِ
    /// تراکنش‌ها می‌نویسم و سیستم اتومات تشخیص بده که واحدِ این حساب تیل است
    /// یا پول… و اگه هر دو بود — هم واحدِ تیل داشت هم واحدِ پول — این تغییر
    /// نخوره و میرزا خودش تغییر بده.»
    /// <para>
    /// ⛔ <c>null</c> بودنِ حالتِ «هر دو» جانِ این قاعده است: حسابی که در هر
    /// دو دفتر ردیف دارد، خودش تصمیمِ ردیفِ تازه را نمی‌گوید و حدس زدنش یعنی
    /// یک قرضِ تیل در دفترِ پول (یا برعکس) — همان «حتی یک عدد هم اشتباه به
    /// حساب نره».
    /// </para>
    /// <para>
    /// ⚠️ و حسابِ <b>خالی</b> (هیچ ردیفی در هیچ دفتر) واحدِ اعلامیِ خودش را
    /// می‌دهد (<see cref="DebtAccount.Mode"/>) — چیزی که کاربر هنگامِ ساختنِ
    /// حساب انتخاب کرده، نه یک پیش‌فرضِ کورکورانه.
    /// </para>
    /// </summary>
    /// <returns>واحدی که باید نشانده شود، یا <c>null</c> یعنی <b>دست نزن</b>.</returns>
    public static LedgerMode? UnitForAccount(bool hasFuelRows, bool hasMoneyRows, LedgerMode mode)
    {
        if (hasFuelRows && hasMoneyRows) return null;
        if (hasFuelRows) return LedgerMode.Fuel;
        if (hasMoneyRows) return LedgerMode.Money;
        return mode;
    }

    /// <summary>
    /// نوعِ سوختِ یک ردیف: اگر خودِ متن گفته باشد همان، وگرنه چیزی که کاربر
    /// در ستونِ «نوع تیل» انتخاب کرده.
    ///
    /// ⚠️ نسخهٔ وب فقط متن را می‌خواند (‎_detectFuelType(t.name)‎) و ستونِ
    /// «نوع تیل»ِ همان ردیف را نادیده می‌گرفت؛ یعنی اگر کاربر «دیزل» را از
    /// کشو برمی‌داشت ولی در نام نمی‌نوشت، ردیف در حسابِ قرض‌دار «پطرول»
    /// می‌نشست. آن یک باگ است و کپی نشد — وقتی متن چیزی نگفته، انتخابِ خودِ
    /// کاربر معتبر است.
    /// </summary>
    public static FuelType FuelTypeFromText(string? text, FuelType fallback) =>
        MentionsFuel(text) ? DetectFuelType(text) : fallback;

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
        var acct = ResolveAccount(person, descText, targetAccount);

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

        var empty = FirstBlank(arr);
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

    // ── ردیفِ خالی — یک قاعده، یک جا ─────────────────────────────────────

    /// <summary>
    /// ردیفی که هیچ چیزی در آن نوشته نشده — جای طبیعیِ ردیفِ خودکارِ تازه.
    ///
    /// ⚠️ <b>تاریخ شمرده نمی‌شود</b>: «➕ ردیف» خودش تاریخِ امروز را می‌گذارد،
    /// پس ردیفِ خالیِ تازه هم تاریخ دارد. اگر تاریخ شمرده شود، هیچ ردیفِ
    /// خالی‌ای پیدا نمی‌شود و همه‌چیز ته جدول می‌رود — همان شکایتِ صاحب ریپو.
    ///
    /// ⛔ <b>‎RasidFuel‎ هم شمرده می‌شود</b>، و این یک باگِ واقعی را می‌بندد:
    /// رسیدِ تیلِ سربرگ ردیفی می‌سازد که جز ‎RasidFuel‎ همه‌چیزش صفر است. با
    /// قاعدهٔ پیشین، اولین ردیفِ خودکارِ ورق روی همان می‌نشست و <b>رسیدِ
    /// تیلِ مشتری پاک می‌شد</b> — بی هیچ صدایی.
    ///
    /// ⛔ <b>ردیفی که کلیدِ منبع دارد خالی نیست</b>، حتی اگر همهٔ خانه‌هایش
    /// صفر باشد: مالِ منبعِ دیگری است و با پر شدن، آن منبع دو ردیف پیدا می‌کرد.
    /// </summary>
    public static bool IsBlankRow(DebtRow r) =>
        string.IsNullOrWhiteSpace(r.Name) && string.IsNullOrWhiteSpace(r.Hawala)
        && r.Liters == 0m && r.Bardagi == 0m && r.Rasid == 0m && r.RasidFuel == 0m
        && string.IsNullOrEmpty(r.SrcKey);

    /// <summary>
    /// نخستین ردیفِ خالیِ دفتر، <b>از بالا</b> — به همان ترتیبی که کاربر
    /// می‌بیند (‎SortIndex‎ و بعد ‎Id‎)، نه به ترتیبِ تصادفیِ فهرستِ حافظه.
    ///
    /// خواستهٔ صاحب ریپو: «اگه کادرِ اولی خالی بود همان، اگه نبود دومی، و اگه
    /// جدولی نبود خودش ساخته شود.»
    /// </summary>
    public static DebtRow? FirstBlank(IEnumerable<DebtRow> rows) =>
        rows.Where(IsBlankRow).OrderBy(r => r.SortIndex).ThenBy(r => r.Id).FirstOrDefault();

    /// <summary>
    /// دفتری که یک ردیف باید در آن بنشیند — تنها جای این تصمیم.
    ///
    /// ⚠️ جدا شد تا «کدام حساب» را بشود <b>پیش از</b> نشاندنِ ردیف هم پرسید:
    /// همگام‌سازیِ ورق با همین، ردیف‌های خالیِ همان حساب‌ها را از دیتابیس
    /// می‌آورد. دو نسخه از این قاعده یعنی روزی ردیف در حسابی بنشیند که
    /// ردیف‌های خالی‌اش خوانده نشده بود.
    /// </summary>
    public static DebtAccount ResolveAccount(Debtor person, string? descText,
                                             DebtAccount? targetAccount) =>
        targetAccount is not null && !ReferenceEquals(targetAccount, person.MainAccount)
            ? targetAccount
            : SubForText(person, descText) ?? person.MainAccount;

    /// <summary>‎_fuelBardagi‎ — بردگیِ حالتِ تیل: مقدار تیل × فی، همیشه زنده.</summary>
    public static decimal FuelBardagi(decimal liters, decimal pricePerLiter) => liters * pricePerLiter;
}
