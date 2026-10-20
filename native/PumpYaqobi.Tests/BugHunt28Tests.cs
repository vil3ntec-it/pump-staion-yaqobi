using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PumpYaqobi.App.Services;
using PumpYaqobi.App.ViewModels.Sections;
using PumpYaqobi.Application.Localization;
using PumpYaqobi.Application.Services;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;
using PumpYaqobi.Persistence;
using PumpYaqobi.Services.Data;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ «برنامه رو اول دیباگ کن» (۱۴۰۵/۰۷/۲۸) ════════════════════════════════════
///
/// هر بند یک باگِ واقعی که بازبینیِ کد پیدا کرد؛ هر آزمون پیش از اصلاح سرخ بود.
/// شرحِ هر کدام کنارِ خودِ اصلاح است.
/// </summary>
public class BugHunt28Tests
{
    private static AppHost Host()
    {
        var host = new AppHost(
            Path.Combine(Path.GetTempPath(), "pump-bh28-" + Guid.NewGuid().ToString("N"), "pump.db"));
        if (host.Auth.NeedsFirstRun()) host.Auth.CreateFirstAdmin("1234");
        host.Auth.SignIn("admin", "1234");
        return host;
    }

    private static async Task<(AppHost Host, AccountViewModel Acct)> OpenPerson()
    {
        var host = Host();
        var sec = new DebtSectionViewModel(host);
        await sec.EnsureLoadedAsync();
        await host.Debtors.AddDebtorAsync("کریم", "", false);
        await sec.RefreshAsync();
        await sec.OpenByNumberAsync(1);
        var person = Assert.IsType<PersonViewModel>(sec.ActivePage);
        return (host, person.Current!);
    }

    // ── ۱) داشبورد: «ورود/خروج»ِ گاوصندوق ────────────────────────────────────

    /// <summary>ماندگی ورود است و بردگی خروج — همان جهتِ موجودی (‎SafeBalance‎).</summary>
    [Fact]
    public void Dashboard_SafeFlow_MandagiIsIn_BardagiIsOut()
    {
        var now = new DateTime(2026, 10, 20, 10, 0, 0);
        var today = Shamsi.Of(now);
        var s = new DashboardService(() => now).Series(DashRange.Day, new DashInput
        {
            SafeEntries = new[]
            {
                new SafeEntry { DateShamsi = today, Kind = SafeEntryKind.Mandagi, Amount = 50_000m, Currency = Currency.Afn },
                new SafeEntry { DateShamsi = today, Kind = SafeEntryKind.Bardagi, Amount = 20_000m, Currency = Currency.Afn },
            },
        });
        var b = s.Buckets[s.Slot + 1];
        Assert.Equal(50_000m, b.SafeIn);
        Assert.Equal(20_000m, b.SafeOut);
    }

    /// <summary>و ردیف‌ها تاریخ دارند — بی آن ‎Series‎ همه را رد می‌کرد و کارت همیشه «۰ · ۰» بود.</summary>
    [Fact]
    public void Dashboard_SafeRowsCarryTheirDate()
    {
        var src = SrcText.Read(Path.Combine(Root(), "PumpYaqobi.App", "ViewModels", "Sections",
                                            "DashboardSectionViewModel.cs"));
        var at = src.IndexOf("SafeLedger.SelectAsync", StringComparison.Ordinal);
        Assert.True(at > 0);
        Assert.Contains("DateShamsi = e.DateShamsi", src.Substring(at, 300));
    }

    // ── ۲) گوشیِ صاحب پمپ: «مفاد امروز» همان عددِ برنامه ─────────────────────

