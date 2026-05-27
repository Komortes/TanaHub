using Material.Icons;

namespace TanaHub.UI.ViewModels;

public sealed record DashboardMetricViewModel(
    string Label,
    string Value,
    string Detail,
    MaterialIconKind Icon = MaterialIconKind.ChartLine);
