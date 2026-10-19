using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Material.Icons;

namespace TanaHub.UI.ViewModels;

public sealed partial class NotificationViewModel : ObservableObject, IDisposable
{
    private readonly Func<TimeSpan, CancellationToken, Task> delay;
    private CancellationTokenSource? expiration;
    private bool disposed;

    public NotificationViewModel() : this(Task.Delay) { }

    internal NotificationViewModel(Func<TimeSpan, CancellationToken, Task> delay) => this.delay = delay;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsVisible))]
    private string message = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Icon))]
    private bool isError;

    public bool IsVisible => !string.IsNullOrWhiteSpace(Message);
    public MaterialIconKind Icon => IsError ? MaterialIconKind.AlertCircleOutline : MaterialIconKind.CheckCircleOutline;
    internal Task Dismissal { get; private set; } = Task.CompletedTask;

    internal void Show(string text, bool error = false)
    {
        if (disposed) return;
        CancelExpiration();
        IsError = error;
        Message = text;
        if (!IsVisible) return;
        expiration = new CancellationTokenSource();
        Dismissal = ExpireAsync(expiration.Token, error ? TimeSpan.FromSeconds(8) : TimeSpan.FromSeconds(4));
    }

    private async Task ExpireAsync(CancellationToken token, TimeSpan duration)
    {
        try
        {
            await delay(duration, token);
            if (!token.IsCancellationRequested) Dismiss();
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
    }

    [RelayCommand]
    private void Dismiss()
    {
        CancelExpiration();
        Message = string.Empty;
    }

    private void CancelExpiration()
    {
        expiration?.Cancel();
        expiration?.Dispose();
        expiration = null;
    }

    public void Dispose()
    {
        disposed = true;
        Dismiss();
    }
}
