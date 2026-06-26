using TanaHub.Application.Insights;

namespace TanaHub.UI.ViewModels;

public sealed record DashboardChartSegmentViewModel(
    string Label,
    int Count,
    int Percent)
{
    public string CountText => Count == 1 ? "1 title" : $"{Count} titles";

    public string PercentText => $"{Percent}%";

    public static DashboardChartSegmentViewModel FromInsightSegment(
        LibraryInsightSegment segment,
        int totalCount)
    {
        var percent = totalCount <= 0
            ? 0
            : (int)Math.Round(segment.Count * 100.0 / totalCount, MidpointRounding.AwayFromZero);

        return new DashboardChartSegmentViewModel(segment.Key, segment.Count, Math.Clamp(percent, 0, 100));
    }
}
