using PumpYaqobi.App.Services;
using Xunit;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ یک فهرستِ هشدار — برای زنگ، توستِ میرزا، سرور و بات ═══════════════════════
///
/// <para>
/// گزارشِ صاحب ریپو (۱۴۰۵/۰۷/۱۴، با عکسِ زنگِ «۲»ِ داشبورد): «دو سه تا هشدار
/// استن اما سرور و برنامه بی‌توجهی می‌کنن… برای قرض‌داری که حسابش تموم بشه
/// چرا پیامِ هشدار به میرزا نمیاد؟»
/// </para>
/// <para>
/// ریشه دو قاعده برای یک چیز بود: زنگ مخزن را با قاعدهٔ خودش می‌شمرد و
/// قرض‌داران را اصلاً، و بات از <see cref="StationSnapshot.Alerts"/> می‌خواند.
/// این‌جا قفل می‌شود که فقط <see cref="AlertWatch"/> فهرست می‌سازد.
/// </para>
/// </summary>
public class AlertWatchTests
{
    private static Dictionary<string, object?> Person(long id, string name, string p = "ok")
        => new() { ["id"] = id, ["name"] = name, ["stP"] = p, ["stD"] = "none", ["stM"] = "none" };

    private static IReadOnlyList<AlertItem> From(params Dictionary<string, object?>[] people)
        => AlertWatch.FromSnapshot(StationSnapshot.Alerts(people.Cast<object?>().ToList(), null));

    [Fact]
    public void Fehrest_Az_HamanStationSnapshotAlerts_Ast()
    {
        var list = From(Person(7, "کریم", "out"), Person(8, "نبی", "low"), Person(9, "سالم"));
        Assert.Equal(2, list.Count);
        var karim = list.Single(a => a.Name == "کریم");
        Assert.True(karim.IsOut);
        Assert.False(karim.IsTank);
        Assert.Equal("debt", karim.Section);
        Assert.Equal("d7-stP-out", karim.Key);
        Assert.Contains("اضافه نده", karim.Text);
    }

    [Fact]
    public void Makhzan_BakhsheKhodash_RaBazMikonad()
    {
        var tank = new Dictionary<string, object?>
        {
            ["diesel"] = new Dictionary<string, object?> { ["low"] = true, ["show"] = 0 },
        };
        var list = AlertWatch.FromSnapshot(StationSnapshot.Alerts(null, tank));
        var a = Assert.Single(list);
        Assert.True(a.IsTank);
        Assert.Equal("storage-diesel", a.Section);
    }

    /// <summary>
    /// ⛔ «حرف‌های تکراری»: همان فهرست دوباره ⇒ هیچ خبرِ تازه‌ای؛ فقط کلیدِ
    /// نو «باز شد» شمرده می‌شود، و نخستین محاسبه «آغاز» است.
    /// </summary>
    [Fact]
    public void FaghatHoshdareTaze_Khabar_Midahad()
    {
        var w = new AlertWatch();
        var events = new List<(int Opened, bool Initial)>();
        w.Changed += (o, i) => events.Add((o.Count, i));

        var first = From(Person(7, "کریم", "out"));
        Assert.Single(w.Accept(first));
        Assert.Equal((1, true), events[^1]);

        //  همان فهرست ⇒ نه خبری، نه «تازه»‌ای
        Assert.Empty(w.Accept(From(Person(7, "کریم", "out"))));
        Assert.Single(events);

        //  نبی تازه آمد ⇒ فقط نبی
        var opened = w.Accept(From(Person(7, "کریم", "out"), Person(8, "نبی", "low")));
        Assert.Equal("نبی", Assert.Single(opened).Name);
        Assert.Equal((1, false), events[^1]);

        //  کریم برطرف شد ⇒ فهرست عوض شد، ولی چیزِ «تازه»‌ای نیست (توستی نمی‌زند)
        Assert.Empty(w.Accept(From(Person(8, "نبی", "low"))));
        Assert.Equal((0, false), events[^1]);
        Assert.Single(w.Current);
    }

    [Fact]
    public void Reset_DaftareDigar_RaAzNoMigirad()
    {
        var w = new AlertWatch();
        w.Accept(From(Person(7, "کریم", "out")));
        w.Reset();
        Assert.Empty(w.Current);
        Assert.False(w.Ready);
        var initial = false;
        w.Changed += (_, i) => initial = i;
        w.Accept(From(Person(7, "کریم", "out")));
        Assert.True(initial);
    }

    [Fact]
    public void TosteMirza_YekPayam_BarayeChandHoshdar()
    {
        var many = From(Person(1, "الف", "out"), Person(2, "ب", "out"), Person(3, "ج", "out"), Person(4, "د", "out"));
        var t = AlertWatch.ToastText(many, initial: false);
        Assert.StartsWith("🔔 هشدارِ تازه:", t);
        Assert.Contains("و 1 هشدارِ دیگر", t);
        Assert.StartsWith("🔔 4 هشدارِ باز", AlertWatch.ToastText(many, initial: true));
        Assert.Equal("", AlertWatch.ToastText(Array.Empty<AlertItem>(), false));
    }

