namespace TanaHub.Domain.Models;

public sealed record MediaImages(
    Uri? PosterUri = null,
    Uri? BannerUri = null,
    Uri? ThumbnailUri = null);
