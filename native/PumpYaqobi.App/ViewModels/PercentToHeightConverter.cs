using System.Globalization;
using Avalonia.Data.Converters;

namespace PumpYaqobi.App.ViewModels.Sections;

/// <summary>درصدِ پرشدگی ← قدِ نوارِ مخزن (نوار ۱۹۶ پیکسل است).</summary>
public sealed class PercentToHeightConverter : IValueConverter
{
    public static readonly PercentToHeightConverter Instance = new(194);

    /// <summary>ستون‌های نمودارِ فروشِ داشبورد — کادرشان ۱۴۰ پیکسل است.</summary>
    public static readonly PercentToHeightConverter Bar = new(140);

    private readonly double _full;

    private PercentToHeightConverter(double full) => _full = full;

    public object? Convert(object? v, Type t, object? p, CultureInfo c) =>
        v is double d ? Math.Max(0, Math.Min(_full, _full * d / 100.0)) : 0d;

    public object? ConvertBack(object? v, Type t, object? p, CultureInfo c) => throw new NotSupportedException();
}
