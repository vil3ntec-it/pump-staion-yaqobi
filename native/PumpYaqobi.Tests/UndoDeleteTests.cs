using Microsoft.EntityFrameworkCore;
using PumpYaqobi.Application.Security;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;
using PumpYaqobi.Persistence;
using PumpYaqobi.Services.Data;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ ‎Ctrl+Z‎ و ‎Ctrl+Y‎ی حذف، و ‎Shift+عدد‎ که می‌پرسد (۱۴۰۵/۰۷/۱۲) ═════════════
///
/// گزارشِ صاحب ریپو: «شیفت با عدد جدولی رو حذف نمیکنه… اگه پر بود تایید
/// بخواد، اگه خالی بود بی سوال حذف کنه… کنترول زد اصلن کار نمیکنه — هر چیزی
/// از کادر حذف بشه یا حسابی حذف بشه — و کنترول وای هم جلو نمیره.»
///
/// رفتارِ کامل با کلیدِ واقعی در سنجهٔ رابطیِ ‎undokeys‎ است؛ این‌جا دو چیزی
/// که زیرِ آن است روی <b>دیتابیسِ واقعی</b> سنجیده می‌شود:
///   • برگشت = بازگردانی از سطل، و «دوباره» <b>همان</b> رکوردها را می‌برد،
///     با همان مُهر، تا برگشتِ بعدی باز هم پدر و فرزندان را با هم برگرداند.
///   • قاعدهٔ «این ردیف چیزی دارد؟» (‎RowData.HasData‎).
/// </summary>
public class UndoDeleteTests : IDisposable
{
    private readonly string _dir =
        Path.Combine(Path.GetTempPath(), "pump-undo-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try { Directory.Delete(_dir, recursive: true); } catch { }
    }

    private (PumpDbFactory Db, TrashService Trash, DebtorService Debtors) Host()
    {
        Directory.CreateDirectory(_dir);
        var dbf = new PumpDbFactory(Path.Combine(_dir, "pump.db"));
        dbf.EnsureReady();
        var session = new UserSession();
        session.SignIn(UserRole.Admin, "آزمون");
        var perm = new PermissionService(session);
        var trash = new TrashService(dbf, perm, session);
        return (dbf, trash, new DebtorService(dbf, perm, trash));
    }

    private static async Task<Debtor> AddPersonAsync(PumpDbFactory dbf)
    {
        await using var db = dbf.Create();
        var p = new Debtor { Name = "حاجی کریم", LegacyId = "p" + Guid.NewGuid().ToString("N")[..8] };
        p.MainAccount.FuelRows.Add(new DebtRow { DateShamsi = "1405/07/01", Name = "بردگی", Liters = 40m, PricePerLiter = 80m });
        p.MainAccount.FuelRows.Add(new DebtRow { DateShamsi = "1405/07/02", Name = "بردگی", Liters = 12m, PricePerLiter = 80m });
        db.Debtors.Add(p);
        await db.SaveChangesAsync();
        return p;
    }

    /// <summary>
    /// حذفِ کلِ حساب ⇒ برگشت ⇒ دوباره ⇒ برگشت. هر بار شمارِ شخص و ردیف‌ها
    /// درست است، و «دوباره» یک قلمِ سطلِ تازه می‌سازد تا برگشتِ بعدی هم کار کند.
    /// </summary>
    [Fact]
    public async Task Hazf_Bargasht_Dobare_Bargasht_HamanRecordha()
    {
        var (dbf, trash, debtors) = Host();
        var p = await AddPersonAsync(dbf);

        await debtors.DeleteDebtorAsync(p.Id);
        var item = Assert.Single(await trash.ListAsync());

        // ── برگشت ──
        var (err, trace) = await trash.RestoreTracedAsync(item.Id);
        Assert.Null(err);
        Assert.NotNull(trace);
        await using (var db = dbf.Create())
        {
            Assert.Single(await db.Debtors.ToListAsync());
            Assert.Equal(2, await db.DebtRows.CountAsync());
        }
        // شخص، حسابش و دو ردیف — هیچ‌کدام جا نماند
        Assert.Contains(trace!.Rows, r => r.Type == typeof(Debtor) && r.Id == p.Id);
        Assert.Equal(2, trace.Rows.Count(r => r.Type == typeof(DebtRow)));

        // ── دوباره ──
        var again = await trash.DeleteAgainAsync(trace);
        Assert.NotNull(again);
        await using (var db = dbf.Create())
        {
            Assert.Empty(await db.Debtors.ToListAsync());
            Assert.Empty(await db.DebtRows.ToListAsync());
            // ⛔ حذفِ نرم، نه پاک کردنِ واقعی — و همه با یک مُهر
            var stamps = await db.DebtRows.IgnoreQueryFilters().Select(r => r.DeletedAt).Distinct().ToListAsync();
            var dStamp = (await db.Debtors.IgnoreQueryFilters().SingleAsync()).DeletedAt;
            Assert.Single(stamps);
            Assert.Equal(dStamp, stamps[0]);
        }
        var item2 = Assert.Single(await trash.ListAsync());
        Assert.Equal(again, item2.Id);
        Assert.Equal("debtor", item2.Kind);

        // ── و برگشتِ دوم هم همه را با هم برمی‌گرداند ──
        Assert.Null(await trash.RestoreAsync(item2.Id));
        await using (var db = dbf.Create())
        {
            Assert.Single(await db.Debtors.ToListAsync());
            Assert.Equal(2, await db.DebtRows.CountAsync());
        }
    }

