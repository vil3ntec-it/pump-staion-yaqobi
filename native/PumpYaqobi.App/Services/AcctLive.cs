using System.Security.Cryptography;
using System.Text.Json;
using PumpYaqobi.Domain.Entities;

namespace PumpYaqobi.App.Services;

/// <summary>
/// ══ کیو‌آرِ زنده — «طرف داره با کیو‌آر حسابشو چک می‌کنه و من دارم زنده تغییر می‌دم» ═══
///
/// خواستهٔ صاحب ریپو (۱۴۰۵/۰۶/۲۵): «من دارم زنده تغییرات میارم توی حسابِ طرف،
/// طرف هم داره با کیو‌آر حسابشو چک می‌کنه و می‌خوام درجا برای اون هم بره…
/// که یارو بتونه هر دقیقه که تغییری براش اومد، چک کنه و بفهمه.»
///
/// ── راه ──────────────────────────────────────────────────────────────────
///
///     برنامهٔ نیتیو ──(هر تغییر)──▶ سرورِ خانگی  acct/&lt;شناسه&gt;         (شبکهٔ پمپ)
///                   ──(هر تغییر)──▶ ابر         files/acct-&lt;شناسه&gt;   (اینترنت)
///     گوشیِ مشتری ◀──(هر ۶۰ ثانیه)── GET /api/pump/public/&lt;پمپ&gt;/acct/&lt;شناسه&gt;?k=…
///
/// کیو‌آر همان کیو‌آرِ قبلی است: دادهٔ حساب **داخلِ خودِ کد** می‌ماند (بی
/// اینترنت هم باز می‌شود) و فقط چهار پارامترِ کوتاه به تکهٔ ‎#‎ اضافه می‌شود:
/// ‎s‎ (کدِ پمپ)، ‎a‎ (شناسهٔ حساب)، ‎k‎ (رمزِ همین حساب) و ‎t‎ (زمانِ ساختِ کد).
/// صفحهٔ ‎view/‎ اول دادهٔ داخلِ کد را نشان می‌دهد و بعد از ابر می‌پرسد؛ اگر
/// تازه‌تر بود، همان لحظه عوض می‌کند.
///
/// ── رمزِ حساب ───────────────────────────────────────────────────────────
///
/// ⚠️ هیچ رمزِ دیگری در کیو‌آر نیست — نه رمزِ برنامه، نه رمزِ فقط‌خواندنیِ
/// سرور. ‎k‎ فقط **همین یک حساب** را باز می‌کند و ربطی به بقیهٔ حساب‌ها ندارد.
/// اولین بار که کیو‌آرِ حسابی ساخته می‌شود ساخته و در همان حساب ذخیره می‌شود
/// (‎QrKey‎) و دیگر عوض نمی‌شود: کاغذی که دستِ مشتری است باید تا ابد کار کند.
/// حسابِ بی‌رمز منتشر هم نمی‌شود — پمپی که کیو‌آر نداده هیچ هزینه‌ای نمی‌دهد.
///
/// ── چه چیزی می‌رود ──────────────────────────────────────────────────────
///
/// همان ‎AcctSnapshot‎ی که داخلِ کیو‌آر می‌رود، بی بریدنِ ردیف‌ها (سرور سقفِ
/// کیو‌آر را ندارد) داخلِ یک پاکت: ‎{v, k, at, d}‎. سرور ‎k‎ را با رمزِ داخلِ
/// درخواست می‌سنجد و فقط ‎{at, d}‎ را پس می‌دهد.
/// </summary>
public static class AcctLive
{
    /// <summary>شاخهٔ سرورِ خانگی: ‎acct/&lt;شناسه&gt;‎.</summary>
    public const string HomeBranch = "acct";

    /// <summary>نامِ فایل روی ابر: ‎acct-&lt;شناسه&gt;‎.</summary>
    public const string CloudPrefix = "acct-";

    /// <summary>نشانیِ ابر که صفحهٔ مشتری از آن می‌پرسد — همان نشانیِ قفل‌شده.</summary>
    public static string PublicUrl(string station, string id, string key) =>
        CloudConfig.BaseUrl + "/api/pump/public/" + Uri.EscapeDataString(station)
        + "/acct/" + Uri.EscapeDataString(id) + "?k=" + Uri.EscapeDataString(key);

