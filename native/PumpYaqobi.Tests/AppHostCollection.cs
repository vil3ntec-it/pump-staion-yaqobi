namespace PumpYaqobi.Tests;

/// <summary>
/// ══ آزمون‌هایی که میزبانِ برنامه را بالا می‌آورند ═══════════════════════════
///
/// <see cref="PumpYaqobi.App.Services.AppHost.Current"/> یکی بیشتر نیست —
/// عمداً، چون در خودِ برنامه هم یک نمونه بیشتر در کار نیست. ولی xUnit
/// کلاس‌های آزمون را موازی می‌دواند، و دو کلاسی که هر دو ‎AppHost.Start‎ را
/// صدا می‌زنند به یک میزبانِ مشترک می‌رسند: هر دو ‎NeedsFirstRun()‎ را «بله»
/// می‌بینند و هر دو «admin» را می‌سازند ⇒
/// ‎UNIQUE constraint failed: Users.UserName‎.
///
/// همین یک‌بار در ورک‌فلوی ساخت قرمز شد و نسخهٔ آن روز ساخته نشد. راهش این
/// است که این کلاس‌ها در یک «مجموعه» بنشینند: xUnit مجموعه را پشتِ‌سرِ‌هم
/// می‌دواند، پس دیگر دو تا با هم شروع نمی‌کنند.
///
/// ⚠️ هر کلاسِ تازه‌ای که ‎AppHost.Start‎ را صدا می‌زند باید
/// ‎[Collection(Name)]‎ بگیرد.
/// </summary>
[CollectionDefinition(Name)]
public sealed class AppHostCollection
{
    public const string Name = "میزبانِ برنامه";
}
