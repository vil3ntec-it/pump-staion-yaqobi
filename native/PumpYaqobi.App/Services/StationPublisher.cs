using System.Security.Cryptography;
using System.Text.Json;

namespace PumpYaqobi.App.Services;

/// <summary>یک پیامی که از گوشیِ کارمند/مشتری بالا آمده.</summary>
/// <param name="Id">شناسهٔ همان پیام روی سرور — برای پاک کردنش.</param>
/// <param name="Text">خودِ متن.</param>
/// <param name="From">نامِ فرستنده، اگر گفته باشد.</param>
/// <param name="Kind">نوعِ پیام — پیش‌فرض ‎note‎.</param>
/// <param name="At">زمانِ سرور (میلی‌ثانیهٔ یونیکس).</param>
public sealed record StationNote(string Id, string Text, string From, string Kind, long At);

/// <summary>
/// ══ انتشارِ زندهٔ برنامه روی سرورِ خانگی ═════════════════════════════════════
///
///     نیتیو ──set live──▶ پوشهٔ همین پمپ ──sub live──▶ اپِ کارمندان (گوشی)
///           ◀─sub inbox──                ◀─post inbox─ اندروید · آیفون
///
/// ══ چرا پوشهٔ اختصاصی و نه یک شاخهٔ مشترک ═══════════════════════════════════
///
/// تا امروز همه‌چیز روی ‎stations/&lt;کد&gt;-live‎ی دفترِ همه‌کارهٔ سرور می‌نشست، با
/// یک رمزِ مشترک. یعنی روزی که پمپِ دوم اضافه می‌شد، همان یک رمز دفترِ پمپِ
/// اول را هم باز می‌کرد. حالا سرور برای هر پمپ پوشه و رمزِ جدا می‌دهد و
/// مسیرها داخلِ همان پوشه‌اند: <see cref="LivePath"/> و <see cref="InboxPath"/>.
///
/// ⚠️ راهِ قدیمی برداشته نشده: سرورِ خانگی‌ای که هنوز به‌روز نشده فقط همان را
/// بلد است. <see cref="HomeSync"/> خودش اول درِ تازه را می‌زند و اگر نبود
/// درِ قدیمی را، و این‌جا فقط مسیر را با همان انتخاب هماهنگ می‌کنیم.
///
/// ══ «هر تغییری که در اپ می‌شود در ربات هم باشد» ═══════════════════════════
///
/// بی این‌که حتی یک خط به مسیرهای ذخیرهٔ برنامه اضافه شود: هر
/// <see cref="Interval"/> یک عکسِ تازه ساخته می‌شود و <b>فقط اگر با عکسِ قبلی
/// فرق داشته باشد</b> فرستاده می‌شود.
///
/// ⚠️ هیچ خطایی بیرون نمی‌دهد. سرورِ خانگی ممکن است خاموش باشد، اینترنت
/// نباشد، یا کاربر هنوز وارد نشده باشد — هیچ‌کدام نباید برنامه را بلرزاند.
/// </summary>
public sealed class StationPublisher : IAsyncDisposable
{
    /// <summary>هر چند وقت یک‌بار دنبالِ تغییر بگردد.</summary>
    public static readonly TimeSpan Interval = TimeSpan.FromSeconds(20);

    /// <summary>عکسِ زنده، داخلِ پوشهٔ اختصاصیِ همین پمپ.</summary>
    public const string LivePath = "live";

    /// <summary>راهِ برگشت: چیزی که گوشی‌ها بالا می‌فرستند.</summary>
    public const string InboxPath = "inbox";

    /// <summary>
    /// وقتی سرور پیدا نشد، هر بیست ثانیه دوباره نگردیم — هر تلاش یک پخشِ
    /// UDP و یک درخواستِ HTTP است و روی شبکهٔ خاموش فقط نویز می‌سازد.
    /// </summary>
    private static readonly TimeSpan EnrollRetry = TimeSpan.FromMinutes(5);

    private readonly AppHost _host;
    private readonly HomeSync _sync;
    private readonly Func<string> _stationCode;
    private CancellationTokenSource? _loop;
    private string _lastHash = "";
    private DateTime _lastEnrollTry = DateTime.MinValue;
    private bool _inboxWatched;

    /// <summary>پیام‌هایی که از سرور دیده‌ایم — تا یک پیام دو بار خبر ندهد.</summary>
    private readonly HashSet<string> _seenNotes = new(StringComparer.Ordinal);

    public StationPublisher(AppHost host, HomeSync sync, Func<string> stationCode)
    { _host = host; _sync = sync; _stationCode = stationCode; }

