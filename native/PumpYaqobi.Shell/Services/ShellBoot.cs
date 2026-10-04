using System.Reflection;
using System.Runtime.CompilerServices;

namespace PumpYaqobi.App.Services;

/// <summary>
/// ══ شورا، ج۵ — پوسته همیشه با سیم‌کشیِ برنامه ════════════════════════════════
///
/// پوسته آوالونیا و <c>UpdateService</c> را نمی‌شناسد؛ برنامه آن‌ها را از راهِ
/// <c>ModuleInitializer</c>ِ خودش وصل می‌کند (<c>UiThreadAvalonia</c>). ⚠️ ولی آن
/// مقداردهی فقط وقتی می‌دود که اسمبلیِ برنامه به کار افتاده باشد — و سنجه‌ای که
/// فقط یک نوعِ پوسته را صدا بزند ممکن بود پیش از آن برسد و رفتارِ دیگری ببیند.
/// پس هر درِ پوسته که به سیم‌کشی بند است اول <see cref="Ensure"/> را می‌زند: اسمبلیِ
/// برنامه (<c>PumpYaqobi</c>) بار و سازندهٔ ماژولش اجرا می‌شود — یک بار، بی‌خطا.
/// </summary>
public static class ShellBoot
{
    private static int _done;

    public static void Ensure()
    {
        if (Interlocked.Exchange(ref _done, 1) == 1) return;
        try
        {
            var app = AppIdentity.Assembly;
            if (!ReferenceEquals(app, typeof(ShellBoot).Assembly))
                RuntimeHelpers.RunModuleConstructor(app.ManifestModule.ModuleHandle);
        }
        catch { /* بی برنامه (ابزارِ جدا) ⇒ پیش‌فرض‌های خودِ پوسته */ }
    }
}

/// <summary>
/// ══ شورا، ج۵ — «این برنامه کدام اسمبلی است؟» یک جا ════════════════════════════
///
/// ⛔ نسخه (<c>AppVersion</c>) و ریشه‌های اعتماد (<c>AssemblyMetadata</c>ِ
/// <c>LicenseKeys</c> · <c>UpdateKey</c> · <c>IntegrityKey</c> · <c>OfflineKeys</c>) مالِ
/// <b>اسمبلیِ برنامه</b> (<c>PumpYaqobi</c>) هستند، نه پوسته. پیش از جدایی
/// <c>typeof(CloudConfig).Assembly</c> همان برنامه بود؛ پس از جدایی همان کد بی‌صدا
/// کلیدهای <b>خالی</b> می‌خواند — یعنی مجوز با TOFU و به‌روزرسانی بی امضا. این در
/// همیشه اسمبلیِ برنامه را می‌دهد و <c>ShellIdentityTests</c> همین را قفل کرده.
/// </summary>
public static class AppIdentity
{
    private static Assembly? _asm;

    public static Assembly Assembly => _asm ??= Find();

    private static Assembly Find()
    {
        foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
            if (a.GetName().Name == "PumpYaqobi") return a;
        try { return Assembly.Load(new AssemblyName("PumpYaqobi")); }
        catch { return typeof(AppIdentity).Assembly; }
    }
}

/// <summary>
/// ⛔ نسلِ دفتر: ردیفی که پیش از عوض شدنِ دفتر ساخته شده، دیگر نمی‌نویسد
/// (<c>RowViewModel</c>). شمارنده این‌جاست چون عوض کردنِ دفتر کارِ میزبان
/// (<c>AppHost.UseLedgerOf</c>) است.
/// </summary>
public static class LedgerGen
{
    private static int _gen;
    public static int Current => Volatile.Read(ref _gen);
    public static void Next() => Interlocked.Increment(ref _gen);
}
