using System.Globalization;
using Avalonia.Data.Converters;

namespace PumpYaqobi.App.ViewModels.Sections;

/// <summary>درصدِ پرشدگی ← قدِ نوارِ مخزن (نوار ۱۹۶ پیکسل است).</summary>
public sealed class PercentToHeightConverter : IValueConverter
{
    public static readonly PercentToHeightConverter Instance = new();
    private const double Full = 194;

    public object? Convert(object? v, Type t, object? p, CultureInfo c) =>
        v is double d ? Math.Max(0, Math.Min(Full, Full * d / 100.0)) : 0d;

    public object? ConvertBack(object? v, Type t, object? p, CultureInfo c) => throw new NotSupportedException();
}
