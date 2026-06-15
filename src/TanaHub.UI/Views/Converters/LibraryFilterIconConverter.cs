using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Material.Icons;

namespace TanaHub.UI.Views.Converters;

public sealed class LibraryFilterIconConverter : IValueConverter
{
    public static readonly LibraryFilterIconConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is string key ? key switch
        {
            "All"       => MaterialIconKind.LayersOutline,
            "Current"   => MaterialIconKind.PlayCircleOutline,
            "Completed" => MaterialIconKind.CheckCircleOutline,
            "Planning"  => MaterialIconKind.BookmarkPlusOutline,
            "Paused"    => MaterialIconKind.PauseCircleOutline,
            "Dropped"   => MaterialIconKind.CloseCircleOutline,
            "Updated"   => MaterialIconKind.ClockEditOutline,
            "Title"     => MaterialIconKind.SortAlphabeticalAscending,
            "Score"     => MaterialIconKind.StarOutline,
            "Progress"  => MaterialIconKind.ChartLine,
            _           => MaterialIconKind.Circle,
        } : MaterialIconKind.Circle;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
