using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

[Collection("SecondInstanceActivator")]
public sealed class SecondInstanceActivatorTests : IDisposable
{
    public SecondInstanceActivatorTests() => SecondInstanceActivator.StopServer();

    public void Dispose() => SecondInstanceActivator.StopServer();

    [Fact]
    public void TryActivateExistingInstance_ReturnsFalse_WhenNoServerListening()
    {
        var activated = SecondInstanceActivator.TryActivateExistingInstance(timeoutMs: 500);

        Assert.False(activated);
    }

    /// <summary>
    /// Generous on purpose. These are hang-guards, not assertions about speed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The previous values were 3 seconds each, and they failed a release. Commit <c>859e989</c> ran twice
    /// within the same minute — once for <c>main</c> and once for the <c>v5.0.1</c> tag — and the identical
    /// code passed on one runner and failed on the other. Same tests, same sha, different outcome: the only
    /// variable was how loaded the machine was.
    /// </para>
    /// <para>
    /// A named-pipe round trip is normally single-digit milliseconds, so raising the cap costs nothing on a
    /// passing run and only changes how long a genuinely wedged one takes to admit it. A timeout tight
    /// enough to be tripped by CPU contention is not measuring the thing it names — it is measuring the
    /// runner.
    /// </para>
    /// </remarks>
    private static readonly TimeSpan PipeHangGuard = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task TryActivateExistingInstance_RestoresWindow_WhenServerListening()
    {
        var signal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        SecondInstanceActivator.StartServer(() => signal.TrySetResult());

        try
        {
            var activated = SecondInstanceActivator.TryActivateExistingInstance(
                timeoutMs: (int)PipeHangGuard.TotalMilliseconds);

            Assert.True(activated);
            await signal.Task.WaitAsync(PipeHangGuard);
        }
        finally
        {
            SecondInstanceActivator.StopServer();
        }
    }
}

[CollectionDefinition("SecondInstanceActivator", DisableParallelization = true)]
public sealed class SecondInstanceActivatorCollection;
