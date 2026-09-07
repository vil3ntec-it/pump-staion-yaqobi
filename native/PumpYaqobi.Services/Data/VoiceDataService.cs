using Microsoft.EntityFrameworkCore;
using PumpYaqobi.Application.Security;
using PumpYaqobi.Application.Services;
using PumpYaqobi.Domain.Entities;

namespace PumpYaqobi.Services.Data;

/// <summary>یک نامزدِ صوتی: کلیدِ حساب، نام و فاصله‌اش تا صدای گفته‌شده.</summary>
public readonly record struct VoiceMatch(string Key, string Name, double Distance);

/// <summary>
/// ══ انبارِ صداهای ثبت‌شده ═══════════════════════════════════════════════════
/// رونوشتِ ‎_vxLoad‎ · ‎_vxSave‎ · ‎_vxEnroll‎ · ‎_vxForget‎ · ‎_vxMatch‎ ·
/// ‎_vxCount‎ · ‎_vxTotal‎ (بندِ ۲۴).
///
/// یادگیری بی‌زحمت است، همان‌طور که در نسخهٔ وب بود: هر بار که با صدا نام پیدا
/// نشد و خودِ کاربر روی نام زد، همان صدا برای آن حساب ثبت می‌شود. دفعهٔ بعد
/// خودش می‌شناسد.
///
/// ⚠️ سقفِ سه صدا برای هر نام: بیشترش نه دقت را بالا می‌برد نه لازم است، و
/// دیتابیس را بی‌جهت بزرگ می‌کند. تازه‌ترین‌ها می‌مانند — درست مثلِ ‎e.s.shift()‎.
/// </summary>
public sealed class VoiceDataService
{
    private readonly PumpDbFactory _dbf;
    private readonly PermissionService _perm;

    public VoiceDataService(PumpDbFactory dbf, PermissionService perm)
    { _dbf = dbf; _perm = perm; }

    /// <summary>نسخهٔ موتور. با عوض شدنش، صداهای کهنه دیگر خوانده نمی‌شوند.</summary>
    public const int EngineVersion = 1;

    /// <summary>چند حساب صدای ثبت‌شده دارند (‎_vxTotal‎).</summary>
    public async Task<int> AccountCountAsync(CancellationToken ct = default)
    {
        _perm.Require(Permission.ViewData);
        await using var db = _dbf.Create();
        return await db.VoiceTemplates.AsNoTracking()
                       .Where(v => v.EngineVersion == EngineVersion)
                       .Select(v => v.AccountKey).Distinct().CountAsync(ct);
    }

    /// <summary>چند صدا برای همین حساب ثبت شده (‎_vxCount‎).</summary>
    public async Task<int> CountAsync(string accountKey, CancellationToken ct = default)
    {
        _perm.Require(Permission.ViewData);
        await using var db = _dbf.Create();
        return await db.VoiceTemplates.AsNoTracking()
                       .CountAsync(v => v.AccountKey == accountKey && v.EngineVersion == EngineVersion, ct);
    }

    /// <summary>‎_vxEnroll(key, name, feat)‎ — ثبتِ یک صدا برای یک حساب.</summary>
    public async Task<bool> EnrollAsync(string accountKey, string? name, VoiceFeatures? feat,
                                        CancellationToken ct = default)
    {
        _perm.Require(Permission.EditData);
        if (string.IsNullOrWhiteSpace(accountKey) || feat is null || feat.Frames <= 0) return false;

        await using var db = _dbf.Create();
        db.VoiceTemplates.Add(new VoiceTemplate
        {
            AccountKey = accountKey,
            Name = name,
            Frames = feat.Frames,
            Dim = feat.Dim,
            Data = ToBytes(feat.Data),
            EngineVersion = EngineVersion,
        });
        await db.SaveChangesAsync(ct);

        // کهنه‌ترین‌ها بیرون می‌روند تا سقفِ سه‌تایی نگه داشته شود
        var extra = await db.VoiceTemplates
            .Where(v => v.AccountKey == accountKey && v.EngineVersion == EngineVersion)
            .OrderByDescending(v => v.Id)
            .Skip(VoiceEngine.MaxPerName)
            .ToListAsync(ct);
        if (extra.Count > 0)
        {
            db.VoiceTemplates.RemoveRange(extra);
            await db.SaveChangesAsync(ct);
        }
        return true;
    }

    /// <summary>‎_vxForget(key)‎ — همهٔ صداهای یک حساب.</summary>
    public async Task<bool> ForgetAsync(string accountKey, CancellationToken ct = default)
    {
        _perm.Require(Permission.EditData);
        await using var db = _dbf.Create();
        var rows = await db.VoiceTemplates.Where(v => v.AccountKey == accountKey).ToListAsync(ct);
        if (rows.Count == 0) return false;
        db.VoiceTemplates.RemoveRange(rows);
        await db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>
    /// ‎_vxMatch(feat)‎ — فاصلهٔ صدای گفته‌شده تا هر حساب، نزدیک‌ترین اول.
    ///
    /// فاصلهٔ هر حساب، **کمترینِ** فاصله‌های صداهای ثبت‌شده‌اش است: کافی است
    /// یکی از سه بار که گفته‌اید شبیه باشد.
    /// </summary>
    public async Task<List<VoiceMatch>> MatchAsync(VoiceFeatures feat, CancellationToken ct = default)
    {
        _perm.Require(Permission.ViewData);
        if (feat is null || feat.Frames <= 0) return new List<VoiceMatch>();

        await using var db = _dbf.Create();
        var rows = await db.VoiceTemplates.AsNoTracking()
                           .Where(v => v.EngineVersion == EngineVersion).ToListAsync(ct);

        var best = new Dictionary<string, VoiceMatch>();
        foreach (var r in rows)
        {
            var key = r.AccountKey ?? "";
            if (key.Length == 0 || r.Data.Length != r.Frames * r.Dim) continue;

            var d = VoiceEngine.Distance(feat, new VoiceFeatures(r.Frames, r.Dim, ToSBytes(r.Data)));
            if (double.IsInfinity(d)) continue;
            if (!best.TryGetValue(key, out var cur) || d < cur.Distance)
                best[key] = new VoiceMatch(key, r.Name ?? "", d);
        }

        return best.Values.OrderBy(m => m.Distance).ToList();
    }

    /// <summary>
    /// تصمیمِ ‎_vxOfflineSearch‎: نزدیک‌ترین کافی نیست — باید هم از حدِ پذیرش
    /// نزدیک‌تر باشد و هم از نفرِ دوم به‌اندازهٔ کافی جلوتر، وگرنه پرسیده می‌شود.
    /// </summary>
    public static bool IsSure(IReadOnlyList<VoiceMatch> sorted)
    {
        if (sorted.Count == 0) return false;
        var best = sorted[0];
        if (best.Distance > VoiceEngine.Accept) return false;
        if (sorted.Count == 1) return true;
        return sorted[1].Distance / best.Distance >= VoiceEngine.Margin;
    }

    private static byte[] ToBytes(sbyte[] d)
    {
        var b = new byte[d.Length];
        Buffer.BlockCopy(d, 0, b, 0, d.Length);
        return b;
    }

    private static sbyte[] ToSBytes(byte[] d)
    {
        var s = new sbyte[d.Length];
        Buffer.BlockCopy(d, 0, s, 0, d.Length);
        return s;
    }
}
