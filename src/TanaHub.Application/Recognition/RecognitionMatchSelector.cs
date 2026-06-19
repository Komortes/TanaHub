using TanaHub.Application.Services;

namespace TanaHub.Application.Recognition;

public static class RecognitionMatchSelector
{
    public static IReadOnlyList<RecognitionMatch> RankAndDedupe(IEnumerable<RecognitionMatch> matches)
    {
        return matches
            .GroupBy(match => match.AniListId)
            .Select(group => group.OrderByDescending(match => match.Similarity).First())
            .OrderByDescending(match => match.Similarity)
            .ToArray();
    }
}
