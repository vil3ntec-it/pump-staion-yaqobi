using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;

namespace PumpYaqobi.App.Services;

/// <summary>نتیجهٔ یک کارِ ابری — هرگز استثنا بیرون نمی‌دهد.</summary>
/// <param name="Ok">شد یا نشد.</param>
/// <param name="Why">اگر نشد، چرا — به فارسیِ خودِ سرور، برای نشان دادن به کاربر.</param>
/// <param name="Code">کدِ ماشینیِ خطا، اگر سرور داده باشد.</param>
public sealed record CloudResult(bool Ok, string Why = "", string Code = "")
{
    public static readonly CloudResult Done = new(true);
    public static CloudResult No(string why, string code = "") => new(false, why, code);
}

/// <summary>وضعیتِ اشتراک، آن‌طور که برنامه نشان می‌دهد.</summary>
/// <param name="Active">اشتراک یا دورهٔ آزمایشی باز است.</param>
/// <param name="Source">subscription | trial | free | none</param>
/// <param name="PlanTitle">نامِ پلن.</param>
/// <param name="DaysLeft">روزهای مانده.</param>
/// <param name="EndsAt">پایان (میلی‌ثانیهٔ یونیکس).</param>
/// <param name="Features">بخش‌هایی که باز است.</param>
public sealed record PumpSubscription(
    bool Active, string Source, string PlanTitle, int DaysLeft, long EndsAt,
    IReadOnlyList<string> Features)
{
    public static readonly PumpSubscription None =
        new(false, "none", "", 0, 0, Array.Empty<string>());
}

