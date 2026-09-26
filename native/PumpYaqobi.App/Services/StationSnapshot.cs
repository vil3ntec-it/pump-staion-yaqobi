using PumpYaqobi.Application.Localization;
using PumpYaqobi.Application.Services;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;
using PumpYaqobi.Services.Data;

namespace PumpYaqobi.App.Services;

/// <summary>
/// ══ عکسِ زندهٔ ایستگاه — خوراکِ اپِ کارمندان و ربات ═════════════════════════
///
/// خواستهٔ صریحِ صاحب ریپو: «اپِ اندروید برای کارمندان تا قرض‌داران را چک کنند
/// که موجودی دارند یا نه — که اضافه ندهند — و موجودیِ تیل در مخزن را ببینند…
/// و یک رباتِ قدرتمند که هرچه از برنامه خواست سریع بدهد: هر شخص که اسم برد،
/// هر بخش، هر ماه و همهٔ بخش‌ها… و هر تغییری که در اپ می‌شود در ربات هم باشد.»
///
/// پس این کلاس یک <b>عکسِ کاملِ خواندنی</b> از برنامه می‌سازد و
/// <see cref="StationPublisher"/> آن را روی سرورِ خانگی می‌گذارد. ربات موتورِ
/// جست‌وجوی همین عکس است.
///
/// ══ سه قاعده که هرگز شکسته نمی‌شوند ════════════════════════════════════════
///
///  ۱) <b>هیچ منطقِ حسابی این‌جا نیست.</b> هر عددی که بیرون می‌رود از خودِ
///     سرویس‌های برنامه گرفته می‌شود (<see cref="DebtCalculationService"/>،
///     <see cref="SafeService"/>، <see cref="CompanyService"/> …). این‌جا فقط
///     «شکل» عوض می‌شود، نه عدد — وگرنه دو حقیقتِ جدا می‌ساختیم و همان چیزی
///     می‌شد که در کیو‌آر پیش آمد.
///
///  ۲) <b>دو دفترِ «واحد تیل» و «واحد پول» هرگز قاطی نمی‌شوند.</b>
///
///  ۳) <b>فقط خواندنی.</b> این عکس هیچ‌وقت به برنامه برنمی‌گردد؛ اپِ کارمندان
///     چیزی نمی‌نویسد. راهِ داده یک‌طرفه است: نیتیو ⟶ سرور ⟶ گوشی.
/// </summary>
public static class StationSnapshot
{
    /// <summary>شمارهٔ شکلِ داده — اگر روزی شکل عوض شد، گوشی بفهمد.</summary>
    public const int Version = 1;

