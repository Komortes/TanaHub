using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using TanaHub.Application.Services;

namespace TanaHub.Infrastructure.Notifications;

internal sealed partial class OsNotificationService : INotificationService
{
    public Task NotifyAsync(string title, string message, CancellationToken cancellationToken = default)
    {
        try
        {
            var safeTitle   = StripControls(title);
            var safeMessage = StripControls(message);

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                return RunMacOsAsync(safeTitle, safeMessage, cancellationToken);

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                return RunLinuxAsync(safeTitle, safeMessage, cancellationToken);
        }
        catch { /* notification failure is non-fatal */ }

        return Task.CompletedTask;
    }

    // Title and message are passed as argv items — never interpolated into the script source.
    private static Task RunMacOsAsync(string title, string message, CancellationToken ct)
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
        return RunAsync(psi, ct);
    }

    // notify-send takes title and body as separate argv — no shell involved.
    private static Task RunLinuxAsync(string title, string message, CancellationToken ct)
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
        return RunAsync(psi, ct);
    }

    private static async Task RunAsync(ProcessStartInfo psi, CancellationToken ct)
    {
        using var process = Process.Start(psi);
        if (process is null) return;
        await process.WaitForExitAsync(ct);
    }

    [GeneratedRegex(@"[\x00-\x1F\x7F]")]
    private static partial Regex ControlChars();

    private static string StripControls(string value)
        => ControlChars().Replace(value, string.Empty);
}
