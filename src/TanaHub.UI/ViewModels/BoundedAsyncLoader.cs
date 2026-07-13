namespace TanaHub.UI.ViewModels;

internal static class BoundedAsyncLoader
{
    public static async Task<TResult[]> RunAsync<TSource, TResult>(
        IReadOnlyList<TSource> source,
        int maxConcurrency,
        Func<TSource, CancellationToken, Task<TResult>> loadAsync,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(loadAsync);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxConcurrency);

        var results = new TResult[source.Count];
        await Parallel.ForEachAsync(
            Enumerable.Range(0, source.Count),
            new ParallelOptions
            {
                MaxDegreeOfParallelism = maxConcurrency,
                CancellationToken = cancellationToken
            },
            async (index, ct) =>
            {
                results[index] = await loadAsync(source[index], ct);
            });

        return results;
    }
}
