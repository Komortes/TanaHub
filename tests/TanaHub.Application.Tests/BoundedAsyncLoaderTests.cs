using TanaHub.UI.ViewModels;

namespace TanaHub.Application.Tests;

public sealed class BoundedAsyncLoaderTests
{
    [Fact]
    public async Task RunAsync_PreservesOrderAndLimitsConcurrency()
    {
        var active = 0;
        var maxActive = 0;

        var results = await BoundedAsyncLoader.RunAsync(
            Enumerable.Range(0, 20).ToArray(),
            maxConcurrency: 3,
            async (value, cancellationToken) =>
            {
                var current = Interlocked.Increment(ref active);
                UpdateMaximum(ref maxActive, current);
                await Task.Delay(5, cancellationToken);
                Interlocked.Decrement(ref active);
                return value * 2;
            });

        Assert.Equal(Enumerable.Range(0, 20).Select(value => value * 2), results);
        Assert.InRange(maxActive, 1, 3);
    }

    private static void UpdateMaximum(ref int target, int value)
    {
        int current;
        do
        {
            current = Volatile.Read(ref target);
            if (current >= value) return;
        }
        while (Interlocked.CompareExchange(ref target, value, current) != current);
    }
}
