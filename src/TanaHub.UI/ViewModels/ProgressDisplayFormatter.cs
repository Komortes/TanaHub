namespace TanaHub.UI.ViewModels;

public static class ProgressDisplayFormatter
{
    public static string Format(int progress, string? total)
    {
        return int.TryParse(total, out var parsedTotal) && parsedTotal > 0
            ? $"{progress}/{parsedTotal}"
            : progress.ToString();
    }
}
