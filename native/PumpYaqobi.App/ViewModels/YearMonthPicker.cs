using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using PumpYaqobi.Application.Localization;

namespace PumpYaqobi.App.ViewModels;

/// <summary>
/// ══ کشویِ «سال» کنارِ کشویِ «ماه» ═══════════════════════════════════════════
///
/// همتای ‎_ymSelectsHtml‎ · ‎_ymYearChanged‎ی نسخهٔ وب. خودِ سایت هم دلیلش را
/// نوشته: «هر بخشی که کادرِ کشویی ماه دارد، یک کادرِ کشویی سال هم داشته باشد؛
/// با انتخابِ هر سال، فقط ماه‌های همان سال در کشویِ ماه بیایند.»
///
/// گزارشِ صاحب ریپو: «کادرِ کشویی سال هم نیست.» — در برنامهٔ نیتیو هیچ بخشی
/// نداشت؛ همهٔ ماه‌های همهٔ سال‌ها در یک فهرستِ بلند بودند.
///
/// ⚠️ نکتهٔ دوم که سایت هم صریح نوشته: «هیچ منطقی عوض نمی‌شود» — کشویِ سال
/// فقط فهرستِ ماه‌ها را کوتاه می‌کند. خودِ بخش مثلِ قبل با ماهِ انتخاب‌شده
/// رندر می‌شود.
///
/// ⚠️ نکتهٔ سوم: کلیدها ‎"YYYY/MM"‎اند ولی آن‌چه کاربر می‌بیند باید برچسبِ
/// خوانا باشد («سنبله ۱۴۰۵»)، نه خودِ کلید. تا امروز کلیدِ خام نشان داده
/// می‌شد.
/// </summary>
public sealed partial class YearMonthPicker : ObservableObject
{
    private readonly Action<string> _onMonthPicked;
    private List<string> _all = new();
    private bool _quiet;

    /// <summary>متنِ «همهٔ ماه‌ها» — خالی یعنی این بخش چنین گزینه‌ای ندارد.</summary>
    public string AllLabel { get; }

    /// <summary>«‎YYYY/*‎» یعنی همهٔ ماه‌های همان سال — همان قراردادِ سایت.</summary>
    public const string AllMark = "/*";

    public YearMonthPicker(Action<string> onMonthPicked, string allLabel = "")
    { _onMonthPicked = onMonthPicked; AllLabel = allLabel; }

    public ObservableCollection<YearMonthItem> Years { get; } = new();
    public ObservableCollection<YearMonthItem> Months { get; } = new();

    [ObservableProperty] private YearMonthItem? _year;
    [ObservableProperty] private YearMonthItem? _selected;

    public bool HasYears => Years.Count > 1;

    /// <summary>کلیدِ ماهِ انتخاب‌شده — همان چیزی که بخش با آن رندر می‌کند.</summary>
    public string SelectedKey => Selected?.Key ?? "";

    /// <summary>
    /// فهرست را از نو بچین. ‎keys‎ همهٔ کلیدهای این بخش است و ‎active‎ کلیدی که
    /// باید انتخاب بماند.
    /// </summary>
    public void Load(IEnumerable<string> keys, string? active)
    {
        // ══ فقط ماه‌هایی که واقعاً هستند ════════════════════════════════════
        //
        // ⚠️ این‌جا یک‌بار «هر دوازده ماهِ هر سال» ساخته می‌شد. غلط بود: سایت
        // فقط ماه‌هایی را در کشویی می‌گذارد که داده دارند، و ماهِ تازه **فقط**
        // با دکمهٔ «📅 ماه جدید» باز می‌شود (‎addExpenseMonth‎). صاحب ریپو با
        // عکسِ خودِ سایت همین را خواست. حالا دقیقاً همان است:
        //
        //     کشویِ سال  → «📆 همهٔ سال‌ها» + سال‌هایی که داده دارند
        //     کشویِ ماه → «همهٔ ماه‌های ‎<سال>‎» + ماه‌های همان سال
        //
        // (‎_ymSelectsHtml‎ی سایت، خطِ ۳۷۶۱۵ی index.html)
        _all = keys.Where(k => k.Any(char.IsDigit)).Distinct()
                   .OrderByDescending(k => k, StringComparer.Ordinal).ToList();

        _quiet = true;
        Years.Clear();
        if (AllLabel.Length > 0) Years.Add(new YearMonthItem("", "📆 همهٔ سال‌ها"));
        foreach (var y in _all.Select(YearOf).Where(y => y.Length > 0).Distinct()
                              .OrderByDescending(y => y, StringComparer.Ordinal))
            Years.Add(new YearMonthItem(y, "📆 " + y));

        // «همهٔ ماه‌ها»ی یک سال، سالِ خودش را نگه می‌دارد — همان ‎'YYYY/*'‎ی سایت
        var wantYear = AllOfYear(active);
        if (wantYear.Length == 0) wantYear = YearOf(active ?? "");
        Year = Years.FirstOrDefault(y => y.Key == wantYear)
               ?? (AllLabel.Length > 0 && IsAll(active) ? Years.FirstOrDefault()
                                                        : Years.FirstOrDefault(y => y.Key.Length > 0))
               ?? Years.FirstOrDefault();
        _quiet = false;

        FillMonths(active);
        OnPropertyChanged(nameof(HasYears));
    }

