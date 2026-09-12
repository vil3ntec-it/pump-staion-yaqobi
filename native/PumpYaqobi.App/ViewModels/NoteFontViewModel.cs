using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PumpYaqobi.App.Services;

namespace PumpYaqobi.App.ViewModels;

/// <summary>
/// ══ اندازهٔ نوشتهٔ «کادرهای یادداشت» ════════════════════════════════════════
///
/// همتای ‎adjNoteFont‎ی نسخهٔ وب. خودِ سایت نوشته چرا از فونتِ بخش جداست:
/// «فونتِ کادرهای یادداشت/توضیحات — جدا از فونتِ کلی هر بخش (‎adjSecFont‎) و
/// فونتِ مودال (‎adjModalFont‎)». یعنی بزرگ کردنِ جدول نباید یادداشت‌ها را
/// بزرگ کند و برعکس.
///
/// ⚠️ یکی است برای همهٔ بخش‌ها — سایت هم یک متغیرِ ریشه‌ای دارد
/// (‎--note-font-scale‎)، نه یکی برای هر بخش. پس نمونهٔ مشترک، و هر کادرِ
/// یادداشتی در هر بخشی همان لحظه با هم عوض می‌شود.
///
/// ⚠️ کف و سقف هم همان سایت است: بینِ ‎0.5‎ و ‎2.5‎، گامِ ‎0.08‎ —
/// عمداً با کف/سقفِ ‎A−/A+‎ِ بخش (‎0.6‎ تا ‎2.2‎) یکی نیست.
/// </summary>
public sealed partial class NoteFontViewModel : ObservableObject
{
    /// <summary>پایهٔ اندازه — همان ‎0.8rem‎ی سایت.</summary>
    public const double BaseSize = 13;

    public const double Step = 0.08;
    public const double Min = 0.5;
    public const double Max = 2.5;

    public static NoteFontViewModel Instance { get; } = new();

    private NoteFontViewModel() => _scale = Clamp(AppSettings.Load().NoteFontScale);

    private static double Clamp(double v)
    {
        if (double.IsNaN(v) || double.IsInfinity(v) || v <= 0) return 1;
        return Math.Round(Math.Clamp(v, Min, Max), 2);
    }

    [ObservableProperty] private double _scale;

    /// <summary>اندازهٔ واقعیِ نوشته — همان چیزی که کادرها به آن بایند می‌شوند.</summary>
    public double Size => Math.Round(BaseSize * Scale, 1);

    partial void OnScaleChanged(double v)
    {
        OnPropertyChanged(nameof(Size));
        AppSettings.SaveNoteFontScale(v);
    }

    [RelayCommand] private void Bigger() => Set(Scale + Step);
    [RelayCommand] private void Smaller() => Set(Scale - Step);

    private void Set(double v)
    {
        v = Clamp(v);
        if (Math.Abs(v - Scale) < 0.0005) return;          // به کف/سقف رسیده
        Scale = v;
    }
}
