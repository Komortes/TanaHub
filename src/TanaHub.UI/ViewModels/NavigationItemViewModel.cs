namespace TanaHub.UI.ViewModels;

public sealed record NavigationItemViewModel(
    string Key,
    string Title,
    string Summary,
    string EmptyState);
