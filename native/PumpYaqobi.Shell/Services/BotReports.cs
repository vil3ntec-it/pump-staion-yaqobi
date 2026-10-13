using System.Text.Json;
using PumpYaqobi.Application.Localization;
using PumpYaqobi.Application.Services;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;
using PumpYaqobi.Reporting.Pdf;
using PumpYaqobi.Services.Data;
using QuestPDF.Fluent;

namespace PumpYaqobi.App.Services;

/// <summary>یک درخواستِ بات از همین برنامه — <c>reqs[]</c>ِ پاسخِ <c>/rate</c>.</summary>
public sealed record BotRequest(string Id, string Kind, JsonElement Arg);

public sealed record BotFile(string Name, string Mime, byte[] Data);

/// <summary>جوابِ برنامه به بات: فایل‌ها (عکس/PDF)، متنِ کوتاهِ زیرِ عکس، و چیزهای اضافه (لینکِ کیو‌آر).</summary>
public sealed record BotReport(bool Ok, string Error, string Caption, List<BotFile> Files, Dictionary<string, string> Extra)
{
    public static BotReport Fail(string why) => new(false, why, "", new(), new());
}

/// <summary>یک مخزن برای عکس — همان خاصیت‌های ‎TankGauge‎ِ بخشِ مخزن.</summary>
public sealed record TankShot(string Title, string Sub, double FillPercent, string FillText,
                              double ThresholdPercent, string Caption, bool Low);

/// <summary>
/// ══ عکس و PDF از خودِ برنامه برای باتِ تلگرام (۱۴۰۵/۰۷/۲۱) ══════════════════
///
/// خواستهٔ صاحب ریپو: «همان عکس‌هایی باشد که در برنامه است — مخزن همان باشد…
/// حسابِ فلانی را بگیرم: عکس با رسید، بردگی، الباقی و واحد، و PDF و کیو‌آرش…
/// کیفیت افت نکند… پیام ساده باشد.»
///
///   بات ⇒ سرورِ حساب (صفِ حافظه) ⇒ ‎/rate‎ی همین برنامه (‎reqs‎)
///       ⇒ <see cref="BuildAsync"/> ⇒ ‎POST /report/:id‎ ⇒ بات ⇒ تلگرام
///
/// ⛔ <b>هیچ نقاشی یا حسابِ تازه‌ای این‌جا نیست</b>:
///   • عکسِ مخزن = خودِ کنترلِ ‎TankGauge‎ (<see cref="TankRenderer"/>، از لایهٔ رابط)
///   • عددهای مخزن = ‎StationSnapshot.TankAsync‎ (همان بخشِ مخزن و داشبورد)
///   • سندِ خریدها = ‎StorageReport‎ (همان «سندِ مخزن»ِ دکمهٔ PDF)
///   • کارت و PDFِ قرض‌دار = ‎AcctSnapshots‎ (همان کیو‌آر) و ‎DebtorStatementReport‎
///     (همان PDFِ صفحهٔ حساب) با همان ورودی‌های ‎PersonViewModel.PdfAsync‎
///   • کیو‌آر = ‎AcctLive.EnsureAsync‎ + ‎AcctView.Url‎ (همان دکمهٔ «📲»)
/// ⛔ هیچ فایلی روی دیسک نوشته نمی‌شود؛ همه در حافظه می‌ماند و فرستاده می‌شود.
/// </summary>
public static class BotReports
{
    /// <summary>
    /// کشیدنِ مخزن‌ها با خودِ ‎TankGauge‎ (PNG). لایهٔ رابط (‎App‎) هنگامِ آغاز می‌گذاردش؛
    /// ‎null‎ ⇒ بی عکس، فقط متن و PDF.
    /// </summary>
    public static Func<IReadOnlyList<TankShot>, string, Task<byte[]?>>? TankRenderer { get; set; }

    /// <summary>سقفِ ردیف‌های «آخرین خریدها» در متن.</summary>
    public const int LastBuys = 3;

