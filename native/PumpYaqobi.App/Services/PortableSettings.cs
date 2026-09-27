using System.Text.Json;
using System.Text.Json.Nodes;
using PumpYaqobi.Reporting.Pdf;

namespace PumpYaqobi.App.Services;

/// <summary>
/// ══ تنظیماتِ «قابلِ بردن» — آن‌چه با «فایلِ کاملِ برنامه» به کامپیوترِ دیگر می‌رود ══
///
/// خواستهٔ صاحب ریپو (۱۴۰۵/۰۷/۱۵): «تمامِ حساب‌ها و تم‌ها و تنظیماتی که قبلاً
/// درست کرده بود.» پس تم، ترتیبِ نوار، ظاهرِ جدول‌ها، پهنای ستون‌ها، اندازهٔ
/// نوشتهٔ هر بخش، تنظیمِ ورقِ چاپ و مانندِ این‌ها.
///
/// ⛔ <b>فهرستِ سفید است، نه فهرستِ سیاه.</b> فقط همین نام‌ها بیرون می‌روند و
/// فقط همین نام‌ها خوانده می‌شوند؛ خانه‌ای که فردا به <see cref="AppSettings"/>
/// اضافه شود خودبه‌خود <b>نمی‌رود</b>. دلیلش این است که آن کلاس رازها را هم
/// دارد — توکنِ حساب، توکنِ دستگاه، مجوز، کلیدِ عمومی، رمزِ سرورِ خانگی — و
/// فایلی که روی فلش دستِ چند نفر می‌گردد هیچ‌کدام را نباید داشته باشد.
/// <c>PortableSettingsTests</c> هر یک از آن‌ها را جدا قدغن کرده.
///
/// ⛔ <b>و بندهای این کامپیوتر هم نمی‌روند</b>: کدِ پمپ، نشانیِ سرورِ خانگی،
/// حساب، اندازهٔ پنجره و چاپگرِ آخر — این‌ها مالِ همین دستگاه‌اند و روی
/// کامپیوترِ دیگر یا غلط‌اند یا خطرناک (نوشتن روی پوشهٔ پمپِ دیگری).
///
/// ⚠️ هر مقدار هنگامِ آوردن <b>سنجیده</b> می‌شود (بازه، نوع، رنگِ خوانا)؛
/// مقدارِ خراب نادیده گرفته می‌شود، نه این‌که کلِ آوردن را بشکند.
/// </summary>
public static class PortableSettings
{
    /// <summary>نسخهٔ همین شکل — فایلِ تازه‌تر فقط آن‌چه را بشناسیم می‌گیرد.</summary>
    public const int Version = 1;