    [Fact]
    public async Task PhoneBanner_TodaysProfit_IsTheWaraqSales_LikeTheDesktop()
    {
        var host = Host();
        var w = await host.WaraqData.OpenOrCreateAsync(Shamsi.Today(), "پمپ یعقوبی");
        await using (var db = host.Db.Create())
        {
            var shift = await db.WaraqShifts.Include(s => s.Pumps)
                                .FirstAsync(s => s.WaraqId == w.Id && s.Kind == ShiftKind.Day);
            var p = shift.Pumps.FirstOrDefault();
            if (p is null) { p = new WaraqPump { ShiftId = shift.Id, Num = 1 }; db.WaraqPumps.Add(p); }
            p.Fuel = FuelType.Petrol; p.Start = 0m; p.End = 100m; p.PricePerLiter = 50m;
            await db.SaveChangesAsync();
        }

        var tk = Shamsi.Key(Shamsi.Today());
        var desktop = (await host.WaraqData.SalesLinesAsync((tk, tk))).Sum(l => l.PetrolMoney + l.DieselMoney);
        Assert.Equal(5_000m, desktop);

        var snap = await StationSnapshot.BuildAsync(host);
        var j = JsonDocument.Parse(JsonSerializer.Serialize(snap)).RootElement;
        var profit = j.GetProperty("banner")[2];
        Assert.Equal("مفاد امروز", profit[0].GetString());
        Assert.Equal(Shamsi.Money(desktop), profit[1].GetString());
    }

    // ── ۳) سقفِ قرض به واحدِ همان حساب ─────────────────────────────────────

    [Fact]
    public async Task CreditLimit_InAFuelAccount_IsLitres_NotAfghani()
    {
        var (_, acct) = await OpenPerson();
        Assert.False(acct.IsMoney);
        await acct.AddRowCommand.ExecuteAsync(null);
        var row = acct.Rows[0];
        row.LitersText = "100";
        row.PriceText = "68";
        await row.FlushAsync();
        acct.CreditLimitText = "500";

        Assert.False(acct.OverLimit);                       // ✖ پیش از اصلاح: «الباقی ۶٬۸۰۰ لیتر»

        row.LitersText = "600";
        row.PriceText = "";                                 // بی فی: پیش از اصلاح هیچ‌وقت هشدار نمی‌داد
        await row.FlushAsync();
        Assert.True(acct.OverLimit);
        Assert.Contains(Shamsi.Money(600m) + " لیتر", acct.OverLimitText);
    }

    // ── ۴) کارتِ قرض‌دار همان عددِ داخلِ حساب، با دو رقمِ اعشار ─────────────

    [Fact]
    public async Task Card_Albaqi_KeepsDecimals_ASettledAccountIsZero()
    {
        var (host, acct) = await OpenPerson();
        var id = acct.Entity.Id;
        for (var i = 0; i < 3; i++)
        {
            var r = new DebtRow { FuelAccountId = id, DateShamsi = "1405/07/01", Liters = 1m, PricePerLiter = 65.5m };
            host.Debt.NormalizeRow(r);
            await host.Debtors.SaveRowAsync(r);
        }
        var paid = new DebtRow { FuelAccountId = id, DateShamsi = "1405/07/02", Rasid = 196.5m };
        host.Debt.NormalizeRow(paid);
        await host.Debtors.SaveRowAsync(paid);

        var cards = await host.Debtors.CardAccountsAsync();
        var debt = cards.Values.Sum(list => host.Debt.SumTotals(list).All.Albaqi);
        Assert.Equal(0m, debt);                             // ✖ پیش از اصلاح: ۱۹۸ − ۱۹۷ = ۱
    }

    // ── ۵) شمارهٔ حواله با نویسهٔ نامرئی ─────────────────────────────────────

    [Fact]
    public void Hawala_Number_WithRlm_StillMatches()
    {
        Assert.Equal("42", HawalaCalc.Norm("‏۴۲"));
        Assert.Equal("42", HawalaCalc.Norm("حواله ‎42‌"));
        Assert.True(HawalaCalc.Matches("‏۴۲", "42"));
        Assert.True(HawalaCalc.Matches("42", "‏۴۲"));
    }

    // ── ۶) ورق ⇒ پارچه: «ختمِ کمتر از شروع هیچ چیزی نمی‌نویسد» ──────────────

