namespace PumpYaqobi.Services.Data;

/// <summary>
/// ══ شورا، ج۵ — «داده عوض شد؟» بی دست زدن به DbContext ═════════════════════════
///
/// ترمزِ <c>Version</c> (قانونِ سرعت: «هیچ کارِ دوره‌ای بی ترمزِ Version») از
/// ویومدل‌ها خوانده می‌شود؛ از امروز از این در، نه از خودِ <c>PumpDbContext</c> —
/// پس <c>ArchitectureTests</c> می‌تواند بگوید هیچ ویومدلی به آن نوع دست نمی‌زند.
/// </summary>
public static class DataVersion
{
    public static long Current => PumpYaqobi.Persistence.PumpDbContext.Version;
}
