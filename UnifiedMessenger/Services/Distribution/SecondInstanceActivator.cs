using System.Diagnostics;
using System.IO.Pipes;

namespace UnifiedMessenger.Services;

/// <summary>
/// Lets a second process ask the running instance to restore its window (tray-hide scenario).
/// </summary>
public static class SecondInstanceActivator
{
    internal const string PipeName = "UnifiedMessenger.Activate.v1";
    internal const string ShowCommand = "SHOW";

    private static CancellationTokenSource? _serverCts;

    public static void StartServer(Action onShowRequested)
    {
        ArgumentNullException.ThrowIfNull(onShowRequested);

        _serverCts?.Cancel();
        _serverCts?.Dispose();
        _serverCts = new CancellationTokenSource();
        var token = _serverCts.Token;

        _ = Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await using var server = new NamedPipeServerStream(
                        PipeName,
                        PipeDirection.In,
                        maxNumberOfServerInstances: 1,
                        PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous);

                    await server.WaitForConnectionAsync(token).ConfigureAwait(false);

                    using var reader = new StreamReader(server);
                    var command = await reader.ReadLineAsync(token).ConfigureAwait(false);
                    if (string.Equals(command, ShowCommand, StringComparison.Ordinal))
                    {
                        onShowRequested();
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Second-instance activator server error: {ex.Message}");
                    try
                    {
                        await Task.Delay(250, token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            }
        }, token);
    }

    public static void StopServer()
    {
        _serverCts?.Cancel();
        _serverCts?.Dispose();
        _serverCts = null;
    }

    public static bool TryActivateExistingInstance(int timeoutMs = 4000)
    {
        try
        {
            using var client = new NamedPipeClientStream(
                ".",
                PipeName,
                PipeDirection.Out,
                PipeOptions.None);

            client.Connect(timeoutMs);
            using var writer = new StreamWriter(client) { AutoFlush = true, NewLine = "\n" };
            writer.WriteLine(ShowCommand);
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Second-instance activation failed: {ex.Message}");
            return false;
        }
    }
}
