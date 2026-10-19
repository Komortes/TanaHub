using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace TanaHub.Infrastructure.Catalog;

/// <summary>
/// macOS SslStream cannot negotiate the TLS 1.3 required by MangaDex.
/// Use the system curl transport only for this provider's public manga metadata GETs.
/// </summary>
internal sealed class MangaDexCurlHandler(
    Func<ProcessStartInfo, CancellationToken, Task<MangaDexCurlHandler.ProcessResult>>? runProcess = null)
    : HttpMessageHandler
{
    internal const int MaxResponseBytes = 10 * 1024 * 1024;
    private readonly Func<ProcessStartInfo, CancellationToken, Task<ProcessResult>> run = runProcess ?? RunProcessAsync;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!IsAllowed(request))
            throw new HttpRequestException("Only public MangaDex metadata requests are supported by this transport.");

        var start = new ProcessStartInfo("/usr/bin/curl")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        // --disable must be first: a user's .curlrc must not add credentials, redirects or other behavior.
        foreach (var argument in new[]
                 {
                     "--disable", "--silent", "--show-error", "--globoff", "--proto", "=https",
                     "--connect-timeout", "5", "--max-time", "10", "--max-filesize",
                     MaxResponseBytes.ToString(CultureInfo.InvariantCulture), "--user-agent", "TanaHub/0.1",
                     "--write-out", "\n%{http_code}", "--", request.RequestUri!.AbsoluteUri
                 })
            start.ArgumentList.Add(argument);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(12));
        ProcessResult result;
        try
        {
            result = await run(start, timeout.Token).ConfigureAwait(false);
        }
        catch (Win32Exception)
        {
            throw new HttpRequestException("The system MangaDex transport could not be started.");
        }
        cancellationToken.ThrowIfCancellationRequested();
        if (result.ExitCode != 0)
            throw new HttpRequestException("The MangaDex connection could not be completed.");
        if (result.Output.Length < 4 || result.Output.Length > MaxResponseBytes + 4 || result.Output[^4] != '\n' ||
            !int.TryParse(Encoding.ASCII.GetString(result.Output.AsSpan(result.Output.Length - 3)),
                NumberStyles.None, CultureInfo.InvariantCulture, out var status) || status is < 100 or > 599)
            throw new HttpRequestException("MangaDex returned an invalid transport response.");

        // The final four bytes are curl's status trailer, never part of the API's JSON body.
        return new HttpResponseMessage((HttpStatusCode)status)
        {
            RequestMessage = request,
            Content = new ByteArrayContent(result.Output, 0, result.Output.Length - 4)
            {
                Headers = { ContentType = new MediaTypeHeaderValue("application/json") }
            }
        };
    }

    private static bool IsAllowed(HttpRequestMessage request)
    {
        var uri = request.RequestUri;
        if (request.Method != HttpMethod.Get || request.Content is not null || request.Headers.Authorization is not null ||
            request.Headers.Contains("Cookie") || uri is not { IsAbsoluteUri: true } ||
            uri.Scheme != "https" || !uri.Host.Equals("api.mangadex.org", StringComparison.OrdinalIgnoreCase) ||
            uri.Port != 443 || uri.UserInfo.Length > 0 || uri.Fragment.Length > 0)
            return false;

        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments.Length is >= 1 and <= 3 && segments[0] == "manga" &&
               (segments.Length == 1 || Guid.TryParse(segments[1], out _)) &&
               (segments.Length < 3 || segments[2] == "relation");
    }

    internal static async Task<ProcessResult> RunProcessAsync(ProcessStartInfo start, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var process = new Process { StartInfo = start };
        if (!process.Start()) throw new HttpRequestException("The system MangaDex transport could not be started.");
        using var registration = cancellationToken.Register(() => Kill(process));
        // Drain errors without retaining or exposing potentially sensitive environment diagnostics.
        var stderr = process.StandardError.BaseStream.CopyToAsync(Stream.Null, cancellationToken);
        try
        {
            using var output = new MemoryStream();
            var buffer = new byte[8192];
            int read;
            while ((read = await process.StandardOutput.BaseStream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
            {
                if (output.Length + read > MaxResponseBytes + 4)
                    throw new HttpRequestException("The MangaDex response is too large.");
                output.Write(buffer, 0, read);
            }
            await Task.WhenAll(process.WaitForExitAsync(cancellationToken), stderr).ConfigureAwait(false);
            return new ProcessResult(process.ExitCode, output.ToArray());
        }
        catch
        {
            Kill(process);
            try { await stderr.WaitAsync(TimeSpan.FromSeconds(1)).ConfigureAwait(false); }
            catch (Exception exception) when (exception is IOException or OperationCanceledException or TimeoutException) { }
            throw;
        }
    }

    private static void Kill(Process process)
    {
        try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
        catch (Exception exception) when (exception is InvalidOperationException or Win32Exception) { }
    }

    internal sealed record ProcessResult(int ExitCode, byte[] Output);
}
