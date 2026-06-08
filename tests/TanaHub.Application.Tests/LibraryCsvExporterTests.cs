using TanaHub.Application.Export;

namespace TanaHub.Application.Tests;

public sealed class LibraryCsvExporterTests
{
    [Fact]
    public void Export_WritesHeaderAndEscapesText()
    {
        var csv = LibraryCsvExporter.Export([
            new LibraryExportItem(
                "anilist:1",
                "Title, \"Special\"",
                "Anime",
                "Completed",
                26,
                10)
        ]);

        Assert.Contains("MediaId,Title,Type,Status,Progress,Score", csv);
        Assert.Contains("\"Title, \"\"Special\"\"\"", csv);
        Assert.Contains("anilist:1", csv);
        Assert.Contains(",26,10", csv);
    }
}
