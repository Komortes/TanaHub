using System.Text;
using Microsoft.Extensions.DependencyInjection;
using TanaHub.Application.Services;
using TanaHub.Infrastructure.Catalog;
using TanaHub.Infrastructure.DependencyInjection;

namespace TanaHub.Bridge;

internal static class Program
{
    public static async Task<int> Main()
    {
        Console.InputEncoding = Encoding.UTF8;
        Console.OutputEncoding = new UTF8Encoding(false);
        using var cancellation = new CancellationTokenSource();
        Console.CancelKeyPress += (_, args) =>
        {
            args.Cancel = true;
            cancellation.Cancel();
        };
        try
        {
            // Uses the same SQLite, cache and absolute TANAHUB_DATA_DIRECTORY override as the Avalonia app.
            var services = new ServiceCollection();
            services.AddTanaHubInfrastructure();
            await using var provider = services.BuildServiceProvider();
            var settings = await provider.GetRequiredService<IAppSettingsService>().GetAsync(cancellation.Token);
            if (settings.IsSuccess)
            {
                var source = provider.GetRequiredService<ICatalogSourceSelector>();
                source.SetSource(settings.Value!.PreferredSyncSource);
                source.SetOfflineCacheEnabled(settings.Value.OfflineCacheEnabled);
                provider.GetRequiredService<IMediaRelationsService>().SetOfflineCacheEnabled(settings.Value.OfflineCacheEnabled);
            }
            var cache = provider.GetRequiredService<OfflineCatalogCache>();
            var handler = new BridgeHandler(
                provider.GetRequiredService<IMediaCatalogService>(),
                provider.GetRequiredService<IUserLibraryService>(),
                id => cache.TryGet(id, out var item) ? item : null,
                provider.GetRequiredService<InMemoryMediaCatalogService>(),
                Console.Error,
                provider,
                async (items, ct) =>
                {
                    cache.PutRange(items);
                    await cache.FlushAsync(ct);
                });
            await BridgeProtocol.RunAsync(handler, Console.In, Console.Out, cancellation.Token);
            return 0;
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            return 0;
        }
        catch (Exception exception)
        {
            await Console.Error.WriteLineAsync($"TanaHub bridge could not start: {exception.GetType().Name}.");
            return 1;
        }
    }
}