    public static async Task<BotReport> BuildAsync(AppHost host, BotRequest req, CancellationToken ct)
    {
        try
        {
            return req.Kind switch
            {
                "tanks" => await TanksAsync(host, ct),
                "debtor" => await DebtorAsync(host, req.Arg, ct),
                _ => BotReport.Fail("این برنامه این درخواست را نمی‌شناسد؛ برنامه را به‌روز کنید."),
            };
        }
        catch (OperationCanceledException) { throw; }
        catch (PumpYaqobi.Application.Security.PermissionDeniedException)
        {
            return BotReport.Fail("کاربری که در برنامهٔ کامپیوتر وارد است اجازهٔ دیدنِ این را ندارد.");
        }
        catch
        {
            return BotReport.Fail("برنامهٔ کامپیوتر نتوانست بسازد؛ دوباره امتحان کنید.");
        }
    }

    // ══════════════════════════════════════════════════════════════════════
    //  🛢 مخزن‌ها + بدهیِ شرکت‌ها + خریدها
    // ══════════════════════════════════════════════════════════════════════

    private static async Task<BotReport> TanksAsync(AppHost host, CancellationToken ct)
    {
        var tank = await StationSnapshot.TankAsync(host, ct);
        var threshold = host.Settings.GetDecimal(SettingsService.LowStockThreshold, 1000m);
        var shots = new List<TankShot>();
        var lines = new List<string>();
        var files = new List<BotFile>();

        foreach (var (key, fuel, label) in new[] { ("petrol", FuelType.Petrol, "پطرول"), ("diesel", FuelType.Diesel, "دیزل") })
        {
            if (tank.GetValueOrDefault(key) is not Dictionary<string, object?> t) continue;
            var show = Num(t, "show");
            var cap = Num(t, "cap");
            var pct = StationSnapshot.FillPercent(show, cap);
            var low = t.GetValueOrDefault("low") is true;
            //  ⛔ همان سه خاصیتِ بخشِ مخزن (‎StorageSectionViewModel.ShowTank‎)
            shots.Add(new TankShot(
                label,
                Shamsi.Money(Math.Round(show, 0, MidpointRounding.AwayFromZero)) + " از " + Shamsi.Money(cap) + " لیتر",
                (double)pct,
                ((double)pct).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + "%",
                cap > 0m ? (double)Math.Min(100m, Math.Max(0m, threshold / cap * 100m)) : 0,
                "حد هشدار: " + Shamsi.Money(threshold) + " لیتر",
                low));
            //  ⛔ هر تیل بخشِ خودش، با یک خطِ خالی پیش از آن (۱۴۰۵/۰۷/۲۲: «نوشته‌ها به هم چسپیده‌اند»)
            lines.Add("");
            lines.Add((low ? "🔴 " : "🟢 ") + label);
            lines.Add(Shamsi.Money(Math.Round(show, 0, MidpointRounding.AwayFromZero)) + " لیتر · "
                      + Shamsi.Money(Math.Round(pct, 0, MidpointRounding.AwayFromZero)) + "٪ پر");

            //  سندِ مخزن — همان دکمهٔ PDFِ بخشِ مخزن
            var buys = await host.StorageData.PurchasesAsync(fuel, ct);
            var sold = await host.StorageData.ShiftSumsAsync(fuel, ct);
            var dips = await host.StorageData.DipsAsync(fuel, ct);
            var state = host.Storage.Tank(buys, sold.Sale, threshold, dips);
            if (buys.Count > 0)
            {
                var input = new StorageReportInput(fuel, buys, state, DocDates.Line());
                files.Add(new BotFile("خریدهای " + label, "application/pdf", Pdf(new StorageReport(input, host.Storage))));
                foreach (var b in buys.Take(LastBuys))
                {
                    var n = host.Storage.Compute(b.Kg, b.Density, b.PriceTon, b.UsdRate);
                    lines.Add("▫️ خرید " + (b.DateShamsi ?? "") + " — " + Shamsi.Money(Math.Round(n.Liters, 0, MidpointRounding.AwayFromZero))
                              + " لیتر" + (string.IsNullOrWhiteSpace(b.Seller) ? "" : " — " + b.Seller));
                }
            }
        }

        //  🏢 بدهیِ شرکت‌های تیل — همان ‎OweAsync‎ی گزارشِ صبح
        var owe = new List<string>();
        foreach (var c in await host.Companies.ListAsync(ct))
        {
            var sum = host.Company.Summarize(c, c.Rows);
            if (sum.AlbaqiAfn <= 0m && sum.AlbaqiUsd <= 0m) continue;
            var parts = new List<string>();
            if (sum.AlbaqiAfn > 0m) parts.Add(Shamsi.Money(Math.Round(sum.AlbaqiAfn, 0, MidpointRounding.AwayFromZero)) + " ؋");
            if (sum.AlbaqiUsd > 0m) parts.Add(Shamsi.Money(Math.Round(sum.AlbaqiUsd, 2)) + " $");
            owe.Add("▫️ " + (c.Name ?? "") + ": " + string.Join(" · ", parts));
        }
        if (owe.Count > 0) { lines.Add(""); lines.Add("🏢 بدهی به شرکت‌ها"); lines.AddRange(owe.Take(8)); }

        if (TankRenderer is { } draw && shots.Count > 0)
        {
            var png = await draw(shots, "⛽ " + PumpBrand.Name + " — مخزن‌ها · " + Shamsi.Today());
            if (png is { Length: > 0 }) files.Insert(0, new BotFile("مخزن‌ها", "image/png", png));
        }
        if (files.Count == 0 && lines.Count == 0) return BotReport.Fail("هنوز مخزنی در برنامه ثبت نشده است.");
        return new BotReport(true, "", "🛢 مخزن‌ها · " + Shamsi.Today() + "\n" + string.Join("\n", lines),
                             files.Take(4).ToList(), new());
    }

