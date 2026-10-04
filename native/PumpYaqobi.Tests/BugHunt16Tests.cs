using System.Text.Json;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Persistence;
using PumpYaqobi.Services.Data;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ بازبینیِ کاملِ ۱۴۰۵/۰۷/۱۶ — «تک‌تکِ کدها را بگرد تا همهٔ باگ‌ها رفع شوند» ══
///
/// هر بند یک باگِ واقعیِ پیداشده است که بسته شد. آن‌هایی که بی پنجره سنجیدنی‌اند
/// رفتاری سنجیده می‌شوند (SQLiteِ واقعی)، بقیه روی خودِ سورس — تا برنگردند.
/// </summary>
[Collection(OpLogCollection.Name)]
public class BugHunt16Tests : IDisposable
{
    private readonly string _file = Path.Combine(Path.GetTempPath(), $"pump-bh16-{Guid.NewGuid():N}.db");

    public void Dispose()
    {
        OpLog.Enabled = true;
        foreach (var f in new[] { _file, _file + "-wal", _file + "-shm" })
            try { if (File.Exists(f)) File.Delete(f); } catch { }
        GC.SuppressFinalize(this);
    }

    private static string Root()
    {
        var d = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (d is not null && !File.Exists(Path.Combine(d.FullName, "PumpYaqobi.sln")))
            d = d.Parent;
        Assert.NotNull(d);
        return d!.FullName;
    }

    private static string Read(params string[] parts) =>
        SrcText.Read(Path.Combine(new[] { Root() }.Concat(parts).ToArray()));

    //  ⚠️ شورا ج۵: پل‌های بیرونی به ‎PumpYaqobi.Shell‎ رفتند (همان فضای نام) — اول برنامه، بعد پوسته
    private static string App(params string[] parts) =>
        File.Exists(Path.Combine(new[] { Root(), "PumpYaqobi.App" }.Concat(parts).ToArray()))
            ? Read(new[] { "PumpYaqobi.App" }.Concat(parts).ToArray())
            : Read(new[] { "PumpYaqobi.Shell" }.Concat(parts).ToArray());

    // ══ همگام‌سازی ═════════════════════════════════════════════════════════

    /// <summary>
    /// ⛔ ذخیرهٔ کاربر هم‌زمان با ذخیرهٔ «بی‌صدا»ی همگام‌سازی هنوز op و شناسهٔ
    /// سراسری می‌گیرد. تا امروز «بی‌صدا» کلیدِ <b>سراسریِ</b> ‎OpLog.Enabled‎ را
    /// خاموش می‌کرد و تایپِ کاربر در همان لحظه هیچ‌وقت به سرور نمی‌رفت.
    /// </summary>
    [Fact]
    public void ZakhireyeKarbar_HamZaman_BaZakhireyeBiSeda_OpMigirad()
    {
        var dbf = new PumpDbFactory(_file); dbf.EnsureReady();
        var store = new SyncStore(dbf);
        var before = store.Pending();

        using var quiet = dbf.Create();
        quiet.SuppressOps = true;
        quiet.SafeEntries.Add(new SafeEntry { Title = "بی‌صدا", Amount = 1, DateKey = 14050701, MonthKey = "1405/07" });

        using (var user = dbf.Create())
        {
            user.SafeEntries.Add(new SafeEntry { Title = "کاربر", Amount = 2, DateKey = 14050701, MonthKey = "1405/07" });
            user.SaveChanges();                          // «هم‌زمان» — پیش از ذخیرهٔ بی‌صدا
        }
        quiet.SaveChanges();

        Assert.Equal(before + 1, store.Pending());       // فقط ذخیرهٔ کاربر op ساخت
        using var read = dbf.Create();
        Assert.All(read.SafeEntries.ToList(), e => Assert.False(string.IsNullOrEmpty(e.SyncUid)));
        Assert.False(OpLog.Enabled == false);            // کلیدِ سراسری دست نخورد
    }

