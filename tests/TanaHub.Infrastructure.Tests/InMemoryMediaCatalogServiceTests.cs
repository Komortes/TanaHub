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
    public async Task GetByIdAsync_ReturnsNotFoundForUnknownId()
    {
        var service = new InMemoryMediaCatalogService();

        var result = await service.GetByIdAsync("missing");

        Assert.True(result.IsFailure);
        Assert.Equal("not_found", result.Error.Code);
    }
}