    /// <summary>
    /// عکسِ تازه. ‎gate‎ هشِ رمزِ مدیر است (هرگز خودِ رمز) — قفلِ اپِ کارمندان.
    /// </summary>
    public static async Task<Dictionary<string, object?>> BuildAsync(
        AppHost host, CancellationToken ct = default)
    {
        var s = host.Settings;

        // یک‌بار سنجیده می‌شود و هم به خودِ عکس می‌رود و هم به سازندهٔ قرض‌داران
        var detailed = await host.Debtors.RowCountAsync(ct) <= RowBudget;

        var snap = new Dictionary<string, object?>
        {
            ["v"] = Version,
            // ⚠️ ‎_seq‎ باید همیشه بالا برود، وگرنه گیرنده عکسِ کهنه را قبول
            // می‌کند. میلی‌ثانیهٔ یونیکس همیشه صعودی است.
            ["seq"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            ["at"] = Shamsi.Today(),
            ["atUtc"] = DateTime.UtcNow.ToString("O"),
            ["gate"] = host.Auth.AdminPasswordHash() ?? "",
            ["station"] = new Dictionary<string, object?>
            {
                ["name"] = s.GetString(SettingsService.StationName),
                ["address"] = s.GetString(SettingsService.StationAddress),
                ["phone"] = s.GetString(SettingsService.StationPhone),
                ["ratePetrol"] = D(s.GetDecimal(SettingsService.UnionRatePetrol)),
                ["rateDiesel"] = D(s.GetDecimal(SettingsService.UnionRateDiesel)),
            },
            ["tank"] = await TankAsync(host, ct),
            ["debtors"] = await DebtorsAsync(host, detailed, ct),
            // ‎false‎ یعنی دفتر آن‌قدر بزرگ است که ردیف‌ها به گوشی برده نشدند —
            // فقط جمع‌ها. اپ همین را به کارمند می‌گوید، نه جدولِ خالی.
            ["detail"] = detailed,
            ["sections"] = await SectionsAsync(host, ct),
        };
        snap["alerts"] = Alerts(snap["debtors"] as List<object?>, snap["tank"] as Dictionary<string, object?>);
        // چهار عددِ نوارِ بالای برنامهٔ کامپیوتر — همان ‎MainViewModel.Banner‎، تا
        // صاحبِ پمپ در گوشی هم اول همین چهار عدد را ببیند.
        snap["banner"] = await BannerAsync(host, ct);
        return snap;
    }

    // ══════════════════════════════════════════════════════════════════════
    //  هشدارها — «برنامه یک پیام بدهد»
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// ══ فهرستِ کسانی که کارمند باید همین حالا خبرشان را بگیرد ═══════════════
    ///
    /// خواستهٔ صریحِ صاحب ریپو: «وقتی که یک قرض‌دار اضافه برد یا کم مانده بود
    /// از حسابش، برنامه یک پیام بدهد — حتی اگر گوشی خاموش یا حتی اگر توی
    /// برنامه نبود هم پیام برود تا بفهمد.»
    ///
    /// ⚠️ اینجا هیچ قاعدهٔ تازه‌ای ساخته نمی‌شود: «اضافه برد» و «کم مانده»
    /// همان ‎DebtStatus.Out‎ و ‎DebtStatus.Low‎ی ‎DebtCalculationService.Status‎
    /// هستند — همان چیزی که رنگِ کارتِ قرض‌دار روی کامپیوتر از آن می‌آید. اگر
    /// قاعده‌ای این‌جا جدا نوشته می‌شد، روزی کارت سرخ می‌بود و گوشی ساکت.
    ///
    /// ⚠️ چرا فهرست در خودِ عکس است و هر گیرنده‌ای خودش حسابش نمی‌کند: کارِ
    /// پس‌زمینهٔ گوشی نباید عکسِ چندمگابایتی را باز کند و روی همهٔ حساب‌ها
    /// بگردد. این فهرست چند خط است و همان است که هم اپ می‌بیند هم کارِ
    /// پس‌زمینه — پس هرگز دو چیزِ متفاوت نمی‌گویند.
    ///
    /// <b>کلید (‎k‎)</b> مهم‌ترین تکه است: گیرنده هر کلید را <i>یک بار</i> خبر
    /// می‌دهد. پس کلید باید هم ثابت باشد (وگرنه هر بیست ثانیه دوباره زنگ
    /// می‌زند) و هم با عوض شدنِ حال عوض شود (وگرنه حسابی که از «کم مانده» به
    /// «تمام شد» رسید، خبرِ تازه‌ای نمی‌داد). برای همین حال هم داخلِ کلید است.
    /// </summary>
    public static List<object?> Alerts(List<object?>? people, Dictionary<string, object?>? tank)
    {
        var outList = new List<object?>();

        foreach (var p in people ?? new List<object?>())
        {
            if (p is not Dictionary<string, object?> d) continue;
            var name = d.TryGetValue("name", out var n) ? n as string ?? "" : "";
            var id = d.TryGetValue("id", out var i) ? i : 0;

            foreach (var (key, fuel, unit) in new[] { ("stP", "پطرول", "لیتر"), ("stD", "دیزل", "لیتر"), ("stM", "پول", "افغانی") })
            {
                var st = d.TryGetValue(key, out var v) ? v as string ?? "" : "";
                var use = UseOf(d, key);
                var level = DebtLevel(st, use);
                if (level.Length == 0) continue;

                var (text, act) = DebtWords(level, name, fuel, unit, use);
                outList.Add(new Dictionary<string, object?>
                {
                    ["k"] = "d" + id + "-" + key + "-" + level,
                    ["n"] = name,
                    ["f"] = fuel,
                    ["s"] = level is "out" or "over" ? "out" : "low",
                    ["t"] = text,
                    ["a"] = act,
                });
            }
        }

        foreach (var (key, label) in new[] { ("petrol", "پطرول"), ("diesel", "دیزل") })
        {
            if (tank is null || !tank.TryGetValue(key, out var raw)) continue;
            if (raw is not Dictionary<string, object?> t) continue;
            var low = t.TryGetValue("low", out var l) && l is true;
            var near = t.TryGetValue("near", out var nr) && nr is true;
            if (!low && !near) continue;

            var show = Num(t.TryGetValue("show", out var sh) ? sh : null);
            var thr = Num(t.TryGetValue("threshold", out var th) ? th : null);
            var lim = thr > 0m ? " (حدِ هشدار " + Amount(thr) + " لیتر)" : "";
            string text, act;
            if (low && show <= 0m)
            {
                text = "مخزنِ " + label + " خالی شد — 0 لیتر مانده";
                act = "همین حالا " + label + " سفارش بدهید؛ فروشِ " + label + " ایستاده است.";
            }
            else if (low)
            {
                text = "مخزنِ " + label + " رو به ته کشیدن است — " + Amount(show) + " لیتر مانده" + lim;
                act = "امروز " + label + " سفارش بدهید تا پمپ خالی نماند.";
            }
            else
            {
                text = "مخزنِ " + label + " نزدیکِ حدِ هشدار است — " + Amount(show) + " لیتر مانده" + lim;
                act = "برای خریدِ " + label + " آماده شوید؛ چند روزِ دیگر کم می‌آید.";
            }

            outList.Add(new Dictionary<string, object?>
            {
                ["k"] = "tank-" + key + (low ? "-out" : "-low"),
                ["n"] = "مخزنِ " + label,
                ["f"] = label,
                ["s"] = low ? "out" : "low",
                ["t"] = text,
                ["a"] = act,
            });
        }

        return outList;
    }

    /// <summary>
    /// ══ حالِ هشدارِ یک دفتر — ۷۰٪ · ۹۰٪ · تمام شد · اضافه داده شد ════════════
    ///
    /// خواستهٔ صاحب ریپو (۱۴۰۵/۰۷/۱۴): «اگه قرض‌داری به ۷۰ فیصد از حسابش
    /// می‌رسید بگه متوجه باشه، و تا ۹۰، و اگه تموم شد بگه تموم شده، و اگه
    /// اضافه داد بازپرسی کنه که چرا اضافه دادی.»
    ///
    /// ⛔ «تمام شد» هنوز فقط از <c>DebtStatus.Out</c> است (همان رنگِ سرخِ کارت)؛
    /// درصد و مانده از <c>DebtCalculationService.Usage</c> — همان سنجش‌ها. پس
    /// کارت و هشدار هرگز دو چیزِ متفاوت نمی‌گویند. حسابِ تسویه‌شده (حالِ ‎ok‎ با
    /// مصرفِ ۱۰۰٪) هیچ هشداری ندارد.
    ///
    /// ⚠️ حال داخلِ کلید است (‎d7-stP-w70‎ ⇒ ‎-w90‎ ⇒ ‎-out‎ ⇒ ‎-over‎)، پس هر
    /// پله خبرِ تازهٔ خودش را می‌دهد و همان پله دوباره نه.
    /// </summary>
    internal static string DebtLevel(string st, (bool Has, decimal Deposit, decimal Used) use)
    {
        if (st == "out") return use.Has && use.Used > use.Deposit ? "over" : "out";
        if (use.Has && use.Deposit > 0m)
        {
            var pct = use.Used * 100m / use.Deposit;
            if (pct >= 100m) return "";          // تسویه‌شده — سرخ نیست، هشدار هم نیست
            if (pct >= 90m) return "w90";
            if (pct >= 70m) return "w70";
            return st == "low" ? "w90" : "";
        }
        return st == "low" ? "low" : "";
    }

    private static (string Text, string Action) DebtWords(string level, string name, string fuel, string unit,
                                                          (bool Has, decimal Deposit, decimal Used) use)
    {
        var money = fuel == "پول";
        var rem = Amount(Math.Max(0m, use.Deposit - use.Used));
        var pct = use.Deposit > 0m ? Amount(Math.Floor(use.Used * 100m / use.Deposit)) : "";
        var give = money ? "قرض" : fuel;
        return level switch
        {
            "w70" => (name + " — " + pct + "٪ِ " + fuel + "ِ حسابش مصرف شد؛ " + rem + " " + unit + " مانده",
                      "متوجهِ " + name + " باشید؛ کم‌کم رسیدِ تازه بخواهید."),
            "w90" => (name + " — " + pct + "٪ِ " + fuel + "ِ حسابش رفت؛ فقط " + rem + " " + unit + " مانده",
                      "به " + name + " بگویید حسابش را پر کند؛ بیشتر از " + rem + " " + unit + " به او ندهید."),
            "over" => (name + " — " + Amount(use.Used - use.Deposit) + " " + unit + " بیشتر از حسابش " + give
                       + " داده شده؛ اضافه نده",
                       "بازپرسی کنید: چه کسی و چرا به " + name + " اضافه داد؟ همین امروز "
                       + Amount(use.Used - use.Deposit) + " " + unit + " را از او بگیرید."),
            "out" => (name + " — " + fuel + "ِ حسابش تمام شد، اضافه نده",
                      "به " + name + " دیگر " + give + " ندهید تا رسیدِ تازه بیاورد."),
            _ => (name + " — " + fuel + "ِ حسابش کم مانده",
                  "متوجهِ " + name + " باشید؛ حسابش کم مانده."),
        };
    }

    /// <summary>اعتبار و مصرفِ یک دفتر از عکس (‎use.stP = [اعتبار, مصرف]‎).</summary>
    private static (bool Has, decimal Deposit, decimal Used) UseOf(Dictionary<string, object?> d, string key)
    {
        if (!d.TryGetValue("use", out var u) || u is not Dictionary<string, object?> use) return (false, 0m, 0m);
        if (!use.TryGetValue(key, out var pair) || pair is not double[] v || v.Length < 2) return (false, 0m, 0m);
        return (true, (decimal)v[0], (decimal)v[1]);
    }

    /// <summary>اعتبار و مصرفِ سه دفتر — همان <c>DebtCalculationService.Usage</c>.</summary>
    private static Dictionary<string, object?> Use(DebtUsageInfo u)
    {
        var d = new Dictionary<string, object?>();
        if (u.Petrol.Any) d["stP"] = new[] { D(u.Petrol.Deposit), D(u.Petrol.Used) };
        if (u.Diesel.Any) d["stD"] = new[] { D(u.Diesel.Deposit), D(u.Diesel.Used) };
        if (u.Money.Any) d["stM"] = new[] { D(u.Money.Deposit), D(u.Money.Used) };
        return d;
    }

    private static decimal Num(object? v) => v switch
    {
        double x => (decimal)x,
        decimal m => m,
        int i => i,
        long l => l,
        float f => (decimal)f,
        _ => 0m,
    };

    private static string Amount(decimal v) => Shamsi.Money(Math.Round(v, 0, MidpointRounding.AwayFromZero));

    // ══════════════════════════════════════════════════════════════════════
    //  مخزن — «موجودیِ تیل در مخزن» که کارمند باید ببیند
    // ══════════════════════════════════════════════════════════════════════

    /// <remarks>
    /// ⚠️ «فروش» فقط از ستونِ ‎Sale‎ خوانده می‌شود (‎ShiftSumsAsync‎)، نه همهٔ
    /// پارچه‌های پنج سال به شیءِ کامل — این تابع حالا از <see cref="AlertWatch"/>
    /// هم صدا زده می‌شود، با هر ذخیرهٔ دفتر. عددها همان‌اند:
    /// <c>StorageService.Tank(purchases, reports, …)</c> خودش به همین می‌رسد.
    /// </remarks>
    internal static async Task<Dictionary<string, object?>> TankAsync(AppHost host, CancellationToken ct)
    {
        var threshold = host.Settings.GetDecimal(SettingsService.LowStockThreshold, 1000m);
        var tank = new Dictionary<string, object?>();
        foreach (var (key, fuel) in new[] { ("petrol", FuelType.Petrol), ("diesel", FuelType.Diesel) })
        {
            var purchases = await host.StorageData.PurchasesAsync(fuel, ct);
            var sold = await host.StorageData.ShiftSumsAsync(fuel, ct);
            var dips = await host.StorageData.DipsAsync(fuel, ct);

            // ⚠️ همان تابعی که خودِ بخشِ مخزن و داشبورد از آن می‌خوانند
            var t = host.Storage.Tank(purchases, sold.Sale, threshold, dips);
            tank[key] = new Dictionary<string, object?>
            {
                ["in"] = D(t.In),
                ["out"] = D(t.Out),
                ["current"] = D(t.Current),
                ["show"] = D(t.Display),
                ["low"] = t.IsLow,
                ["near"] = t.IsNear,
                ["threshold"] = D(threshold),
            };
        }
        return tank;
    }

    // ══════════════════════════════════════════════════════════════════════
    //  قرض‌داران — «موجودی دارند یا نه، که اضافه ندهند»
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// ══ سقفِ ردیف‌هایی که به گوشی برده می‌شود ═══════════════════════════════
    ///
    /// ⚠️ این عدد یک تجمل نیست، ترمزِ همان چاله‌ای است که یک‌بار برنامه را
    /// خواباند: «همهٔ ردیف‌های همهٔ حساب‌ها را بخوان». با ده هزار قرض‌دار و یک
    /// میلیون ردیف، خواندنِ همه‌شان هر بیست ثانیه یعنی برنامهٔ روی کامپیوتر
    /// کند شود — درست همان چیزی که نباید بشود.
    ///
    /// پس دو حالت هست و هر دو کار می‌کنند:
    ///   • زیرِ سقف ⇒ هر حساب جدولِ کاملش را هم با خودش می‌برد.
    ///   • بالای سقف ⇒ فقط جمع‌ها و حال و الباقی می‌روند (که کارمند برای
    ///     «موجودی دارد یا نه» همان را لازم دارد) و اپ خودش می‌گوید که
    ///     ردیف‌ها در این عکس نیستند.
    ///
    /// در هر دو حالت، <b>حال و الباقیِ همهٔ قرض‌داران</b> می‌رود — چون همان
    /// چیزی است که جلوی «تیلِ اضافه دادن» را می‌گیرد. و در هر دو حالت،
    /// عددها با یک ‎GROUP BY‎ی خودِ SQLite درمی‌آیند، نه با خواندنِ ردیف‌ها.
    /// </summary>
    public const int RowBudget = 25_000;

    private static async Task<List<object?>> DebtorsAsync(AppHost host, bool detailed,
                                                         CancellationToken ct)
    {
        var calc = host.Debt;
        var people = new List<object?>();

        foreach (var noInv in new[] { false, true })
        {
            // ⚠️ هر دو راه شمارِ کوئریِ **ثابت** دارند، نه «چهارتا برای هر نفر»:
            //   • خلاصه ⇒ ‎CardAccountsAsync‎، جمع‌ها را خودِ SQLite با یک
            //     ‎GROUP BY‎ می‌زند و هیچ ردیفی خوانده نمی‌شود.
            //   • کامل  ⇒ ‎LoadAllAsync‎، همهٔ ردیف‌ها در یک کوئری.
            // صدا زدنِ ‎LoadFullAsync‎ داخلِ حلقه همان چاله‌ای بود که یک‌بار
            // برنامه را خواباند؛ این‌جا هرگز آن راه نرود.
            //
            // در هر دو حالت فهرستِ خودِ اشخاص از یک جا می‌آید و حساب‌هایشان از
            // جای دیگر — فقط «جای دیگر» فرق می‌کند.
            List<Debtor> list;
            Dictionary<long, List<DebtAccount>> byPerson;

            if (detailed)
            {
                list = await host.Debtors.LoadAllAsync(noInv, ct);
                byPerson = list.ToDictionary(p => p.Id, p => p.AllAccounts().ToList());
            }
            else
            {
                list = await host.Debtors.ListAsync(noInv, null, ct);
                byPerson = await host.Debtors.CardAccountsAsync(noInv, ct);
            }

            foreach (var lite in list)
            {
                ct.ThrowIfCancellationRequested();

                if (!byPerson.TryGetValue(lite.Id, out var accounts) || accounts.Count == 0)
                    continue;

                // ⚠️ حال و الباقی از خودِ سرویس — همان عددی که روی کارتِ
                // برنامه دیده می‌شود، نه یک حسابِ تازه.
                var st = calc.Status(accounts);
                var bal = calc.Balances(accounts);

                people.Add(new Dictionary<string, object?>
                {
                    ["id"] = lite.Id,
                    ["name"] = lite.Name,
                    ["use"] = Use(calc.Usage(accounts)),
                    ["phone"] = lite.Phone ?? "",
                    ["noinv"] = lite.IsNoInvoice,
                    ["status"] = StatusText(st.Worst),
                    ["stP"] = StatusText(st.Petrol),
                    ["stD"] = StatusText(st.Diesel),
                    ["stM"] = StatusText(st.Money),
                    ["bal"] = new Dictionary<string, object?>
                    {
                        ["money"] = D(bal.Money),
                        ["petrol"] = D(bal.Petrol),
                        ["diesel"] = D(bal.Diesel),
                    },
                    ["accounts"] = accounts.Select(a => Account(lite.Name, a, calc, detailed)).ToList(),
                });
            }
        }
        return people;
    }

    /// <summary>
    /// ══ حال و الباقیِ همهٔ قرض‌داران — بی جدول ══════════════════════════════
    ///
    /// همان <see cref="DebtorsAsync"/> در حالتِ «خلاصه»، ولی بی حساب‌ها و
    /// ردیف‌ها: فقط آن‌چه <see cref="Alerts"/> و جست‌وجوی باتِ تلگرام لازم
    /// دارند. ⚠️ حال و الباقی از همان ‎DebtCalculationService‎ و همان
    /// ‎CardAccountsAsync‎ است که کارتِ قرض‌دار از آن رنگ می‌گیرد — قاعدهٔ
    /// تازه‌ای این‌جا نیست. شمارِ کوئری ثابت است (یک ‎GROUP BY‎ برای هر دو
    /// فهرست)، نه یکی برای هر نفر.
    /// </summary>
    internal static async Task<List<object?>> DebtorsLiteAsync(AppHost host, CancellationToken ct)
    {
        var calc = host.Debt;
        var people = new List<object?>();
        foreach (var noInv in new[] { false, true })
        {
            var list = await host.Debtors.ListAsync(noInv, null, ct);
            var byPerson = await host.Debtors.CardAccountsAsync(noInv, ct);
            foreach (var lite in list)
            {
                ct.ThrowIfCancellationRequested();
                if (!byPerson.TryGetValue(lite.Id, out var accounts) || accounts.Count == 0) continue;
                var st = calc.Status(accounts);
                var bal = calc.Balances(accounts);
                people.Add(new Dictionary<string, object?>
                {
                    ["id"] = lite.Id,
                    ["name"] = lite.Name,
                    ["use"] = Use(calc.Usage(accounts)),
                    ["stP"] = StatusText(st.Petrol),
                    ["stD"] = StatusText(st.Diesel),
                    ["stM"] = StatusText(st.Money),
                    ["bal"] = new Dictionary<string, object?>
                    {
                        ["money"] = D(bal.Money),
                        ["petrol"] = D(bal.Petrol),
                        ["diesel"] = D(bal.Diesel),
                    },
                });
            }
        }
        return people;
    }

    /// <summary>یک حساب — همان جدول و همان چهار عددِ سربرگ.</summary>
    /// <param name="withRows">
    /// ⚠️ در حالتِ خلاصه <b>حتماً</b> باید ‎false‎ باشد: ردیف‌هایی که
    /// ‎CardAccountsAsync‎ می‌سازد «ردیفِ جمع»اند نه ردیفِ واقعی، و نشان
    /// دادنشان یعنی جدولی که هیچ‌جای برنامه وجود ندارد. جمع‌هایشان اما
    /// دقیقاً درست است — کارت‌های خودِ برنامه هم از همان می‌خوانند.
    /// </param>
    private static Dictionary<string, object?> Account(string personName, DebtAccount a,
                                                       DebtCalculationService calc, bool withRows)
    {
        // ⚠️ عینِ کیو‌آر: همان ‎AcctSnapshots‎، تا عددِ گوشیِ کارمند و عددِ
        // کیو‌آرِ مشتری و عددِ خودِ برنامه هر سه یکی باشند.
        var snap = AcctSnapshots.ForDebtAccount(personName, a.IsMain ? null : a.Name, a, calc);
        return new Dictionary<string, object?>
        {
            ["id"] = a.Id,
            ["title"] = a.IsMain ? "" : (a.Name ?? ""),
            ["sub"] = a.LegacySubId ?? "",
            ["unit"] = a.Mode.IsMoney() ? "money" : "fuel",
            ["pctP"] = D(calc.PercentOf(a, FuelType.Petrol)),
            ["pctD"] = D(calc.PercentOf(a, FuelType.Diesel)),
            ["sum"] = snap.Summary,
            ["head"] = snap.Head,
            ["rows"] = withRows ? snap.Rows : new List<string[]>(),
        };
    }

    private static string StatusText(DebtStatus s) => s switch
    {
        DebtStatus.Out => "out",
        DebtStatus.Low => "low",
        DebtStatus.Ok => "ok",
        _ => "none",
    };

    // ══════════════════════════════════════════════════════════════════════
    //  بقیهٔ بخش‌ها — «هر بخش، هر ماه» که ربات باید جواب بدهد
    // ══════════════════════════════════════════════════════════════════════
    //
    //  هر بخش یک شکلِ واحد دارد تا ربات لازم نباشد برای هر کدام کدِ جدا
    //  داشته باشد:
    //      { t: عنوان, head: [ستون‌ها], rows: [[خانه‌ها]], m: [ماهِ هر ردیف],
    //        sum: [[برچسب, مقدار]] }
    //  ‎m[i]‎ ماهِ ردیفِ ‎i‎ است («1405/06») — با همین، ربات «ماه فلان» را
    //  فیلتر می‌کند بی این‌که تاریخ را دوباره بخواند.

    private static async Task<Dictionary<string, object?>> SectionsAsync(AppHost host, CancellationToken ct)
    {
        var out_ = new Dictionary<string, object?>();

        // ── گاوصندوق ────────────────────────────────────────────────────────
        var safe = await host.SafeLedger.ListAsync(null, ct);
        var safeSum = host.Safe.Summarize(safe);
        out_["safe"] = Section("گاوصندوق",
            new[] { "تاریخ", "نوع", "شرح", "مقدار", "واحد" },
            safe.Select(e => new[]
            {
                e.DateShamsi ?? "",
                e.Kind == SafeEntryKind.Bardagi ? "بردگی" : "ماندگی",
                e.Title ?? "",
                Shamsi.Money(e.Amount),
                e.Currency == Currency.Usd ? "دالر" : "افغانی",
            }),
            safe.Select(e => e.MonthKey ?? ""),
            new[]
            {
                new[] { "بردگی افغانی", Shamsi.Money(safeSum.Bardagi.Afn) },
                new[] { "ماندگی افغانی", Shamsi.Money(safeSum.Mandagi.Afn) },
                new[] { "خالص افغانی", Shamsi.Money(safeSum.Net.Afn) },
                new[] { "خالص دالر", Shamsi.Money(safeSum.Net.Usd) },
            });

        // ── صرافی ───────────────────────────────────────────────────────────
        var ex = await host.ExchangeLedger.ListAsync(null, ct);
        var exSum = host.Exchange.Summarize(ex);
        out_["sarrafi"] = Section("صرافی",
            new[] { "تاریخ", "شرح", "مبلغ", "واحد", "فی", "دالر", "بردگی", "باقی" },
            ex.Select(r => new[]
            {
                r.DateShamsi ?? "", r.Description ?? "", Shamsi.Money(r.Amount),
                r.Currency switch
                {
                    ExchangeCurrency.Afghani => "افغانی",
                    ExchangeCurrency.Kaldar => "کلدار",
                    _ => "تومان",
                },
                Shamsi.Money(r.Rate), Shamsi.Money(host.Exchange.ToUsd(r)),
                Shamsi.Money(r.Bardagi), Shamsi.Money(host.Exchange.RowBaqi(r)),
            }),
            ex.Select(r => r.MonthKey ?? ""),
            new[]
            {
                new[] { "جمله دالر", Shamsi.Money(exSum.TotalUsd) },
                new[] { "جمله بردگی", Shamsi.Money(exSum.TotalBardagi) },
                new[] { "باقی", Shamsi.Money(exSum.Baqi) },
            });

        // ── مصارف ───────────────────────────────────────────────────────────
        var exp = await host.ExpenseLedger.ListAsync(null, ct);
        out_["expense"] = Section("مصارف",
            new[] { "تاریخ", "عنوان", "مقدار", "یادداشت" },
            exp.Select(r => new[]
            {
                r.DateShamsi ?? "", r.Title ?? "", Shamsi.Money(r.Amount), r.Note ?? "",
            }),
            exp.Select(r => r.MonthKey ?? ""),
            new[] { new[] { "جمله مصارف", Shamsi.Money(host.Expenses.Total(exp)) } });

        // ── چکنه ────────────────────────────────────────────────────────────
        var ret = await host.RetailLedger.ListAsync(null, ct);
        var retSum = host.Retail.Summarize(ret);
        out_["chakana"] = Section("چکنه",
            new[] { "تاریخ", "نام", "تیل", "مقدار", "فی", "بردگی", "رسید", "الباقی" },
            ret.Select(r => new[]
            {
                r.DateShamsi ?? "", r.Name ?? "",
                r.Fuel == FuelType.Diesel ? "دیزل" : "پطرول",
                Shamsi.MoneyOrBlank(r.Liters), Shamsi.MoneyOrBlank(r.PricePerLiter),
                Shamsi.Money(host.Retail.Bardagi(r)), Shamsi.MoneyOrBlank(r.Rasid),
                Shamsi.Money(host.Retail.Albaqi(r)),
            }),
            ret.Select(r => r.MonthKey ?? ""),
            new[]
            {
                new[] { "جمله لیتر", Shamsi.Money(retSum.Liters) },
                new[] { "جمله بردگی", Shamsi.Money(retSum.Bardagi) },
                new[] { "جمله رسید", Shamsi.Money(retSum.Rasid) },
                new[] { "الباقی", Shamsi.Money(retSum.Albaqi) },
            });

        // ── عایدات اضافی ────────────────────────────────────────────────────
        var extra = await host.ExtraIncomeLedger.ListAsync(null, ct);
        out_["extraincome"] = Section("عایدات",
            new[] { "تاریخ", "فروشنده", "مقدار", "خرید", "بازار", "مبلغ" },
            extra.Select(r => new[]
            {
                r.DateShamsi ?? "", r.Seller ?? "", Shamsi.MoneyOrBlank(r.Qty),
                Shamsi.MoneyOrBlank(r.Buy), Shamsi.MoneyOrBlank(r.Market), Shamsi.Money(r.Amount),
            }),
            extra.Select(r => r.MonthKey ?? ""),
            new[] { new[] { "جمله", Shamsi.Money(extra.Sum(r => r.Amount)) } });

        // ── شرکت‌های تیل ────────────────────────────────────────────────────
        out_["company"] = await CompaniesAsync(host, ct);

        // ── امانت ───────────────────────────────────────────────────────────
        out_["amanat"] = await AmanatAsync(host, ct);

        // ── فاکتورها ────────────────────────────────────────────────────────
        var invoices = await host.Invoices.ListAsync(null, null, ct);
        out_["invoice"] = Section("فاکتورها",
            new[] { "شماره", "تاریخ", "مشتری", "تیل", "مقدار", "فی", "مبلغ", "حال" },
            invoices.Select(v => new[]
            {
                Shamsi.Money(v.InvoiceNumber), v.DateShamsi ?? "", v.CustomerName ?? "",
                v.Fuel == FuelType.Diesel ? "دیزل" : "پطرول",
                Shamsi.MoneyOrBlank(v.Liters), Shamsi.MoneyOrBlank(v.PricePerLiter),
                Shamsi.Money(v.Amount),
                v.Status == InvoiceStatus.Approved ? "تایید شده" : "در انتظار",
            }),
            invoices.Select(v => MonthOf(v.DateShamsi)),
            new[]
            {
                new[] { "شمارِ فاکتور", Shamsi.Money(invoices.Count) },
                new[] { "جمله مبلغ", Shamsi.Money(invoices.Sum(v => v.Amount)) },
            });

        // ── خریدهای مخزن ────────────────────────────────────────────────────
        var buys = new List<FuelPurchase>();
        buys.AddRange(await host.StorageData.PurchasesAsync(FuelType.Petrol, ct));
        buys.AddRange(await host.StorageData.PurchasesAsync(FuelType.Diesel, ct));
        out_["storage"] = Section("خریدِ تیل",
            new[] { "تاریخ", "تیل", "فروشنده", "کیلو", "لیتر", "فی لیتر", "جمله افغانی" },
            buys.Select(p => new[]
            {
                p.DateShamsi ?? "", p.Fuel == FuelType.Diesel ? "دیزل" : "پطرول",
                p.Seller ?? "", Shamsi.Money(p.Kg), Shamsi.Money(p.Liters),
                Shamsi.Money(p.PerLiter), Shamsi.Money(p.TotalAfn),
            }),
            buys.Select(p => MonthOf(p.DateShamsi)),
            new[]
            {
                new[] { "جمله لیتر", Shamsi.Money(buys.Sum(p => p.Liters)) },
                new[] { "جمله افغانی", Shamsi.Money(buys.Sum(p => p.TotalAfn)) },
            });

        // ── کارمندان ────────────────────────────────────────────────────────
        var staff = await host.Attendance.StaffAsync(ct);
        out_["staff"] = Section("کارمندان",
            new[] { "نام", "معاش", "شروع", "پایان", "یادداشت" },
            staff.Select(m => new[]
            {
                m.Name ?? "", Shamsi.Money(m.Salary),
                m.ShiftIn ?? "", m.ShiftOut ?? "", m.Note ?? "",
            }),
            staff.Select(_ => ""),
            new[] { new[] { "شمارِ کارمند", Shamsi.Money(staff.Count) } });

        // ── پارچه (پطرول و دیزل) ─────────────────────────────────────────────
        //  خواستهٔ صاحب ریپو: «حساب‌های پمپ» در گوشی همان بخش‌های برنامهٔ
        //  کامپیوتر باشد. تا این‌جا پارچه، ورق، رسیدها و حاضری جا مانده بودند.
        var parchaRows = new List<string[]>();
        var parchaMonths = new List<string>();
        decimal pSale = 0, pMoney = 0, pDebt = 0, pProfit = 0;
        foreach (var fuel in new[] { FuelType.Petrol, FuelType.Diesel })
        {
            foreach (var r in await host.ParchaData.ListAsync(fuel, null, ct))
            {
                foreach (var (kind, sh) in new[] { ("روز", r.DayShift), ("شب", r.NightShift) })
                {
                    if (sh is null) continue;
                    var n = host.Parcha.Compute(sh);
                    pSale += n.Sale; pMoney += n.Money; pDebt += sh.Debt; pProfit += n.Profit;
                    parchaRows.Add(new[]
                    {
                        r.DateShamsi ?? "", fuel == FuelType.Diesel ? "دیزل" : "پطرول", kind,
                        sh.Name ?? "", Shamsi.Money(n.Sale), Shamsi.Money(sh.Price),
                        Shamsi.Money(n.Money), Shamsi.Money(sh.Debt), Shamsi.Money(n.Profit),
                    });
                    parchaMonths.Add(MonthOf(r.DateShamsi));
                }
            }
        }
        out_["parcha"] = Section("پارچه",
            new[] { "تاریخ", "تیل", "شیفت", "کارمند", "فروش (لیتر)", "فی", "پول", "قرض", "فایده" },
            parchaRows, parchaMonths,
            new[]
            {
                new[] { "جمله فروش (لیتر)", Shamsi.Money(pSale) },
                new[] { "جمله پول", Shamsi.Money(pMoney) },
                new[] { "جمله قرض", Shamsi.Money(pDebt) },
                new[] { "جمله فایده", Shamsi.Money(pProfit) },
            });

        // ── ورق ──────────────────────────────────────────────────────────────
        var waraqRows = new List<string[]>();
        var waraqMonths = new List<string>();
        decimal wPetrol = 0, wDiesel = 0, wSales = 0, wDebt = 0, wExp = 0, wShort = 0;
        foreach (var w in await host.WaraqData.ListAsync(null, ct))
        {
            foreach (var sh in w.Shifts)
            {
                var t = host.Waraq.ShiftTotals(sh);
                var sc = host.Waraq.Shortage(t);
                wPetrol += t.PetrolLiters; wDiesel += t.DieselLiters; wSales += t.Sales;
                wDebt += t.Debt; wExp += t.Expenses; wShort += sc.Shortage;
                waraqRows.Add(new[]
                {
                    w.DateShamsi ?? "", sh.Kind == ShiftKind.Night ? "شب" : "روز", sh.WorkerName ?? "",
                    Shamsi.Money(t.PetrolLiters), Shamsi.Money(t.DieselLiters), Shamsi.Money(t.Sales),
                    Shamsi.Money(t.Debt), Shamsi.Money(t.Expenses),
                    sc.Shortage > 0 ? Shamsi.Money(sc.Shortage) : (sc.Excess > 0 ? "+" + Shamsi.Money(sc.Excess) : "0"),
                });
                waraqMonths.Add(MonthOf(w.DateShamsi));
            }
        }
        out_["waraq"] = Section("ورق",
            new[] { "تاریخ", "شیفت", "کارمند", "پطرول (لیتر)", "دیزل (لیتر)", "فروش", "قرض", "مصرف", "کمبودی" },
            waraqRows, waraqMonths,
            new[]
            {
                new[] { "جمله پطرول", Shamsi.Money(wPetrol) },
                new[] { "جمله دیزل", Shamsi.Money(wDiesel) },
                new[] { "جمله فروش", Shamsi.Money(wSales) },
                new[] { "جمله قرض", Shamsi.Money(wDebt) },
                new[] { "جمله مصرف", Shamsi.Money(wExp) },
                new[] { "جمله کمبودی", Shamsi.Money(wShort) },
            });

        // ── رسیدهای قرض‌داران ─────────────────────────────────────────────────
        var quick = await host.DebtReceipts.ListAsync(null, ct);
        out_["debtrasid"] = Section("رسید قرض‌داران",
            new[] { "تاریخ", "حساب", "مبلغ", "یادداشت" },
            quick.Select(q => new[] { q.DateShamsi ?? "", q.Account ?? "", Shamsi.Money(q.Amount), q.Note ?? "" }),
            quick.Select(q => q.MonthKey ?? MonthOf(q.DateShamsi)),
            new[]
            {
                new[] { "شمارِ رسید", Shamsi.Money(quick.Count) },
                new[] { "جمله مبلغ", Shamsi.Money(quick.Sum(q => q.Amount)) },
            });

        // ── رسیدهای پارچه (در انتظارِ ثبت) ──────────────────────────────────
        var pr = await host.ParchaReceipts.ListAsync(ct);
        out_["parcharasid"] = Section("رسید پارچه",
            new[] { "تاریخ", "به حساب", "شرح", "حواله", "لیتر", "فی", "رسید" },
            pr.Select(x => new[]
            {
                x.DateShamsi ?? "", x.Account ?? "", x.Name ?? "", x.Hawala ?? "",
                Shamsi.MoneyOrBlank(x.Liters), Shamsi.MoneyOrBlank(x.PricePerLiter), Shamsi.Money(x.Rasid),
            }),
            pr.Select(x => MonthOf(x.DateShamsi)),
            new[]
            {
                new[] { "شمارِ ردیف", Shamsi.Money(pr.Count) },
                new[] { "جمله رسید", Shamsi.Money(pr.Sum(x => x.Rasid)) },
            });

        // ── حاضری (ماهِ جاری) و معاش‌های پرداخت‌شده ─────────────────────────
        var thisMonth = Shamsi.ThisMonth();
        var attRows = await host.Attendance.RowsAsync(thisMonth, ct);
        var pays = await host.Attendance.PaymentsAsync(ct);
        var byId = staff.ToDictionary(m => m.Id, m => m.Name ?? "");
        out_["attendance"] = Section("حاضری",
            new[] { "تاریخ", "کارمند", "آمدن", "رفتن", "ساعت", "یادداشت" },
            attRows.Select(r => new[]
            {
                r.DateShamsi ?? "", byId.TryGetValue(r.StaffId, out var nm) ? nm : (r.Staff?.Name ?? ""),
                r.In ?? "", r.Out ?? "", Shamsi.Money(host.AttendanceCalc.Hours(r)), r.Note ?? "",
            }),
            attRows.Select(r => MonthOf(r.DateShamsi)),
            new[]
            {
                new[] { "روزهای ثبت‌شدهٔ این ماه", Shamsi.Money(attRows.Count) },
                new[] { "جمله ساعت", Shamsi.Money(attRows.Sum(r => host.AttendanceCalc.Hours(r))) },
                new[] { "معاشِ پرداخت‌شده (همه)", Shamsi.Money(pays.Sum(p => p.Amount)) },
            });

        return out_;
    }

    /// <summary>
    /// چهار عددِ نوارِ بالا — رونوشتِ ‎MainViewModel.RefreshBannerAsync‎ با همان
    /// سرویس‌ها؛ هیچ حسابِ تازه‌ای این‌جا نیست.
    /// </summary>
    private static async Task<List<string[]>> BannerAsync(AppHost host, CancellationToken ct)
    {
        var companies = await host.Companies.ListAsync(ct);
        var compAlbaqi = companies.Sum(c => host.Company.Summarize(c, c.Rows).AlbaqiAfn);

        var accounts = await host.Debtors.CardAccountsAsync(ct: ct);
        decimal debt = 0;
        foreach (var list in accounts.Values) debt += host.Debt.SumTotals(list).All.Albaqi;

        var today = Shamsi.Today();
        var reports = (await host.StorageData.ReportsAsync(FuelType.Petrol, ct))
            .Concat(await host.StorageData.ReportsAsync(FuelType.Diesel, ct))
            .Where(r => r.DateShamsi == today);
        var profit = reports.Sum(r => (r.DayShift?.Profit ?? 0) + (r.NightShift?.Profit ?? 0));

        var expToday = new PumpYaqobi.Application.Services.DashboardService()
            .ExpQuick(await host.ExpenseLedger.ListAsync(Shamsi.ThisMonth(), ct)).Day;

        static string M(decimal v) => Shamsi.Money(Math.Round(v, 0, MidpointRounding.AwayFromZero));
        return new List<string[]>
        {
            new[] { "شرکت ها تیل (الباقی)", M(compAlbaqi), "accent" },
            new[] { "قرض کل", M(debt), "danger" },
            new[] { "مفاد امروز", M(profit), "ok" },
            new[] { "مصارف امروز", M(expToday), "warn" },
        };
    }

    /// <summary>
    /// ══ بدهیِ پمپ به هر شرکت — برای «اعلامیه»ی بات ══════════════════════════
    ///
    /// خواستهٔ صاحب ریپو (۱۴۰۵/۰۷/۱۴): «…یا از شرکت این‌قدر قرض‌دار استین.»
    /// ⛔ همان الباقیِ نوارِ بالای برنامه (<see cref="BannerAsync"/>:
    /// ‎Summarize(c, c.Rows)‎) — حسابِ تازه‌ای این‌جا نیست. فقط شرکتی که پمپ
    /// واقعاً به آن بدهکار است.
    /// </summary>
    internal static async Task<List<object>> OweAsync(AppHost host, CancellationToken ct)
    {
        var list = new List<object>();
        foreach (var c in await host.Companies.ListAsync(ct))
        {
            var sum = host.Company.Summarize(c, c.Rows);
            if (sum.AlbaqiAfn <= 0m && sum.AlbaqiUsd <= 0m) continue;
            list.Add(new
            {
                n = c.Name ?? "",
                afn = D(Math.Max(0m, sum.AlbaqiAfn)),
                usd = D(Math.Max(0m, sum.AlbaqiUsd)),
            });
        }
        return list;
    }

    private static async Task<Dictionary<string, object?>> CompaniesAsync(AppHost host, CancellationToken ct)
    {
        var rows = new List<string[]>();
        var months = new List<string>();
        decimal albaqiAfn = 0, albaqiUsd = 0;

        // ⚠️ ‎ListAsync‎ خودش ردیف‌ها را با ‎Include‎ می‌آورد؛ خواندنِ دوبارهٔ
        // هر شرکت فقط یک رفت‌وبرگشتِ اضافه به دیتابیس بود.
        foreach (var c in await host.Companies.ListAsync(ct))
        {
            foreach (var fuel in new[] { FuelType.Petrol, FuelType.Diesel })
            {
                var list = CompanyService.RowsOf(c, fuel).ToList();
                if (list.Count == 0) continue;
                var sum = host.Company.Summarize(c, list);
                albaqiAfn += sum.AlbaqiAfn;
                albaqiUsd += sum.AlbaqiUsd;
                rows.Add(new[]
                {
                    c.Name ?? "", fuel == FuelType.Diesel ? "دیزل" : "پطرول",
                    Shamsi.Money(sum.TotalUsd), Shamsi.Money(sum.TotalAfn),
                    Shamsi.Money(sum.PaidAfn), Shamsi.Money(sum.AlbaqiAfn),
                    Shamsi.Money(sum.AlbaqiUsd),
                });
                months.Add("");
            }
        }

        return Section("شرکت‌های تیل",
            new[] { "شرکت", "تیل", "جمله دالر", "جمله افغانی", "پرداخت", "الباقی افغانی", "الباقی دالر" },
            rows, months,
            new[]
            {
                new[] { "الباقیِ همه (افغانی)", Shamsi.Money(albaqiAfn) },
                new[] { "الباقیِ همه (دالر)", Shamsi.Money(albaqiUsd) },
            });
    }

    private static async Task<Dictionary<string, object?>> AmanatAsync(AppHost host, CancellationToken ct)
    {
        var rows = new List<string[]>();
        var months = new List<string>();
        foreach (var fuel in new[] { FuelType.Petrol, FuelType.Diesel })
            foreach (var a in await host.Amanat.ListAsync(fuel, ct))
                foreach (var r in a.Rows.OrderBy(x => x.SortIndex).ThenBy(x => x.Id))
                {
                    rows.Add(new[]
                    {
                        a.Name ?? "", fuel == FuelType.Diesel ? "دیزل" : "پطرول",
                        r.DateShamsi ?? "", r.Name ?? "",
                        Shamsi.MoneyOrBlank(r.Liters ?? 0m), Shamsi.MoneyOrBlank(r.Taken ?? 0m),
                        r.CloseDate ?? "",
                    });
                    months.Add(MonthOf(r.DateShamsi));
                }

        return Section("امانت",
            new[] { "حساب", "تیل", "تاریخ", "نام", "مقدار", "برداشت", "بستن" },
            rows, months,
            new[] { new[] { "شمارِ ردیف", Shamsi.Money(rows.Count) } });
    }

    // ── ابزارِ کوچک ────────────────────────────────────────────────────────

    // ══════════════════════════════════════════════════════════════════════
    //  نسخهٔ سرورِ حساب — «ده سال داده هم باید برسد»
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// سقفِ عکسی که به سرورِ حساب می‌رود، بایتِ UTF-8.
    ///
    /// ⛔ سرورِ حساب هر فایل را تا ۱٫۵ میلیون نویسه می‌پذیرد
    /// (<c>routes/pump-device.js</c>، <c>too_large</c>) و بدنهٔ هر درخواست را تا ۲
    /// مگابایت (<c>express.json</c>). سنجهٔ ده‌ساله (۱۴۰۵/۰۷/۱۴) نشان داد عکسِ
    /// کامل با <b>یک</b> سال داده ۱٫۶۵ میلیون نویسه است و با ده سال ۱۴ مگابایت —
    /// یعنی گوشیِ بیرون از شبکهٔ پمپ عملاً هیچ‌وقت عکسی نمی‌دید. پس سقف زیرِ هر
    /// دو است، با جای خالی.
    /// </summary>
    public const int CloudBudgetBytes = 1_300_000;

    /// <summary>JSONِ فشرده — نویسهٔ فارسی خودش، نه <c>\uXXXX</c> (سه برابر بزرگ‌تر).</summary>
    public static readonly System.Text.Json.JsonSerializerOptions Compact = new()
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>اندازهٔ یک شیء همان‌طور که روی سیم می‌رود.</summary>
    public static int WireBytes(object? o) =>
        System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(o, Compact).Length;

    /// <summary>
    /// ⛔ <b>همان عکس، کوچک‌شده تا زیرِ <paramref name="budget"/></b> — برای سرورِ
    /// حساب. عکسِ سرورِ خانگی دست نمی‌خورد و همیشه کامل است.
    ///
    /// ترتیبِ بریدن از کم‌ارزش به پرارزش، و هر پله فقط اگر پلهٔ قبل بس نبود:
    /// <list type="number">
    /// <item>ردیف‌های دفترها از <b>قدیمی‌ترین ماه</b> — تا دو ماهِ آخر.</item>
    /// <item>ردیف‌های حسابِ قرض‌داران (جمع‌ها، حال و مانده می‌مانند —
    ///   همان <c>detail = false</c>ی که اپ از قبل می‌شناسد).</item>
    /// <item>ردیف‌های دفترها تا یک ماه، و بعد هیچ.</item>
    /// </list>
    ///
    /// ⚠️ هیچ عددی حساب نمی‌شود و هیچ جمعی عوض نمی‌شود: <c>sum</c>ِ هر بخش همان
    /// جمعِ کامل است، هشدارها و مخزن و نوارِ بالا دست‌نخورده‌اند. فقط ردیف‌ها کم
    /// می‌شوند، و <c>from</c> می‌گوید از کدام ماه به بعد آمده تا اپ بگوید
    /// ماه‌های پیش‌تر فقط در شبکهٔ پمپ است.
    /// </summary>
    public static Dictionary<string, object?> ForCloud(Dictionary<string, object?> snap, int budget = CloudBudgetBytes)
    {
        if (WireBytes(snap) <= budget) return snap;

        var copy = new Dictionary<string, object?>(snap);
        var secs = snap.TryGetValue("sections", out var so) ? so as Dictionary<string, object?> : null;
        var months = (secs?.Values ?? Enumerable.Empty<object?>())
            .OfType<Dictionary<string, object?>>()
            .SelectMany(s => s.TryGetValue("m", out var m) ? m as List<string> ?? new() : new())
            .Where(m => m.Length > 0).Distinct().OrderByDescending(m => m, StringComparer.Ordinal).ToList();

        //  ‎keep‎ = چند ماهِ آخر بماند (‎-1‎ = همه)
        Dictionary<string, object?>? Trim(int keep)
        {
            if (secs is null) return null;
            var from = keep < 0 || keep >= months.Count ? "" : keep == 0 ? "~" : months[keep - 1];
            var outS = new Dictionary<string, object?>();
            foreach (var (id, v) in secs)
            {
                //  بخشی که شکلِ ‎{rows, m}‎ ندارد (شرکت‌ها، امانت) دست نمی‌خورد
                if (v is not Dictionary<string, object?> sec || from.Length == 0
                    || !sec.ContainsKey("rows") || !sec.ContainsKey("m")) { outS[id] = v; continue; }
                var rows = sec.TryGetValue("rows", out var r) ? r as List<string[]> ?? new() : new();
                var ms = sec.TryGetValue("m", out var mm) ? mm as List<string> ?? new() : new();
                var nr = new List<string[]>(); var nm = new List<string>();
                for (var i = 0; i < rows.Count; i++)
                {
                    var m = i < ms.Count ? ms[i] : "";
                    //  ردیفِ بی‌ماه (مثلاً خریدِ بی‌تاریخ) کوچک است و در ماهی نمی‌افتد
                    if (from != "~" && (m.Length == 0 || string.CompareOrdinal(m, from) >= 0))
                    { nr.Add(rows[i]); nm.Add(m); }
                }
                outS[id] = new Dictionary<string, object?>(sec) { ["rows"] = nr, ["m"] = nm };
            }
            copy["from"] = from == "~" ? "" : from;
            return outS;
        }

        bool Fits(int keep)
        {
            copy["sections"] = Trim(keep);
            return WireBytes(copy) <= budget;
        }

        //  بیشترین ماه‌هایی که جا می‌شوند — دست‌کم دو ماه (جست‌وجوی دودویی)
        bool Most()
        {
            int lo = Math.Min(2, months.Count), hi = months.Count;
            if (Fits(hi)) return true;
            if (!Fits(lo)) return false;
            while (lo < hi - 1) { var mid = (lo + hi) / 2; if (Fits(mid)) lo = mid; else hi = mid; }
            Fits(lo);
            return true;
        }

        //  ۱) فقط ماه‌های قدیمیِ دفترها
        if (Most()) return copy;

        //  ۲) ردیف‌های حسابِ قرض‌داران
        if (snap.TryGetValue("debtors", out var dO) && dO is List<object?> people)
        {
            copy["debtors"] = people.Select(p => p is Dictionary<string, object?> d
                    && d.TryGetValue("accounts", out var ac) && ac is System.Collections.IEnumerable list
                ? new Dictionary<string, object?>(d)
                {
                    ["accounts"] = list.Cast<object?>().Select(a => a is Dictionary<string, object?> ad
                        ? new Dictionary<string, object?>(ad) { ["rows"] = new List<string[]>() } : a).ToList(),
                }
                : p).ToList();
            copy["detail"] = false;
        }
        if (Most()) return copy;

        //  ۳) یک ماه، و بعد هیچ ردیفی از دفترها
        if (Fits(Math.Min(1, months.Count))) return copy;
        Fits(0);
        return copy;
    }

    private static Dictionary<string, object?> Section(
        string title, IEnumerable<string> head, IEnumerable<string[]> rows,
        IEnumerable<string> months, IEnumerable<string[]> totals) =>
        new()
        {
            ["t"] = title,
            ["head"] = head.ToList(),
            ["rows"] = rows.ToList(),
            ["m"] = months.ToList(),
            ["sum"] = totals.ToList(),
        };

    /// <summary>«1405/06/22» ⇒ «1405/06». خالی یعنی تاریخ نداشت.</summary>
    public static string MonthOf(string? dateShamsi)
    {
        var d = (dateShamsi ?? "").Trim();
        var cut = d.LastIndexOf('/');
        return cut > 0 && d.Count(c => c == '/') >= 2 ? d[..cut] : "";
    }

    /// <summary>
    /// ⚠️ ‎decimal‎ در JSON به ‎double‎ می‌رود و رقمِ پول را می‌شکند. عددها
    /// این‌جا با دو رقمِ اعشار گرد و به ‎double‎ی امن تبدیل می‌شوند — همان
    /// دقتی که خودِ صفحه نشان می‌دهد. عددهای «نمایشی» جدا از این، رشته‌اند.
    /// </summary>
    private static double D(decimal v) => (double)Math.Round(v, 2, MidpointRounding.AwayFromZero);
}
