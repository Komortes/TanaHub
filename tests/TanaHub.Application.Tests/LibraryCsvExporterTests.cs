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
            {
                Tags = ["rewatch", "comfort"],
                CustomLists = ["Friday queue"]
            }
        ]);

        Assert.Contains("MediaId,Title,Type,Status,Progress,Score,Tags,CustomLists", csv);
        Assert.Contains("\"Title, \"\"Special\"\"\"", csv);
        Assert.Contains("anilist:1", csv);
        Assert.Contains(",26,10,rewatch; comfort,Friday queue", csv);
    }

    [Theory]
    [InlineData("=WEBSERVICE(\"https://example.test\")")]
    [InlineData("+1+1")]
    [InlineData("-2+3")]
    [InlineData("@SUM(1,2)")]
    public void Export_NeutralizesSpreadsheetFormulas(string title)
    {
        var csv = LibraryCsvExporter.Export([
            new LibraryExportItem("anilist:1", title, "Anime", "Planning", 0, null)
        ]);

        Assert.Contains($"'{title.Replace("\"", "\"\"")}", csv);
    }
}
