using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Material.Icons;

namespace TanaHub.UI.Views.Converters;

public sealed class NavKeyToIconConverter : IValueConverter
{
    public static readonly NavKeyToIconConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is string key ? key switch
        {
            "home" => MaterialIconKind.HomeVariant,
            "discover" => MaterialIconKind.CompassOutline,
            "library" => MaterialIconKind.Bookshelf,
            "schedule" => MaterialIconKind.CalendarClock,
            "settings" => MaterialIconKind.CogOutline,
            "recognize" => MaterialIconKind.ImageSearchOutline,
            _ => MaterialIconKind.HelpCircle
        } : MaterialIconKind.HelpCircle;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