    /// <summary>رمزِ تازه — ۸۰ بیت، شانزده‌شانزدهی، همیشه ۲۰ حرف.</summary>
    public static string NewKey() =>
        Convert.ToHexString(RandomNumberGenerator.GetBytes(10)).ToLowerInvariant();

    /// <summary>شناسهٔ عمومیِ حسابِ قرض‌دار: ‎d&lt;Id&gt;‎.</summary>
    public static string IdOf(DebtAccount a) => "d" + a.Id;

    /// <summary>شناسهٔ عمومیِ شرکت: ‎c&lt;Id&gt;‎.</summary>
    public static string IdOf(TilCompany c) => "c" + c.Id;

    /// <summary>
    /// تکهٔ زنده‌ای که پیش از ‎d=‎ در هشِ نشانی می‌نشیند. خالی یعنی زنده نیست.
    /// </summary>
    public static string Fragment(string station, string id, string key, long atMs)
    {
        if (string.IsNullOrWhiteSpace(station) || string.IsNullOrWhiteSpace(id)
            || string.IsNullOrWhiteSpace(key)) return "";
        return "s=" + Uri.EscapeDataString(CloudCode(station))
             + "&a=" + Uri.EscapeDataString(id)
             + "&k=" + Uri.EscapeDataString(key)
             + "&t=" + atMs;
    }

    /// <summary>
    /// کدِ پمپ همان‌طور که ابر نگهش می‌دارد (‎cleanCode‎ در ‎shop‎): کوچک،
    /// فقط ‎a-z0-9-‎. صفحهٔ مشتری با همین کد از ابر می‌پرسد، پس باید مو‌به‌مو
    /// همان باشد که هنگامِ فعال‌سازی ثبت شده — نه آن‌چه کاربر تایپ کرده.
    /// </summary>
    public static string CloudCode(string? station)
    {
        var s = (station ?? "").Trim().ToLowerInvariant();
        var sb = new System.Text.StringBuilder(s.Length);
        foreach (var ch in s) sb.Append(ch is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '-' ? ch : '-');
        var t = System.Text.RegularExpressions.Regex.Replace(sb.ToString(), "-+", "-").Trim('-');
        return t;
    }

    /// <summary>همین لحظه، به میلی‌ثانیهٔ یونیکس — همان چیزی که ‎t‎ و ‎at‎ می‌گیرند.</summary>
    public static long NowMs() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    /// <summary>پاکتی که روی سرور می‌نشیند. ‎k‎ همان رمز است و سرور با آن می‌سنجد.</summary>
    public static Dictionary<string, object?> Envelope(string key, AcctSnapshot snap, long? atMs = null) =>
        new()
        {
            ["v"] = 1,
            ["k"] = key,
            ["at"] = atMs ?? NowMs(),
            ["d"] = snap,
        };

