using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PumpYaqobi.App.Services;
using PumpYaqobi.Application.Localization;
using PumpYaqobi.Domain;

namespace PumpYaqobi.App.ViewModels;

/// <summary>
/// پنجرهٔ «🕘 تاریخ و ساعت» — مالِ خودِ برنامه، شمسی و با نامِ ماه.
/// ⛔ هیچ ساعتِ دومی نگه نمی‌دارد: «ثبت» ساعتِ خودِ ویندوز را عوض می‌کند
/// (<see cref="ClockService"/>)، و تا آن نشده برنامه همان ساعتِ کامپیوتر را
/// می‌خواند. شرحِ چرایی بالای <see cref="ClockService"/>.
/// </summary>
public sealed partial class ClockViewModel : ObservableObject
{
    private static readonly PersianCalendar Cal = new();

    public ObservableCollection<int> Years { get; } = new();
    public IReadOnlyList<string> Months { get; } = Shamsi.MonthNames;
    public ObservableCollection<int> Days { get; } = new();
    //  ⛔ دوازده‌ساعته (خواستهٔ ۱۴۰۵/۰۷/۱۵)؛ شاخص همان ۰ تا ۲۳ است، پس «ثبت» دست نخورد
    public IReadOnlyList<string> Hours { get; } = Enumerable.Range(0, 24).Select(Localization.Clock.HourLabel).ToList();
    public IReadOnlyList<string> Minutes { get; } = Enumerable.Range(0, 60).Select(m => m.ToString("00")).ToList();

    [ObservableProperty] private int _year;
    /// <summary>شمارهٔ ماه از صفر (همان ترتیبِ ‎Months‎).</summary>
    [ObservableProperty] private int _monthIndex;
    [ObservableProperty] private int _day;
    [ObservableProperty] private int _hourIndex;
    [ObservableProperty] private int _minuteIndex;
    [ObservableProperty] private string _status = "";
    [ObservableProperty] private bool _busy;
    [ObservableProperty] private string _nowText = "";

    public ClockViewModel() : this(AppClock.Now) { }

    public ClockViewModel(DateTime now)
    {
        var y = Cal.GetYear(now);
        for (var i = y - 3; i <= y + 3; i++) Years.Add(i);
        _year = y;
        _monthIndex = Cal.GetMonth(now) - 1;
        FillDays();
        _day = Cal.GetDayOfMonth(now);
        _hourIndex = now.Hour;
        _minuteIndex = now.Minute;
        Tick(now);
    }

    /// <summary>ساعتِ کنونیِ کامپیوتر، با همان شکلِ سربرگ.</summary>
    public void Tick(DateTime now)
    {
        NowText = MainViewModel.HeaderDate(now) + " · " + Localization.Clock.Of(now, seconds: true);
        var skew = AppClock.WallSkewMs;
        WallText = Math.Abs(skew) > AppClock.SkewWarnMs
            ? "⚠️ ساعتِ ویندوز " + MainViewModel.SkewText(skew) + " — برنامه با تاریخ و ساعتِ بالا (از اینترنت) کار می‌کند."
            : AppClock.Trusted ? "✅ ساعتِ ویندوز با ساعتِ اینترنت یکی است." : "";
    }

    /// <summary>حالِ ساعتِ ویندوز در برابرِ ساعتِ اینترنت (‎AppClock‎).</summary>
    [ObservableProperty] private string _wallText = "";

    /// <summary>همان لحظه‌ای که روی پنجره چیده شده — به وقتِ محلی.</summary>
    public DateTime Picked => Cal.ToDateTime(Year, MonthIndex + 1, Math.Min(Day, Cal.GetDaysInMonth(Year, MonthIndex + 1)),
                                             HourIndex, MinuteIndex, 0, 0);

    /// <summary>پیش‌نمایشِ همان چیزی که ثبت خواهد شد — «یک‌شنبه میزان 1405/7/5 · 09:30 AM».</summary>
    public string PickedText
    {
        get
        {
            try { return MainViewModel.HeaderDate(Picked) + " · " + Localization.Clock.Of(Picked); }
            catch { return ""; }
        }
    }

    partial void OnYearChanged(int value) { FillDays(); OnPropertyChanged(nameof(PickedText)); }
    partial void OnMonthIndexChanged(int value) { FillDays(); OnPropertyChanged(nameof(PickedText)); }
    partial void OnDayChanged(int value) => OnPropertyChanged(nameof(PickedText));
    partial void OnHourIndexChanged(int value) => OnPropertyChanged(nameof(PickedText));
    partial void OnMinuteIndexChanged(int value) => OnPropertyChanged(nameof(PickedText));

    /// <summary>روزهای همان ماه: ۳۱ · ۳۰ · ۲۹/۳۰ برای حوت — از خودِ تقویم، نه حدس.</summary>
    private void FillDays()
    {
        if (Year <= 0 || MonthIndex is < 0 or > 11) return;
        var n = Cal.GetDaysInMonth(Year, MonthIndex + 1);
        if (Days.Count == n) return;
        var keep = Day;
        Days.Clear();
        for (var d = 1; d <= n; d++) Days.Add(d);
        Day = Math.Clamp(keep, 1, n);
    }

    [RelayCommand]
    private async Task ApplyAsync()
    {
        if (Busy) return;
        Busy = true;
        Status = "⏳ ویندوز یک بار اجازه می‌خواهد…";
        try
        {
            var r = await ClockService.SetAsync(Picked);
            Status = ClockService.Why(r);
            //  ⛔ برنامه خودش با ساعتِ اینترنت کار می‌کند (‎AppClock‎) — ساعتِ ویندوز
            //  فقط وقتی حرفِ آخر است که ساعتِ اینترنت هنوز نیامده.
            if (r == ClockService.Result.Done)
            {
                AppClock.UserSetWall();
                if (AppClock.Trusted)
                    Status += " — برنامه همچنان با تاریخ و ساعتِ واقعی (از اینترنت) کار می‌کند.";
            }
        }
        finally { Busy = false; Tick(AppClock.Now); }
    }

    [RelayCommand]
    private async Task SyncAsync()
    {
        if (Busy) return;
        Busy = true;
        Status = "⏳ گرفتنِ ساعتِ درست از اینترنت…";
        try
        {
            var r = await ClockService.SyncInternetAsync();
            //  ساعتِ خودِ برنامه هم — مستقل از ساعتِ ویندوز (‎TimeSync‎)
            var app = await TimeSync.CheckAsync();
            Status = r == ClockService.Result.Done ? "✅ ساعتِ کامپیوتر با اینترنت یکی شد"
                   : app ? ClockService.Why(r) + " — ولی برنامه ساعتِ واقعی را از اینترنت گرفت و با همان کار می‌کند."
                   : ClockService.Why(r);
        }
        finally { Busy = false; Tick(AppClock.Now); }
    }

    /// <summary>برگشت به همین حالا — اگر کاربر چیزی را به‌هم زد.</summary>
    [RelayCommand]
    private void Now()
    {
        var now = AppClock.Now;
        Year = Cal.GetYear(now); MonthIndex = Cal.GetMonth(now) - 1; Day = Cal.GetDayOfMonth(now);
        HourIndex = now.Hour; MinuteIndex = now.Minute;
        Status = "";
    }
}
