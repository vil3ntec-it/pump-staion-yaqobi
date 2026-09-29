using System.Globalization;
using System.Text.Json;

namespace PumpYaqobi.App.Services;

/// <summary>
/// ══ «تنظیماتِ زنده» — مقداری که از سرور عوض می‌شود، بی آپدیتِ برنامه ════════
///
/// خواستهٔ صاحب ریپو (۱۴۰۵/۰۷/۱۷): «برنامه رو جوری کن که بعدن اگه قابلیتی
/// خاستم بتونم راحت روش اجرا کنم… لایف اپدیت باشه… روی سرور فشاری نیاد…
/// هیچ منطقی رو دست نزن و خراب نکن.»
///
/// <list type="bullet">
/// <item>⛔ <b>امروز هیچ بخشی از برنامه از این نمی‌خواند</b> — فقط در است. قابلیتِ
/// تازه‌ای که بعداً بخواهد از سرور عوض شود، یک کلید این‌جا می‌خواند
/// (<see cref="GetString"/> · <see cref="GetDecimal"/> · <see cref="GetBool"/>) و به
/// <see cref="Changed"/> گوش می‌دهد. نبودِ کلید همیشه یعنی «همان پیش‌فرضِ خودِ برنامه».</item>
/// <item>⛔ <b>فقط مقدار، هرگز کد</b>: هیچ چیزی از این برگه اجرا نمی‌شود، و کلیدی که
/// برنامه نمی‌شناسد فقط نگه داشته می‌شود.</item>
/// <item>⛔ <b>درخواستِ تازه‌ای نیست</b>: نسخه روی پاسخِ همان پرسشِ دقیقه‌ایِ
/// <c>/api/pump/device/rate</c> می‌آید (<see cref="NoteServerVersion"/>)؛ برگه فقط وقتی
/// خوانده می‌شود که نسخه عوض شده باشد (<see cref="NeedsFetch"/>).</item>
/// <item>فایلِ کوچکِ <c>live-config.json</c> کنارِ تنظیمات، تا بی‌اینترنت هم آخرین
/// مقدارها بمانند. خراب یا نبودنش فقط یعنی «هیچ مقداری» — همان رفتارِ امروز.</item>
/// </list>
/// سمتِ سرور: <c>lib/live-config.js</c> در ریپوی <c>shop</c>.
/// </summary>
public static class LiveConfig
{
    private static readonly object Gate = new();
    private static Dictionary<string, JsonElement>? _values;
    private static string _version = "";
    private static string _serverVersion = "";

    /// <summary>
    /// مقدارها عوض شدند. ⚠️ روی رشتهٔ پس‌زمینه صدا زده می‌شود — شنوندهٔ رابط باید
    /// خودش به رشتهٔ رابط برگردد.
    /// </summary>
    public static event Action? Changed;

    /// <summary>نسخهٔ برگه‌ای که روی این کامپیوتر است («همه.پمپ»). خالی ⇒ هنوز هیچ.</summary>
    public static string Version { get { lock (Gate) { EnsureLoaded(); return _version; } } }

    /// <summary>سرور نسخهٔ تازه‌تری گفته که هنوز این‌جا نیامده؟</summary>
    public static bool NeedsFetch
    {
        get
        {
            lock (Gate)
            {
                EnsureLoaded();
                return _serverVersion.Length > 0 && !string.Equals(_serverVersion, _version, StringComparison.Ordinal);
            }
        }
    }

    /// <summary>کلید هست؟ (مقدارِ خام، برای قابلیتی که شکلِ خودش را دارد.)</summary>
    public static bool TryGet(string key, out JsonElement value)
    {
        lock (Gate)
        {
            EnsureLoaded();
            if (_values!.TryGetValue(key, out var v)) { value = v; return true; }
        }
        value = default;
        return false;
    }

