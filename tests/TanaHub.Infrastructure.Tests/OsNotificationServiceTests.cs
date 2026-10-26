using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using TanaHub.Infrastructure.Notifications;

namespace TanaHub.Infrastructure.Tests;

public sealed class OsNotificationServiceTests
{
    [Fact]
    public async Task WindowsIsUnsupportedAndNeverStartsANotifier()
    {
        var calls = 0;
        var service = new OsNotificationService(platform => platform == OSPlatform.Windows, (_, _) =>
        {
            calls++;
            return Task.FromResult<int?>(0);
        });

        Assert.False(service.Support.Supported);
        var reason = service.Support.Reason;
        Assert.NotNull(reason);
        Assert.Contains("Windows", reason);
        Assert.Contains("this build", reason);
        Assert.False(await service.NotifyAsync("TanaHub", "Airing today"));
        Assert.Equal(0, calls);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SupportedPlatformsSendSanitizedTextAsSeparateArguments(bool macOs)
    {
        ProcessStartInfo? captured = null;
        var service = new OsNotificationService(
            platform => platform == (macOs ? OSPlatform.OSX : OSPlatform.Linux),
            (start, _) =>
            {
                captured = start;
                return Task.FromResult<int?>(0);
            });

        var accepted = await service.NotifyAsync("TanaHub \"title\"\n\0", "EP 4 of Frieren — \"$(not-a-command)\" \r\n\u007F");

        Assert.True(accepted);
        Assert.True(service.Support.Supported);
        Assert.Null(service.Support.Reason);
        Assert.NotNull(captured);
        Assert.Equal(macOs ? "osascript" : "notify-send", Path.GetFileName(captured.FileName));
        Assert.False(captured.UseShellExecute);
        Assert.True(captured.CreateNoWindow);
        Assert.True(captured.RedirectStandardOutput);
        Assert.True(captured.RedirectStandardError);
        Assert.Empty(captured.Arguments);
        var arguments = captured.ArgumentList.ToArray();
        Assert.Equal("TanaHub \"title\"", macOs ? arguments[^1] : arguments[^2]);
        Assert.Equal("EP 4 of Frieren — \"$(not-a-command)\" ", macOs ? arguments[^2] : arguments[^1]);
        if (macOs)
        {
            Assert.Contains("display notification (item 1 of argv) with title (item 2 of argv)", arguments);
            Assert.DoesNotContain("TanaHub \"title\"", string.Join('\n', arguments[..^2]));
            Assert.DoesNotContain("$(not-a-command)", string.Join('\n', arguments[..^2]));
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(42)]
    [InlineData(null)]
    public async Task MissingProcessOrNonzeroExitNeverClaimsTheNotificationWasAccepted(int? exitCode)
    {
        var service = new OsNotificationService(platform => platform == OSPlatform.Linux,
            (_, _) => Task.FromResult(exitCode));

        Assert.False(await service.NotifyAsync("TanaHub", "Airing today"));
    }

    [Fact]
    public async Task MissingNotifierExecutableReturnsFailureWithoutThrowing()
    {
        var service = new OsNotificationService(platform => platform == OSPlatform.Linux,
            (_, _) => throw new Win32Exception("The notifier executable was not found."));

        Assert.False(await service.NotifyAsync("TanaHub", "Airing today"));
    }

    [Fact]
    public async Task ACancelledCallerNeverStartsANotifier()
    {
        var calls = 0;
        var service = new OsNotificationService(platform => platform == OSPlatform.Linux, (_, _) =>
        {
            calls++;
            return Task.FromResult<int?>(0);
        });
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.NotifyAsync("TanaHub", "Airing today", cancellation.Token));

        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task CallerCancellationReachesTheRunningNotifierAndRemainsCancellation()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken helperCancellation = default;
        var service = new OsNotificationService(platform => platform == OSPlatform.Linux, async (_, cancellationToken) =>
        {
            helperCancellation = cancellationToken;
            started.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return 0;
        });
        using var cancellation = new CancellationTokenSource();
        var sending = service.NotifyAsync("TanaHub", "Airing today", cancellation.Token);
        try
        {
            await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            cancellation.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => sending.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.True(helperCancellation.IsCancellationRequested);
        }
        finally { cancellation.Cancel(); }
    }

    [Fact]
    public async Task CallerCancellationTakesPrecedenceOverARunnerFault()
    {
        using var cancellation = new CancellationTokenSource();
        var service = new OsNotificationService(platform => platform == OSPlatform.Linux, (_, _) =>
        {
            cancellation.Cancel();
            return Task.FromException<int?>(new IOException("A notifier pipe closed during cancellation."));
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.NotifyAsync("TanaHub", "Airing today", cancellation.Token));
    }

    [Fact]
    public async Task AHungNotifierIsBoundedAndTheNextExplicitAttemptCanSucceed()
    {
        var attempts = 0;
        CancellationToken helperCancellation = default;
        var held = new TaskCompletionSource<int?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var service = new OsNotificationService(platform => platform == OSPlatform.Linux, (_, cancellationToken) =>
        {
            attempts++;
            helperCancellation = cancellationToken;
            return attempts == 1 ? held.Task : Task.FromResult<int?>(0);
        }, TimeSpan.FromMilliseconds(30));
        try
        {
            Assert.False(await service.NotifyAsync("TanaHub", "Airing today").WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.True(helperCancellation.IsCancellationRequested);
            Assert.True(await service.NotifyAsync("TanaHub", "Airing today").WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.Equal(2, attempts);
        }
        finally { held.TrySetResult(0); }
    }

    [Fact]
    public async Task NativeRunnerPreservesAnUnsuccessfulExitCode()
    {
        if (!OperatingSystem.IsMacOS() && !OperatingSystem.IsLinux()) return;
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var start = StartInfo("/bin/sh", "-c", "exit 7");

        var exitCode = await OsNotificationService.RunProcessAsync(start, cancellation.Token);

        Assert.Equal<int?>(7, exitCode);
    }

    [Fact]
    public async Task NativeRunnerKillsAndReapsItsOwnedChildOnCancellation()
    {
        if (!OperatingSystem.IsMacOS() && !OperatingSystem.IsLinux()) return;
        var directory = Path.Combine(Path.GetTempPath(), "TanaHub.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var pidFile = Path.Combine(directory, "notifier-test.pid");
        var start = StartInfo("/bin/sh", "-c", "printf '%s' \"$$\" > \"$1\"; exec /bin/sleep 30", "tanahub-notification-test", pidFile);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var running = OsNotificationService.RunProcessAsync(start, cancellation.Token);
        Process? child = null;
        try
        {
            int pid;
            while (!File.Exists(pidFile) || !int.TryParse(await File.ReadAllTextAsync(pidFile, cancellation.Token), out pid))
                await Task.Delay(20, cancellation.Token);
            child = Process.GetProcessById(pid);
            cancellation.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => running.WaitAsync(TimeSpan.FromSeconds(5)));
            await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
            Assert.True(child.HasExited);
        }
        finally
        {
            cancellation.Cancel();
            try { await running.WaitAsync(TimeSpan.FromSeconds(5)); }
            catch (OperationCanceledException) { }
            finally
            {
                try
                {
                    if (child is not null)
                    {
                        if (!child.HasExited) child.Kill(entireProcessTree: true);
                        await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
                    }
                }
                finally
                {
                    child?.Dispose();
                    Directory.Delete(directory, recursive: true);
                }
            }
        }
    }

    private static ProcessStartInfo StartInfo(string executable, params string[] arguments)
    {
        var start = new ProcessStartInfo(executable)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        return start;
    }
}