    /// <summary>نام‌هایی که می‌روند — و هیچ نامِ دیگری.</summary>
    public static readonly IReadOnlyList<string> Fields = new[]
    {
        nameof(AppSettings.ThemeId),
        nameof(AppSettings.NavOrder),
        nameof(AppSettings.CalcWidth), nameof(AppSettings.CalcHeight), nameof(AppSettings.CalcLarge),
        nameof(AppSettings.ParchaChainCheck),
        nameof(AppSettings.TableBorderColor), nameof(AppSettings.TableLine),
        nameof(AppSettings.TableHeadLine), nameof(AppSettings.TableSumLine),
        nameof(AppSettings.SecFontScales), nameof(AppSettings.ColumnWidths),
        nameof(AppSettings.NoteFontScale),
        nameof(AppSettings.PrintSetup),
        nameof(AppSettings.ReportErrorsOff),
    };

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    /// <summary>متنِ JSONِ تنظیماتِ قابلِ بردن از روی یک نمونه.</summary>
    public static string Capture(AppSettings s)
    {
        var o = new JsonObject
        {
            ["v"] = Version,
            [nameof(AppSettings.ThemeId)] = s.ThemeId,
            [nameof(AppSettings.NavOrder)] = s.NavOrder,
            [nameof(AppSettings.CalcWidth)] = s.CalcWidth,
            [nameof(AppSettings.CalcHeight)] = s.CalcHeight,
            [nameof(AppSettings.CalcLarge)] = s.CalcLarge,
            [nameof(AppSettings.ParchaChainCheck)] = s.ParchaChainCheck,
            [nameof(AppSettings.TableBorderColor)] = s.TableBorderColor,
            [nameof(AppSettings.TableLine)] = s.TableLine,
            [nameof(AppSettings.TableHeadLine)] = s.TableHeadLine,
            [nameof(AppSettings.TableSumLine)] = s.TableSumLine,
            [nameof(AppSettings.NoteFontScale)] = s.NoteFontScale,
            [nameof(AppSettings.ReportErrorsOff)] = s.ReportErrorsOff,
        };

        var fonts = new JsonObject();
        foreach (var (k, v) in s.SecFontScales) fonts[k] = v;
        o[nameof(AppSettings.SecFontScales)] = fonts;

        //  پهنای در صف هم — وگرنه ستونی که همین حالا کشیده شده جا می‌ماند
        var widths = new JsonObject();
        foreach (var (k, v) in s.ColumnWidths) widths[k] = new JsonArray(v.Select(x => (JsonNode?)x).ToArray());
        foreach (var (k, v) in AppSettings.PendingColumnWidths())
            widths[k] = new JsonArray(v.Select(x => (JsonNode?)x).ToArray());
        o[nameof(AppSettings.ColumnWidths)] = widths;

        if (s.PrintSetup is not null)
        {
            try { o[nameof(AppSettings.PrintSetup)] = JsonSerializer.SerializeToNode(s.PrintSetup, Json); }
            catch { /* تنظیمِ ورقِ نانوشتنی — بی آن */ }
        }
        return o.ToJsonString(Json);
    }