    /// <summary>پیامی از گوشی رسید.</summary>
    public event Action<StationNote>? NoteArrived;

    /// <summary>از کدام در وصل‌ایم — برای صفحهٔ تنظیمات.</summary>
    public HomeSyncMode Mode => _sync.Mode;

    /// <summary>‎stations/&lt;کد&gt;-live‎ — مسیرِ سرورهای به‌روزنشده.</summary>
    public static string PathOf(string? stationCode)
    {
        var code = (stationCode ?? "").Trim();
        if (code.Length == 0) code = HomeLink.DefaultStationCode;
        return "stations/" + code + "-live";
    }

    /// <summary>عکسِ همین لحظه، بی فرستادن — برای آزمون و برای دکمهٔ دستی.</summary>
    public Task<Dictionary<string, object?>> SnapshotAsync(CancellationToken ct = default)
        => StationSnapshot.BuildAsync(_host, ct);

    /// <summary>
    /// یک‌بار منتشر کن. ‎force‎ی خالی یعنی «فقط اگر چیزی عوض شده».
    /// خروجی: آیا واقعاً چیزی رفت.
    /// </summary>
    public async Task<bool> PublishOnceAsync(bool force = false, CancellationToken ct = default)
    {
        try
        {
            if (!await ReadyAsync(force, ct)) return false;

            var snap = await StationSnapshot.BuildAsync(_host, ct);

            // ⚠️ ‎seq‎ هر بار عوض می‌شود، پس در محکِ «چیزی عوض شده؟» نمی‌آید —
            // وگرنه هر بیست ثانیه یک‌بار کلِ داده بیخود فرستاده می‌شد.
            var hash = HashOf(snap);
            if (!force && hash == _lastHash) return false;

            var path = _sync.Mode == HomeSyncMode.Station ? LivePath : PathOf(_stationCode());
            if (!await _sync.SetAsync(path, snap, ct)) return false;
            _lastHash = hash;

            //  ⚠️ نشانیِ سرورِ خانگی را هم به ابر بسپار — همان چیزی که
            //  اپِ کارمند را از پرسیدنِ آدرس بی‌نیاز می‌کند. آی‌پیِ خانگی
            //  با هر بار روشن شدنِ مودم عوض می‌شود، پس باید تکرار شود؛
            //  ولی نه هر بیست ثانیه، که بی‌جهت به سرور فشار بیاورد.
            _ = PublishHomeToCloudAsync(ct);
            return true;
        }
        catch (OperationCanceledException) { throw; }
        catch { return false; }
    }

    /// <summary>آخرین باری که نشانی به ابر رفت — تا هر بیست ثانیه نرود.</summary>
    private DateTime _lastHomePush = DateTime.MinValue;

    /// <summary>
    /// سپردنِ نشانی و رمزِ فقط‌خواندنیِ سرورِ خانگی به ابر.
    ///
    /// ── چرا ────────────────────────────────────────────────────────────
    /// اپِ کارمند دیگر آدرس نمی‌پرسد: با گوگل وارد می‌شود و نشانی را از ابر
    /// می‌گیرد. ولی ابر فقط وقتی می‌داند که همین‌جا گفته باشیم.
    ///
    /// ⚠️ هیچ‌وقت جلوی انتشارِ اصلی را نمی‌گیرد: اگر اینترنت نباشد یا
    /// برنامه هنوز فعال نشده باشد، بی‌صدا رد می‌شود. دفترِ پمپ روی سرورِ
    /// خانگی کارِ خودش را می‌کند.
    /// </summary>
    private async Task PublishHomeToCloudAsync(CancellationToken ct)
    {
        try
        {
            if ((DateTime.UtcNow - _lastHomePush) < TimeSpan.FromMinutes(10)) return;

            var file = AppSettings.Load();
            if (string.IsNullOrWhiteSpace(file.CloudDeviceToken)) return;   // هنوز فعال نشده

            var url = HomeLink.Url(_host);
            if (string.IsNullOrWhiteSpace(url)) return;

            _lastHomePush = DateTime.UtcNow;
            var cloud = new CloudLink(file, () => { file.Save(); return Task.CompletedTask; });
            await cloud.PublishHomeAsync(url, HomeLink.ReadKey(_host), ct);
        }
        catch (OperationCanceledException) { throw; }
        catch { /* ابر نرسید — کارِ پمپ نباید بایستد */ }
    }

