using System.Text.RegularExpressions;

namespace PumpYaqobi.Tests;

/// <summary>
/// ══ نصاب ═══════════════════════════════════════════════════════════════════
/// نصاب فقط روی ویندوز و با Inno Setup ساخته می‌شود، پس این‌جا خودش اجرا
/// نمی‌شود. ولی چیزهایی که <b>بی‌صدا</b> خراب می‌شوند این‌جا گرفته می‌شوند:
/// مسیری که به فایلِ نبوده اشاره کند، آیکونی که جا مانده باشد، یا — بدترین —
/// خطی که روزی کسی اضافه کند و پوشهٔ حساب‌های کاربر را پاک کند.
///
/// گلایه‌ای که این‌ها را ساخت: «برنامه روی دسکتاپ نیامد و نصب هم نمی‌شد».
/// انتشار تا آن روز فقط یک زیپ بود.
/// </summary>
public class InstallerTests
{
    private static readonly string Native =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    private static readonly string Repo = Path.GetFullPath(Path.Combine(Native, ".."));

    private static string Iss() =>
        File.ReadAllText(Path.Combine(Native, "installer", "PumpYaqobi.iss"));

    private static string Workflow() =>
        File.ReadAllText(Path.Combine(Repo, ".github", "workflows", "build-native.yml"));

    [Fact]
    public void Installer_script_exists()
        => Assert.True(File.Exists(Path.Combine(Native, "installer", "PumpYaqobi.iss")));

    /// <summary>
    /// آیکون باید واقعاً باشد و چند اندازه داشته باشد — وگرنه فایلِ اجرایی
    /// آیکونِ خالیِ پیش‌فرضِ ویندوز می‌گیرد و میان‌برِ دسکتاپ بی‌ریخت می‌شود.
    /// </summary>
    [Fact]
    public void App_icon_exists_and_has_several_sizes()
    {
        var ico = Path.Combine(Native, "PumpYaqobi.App", "Assets", "app.ico");
        Assert.True(File.Exists(ico), "app.ico نیست");

        // سرآیندِ ICO: 2 بایت صفر، 2 بایت نوع (=1)، 2 بایت شمارِ تصویرها
        using var fs = File.OpenRead(ico);
        var head = new byte[6];
        Assert.Equal(6, fs.Read(head, 0, 6));
        Assert.Equal(0, head[0] + head[1]);
        Assert.Equal(1, head[2] + head[3] * 256);
        var count = head[4] + head[5] * 256;
        Assert.True(count >= 5, $"آیکون فقط {count} اندازه دارد");
    }

    [Fact]
    public void Csproj_points_at_the_icon()
    {
        var proj = File.ReadAllText(Path.Combine(Native, "PumpYaqobi.App", "PumpYaqobi.App.csproj"));
        Assert.Contains("<ApplicationIcon>", proj);
        Assert.Contains("app.ico", proj);
    }

    /// <summary>هر مسیری که نصاب به آن اشاره می‌کند باید واقعاً وجود داشته باشد.</summary>
    [Fact]
    public void Paths_referenced_by_the_script_resolve()
    {
        var s = Iss();
        var dir = Path.Combine(Native, "installer");

        var icon = Regex.Match(s, @"SetupIconFile=(.+)").Groups[1].Value.Trim();
        Assert.True(File.Exists(Path.GetFullPath(Path.Combine(dir, icon.Replace('\\', Path.DirectorySeparatorChar)))),
                    $"SetupIconFile پیدا نشد: {icon}");
    }

    /// <summary>آیکونِ دسکتاپ — همان چیزی که نبودش گلایه شد.</summary>
    [Fact]
    public void A_desktop_shortcut_is_created_and_ticked_by_default()
    {
        var s = Iss();
        Assert.Contains("{autodesktop}", s);
        Assert.Contains("Tasks: desktopicon", s);
        // بدونِ ‎unchecked‎ یعنی پیش‌فرض تیک‌خورده است
        var task = Regex.Match(s, @"Name: ""desktopicon"".*").Value;
        Assert.DoesNotContain("unchecked", task);
    }