    [Fact]
    public void WaraqToParcha_EndBelowStart_WritesNothing()
    {
        var s = new ShiftData { Name = "احمد", Start = 120_000m, End = 120_500m, Price = 80m, Sale = 500m };
        Assert.False(ShiftWaraqSyncService.ApplyToShift(s, "احمد", 1, 120_000m, 12_050m, 80m, 0m));
        Assert.Equal(120_500m, s.End);
        Assert.Equal(500m, s.Sale);

        //  لیترِ آزمایشیِ بیش از فروشِ پایه هم نه
        Assert.False(ShiftWaraqSyncService.ApplyToShift(s, "احمد", 1, 120_000m, 120_100m, 80m, 0m, test: 200m));
        Assert.Equal(120_500m, s.End);

        //  ولی عددِ درست می‌نشیند
        Assert.True(ShiftWaraqSyncService.ApplyToShift(s, "احمد", 1, 120_000m, 120_600m, 80m, 0m));
        Assert.Equal(600m, s.Sale);
    }

    private static string Root() =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
}

/// <summary>
/// ⛔ حذفِ نرم با نوشتنِ خودِ ستون (‎x.DeletedAt = now‎) روی کامپیوترِ دیگر هم حذف است.
/// تا ۱۴۰۵/۰۷/۲۸ «ویرایش» فرستاده می‌شد و گیرنده با ‎"DeletedAt" = NULL‎ِ آخرِ همان
/// ‎UPDATE‎ زنده‌اش می‌کرد: تقسیمِ کهنهٔ خرید روی مخزن‌ها دو بار شمرده می‌شد.
/// </summary>
[Collection(OpLogCollection.Name)]
public class SoftDeleteSyncTests : IDisposable
{
    private readonly string _file = Path.Combine(Path.GetTempPath(), $"pump-sds-{Guid.NewGuid():N}.db");
    private readonly PumpDbFactory _dbf;

    public SoftDeleteSyncTests()
    {
        _dbf = new PumpDbFactory(_file);
        _dbf.EnsureReady();
    }

    public void Dispose()
    {
        OpLog.Enabled = true;
        foreach (var f in new[] { _file, _file + "-wal", _file + "-shm" })
            try { if (File.Exists(f)) File.Delete(f); } catch { }
        GC.SuppressFinalize(this);
    }

    private long AddRow()
    {
        using var db = _dbf.Create();
        var row = new SafeEntry { Title = "ردیف", Amount = 100m, DateKey = 14050701, MonthKey = "1405/07" };
        db.SafeEntries.Add(row);
        db.SaveChanges();
        return row.Id;
    }

    private static JsonElement Json(string raw)
    {
        using var doc = JsonDocument.Parse(raw);
        return doc.RootElement.Clone();
    }

    [Fact]
    public void Sender_SettingDeletedAt_IsADeleteOp()
    {
        var id = AddRow();
        using (var db = _dbf.Create())
        {
            db.SafeEntries.Single(x => x.Id == id).DeletedAt = DateTime.UtcNow;
            db.SaveChanges();
        }

        using var read = _dbf.Create();
        var last = read.SyncOps.OrderBy(x => x.Id).ToList().Last();
        Assert.Equal("delete", last.OpType);
    }

    [Fact]
    public void Receiver_AnUpdateCarryingDeletedAt_DoesNotRevive()
    {
        var id = AddRow();
        string uid;
        using (var db = _dbf.Create()) uid = db.SafeEntries.Single(x => x.Id == id).SyncUid!;
        var ts = DateTime.UtcNow.ToString("O", System.Globalization.CultureInfo.InvariantCulture);

        //  opی که نسخهٔ پیشین می‌فرستاد
        new SyncStore(_dbf).ApplyIncoming(new[]
        {
            new IncomingOp("op-softdel", nameof(SafeEntry), uid, "update",
                           Json($$"""{"DeletedAt":"{{ts}}","UpdatedAt":"{{ts}}"}"""), 1),
        });

        using var read = _dbf.Create();
        Assert.Empty(read.SafeEntries.ToList());
        Assert.NotNull(read.SafeEntries.IgnoreQueryFilters().Single(x => x.Id == id).DeletedAt);
    }
}

