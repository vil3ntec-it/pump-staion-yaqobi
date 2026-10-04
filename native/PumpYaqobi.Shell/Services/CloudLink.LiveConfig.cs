namespace PumpYaqobi.App.Services;

/// <summary>
/// ══ «تنظیماتِ زنده» از سرورِ حساب ════════════════════════════════════════
///
/// فقط وقتی می‌رود که سرور روی پاسخِ دقیقه‌ایِ <c>/rate</c> نسخهٔ تازه‌تری گفته
/// باشد (<see cref="LiveConfig.NeedsFetch"/>) — پس در سکوت هیچ درخواستی نمی‌زند.
/// ⛔ فقط با <b>توکنِ دستگاهِ همین پمپ</b>: سرور برگهٔ «همه» + همین پمپ را می‌دهد.
/// شرحِ کامل بالای <see cref="LiveConfig"/>.
/// </summary>
public sealed partial class CloudLink
{
    /// <summary>برگهٔ تازه را می‌گیرد و می‌نشاند. هیچ‌وقت استثنا بیرون نمی‌دهد.</summary>
    /// <returns>مقدارها عوض شدند؟</returns>
    public async Task<bool> LiveConfigTickAsync(CancellationToken ct = default)
    {
        if (!Activated || !LiveConfig.NeedsFetch) return false;
        try
        {
            var (ok, json, _, _) = await DevGetAsync("/api/pump/device/live-config", ct);
            return ok && LiveConfig.Apply(json);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch { return false; }
    }
}