    partial void OnYearChanged(YearMonthItem? v)
    {
        if (_quiet) return;
        // با عوض شدنِ سال: اگر روی «همهٔ ماه‌ها» بودیم همان می‌مانیم (ولی
        // ماه‌های سالِ تازه)، وگرنه هم‌شمارهٔ ماهِ قبلی در سالِ تازه و اگر
        // نبود، اولین ماهِ همان سال. عینِ ‎_ymYearChanged‎ی سایت.
        var curM = MonthOf(SelectedKey);
        var wasAll = IsAll(SelectedKey);
        var y = v?.Key ?? "";
        var inYear = y.Length > 0 ? _all.Where(k => YearOf(k) == y).ToList() : _all.ToList();

        var want = wasAll && AllLabel.Length > 0
            ? (y.Length > 0 ? y + AllMark : "")
            : (inYear.FirstOrDefault(k => MonthOf(k) == curM) ?? inYear.FirstOrDefault() ?? "");

        FillMonths(want);
    }

    partial void OnSelectedChanged(YearMonthItem? v)
    {
        if (_quiet || v is null) return;
        _onMonthPicked(v.Key);
    }

    private void FillMonths(string? want)
    {
        var y = Year?.Key ?? "";
        var inYear = y.Length > 0 ? _all.Where(k => YearOf(k) == y).ToList() : _all.ToList();

        _quiet = true;
        Months.Clear();
        if (AllLabel.Length > 0)
            Months.Add(new YearMonthItem(y.Length > 0 ? y + AllMark : "",
                                         y.Length > 0
                                             ? AllLabel.Replace("ماه‌ها", "ماه‌های " + y)
                                             : AllLabel));
        foreach (var k in inYear) Months.Add(new YearMonthItem(k, OptionLabel(k)));

        Selected = Months.FirstOrDefault(m => m.Key == want) ?? Months.FirstOrDefault();
        _quiet = false;

        if (Selected is not null) _onMonthPicked(Selected.Key);
    }

    /// <summary>ماهی که تازه ساخته شد را بیاور و رویش بایست.</summary>
    public void Adopt(string key)
    {
        if (!_all.Contains(key)) _all.Insert(0, key);
        Load(_all, key);
    }

    public static string YearOf(string? k)
    {
        var s = Shamsi.ToEnDigits(k ?? "");
        var i = s.IndexOf('/');
        return i <= 0 ? "" : s[..i];
    }

    public static string MonthOf(string? k)
    {
        var s = Shamsi.ToEnDigits(k ?? "");
        var i = s.IndexOf('/');
        return i < 0 || i + 1 >= s.Length ? "" : s[(i + 1)..];
    }

    /// <summary>
    /// «همهٔ ماه‌ها»؟ — خالی هم «همه» است، دقیقاً مثلِ ‎_ymIsAll‎ی سایت
    /// (<c>return !v || String(v).slice(-2) === '/*'</c>).
    /// </summary>
    public static bool IsAll(string? k) =>
        string.IsNullOrEmpty(k) || k.EndsWith(AllMark, StringComparison.Ordinal);

    /// <summary>«1405/07» ⇒ «میزان — 1405/07» — همان ‎_monthOptionLabel‎ی سایت.</summary>
    public static string OptionLabel(string? k)
    {
        var s = Shamsi.ToEnDigits(k ?? "");
        var p = s.Split('/');
        if (p.Length != 2 || !int.TryParse(p[1], out var m)) return Shamsi.MonthLabel(k);
        var name = Shamsi.MonthName(m);
        return (name.Length > 0 ? name : p[1]) + " — " + s;
    }

    /// <summary>«1405/*» ⇒ «1405». اگر «همهٔ ماه‌ها» نباشد، رشتهٔ خالی.</summary>
    public static string AllOfYear(string? k)
    {
        var s = Shamsi.ToEnDigits(k ?? "");
        return s.EndsWith(AllMark, StringComparison.Ordinal) ? s[..^AllMark.Length] : "";
    }
}

/// <summary>یک گزینهٔ کشویی: کلیدِ واقعی و برچسبی که کاربر می‌بیند.</summary>
public sealed record YearMonthItem(string Key, string Label)
{
    public override string ToString() => Label;
}
