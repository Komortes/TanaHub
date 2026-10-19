using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.VisualTree;

namespace TanaHub.UI.Views.Pages;

public sealed partial class ScheduleView : UserControl
{
    private readonly StackPanel? scheduleRowsHost;
    private readonly StackPanel? scheduleSidebar;
    private readonly Border? scheduleLegendPanel;
    private readonly WrapPanel? scheduleLegend;
    private readonly TextBlock? scheduleLegendHeading;

    public ScheduleView()
    {
        InitializeComponent();
        scheduleRowsHost = this.FindControl<StackPanel>("ScheduleRowsHost");
        scheduleSidebar = this.FindControl<StackPanel>("ScheduleSidebar");
        scheduleLegendPanel = this.FindControl<Border>("ScheduleLegendPanel");
        scheduleLegend = this.FindControl<WrapPanel>("ScheduleLegend");
        scheduleLegendHeading = this.FindControl<TextBlock>("ScheduleLegendHeading");
        Loaded += (_, _) =>
        {
            UpdatePageLayout();
            UpdateContentLayout();
        };
    }

    private void OnPageSizeChanged(object? sender, SizeChangedEventArgs e) => UpdatePageLayout();

    private void UpdatePageLayout()
    {
        if (SchedulePageFrame.Bounds.Width <= 0 || scheduleRowsHost is null || scheduleSidebar is null) return;

        // Set every affected property together so expanding restores the full desktop layout.
        var compact = SchedulePageFrame.Bounds.Width <= 850;
        var expectedColumns = compact ? 1 : 2;
        if (ScheduleLayout.ColumnDefinitions.Count != expectedColumns)
            ScheduleLayout.ColumnDefinitions = new ColumnDefinitions(compact ? "*" : "220,*");

        ScheduleLayout.ColumnSpacing = compact ? 0 : 20;
        ScheduleLayout.RowSpacing = compact ? 16 : 0;
        Grid.SetRowSpan(scheduleSidebar, compact ? 1 : 2);
        Grid.SetColumn(scheduleRowsHost, compact ? 0 : 1);
        Grid.SetRow(scheduleRowsHost, compact ? 1 : 0);
        Grid.SetColumnSpan(scheduleRowsHost, 1);
        Grid.SetRowSpan(scheduleRowsHost, 1);

        var expectedFilterColumns = compact ? 4 : 1;
        if (ScheduleRangeOptions.ColumnDefinitions.Count != expectedFilterColumns)
        {
            ScheduleRangeOptions.ColumnDefinitions = new ColumnDefinitions(compact ? "*,*,*,*" : "*");
            ScheduleRangeOptions.RowDefinitions = new RowDefinitions(compact ? "Auto" : "Auto,Auto,Auto,Auto");
        }
        ScheduleRangeOptions.ColumnSpacing = compact ? 8 : 0;
        ScheduleRangeOptions.RowSpacing = compact ? 0 : 10;
        for (var index = 0; index < ScheduleRangeOptions.Children.Count; index++)
        {
            var option = ScheduleRangeOptions.Children[index];
            Grid.SetColumn(option, compact ? index : 0);
            Grid.SetRow(option, compact ? 0 : index);
        }

        if (scheduleLegend is not null) scheduleLegend.Orientation = compact ? Orientation.Horizontal : Orientation.Vertical;
        if (scheduleLegendHeading is not null) scheduleLegendHeading.IsVisible = !compact;
        if (scheduleLegendPanel is not null)
        {
            if (compact) scheduleLegendPanel.Padding = new Thickness(16, 10);
            else scheduleLegendPanel.ClearValue(Border.PaddingProperty);
        }
    }

    private void OnEntryLoaded(object? sender, RoutedEventArgs e) => UpdateEntryLayout(sender);

    private void OnEntrySizeChanged(object? sender, SizeChangedEventArgs e) => UpdateEntryLayout(sender);

    private void OnContentSizeChanged(object? sender, SizeChangedEventArgs e) => UpdateContentLayout();

    private void UpdateContentLayout()
    {
        if (scheduleRowsHost is null || scheduleRowsHost.Bounds.Width <= 0) return;

        foreach (var entry in scheduleRowsHost.GetVisualDescendants().OfType<Grid>()
                     .Where(grid => grid.Classes.Contains("schedule-entry")))
            UpdateEntryLayout(entry);
    }

    private void UpdateEntryLayout(object? sender)
    {
        if (sender is not Grid entry || scheduleRowsHost is null || scheduleRowsHost.Bounds.Width <= 0) return;

        var compact = scheduleRowsHost.Bounds.Width <= 720;
        var expectedColumns = compact ? 4 : 5;
        if (entry.ColumnDefinitions.Count != expectedColumns)
            entry.ColumnDefinitions = new ColumnDefinitions(compact ? "80,38,*,Auto" : "80,38,*,Auto,Auto");

        foreach (var child in entry.Children)
        {
            if (child.Classes.Contains("schedule-match-chip")) child.IsVisible = !compact;
            else if (child.Classes.Contains("schedule-open")) Grid.SetColumn(child, compact ? 3 : 4);
            else if (child is StackPanel title)
            {
                foreach (var line in title.Children.Where(line => line.Classes.Contains("schedule-inline-match")))
                    line.IsVisible = compact;
            }
        }
    }
}