/// <summary>
/// ══ ابر — «اشتراک از سرور می‌آید» ═══════════════════════════════════════════
///
/// خواستهٔ صریحِ صاحب ریپو: «اشتراک هم از سرور بره براش.»
///
/// ── راه ────────────────────────────────────────────────────────────────────
/// <code>
///   صاحبِ پمپ شش رقم را در تنظیمات می‌زند
///        │
///        ▼  POST /api/pump/device/activate
///   توکنِ دستگاه + مجوزِ امضاشده + کلیدِ عمومیِ سرور
///        │
///        ▼  هر بار بالا آمدن: /api/pump/device/me و /license
///   اشتراک تازه می‌شود، بی این‌که کسی چیزی بپرسد
/// </code>
///
/// نه حسابی، نه مرورگری، نه نشانی‌ای. نشانی در
/// <see cref="CloudConfig.BaseUrl"/> قفل است و از تنظیمات خوانده نمی‌شود.
///
/// ── سه قاعده ───────────────────────────────────────────────────────────────
///
///  • <b>هیچ استثنایی بیرون نمی‌دهد.</b> نبودِ اینترنت، سرورِ خاموش و فایروال
///    همه یعنی «نشد»، نه «خطا». برنامهٔ پمپ باید آفلاین هم باز شود.
///
///  • <b>کلیدِ عمومی فقط یک بار قفل می‌شود.</b> ⚠️ اگر سرور روزی کلیدِ
///    دیگری بدهد، <b>نمی‌پذیریم</b> — چون همان یعنی یا سرور عوض شده یا
///    کسی وسط نشسته. بی این قید، کلِ قفل با یک سرورِ ساختگی دور می‌خورد.
///
///  • <b>مجوز ذخیره می‌شود.</b> تصمیمِ آفلاین از روی همان گرفته می‌شود، و
///    <see cref="LicenseGuard"/> امضایش را می‌سنجد — نه یک پرچمِ ساده که
///    هر کسی در فایلِ تنظیمات عوضش کند.
/// </summary>
public sealed class CloudLink
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(20) };

    private readonly AppSettings _settings;
    private readonly Func<Task> _save;

    public CloudLink(AppSettings settings, Func<Task> save)
    {
        _settings = settings;
        _save = save;
    }

    /// <summary>آیا این برنامه از قبل فعال شده است.</summary>
    public bool Activated => !string.IsNullOrWhiteSpace(_settings.CloudDeviceToken);

    /// <summary>شناسهٔ این کامپیوتر.</summary>
    public string DeviceUid => CloudConfig.DeviceUid(_settings);

    /// <summary>آخرین وضعیتِ اشتراک که از سرور گرفته شده.</summary>
    public PumpSubscription Subscription { get; private set; } = PumpSubscription.None;

    /// <summary>سنجشِ مجوزِ ذخیره‌شده — همان چیزی که آفلاین هم کار می‌کند.</summary>
    public LicenseCheck Verify(long? nowMs = null) => LicenseGuard.Check(
        _settings.CloudLicense,
        _settings.CloudPublicKey,
        DeviceUid,
        _settings.CloudStationId,
        nowMs ?? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

    // ── فعال‌سازی ──────────────────────────────────────────────────────

    /// <summary>
    /// کدِ شش‌رقمی ⇒ پمپ، اشتراک، توکنِ دستگاه و مجوز.
    ///
    /// کدِ پمپ و نامش از تنظیماتِ خودِ برنامه می‌روند، پس کاربر جز همان شش
    /// رقم چیزی تایپ نمی‌کند.
    /// </summary>
    public async Task<CloudResult> ActivateAsync(string code, CancellationToken ct = default)
    {
        var clean = new string((code ?? "").Where(char.IsDigit).ToArray());
        if (clean.Length != 6) return CloudResult.No("کد باید شش رقم باشد");

        var body = new
        {
            code = clean,
            device = new
            {
                uid = DeviceUid,
                name = Environment.MachineName,
                platform = "windows",
            },
            station = new
            {
                code = _settings.StationCode ?? "",
                name = "",
            },
        };

        var (ok, json, why, errCode) = await PostAsync("/api/pump/device/activate", body, null, ct);
        if (!ok) return CloudResult.No(why, errCode);

        //  ⚠️ کلیدِ عمومی فقط همین یک بار قفل می‌شود. اگر از قبل کلیدی
        //  داریم و سرور کلیدِ دیگری داد، نمی‌پذیریم.
        var serverKey = Str(json, "publicKey");
        if (string.IsNullOrWhiteSpace(_settings.CloudPublicKey))
        {
            if (string.IsNullOrWhiteSpace(serverKey))
                return CloudResult.No("سرور کلیدِ عمومی نداد؛ فعال‌سازی نیمه‌کاره ماند");
            _settings.CloudPublicKey = serverKey;
        }
        else if (!string.IsNullOrWhiteSpace(serverKey) && serverKey != _settings.CloudPublicKey)
        {
            return CloudResult.No(
                "کلیدِ سرور با آن‌چه این برنامه قفل کرده فرق دارد. اگر سرور را واقعاً "
                + "عوض کرده‌اید، با پشتیبانی تماس بگیرید.", "key_mismatch");
        }

        _settings.CloudDeviceToken = Str(json, "deviceToken");
        _settings.CloudStationId = StationId(json);
        _settings.CloudLicense = Str(json, "license");
        _settings.CloudSyncedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        ReadSubscription(json);
        await SaveQuiet();

        return CloudResult.Done;
    }

    /// <summary>تمدید با کدِ تازه — بی فعال‌سازیِ دوباره.</summary>
    public async Task<CloudResult> RedeemAsync(string code, CancellationToken ct = default)
    {
        if (!Activated) return await ActivateAsync(code, ct);

        var clean = new string((code ?? "").Where(char.IsDigit).ToArray());
        if (clean.Length != 6) return CloudResult.No("کد باید شش رقم باشد");

        var (ok, json, why, errCode) =
            await PostAsync("/api/pump/device/redeem", new { code = clean }, _settings.CloudDeviceToken, ct);
        if (!ok) return CloudResult.No(why, errCode);

        _settings.CloudLicense = Str(json, "license");
        _settings.CloudSyncedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        ReadSubscription(json);
        await SaveQuiet();
        return CloudResult.Done;
    }

    // ── تازه‌سازی ──────────────────────────────────────────────────────

    /// <summary>
    /// وضعیتِ اشتراک و مجوزِ تازه.
    ///
    /// هر بار که برنامه بالا می‌آید صدا زده می‌شود. نشدنش اشکالی ندارد:
    /// مجوزِ ذخیره‌شده تا ده روز آفلاین کار می‌کند.
    /// </summary>
    public async Task<CloudResult> RefreshAsync(CancellationToken ct = default)
    {
        if (!Activated) return CloudResult.No("این برنامه هنوز فعال نشده است", "not_activated");

        var (ok, me, why, errCode) = await GetAsync("/api/pump/device/me", _settings.CloudDeviceToken, ct);
        if (!ok) return CloudResult.No(why, errCode);

        _settings.CloudStationId = StationId(me);
        ReadSubscription(me);

        //  مجوزِ تازه — جدا، چون ممکن است اشتراک تمام شده باشد و مجوزی
        //  صادر نشود. آن هم یک جوابِ درست است، نه خطا.
        var (licOk, lic, _, _) =
            await PostAsync("/api/pump/device/license", new { }, _settings.CloudDeviceToken, ct);
        if (licOk)
        {
            var token = Str(lic, "license");
            //  ⚠️ خالی بودن یعنی «اشتراک ندارد» — مجوزِ قبلی را پاک می‌کنیم،
            //  وگرنه تا ده روز با مجوزِ کهنه باز می‌ماند.
            _settings.CloudLicense = token;
            var key = Str(lic, "publicKey");
            if (!string.IsNullOrWhiteSpace(key) && string.IsNullOrWhiteSpace(_settings.CloudPublicKey))
                _settings.CloudPublicKey = key;
        }

        _settings.CloudSyncedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        await SaveQuiet();
        return CloudResult.Done;
    }

    /// <summary>
    /// سپردنِ نشانی و رمزِ فقط‌خواندنیِ سرورِ خانگی به ابر.
    ///
    /// همان چیزی که اپِ کارمند را از پرسیدنِ آدرس بی‌نیاز می‌کند: آی‌پیِ
    /// خانگی با هر بار روشن شدنِ مودم عوض می‌شود، پس این باید پرتکرار و
    /// ارزان باشد.
    /// </summary>
    public async Task<CloudResult> PublishHomeAsync(string homeUrl, string readKey,
        CancellationToken ct = default)
    {
        if (!Activated) return CloudResult.No("فعال نشده", "not_activated");
        if (string.IsNullOrWhiteSpace(homeUrl)) return CloudResult.No("نشانیِ خانگی خالی است");

        var (ok, _, why, code) = await PostAsync("/api/pump/device/home",
            new { homeUrl, readKey = readKey ?? "" }, _settings.CloudDeviceToken, ct);
        return ok ? CloudResult.Done : CloudResult.No(why, code);
    }

    /// <summary>کدِ کوتاهی که کارمند با آن به این پمپ می‌پیوندد.</summary>
    public async Task<(bool Ok, string Code, string Why)> JoinCodeAsync(CancellationToken ct = default)
    {
        if (!Activated) return (false, "", "این برنامه هنوز فعال نشده است");
        var (ok, json, why, _) = await PostAsync("/api/pump/device/join-code",
            new { role = "staff", hours = 24, maxUses = 10 }, _settings.CloudDeviceToken, ct);
        return ok ? (true, Str(json, "code"), "") : (false, "", why);
    }

    // ── حساب: ورود با گوگل ─────────────────────────────────────────────

    /// <summary>آیا با حسابِ گوگل وارد شده‌ایم.</summary>
    public bool SignedIn => !string.IsNullOrWhiteSpace(_settings.CloudAccountToken);

    /// <summary>ایمیلِ حساب — برای نشان دادن در صفحهٔ حساب.</summary>
    public string Email => _settings.CloudEmail;

    /// <summary>نامِ حساب — برای نشان دادن در صفحهٔ حساب.</summary>
    public string Name => _settings.CloudName;

    /// <summary>
    /// شناسهٔ کلاینتِ گوگل را از خودِ سرور می‌پرسد.
    ///
    /// ⚠️ از داخلِ کد نمی‌آید تا عوض شدنش نسخهٔ تازهٔ برنامه نخواهد — همان
    /// قاعده‌ای که اپِ کارمندان هم دارد.
    /// </summary>
    public async Task<string> GoogleClientIdAsync(CancellationToken ct = default)
    {
        var (ok, json, _, _) = await GetAsync("/api/config", null, ct);
        if (!ok) return "";
        //  شناسهٔ Desktop مالِ همین برنامه است؛ شناسهٔ Web مالِ سایت و اپِ
        //  کارمندان. اگر سرور هنوز جدا نکرده باشد، به همان یکی برمی‌گردیم.
        var desktop = Str(json, "googleDesktopClientId");
        return desktop.Length > 0 ? desktop : Str(json, "googleClientId");
    }

    /// <summary>
    /// توکنِ هویتِ گوگل را به سرور می‌دهد و نشستِ حساب می‌گیرد.
    /// </summary>
    public async Task<CloudResult> SignInAsync(string idToken, CancellationToken ct = default)
    {
        var (ok, json, why, code) = await PostAsync(
            "/api/auth/google", new { idToken, app = "pump" }, null, ct);
        if (!ok) return CloudResult.No(why, code);

        var token = Str(json, "token");
        if (token.Length == 0) return CloudResult.No("سرور نشست نداد");

        _settings.CloudAccountToken = token;
        _settings.CloudRefreshToken = Str(json, "refreshToken");
        if (json.TryGetProperty("user", out var u) && u.ValueKind == JsonValueKind.Object)
        {
            _settings.CloudEmail = Str(u, "email");
            _settings.CloudName  = Str(u, "name");
        }
        await SaveQuiet();
        return CloudResult.Done;
    }

    /// <summary>خروج از حساب — توکن‌ها پاک می‌شوند، دفترِ روی کامپیوتر نه.</summary>
    public async Task SignOutAsync()
    {
        _settings.CloudAccountToken = "";
        _settings.CloudRefreshToken = "";
        _settings.CloudEmail = "";
        _settings.CloudName = "";
        await SaveQuiet();
    }

    /// <summary>
    /// نشانیِ سرورِ خانگی و رمزِ خواندن را از حساب می‌گیرد.
    ///
    /// ⚠️ همین است که کادرِ «نشانیِ سرور» و «رمزِ سرور» را از تنظیمات
    /// برداشت: کاربر هیچ‌کدام را تایپ نمی‌کند، از حسابش می‌آید.
    /// </summary>
    public async Task<(bool Ok, string Url, string ReadKey, string Station, string Why)>
        HomeFromAccountAsync(CancellationToken ct = default)
    {
        if (!SignedIn) return (false, "", "", "", "اول با گوگل وارد شوید");

        var (ok, json, why, _) = await GetAsync("/api/pump/me", _settings.CloudAccountToken, ct);
        if (!ok) return (false, "", "", "", why);

        if (!json.TryGetProperty("home", out var home) || home.ValueKind != JsonValueKind.Object)
            return (false, "", "", "", "هنوز پمپی به این حساب وصل نشده است");

        return (true, Str(home, "url"), Str(home, "readKey"), Str(home, "station"), "");
    }

    // ── گفت‌وگو با ابر ─────────────────────────────────────────────────

    private static async Task<(bool, JsonElement, string, string)> Send(
        HttpRequestMessage req, CancellationToken ct)
    {
        try
        {
            using var res = await Http.SendAsync(req, ct);
            var text = await res.Content.ReadAsStringAsync(ct);
            JsonElement json = default;
            try
            {
                using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(text) ? "{}" : text);
                json = doc.RootElement.Clone();
            }
            catch { /* پاسخِ بی‌شکل */ }

            if (res.IsSuccessStatusCode) return (true, json, "", "");

            var why = "";
            var code = "";
            if (json.ValueKind == JsonValueKind.Object && json.TryGetProperty("error", out var e))
            {
                why = Str(e, "message");
                code = Str(e, "code");
            }
            if (string.IsNullOrWhiteSpace(why)) why = $"سرور جواب نداد ({(int)res.StatusCode})";
            return (false, json, why, code);
        }
        catch (TaskCanceledException) { return (false, default, "سرور دیر جواب داد", "timeout"); }
        catch (HttpRequestException) { return (false, default, "به سرور نرسیدیم — اینترنت را بررسی کنید", "offline"); }
        catch (Exception ex) { return (false, default, ex.Message, "error"); }
    }

    private static Task<(bool, JsonElement, string, string)> PostAsync(
        string path, object body, string? token, CancellationToken ct)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, CloudConfig.BaseUrl + path)
        {
            Content = JsonContent.Create(body),
        };
        if (!string.IsNullOrWhiteSpace(token)) req.Headers.Add("Authorization", $"Bearer {token}");
        return Send(req, ct);
    }

    private static Task<(bool, JsonElement, string, string)> GetAsync(
        string path, string? token, CancellationToken ct)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, CloudConfig.BaseUrl + path);
        if (!string.IsNullOrWhiteSpace(token)) req.Headers.Add("Authorization", $"Bearer {token}");
        return Send(req, ct);
    }

    // ── خواندنِ پاسخ ───────────────────────────────────────────────────

    private void ReadSubscription(JsonElement json)
    {
        if (json.ValueKind != JsonValueKind.Object) return;
        if (!json.TryGetProperty("entitlement", out var ent) || ent.ValueKind != JsonValueKind.Object)
            return;

        var source = Str(ent, "source");
        var feats = new List<string>();
        if (ent.TryGetProperty("features", out var f) && f.ValueKind == JsonValueKind.Array)
            foreach (var x in f.EnumerateArray())
                if (x.ValueKind == JsonValueKind.String) feats.Add(x.GetString() ?? "");

        var days = 0;
        long endsAt = 0;
        var plan = "";
        if (source == "trial" && ent.TryGetProperty("trial", out var tr) && tr.ValueKind == JsonValueKind.Object)
        {
            days = (int)Num(tr, "daysLeft");
            endsAt = Num(tr, "endsAt");
            plan = "دورهٔ آزمایشی";
        }
        else if (ent.TryGetProperty("subscription", out var sub) && sub.ValueKind == JsonValueKind.Object)
        {
            days = (int)Num(sub, "daysLeft");
            endsAt = Num(sub, "endsAt");
            plan = Str(sub, "plan");
        }

        Subscription = new PumpSubscription(
            source is "subscription" or "trial", source, plan, days, endsAt, feats);
    }

    private static string StationId(JsonElement json) =>
        json.ValueKind == JsonValueKind.Object
        && json.TryGetProperty("station", out var st)
        && st.ValueKind == JsonValueKind.Object
            ? Str(st, "id") : "";

    private static string Str(JsonElement e, string k) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(k, out var v)
        && v.ValueKind == JsonValueKind.String ? (v.GetString() ?? "") : "";

    private static long Num(JsonElement e, string k) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(k, out var v)
        && v.ValueKind == JsonValueKind.Number && v.TryGetInt64(out var n) ? n : 0;

    private async Task SaveQuiet()
    {
        try { await _save(); } catch { /* ذخیره نشدنِ تنظیمات نباید کار را بشکند */ }
    }
}