    /// <summary>
    /// آن‌چه در فایل بود و پذیرفته شد، روی <paramref name="s"/> می‌نشیند
    /// (<b>ذخیره نمی‌کند</b> — آن کارِ صدازننده است). خروجی نامِ خانه‌هایی است که
    /// نشستند؛ متنِ خراب ⇒ فهرستِ خالی و <paramref name="s"/> دست‌نخورده.
    /// </summary>
    public static IReadOnlyList<string> ApplyTo(string? json, AppSettings s)
    {
        var done = new List<string>();
        if (string.IsNullOrWhiteSpace(json)) return done;
        JsonObject o;
        try { o = JsonNode.Parse(json) as JsonObject ?? throw new FormatException(); }
        catch { return done; }

        if (Str(o, nameof(AppSettings.ThemeId)) is { Length: > 0 and <= 40 } theme)
        { s.ThemeId = theme; done.Add(nameof(AppSettings.ThemeId)); }

        //  ترتیبِ نوار فقط شناسه است؛ شناسهٔ ناشناس را خودِ ‎NavOrder.Arrange‎ رد می‌کند
        if (Str(o, nameof(AppSettings.NavOrder)) is { } nav && nav.Length <= 2000
            && nav.All(c => char.IsAsciiLetterOrDigit(c) || c is ',' or '-' or '_'))
        { s.NavOrder = nav; done.Add(nameof(AppSettings.NavOrder)); }

        if (Num(o, nameof(AppSettings.CalcWidth)) is { } cw)
        { s.CalcWidth = Math.Clamp(cw, 230, 900); done.Add(nameof(AppSettings.CalcWidth)); }
        if (Num(o, nameof(AppSettings.CalcHeight)) is { } ch)
        { s.CalcHeight = Math.Clamp(ch, 300, 1100); done.Add(nameof(AppSettings.CalcHeight)); }
        if (Bool(o, nameof(AppSettings.CalcLarge)) is { } cl)
        { s.CalcLarge = cl; done.Add(nameof(AppSettings.CalcLarge)); }
        if (Bool(o, nameof(AppSettings.ParchaChainCheck)) is { } pc)
        { s.ParchaChainCheck = pc; done.Add(nameof(AppSettings.ParchaChainCheck)); }
        if (Bool(o, nameof(AppSettings.ReportErrorsOff)) is { } ro)
        { s.ReportErrorsOff = ro; done.Add(nameof(AppSettings.ReportErrorsOff)); }

        //  رنگِ خط: خالی (رنگِ خودِ تم) یا ‎#rrggbb‎ / ‎#aarrggbb‎ — هیچ چیزِ دیگر
        if (Str(o, nameof(AppSettings.TableBorderColor)) is { } color
            && (color.Length == 0 || IsHex(color)))
        { s.TableBorderColor = color; done.Add(nameof(AppSettings.TableBorderColor)); }
        if (Num(o, nameof(AppSettings.TableLine)) is { } tl)
        { s.TableLine = Math.Clamp(tl, 1, 4); done.Add(nameof(AppSettings.TableLine)); }
        if (Num(o, nameof(AppSettings.TableHeadLine)) is { } th)
        { s.TableHeadLine = Math.Clamp(th, 1, 4); done.Add(nameof(AppSettings.TableHeadLine)); }
        if (Num(o, nameof(AppSettings.TableSumLine)) is { } ts)
        { s.TableSumLine = Math.Clamp(ts, 1, 4); done.Add(nameof(AppSettings.TableSumLine)); }
        if (Num(o, nameof(AppSettings.NoteFontScale)) is { } nf)
        { s.NoteFontScale = Math.Round(Math.Clamp(nf, 0.5, 2.5), 2); done.Add(nameof(AppSettings.NoteFontScale)); }

        if (o[nameof(AppSettings.SecFontScales)] is JsonObject fonts)
        {
            var map = new Dictionary<string, double>();
            foreach (var (k, v) in fonts)
            {
                if (!IsKey(k) || Num(v) is not { } f) continue;
                map[k] = Math.Round(Math.Clamp(f, 0.6, 2.2), 2);
            }
            s.SecFontScales = map;
            done.Add(nameof(AppSettings.SecFontScales));
        }

        if (o[nameof(AppSettings.ColumnWidths)] is JsonObject cols)
        {
            var map = new Dictionary<string, double[]>();
            foreach (var (k, v) in cols)
            {
                if (k.Length is 0 or > 400 || v is not JsonArray arr || arr.Count is 0 or > 200) continue;
                var w = new double[arr.Count];
                var ok = true;
                for (var i = 0; i < arr.Count && ok; i++)
                {
                    if (Num(arr[i]) is { } x && x >= 0 && x <= 10_000) w[i] = x;
                    else ok = false;
                }
                if (ok) map[k] = w;
            }
            s.ColumnWidths = map;
            done.Add(nameof(AppSettings.ColumnWidths));
        }

        if (o[nameof(AppSettings.PrintSetup)] is JsonObject ps)
        {
            try
            {
                var setup = ps.Deserialize<PageSetup>(Json);
                if (setup is not null) { s.PrintSetup = setup; done.Add(nameof(AppSettings.PrintSetup)); }
            }
            catch { /* تنظیمِ ورقِ خراب — همان تنظیمِ فعلی می‌ماند */ }
        }
        return done;
    }

    // ── ابزار ─────────────────────────────────────────────────────────────

    private static string? Str(JsonObject o, string k)
    {
        try { return o[k] is JsonValue v && v.TryGetValue<string>(out var s) ? s : null; }
        catch { return null; }
    }

    private static double? Num(JsonObject o, string k) => Num(o[k]);

    private static double? Num(JsonNode? n)
    {
        try
        {
            if (n is not JsonValue v || !v.TryGetValue<double>(out var d)) return null;
            return double.IsFinite(d) ? d : null;
        }
        catch { return null; }
    }

    private static bool? Bool(JsonObject o, string k)
    {
        try { return o[k] is JsonValue v && v.TryGetValue<bool>(out var b) ? b : null; }
        catch { return null; }
    }

    private static bool IsKey(string k) =>
        k.Length is > 0 and <= 60 && k.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.');

    private static bool IsHex(string c) =>
        c.Length is 7 or 9 && c[0] == '#' && c.Skip(1).All(char.IsAsciiHexDigit);
}
