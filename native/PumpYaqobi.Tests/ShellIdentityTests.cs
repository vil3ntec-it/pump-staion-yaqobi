using System.Reflection;
using PumpYaqobi.App.Services;
using PumpYaqobi.App.Update;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ شورا، ج۵ (گامِ ۱) — پوسته جدا، هویت همان ═══════════════════════════════════
///
/// پل‌های بیرونِ برنامه (CloudLink، SyncEngine، StationPublisher، BackupPusher،
/// LicenseGuard، TimeSync و میزبان) در <c>PumpYaqobi.Shell</c>اند، بی هیچ ارجاعی به
/// آوالونیا. ⛔ و سه چیز که با جدایی <b>بی‌صدا</b> می‌شکستند، این‌جا قفل‌اند.
/// </summary>
public class ShellIdentityTests
{
    private static Assembly Shell => typeof(CloudLink).Assembly;
    private static Assembly App => typeof(PumpYaqobi.App.ViewModels.MainViewModel).Assembly;

    [Fact]
    public void Pooste_JodaAst_VaAvaloniaNemishenasad()
    {
        Assert.Equal("PumpYaqobi.Shell", Shell.GetName().Name);
        foreach (var t in new[] { typeof(CloudLink), typeof(SyncEngine), typeof(StationPublisher), typeof(BackupPusher),
                                  typeof(LicenseGuard), typeof(TimeSync), typeof(AppHost) })
            Assert.Same(Shell, t.Assembly);
        Assert.DoesNotContain(Shell.GetReferencedAssemblies(), a => a.Name!.StartsWith("Avalonia", StringComparison.Ordinal));
        Assert.DoesNotContain(Shell.GetReferencedAssemblies(), a => a.Name == "PumpYaqobi");   //  پوسته برنامه را نمی‌شناسد
    }

    /// <summary>
    /// ⛔ نسخه و ریشه‌های اعتماد از اسمبلیِ <b>برنامه</b> — نه پوسته. بی این، کلیدهای
    /// امضای مجوز و به‌روزرسانی بی‌صدا خالی خوانده می‌شدند.
    /// </summary>
    [Fact]
    public void NoskheVaKelidha_AzBarname_NaPooste()
    {
        Assert.Same(App, AppIdentity.Assembly);
        var v = App.GetName().Version!;
        Assert.Equal($"{v.Major}.{v.Minor}.{v.Build}", AppVersion.Current);

        var keys = App.GetCustomAttributes<AssemblyMetadataAttribute>().Select(m => m.Key).ToHashSet();
        foreach (var k in new[] { "LicenseKeys", "UpdateKey", "IntegrityKey", "OfflineKeys" })
            Assert.Contains(k, keys);
        Assert.DoesNotContain(Shell.GetCustomAttributes<AssemblyMetadataAttribute>(), m => m.Key == "LicenseKeys");

        //  و همان دری که مجوز و به‌روزرسانی از آن می‌خوانند (‎CloudConfig.Metadata‎) واقعاً به برنامه می‌رسد
        var read = typeof(CloudConfig).GetMethod("Metadata", BindingFlags.Static | BindingFlags.NonPublic)!;
        Assert.Equal("PumpYaqobi", read.Invoke(null, new object[] { "AppIdentityProbe" }));
    }

    /// <summary>⛔ نخِ رابط و میزبان‌های ساعت از سیم‌کشیِ برنامه — همان رفتارِ پیش از جدایی.</summary>
    [Fact]
    public void SimKeshiyeBarname_VaslAst()
    {
        ShellBoot.Ensure();
        var hosts = TimeSync.UpdateTimeHosts().ToList();
        Assert.Contains("github.com", hosts);
        Assert.StartsWith("https://", TimeSync.UpdateProbeUrl());
        Assert.Equal(Avalonia.Threading.Dispatcher.UIThread.CheckAccess(), UiThread.CheckAccess());
    }
}
