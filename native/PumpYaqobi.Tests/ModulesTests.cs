using PumpYaqobi.App.Services;
using PumpYaqobi.App.ViewModels;
using PumpYaqobi.App.ViewModels.Sections;
using PumpYaqobi.Domain.Entities;
using PumpYaqobi.Domain.Enums;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ شورا، ج۷ — حاشیه جداشدنی ══════════════════════════════════════════════════
///
/// ⛔ با شکستنِ عمدیِ <b>هر</b> ماژولِ حاشیه، برنامه وارد می‌شود و پارچه و ورق کار
/// می‌کنند؛ فقط همان ماژول خاموش می‌ماند و پروفایل می‌گوید. و قطعیِ شبکه هیچ
/// ماژولی را خاموش نمی‌کند.
/// </summary>
[Collection(AppHostCollection.Name)]
public class ModulesTests : IDisposable
{
    public ModulesTests() => Modules.ResetForTests();
    public void Dispose() { Modules.ResetForTests(); GC.SuppressFinalize(this); }

    [Fact]
    public async Task KhatayeShabake_MajulRa_KhamushNemikonad_AmmaBag_Mikonad()
    {
        await Modules.Run("liveconfig", () => throw new HttpRequestException("بی‌اینترنت"), default);
        await Modules.Run("liveconfig", () => throw new TaskCanceledException("تایم‌اوت"), default);
        Assert.True(Modules.Alive("liveconfig"));

        await Modules.Run("liveconfig", () => throw new NullReferenceException(), default);
        Assert.False(Modules.Alive("liveconfig"));
        Assert.True(Modules.Alive("alerts"));                   //  فقط خودش

        var ran = false;
        await Modules.Run("liveconfig", () => { ran = true; return Task.CompletedTask; }, default);
        Assert.False(ran);                                       //  خاموش یعنی دیگر هیچ کاری
        Assert.Contains("تنظیماتِ زنده", Modules.FailedLine());
    }

    [Fact]
    public void NameNashenakhte_Khata_Midahad_NaSokoot() =>
        Assert.Throws<ArgumentException>(() => Modules.Alive("raet"));

    /// <summary>
    /// ⛔ هر ماژول عمداً می‌شکند ⇒ ورود انجام می‌شود، بقیهٔ ماژول‌ها و دفتر
    /// سرِ جایشان، پارچه و ورق ذخیره می‌شوند، و پروفایل همه را می‌گوید.
    /// </summary>
    [Fact]
    public async Task HarMajulBeshkanad_BarnameVaredMishavad_VaParchaVaVaraq_KarMikonand()
    {
        var host = AppHost.Start(Path.Combine(Path.GetTempPath(), "pump-shared-" + Guid.NewGuid().ToString("N"), "pump.db"));
        if (host.Auth.NeedsFirstRun()) host.Auth.CreateFirstAdmin("1234");
        if (!host.Auth.HasPassword()) host.Auth.SetFirstPassword("1234");

        Modules.BreakHook = id => new InvalidOperationException("شکستِ عمدیِ " + id);
        var vm = new MainViewModel();
        vm.Lock.Password = "1234";
        await vm.Lock.SubmitCommand.ExecuteAsync(null);

        Assert.Equal(MainViewModel.AppPhase.Ready, vm.Phase);
        Assert.True(host.Session.IsSignedIn);
        foreach (var id in new[] { "publisher", "autoupdate", "backup-push" })
            Assert.False(Modules.Alive(id), id + " باید خاموش مانده باشد");

        //  و ماژول‌های دوره‌ای هم، هر کدام سرِ دورِ خودش
        foreach (var id in new[] { "alerts", "rate", "liveconfig" })
            await Modules.Run(id, () => Task.CompletedTask, default);
        Assert.Equal(Modules.All.Count, Modules.Failed.Count);
        Modules.BreakHook = null;

        //  دفتر بند به هیچ‌کدام نیست
        var report = await host.ParchaData.AddAsync(FuelType.Petrol, "1405/07/20");
        Assert.True(report.Id > 0);
        var w = await host.WaraqData.OpenOrCreateAsync("1405/07/20", null);
        var t = new WaraqTransaction { ShiftId = w.Shifts.First().Id, SortIndex = 99, Name = "کریم" };
        await host.WaraqData.SaveTxnAsync(t);
        Assert.Contains((await host.WaraqData.LoadAsync(w.Id))!.Shifts.SelectMany(s => s.Transactions), x => x.Id == t.Id);

        //  و پروفایل می‌گوید
        var acct = (AccountSectionViewModel)vm.Sections.Single(s => s.Id == "account");
        await acct.OnActivatedAsync();
        foreach (var m in Modules.All) Assert.Contains(m.Title, acct.ModulesLine);
    }
}
