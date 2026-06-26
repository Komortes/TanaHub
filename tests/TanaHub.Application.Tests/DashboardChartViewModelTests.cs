using TanaHub.Application.Insights;
using TanaHub.UI.ViewModels;

namespace TanaHub.Application.Tests;

public sealed class DashboardChartViewModelTests
{
    [Fact]
    public void FromSegments_FormatsSegmentPercentages()
    {
        var chart = DashboardChartViewModel.FromSegments(
            "Library mix",
            "3 titles tracked",
            [
                new LibraryInsightSegment("Anime", 2, 1, 0.5, 8),
                new LibraryInsightSegment("Manga", 1, 0, 0, null)
            ],
            totalCount: 3);

        Assert.True(chart.HasSegments);
        Assert.Equal("Anime", chart.Segments[0].Label);
        Assert.Equal(67, chart.Segments[0].Percent);
        Assert.Equal("67%", chart.Segments[0].PercentText);
        Assert.Equal("2 titles", chart.Segments[0].CountText);
    }

    [Fact]
    public void FromSegments_ExposesEmptyStateForEmptyLibrary()
    {
        var chart = DashboardChartViewModel.FromSegments(
            "Status breakdown",
            "No titles tracked yet",
            [],
            totalCount: 0);

        Assert.False(chart.HasSegments);
        Assert.Empty(chart.Segments);
        Assert.Equal("No titles tracked yet", chart.Detail);
    }
}
