using Avalonia.Controls;
using Avalonia.Input;
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

    private async void SearchBox_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        viewModel.SearchText = SearchBox.Text ?? string.Empty;

        if (viewModel.SearchCommand.CanExecute(null))
        {
            await viewModel.SearchCommand.ExecuteAsync(null);
        }

        e.Handled = true;
    }
}
