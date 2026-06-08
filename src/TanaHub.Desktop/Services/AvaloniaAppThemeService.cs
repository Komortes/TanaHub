using Avalonia;
using Avalonia.Media;
using TanaHub.UI.Services;

namespace TanaHub.Desktop.Services;

public sealed class AvaloniaAppThemeService : IAppThemeService
{
    private static readonly IReadOnlyDictionary<string, string> NebulaPalette =
        new Dictionary<string, string>
        {
            ["TanaHubBackgroundBrush"] = "#09080F",
            ["TanaHubSurfaceBrush"] = "#120F1C",
            ["TanaHubSurfaceRaisedBrush"] = "#1A1628",
            ["TanaHubSurfaceElevatedBrush"] = "#201C30",
            ["TanaHubSurfaceHoverBrush"] = "#271F38",
            ["TanaHubBorderBrush"] = "#2C2244",
            ["TanaHubBorderLightBrush"] = "#3C2E58",
            ["TanaHubAccentBrush"] = "#9B6EF8",
            ["TanaHubAccentLightBrush"] = "#C8B8FF",
            ["TanaHubAccentGlowBrush"] = "#2E9B6EF8",
            ["TanaHubAccentGlowStrongBrush"] = "#489B6EF8",
            ["TanaHubTextPrimaryBrush"] = "#EDE9FF",
            ["TanaHubTextSecondaryBrush"] = "#A99EC0",
            ["TanaHubTextMutedBrush"] = "#6B6080"
        };

    private static readonly IReadOnlyDictionary<string, string> HighContrastPalette =
        new Dictionary<string, string>
        {
            ["TanaHubBackgroundBrush"] = "#000000",
            ["TanaHubSurfaceBrush"] = "#080808",
            ["TanaHubSurfaceRaisedBrush"] = "#141414",
            ["TanaHubSurfaceElevatedBrush"] = "#202020",
            ["TanaHubSurfaceHoverBrush"] = "#2A2A2A",
            ["TanaHubBorderBrush"] = "#737373",
            ["TanaHubBorderLightBrush"] = "#A3A3A3",
            ["TanaHubAccentBrush"] = "#C4B5FD",
            ["TanaHubAccentLightBrush"] = "#FFFFFF",
            ["TanaHubAccentGlowBrush"] = "#3DC4B5FD",
            ["TanaHubAccentGlowStrongBrush"] = "#5CC4B5FD",
            ["TanaHubTextPrimaryBrush"] = "#FFFFFF",
            ["TanaHubTextSecondaryBrush"] = "#E5E5E5",
            ["TanaHubTextMutedBrush"] = "#BDBDBD"
        };

    public void Apply(string theme)
    {
        var application = Avalonia.Application.Current;
        if (application is null)
        {
            return;
        }

        var palette = theme == "High contrast" ? HighContrastPalette : NebulaPalette;
        foreach (var (key, color) in palette)
        {
            application.Resources[key] = new SolidColorBrush(Color.Parse(color));
        }
    }
}
