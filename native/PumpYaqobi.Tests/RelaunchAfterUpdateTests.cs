using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ «موقعِ آپدیت برنامه بسته می‌شود و دوباره باز نمی‌شود» — ۱۴۰۵/۰۷/۱۸ ═══════
/// نصابِ بی‌صدا با ‎/RELAUNCH=1‎ برنامه را دوباره باز می‌کند (با کاربرِ خودش)، و
/// برنامهٔ تازه با ‎--after-update‎ منتظرِ بسته شدنِ نمونهٔ کهنه می‌ماند.
/// رفتارِ واقعیِ نصاب: بندِ «۴د۲» در ‎installer-check.yml‎ (ویندوزِ واقعی).
/// </summary>
public class RelaunchAfterUpdateTests
{
    private static string Src(string rel) => SrcText.Read(Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "../../../../", rel)));

    [Fact]
    public void Nasab_BaRelaunch_BarnameRaDobareBazMikonad()
    {
        var iss = Src("installer/PumpYaqobi.iss");
        Assert.Contains("Parameters: \"--after-update\"; Flags: nowait skipifnotsilent runasoriginaluser; Check: WantRelaunch", iss);
        Assert.Contains("Result := ExpandConstant('{param:RELAUNCH|0}') = '1';", iss);
        //  ⛔ و نسخه‌های پیش از ۳.۱.۲۳۱ که ‎/RELAUNCH‎ نمی‌فرستادند (۱۴۰۵/۰۷/۲۰)
        Assert.Contains("Result := WizardSilent and HasSwitch('/RESTARTAPPLICATIONS');", iss);
        Assert.Contains("if CompareText(ParamStr(I), S) = 0 then Result := True;", iss);
        //  خطِ ویزارد همان ماند — نصبِ دستی همان «اجرای برنامه» را دارد
        Assert.Contains("Flags: nowait postinstall skipifsilent", iss);
    }

    [Fact]
    public void BarnameyeKhodash_Relaunch_MiKhahad()
    {
        var svc = Src("PumpYaqobi.App/Update/UpdateService.cs");
        Assert.Contains("/SILENT /NORESTART /RESTARTAPPLICATIONS /RELAUNCH=1", svc);
        //  مسیرِ زیپ: برنامهٔ تازه با ‎--after-update‎ و پوشهٔ کارِ درست؛ بالابرده ⇒ explorer
        Assert.Contains("start \\\"\\\" /D \\\"{appDir}\\\" \\\"{exe}\\\" --after-update", svc);
        Assert.Contains("explorer.exe \\\"{exe}\\\"", svc);
        Assert.Contains("{relaunch}", svc);
        Assert.DoesNotContain("            start \"\" \"{exe}\"\n", svc.Replace("\r\n", "\n"));
        //  ‎tasklist‎ خراب هم اسکریپت را تا ابد نگه نمی‌دارد
        Assert.Contains("if %N% GEQ 120 goto go", svc);
    }

    [Fact]
    public void PasAzApdeit_ShruNemishavad_MontazerMimanad()
    {
        var prog = Src("PumpYaqobi.App/Program.cs");
        Assert.Contains("SingleInstance.Acquire(afterUpdate ? TimeSpan.FromSeconds(60) : TimeSpan.Zero)", prog);
        var si = Src("PumpYaqobi.App/Services/SingleInstance.cs");
        Assert.Contains("try { got = _mutex.WaitOne(wait); }", si);
    }

    [Fact]
    public void Ghofl_BaEntezar_PasAzBastaneKohne_Migirad()
    {
        //  همان رفتارِ ‎Mutex‎ که ‎Acquire(wait)‎ به آن تکیه دارد: نمونهٔ کهنه قفل را
        //  نگه داشته و وسطِ انتظار رها می‌کند ⇒ نمونهٔ تازه می‌گیرد، نه بیرون برود.
        var name = "Local\\pyq-test-" + Guid.NewGuid().ToString("N");
        using var held = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var old = new Thread(() =>
        {
            using var m = new Mutex(true, name, out _);
            held.Set();
            release.Wait();
            m.ReleaseMutex();
        });
        old.Start();
        held.Wait();
        using var mine = new Mutex(true, name, out var created);
        Assert.False(created);
        Assert.False(mine.WaitOne(0));                     // بی انتظار ⇒ بیرون می‌رفت
        _ = Task.Run(async () => { await Task.Delay(300); release.Set(); });
        Assert.True(mine.WaitOne(TimeSpan.FromSeconds(10)));   // با انتظار ⇒ می‌گیرد
        mine.ReleaseMutex();
        old.Join();
    }

    // ══ پرسشِ «پیش از بستن بکاپ؟» برنامه را باز نگه می‌داشت (۱۴۰۵/۰۷/۲۰) ══════
    [Fact]
    public void BastanBarayeNasb_PorsesheBackupRaRadMikonad_BaMohlat()
    {
        var t0 = new DateTime(2026, 1, 1, 10, 0, 0);
        Assert.False(PumpYaqobi.App.Update.UpdateExit.Armed(null, t0));
        Assert.True(PumpYaqobi.App.Update.UpdateExit.Armed(t0, t0));
        Assert.True(PumpYaqobi.App.Update.UpdateExit.Armed(t0, t0.AddMinutes(9)));
        Assert.False(PumpYaqobi.App.Update.UpdateExit.Armed(t0, t0.AddMinutes(11)));   // ویزارد لغو شد ⇒ دوباره می‌پرسد
        Assert.False(PumpYaqobi.App.Update.UpdateExit.Armed(t0, t0.AddMinutes(-1)));

        PumpYaqobi.App.Update.UpdateExit.TestReset();
        Assert.False(PumpYaqobi.App.Update.UpdateExit.Active);
        PumpYaqobi.App.Update.UpdateExit.Arm();
        Assert.True(PumpYaqobi.App.Update.UpdateExit.Active);
        PumpYaqobi.App.Update.UpdateExit.TestReset();
    }

    [Fact]
    public void HarSeDarNasb_MohrMizanand_VaPorseshRadMishavad()
    {
        var life = Src("PumpYaqobi.App/ViewModels/MainViewModel.Lifecycle.cs");
        var offer = life[life.IndexOf("public async Task OfferBackupBeforeExitAsync()", StringComparison.Ordinal)..];
        offer = offer[..offer.IndexOf("ConfirmAsync", StringComparison.Ordinal)];
        Assert.Contains("if (Update.UpdateExit.Active) return;", offer);

        var auto = Src("PumpYaqobi.App/Update/AutoUpdate.cs");
        var a = auto[auto.IndexOf("InstallAndExitAsync", StringComparison.Ordinal)..];
        Assert.True(a.IndexOf("UpdateExit.Arm();", StringComparison.Ordinal) is > 0 and var i
                    && i < a.IndexOf("d.Shutdown();", StringComparison.Ordinal));

        var bk = Src("PumpYaqobi.App/ViewModels/Sections/BackupSectionViewModel.cs");
        var inst = bk[bk.IndexOf("private async Task InstallUpdateAsync()", StringComparison.Ordinal)..];
        Assert.True(inst.IndexOf("UpdateExit.Arm();", StringComparison.Ordinal) is > 0 and var j
                    && j < inst.IndexOf("d.Shutdown();", StringComparison.Ordinal));
        var off = bk[bk.IndexOf("UpdateService.LaunchOffline(path)", StringComparison.Ordinal)..];
        Assert.Contains("UpdateExit.Arm();", off[..off.IndexOf("نصاب باز شد", StringComparison.Ordinal)]);
    }
}
