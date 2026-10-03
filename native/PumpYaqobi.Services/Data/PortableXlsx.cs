using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace PumpYaqobi.Services.Data;

/// <summary>
/// ══ فایلِ اکسلِ یک حساب یا بخش (‎.xlsx‎) — بی هیچ بستهٔ تازه ═══════════════════
///
/// یک کارپوشهٔ واقعیِ اکسل: برای هر جدول یک برگهٔ دیدنی (راست‌به‌چپ، با نامِ فارسیِ
/// ستون‌ها)، و یک برگهٔ <b>پنهانِ «_pump»</b> که عکسِ قابلِ بازگرداندنِ همان ردیف‌ها
/// را دارد (‎SyncStore.ExportPortable‎). ورودی <b>فقط</b> از برگهٔ پنهان می‌خواند —
/// پس دست‌کاریِ برگه‌های دیدنی در اکسل هیچ عددی را در برنامه خراب نمی‌کند.
///
/// ⚠️ اکسل هنگامِ «ذخیره» نوشته‌ها را به ‎sharedStrings‎ می‌برد و ترتیبِ برگه‌ها را
/// می‌تواند عوض کند؛ خواننده هر دو را می‌فهمد و برگه را با <b>نام</b> پیدا می‌کند.
/// </summary>
public static class PortableXlsx
{
    public const string Extension = ".xlsx";
    private const string DataSheet = "_pump";
    private const string Marker = "PUMPYAQOBI-PART-1";
    private const int CellMax = 30000;      // اکسل یک خانه را تا ۳۲٬۷۶۷ نویسه می‌پذیرد

    private static readonly XNamespace S = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace R = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

    /// <summary>نامِ فارسیِ ستون‌های رایج؛ بقیه همان نامِ خودشان.</summary>
    private static readonly Dictionary<string, string> Labels = new(StringComparer.Ordinal)
    {
        ["DateShamsi"] = "تاریخ", ["Name"] = "نام", ["Title"] = "نام", ["Description"] = "شرح",
        ["Amount"] = "مبلغ", ["Currency"] = "ارز", ["Note"] = "یادداشت", ["Kind"] = "نوع",
        ["Liters"] = "لیتر", ["Fuel"] = "تیل", ["Price"] = "فی", ["PricePerLiter"] = "فی لیتر",
        ["Rate"] = "نرخ", ["Bardagi"] = "بردگی", ["Poul"] = "پول", ["PoulCurrency"] = "ارزِ پول",
        ["Ton"] = "تن", ["Kg"] = "کیلو", ["Usd"] = "دالر", ["Start"] = "شروع", ["End"] = "ختم",
        ["Debt"] = "قرض", ["Sale"] = "فروش (لیتر)", ["Money"] = "پول", ["Profit"] = "فایده",
        ["Phone"] = "شماره", ["InvoiceNumber"] = "شمارهٔ فاکتور", ["CustomerName"] = "مشتری",
        ["Status"] = "حال", ["PumpNum"] = "شمارهٔ پایه", ["Worker"] = "کارمند", ["ReportNum"] = "شمارهٔ گزارش",
        ["Rasid"] = "رسید", ["RasidFuel"] = "رسیدِ تیل", ["Unit"] = "واحد", ["Hawala"] = "حواله",
        ["MonthKey"] = "ماه", ["Albaqi"] = "الباقی", ["Mandagi"] = "ماندگی", ["Detail"] = "جزئیات",
        ["Customer"] = "مشتری", ["Liter"] = "لیتر", ["Tons"] = "تن", ["Dollar"] = "دالر", ["Afghani"] = "افغانی",
    };