    /// <summary>
    /// «همین حالا چیزی به سطل رفت» از <b>یک نقطه</b> خبر داده می‌شود
    /// (‎PumpDbContext.TrashAdded‎) — ‎UndoHub‎ از همین می‌فهمد ‎Ctrl+Z‎ چه را
    /// برگرداند، بی دست زدن به سی‌وچند سرویسِ حذف.
    /// </summary>
    [Fact]
    public async Task HarHazf_Az_YekNoghte_Khabar_Midahad()
    {
        var (dbf, _, debtors) = Host();
        var p = await AddPersonAsync(dbf);
        long rowId;
        await using (var db = dbf.Create()) rowId = (await db.DebtRows.FirstAsync()).Id;

        var got = new List<(long Id, string? Kind)>();
        void On(IReadOnlyList<(long Id, string? Kind)> l) { lock (got) got.AddRange(l); }
        PumpDbContext.TrashAdded += On;
        try { await debtors.DeleteRowAsync(rowId); }
        finally { PumpDbContext.TrashAdded -= On; }

        lock (got)
        {
            Assert.Contains(got, g => g.Kind == "debtrow" && g.Id > 0);
        }
    }

    /// <summary>نوعی که بازگردانی ندارد، قولِ برگشت هم نمی‌گیرد.</summary>
    [Fact]
    public void FaghatNowhayeBargashtani_DarPoshte_Minshinand()
    {
        foreach (var k in new[] { "debtor", "debtrow", "safe", "expense", "sarrafi", "companyrow", "waraq" })
            Assert.True(TrashService.CanRestore(k), k);
        foreach (var k in new[] { "companyarchive", "debtarchive", "", null })
            Assert.False(TrashService.CanRestore(k), k ?? "null");
    }

    // ══ «این ردیف چیزی دارد؟» ═══════════════════════════════════════════════

    [Fact]
    public void RadifeKhali_Pishfarzha_Ra_Nemishomarad()
    {
        // ردیفِ تازهٔ گاوصندوق: تاریخِ امروز و ارز دارد، ولی کاربر چیزی ننوشته
        Assert.False(PumpYaqobi.App.ViewModels.RowData.HasData(new SafeEntry { DateShamsi = "1405/07/12" }));
        // ردیفِ تازهٔ قرض‌دار با فیِ پیش‌فرض — باز هم خالی
        Assert.False(PumpYaqobi.App.ViewModels.RowData.HasData(new DebtRow { DateShamsi = "1405/07/12", PricePerLiter = 80m }));
        // صرافی با نرخِ روزِ پیش‌فرض
        Assert.False(PumpYaqobi.App.ViewModels.RowData.HasData(new ExchangeRow { DateShamsi = "1405/07/12", Rate = 72m }));
    }

    [Fact]
    public void RadifePor_Shenakhte_Mishavad()
    {
        Assert.True(PumpYaqobi.App.ViewModels.RowData.HasData(new SafeEntry { Title = "ماندگی" }));
        Assert.True(PumpYaqobi.App.ViewModels.RowData.HasData(new SafeEntry { Amount = 500m }));
        Assert.True(PumpYaqobi.App.ViewModels.RowData.HasData(new Expense { Note = "نان" }));
        Assert.True(PumpYaqobi.App.ViewModels.RowData.HasData(new DebtRow { Liters = 5m }));
        // ⛔ ردیفِ قرض‌دار با رسیدِ تیل «پر» است — همان قاعدهٔ ‎IsBlankRow‎
        Assert.True(PumpYaqobi.App.ViewModels.RowData.HasData(new DebtRow { RasidFuel = 20m }));
    }

    // ══ قاعده‌ها روی خودِ سورس ═══════════════════════════════════════════════