    /// <summary>
    /// اثرِ انگشتِ عکس، بی تاریخِ ساخت — تا «عوض شد؟» یعنی حساب عوض شد، نه
    /// این‌که روز عوض شد.
    /// </summary>
    public static string HashOf(AcctSnapshot snap)
    {
        var date = snap.Date;
        snap.Date = "";
        try
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(snap, AcctView.JsonOptions);
            return Convert.ToHexString(SHA256.HashData(bytes));
        }
        finally { snap.Date = date; }
    }

    /// <summary>
    /// رمزِ حسابِ قرض‌دار را (اگر نداشت) بساز و ذخیره کن؛ تکهٔ زنده را بده.
    /// اگر ذخیره نشد (مثلاً نقشِ بی‌اجازهٔ ویرایش) خالی برمی‌گردد و کیو‌آر
    /// همان کیو‌آرِ ایستا می‌شود — هیچ‌وقت خطا بیرون نمی‌دهد.
    /// </summary>
    public static async Task<string> EnsureAsync(AppHost host, DebtAccount acct, CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(acct.QrKey))
            {
                acct.QrKey = NewKey();
                await host.Debtors.UpdateAccountAsync(acct, ct);
            }
            return Fragment(HomeLink.StationCode(host), IdOf(acct), acct.QrKey!, NowMs());
        }
        catch { acct.QrKey = null; return ""; }
    }

    /// <summary>همان برای شرکتِ تیل.</summary>
    public static async Task<string> EnsureAsync(AppHost host, TilCompany company, CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(company.QrKey))
            {
                company.QrKey = NewKey();
                await host.Companies.UpdateAsync(company, ct);
            }
            return Fragment(HomeLink.StationCode(host), IdOf(company), company.QrKey!, NowMs());
        }
        catch { company.QrKey = null; return ""; }
    }

    /// <summary>
    /// ══ «زنده» فقط وقتی واقعاً زنده است ═══════════════════════════════════
    ///
    /// گزارشِ صاحب ریپو (۱۴۰۵/۰۶/۲۶): «با اسکن کار نمی‌کند، سایت به‌روز نمی‌شود،
    /// اطلاعاتِ جدیدی که می‌رسانم نمی‌رود؛ چتِ پشتیبانی هم کار نمی‌کند.»
    ///
    /// ریشه: کیو‌آر تا وقتی حساب ‎QrKey‎ داشته باشد پارامترهای زنده را می‌گیرد،
    /// ولی **فرستادن** به ابر فقط با برنامهٔ فعال‌شده (کدِ شش‌رقمی) انجام
    /// می‌شود (‎StationPublisher.CloudActivated‎). پس روی پمپی که هنوز فعال
    /// نشده، کیو‌آر ساخته می‌شد و نوشته هم می‌گفت «تا یک دقیقه بعد می‌آید» —
    /// در حالی که هیچ‌وقت نمی‌آمد و چتِ پشتیبانی هم نبود.
    ///
    /// حالا همان جمله حقیقت را می‌گوید و راهِ درست کردنش را هم.
    /// </summary>
    public static string Hint(AppHost host, string live)
    {
        if (live.Length == 0) return " و به سرور وصل نمی‌شود.";
        return Activated(host)
            ? "، و هر تغییری که این‌جا بدهید تا یک دقیقه بعد روی گوشی‌اش هم می‌آید — "
              + "چتِ پشتیبانی هم روی همین صفحه باز است."
            : "، ولی تا برنامه با کدِ شش‌رقمیِ اشتراک فعال نشود، تغییرهای تازه و "
              + "چتِ پشتیبانی روی گوشیِ مشتری نمی‌آید (پروفایل ← فعال کردن).";
    }

    /// <summary>برنامه با کدِ شش‌رقمی فعال شده؟ — شرطِ رفتنِ عکسِ حساب به ابر.</summary>
    public static bool Activated(AppHost _) =>
        !string.IsNullOrWhiteSpace(AppSettings.Load().CloudDeviceToken);

    /// <summary>یک حسابِ آمادهٔ انتشار.</summary>
    public sealed record Item(string Id, string Key, AcctSnapshot Snap);

    /// <summary>
    /// همهٔ حساب‌های کیو‌آردار با عکسِ همین لحظه‌شان. هر عددی از خودِ
    /// سرویس‌های برنامه می‌آید (‎AcctSnapshots‎) — این‌جا هیچ حسابی نیست.
    /// </summary>
    public static async Task<List<Item>> CollectAsync(AppHost host, CancellationToken ct = default)
    {
        var list = new List<Item>();

        foreach (var (person, acct) in await host.Debtors.LoadQrAccountsAsync(ct))
        {
            ct.ThrowIfCancellationRequested();
            var title = acct.IsMain ? null : acct.Name;
            var snap = AcctSnapshots.ForDebtAccount(person.Name, title, acct, host.Debt);
            list.Add(new Item(IdOf(acct), acct.QrKey!, snap));
        }

        foreach (var lite in await host.Companies.ListAsync(ct))
        {
            if (string.IsNullOrWhiteSpace(lite.QrKey)) continue;
            ct.ThrowIfCancellationRequested();
            list.Add(new Item(IdOf(lite), lite.QrKey!, AcctSnapshots.ForCompany(lite, host.Company)));
        }

        return list;
    }
}

