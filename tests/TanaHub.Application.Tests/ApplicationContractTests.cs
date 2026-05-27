using TanaHub.Application.Common;
using TanaHub.Application.Queries;
using TanaHub.Domain.Enums;

namespace TanaHub.Application.Tests;

public sealed class ApplicationContractTests
{
    [Fact]
    public void Result_Success_StoresValue()
    {
        var result = Result<string>.Success("ok");

        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        Assert.Equal("ok", result.Value);
        Assert.Equal(ApplicationError.None, result.Error);
    }

    [Fact]
    public void Result_Failure_RequiresRealError()
    {
        Assert.Throws<ArgumentException>(() => Result<string>.Failure(ApplicationError.None));
    }

    [Fact]
    public void PagedResult_HasItems_ReflectsItemCount()
    {
        var result = new PagedResult<int>([1], Page: 1, PageSize: 20);

        Assert.True(result.HasItems);
    }

    [Fact]
    public void MediaSearchQuery_DefaultsToFirstPage()
    {
        var query = new MediaSearchQuery
        {
            Type = MediaType.Anime
        };

        Assert.Equal(1, query.Page);
        Assert.Equal(20, query.PageSize);
        Assert.Equal(MediaType.Anime, query.Type);
    }
}