    // ══════════════════════════════════════════════════════════════════════
    //  👤 حسابِ یک قرض‌دار: کارت (عکس) + PDF + کیو‌آر
    // ══════════════════════════════════════════════════════════════════════

    private static async Task<BotReport> DebtorAsync(AppHost host, JsonElement arg, CancellationToken ct)
    {
        long id = 0;
        if (arg.ValueKind == JsonValueKind.Object && arg.TryGetProperty("id", out var i)
            && i.ValueKind == JsonValueKind.Number) i.TryGetInt64(out id);
        var name = arg.ValueKind == JsonValueKind.Object && arg.TryGetProperty("name", out var n)
                   && n.ValueKind == JsonValueKind.String ? (n.GetString() ?? "").Trim() : "";

        if (id <= 0 && name.Length > 0)
        {
            var hits = new List<Debtor>();
            foreach (var noInv in new[] { false, true }) hits.AddRange(await host.Debtors.ListAsync(noInv, name, ct));
            var exact = hits.Where(h => string.Equals(h.Name?.Trim(), name, StringComparison.Ordinal)).ToList();
            var pick = exact.Count == 1 ? exact : hits;
            if (pick.Count == 0) return BotReport.Fail("قرض‌داری با نامِ «" + name + "» پیدا نشد.");
            if (pick.Count > 1)
                return BotReport.Fail("چند قرض‌دار با این نام هست: " + string.Join("، ", pick.Take(6).Select(p => p.Name))
                                      + " — نامِ کامل را بنویسید.");
            id = pick[0].Id;
        }
        if (id <= 0) return BotReport.Fail("کدام قرض‌دار؟ نامش را بنویسید.");

        var person = await host.Debtors.LoadFullAsync(id, ct);
        if (person is null) return BotReport.Fail("این قرض‌دار دیگر در برنامه نیست.");

        var calc = host.Debt;
        var files = new List<BotFile>();
        var extra = new Dictionary<string, string>();
        var caption = new List<string>();

        //  ⛔ فقط حسابِ اصلیِ همین شخص (۱۴۰۵/۰۷/۲۲، با عکس): «چرا حسابِ هارون که در حسابِ اصلیِ
        //  ح هاشم فرعی است را هم با آن می‌دهی؟» — حسابِ فرعی دکمه و درخواستِ خودش را دارد.
        var acct = person.MainAccount;
        var snap = AcctSnapshots.ForDebtAccount(person.Name, null, acct, calc);

        //  ⛔ دفتری که واقعاً ردیف یا عدد دارد (۱۴۰۵/۰۷/۲۲): حسابی که حالتش «پول» است ولی تیل برده،
        //  کارتِ «۰ افغانی» می‌گرفت. هر دفتری که چیزی دارد یک کارت؛ هیچ ⇒ همان دفترِ فعال.
        var books = snap.Books.Where(HasData).Take(2).ToList();
        if (books.Count == 0 && snap.Books.Count > 0) books.Add(snap.Books[0]);

        var st = calc.Status(new List<DebtAccount> { acct }).Worst;
        var (status, bad) = st switch
        {
            DebtStatus.Out => ("🔴 تمام شده", true),
            DebtStatus.Low => ("🟡 رو به تمام", true),
            _ => ("🟢 روبه‌راه", false),
        };

        //  کیو‌آر — همان دکمهٔ «📲»ِ صفحهٔ حساب
        var live = await AcctLive.EnsureAsync(host, acct, ct);
        var link = AcctLive.LinkFor(host, snap, Marker(person.Id, null), live);
        var qr = PumpYaqobi.Services.Vision.QrWriter.EncodePng(link);
        extra["qr"] = link;

        caption.Add("👤 " + person.Name);
        caption.Add(status);
        foreach (var book in books)
        {
            var money = book.Unit == "افغانی";
            var albaqi = book.Summary.FirstOrDefault(s => s.Length > 1 && s[0] == "الباقی")?[1] ?? "0";

            var card = new DebtorCardInput(PumpBrand.Name, person.Name, "", book.Unit, snap.Date,
                                           book.Fuels, albaqi, status, bad, qr);
            files.Add(new BotFile(person.Name + " — " + book.Title, "image/png", new DebtorCardImage(card).Png()));

            //  PDF — همان دفترِ همین کارت (نه حالتِ حساب)
            var rows = money ? acct.MoneyRows : acct.FuelRows;
            var t = calc.SplitTotals(rows);
            var input = new DebtorStatementInput(
                PersonName: person.Name,
                AccountTitle: "حساب " + person.Name + (books.Count > 1 ? " — " + book.Title : ""),
                IsMoneyLedger: money,
                Filter: null,
                Rows: rows,
                PercentPetrol: calc.PercentOf(acct, FuelType.Petrol),
                PercentDiesel: calc.PercentOf(acct, FuelType.Diesel),
                RasidPetrol: money ? t.Petrol.Rasid : t.Petrol.RasidFuel,
                RasidDiesel: money ? t.Diesel.Rasid : t.Diesel.RasidFuel,
                BordPetrol: money ? t.Petrol.Bardagi : t.Petrol.Liters,
                BordDiesel: money ? t.Diesel.Bardagi : t.Diesel.Liters,
                RasidRowsPetrol: t.Petrol.Rasid, RasidRowsDiesel: t.Diesel.Rasid,
                Dates: DocDates.Line());
            files.Add(new BotFile("حساب " + person.Name + (books.Count > 1 ? " — " + book.Title : ""), "application/pdf",
                                  Pdf(new DebtorStatementReport(input, calc))));

            caption.Add("");
            caption.Add("الباقی: " + albaqi + " " + book.Unit);
        }
        return new BotReport(true, "", string.Join("\n", caption), files.Take(4).ToList(), extra);
    }

