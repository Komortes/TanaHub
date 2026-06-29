using TanaHub.Domain.Enums;
using TanaHub.Domain.Models;

namespace TanaHub.Application.Recommendations;

public sealed record LibraryRecommendationGenre(string Name, int Weight);

public static class LibraryRecommendationBuilder
{
    public static IReadOnlyList<LibraryRecommendationGenre> BuildGenreProfile(
        IEnumerable<UserMediaEntry> entries,
        IReadOnlyDictionary<string, MediaItem> mediaById,
        int maxGenres = 3)
    {
        var weights = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in entries)
        {
            if (!mediaById.TryGetValue(entry.MediaId, out var media))
            {
                continue;
            }

            var entryWeight = GetEntryWeight(entry);
            if (entryWeight <= 0)
            {
                continue;
            }

            foreach (var genre in media.Genres.Where(genre => !string.IsNullOrWhiteSpace(genre)))
            {
                weights[genre] = weights.GetValueOrDefault(genre) + entryWeight;
            }
        }

        return weights
            .OrderByDescending(pair => pair.Value)
            .ThenBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .Take(Math.Max(0, maxGenres))
            .Select(pair => new LibraryRecommendationGenre(pair.Key, pair.Value))
            .ToArray();
    }

    public static IReadOnlyList<MediaItem> RankCandidates(
        IEnumerable<UserMediaEntry> entries,
        IReadOnlyList<LibraryRecommendationGenre> genreProfile,
        IEnumerable<MediaItem> candidates,
        int maxItems = 8)
    {
        var existingIds = entries
            .Select(entry => entry.MediaId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var genreWeights = genreProfile.ToDictionary(
            genre => genre.Name,
            genre => genre.Weight,
            StringComparer.OrdinalIgnoreCase);

        return candidates
            .Where(candidate => !existingIds.Contains(candidate.Id))
            .Select(candidate => new
            {
                Item = candidate,
                Score = candidate.Genres.Sum(genre => genreWeights.GetValueOrDefault(genre))
            })
            .Where(candidate => candidate.Score > 0)
            .OrderByDescending(candidate => candidate.Score)
            .ThenByDescending(candidate => candidate.Item.AverageScore ?? 0)
            .ThenBy(candidate => candidate.Item.Title.DisplayTitle, StringComparer.OrdinalIgnoreCase)
            .Take(Math.Max(0, maxItems))
            .Select(candidate => candidate.Item)
            .ToArray();
    }

    private static int GetEntryWeight(UserMediaEntry entry)
    {
        var statusWeight = entry.Status switch
        {
            MediaListStatus.Completed => 4,
            MediaListStatus.Current => 3,
            MediaListStatus.Repeating => 3,
            MediaListStatus.Paused => 1,
            MediaListStatus.Planning => 1,
            _ => 0
        };

        if (statusWeight == 0)
        {
            return 0;
        }

        return statusWeight + (entry.Score ?? 0);
    }
}
