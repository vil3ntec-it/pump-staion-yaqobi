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
/// ⛔ تاریخ و ساعتی که این‌جا گذاشته می‌شود <b>نمایشی</b> است
/// (<see cref="DisplayClock"/>، ۱۴۰۵/۰۷/۱۶ — «من گفتم نمایشی… هر جور بخواهم
/// می‌گذارم و روی برنامه تأثیر نگذارد»): فقط سربرگ و داشبورد عوض می‌شوند؛ نه
/// ساعتِ ویندوز، نه اجازهٔ مدیر، و نه هیچ ردیف، ماه یا اشتراکی.
/// ⛔ هیچ هشدارِ «ساعتِ ویندوز جلو/عقب است» در این پنجره نیست.
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
    [ObservableProperty] private string _nowText = "";

    public ClockViewModel() : this(DisplayClock.Now) { }

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

    /// <summary>همان تاریخ و ساعتِ سربرگ.</summary>
    public void Tick(DateTime now)
    {
        NowText = MainViewModel.HeaderDate(now) + " · " + Localization.Clock.Of(now, seconds: true);
        OnPropertyChanged(nameof(Shifted));
    }

    /// <summary>کاربر خودش تاریخ و ساعت گذاشته — دکمهٔ «برگشت به ساعتِ واقعی» دیده شود.</summary>
    public bool Shifted => DisplayClock.Shifted;

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

    /// <summary>«✔ ثبت» — فقط نمایش؛ بی اجازهٔ ویندوز و بی اثر روی هیچ حسابی.</summary>
    [RelayCommand]
    private void Apply()
    {
        DisplayClock.Show(Picked);
        Status = DisplayClock.Shifted
            ? "✅ ثبت شد — فقط تاریخ و ساعتِ نمایشی عوض شد؛ هیچ حساب، ماه یا اشتراکی دست نخورد."
            : "✅ همان تاریخ و ساعتِ واقعی نشان داده می‌شود.";
        Tick(DisplayClock.Now);
    }

    /// <summary>برگشت به تاریخ و ساعتِ واقعی.</summary>
    [RelayCommand]
    private void Real()
    {
        DisplayClock.Reset();
        Now();
        Status = "✅ تاریخ و ساعتِ واقعی نشان داده می‌شود.";
        Tick(DisplayClock.Now);
    }

    /// <summary>برگشت به همین حالا — اگر کاربر چیزی را به‌هم زد.</summary>
    [RelayCommand]
    private void Now()
    {
        var now = DisplayClock.Now;
        Year = Cal.GetYear(now); MonthIndex = Cal.GetMonth(now) - 1; Day = Cal.GetDayOfMonth(now);
        HourIndex = now.Hour; MinuteIndex = now.Minute;
        Status = "";
    }
}