    /// <summary>
    /// ⛔ opِ رسیده‌ای که ننشست (پدرش هنوز نرسیده) گزارش می‌شود تا نگه داشته شود —
    /// مکان‌نما از رویش رد می‌شود و تا امروز برای همیشه گم می‌شد. وقتی پدر آمد،
    /// همان op می‌نشیند.
    /// </summary>
    [Fact]
    public void OpeNaneshasteh_Gozaresh_Mishavad_Va_BaAmadanePedar_Mineshinad()
    {
        var dbf = new PumpDbFactory(_file); dbf.EnsureReady();
        var store = new SyncStore(dbf);

        using var child = JsonDocument.Parse("{\"MainOfDebtorId\":1,\"MainOfDebtorId@\":\"uid-pedar\",\"Name\":\"فرزند\"}");
        var first = store.ApplyIncoming(new[]
        {
            new IncomingOp("op-c", nameof(DebtAccount), "uid-acct", "insert", child.RootElement.Clone(), 5),
        });
        Assert.Equal(1, first.Failed);
        var kept = Assert.Single(first.FailedOps);
        Assert.Equal("op-c", kept.OpId);

        using var parent = JsonDocument.Parse("{\"Name\":\"پدر\"}");
        var second = store.ApplyIncoming(new[]
        {
            kept,
            new IncomingOp("op-p", nameof(Debtor), "uid-pedar", "insert", parent.RootElement.Clone(), 6),
        });
        Assert.Equal(0, second.Failed);
        Assert.Empty(second.FailedOps);
        using var read = dbf.Create();
        var acct = read.DebtAccounts.Single(x => x.SyncUid == "uid-acct");
        Assert.Equal(read.Debtors.Single(x => x.SyncUid == "uid-pedar").Id, acct.MainOfDebtorId);
    }

    /// <summary>
    /// ⛔ دفتری که از کامپیوترِ دیگر آمده، شناسهٔ همگام‌سازیِ آن‌جا را نمی‌برد؛
    /// بازگردانی روی همان کامپیوتر هیچ چیزی را عوض نمی‌کند.
    /// </summary>
    [Fact]
    public void DaftareKampiootereDigar_ShenaseyeHamgamRaNemiBarad()
    {
        var dbf = new PumpDbFactory(_file); dbf.EnsureReady();
        var store = new SyncStore(dbf);

        store.Update(x => { x.DeviceId = "pc-aaaa-1234567890"; x.Cursor = 42; });
        Assert.False(store.ForgetForeignDevice("pc-aaaa"));      // همان کامپیوتر
        Assert.Equal("pc-aaaa-1234567890", store.State().DeviceId);

        Assert.True(store.ForgetForeignDevice("pc-bbbb"));       // کامپیوترِ دیگر
        Assert.Equal("", store.State().DeviceId);
        Assert.Equal(42, store.State().Cursor);                  // ⚠️ مکان‌نما می‌ماند
    }

    [Fact]
    public void Hamgamsazi_YekDor_DarHarLahze_Va_DaftareJabeja_RaNeminevisad()
    {
        var s = App("Services", "SyncEngine.cs");
        Assert.Contains("private readonly SemaphoreSlim _stepGate = new(1, 1);", s);
        Assert.Contains("await _stepGate.WaitAsync(ct);", s);
        Assert.Contains("bool Moved() =>", s);
        //  پس از هر سه ‎await‎ِ شبکه، پیش از هر نوشتن
        Assert.Equal(3, System.Text.RegularExpressions.Regex.Matches(s, @"if \(Moved\(\)\) \{ _lastVersion = -1; Nudge\(\); return; \}").Count);
        Assert.Contains("_deferred.Concat(pull.Ops)", s);
        Assert.Contains("res.TooLarge && batch.Count == 1", s);
        //  ⛔ مهلتِ شبکه حلقه را نمی‌کشد
        Assert.Contains("catch (OperationCanceledException) when (ct.IsCancellationRequested) { return; }", s);
    }

    [Fact]
    public void HalgheHa_BaMohlatShabake_NemiMirand()
    {
        foreach (var f in new[] { "StationPublisher.cs", "HomeServer.cs", "SyncEngine.cs", "CameraFeed.cs" })
        {
            var s = App("Services", f);
            Assert.DoesNotContain("catch (OperationCanceledException) { return; }\n            catch", s.Replace("\r", ""));
        }
        var pub = App("Services", "StationPublisher.cs");
        Assert.Equal(0, System.Text.RegularExpressions.Regex.Matches(pub, @"catch \(OperationCanceledException\) \{ return; \}").Count);
        Assert.Contains("open.CancelAfter(OpenTimeout);", App("Services", "HomeSync.cs"));
    }

    [Fact]
    public void Bazgardani_RisheyeShenaseRa_BeErsMibarad_Va_AksAtomi_Ast()
    {
        var b = Read("PumpYaqobi.Services", "Data", "BackupService.cs");
        Assert.Contains("PumpDbFactory.RestoreSeedHint = liveSeed;", b);
        Assert.Contains("File.Move(part, target, overwrite: true);", b);
        Assert.DoesNotContain("if (File.Exists(target)) File.Delete(target);", b);
        Assert.Contains("RestoreSeedHint is { Length: > 10 } hint", Read("PumpYaqobi.Services", "Data", "PumpDbFactory.cs"));
        Assert.Contains("ForgetForeignSyncDevice();", App("ViewModels", "Sections", "BackupSectionViewModel.cs"));
    }

