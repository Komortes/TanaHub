namespace TanaHub.Bridge;

public static class BridgeProtocol
{
    /// <summary>One request and one response per line; only JSON responses belong on stdout.</summary>
    public static async Task RunAsync(
        BridgeHandler handler,
        TextReader input,
        TextWriter output,
        CancellationToken cancellationToken = default)
    {
        while (await input.ReadLineAsync(cancellationToken) is { } line)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var response = await handler.HandleAsync(line, cancellationToken);
            await output.WriteLineAsync(response.AsMemory(), cancellationToken);
            await output.FlushAsync(cancellationToken);
        }
    }
}
