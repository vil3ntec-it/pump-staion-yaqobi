using Microsoft.EntityFrameworkCore;
using PumpYaqobi.Application.Security;
using PumpYaqobi.Domain.Entities;

namespace PumpYaqobi.Services.Data;

/// <summary>
/// ══ صندوقِ نوت‌های هر بخش ═══════════════════════════════════════════════════
///
/// همتای ‎sendNoteToBox‎ · ‎openNotes‎ · ‎saveNoteDraft‎ی نسخهٔ وب.
///
/// در سایت پایینِ چهارده بخش یک کادرِ ‎.sec-note-box‎ هست — می‌نویسی، «📨
/// فرستادن به صندوق نوت‌ها» را می‌زنی و همان‌جا ثبت می‌شود؛ «📋 لیست نوت‌ها»
/// همه‌شان را نشان می‌دهد. برنامهٔ نیتیو هیچ‌کدامش را نداشت.
///
/// ⚠️ پیش‌نویس و نوتِ ثبت‌شده دو چیزِ جدا هستند، مثلِ سایت: پیش‌نویس همان
/// چیزی است که هنوز فرستاده نشده و با هر حرف ذخیره می‌شود تا با بسته شدنِ
/// برنامه نپرد؛ نوت وقتی ساخته می‌شود که دکمهٔ فرستادن زده شود.
/// </summary>
public sealed class SectionNoteService
{
    private readonly PumpDbFactory _dbf;
    private readonly PermissionService? _perm;

    public SectionNoteService(PumpDbFactory dbf, PermissionService? perm = null)
    { _dbf = dbf; _perm = perm; }

    /// <summary>نوت‌های یک بخش — تازه‌ها اول، مثلِ سایت.</summary>
    public async Task<List<SectionNote>> ListAsync(string sectionKey, CancellationToken ct = default)
    {
        await using var db = _dbf.Create();
        return await db.SectionNotes
            .Where(n => n.SectionKey == sectionKey && n.DeletedAt == null)
            .OrderByDescending(n => n.Id)
            .ToListAsync(ct);
    }

    /// <summary>شمارِ نوت‌های یک بخش — عددِ کنارِ «📋 لیست نوت‌ها».</summary>
    public async Task<int> CountAsync(string sectionKey, CancellationToken ct = default)
    {
        await using var db = _dbf.Create();
        return await db.SectionNotes
            .CountAsync(n => n.SectionKey == sectionKey && n.DeletedAt == null, ct);
    }

    /// <summary>
    /// «📨 فرستادن به صندوق نوت‌ها». متنِ خالی چیزی نمی‌سازد — همان کاری که
    /// سایت می‌کند («اول یادداشت را بنویسید»). پیش‌نویس هم پاک می‌شود.
    /// </summary>
    public async Task<SectionNote?> SendAsync(string sectionKey, string? text, string dateShamsi,
                                              CancellationToken ct = default)
    {
        var t = (text ?? "").Trim();
        if (t.Length == 0) return null;

        _perm?.Require(Permission.EditData);
        await using var db = _dbf.Create();

        var n = new SectionNote { SectionKey = sectionKey, Text = t, DateShamsi = dateShamsi };
        db.SectionNotes.Add(n);

        var draft = await db.SectionNoteDrafts.FirstOrDefaultAsync(d => d.SectionKey == sectionKey, ct);
        if (draft is not null) draft.Text = "";

        await db.SaveChangesAsync(ct);
        return n;
    }

    /// <summary>حذفِ نرمِ یک نوت — رکورد واقعاً پاک نمی‌شود.</summary>
    public async Task DeleteAsync(long id, CancellationToken ct = default)
    {
        _perm?.Require(Permission.EditData);
        await using var db = _dbf.Create();
        var n = await db.SectionNotes.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (n is null) return;
        n.DeletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    /// <summary>پیش‌نویسِ همان کادر — چیزی که هنوز فرستاده نشده.</summary>
    public async Task<string> GetDraftAsync(string sectionKey, CancellationToken ct = default)
    {
        await using var db = _dbf.Create();
        var d = await db.SectionNoteDrafts.FirstOrDefaultAsync(x => x.SectionKey == sectionKey, ct);
        return d?.Text ?? "";
    }

    /// <summary>ذخیرهٔ پیش‌نویس — همتای ‎saveNoteDraft‎.</summary>
    public async Task SaveDraftAsync(string sectionKey, string? text, CancellationToken ct = default)
    {
        await using var db = _dbf.Create();
        var d = await db.SectionNoteDrafts.FirstOrDefaultAsync(x => x.SectionKey == sectionKey, ct);
        if (d is null)
        {
            d = new SectionNoteDraft { SectionKey = sectionKey };
            db.SectionNoteDrafts.Add(d);
        }
        d.Text = text ?? "";
        await db.SaveChangesAsync(ct);
    }
}