    private static readonly Dictionary<string, string> TableTitles = new(StringComparer.Ordinal)
    {
        ["Debtor"] = "قرض‌دار", ["DebtAccount"] = "حساب‌ها", ["DebtRow"] = "ردیف‌های حساب",
        ["RasidEntry"] = "رسیدها", ["DebtTableArchive"] = "آرشیوِ حساب", ["Invoice"] = "فاکتورها",
        ["TilCompany"] = "شرکت", ["CompanyRow"] = "ردیف‌های شرکت", ["CompanyTableArchive"] = "آرشیوِ شرکت",
        ["SafeEntry"] = "گاوصندوق", ["Expense"] = "مصارف", ["ExchangeRow"] = "صرافی", ["RetailRow"] = "چکنه",
        ["ExtraIncome"] = "درآمد", ["WaraqEntry"] = "ورق‌ها", ["WaraqShift"] = "شیفت‌های ورق",
        ["WaraqPump"] = "پایه‌های ورق", ["WaraqTransaction"] = "ردیف‌های ورق",
        ["ParchaReport"] = "پارچه‌ها", ["ShiftData"] = "شیفت‌های پارچه", ["StaffMember"] = "کارمندان",
    };

    /// <summary>
    /// فایل را می‌نویسد — ⛔ اتمی: اول ‎.part‎ و بعد جابه‌جایی، پس فایلِ قبلیِ همان نام
    /// (بکاپِ «به‌روزشدنی») تا لحظهٔ آخر سالم می‌ماند.
    /// </summary>
    public static void Write(string path, string title, PortableExport export)
    {
        var part = path + ".part";
        if (File.Exists(part)) File.Delete(part);
        using (var fs = new FileStream(part, FileMode.CreateNew))
        using (var zip = new ZipArchive(fs, ZipArchiveMode.Create))
        {
            var sheets = new List<(string Name, string File, bool Hidden)>();
            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var n = 0;
            foreach (var t in export.Tables)
            {
                var name = SheetName(TableTitles.GetValueOrDefault(t.Entity, t.Entity), used);
                var file = $"sheet{++n}.xml";
                sheets.Add((name, file, false));
                //  ⛔ برگهٔ دیدنی فقط ستون‌هایی را دارد که نامِ فارسی دارند — بقیه فنی‌اند
                //  و همه‌شان در برگهٔ پنهان برای «آوردن» هست. عدد همان عددِ دفتر است.
                var keep = Enumerable.Range(0, t.Columns.Count).Where(i => Labels.ContainsKey(t.Columns[i])).ToList();
                if (keep.Count == 0) keep = Enumerable.Range(0, t.Columns.Count).ToList();
                var rows = new List<IReadOnlyList<object?>> { keep.Select(i => (object?)Labels.GetValueOrDefault(t.Columns[i], t.Columns[i])).ToList() };
                rows.AddRange(t.Rows.Select(r => (IReadOnlyList<object?>)keep.Select(i => i < r.Count ? Show(r[i]) : null).ToList()));
                Entry(zip, "xl/worksheets/" + file, SheetXml(rows, rtl: true));
            }
            if (sheets.Count == 0)
            {
                sheets.Add((SheetName(title, used), $"sheet{++n}.xml", false));
                Entry(zip, "xl/worksheets/" + sheets[^1].File, SheetXml(new[] { new object?[] { "این بازه هیچ ردیفی ندارد" } }, true));
            }
            //  برگهٔ پنهانِ داده
            var json = export.SnapshotJson;
            var dataRows = new List<IReadOnlyList<object?>> { new object?[] { Marker } };
            for (var i = 0; i < json.Length; i += CellMax)
                dataRows.Add(new object?[] { json.Substring(i, Math.Min(CellMax, json.Length - i)) });
            sheets.Add((DataSheet, $"sheet{++n}.xml", true));
            Entry(zip, "xl/worksheets/" + sheets[^1].File, SheetXml(dataRows, false));

            Entry(zip, "[Content_Types].xml", ContentTypes(sheets.Count));
            Entry(zip, "_rels/.rels",
                "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">"
                + "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/></Relationships>");
            var wb = new StringBuilder("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><workbook xmlns=\"" + S.NamespaceName
                + "\" xmlns:r=\"" + R.NamespaceName + "\"><bookViews><workbookView/></bookViews><sheets>");
            var rels = new StringBuilder("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">");
            for (var i = 0; i < sheets.Count; i++)
            {
                wb.Append($"<sheet name=\"{Esc(sheets[i].Name)}\" sheetId=\"{i + 1}\"{(sheets[i].Hidden ? " state=\"hidden\"" : "")} r:id=\"rId{i + 1}\"/>");
                rels.Append($"<Relationship Id=\"rId{i + 1}\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/{sheets[i].File}\"/>");
            }
            wb.Append("</sheets></workbook>");
            rels.Append("</Relationships>");
            Entry(zip, "xl/workbook.xml", wb.ToString());
            Entry(zip, "xl/_rels/workbook.xml.rels", rels.ToString());
        }
        File.Move(part, path, overwrite: true);
    }

    /// <summary>عکسِ داخلِ برگهٔ پنهان — ‎null‎ یعنی این فایل خروجیِ بخشِ همین برنامه نیست.</summary>
    public static string? ReadSnapshot(string path)
    {
        try { return ReadSnapshotCore(path); }
        catch (Exception e) when (e is InvalidDataException or System.Xml.XmlException or IOException) { return null; }
    }

    private static string? ReadSnapshotCore(string path)
    {
        using var zip = ZipFile.OpenRead(path);
        var wb = Load(zip, "xl/workbook.xml");
        var rels = Load(zip, "xl/_rels/workbook.xml.rels");
        if (wb is null || rels is null) return null;
        var sheet = wb.Descendants(S + "sheet").FirstOrDefault(s => (string?)s.Attribute("name") == DataSheet);
        var rid = (string?)sheet?.Attribute(R + "id");
        XNamespace pr = "http://schemas.openxmlformats.org/package/2006/relationships";
        var target = rels.Descendants(pr + "Relationship").FirstOrDefault(r => (string?)r.Attribute("Id") == rid)?.Attribute("Target")?.Value;
        if (target is null) return null;
        target = target.StartsWith('/') ? target.TrimStart('/') : "xl/" + target;
        var ws = Load(zip, target);
        if (ws is null) return null;
        var shared = Load(zip, "xl/sharedStrings.xml")?.Descendants(S + "si")
                         .Select(si => string.Concat(si.Descendants(S + "t").Select(t => t.Value))).ToList();
        var cells = new List<(int Row, string Text)>();
        foreach (var c in ws.Descendants(S + "c"))
        {
            var r = (string?)c.Attribute("r") ?? "";
            if (!r.StartsWith('A') || !int.TryParse(r[1..], out var row)) continue;
            var t = (string?)c.Attribute("t");
            string text = t switch
            {
                "inlineStr" => string.Concat(c.Descendants(S + "t").Select(x => x.Value)),
                "s" => int.TryParse(c.Element(S + "v")?.Value, out var si) && shared is not null && si < shared.Count ? shared[si] : "",
                _ => c.Element(S + "v")?.Value ?? "",
            };
            cells.Add((row, text));
        }
        cells.Sort((a, b) => a.Row.CompareTo(b.Row));
        if (cells.Count == 0 || cells[0].Text != Marker) return null;
        return string.Concat(cells.Skip(1).Select(c => c.Text));
    }

    // ── کمکی ──────────────────────────────────────────────────────────────

    private static XDocument? Load(ZipArchive zip, string name)
    {
        var e = zip.GetEntry(name);
        if (e is null) return null;
        using var s = e.Open();
        return XDocument.Load(s);
    }

    private static void Entry(ZipArchive zip, string name, string xml)
    {
        var e = zip.CreateEntry(name, CompressionLevel.Optimal);
        using var w = new StreamWriter(e.Open(), new UTF8Encoding(false));
        w.Write(xml);
    }

    private static string SheetName(string want, HashSet<string> used)
    {
        var bad = new[] { '\\', '/', '?', '*', '[', ']', ':' };
        var s = new string(want.Where(ch => !bad.Contains(ch)).ToArray()).Trim();
        if (s.Length == 0) s = "برگه";
        if (s.Length > 28) s = s[..28];
        var name = s;
        for (var i = 2; !used.Add(name); i++) name = s + " " + i;
        return name;
    }

    /// <summary>‎12960.0‎ ⇒ ‎12960‎ و ‎60.140‎ ⇒ ‎60.14‎ — همان عدد، بی صفرِ اضافه.</summary>
    public static string Plain(decimal d) =>
        (d / 1.000000000000000000000000000000000m).ToString(CultureInfo.InvariantCulture);

    /// <summary>نوعِ تیل و ارز به فارسی؛ بقیه همان‌طور.</summary>
    private static object? Show(object? v) => v switch
    {
        Enum e when e.GetType().Name == "FuelType" => Convert.ToInt32(e) == 2 ? "دیزل" : "پطرول",
        Enum e when e.GetType().Name == "Currency" => Convert.ToInt32(e) == 2 ? "دالر" : "افغانی",
        Enum e => e.ToString(),
        _ => v,
    };

    private static string SheetXml(IEnumerable<IReadOnlyList<object?>> rows, bool rtl)
    {
        var sb = new StringBuilder("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><worksheet xmlns=\"" + S.NamespaceName + "\">");
        sb.Append(rtl ? "<sheetViews><sheetView rightToLeft=\"1\" workbookViewId=\"0\"/></sheetViews>" : "");
        sb.Append("<sheetData>");
        var r = 0;
        foreach (var row in rows)
        {
            r++;
            sb.Append($"<row r=\"{r}\">");
            for (var c = 0; c < row.Count; c++)
            {
                var refc = Col(c) + r.ToString(CultureInfo.InvariantCulture);
                switch (row[c])
                {
                    case null: break;
                    case decimal or double or float or int or long or short or byte:
                        sb.Append($"<c r=\"{refc}\"><v>{Plain(Convert.ToDecimal(row[c], CultureInfo.InvariantCulture))}</v></c>");
                        break;
                    case bool b:
                        sb.Append($"<c r=\"{refc}\" t=\"inlineStr\"><is><t>{(b ? "✔" : "")}</t></is></c>");
                        break;
                    default:
                        sb.Append($"<c r=\"{refc}\" t=\"inlineStr\"><is><t xml:space=\"preserve\">{Esc(Convert.ToString(row[c], CultureInfo.InvariantCulture) ?? "")}</t></is></c>");
                        break;
                }
            }
            sb.Append("</row>");
        }
        sb.Append("</sheetData></worksheet>");
        return sb.ToString();
    }

    private static string Col(int i)
    {
        var s = "";
        for (i++; i > 0; i = (i - 1) / 26) s = (char)('A' + (i - 1) % 26) + s;
        return s;
    }

    /// <summary>XML: نویسه‌های نامعتبر (کنترلی) کنار می‌روند — ⚠️ فقط در برگهٔ دیدنی معنا دارد؛ JSON خودش آن‌ها را ‎\u‎ می‌نویسد.</summary>
    private static string Esc(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (var ch in s)
        {
            if (!XmlConvert.IsXmlChar(ch) && !char.IsSurrogate(ch)) continue;
            sb.Append(ch switch { '<' => "&lt;", '>' => "&gt;", '&' => "&amp;", '"' => "&quot;", _ => ch.ToString() });
        }
        return sb.ToString();
    }

    private static string ContentTypes(int sheets)
    {
        var sb = new StringBuilder("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">"
            + "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>"
            + "<Default Extension=\"xml\" ContentType=\"application/xml\"/>"
            + "<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>");
        for (var i = 1; i <= sheets; i++)
            sb.Append($"<Override PartName=\"/xl/worksheets/sheet{i}.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>");
        sb.Append("</Types>");
        return sb.ToString();
    }
}
