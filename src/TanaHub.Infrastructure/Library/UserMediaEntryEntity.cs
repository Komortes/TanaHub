namespace TanaHub.Infrastructure.Library;

public sealed class UserMediaEntryEntity
{
    public string MediaId { get; set; } = string.Empty;
    public string MediaType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int Progress { get; set; }
    public int? Score { get; set; }
    public string? PosterUri { get; set; }
    public string? Notes { get; set; }
    public string? Review { get; set; }
    public List<string> Tags { get; set; } = [];
    public List<string> CustomLists { get; set; } = [];
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
