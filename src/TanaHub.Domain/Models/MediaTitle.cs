namespace TanaHub.Domain.Models;

public sealed record MediaTitle(
    string Romaji,
    string? English = null,
    string? Native = null)
{
    public string DisplayTitle => English ?? Romaji;
}