    /// <summary>دفتری که ردیف یا عددی غیرِ صفر دارد — همان ‎AcctSnapshots.HasAnything‎.</summary>
    private static bool HasData(AcctBook b)
        => b.Rows.Count > 0 || b.Summary.Any(s => s.Length > 1 && Shamsi.Num(s[1]) != 0m);

    // ── ابزار ──────────────────────────────────────────────────────────────

    /// <summary>
    /// نشانهٔ حساب در کیو‌آر — عینِ ‎AcctLink.Build(personId, sub)‎ِ لایهٔ رابط (این لایه
    /// به آن دسترسی ندارد). ⚠️ با آن عوض شود؛ آزمونِ ‎BotReportsTests‎ برابریِ دو را می‌سنجد.
    /// </summary>
    public static string Marker(long personId, string? subId) =>
        "#roview-debt-" + personId + (string.IsNullOrWhiteSpace(subId) ? "" : "~" + subId) + "-pdf";

    private static byte[] Pdf(QuestPDF.Infrastructure.IDocument doc)
    {
        PdfEngine.Initialize();
        using var ms = new MemoryStream();
        doc.GeneratePdf(ms);
        return ms.ToArray();
    }

    private static decimal Num(Dictionary<string, object?> d, string k) => d.GetValueOrDefault(k) switch
    {
        double x => (decimal)x,
        decimal m => m,
        int i => i,
        long l => l,
        _ => 0m,
    };
}
