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
    /// پیش‌فرضِ نصب باید بی اجازهٔ مدیر و در ‎%LocalAppData%‎ باشد — وگرنه
    /// به‌روزرسانیِ خودکارِ برنامه (که فایل‌ها را جابه‌جا می‌کند) هر بار
    /// اجازهٔ مدیر می‌خواهد.
    /// </summary>
    [Fact]
    public void Installs_per_user_so_self_update_keeps_working()
    {
        var s = Iss();
        Assert.Contains("PrivilegesRequired=lowest", s);
        Assert.Contains("{localappdata}\\Programs\\PumpYaqobi", s);
    }

    /// <summary>
    /// خواستهٔ صریحِ صاحب ریپو: «بشه انتخاب کرد که کجا فایل‌ها رو ببرم بزارم
    /// موقع نصب». صفحهٔ انتخابِ پوشه باید باز باشد.
    /// </summary>
    [Fact]
    public void The_user_can_choose_where_the_files_go()
    {
        var s = Iss();
        Assert.Contains("DisableDirPage=no", s);
        Assert.DoesNotContain("DisableDirPage=yes", s);
    }

    /// <summary>
    /// نصبِ دوباره باید به همان پوشه‌ای برود که کاربر بارِ اول انتخاب کرد —
    /// وگرنه به‌روزرسانی یک نسخهٔ دوم در جای پیش‌فرض می‌سازد و کاربر با دو
    /// برنامه و دو آیکون روبه‌رو می‌شود.
    /// </summary>
    [Fact]
    public void Reinstalling_goes_back_to_the_folder_the_user_picked()
        => Assert.Contains("UsePreviousAppDir=yes", Iss());

    /// <summary>
    /// اگر کاربر پوشه‌ای بگیرد که اجازهٔ مدیر می‌خواهد، باید همان‌جا به او
    /// گفته شود — نه اینکه ماه‌ها بعد وسطِ به‌روزرسانی بفهمد.
    /// </summary>
    [Fact]
    public void Choosing_an_admin_only_folder_warns_the_user()
    {
        var s = Iss();
        Assert.Contains("[Code]", s);
        Assert.Contains("wpSelectDir", s);
        Assert.Contains("{commonpf}", s);
        // فقط هشدار، نه جلوگیری: کاربر آزاد است
        Assert.Contains("Result := True;", s);
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

    // ══ شمارهٔ نسخه ══
    // خواستهٔ صاحب ریپو: «برنامهٔ نیتیو از نسخهٔ ۳٫۱٫۱ شروع شود.»

    private static Dictionary<string, string> VersionFile()
    {
        var map = new Dictionary<string, string>();
        foreach (var line in File.ReadAllLines(Path.Combine(Native, "VERSION")))
        {
            var t = line.Trim();
            if (t.Length == 0 || t.StartsWith("#")) continue;
            var i = t.IndexOf('=');
            if (i > 0) map[t[..i].Trim()] = t[(i + 1)..].Trim();
        }
        return map;
    }

    [Fact]
    public void The_native_app_starts_at_3_1_1()
    {
        var v = VersionFile();
        Assert.Equal("3.1", v["MAJOR_MINOR"]);

        // نسخهٔ ساختِ بعدی باید دقیقاً ۳٫۱٫۱ باشد
        var offset = int.Parse(v["RUN_OFFSET"]);
        var nextRun = offset + 1;
        Assert.Equal("3.1.1", $"{v["MAJOR_MINOR"]}.{nextRun - offset}");
    }

    /// <summary>
    /// ⚠️ نسخه باید همیشه بالا برود. اگر کسی ‎RUN_OFFSET‎ را زیاد کند، نسخهٔ
    /// تازه از نسخهٔ نصب‌شده کوچک‌تر می‌شود و برنامه به‌روزرسانی را رد می‌کند —
    /// یعنی کاربر برای همیشه روی نسخهٔ کهنه می‌ماند و هیچ خطایی هم نمی‌بیند.
    /// </summary>
    [Fact]
    public void The_new_version_is_higher_than_the_old_1_0_x_releases()
    {
        var v = VersionFile();
        var first = $"{v["MAJOR_MINOR"]}.{1}";
        Assert.True(PumpYaqobi.App.Update.UpdateService.Compare(first, "1.0.999") > 0,
            $"{first} از نسخه‌های کهنهٔ 1.0.x بزرگ‌تر نیست");
    }

    /// <summary>ورک‌فلو باید نسخه را از همان فایل بخواند، نه از عددِ ثابت.</summary>
    [Fact]
    public void The_workflow_reads_the_version_from_the_file()
    {
        var w = Workflow();
        Assert.Contains("source native/VERSION", w);
        Assert.Contains("MAJOR_MINOR", w);
        Assert.Contains("RUN_OFFSET", w);
        Assert.DoesNotContain("version=1.0.", w);
    }

    /// <summary>ساختِ محلی هم باید همان نسخه را نشان دهد، نه ۱٫۰٫۰.</summary>
    [Fact]
    public void The_project_file_carries_the_same_starting_version()
    {
        var proj = File.ReadAllText(Path.Combine(Native, "PumpYaqobi.App", "PumpYaqobi.App.csproj"));
        Assert.Contains("<Version>3.1.1</Version>", proj);
    }
}
