using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Input;
using Avalonia.Threading;
using System.ComponentModel;
using TanaHub.UI.ViewModels;

namespace TanaHub.UI.Views;

public sealed partial class MainWindow : Window
{
    private MainWindowViewModel? observedViewModel;

    public MainWindow()
    {
        InitializeComponent();
        DetailContent.PropertyChanged += OnDetailContentChanged;
        DataContextChanged += (_, _) => ObserveNavigation();
        Closed += (_, _) => StopObservingNavigation();
    }

    public MainWindow(MainWindowViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }

    private void OnDetailContentChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property != ContentControl.ContentProperty || e.NewValue is not MediaDetailViewModel title) return;
        Dispatcher.UIThread.Post(() =>
        {
            if (DataContext is MainWindowViewModel vm && ReferenceEquals(vm.SelectedMedia, title))
                MainContentScroll.ScrollToHome();
        }, DispatcherPriority.Loaded);
    }

    private void ObserveNavigation()
    {
        StopObservingNavigation();
        observedViewModel = DataContext as MainWindowViewModel;
        if (observedViewModel is not null)
            observedViewModel.PropertyChanged += OnPageChanged;
    }

    private void StopObservingNavigation()
    {
        if (observedViewModel is not null)
            observedViewModel.PropertyChanged -= OnPageChanged;
        observedViewModel = null;
    }

    private void OnPageChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.CurrentPageTitle))
            Dispatcher.UIThread.Post(() => MainContentScroll.ScrollToHome(), DispatcherPriority.Loaded);
    }

    private async void SearchBox_OnLostFocus(object? sender, RoutedEventArgs e)
    {
        // Small delay lets a click on a dropdown item register before the popup closes.
        await System.Threading.Tasks.Task.Delay(200);
        if (DataContext is MainWindowViewModel vm)
            vm.IsSearchDropdownOpen = false;
    }

    private void Navigation_OnTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm &&
            e.Source is StyledElement { DataContext: NavigationItemViewModel item })
            vm.NavigateToPageCommand.Execute(item.Key);
    }
}
