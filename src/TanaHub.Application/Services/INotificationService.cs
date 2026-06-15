namespace TanaHub.Application.Services;

public interface INotificationService
{
    Task NotifyAsync(string title, string message, CancellationToken cancellationToken = default);
}
