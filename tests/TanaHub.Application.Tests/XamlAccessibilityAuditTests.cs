using System.Text.RegularExpressions;

namespace TanaHub.Application.Tests;

public sealed partial class XamlAccessibilityAuditTests
{
    [Fact]
    public void FocusVisibleStyles_DoNotHideKeyboardFocus()
    {
        var issues = ReadXamlFiles()
            .SelectMany(file => FocusSuppressionRegex()
                .Matches(file.Content)
                .Select(match => $"{file.Path}:{GetLine(file.Content, match.Index)}"))
            .ToArray();

        Assert.Empty(issues);
    }

    [Fact]
    public void IconOnlyButtons_HaveTooltips()
    {
        var issues = ReadXamlFiles()
            .SelectMany(file => ButtonRegex()
                .Matches(file.Content)
                .Where(match => IsIconOnlyButton(match.Value) && !match.Value.Contains("ToolTip.Tip=", StringComparison.Ordinal))
                .Select(match => $"{file.Path}:{GetLine(file.Content, match.Index)}"))
            .ToArray();

        Assert.Empty(issues);
    }

    private static IEnumerable<(string Path, string Content)> ReadXamlFiles()
    {
        var root = FindRepositoryRoot();
        return Directory
            .EnumerateFiles(System.IO.Path.Combine(root, "src", "TanaHub.UI", "Views"), "*.axaml", SearchOption.AllDirectories)
            .Order(StringComparer.Ordinal)
            .Select(path => (System.IO.Path.GetRelativePath(root, path), File.ReadAllText(path)));
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (Directory.Exists(System.IO.Path.Combine(directory.FullName, "src", "TanaHub.UI", "Views")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Repository root was not found.");
    }

    private static bool IsIconOnlyButton(string buttonMarkup)
    {
        return buttonMarkup.Contains("material:MaterialIcon", StringComparison.Ordinal) &&
            !buttonMarkup.Contains("<TextBlock", StringComparison.Ordinal);
    }

    private static int GetLine(string content, int index)
    {
        return content[..index].Count(character => character == '\n') + 1;
    }

    [GeneratedRegex("""<Style\s+Selector="[^"]*:focus-visible[^"]*"[^>]*>[\s\S]*?<Setter\s+Property="Background"\s+Value="Transparent"\s*/>[\s\S]*?</Style>""")]
    private static partial Regex FocusSuppressionRegex();

    [GeneratedRegex("""<Button\b[^>]*>[\s\S]*?</Button>""")]
    private static partial Regex ButtonRegex();
}