/// <summary>
/// ══ انتشارِ حساب‌های کیو‌آردار — به سرورِ خانگی و به ابر ═════════════════════
///
/// از داخلِ حلقهٔ <see cref="StationPublisher"/> صدا زده می‌شود، و فقط وقتی
/// که عکسِ کلِ پمپ عوض شده باشد. هر حساب اثرِ انگشتِ خودش را دارد و فقط
/// حسابی می‌رود که واقعاً عوض شده — پس ویرایشِ حسابِ «الف» چیزی برای «ب»
/// نمی‌فرستد.
///
/// ⚠️ دو مقصد، دو حافظه: اگر خانگی رفت و ابر نرفت (اینترنت قطع)، دفعهٔ بعد
/// فقط ابر دوباره امتحان می‌شود، نه هر دو. و هیچ خطایی بیرون نمی‌آید.
/// </summary>
public sealed class AcctLivePublisher
{
    private readonly Dictionary<string, string> _home = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _cloud = new(StringComparer.Ordinal);

    /// <summary>چند حساب در این دور واقعاً به جایی رفت — برای آزمون و گزارش.</summary>
    public int LastSent { get; private set; }

    /// <summary>
    /// چند مقصد در دورِ پیش طلبکار ماند (فرستادن لازم بود و نرفت). تا صفر
    /// نشده، دورِ بعد دوباره تلاش می‌شود حتی اگر هیچ چیزی عوض نشده باشد.
    /// </summary>
    public int Pending { get; private set; }

    /// <summary>
    /// یک دور. ‎homeSet‎ی ‎null‎ یعنی «سرورِ خانگی راهِ حساب‌ها را ندارد» (درِ
    /// قدیمی)، ‎cloudPut‎ی ‎null‎ یعنی «برنامه هنوز فعال نشده».
    /// </summary>
    public async Task<int> PublishAsync(
        IReadOnlyList<AcctLive.Item> items,
        Func<string, object, CancellationToken, Task<bool>>? homeSet,
        Func<string, object, CancellationToken, Task<bool>>? cloudPut,
        CancellationToken ct = default)
    {
        var alive = new HashSet<string>(items.Select(i => i.Id), StringComparer.Ordinal);
        // حسابی که کیو‌آرش پاک شد (یا خودِ حساب) از حافظه هم می‌رود تا اگر
        // روزی برگشت دوباره فرستاده شود.
        foreach (var k in _home.Keys.Where(k => !alive.Contains(k)).ToList()) _home.Remove(k);
        foreach (var k in _cloud.Keys.Where(k => !alive.Contains(k)).ToList()) _cloud.Remove(k);

        var sent = 0;
        var pending = 0;
        foreach (var it in items)
        {
            ct.ThrowIfCancellationRequested();
            var hash = AcctLive.HashOf(it.Snap);
            var needHome = homeSet is not null && (!_home.TryGetValue(it.Id, out var h) || h != hash);
            var needCloud = cloudPut is not null && (!_cloud.TryGetValue(it.Id, out var c) || c != hash);
            if (!needHome && !needCloud) continue;

            var env = AcctLive.Envelope(it.Key, it.Snap);
            var went = false;
            if (needHome)
            {
                var ok = false;
                try { ok = await homeSet!(AcctLive.HomeBranch + "/" + it.Id, env, ct); }
                catch (OperationCanceledException) { throw; }
                catch { /* سرورِ خانگی خاموش — دورِ بعد */ }
                if (ok) { _home[it.Id] = hash; went = true; } else pending++;
            }
            if (needCloud)
            {
                var ok = false;
                try { ok = await cloudPut!(AcctLive.CloudPrefix + it.Id, env, ct); }
                catch (OperationCanceledException) { throw; }
                catch { /* ابر نرسید — دورِ بعد */ }
                if (ok) { _cloud[it.Id] = hash; went = true; } else pending++;
            }
            if (went) sent++;
        }
        LastSent = sent;
        Pending = pending;
        return sent;
    }
}