    /// <summary>
    /// ⛔ «یک خطِ طولانی که نه خوانده می‌شود نه جدا جداست» (۱۴۰۵/۰۷/۱۴): هر
    /// هشدار خطِ خودش را دارد و هیچ دو هشداری با «·» کنارِ هم نمی‌نشینند.
    /// </summary>
    [Fact]
    public void TosteMirza_HarHoshdar_KhateKhodash()
    {
        var three = From(Person(1, "الف", "out"), Person(2, "ب", "out"), Person(3, "ج", "out"));
        var lines = AlertWatch.ToastText(three, initial: true).Split('\n');
        Assert.Equal(4, lines.Length);                 // سر + سه هشدار
        Assert.All(lines.Skip(1), l => Assert.StartsWith("• ", l));
        Assert.DoesNotContain(" · ", AlertWatch.ToastText(three, initial: false));
    }

    /// <summary>زنگِ داشبورد فهرست باز می‌کند، نه توستِ یک‌خطی.</summary>
    [Fact]
    public void ZangeDashboard_Fehrest_Ast_NaToast()
    {
        var vm = Src("PumpYaqobi.App", "ViewModels", "Sections", "DashboardSectionViewModel.cs");
        Assert.DoesNotContain("string.Join(\" · \", real", vm);
        Assert.Contains("AlertsOpen = true", vm);
        var x = Src("PumpYaqobi.App", "Views", "Sections", "DashboardSectionView.axaml");
        Assert.Contains("x:Name=\"AlertsPopup\"", x);
        Assert.Contains("{Binding Action", x);
    }

    // ── سیم‌کشی — روی خودِ سورس ────────────────────────────────────────

    private static readonly string Root =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    private static string Src(params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { Root }.Concat(parts).ToArray()));

    /// <summary>
    /// ⛔ زنگِ داشبورد قاعدهٔ جدا ندارد: از <c>LiveAlerts</c> می‌خواند، و
    /// «موجودیِ خام زیرِ حد» (قاعدهٔ کهنه‌ای که مخزنِ بی‌خرید را «تمام شده»
    /// می‌خواند) برنگشته است.
    /// </summary>
    [Fact]
    public void Zang_AzHamanFehrest_MikhAnad()
    {
        var dash = Src("PumpYaqobi.App", "ViewModels", "Sections", "DashboardSectionViewModel.cs");
        var build = dash[dash.IndexOf("private void BuildAlerts()", StringComparison.Ordinal)..];
        build = build[..build.IndexOf("private void BuildRecent()", StringComparison.Ordinal)];
        Assert.Contains("_host.LiveAlerts.Current", build);
        Assert.DoesNotContain("_threshold", build);
        Assert.DoesNotContain("t.Raw", build);
    }

    /// <summary>
    /// ⛔ «پیامِ هشدار به میرزا»: فهرستِ تازه ⇒ توست، و زنگ از همان‌جا تازه می‌شود.
    /// و عوض شدنِ دفتر فهرستِ قبلی را پاک می‌کند.
    /// </summary>
    [Fact]
    public void Mirza_Tost_Migirad_Va_DaftareDigar_Pak_Mishavad()
    {
        var main = Src("PumpYaqobi.App", "ViewModels", "MainViewModel.cs");
        Assert.Contains("AppHost.Current.LiveAlerts.Changed +=", main);
        Assert.Contains("AlertWatch.ToastText(opened, initial)", main);
        Assert.Contains("d.ApplyAlerts()", main);
        var sw = main[main.IndexOf("public async Task OnLedgerSwitchedAsync()", StringComparison.Ordinal)..];
        Assert.Contains("LiveAlerts.Reset()", sw[..400]);
    }

    /// <summary>⚡ قانونِ سرعت: ترمزِ ‎Version‎ و کمینهٔ فاصله، و مخزن بی خواندنِ همهٔ پارچه‌ها.</summary>
    [Fact]
    public void Tormoz_Version_Va_BiKhandaneHamePArcheha()
    {
        var w = Src("PumpYaqobi.App", "Services", "AlertWatch.cs");
        Assert.Contains("version == _lastVersion) return false;", w);
        Assert.Contains("DateTime.UtcNow - _lastRun < MinGap", w);
        var snap = Src("PumpYaqobi.App", "Services", "StationSnapshot.cs");
        var tank = snap[snap.IndexOf("internal static async Task<Dictionary<string, object?>> TankAsync", StringComparison.Ordinal)..];
        tank = tank[..tank.IndexOf("return tank;", StringComparison.Ordinal)];
        Assert.Contains("ShiftSumsAsync(fuel, ct)", tank);
        Assert.DoesNotContain("ReportsAsync(", tank);
    }
}