    [Fact]
    public void A_start_menu_entry_and_uninstaller_exist()
    {
        var s = Iss();
        Assert.Contains("{group}\\", s);
        Assert.Contains("{uninstallexe}", s);
        Assert.Contains("UninstallDisplayName", s);
    }

    /// <summary>
    /// نصب باید بی اجازهٔ مدیر باشد و در ‎%LocalAppData%‎ بنشیند — وگرنه
    /// به‌روزرسانیِ خودکارِ برنامه (که فایل‌ها را جابه‌جا می‌کند) هر بار
    /// اجازهٔ مدیر می‌خواهد و عملاً از کار می‌افتد.
    /// </summary>
    [Fact]
    public void Installs_per_user_so_self_update_keeps_working()
    {
        var s = Iss();
        Assert.Contains("PrivilegesRequired=lowest", s);
        Assert.Contains("{localappdata}\\Programs\\PumpYaqobi", s);
    }

    /// <summary>
    /// ⚠️ مهم‌ترین آزمونِ این فایل. حساب‌های کاربر در ‎%AppData%\PumpYaqobi‎
    /// است. اگر روزی کسی ‎[UninstallDelete]‎ اضافه کند، یک «حذفِ برنامه»
    /// کلِ دفترِ حساب‌ها را می‌برد.
    /// </summary>
    [Fact]
    public void Uninstalling_never_touches_the_users_data()
    {
        var s = Iss();
        var live = string.Join("\n", s.Split('\n').Where(l => !l.TrimStart().StartsWith(";")));
        Assert.DoesNotContain("[UninstallDelete]", live);
        Assert.DoesNotContain("{userappdata}", live);
    }

    /// <summary>برنامهٔ باز باید هنگامِ به‌روزرسانی خودش بسته و باز شود.</summary>
    [Fact]
    public void Setup_closes_and_restarts_the_running_app()
    {
        var s = Iss();
        Assert.Contains("CloseApplications=yes", s);
        Assert.Contains("RestartApplications=yes", s);
    }

    // ══ ورک‌فلو ══
    [Fact]
    public void Workflow_builds_and_publishes_the_installer()
    {
        var w = Workflow();
        Assert.Contains("PumpYaqobi.iss", w);
        Assert.Contains("rel/PumpYaqobi-Setup.exe", w);
    }

    /// <summary>
    /// نصاب باید <b>پس از</b> مرحلهٔ بسته‌ها ساخته شود: آن مرحله ‎base.id‎ را
    /// کنارِ برنامه می‌گذارد و نصاب باید همان را هم ببرد. وگرنه نصبِ کاربر
    /// «پایه»اش را نمی‌شناسد و هر به‌روزرسانی به‌جای چند مگابایت، کلِ بسته را
    /// می‌گیرد — یعنی همان چیزی که بستهٔ کوچک برای جلوگیری‌اش ساخته شد.
    /// </summary>
    [Fact]
    public void Installer_is_built_after_the_base_id_is_written()
    {
        var w = Workflow();
        var packages = w.IndexOf("name: بسته‌ها", StringComparison.Ordinal);
        var installer = w.IndexOf("name: ساختِ نصاب", StringComparison.Ordinal);
        Assert.True(packages > 0 && installer > 0, "مرحله‌ها پیدا نشدند");
        Assert.True(packages < installer,
            "نصاب پیش از نوشتنِ base.id ساخته می‌شود — بستهٔ کوچک از کار می‌افتد");
    }

    /// <summary>به‌روزرسانی نباید ویزارد را جلوی کاربر باز کند.</summary>
    [Fact]
    public void Auto_update_runs_the_installer_silently()
    {
        var svc = File.ReadAllText(Path.Combine(Native, "PumpYaqobi.App", "Update", "UpdateService.cs"));
        Assert.Contains("/SILENT", svc);
    }
}
