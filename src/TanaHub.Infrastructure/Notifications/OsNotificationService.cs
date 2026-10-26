using System.Diagnostics;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using TanaHub.Application.Services;

namespace TanaHub.Infrastructure.Notifications;

internal sealed partial class OsNotificationService : INotificationService
{
    private readonly Func<OSPlatform, bool> isPlatform;
    private readonly Func<ProcessStartInfo, CancellationToken, Task<int?>> runProcess;
    private readonly TimeSpan timeout;

    public OsNotificationService() : this(RuntimeInformation.IsOSPlatform, RunProcessAsync) { }

    internal OsNotificationService(Func<OSPlatform, bool> isPlatform, Func<ProcessStartInfo, CancellationToken, Task<int?>> runProcess, TimeSpan? timeout = null)
    {
        this.isPlatform = isPlatform;
        this.runProcess = runProcess;
        this.timeout = timeout ?? TimeSpan.FromSeconds(5);
    }

    public NotificationSupport Support => isPlatform(OSPlatform.OSX) || isPlatform(OSPlatform.Linux)
        ? new(true, null)
        : new(false, isPlatform(OSPlatform.Windows)
            ? "System reminders are not supported on Windows in this build."
            : "System reminders are not supported on this platform in this build.");

    public async Task<bool> NotifyAsync(string title, string message, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!Support.Supported) return false;
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(timeout);
        try
        {
            var safeTitle = StripControls(title);
            var safeMessage = StripControls(message);
            var start = isPlatform(OSPlatform.OSX) ? MacOsStart(safeTitle, safeMessage) : LinuxStart(safeTitle, safeMessage);
            return await runProcess(start, deadline.Token).WaitAsync(deadline.Token).ConfigureAwait(false) == 0;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch { cancellationToken.ThrowIfCancellationRequested(); return false; }
    }

    // Title and message are passed as argv items — never interpolated into the script source.
    private static ProcessStartInfo MacOsStart(string title, string message)
    {
        var psi = new ProcessStartInfo("osascript")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        psi.ArgumentList.Add("-e"); psi.ArgumentList.Add("on run argv");
        psi.ArgumentList.Add("-e"); psi.ArgumentList.Add("display notification (item 1 of argv) with title (item 2 of argv)");
        psi.ArgumentList.Add("-e"); psi.ArgumentList.Add("end run");
        psi.ArgumentList.Add("--");
        psi.ArgumentList.Add(message);
        psi.ArgumentList.Add(title);
        return psi;
    }

    // notify-send takes title and body as separate argv — no shell involved.
    private static ProcessStartInfo LinuxStart(string title, string message)
    {
        var psi = new ProcessStartInfo("notify-send")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        psi.ArgumentList.Add(title);
        psi.ArgumentList.Add(message);
        return psi;
    }

    internal static async Task<int?> RunProcessAsync(ProcessStartInfo start, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var process = new Process { StartInfo = start };
        if (!process.Start()) return null;
        using var registration = ct.Register(() => Kill(process));
        var stdout = process.StandardOutput.BaseStream.CopyToAsync(Stream.Null, ct);
        var stderr = process.StandardError.BaseStream.CopyToAsync(Stream.Null, ct);
        try
        {
            await Task.WhenAll(process.WaitForExitAsync(ct), stdout, stderr).ConfigureAwait(false);
            return process.ExitCode;
        }
        catch
        {
            Kill(process);
            try { await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(1)).ConfigureAwait(false); }
            catch (Exception exception) when (exception is TimeoutException or InvalidOperationException or Win32Exception) { }
            throw;
        }
    }

    private static void Kill(Process process)
    {
        try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
        catch (Exception exception) when (exception is InvalidOperationException or Win32Exception) { }
    }

    [GeneratedRegex(@"[\x00-\x1F\x7F]")]
    private static partial Regex ControlChars();

    private static string StripControls(string value)
        => ControlChars().Replace(value, string.Empty);
}
