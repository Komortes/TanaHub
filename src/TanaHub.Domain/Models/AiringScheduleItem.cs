namespace TanaHub.Domain.Models;

public sealed record AiringScheduleItem(
    string MediaId,
    string Title,
    int Episode,
    DateTimeOffset AiringAt,
    string Format,
    string ReleaseStatus,
    Uri? PosterUri);
