using System.Diagnostics;
using UnifiedMessenger.Services;

namespace UnifiedMessenger.Tests;

[Collection("SecondInstanceActivator")]
public sealed class SecondInstanceActivatorTests : IDisposable
{
    public SecondInstanceActivatorTests()
    {
        SecondInstanceActivator.StopServer();
        foreach (var process in Process.GetProcessesByName("UnifiedMessenger"))
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit(3000);
                }
            }
            catch
            {
                // Best effort so unit tests do not fight a live install.
            }
        }
    }

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
