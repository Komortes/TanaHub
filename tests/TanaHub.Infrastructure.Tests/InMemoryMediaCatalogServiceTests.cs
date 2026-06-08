using TanaHub.Application.Queries;
using TanaHub.Domain.Enums;
using TanaHub.Infrastructure.Catalog;

namespace TanaHub.Infrastructure.Tests;

public sealed class InMemoryMediaCatalogServiceTests
{
    [Fact]
    public async Task SearchAsync_FiltersBySearchText()
    {
        var service = new InMemoryMediaCatalogService();

        var result = await service.SearchAsync(new MediaSearchQuery
        {
            SearchText = "Frieren"
        });

        Assert.True(result.IsSuccess);
        var item = Assert.Single(result.Value!.Items);
        Assert.Equal("anilist:154587", item.Id);
    }

    [Fact]
    public async Task SearchAsync_FiltersByMediaType()
    {
        var service = new InMemoryMediaCatalogService();

        var result = await service.SearchAsync(new MediaSearchQuery
        {
            Type = MediaType.Manga
        });

        Assert.True(result.IsSuccess);
        Assert.All(result.Value!.Items, item => Assert.Equal(MediaType.Manga, item.Type));
    }

    [Fact]
    public async Task SearchAsync_FiltersByGenre()
    {
        var service = new InMemoryMediaCatalogService();

        var result = await service.SearchAsync(new MediaSearchQuery
        {
            Genres = ["Sci-Fi"]
        });

        Assert.True(result.IsSuccess);
        var item = Assert.Single(result.Value!.Items);
        Assert.Equal("anilist:1", item.Id);
    }

    [Fact]
    public async Task SearchAsync_SortsByScoreDescending()
    {
        var service = new InMemoryMediaCatalogService();

        var result = await service.SearchAsync(new MediaSearchQuery
        {
            Sort = MediaSearchSort.Score
        });

        Assert.True(result.IsSuccess);
        Assert.Equal("mangadex:berserk", result.Value!.Items[0].Id);
        Assert.Equal("anilist:154587", result.Value.Items[1].Id);
    }

    [Fact]
    public async Task SearchAsync_SortsNewestFirst()
    {
        var service = new InMemoryMediaCatalogService();

        var result = await service.SearchAsync(new MediaSearchQuery
        {
            Sort = MediaSearchSort.Newest
        });

        Assert.True(result.IsSuccess);
        Assert.Equal("anilist:154587", result.Value!.Items[0].Id);
        Assert.Equal("mangadex:solo-leveling", result.Value.Items[1].Id);
    }

    [Fact]
    public async Task SearchAsync_ReturnsRequestedPageAndPaginationMetadata()
    {
        var service = new InMemoryMediaCatalogService();

        var result = await service.SearchAsync(new MediaSearchQuery
        {
            Page = 2,
            PageSize = 2
        });

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.Items.Count);
        Assert.Equal(5, result.Value.TotalCount);
        Assert.True(result.Value.HasNextPage);
    }

    [Fact]
    public async Task SearchAsync_FiltersManhwaByCountryCode()
    {
        var service = new InMemoryMediaCatalogService();

        var result = await service.SearchAsync(new MediaSearchQuery
        {
            Type = MediaType.Manga,
            CountryCode = "KR"
        });

        Assert.True(result.IsSuccess);
        var item = Assert.Single(result.Value!.Items);
        Assert.Equal("mangadex:solo-leveling", item.Id);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNotFoundForUnknownId()
    {
        var service = new InMemoryMediaCatalogService();

        var result = await service.GetByIdAsync("missing");

        Assert.True(result.IsFailure);
        Assert.Equal("not_found", result.Error.Code);
    }
}
