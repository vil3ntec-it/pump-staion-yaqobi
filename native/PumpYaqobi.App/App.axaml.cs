using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using PumpYaqobi.App.Themes;
using PumpYaqobi.App.Views;

namespace PumpYaqobi.App;

public partial class App : Avalonia.Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        // ⚠️ ترتیب مهم است: گیرندهٔ خطای نخِ رابط باید **این‌جا** بنشیند، نه در
        // ‎Program.Main‎. آن‌جا هنوز پلتفرم راه نیفتاده و دست زدن به دیسپچر،
        // آن را بی‌اتصال به حلقهٔ پیام‌های ویندوز می‌ساخت — نتیجه‌اش پنجرهٔ
        // سفیدِ مرده بود. توضیحِ کامل در ‎CrashGuard.Install‎.
        Services.CrashGuard.InstallUi();

        Services.AppHost.Start();
        ThemeManager.Apply(PumpTheme.ById(Services.AppSettings.Load().ThemeId), this);

        // ══ نشانگرِ «خانهٔ اکسل» ═════════════════════════════════════════════
        // همتای ‎cursor:cell‎ی سایت. این‌جا ثبت می‌شود، نه در خودِ سبک‌ها، چون
        // کشیدنش به موتورِ رسم نیاز دارد و این‌جا پلتفرم قطعاً بالا آمده است —
        // همان درسی که پنجرهٔ سفیدِ مرده داد.
        Resources["Pump.CellCursor"] = Controls.ExcelCursor.Cell;

        // ══ خطِ جدول‌ها ══════════════════════════════════════════════════════
        // رنگ و ضخامت از تنظیماتِ کاربر می‌آید، نه از داخلِ سبک‌ها. ‎Hook‎ هم
        // می‌بندد که با هر تعویضِ تم دوباره نوشته شوند (توضیح در ‎TableStyle‎).
        TableStyle.Hook();
        TableStyle.Apply(app: this);

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow = new MainWindow();

        base.OnFrameworkInitializationCompleted();
    }
}
