using TanaHub.UI.ViewModels;

namespace TanaHub.Application.Tests;

public sealed class NotificationViewModelTests
{
    [Theory]
    [InlineData(false, 4)]
    [InlineData(true, 8)]
    public async Task Feedback_ExpiresAfterItsReadingTime(bool error, int seconds)
    {
        var timer = new ManualDelay();
        using var notification = new NotificationViewModel(timer.WaitAsync);
        notification.Show("Saved", error);
        Assert.True(notification.IsVisible);
        Assert.Equal(error, notification.IsError);
        Assert.Equal(TimeSpan.FromSeconds(seconds), timer.Duration);

        timer.Pending[0].SetResult();
        await notification.Dismissal;

        Assert.False(notification.IsVisible);
        Assert.Empty(notification.Message);
    }

    [Fact]
    public async Task OlderTimer_CannotDismissReplacementEvenWhenMessageIsIdentical()
    {
        var timer = new ManualDelay();
        using var notification = new NotificationViewModel(timer.WaitAsync);
        notification.Show("Progress updated");
        var oldDismissal = notification.Dismissal;
        notification.Show("Progress updated");

        timer.Pending[0].SetResult();
        await oldDismissal;
        Assert.True(notification.IsVisible);

        timer.Pending[1].SetResult();
        await notification.Dismissal;
        Assert.False(notification.IsVisible);
    }

    [Fact]
    public async Task ManualDismissal_CancelsTimerAndDisposeIgnoresLateFeedback()
    {
        var timer = new ManualDelay();
        var notification = new NotificationViewModel(timer.WaitAsync);
        notification.Show("Saved");
        notification.DismissCommand.Execute(null);
        Assert.False(notification.IsVisible);
        Assert.True(timer.Token.IsCancellationRequested);
        timer.Pending[0].SetResult();
        await notification.Dismissal;

        notification.Dispose();
        notification.Show("Late result");
        Assert.False(notification.IsVisible);
    }

    private sealed class ManualDelay
    {
        public List<TaskCompletionSource> Pending { get; } = [];
        public TimeSpan Duration { get; private set; }
        public CancellationToken Token { get; private set; }

        public Task WaitAsync(TimeSpan duration, CancellationToken token)
        {
            Duration = duration;
            Token = token;
            var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            Pending.Add(completion);
            // Deliberately allow a callback after cancellation to exercise the stale-timer guard.
            return completion.Task;
        }
    }
}
