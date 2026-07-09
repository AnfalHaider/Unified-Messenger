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

    [Fact]
    public async Task TryActivateExistingInstance_RestoresWindow_WhenServerListening()
    {
        var signal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        SecondInstanceActivator.StartServer(() => signal.TrySetResult());

        try
        {
            var activated = SecondInstanceActivator.TryActivateExistingInstance(timeoutMs: 3000);

            Assert.True(activated);
            await signal.Task.WaitAsync(TimeSpan.FromSeconds(3));
        }
        finally
        {
            SecondInstanceActivator.StopServer();
        }
    }
}

[CollectionDefinition("SecondInstanceActivator", DisableParallelization = true)]
public sealed class SecondInstanceActivatorCollection;
