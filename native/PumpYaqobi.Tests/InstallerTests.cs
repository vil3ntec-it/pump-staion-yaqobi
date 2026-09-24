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
        Assert.Contains("DefaultDirName={localappdata}\\Programs\\", s);

        //  ⛔ و نامِ پوشهٔ ۶۴بیتی **یک حرف هم عوض نشد**. از ۳.۱.۱۵۸ پوشه از
        //  ‎InstallFolder‎ می‌آید (چون ۳۲بیتی پوشهٔ جدا دارد)، پس این بند
        //  دیگر رشتهٔ چسبیده را نمی‌بیند — و همان جایی است که یک تغییرِ
        //  بی‌دقت می‌توانست هر نصبی را که همین حالا دستِ مشتری است از
        //  به‌روزرسانی بیندازد. پس هر دو نام صریح خواسته می‌شوند.
        Assert.Contains("#define InstallFolder \"PumpYaqobi\"", s);
        Assert.Contains("#define InstallFolder \"PumpYaqobi-32\"", s);
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

    // ══ برچسبِ چرخشی ═════════════════════════════════════════════════════════
    // دو چیز از آن می‌خورند و هر دو تا ۱۴۰۵/۰۷/۰۶ بی‌صدا مرده بودند: درِ دومِ
    // به‌روزرسانیِ خودِ برنامه، و دکمهٔ دانلودِ ویندوزِ سایت. سایت ماه‌ها
    // `grab('desktop-latest', …)` می‌زد و ورک‌فلو چنین برچسبی نمی‌ساخت، پس
    // فقط یک `::warning::` می‌گرفت و `version.json` ستونِ ویندوز را null
    // می‌داد. هیچ‌کس نمی‌فهمید.

    [Fact]
    public void The_workflow_publishes_the_rolling_tag()
    {
        var w = Workflow();
        Assert.Contains("tag_name: desktop-latest", w);
        Assert.Contains("rel/base.txt", w);

        // ⛔ و هرگز «تازه‌ترین انتشار» نشود: برچسبِ بی‌شماره یعنی برنامه هیچ
        // شماره‌ای از آن درنمی‌آورد. (همان تله‌ای که ریپوی سرور خورد.)
        var i = w.IndexOf("tag_name: desktop-latest", StringComparison.Ordinal);
        var block = w[i..Math.Min(w.Length, i + 900)];
        Assert.Contains("prerelease: true", block);
        Assert.Contains("make_latest: false", block);

        // و همان برچسب باید همان فایل‌هایی را داشته باشد که درِ دوم می‌خواند
        Assert.Contains("rel/version.txt", block);
        Assert.Contains("rel/base.txt", block);
        Assert.Contains("rel/PumpYaqobi-Setup.exe", block);
    }

    [Fact]
    public void The_site_reads_the_same_rolling_tag()
    {
        var pages = File.ReadAllText(Path.Combine(Repo, ".github", "workflows", "deploy-pages.yml"));
        Assert.Contains("grab('desktop-latest', 'pumpyaqobi-setup.exe', 'PumpYaqobi-Setup.exe')", pages);
        Assert.Contains("grab('desktop-latest', 'version.txt', 'exe-version.txt')", pages);

        //  ⛔ نامِ **دقیق**، نه پسوندِ `.exe`. از ۳.۱.۱۵۸ دو نصاب روی آن
        //  برچسب است و `grab` تازه‌ترینِ هر پسوند را برمی‌دارد — یعنی
        //  دکمهٔ دانلودِ ویندوزِ سایت می‌توانست فایلِ ۳۲بیتی بدهد و کاربرِ
        //  ۶۴بیتی برنامه‌ای بگیرد که کندتر است، یا برعکسش که اصلاً بالا
        //  نمی‌آید.
        Assert.DoesNotContain("grab('desktop-latest', '.exe'", pages);

        // ⛔ زیپِ آن برچسب بستهٔ کوچکِ به‌روزرسانی است، نه برنامهٔ کامل —
        // کنارِ سایت گذاشتنش یعنی کسی ۶ مگابایت می‌گیرد و برنامه بالا نمی‌آید.
        Assert.DoesNotContain("'PumpYaqobi-Portable.zip'", pages);
    }

    // ══ چک‌سام ══
    // گزارشِ صاحب ریپو (۱۴۰۵/۰۷/۰۸) با عکس: نصاب وسطِ کار «The source file
    // is corrupted» داد، و اندازهٔ فایلِ دانلودشده مو‌به‌مو با اندازهٔ
    // منتشرشده یکی بود. هیچ راهی نبود بفهمیم خرابی از دانلود است یا از
    // خودِ ساخت — چون این انتشار چک‌سام نداشت.

    /// <summary>
    /// ⛔ هر انتشار باید ‎SHA256SUMS.txt‎ داشته باشد. بی آن، «دانلودت خراب
    /// است» و «ساختِ ما خراب است» از هم جدا نمی‌شوند و هر دو طرف حدس
    /// می‌زنند. ریپوی ‎server‎ این را از قبل دارد.
    /// </summary>
    [Fact]
    public void Every_release_publishes_a_checksum_file()
    {
        var w = Workflow();
        Assert.Contains("name: چک‌سام‌ها", w);
        Assert.Contains("SHA256SUMS.txt", w);

        //  در هر دو انتشار — اصلی و چرخشی. برچسبِ چرخشی همان است که
        //  درِ دومِ به‌روزرسانی و دکمهٔ دانلودِ سایت از آن می‌خوانند، پس
        //  فایلی که آن‌جا نباشد برای نیمی از دانلودها بی‌فایده است.
        //  ⚠️ فقط سطرهای فهرستِ ‎files:‎ شمرده می‌شوند، نه هر جایی که این
        //  نام آمده: خودِ گامِ چک‌سام دو بار نامش را می‌برد، پس شمارشِ خام
        //  حتی وقتی فایل در هیچ انتشاری بالا نرود هم سبز می‌ماند — یعنی
        //  سنجه‌ای که چیزی را نگه نمی‌دارد.
        var uploads = Regex.Matches(
            //  ⚠️ ‎\r?‎ لازم است: روی ویندوز گیت فایل را با CRLF چک‌اوت
            //  می‌کند و در دات‌نت ‎$‎ با ‎Multiline‎ پیش از ‎\n‎ می‌ایستد، پس
            //  یک ‎\r‎ بینشان می‌ماند و الگو هیچ‌وقت نمی‌گیرد. بی این، همین
            //  سنجه روی رانرِ ویندوز «۰ سطر» دید در حالی که فایل درست بود.
            w, @"^ {12}rel/SHA256SUMS\.txt\r?$", RegexOptions.Multiline).Count;
        Assert.True(uploads == 2,
            $"چک‌سام باید در هر دو انتشار بالا برود — {uploads} سطر دیده شد");
    }

    /// <summary>
    /// ⚠️ چک‌سام باید <b>پس از</b> ساختِ نصاب گرفته شود، وگرنه خودِ نصاب —
    /// یعنی همان فایلی که کاربر دانلود می‌کند — در فهرست نیست و این کار
    /// هیچ کدام از دو حالتِ بالا را جدا نمی‌کند.
    /// </summary>
    [Fact]
    public void Checksums_are_taken_after_the_installer_is_built()
    {
        var w = Workflow();
        var installer = w.IndexOf("name: ساختِ نصاب", StringComparison.Ordinal);
        var sums = w.IndexOf("name: چک‌سام‌ها", StringComparison.Ordinal);
        Assert.True(installer > 0 && sums > 0, "مرحله‌ها پیدا نشدند");
        Assert.True(installer < sums,
            "چک‌سام پیش از ساختِ نصاب گرفته می‌شود — خودِ نصاب در فهرست نیست");

        //  و پیش از انتشار، وگرنه فایلی برای بالا فرستادن نیست.
        var release = w.IndexOf("name: انتشار", StringComparison.Ordinal);
        Assert.True(release > sums,
            "چک‌سام پس از انتشار گرفته می‌شود — فایلش هیچ‌وقت بالا نمی‌رود");
    }

    // ══ اندازهٔ نصاب ══
    // گزارشِ صاحب ریپو (۱۴۰۵/۰۷/۰۸): «نصب نمی‌شود، چند نسخه قبل‌تر
    // می‌شد.» نصاب از ~۷۹ مگابایت به ~۱۱۰ پریده بود، و ۵۴٪ بارش libvlc
    // بود — نیمی از آن برای معماری‌ای که این برنامه هرگز اجرا نمی‌کند.

    /// <summary>
    /// ⛔ بارِ هر معماری فقط <b>کتابخانهٔ خودش</b> را داشته باشد.
    /// <c>VideoLAN.LibVLC.Windows</c> هر دو را می‌آورد
    /// (<c>libvlc/win-x64</c> و <c>libvlc/win-x86</c>، روی هم ~۱۹۵ مگابایت)
    /// و آن‌که به بیتیِ پروسه نمی‌خورد هرگز بار نمی‌شود —
    /// <c>Core.Initialize()</c>ی بی‌مسیر از بیتیِ خودِ پروسه انتخاب می‌کند.
    ///
    /// ⚠️ و از ۳.۱.۱۵۸ این گام <b>وارونه هم</b> می‌شود: در بارِ ۳۲بیتی
    /// آن‌که باید برود <c>win-x64</c> است. پس عددِ ثابت ممنوع — تصمیم باید
    /// از روی خودِ RID باشد، وگرنه بارِ ۳۲بیتی همان چیزی را از دست می‌دهد
    /// که لازم دارد و کارتِ دوربین بی‌صدا می‌میرد.
    /// </summary>
    [Fact]
    public void The_unused_vlc_architecture_is_trimmed_before_packing()
    {
        var w = Workflow();
        var trim = w.IndexOf("name: چیدنِ معماریِ بی‌استفادهٔ VLC", StringComparison.Ordinal);
        Assert.True(trim > 0, "گامِ چیدن پیدا نشد");
        var step = w[trim..];
        var end = step.IndexOf("\n      - name:", StringComparison.Ordinal);
        if (end > 0) step = step[..end];

        Assert.Contains("Remove-Item", step);

        //  ⛔ هر دو معماری باید در تصمیم باشند — نه یکی که همیشه برود.
        Assert.Contains("win-x64", step);
        Assert.Contains("win-x86", step);

        //  ⛔ و آن‌چه برداشته می‌شود **متغیر** است، نه یک نامِ ثابت.
        Assert.Contains("$drop", step);
        Assert.Contains("$keep", step);

        //  ⛔ ترتیب: انتشار ⇒ چیدن ⇒ بسته‌ها. اگر پس از «بسته‌ها» بدود،
        //  زیپ و نصاب و شناسهٔ پایه هر سه از درختِ نچیده ساخته می‌شوند.
        //  ⚠️ لنگرِ گامِ ساخت «dotnet publish» است، نه «name: ساخت»:
        //  خطِ اولِ ورک‌فلو نامِ خودش است («ساخت برنامهٔ نیتیو (ویندوز)»)،
        //  و «- name: ساخت» هم پیشوندِ «- name: ساختِ نصاب» است.
        var build = w.IndexOf("dotnet publish PumpYaqobi.App", StringComparison.Ordinal);
        var pack = w.IndexOf("name: بسته‌ها", StringComparison.Ordinal);
        Assert.True(build > 0 && pack > 0, "مرحله‌ها پیدا نشدند");
        Assert.True(build < trim, "چیدن پیش از ساخت — پوشه‌ای برای چیدن نیست");
        Assert.True(trim < pack, "چیدن پس از بسته‌ها — بار نچیده بسته می‌شود");
    }

    /// <summary>
    /// ⚠️ و کتابخانهٔ **خودِ همان معماری** باید بماند: برداشتنش کارتِ دوربین
    /// را بی‌صدا می‌کشد. پس ساخت باید همان‌جا بشکند، نه این‌که رد شود.
    /// </summary>
    [Fact]
    public void The_used_vlc_architecture_is_checked_not_assumed()
    {
        var w = Workflow();
        Assert.Contains("libvlc/$keep/libvlc.dll", w);
        Assert.Contains("throw", w);

        //  و کدِ برنامه هم باید همان راهِ «بیتیِ پروسه» را برود، وگرنه
        //  این چیدن بی‌معنا می‌شود.
        var feed = File.ReadAllText(
            Path.Combine(Native, "PumpYaqobi.App", "Services", "VlcVideoFeed.cs"));
        Assert.Contains("Core.Initialize()", feed);
    }
}
