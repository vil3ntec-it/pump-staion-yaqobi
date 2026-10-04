using System.Globalization;
using Avalonia.Data.Converters;
using PumpYaqobi.Application.Localization;

namespace PumpYaqobi.App.Themes;

/// <summary>شورا، ث۵ — عنوانی که واژهٔ برنامه است ⇒ معنایش (یا هیچ ToolTipی).</summary>
public sealed class GlossaryTipConverter : IValueConverter
{
    public static readonly GlossaryTipConverter Instance = new();
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Glossary.MeaningIn(value as string);
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
