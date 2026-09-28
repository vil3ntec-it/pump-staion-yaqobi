using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using PumpYaqobi.Application.Security;
using PumpYaqobi.Application.Services;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;
using PumpYaqobi.Services.Data;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ پنج کارِ ماندهٔ ۳.۱.۲۱۳ — ۱۴۰۵/۰۷/۱۶ ═════════════════════════════════════
///
/// «اون ها رو هم درست کن و هیچ باگ یا مشکلی توی برنامه نباشه.» هر بند با
/// دفترِ واقعیِ SQLite سنجیده می‌شود، نه با رشتهٔ سورس.
/// </summary>
[Collection(OpLogCollection.Name)]
public class Round3FixTests : IDisposable
{
    private readonly string _file = Path.Combine(Path.GetTempPath(), $"pump-r3-{Guid.NewGuid():N}.db");

    public void Dispose()
    {
        foreach (var f in new[] { _file, _file + "-wal", _file + "-shm" })
            try { if (File.Exists(f)) File.Delete(f); } catch { }
    }

    private sealed record Host(PumpDbFactory Db, ParchaDataService Parcha, TrashService Trash,
                               WaraqDataService Waraq, ShiftWaraqSyncService Sync, BackupService Backup);

    private Host Make()
    {
        var dbf = new PumpDbFactory(_file);
        dbf.EnsureReady();
        var session = new UserSession();
        session.SignIn(UserRole.Admin, "آزمون");
        var perm = new PermissionService(session);
        var trash = new TrashService(dbf, perm, session);
        var settings = new SettingsService(dbf, perm);
        var sync = new ShiftWaraqSyncService(dbf, perm, new WaraqService(), settings);
        trash.ResyncWaraqSales = sync.ResyncSalesAsync;
        return new Host(dbf, new ParchaDataService(dbf, perm, trash, new ParchaService(), sync),
                        trash, new WaraqDataService(dbf, perm, trash), sync, new BackupService(dbf, perm));
    }

    private static Task<ShiftSaveResult> Save(ParchaDataService p, string date, decimal end,
                                              FuelType fuel = FuelType.Petrol, ShiftKind kind = ShiftKind.Day) =>
        p.SaveShiftFlowAsync(new ShiftSaveRequest(fuel, kind, date, "کریم", 1, 0m, end, 60m,
                                                  0m, 0m, 0m, "", 20m, false));

    private static async Task<(int Pumps, decimal Sales)> WaraqState(PumpDbFactory dbf)
    {
        await using var db = dbf.Create();
        var pumps = await db.WaraqPumps.AsNoTracking().CountAsync(p => p.SrcKey != null && p.SrcKey != "");
        var sales = (await db.SafeEntries.AsNoTracking()
                             .Where(e => e.SrcKey != null && e.SrcKey.StartsWith("wq-sales-"))
                             .Select(e => e.Amount).ToListAsync()).Sum();
        return (pumps, sales);
    }

    // ── ۱) حذفِ پارچه پایه‌هایش را از ورق می‌برد — و بازگردانی برمی‌گرداندشان ──
    [Fact]
    public async Task HazfeParcha_PayehayashRa_AzWaraqMibarad_VaBargashtBarmigardanad()
    {
        var h = Make();
        var r = await Save(h.Parcha, "1405/07/10", 100m);
        Assert.True(r.Ok, r.Error);
        Assert.Equal((1, 6000m), await WaraqState(h.Db));

        await h.Parcha.DeleteAsync(r.Report!.Id);
        Assert.Equal((0, 0m), await WaraqState(h.Db));

        long itemId;
        await using (var db = h.Db.Create())
            itemId = (await db.Trash.AsNoTracking().FirstAsync(t => t.Kind == "parcha")).Id;
        var (err, trace) = await h.Trash.RestoreTracedAsync(itemId);
        Assert.Null(err);
        Assert.Equal((1, 6000m), await WaraqState(h.Db));

        // ‎Ctrl+Y‎: همان پایه دوباره می‌رود، و فروشِ گاوصندوق هم
        Assert.NotNull(await h.Trash.DeleteAgainAsync(trace!));
        Assert.Equal((0, 0m), await WaraqState(h.Db));
    }

    // ── ۱ب) تاریخِ پارچه که عوض شد، پایه در ورقِ قبلی جا نمی‌ماند ──────────────
    [Fact]
    public async Task TarikheParchaAvazShod_PayeDarWaraqeGhabli_JaNemimanad()
    {
        var h = Make();
        var a = await Save(h.Parcha, "1405/07/10", 100m);
        Assert.True(a.Ok, a.Error);
        await using (var db = h.Db.Create())
        {
            var rep = await db.Reports.Include(x => x.DayShift).FirstAsync(x => x.Id == a.Report!.Id);
            rep.DayShift!.SavedAt = "1405/07/11";
            await db.SaveChangesAsync();
            await h.Sync.SyncSavedShiftAsync(ShiftKind.Day, rep.DayShift, FuelType.Petrol,
                ShiftWaraqSyncService.SrcKeyOf(FuelType.Petrol, rep.Id, ShiftKind.Day));
        }

        await using var check = h.Db.Create();
        var where = await check.WaraqPumps.AsNoTracking().Where(p => p.SrcKey != null && p.SrcKey != "")
                               .Select(p => p.Shift!.Waraq!.DateShamsi).ToListAsync();
        Assert.Equal(new[] { "1405/07/11" }, where);
        Assert.Equal((1, 6000m), await WaraqState(h.Db));
    }

