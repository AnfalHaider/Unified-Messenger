using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

/// <summary>
/// Regression tests for F-CRASH-01 (S1).
///
/// The seven durable stores used to share a single try block in FlushPersistentStateAsync, so the first
/// store that threw unwound past every remaining flush — silently discarding awaiting-overrides,
/// response-time history and KPI trends while shutdown still reported success. These tests pin the
/// isolation guarantee: one store failing must never stop another store from persisting.
/// </summary>
public class ApplicationLifecycleFlushTests
{
    private static (string, Func<CancellationToken, Task>) Ok(string name, List<string> log) =>
        (name, _ =>
        {
            log.Add(name);
            return Task.CompletedTask;
        });

    private static (string, Func<CancellationToken, Task>) Throws(string name, List<string> log) =>
        (name, _ =>
        {
            log.Add($"{name}:attempted");
            throw new IOException($"{name} could not be written");
        });

    [Fact]
    public async Task AllStoresFlush_WhenNoneFail()
    {
        var log = new List<string>();
        var stores = new[] { Ok("a", log), Ok("b", log), Ok("c", log) };

        var failed = await ApplicationLifecycleService.FlushStoresAsync(stores);

        Assert.Equal(["a", "b", "c"], log);
        Assert.Empty(failed);
    }

    [Fact]
    public async Task LaterStoresStillFlush_WhenTheFirstStoreThrows()
    {
        // This is the exact F-CRASH-01 scenario: the first store in the chain fails.
        // Before the fix, "b" and "c" never ran at all.
        var log = new List<string>();
        var stores = new[] { Throws("a", log), Ok("b", log), Ok("c", log) };

        var failed = await ApplicationLifecycleService.FlushStoresAsync(stores);

        Assert.Contains("b", log);
        Assert.Contains("c", log);
        Assert.Equal(["a"], failed);
    }

    [Fact]
    public async Task EveryHealthyStoreFlushes_WhenSeveralStoresThrow()
    {
        var log = new List<string>();
        var stores = new[]
        {
            Throws("first", log), Ok("second", log), Throws("third", log), Ok("fourth", log)
        };

        var failed = await ApplicationLifecycleService.FlushStoresAsync(stores);

        Assert.Contains("second", log);
        Assert.Contains("fourth", log);
        Assert.Equal(["first", "third"], failed);
    }

    [Fact]
    public async Task FailureNamesAreReported_SoTheUserCanBeWarnedTheirStateIsStale()
    {
        var log = new List<string>();
        var stores = new[] { Ok("MessageAnalytics", log), Throws("AwaitingOverrides", log) };

        var failed = await ApplicationLifecycleService.FlushStoresAsync(stores);

        // Losing AwaitingOverrides re-surfaces work the owner already handled, so it must be nameable.
        Assert.Equal(["AwaitingOverrides"], failed);
    }

    [Fact]
    public async Task CancellationOfOneStoreIsRecordedAsAFailure_AndDoesNotAbortTheRest()
    {
        // A cancelled flush is data that did not reach disk. It must be reported, not treated as success,
        // and it must not prevent the remaining stores from writing.
        var log = new List<string>();
        var stores = new (string, Func<CancellationToken, Task>)[]
        {
            ("cancelled", _ => throw new OperationCanceledException()),
            Ok("after", log)
        };

        var failed = await ApplicationLifecycleService.FlushStoresAsync(stores);

        Assert.Contains("after", log);
        Assert.Equal(["cancelled"], failed);
    }

    [Fact]
    public async Task EmptyStoreListIsHarmless()
    {
        var failed = await ApplicationLifecycleService.FlushStoresAsync([]);

        Assert.Empty(failed);
    }
}
