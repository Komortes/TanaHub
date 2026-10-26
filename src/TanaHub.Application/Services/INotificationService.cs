namespace TanaHub.Application.Services;

public interface INotificationService
{
    NotificationSupport Support { get; }

    /// <summary>True when the system notifier accepts the request; this does not confirm that it was seen.</summary>
    Task<bool> NotifyAsync(string title, string message, CancellationToken cancellationToken = default);
}