/// <summary>
/// ⛔ نصبِ تازه: نمونه‌ای که پیش از بودنِ ‎settings.json‎ خوانده شد، با ذخیرهٔ بعدی‌اش
/// (مثلاً «پښتو») آن‌چه نمونه‌های دیگر در این میان نوشته‌اند را پاک نمی‌کند.
/// </summary>
[Collection(AppHostCollection.Name)]
public class FreshSettingsBaseTests : IDisposable
{
    private readonly string _dir;
    private readonly string? _was;

    public FreshSettingsBaseTests()
    {
        _was = AppSettings.DirOverride;
        _dir = Path.Combine(Path.GetTempPath(), "pump-fresh-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_dir);
        AppSettings.DirOverride = _dir;
    }

    public void Dispose()
    {
        AppSettings.DirOverride = _was;
        try { Directory.Delete(_dir, true); } catch { }
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void AnInstanceLoadedBeforeTheFileExisted_DoesNotWipeLaterLogins()
    {
        Assert.False(File.Exists(Path.Combine(_dir, "settings.json")));
        var early = AppSettings.Load();                     // پنجره، پیش از ورود

        var login = AppSettings.Load();
        login.CloudAccountToken = "acc-1";
        login.CloudRefreshToken = "ref-1";
        login.CloudDeviceToken = "dev-1";
        login.StationCode = "p-abc";
        login.Save();

        early.UiLang = "ps";                                // «پښتو»
        early.Save();

        var disk = AppSettings.Load();
        Assert.Equal("ps", disk.UiLang);
        Assert.Equal("ref-1", disk.CloudRefreshToken);      // ✖ پیش از اصلاح: خالی
        Assert.Equal("acc-1", disk.CloudAccountToken);
        Assert.Equal("dev-1", disk.CloudDeviceToken);
        Assert.Equal("p-abc", disk.StationCode);
    }
}

/// <summary>
/// ⛔ op‌ای که سرورِ کهنه با «جدولِ ناشناس» رد کرد دوباره در صف می‌رود — جدول‌های تازهٔ برنامه
/// (حواله، مخزن‌ها، …) تا ۱۴۰۵/۰۷/۲۸ روی سرور نبودند و opشان برای همیشه «رفته» می‌ماند.
/// </summary>
[Collection(OpLogCollection.Name)]
public class UnknownTableRequeueTests : IDisposable
{
    private readonly string _file = Path.Combine(Path.GetTempPath(), $"pump-utr-{Guid.NewGuid():N}.db");
    private readonly PumpDbFactory _dbf;

    public UnknownTableRequeueTests()
    {
        _dbf = new PumpDbFactory(_file);
        _dbf.EnsureReady();
    }

    public void Dispose()
    {
        OpLog.Enabled = true;
        foreach (var f in new[] { _file, _file + "-wal", _file + "-shm" })
            try { if (File.Exists(f)) File.Delete(f); } catch { }
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void UnknownTable_IsRetried_OtherRejectionsAreNot()
    {
        using (var db = _dbf.Create())
        {
            db.SafeEntries.Add(new SafeEntry { Title = "الف", Amount = 1m, DateKey = 14050701, MonthKey = "1405/07" });
            db.SafeEntries.Add(new SafeEntry { Title = "ب", Amount = 2m, DateKey = 14050701, MonthKey = "1405/07" });
            db.SaveChanges();
        }
        var store = new SyncStore(_dbf);
        var ops = store.Take();
        Assert.Equal(2, ops.Count);
        store.MarkResults(new Dictionary<string, string>
        {
            [ops[0].OpId] = "unknown_table",
            [ops[1].OpId] = "bad_fields",
        });
        Assert.Equal(0, store.Pending());

        Assert.Equal(1, store.RequeueUnknownTables());
        var again = Assert.Single(store.Take());
        Assert.Equal(ops[0].OpId, again.OpId);
    }
}
