using TanaHub.UI.ViewModels;

namespace TanaHub.Application.Tests;

public sealed class ProgressDisplayFormatterTests
{
    [Fact]
    public void Format_ShowsProgressAndTotalWhenTotalIsKnown()
    {
        var progress = ProgressDisplayFormatter.Format(12, "84");

        Assert.Equal("12/84", progress);
    }

    [Fact]
    public void Format_ShowsOnlyProgressWhenTotalIsUnknown()
    {
        var progress = ProgressDisplayFormatter.Format(12, "?");

        Assert.Equal("12", progress);
    }

    [Fact]
    public void Format_ShowsOnlyProgressWhenTotalIsMissing()
    {
        var progress = ProgressDisplayFormatter.Format(12, "—");

        Assert.Equal("12", progress);
    }
}
