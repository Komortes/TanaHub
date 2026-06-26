using Material.Icons;
using TanaHub.Application.Insights;

namespace TanaHub.UI.ViewModels;

public sealed record DashboardChartViewModel(
    string Title,
    string Detail,
    MaterialIconKind Icon,
    IReadOnlyList<DashboardChartSegmentViewModel> Segments)
{
    public bool HasSegments => Segments.Count > 0;

    public static DashboardChartViewModel FromSegments(
        string title,
        string detail,
        IReadOnlyList<LibraryInsightSegment> segments,
        int totalCount,
        MaterialIconKind icon = MaterialIconKind.ChartBar)
    {
        return new DashboardChartViewModel(
            title,
            detail,
            icon,
            segments
                .OrderByDescending(segment => segment.Count)
                .ThenBy(segment => segment.Key, StringComparer.OrdinalIgnoreCase)
                .Select(segment => DashboardChartSegmentViewModel.FromInsightSegment(segment, totalCount))
                .ToArray());
    }
}
