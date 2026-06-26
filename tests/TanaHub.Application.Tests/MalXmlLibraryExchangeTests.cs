using TanaHub.Application.Export;
using TanaHub.Domain.Enums;

namespace TanaHub.Application.Tests;

public sealed class MalXmlLibraryExchangeTests
{
    [Fact]
    public void Export_WritesAnimeEntryWithMalFields()
    {
        var xml = MalXmlLibraryExchange.Export([
            new LibraryExportItem(
                "mal:anime:1",
                "Cowboy Bebop",
                "Anime",
                "Completed",
                26,
                10)
        ]);

        Assert.Contains("<myanimelist>", xml);
        Assert.Contains("<anime>", xml);
        Assert.Contains("<series_animedb_id>1</series_animedb_id>", xml);
        Assert.Contains("<series_title>Cowboy Bebop</series_title>", xml);
        Assert.Contains("<my_watched_episodes>26</my_watched_episodes>", xml);
        Assert.Contains("<my_status>Completed</my_status>", xml);
    }

    [Fact]
    public void Export_WritesZeroMalIdForNonMalEntry()
    {
        var xml = MalXmlLibraryExchange.Export([
            new LibraryExportItem(
                "anilist:1",
                "Cowboy Bebop",
                "Anime",
                "Completed",
                26,
                10)
        ]);

        Assert.Contains("<series_animedb_id>0</series_animedb_id>", xml);
    }

    [Fact]
    public void Import_ReadsAnimeEntry()
    {
        var result = MalXmlLibraryExchange.Import("""
            <myanimelist>
              <anime>
                <series_animedb_id>1</series_animedb_id>
                <series_title>Cowboy Bebop</series_title>
                <my_watched_episodes>26</my_watched_episodes>
                <my_score>10</my_score>
                <my_status>Completed</my_status>
              </anime>
            </myanimelist>
            """);

        Assert.True(result.IsSuccess);
        var entry = Assert.Single(result.Value!);
        Assert.Equal("mal:anime:1", entry.MediaId);
        Assert.Equal(MediaType.Anime, entry.MediaType);
        Assert.Equal(MediaListStatus.Completed, entry.Status);
        Assert.Equal(26, entry.Progress);
        Assert.Equal(10, entry.Score);
    }

    [Fact]
    public void Import_ReadsMangaEntry()
    {
        var result = MalXmlLibraryExchange.Import("""
            <myanimelist>
              <manga>
                <manga_mangadb_id>2</manga_mangadb_id>
                <manga_title>Yotsuba&amp;!</manga_title>
                <my_read_chapters>12</my_read_chapters>
                <my_score>9</my_score>
                <my_status>Reading</my_status>
              </manga>
            </myanimelist>
            """);

        Assert.True(result.IsSuccess);
        var entry = Assert.Single(result.Value!);
        Assert.Equal("mal:manga:2", entry.MediaId);
        Assert.Equal(MediaType.Manga, entry.MediaType);
        Assert.Equal(MediaListStatus.Current, entry.Status);
        Assert.Equal(12, entry.Progress);
        Assert.Equal(9, entry.Score);
    }

    [Fact]
    public void Import_ReturnsValidationErrorForInvalidXml()
    {
        var result = MalXmlLibraryExchange.Import("<myanimelist><anime>");

        Assert.True(result.IsFailure);
        Assert.Equal("validation_error", result.Error.Code);
    }
}
