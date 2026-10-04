using System.Net;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PumpYaqobi.App.Services;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Services.Data;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ شورا، ت۳ — «هر حساب، دفترِ خودش» با <b>رفتارِ خودِ میزبان</b> ══════════
///
/// تا امروز این چهار قاعده فقط با گشتنِ متنِ <c>AppHost.cs</c> سنجیده می‌شدند
/// (یک رشته باید در فایل باشد). حالا یک <see cref="AppHost"/>ِ واقعی روی
/// دیتابیسِ موقت ساخته می‌شود — <b>نه</b> <c>AppHost.Current</c> — و
/// <c>UseLedgerOf</c> همان‌طور که حلقهٔ همگام‌سازی و ورود صدایش می‌زنند زده
/// می‌شود؛ بعد فایل‌های روی دیسک و خودِ ردیف‌ها خوانده می‌شوند.
///
/// ⚠️ سازندهٔ میزبان <c>AppSettings.DirOverride</c> را عوض می‌کند (سراسری)، پس
/// کلاس در مجموعهٔ میزبان است و مقدارِ <b>قبلی</b> را برمی‌گرداند.
/// </summary>
[Collection(AppHostCollection.Name)]
public class AccountLedgerBehaviourTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"pump-ledger-host-{Guid.NewGuid():N}");
    private readonly string? _prevDir = AppSettings.DirOverride;
    private string RootDb => Path.Combine(_dir, AccountLedger.FileName);

    public AccountLedgerBehaviourTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        CloudLink.TestTransport = null;
        CloudLink.ResetReach();
        AppSettings.DirOverride = _prevDir;
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(_dir, true); } catch { /* ویندوز گاهی دیر رها می‌کند */ }
        GC.SuppressFinalize(this);
    }

    private AppHost NewHost() => new(RootDb);

    private static void Put(AppHost h, string title)
    {
        using var db = h.Db.Create();
        db.SafeEntries.Add(new SafeEntry { Title = title, Amount = 1, MonthKey = "1405-07" });
        db.SaveChanges();
    }

    private static List<string> Titles(AppHost h)
    {
        using var db = h.Db.Create();
        return db.SafeEntries.AsNoTracking().Select(x => x.Title ?? "").OrderBy(x => x).ToList();
    }

    private HashSet<string> FilesOnDisk() =>
        Directory.EnumerateFiles(_dir, "*.db", SearchOption.AllDirectories)
                 .Select(Path.GetFullPath).ToHashSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>صاحبِ دفترِ ریشه، همان‌طور که روی دیسک نشسته — نه از حافظه.</summary>
    private string LedgerOwnerOnDisk()
    {
        var f = Path.Combine(_dir, "settings.json");
        Assert.True(File.Exists(f), "تنظیمات اصلاً روی دیسک ننشست");
        using var doc = JsonDocument.Parse(SrcText.Read(f));
        return doc.RootElement.TryGetProperty("LedgerAccountId", out var v) ? v.GetString() ?? "" : "";
    }

    /// <summary>
    /// ⛔ <b>نخستین ورود دفترِ بی‌حساب را با خودش می‌برد</b>، حسابِ دوم دفترِ
    /// خودش را می‌گیرد، و برگشتنِ حسابِ اول همان دفتر را دست‌نخورده برمی‌گرداند.
    /// </summary>
    [Fact]
    public void Nokhostin_Vorood_Rishe_Ra_Mibarad_VaHesabeDovom_DaftareKhodash()
    {
        var h = NewHost();
        Put(h, "بی‌حساب");

        Assert.False(h.UseLedgerOf("u-1"));                       // همان فایل ⇒ «جابه‌جا نشد»
        Assert.Equal(Path.GetFullPath(RootDb), Path.GetFullPath(h.Db.DbPath));
        Assert.Equal("u-1", h.LedgerAccountId);
        Assert.Equal(new[] { "بی‌حساب" }, Titles(h));

        Assert.True(h.UseLedgerOf("u-2"));
        Assert.NotEqual(Path.GetFullPath(RootDb), Path.GetFullPath(h.Db.DbPath));
        Assert.Empty(Titles(h));                                  // دفترِ حسابِ اول دیده نمی‌شود
        Put(h, "دوم");

        Assert.True(h.UseLedgerOf("u-1"));
        Assert.Equal(Path.GetFullPath(RootDb), Path.GetFullPath(h.Db.DbPath));
        Assert.Equal(new[] { "بی‌حساب" }, Titles(h));             // دست‌نخورده

        Assert.True(h.UseLedgerOf("u-2"));
        Assert.Equal(new[] { "دوم" }, Titles(h));
    }

    /// <summary>
    /// ⛔ <b>صاحبِ دفترِ ریشه همان لحظه روی دیسک است</b> (بادوام، نه «بعداً»):
    /// میزبانِ تازه‌ای که همین الان بالا بیاید آن را می‌بیند.
    /// </summary>
    [Fact]
    public void Sahebe_Daftare_Rishe_HamanLahze_RooyeDisk_Ast()
    {
        var h = NewHost();
        h.UseLedgerOf("u-1");
        Assert.Equal("u-1", LedgerOwnerOnDisk());

        //  و حسابِ دوم صاحب را عوض نمی‌کند
        h.UseLedgerOf("u-2");
        Assert.Equal("u-1", LedgerOwnerOnDisk());
    }

    /// <summary>
    /// ⛔ <b>خروج از حساب دفتر را عوض نمی‌کند</b> — و هیچ خبرِ جابه‌جایی هم نمی‌دهد.
    /// </summary>
    [Fact]
    public void Khoruj_Az_Hesab_Daftar_Ra_Avaz_Nemikonad()
    {
        var h = NewHost();
        h.UseLedgerOf("u-1");
        h.UseLedgerOf("u-2");
        var path = h.Db.DbPath;
        Put(h, "دوم");

        var fired = 0;
        h.LedgerSwitched += () => fired++;

        Assert.False(h.UseLedgerOf(""));
        Assert.False(h.UseLedgerOf(null));
        Assert.Equal(path, h.Db.DbPath);
        Assert.Equal("u-2", h.LedgerAccountId);
        Assert.Equal(new[] { "دوم" }, Titles(h));
        Assert.Equal(0, fired);

        //  و جابه‌جاییِ واقعی دقیقاً یک خبر
        h.UseLedgerOf("u-1");
        Assert.Equal(1, fired);
    }

    /// <summary>
    /// ⛔ <b>عوض کردنِ حساب هیچ فایلی را پاک نمی‌کند</b>، و پوشهٔ حساب‌ها کنارِ
    /// دفتری است که میزبان با آن بالا آمد — نه پوشهٔ واقعیِ کاربر.
    /// </summary>
    [Fact]
    public void HichFayli_Pak_Nemishavad_VaPushehKenareHaminDaftar_Ast()
    {
        var h = NewHost();
        h.UseLedgerOf("u-1");
        var before = FilesOnDisk();

        foreach (var id in new[] { "u-2", "u-3", "u-1", "u-2", "u-1" })
        {
            h.UseLedgerOf(id);
            Put(h, id);
            Assert.StartsWith(Path.GetFullPath(_dir), Path.GetFullPath(h.Db.DbPath));
            Assert.DoesNotContain(Path.GetFullPath(PumpDbFactory.DefaultPath), Path.GetFullPath(h.Db.DbPath));
        }

        var after = FilesOnDisk();
        Assert.Subset(after, before);                              // هیچ‌کدامِ قبلی نرفت
        Assert.Equal(3, after.Count);                              // ریشه + دو پوشهٔ حساب
    }

    /// <summary>
    /// ⛔ <b>حلقهٔ همگام‌سازی پیش از هر خواندنی از دفتر، دفترِ همان حساب را
    /// می‌خواهد.</b> حساب از جای دیگری (ورود، تازه‌سازیِ نشست) عوض شده و پوسته
    /// هنوز خبر ندارد؛ موتور روی نخِ خودش بیدار می‌شود. بی این قاعده، همان دور
    /// ردیف‌های دفترِ حسابِ <b>قبلی</b> با توکنِ حسابِ <b>تازه</b> می‌رفتند.
    /// </summary>
    [Fact]
    public async Task Halgheye_Hamgamsazi_DaftareHesabeDigar_RaNemiferestad()
    {
        var h = NewHost();
        h.UseLedgerOf("u-1");
        Put(h, "مالِ-حسابِ-اول");

        var pushed = new List<string>();
        var pulls = 0;
        CloudLink.TestTransport = async (req, _) =>
        {
            var path = req.RequestUri!.AbsolutePath;
            if (path == "/api/sync/v1/push")
            {
                var body = req.Content is null ? "" : await req.Content.ReadAsStringAsync();
                lock (pushed) pushed.Add(body);
                return Json(HttpStatusCode.OK, """{"results":[],"applied":0,"cursor":0}""");
            }
            if (path == "/api/sync/v1/pull")
            {
                Interlocked.Increment(ref pulls);
                return Json(HttpStatusCode.OK, """{"ops":[],"cursor":0,"has_more":false}""");
            }
            return Json(HttpStatusCode.NotFound, "{}");
        };

        //  حساب از جای دیگری عوض شد — پوسته هنوز «u-1» را باز دارد
        var f = AppSettings.Load();
        f.CloudDeviceToken = "pd_ledger";
        f.CloudUserId = "u-2";
        f.Save();

        var engine = new SyncEngine(h) { RunWhenDisabled = true };
        //  «بارِ اول» چند دور می‌خواهد؛ تا موتور به گرفتن برسد
        for (var i = 0; i < 200 && pulls == 0; i++) await engine.SyncNowAsync();
        for (var i = 0; i < 3; i++) await engine.SyncNowAsync();

        Assert.True(pulls > 0, "موتور هیچ‌وقت به سرور نرسید — سنجه چیزی نمی‌سنجد");
        Assert.Equal("u-2", h.LedgerAccountId);
        Assert.NotEqual(Path.GetFullPath(RootDb), Path.GetFullPath(h.Db.DbPath));
        Assert.DoesNotContain(pushed, b => b.Contains("مالِ-حسابِ-اول", StringComparison.Ordinal));

        //  و دفترِ حسابِ اول دست‌نخورده سرِ جایش است
        h.UseLedgerOf("u-1");
        Assert.Equal(new[] { "مالِ-حسابِ-اول" }, Titles(h));
    }

    /// <summary>
    /// ⛔ <b>وسطِ جابه‌جاییِ دفتر هیچ دوری نمی‌دود</b> — نه یک درخواست، نه یک
    /// خواندن (<c>Hold</c>).
    /// </summary>
    [Fact]
    public async Task VasateJabejayi_HichDoriNemidavad()
    {
        var h = NewHost();
        h.UseLedgerOf("u-1");
        var hits = 0;
        CloudLink.TestTransport = (_, _) =>
        {
            Interlocked.Increment(ref hits);
            return Task.FromResult(Json(HttpStatusCode.OK, """{"ops":[],"cursor":0,"has_more":false}"""));
        };
        var f = AppSettings.Load();
        f.CloudDeviceToken = "pd_ledger";
        f.CloudUserId = "u-1";
        f.Save();

        var engine = new SyncEngine(h) { RunWhenDisabled = true };
        //  اول تا جایی که هر دور واقعاً به سرور می‌رسد (پس از «بارِ اول»)
        for (var i = 0; i < 200 && hits == 0; i++) await engine.SyncNowAsync();
        Assert.True(hits > 0, "موتور هیچ‌وقت به سرور نرسید — سنجه چیزی نمی‌سنجد");

        engine.Hold(true);
        Interlocked.Exchange(ref hits, 0);
        for (var i = 0; i < 5; i++) await engine.SyncNowAsync();
        Assert.Equal(0, hits);

        engine.Hold(false);
        await engine.SyncNowAsync();
        Assert.True(hits > 0, "پس از رها شدن هم هیچ دوری ندوید: " + engine.Light + " " + engine.Reason);
    }

    private static HttpResponseMessage Json(HttpStatusCode code, string body) =>
        new(code) { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") };

    private sealed class NoteStep : UndoHub.IStep
    {
        public string What => "آزمون";
        public Task<bool> UndoAsync() => Task.FromResult(true);
        public Task<bool> RedoAsync() => Task.FromResult(true);
    }

    /// <summary>
    /// ⛔ <b>دفترِ دیگر ⇒ <c>Ctrl+Z</c> پاک</b>: قلمِ سطلِ دفترِ قبلی نباید در
    /// دفترِ حسابِ تازه برگردانده شود. (شورا ت۳: تا امروز فقط ترتیبِ دو خط در
    /// سورسِ <c>AppHost.cs</c> سنجیده می‌شد.) و جابه‌جا نشدن پاکش نمی‌کند.
    /// </summary>
    [Fact]
    public void DaftareDigar_Tarikhche_Ra_Pak_Mikonad()
    {
        var h = NewHost();
        UndoHub.Clear();
        try
        {
            h.UseLedgerOf("u-1");
            UndoHub.Push(new NoteStep());
            Assert.False(h.UseLedgerOf("u-1"));                   // همان دفتر
            Assert.Equal(1, UndoHub.UndoCount);

            var seen = -1;
            h.LedgerSwitched += () => seen = UndoHub.UndoCount;
            Assert.True(h.UseLedgerOf("u-2"));
            Assert.Equal(0, UndoHub.UndoCount);
            Assert.Equal(0, seen);                                 // پیش از خبرِ «عوض شد»
        }
        finally { UndoHub.Clear(); }
    }

}
