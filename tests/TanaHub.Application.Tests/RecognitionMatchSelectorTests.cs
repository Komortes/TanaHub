using TanaHub.Application.Recognition;
using TanaHub.Application.Services;

namespace TanaHub.Application.Tests;

public sealed class RecognitionMatchSelectorTests
{
    [Fact]
    public void RankAndDedupe_ReturnsBestMatchFirstAndDropsDuplicateTitles()
    {
        var matches = new[]
        {
            new RecognitionMatch(2, "Second", null, null, "4", 0.74, null),
            new RecognitionMatch(1, "First weaker duplicate", null, null, "1", 0.81, null),
            new RecognitionMatch(1, "First best duplicate", null, null, "2", 0.93, null),
            new RecognitionMatch(3, "Third", null, null, "7", 0.69, null)
        };

        var ranked = RecognitionMatchSelector.RankAndDedupe(matches);

        Assert.Collection(
            ranked,
            match =>
            {
                Assert.Equal(1, match.AniListId);
                Assert.Equal(0.93, match.Similarity);
                Assert.Equal("First best duplicate", match.RomajiTitle);
            },
            match => Assert.Equal(2, match.AniListId),
            match => Assert.Equal(3, match.AniListId));
    }
}
