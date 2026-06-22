using Avalonia.Controls;
using Avalonia.Interactivity;
using TanaHub.UI.ViewModels;

namespace TanaHub.UI.Views;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private async void SearchBox_OnLostFocus(object? sender, RoutedEventArgs e)
    {
        // Small delay lets a click on a dropdown item register before the popup closes.
        await System.Threading.Tasks.Task.Delay(200);
        if (DataContext is MainWindowViewModel vm)
            vm.IsSearchDropdownOpen = false;
    }
}
