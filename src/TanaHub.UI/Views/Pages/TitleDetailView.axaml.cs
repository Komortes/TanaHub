using Avalonia.Controls;

namespace TanaHub.UI.Views.Pages;

public partial class TitleDetailView : UserControl
{
    private (bool Compact, bool HasSynopsis)? _layoutState;

    public TitleDetailView()
    {
        InitializeComponent();
        SizeChanged += (_, _) => UpdateResponsiveLayout();
        Loaded += (_, _) => UpdateResponsiveLayout();
        DetailSynopsis.PropertyChanged += (_, change) =>
        {
            if (change.Property == IsVisibleProperty) UpdateResponsiveLayout();
        };
    }

    private void UpdateResponsiveLayout()
    {
        if (Bounds.Width <= 0) return;
        var compact = Bounds.Width <= 760;
        var hasSynopsis = DetailSynopsis.IsVisible;
        if (_layoutState == (compact, hasSynopsis)) return;
        _layoutState = (compact, hasSynopsis);

        // Grid definitions are CLR properties; change layout without replacing the controls.
        TrackingControls.ColumnDefinitions = new ColumnDefinitions(compact ? "*" : "Auto,*");
        TrackingControls.RowDefinitions = new RowDefinitions(compact ? "Auto,Auto" : "Auto");
        Grid.SetColumn(ProgressControls, compact ? 0 : 1);
        Grid.SetRow(ProgressControls, compact ? 1 : 0);

        DetailOverview.ColumnDefinitions = new ColumnDefinitions(compact || !hasSynopsis ? "*" : "*,260");
        DetailOverview.RowDefinitions = new RowDefinitions(compact && hasSynopsis ? "Auto,Auto" : "Auto");
        Grid.SetColumn(DetailFacts, compact || !hasSynopsis ? 0 : 1);
        Grid.SetRow(DetailFacts, compact && hasSynopsis ? 1 : 0);

        OrganizeFields.ColumnDefinitions = new ColumnDefinitions(compact ? "*" : "*,*");
        OrganizeFields.RowDefinitions = new RowDefinitions(compact ? "Auto,Auto,Auto" : "Auto,Auto");
        Grid.SetColumn(ReviewFields, compact ? 0 : 1);
        Grid.SetRow(ReviewFields, compact ? 2 : 0);
        Grid.SetRowSpan(ReviewFields, compact ? 1 : 2);
    }
}
