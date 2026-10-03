using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace PumpYaqobi.Services.Data;

/// <summary>
/// ══ «فایلِ حساب» — یک حساب، یک شرکت یا یک بخش در یک فایلِ خودِ برنامه (‎.pumphesab‎) ══
///
/// خواستهٔ صاحب ریپو (۱۴۰۵/۰۷/۱۹): «مثلِ اکسل، نه خودِ اکسل — و اسمِ اکسل را
/// بردار.» یعنی همان کاری که اکسل می‌کند (یک تکه را جدا بیرون ببری و دوباره
/// بیاوری)، با فایلِ <b>خودِ برنامه</b>. ⛔ پس این فایل کارپوشهٔ اکسل نیست و
/// واژهٔ «اکسل» هیچ‌جای این راه نیست.
///
/// ساختار: یک زیپ با دو ورودی —
///   • ‎part.json‎      عکسِ قابلِ بازگرداندن (‎SyncStore.ExportPortable‎)
///   • ‎manifest.json‎  نشانِ برنامه، عنوان، شمارِ ردیف‌ها و ‎SHA-256‎ِ ‎part.json‎
/// ⛔ خواندن فقط وقتی چیزی می‌دهد که نشان و هش هر دو بخوانند — فایلِ دست‌خورده،
/// نیمه‌کپی یا غریبه هیچ‌وقت به «آوردن» نمی‌رسد.
/// </summary>
public static class PortableFile
{
    public const string Extension = ".pumphesab";
    private const string Marker = "PUMPYAQOBI-PART-2";

    /// <summary>
    /// فایل را می‌نویسد — ⛔ اتمی: اول ‎.part‎ و بعد جابه‌جایی، پس فایلِ قبلیِ همان نام
    /// (بکاپِ «به‌روزشدنی») تا لحظهٔ آخر سالم می‌ماند.
    /// </summary>
    public static void Write(string path, string title, PortableExport export)
    {
        var data = Encoding.UTF8.GetBytes(export.SnapshotJson);
        var manifest = new JsonObject
        {
            ["marker"] = Marker,
            ["title"] = title,
            ["rows"] = export.RowCount,
            ["tables"] = new JsonArray(export.Tables.Select(t => (JsonNode?)new JsonObject
                { ["entity"] = t.Entity, ["rows"] = t.Rows.Count }).ToArray()),
            ["sha256"] = Convert.ToHexString(SHA256.HashData(data)),
        };
        var part = path + ".part";
        if (File.Exists(part)) File.Delete(part);
        using (var fs = new FileStream(part, FileMode.CreateNew))
        using (var zip = new ZipArchive(fs, ZipArchiveMode.Create))
        {
            using (var s = zip.CreateEntry("part.json", CompressionLevel.Optimal).Open()) s.Write(data);
            using (var s = zip.CreateEntry("manifest.json", CompressionLevel.Optimal).Open())
                s.Write(Encoding.UTF8.GetBytes(manifest.ToJsonString(new JsonSerializerOptions
                    { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping })));
        }
        File.Move(part, path, overwrite: true);
    }

    /// <summary>عکسِ داخلِ فایل — ‎null‎ یعنی این فایل «فایلِ حساب»ِ سالمِ همین برنامه نیست.</summary>
    public static string? ReadSnapshot(string path)
    {
        try
        {
            using var zip = ZipFile.OpenRead(path);
            var m = zip.GetEntry("manifest.json");
            var p = zip.GetEntry("part.json");
            if (m is null || p is null) return null;
            JsonNode? manifest;
            using (var s = m.Open()) manifest = JsonNode.Parse(s);
            if ((string?)manifest?["marker"] != Marker) return null;
            byte[] data;
            using (var s = p.Open()) using (var ms = new MemoryStream()) { s.CopyTo(ms); data = ms.ToArray(); }
            if (!string.Equals((string?)manifest?["sha256"], Convert.ToHexString(SHA256.HashData(data)),
                               StringComparison.OrdinalIgnoreCase)) return null;
            return Encoding.UTF8.GetString(data);
        }
        catch (Exception e) when (e is InvalidDataException or JsonException or IOException or InvalidOperationException) { return null; }
    }
}
