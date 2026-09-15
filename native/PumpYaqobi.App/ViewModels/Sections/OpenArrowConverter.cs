using System.Globalization;
using Avalonia.Data.Converters;

namespace PumpYaqobi.App.ViewModels.Sections;

/// <summary>فلشِ نوارِ کشویی: باز «▾»، بسته «◂».</summary>
public sealed class OpenArrowConverter : IValueConverter
{
    public static readonly OpenArrowConverter Instance = new();
    public object? Convert(object? value, Type t, object? p, CultureInfo c) => value is true ? "▾" : "◂";
    public object? ConvertBack(object? v, Type t, object? p, CultureInfo c) => throw new NotSupportedException();
}