    private static string Src(params string[] p)
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !Directory.Exists(Path.Combine(dir, "PumpYaqobi.App")))
            dir = Path.GetDirectoryName(dir);
        return File.ReadAllText(Path.Combine(new[] { dir!, "PumpYaqobi.App" }.Concat(p).ToArray()));
    }

    /// <summary>
    /// ⛔ ‎Shift+عدد‎ پیش از حذف می‌پرسد اگر چیزی نوشته شده باشد، و همهٔ ‎n‎
    /// ردیف یک قدمِ برگشت‌اند.
    /// </summary>
    [Fact]
    public void ShiftAdad_Miporsad_Va_YekGhadam_Ast()
    {
        var s = Src("Services", "Shortcuts.cs");
        Assert.Contains("var filled = last.Count(RowData.HasData);", s);
        Assert.Contains("if (filled > 0)", s);
        Assert.Contains("Dialogs.ConfirmAsync(\"حذفِ ردیف\"", s);
        Assert.Contains("using (UndoHub.Group(", s);
    }

    /// <summary>
    /// ⛔ کلیکِ تک ویرایش را باز نمی‌کند — ریشهٔ «شیفت با عدد حذف نمی‌کند».
    /// کلیکِ دوم روی همان خانه کادرِ تایپ را باز می‌کرد و از آن لحظه
    /// ‎Shift+عدد‎ نویسه بود و ‎Delete‎ یک حرف پاک می‌کرد.
    /// </summary>
    [Fact]
    public void KelikeTak_Virayesh_Ra_Baz_Nemikonad()
    {
        var g = Src("Controls", "ExcelGrid.cs");
        Assert.Contains("if (e.EditingEventArgs is PointerPressedEventArgs { ClickCount: < 2 }", g);
        Assert.Contains("&& e.Column is not DataGridCheckBoxColumn)", g);
    }

    /// <summary>⛔ عوض شدنِ دفتر (حسابِ دیگر) تاریخچهٔ برگشت را پاک می‌کند.</summary>
    [Fact]
    public void DaftareDigar_Tarikhche_Ra_Pak_Mikonad()
    {
        var h = Src("Services", "AppHost.cs");
        var i = h.IndexOf("UndoHub.Clear();", StringComparison.Ordinal);
        var j = h.IndexOf("LedgerSwitched?.Invoke();", StringComparison.Ordinal);
        Assert.True(i > 0 && j > i);
    }

    /// <summary>
    /// ⛔ تکملهٔ خودکار کم‌رنگ است، نه هم‌رنگِ نوشتهٔ کاربر — و کلاسش با برخاستنِ
    /// تکمله برمی‌خیزد، وگرنه انتخابِ واقعیِ بعدی هم نامرئی می‌شد.
    /// </summary>
    [Fact]
    public void Takmele_KamRang_Ast()
    {
        var css = Src("Themes", "Controls.axaml");
        Assert.Contains("<Style Selector=\"TextBox.ghost\">", css);
        Assert.Contains("SelectionForegroundBrush\" Value=\"{DynamicResource Pump.Ghost}\"", css);
        Assert.Contains("Br(\"Ghost\", Mix(t.Card, t.Muted, 0.55));", Src("Themes", "ThemeManager.cs"));
        var sg = Src("Controls", "Suggest.cs");
        Assert.True(System.Text.RegularExpressions.Regex.Matches(sg, @"Ghostly\(false\);").Count >= 4);
        Assert.Contains("Ghostly(true);", sg);
    }

    /// <summary>
    /// ⛔ «زیان ناشی از افزایش قیمت»: کادر هست، محتوا نه — تا فرمولِ تازهٔ صاحب
    /// ریپو برسد. فرمولِ قبلی برنمی‌گردد.
    /// </summary>
    [Fact]
    public void ZiyaneAfzayeshGheymat_Kadr_Hast_Mohtava_Na()
    {
        var vm = string.Join("\n", Src("ViewModels", "Sections", "PriceLossSectionViewModel.cs")
                     .Split('\n').Where(l => !l.TrimStart().StartsWith("//")));   // نه توضیح‌ها
        Assert.Contains("base(\"priceloss\", \"debt\", \"زیان ناشی از افزایش قیمت\")", vm);
        Assert.DoesNotContain("PriceLossService", vm);
        Assert.DoesNotContain("_host.", vm);          // هیچ خواندنی از دیتابیس
        var view = Src("Views", "Sections", "PriceLossSectionView.axaml");
        Assert.DoesNotContain("DataGrid", view);
        Assert.Contains("این بخش فعلاً خالی است", view);
        Assert.Contains("debt.AddSub(new PriceLossSectionViewModel(host, Open)", Src("ViewModels", "MainViewModel.cs"));
    }
}