    // ══ کرش و دادهٔ کهنه ══════════════════════════════════════════════════

    [Fact]
    public void DasteJami_HesabRa_RooyeNakheRabet_BazMikonad()
    {
        var mv = App("ViewModels", "MainViewModel.cs");
        Assert.DoesNotContain(".ContinueWith(_ => debt.OpenPersonAsync(id))", mv);
        Assert.Contains("async Task Open(long id) { await GoAsync(debt); await debt.OpenPersonAsync(id); }", mv);
    }

    [Fact]
    public void Khorooj_AvvalNeveshtehaRaMinevisad()
    {
        var mv = App("ViewModels", "MainViewModel.cs");
        var i = mv.IndexOf("private async Task SignOut()", StringComparison.Ordinal);
        Assert.True(i > 0);
        var body = mv[i..mv.IndexOf("Phase = AppPhase.Locked;", i, StringComparison.Ordinal)];
        Assert.True(body.IndexOf("await FlushEverythingAsync();", StringComparison.Ordinal)
                    < body.IndexOf("Auth.SignOut();", StringComparison.Ordinal));
    }

    /// <summary>
    /// ⛔ بخش‌هایی که ردیفِ خودکار در «نخستین ردیفِ خالی»شان می‌نشیند، با برگشتن
    /// دوباره خوانده می‌شوند (با ترمزِ ‎Version‎) — وگرنه تایپ در همان ردیفِ
    /// «خالی»ِ کهنه، ردیفِ خودکار را پاک می‌کرد.
    /// </summary>
    [Fact]
    public void BakhshhayeRadifeKhodkar_BaBargashtan_TazeMishavand()
    {
        foreach (var f in new[] { "SafeSectionViewModel", "CompanySectionViewModel", "StorageSectionViewModel",
                                  "DebtReceiptSectionViewModel", "HistorySectionViewModel" })
        {
            var s = App("ViewModels", "Sections", f + ".cs");
            Assert.Contains("public override bool ActivationOnlyReadsDb => true;", s);
            Assert.Contains("public override", s[s.IndexOf("ActivationOnlyReadsDb => true;", StringComparison.Ordinal)..]);
        }
        //  و پیش از هر خواندنِ دوباره، نوشته‌های در صف
        Assert.Contains("await r.FlushAsync();", App("ViewModels", "LedgerSectionViewModel.cs"));
        Assert.Contains("await r.FlushAsync();", App("ViewModels", "Sections", "ParchaReceiptSectionViewModel.cs"));
        var att = App("ViewModels", "Sections", "AttendanceSectionViewModel.cs");
        Assert.True(System.Text.RegularExpressions.Regex.Matches(att, "await FlushRowsAsync\\(\\);").Count >= 5);
    }

    [Fact]
    public void RadifeTaze_JayeDorost_Va_TileDorost()
    {
        //  حسابِ قرض‌دار رفتاری است (PersonAccountBehaviourTests، شورا ت۳)؛ این‌جا فقط بقیهٔ بخش‌ها و ممنوعه
        var p = App("ViewModels", "Sections", "PersonViewModel.cs");
        foreach (var f in new[] { "AmanatSectionViewModel", "CompanySectionViewModel", "WaraqSectionViewModel" })
            Assert.Contains(".Max(", App("ViewModels", "Sections", f + ".cs"));
        Assert.DoesNotContain("SortIndex = Entity.ActiveRows().Count,", p);
    }

    //  «حذف می‌پرسد و تکراری از همه سنجیده می‌شود» ⇒ رفتاری: DebtorListBehaviourTests (شورا، ت۳)

    [Fact]
    public void TarikheParcha_BaHarKelid_GozaresheTaze_NemiSazad()
    {
        Assert.Contains("PaDate, Mode=TwoWay, UpdateSourceTrigger=LostFocus", App("Views", "Sections", "ParchaSectionView.axaml"));
        Assert.Contains("Shamsi.Key(newDate) == 0", App("ViewModels", "Sections", "ParchaSectionViewModel.cs"));
    }

    [Fact]
    public void Faktor_PasAzTayid_SafheyeTaze_Va_ParametreMotmaen()
    {
        var vm = App("ViewModels", "Sections", "InvoiceSectionViewModel.cs");
        Assert.Contains("RepickDetail(row);", vm);
        var v = App("Views", "Sections", "InvoiceSectionView.axaml");
        Assert.Equal(3, System.Text.RegularExpressions.Regex.Matches(v, "CommandParameter=\"\\{Binding \\$parent\\[UserControl\\]\\.DataContext\\.Detail\\}\"").Count);
    }

