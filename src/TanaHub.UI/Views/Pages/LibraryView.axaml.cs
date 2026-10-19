using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.VisualTree;

namespace TanaHub.UI.Views.Pages;

public sealed partial class LibraryView : UserControl
{
    private readonly StackPanel? libraryRowsHost;
    private readonly Border? libraryColumnHeader;
    private readonly Border? librarySidebar;
    private readonly Border? libraryCompactFilters;

    public LibraryView()
    {
        InitializeComponent();
        libraryRowsHost = this.FindControl<StackPanel>("LibraryRowsHost");
        libraryColumnHeader = this.FindControl<Border>("LibraryColumnHeader");
        librarySidebar = this.FindControl<Border>("LibrarySidebar");
        libraryCompactFilters = this.FindControl<Border>("LibraryCompactFilters");
        Loaded += (_, _) =>
        {
            UpdatePageLayout();
            UpdateContentLayout();
        };
    }

    private void OnPageSizeChanged(object? sender, SizeChangedEventArgs e) => UpdatePageLayout();

    private void UpdatePageLayout()
    {
        if (LibraryPageFrame.Bounds.Width <= 0 || libraryRowsHost is null ||
            librarySidebar is null || libraryCompactFilters is null) return;

        // Set every affected property together so expanding restores the full desktop layout.
        var compact = LibraryPageFrame.Bounds.Width <= 850;
        var expectedColumns = compact ? 1 : 2;
        if (LibraryLayout.ColumnDefinitions.Count != expectedColumns)
            LibraryLayout.ColumnDefinitions = new ColumnDefinitions(compact ? "*" : "220,*");

        LibraryLayout.ColumnSpacing = compact ? 0 : 20;
        LibraryLayout.RowSpacing = compact ? 12 : 0;
        librarySidebar.IsVisible = !compact;
        Grid.SetRowSpan(librarySidebar, compact ? 1 : 2);
        libraryCompactFilters.IsVisible = compact;
        Grid.SetColumn(libraryRowsHost, compact ? 0 : 1);
        Grid.SetRow(libraryRowsHost, compact ? 1 : 0);
        Grid.SetColumnSpan(libraryRowsHost, 1);
        Grid.SetRowSpan(libraryRowsHost, 1);
    }

    private void OnEntryLoaded(object? sender, RoutedEventArgs e) => UpdateEntryLayout(sender);

    private void OnEntrySizeChanged(object? sender, SizeChangedEventArgs e) => UpdateEntryLayout(sender);

    private void OnContentSizeChanged(object? sender, SizeChangedEventArgs e) => UpdateContentLayout();

    private void UpdateContentLayout()
    {
        if (libraryRowsHost is null || libraryRowsHost.Bounds.Width <= 0) return;

        var compact = libraryRowsHost.Bounds.Width <= 700;
        if (libraryColumnHeader is not null) libraryColumnHeader.IsVisible = !compact;
        foreach (var entry in libraryRowsHost.GetVisualDescendants().OfType<Grid>()
                     .Where(grid => grid.Classes.Contains("library-entry")))
            UpdateEntryLayout(entry);
    }

    private void UpdateEntryLayout(object? sender)
    {
        if (sender is not Grid entry || libraryRowsHost is null || libraryRowsHost.Bounds.Width <= 0) return;

        var compact = libraryRowsHost.Bounds.Width <= 700;
        var expectedColumns = compact ? 4 : 5;
        if (entry.ColumnDefinitions.Count != expectedColumns)
            entry.ColumnDefinitions = new ColumnDefinitions(compact ? "36,*,44,108" : "36,*,108,44,108");

        entry.RowSpacing = compact ? 6 : 0;
        foreach (var child in entry.Children)
        {
            if (child.Classes.Contains("library-entry-poster"))
            {
                Grid.SetRowSpan(child, compact ? 2 : 1);
                child.VerticalAlignment = compact ? VerticalAlignment.Top : VerticalAlignment.Stretch;
            }
            else if (child.Classes.Contains("library-entry-title"))
                Grid.SetColumnSpan(child, compact ? 3 : 1);
            else if (child.Classes.Contains("library-entry-progress"))
            {
                Grid.SetColumn(child, compact ? 1 : 2);
                Grid.SetRow(child, compact ? 1 : 0);
            }
            else if (child.Classes.Contains("library-entry-score"))
            {
                Grid.SetColumn(child, compact ? 2 : 3);
                Grid.SetRow(child, compact ? 1 : 0);
            }
            else if (child.Classes.Contains("library-entry-actions"))
            {
                Grid.SetColumn(child, compact ? 3 : 4);
                Grid.SetRow(child, compact ? 1 : 0);
            }
        }
    }
}