    // ── ۱ج) ورقِ برگشته از سطل فروشِ گاوصندوقش را هم برمی‌گرداند ───────────────
    [Fact]
    public async Task WaraqeBargashte_ForusheGavsandooghRaHamMiavarad()
    {
        var h = Make();
        Assert.True((await Save(h.Parcha, "1405/07/10", 100m)).Ok);
        long wid;
        await using (var db = h.Db.Create()) wid = (await db.WaraqEntries.AsNoTracking().FirstAsync()).Id;

        await h.Waraq.DeleteAsync(wid);
        Assert.Equal(0m, (await WaraqState(h.Db)).Sales);

        long itemId;
        await using (var db = h.Db.Create())
            itemId = (await db.Trash.AsNoTracking().FirstAsync(t => t.Kind == "waraq")).Id;
        Assert.Null(await h.Trash.RestoreAsync(itemId));
        Assert.Equal((1, 6000m), await WaraqState(h.Db));
    }

    // ── ۲) پشتیبانِ رمزشده (‎.pyq‎) بازگرداندنی است — نسخهٔ پیشین هم ────────────
    [Fact]
    public void PoshtibaneRamzshode_BazMishavad_HattaNoskheyePishin()
    {
        var h = Make();
        var path = SyncBackup.Write(h.Db, label: "r3");
        Assert.NotNull(path);
        Assert.True(SyncBackup.IsEncrypted(path!));
        Assert.False(SyncBackup.IsEncrypted(_file));

        var tmp = h.Backup.DecryptToTemp(path!);
        try
        {
            Assert.NotNull(tmp);
            Assert.True(new FileInfo(tmp!).Length > 0);
        }
        finally { if (tmp is not null) File.Delete(tmp); File.Delete(path!); }

        // نسخهٔ پیشین (‎PYQB1‎، کلید با مسیرِ نصب) هنوز خوانده می‌شود
        var plain = Encoding.UTF8.GetBytes("SQLite format 3\0 آزمون");
        var key = SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('|', "pump-yaqobi-backup-v1",
            Environment.MachineName, Environment.UserName, AppContext.BaseDirectory)));
        var nonce = RandomNumberGenerator.GetBytes(12);
        var cipher = new byte[plain.Length];
        var tag = new byte[16];
        using (var aes = new AesGcm(key, 16)) aes.Encrypt(nonce, plain, cipher, tag);
        var old = Path.Combine(Path.GetTempPath(), $"r3-old-{Guid.NewGuid():N}.pyq");
        var back = old + ".db";
        try
        {
            File.WriteAllBytes(old, Encoding.ASCII.GetBytes("PYQB1\0\0\0").Concat(nonce).Concat(tag).Concat(cipher).ToArray());
            Assert.True(SyncBackup.IsEncrypted(old));
            Assert.True(SyncBackup.Read(old, back));
            Assert.Equal(plain, File.ReadAllBytes(back));
        }
        finally { File.Delete(old); if (File.Exists(back)) File.Delete(back); }
    }

    // ── ۳) نرخِ اتحادیه در تاریخچه ثبت می‌شود — یک بار، نه به ازای هر حرف ───────
    [Fact]
    public async Task NerkheEttehadiye_YekBarDarTarikhche()
    {
        var calls = new List<(FuelType, decimal)>();
        var rec = new PumpYaqobi.App.Services.RateRecorder((f, r) => { lock (calls) calls.Add((f, r)); return Task.CompletedTask; });
        rec.Note(FuelType.Petrol, 8m);
        rec.Note(FuelType.Petrol, 80m);
        rec.Note(FuelType.Petrol, 80.5m);
        Assert.True(rec.IsDirty);
        await rec.FlushAsync();           // همان کارِ ‎SaveGuard‎ سرِ بستنِ برنامه
        Assert.False(rec.IsDirty);
        Assert.Equal(new[] { (FuelType.Petrol, 80.5m) }, calls);

        // و بی ‎Flush‎، پس از مکث خودش ثبت می‌کند
        rec.Note(FuelType.Diesel, 70m);
        await Task.Delay(PumpYaqobi.App.Services.RateRecorder.Delay + TimeSpan.FromMilliseconds(1500));
        lock (calls) Assert.Contains((FuelType.Diesel, 70m), calls);
    }

    // ── سورس: هر پنج بند سرِ جایشان ──────────────────────────────────────────
    [Fact]
    public void Sors_HarPanjBand()
    {
        var root = FindRoot();
        string R(string p) => File.ReadAllText(Path.Combine(root, p));

        Assert.Contains("RecordRateAsync", R("PumpYaqobi.App/ViewModels/Sections/ProfitSectionViewModel.cs"));
        Assert.Contains("keepFilters: true", R("PumpYaqobi.App/ViewModels/Sections/HistorySectionViewModel.cs"));
        Assert.Contains("Classes.hit=\"{Binding IsHighlighted}\"", R("PumpYaqobi.App/Views/Sections/CompanyPurchasesView.axaml"));
        Assert.Contains("SelectedItem=\"{Binding SelectedRow}\"", R("PumpYaqobi.App/Views/Sections/CompanyArchiveView.axaml"));
        Assert.Contains("SyncBackup.IsEncrypted", R("PumpYaqobi.App/ViewModels/Sections/BackupSectionViewModel.cs"));
        Assert.Contains("Trash.ResyncWaraqSales = ShiftWaraqSync.ResyncSalesAsync", R("PumpYaqobi.App/Services/AppHost.cs"));
    }

    private static string FindRoot()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null && !File.Exists(Path.Combine(d.FullName, "PumpYaqobi.App", "PumpYaqobi.App.csproj")))
            d = d.Parent;
        return d?.FullName ?? throw new DirectoryNotFoundException("native/");
    }
}