    public static string? GetString(string key, string? fallback = null) =>
        TryGet(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : fallback;

    public static decimal? GetDecimal(string key, decimal? fallback = null)
    {
        if (!TryGet(key, out var v)) return fallback;
        if (v.ValueKind == JsonValueKind.Number && v.TryGetDecimal(out var d)) return d;
        if (v.ValueKind == JsonValueKind.String
            && decimal.TryParse(v.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var s)) return s;
        return fallback;
    }

    public static bool GetBool(string key, bool fallback) =>
        TryGet(key, out var v) && v.ValueKind is JsonValueKind.True or JsonValueKind.False ? v.GetBoolean() : fallback;

    /// <summary>
    /// نسخه‌ای که سرور روی پاسخِ <c>/rate</c> گفت (<c>liveConfig</c>). پاسخِ سرورِ حسابِ
    /// کهنه این کلید را ندارد ⇒ هیچ کاری نمی‌شود.
    /// </summary>
    public static void NoteServerVersion(JsonElement rateResponse)
    {
        if (rateResponse.ValueKind != JsonValueKind.Object) return;
        if (!rateResponse.TryGetProperty("liveConfig", out var v) || v.ValueKind != JsonValueKind.String) return;
        var s = v.GetString() ?? "";
        lock (Gate) _serverVersion = s;
    }

    /// <summary>
    /// برگهٔ سرور (‎{version, values}‎) را می‌نشاند و نگه می‌دارد.
    /// <returns>مقدارها واقعاً عوض شدند؟</returns>
    /// </summary>
    public static bool Apply(JsonElement sheet)
    {
        if (sheet.ValueKind != JsonValueKind.Object) return false;
        if (!sheet.TryGetProperty("version", out var ver) || ver.ValueKind != JsonValueKind.String) return false;
        var next = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        if (sheet.TryGetProperty("values", out var vals) && vals.ValueKind == JsonValueKind.Object)
            foreach (var p in vals.EnumerateObject()) next[p.Name] = p.Value.Clone();

        bool changed;
        lock (Gate)
        {
            EnsureLoaded();
            changed = !SameValues(_values!, next);
            _values = next;
            _version = ver.GetString() ?? "";
            if (_serverVersion.Length == 0) _serverVersion = _version;
            Write();
        }
        if (changed)
        {
            try { Changed?.Invoke(); }
            catch { /* شنونده‌ای که خطا داد نباید بقیه را بخواباند */ }
        }
        return changed;
    }

    /// <summary>فقط برای سنجه‌ها.</summary>
    public static void ResetForTests()
    {
        lock (Gate)
        {
            _values = null;
            _version = "";
            _serverVersion = "";
        }
    }

    private static bool SameValues(Dictionary<string, JsonElement> a, Dictionary<string, JsonElement> b)
    {
        if (a.Count != b.Count) return false;
        foreach (var (k, v) in a)
            if (!b.TryGetValue(k, out var w) || v.GetRawText() != w.GetRawText()) return false;
        return true;
    }

    private static string FilePath() => Path.Combine(AppSettings.Dir, "live-config.json");

    private static void EnsureLoaded()
    {
        if (_values is not null) return;
        _values = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        try
        {
            var p = FilePath();
            if (!File.Exists(p)) return;
            using var doc = JsonDocument.Parse(File.ReadAllText(p));
            var root = doc.RootElement;
            if (root.TryGetProperty("version", out var ver) && ver.ValueKind == JsonValueKind.String)
                _version = ver.GetString() ?? "";
            if (root.TryGetProperty("values", out var vals) && vals.ValueKind == JsonValueKind.Object)
                foreach (var prop in vals.EnumerateObject()) _values[prop.Name] = prop.Value.Clone();
        }
        catch
        {
            //  فایلِ خراب ⇒ هیچ مقداری، و نسخهٔ خالی تا دورِ بعد دوباره بخواند
            _values.Clear();
            _version = "";
        }
    }

    private static void Write()
    {
        try
        {
            var p = FilePath();
            Directory.CreateDirectory(Path.GetDirectoryName(p)!);
            var tmp = p + ".tmp";
            using (var fs = File.Create(tmp))
            using (var w = new Utf8JsonWriter(fs, new JsonWriterOptions { Indented = true }))
            {
                w.WriteStartObject();
                w.WriteString("version", _version);
                w.WritePropertyName("values");
                w.WriteStartObject();
                foreach (var (k, v) in _values!)
                {
                    w.WritePropertyName(k);
                    v.WriteTo(w);
                }
                w.WriteEndObject();
                w.WriteEndObject();
            }
            File.Move(tmp, p, overwrite: true);
        }
        catch { /* نوشتن نشد ⇒ دورِ بعد دوباره؛ مقدارها در حافظه هستند */ }
    }
}