    /// <summary>
    /// «نشانی و رمز داریم و وصل‌ایم؟» — و اگر نه، خودش درستش می‌کند.
    ///
    /// این همان «اگر آدرس نداشت، برایش بساز» است: تا سرور پیدا نشود چیزی
    /// منتشر نمی‌شود، و کاربر هم هیچ‌وقت آدرسی تایپ نمی‌کند.
    /// </summary>
    private async Task<bool> ReadyAsync(bool force, CancellationToken ct)
    {
        if (!_sync.Configured || _sync.Mode == HomeSyncMode.None)
        {
            // روی سرورِ خاموش، هر بیست ثانیه نگردیم
            var due = DateTime.UtcNow - _lastEnrollTry >= EnrollRetry;
            if (force || due)
            {
                _lastEnrollTry = DateTime.UtcNow;
                await StationLink.EnsureAsync(_host, ct: ct);
            }
            else if (!_sync.Configured)
            {
                return false;
            }
        }

        if (!await _sync.ConnectAsync(ct)) return false;
        await WatchInboxAsync(ct);
        return true;
    }

    /// <summary>
    /// گوش دادن به صندوقِ ورودی — یک‌بار، و <see cref="HomeSync"/> خودش با هر
    /// قطعیِ شبکه از نو می‌گیردش.
    /// </summary>
    private async Task WatchInboxAsync(CancellationToken ct)
    {
        if (_inboxWatched || _sync.Mode != HomeSyncMode.Station) return;
        _inboxWatched = await _sync.SubscribeAsync("inbox", InboxPath, OnInbox, ct);
    }

    /// <summary>
    /// عکسِ تازهٔ صندوق رسید. سرور کلِ شاخه را می‌فرستد (نه فقط تفاوت را)،
    /// پس خودمان می‌فهمیم کدام‌ها تازه‌اند.
    /// </summary>
    private void OnInbox(JsonElement box)
    {
        if (box.ValueKind != JsonValueKind.Object) return;
        foreach (var note in Notes(box))
        {
            if (!_seenNotes.Add(note.Id)) continue;
            NoteArrived?.Invoke(note);
            var who = note.From.Length > 0 ? note.From + ": " : "";
            _host.Toast(who + note.Text, ToastKind.Info);
        }
    }

    /// <summary>خواندنِ شاخهٔ صندوق. هر ردیفِ ناشناس بی‌صدا رد می‌شود.</summary>
    public static IReadOnlyList<StationNote> Notes(JsonElement box)
    {
        var list = new List<StationNote>();
        if (box.ValueKind != JsonValueKind.Object) return list;

        foreach (var row in box.EnumerateObject())
        {
            if (row.Value.ValueKind != JsonValueKind.Object) continue;
            string S(string k) =>
                row.Value.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
            var text = S("text");
            if (text.Length == 0) continue;
            var at = row.Value.TryGetProperty("at", out var a) && a.TryGetInt64(out var n) ? n : 0;
            var kind = S("kind");
            list.Add(new StationNote(row.Name, text, S("from"), kind.Length > 0 ? kind : "note", at));
        }
        list.Sort((x, y) => x.At.CompareTo(y.At));
        return list;
    }

    /// <summary>پیامِ خوانده‌شده را از سرور بردار.</summary>
    public async Task<bool> ClearNoteAsync(string id, CancellationToken ct = default)
    {
        if (_sync.Mode != HomeSyncMode.Station || string.IsNullOrWhiteSpace(id)) return false;
        return await _sync.RemoveAsync(InboxPath + "/" + id, ct);
    }

    /// <summary>حلقهٔ پس‌زمینه. صدا زدنش دو بار، یکی بیشتر نمی‌سازد.</summary>
    public void Start()
    {
        if (_loop is not null) return;
        var cts = new CancellationTokenSource();
        _loop = cts;
        _ = Task.Run(() => LoopAsync(cts.Token), cts.Token);
    }

    private async Task LoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try { await PublishOnceAsync(false, ct); }
            catch (OperationCanceledException) { return; }
            catch { /* سرورِ خاموش خطا نیست */ }

            try { await Task.Delay(Interval, ct); }
            catch { return; }
        }
    }

    /// <summary>
    /// اثرِ انگشتِ عکس، بی ‎seq‎ و بی زمان — تا «عوض شد؟» معنی داشته باشد.
    /// </summary>
    public static string HashOf(Dictionary<string, object?> snap)
    {
        var copy = new Dictionary<string, object?>(snap);
        copy.Remove("seq");
        copy.Remove("at");
        copy.Remove("atUtc");
        var bytes = JsonSerializer.SerializeToUtf8Bytes(copy);
        return Convert.ToHexString(SHA256.HashData(bytes));
    }

    public async ValueTask DisposeAsync()
    {
        var cts = _loop;
        _loop = null;
        if (cts is not null)
        {
            await cts.CancelAsync();
            cts.Dispose();
        }
        await _sync.DisposeAsync();
    }
}