    [Fact]
    public void Dashboard_LinkeDiesel_Va_JameGharzdarBaKelideTil()
    {
        var mv = App("ViewModels", "MainViewModel.cs");
        Assert.Contains("id.EndsWith(\"-diesel\", StringComparison.Ordinal)", mv);
        var d = App("ViewModels", "Sections", "DashboardSectionViewModel.cs");
        Assert.Contains("_debtByFuel", d);
    }

    // ══ نما ════════════════════════════════════════════════════════════════

    /// <summary>
    /// ⛔ ‎DataContext‎ و ‎IsVisible‎ روی یک عنصر: ‎IsVisible‎ از ‎DataContext‎ِ
    /// <b>خودِ</b> همان عنصر می‌خواند. کادرِ «📝 یادداشت این بخش» در ده بخش هرگز
    /// دیده نمی‌شد و صفحهٔ گزارشِ پارچه زیرِ فهرست همیشه پیدا بود.
    /// </summary>
    [Fact]
    public void DataContextVaIsVisible_RooyeYekOnsor_TaleNist()
    {
        var c = App("Themes", "Controls.axaml");
        Assert.DoesNotContain("IsVisible=\"{Binding Notes, Converter", c);
        Assert.Contains("RowDeleteCommand=\"{Binding DeleteRowCommand}\"", App("Views", "Sections", "AmanatSectionView.axaml"));
        Assert.DoesNotContain("IsVisible=\"{Binding ShowReportDetail}\" DataContext=", App("Views", "Sections", "ParchaSectionView.axaml"));
    }

    [Fact]
    public void AnimationeBiPayan_FaghatRooyeFehresteDideShode()
    {
        Assert.Contains("Classes.alarm=\"{Binding AlarmLive}\"", App("Views", "Sections", "DebtSectionView.axaml"));
        Assert.Contains("public bool CardsLive => IsShown && IsListVisible;", App("ViewModels", "Sections", "DebtSectionViewModel.cs"));
    }

    [Fact]
    public void JadvalhaVaKadrhayeTanbal_ShenavandeRaRahaMikonand()
    {
        var g = App("Controls", "ExcelGrid.cs");
        Assert.Contains("_outsideTop?.RemoveHandler(PointerPressedEvent, OnOutsidePressed);", g);
        var scroll = g[g.IndexOf("private void OnPageScroll", StringComparison.Ordinal)..];
        Assert.True(scroll.IndexOf("if (!IsEffectivelyVisible) return;", StringComparison.Ordinal)
                    < scroll.IndexOf("SyncSticky()", StringComparison.Ordinal));
        var lazy = App("Controls", "LazyBox.cs");
        Assert.Contains("p.ScrollChanged -= OnPageScroll", lazy);
        Assert.Contains("if (!IsEffectivelyVisible) return;", lazy);
        Assert.Contains("Services.DisplayClock.Changed -= onDisplay", App("Views", "MainWindow.axaml.cs"));
    }

    [Fact]
    public void ZakhireyeChapVaQr_BiEstesnayeBiSahab()
    {
        var p = App("Printing", "DocumentPreview.cs");
        Assert.Contains("catch (IOException) when (n < 20)", p);
        var w = App("Views", "DocumentPreviewWindow.axaml.cs");
        //  ۱۴۰۵/۰۷/۱۶: ساختنِ فایل روی نخِ دیگر رفت — هر دو جا هنوز پشتِ ‎try‎
        Assert.Equal(2, System.Text.RegularExpressions.Regex.Matches(w, "try \\{ path = await Task.Run\\(\\(\\) => vm.SaveTo\\(PrintService.DocsFolder\\)\\); \\}").Count);
        Assert.DoesNotContain("path = Vm.SaveTo(", w);
        Assert.Contains("❌ کیو‌آر ذخیره نشد", App("Views", "QrWindow.axaml.cs"));
        Assert.DoesNotContain("BoxShadow=\"0 2 12 0", App("Views", "DocumentPreviewWindow.axaml"));
    }

    [Fact]
    public void KharidhayeSherkat_Panjare_Darand()
    {
        Assert.Contains("_allItems.Take(Page)", App("ViewModels", "Sections", "CompanyPagesViewModel.cs"));
        Assert.Contains("Command=\"{Binding ShowOlderCommand}\"", App("Views", "Sections", "CompanyPurchasesView.axaml"));
    }
}
